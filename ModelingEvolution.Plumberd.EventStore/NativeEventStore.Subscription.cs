using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using FxResources.Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Modellution.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ModelingEvolution.Plumberd.EventStore
{
    public partial class NativeEventStore : IEventStore
    {
        interface INativeSubscription : ISubscription
        {
            Task Subscribe();
        }

        private class PersistentSubscription : INativeSubscription
        {
            private readonly NativeEventStore _parent;

            private static readonly Microsoft.Extensions.Logging.ILogger _log =
                LogFactory.GetLogger<PersistentSubscription>();
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly IEventStoreConnection _connection;
            private long? _processedEvent;
            private EventStorePersistentSubscriptionBase _subscription;

            public PersistentSubscription(NativeEventStore parent, 
                
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
                    this._subscription = await _connection.ConnectToPersistentSubscriptionAsync(_streamName,
                        Environment.MachineName,
                        OnEventAppeared,
                        userCredentials: _parent._credentials,
                        subscriptionDropped: OnSubscriptionDropped);
                }
                catch (ArgumentException)
                {
                    // expected ex.Message = "Subscription not found";
                    var settings = await CreatePersistentSubscriptionSettings();
                    
                    _log.LogInformation("Creating persistent subscription {subscriptionName} from beginning: {isFromBeginning}", _streamName, _fromBeginning);
                    await _connection.CreatePersistentSubscriptionAsync(_streamName,
                        Environment.MachineName,
                        settings.Build(),
                        _parent._credentials);

                    _log.LogInformation("Connecting to persistent subscription {subscriptionName}.", _streamName);
                    this._subscription = await _connection.ConnectToPersistentSubscriptionAsync(_streamName, 
                        Environment.MachineName,
                        OnEventAppeared, userCredentials: _parent._credentials,
                        subscriptionDropped: OnSubscriptionDropped);
                }
            }

            private async Task<PersistentSubscriptionSettingsBuilder> CreatePersistentSubscriptionSettings()
            {
                var settings = PersistentSubscriptionSettings.Create()
                    .ResolveLinkTos();

                if (_fromBeginning)
                    settings = settings.StartFromBeginning();
                else
                {
                    var lastEventNumber = await ReadLastEventNumber();

                    if (lastEventNumber.HasValue)
                        settings = settings.StartFrom(lastEventNumber.Value);
                }

                return settings;
            }

            private async Task<long?> ReadLastEventNumber()
            {
                var lastSlice = await _connection.ReadStreamEventsBackwardAsync(_streamName, StreamPosition.End, 1, false,
                    _parent._credentials);
                long? lastEventNumber = lastSlice.Events.Any() ? lastSlice.Events[0].OriginalEventNumber : (long?) null;
                return lastEventNumber;
            }

            private async Task OnEventAppeared(EventStorePersistentSubscriptionBase s, ResolvedEvent e)
            {
                using (var context = _processingContextFactory.Create())
                {
                    using (StaticProcessingContext.CreateScope(context)) // should be moved to decorator.
                    {
                        var (m, ev) = _parent.ReadEvent(e, context);

                        _log.LogInformation("Reading persistently {eventNumber} {eventType} from {streamName}", e.OriginalEventNumber, ev.GetType().Name, _streamName);

                        if (e.OriginalEventNumber > _processedEvent || _processedEvent == null)
                        {
                            await _onEvent(context, m, ev);
                            _processedEvent = e.OriginalEventNumber;
                        }
                        else _log.LogInformation("Ignoring already delivered event: {eventType} from {streamName}", ev.GetType().Name, _streamName);

                    }
                }
            }

            private void OnSubscriptionDropped(EventStorePersistentSubscriptionBase s, SubscriptionDropReason r, Exception e)
            {
                _log.LogWarning(e,"Subscription dropped! {ProcessingMode} {ProcessingUnitType} {Reason}", _processingContextFactory.Config.ProcessingMode, _processingContextFactory.Config.Type.Name, r);
                if (r == SubscriptionDropReason.EventHandlerException)
                {
                    _log.LogError(e, "Exception in event-handler {streamName}. We won't resubscribe. Please reset the server.", _streamName);
                    return;
                }
                Task.Run(() => TrySubscribe(r));
            }

            private async Task TrySubscribe(SubscriptionDropReason reason)
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

            public void Dispose()
            {
                _subscription.Stop(TimeSpan.FromSeconds(5));
            }
        }
        private class ContinuesSubscription : INativeSubscription
        {
            private readonly NativeEventStore _parent;
            private readonly static ILogger _log = LogFactory.GetLogger<ContinuesSubscription>();
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly IEventStoreConnection _connection;
            private long? _streamPosition = null;
            private EventStoreStreamCatchUpSubscription _subscriptionCatchUp;
            private EventStoreSubscription _subscription;
            
            public ContinuesSubscription(NativeEventStore parent,
                in bool fromBeginning,
                EventHandler onEvent,
                string streamName,
                IProcessingContextFactory processingContextFactory)
            {
                _parent = parent;
                _connection = _parent._connection;
                _fromBeginning = fromBeginning;
                _onEvent = onEvent;
                _streamName = streamName;
                _processingContextFactory = processingContextFactory;
            }

            public async Task Subscribe()
            {
                if (_streamPosition == null)
                    _log.LogInformation("Subscribing from stream {streamName} from beginning: {isFromBeginning}",
                        _streamName, _fromBeginning);
                else 
                    _log.LogInformation("Subscribing from stream {streamName} from: {streamPosition}", _streamName, _streamPosition);

                if (_fromBeginning || _streamPosition > 0)
                {
                    this._subscriptionCatchUp = _connection.SubscribeToStreamFrom(_streamName, 
                        _streamPosition,
                        CatchUpSubscriptionSettings.Default, 
                        OnEventAppeared, 
                        liveProcessingStarted: (s) =>
                        {
                            var live = _processingContextFactory?.Config?.OnLive;
                            if (live != null)
                            {
                                live();
                                _log.LogInformation("{streamName} is live", _streamName);
                            }
                            
                        },
                        subscriptionDropped: OnSubscriptionDropped);

                }
                else
                {
                    this._subscription = await _connection.SubscribeToStreamAsync(_streamName, 
                        true, 
                        OnEventAppeared, 
                        subscriptionDropped: OnSubscriptionDropped);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private async Task OnEventAppeared(EventStoreSubscription s, ResolvedEvent e)
            {
                await OnEventAppeared(e);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private async Task OnEventAppeared(EventStoreCatchUpSubscription s, ResolvedEvent e)
            {
                await OnEventAppeared(e);
            }
            private async Task OnEventAppeared(ResolvedEvent e)
            {
                _log.LogInformation("Event appeared: {steamName} {eventType}", e.Event.EventStreamId, e.Event.EventType);
                using (var context = _processingContextFactory.Create())
                {
                    using (StaticProcessingContext.CreateScope(context)) // should be moved to decorator.
                    {
                        var (m, ev) = _parent.ReadEvent(e, context);

                        //_log.LogInformation("Reading {eventNumber} {eventType} from {streamName}", e.Event.EventNumber,ev.GetType().Name, _streamName);
                        if (e.OriginalEventNumber > _streamPosition || _streamPosition == null)
                        {
                            await _onEvent(context, m, ev);
                            _streamPosition = e.OriginalEventNumber;
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
                while(true)
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

            private void OnSubscriptionDropped(EventStoreCatchUpSubscription eventStoreCatchUpSubscription, SubscriptionDropReason subscriptionDropReason, Exception arg3)
            {
                _log.LogInformation("Subscription dropped {reason} {exception}", subscriptionDropReason, arg3?.Message ?? "NoException");
                eventStoreCatchUpSubscription.Stop();
                if(subscriptionDropReason != SubscriptionDropReason.UserInitiated)
                    Task.Run(TrySubscribe);
            }
            private void OnSubscriptionDropped(EventStoreSubscription eventStoreCatchUpSubscription, SubscriptionDropReason subscriptionDropReason, Exception arg3)
            {
                _log.LogInformation("Subscription dropped {reason} {exception}", subscriptionDropReason, arg3?.Message ?? "NoException");
                eventStoreCatchUpSubscription.Dispose();
                if (subscriptionDropReason != SubscriptionDropReason.UserInitiated)
                    Task.Run(TrySubscribe);
            }

            public void Dispose()
            {
                _subscriptionCatchUp?.Stop(TimeSpan.FromSeconds(10));
                _subscription?.Dispose();
            }
        }
    }
}
