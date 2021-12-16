using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public class ExecuteResult<TAggregate> : IExecuteResult<TAggregate>
    where TAggregate:class
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
            IsSuccess = true;
        }
    }
    public class AggregateInvoker<TAggregate> : IAggregateInvoker<TAggregate>
        where TAggregate : class, IRootAggregate, new()
    {
        
        private readonly IEventStore _eventStore;
        private readonly string _streamName;
        private readonly IAggregateRepository<TAggregate> _repo;
        public AggregateInvoker(IEventStore eventStore, IAggregateRepository<TAggregate> repo)
        {
            _eventStore = eventStore;
            _repo = repo;
            var type = typeof(TAggregate);
            _streamName = type.GetCustomAttribute<StreamAttribute>()?.Category ?? type.Namespace.LastSegment('.');
        }
        public async Task AppendEvents(Guid id, ulong expectedVersion, IEnumerable<IEvent> events)
        {
            await _eventStore.GetStream(_streamName, id).Append(events);
        }
        public async Task<IExecuteResult<TAggregate>> Execute<TCommand>(Guid id, TCommand cmd) where TCommand : ICommand
        {
            var aggregate = await _repo.Get(id);
            return await Execute(aggregate, cmd);
        }

        public async Task<IExecuteResult<TAggregate>> Execute<TCommand>(TAggregate aggregate, TCommand cmd) where TCommand : ICommand
        {
            try
            {
                var version = aggregate.Version;
                var events = aggregate.Execute(cmd);
                await AppendEvents(aggregate.Id, version, events);
                return new ExecuteResult<TAggregate>(aggregate, events);
            }
            catch (Exception ex)
            {
                return new ExecuteResult<TAggregate>(aggregate, ex);
            }
        }
    }
}