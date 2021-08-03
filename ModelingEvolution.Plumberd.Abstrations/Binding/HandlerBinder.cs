using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
using Serilog;

namespace ModelingEvolution.Plumberd.Binding
{
    public class EventHandlerBinder<TProcessingUnit> : EventHandlerBinder
    {
        public EventHandlerBinder() : base(typeof(TProcessingUnit))
        {

        }
    }

    [Flags]
    public enum BindingFlags
    {
        ReturnCommands = 0x1,
        ReturnEvents = 0x1 << 1,
        ReturnNothing = 0x1 << 2,
        ReturnAll = 0x1 | 0x2 | 0x4,
        ProcessEvents = 0x1 << 3,
        ProcessCommands = 0x1 << 4,
    }

    public partial class EventHandlerBinder : IEventHandlerBinder
    {
        protected const System.Reflection.BindingFlags FLAGS = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private readonly List<(Type, MethodInfo)> _methods;
        private readonly Dictionary<Type, List<(PropertyInfo, MethodInfo)>> _models;
        private readonly Type _type;
        public EventHandlerBinder(Type type)
        {
            _type = type;
            _methods = new List<(Type, MethodInfo)>();
            _models = new Dictionary<Type, List<(PropertyInfo, MethodInfo)>>();
        }
        public virtual IEventHandlerBinder Discover(bool searchInProperties, Predicate<MethodInfo> methodFilter = null)
        {
            methodFilter ??= (x => true);

            foreach (var i in Discover(_type))
                if(methodFilter(i)) 
                    Register(i);

            if (searchInProperties)
            {
                var props = _type.GetProperties()
                    .Where(x => typeof(IModel).IsAssignableFrom(x.PropertyType) && x.CanRead)
                    .ToArray();

                foreach (var p in props)
                    Register(methodFilter, p);
            }

            return this;
        }

        public virtual IEnumerable<Type> Types()
        {
            return _methods
                .Select(x => x.Item1)
                .Union(_models.Keys)
                .Distinct();
        }
        

        
        private static HashSet<string> CommandHandlerMethodNames = new HashSet<string>() { "When", "Execute" };
        private static HashSet<string> EventHandlerMethodNames = new HashSet<string>() { "When", "Given" };
       
        private void Register(Predicate<MethodInfo> methodFilter, PropertyInfo p)
        {
            foreach (var i in Discover(p.PropertyType))
                if(methodFilter(i)) 
                    Register(p, i);
        }

        private void Register(MethodInfo m)
        {
            var parameterInfos = m.GetParameters();
            _methods.Add((parameterInfos[1].ParameterType, m));
        }

        private void Register(PropertyInfo p, MethodInfo m)
        {
            var parameterInfos = m.GetParameters();
            var parameterType = parameterInfos[1].ParameterType;
            if (!_models.TryGetValue(parameterType, out var list))
                _models.Add(parameterType, list = new List<(PropertyInfo, MethodInfo)>());
            list.Add((p, m));
        }

        
        private static (Expression, ParameterExpression[]) CreateCallExpression(Expression thisExpression, 
            MethodInfo methodInfo,
            ParameterExpression metaParam, 
            ParameterExpression eventParam, 
            ParameterExpression commandParam,
            ParameterExpression idParam,
            Type recordType)
        {
            if (methodInfo.HasEventHandlerParameters())
            {
                Expression callExpression = Expression.Call(thisExpression, methodInfo, metaParam,
                    Expression.Convert(eventParam, recordType));
                ParameterExpression[] paraExpressions = new[] {metaParam, eventParam};
                return (callExpression, paraExpressions);
            }
            else if (methodInfo.HasCommandHandlerParameters())
            {
                var args = methodInfo.GetParameters();
                if (args.Length == 2)
                {
                    Expression callExpression = Expression.Call(thisExpression, methodInfo, idParam,
                        Expression.Convert(commandParam, recordType));
                    ParameterExpression[] paraExpressions = new[] { idParam, commandParam };
                    return (callExpression,  paraExpressions);
                } else if (args.Length == 3)
                {
                    Expression callExpression = Expression.Call(thisExpression, methodInfo, idParam,
                        Expression.Convert(commandParam, recordType),
                        metaParam);
                    ParameterExpression[] paraExpressions = new[] { idParam, commandParam, metaParam };
                    return (callExpression,  paraExpressions);
                }
            }
            throw new NotSupportedException("Method is not supported!");
        }
        private static void Bind(Type rootType, 
            Expression thisExpression, 
            MethodInfo methodInfo,
            ParameterExpression metaParam, 
            ParameterExpression eventParam,
            ParameterExpression commandParam,
            ParameterExpression idParam,
            Type recordType, 
            ParameterExpression baseRef,
            DispatcherBuilder d)
        {
            var (callExpression, parameterExpressions) = CreateCallExpression(thisExpression, 
                methodInfo, 
                metaParam, 
                eventParam, 
                commandParam, 
                idParam, 
                recordType);

            HandlerDelegateFactory f = new HandlerDelegateFactory();
            List<ParameterExpression> p = new List<ParameterExpression>() { baseRef };
            p.AddRange(parameterExpressions);

            var dispatcher = f.Create(methodInfo, rootType, callExpression, p.ToArray());
            d.Register(dispatcher, recordType);
        

        }

        

        private class DispatcherBuilder
        {
            private static ILogger Log = Serilog.Log.ForContext<DispatcherBuilder>();
            private readonly Dictionary<Type, List<HandlerDispatcher>> invokers =
                new Dictionary<Type, List<HandlerDispatcher>>();

            public void Register(HandlerDispatcher method, Type ev)
            {
                if (!invokers.TryGetValue(ev, out var list))
                    invokers.Add(ev, list = new List<HandlerDispatcher>());
                list.Add(method);
            }

            public async Task<ProcessingResults> Execute(object processor,
                IMetadata m, IRecord ev)
            {
                ProcessingResults result = new ProcessingResults();
                if(invokers.TryGetValue(ev.GetType(), out var invoker))
                {
                    foreach (var i in invoker)
                        result += await i(processor, m, ev);
                }
                else Log.Warning("Found event {eventType} in a stream that cannot be dispatched on {processorType}", ev.GetType(), processor?.GetType().Name ?? "-");
                return result;
            }
        }
 
    }
}