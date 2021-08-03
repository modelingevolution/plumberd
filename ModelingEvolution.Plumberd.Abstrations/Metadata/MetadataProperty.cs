using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    public sealed class MetadataProperty
    {
        public static readonly MetadataProperty StreamId = new MetadataProperty("StreamId", typeof(Guid), -1, null, false);
        public static readonly MetadataProperty Category = new MetadataProperty("Category", typeof(String),-1, null, false);
        public static readonly MetadataProperty StreamPosition = new MetadataProperty("StreamPosition", typeof(ulong), -1, null, false);
        
        public MetadataProperty(string name, 
            Type type, 
            int order, 
            IMetadataEnricher enricher, 
            bool isPersistable)
        {
            Name = name;
            Type = type;
            Order = order;
            Enricher = enricher;
            IsPersistable = isPersistable;
        }
        public bool IsPersistable { get; }
        public string Name { get;  }
        public Type Type { get; }
        public IMetadataEnricher Enricher { get; }
        public int Order { get; internal set; }
    }
}