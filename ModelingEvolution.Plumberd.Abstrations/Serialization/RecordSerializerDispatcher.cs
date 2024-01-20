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
    public bool IsJson(IMetadata m)
    {
        Type t = m.TryResolveNativeType();
        return t == (Type)null || this.Find(t).IsJson(m);
    }

    public byte[] Serialize(IRecord ev, IMetadata m) => this.Find(m.TryResolveNativeType(ev.GetType())).Serialize(ev, m);


    private IRecordSerializer Find(Type t) => this._cache.GetOrAdd(t, (Func<Type, IRecordSerializer>)(x => t.GetCustomAttribute<UseProtoAttribute>() == null ? (IRecordSerializer)this._json : (IRecordSerializer)this._proto));

    public IRecord Deserialize(ReadOnlyMemory<byte> data, IMetadata m) => Find(m.TryResolveNativeType()).Deserialize(data,m);
}