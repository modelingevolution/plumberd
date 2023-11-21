using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            var app = builder.Build();
            var plumberBuilder = new PlumberBuilder()
                .WithDefaultServiceProvider(app.Services)
                .WithLoggerFactory(LoggerFactory.Create(config => config.AddConsole()))
                .WithGrpc(x => x
                    .WithCredentials("", "")
                    .WithHttpUrl(new Uri(""))
                    .InSecure()
                    .WithWrittenEventsToLog(true)
                    .IgnoreServerCert() // <---
                    .WithDevelopmentEnv(true));
         
            IPlumberRuntime plumberRuntime = plumberBuilder.Build();

            return app;
        }
    }
}
