using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public interface IAggregateInvoker<TAggregate> where TAggregate : class, IRootAggregate, new()
    {
        Task<IExecuteResult<TAggregate>> Execute<TCommand>(Guid id, TCommand cmd) where TCommand : ICommand;

        Task<IExecuteResult<TAggregate>> Execute<TCommand>(TAggregate aggregate, TCommand cmd)
            where TCommand : ICommand;
    }
    public interface IAggregateRepository<TAggregate> where TAggregate : IRootAggregate, new()
    {
        Task<TAggregate> Get(Guid id);
        Task<IRecord[]> GetEvents(Guid id);
    }

    
    public interface IExecuteResult<out TAggregate>
    {
        TAggregate Aggregate { get; }
        IEvent[] Events { get; }
        Exception Exception { get; }
        TEvent Single<TEvent>() where TEvent : class, IEvent;
        bool IsSuccess { get; }
    }

    //public struct EventData
    //{
    //    public IEvent Event { get; set; }
    //    public IMetadata Metadata { get; set; }
    //}
}