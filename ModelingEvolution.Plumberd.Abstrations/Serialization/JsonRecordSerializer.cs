using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization;

public sealed class JsonRecordSerializer : IRecordSerializer
{
    public bool IsJson(IMetadata m) => true;
    private class SerializerInfo{}
    private readonly JsonSerializerOptions _options;
        
    public JsonRecordSerializer()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new JsonTimeSpanConverter());
        _options.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString;
    }
    public byte[] Serialize(IRecord ev, IMetadata m)
    {
        return ev is ILink l
            ? Encoding.UTF8.GetBytes($"{l.SourceStreamPosition}@{l.SourceCategory}-{l.SourceStreamId}")
            : JsonSerializer.SerializeToUtf8Bytes(ev, ev.GetType(), _options);
    }

    public IRecord Deserialize(ReadOnlyMemory<byte> data, IMetadata m)
    {
        var type = m.TryResolveNativeType();
        return (IRecord)JsonSerializer.Deserialize(data.Span, type, _options);
    }
     
}