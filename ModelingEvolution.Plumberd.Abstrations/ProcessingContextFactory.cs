using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd
{
    [DebuggerDisplay("Name: {Type.Name}, Mode: {_processingMode}")]
    public class ProcessingContextFactory : IProcessingContextFactory
    {
        private readonly Func<Type,object> _factory;
        private readonly Type _type;
        private readonly bool _isScopedFactory;
        private readonly HandlerDispatcher _dispatcher;
        private readonly IProcessingUnitConfig _config;
        private readonly IEventStore _store;
        private readonly IEventHandlerBinder _binder;
        private readonly ICommandInvoker _commandInvoker;
        private readonly ProcessingMode _processingMode;
        private SynchronizationContext _synchronizationContext;

        internal IEventHandlerBinder Binder => _binder;
        internal HandlerDispatcher Dispatcher => _dispatcher;
        public IProcessingUnitConfig Config => _config;
        internal Type Type => _type;
        internal SynchronizationContext SynchronizationContext => _synchronizationContext;
        public IEventStore EventStore => _store;
        public ICommandInvoker CommandInvoker => _commandInvoker;
        public ProcessingMode ProcessingMode => _processingMode;
        public ISubscription Subscription { get; set; }
        public Version Version { get;  }
        public ProcessingContextFactory(Func<Type,object> factory,
            Type type,
            bool isScopedFactory,
            HandlerDispatcher dispatcher,
            IEventStore store,
            ICommandInvoker commandInvoker,
            IEventHandlerBinder binder,
            IProcessingUnitConfig config,
            SynchronizationContext synchronizationContext,
            Version version)
        {
            _binder = binder;
            _factory = factory;
            _type = type;
            _isScopedFactory = isScopedFactory;
            _dispatcher = dispatcher;
            _store = store;

            _config = config;
            _synchronizationContext = synchronizationContext;
            _commandInvoker = commandInvoker;

            var types = _binder.Types().ToArray();
            if (types.All(x => typeof(ICommand).IsAssignableFrom(x)))
            {
                _processingMode = ProcessingMode.CommandHandler;
            }
            else if (types.All(x => typeof(IEvent).IsAssignableFrom(x)))
            {
                _processingMode = ProcessingMode.EventHandler;
            }
            else
                throw new NotSupportedException("Mixing command-handler scope and event-handler is not supported.");
            Version = version;
        }

        public IProcessingContext Create()
        {
            if (_processingMode == ProcessingMode.EventHandler)
                return new EventHandlerContext(this, _factory(_type), _isScopedFactory);
            if (_processingMode == ProcessingMode.CommandHandler)
                return new CommandHandlerContext(this, _factory(_type), _isScopedFactory);
            return null;
        }


        public void Dispose()
        {
            Subscription?.Dispose();
        }
    }
    public enum ProcessingMode
    {
        Both = 0x3,
        // Handler will react to IEventMetadata, TEvent
        // can return commands, events or nothing.
        EventHandler = 0x1,

        // Handler will react to Guid, ICommand
        CommandHandler = 0x2,
    }
}