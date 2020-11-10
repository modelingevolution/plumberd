using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    public interface IMetadataSchema 
    {
        public IEnumerable<MetadataProperty> Properties { get; }
        public IReadOnlyList<MetadataProperty> WriteProperties { get; } // More than read
        public int Count { get; }
        MetadataProperty this[string name] { get; }
        
        IReadOnlyDictionary<Type, IMetadataEnricher> Enrichers { get; }

        
        MetadataProperty RegisterSystem(MetadataProperty prop);
        MetadataProperty Register(string propertyName, Type propertyType, IMetadataEnricher enricher, bool persistable);
        MetadataProperty Register<TPropertyType>(string propertyName, IMetadataEnricher enricher, bool persistable);
    }
}