using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Querying
{
   

    public class LiveQueryExecutor : ILiveQueryExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPlumberRuntime _plumber;

        class ModelQueryExecutor<TProjection>
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly IPlumberRuntime _plumber;
            private readonly string _streamName;
            public ModelQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber, string streamName)
            {
                _serviceProvider = serviceProvider;
                _plumber = plumber;
                _streamName = streamName;
            }

            public async Task<IProjectionResult<TProjection>> Execute()
            {
                var results = new ProjectionResult<TProjection>();
                var scopedProvider = (results.Scope = _serviceProvider.CreateScope()).ServiceProvider;

                results.Projection = scopedProvider.GetRequiredService<TProjection>();
                
                results.ProcessingUnit = await _plumber.RunController(results.Projection, new ProcessingUnitConfig(typeof(TProjection))
                {
                    IsEventEmitEnabled = false,
                    IsCommandEmitEnabled = false,
                    IsPersistent = false,
                    ProcessingMode = ProcessingMode.EventHandler,
                    SubscribesFromBeginning = true,
                    // Projection Schema => From Metadata.UserId or Event.Property
                    ProjectionSchema = new ProjectionSchema() { StreamName = _streamName, IsDirect = true },
                    OnAfterDispatch = async (u, m, e, r) => results.FireChanged(m,e)
                });


                return results;
            }
        }

        class ModelQueryExecutor<TProjection, TModel>
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly IPlumberRuntime _plumber;
            private readonly string _streamName;
            public ModelQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber, string streamName)
            {
                _serviceProvider = serviceProvider;
                _plumber = plumber;
                _streamName = streamName;
            }

            public async Task<IModelResult<TProjection, TModel>> Execute()
            {
                var  results = new ModelResult<TProjection, TModel>();
                var scopedProvider = (results.Scope = _serviceProvider.CreateScope()).ServiceProvider;

                results.Model = scopedProvider.GetRequiredService<TModel>();
                results.Projection= scopedProvider.GetRequiredService<TProjection>();

                
                results.ProcessingUnit = await _plumber.RunController(results.Projection, new ProcessingUnitConfig(typeof(TProjection))
                {
                    IsEventEmitEnabled = false,
                    IsCommandEmitEnabled = false,
                    IsPersistent = false,
                    ProcessingMode = ProcessingMode.EventHandler,
                    SubscribesFromBeginning = true,
                    // Projection Schema => From Metadata.UserId or Event.Property
                    ProjectionSchema = new ProjectionSchema() { StreamName = _streamName, IsDirect = true},
                    OnAfterDispatch = async (u,m,e,r) => results.FireChanged(m,e)
                });


                return results;
            }
        }
        class CollectionResultQueryExecutor<TQuery, TResult, TQueryHandler, TProjection, TModel>
            where TQuery:ICollectionResultQuery<TResult>
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly IPlumberRuntime _plumber;
            private readonly string _streamName;


            public CollectionResultQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber,
                string streamName)
            {
                _serviceProvider = serviceProvider;
                _plumber = plumber;
                _streamName = streamName;
            }

            public async Task<ICollectionResult<TResult>> Execute(ICollectionResultQuery<TResult> query)
            {
                CollectionResult<TResult> results = new CollectionResult<TResult>();
                var scopedProvider = (results.Scope = _serviceProvider.CreateScope()).ServiceProvider;

                results.Model = scopedProvider.GetRequiredService<TModel>();
                var queryHandler = scopedProvider.GetRequiredService<TQueryHandler>();
                var projectionHandler = scopedProvider.GetRequiredService<TProjection>();

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
                    ProjectionSchema = new ProjectionSchema() { StreamName = _streamName },
                    OnAfterDispatch = async (unit, metadata, ev, r) =>
                    {
                        var result = await queryHandlerInvocation(queryHandler, (TQuery) query);
                        await results.OnChanged(result);
                    }
                });

                
                return results;
            }
        }

        class CollectionResultQueryExecutor<TModelItem, TProjection, TModel>
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly IPlumberRuntime _plumber;
            private readonly string _streamName;


            public CollectionResultQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber,
                string streamName)
            {
                _serviceProvider = serviceProvider;
                _plumber = plumber;
                _streamName = streamName;
            }

            public async Task<IObservableCollectionResult<TModelItem, TProjection, TModel>> Execute(ICollectionResultQuery<TModelItem> query, Func<TModel, ObservableCollection<TModelItem>> accesor)
            {
                var scope = _serviceProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var model = scopedProvider.GetRequiredService<TModel>();
                var results = new ObservableCollectionResult<TModelItem, TProjection, TModel>(accesor(model))
                {
                    Scope = scope, 
                    Model = model, 
                    Projection = scopedProvider.GetRequiredService<TProjection>()
                };

                results.ProcessingUnit = await _plumber.RunController(results.Projection, new ProcessingUnitConfig(typeof(TProjection))
                {
                    IsEventEmitEnabled = false,
                    IsCommandEmitEnabled = false,
                    IsPersistent = false,
                    ProcessingMode = ProcessingMode.EventHandler,
                    SubscribesFromBeginning = true,
                    ProjectionSchema = new ProjectionSchema() { StreamName = _streamName },
                    
                });


                return results;
            }
        }

        class CollectionResultQueryExecutor<TViewModel, TModelItem, TProjection, TModel> where TViewModel : IViewFor<TModelItem>, IEquatable<TViewModel>
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly IPlumberRuntime _plumber;
            private readonly string _streamName;


            public CollectionResultQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber,
                string streamName)
            {
                _serviceProvider = serviceProvider;
                _plumber = plumber;
                _streamName = streamName;
            }

            public async Task<IObservableCollectionResult<TViewModel, TModelItem>> Execute(ICollectionResultQuery<TModelItem> query, 
                Func<TModel, ObservableCollection<TModelItem>> accesor, Func<TModelItem,TViewModel> converter)
            {
                var scope = _serviceProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var model = scopedProvider.GetRequiredService<TModel>();
                ObservableCollectionResult<TViewModel, TModelItem> results = new ObservableCollectionResult<TViewModel,TModelItem>(converter,accesor(model));
                results.Scope = scope;
                results.Model = model;

                var projectionHandler = scopedProvider.GetRequiredService<TProjection>();

                results.ProcessingUnit = await _plumber.RunController(projectionHandler, new ProcessingUnitConfig(typeof(TProjection))
                {
                    IsEventEmitEnabled = false,
                    IsCommandEmitEnabled = false,
                    IsPersistent = false,
                    ProcessingMode = ProcessingMode.EventHandler,
                    SubscribesFromBeginning = true,
                    ProjectionSchema = new ProjectionSchema() { StreamName = _streamName },
                    
                });


                return results;
            }
        }
        public LiveQueryExecutor(IServiceProvider serviceProvider, IPlumberRuntime plumber)
        {
            _serviceProvider = serviceProvider;
            _plumber = plumber;
        }

        public ISingleResult<TResult> ExecuteSingle<TResult>(ISingleResultQuery<TResult> query, string streamName)
        {
            throw new NotImplementedException();
        }

        public ICollectionResult<TResult> Execute<TResult>(ICollectionResultQuery<TResult> query, string streamName)
        {
            throw new NotImplementedException();
        }

        public Task<IModelResult<TProjection, TModel>> Execute<TProjection, TModel>(string streamName)
        {
            var executor = new ModelQueryExecutor<TProjection, TModel>(_serviceProvider, _plumber, streamName);
            return executor.Execute();
        }
        public Task<IProjectionResult<TProjection>> Execute<TProjection>(string streamName)
        {
            var executor = new ModelQueryExecutor<TProjection>(_serviceProvider, _plumber, streamName);
            return executor.Execute();
        }

        public Task<ICollectionResult<TResult>> Execute<TQuery, TResult, TQueryHandler, TProjection, TModel>(ICollectionResultQuery<TResult> query, string streamName)
            where TQuery : ICollectionResultQuery<TResult>
        {
            var executor = new CollectionResultQueryExecutor<TQuery, TResult, TQueryHandler, TProjection, TModel>(_serviceProvider, _plumber, streamName);
            return executor.Execute(query);
        }
        public Task<IObservableCollectionResult<TResult, TProjection, TModel>> Execute<TResult, TProjection, TModel>(
            ICollectionResultQuery<TResult> query, string streamName,
            Func<TModel, ObservableCollection<TResult>> accesor)
        {
            var executor = new CollectionResultQueryExecutor<TResult, TProjection, TModel>(_serviceProvider, _plumber, streamName);
            return executor.Execute(query, accesor);
        }
        public Task<IObservableCollectionResult<TViewModel,TModelItem>> Execute<TViewModel, TModelItem, TProjection, TModel>(
            ICollectionResultQuery<TModelItem> query, string streamName,
            Func<TModel, ObservableCollection<TModelItem>> accesor,
            Func<TModelItem, TViewModel> converter)
        where TViewModel:IViewFor<TModelItem>, IEquatable<TViewModel>
        {
            var executor = new CollectionResultQueryExecutor<TViewModel, TModelItem, TProjection, TModel>(_serviceProvider, _plumber, streamName);
            return executor.Execute(query, accesor, converter);
        }
    }
}