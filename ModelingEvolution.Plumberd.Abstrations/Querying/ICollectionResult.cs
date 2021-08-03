using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

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
        event Action<IMetadata, IRecord> ModelsChanged;
    }
    public interface IModelResult<out TProjection, out TModel> : IDisposable
    {
        TProjection Projection { get; }
        TModel Model { get; }
        event Action<IMetadata, IRecord> ModelChanged;
    }
}