using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Querying
{
    public class ProjectionResult<TProjection> : IProjectionResult<TProjection>
    {
        public TProjection Projection { get; internal set; }
        public event Action<IMetadata, IRecord> ModelsChanged;

        public IServiceScope Scope { get; set; }
        public IProcessingUnit ProcessingUnit { get; set; }
        public void FireChanged(IMetadata metadata, IRecord ev)
        {
            ModelsChanged?.Invoke(metadata,ev);
        }
        public void Dispose()
        {
            Scope?.Dispose();
            ProcessingUnit?.Dispose();
        }

        
    }
    public class ModelResult<TProjection, TModel> : IModelResult<TProjection, TModel>
    {
        public TProjection Projection { get; internal set; }
        public TModel Model { get; internal set; }
        public event Action<IMetadata, IRecord> ModelChanged;

        public void FireChanged(IMetadata metadata, IRecord ev)
        {
            ModelChanged?.Invoke(metadata, ev);
        }
        public IServiceScope Scope { get; set; }
        public IProcessingUnit ProcessingUnit { get; set; }

        public void Dispose()
        {
            Scope?.Dispose();
            ProcessingUnit?.Dispose();
        }
    }
    public class CollectionResult<TModelItem> : ICollectionResult<TModelItem>
    {
        public IList<TModelItem> Items { get; }
        internal IServiceScope Scope { get; set; }
        public object Model { get; internal set; }
        public IProcessingUnit ProcessingUnit { get; internal set; }

        public event Func<Task> Changed;

        public CollectionResult()
        {
            Items = new List<TModelItem>();
        }
        internal async Task OnChanged(IList<TModelItem> result)
        {
            Items.Clear();
            Items.AddRange(result);

            var c = Changed;
            if (c != null)
                await c();
        }


        public void Dispose()
        {
            Scope?.Dispose();
            ProcessingUnit?.Dispose();
        }
    }
}