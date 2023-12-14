using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Client;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;


namespace ModelingEvolution.Plumberd.EventStore
{
    public partial class NativeEventStore
    {
        class Stream : IStream
        {
            private readonly NativeEventStore _store;
            private readonly string _category;
            private readonly Guid _id;
            private readonly EventStoreClient _connection;
            private readonly IMetadataSerializer _metadataSerializer;
            private readonly IRecordSerializer _recordSerializer;

            private readonly string _streamName;

            public Guid Id => _id;
            public string Category => _category;
            public IEventStore EventStore => _store;

            public Stream(NativeEventStore store,
                string category,
                Guid id,
                EventStoreClient connection,
                IMetadataSerializer metadataSerializer,
                IRecordSerializer recordSerializer)
            {
                _store = store;
                _category = category;
                _id = id;
                _connection = connection;
                _metadataSerializer = metadataSerializer;
                _recordSerializer = recordSerializer;
                _streamName = $"{_category}-{id}";
            }

            public async Task Append(IRecord x, IMetadata metadata, ulong expectedVersion)
            {
                var data = CreateEventData(x, metadata);

                StreamRevision sr = new StreamRevision(expectedVersion);
                var eventDatas = Enumerable.Repeat(data, 1);
                var result = await _connection.AppendToStreamAsync(_streamName,
                    sr,
                    eventDatas);
            }

            public async Task Append(IRecord x, IMetadata metadata)
            {
                var data = CreateEventData(x, metadata);
                var eventDatas = Enumerable.Repeat(data, 1);
                var result = await _connection.AppendToStreamAsync(_streamName, StreamState.Any, eventDatas);

            }

            private global::EventStore.Client.EventData CreateEventData(IRecord x, IMetadata metadata)
            {
                var metadataBytes = _metadataSerializer.Serialize(metadata);
                var eventBytes = _recordSerializer.Serialize(x, metadata);
                var eventType = x is ILink ? "$>" : _store.Settings.RecordNamingConvention(x.GetType())[0];
                var id = Uuid.FromGuid(x.Id);
                var data = new global::EventStore.Client.EventData(id, eventType, eventBytes, metadataBytes,
                     _recordSerializer.IsJson(metadata) ? "application/json" : "application/octet-stream");
                return data;
            }

            public async IAsyncEnumerable<(IMetadata, IRecord)> Read()
            {
                var result = _connection.ReadStreamAsync(Direction.Forwards, _streamName, StreamPosition.Start,
                    long.MaxValue, true);
                if (await result.ReadState == ReadState.StreamNotFound) yield break;
                
                await foreach (var i in result.OnlyNew())
                {
                    var d = ReadEvent(i);
                    yield return d;
                }
            }

            private (IMetadata, IRecord) ReadEvent(ResolvedEvent r)
            {
                var m = _metadataSerializer.Deserialize(r.Event.Metadata);

                var streamId = r.OriginalStreamId;
                var splitIndex = streamId.IndexOf('-');

                m[m.Schema[MetadataProperty.CategoryName]] = streamId.Remove(splitIndex);
                m[m.Schema[MetadataProperty.StreamIdName]] = streamId.Substring(splitIndex + 1);
                m[m.Schema[MetadataProperty.StreamPositionName]] = (ulong)r.OriginalEventNumber;
                m[m.Schema[MetadataProperty.LinkPositionName]] = (ulong)(r.Link?.EventNumber ?? 0);

                var ev = _recordSerializer.Deserialize(r.Event.Data, m);
                return (m, ev);
            }



            public async IAsyncEnumerable<IRecord> ReadEvents()
            {
                var result = _connection.ReadStreamAsync(Direction.Forwards, _streamName, StreamPosition.Start,
                    long.MaxValue, true);
                if ((await result.ReadState) == ReadState.StreamNotFound) yield break;

                await foreach (var i in result.OnlyNew())
                {
                    var (m, record) = ReadEvent(i);
                    yield return record;
                }
            }
        }




    }
}
