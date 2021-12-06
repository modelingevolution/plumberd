using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.VisualBasic.CompilerServices;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Newtonsoft.Json;

namespace ModelingEvolution.Plumberd.EventStore
{
    public partial class GrpcEventStore
    {
        class GrpcStream : IStream
        {
            private readonly GrpcEventStore _store;
            private readonly string _category;
            private readonly Guid _id;
            private readonly EventStoreClient _connection;
            private readonly IMetadataSerializer _metadataSerializer;
            private readonly IRecordSerializer _recordSerializer;

            private readonly string _streamName;

            public Guid Id => _id;
            public string Category => _category;
            public IEventStore EventStore => _store;

            public GrpcStream(GrpcEventStore store,
                string category,
                Guid id,
                EventStoreClient connection,
                IMetadataSerializer metadataSerializer,
                IRecordSerializer recordSerializer)
            {
                _store = store;
                _category = category;
                _id = id;
                _connection = _store._connection;
                _metadataSerializer = metadataSerializer;
                _recordSerializer = recordSerializer;
                _streamName = $"{_category}-{id}";  
            }

            public async Task Append(IRecord x, IMetadata metadata, ulong expectedVersion)
            {
                var data = CreateGrpcEventData(x, metadata);
                await _connection.AppendToStreamAsync(_streamName, StreamRevision.FromInt64((Int64)expectedVersion), new [] {data});
            }

            public async Task Append(IRecord x, IMetadata metadata)
            {
               var data = CreateGrpcEventData(x, metadata);
               await _connection.AppendToStreamAsync(_streamName, StreamState.Any, new[] {data});

            }
            private global::EventStore.Client.EventData CreateGrpcEventData(IRecord x, IMetadata metadata)
            {
                var metadataBytes = _metadataSerializer.Serialize(metadata);
                var eventBytes = _recordSerializer.Serialize(x, metadata);
                var eventType = x is ILink ? "$>" : _store._settings.CommandStreamPrefix; //Todo check corectness
                var data = new global::EventStore.Client.EventData(Uuid.FromGuid(x.Id), eventType, eventBytes, metadataBytes);
                return data;
            }

            public async IAsyncEnumerable<(IMetadata, IRecord)> Read() 
            {
                EventStoreClient.ReadStreamResult slice = null;
                ulong start = StreamPosition.Start;
  
                    slice =  _connection.ReadStreamAsync(Direction.Forwards,_streamName, StreamPosition.Start, int.MaxValue ); //TODO check max count
                    await foreach (var i in slice)
                    {
                        var d = ReadEvent(i);

                        yield return d;
                    }
                    
            }

            private (IMetadata, IRecord) ReadEvent(ResolvedEvent i)
            {
                var m = _metadataSerializer.Deserialize(i.Event.Metadata.ToArray());

                var streamId = i.Event.EventStreamId;
                var splitIndex = streamId.IndexOf('-');

                m[MetadataProperty.Category] = streamId.Remove(splitIndex);
                m[MetadataProperty.StreamId] = Guid.Parse(streamId.Substring(splitIndex + 1));
                m[MetadataProperty.StreamPosition] = (ulong)i.Event.EventNumber;

                var ev = _recordSerializer.Deserialize(i.Event.Data.ToArray(), m);
                return (m, ev);
            }

            public async IAsyncEnumerable<IRecord> ReadEvents() 
            {
               
                
                EventStoreClient.ReadStreamResult slice = _connection.ReadStreamAsync(Direction.Forwards,_streamName, StreamPosition.Start, int.MaxValue);
                    
                    await foreach (var i in slice)
                    {
                        var d = ReadEvent(i);

                        yield return d.Item2;
                    }
            }
        }

        
    }


}
