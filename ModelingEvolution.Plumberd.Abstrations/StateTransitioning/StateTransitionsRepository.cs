using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public class StateTransitionsRepository : IStateTransitionsRepository
    {
        private readonly IEventStore _eventStore;
        
        public StateTransitionsRepository(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public async Task<T> Get<T>(Guid id) where T : IStateTransitionUnit, new()
        {
            T n = new T();
            n.Id = id;
            var stream = _eventStore.GetStream(typeof(T).Name, id);
            await n.RehydrateAsync(stream.ReadEvents());

            return n;
        }
        public async Task<T> ExecuteAndSave<T>(Guid id, ICommand cmd, 
            ICommandHandlerContext context) where T : IStateTransitionUnit, new()
        {
            var result = await Get<T>(id);
            await ExecuteAndSave(result, cmd, context);
            return result;
        }
        public async Task<T> ExecuteAndSave<T>(T arg, 
            ICommand cmd, 
            ICommandHandlerContext context) where T : IStateTransitionUnit
        {
            var events = arg.Execute(cmd);
            await Save( arg, events, context);
            return arg;
        }

        public async Task Save<T>( T arg,  IEvent[] events, ICommandHandlerContext context) where T : IStateTransitionUnit
        {
            var stream = _eventStore.GetStream(typeof(T).Name, arg.Id);
            foreach (var i in events)
                await stream.Append(i, context);
        }
    }
}