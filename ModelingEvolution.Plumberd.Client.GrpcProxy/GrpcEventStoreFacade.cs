using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
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
        class Subscription : ISubscription
        {
            public void Dispose()
            {
            }
        }
        private readonly TypeRegister _typeRegister;
        private readonly Func<GrpcChannel> _channel;
        private readonly IMetadataSerializerFactory _factory;
        private readonly ArrayBufferWriter<byte> _buffer;
        private readonly IMetadataSchema _mSchema;
        private readonly Dictionary<Guid, MetadataProperty> _mPropIndex;

        public GrpcEventStoreFacade(Func<GrpcChannel> channel, 
            IMetadataFactory eventMetadataFactory,
            IMetadataSerializerFactory factory, 
            TypeRegister typeRegister)
        {
            _channel = channel;
            _factory = factory;
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
            var client = new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel());
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

            CancellationToken token = new CancellationToken();
            var result = client.ReadStream(r, null, null, token);

            Task.Run(() => Read(result, factory, onEvent)).ConfigureAwait(false);
            return new Subscription();
        }

        public async Task<ISubscription> Subscribe(string name, 
            bool fromBeginning, 
            bool isPersistent, 
            EventHandler onEvent,
            IProcessingContextFactory factory,
            params string[] sourceEventTypes)
        {
            var client = new GrpcEventStoreProxy.GrpcEventStoreProxyClient(_channel());

            ReadReq r = new ReadReq();
            r.FromBeginning = fromBeginning;
            r.IsPersistent = isPersistent;
            r.EventTypeSchema = new EventTypeProjectionSchema()
            {
                Name = name,
                EventTypes = { sourceEventTypes }
            };

            CancellationToken token = new CancellationToken();
            var result = client.ReadStream(r, null, null, token);
            //await foreach (var i in result.ResponseStream.ReadAllAsync())
            //{
            //    Debug.WriteLine(i.Seq);
            //}
            Task.Run(() => Read(result, factory, onEvent)).ConfigureAwait(false);
            return new Subscription();
        }

        private async Task Read(AsyncServerStreamingCall<ReadRsp> callContext,
            IProcessingContextFactory factory, EventHandler onEvent)
        {
            try
            {
                await foreach (var i in callContext.ResponseStream.ReadAllAsync())
                {
                    var context = factory.Create();
                    Guid typeId = new Guid(i.TypeId.Value.Span);
                    var type = _typeRegister[typeId];
                    if(type == null)
                        throw new InvalidOperationException("Type is unknown, have you forgotten to discover types with TypeRegister?");

                    var ev = SerializerFacade.Deserialize(type, i.Data.Memory) as IRecord;

                    var metadata = new Metadata.Metadata(_mSchema, true);
                    foreach (var m in i.MetadataProps)
                    {
                        Guid propId = new Guid(m.Id.Value.ToByteArray());
                        if (_mPropIndex.TryGetValue(propId, out var mp))
                        {
                            if(mp.Type != typeof(DateTimeOffset))
                                metadata[mp] = SerializerFacade.Deserialize(mp.Type, m.Data.Memory);
                            else
                            {
                                var dt = Serializer.Deserialize<DateTime>(m.Data.Memory.Slice(0, 13));
                                var ts = Serializer.Deserialize<TimeSpan>(m.Data.Memory.Slice(13, 6));
                                metadata[mp] = new DateTimeOffset(dt,ts);
                            }
                        }
                    }

                    context.Record = ev;
                    context.Metadata = metadata;

                    await onEvent(context, metadata, ev);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            
        }
    }
}