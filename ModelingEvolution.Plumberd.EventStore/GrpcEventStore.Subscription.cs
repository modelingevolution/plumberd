using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using FxResources.Microsoft.Extensions.Logging;
using LiquidProjections.Abstractions;
using Microsoft.Extensions.Logging;
using Modellution.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;


namespace ModelingEvolution.Plumberd.EventStore
{
    public partial class GrpcEventStore : IEventStore
    {
        interface INativeSubscription : ISubscription
        {
            Task Subscribe();
        }

        private class GrpcPersistentSubscription : INativeSubscription
        {
            private readonly GrpcEventStore _parent;

            private static readonly Microsoft.Extensions.Logging.ILogger _log =
                LogFactory.GetLogger<GrpcPersistentSubscription>();
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly EventStoreClient _connection;
            private long? _processedEvent;
            private PersistentSubscriptionSettings settings;
            private EventStorePersistentSubscriptionsClient _subscriptionClient;
            public GrpcPersistentSubscription(GrpcEventStore parent,

                in bool fromBeginning,
                EventHandler onEvent,
                string streamName,
                IProcessingContextFactory processingContextFactory)
            {
                _parent = parent;
                this._fromBeginning = fromBeginning;
                this._onEvent = onEvent;
                this._streamName = streamName;
                this._processingContextFactory = processingContextFactory;
                this._connection = _parent._connection;
            }

            public async Task Subscribe()
            {
               
                try
                {
                    _log.LogInformation("Connecting to persistent subscription {subscriptionName}.", _streamName);
                    var settings = new PersistentSubscriptionSettings();
                    _subscriptionClient =
                         new EventStorePersistentSubscriptionsClient(new EventStoreClientSettings());
                    
                    await    _subscriptionClient.SubscribeAsync(
                        _streamName,
                        Environment.MachineName,OnEventAppeared,OnSubscriptionDropped, _parent._credentials);
                  await _subscriptionClient.SubscribeAsync(_streamName,Environment.MachineName,OnEventAppeared,OnSubscriptionDropped);
                }
                catch (ArgumentException)
                {
                    // expected ex.Message = "Subscription not found";
                    //var settings = await CreatePersistentSubscriptionSettings();

                    _log.LogInformation("Creating persistent subscription {subscriptionName} from beginning: {isFromBeginning}", _streamName, _fromBeginning);
                    await _parent._subscriptionsClient.CreateAsync(_streamName,
                        Environment.MachineName,settings,
                        _parent._credentials);

                    _log.LogInformation("Connecting to persistent subscription {subscriptionName}.", _streamName);
                    await _parent.ConnectToPersistentSubscriptionAsync(_streamName,
                        Environment.MachineName,
                        OnEventAppeared, userCredentials: _parent._credentials,
                        subscriptionDropped: OnSubscriptionDropped);
                }
            }

            private void OnSubscriptionDropped(PersistentSubscription arg1, SubscriptionDroppedReason arg2, Exception arg3)
            {
                _log.LogWarning(arg3, "Subscription dropped! {ProcessingMode} {ProcessingUnitType} {Reason}", _processingContextFactory.Config.ProcessingMode, _processingContextFactory.Config.Type.Name);
                if (arg2 == SubscriptionDroppedReason.SubscriberError)
                {
                    _log.LogError(arg3, "Exception in event-handler {streamName}. We won't resubscribe. Please reset the server.", _streamName);
                    return;
                }
                Task.Run(() => TrySubscribe(arg2));
            }

            private async Task TrySubscribe(SubscriptionDroppedReason arg2)
            {
                while (true)
                    try
                    {
                        _log.LogInformation("Trying to re-subscribe to persistant subscription '{steamName}'", _streamName);
                        await Subscribe();
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(5000);
                    }
            }

            private Task OnEventAppeared(PersistentSubscription arg1, ResolvedEvent arg2, int? arg3, CancellationToken arg4)
            {
                throw new NotImplementedException();
            }
            private void OnSubscriptionDropped(StreamSubscription arg1, SubscriptionDroppedReason arg2, Exception arg3)
            {
                _log.LogInformation("Subscription dropped {reason} {exception}", arg2, arg3?.Message ?? "NoException");
                arg1.Dispose();
                if (arg2 == SubscriptionDroppedReason.SubscriberError)
                    Task.Run(TrySubscribe);
            }

            private async Task<PersistentSubscriptionSettings> CreatePersistentSubscriptionSettings()
            { 
                StreamPosition position = StreamPosition.End;
                if (_fromBeginning)
                {
                    position = StreamPosition.Start;
                }
                else
                {
                    var lastEventNumber = await ReadLastEventNumber();

                    if (lastEventNumber.HasValue)
                        position = StreamPosition.FromInt64(lastEventNumber.Value);
                }
                var settings = new PersistentSubscriptionSettings(false, position);
                return settings;
            }

            private async Task<long?> ReadLastEventNumber()
            {
                var lastSlice = _connection.ReadStreamAsync(Direction.Backwards, _streamName, StreamPosition.End, 1, null, false,
                    _parent._credentials);
                long? lastEventNumber = await lastSlice.AnyAsync() ? lastSlice.Current.Event.EventNumber.ToInt64() : (long?)null;
                return lastEventNumber;
            }

            private  Task OnEventAppeared(ResolvedEvent e)
            {
                using (var context = _processingContextFactory.Create())
                {
                    using (StaticProcessingContext.CreateScope(context)) // should be moved to decorator.
                    {
                        var (m, ev) = _parent.ReadEvent(e, context);

                        _log.LogInformation("Reading persistently {eventNumber} {eventType} from {streamName}", e.OriginalEventNumber, ev.GetType().Name, _streamName);

                        if (e.OriginalEventNumber.ToInt64() > _processedEvent.Value || _processedEvent == null)
                        {
                            _onEvent(context, m, ev);
                            _processedEvent = e.OriginalEventNumber.ToInt64();
                        }
                        else _log.LogInformation("Ignoring already delivered event: {eventType} from {streamName}", ev.GetType().Name, _streamName);

                    }
                }
                return null;
            }

            public void Dispose()
            {
                _connection.Dispose();
            }



            private async Task TrySubscribe()
            {
                while (true)
                    try
                    {
                        _log.LogInformation("Trying to re-subscribe to persistant subscription '{steamName}'", _streamName);
                        await Subscribe();
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(5000);
                    }

            }

          
        }

        private async Task ConnectToPersistentSubscriptionAsync(string streamName, string machineName, Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> onEventAppeared, UserCredentials userCredentials, Action<PersistentSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped)
        {
            PersistentSubscriptionSettings settings = new PersistentSubscriptionSettings(); 
            
           await _subscriptionsClient.SubscribeAsync(streamName, Environment.MachineName, onEventAppeared, subscriptionDropped,
                _credentials);

        }

  

        private class GrpcContinuesSubscription : INativeSubscription
        {
            private readonly GrpcEventStore _parent;
            private readonly static ILogger _log = LogFactory.GetLogger<GrpcContinuesSubscription>();
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
         //   private readonly IEventStoreConnection _connection;
            private long? _streamPosition = null;
            //private EventStoreStreamCatchUpSubscription _subscriptionCatchUp;
            //private EventStoreSubscription _subscription;

            public GrpcContinuesSubscription(GrpcEventStore parent,
                in bool fromBeginning,
                EventHandler onEvent,
                string streamName,

                IProcessingContextFactory processingContextFactory)
            {
                _parent = parent;
                //_connection = _parent._connection;
                _fromBeginning = fromBeginning;
                _onEvent = onEvent;
                _streamName = streamName;
                _processingContextFactory = processingContextFactory;
            }

            public async  Task Subscribe()
            {
                if (_streamPosition == null)
                    _log.LogInformation("Subscribing from stream {streamName} from beginning: {isFromBeginning}",
                        _streamName, _fromBeginning);
                else
                    _log.LogInformation("Subscribing from stream {streamName} from: {streamPosition}", _streamName, _streamPosition);

                if (_fromBeginning || _streamPosition > 0)
                {
                    await _parent._connection.SubscribeToStreamAsync(_streamName,OnEventAppeared, false, OnSubscriptionDropped);

                }
                else
                {
                     await _parent._connection.SubscribeToStreamAsync(_streamName,
                        OnEventAppeared, false,
                        subscriptionDropped: OnSubscriptionDropped);
                }
            }

            private async Task OnEventAppeared(StreamSubscription arg1, ResolvedEvent arg2, CancellationToken arg3)
            {
                await OnEventAppeared(arg2);
            }


            private void OnSubscriptionDropped(StreamSubscription arg1, SubscriptionDroppedReason arg2, Exception arg3)
            {
                _log.LogInformation("Subscription dropped {reason} {exception}", arg2, arg3?.Message ?? "NoException");
                arg1.Dispose();
                if (arg2 != SubscriptionDroppedReason.SubscriberError)
                    Task.Run(TrySubscribe);
            }

            private async Task OnEventAppeared(ResolvedEvent e)
            {
               
                _log.LogInformation("Event appeared: {steamName} {eventType}", e.Event.EventStreamId, e.Event.EventType);
                using (var context = _processingContextFactory.Create())
                {
                    using (StaticProcessingContext.CreateScope(context)) // should be moved to decorator.
                    {
                        var (m, ev) = _parent.ReadEvent(e, context);

                        _log.LogInformation("Reading {eventNumber} {eventType} from {streamName}", e.Event.EventNumber, ev.GetType().Name, _streamName);
                        if (e.OriginalEventNumber.ToInt64() > _streamPosition.Value || _streamPosition == null)
                        {
                           await _onEvent(context, m, ev);
                            _streamPosition = (long)e.OriginalPosition.Value.CommitPosition;
                        }
                        else
                        {
                            _log.LogInformation("Ignoring already delivered event: {eventType} from {streamName}", ev.GetType().Name, _streamName);
                        }


                    }
                }
            }

            private async Task TrySubscribe()
            {
                //TODO: Should move this to Polly.
                while (true)
                    try
                    {
                        _log.LogInformation("Trying to re-subscribe to steam '{steamName}'", _streamName);
                        await Subscribe();
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(5000);
                    }
            }

          

            public void Dispose()
            {
                //_subscriptionCatchUp?.Stop(TimeSpan.FromSeconds(10));
                //_subscription?.Dispose();
            }
        }
    }
}

