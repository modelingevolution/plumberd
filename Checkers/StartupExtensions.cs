using Checkers.Common.Validation;
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
            return _provider.GetService(serviceType);
        }
    }
    public static class Startup
    {
        private static ServiceProviderProxy _serviceProvider;
        private static IPlumberRuntime _plumberRuntime;

        public static void AddCheckers(this IServiceCollection services,
            IConfiguration Configuration, bool isDevelopment)
        {
            _serviceProvider = new ServiceProviderProxy();

            var b = new PlumberBuilder()
                .WithDefaultServiceProvider(_serviceProvider)
                .WithLoggerFactory(LoggerFactory.Create(s => {}))
                .WithGrpc(x => x
                    .WithConfig(Configuration)
                    .WithWrittenEventsToLog(isDevelopment)
                    .IgnoreServerCert() // <---
                    .InSecure()
                    .WithDevelopmentEnv(isDevelopment)
                    .WithProjectionsConfigFrom(typeof(Startup).Assembly));
            _plumberRuntime = b.Build();
            services.AddSingleton(_plumberRuntime.DefaultCommandInvoker);
            services.AddSingleton(_plumberRuntime.DefaultEventStore);
            services.AddSingleton(_plumberRuntime);

            services.AddSingleton<ValidatorFactory>();

            _processingUnitTypes = GetTypes()
                .HavingAttribute<ProcessingUnitConfigAttribute>().ToArray();

            services.AddSingletons(_processingUnitTypes);
            services.AddSingletons(GetTypes().IsAssignableToClass<IModel>());

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

        public static void ConfigureCheckers(this WebApplication app)
        {
            _serviceProvider.SetProvider(app.Services);

            foreach (var pu in _processingUnitTypes)
                _plumberRuntime.RegisterController(pu);

            Task.Run(() => _plumberRuntime.StartAsync()).GetAwaiter().GetResult();
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
