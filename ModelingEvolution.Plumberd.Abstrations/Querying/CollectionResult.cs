using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Querying
{
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


    }
}