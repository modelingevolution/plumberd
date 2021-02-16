using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public class AggregateRepository<TAggregate> : IAggregateRepository<TAggregate> where TAggregate: IRootAggregate, new()
    {
        
        private readonly IEventStore _eventStore;
        private readonly string _streamName;
        public AggregateRepository(IEventStore eventStore)
        {
            _eventStore = eventStore;
            var type = typeof(TAggregate);
            _streamName = type.GetCustomAttribute<StreamAttribute>()?.Category ?? type.Namespace.LastSegment('.');
        }

        public async Task<IRecord[]> GetEvents(Guid id)
        {
            return await _eventStore.GetStream(_streamName, id).ReadEvents().ToArrayAsync();
        }
        public async Task<TAggregate> Get(Guid id)
        {
            var events = _eventStore.GetStream(_streamName, id).ReadEvents();
            TAggregate result = new TAggregate();
            result.Id = id;
            await result.RehydrateAsync(events);
            
            return result;
        }
       
       
    }
}