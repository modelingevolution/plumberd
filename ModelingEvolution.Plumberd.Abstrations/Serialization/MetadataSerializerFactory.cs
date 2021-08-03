using System;
using System.Collections.Generic;
using System.Linq;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public class MetadataSerializerFactory : IMetadataSerializerFactory
    {
        private readonly Dictionary<ContextScope, IMetadataSerializer> _schemas;
        private Lazy<IMetadataSerializer> _genericMetadataSerializer;
        public MetadataSerializerFactory()
        {
            _schemas = new Dictionary<ContextScope, IMetadataSerializer>();
            _genericMetadataSerializer = new Lazy<IMetadataSerializer>(CreateGenericSerializer);
        }

        private IMetadataSerializer CreateGenericSerializer()
        {
            var enrichers = _schemas.Values
                .SelectMany(x => x.Schema.Enrichers.Values.Select(x=>x.Clone()))
                .ToArray();
                                
            var schema = new MetadataSchema();
            schema.IgnoreDuplicates();

            foreach(var i in enrichers)
                i.RegisterSchema(schema);
            
            return new MetadataSerializer(schema);
        }

        public IMetadataSerializer Get(ContextScope context)
        {
            return _schemas[context];
        }
        public IMetadataSerializer Get(IContext context)
        {
            if (context is ICommandHandlerContext)
                return _schemas[ContextScope.Command];
            else if (context is IEventHandlerContext)
                return _schemas[ContextScope.Event];
            else if (context is ICommandInvocationContext)
                return _schemas[ContextScope.Invocation];
            return _genericMetadataSerializer.Value;
        }
        public void RegisterSerializerForContext(IMetadataSerializer schema, ContextScope contextType)
        {
            _schemas.Add(contextType, schema);
        }
        public void RegisterSchemaForContext(IMetadataSchema schema, ContextScope contextType)
        {
            RegisterSerializerForContext(new MetadataSerializer(schema),contextType );
        }
    }
}