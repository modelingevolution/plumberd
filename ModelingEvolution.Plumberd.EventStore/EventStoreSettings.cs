using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModelingEvolution.Plumberd.Serialization;
using Serilog;

namespace ModelingEvolution.Plumberd.EventStore
{

    public class EventTypeNameConverter
    {
        private Dictionary<Type, string[]> _index;

        public EventTypeNameConverter()
        {
            _index = new Dictionary<Type, string[]>();
        }
        public string[] Convert(Type t)
        {
            if (!_index.TryGetValue(t, out var names))
            {
                names = t.GetCustomAttributes<EventTypeNameAttribute>()
                    .Select(x => x.Name)
                    .ToArray();
                if(names.Length == 0)
                    names = new string[] { t.Name };
                _index.Add(t, names);
            }

            return names;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EventTypeNameAttribute : Attribute
    {
        public EventTypeNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
    public class EventStoreSettings : IEventStoreSettings
    {
        public IMetadataFactory MetadataFactory { get; }
        public IMetadataSerializerFactory MetadataSerializerFactory { get; }
        public IRecordSerializer Serializer { get; }
        public string ProjectionStreamPrefix { get; }
        public string CommandStreamPrefix { get; }
        public Func<Type, string[]> RecordNamingConvention { get; }

        public ILogger Logger { get; }
        

        public EventStoreSettings(IMetadataFactory metadataFactory, 
            IMetadataSerializerFactory metadataSerializer, 
            IRecordSerializer serializer, 
            Func<Type, string[]> eventNamingConvention = null, 
            ILogger logger = null, 
            string projectionStreamPrefix = "/", 
            string commandStreamPrefix = ">")
        {
            RecordNamingConvention = eventNamingConvention ?? new EventTypeNameConverter().Convert;
            Logger = logger ?? Log.Logger ?? Serilog.Core.Logger.None;
            MetadataFactory = metadataFactory;
            MetadataSerializerFactory = metadataSerializer;
            Serializer = serializer;
            
            Logger = logger;
            ProjectionStreamPrefix = projectionStreamPrefix;
            CommandStreamPrefix = commandStreamPrefix;
        }
    }
}