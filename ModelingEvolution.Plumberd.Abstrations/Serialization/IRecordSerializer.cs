﻿using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public interface IRecordSerializer
    {
        byte[] Serialize(IRecord ev, IMetadata m);
        IRecord Deserialize(byte[] data, IMetadata m);
    }
}