using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.Querying
{
    public class CollectionResult<TResult> : ICollectionResult<TResult>
    {
        public IList<TResult> Items { get; }
        internal IServiceScope Scope { get; set; }
        public object Model { get; internal set; }
        public IProcessingUnit ProcessingUnit { get; internal set; }

        public event Func<Task> Changed;

        internal async Task OnChanged(IList<TResult> result)
        {
            Items.Clear();
            Items.AddRange(result);

            var c = Changed;
            if (c != null)
                await c();
        }


    }

    public interface ILiveQuery
    {

    }
    public interface ICollectionResultQuery<TResult> : ILiveQuery { }
    public interface ISingleResultQuery<TResult> : ILiveQuery { }
    public interface ISingleResult<out TResult> { TResult Result { get; } }

    public interface ICollectionResult<TResult>
    {
        IList<TResult> Items { get; }
        event Func<Task> Changed;
    }
    public interface ILiveQueryExecutor
    {
        ISingleResult<TResult> ExecuteSingle<TResult>(ISingleResultQuery<TResult> query, ProjectionSchema schema);
        ICollectionResult<TResult> Execute<TResult>(ICollectionResultQuery<TResult> query, ProjectionSchema schema);

        Task<ICollectionResult<TResult>> Execute<TQuery, TResult, TQueryHandler, TProjection, TModel>(
            ICollectionResultQuery<TResult> query, ProjectionSchema schema)
            where TQuery : ICollectionResultQuery<TResult>;
    }
    public class LiveQueryExecutor : ILiveQueryExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPlumberRuntime _plumber;
        class CollectionResultQueryExecutor<TQuery, TResult, TQueryHandler, TProjection, TModel>
            where TQuery:ICollectionResultQuery<TResult>
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly IPlumberRuntime _plumber;
            private readonly ProjectionSchema _schema;


            public CollectionResultQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber,
                ProjectionSchema schema)
            {
                _serviceProvider = serviceProvider;
                _plumber = plumber;
                _schema = schema;
            }

            public async Task<ICollectionResult<TResult>> Execute<TResult>(ICollectionResultQuery<TResult> query)
            {
                CollectionResult<TResult> results = new CollectionResult<TResult>();
                var scopedProvider = (results.Scope = _serviceProvider.CreateScope()).ServiceProvider;

                results.Model = scopedProvider.GetService<TModel>();
                var queryHandler = scopedProvider.GetService<TQueryHandler>();
                var projectionHandler = scopedProvider.GetService<TProjection>();

                QueryHandlerBinder queryBinder = new QueryHandlerBinder(typeof(TQueryHandler)); // Should be cached.
                
                var queryHandlerInvocation = queryBinder.Create<TQuery,TQueryHandler, IList<TResult>>();
                results.ProcessingUnit = await _plumber.RunController(projectionHandler, new ProcessingUnitConfig(typeof(TProjection))
                {
                    IsEventEmitEnabled = false,
                    IsCommandEmitEnabled = false,
                    IsPersistent = false,
                    ProcessingMode = ProcessingMode.EventHandler,
                    SubscribesFromBeginning = true,
                    // Projection Schema => From Metadata.UserId or Event.Property
                    ProjectionSchema = _schema,
                    OnAfterDispatch = async (unit, metadata, ev, r) =>
                    {
                        var result = await queryHandlerInvocation(queryHandler, (TQuery) query);
                        await results.OnChanged(result);
                    }
                });

                
                return results;
            }
        }
        

        public LiveQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber)
        {
            _serviceProvider = serviceProvider;
            _plumber = plumber;
        }

        public ISingleResult<TResult> ExecuteSingle<TResult>(ISingleResultQuery<TResult> query, ProjectionSchema schema)
        {
            throw new NotImplementedException();
        }

        public ICollectionResult<TResult> Execute<TResult>(ICollectionResultQuery<TResult> query, ProjectionSchema schema)
        {
            throw new NotImplementedException();
        }


        public Task<ICollectionResult<TResult>> Execute<TQuery, TResult, TQueryHandler, TProjection, TModel>(ICollectionResultQuery<TResult> query, ProjectionSchema schema)
            where TQuery : ICollectionResultQuery<TResult>
        {
            var executor = new CollectionResultQueryExecutor<TQuery, TResult, TQueryHandler, TProjection, TModel>(_serviceProvider, _plumber, schema);
            return executor.Execute(query);
        }

    }
}