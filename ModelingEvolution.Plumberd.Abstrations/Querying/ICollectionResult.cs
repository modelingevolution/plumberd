using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface ICollectionResult<TResult>
    {
        IList<TResult> Items { get; }
        event Func<Task> Changed;
    }
    public interface IProjectionResult<out TProjection>
    {
        TProjection Projection { get; }
    }
    public interface IModelResult<out TProjection, out TModel>
    {
        TProjection Projection { get; }
        TModel Model { get; }
    }
}