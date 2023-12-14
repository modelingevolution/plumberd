using System;
using System.Collections.Concurrent;
using System.Reflection;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization;

public sealed class RecordSerializerDispatcher : IRecordSerializer
{
    private readonly ProtoRecordSerializer _proto = new();
    private readonly JsonRecordSerializer _json = new();
    private readonly ConcurrentDictionary<Type, IRecordSerializer> _cache = new();
    public bool IsJson(IMetadata m) => Find(m.TryResolveNativeType()).IsJson(m);

    public byte[] Serialize(IRecord ev, IMetadata m) => Find(m.TryResolveNativeType()).Serialize(ev, m);

    private IRecordSerializer Find(Type t) => _cache.GetOrAdd(t,x => t.GetCustomAttribute<UseProtoAttribute>() != null ? _proto:_json);

    public IRecord Deserialize(ReadOnlyMemory<byte> data, IMetadata m) => Find(m.TryResolveNativeType()).Deserialize(data,m);
}