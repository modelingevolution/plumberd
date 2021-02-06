namespace ModelingEvolution.Plumberd.Querying
{
    public interface IObservableCollectionResult<TViewModel,TModelItem> where TViewModel : IViewFor<TModelItem>
    {
        ObservableCollectionView<TViewModel,TModelItem> View { get; }
    }
}