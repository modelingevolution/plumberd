using Checkers.Shared;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.StateTransitioning;

namespace Checkers.Game
{

    [ProcessingUnitConfig(IsEventEmitEnabled = true,
        SubscribesFromBeginning = false,
        IsPersistent = false,
        ProcessingMode = ProcessingMode.CommandHandler)]
    public class CommandHandler
    {
        private readonly IAggregateRepository<CheckersAggregate> _aggregateRepo;
        private readonly IAggregateInvoker<CheckersAggregate> _invoker;

        public CommandHandler(IAggregateRepository<CheckersAggregate> aggregateRepo, IAggregateInvoker<CheckersAggregate> invoker)
        {
            _aggregateRepo = aggregateRepo;
            _invoker = invoker;
        }

        public async Task When(Guid id, Move cmd)
        {
            var aggregate = await _aggregateRepo.Get(id);
            await _invoker.Execute(aggregate, cmd);
        }
    }
}