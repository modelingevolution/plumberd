using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

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
            string name = eventType.GetCustomAttribute<StreamAttribute>()?.Category ?? eventType.Namespace.LastSegment('.');

            return eventStore.GetStream(name, id, context);
        }

        public static IStream GetCorrelationStream(this IEventStore eventStore, Guid correlationId, ContextScope context)
        {
            var serializer = eventStore.Settings.MetadataSerializerFactory.Get(context);
            return eventStore.GetStream("$bc", correlationId, null, serializer);
        }
        public static IStream GetCommandStream<TCommand>(this IEventStore eventStore, Guid id)
            where TCommand : ICommand
        {
            var commandType = typeof(TCommand);
            return GetCommandStream(eventStore, commandType,id);
        }

        public static IStream GetCommandStream(this IEventStore eventStore, Type commandType, Guid id, IContext context = null)
        {
            string name = commandType.GetCustomAttribute<StreamAttribute>()?.Category ?? commandType.Namespace.LastSegment('.');

            return eventStore.GetStream($"{eventStore.Settings.CommandStreamPrefix}{name}", id, context);
        }
    }
}