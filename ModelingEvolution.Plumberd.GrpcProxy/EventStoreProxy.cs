using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.AspNetCore.Server.Internal;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
    public readonly struct BlobDescriptor
    {
        public readonly BlobUploadReason BlobUploadReason { get; init; }
        public readonly string FileName { get; init; }
        public readonly string Category { get; init; }
        public readonly Guid Sha1 { get; init; }
        public readonly Guid Id { get; init; }
        public readonly long Size { get; init; }
        public readonly int ChunkSize { get; init; }
        public readonly bool ForceOverride { get; init; }

        public BlobDescriptor(string fileName,
            string category,
            Guid sha1,
            Guid id,
            long size,
            int chunkSize, bool forceOverride, 
            BlobUploadReason blobUploadReason)
        {
            BlobUploadReason = blobUploadReason;
            FileName = fileName;
            Category = category;
            Sha1 = sha1;
            Id = id;
            Size = size;
            ChunkSize = chunkSize;
            ForceOverride = forceOverride;
        }
    }
    public class EventStoreProxy : GrpcEventStoreProxy.GrpcEventStoreProxyBase
    {
        private readonly TypeRegister _typeRegister;
        private readonly ICommandInvoker _commandInvoker;
        private readonly IEventStore _eventStore;
        private readonly ILogger _logger;
        private readonly UsersModel _userModel;
        private readonly IConfiguration _config;
        public EventStoreProxy(TypeRegister typeRegister, 
            ICommandInvoker commandInvoker, 
            IEventStore eventStore, ILogger logger, 
            UsersModel userModel, 
            IConfiguration config)
        {
            _typeRegister = typeRegister;
            _commandInvoker = commandInvoker;
            _eventStore = eventStore;
            _logger = logger;
            _userModel = userModel;
            _config = config;
        }
        public override async Task<BlobData> WriteBlob(IAsyncStreamReader<BlobChunk> requestStream,
            ServerCallContext context)
        {
            var userId = await CheckAuthorizationData(context);
            var blobDescriptor = Get(context.RequestHeaders);
            var blobDir = _config["BlobDir"];
            var root = string.IsNullOrWhiteSpace(blobDir) ? Path.Combine(Path.GetTempPath(), "Modellution") : blobDir;
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
                    .Select(x=>(int?)int.Parse(Path.GetFileNameWithoutExtension(Path.GetFileName(x)))).Max();

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

            long writtenBytes = 0;
            var fileMode = blobDescriptor.ForceOverride ? FileMode.Create : FileMode.CreateNew;
            _logger.Information("Writing blob to: {fileName}", fileName);
            using (var stream = new FileStream(fileName, fileMode, FileAccess.Write, FileShare.None))
            {
                int i = 0;
                await foreach (var chunk in requestStream.ReadAllAsync())
                {
                    if(chunk.I != i++)
                        throw new InvalidOperationException("Unsupported");
                    var expectedLocation = chunk.I * blobDescriptor.ChunkSize;
                    //if (stream.Position != expectedLocation && expectedLocation < MAX_FILE_SIZE)
                    //    stream.Seek(expectedLocation, SeekOrigin.Begin);

                    await stream.WriteAsync(chunk.Data.Memory);
                    writtenBytes += chunk.Data.Memory.Length;
                }
            }
            _logger.Information("Blob {fileName} written.", fileName);
            await InvokeUploadEvent(context, blobDescriptor, writtenBytes, userId, fileName);
            
            return new BlobData()
            {
                Url = $"/blob/{blobDescriptor.Category}-{blobDescriptor.Id}",
                WrittenBytes = writtenBytes
            };
        }
        private static string[] bitmapExtensions = new string[] {".png",".jpg",".jpeg",".bmp"};

        private async Task InvokeUploadEvent(ServerCallContext context, 
            BlobDescriptor blobDescriptor, 
            long writtenBytes,
            Guid userId, string fileName)
        {
            ExtraProperties props = null;
            var ext = Path.GetExtension(blobDescriptor.FileName).ToLowerInvariant();
            if (bitmapExtensions.Contains(ext))
            {
                using var image = Image.FromFile(fileName);
                props = new ImageProperties() 
                { 
                    Width = image.Width, 
                    Height = image.Width
                };
            } 
            else if(ext == ".svg")
            {
                // let's read view-port.
                XmlDocument doc = new XmlDocument();
                doc.Load(fileName);
                var viewBoxAttr = doc.DocumentElement
                    .Attributes.OfType<XmlAttribute>()
                    .FirstOrDefault(x => x.Name == "viewBox");

                if (viewBoxAttr != null)
                {
                    string[] values = viewBoxAttr.Value.Split(new char[]{' ',','}, StringSplitOptions.RemoveEmptyEntries);
                    if(values.Length == 4)
                        props = new ImageProperties()
                        {
                            Width = int.Parse(values[2]),
                            Height = int.Parse(values[3])
                        };
                }
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
            string fileName = metadata.GetValue("file_name");
            string table = metadata.GetValue("table_name");
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
                BitConverter.ToInt64(size64),
                BitConverter.ToInt32(chunkSize32),
                BitConverter.ToBoolean(forceOverride), 
                blobUploadReason);

            if (string.IsNullOrWhiteSpace(desc.FileName))
                throw new ArgumentException("FileName");
            if (string.IsNullOrWhiteSpace(desc.Category))
                throw new ArgumentException("TableName");
            if (desc.ChunkSize <= 0 || desc.ChunkSize > 1024 * 1024)
                throw new ArgumentException("ChunkSize");
            if (desc.Size <= 0 || desc.Size > MAX_FILE_SIZE) // 64MB
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
            _logger.Information("GrpcProxy -> Reading started.");
            using SemaphoreSlim subExit = new SemaphoreSlim(0);
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
