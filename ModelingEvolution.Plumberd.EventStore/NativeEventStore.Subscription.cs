using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ILogger = Serilog.ILogger;

namespace ModelingEvolution.Plumberd.EventStore
{
    public partial class NativeEventStore : IEventStore
    {
        interface ISubscription
        {
            Task Subscribe();
        }

        private class PersistentSubscription : ISubscription
        {
            private readonly NativeEventStore _parent;
            private readonly ILogger _log;
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly IEventStoreConnection _connection;
            private long? _processedEvent;
            public PersistentSubscription(NativeEventStore parent, 
                ILogger log, 
                in bool fromBeginning, 
                EventHandler onEvent, 
                string streamName, 
                IProcessingContextFactory processingContextFactory)
            {
                _parent = parent;
                _log = log;
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
                    _log.Information("Connecting to persistent subscription {subscriptionName}.", _streamName);
                    await _connection.ConnectToPersistentSubscriptionAsync(_streamName,
                        Environment.MachineName,
                        OnEventAppeared,
                        userCredentials: _parent._credentials,
                        subscriptionDropped: OnSubscriptionDropped);
                }
                catch (ArgumentException)
                {
                    // expected ex.Message = "Subscription not found";
                    var settings = await CreatePersistentSubscriptionSettings();
                    
                    _log.Information("Creating persistent subscription {subscriptionName} from beginning: {isFromBeginning}", _streamName, _fromBeginning);
                    await _connection.CreatePersistentSubscriptionAsync(_streamName,
                        Environment.MachineName,
                        settings.Build(),
                        _parent._credentials);

                    _log.Information("Connecting to persistent subscription {subscriptionName}.", _streamName);
                    await _connection.ConnectToPersistentSubscriptionAsync(_streamName, 
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

                        _log.Information("Reading persistently {eventNumber} {eventType} from {streamName}", e.OriginalEventNumber, ev.GetType().Name, _streamName);

                        if (e.OriginalEventNumber > _processedEvent || _processedEvent == null)
                        {
                            await _onEvent(context, m, ev);
                            _processedEvent = e.OriginalEventNumber;
                        }
                        else _log.Information("Ignoring already delivered event: {eventType} from {streamName}", ev.GetType().Name, _streamName);

                    }
                }
            }

            private void OnSubscriptionDropped(EventStorePersistentSubscriptionBase s, SubscriptionDropReason r, Exception e)
            {
                _log.Warning("Subscription dropped! {ProcessingMode} {ProcessingUnitType}", _processingContextFactory.Config.ProcessingMode, _processingContextFactory.Config.Type.Name);
                Task.Run(TrySubscribe);
            }

            private async Task TrySubscribe()
            {
                while (true)
                    try
                    {
                        _log.Information("Trying to re-subscribe to persistant subscription '{steamName}'", _streamName);
                        await Subscribe();
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(5000);
                    }

            }
        }
        private class ContinuesSubscription : ISubscription
        {
            private readonly NativeEventStore _parent;
            private readonly ILogger _log;
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly IEventStoreConnection _connection;
            private long? _streamPosition = null;
            public ContinuesSubscription(NativeEventStore parent,
                ILogger log,
                in bool fromBeginning,
                EventHandler onEvent,
                string streamName,

                IProcessingContextFactory processingContextFactory)
            {
                _parent = parent;
                _connection = _parent._connection;
                _log = log;
                _fromBeginning = fromBeginning;
                _onEvent = onEvent;
                _streamName = streamName;
                _processingContextFactory = processingContextFactory;
            }

            public async Task Subscribe()
            {
                if (_streamPosition == null)
                    _log.Information("Subscribing from stream {streamName} from beginning: {isFromBeginning}",
                        _streamName, _fromBeginning);
                else 
                    _log.Information("Subscribing from stream {streamName} from: {streamPosition}", _streamName, _streamPosition);

                if (_fromBeginning || _streamPosition > 0)
                {
                    _connection.SubscribeToStreamFrom(_streamName, 
                        _streamPosition,
                        CatchUpSubscriptionSettings.Default, 
                        OnEventAppeared, 
                        subscriptionDropped: OnSubscriptionDropped);

                }
                else
                {
                    await _connection.SubscribeToStreamAsync(_streamName, 
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
                using (var context = _processingContextFactory.Create())
                {
                    using (StaticProcessingContext.CreateScope(context)) // should be moved to decorator.
                    {
                        var (m, ev) = _parent.ReadEvent(e, context);

                        //_log.Information("Reading {eventNumber} {eventType} from {streamName}", e.Event.EventNumber,ev.GetType().Name, _streamName);
                        if (e.OriginalEventNumber > _streamPosition || _streamPosition == null)
                        {
                            await _onEvent(context, m, (IEvent) ev);
                            _streamPosition = e.OriginalEventNumber;
                        }
                        else
                        {
                            _log.Information("Ignoring already delivered event: {eventType} from {streamName}", ev.GetType().Name, _streamName);
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
                    _log.Information("Trying to re-subscribe to steam '{steamName}'", _streamName);
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
                eventStoreCatchUpSubscription.Stop();
                Task.Run(TrySubscribe);
            }
            private void OnSubscriptionDropped(EventStoreSubscription eventStoreCatchUpSubscription, SubscriptionDropReason subscriptionDropReason, Exception arg3)
            {
                eventStoreCatchUpSubscription.Dispose();
                Task.Run(TrySubscribe);
            }
        }
    }
}
