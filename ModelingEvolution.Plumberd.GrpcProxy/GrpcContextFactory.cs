using System;
using System.Threading;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public class GrpcContextFactory : IProcessingContextFactory
    {
        private class EventHandlerContext : IEventHandlerContext
        {
            public void Dispose()
            {
                
            }

            public Type ProcessingUnitType { get; }
            public object ProcessingUnit { get; }
            public HandlerDispatcher Dispatcher { get; }
            public IProcessingUnitConfig Config { get; }
            public SynchronizationContext SynchronizationContext { get; }
            public IEventStore EventStore { get; }
            public ICommandInvoker CommandInvoker { get; }
            public IRecord Record { get; set; }
            public IMetadata Metadata { get; set; }
        }
        public IEventStore EventStore { get; }
        public IProcessingUnitConfig Config { get; }
        public ICommandInvoker CommandInvoker { get; }
        public ProcessingMode ProcessingMode { get; }
        public IProcessingContext Create()
        {
            return new EventHandlerContext();
        }
    }
}