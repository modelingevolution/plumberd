using System;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.Tests.Models
{
    public class Command1 : Command { }
    
    [ProtoContract]
    public class SimpleCommand : ICommand
    {
        [ProtoMember(1)]
        public Guid Id { get; }

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