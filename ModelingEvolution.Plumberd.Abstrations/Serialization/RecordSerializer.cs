using System;
using System.Text;
using System.Text.Json;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
    public sealed class RecordSerializer : IRecordSerializer
    {
        private JsonSerializerOptions _options;
        public RecordSerializer()
        {
            _options = new JsonSerializerOptions();
            _options.Converters.Add(new JsonTimeSpanConverter());
            
        }
        //private static readonly JsonSerializerSettings JSON_SETTINGS = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };
        public byte[] Serialize(IRecord ev, IMetadata m)
        {
            if (ev is ILink l)
            {
                return Encoding.UTF8.GetBytes($"{l.SourceStreamPosition}@{l.SourceCategory}-{l.SourceStreamId}");
            } 
            else 
                return JsonSerializer.SerializeToUtf8Bytes(ev, ev.GetType(), _options);
        }

        public IRecord Deserialize(byte[] data, IMetadata m)
        {
            return (IRecord)JsonSerializer.Deserialize(data.AsSpan(), m.TryResolveNativeType(), _options);
        }
    }
}