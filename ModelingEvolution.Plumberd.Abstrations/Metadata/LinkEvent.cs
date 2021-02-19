using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    readonly struct LinkEvent : ILink
    {
        public LinkEvent(string sourceCategory, 
            Guid sourceStreamId, 
            ulong sourceStreamPosition, 
            string destinationCategory)
        {
            SourceCategory = sourceCategory;
            SourceStreamId = sourceStreamId;
            SourceStreamPosition = sourceStreamPosition;
            DestinationCategory = destinationCategory;
            Id = Guid.NewGuid();
        }
        public string DestinationCategory { get; }
        public Guid Id { get; }
        public string SourceCategory { get; }
        public Guid SourceStreamId { get; }
        public ulong SourceStreamPosition { get; }
    }
}