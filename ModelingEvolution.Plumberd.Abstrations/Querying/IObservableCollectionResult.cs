using System;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface IObservableCollectionResult<TViewModel,TModelItem> where TViewModel : IViewFor<TModelItem>, IEquatable<TViewModel>
    {
        IObservableCollectionView<TViewModel,TModelItem> View { get; }
    }
}