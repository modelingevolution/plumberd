using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Querying
{
    public class ObservableCollectionResult<TViewModel,TModelItem> : IObservableCollectionResult<TViewModel,TModelItem> 
        where TViewModel : IViewFor<TModelItem>
    {
        public ObservableCollection<TModelItem> SourceItems { get; }
        public ObservableCollectionView<TViewModel,TModelItem> View { get; }
        internal IServiceScope Scope { get; set; }
        public object Model { get; internal set; }
        public IProcessingUnit ProcessingUnit { get; internal set; }


        public ObservableCollectionResult(Func<TModelItem, TViewModel> convertItem,ObservableCollection<TModelItem> source)
        {
            SourceItems = source;
            View = new ObservableCollectionView<TViewModel,TModelItem>(convertItem,source);
        }

    }
}