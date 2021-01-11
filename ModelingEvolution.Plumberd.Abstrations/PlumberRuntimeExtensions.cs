using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd
{
    public static class PlumberRuntimeExtensions
    {
        public static void RegisterControllers(this IPlumberRuntime runtime, 
            Assembly a, 
            IServiceProvider sp = null, 
            LifetimeMode mode = LifetimeMode.Singleton,
            SynchronizationContext context = null)
        {
            Func<Type, object> factory = runtime.DefaultServiceProvider.GetService;
            if (sp != null)
                factory = sp.GetService;

            var types = a.GetTypes()
                .Where(x => x.GetCustomAttribute<ProcessingUnitConfigAttribute>() != null)
                .Where(x => x.IsClass && !x.IsAbstract)
                .ToArray();

            if (mode == LifetimeMode.Singleton)
            {
                foreach (var t in types)
                {
                    var instance = factory(t);
                    runtime.RegisterController(instance, context: context);
                }
            }
            else
            {
                foreach (var t in types)
                {
                    runtime.RegisterController(t, controllerFactory:factory, context:context);
                }
            }
        }
        public static IPlumberRuntime RegisterController<T>(this IPlumberRuntime runtime,
            Func<Type, object> factory = null,
            IProcessingUnitConfig config = null,
            IEventHandlerBinder binder = null,
            ICommandInvoker invoker = null,
            IEventStore eventStore = null,
            SynchronizationContext context = null)
        {
            runtime.RegisterController(typeof(T), factory, config, binder, invoker, eventStore, context);
            return runtime;
        }

    }
}