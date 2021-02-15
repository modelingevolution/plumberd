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
    public class GrpcCommandInvokerFacade : ICommandInvoker, IAsyncDisposable
    {
        private static ulong _counter = 0;
        private readonly GrpcChannel _channel;
        
        private readonly ArrayBufferWriter<byte> _buffer;
        public GrpcCommandInvokerFacade(GrpcChannel channel)
        {
            _channel = channel;
            
            _buffer = new ArrayBufferWriter<byte>(1024*128); // 128 KB
        }


        public Task Execute(Guid id, ICommand c, Guid userId)
        {
            return Execute(id, c, new CommandInvocationContext(id, c, userId));
        }
        public async Task Execute(Guid id, ICommand c, IContext context = null)
        {
            var client = new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel);
            // allocations
            var msg = new WriteReq();
            lock (_buffer)
            {
                Serializer.Serialize(_buffer, c);
                
                msg.Seq = Interlocked.Increment(ref _counter);
                msg.SteamId = new UUID() { Value = ByteString.CopyFrom(id.ToByteArray()) };
                msg.TypeId = new UUID() { Value = ByteString.CopyFrom(c.GetType().NameHash()) };
                msg.Data = ByteString.CopyFrom(_buffer.WrittenSpan);
                _buffer.Clear();
            }

            var writeStream = client.WriteStream();
            await writeStream.RequestStream.WriteAsync(msg);
            await writeStream.RequestStream.CompleteAsync();
        }


        public async ValueTask DisposeAsync()
        {
            
        }
    }
}
