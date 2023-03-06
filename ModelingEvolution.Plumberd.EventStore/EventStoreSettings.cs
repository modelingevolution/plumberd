using System;
using ModelingEvolution.Plumberd.Serialization;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
namespace ModelingEvolution.Plumberd.EventStore
{
    public class EventStoreSettings : IEventStoreSettings
    {
        public ILoggerFactory LoggerFactory { get; }
        public bool IsDevelopment { get;  }
        public IMetadataFactory MetadataFactory { get; }
        public IMetadataSerializerFactory MetadataSerializerFactory { get; }
        public IRecordSerializer Serializer { get; }
        public string ProjectionStreamPrefix { get; }
        public string CommandStreamPrefix { get; }
        public Func<Type, string[]> RecordNamingConvention { get; }
        
        
        

        public EventStoreSettings(IMetadataFactory metadataFactory, 
            IMetadataSerializerFactory metadataSerializer, 
            IRecordSerializer serializer, bool isDevelopment, ILoggerFactory loggerFactory, Func<Type, string[]> eventNamingConvention = null,
            string projectionStreamPrefix = "/", 
            string commandStreamPrefix = ">")
        {
            RecordNamingConvention = eventNamingConvention ?? new EventTypeNameConverter().Convert;
            
            MetadataFactory = metadataFactory;
            MetadataSerializerFactory = metadataSerializer;
            Serializer = serializer;
            IsDevelopment = isDevelopment;
            LoggerFactory = loggerFactory;


            ProjectionStreamPrefix = projectionStreamPrefix;
            CommandStreamPrefix = commandStreamPrefix;
        }
    }
}