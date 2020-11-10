using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public interface IStateTransitionUnit
    {
        /// <summary>
        /// Number of events that the aggregate received
        /// </summary>
        ulong Version { get; }
        Guid Id { get; set; }
        void Rehydrate(IEnumerable<IRecord> events);
        Task RehydrateAsync(IAsyncEnumerable<IRecord> events);
        IEvent[] Execute(ICommand cmd);
    }
}