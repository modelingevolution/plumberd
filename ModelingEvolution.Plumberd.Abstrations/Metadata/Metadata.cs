using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public readonly struct Metadata : IMetadata
    {
        private readonly IMetadataSchema _schema;
        private readonly object[] _data; // fixed?
        public Metadata(IMetadataSchema schema, bool read)
        {
            _schema = schema;
            _data = new object[read ? _schema.Count : _schema.WriteProperties.Count];
        }

        public IMetadataSchema Schema => _schema;
        public object this[int index]
        {
            get => _data[index];
            set
            {
                if (_data[index] != null)
                    throw new InvalidOperationException("Cannot override metadata.");
                _data[index] = value;
            }
        }
        public object this[MetadataProperty property]
        {
            get => this[property.Order];
            set => this[property.Order] = value;
        }

        public ILink Link(string destinationCategory)
        {
            string category = this.Category();
            Guid streamId = this.StreamId();
            ulong streamPosition = this.StreamPosition();
            return new LinkEvent(category, 
                streamId, 
                streamPosition, 
                destinationCategory);
        }
    }
}