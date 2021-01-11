using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using ModelingEvolution.EventStore.GrpcProxy;
using ProtoBuf;

#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    /// <summary>
    /// Designed to be thread-safe
    /// </summary>
    public class GrpcCommandInvokerProxy : ICommandInvoker, IAsyncDisposable
    {
        private static ulong _counter = 0;
        private readonly GrpcChannel _channel;
        private readonly Lazy<GrpcEventStoreProxy.GrpcEventStoreProxyClient> _client;
        private readonly Lazy<AsyncDuplexStreamingCall<WriteReq, WriteRsp>> _writeCall;
        private readonly ArrayBufferWriter<byte> _buffer;
        public GrpcCommandInvokerProxy(GrpcChannel channel)
        {
            _channel = channel;
            _client = new Lazy<GrpcEventStoreProxy.GrpcEventStoreProxyClient>(OnCreateClient);
            _writeCall = new Lazy<AsyncDuplexStreamingCall<WriteReq,WriteRsp>>(OnCreateWriteCall);
            _buffer = new ArrayBufferWriter<byte>(1024*128); // 128 KB
        }

        private AsyncDuplexStreamingCall<WriteReq, WriteRsp> OnCreateWriteCall()
        {
            return _client.Value.WriteStream();
        }

        private GrpcEventStoreProxy.GrpcEventStoreProxyClient OnCreateClient()
        {
            return new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel);
        }

        public async Task Execute(Guid id, ICommand c, IContext context = null)
        {
            var msg = new WriteReq();
            lock (_buffer)
            {
                Serializer.Serialize(_buffer, c);

                msg.Seq = Interlocked.Increment(ref _counter);
                msg.TypeId = new UUID() { Value = ByteString.CopyFrom(c.GetType().NameHash()) };
                msg.Data = ByteString.CopyFrom(_buffer.WrittenSpan);
                _buffer.Clear();
            }

            await this._writeCall.Value.RequestStream.WriteAsync(msg);
            await this._writeCall.Value.RequestStream.CompleteAsync();
        }


        public async ValueTask DisposeAsync()
        {
            if (_writeCall.IsValueCreated)
                await _writeCall.Value.RequestStream.CompleteAsync();

            _channel.Dispose();
        }
    }
}
