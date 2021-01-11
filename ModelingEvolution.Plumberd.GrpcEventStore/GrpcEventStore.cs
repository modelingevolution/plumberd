using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.Client;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using EventHandler = ModelingEvolution.Plumberd.EventStore.EventHandler;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.GrpcEventStore
{
    public class GrpcEventStore : IEventStore
    {
        private class Stream : IStream
        {
            private GrpcEventStore _parent;

            public Stream(GrpcEventStore grpcEventStore, string category, Guid id, IContext context)
            {
                this._parent = grpcEventStore;
            }

            public IEventStore EventStore { get; }
            public string Category { get; }
            public Guid Id { get; }
            public async Task Append(IRecord ev, IMetadata m)
            {
                //_parent._client.AppendToStreamAsync()
            }

            public IAsyncEnumerable<IRecord> Read()
            {
                throw new NotImplementedException();
            }
        }

        private EventStoreClient _client;

        public GrpcEventStore(EventStoreClientSettings connectionSettings, IEventStoreSettings settings)
        {
            this._client = new EventStoreClient(connectionSettings);
            Settings = settings;
        }
        public IEventStoreSettings Settings { get; }
        public IStream GetStream(string category, Guid id, IContext context = null)
        {
            return new Stream(this, category, id, context);
        }

        public async Task Subscribe(string name, bool fromBeginning, bool isPersistent, EventHandler onEvent,
            IProcessingContextFactory processingContextFactory,
            ProjectionSchema schema,
            params string[] sourceEventTypes)
        {
            
        }
    }
}
