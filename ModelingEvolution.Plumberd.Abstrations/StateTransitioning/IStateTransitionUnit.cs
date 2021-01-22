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
        public ulong Version { get; }

        public Guid Id { get; set; }
        public void Rehydrate(IEnumerable<IRecord> events);
        public Task RehydrateAsync(IAsyncEnumerable<IRecord> events);
        public IEvent[] Execute(ICommand cmd);
    }
}