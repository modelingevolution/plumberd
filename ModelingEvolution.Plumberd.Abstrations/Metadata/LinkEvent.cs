using System;
using System.Collections.Concurrent;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.Metadata
{
    readonly struct LinkEvent : ILink
    {
        public LinkEvent(string sourceCategory, 
            Guid sourceStreamId, 
            ulong sourceStreamPosition, 
            string dstStreamCategory)
        {
            SourceCategory = sourceCategory;
            SourceStreamId = sourceStreamId;
            SourceStreamPosition = sourceStreamPosition;
            StreamCategory = dstStreamCategory;
            Id = Guid.NewGuid();
        }
        public string StreamCategory { get; }
        public Guid Id { get; }
        public string SourceCategory { get; }
        public Guid SourceStreamId { get; }
        public ulong SourceStreamPosition { get; }
    }

    internal interface IIgnoreFilterModel
    {
        bool IsFiltered(Guid correlationId);
    }
    [Stream("Ignored")]
    public record IgnoreByCorrelationId : ICommand
    {
        public Guid CorrelationId { get; init; }
        public Guid Id { get; init; }

        public IgnoreByCorrelationId()
        {
            Id = Guid.NewGuid();
        }
    }
    [Stream("Ignored")]
    public record ByCorrelationIdIgnored : IEvent
    {
        public Guid CorrelationId { get; init; }
        public Guid Id { get; init; }

        public ByCorrelationIdIgnored()
        {
            Id = Guid.NewGuid();
        }
    }
}