using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;

using FxResources.Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
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

            private readonly Microsoft.Extensions.Logging.ILogger<PersistentSubscription> _log;
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly EventStoreClient _connection;
            private StreamPosition? _processedEvent;
            private global::EventStore.Client.PersistentSubscription _subscription;

            public PersistentSubscription(NativeEventStore parent, 
                
                in bool fromBeginning, 
                EventHandler onEvent, 
                string streamName, 
                IProcessingContextFactory processingContextFactory, ILogger<PersistentSubscription> log)
            {
                _parent = parent;
                this._fromBeginning = fromBeginning;
                this._onEvent = onEvent;
                this._streamName = streamName;
                this._processingContextFactory = processingContextFactory;
                _log = log;
                this._connection = _parent._connection;
            }

            public async Task Subscribe()
            {
                var group = _processingContextFactory.Config.Type.Name;
                try
                {
                    var position = _fromBeginning ? StreamPosition.Start : StreamPosition.End;
                    PersistentSubscriptionSettings s = new PersistentSubscriptionSettings(true,position);
                    
                    
                    var subs = (await _parent.PersistentSubscriptions.ListAllAsync()).ToArray();

                    if (subs.All(x => x.GroupName != group))
                        await _parent.PersistentSubscriptions.CreateToStreamAsync(_streamName, group, s);
                    
                    _log.LogInformation("Connecting to persistent subscription {subscriptionName}.", _streamName);
                    this._subscription = await _parent.PersistentSubscriptions.SubscribeToStreamAsync(_streamName,
                        group /*Environment.MachineName*/,
                        OnEventAppeared,
                        userCredentials: _parent._credentials,
                        subscriptionDropped: OnSubscriptionDropped);
                }
                catch (ArgumentException)
                {
                    await Task.Delay(2000);
                    _log.LogInformation("Connecting to persistent subscription {subscriptionName} attempt 2.", _streamName);

                    this._subscription = await _parent.PersistentSubscriptions.SubscribeToStreamAsync(_streamName,
                        group /*Environment.MachineName*/,
                        OnEventAppeared,
                        userCredentials: _parent._credentials,
                        subscriptionDropped: OnSubscriptionDropped);
                }
            }

            

           

            private async Task OnEventAppeared(global::EventStore.Client.PersistentSubscription s, ResolvedEvent e, int? i, CancellationToken t)
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

            private void OnSubscriptionDropped(global::EventStore.Client.PersistentSubscription s, SubscriptionDroppedReason r, Exception? e)
            {
                _log.LogWarning(e,"Subscription dropped! {ProcessingMode} {ProcessingUnitType} {Reason}", _processingContextFactory.Config.ProcessingMode, _processingContextFactory.Config.Type.Name, r);
                if (r == SubscriptionDroppedReason.SubscriberError)
                {
                    _log.LogError(e, "Exception in event-handler {streamName}. We won't resubscribe. Please reset the server.", _streamName);
                    return;
                }
                Task.Run(() => TrySubscribe(r));
            }

            private async Task TrySubscribe(SubscriptionDroppedReason reason)
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
                _subscription?.Dispose();
            }
        }
        private class ContinuesSubscription : INativeSubscription
        {
            private readonly NativeEventStore _parent;
            private readonly ILogger _log;
            private readonly bool _fromBeginning;
            private readonly EventHandler _onEvent;
            private readonly string _streamName;
            private readonly IProcessingContextFactory _processingContextFactory;
            private readonly EventStoreClient _connection;
            private FromStream _streamPosition = FromStream.Start;
            private StreamSubscription _subscriptionCatchUp;

            private StreamPosition? _lastSteamPosition;
            //private EventStoreSubscription _subscription;
            
            public ContinuesSubscription(NativeEventStore parent,
                in bool fromBeginning,
                EventHandler onEvent,
                string streamName,
                IProcessingContextFactory processingContextFactory, ILogger log)
            {
                _parent = parent;
                _connection = _parent._connection;
                _fromBeginning = fromBeginning;
                _onEvent = onEvent;
                _streamName = streamName;
                _processingContextFactory = processingContextFactory;
                _streamPosition = _fromBeginning ? FromStream.Start : FromStream.End;
                _log = log;
            }

            public async Task Subscribe()
            {
                if (_streamPosition == FromStream.Start)
                    _log.LogInformation("Subscribing from stream {streamName} from beginning: {isFromBeginning}",
                        _streamName, _fromBeginning);
                else 
                    _log.LogInformation("Subscribing from stream {streamName} from: {streamPosition}", _streamName, _streamPosition);

                if (_fromBeginning || (_streamPosition != FromStream.Start && _streamPosition != FromStream.End))
                {
                    await LoadCurrentStreamPosition();

                    _log.LogDebug("Last event at position: {lastEvent}", _lastSteamPosition);
                    if(_lastSteamPosition == null) SetSubscriptionLive();

                    _subscriptionCatchUp = await _connection.SubscribeToStreamAsync(_streamName, _streamPosition, OnEventAppeared, true, OnSubscriptionDropped);


                    //this._subscriptionCatchUp = _connection.SubscribeToStreamFrom(_streamName, 
                    //    _streamPosition,
                    //    CatchUpSubscriptionSettings.Default, 
                    //    OnEventAppeared, 
                    //    liveProcessingStarted: (s) =>
                    //    {
                    //        var live = _processingContextFactory?.Config?.OnLive;
                    //        if (live != null)
                    //        {
                    //            live();
                    //            _log.LogInformation("{streamName} is live", _streamName);
                    //        }
                            
                    //    },
                    //    subscriptionDropped: OnSubscriptionDropped);

                }
                else
                {
                    this._subscriptionCatchUp = await _connection.SubscribeToStreamAsync(_streamName, FromStream.End,
                        OnEventAppeared, true,
                        subscriptionDropped: OnSubscriptionDropped);
                }
            }

            private async Task LoadCurrentStreamPosition()
            {
                try
                {
                    var lastEv = (await _connection
                        .ReadStreamAsync(Direction.Backwards, _streamName, StreamPosition.End, 1)
                        .FirstOrDefaultAsync());
                    this._lastSteamPosition = lastEv.OriginalEventNumber;
                } catch (StreamNotFoundException) {  }
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private async Task OnEventAppeared(StreamSubscription arg1, ResolvedEvent arg2, CancellationToken arg3)
            {
                await OnEventAppeared(arg2);
                if (arg2.OriginalEventNumber == _lastSteamPosition)
                {
                    SetSubscriptionLive();
                }
                
            }

            private void SetSubscriptionLive()
            {
                var live = _processingContextFactory?.Config?.OnLive;
                if (live != null)
                {
                    live();
                    _log.LogInformation("{streamName} is live", _streamName);
                }
            }


            //private async Task OnEventAppeared(EventStoreSubscription s, ResolvedEvent e)
            //{
            //    await OnEventAppeared(e);
            //}

          
            private async Task OnEventAppeared(ResolvedEvent e)
            {
                _log.LogInformation("Event appeared: {steamName} {eventType}", e.Event.EventStreamId, e.Event.EventType);
                using (var context = _processingContextFactory.Create())
                {
                    using (StaticProcessingContext.CreateScope(context)) // should be moved to decorator.
                    {
                        var (m, ev) = _parent.ReadEvent(e, context);
                        
                        var currentIndex = FromStream.After(e.OriginalEventNumber);

                        //_log.LogInformation("Reading {eventNumber} {eventType} from {streamName}", e.Event.EventNumber,ev.GetType().Name, _streamName);
                        if (currentIndex >= _streamPosition || _streamPosition == FromStream.Start)
                        {
                            await _onEvent(context, m, ev);
                            _streamPosition = FromStream.After(e.OriginalEventNumber);
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
            private void OnSubscriptionDropped(StreamSubscription arg1, SubscriptionDroppedReason arg2, Exception arg3)
            {
                _log.LogInformation("Subscription dropped {steamName} {reason} {exception} at {position}",
                    _streamName,
                    arg2, arg3?.Message ?? "NoException",
                    _streamPosition);

                arg1.Dispose();

                if (arg2 != SubscriptionDroppedReason.Disposed)
                    Task.Run(TrySubscribe);
                else _log.LogInformation("We won't resubscribe to {streamName}", _streamName);
            }
            //private void OnSubscriptionDropped(EventStoreCatchUpSubscription eventStoreCatchUpSubscription, SubscriptionDropReason subscriptionDropReason, Exception arg3)
            //{
            //    _log.LogInformation("Subscription dropped {steamName} {reason} {exception} at {position}",
            //        _streamName,
            //        subscriptionDropReason, arg3?.Message ?? "NoException",
            //        _streamPosition);

            //    eventStoreCatchUpSubscription.Stop();
            //    if(subscriptionDropReason != SubscriptionDropReason.UserInitiated)
            //        Task.Run(TrySubscribe);
            //    else _log.LogInformation("We won't resubscribe to {streamName}", _streamName);
            //}
            //private void OnSubscriptionDropped(EventStoreSubscription eventStoreCatchUpSubscription, SubscriptionDropReason subscriptionDropReason, Exception arg3)
            //{
            //    _log.LogInformation("Subscription dropped {steamName} {reason} {exception}  at {position}", 
            //        _streamName,
            //        subscriptionDropReason, arg3?.Message ?? "NoException",
            //        _streamPosition);

            //    eventStoreCatchUpSubscription.Dispose();
            //    if (subscriptionDropReason != SubscriptionDropReason.UserInitiated)
            //        Task.Run(TrySubscribe);
            //    else _log.LogInformation("We won't resubscribe to {streamName}", _streamName);
            //}

            public void Dispose()
            {
                _subscriptionCatchUp?.Dispose();
            }
        }
    }
}
