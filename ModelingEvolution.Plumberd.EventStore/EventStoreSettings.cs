using System;
using ModelingEvolution.Plumberd.Serialization;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
namespace ModelingEvolution.Plumberd.EventStore
{
    public class EventStoreSettings : IEventStoreSettings
    {
        public bool IsDevelopment { get;  }
        public IMetadataFactory MetadataFactory { get; }
        public IMetadataSerializerFactory MetadataSerializerFactory { get; }
        public IRecordSerializer Serializer { get; }
        public string ProjectionStreamPrefix { get; }
        public string CommandStreamPrefix { get; }
        public Func<Type, string[]> RecordNamingConvention { get; }
        
        public ILogger Logger { get; }
        

        public EventStoreSettings(IMetadataFactory metadataFactory, 
            IMetadataSerializerFactory metadataSerializer, 
            IRecordSerializer serializer, bool isDevelopment, 
            Func<Type, string[]> eventNamingConvention = null, 
            ILogger logger = null, 
            string projectionStreamPrefix = "/", 
            string commandStreamPrefix = ">")
        {
            RecordNamingConvention = eventNamingConvention ?? new EventTypeNameConverter().Convert;
            Logger = logger ?? throw new ArgumentNullException("logger");
            MetadataFactory = metadataFactory;
            MetadataSerializerFactory = metadataSerializer;
            Serializer = serializer;
            IsDevelopment = isDevelopment;

            Logger = logger;
            ProjectionStreamPrefix = projectionStreamPrefix;
            CommandStreamPrefix = commandStreamPrefix;
        }
    }
}