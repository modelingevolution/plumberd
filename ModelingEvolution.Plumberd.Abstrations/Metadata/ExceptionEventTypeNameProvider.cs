using System;
using System.Reflection;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class ExceptionEventTypeNameProvider : IEventTypeNameProvider
    {
        public string GetName(Type recordType)
        {
            if (recordType.IsGenericType && recordType.GetGenericTypeDefinition() == typeof(EventException<,>))
            {
                var args = recordType.GetGenericArguments();
                var exceptionType = args[1];
                var att = exceptionType.GetCustomAttribute<EventTypeNameAttribute>();
                if (att != null && att.Name != null) return att.Name;
                return exceptionType.Name;
            } else throw new InvalidOperationException("ExceptionEventTypeProvider can be only applied to EventException!");
        }
    }
}