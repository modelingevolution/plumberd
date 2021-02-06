using System.Runtime.CompilerServices;
using System;

[assembly: InternalsVisibleTo("ModelingEvolution.Plumberd.Tests")]

namespace ModelingEvolution.Plumberd.EventStore
{
    public static class EventStoreExtensions
    {
        public static PlumberBuilder WithDefaultEventStore(this PlumberBuilder builder, 
            Func<NativeEventStoreBuilder, NativeEventStoreBuilder> configureEventStore, 
            bool checkConnectivity = true)
        {
            NativeEventStoreBuilder b = new NativeEventStoreBuilder();
            b = configureEventStore(b);
            b.WithLogger(builder.Logger);
            return builder.WithDefaultEventStore(b.Build(checkConnectivity));
        }
    }
}