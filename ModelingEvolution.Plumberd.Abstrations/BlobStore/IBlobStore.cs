using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.BlobStore
{
    public interface IBlobStore
    {
        IBlobPartition GetPartition(string category, Guid id);
        string GetUrl(string category, Guid id);
    }
}