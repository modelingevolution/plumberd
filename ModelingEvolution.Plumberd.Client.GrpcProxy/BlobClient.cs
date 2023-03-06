using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Google.Protobuf;
using Grpc.Core;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd.EventStore;
using ProtoBuf;
using Microsoft.Extensions.Logging;


namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    public static class StreamExtensions
    {
        public static ValueTask<int> ReadBlock(this Stream s, byte[] buffer)
        {
            return s.ReadBlock(buffer, 0, buffer.Length);
        }
        public static async ValueTask<int> ReadBlock(this Stream s, byte[] buffer, int offset, int count)
        {
            int numRead = 0;
            do
            {
                int n = await s.ReadAsync(buffer, offset+numRead, count);
                if (n == 0)
                {
                    break;
                }

                numRead += n;
                count -= n;
            } while (count > 0);

            return numRead;
        }
    }
    public class BlobClient
    {
        
        private readonly Channel _channel;
        private GrpcEventStoreProxy.GrpcEventStoreProxyClient _client;
        private ISessionManager _sessionManager;
        private readonly ILogger<BlobClient> _logger;
        const int BUFFER_SIZE = 64 * 1024; // 64KB
        public BlobClient(Channel channel, ISessionManager sessionManager, ILogger<BlobClient> logger)
        {
            _channel = channel;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task Write(Guid id, 
            string category, 
            string fileName,
            bool forceOverride, 
            Stream data, BlobUploadReason reason=null)
        {
            _client ??= new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel.GrpcChannel);
            
            Grpc.Core.Metadata m = new Grpc.Core.Metadata();
            m.Add("file_name", HttpUtility.HtmlEncode(fileName));
            m.Add("table_name", HttpUtility.HtmlEncode(category));
            m.Add("id-bin", id.ToByteArray());

            long len = data.Length;
            m.Add("size-bin", BitConverter.GetBytes(len));
            m.Add("chunk_size-bin", BitConverter.GetBytes(BUFFER_SIZE));
            m.Add("force_override-bin", BitConverter.GetBytes(forceOverride));
            m.Add("sessionid-bin", _sessionManager.Default().ToByteArray());
            if (reason != null)
            {
                var reasonBuffer = new ArrayBufferWriter<byte>(1024);
                Serializer.Serialize(reasonBuffer, reason);
                m.Add("upload_reason-bin", reasonBuffer.WrittenSpan.ToArray());
            }
            // context is disposable but we cannot DISPOSE IT!
            
            var context = _client.WriteBlob(m);
            
            byte[] buffer = new byte[BUFFER_SIZE];
            //int read = BUFFER_SIZE;
            long written = 0;
            int i = 0;
            for (; written < len; i++) // MAX 64MB
            {
                int read = await data.ReadBlock(buffer);
                if (read == 0) break;
                BlobChunk b = new BlobChunk() {Data = ByteString.CopyFrom(buffer, 0, read), I = i};
                written += read;
                await context.RequestStream.WriteAsync(b);
                
            }

            await context.RequestStream.WriteAsync(new BlobChunk() {Data = ByteString.Empty, I=i});
            await Task.Delay(100);
            _logger.LogInformation("Written {written}/{len} bytes in {i} iterations. [ {completness}% ]", written, len, i, written/len*100);
            await context.RequestStream.CompleteAsync();
        }

    }
}