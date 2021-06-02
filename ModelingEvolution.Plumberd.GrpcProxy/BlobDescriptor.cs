using System;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public readonly struct BlobDescriptor
    {
        public readonly BlobUploadReason BlobUploadReason { get; init; }
        public readonly string FileName { get; init; }
        public readonly string Category { get; init; }
        public readonly Guid Sha1 { get; init; }
        public readonly Guid Id { get; init; }
        public readonly long Size { get; init; }
        public readonly int ChunkSize { get; init; }
        public readonly bool ForceOverride { get; init; }

        public BlobDescriptor(string fileName,
            string category,
            Guid sha1,
            Guid id,
            long size,
            int chunkSize, bool forceOverride, 
            BlobUploadReason blobUploadReason)
        {
            BlobUploadReason = blobUploadReason;
            FileName = fileName;
            Category = category;
            Sha1 = sha1;
            Id = id;
            Size = size;
            ChunkSize = chunkSize;
            ForceOverride = forceOverride;
        }

        public override string ToString()
        {
            return $"{nameof(FileName)}: {FileName}, {nameof(Category)}: {Category}, {nameof(Sha1)}: {Sha1}, {nameof(Id)}: {Id}, {nameof(Size)}: {Size}, {nameof(ChunkSize)}: {ChunkSize}, {nameof(ForceOverride)}: {ForceOverride}";
        }
    }
}