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
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.CommandHandling;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ModelingEvolution.Plumberd.Threading;
using BindingFlags = ModelingEvolution.Plumberd.Binding.BindingFlags;
using EventHandler = ModelingEvolution.Plumberd.EventStore.EventHandler;

namespace ModelingEvolution.Plumberd
{
    public interface IPlumberRuntime
    {
        IIgnoreFilter IgnoreFilter { get; }
        ICommandInvoker DefaultCommandInvoker { get; }
        IServiceProvider DefaultServiceProvider { get; }
        IEventStore DefaultEventStore { get; }
        Version DefaultVersion { get; }
        IReadOnlyList<IProcessingUnit> Units { get; }
        IPlumberRuntime RegisterController(Type processingUnitType,
            Func<Type, object> controllerFactory = null,
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
            ICommandInvoker commandInvoker = null,
            IEventStore store = null,
            SynchronizationContext context = null);

        IPlumberRuntime RegisterController(object controller,
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
            ICommandInvoker invoker = null,
            IEventStore eventStore = null,
            SynchronizationContext context = null);

        Task<IProcessingUnit> RunController(object controller,
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
            ICommandInvoker invoker = null,
            IEventStore store = null,
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
        public IReadOnlyList<IProcessingUnit> Units => _units;
        private readonly List<ProcessingContextFactory> _units;
        private readonly Action<Exception> _onException;
            
        
        public PlumberRuntime(
            ICommandInvoker defaultCommandInvoker,
            IEventStore defaultEventStore,
            SynchronizationContext defaultSynchronizationContext,
            IServiceProvider defaultServiceProvider, 
            Version defaultVersion,
            Action<Exception> onException)
        {
            DefaultCommandInvoker = defaultCommandInvoker;
            DefaultEventStore = defaultEventStore;
            DefaultSynchronizationContext = defaultSynchronizationContext;
            DefaultServiceProvider = defaultServiceProvider;
            DefaultVersion = defaultVersion;
            IgnoreFilter = new IgnoreFilter();
            _units = new List<ProcessingContextFactory>();
            _onException = onException;
        }
        public IIgnoreFilter IgnoreFilter { get;  }
        public IServiceProvider DefaultServiceProvider { get; }
        public ICommandInvoker DefaultCommandInvoker { get; }
        public IEventStore DefaultEventStore { get; }
        public SynchronizationContext DefaultSynchronizationContext { get; }
        public Version DefaultVersion { get; }

        public IPlumberRuntime RegisterController(
            Type processingUnitType,
            Func<Type, object> controllerFactory = null,
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
            ICommandInvoker commandInvoker = null,
            IEventStore store = null,
            SynchronizationContext context = null)
        {
            controllerFactory ??= DefaultServiceProvider.GetRequiredService;

            RegisterController(controllerFactory,
                processingUnitType,
                true,
                config,
                binder,
                commandInvoker,
                store,
                context);
            return this;
        }

        public IPlumberRuntime RegisterController(object controller, 
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
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
            return this;
        }

        public async Task<IProcessingUnit> RunController(object controller, 
            IProcessingUnitConfig config = null, 
            IEventHandlerBinder binder = null,
            ICommandInvoker invoker = null, 
            IEventStore store = null, 
            SynchronizationContext context = null)
        {
            var processingUnitType = controller.GetType();
            var eventConfig = BuildConfiguration(processingUnitType, config, store ?? DefaultEventStore, ProcessingMode.EventHandler);
            HookupLiveProjection(controller, eventConfig);
            
            var eventBinder = binder ?? new EventHandlerBinder(processingUnitType)
                .Discover(true,
                    eventConfig != null
                        ? eventConfig.BindingFlags & (BindingFlags.ProcessEvents | BindingFlags.ReturnAll)
                        : BindingFlags.ProcessEvents | BindingFlags.ReturnAll);

            var unit = (ProcessingContextFactory)Subscribe((t) => controller, 
                processingUnitType, false,
                eventConfig, eventBinder, invoker, store, context,ProcessingMode.EventHandler);

            unit.Subscription = await Start(unit);
            return unit;
        }

        private static void HookupLiveProjection(object controller, IProcessingUnitConfig config)
        {
            if (controller is not ILiveProjection lp) return;
            
            if (config.OnLive != null)
            {
                var tmp = config.OnLive;
                config.OnLive = () =>
                {
                    tmp();
                    lp.IsLive = true;
                };
            }
            else config.OnLive = () => { lp.IsLive = true; };
        }


        public void RegisterController(Func<Type,object> controllerFactory,
            Type processingUnitType,
            bool isScopeFactory,
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
            ICommandInvoker commandInvoker = null,
            IEventStore store = null,
            SynchronizationContext context = null)
        {
            var eventConfig = BuildConfiguration(processingUnitType, config, store ?? DefaultEventStore, ProcessingMode.EventHandler);
            var commandConfig = BuildConfiguration(processingUnitType, config, store ?? DefaultEventStore, ProcessingMode.CommandHandler);

            var eventBinder = binder ?? new EventHandlerBinder(processingUnitType)
                .Discover(true,
                    eventConfig != null
                        ? eventConfig.BindingFlags & (BindingFlags.ProcessEvents | BindingFlags.ReturnAll)
                        : BindingFlags.ProcessEvents | BindingFlags.ReturnAll);

            var commandBinder = binder ?? new EventHandlerBinder(processingUnitType)
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
            IEventHandlerBinder eventHandlerBinder,
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

            eventHandlerBinder ??= new EventHandlerBinder(processingUnitType).Discover(true, config.BindingFlags);
            
            commandInvoker ??= DefaultCommandInvoker;
            context ??= DefaultSynchronizationContext;

            if(config.IsCommandEmitEnabled && commandInvoker == null)
                throw new ArgumentException("CommandInvoker cannot be null when 'IsCommandProcessingEnabled' is ON.");

            var recordTypes = eventHandlerBinder
                .Types()
                .SelectMany(store.Settings.RecordNamingConvention)
                .ToArray();

            if (recordTypes.Any())
            {
                var dispatcher = eventHandlerBinder.CreateDispatcher();
                if (config.OnAfterDispatch != null)
                {
                    var inner = dispatcher;
                    dispatcher = async (unit, metadata, ev) =>
                    {
                        var r = await inner(unit, metadata, ev);
                        await config.OnAfterDispatch.Invoke(unit, metadata, ev, r);
                        return r;
                    };
                }
                var factory = new ProcessingContextFactory(processingUnitFactory,
                    processingUnitType,
                    isScopedFactory,
                    dispatcher,
                    store,
                    commandInvoker,
                    eventHandlerBinder,
                    config,
                    context, DefaultVersion);

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

            var filterConfig = BuildConfiguration(this.IgnoreFilter.GetType(), null, DefaultEventStore, ProcessingMode.EventHandler);
            AsyncManualResetEvent sv = new AsyncManualResetEvent(false);
            filterConfig.OnLive += sv.Set;
            var filterUnit = await RunController(this.IgnoreFilter, filterConfig);
            await sv.WaitAsync();

            RegisterController(new IgnoreFilterCommandHandler());

            await _units
                .Where(x=>x.EventStore != null && filter(x))
                .Select(x => x.EventStore)
                .Distinct()
                .ExecuteForAll(x=>x.Init());

            await _units.Where(x => filter(x))
                .ExecuteForAll(Start);
        }

        private async Task<ISubscription> Start(ProcessingContextFactory u)
        {
            var types = u.Binder
                .Types()
                .SelectMany(u.EventStore.Settings.RecordNamingConvention)
                .ToArray();

            try
            {
                EventHandler loop = ProcessEventsLoop;
                if (u.SynchronizationContext != null)
                    //loop = async (c,m,e) => await ProcessEventsLoop(c,m,e);
                    //else 
                    loop = ProcessEventsLoopWithSync;
                if (u.Config.ProjectionSchema != null)
                    return await u.EventStore.Subscribe(u.Config.ProjectionSchema, u.Config.SubscribesFromBeginning,
                                                        u.Config.IsPersistent, loop, u);
                else
                    return await u.EventStore.Subscribe(u.Config.Name, u.Config.SubscribesFromBeginning,
                                                        u.Config.IsPersistent, loop, u, types);
            }
            catch (Exception ex)
            {
                _onException?.Invoke(ex);
                throw;
            }
        }
        
        async Task ProcessEventsLoop(IProcessingContext context, IMetadata m, IRecord e)
        {
            if (context.Config.ProcessingLag > TimeSpan.Zero)
                await Task.Delay(context.Config.ProcessingLag);
            ProcessingResults result = new ProcessingResults();
            try
            {
                // BAD: we assume that correllation-id is on.
                if (this.IgnoreFilter?.IsFiltered(m.CorrelationId()) ?? false)
                    return;

                if (context.Config.RequiresCurrentVersion)
                {
                    if(context.Version == m.Version())
                        result = await context.Dispatcher(context.ProcessingUnit, m, e);
                }
                else
                {
                    result = await context.Dispatcher(context.ProcessingUnit, m, e);
                }
            }
            catch (ProcessingException ex)
            {
                // we need to write the response.
                var recordType = e.GetType();
                var exceptionType = ex.Payload.GetType();
                var et = ex.Payload;

                if (e is ICommand)
                    await context.EventStore.GetCommandStream(recordType, m.StreamId(), context).Append(et); 
                else 
                    await context.EventStore.GetEventStream(recordType, m.StreamId(), context).Append(et);
            }
            

            if (!result.IsEmpty)
            {
                if (context.Config.IsEventEmitEnabled)
                    foreach (var (nm, nev) in result.Events)
                    {
                        if (nev is IStreamAware le) 
                        {
                            await context.EventStore.GetStream(le.StreamCategory, nm, context)
                                .Append(nev, context);
                        }
                        else
                        {
                           await context.EventStore.GetEventStream(nev.GetType(), nm, context)
                                .Append(nev,context);
                        }
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
            if (context.Config.RequiresCurrentVersion)
            {
                if (context.Version == m.Version())
                    context.SynchronizationContext.Send(x => result += context.Dispatcher(context.ProcessingUnit, m, e).GetAwaiter().GetResult(), null);
            }
            else
            {
                context.SynchronizationContext.Send(x => result += context.Dispatcher(context.ProcessingUnit, m, e).GetAwaiter().GetResult(), null);
            }
            
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