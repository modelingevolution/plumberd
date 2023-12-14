using System;
using System.Buffers;
using System.Text;
using ModelingEvolution.Plumberd.Metadata;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.Serialization
{
    public sealed class ProtoRecordSerializer : IRecordSerializer
    {
        public bool IsJson(IMetadata m) => false;

        public byte[] Serialize(IRecord ev, IMetadata m)
        {
            if (ev is ILink l)
                return Encoding.UTF8.GetBytes($"{l.SourceStreamPosition}@{l.SourceCategory}-{l.SourceStreamId}");
            else
            {
                ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();
                Serializer.Serialize(buffer, ev);
                return buffer.WrittenSpan.ToArray();
            }
        }

        public IRecord Deserialize(ReadOnlyMemory<byte> data, IMetadata m)
        {
            return (IRecord)Serializer.Deserialize(data.Span, m.TryResolveNativeType()); 
        }
    }
}