using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class EventTypeNameConverter
    {
        private readonly ConcurrentDictionary<Type, string[]> _index = new();

        
        public string[] Convert(Type t)
        {
            return _index.GetOrAdd(t, static type =>
            {
                var names = type.GetCustomAttributes<EventTypeNameAttribute>()
                    .Select(x =>
                        x.ProviderType != null
                            ? ((IEventTypeNameProvider)Activator.CreateInstance(x.ProviderType))?.GetName(type)
                            : x.Name)
                    .ToArray();
                if (names.Length == 0)
                    names = new string[] { type.Name };
                return names;
            });

            
        }
    }
}