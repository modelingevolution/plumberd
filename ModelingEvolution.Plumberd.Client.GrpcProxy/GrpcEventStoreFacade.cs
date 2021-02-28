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
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ProtoBuf;
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
        private readonly Guid _sessionId = Guid.NewGuid();
        
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
            TypeRegister typeRegister)
        {
            _channel = channel;
            _factory = factory;
            _sessionManager = sessionManager;
            _typeRegister = typeRegister;

            _buffer = new ArrayBufferWriter<byte>(1024 * 128); // 128 KB

            var metadataSerializer = _factory.Get(ContextScope.Event);
            this._mSchema = metadataSerializer.Schema;
            this._mPropIndex = _mSchema.Properties.ToDictionary(x => x.Name.ToGuid());

            Settings = new EventStoreSettings(eventMetadataFactory, factory,null);
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
            return new Subscription(() => result.Dispose());
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
            return new Subscription(() => result.Dispose());
        }

        private async Task Read(AsyncServerStreamingCall<ReadRsp> callContext,
            IProcessingContextFactory factory, EventHandler onEvent)
        {
            Serilog.Log.Information("Reading started.");
            try
            {
                int c = 0;
                await foreach (var i in callContext.ResponseStream.ReadAllAsync())
                {
                    Serilog.Log.Debug($"Reading record {c++}");
                    var context = factory.Create();
                    Guid typeId = new Guid(i.TypeId.Value.Span);
                    var type = _typeRegister[typeId];
                    if(type == null)
                        throw new InvalidOperationException("Type is unknown, have you forgotten to discover types with TypeRegister?");
                    else Serilog.Log.Debug("Found type to deserialize {type}", type);

                    var ev = SerializerFacade.Deserialize(type, i.Data.Memory) as IRecord;
                    Serilog.Log.Debug("Event was successfully deserialized: {ev}", ev);

                    Serilog.Log.Debug("Deserializing metadata with {propertyCount} properties.", _mSchema?.Count ?? -1);
                    var metadata = new Metadata.Metadata(_mSchema, true);
                    
                    foreach (var m in i.MetadataProps)
                    {
                        Guid propId = new Guid(m.Id.Value.Span);
                        if (_mPropIndex.TryGetValue(propId, out var mp))
                        {
                            Serilog.Log.Debug("Deserializing metadata property {propertyName}.", mp.Name);
                            if (mp.Type != typeof(DateTimeOffset))
                                metadata[mp] = SerializerFacade.Deserialize(mp.Type, m.Data.Memory);
                            else
                            {
                                if (m.Data.Span.Length != 16)
                                {
                                    Serilog.Log.Debug("Unexpected number of bytes. Found {actualLength}.", m.Data.Span.Length );
                                    continue;
                                }
                                
                                var dt = BitConverter.ToInt64(m.Data.Span.Slice(0, sizeof(Int64)));
                                var ts = BitConverter.ToInt64(m.Data.Span.Slice(sizeof(Int64), sizeof(Int64)));
                                metadata[mp] = new DateTimeOffset(new DateTime(dt),new TimeSpan(ts));
                            }
                            
                        } else
                        {
                            Serilog.Log.Debug("Could not deserialize metadata property with id: {propertyId}.", propId);
                        }
                    }
                    Serilog.Log.Debug("Metadata was successfully deserialized.");
                    context.Record = ev;
                    context.Metadata = metadata;

                    await onEvent(context, metadata, ev);
                }
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.Cancelled)
            {
                Serilog.Log.Information(e, "Streaming was cancelled from the client!");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Reading failed.");
            }
            Serilog.Log.Debug("Read closed.");
        }
    }
}