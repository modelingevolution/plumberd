using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public interface IRootAggregate
    {
        Guid Id { get; set; }
        ulong Version { get; }
        void Rehydrate(IEnumerable<IRecord> events);
        Task RehydrateAsync(IAsyncEnumerable<IRecord> events);
        IEvent[] Execute(ICommand command);
    }
}