using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd;
using ProtoBuf;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public class TypeRegister
    {
        private Dictionary<Guid, Type> _index;

        public TypeRegister Index(params Type[] types)
        {
            foreach (var i in types)
            {
                var id = i.NameId();
                if(!_index.TryGetValue(id, out Type t))
                    _index.Add(id, i);
            }

            return this;
        }

        public Type this[Guid id]
        {
            get
            {
                if (_index.TryGetValue(id, out var t))
                    return t;
                return null;
            }
        }
        public TypeRegister()
        {
            _index = new Dictionary<Guid, Type>();
        }
    }
    public class EventStoreProxy : GrpcEventStoreProxy.GrpcEventStoreProxyBase
    {
        private readonly TypeRegister _typeRegister;
        private readonly ICommandInvoker _commandInvoker;
        public EventStoreProxy(TypeRegister typeRegister, ICommandInvoker commandInvoker)
        {
            _typeRegister = typeRegister;
            _commandInvoker = commandInvoker;
        }

        public async override Task ReadStream(ReadReq request, IServerStreamWriter<ReadRsp> responseStream, ServerCallContext context)
        {
            // Subscribe to EventStore using NEW connection. 
            var httpContext = context.GetHttpContext();
            
            var user = httpContext.User;
            var userId = user.Claims
                .Where(x => x.Type == ClaimTypes.NameIdentifier || x.Type == "sub")
                .Select(x => x.Value)
                .FirstOrDefault();

            Debug.WriteLine("User IsAuthenticated:" + httpContext.User?.Identity?.IsAuthenticated);
            Debug.WriteLine("User: " + user?.Identity?.Name ?? "Anonymous");
            StringValues header = httpContext.Request.Headers["Authorization"];
            Debug.WriteLine("Authorization Header:" + (header.Count >= 1 ? "present" : "undefined"));
            Debug.WriteLine("Headers: " + string.Join(", ",httpContext.Request.Headers.Keys));
            Debug.WriteLine("UserId:  " + userId);

            await Task.Delay(1000);
        }

        public async override Task WriteStream(IAsyncStreamReader<WriteReq> requestStream, IServerStreamWriter<WriteRsp> responseStream, ServerCallContext context)
        {
            await foreach (var i in requestStream.ReadAllAsync())
            {
                Guid id = new Guid(i.TypeId.Value.Span);
                var t = _typeRegister[id];
                var cmd = Serializer.Deserialize(t, i.Data.Memory) as ICommand;

                await _commandInvoker.Execute(id, cmd);

                await responseStream.WriteAsync(new WriteRsp() { Seq = i.Seq });
            }
            
        }
    }

}
