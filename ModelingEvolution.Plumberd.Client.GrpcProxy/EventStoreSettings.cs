﻿using System;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Serialization;
using Microsoft.Extensions.Logging;
using Modellution.Logging;

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    public class EventStoreSettings : IEventStoreSettings
    {
        public bool IsDevelopment { get; }
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
            bool isDevelopment, 
            Func<Type, string[]> eventNamingConvention = null,
            ILogger logger = null,
            string projectionStreamPrefix = "/",
            string commandStreamPrefix = ">")
        {
            RecordNamingConvention = eventNamingConvention ?? new EventTypeNameConverter().Convert;
            Logger = logger ?? LogFactory.GetLogger<EventStoreSettings>();
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