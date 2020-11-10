using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.StateTransitioning;

namespace ModelingEvolution.Plumberd.CommandHandling
{
    public static class CommandHandler<TCommand, TEvent>
        where TCommand : ICommand
        where TEvent : IEvent
    {
        public abstract class EventuallyConsistent : CommandHandler<TCommand>
        {
            private readonly IEventStore _eventStore;
            protected IEventStore EventStore => _eventStore;
            
            protected EventuallyConsistent(IEventStore eventStore)
            {
                _eventStore = eventStore;

            }

            protected abstract Task<TEvent> When(TCommand c);

            public override async Task Execute(Guid id, TCommand c)
            {
                await _eventStore.GetEventStream<TEvent>(id).Append(await When(c));
            }
        }

        public sealed class Transactional<TStateTransitionUnit> : CommandHandler<TCommand>
            where TStateTransitionUnit : IStateTransitionUnit, new()
        {
            private readonly IStateTransitionsRepository _repository;
            private readonly ICommandProcessingContextFactory _contextFactory;
            public Transactional(IStateTransitionsRepository repository, ICommandProcessingContextFactory contextFactory)
            {
                _repository = repository;
                _contextFactory = contextFactory;
            }

            public override async Task Execute(Guid id, TCommand c)
            {
                using (var context = _contextFactory.Create(this.GetType(), id, c))
                {
                    await _repository.ExecuteAndSave<TStateTransitionUnit>(id, c, context);
                }
            }
        }
    }

}