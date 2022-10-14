using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Logging;

namespace ModelingEvolution.Plumberd
{
    class NullLogger : ILogger, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }

        public void Dispose()
        {
        }
    }

    public class PlumberBuilder
    {
        public IServiceProvider DefaultServiceProvider { get; private set; }
        public Version DefaultVersion { get; set; } = new Version(0, 0);
        private ICommandInvoker DefaultCommandInvoker { get;  set; }
        private IEventStore DefaultEventStore { get;  set; }
        private SynchronizationContext DefaultSynchronizationContext { get;  set;}
        private Action<Exception> OnException { get; set; }
        
        private static readonly ILogger Logger = LogFactory.GetLogger<PlumberBuilder>();

        public PlumberBuilder WithSynchronizationContext(SynchronizationContext context)
        {
            DefaultSynchronizationContext = context;
            return this;
        }
        public PlumberBuilder WithCommandInvoker(ICommandInvoker commandInvoker)
        {
            DefaultCommandInvoker = commandInvoker;
            return this;
        }
        public PlumberBuilder WithDefaultEventStore(IEventStore eventStore)
        {
            DefaultEventStore = eventStore;
            return this;
        }

        public PlumberBuilder WithExceptionHook(Action<Exception> onException)
        {
            OnException = onException;
            return this;
        }
        public PlumberBuilder WithVersion(Version v)
        {
            DefaultVersion = v;
            return this;
        }
        public PlumberBuilder WithVersionFrom<T>()
        {
            DefaultVersion = typeof(T).Assembly.GetName().Version;
            return this;
        }
        public IPlumberRuntime Build()
        {
            if (DefaultCommandInvoker == null)
            {
                var factory = new CommandInvokerMetadataFactory();
                
                DefaultCommandInvoker = new CommandInvoker(DefaultEventStore, DefaultVersion);
            }

            if (DefaultServiceProvider == null)
            {
                DefaultServiceProvider = new ActivatorServiceProvider();
            }

            // validate
            return new PlumberRuntime(
                DefaultCommandInvoker, 
                DefaultEventStore, 
                DefaultSynchronizationContext,
                DefaultServiceProvider,
                DefaultVersion,
                OnException);
        }

        public PlumberBuilder WithDefaultServiceProvider(IServiceProvider serviceProvider)
        {
            DefaultServiceProvider = serviceProvider;
            return this; 
        }
    }

    class ActivatorServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            return Activator.CreateInstance(serviceType);
        }
    }

    public class EventHandlerContext : IEventHandlerContext
    {
        private readonly ProcessingContextFactory _parent;
        private readonly bool _dispose;

        public void Dispose()
        {
            if(_dispose && ProcessingUnit is IDisposable d)
                d.Dispose();
        }

        public EventHandlerContext(ProcessingContextFactory parent, object processingUnit, bool dispose)
        {
            _parent = parent;
            _dispose = dispose;
            ProcessingUnit = processingUnit;
        }
        public HandlerDispatcher Dispatcher => _parent.Dispatcher;
        public IProcessingUnitConfig Config => _parent.Config;
        public SynchronizationContext SynchronizationContext => _parent.SynchronizationContext;
        public IEventStore EventStore => _parent.EventStore;
        public ICommandInvoker CommandInvoker => _parent.CommandInvoker;
        public Version Version => _parent.Version;
        public IRecord Record { get; set; }
        public IMetadata Metadata { get; set; }
        public Type ProcessingUnitType => _parent.Type;
        public object ProcessingUnit { get; }
    }

    public interface IProcessingContextFactory : IProcessingUnit
    {
        IProcessingContext Create();
    }
    public interface IEventHandlerContext : IProcessingContext
    {
        
        
    }

    public interface IContext : IDisposable
    {

    }

    public class CommandInvocationContext : ICommandInvocationContext
    {
        public Guid Id { get;  }
        public ICommand Command { get;  }
        public Guid UserId { get; }
        public Guid ClientSessionId { get; }
        public Version Version { get; }
        public CommandInvocationContext(Guid id, ICommand command, Guid userId, Guid sessionId, Version version)
        {
            Id = id;
            Command = command;
            ClientSessionId = sessionId;
            UserId = userId;
            Version = version;
        }

        public void Dispose()
        {
        }
    }
    public interface ICommandInvocationContext : IContext
    {
        Guid Id { get; }
        ICommand Command { get; }
        Guid UserId { get; }
        Guid ClientSessionId { get; }
        Version Version { get; }
    }

    public interface ICommandHandlerContext : IProcessingContext
    {
        //public Guid Id { get; }
        //public ICommand Command { get; } // It's used in enrichers.
    }

    
    public interface IProcessingContext : IContext
    {
        Type ProcessingUnitType { get; }
        object ProcessingUnit { get; }
        HandlerDispatcher Dispatcher { get; }
        IProcessingUnitConfig Config { get; }
        SynchronizationContext SynchronizationContext { get; }
        IEventStore EventStore { get; }
        ICommandInvoker CommandInvoker { get; }
        Version Version { get; }
        IRecord Record { get; set; }  // We need to set it up.
        IMetadata Metadata { get; set; }
    }
    // this class is for create IEventMetadata on EventStore.Append
    public interface IMetadataFactory
    {
        IMetadataSchema Schema(ContextScope scope);
        void Register(Func<IMetadataEnricher> enricher, ContextScope scope = ContextScope.All);
        
        IMetadata Create(IRecord record, IContext context);
        void LockRegistration();
    }
    
   
    
    
}