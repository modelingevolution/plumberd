using System;
using System.Text.Json;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public sealed class MetadataSerializer : IMetadataSerializer
    {
        public IMetadataSchema Schema { get; }
        private readonly JsonSerializerOptions _options;
        public MetadataSerializer(IMetadataSchema schema)
        {
            Schema = schema;
            _options = new JsonSerializerOptions();
            _options.Converters.Add(new MetadataJsonConverter(schema));
        }

        public byte[] Serialize( IMetadata m)
        {
            return JsonSerializer.SerializeToUtf8Bytes(m, typeof(IMetadata), _options);
        }
        
        public IMetadata Deserialize(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Deserialize<IMetadata>(data.Span, _options);
        }
    }
}