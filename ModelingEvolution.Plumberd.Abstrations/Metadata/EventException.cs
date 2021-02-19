using System;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.Metadata
{
    /// <summary>
    /// Name of event would be - {Exception}
    /// like: NotFound
    /// </summary>
    /// <typeparam name="TRecord"></typeparam>
    /// <typeparam name="TExceptionData"></typeparam>
    [EventTypeName(typeof(ExceptionEventTypeNameProvider))]
    [ProtoContract]
    public class EventException<TRecord, TExceptionData> : IEvent
    where TRecord: IRecord
    {
        [ProtoMember(1)]
        public Guid Id { get; set;  }

        [ProtoMember(2)]
        public TRecord Record { get; set; }

        [ProtoMember(3)]
        public TExceptionData ExceptionData { get; set; }
        public EventException()
        {
            Id = Guid.NewGuid();
        }

        public EventException(TRecord record, TExceptionData exceptionData)
        {
            this.Record = record;
            this.ExceptionData = exceptionData;
            Id = Guid.NewGuid();
        }
    }
}