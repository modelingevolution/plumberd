using System;
using ModelingEvolution.Plumberd.EventStore;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.GrpcProxy.Authentication
{
    [Stream("User")]
    [ProtoContract]
    public class AuthorizationDataRetrieved : IEvent
    {
        [ProtoMember(1)]
        public Guid Id { get; set;  }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public string Email { get; set; }

        public AuthorizationDataRetrieved()
        {
            Id = Guid.NewGuid();
        }
    }
}