using System;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public interface IStateTransitionInvoker
    {
        Task Execute<TAggregate>(Guid id, ICommand cmd);
    }
}