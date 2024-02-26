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
        public static ulong LinkPosition(this IMetadata m)
        {
            return (ulong)m[m.Schema[MetadataProperty.LinkPositionName]];
        }
        public static Guid StreamId(this IMetadata m)
        {
            var prop = m.Schema[MetadataProperty.StreamIdName];
            var o = m[prop];
            if (o is Guid guid) return guid;
            return Guid.Parse((string)o);
        }
        public static string StreamIdName(this IMetadata m)
        {
            return (string)m[m.Schema[MetadataProperty.StreamIdName]];
        }
        public static string Category(this IMetadata m)
        {
            return (string) m[m.Schema[MetadataProperty.CategoryName]];
        }
    }
}