using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Binding
{
    public delegate Task<TResult> QueryHandler<in TQuery, in TQueryHandler, TResult>(TQueryHandler handler, TQuery query);
    public class QueryHandlerBinder
    {
        private readonly Type _type;

        public QueryHandlerBinder(Type type)
        {
            _type = type;
        }
        public QueryHandler<TQuery, TQueryHandler, TResult> Create<TQuery, TQueryHandler, TResult>()
        {
            var methods = _type.GetMethods(System.Reflection.BindingFlags.Instance |
                             System.Reflection.BindingFlags.InvokeMethod |
                             System.Reflection.BindingFlags.NonPublic |
                             System.Reflection.BindingFlags.Public);
            var mth = methods
                .Where(x => !x.IsAbstract && x.ReturnType == typeof(Task<TResult>) && (x.Name == "Execute" || x.Name.StartsWith("Find")) )
                .Select(x=>new { Method = x, Parameters = x.GetParameters() })
                .FirstOrDefault(x => x.Parameters.Length == 1 && x.Parameters[0].ParameterType == typeof(TQuery));
            if (mth != null)
            {
                var action = mth.Method.CreateDelegate<QueryHandler<TQuery, TQueryHandler, TResult>>();
                return action;
            } else throw new ArgumentException("Could not find method. ");
        }
        
    }
    public partial class EventHandlerBinder : IEventHandlerBinder
    {
        public HandlerDispatcher CreateDispatcher()
        {
            var d = new DispatcherBuilder();


            var metaParam = Expression.Parameter(typeof(IMetadata), "schema");
            var eventParam = Expression.Parameter(typeof(IEvent), "event");
            var commandParam = Expression.Parameter(typeof(ICommand), "command");
            var idParam = Expression.Parameter(typeof(Guid), "id");
            var baseRef = Expression.Parameter(typeof(object), "root");
            var thisExpression = Expression.Convert(baseRef, _type);

            // Given methods are before When
            foreach (var (eventType, methodInfo) in _methods.OrderBy(x => x.Item2.Name))
            {
                Bind(_type, thisExpression, methodInfo, metaParam, eventParam, commandParam, idParam,eventType, baseRef, d);
            }

            foreach (var (ev, list) in _models)
            foreach (var (p, m) in list)
            {
                var propertyExpression = Expression.Property(thisExpression, p);

                Bind(_type, propertyExpression, m, metaParam, eventParam, commandParam, idParam, ev, baseRef, d);
            }

            return d.Execute;
        }
        private static IEnumerable<MethodInfo> Discover(Type sourceType)
        {
            var methods = sourceType
                .GetMethods(FLAGS)
                .Where(x => (CommandHandlerMethodNames.Contains(x.Name) || EventHandlerMethodNames.Contains(x.Name)) && !x.IsAbstract)
                .ToArray();

            // Discover CommandHandlers
            foreach (var i in methods.Where(x =>
                (CommandHandlerMethodNames.Contains(x.Name) && x.HasCommandHandlerParameters())))
            {
                if (i.ReturnsEvents() || i.ReturnsNothing() || i.ReturnsCommands())
                    yield return i;
            }

            // Discover EventHandlers
            foreach (var i in methods.Where(x => EventHandlerMethodNames.Contains(x.Name) && x.HasEventHandlerParameters()))
                if (i.Name == "Given" && (i.ReturnsEvents() || i.ReturnsNothing()))
                {
                    yield return i;
                }
                else if (i.Name == "When" && i.ReturnsCommands())
                {
                    yield return i;
                }
                else
                {
                    throw new InvalidMethodSignatureException(i,
                        $"Expected Task as a return type at {i.DeclaringType.Name}");
                }
        }
    }
}