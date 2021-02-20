﻿using System;
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
            where TError : IErrorEvent;

        Task Execute<TCommand>(Guid id, TCommand c)
            where TCommand : ICommand;

        Task Execute<TCommand>(Guid id, TCommand c,
            Action<TCommand, IErrorEvent> onError)
            where TCommand : ICommand;

        Task Execute<TCommand, TError>(Guid id, TCommand c, 
            Action<TCommand, TError> onError)
            where TCommand:ICommand
            where TError : IErrorEvent;

        Task Execute<TCommand, TError, TError2>(Guid id, TCommand c,
            Action<TCommand, TError> onError,
            Action<TCommand, TError2> onError2)
            where TCommand : ICommand
            where TError : IErrorEvent
            where TError2 : IErrorEvent;

        Task Execute<TCommand, TError, TError2, TError3>(Guid id, TCommand c,
            Action<TCommand, TError> onError,
            Action<TCommand, TError2> onError2,
            Action<TCommand, TError2> onError3)
            where TCommand : ICommand
            where TError : IErrorEvent
            where TError2 : IErrorEvent
            where TError3 : IErrorEvent;

    }

    public class CommandManager : ICommandManager
    {
        private readonly CommandErrorSubscriptionManager _manager;
        private readonly ICommandInvoker _commandInvoker;
        public CommandManager(CommandErrorSubscriptionManager manager, ICommandInvoker commandInvoker)
        {
            _manager = manager;
            _commandInvoker = commandInvoker;
        }

        public void SubscribeErrorHandler<TCommand, TError>(Action<TCommand, TError> onError) where TCommand : ICommand where TError : IErrorEvent
        {
            _manager.SubscribeErrorHandler(onError);
        }

        public Task Execute<TCommand>(Guid id, TCommand c) where TCommand : ICommand
        {
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand>(Guid id, TCommand c, Action<TCommand, IErrorEvent> onError) where TCommand : ICommand
        {
            _manager.Subscribe(c,onError);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand, TError>(Guid id, TCommand c, Action<TCommand, TError> onError) where TCommand : ICommand where TError : IErrorEvent
        {
            _manager.Subscribe<TCommand, TError>(c, onError);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand, TError, TError2>(Guid id, TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2) where TCommand : ICommand where TError : IErrorEvent where TError2 : IErrorEvent
        {
            _manager.Subscribe(c, onError, onError2);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand, TError, TError2, TError3>(Guid id, TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2, Action<TCommand, TError2> onError3) where TCommand : ICommand where TError : IErrorEvent where TError2 : IErrorEvent where TError3 : IErrorEvent
        {
            _manager.Subscribe(c, onError, onError2, onError3);
            return _commandInvoker.Execute(id, c);
        }
    }
    public class CommandErrorSubscriptionManager 
    {
        
        class HandlerBinder : IEventHandlerBinder
        {
            private Type[] types = new[] {typeof(IErrorEvent)};
            public IEventHandlerBinder Discover(bool searchInProperties, Predicate<MethodInfo> methodFilter = null)
            {
                return this;
            }

            public IEnumerable<Type> Types()
            {
                return types;
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
            private readonly CommandErrorSubscriptionManager _parent;

            public Handler(CommandErrorSubscriptionManager parent)
            {
                _parent = parent;
            }

            public void Given(IMetadata m, IRecord ev)
            {
                var errorEventType = ev.GetType();
                var correlationId = m.CorrelationId();

                ErrorHandlerSlot slot = new ErrorHandlerSlot(correlationId, errorEventType);
                if (ev is IEvent)
                {
                    var e = (IEvent)ev;
                    if (_parent._errorSpecificHandlers.TryGetValue(slot, out var action))
                        action(e);

                    if (_parent._errorCorrelationOnlyHandlers.TryGetValue(correlationId, out var correlationAction))
                        correlationAction(e);

                    if (_parent._errorGenericHandlers.TryGetValue(errorEventType, out var actions))
                        foreach (var a in actions)
                            a(e);
                    _parent._genericHandler?.Invoke(e);
                }
            }
        }

        struct ErrorHandlerSlot : IEquatable<ErrorHandlerSlot>
        {
            public Guid CorrelationId;
            public Type ErrorEventType;

            public bool Equals(ErrorHandlerSlot other)
            {
                return CorrelationId.Equals(other.CorrelationId) && Equals(ErrorEventType, other.ErrorEventType);
            }

            public override bool Equals(object obj)
            {
                return obj is ErrorHandlerSlot other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CorrelationId, ErrorEventType);
            }

            public static bool operator ==(ErrorHandlerSlot left, ErrorHandlerSlot right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(ErrorHandlerSlot left, ErrorHandlerSlot right)
            {
                return !left.Equals(right);
            }

            public ErrorHandlerSlot(Guid correlationId, Type errorEventType)
            {
                CorrelationId = correlationId;
                ErrorEventType = errorEventType;
            }
        }
        private bool _isInitialized;
        private Action<IEvent> _genericHandler;
        
        private readonly IPlumberRuntime _plumber;
        private readonly ISessionManager _sessionManager;
        private readonly ConcurrentDictionary<ErrorHandlerSlot, Action<IEvent>> _errorSpecificHandlers;
        private readonly ConcurrentDictionary<Type, List<Action<IEvent>>> _errorGenericHandlers;
        private readonly ConcurrentDictionary<Guid, Action<IEvent>> _errorCorrelationOnlyHandlers;
        
        private IProcessingUnit _processingUnit;

        public CommandErrorSubscriptionManager(IPlumberRuntime plumber, 
            ISessionManager sessionManager)
        {
           
            _plumber = plumber;
            _sessionManager = sessionManager;
            _errorSpecificHandlers = new ConcurrentDictionary<ErrorHandlerSlot, Action<IEvent>>();
            _errorGenericHandlers = new ConcurrentDictionary<Type, List<Action<IEvent>>>();
            _errorCorrelationOnlyHandlers = new ConcurrentDictionary<Guid, Action<IEvent>>();
            _isInitialized = false;
        }

        private void CheckInit()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                Task.Run(Subscribe).GetAwaiter().GetResult();
            }
        }

        private async void Subscribe()
        {
            // we subscribe to SessionStream.
            var handler = new Handler(this);
            IEventHandlerBinder eventHandlerBinder = new HandlerBinder();
            var config = new ProcessingUnitConfig(typeof(Handler))
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
            };
            this._processingUnit = await _plumber.RunController(handler, config, eventHandlerBinder);
        }

        public void SubscribeErrorHandler(Action<ICommand, IErrorEvent> action)
        {
            CheckInit();
            this._genericHandler += (e) =>
            {
                var ee = (IEventException) e;
                action(ee.Record as ICommand, ee.ExceptionData);
            };
        }
        public void SubscribeErrorHandler<TCommand, TError>(Action<TCommand, TError> onError)
            where TCommand : ICommand
            where TError : IErrorEvent
        {
            CheckInit();
            var key = typeof(EventException<TCommand, TError>);
            var list = _errorGenericHandlers.GetOrAdd(key, (k) => new List<Action<IEvent>>());
            list.Add(x =>
            {
                if(x is EventException<TCommand, TError> ee)
                    onError(ee.Record, ee.ExceptionData);
            });
        }

      

        public void Subscribe<TCommand>(TCommand c, Action<TCommand, IErrorEvent> onError) where TCommand : ICommand
        {
            CheckInit();
            _errorCorrelationOnlyHandlers.TryAdd(c.Id, (ev) =>
            {
                var er = (IEventException) ev;
                if(er.Record is TCommand record)
                    onError(record, er.ExceptionData);
            });
            
        }

        public void Subscribe<TCommand, TError>(TCommand c, 
            Action<TCommand, TError> onError)
            where TCommand:ICommand
            where TError : IErrorEvent
        {
            CheckInit();
            AddSpecificSubscription(c, onError);
        }

        public void Subscribe<TCommand, TError, TError2>(TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2) 
            where TCommand : ICommand 
            where TError : IErrorEvent
            where TError2 : IErrorEvent
        {
            CheckInit();
            AddSpecificSubscription(c, onError);
            AddSpecificSubscription(c, onError2);
            
        }

        public void Subscribe<TCommand, TError, TError2, TError3>(TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2, Action<TCommand, TError3> onError3)
            where TCommand : ICommand 
            where TError : IErrorEvent
            where TError2 : IErrorEvent
            where TError3 : IErrorEvent
        {
            CheckInit();
            AddSpecificSubscription(c, onError);
            AddSpecificSubscription(c, onError2);
            AddSpecificSubscription(c, onError3);
            
        }

        private void AddSpecificSubscription<TCommand, TError>(TCommand c, Action<TCommand, TError> onError)
            where TCommand : ICommand where TError : IErrorEvent
        {
            var key = typeof(EventException<TCommand, TError>);
            _errorSpecificHandlers.TryAdd(new ErrorHandlerSlot(c.Id, key), x =>
            {
                if(x is EventException<TCommand, TError> c)
                   onError(c.Record, c.ExceptionData);
            });
        }
    }
}