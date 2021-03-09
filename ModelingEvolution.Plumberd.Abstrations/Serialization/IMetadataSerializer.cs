using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public interface IMetadataSerializer
    {
        IMetadataSchema Schema { get; }
        byte[] Serialize(IMetadata m);
        IMetadata Deserialize(byte[] data);
    }
}