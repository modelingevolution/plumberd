using System;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.Metadata
{
    public interface IEventException
    {
        public IRecord Record { get; }
        public IErrorEvent ExceptionData { get; }
    }
    /// <summary>
    /// Name of event would be - {Exception}
    /// like: NotFound
    /// </summary>
    /// <typeparam name="TRecord"></typeparam>
    /// <typeparam name="TExceptionData"></typeparam>
    [EventTypeName(typeof(ExceptionEventTypeNameProvider))]
    [ProtoContract]
    public class EventException<TRecord, TExceptionData> : IEvent, IEventException
    where TRecord: IRecord
    where TExceptionData: IErrorEvent
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

        IRecord IEventException.Record => this.Record;
        IErrorEvent IEventException.ExceptionData => this.ExceptionData;
        public EventException(TRecord record, TExceptionData exceptionData)
        {
            this.Record = record;
            this.ExceptionData = exceptionData;
            Id = Guid.NewGuid();
        }
    }
}