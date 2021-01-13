using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.AspNetCore.Server.Internal;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ProtoBuf;
using Serilog;
using EventHandler = ModelingEvolution.Plumberd.EventStore.EventHandler;
using MetadataProperty = ModelingEvolution.EventStore.GrpcProxy.MetadataProperty;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public class EventStoreProxy : GrpcEventStoreProxy.GrpcEventStoreProxyBase
    {
        private readonly TypeRegister _typeRegister;
        private readonly ICommandInvoker _commandInvoker;
        private readonly IEventStore _eventStore;
        private readonly ILogger _logger;

        public EventStoreProxy(TypeRegister typeRegister, 
            ICommandInvoker commandInvoker, 
            IEventStore eventStore, ILogger logger)
        {
            _typeRegister = typeRegister;
            _commandInvoker = commandInvoker;
            _eventStore = eventStore;
            _logger = logger;
        }

        public async override Task ReadStream(ReadReq request, IServerStreamWriter<ReadRsp> responseStream, ServerCallContext context)
        {
            _logger.Information("GrpcProxy -> ReadStream: {isAuthenticated} {userId} {userName}", context.IsAuthenticated(),
                context.UserId(), context.UserName());
            
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(128 * 1024);

            if (request.SchemaCase == ReadReq.SchemaOneofCase.GenericSchema)
            {
                SemaphoreSlim subExit = new SemaphoreSlim(0);
                EventHandler handler = (c,m,e) => Transfer(c,m,e,responseStream,context, buffer);
                ProjectionSchema schema = new ProjectionSchema
                {
                    ProjectionName = request.GenericSchema.Name,
                    Script = request.GenericSchema.Script,
                    StreamName = request.GenericSchema.StreamName
                };
                _logger.Information("GrpcProxy -> Subscribing -> Generic({projectionName},{script},{steamName})", 
                    schema.ProjectionName, 
                    schema.Script, 
                    schema.StreamName);

                IProcessingContextFactory f = new GrpcContextFactory();
                await _eventStore.Subscribe(schema,
                    request.FromBeginning,
                    request.IsPersistent,
                    handler, f);
                await subExit.WaitAsync();
            }
            else if (request.SchemaCase == ReadReq.SchemaOneofCase.EventTypeSchema)
            {
                IProcessingContextFactory f = new GrpcContextFactory();
                SemaphoreSlim subExit = new SemaphoreSlim(0);
                EventHandler handler = (c, m, e) => Transfer(c, m, e, responseStream, context, buffer);

                _logger.Information("GrpcProxy -> Subscribing -> EventType({name,eventTypes})",
                    request.EventTypeSchema.Name,
                    string.Join(", ", request.EventTypeSchema.EventTypes));

                await _eventStore.Subscribe(request.EventTypeSchema.Name,
                    request.FromBeginning,
                    request.IsPersistent,
                    handler, f, request.EventTypeSchema.EventTypes.ToArray());

                await subExit.WaitAsync();
            }

        }

        private async Task Transfer(IProcessingContext context,
            IMetadata metadata,
            IRecord ev,
            IServerStreamWriter<ReadRsp> responseStream,
            ServerCallContext serverCallContext,
            ArrayBufferWriter<byte> buffer)
        {
            ReadRsp rsp = new ReadRsp();
            buffer.Clear();
            Serializer.Serialize(buffer, ev);
            rsp.Data = ByteString.CopyFrom(buffer.WrittenSpan);
            rsp.TypeId = new UUID() {Value = ByteString.CopyFrom(ev.GetType().NameId().ToByteArray())};

            foreach (var i in metadata.Schema.Properties)
            {
                var value = metadata[i];
                var id = i.Name.ToGuid();
                buffer.Clear();
                if (value is DateTimeOffset dto)
                {
                    Serializer.Serialize(buffer, dto.DateTime);
                    Serializer.Serialize(buffer, dto.Offset);
                }
                else
                {
                    Serializer.Serialize(buffer, value);
                }

                var metadataProperty = new MetadataProperty()
                {
                    Id= new UUID() { Value = ByteString.CopyFrom(id.ToByteArray()) },
                    Data = ByteString.CopyFrom(buffer.WrittenSpan)
                };
                rsp.MetadataProps.Add(metadataProperty);
            }
            _logger.Information("GrpcProxy -> Subscription.WriteAsync");
            await responseStream.WriteAsync(rsp);
        }

        public async override Task WriteStream(IAsyncStreamReader<WriteReq> requestStream, IServerStreamWriter<WriteRsp> responseStream, ServerCallContext context)
        {
            await foreach (var i in requestStream.ReadAllAsync())
            {
                var steamId = new Guid(i.SteamId.Value.Span);
                Guid typeId = new Guid(i.TypeId.Value.Span);
                var type = _typeRegister[typeId];
                var cmd = Serializer.Deserialize(type, i.Data.Memory) as ICommand;
                
                await _commandInvoker.Execute(steamId, cmd);

                await responseStream.WriteAsync(new WriteRsp() { Seq = i.Seq });
            }
            
        }
    }

}
