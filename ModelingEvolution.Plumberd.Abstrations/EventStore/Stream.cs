namespace ModelingEvolution.Plumberd.EventStore
{
    public class EventData<TEvent> : EventData where TEvent : Event
    {
        public new TEvent Event
        {
            get { return (TEvent)base.Event; }
            set { base.Event = value; }
        }
    }

}