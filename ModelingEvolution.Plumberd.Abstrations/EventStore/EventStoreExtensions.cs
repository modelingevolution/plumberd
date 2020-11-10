using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class NoContextAvailableException : Exception
    {
        public NoContextAvailableException() : base("There is not processing context available. Thus we cannot promise 'traceability'")
        {

        }
    }
    public static class EventStoreExtensions
    {
        
        public static IStream GetEventStream<TEvent>(this IEventStore eventStore, Guid id)
            where TEvent : IEvent
        {
            return eventStore.GetEventStream(typeof(TEvent), id);
        }
        public static IStream GetEventStream(this IEventStore eventStore, Type eventType, Guid id, IContext context = null)
        {
            string name = eventType.GetCustomAttribute<StreamNameAttribute>()?.Name ?? eventType.Namespace.LastSegment('.');

            return eventStore.GetStream(name, id, context);
        }

        public static IStream GetCorrelationStream(this IEventStore eventStore, Guid correlationId)
        {
            return eventStore.GetStream("$bc", correlationId);
        }
        public static IStream GetCommandStream<TCommand>(this IEventStore eventStore, Guid id)
            where TCommand : ICommand
        {
            
            string name = typeof(TCommand).GetCustomAttribute<StreamNameAttribute>()?.Name ?? typeof(TCommand).Namespace.LastSegment('.');
            
            return eventStore.GetStream($"{eventStore.Settings.CommandStreamPrefix}{name}", id);
        }
    }
}