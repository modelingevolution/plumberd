namespace ModelingEvolution.Plumberd.Metadata
{
    public static class MetadataSchemaExtensions
    {
        public static T Enricher<T>(this IMetadataSchema schema) where T : IMetadataEnricher
        {
            return (T)schema.Enrichers[typeof(T)];
        }
    }
}