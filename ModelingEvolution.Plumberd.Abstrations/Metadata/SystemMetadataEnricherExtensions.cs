using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    
    public static class SystemMetadataEnricherExtensions
    {
       
        public static ulong StreamPosition(this IMetadata m)
        {
            return (ulong) m[m.Schema[MetadataProperty.StreamPositionName]];
        }
        public static Guid StreamId(this IMetadata m)
        {
            return (Guid) m[m.Schema[MetadataProperty.StreamIdName]];
        }

        public static string Category(this IMetadata m)
        {
            return (string) m[m.Schema[MetadataProperty.CategoryName]];
        }
    }
}