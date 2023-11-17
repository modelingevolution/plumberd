using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;

namespace ModelingEvolution.Plumberd.EventStore
{
    static class StreamExtensions
    {
        public static async Task<int> ReadAllAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            var r = await s.ReadAsync(buffer, offset, count);
            while (r != count)
            {
                var n = await s.ReadAsync(buffer, offset + r, count - r);
                if (n == 0) 
                    return r;
                r += n;
            }
            return r;
        }

        public static async IAsyncEnumerable<ResolvedEvent> OnlyNew(this IAsyncEnumerable<ResolvedEvent> items, StreamPosition? start=null)
        {
            StreamPosition p = start ?? StreamPosition.Start;
            await foreach (var i in items)
            {
                if (i.OriginalEventNumber < p) continue;
                p = p.Next();
                yield return i;
            }
        }
    }

    public class GrpcHttpClientHandler : HttpClientHandler
    {
        public GrpcHttpClientHandler()
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Version = HttpVersion.Version20;
            return base.Send(request, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Version = HttpVersion.Version20;
            return await base.SendAsync(request,cancellationToken);
        }
    }
}