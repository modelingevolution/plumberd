using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    public sealed class MetadataProperty
    {
        public const string CategoryName = "Category";
        public const string StreamPositionName = "StreamPosition";
        public const string LinkPositionName = "LinkPosition";
        public const string StreamIdName = "StreamId";
        
        
        public static MetadataProperty StreamId() => new MetadataProperty(StreamIdName, typeof(string), -1, null, false);
        public static MetadataProperty Category() => new MetadataProperty(CategoryName, typeof(String),-1, null, false);
        public static MetadataProperty StreamPosition() => new MetadataProperty(StreamPositionName, typeof(ulong), -1, null, false);
        public static MetadataProperty LinkPosition() => new MetadataProperty(LinkPositionName, typeof(ulong), -1, null, false);

        private int _order;

        public MetadataProperty(string name, 
            Type type, 
            int order, 
            IMetadataEnricher enricher, 
            bool isPersistable)
        {
            Name = name;
            Type = type;
            _order = order;
            Enricher = enricher;
            IsPersistable = isPersistable;
        }
        public bool IsPersistable { get; }
        public string Name { get;  }
        public Type Type { get; }
        public IMetadataEnricher Enricher { get; }

        public int Order
        {
            get => _order;
            internal set
            {
                if (_order == -1)
                    _order = value;
                else throw new ArgumentException();
            }
        }
    }
}