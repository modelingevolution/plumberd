using System;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public interface IRecordSerializer
    {
        bool IsJson(IMetadata m);
        byte[] Serialize(IRecord ev, IMetadata m);
        IRecord Deserialize(ReadOnlyMemory<byte> data, IMetadata m);
    }
}