using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ProtoBuf;
using Microsoft.Extensions.Logging;
using EventHandler = ModelingEvolution.Plumberd.EventStore.EventHandler;
using MetadataProperty = ModelingEvolution.Plumberd.Metadata.MetadataProperty;

#pragma warning disable 4014
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    
    public static class SerializerFacade
    {
        public static object Deserialize(Type t, in ReadOnlySpan<byte> mem)
        {
            try
            {
                MemoryStream ms = new MemoryStream(mem.ToArray());
                return Serializer.Deserialize(t,ms);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder($"Cannot deserialize '{t.Name}'");
                sb.AppendLine($"Corrupted bytes: {mem.Length}");
                sb.AppendLine(Convert.ToBase64String(mem.ToArray()));
                throw new Exception(sb.ToString(), ex);
            }
        }
        public static object Deserialize(Type t, in ReadOnlyMemory<byte> mem)
        {
            try
            {
                return Serializer.Deserialize(t, mem);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder($"Cannot deserialize '{t.Name}'");
                sb.AppendLine($"Corrupted bytes: {mem.Length}");
                sb.AppendLine(Convert.ToBase64String(mem.ToArray()));
                throw new Exception(sb.ToString(), ex);
            }
        }
    }

    public class GrpcEventStoreFacade : IEventStore
    {
        private static readonly ILogger Log = Modellution.Logging.LogFactory.GetLogger<GrpcEventStoreFacade>();
        private readonly Guid _sessionId = Guid.NewGuid();
        public static event Func<Task> ReadingFailed;
        class Subscription : ISubscription
        {
            private readonly Action _cleanUp;

            public Subscription(Action cleanUp)
            {
                _cleanUp = cleanUp;
            }

            public void Dispose()
            {
                _cleanUp();
            }
        }
        private readonly TypeRegister _typeRegister;
        private readonly Func<Channel> _channel;
        private readonly IMetadataSerializerFactory _factory;
        private readonly Func<ISessionManager> _sessionManager;
        private readonly ArrayBufferWriter<byte> _buffer;
        private readonly IMetadataSchema _mSchema;
        private readonly Dictionary<Guid, MetadataProperty> _mPropIndex;

        public GrpcEventStoreFacade(Func<Channel> channel,
            IMetadataFactory eventMetadataFactory,
            IMetadataSerializerFactory factory,
            Func<ISessionManager> sessionManager,
            TypeRegister typeRegister, bool isDevelopment)
        {
            _channel = channel;
            _factory = factory;
            _sessionManager = sessionManager;
            _typeRegister = typeRegister;

            _buffer = new ArrayBufferWriter<byte>(1024 * 128); // 128 KB

            var metadataSerializer = _factory.Get(ContextScope.Event);
            this._mSchema = metadataSerializer.Schema;
            this._mPropIndex = _mSchema.Properties.ToDictionary(x => x.Name.ToGuid());

            Settings = new EventStoreSettings(eventMetadataFactory, 
                factory,null, isDevelopment);
        }


       

        public IEventStoreSettings Settings { get; }
        public IStream GetStream(string category, Guid id, IContext context = null)
        {
            throw new NotImplementedException();
        }

        public Task Init()
        {
            return Task.CompletedTask;
        }

        public async Task<ISubscription> Subscribe(ProjectionSchema schema,
            bool fromBeginning,
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory factory)
        {
            var channel = _channel();
            var client = new GrpcEventStoreProxy.GrpcEventStoreProxyClient(channel.GrpcChannel);
            
            var gSchema = new GenericProjectionSchema();
            if (!String.IsNullOrWhiteSpace(schema.ProjectionName))
                gSchema.Name = schema.ProjectionName;

            if (!String.IsNullOrWhiteSpace(schema.Script))
                gSchema.Script = schema.Script;

            if (!String.IsNullOrWhiteSpace(schema.StreamName))
                gSchema.StreamName = schema.StreamName;


            ReadReq r = new ReadReq();
            r.FromBeginning = fromBeginning;
            r.IsPersistent = isPersistent;
            r.GenericSchema = gSchema;

            var metadata = new Grpc.Core.Metadata();
            metadata.Add("SessionId-bin", _sessionManager().GetSessionId(channel.Address).ToByteArray());

            CancellationTokenSource source = new CancellationTokenSource();
            var result = client.ReadStream(r, metadata, null, source.Token);

            Task.Run(() => Read(result, factory, onEvent)).ConfigureAwait(false);
            return new Subscription(() =>
            {
                //source.Cancel();
                result.Dispose();
            });
        }

        public async Task<ISubscription> Subscribe(string name, 
            bool fromBeginning, 
            bool isPersistent, 
            EventHandler onEvent,
            IProcessingContextFactory factory,
            params string[] sourceEventTypes)
        {
            var channel = _channel();
            var client = new GrpcEventStoreProxy.GrpcEventStoreProxyClient(channel.GrpcChannel);
            var metadata = new Grpc.Core.Metadata();
            metadata.Add("SessionId-bin", _sessionManager().GetSessionId(channel.Address).ToByteArray());

            ReadReq r = new ReadReq();
            r.FromBeginning = fromBeginning;
            r.IsPersistent = isPersistent;
            r.EventTypeSchema = new EventTypeProjectionSchema()
            {
                Name = name,
                EventTypes = { sourceEventTypes }
            };
            CancellationTokenSource source = new CancellationTokenSource();
           
            var result = client.ReadStream(r, metadata, null, source.Token);
            
            Task.Run(() => Read(result, factory, onEvent)).ConfigureAwait(false);
            return new Subscription(() =>
            {
                //source.Cancel();
                result.Dispose();
            });
        }

        private async Task Read(AsyncServerStreamingCall<ReadRsp> callContext,
            IProcessingContextFactory factory, EventHandler onEvent)
        {
            Log.LogInformation("Reading started.");
            IRecord lastEvent = null;
            IMetadata lastMeta = null;
            try
            {
                int c = 0;
                await foreach (var i in callContext.ResponseStream.ReadAllAsync())
                {
                    Log.LogDebug($"Reading record {c++}");
                    var context = ReadEvent(factory, i, out var ev, out var metadata);
                    lastEvent = ev;
                    lastMeta = metadata;
                    
                    await onEvent(context, metadata, ev);
                }
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.Cancelled)
            {
                Log.LogInformation("Streaming was cancelled from the client!");
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.Internal)
            {
                Log.LogInformation(e, "We need to logout.");
                await ReadingFailed?.Invoke();
            }
            catch (ObjectDisposedException e)
            {
                Log.LogInformation(e, "We need to logout.");
                await ReadingFailed?.Invoke();
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Reading failed. {lastEvent} {lastMatadata}", lastEvent, lastMeta);
            }
            Log.LogDebug("Read closed.");
        }

        private IProcessingContext ReadEvent(IProcessingContextFactory factory, ReadRsp i, out IRecord ev,
            out Metadata.Metadata metadata)
        {
            
            var context = factory.Create();
            Guid typeId = new Guid(i.TypeId.Value.Span);
            var type = _typeRegister[typeId];
            if (type == null)
            {
                var debugDict = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(x => x.GetTypes()).ToLookup(x => x.NameId());
                string additionalInfo = $"TypeId={typeId}";
                var possibilities = debugDict[typeId].ToArray();
                if (possibilities.Any())
                    additionalInfo = $"Unregistered type: {string.Join("; ", possibilities.Select(x => x.FullName))}";
                throw new InvalidOperationException(
                    $"Type is unknown, have you forgotten to discover types with TypeRegister?{additionalInfo}");
            }
            else Log.LogDebug("Found type to deserialize {type}", type);

            ev = SerializerFacade.Deserialize(type, i.Data.Memory) as IRecord;
            Log.LogDebug("Event was successfully deserialized: {ev}", ev);

            Log.LogDebug("Deserializing metadata with {propertyCount} properties.", _mSchema?.Count ?? -1);
            metadata = new Metadata.Metadata(_mSchema, true);

            foreach (var m in i.MetadataProps)
            {
                Guid propId = new Guid(m.Id.Value.Span);
                if (_mPropIndex.TryGetValue(propId, out var mp))
                {
                    Log.LogDebug("Deserializing metadata property {propertyName}.", mp.Name);
                    if (mp.Type != typeof(DateTimeOffset))
                        metadata[mp] = SerializerFacade.Deserialize(mp.Type, m.Data.Memory);
                    else
                    {
                        if (m.Data.Span.Length != 16)
                        {
                            Log.LogDebug("Unexpected number of bytes. Found {actualLength}.",
                                m.Data.Span.Length);
                            continue;
                        }

                        var dt = BitConverter.ToInt64(m.Data.Span.Slice(0, sizeof(Int64)));
                        var ts = BitConverter.ToInt64(m.Data.Span.Slice(sizeof(Int64), sizeof(Int64)));
                        metadata[mp] = new DateTimeOffset(new DateTime(dt), new TimeSpan(ts));
                    }
                }
                else
                {
                    Log.LogDebug("Could not deserialize metadata property with id: {propertyId}.", propId);
                }
            }

            Log.LogDebug("Metadata was successfully deserialized.");
            context.Record = ev;
            context.Metadata = metadata;
            return context;
        }
    }
}