using System;

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
}