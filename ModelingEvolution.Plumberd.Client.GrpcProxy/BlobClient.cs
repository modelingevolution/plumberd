using System;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using ModelingEvolution.EventStore.GrpcProxy;

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
        const int BUFFER_SIZE = 64 * 1024; // 64KB
        public BlobClient(Channel channel, ISessionManager sessionManager)
        {
            _channel = channel;
            _sessionManager = sessionManager;
        }

        public async Task Write(Guid id, string category, string fileName, bool forceOverride, Stream data)
        {
            _client ??= new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel.GrpcChannel);

            Grpc.Core.Metadata m = new Grpc.Core.Metadata();
            m.Add("file_name", fileName);
            m.Add("table_name", category);
            m.Add("id-bin", id.ToByteArray());
            m.Add("size-bin", BitConverter.GetBytes(data.Length));
            m.Add("chunk_size-bin", BitConverter.GetBytes(BUFFER_SIZE));
            m.Add("force_override-bin", BitConverter.GetBytes(forceOverride));
            m.Add("sessionid-bin", _sessionManager.Default().ToByteArray());
            // context is disposable but we cannot DISPOSE IT!
            var context = _client.WriteBlob(m);
            byte[] buffer = new byte[BUFFER_SIZE];
            //int read = BUFFER_SIZE;
            long written = 0;
            
                for (int i = 0; written < data.Length; i++) // MAX 64MB
                {
                    int read = await data.ReadBlock(buffer);
                    if (read == 0) break;
                    BlobChunk b = new BlobChunk()
                    {
                        Data = ByteString.CopyFrom(buffer, 0, read),
                        I = i
                    };
                    written += read;
                    await context.RequestStream.WriteAsync(b);
                }
            

            Console.WriteLine($"Written {written} bytes.");
            await context.RequestStream.CompleteAsync();
        }

    }
}