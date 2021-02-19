using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class SystemMetadataEnricherExtensions
    {
        public static ulong StreamPosition(this IMetadata m)
        {
            return (ulong) m[MetadataProperty.StreamPosition];
        }
        public static Guid StreamId(this IMetadata m)
        {
            return (Guid) m[MetadataProperty.StreamId];
        }

        public static string Category(this IMetadata m)
        {
            return (string) m[MetadataProperty.Category];
        }
    }
}