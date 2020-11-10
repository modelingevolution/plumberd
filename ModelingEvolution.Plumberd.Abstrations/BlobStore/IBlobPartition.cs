using System;
using System.Collections.Generic;
using System.IO;

namespace ModelingEvolution.Plumberd.BlobStore
{
    public interface IBlobPartition
    {
        string Category { get; }
        Guid Id { get; }
        bool BlobExists(string name);
        string GetBlobUrl(string name);
        void SaveBlob(string name, byte[] data);
        void SaveBlob(string name, Stream rSteam);
        IEnumerable<IBlob> GetBlobs();
    }
}