using System;
using System.Threading;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd
{
    public interface ICommandProcessingContextFactory
    {
        ICommandHandlerContext Create<TCommand>(object processingUnit,
            Guid id,
            TCommand cmd)
            where TCommand : ICommand;
    }

    public class CommandHandlerContext : ICommandHandlerContext
    {
        private readonly ProcessingContextFactory _parent;
        internal IEventHandlerBinder Binder => _parent.Binder;

        public HandlerDispatcher Dispatcher => _parent.Dispatcher;

        public IProcessingUnitConfig Config => _parent.Config;

        public SynchronizationContext SynchronizationContext => _parent.SynchronizationContext;

        public IEventStore EventStore => _parent.EventStore;

        public ICommandInvoker CommandInvoker => _parent.CommandInvoker;
        public IRecord Record { get; set; }
        public IMetadata Metadata { get; set; }

        public CommandHandlerContext(ProcessingContextFactory parent, object processingUnit, bool dispose)
        {
            _parent = parent;
            ProcessingUnit = processingUnit;
            ProcessingUnitType = processingUnit.GetType();
        }
        public CommandHandlerContext(object processingUnit, ICommand command, Guid id)
        {
            Record = command;
            Metadata = new Metadata.Metadata(MetadataSchema.System,true);
            Metadata[MetadataProperty.StreamId] = id;
            ProcessingUnit = processingUnit;
            ProcessingUnitType = processingUnit.GetType();
        }

        public void Dispose()
        {
            if (ProcessingUnit is IDisposable d)
                d.Dispose();
        }
        
        
        public Type ProcessingUnitType { get; }
        public object ProcessingUnit { get; }
    }
    public class CommandProcessingContextFactory : ICommandProcessingContextFactory
    {
        public ICommandHandlerContext Create<TCommand>(object processingUnit, Guid id, TCommand cmd)
            where TCommand : ICommand
        {
            return new CommandHandlerContext(processingUnit, cmd, id);
        }
    }
}