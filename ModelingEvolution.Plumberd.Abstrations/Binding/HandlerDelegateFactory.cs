using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Binding
{
    public class HandlerDelegateFactory
    {
        private readonly Dictionary<Type, HandlerResultAdapter> _staticResultAdapters;

        public HandlerDelegateFactory()
        {
            _staticResultAdapters = new Dictionary<Type, HandlerResultAdapter>();
            _staticResultAdapters.Add(typeof(void), new HandlerResultAdapterEmpty());
            
            RegisterReturnAdapter<Task>(async (m, x) => { await x; return new ProcessingResults(); });
            
            RegisterReturnAdapter<Task<IEvent>>(async (m, x) => new ProcessingResults((m.StreamId(), await x)));
            
            RegisterReturnAdapter<IEvent[]>(async (m,events)=>new ProcessingResults(events.Select(x=> (m.StreamId(), x))));

            RegisterReturnAdapter<(Guid, IEvent)[]>(async (m, events) => new ProcessingResults(events));

            RegisterReturnAdapter<Task<IEvent[]>>(async (m, events) => new ProcessingResults((await events).Select(x => (m.StreamId(), x))));

            RegisterReturnAdapter<Task<(Guid, IEvent)[]>>(async (m, events) => new ProcessingResults((await events)));

            RegisterReturnAdapter<IEnumerable<IEvent>>(async (m, events) =>
                new ProcessingResults(events.Select(x => (m.StreamId(), x)).ToArray()));
            
            RegisterReturnAdapter<IEnumerable<(Guid,IEvent)>>(async (m, events) =>
                new ProcessingResults(events.ToArray()));

            RegisterReturnAdapter<Task<(Guid, IEvent)>>(async (m,e) => new ProcessingResults(await e));

            RegisterReturnAdapter<(Guid,IEvent)>(async (m,e) => new ProcessingResults(e));

            RegisterReturnAdapter<IAsyncEnumerable<IEvent>>(async (m,events) => new ProcessingResults((await events.ToArrayAsync()).Select(x=>(m.StreamId(), x))));

            RegisterReturnAdapter<IAsyncEnumerable<(Guid, IEvent)>>(async (m,events) => new ProcessingResults(await events.ToArrayAsync()));

            RegisterReturnAdapter<Task<ICommand>>(async (m,x)=>new ProcessingResults((m.StreamId(),await x)));

            RegisterReturnAdapter<Task<(Guid,ICommand)>>(async (m, x) => new ProcessingResults(await x));

            RegisterReturnAdapter<IEnumerable<ICommand>>(async (m, events) =>
                new ProcessingResults(events.Select(x => (m.StreamId(), x)).ToArray()));

            RegisterReturnAdapter<IEnumerable<(Guid, ICommand)>>(async (m, events) =>
                new ProcessingResults(events.ToArray()));

            RegisterReturnAdapter<IAsyncEnumerable<ICommand>>(async (m, events) => new ProcessingResults((await events.ToArrayAsync()).Select(x => (m.StreamId(), x))));

            RegisterReturnAdapter<IAsyncEnumerable<(Guid, ICommand)>>(async (m, events) => new ProcessingResults(await events.ToArrayAsync()));


        }

        private void RegisterReturnAdapter<TResult>(Func<IMetadata, TResult, Task<ProcessingResults>> func,
            Type retType = null)
        {
            var adapter = new HandlerResultAdapter<TResult>(func);
            _staticResultAdapters.Add(retType ?? adapter.ResultType, adapter);
        }

        public HandlerDispatcher Create(MethodInfo m,
            Type rootType,
            Expression callExpression,
            ParameterExpression[] paramExpressions)
        {
            var args = m.GetParameters();
            if (args.Length < 2 || args.Length > 3)
                throw new NotSupportedException("Only 2 or 3 method's arguments are supported.");

            Type tArg1 = args[0].ParameterType;
            Type tArg2 = args[1].ParameterType;
            Type tArg3 = args.Length == 3 ? args[2].ParameterType : null;
            Type tRet = m.ReturnType;

            IInvocationAdapter adapter = CreateInvocationAdapter(rootType, tArg1, tArg2, tArg3, tRet);

            HandlerParameterAdapter parameterAdapter = FindParameterAdapter(rootType, tArg1, tArg2, tArg3, tRet);
            parameterAdapter.Compile(callExpression, paramExpressions);

            HandlerResultAdapter resultAdapter = FindResultAdapter(tRet);

            return adapter.Build(parameterAdapter, resultAdapter);
        }

        private HandlerResultAdapter FindResultAdapter(Type tRet)
        {
            if (_staticResultAdapters.TryGetValue(tRet, out var adapter))
                return adapter;

            // let's try build dynamic adapter.
            if (typeof(IEvent).IsAssignableFrom(tRet) && !tRet.IsInterface)
                return DynamicHandlerResultFactory.Create_TEvent(tRet);

            else if (typeof(ICommand).IsAssignableFrom(tRet) && !tRet.IsInterface)
                return DynamicHandlerResultFactory.Create_TCommand(tRet);

            else if (tRet.IsGenericType)
            {
                var genericDef = tRet.GetGenericTypeDefinition();
                if (genericDef == typeof(Task<>))
                {
                    var args = tRet.GetGenericArguments();
                    var arg0Type = args[0];
                    if (typeof(IEvent).IsAssignableFrom(arg0Type) && !tRet.IsInterface)
                        return DynamicHandlerResultFactory.Create_Task_TEvent(arg0Type);
                    else if (typeof(ICommand).IsAssignableFrom(arg0Type) && !tRet.IsInterface)
                        return DynamicHandlerResultFactory.Create_Task_TCommand(arg0Type);
                    else if (arg0Type.IsGenericType && arg0Type.GetGenericTypeDefinition() == typeof(ValueTuple<,>))
                    {
                        var targs = arg0Type.GetGenericArguments();
                        var idParam = targs[0];
                        var recordType = targs[1];
                        if (idParam == typeof(Guid))
                        {
                            if (typeof(IEvent).IsAssignableFrom(recordType) && !tRet.IsInterface)
                                return DynamicHandlerResultFactory.Create_Task_Guid_TEvent(recordType);
                            else if (typeof(ICommand).IsAssignableFrom(recordType) && !tRet.IsInterface)
                                return DynamicHandlerResultFactory.Create_Task_Guid_TCommand(recordType);
                        }
                    }
                } 
                else if (genericDef == typeof(ValueTuple<,>))
                {
                    var args = tRet.GetGenericArguments();
                    var idParam = args[0];
                    var recordType = args[1];
                    if (idParam == typeof(Guid))
                    {
                        if (typeof(IEvent).IsAssignableFrom(recordType) && !tRet.IsInterface)
                            return DynamicHandlerResultFactory.Create_Guid_TEvent(recordType);
                        else if(typeof(ICommand).IsAssignableFrom(recordType) && !tRet.IsInterface)
                            return DynamicHandlerResultFactory.Create_Guid_TCommand(recordType);
                    }
                }
            }
            throw new NotSupportedException($"Sorry, this return type '{tRet.Name}' is not supported.");
        }

        private HandlerParameterAdapter FindParameterAdapter(Type tArg0, Type tArg1, Type tArg2, Type tArg3, Type tRet)
        {
            var genericArgs = tRet != typeof(void) ? new[] { tArg0, tRet, tArg2 } : new[] { tArg0, tArg2};

            Type genericType;
            if (tRet != typeof(void) && tArg1 == typeof(IMetadata) && typeof(IEvent).IsAssignableFrom(tArg2))
                genericType = typeof(EventHandlerParameterAdapter<,,>);
            else if (tRet == typeof(void) && tArg1 == typeof(IMetadata) && typeof(IEvent).IsAssignableFrom(tArg2))
                genericType = typeof(EventVoidHandlerParameterAdapter<,>);
            else if (tRet != typeof(void) && tArg1 == typeof(Guid) && typeof(ICommand).IsAssignableFrom(tArg2))
            {
                if(tArg3 == null)
                    genericType = typeof(CommandHandlerParameterAdapter<,,>);
                else genericType = typeof(CommandHandlerParameterAdapterExt<,,>);
            }
            else if (tRet == typeof(void) && typeof(ICommand).IsAssignableFrom(tArg2))
            {
                if(tArg3 == null)
                    genericType = typeof(CommandHandlerVoidParameterAdapter<,>);
                else
                    genericType = typeof(CommandHandlerVoidParameterAdapterExt<,>);
            }
            else throw new NotSupportedException();

            var adapter = (HandlerParameterAdapter)Activator.CreateInstance(genericType.MakeGenericType(genericArgs.ToArray()));

            return adapter;
        }

        private IInvocationAdapter CreateInvocationAdapter(Type tArg0, Type tArg1, Type tArg2, Type tArg3, Type tRet)
        {
            //tArg0 = typeof(object);
            if (tRet != typeof(void))
            {
                if (tArg3 != null) 
                    return (IInvocationAdapter) Activator.CreateInstance(typeof(InvocationAdapter<,,,,>).MakeGenericType(tArg0, tArg1, tArg2, tArg3, tRet));
                return (IInvocationAdapter) Activator.CreateInstance(typeof(InvocationAdapter<,,,>).MakeGenericType(tArg0, tArg1, tArg2, tRet));
            }
            else return (IInvocationAdapter)Activator.CreateInstance(typeof(InvocationAdapter<,,>).MakeGenericType(tArg0, tArg1, tArg2));}
        }
    }
