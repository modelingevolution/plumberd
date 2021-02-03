using System;
using System.Buffers;
using System.Threading.Tasks;
using FluentAssertions;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ModelingEvolution.Plumberd.Tests.Models;
using ProtoBuf;
using Xunit;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Tests
{
    
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
    }
    public class NativeEventStoreBuilderTests
    {
        [Fact]
        public void Build()
        {
            NativeEventStoreBuilder s = new NativeEventStoreBuilder()
                .WithMetadataFactory(new MetadataFactory())
                .WithMetadataSerializerFactory(new MetadataSerializerFactory())
                .WithRecordSerializer(new RecordSerializer());

        }
    }
    public class TransitionUnitTests
    {
        [Fact]
        public async Task WhenReturnsEnumerable()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            var events = sut.Execute(new Command1());

            events.Should().HaveCount(1);
            events[0].Should().BeOfType<Event1>();
        }

        [Fact]
        public void Given()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            sut.Rehydrate(new[] { new Event1() });

            sut.GetState().Name.Should().Be("Foo");
        }
        [Fact]
        public void GivenMany()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            sut.Rehydrate(new[] { new Event1(), new Event1(), new Event1() });

            sut.GetState().Name.Should().Be("Foo");
        }

        [Fact]
        public void WhenReturnsEvent()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            var events = sut.Execute(new Command2());

            events.Should().HaveCount(1);
            events[0].Should().BeOfType<Event2>();
        }

    }
}