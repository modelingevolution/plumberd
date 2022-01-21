using System.Runtime.CompilerServices;
using System;

[assembly: InternalsVisibleTo("ModelingEvolution.Plumberd.Tests")]

namespace ModelingEvolution.Plumberd.EventStore
{
    public static class EventStoreExtensions
    {
        public static PlumberBuilder WithTcpEventStore(this PlumberBuilder builder, 
            Func<NativeEventStoreBuilder, NativeEventStoreBuilder> configureEventStore, 
            bool checkConnectivity = true)
        {
            NativeEventStoreBuilder b = new NativeEventStoreBuilder();
            b = configureEventStore(b);
            
            return builder.WithDefaultEventStore(b.Build(checkConnectivity));
        }
        /// <summary>
        /// EXPERIMENTAL!
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configureEventStore"></param>
        /// <param name="checkConnectivity"></param>
        /// <returns></returns>
        public static PlumberBuilder WithGrpcEventStore(this PlumberBuilder builder,
            Func<GrpcEventStoreBuilder, GrpcEventStoreBuilder> configureEventStore,
            bool checkConnectivity = true)
        {
            GrpcEventStoreBuilder b = new GrpcEventStoreBuilder();
            b = configureEventStore(b);

            return builder.WithDefaultEventStore(b.Build(checkConnectivity));
        }
    }
    
}