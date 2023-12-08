using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ModelingEvolution.Plumberd.Metadata
{
    public sealed class MetadataSchema : IMetadataSchema
    {
        private IMetadataSchema _system;
        public IMetadataSchema System
        {
            get
            {
                if (_system == null)
                {
                    _system = new MetadataSchema();
                    _system.RegisterSystem(MetadataProperty.Category());
                    _system.RegisterSystem(MetadataProperty.StreamId());
                    _system.RegisterSystem(MetadataProperty.StreamPosition());
                    _system.RegisterSystem(MetadataProperty.LinkPosition());
                }

                return _system;
            }
        }
        private readonly SortedList<string, MetadataProperty> _properties;
        private readonly List<MetadataProperty> _writeProperties;
        private readonly Dictionary<Type, IMetadataEnricher> _enrichers;
        private int _order;

        private bool _ignoreDuplicates;
        //private const int _reserved = 2;
        public MetadataSchema()
        {
            _properties = new SortedList<string, MetadataProperty>();
            _writeProperties = new List<MetadataProperty>();
            _enrichers = new Dictionary<Type, IMetadataEnricher>();
            _order = 0;
        }

        public IReadOnlyDictionary<Type, IMetadataEnricher> Enrichers => _enrichers;
        
        public MetadataProperty this[string name] => _properties.TryGetValue(name, out var result) ? result : null;
        public IEnumerable<MetadataProperty> Properties => _properties.Values;
        public IReadOnlyList<MetadataProperty> WriteProperties
        {
            get => _writeProperties;
        }

        public int Count
        {
            get => _properties.Count;
        }
        
        public MetadataProperty RegisterSystem(MetadataProperty prop)
        {
            _properties.Add(prop.Name, prop);
            prop.Order = _order++;
            if (prop.IsPersistable)
                _writeProperties.Add(prop);
            return prop;
        }

        public MetadataProperty Register(string propertyName, Type propertyType, IMetadataEnricher enricher,
            bool persistable)
        {
            if (!_properties.ContainsKey(propertyName))
            {
                if (enricher != null && !_enrichers.ContainsKey(enricher.GetType()))
                    _enrichers.Add(enricher.GetType(), enricher);

                MetadataProperty p = new MetadataProperty(propertyName, propertyType, _order++, enricher, persistable);
                _properties.Add(propertyName, p);

                if (persistable)
                    _writeProperties.Add(p);

                return p;
            }
            else
            {
                if (!_ignoreDuplicates)
                    throw new DuplicateNameException($"Property name '{propertyType}' is already registered.");
                return _properties[propertyName];
            }
        }
    

        public MetadataProperty Register<TPropertyType>(string propertyName, IMetadataEnricher enricher, bool persistable)
        {
            return Register(propertyName, typeof(TPropertyType), enricher, persistable);
        }

        public void IgnoreDuplicates()
        {
            _ignoreDuplicates = true;
        }
    }
}