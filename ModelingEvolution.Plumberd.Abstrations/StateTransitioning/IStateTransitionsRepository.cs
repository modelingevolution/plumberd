using System;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public interface IStateTransitionsRepository
    {
        Task<T> Get<T>(Guid id) where T : IStateTransitionUnit, new();
        Task<T> ExecuteAndSave<T>(Guid id, ICommand cmd, ICommandHandlerContext context = null) where T : IStateTransitionUnit, new();
        Task<T> ExecuteAndSave<T>(T arg, ICommand cmd, ICommandHandlerContext context = null) where T : IStateTransitionUnit;
        Task Save<T>( T arg,  IEvent[] events, ICommandHandlerContext context = null) where T : IStateTransitionUnit;
    }
}