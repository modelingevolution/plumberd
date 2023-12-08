using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;

using ProtoBuf.Meta;
using ProtoBuf;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using EventStore.Client;


namespace ModelingEvolution.Plumberd.EventStore
{

    public partial class NativeEventStore : IEventStore
    {
        public event Action<NativeEventStore> Connected;
        private readonly ConcurrentBag<ISubscription> _subscriptions;
        private readonly ProjectionConfigurations _projectionConfigurations;
        private bool _connected = false;
        private readonly Lazy<ILogger> _lazyLog;
        private ILogger Log => _lazyLog.Value;
        public bool IsConnected => _connected;
        private readonly EventStoreClient _connection;
        //private readonly ProjectionsManager _projectionsManager;

        private EventStorePersistentSubscriptionsClient _subscriptionsClient;
        private EventStoreProjectionManagementClient _projectionManagement;

        private EventStoreProjectionManagementClient ProjectionManagement
        {
            get
            {
                if(_projectionManagement != null) return _projectionManagement;
                _projectionManagement = new EventStoreProjectionManagementClient(_dbSettings);
                return _projectionManagement;
            }
        }
        public EventStorePersistentSubscriptionsClient PersistentSubscriptions
        {
            get
            {
                if(_subscriptionsClient != null) return _subscriptionsClient;
                _subscriptionsClient = new EventStorePersistentSubscriptionsClient(_dbSettings);
                return _subscriptionsClient;
            }
        }

        private EventStoreClientSettings _dbSettings;
        private readonly UserCredentials _credentials;
        
        private readonly EventStoreSettings _settings;
        public EventStoreClient Connection => _connection;
        public IEventStoreSettings Settings => _settings;
        private HttpClientHandler IgnoreServerCertificateHandler()
        {
            return new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (r, cer, c, e) => true
            };
        }

        public async Task UpdateProjections()
        {
            await _projectionConfigurations.UpdateIfRequired();
        }

        
        public async Task LoadEventFromFile(string file)
        {
            Log.LogInformation("Loading events from file: {fileName}", file);
            long n = 0;
            byte[] buffer = new byte[16 * 1024]; // 16KB
            Task writeTask = null;
            await using (var fileStream = File.Open(file, FileMode.Open))
            {
                await using (var fs = new DeflateStream(fileStream, CompressionMode.Decompress))
                {
                    while (fs.CanRead)
                    {
                        var headerLen = await fs.ReadAllAsync(buffer, 0, 32);
                        if (headerLen != 32)
                            break;

                        Guid g = new Guid(new ReadOnlySpan<byte>(buffer, 0, 16));
                        int streamNameLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16, 4));
                        int eventTypeLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16 + 4, 4));
                        int metadataLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16 + 8, 4));
                        int dataLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16 + 12, 4));


                        var nameLen = streamNameLen + eventTypeLen;
                        if (await fs.ReadAllAsync(buffer, 0, nameLen) != nameLen) break;

                        string streamName = Encoding.UTF8.GetString(buffer, 0, streamNameLen);
                        string eventType = Encoding.UTF8.GetString(buffer, streamNameLen, eventTypeLen);

                        byte[] metadata = new byte[metadataLen];
                        byte[] data = new byte[dataLen];

                        if (await fs.ReadAllAsync(metadata, 0, metadataLen) != metadataLen) break;
                        if (await fs.ReadAllAsync(data, 0, dataLen) != dataLen) break;

                        if (writeTask != null)
                            await writeTask;
                        var d = new global::EventStore.Client.EventData(Uuid.FromGuid(g), eventType, metadata, data);
                        writeTask = _connection.AppendToStreamAsync(streamName, StreamRevision.None, Enumerable.Repeat(d,1));
                        n += 1;
                    }
                }

                if (writeTask != null)
                    await writeTask;
                Log.LogInformation("Loaded {count} events from file: {fileName}",n, file);
            }

        }
        public async Task WriteEventsToFile(string file)
        {
            Log.LogInformation("Writing events to file {fileName}", file);
            long n = 0;
            Stopwatch s = new Stopwatch();
            s.Start();
            byte[] guidBuffer = new byte[16];
            await using (var fileStream = File.Open(file, FileMode.Create))
            {
                await using (var fs = new DeflateStream(fileStream, CompressionMode.Compress))
                await using (BinaryWriter sw = new BinaryWriter(fs))
                {
                    Position p = Position.Start;
                    EventStoreClient.ReadAllStreamResult slice = null;
                    const int chunkSize = 1000;
                    int chunkCount = 0;
                    do
                    {
                        chunkCount = 0;
                        slice = _connection.ReadAllAsync(Direction.Forwards, p, 1000, false);
                        
                        //TODO: Make sure we don't write twice the same event.
                        await foreach (var e in slice)
                        {
                            if (!e.Event.EventStreamId.StartsWith("$") && e.Event.EventType != "$>")
                            {
                                e.Event.EventId.ToGuid().TryWriteBytes(guidBuffer);
                                var streamIdName = Encoding.UTF8.GetBytes(e.Event.EventStreamId);
                                var eventType = Encoding.UTF8.GetBytes(e.Event.EventType);

                                sw.Write(guidBuffer);               // 16
                                sw.Write(streamIdName.Length);      // 16 + 4 
                                sw.Write(eventType.Length);         // 16 + 8
                                sw.Write(e.Event.Metadata.Length);  // 16 + 12 
                                sw.Write(e.Event.Data.Length);      // 16 + 16

                                sw.Write(streamIdName);
                                sw.Write(eventType);
                                sw.Write(e.Event.Metadata.Span);
                                sw.Write(e.Event.Data.Span);
                                n += 1;
                            }

                            chunkCount += 1;
                        }
                        
                        p = slice.LastPosition ?? Position.Start;
                    } while (chunkCount == chunkSize);
                }
            }
            s.Stop();
            Log.LogInformation("Writing {count} done in: {duration}",n, s.Elapsed);
            
        }
        public NativeEventStore(EventStoreClientSettings connectionsettings,
            UserCredentials credentials,
            EventStoreSettings settings, 
            Func<ILogger<NativeEventStore>> log,
            IReadOnlyList<IProjectionConfig> configurations)

        {
            _settings = settings;
            _lazyLog = new Lazy<ILogger>(log);
            _subscriptions = new ConcurrentBag<ISubscription>();
            _subscriptionsClient = new EventStorePersistentSubscriptionsClient(connectionsettings);
            _projectionManagement = new EventStoreProjectionManagementClient(connectionsettings);
            _connection = new EventStoreClient(connectionsettings);
            _credentials = credentials;
            _projectionConfigurations = new ProjectionConfigurations(_projectionManagement, _credentials, _settings);
            _projectionConfigurations.Register(configurations);
        }
        public NativeEventStore(EventStoreSettings evSettings, 
            EventStoreClientSettings dbSettings, 
            string userName, string password,
            Func<ILogger<NativeEventStore>> log,
            IReadOnlyList<IProjectionConfig> configurations)
        {
            _dbSettings = dbSettings;
            _settings = evSettings;
            _lazyLog = new Lazy<ILogger>(log);
            _subscriptions = new ConcurrentBag<ISubscription>();
            _credentials = new UserCredentials(userName ?? "admin", password ?? "changeit");
            dbSettings.DefaultCredentials = _credentials;
            
            _subscriptionsClient = new EventStorePersistentSubscriptionsClient(dbSettings);
            _projectionManagement = new EventStoreProjectionManagementClient(dbSettings);
            _connection = new EventStoreClient(dbSettings);
            _projectionConfigurations = new ProjectionConfigurations(_projectionManagement, _credentials, _settings);
            _projectionConfigurations.Register(configurations);
        }

        
        public async Task CheckConnectivity()
        {
            if (!_connected)
            {
                Log.LogInformation("Establishing connection.");
                for (int i = 0; i < 10; i++)
                {
                    Log.LogInformation($"Testing reading({i}).");
                    try
                    {
                        var slice = _connection.ReadAllAsync(Direction.Backwards, Position.End, 1);
                        var c = await slice.CountAwaitAsync(async (x) => true);
                    }
                    catch (Exception e)
                    {
                        Log.LogError("{e}",1);
                        await Task.Delay(1000); continue;
                    }

                    Log.LogInformation("Connected.");
                    _connected = true;
                    Connected?.Invoke(this);
                    return;
                }

                throw new InvalidOperationException("Could not establish connection after 10 retries.");
            }
        }
        
        public async Task<ISubscription> Subscribe(string name,
            bool fromBeginning,
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory factory,
            params string[] types)
        {
            await CheckConnectivity();
            Array.Sort(types);
            
            Guid key = String.Concat(types).ToGuid();

            // REFACTOR, SIMPLIFY
            ProjectionSchema schema = new ProjectionSchema();
            if (types.Length == 1)
            {
                // Direct, no projection name;
                schema.StreamName = $"$et-{types[0]}";
                schema.ProjectionName = $"{name}-{key}";
                schema.IsDirect = true;
            }
            else
            { 
                // InDirect
                schema.StreamName = $"{_settings.ProjectionStreamPrefix}{name}-{key}";
                schema.ProjectionName = $"{name}-{key}";
                
            }

            schema.Script = new ProjectionSchemaBuilder().FromEventTypes(types).EmittingLinksToStream(schema.StreamName).Script();

            return await Subscribe(schema, fromBeginning, isPersistent, onEvent, factory);
        }

        public async Task Init()
        {
            await _projectionConfigurations.UpdateIfRequired();
        }

        public async Task<ISubscription> Subscribe(ProjectionSchema schema,
            bool fromBeginning,
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory factory)
        {

            await CheckConnectivity();
            
            if (!schema.IsDirect) 
                await _projectionConfigurations.UpdateProjectionSchema(schema);

            if (isPersistent)
                return await SubscribePersistently(fromBeginning, onEvent, schema.StreamName, factory);
            else
                return await Subscribe(fromBeginning, onEvent, schema.StreamName, factory);
        }
        //private async Task UpdateDefaultTrackingProjectionIfNeeded(string projectionName,
        //    string streamName,
        //    IEnumerable<string> types)
        //{
        //    var query = new ProjectionSchemaBuilder().FromEventTypes(types).EmittingLinksToStream(streamName).Script();
        //    var config = await ProjectionManagement.GetConfigAsync(projectionName, _credentials);
        //    var list = await ProjectionManagement.ListAllAsync().ToListAsync();
            
        //    var currentQuery = await ProjectionManagement.GetQueryAsync(projectionName, _credentials);
        //    if (query != currentQuery || !config.EmitEnabled)
        //    {
        //        Log.LogInformation("Updating continues projection definition and config: {projectionName}", projectionName);
        //        await ProjectionManagement.UpdateQueryAsync(projectionName, query, true, _credentials);
        //    }
            
        //}
       
       

        private async Task<ISubscription> Subscribe(bool fromBeginning,
            EventHandler onEvent,
            string streamName, 
            IProcessingContextFactory processingContextFactory)
        {
            ContinuesSubscription s = new ContinuesSubscription(this,  fromBeginning, onEvent, streamName, processingContextFactory, _settings.LoggerFactory.CreateLogger<ContinuesSubscription>());
            await s.Subscribe();
            _subscriptions.Add(s);
            return s;
        }
        
       
        private async Task<ISubscription> SubscribePersistently(bool fromBeginning,
            EventHandler onEvent,
            string streamName, 
            IProcessingContextFactory processingContextFactory)
        {
            PersistentSubscription s = new PersistentSubscription(this,  fromBeginning, onEvent, streamName, processingContextFactory, _settings.LoggerFactory.CreateLogger<PersistentSubscription>());
            await s.Subscribe();
            _subscriptions.Add(s);
            return s;
        }

        
        public IStream GetStream(string category, Guid id, IContext context, 
            IMetadataSerializer serializer = null, IRecordSerializer recordSerializer = null)
        {
            context ??= StaticProcessingContext.Context;

            return new Stream(this, category, id, _connection,
                serializer ?? _settings.MetadataSerializerFactory.Get(context),
                recordSerializer ?? _settings.Serializer);
        }

        
        private (IMetadata, IRecord) ReadEvent(ResolvedEvent r, IProcessingContext context)
        {
            var m = _settings.MetadataSerializerFactory.Get(context).Deserialize(r.Event.Metadata);
            var e = _settings.Serializer.Deserialize(r.Event.Data, m);
            
            if (context != null)
            {
                context.Record = e;
                context.Metadata = m;
            }

            var streamId = r.Event.EventStreamId;
            var splitIndex = streamId.IndexOf('-');
            
            m[m.Schema[MetadataProperty.CategoryName]] = streamId.Remove(splitIndex);
            m[m.Schema[MetadataProperty.StreamIdName]] = streamId.Substring(splitIndex + 1);
            m[m.Schema[MetadataProperty.StreamPositionName]] = (ulong)r.Event.EventNumber;
            m[m.Schema[MetadataProperty.LinkPositionName]] = (ulong)(r.Link?.EventNumber ?? 0);

            return (m, e);
        }

        public async IAsyncEnumerable<IStream> GetStreams()
        {
            EventStoreClient.ReadStreamResult result = _connection.ReadStreamAsync(Direction.Forwards, "$streams", StreamPosition.Start, long.MaxValue, false);
            if ((await result.ReadState) == ReadState.StreamNotFound) yield break;
            await foreach (var s in result)
            {
                string streamName = Encoding.UTF8.GetString(s.Event.Data.Span);

                int atIndex = streamName.IndexOf('@');
                int dashIndex = streamName.IndexOf('-', atIndex);

                string category = streamName.Substring(atIndex + 1, dashIndex - atIndex - 1);
                Guid id = Guid.Parse(streamName.Substring(dashIndex + 1));

                yield return GetStream(category, id, null);

            }

        }
    }
}
