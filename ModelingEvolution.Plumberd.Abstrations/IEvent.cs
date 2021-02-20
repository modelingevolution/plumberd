using System;

namespace ModelingEvolution.Plumberd
{
    public interface IErrorEvent : IEvent
    {
        string Message { get; }
    }
    public interface IEvent : IRecord
    {
        
    }

    public interface ILink : IEvent
    {
        string SourceCategory { get; }
        Guid SourceStreamId { get; }
        ulong SourceStreamPosition { get; }
    }
    public interface IRecord
    {
        public Guid Id { get; }
    }
}