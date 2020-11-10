using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Serialization;
using System.Threading;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using Serilog;

namespace ModelingEvolution.Plumberd
{
   

    public class PlumberBuilder
    {
        public PlumberBuilder()
        {
            Logger = Serilog.Core.Logger.None;
        }
        public ILogger Logger { get; private set; }
        public IServiceProvider DefaultServiceProvider { get; private set; }
        private ICommandInvoker DefaultCommandInvoker { get;  set; }
        private IEventStore DefaultEventStore { get;  set; }
        private SynchronizationContext DefaultSynchronizationContext { get;  set;}
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

        public PlumberBuilder WithLogger(ILogger logger)
        {
            Logger = logger;
            return this;
        }
        public IPlumberRuntime Build()
        {
            if (DefaultCommandInvoker == null)
            {
                var factory = new CommandInvokerMetadataFactory();
                
                DefaultCommandInvoker = new CommandInvoker(DefaultEventStore, Logger);
            }
            

            // validate
            return new PlumberRuntime(DefaultCommandInvoker, 
                DefaultEventStore, 
                DefaultSynchronizationContext,
                DefaultServiceProvider);
        }

        public PlumberBuilder WithDefaultServiceProvider(IServiceProvider serviceProvider)
        {
            DefaultServiceProvider = serviceProvider;
            return this;
        }
    }

    internal class EventHandlerContext : IEventHandlerContext
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

        public CommandInvocationContext(Guid id, ICommand command)
        {
            Id = id;
            Command = command;
        }

        public void Dispose()
        {
        }
    }
    public interface ICommandInvocationContext : IContext
    {
        Guid Id { get; }
        ICommand Command { get; }

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