using System;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public interface IAggregateRepository<TAggregate> where TAggregate : IRootAggregate, new()
    {
        Task<TAggregate> Get(Guid id);
    }
}