using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Binding
{
    public class EventHandlerParameterAdapter<TUnit, TEvent> : HandlerParameterAdapter<TUnit, IMetadata, TEvent>
        where TEvent : IEvent
    {
        public override void Invoke(object unit, IMetadata m, IRecord r)
        {
            Func((TUnit)unit, m, (TEvent)r);
        }
    }
    public class EventHandlerParameterAdapter<TUnit, TResult, TEvent> : HandlerParameterAdapter<TUnit, IMetadata, TEvent, TResult>
        where TEvent : IEvent
    {
        public override TResult Convert(object unit, IMetadata m, IRecord r)
        {
            return Func((TUnit)unit, m, (TEvent)r);
        }
    }
}