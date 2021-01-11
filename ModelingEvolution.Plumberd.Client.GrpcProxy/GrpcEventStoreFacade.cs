using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd.EventStore;
using EventHandler = ModelingEvolution.Plumberd.EventStore.EventHandler;
#pragma warning disable 4014
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    public class GrpcEventStoreFacade : IEventStore
    {
        private readonly GrpcChannel _channel;
        private readonly Lazy<GrpcEventStoreProxy.GrpcEventStoreProxyClient> _client;
        
        private readonly ArrayBufferWriter<byte> _buffer;
        public GrpcEventStoreFacade(GrpcChannel channel)
        {
            _channel = channel;
            _client = new Lazy<GrpcEventStoreProxy.GrpcEventStoreProxyClient>(OnCreateClient);
            _buffer = new ArrayBufferWriter<byte>(1024 * 128); // 128 KB
        }


        private GrpcEventStoreProxy.GrpcEventStoreProxyClient OnCreateClient()
        {
            return new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel);
        }

        public IEventStoreSettings Settings { get; }
        public IStream GetStream(string category, Guid id, IContext context = null)
        {
            throw new NotImplementedException();
        }

        public async Task Subscribe(string name, 
            bool fromBeginning, 
            bool isPersistent, 
            EventHandler onEvent,
            IProcessingContextFactory processingContextFactory, 
            ProjectionSchema schema = null,
            params string[] sourceEventTypes)
        {
            ReadReq r = new ReadReq();

            CancellationToken token = new CancellationToken();
            var result = _client.Value.ReadStream(r, null, null, token);
            
            //Task.Factory.StartNew()
        }

        private void Read(AsyncServerStreamingCall<ReadRsp> callContext)
        {
            
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
        }
    }
}