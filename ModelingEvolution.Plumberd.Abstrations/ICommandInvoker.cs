using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using Serilog;

namespace ModelingEvolution.Plumberd
{
    public interface ICommandInvoker
    {
        Task Execute(Guid id, ICommand c, IContext context = null);
        Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId);
    }

    public interface ICommandInvokerMetadataFactory
    {
        IMetadata CreateMetadata(Guid id, ICommand c, string streamName, IContext context);
        void LockSchema();
        MetadataProperty Register(string propertyName, Type propertyType, IMetadataEnricher enricher, bool persistable);
    }

    public class CommandInvokerMetadataFactory : ICommandInvokerMetadataFactory
    {
        private readonly IMetadataSchema _schema;
        public CommandInvokerMetadataFactory()
        {
            _schema = new MetadataSchema();
        }

        public MetadataProperty Register(string propertyName, 
            Type propertyType, 
            IMetadataEnricher enricher, 
            bool persistable)
        {
            return _schema.Register(propertyName, propertyType, enricher, persistable);
        }

        public void LockSchema()
        {
            _schema.RegisterSystem(MetadataProperty.Category);
            _schema.RegisterSystem(MetadataProperty.StreamId);
        }
        public virtual IMetadata CreateMetadata(Guid id, ICommand c, string streamName, IContext context)
        {
            Metadata.Metadata m = new Metadata.Metadata(_schema,false);
            m[MetadataProperty.Category] = streamName;
            m[MetadataProperty.StreamId] = id;
            foreach (var i in _schema.WriteProperties)
            {
                if(context == null)
                    throw new ArgumentNullException(nameof(context));

                i.Enricher.Enrich(m, c, context);
            }
            return m;
        }
    }
    
    public class CommandInvoker : ICommandInvoker
    {
        private readonly IEventStore _eventStore;
        private ILogger _logger;
        public CommandInvoker(IEventStore eventStore, ILogger logger)
        {
            _eventStore = eventStore;
            _logger = logger;
        }
     

        public async Task Execute(Guid id, ICommand c, IContext context = null)
        {
            if (context == null)
                context = StaticProcessingContext.Context;
            if (context == null)
            {
                // this is brand new invocation.
                context = new CommandInvocationContext(id,c, Guid.Empty, Guid.Empty);
            }
            Type commandType = c.GetType();
            _logger.Information("Invoking command {commandType} from context {contextName}", c.GetType().Name, context.GetType().Name);
            string name = commandType.GetCustomAttribute<StreamAttribute>()?.Category ?? commandType.Namespace.LastSegment('.');
            var stream = _eventStore.GetStream($"{_eventStore.Settings.CommandStreamPrefix}{name}", id, context);
            await stream.Append(c, context);
        }

        public Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId)
        {
            return Execute(id, c, new CommandInvocationContext(id, c, userId, sessionId));
        }
    }
}