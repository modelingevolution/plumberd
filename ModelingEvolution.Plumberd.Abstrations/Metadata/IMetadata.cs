namespace ModelingEvolution.Plumberd.Metadata
{
    public interface IMetadata
    {
        IMetadataSchema Schema { get; }
        object this[MetadataProperty property] { get; set; }
        ILink Link(string destinationCategory);
    }
}