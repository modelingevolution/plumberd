﻿using System;
using System.Buffers;
using System.IO;
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