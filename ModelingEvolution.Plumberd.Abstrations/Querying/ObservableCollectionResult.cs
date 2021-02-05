using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface IObservableCollectionResult<TResult, TProjection, TModel>
    {
        ObservableCollectionView<TResult> View { get; }
        TModel Model { get; }
        TProjection Projection { get; }
    }
    public class ObservableCollectionResult<TModelItem, TProjection, TModel> : IObservableCollectionResult<TModelItem, TProjection, TModel>
    {
        public ObservableCollection<TModelItem> SourceItems { get; }
        public ObservableCollectionView<TModelItem> View { get; }
        internal IServiceScope Scope { get; set; }
        public TModel Model { get; internal set; }
        public TProjection Projection { get; internal set; }
        public IProcessingUnit ProcessingUnit { get; internal set; }


        public ObservableCollectionResult(ObservableCollection<TModelItem> source)
        {
            SourceItems = source;
            View = new ObservableCollectionView<TModelItem>(source);
        }
        
    }

    public class
        ObservableCollectionResult<TViewModel, TModelItem> : IObservableCollectionResult<TViewModel, TModelItem>
        where TViewModel : IViewFor<TModelItem>
    {
        public ObservableCollection<TModelItem> SourceItems { get; }
        public ObservableCollectionView<TViewModel, TModelItem> View { get; }
        internal IServiceScope Scope { get; set; }
        public object Model { get; internal set; }
        public IProcessingUnit ProcessingUnit { get; internal set; }


        public ObservableCollectionResult(Func<TModelItem, TViewModel> convertItem,
            ObservableCollection<TModelItem> source)
        {
            SourceItems = source;
            View = new ObservableCollectionView<TViewModel, TModelItem>(convertItem, source);
        }
    }
}