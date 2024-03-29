﻿using System;

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    public static class EventStoreExtensions
    {
        public static PlumberBuilder WithProxyEventStore(this PlumberBuilder builder, Action<ProxyEventStoreBuilder> proxyConfig, IServiceProvider sp)
        {
            ProxyEventStoreBuilder b = new ProxyEventStoreBuilder();
            b.WithLoggerFactory(builder.DefaultLoggerFactory);
            proxyConfig(b);
            return builder.WithDefaultEventStore(b.Build(sp));
        }
    }
}