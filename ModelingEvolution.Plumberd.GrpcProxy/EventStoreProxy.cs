using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.AspNetCore.Server.Internal;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.GrpcProxy.Authentication;
using ModelingEvolution.Plumberd.Metadata;
using ProtoBuf;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using EventHandler = ModelingEvolution.Plumberd.EventStore.EventHandler;
using MetadataProperty = ModelingEvolution.EventStore.GrpcProxy.MetadataProperty;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public class EventStoreProxy : GrpcEventStoreProxy.GrpcEventStoreProxyBase
    {
        private readonly TypeRegister _typeRegister;
        private readonly IPlumberRuntime _plumberRuntime;
        private readonly ICommandInvoker _commandInvoker;
        private readonly IEventStore _eventStore;
        private readonly ILogger _logger;
        private readonly UsersModel _userModel;
        private readonly IConfiguration _config;
        public EventStoreProxy(TypeRegister typeRegister, 
            IPlumberRuntime plumberRuntime,
            ICommandInvoker commandInvoker, 
            IEventStore eventStore, ILogger<EventStoreProxy> logger, 
            UsersModel userModel,
            IConfiguration config)
        {
            _typeRegister = typeRegister;
            _plumberRuntime = plumberRuntime;
            _commandInvoker = commandInvoker;
            _eventStore = eventStore;
            _logger = logger;
            _userModel = userModel;
            _config = config;
        }
        public override async Task<BlobData> WriteBlob(IAsyncStreamReader<BlobChunk> requestStream,
            ServerCallContext context)
        {
            long writtenBytes = 0;
            int i = 0;
            try
            {
                _logger.LogInformation("Blob writing started...");
                var userId = await CheckAuthorizationData(context);
                _logger.LogInformation("UserId from authorized data:{userID}. ", userId);
                var blobDescriptor = Get(context.RequestHeaders);
                _logger.LogInformation("Blob Descriptor. ", userId);
                var blobDir = _config["BlobDir"];
                var root = string.IsNullOrWhiteSpace(blobDir)
                    ? Path.Combine(Path.GetTempPath(), "Modellution")
                    : blobDir;
                if (!Directory.Exists(root))
                    Directory.CreateDirectory(root);


                var dir = Path.Combine(root, $"{blobDescriptor.Category.ToLower()}-{blobDescriptor.Id}");
                int number = 0;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                else
                {
                    var nr = Directory.EnumerateFiles(dir, "*.dat")
                        .Select(x => (int?) int.Parse(Path.GetFileNameWithoutExtension(Path.GetFileName(x)))).Max();

                    if (nr.HasValue)
                    {
                        if (!blobDescriptor.ForceOverride)
                            number = nr.Value + 1;
                        else number = nr.Value;
                    }
                    else number = 0;
                }

                var fileName = Path.Combine(dir, $"{number}.dat");
                var metaFile = Path.Combine(dir, $"{number}.meta");
                await File.WriteAllTextAsync(metaFile, JsonSerializer.Serialize(blobDescriptor));

                
                var fileMode = blobDescriptor.ForceOverride ? FileMode.Create : FileMode.CreateNew;
                _logger.LogInformation("Writing blob {blobDescriptor}", blobDescriptor);
                using (var stream = new FileStream(fileName, fileMode, FileAccess.Write, FileShare.None))
                {
                   
                    await foreach (var chunk in requestStream.ReadAllAsync())
                    {
                        if (chunk.I != i++)
                            throw new InvalidOperationException("Unsupported");
                        //var expectedLocation = chunk.I * blobDescriptor.ChunkSize;
                        //if (stream.Position != expectedLocation && expectedLocation < MAX_FILE_SIZE)
                        //    stream.Seek(expectedLocation, SeekOrigin.Begin);

                        await stream.WriteAsync(chunk.Data.Memory);
                        writtenBytes += chunk.Data.Memory.Length;
                        
                        if (blobDescriptor.Size == writtenBytes) break;
                    }
                }

                _logger.LogInformation("Blob {fileName} written. Written {writtenBytes} iteration {iteration}. ", fileName, writtenBytes, i);
                await InvokeUploadEvent(context, blobDescriptor, writtenBytes, userId, fileName);

                return new BlobData()
                {
                    Url = $"/blob/{blobDescriptor.Category}-{blobDescriptor.Id}",
                    WrittenBytes = writtenBytes
                };
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Could not write blob. {Headers}. Written {writtenBytes} iteration {iteration}.", GetHeaders(context), writtenBytes, i);
                throw;
            }
        }

        private string GetHeaders(ServerCallContext context)
        {
            // very useful when configuring proxy.
            
            StringBuilder sb = new StringBuilder();
            foreach (var i in context.RequestHeaders)
            {
                var value = i.IsBinary ? BitConverter.ToString(i.ValueBytes) : i.Value;
                sb.AppendLine($"metadata: {i.Key}:{value}");
            }

            // sb.AppendLine("---");
            // foreach (var h in context.GetHttpContext().Request.Headers)
            // {
            //     sb.AppendLine($"http: {h.Key}: {string.Join('|', h.Value)}");
            // }

            return sb.ToString();
        }

        private static string[] bitmapExtensions = {".png", ".jpg", ".jpeg", ".bmp", ".webp"};

        private async Task InvokeUploadEvent(ServerCallContext context, 
            BlobDescriptor blobDescriptor, 
            long writtenBytes,
            Guid userId, string fileName)
        {
            ExtraProperties props = null;
            var ext = Path.GetExtension(blobDescriptor.FileName).ToLowerInvariant();
            if (bitmapExtensions.Contains(ext))
            {
                using var bitmap = SKBitmap.Decode(fileName);
                using var image = SKImage.FromBitmap(bitmap);
                props = new ImageProperties() 
                { 
                    Width = image.Width, 
                    Height = image.Height
                };
            } 
            else if(ext == ".svg")
            {
                // let's read view-port.
                var (width,height) = SvgSizeParser.Load(fileName);
                props = new ImageProperties()
                {
                    Width = width,
                    Height = height
                };
            }

            
            var uploadBlob = new UploadBlob()
            {
                Name = blobDescriptor.FileName,
                StreamCategory = blobDescriptor.Category,
                Size = writtenBytes,
                Reason = blobDescriptor.BlobUploadReason,
                Properties = props
            };

            var streamId = blobDescriptor.Id;
            var sessionId = context.SessionId();
            
            await _commandInvoker.Execute(streamId, uploadBlob, userId, sessionId??Guid.Empty);
            
        }
         
        private static BlobDescriptor Get(Grpc.Core.Metadata metadata)
        {
            string fileName = HttpUtility.HtmlDecode(metadata.GetValue("file_name"));
            string table = HttpUtility.HtmlDecode(metadata.GetValue("table_name"));
            byte[] sha1 = metadata.GetValueBytes("bin_sha1-bin");
            byte[] id = metadata.GetValueBytes("id-bin");
            byte[] size64 = metadata.GetValueBytes("size-bin");
            byte[] chunkSize32 = metadata.GetValueBytes("chunk_size-bin");
            byte[] forceOverride = metadata.GetValueBytes("force_override-bin");
            byte[] reason = metadata.GetValueBytes("upload_reason-bin");
            BlobUploadReason blobUploadReason = null;
            if (reason != null)
            {
                blobUploadReason = Serializer.Deserialize<BlobUploadReason>(reason.AsSpan());
            }
            var desc = new BlobDescriptor(fileName,
                table,
                sha1 == null ? Guid.Empty : new Guid(sha1),
                new Guid(id),
                size64.SafeToInt64(-1,"Size"),
                chunkSize32.SafeToInt32(-1,"ChunkSize"),
                forceOverride.SafeToBoolean(false, "ForceOverride"), 
                blobUploadReason);

            if (string.IsNullOrWhiteSpace(desc.FileName))
                throw new ArgumentException("FileName");
            if (string.IsNullOrWhiteSpace(desc.Category))
                throw new ArgumentException("TableName");
            if (desc.ChunkSize < -1 || desc.ChunkSize > 1024 * 1024)
                throw new ArgumentException("ChunkSize");
            if (desc.Size <= -1 || desc.Size > MAX_FILE_SIZE) // 64MB
                throw new ArgumentException("Size");
            if (desc.Id == Guid.Empty)
                throw new ArgumentException("Id");

            return desc;
        }

        private const long MAX_FILE_SIZE = 1024 * 1024 * 64;
        public async override Task ReadStream(ReadReq request, 
            IServerStreamWriter<ReadRsp> responseStream, 
            ServerCallContext context)
        {
            IDisposable resources = null;
            _logger.LogInformation("GrpcProxy -> Reading started.");
            using SemaphoreSlim subExit = new SemaphoreSlim(0);
            context.CancellationToken.Register(() =>
            {
                _logger.LogInformation("GrpcProxy -> Releasing connection.");
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
                            //Console.WriteLine("-> " + e.GetType().Name);
                            await Transfer(c, m, e, responseStream, context, buffer);
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.LogWarning(ex, "Reading ended.");
                            subExit.Release(1);
                        }
                    };
                    ProjectionSchema schema = new ProjectionSchema
                    {
                        ProjectionName = request.GenericSchema.Name,
                        Script = request.GenericSchema.Script,
                        StreamName = request.GenericSchema.StreamName,
                        IsDirect = string.IsNullOrWhiteSpace(request.GenericSchema.Name)
                    };
                    _logger.LogInformation("GrpcProxy -> Subscribing -> Generic({projectionName},{script},{steamName},{isDirect})",
                        schema.ProjectionName,
                        schema.Script,
                        schema.StreamName, 
                        schema.IsDirect);

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
                            _logger.LogWarning(ex, "Reading ended.");
                            subExit.Release(1);
                        }
                    };

                    _logger.LogInformation("GrpcProxy -> Subscribing -> EventType({name},{eventTypes})",
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
                _logger.LogInformation("GrpcProxy -> Reading finished, resources released.");
            }
        }

        private async Task<Guid> CheckAuthorizationData(ServerCallContext context)
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
                    if (string.IsNullOrWhiteSpace(email))
                        throw new AuthorizationDeniedException("Email cannot be empty.");

                    if (string.IsNullOrWhiteSpace(name))
                        throw new AuthorizationDeniedException("Name cannot be empty.");

                    var cmd = new RetrieveAuthorizationData()
                    {
                        Email = email,
                        Name = name
                    };
                    using (CommandInvocationContext cc = new CommandInvocationContext(streamId, cmd, streamId, sessionId ?? Guid.Empty, new Version(0,0)))
                    {
                        await _commandInvoker.Execute(streamId, cmd, cc);
                    }
                }

                return streamId;
            }
            else
            {
                Debug.WriteLine("No UserId!");
                return Guid.Empty;
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

            var correlationId = metadata.CorrelationId();
            if (this._plumberRuntime.IgnoreFilter.IsFiltered(correlationId))
            {
                _logger.LogInformation("GrpcProxy -> {recordType} Ignored for transmission.", ev.GetType().Name);
                return;
            }
            _logger.LogInformation("GrpcProxy -> Subscription.WriteResponse({recordType})", ev.GetType().Name);
            await responseStream.WriteAsync(rsp);
        }

        public override async Task<WriteRsp> WriteStream(WriteReq request, ServerCallContext context)
        {
            await CheckAuthorizationData(context);

            Guid sessionId = context.SessionId() ?? Guid.Empty;

            var steamId = new Guid(request.StreamId.Value.Span);
            if (steamId == Guid.Empty) return null;

            Guid typeId = new Guid(request.TypeId.Value.Span);
            var type = _typeRegister.GetRequiredType(typeId);
            var cmd = Serializer.NonGeneric.Deserialize(type, request.Data.Memory) as ICommand;
            var version = Version.Parse(request.Version);
            using (CommandInvocationContext cc = new CommandInvocationContext(steamId,
                cmd, context.UserId() ?? Guid.Empty, sessionId, version))
            {
                await _commandInvoker.Execute(steamId, cmd, cc);
                return new WriteRsp() { Status = 200 };
            }
        }
    }

    public class AuthorizationDeniedException : Exception
    {
        public AuthorizationDeniedException(string msg) : base(msg){}
        
    }
}
