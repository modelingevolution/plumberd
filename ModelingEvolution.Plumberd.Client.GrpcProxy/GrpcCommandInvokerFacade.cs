using System;
using System.Buffers;
using System.Collections.Concurrent;
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
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Logging;

#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    public class Channel
    {
        public readonly GrpcChannel GrpcChannel;
        public readonly Uri Address;

        public Channel(GrpcChannel grpcChannel, Uri address)
        {
            GrpcChannel = grpcChannel;
            Address = address;
        }
    }

    public interface ISessionManager
    {
        Guid GetSessionId(Uri url);
        Guid Default();
    }

    public class SessionManager : ISessionManager
    {
        private ConcurrentDictionary<Uri, Guid> _sessions;

        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<Uri, Guid>();
        }
        
        public Guid GetSessionId(Uri url)
        {
            return _sessions.GetOrAdd(url, x => Guid.NewGuid());
        }

        public Guid Default()
        {
            return _sessions.Values.First();
        }
    }
    /// <summary>
    /// Designed to be thread-safe
    /// </summary>
    public class GrpcCommandInvokerFacade : ICommandInvoker, IAsyncDisposable
    {
        private static ulong _counter = 0;
        private static readonly ILogger Log = LogFactory.GetLogger<GrpcCommandInvokerFacade>();
        private readonly Channel _channel;
        private readonly ISessionManager _sessionManager;
        
        private readonly ArrayBufferWriter<byte> _buffer;
        public GrpcCommandInvokerFacade(Channel channel, ISessionManager sessionManager)
        {
            _channel = channel;
            _sessionManager = sessionManager;
            
            _buffer = new ArrayBufferWriter<byte>(1024*128); // 128 KB
        }


        public Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId)
        {
            return Execute(id, c, new CommandInvocationContext(id, c, userId, sessionId));
        }
        public async Task Execute(Guid id, ICommand c, IContext context = null)
        {
            var client = new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel.GrpcChannel);
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

            var metadata = new Grpc.Core.Metadata();
            var sessionId = _sessionManager.GetSessionId(_channel.Address);
            metadata.Add("SessionId-bin", sessionId.ToByteArray());

            var writeStream = client.WriteStream(metadata);
            {
                await writeStream.RequestStream.WriteAsync(msg);
                await writeStream.RequestStream.CompleteAsync();
            }
        }


        public async ValueTask DisposeAsync()
        {
            
        }
    }
}
