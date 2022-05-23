using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ProtoBuf;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    public class MetadataSerializerTests
    {
        [Fact]
        public void CanDeserialize()
        {
            string json =
                "{\r\n  \"$correlationId\": \"49fc3411-7421-48ad-bc7c-6310b4afd26c\",\r\n  \"$causationId\": \"49fc3411-7421-48ad-bc7c-6310b4afd26c\",\r\n  \"hop\": 2,\r\n  \"UserId\": \"00000000-0000-0000-0000-000000000000\",\r\n  \"SessionId\": \"00000000-0000-0000-0000-000000000000\",\r\n  \"Type\": \"Modellution.CodingServices.Server.Domain.Renderers.SourceRendererRegistered, Modellution.CodingServices.Server, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\",\r\n  \"ProcessingUnit\": \"SourceRendererCommandHandler\",\r\n  \"Created\": \"2022-05-22T19:52:41.1612563+00:00\"\r\n, \r\n  \"Complex\": { \"Name\":\"Value\" }\r\n}";

            var schema = new MetadataSchema();
            schema.IgnoreDuplicates();

            var enrichers = new List<IMetadataEnricher>()
            {
                new CorrelationEnricher(),
                new CreateTimeEnricher(),
                new DictionaryEnricher(),
                new UserIdEnricher()
            };
            foreach (var i in enrichers)
                i.RegisterSchema(schema);

            MetadataSerializer serializer = new MetadataSerializer(schema);
            var metadata = serializer.Deserialize(Encoding.UTF8.GetBytes(json));
        }
    }
    public class SerializationTests
    {
        [Fact]
        public void CanSerializeDateTimeOffset()
        {
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(1024);
            
            DateTimeOffset t = DateTimeOffset.Now;

            Serializer.Serialize(buffer, t.DateTime);
            Serializer.Serialize(buffer, t.Offset);

            var data = buffer.WrittenMemory;

            var dt = Serializer.Deserialize<DateTime>(data.Slice(0,13));
            var ts = Serializer.Deserialize<TimeSpan>(data.Slice(13,6));

            DateTimeOffset a = new DateTimeOffset(dt,ts);

            a.Should().Be(t);
        }

        [Fact]
        public void CanSerializeDateTimeOffset2()
        {
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(1024);

            DateTimeOffset t = DateTimeOffset.Now;
            
            if(!BitConverter.TryWriteBytes(buffer.GetSpan(sizeof(Int64)), t.DateTime.Ticks))
                throw new InternalBufferOverflowException();
            buffer.Advance(sizeof(Int64));
            if (!BitConverter.TryWriteBytes(buffer.GetSpan(sizeof(Int64)), t.Offset.Ticks))
                throw new InternalBufferOverflowException();
            buffer.Advance(sizeof(Int64));

            var data = buffer.WrittenMemory;

            var dt = BitConverter.ToInt64(data.Span.Slice(0, sizeof(Int64)));
            var ts = BitConverter.ToInt64(data.Span.Slice(sizeof(Int64), sizeof(Int64)));

            DateTimeOffset a = new DateTimeOffset(new DateTime(dt), new TimeSpan(ts));

            a.Should().Be(t);
        }
    }
}