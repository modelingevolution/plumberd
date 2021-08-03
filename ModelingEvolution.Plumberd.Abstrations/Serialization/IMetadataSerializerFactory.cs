using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public interface IMetadataSerializerFactory
    {
        IMetadataSerializer Get(IContext context);
        IMetadataSerializer Get(ContextScope context);
        void RegisterSchemaForContext(IMetadataSchema schema, ContextScope contextType);
        void RegisterSerializerForContext(IMetadataSerializer schema, ContextScope contextType);
    }
}