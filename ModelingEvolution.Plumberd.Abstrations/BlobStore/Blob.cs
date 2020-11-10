using System.IO;

namespace ModelingEvolution.Plumberd.BlobStore
{
    public interface IBlob
    {
        Stream Open();
        string Name { get; }
    }
}