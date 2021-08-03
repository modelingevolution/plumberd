﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class EventTypeNameConverter
    {
        private readonly Dictionary<Type, string[]> _index;

        public EventTypeNameConverter()
        {
            _index = new Dictionary<Type, string[]>();
        }
        public string[] Convert(Type t)
        {
            if (!_index.TryGetValue(t, out var names))
            {
                names = t.GetCustomAttributes<EventTypeNameAttribute>()
                    .Select(x => x.ProviderType != null ? ((IEventTypeNameProvider) Activator.CreateInstance(x.ProviderType))?.GetName(t) : x.Name)
                    .ToArray();
                if(names.Length == 0)
                    names = new string[] { t.Name };
                _index.Add(t, names);
            }

            return names;
        }
    }
}