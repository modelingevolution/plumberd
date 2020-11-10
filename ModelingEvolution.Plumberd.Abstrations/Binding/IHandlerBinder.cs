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
    public interface IHandlerBinder
    {
        IHandlerBinder Discover(bool searchInProperties, Predicate<MethodInfo> methodFilter = null);
        IEnumerable<Type> Types();
        HandlerDispatcher CreateDispatcher();
    }

    public static class HandlerBinderExtensions
    {
        public static IHandlerBinder Discover(this IHandlerBinder binder, bool searchInProperties, BindingFlags flags)
        {
            BinderFilter f = new BinderFilter(flags);
            return binder.Discover(searchInProperties, f.Filter);
        }
        
    }
    public partial class HandlerBinder : IHandlerBinder
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
                if (i.ReturnsEvents() || i.ReturnsNothing())
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