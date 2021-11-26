using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Metadata;
using Modellution.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Position = EventStore.Client.Position;
using ResolvedEvent = EventStore.Client.ResolvedEvent;
using StreamPosition = EventStore.Client.StreamPosition;


namespace ModelingEvolution.Plumberd.EventStore
{
    
    public partial class GrpcEventStore : IEventStore
    {
        public event Action<NativeEventStore> Connected;
        private readonly ConcurrentBag<ISubscription> _subscriptions;
        private readonly ProjectionConfigurations _projectionConfigurations;
        private bool _connected = false;
        private static readonly ILogger Log = LogFactory.GetLogger<NativeEventStore>();

        EventStoreClient _connection;
        internal EventStoreClient Connection => _connection; 
        private readonly EventStoreProjectionManagementClient management;
        private readonly UserCredentials _credentials;
        EventStorePersistentSubscriptionsClient _subscriptionsClient;
        private readonly EventStoreSettings _settings;
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
                        var eventData =
                            new global::EventStore.Client.EventData(Uuid.FromGuid(g), eventType, data, metadata);
                        writeTask = _connection.AppendToStreamAsync(streamName,StreamRevision.None,new [] {eventData});
                        n += 1;
                    }
                }

                if (writeTask != null)
                    await writeTask;
                Log.LogInformation("Loaded {count} events from file: {fileName}", n, file);
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

                    var slice = _connection.ReadAllAsync(Direction.Forwards, Position.Start, int.MaxValue, null,
                        false);
                       await  foreach (var e in slice)
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
                        }

                  
                }
            }
            s.Stop();
            Log.LogInformation("Writing {count} done in: {duration}", n, s.Elapsed);

        }
        public GrpcEventStore(
            UserCredentials credentials,
            EventStoreClientSettings settings)

        {
            _subscriptions = new ConcurrentBag<ISubscription>();
            EventStoreClient cl =
                _connection = new EventStoreClient(settings);
            _credentials = credentials;
            //_projectionConfigurations = new ProjectionConfigurations(_projectionsManager, _credentials, _settings);
        }
        public GrpcEventStore(EventStoreSettings settings,
            Uri tcpUrl = null,
            Uri httpProjectionUrl = null,
            string userName = "admin",
            string password = "changeit",
            bool ignoreServerCert = false,
            bool disableTls = false,
            IEnumerable<IProjectionConfig> configurations = null)
        {
            _settings = settings;
            _subscriptions = new ConcurrentBag<ISubscription>();
            _credentials = new UserCredentials(userName, password);

            
            //httpProjectionUrl = httpProjectionUrl == null ? new Uri("https://localhost:2113") : httpProjectionUrl;
            //tcpUrl = tcpUrl == null ? new Uri("tcp://127.0.0.1:1113") : tcpUrl;


            //var tcpSettings = ConnectionSettings.Create()
            //    //.DisableServerCertificateValidation()
            //    //.UseDebugLogger()
            //    //.EnableVerboseLogging()
            //    .KeepReconnecting()
            //    .KeepRetrying()
            //    .LimitReconnectionsTo(1000)
            //    .LimitRetriesForOperationTo(100)
            //    .WithConnectionTimeoutOf(TimeSpan.FromSeconds(5))
            //    .SetDefaultUserCredentials(_credentials);

            //if (disableTls)
            //{
            //    tcpSettings = tcpSettings.DisableTls();
            //    const string msg = "Tls is disabled";
            //    if (_settings.IsDevelopment)
            //        Log.LogInformation(msg);
            //    else
            //        Log.LogWarning(msg);
            //}

            //if (ignoreServerCert)
            //{
            //    tcpSettings = tcpSettings.DisableServerCertificateValidation();
            //    const string msg = "Server certificate validation is disabled";
            //    if (_settings.IsDevelopment)
            //        Log.LogInformation(msg);
            //    else
            //        Log.LogWarning(msg);
            //}

            //connectionBuilder?.Invoke(tcpSettings);
            
            //_projectionsManager = new ProjectionsManager(new ConsoleLogger(), new DnsEndPoint(httpProjectionUrl.Host, httpProjectionUrl.Port), TimeSpan.FromSeconds(10),
            //    ignoreServerCert ? IgnoreServerCertificateHandler() : null, httpProjectionUrl.Scheme);

            var set = EventStoreClientSettings.Create(tcpUrl.OriginalString);
            _connection = new EventStoreClient(set);
            //_projectionConfigurations = new ProjectionConfigurations(_projectionsManager, _credentials, _settings);
            //if (configurations != null)
            //    _projectionConfigurations.Register(configurations);
        }


        public void CheckConnectivity()
        {
            if (!_connected)
            {
                var result = _connection.ReadAllAsync(Direction.Backwards, Position.End, 1,
                    null, false, _credentials);
              if(!result.GetAsyncEnumerator().Current.Equals(null))
                Log.LogInformation("Connected.");
                _connected = true;
             
            }
        }

        public async Task<ISubscription> Subscribe(string name,
            bool fromBeginning,
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory factory,
            params string[] types)
        {
            CheckConnectivity();
            Array.Sort(types);

            Guid key = String.Concat(types).ToGuid();

            // REFACTOR, SIMPLIFY
            ProjectionSchema schema = new ProjectionSchema();
            if (types.Length == 1)
            {
                // Direct, no projection name;
                schema.StreamName = $"$et-{types[0]}";
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
            CheckConnectivity();

            if (!schema.IsDirect)
                await _projectionConfigurations.UpdateProjectionSchema(schema);

            if (isPersistent)
                return await SubscribePersistently(fromBeginning, onEvent, schema.StreamName, factory);
            else
                return await Subscribe(fromBeginning, onEvent, schema.StreamName, factory);
        }
        



        private async Task<ISubscription> Subscribe(bool fromBeginning,
            EventHandler onEvent,
            string streamName,
            IProcessingContextFactory processingContextFactory)
        {
            GrpcContinuesSubscription s = new GrpcContinuesSubscription(this, fromBeginning, onEvent, streamName, processingContextFactory);
            await s.Subscribe();
            _subscriptions.Add(s);
            return s;
        }


        private async Task<ISubscription> SubscribePersistently(bool fromBeginning,
            EventHandler onEvent,
            string streamName,
            IProcessingContextFactory processingContextFactory) //TODO swap to async
        {
            var q = new GrpcPersistentSubscription(this, fromBeginning, onEvent, streamName, processingContextFactory);
            await q.Subscribe();
            _subscriptions.Add(q);
            return q ;
        }


        public IStream GetStream(string category, Guid id, IContext context)
        {
            context ??= StaticProcessingContext.Context;

            return new GrpcStream(this, category, id, _connection,
                _settings.MetadataSerializerFactory.Get(context),
                _settings.Serializer);
        }


        private (IMetadata, IRecord) ReadEvent(ResolvedEvent r, IProcessingContext context)
        {
            var m = _settings.MetadataSerializerFactory.Get(context).Deserialize(r.Event.Metadata.ToArray());
            var e = _settings.Serializer.Deserialize(r.Event.Data.ToArray(), m);

            if (context != null)
            {
                context.Record = e;
                context.Metadata = m;
            }

            var streamId = r.Event.EventStreamId;
            var splitIndex = streamId.IndexOf('-');

            m[MetadataProperty.Category] = streamId.Remove(splitIndex);
            m[MetadataProperty.StreamId] = Guid.Parse(streamId.Substring(splitIndex + 1));
            m[MetadataProperty.StreamPosition] = (ulong)r.Event.EventNumber;
            return (m, e);
        }

        public async IAsyncEnumerable<IStream> GetStreams()
        {
            var pack =  _connection.ReadStreamAsync(Direction.Forwards, "$streams", StreamPosition.Start, int.MaxValue);
                await foreach (var s in pack)
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

