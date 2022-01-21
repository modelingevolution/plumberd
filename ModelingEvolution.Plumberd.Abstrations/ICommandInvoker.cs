using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Logging;

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
            _schema.RegisterSystem(MetadataProperty.Category());
            _schema.RegisterSystem(MetadataProperty.StreamId());
        }
        public virtual IMetadata CreateMetadata(Guid id, ICommand c, string streamName, IContext context)
        {
            Metadata.Metadata m = new Metadata.Metadata(_schema,false);
            m[m.Schema[MetadataProperty.CategoryName]] = streamName;
            m[m.Schema[MetadataProperty.StreamIdName]] = id;
            
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

        private static readonly ILogger _logger = LogFactory.GetLogger<CommandInvoker>();

        public CommandInvoker(IEventStore eventStore)
        {
            _eventStore = eventStore;

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
            _logger.LogInformation("Invoking command {commandType} from context {contextName}", c.GetType().Name, context.GetType().Name);
            
            string name = GetStreamName(commandType,c);
            
            var stream = _eventStore.GetStream($"{_eventStore.Settings.CommandStreamPrefix}{name}", id, context);
            await stream.Append(c, context);
        }

        private string GetStreamName(Type commandType, ICommand c)
        {
            if (c is IStreamAware sa)
                return sa.StreamCategory;
            
            var streamAttr = commandType.GetCustomAttribute<StreamAttribute>();
            return streamAttr != null ? streamAttr.Category : commandType.Namespace.LastSegment('.');
            
        }

        public Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId)
        {
            return Execute(id, c, new CommandInvocationContext(id, c, userId, sessionId));
        }
    }
}