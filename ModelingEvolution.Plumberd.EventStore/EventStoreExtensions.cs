using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.EventProcessing;

[assembly: InternalsVisibleTo("ModelingEvolution.Plumberd.Tests")]

namespace ModelingEvolution.Plumberd.EventStore
{
    public static class EventStoreExtensions
    {
        public static PlumberBuilder WithGrpc(this PlumberBuilder builder, 
            Func<ConfigurationBuilder, ConfigurationBuilder> configureEventStore, 
            bool checkConnectivity = true)
        {
            ConfigurationBuilder b = new ConfigurationBuilder();
            b.WithLoggerFactory(builder.DefaultLoggerFactory);
            b = configureEventStore(b);
            
            return builder.WithDefaultEventStore(b.Build(checkConnectivity));
        }
        
    }
    public class PlumberdProjectionStarter : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public PlumberdProjectionStarter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var plumberd = _serviceProvider.GetRequiredService<IPlumberRuntime>();
            var processingUnits = _serviceProvider.GetRequiredService<IEnumerable<IProcessingUnitRegistration>>();
            foreach (var r in processingUnits)
                plumberd.RegisterController(r.ProcessingUnit);
            await plumberd.StartAsync();
        }
    }
    interface IProcessingUnitRegistration { Type ProcessingUnit { get; }}
    record ProcessingUnitRegistration(Type ProcessingUnit) : IProcessingUnitRegistration { }

    public static class ContainerExtensions
    {
        public static IServiceCollection RegisterController(this IServiceCollection services, Type processingUnit)
        {

            services.AddSingleton(typeof(IProcessingUnitRegistration), new ProcessingUnitRegistration(processingUnit));
            return services;
        }
        public static IServiceCollection RegisterControllers(this IServiceCollection services, params Type[] processingUnits)
        {
            foreach (var i in processingUnits) services.RegisterController(i);
            return services;
        }
        public static IServiceCollection RegisterController<TController>(this IServiceCollection services)
        {
            return services.RegisterController(typeof(TController));
        }
        public static IServiceCollection AddPlumberd(this IServiceCollection services, Action<PlumberBuilder> builder)
        {
            // Check in WASM - this might not work.
            services.AddHostedService<PlumberdProjectionStarter>();
            
            services.AddSingleton<IPlumberRuntime>(sp =>
            {
                PlumberBuilder b = new PlumberBuilder()
                    .WithDefaultServiceProvider(sp)
                    .WithLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .WithGrpc(x => x.WithConfig(sp.GetRequiredService<IConfiguration>())
                        .WithLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .WithStartupProjections(StartupProjection.All));
                
                builder(b);
                return b.Build();
            });
            services.AddSingleton<ICommandInvoker>(sp => sp.GetRequiredService<IPlumberRuntime>().DefaultCommandInvoker);
            services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<IPlumberRuntime>().DefaultEventStore);
            services.AddSingleton<TypeRegister>();
            return services;
        }
    }
}