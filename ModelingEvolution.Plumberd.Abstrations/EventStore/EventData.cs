using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.EventStore
{
    public abstract class EventData
    {
        public IEvent Event { get; set; }
        public IMetadata Metadata { get; set; }
    }
}