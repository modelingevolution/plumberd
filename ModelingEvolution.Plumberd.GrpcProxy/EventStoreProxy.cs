using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Eventstore;
using Grpc.Core;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public class EventStoreProxy : EventStore.EventStoreBase
    {
        public async override Task ReadStream(ReadReq request, IServerStreamWriter<ReadRsp> responseStream, ServerCallContext context)
        {
            // Subscribe to EventStore using NEW connection. 
            var httpContext = context.GetHttpContext();
            
            var user = httpContext.User;
            Debug.WriteLine("User IsAuthenticated:" + httpContext.User?.Identity?.IsAuthenticated);
            Debug.WriteLine("User:" + user?.Identity?.Name ?? "Anonymous");
            Debug.WriteLine("Authorization Header:" + httpContext.Request.Headers["Authorization"]);
            Debug.WriteLine("Headers: "+ string.Join(", ",httpContext.Request.Headers.Keys));
            await Task.Delay(1000);
        }

        public async override Task WriteStream(IAsyncStreamReader<WriteReq> requestStream, IServerStreamWriter<WriteRsp> responseStream, ServerCallContext context)
        {
            await foreach (var i in requestStream.ReadAllAsync())
            {
                await responseStream.WriteAsync(new WriteRsp());
            }
            
        }
    }
}
