﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Newtonsoft.Json;

namespace ModelingEvolution.Plumberd.EventStore
{
    public partial class NativeEventStore
    {
        class Stream : IStream
        {
            private readonly NativeEventStore _store;
            private readonly string _category;
            private readonly Guid _id;
            private readonly IEventStoreConnection _connection;
            private readonly IMetadataSerializer _metadataSerializer;
            private readonly IRecordSerializer _recordSerializer;
            
            private readonly string _streamName;

            public Guid Id => _id;
            public string Category => _category;
            public IEventStore EventStore => _store;
            public Stream(NativeEventStore store, 
                string category, 
                Guid id, 
                IEventStoreConnection connection, 
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
                var result = await _connection.AppendToStreamAsync(_streamName, (long)expectedVersion, data);
            }

            public async Task Append(IRecord x, IMetadata metadata)
            {
                var data = CreateEventData(x, metadata);
                var result = await _connection.AppendToStreamAsync(_streamName,  ExpectedVersion.Any, data);
                
            }

            private global::EventStore.ClientAPI.EventData CreateEventData(IRecord x, IMetadata metadata)
            {
                var metadataBytes = _metadataSerializer.Serialize( metadata);
                var eventBytes = _recordSerializer.Serialize(x, metadata);
                var eventType = x is ILink ? "$>" : _store.Settings.RecordNamingConvention(x.GetType())[0];  
                var data = new global::EventStore.ClientAPI.EventData(x.Id, eventType, true,  eventBytes, metadataBytes);
                return data;
            }
            public async IAsyncEnumerable<(IMetadata, IRecord)> Read()
            {
                StreamEventsSlice slice = null;
                long start = StreamPosition.Start;
                do
                {
                    slice = await _connection.ReadStreamEventsForwardAsync(_streamName, start, 100, true);
                    foreach (var i in slice.Events)
                    {
                        var d = ReadEvent(i);

                        yield return d;
                    }
                    start = slice.NextEventNumber;
                } while (!slice.IsEndOfStream);
            }

            private (IMetadata, IRecord) ReadEvent(ResolvedEvent r)
            {
                var m = _metadataSerializer.Deserialize(r.Event.Metadata);

                var streamId = r.Event.EventStreamId;
                var splitIndex = streamId.IndexOf('-');

                m[m.Schema[MetadataProperty.CategoryName]] = streamId.Remove(splitIndex);
                m[m.Schema[MetadataProperty.StreamIdName]] = Guid.Parse(streamId.Substring(splitIndex + 1));
                m[m.Schema[MetadataProperty.StreamPositionName]] = (ulong)r.Event.EventNumber;

                var ev = _recordSerializer.Deserialize(r.Event.Data, m);
                return (m, ev);
            }

            

            public async IAsyncEnumerable<IRecord> ReadEvents()
            {
                StreamEventsSlice slice = null;
                long start = StreamPosition.Start;
                do
                {
                    slice = await _connection.ReadStreamEventsForwardAsync(_streamName, start, 100, true);
                    foreach (var i in slice.Events)
                    {
                        var d = ReadEvent(i);

                        yield return d.Item2;
                    }
                    start = slice.NextEventNumber;
                } while (!slice.IsEndOfStream);
            }
        }
    }

    
}
