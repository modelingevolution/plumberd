using Checkers.Common.Validation;
using Microsoft.Extensions.Logging.Console;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Querying;
using ModelingEvolution.Plumberd.Serialization;
using ModelingEvolution.Plumberd.StateTransitioning;
using ProtoBuf.Meta;

namespace Checkers
{
    sealed class ServiceProviderProxy : IServiceProvider
    {
        private IServiceProvider _provider;
        public void SetProvider(IServiceProvider provider) => _provider = provider;
        public object GetService(Type serviceType)
        {
            return _provider?.GetService(serviceType);
        }
    }


    class LazyLogProvider : ILoggerFactory
    {
        IServiceProvider _provider;

        class ConsoleLogger : ILogger, IDisposable
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return this;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, 
                EventId eventId, 
                TState state, 
                Exception exception, 
                Func<TState, Exception, string> formatter)
            {
                var line = formatter(state, exception);
                Console.WriteLine(line);
            }

            public void Dispose()
            {
            }
        }
        
        public LazyLogProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public void Dispose()
        {
            
        }

        public void AddProvider(ILoggerProvider provider)
        {
            
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _provider.GetService<ILogger>() ?? new ConsoleLogger();
        }
    }
    public static class Startup
    {
        
        private static IPlumberRuntime _plumberRuntime;

        public static void AddCheckers(this IServiceCollection services)
        {
            services.AddPlumberd(x => x.WithGrpc(y => y.IgnoreServerCert().InSecure().WithDevelopmentEnv(true)));

            
            services.AddSingleton<ValidatorFactory>();

            _processingUnitTypes = GetTypes()
                .HavingAttribute<ProcessingUnitConfigAttribute>().ToArray();

            services.AddSingletons(_processingUnitTypes);
            services.AddSingletons(GetTypes().IsAssignableToClass<IModel>());

            services.RegisterControllers(_processingUnitTypes);

            bool hasAggregates = false;
            foreach (var at in GetTypes().IsAssignableToClass<IRootAggregate>())
            {
                var invokerService = typeof(IAggregateInvoker<>).MakeGenericType(at);
                var implementationType = typeof(AggregateInvoker<>).MakeGenericType(at);
                services.AddSingleton(invokerService, implementationType);
                hasAggregates = true;
            }
            if (hasAggregates)
                services.Decorate(typeof(IAggregateInvoker<>), typeof(AggregateInvokerValidationDecorator<>));

            services.AddSingleton(new TypeRegister().Index(GetTypes().IsAssignableToClass<ICommand>()));
            RuntimeTypeModel.Default.RegisterReverseInheritanceFrom(GetTypes());
            services.AddSingleton(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
            services.Decorate<ICommandInvoker, ValidationCommandInvokerDecorator>();
            GetTypes().Where(x => typeof(FluentValidation.IValidator).IsAssignableFrom(x))
                .Select(x => x.GetGenericInterfaceArgument(typeof(FluentValidation.IValidator<>)))
                .Where(x => x.IsImplemented)
                .ExecuteForAll(x =>
                {

                    services.AddSingleton(x.ConcreteInterface, x.ImplementationType);
                    services.AddSingleton(typeof(IValidatorAdapter<>).MakeGenericType(x.ArgumentType), typeof(ValidatorAdapter<>).MakeGenericType(x.ArgumentType));
                });
            services.AddScoped<ILiveQueryExecutor, LiveQueryExecutor>();
        }

       
        private static Type[] _types;
        private static Type[] _processingUnitTypes;

        public static Type[] GetTypes()
        {
            if (_types != null)
                return _types;
            _types = typeof(Startup).Assembly.GetTypes();
            return _types;
        }
    }
}
