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
using ModelingEvolution.Plumberd.GrpcProxy.Authentication;
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
        private readonly UsersModel _userModel;

        public EventStoreProxy(TypeRegister typeRegister, 
            ICommandInvoker commandInvoker, 
            IEventStore eventStore, ILogger logger, UsersModel userModel)
        {
            _typeRegister = typeRegister;
            _commandInvoker = commandInvoker;
            _eventStore = eventStore;
            _logger = logger;
            _userModel = userModel;
        }

        public async override Task ReadStream(ReadReq request, 
            IServerStreamWriter<ReadRsp> responseStream, 
            ServerCallContext context)
        {
            IDisposable resources = null;
            _logger.Information("GrpcProxy -> Reading started.");
            SemaphoreSlim subExit = new SemaphoreSlim(0);
            context.CancellationToken.Register(() =>
            {
                _logger.Information("GrpcProxy -> Releasing connection.");
                subExit.Release(1);
            });
            try
            {
                await CheckAuthorizationData(context);

                ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(128 * 1024);

                if (request.SchemaCase == ReadReq.SchemaOneofCase.GenericSchema)
                {
                    EventHandler handler = async (c, m, e) =>
                    {
                        try
                        {
                            await Transfer(c, m, e, responseStream, context, buffer);
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.Warning(ex, "Reading ended.");
                            subExit.Release(1);
                        }
                    };
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
                    resources = await _eventStore.Subscribe(schema,
                        request.FromBeginning,
                        request.IsPersistent,
                        handler, f);
                    
                    await subExit.WaitAsync();
                    
                }
                else if (request.SchemaCase == ReadReq.SchemaOneofCase.EventTypeSchema)
                {
                    IProcessingContextFactory f = new GrpcContextFactory();

                    EventHandler handler = async (c, m, e) =>
                    {
                        try
                        {
                            await Transfer(c, m, e, responseStream, context, buffer);
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.Warning(ex, "Reading ended.");
                            subExit.Release(1);
                        }
                    };

                    _logger.Information("GrpcProxy -> Subscribing -> EventType({name,eventTypes})",
                        request.EventTypeSchema.Name,
                        string.Join(", ", request.EventTypeSchema.EventTypes));

                    resources = await _eventStore.Subscribe(request.EventTypeSchema.Name,
                        request.FromBeginning,
                        request.IsPersistent,
                        handler, f, request.EventTypeSchema.EventTypes.ToArray());

                    await subExit.WaitAsync();
                    
                }
            }
            finally
            {
                resources?.Dispose();
                _logger.Information("GrpcProxy -> Reading finished, resources released.");
            }
        }

        private async Task CheckAuthorizationData(ServerCallContext context)
        {
            var uid = context.UserId();
            if (uid.HasValue)
            {
                Debug.WriteLine($"GrpcProxy -> CheckAuthorization: {uid}");
                var streamId = uid.Value;
                var email = context.UserEmail();
                var name = context.UserName();
                var sessionId = context.SessionId();
                var u = _userModel.FindByUserId(streamId);
                if (u == null || u.Name != name || u.Email != email)
                {
                    var cmd = new RetrieveAuthorizationData()
                    {
                        Email = email,
                        Name = name
                    };
                    using (CommandInvocationContext cc = new CommandInvocationContext(streamId, cmd, streamId, sessionId ?? Guid.Empty))
                    {
                        await _commandInvoker.Execute(streamId, cmd, cc);
                    }
                }
            }
            else Debug.WriteLine("No UserId!");
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
                    BitConverter.TryWriteBytes(buffer.GetSpan(sizeof(Int64)), dto.DateTime.Ticks);
                    buffer.Advance(sizeof(Int64));
                    BitConverter.TryWriteBytes(buffer.GetSpan(sizeof(Int64)), dto.Offset.Ticks);
                    buffer.Advance(sizeof(Int64));
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
            _logger.Information("GrpcProxy -> Subscription.WriteResponse({recordType})", ev.GetType().Name);
            await responseStream.WriteAsync(rsp);
        }

        public async override Task WriteStream(IAsyncStreamReader<WriteReq> requestStream, IServerStreamWriter<WriteRsp> responseStream, ServerCallContext context)
        {
            await CheckAuthorizationData(context);

            Guid sessionId = context.SessionId() ?? Guid.Empty;
            await foreach (var i in requestStream.ReadAllAsync())
            {
                var steamId = new Guid(i.SteamId.Value.Span);
                Guid typeId = new Guid(i.TypeId.Value.Span);
                
                var type = _typeRegister[typeId];
                var cmd = Serializer.Deserialize(type, i.Data.Memory) as ICommand;

                using (CommandInvocationContext cc = new CommandInvocationContext(steamId, 
                    cmd, context.UserId() ?? Guid.Empty, sessionId))
                {
                    await _commandInvoker.Execute(steamId, cmd, cc);

                    await responseStream.WriteAsync(new WriteRsp() {Seq = i.Seq});
                }
            }
            
        }
    }

}
