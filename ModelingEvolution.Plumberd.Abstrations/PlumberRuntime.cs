using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using BindingFlags = ModelingEvolution.Plumberd.Binding.BindingFlags;

namespace ModelingEvolution.Plumberd
{
    public interface IPlumberRuntime
    {
        ICommandInvoker DefaultCommandInvoker { get; }
        IServiceProvider DefaultServiceProvider { get; }
        IEventStore DefaultEventStore { get; }
        IReadOnlyList<IProcessingUnit> Units { get; }
        void RegisterController(Type processingUnitType,
            Func<Type, object> controllerFactory = null,
            IProcessingUnitConfig config = null,
            IHandlerBinder binder = null,
            ICommandInvoker commandInvoker = null,
            IEventStore store = null,
            SynchronizationContext context = null);

        void RegisterController(object controller,
            IProcessingUnitConfig config = null,
            IHandlerBinder binder = null,
            ICommandInvoker invoker = null,
            IEventStore eventStore = null,
            SynchronizationContext context = null);

        
        Task StartAsync(Predicate<IProcessingUnit> filter = null);
    }

    public enum LifetimeMode
    {
        Singleton,
        Scoped
    }
    
    internal sealed class PlumberRuntime : IPlumberRuntime
    {
        private readonly List<ProcessingContextFactory> _units;
        public IReadOnlyList<IProcessingUnit> Units => _units;
        public PlumberRuntime(ICommandInvoker defaultCommandInvoker, 
            IEventStore defaultEventStore, 
            SynchronizationContext defaultSynchronizationContext, 
            IServiceProvider defaultServiceProvider)
        {
            DefaultCommandInvoker = defaultCommandInvoker;
            DefaultEventStore = defaultEventStore;
            DefaultSynchronizationContext = defaultSynchronizationContext;
            DefaultServiceProvider = defaultServiceProvider;
            _units = new List<ProcessingContextFactory>();
        }
        public IServiceProvider DefaultServiceProvider { get; }
        public ICommandInvoker DefaultCommandInvoker { get; }
        public IEventStore DefaultEventStore { get; }
        public SynchronizationContext DefaultSynchronizationContext { get; }

        public void RegisterController(
            Type processingUnitType,
            Func<Type, object> controllerFactory = null,
            IProcessingUnitConfig config = null,
            IHandlerBinder binder = null,
            ICommandInvoker commandInvoker = null,
            IEventStore store = null,
            SynchronizationContext context = null)
        {
            controllerFactory ??= DefaultServiceProvider.GetService;

            RegisterController(controllerFactory,
                processingUnitType,
                true,
                config,
                binder,
                commandInvoker,
                store,
                context);
        }

        public void RegisterController(object controller, 
            IProcessingUnitConfig config = null,
            IHandlerBinder binder = null,
            ICommandInvoker invoker = null,
            IEventStore eventStore = null,
            SynchronizationContext context = null)
        {
            if(controller == null)
                throw new ArgumentNullException(nameof(controller));

            var processingUnitType = controller.GetType();

            RegisterController((t) => controller, 
                processingUnitType,
                false, 
                config, 
                binder, 
                invoker, 
                eventStore,
                context);
        }

        

        public async void RegisterController(Func<Type,object> controllerFactory,
            Type processingUnitType,
            bool isScopeFactory,
            IProcessingUnitConfig config = null,
            IHandlerBinder binder = null,
            ICommandInvoker commandInvoker = null,
            IEventStore store = null,
            SynchronizationContext context = null)
        {
            var eventConfig = BuildConfiguration(processingUnitType, config, store ?? DefaultEventStore, ProcessingMode.EventHandler);
            var commandConfig = BuildConfiguration(processingUnitType, config, store ?? DefaultEventStore, ProcessingMode.CommandHandler);

            var eventBinder = binder ?? new HandlerBinder(processingUnitType)
                .Discover(true,
                    eventConfig != null
                        ? eventConfig.BindingFlags & (BindingFlags.ProcessEvents | BindingFlags.ReturnAll)
                        : BindingFlags.ProcessEvents | BindingFlags.ReturnAll);

            var commandBinder = binder ?? new HandlerBinder(processingUnitType)
                .Discover(true,
                    commandConfig != null
                        ? commandConfig.BindingFlags & (BindingFlags.ProcessCommands | BindingFlags.ReturnAll)
                        : BindingFlags.ProcessCommands | BindingFlags.ReturnAll);


            if (binder != null)
            {
                Subscribe(controllerFactory, processingUnitType, isScopeFactory,
                    config, binder, commandInvoker, store, context, ProcessingMode.Both);
                return;
            }

            bool subscribed = false;
            if (eventBinder.Types().Any())
            {
                Subscribe(controllerFactory, processingUnitType, isScopeFactory,
                    config, eventBinder, commandInvoker, store, context, ProcessingMode.EventHandler);
                subscribed = true;
            }
            if (commandBinder.Types().Any())
            {
                Subscribe(controllerFactory, processingUnitType, isScopeFactory,
                    config, commandBinder, commandInvoker, store, context, ProcessingMode.CommandHandler);
                subscribed = true;
            }

            if (!subscribed)
                throw new ControllerRegistrationFailed($"Type '{processingUnitType.Name}' has no methods to subscribe to.");
        }
        private IProcessingUnit Subscribe(Func<Type,object> processingUnitFactory,
            Type processingUnitType,
            bool isScopedFactory,
            IProcessingUnitConfig config,
            IHandlerBinder handlerBinder,
            ICommandInvoker commandInvoker,
            IEventStore store,
            SynchronizationContext context, 
            ProcessingMode processingMode)
        {
            // We'll create 2 subscriptions
            // one is for commands
            // second is for events

            if(processingUnitType == null)
                throw new ArgumentNullException(nameof(processingUnitType));
            if(processingUnitFactory == null)
                throw new ArgumentNullException(nameof(processingUnitFactory));
            
            store ??= DefaultEventStore;
            if (store == null) 
                throw new ArgumentException("EventStore cannot be null.");

            // we ignore configuration if the processing mode is not appropriate.
            config = BuildConfiguration(processingUnitType, config, store, processingMode);

            handlerBinder ??= new HandlerBinder(processingUnitType).Discover(true, config.BindingFlags);
            
            commandInvoker ??= DefaultCommandInvoker;
            context ??= DefaultSynchronizationContext;

            if(config.IsCommandEmitEnabled && commandInvoker == null)
                throw new ArgumentException("CommandInvoker cannot be null when 'IsCommandProcessingEnabled' is ON.");

            var recordTypes = handlerBinder
                .Types()
                .SelectMany(store.Settings.RecordNamingConvention)
                .ToArray();

            if (recordTypes.Any())
            {
                var dispatcher = handlerBinder.CreateDispatcher();

                var factory = new ProcessingContextFactory(processingUnitFactory,
                    processingUnitType,
                    isScopedFactory,
                    dispatcher,
                    store,
                    commandInvoker,
                    handlerBinder,
                    config,
                    context);

                _units.Add(factory);
                return factory;
            }
            
            throw new ArgumentException("There must be some events or commands to subscribe to.");

        }

        private static IProcessingUnitConfig BuildConfiguration(Type processingUnitType, IProcessingUnitConfig config, IEventStore store,
            ProcessingMode processingMode)
        {
            if (config != null && !config.ProcessingMode.HasFlag(processingMode))
                config = null;

            // this will create processing config for both command-handler & event-handler
            if (config == null || !config.IsNameOverriden)
            {
                config ??= new ProcessingUnitConfig(processingUnitType);

                if (processingMode == ProcessingMode.CommandHandler)
                    config.Name = $"{store.Settings.CommandStreamPrefix}{config.Name}";
            }

            return config;
        }

        public async Task StartAsync(Predicate<IProcessingUnit> filter = null)
        {
            filter ??= x => true;

            foreach (var u in _units.Where(x=>filter(x)))
            {
                var types = u.Binder
                    .Types()
                    .SelectMany(u.EventStore.Settings.RecordNamingConvention)
                    .ToArray();

                if (u.SynchronizationContext == null)
                    await u.EventStore.Subscribe(u.Config.Name, u.Config.SubscribesFromBeginning,
                        u.Config.IsPersistent, ProcessEventsLoop, u, types);

                else
                    await u.EventStore.Subscribe(u.Config.Name, u.Config.SubscribesFromBeginning,
                        u.Config.IsPersistent, ProcessEventsLoopWithSync, u, types);
            }
        }
        async Task ProcessEventsLoop(IProcessingContext context, IMetadata m, IRecord e)
        {
            if (context.Config.ProcessingLag > TimeSpan.Zero)
                await Task.Delay(context.Config.ProcessingLag);
            var result = await context.Dispatcher(context.ProcessingUnit, m, e);
            if (!result.IsEmpty)
            {
                if (context.Config.IsEventEmitEnabled)
                    foreach (var (nm, nev) in result.Events)
                    {
                        await context.EventStore.GetEventStream(nev.GetType(), nm, context)
                            .Append(nev,context);
                    }

                if (context.Config.IsCommandEmitEnabled)
                    foreach (var (id, cmd) in result.Commands)
                        await context.CommandInvoker.Execute(id, cmd);
            }
        }
        async Task ProcessEventsLoopWithSync(IProcessingContext context, IMetadata m, IRecord e)
        {
            if (context.Config.ProcessingLag > TimeSpan.Zero)
                await Task.Delay(context.Config.ProcessingLag);
            ProcessingResults result = new ProcessingResults();
            context.SynchronizationContext.Send(x => result += context.Dispatcher(context.ProcessingUnit, m, e).GetAwaiter().GetResult(), null);
            if (!result.IsEmpty)
            {
                if (context.Config.IsEventEmitEnabled)
                    foreach (var (nm, nev) in result.Events)
                        await context.EventStore.GetEventStream(nev.GetType(), nm)
                            .Append(nev, context);

                if (context.Config.IsCommandEmitEnabled)
                    foreach (var (id, cmd) in result.Commands)
                        await context.CommandInvoker.Execute(id, cmd);
            }
        }
    }
}