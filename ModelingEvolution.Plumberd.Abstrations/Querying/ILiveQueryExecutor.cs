using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface ILiveQueryExecutor
    {
        ISingleResult<TResult> ExecuteSingle<TResult>(ISingleResultQuery<TResult> query, string streamName);
        ICollectionResult<TResult> Execute<TResult>(ICollectionResultQuery<TResult> query, string streamName);

        Task<ICollectionResult<TModelItem>> Execute<TQuery, TModelItem, TQueryHandler, TProjection, TModel>(
            ICollectionResultQuery<TModelItem> query, string streamName)
            where TQuery : ICollectionResultQuery<TModelItem>;

        Task<IObservableCollectionResult<TModelItem>> Execute<TModelItem, TProjection, TModel>(
            ICollectionResultQuery<TModelItem> query, string streamName,
            Func<TModel, ObservableCollection<TModelItem>> accesor);

        Task<IObservableCollectionResult<TViewModel, TModelItem>> Execute<TViewModel, TModelItem, TProjection, TModel>(
            ICollectionResultQuery<TModelItem> query, string streamName,
            Func<TModel, ObservableCollection<TModelItem>> accesor, Func<TModelItem, TViewModel> converter) where TViewModel : IViewFor<TModelItem>;
    }
}