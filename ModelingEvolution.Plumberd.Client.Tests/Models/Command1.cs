using System;
using ModelingEvolution.Plumberd.EventProcessing;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.Tests.Models
{
    [ProtoContract]
    public class MyExceptionData : IErrorEvent
    {
        [ProtoMember(1)]
        public string Text { get; set; }

        public Guid Id { get; set; }
        public string Message { get; set; }
    }
    [ProtoContract]
    public class CommandRaisingException : ICommand
    {
        public Guid Id { get; set; }

        public CommandRaisingException()
        {
            Id = Guid.NewGuid();
        }
    }
    public class Command1 : Command { }
    
    [ProtoContract]
    public class SimpleCommand : ICommand
    {
        [ProtoMember(1)]
        public Guid Id { get; set; }

        public SimpleCommand()
        {
            Id = Guid.NewGuid();
        }
    }

    [ProtoContract]
    public class SimpleEvent : IEvent
    {
        [ProtoMember(1)]
        public Guid Id { get; }

        public SimpleEvent()
        {
            Id = Guid.NewGuid();
        }
    }
}