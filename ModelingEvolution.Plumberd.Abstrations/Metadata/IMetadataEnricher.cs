namespace ModelingEvolution.Plumberd.Metadata
{
    public interface IMetadataEnricher
    {
        void RegisterSchema(IMetadataSchema register);
        IMetadata Enrich(IMetadata m, IRecord e, IContext context);
        IMetadataEnricher Clone();
    }
}