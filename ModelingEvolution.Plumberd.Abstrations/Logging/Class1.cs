using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


// TODO: Extract this to a nuget.
namespace ModelingEvolution.Plumberd.Logging
{

    public sealed class LogFactory
    {
        class DelayedLogger<T> : DelayedLogger, ILogger<T>
        {
            public DelayedLogger([NotNull] Func<ILogger> factory) : base(factory)
            {
                Category = typeof(T).FullName + Environment.NewLine + "\t";
            }
        }
        class DelayedLogger : ILogger, IDisposable
        {
            private ILogger _logger;
            private Func<ILogger> _factory;

            public DelayedLogger(Func<ILogger> factory)
            {
                _factory = factory;
                Category = string.Empty;
            }

            protected string Category;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (_logger != null)
                    _logger.Log(logLevel, eventId, state, exception, formatter);
                _logger = _factory();
                if (_logger != null)
                    _logger.Log(logLevel, eventId, state, exception, formatter);
                else
                {
                    Console.WriteLine($"{Level(logLevel)}: {Category}{exception?.Message}");
                }
            }

            private string Level(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Trace:
                        return "trace";

                    case LogLevel.Debug:
                        return "debug";

                    case LogLevel.Information:
                        return "info";

                    case LogLevel.Warning:
                        return "warn";

                    case LogLevel.Error:
                        return "error";

                    case LogLevel.Critical:
                        return "crit";

                    case LogLevel.None:
                        return "none";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(level), level, null);
                }
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return this;
            }

            public void Dispose()
            {
            }
        }
        private IServiceProvider _serviceProvider;
        private static LogFactory _instance = new LogFactory();

        private LogFactory()
        {
        }

        public static ILogger GetLogger()
        {
            return _instance._GetLogger<object>();
        }
        private ILogger _GetLogger<T>()
        {
            if (_serviceProvider != null)
                return _serviceProvider.GetRequiredService<ILogger<T>>();
            else return new DelayedLogger<T>(() => _serviceProvider?.GetRequiredService<ILogger<T>>());
        }
        public static ILogger GetLogger<T>()
        {
            return _instance._GetLogger<T>();
        }
        public static void Init(IServiceProvider serviceProvider)
        {
            _instance._serviceProvider = serviceProvider;
        }

    }
}
