using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface ICollectionResult<TResult> : IDisposable
    {
        IList<TResult> Items { get; }
        event Func<Task> Changed;
    }
    public interface IProjectionResult<out TProjection>: IDisposable
    {
        TProjection Projection { get; }
    }
    public interface IModelResult<out TProjection, out TModel> : IDisposable
    {
        TProjection Projection { get; }
        TModel Model { get; }
        event Action ModelChanged;
    }
}