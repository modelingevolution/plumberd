using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using Microsoft.Extensions.Logging;


namespace ModelingEvolution.Plumberd
{
    public interface ICommandInvoker
    {
        Task Execute(Guid id, ICommand c, IContext context = null);
        Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId, Version v = null);
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
        private readonly Version _defaultVersion;

        private readonly ILogger _logger;

        public CommandInvoker(IEventStore eventStore, Version defaultVersion)
        {
            _eventStore = eventStore;
            _defaultVersion = defaultVersion;
            _logger = eventStore.Settings.LoggerFactory.CreateLogger<CommandInvoker>();
        }
     

        public async Task Execute(Guid id, ICommand c, IContext context = null)
        {
            Type commandType = c.GetType();
            var info = GetStreamName(commandType, c);

            context ??= StaticProcessingContext.Context;

            if (context == null)
            {
                // this is brand new invocation.
                context = new CommandInvocationContext(id,c, Guid.Empty, Guid.Empty, info.Version ?? _defaultVersion);
            }
            
            var stream = _eventStore.GetStream($"{_eventStore.Settings.CommandStreamPrefix}{info.Category}", id, context);
            _logger.LogInformation("Invoking command {commandType} from context {contextName}", c.GetType().Name, context.GetType().Name);

            await stream.Append(c, context);
        }

        record StreamInfo(string Category, Version Version);
        private StreamInfo GetStreamName(Type commandType, ICommand c)
        {
            if (c is IStreamAware sa)
            {
                return new StreamInfo(sa.StreamCategory, _defaultVersion);
            }
            
            var streamAttr = commandType.GetCustomAttribute<StreamAttribute>();
            return new StreamInfo(streamAttr != null ? streamAttr.Category : commandType.Namespace.LastSegment('.'),
                    streamAttr != null ? streamAttr.Version : _defaultVersion);
            
        }

        

        public Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId, Version v = null)
        {
            return Execute(id, c, new CommandInvocationContext(id, c, userId, sessionId, v));
        }
    }
}