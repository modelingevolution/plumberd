using System;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd
{
    public interface IErrorEvent : IEvent
    {
        string Message { get; }
    }
    public interface IEvent : IRecord
    {
        
    }

    
    public interface IStreamAware 
    {
        string StreamCategory { get; }

    }
    public interface ILink : IEvent, IStreamAware
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