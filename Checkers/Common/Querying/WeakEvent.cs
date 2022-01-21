namespace Checkers.Common.Querying
{
    public class WeakEvent<TPayload>
    {
        record Handler
        {
            public EventHandler<TPayload> Event;
            public SynchronizationContext Context;
        }
        private readonly WeakCollection<Handler> _index = new WeakCollection<Handler>();

        public event EventHandler<TPayload> On
        {
            add
            {
                lock (_index)
                {
                    var handler = new Handler() { Event = value, Context = SynchronizationContext.Current };
                    _index.Add(handler);
                }
            }
            remove
            {
                lock (_index)
                {
                    var handler = new Handler() { Event = value, Context = SynchronizationContext.Current };
                    _index.Remove(handler);
                }
            }
        }

        public virtual void Execute(TPayload args)
        {
            lock (_index)
            {
                foreach (var handler in _index.GetLiveItems())
                {
                    handler.Context.Post((x) => ((Handler)x).Event( this, args), handler);
                }
            }
        }
    }
}
