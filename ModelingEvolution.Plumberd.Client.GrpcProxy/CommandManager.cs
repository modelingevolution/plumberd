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
        void SubscribeErrorHandler(Action<ICommand, IErrorEvent> onError);
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
        public void SubscribeErrorHandler(Action<ICommand, IErrorEvent> onError)
        {
            _manager.SubscribeErrorHandler(onError);
        }
        public Task Execute<TCommand>(Guid id, TCommand c) where TCommand : ICommand
        {
            _manager.TrackCommand(c);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand>(Guid id, TCommand c, Action<TCommand, IErrorEvent> onError) where TCommand : ICommand
        {
            _manager.Subscribe(c,onError);
            return _commandInvoker.Execute(id, c);
        }

        public Task Execute<TCommand, TError>(Guid id, TCommand c, Action<TCommand, TError> onError) where TCommand : ICommand where TError : IErrorEvent
        {
            _manager.Subscribe(c, onError);
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
                if (ev is IErrorEvent e)
                {
                    if (_parent._errorSpecificHandlers.TryGetValue(slot, out var action))
                        action(m,e);

                    if (_parent._errorCorrelationOnlyHandlers.TryGetValue(correlationId, out var correlationAction))
                        correlationAction(m, e);

                    if (_parent._errorGenericHandlers.TryGetValue(errorEventType, out var actions))
                        foreach (var a in actions)
                            a(m, e);
                    _parent._genericHandler?.Invoke(m, e);
                }
            }
        }
        readonly struct Slot
        {
            public readonly Action cleanUpAction;
            public readonly DateTime InvokedAt;
            public readonly Guid CommandId;
            public Slot(Guid commandId, Action cleanupAction)
            {
                cleanUpAction = cleanupAction;
                InvokedAt = DateTime.Now;
                CommandId = commandId;
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
        private Action<IMetadata, IErrorEvent> _genericHandler;
        
        private readonly IPlumberRuntime _plumber;
        private readonly ISessionManager _sessionManager;
        private readonly ConcurrentDictionary<ErrorHandlerSlot, Action<IMetadata, IErrorEvent>> _errorSpecificHandlers;
        private readonly ConcurrentDictionary<Type, List<Action<IMetadata, IErrorEvent>>> _errorGenericHandlers;
        private readonly ConcurrentDictionary<Guid, Action<IMetadata, IErrorEvent>> _errorCorrelationOnlyHandlers;
        private readonly ConcurrentDictionary<Guid, ICommand> _invokedCommands;
        private readonly ConcurrentQueue<Slot> _slots;
        private IProcessingUnit _processingUnit;

        public int Count
        {
            get
            {
                return _errorCorrelationOnlyHandlers.Count +
                       _errorGenericHandlers.Count +
                       _errorSpecificHandlers.Count + 
                        (_genericHandler?.GetInvocationList().Length ?? 0);
            }
        }
        public CommandErrorSubscriptionManager(IPlumberRuntime plumber, 
            ISessionManager sessionManager)
        {
           
            _plumber = plumber;
            _sessionManager = sessionManager;
            _errorSpecificHandlers = new ConcurrentDictionary<ErrorHandlerSlot, Action<IMetadata, IErrorEvent>>();
            _errorGenericHandlers = new ConcurrentDictionary<Type, List<Action<IMetadata, IErrorEvent>>>();
            _errorCorrelationOnlyHandlers = new ConcurrentDictionary<Guid, Action<IMetadata, IErrorEvent>>();
            _invokedCommands = new ConcurrentDictionary<Guid, ICommand>();
            _slots = new ConcurrentQueue<Slot>();
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

        private ICommand GetCorrelatedCommand(Guid correlationId)
        {
            if(_invokedCommands.TryGetValue(correlationId, out var command))
                return command;
            return null;
        }
        public void SubscribeErrorHandler(Action<ICommand, IErrorEvent> action)
        {
            CheckInit();
            this._genericHandler += (m,e) =>
            {
                var cmd = GetCorrelatedCommand(m.CorrelationId());
                action(cmd, e);
            };
            CleanUp();
        }
        public void SubscribeErrorHandler<TCommand, TError>(Action<TCommand, TError> onError)
            where TCommand : ICommand
            where TError : IErrorEvent
        {
            CheckInit();
            var key = typeof(TError);
            var list = _errorGenericHandlers.GetOrAdd(key, (k) => new List<Action<IMetadata, IErrorEvent>>());
            list.Add((m,e) =>
            {
                var cmd = GetCorrelatedCommand(m.CorrelationId());
                if (cmd is TCommand command && e is TError error)
                    onError(command,error);
            });
            CleanUp();
        }

      

        public void Subscribe<TCommand>(TCommand c, Action<TCommand, IErrorEvent> onError) where TCommand : ICommand
        {
            CheckInit();
            AddCorrelationCommand(c);
            _errorCorrelationOnlyHandlers.TryAdd(c.Id, (m,ev) =>
            {
                var cmd = GetCorrelatedCommand(m.CorrelationId());
                
                if(cmd is TCommand record)
                    onError(record, ev);
            });
            _slots.Enqueue(new Slot(c.Id,() => _errorCorrelationOnlyHandlers.TryRemove(c.Id, out var v)));
            CleanUp();
        }

        private void AddCorrelationCommand<TCommand>(TCommand command) where TCommand : ICommand
        {
            this._invokedCommands.TryAdd(command.Id, command);
        }

        public void Subscribe<TCommand, TError>(TCommand c, 
            Action<TCommand, TError> onError)
            where TCommand:ICommand
            where TError : IErrorEvent
        {
            CheckInit();
            AddCorrelationCommand(c);
            AddSpecificSubscription(c, onError);
            CleanUp();
        }

        public void Subscribe<TCommand, TError, TError2>(TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2) 
            where TCommand : ICommand 
            where TError : IErrorEvent
            where TError2 : IErrorEvent
        {
            CheckInit();
            AddCorrelationCommand(c);
            AddSpecificSubscription(c, onError);
            AddSpecificSubscription(c, onError2);
            CleanUp();
        }

        public void Subscribe<TCommand, TError, TError2, TError3>(TCommand c, Action<TCommand, TError> onError, Action<TCommand, TError2> onError2, Action<TCommand, TError3> onError3)
            where TCommand : ICommand 
            where TError : IErrorEvent
            where TError2 : IErrorEvent
            where TError3 : IErrorEvent
        {
            CheckInit();
            AddCorrelationCommand(c);
            AddSpecificSubscription(c, onError);
            AddSpecificSubscription(c, onError2);
            AddSpecificSubscription(c, onError3);
            CleanUp();
        }
        
        private void AddSpecificSubscription<TCommand, TError>(TCommand c, Action<TCommand, TError> onError)
            where TCommand : ICommand where TError : IErrorEvent
        {
            var key = typeof(TError);
            var slot = new ErrorHandlerSlot(c.Id, key);
            _errorSpecificHandlers.TryAdd(slot, (m,x) =>
            {
                var cmd = GetCorrelatedCommand(m.CorrelationId());
                if (x is TError error && cmd is TCommand command)
                   onError(command, error);
            });
            _slots.Enqueue(new Slot(c.Id, () => _errorSpecificHandlers.TryRemove(slot, out var x)));
        }

        public TimeSpan TimeOut { get; set; } = TimeSpan.FromMinutes(5);

        public void CleanUp()
        {
            lock(_slots)
            {
                while (_slots.TryPeek(out var r))
                {
                    if (r.InvokedAt.Add(TimeOut) < DateTime.Now)
                    {
                        r.cleanUpAction();
                        _slots.TryDequeue(out var r2);
                        _invokedCommands.TryRemove(r.CommandId, out var c);
                    }
                    else break;
                }
            }
        }

        public void TrackCommand<TCommand>(TCommand c) where TCommand : ICommand
        {
            AddCorrelationCommand(c);
        }
    }
}