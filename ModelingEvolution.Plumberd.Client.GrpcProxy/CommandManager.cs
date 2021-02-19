using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.Client.GrpcProxy;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Querying;

namespace ModelingEvolution.Plumberd.Client
{
    public interface ICommandManager
    {
        void SubscribeErrorHandler<TCommand, TError>(Action<TCommand, TError> onError)
            where TCommand : ICommand
            where TError : IEvent;

        Task Execute<TCommand, TError>(Guid id, TCommand c, 
            Action<TCommand, TError> onError)
            where TCommand:ICommand
            where TError : IEvent;

        Task Execute<TCommand, TError, TError2>(Guid id, TCommand c,
            Action<TCommand, TError> onError,
            Action<TCommand, TError2> onError2)
            where TCommand : ICommand
            where TError : IEvent
            where TError2 : IEvent;

        Task Execute<TCommand, TError, TError2, TError3>(Guid id, TCommand c,
            Action<TCommand, TError> onError,
            Action<TCommand, TError2> onError2,
            Action<TCommand, TError2> onError3)
            where TCommand : ICommand
            where TError : IEvent
            where TError2 : IEvent
            where TError3 : IEvent;

    }

    public class CommandManager : ICommandManager
    {
        class HandlerBinder : IEventHandlerBinder
        {
            public IEventHandlerBinder Discover(bool searchInProperties, Predicate<MethodInfo> methodFilter = null)
            {
                return this;
            }

            public IEnumerable<Type> Types()
            {
                return Array.Empty<Type>();
            }

            public HandlerDispatcher CreateDispatcher()
            {
                return (processingUnit, metadata, ev) =>
                {
                    ((Handler) processingUnit).Given(metadata, ev);
                    return Task.FromResult(new ProcessingResults());
                };
            }
        }
        class Handler
        {
            private readonly CommandManager _parent;

            public Handler(CommandManager parent)
            {
                _parent = parent;
            }

            public void Given(IMetadata m, IRecord ev)
            {
                var errorEventType = ev.GetType();
                ErrorHandlerSlot slot = new ErrorHandlerSlot(m.CorrelationId(), errorEventType);
                if (_parent._errorSpecificHandlers.TryGetValue(slot, out var action)) action((IEvent) ev);
                if (_parent._errorGenericHandlers.TryGetValue(errorEventType, out var actions)) 
                    foreach(var a in actions) 
                        a((IEvent) ev);
            }
        }

        struct ErrorHandlerSlot
        {
            public Guid CorrelationId;
            public Type ErrorEventType;

            public ErrorHandlerSlot(Guid correlationId, Type errorEventType)
            {
                CorrelationId = correlationId;
                ErrorEventType = errorEventType;
            }
        }
        private readonly ICommandInvoker _commandInvoker;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPlumberRuntime _plumber;
        private readonly ISessionManager _sessionManager;
        private readonly ConcurrentDictionary<ErrorHandlerSlot, Action<IEvent>> _errorSpecificHandlers;
        private readonly ConcurrentDictionary<Type, List<Action<IEvent>>> _errorGenericHandlers;
        public CommandManager(ICommandInvoker commandInvoker, 
            IServiceProvider serviceProvider,
            IPlumberRuntime plumber)
        {
            _commandInvoker = commandInvoker;
            _serviceProvider = serviceProvider;
            _plumber = plumber;
            _errorSpecificHandlers = new ConcurrentDictionary<ErrorHandlerSlot, Action<IEvent>>();
            _errorGenericHandlers = new ConcurrentDictionary<Type, List<Action<IEvent>>>();
        }

        private async void Subscribe()
        {
            // we subscribe to SessionStream.

            var scope = _serviceProvider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            
            var handler = new Handler(this);
            IEventHandlerBinder eventHandlerBinder = new HandlerBinder();
            var processingUnit = await _plumber.RunController(handler, 
                new ProcessingUnitConfig(typeof(Handler))
            {
                IsEventEmitEnabled = false,
                IsCommandEmitEnabled = false,
                IsPersistent = false,
                ProcessingMode = ProcessingMode.EventHandler,
                SubscribesFromBeginning = false,
                ProjectionSchema = new ProjectionSchema()
                {
                    StreamName = $"/Session-{_sessionManager.Default()}"
                },

            }, eventHandlerBinder);
        }

        public void SubscribeErrorHandler<TCommand, TError>(Action<TCommand, TError> onError)
            where TCommand : ICommand
            where TError : IEvent
        {
            var key = typeof(EventException<TCommand, TError>);
            var list = _errorGenericHandlers.GetOrAdd(key, (k) => new List<Action<IEvent>>());
            list.Add(x =>
            {
                var ee = (EventException<TCommand, TError>) x;
                onError(ee.Record, ee.ExceptionData);
            });
        }
        public Task Execute<TCommand, TError>(Guid id, TCommand c, 
            Action<TCommand, TError> onError)
            where TCommand:ICommand
            where TError : IEvent
        {
            AddSpecificSubscription(c, onError);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand, TError, TError2>(Guid id, TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2) where TCommand : ICommand where TError : IEvent where TError2 : IEvent
        {
            AddSpecificSubscription(c, onError);
            AddSpecificSubscription(c, onError2);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand, TError, TError2, TError3>(Guid id, TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2, Action<TCommand, TError2> onError3) where TCommand : ICommand where TError : IEvent where TError2 : IEvent where TError3 : IEvent
        {
            AddSpecificSubscription(c, onError);
            AddSpecificSubscription(c, onError2);
            AddSpecificSubscription(c, onError3);
            return _commandInvoker.Execute(id, c);
        }

        private void AddSpecificSubscription<TCommand, TError>(TCommand c, Action<TCommand, TError> onError)
            where TCommand : ICommand where TError : IEvent
        {
            var key = typeof(EventException<TCommand, TError>);
            _errorSpecificHandlers.TryAdd(new ErrorHandlerSlot(c.Id, key), x =>
            {
                var c = (EventException<TCommand, TError>) x;
                onError(c.Record, c.ExceptionData);
            });
        }
    }
}