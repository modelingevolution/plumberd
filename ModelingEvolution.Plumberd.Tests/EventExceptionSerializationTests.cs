using System.Buffers;
using System.Text.Json;
using FluentAssertions;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Tests.Models;
using ProtoBuf;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    public class EventExceptionSerializationTests
    {
        [Fact]
        public void CanSerializeWithProtoBuf()
        {
            var cmd = new SimpleCommand();
            var ex = new MyExceptionData() { Text = "123"};
            EventException<SimpleCommand, MyExceptionData> foo = new EventException<SimpleCommand, MyExceptionData>(cmd, ex);
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(1024);
            Serializer.Serialize(buffer, foo);

            var data = buffer.WrittenMemory;

            var dt = Serializer.Deserialize<EventException<SimpleCommand, MyExceptionData>>(data);

            dt.ExceptionData.Text.Should().Be(ex.Text);
            dt.Record.Id.Should().Be(cmd.Id);
        }

        [Fact]
        public void CanSerializeWithJson()
        {
            var cmd = new SimpleCommand();
            var ex = new MyExceptionData() {Text = "123"};
            EventException<SimpleCommand, MyExceptionData> foo = new EventException<SimpleCommand, MyExceptionData>(cmd, ex);

            string json = JsonSerializer.Serialize(foo);

            var dt = JsonSerializer.Deserialize<EventException<SimpleCommand, MyExceptionData>>(json);
            dt.ExceptionData.Text.Should().Be(ex.Text);
            dt.Record.Id.Should().Be(cmd.Id);
        }
    }
}