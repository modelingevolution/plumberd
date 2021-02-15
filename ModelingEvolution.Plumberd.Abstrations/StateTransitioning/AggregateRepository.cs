using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public class AggregateRepository<TAggregate> : IAggregateRepository<TAggregate> where TAggregate: IRootAggregate, new()
    {
        class ExecuteResult<TAggregate> : IExecuteResult<TAggregate>
        {
            public TAggregate Aggregate { get; }
            public IEvent[] Events { get; }
            public Exception Exception { get; }
            public bool IsSuccess { get; }

            public ExecuteResult(TAggregate aggregate, Exception exception)
            {
                Aggregate = aggregate;
                Exception = exception;
                IsSuccess = false;
            }
            public TEvent Single<TEvent>() where TEvent : class, IEvent
            {
                return Events.Single() as TEvent;
            }

            public ExecuteResult(TAggregate aggregate, IEvent[] events)
            {
                Aggregate = aggregate;
                Events = events;
            }
        }
        private readonly IEventStore _eventStore;
        private readonly string _streamName;
        public AggregateRepository(IEventStore eventStore)
        {
            _eventStore = eventStore;
            var type = typeof(TAggregate);
            _streamName = type.GetCustomAttribute<StreamAttribute>()?.Category ?? type.Namespace.LastSegment('.');
        }

        public async Task<TAggregate> Get(Guid id)
        {
            var events = _eventStore.GetStream(_streamName, id).ReadEvents();
            TAggregate result = new TAggregate();
            result.Id = id;
            await result.RehydrateAsync(events);
            
            return result;
        }
       
        public async Task AppendEvents(Guid id, IEnumerable<IEvent> events)
        {
            await _eventStore.GetStream(_streamName, id).Append(events);
        }
        public async Task<IExecuteResult<TAggregate>> Execute<TCommand>(Guid id, TCommand cmd) where TCommand : ICommand
        {
            var aggregate = await Get(id);
            return await Execute(aggregate,cmd);
        }

        public async Task<IExecuteResult<TAggregate>> Execute<TCommand>(TAggregate aggregate,TCommand cmd) where TCommand : ICommand
        {
            try
            {
                var events = aggregate.Execute(cmd);
                await AppendEvents(aggregate.Id, events);
                return new ExecuteResult<TAggregate>(aggregate, events);
            }
            catch (Exception ex)
            {
                return new ExecuteResult<TAggregate>(aggregate, ex);
            }
        }
    }
}