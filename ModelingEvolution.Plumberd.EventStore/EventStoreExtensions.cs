using System.Runtime.CompilerServices;
using System;

[assembly: InternalsVisibleTo("ModelingEvolution.Plumberd.Tests")]

namespace ModelingEvolution.Plumberd.EventStore
{
    public static class EventStoreExtensions
    {
        public static PlumberBuilder WithGrpc(this PlumberBuilder builder, 
            Func<ConfigurationBuilder, ConfigurationBuilder> configureEventStore, 
            bool checkConnectivity = true)
        {
            ConfigurationBuilder b = new ConfigurationBuilder();
            b.WithLoggerFactory(builder.DefaultLoggerFactory);
            b = configureEventStore(b);
            
            return builder.WithDefaultEventStore(b.Build(checkConnectivity));
        }
        
    }
    
}