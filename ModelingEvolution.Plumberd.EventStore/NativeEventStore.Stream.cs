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
                var eventDatas = Enumerable.Repeat(data,1);
                var result = await _connection.AppendToStreamAsync(_streamName, 
                    sr, 
                    eventDatas);
            }

            public async Task Append(IRecord x, IMetadata metadata)
            {
                var data = CreateEventData(x, metadata);
                var eventDatas = Enumerable.Repeat(data, 1);
                var result = await _connection.AppendToStreamAsync(_streamName,  StreamState.Any, eventDatas);
                
            }

            private global::EventStore.Client.EventData CreateEventData(IRecord x, IMetadata metadata)
            {
                var metadataBytes = _metadataSerializer.Serialize( metadata);
                var eventBytes = _recordSerializer.Serialize(x, metadata);
                var eventType = x is ILink ? "$>" : _store.Settings.RecordNamingConvention(x.GetType())[0];
                var id = Uuid.FromGuid(x.Id);
                var data = new global::EventStore.Client.EventData(id, eventType, eventBytes, metadataBytes, "application/json");
                return data;
            }
            public async IAsyncEnumerable<(IMetadata, IRecord)> Read()
            {
                EventStoreClient.ReadStreamResult slice = null;
                StreamPosition? start = StreamPosition.Start;
                do
                {
                    slice = _connection.ReadStreamAsync(Direction.Forwards, _streamName, start.Value, 100, true);
                    await foreach (var i in slice)
                    {
                        var d = ReadEvent(i);

                        yield return d;
                    }
                    start = slice.LastStreamPosition;
                } while (start.HasValue);
            }

            private (IMetadata, IRecord) ReadEvent(ResolvedEvent r)
            {
                var m = _metadataSerializer.Deserialize(r.Event.Metadata);

                var streamId = r.OriginalStreamId;
                var splitIndex = streamId.IndexOf('-');

                m[m.Schema[MetadataProperty.CategoryName]] = streamId.Remove(splitIndex);
                m[m.Schema[MetadataProperty.StreamIdName]] = Guid.Parse(streamId.Substring(splitIndex + 1));
                m[m.Schema[MetadataProperty.StreamPositionName]] = (ulong)r.OriginalEventNumber;

                var ev = _recordSerializer.Deserialize(r.Event.Data, m);
                return (m, ev);
            }

            

            public async IAsyncEnumerable<IRecord> ReadEvents()
            {

                var items = _connection.ReadStreamAsync(Direction.Forwards, _streamName, StreamPosition.Start, long.MaxValue, true);
                var iterator = items.GetAsyncEnumerator();
                bool c = true;
                try
                {
                    c = await iterator.MoveNextAsync();
                }
                catch (StreamNotFoundException)
                {
                    await iterator.DisposeAsync();
                    yield break;
                }

                while (c)
                {
                    (IMetadata m, IRecord record) = (null, null);
                    try
                    {
                        (m, record) = ReadEvent(iterator.Current);
                    }
                    catch
                    {
                        await iterator.DisposeAsync();
                        yield break;
                    }

                    yield return record;
                    c = await iterator.MoveNextAsync();
                }
                await iterator.DisposeAsync();

            }
        }
    }

    

    
}
