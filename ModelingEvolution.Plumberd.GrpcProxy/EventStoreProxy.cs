using System;
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
