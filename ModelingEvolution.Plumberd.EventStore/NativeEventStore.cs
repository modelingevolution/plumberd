﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.SystemData;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Newtonsoft.Json;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace ModelingEvolution.Plumberd.EventStore
{
    
    public partial class NativeEventStore : IEventStore
    {
        private readonly ConcurrentBag<ISubscription> _subscriptions;
        private bool _connected = false;
        private ILogger Log => _settings.Logger;

        private readonly IEventStoreConnection _connection;
        private readonly ProjectionsManager _projectionsManager;
        private readonly UserCredentials _credentials;

        private readonly EventStoreSettings _settings;
        internal IEventStoreConnection Connection => _connection;
        public IEventStoreSettings Settings => _settings;
        private HttpClientHandler IgnoreServerCertificateHandler()
        {
            return new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (r, cer, c, e) => true
            };
        }

        public async Task LoadEventFromFile(string file)
        {
            Log.Information("Loading events from file: {fileName}", file);
            byte[] buffer = new byte[16 * 1024]; // 16KB
            Task writeTask = null;
            await using (var fileStream = File.Open(file, FileMode.Open))
            {
                await using (var fs = new DeflateStream(fileStream, CompressionMode.Decompress))
                {
                    while (fs.CanRead)
                    {
                        if (await fs.ReadAsync(buffer, 0, 32) != 32) break;
                        Guid g = new Guid(new ReadOnlySpan<byte>(buffer, 0, 16));
                        int streamNameLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16, 4));
                        int eventTypeLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16 + 4, 4));
                        int metadataLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16 + 8, 4));
                        int dataLen = MemoryMarshal.Read<int>(new ReadOnlySpan<byte>(buffer, 16 + 12, 4));


                        var nameLen = streamNameLen + eventTypeLen;
                        if (await fs.ReadAsync(buffer, 0, nameLen) != nameLen) break;

                        string streamName = Encoding.UTF8.GetString(buffer, 0, streamNameLen);
                        string eventType = Encoding.UTF8.GetString(buffer, streamNameLen, eventTypeLen);

                        byte[] metadata = new byte[metadataLen];
                        byte[] data = new byte[dataLen];

                        if (await fs.ReadAsync(metadata, 0, metadataLen) != metadataLen) break;
                        if (await fs.ReadAsync(data, 0, dataLen) != dataLen) break;

                        if (writeTask != null)
                            await writeTask;
                        var d = new global::EventStore.ClientAPI.EventData(g, eventType, true, metadata, data);
                        writeTask = _connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, d);
                    }
                }

                if (writeTask != null)
                    await writeTask;
            }
        }
        public async Task WriteEventsToFile(string file)
        {
            Log.Information("Writing events to file {fileName}", file);
            Stopwatch s = new Stopwatch();
            s.Start();
            byte[] guidBuffer = new byte[16];
            await using (var fileStream = File.Open(file, FileMode.Create))
            {
                await using (var fs = new DeflateStream(fileStream, CompressionMode.Compress))
                await using (BinaryWriter sw = new BinaryWriter(fs))
                {
                    Position p = Position.Start;
                    AllEventsSlice slice = null;
                    do
                    {
                        slice = await _connection.ReadAllEventsForwardAsync(p, 1000, false);
                        foreach (var e in slice.Events)
                        {
                            if (!e.Event.EventStreamId.StartsWith("$") && e.Event.EventType != "$>")
                            {
                                e.Event.EventId.TryWriteBytes(guidBuffer);
                                var streamIdName = Encoding.UTF8.GetBytes(e.Event.EventStreamId);
                                var eventType = Encoding.UTF8.GetBytes(e.Event.EventType);

                                sw.Write(guidBuffer);               // 16
                                sw.Write(streamIdName.Length);      // 16 + 4 
                                sw.Write(eventType.Length);         // 16 + 8
                                sw.Write(e.Event.Metadata.Length);  // 16 + 12 
                                sw.Write(e.Event.Data.Length);      // 16 + 16

                                sw.Write(streamIdName);
                                sw.Write(eventType);
                                sw.Write(e.Event.Metadata);
                                sw.Write(e.Event.Data);
                            }
                        }

                        p = slice.NextPosition;
                    } while (!slice.IsEndOfStream);
                }
            }
            s.Stop();
            Log.Information("Wrinting done in: {duration}", s.Elapsed);
            
        }
        public NativeEventStore(IEventStoreConnection connection, 
            ProjectionsManager projectionsManager, 
            UserCredentials credentials,
            EventStoreSettings settings)

        {
            _settings = settings;
            
            _subscriptions = new ConcurrentBag<ISubscription>();
            _projectionsManager = projectionsManager;
            _connection = connection;
            _connection.ConnectAsync().Wait();
            _credentials = credentials;
            
        }
        public NativeEventStore(EventStoreSettings settings, Uri tcpUrl = null, 
            Uri httpProjectionUrl = null,
            string userName = "admin", 
            string password = "changeit",
            bool ignoreServerCert = false,
            bool disableTls = false)
        {
            _settings = settings;
            httpProjectionUrl = httpProjectionUrl == null ? new Uri("https://localhost:2113") : httpProjectionUrl;
            tcpUrl = tcpUrl == null ? new Uri("tcp://127.0.0.1:1113") : tcpUrl;

            _subscriptions = new ConcurrentBag<ISubscription>();
            _credentials = new UserCredentials(userName, password);

            var tcpSettings = ConnectionSettings.Create()
                .DisableServerCertificateValidation()
                //.UseDebugLogger()
                //.EnableVerboseLogging()
                .KeepReconnecting()
                .KeepRetrying()
                .LimitReconnectionsTo(1000)
                .LimitRetriesForOperationTo(100)
                .WithConnectionTimeoutOf(TimeSpan.FromSeconds(5))
                .SetDefaultUserCredentials(_credentials);

            if (disableTls)
                tcpSettings = tcpSettings.DisableTls();

            if (ignoreServerCert)
                tcpSettings = tcpSettings.DisableServerCertificateValidation();

            _projectionsManager = new ProjectionsManager(new ConsoleLogger(), new DnsEndPoint(httpProjectionUrl.Host, httpProjectionUrl.Port), TimeSpan.FromSeconds(10),
                ignoreServerCert ? IgnoreServerCertificateHandler() : null, httpProjectionUrl.Scheme);
            _connection = EventStoreConnection.Create(tcpSettings.Build(), tcpUrl);
           
        }

        
        public async Task CheckConnectivity()
        {
            if (!_connected)
            {
                await _connection.ConnectAsync();
                var slice = await _connection.ReadAllEventsBackwardAsync(Position.End, 1, true, _credentials);
                _connected = true;
            }
        }
        
       

        public async Task Subscribe(string name,
            bool fromBeginning,
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory processingFactory,
            params string[] sourceEventTypes)
        {
            await CheckConnectivity();
            var types = sourceEventTypes.ToList();
            types.Sort();

            Guid key = String.Concat(types).ToGuid();
            var streamName = $"{_settings.ProjectionStreamPrefix}{name}-{key}";
            
            if (types.Count > 1)
            {
                var projectionName = $"{name}-{key}";
                _settings.Logger.Information("Subscription for {ControllerName} with {projectionName} from {stream} on: {events}",
                    processingFactory.Config.Type.Name,
                    projectionName,
                    streamName,
                    string.Join(", ", types.Select(x => x.Replace("$et-", ""))));

                var projections = await _projectionsManager.ListContinuousAsync(_credentials);
                // we make projection only when we need to.
                if (!projections.Exists(x => x.Name == projectionName))
                    await CreateTrackingProjection(projectionName, streamName, types);
                else
                    await UpdateTrackingProjectionIfNeeded(projectionName, streamName, types);
            }
            else
            {
                streamName = $"$et-{sourceEventTypes[0]}";
            }

            if (isPersistent)
                await SubscribePersistently(fromBeginning, onEvent, streamName, processingFactory);
            else
                await Subscribe(fromBeginning, onEvent, streamName, processingFactory);
        }
        private async Task UpdateTrackingProjectionIfNeeded(string projectionName,
            string streamName,
            IEnumerable<string> types)
        {
            var query = CreateProjectionQuery(streamName, types);
            var config = await _projectionsManager.GetConfigAsync(projectionName, _credentials);
            
            var currentQuery = await _projectionsManager.GetQueryAsync(projectionName, _credentials);
            if (query != currentQuery || !config.EmitEnabled)
            {
                Log.Information("Updating continues projection definition and config: {projectionName}", projectionName);
                await _projectionsManager.UpdateQueryAsync(projectionName, query, true, _credentials);
            }
            
        }
        private async Task CreateTrackingProjection(string projectionName, 
            string streamName,
            IEnumerable<string> types)
        {
            var query = CreateProjectionQuery(streamName,  types);
            await _projectionsManager.CreateContinuousAsync(projectionName, query, false, _credentials);
        }

        private static string CreateProjectionQuery(string streamName,
            IEnumerable<string> types)
        {
            StringBuilder query = new StringBuilder();
            //query.AppendLine("options({");
            //query.AppendLine("resultStreamName: \"\","); // we are not creating a stream
            //query.AppendLine("$includeLinks: true,");
            //if (processingLag > TimeSpan.Zero)
            //{
                //query.AppendLine("  reorderEvents: true,");
                //query.AppendLine($"  processingLag: {processingLag.TotalMilliseconds}");
            //}
            //else
            //{
                //query.AppendLine("  reorderEvents: false,");
                //query.AppendLine("  processingLag: 0");
            //}

            //query.AppendLine("});");

            //query.AppendLine();
            query.Append("fromStreams([");
            query.Append(string.Join(',', types.Select(i => $"'$et-{i}'")));
            query.Append("])");
            query.AppendLine();
            query.Append(".when( { \r\n    $any : function(s,e) { linkTo('");
            query.Append(streamName);
            query.Append("', e) }\r\n});");

            string queryT = query.ToString();
            return queryT;
        }

        private async Task Subscribe(bool fromBeginning,
            EventHandler onEvent,
            string streamName, 
            IProcessingContextFactory processingContextFactory)
        {
            ContinuesSubscription s = new ContinuesSubscription(this, Log, fromBeginning, onEvent, streamName, processingContextFactory);
            await s.Subscribe();
            _subscriptions.Add(s);
        }
        
       
        private async Task SubscribePersistently(bool fromBeginning,
            EventHandler onEvent,
            string streamName, 
            IProcessingContextFactory processingContextFactory)
        {
            PersistentSubscription s = new PersistentSubscription(this, Log, fromBeginning, onEvent, streamName, processingContextFactory);
            await s.Subscribe();
            _subscriptions.Add(s);
        }

        
        public IStream GetStream(string category, Guid id, IContext context)
        {
            context ??= StaticProcessingContext.Context;

            return new Stream(this, category, id, _connection, 
                _settings.MetadataSerializerFactory.Get(context),
                _settings.Serializer);
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

            m[MetadataProperty.Category] = streamId.Remove(splitIndex);
            m[MetadataProperty.StreamId] = Guid.Parse(streamId.Substring(splitIndex + 1));

            return (m, e);
        }

        public IEnumerable<IStream> GetStreams()
        {
            StreamEventsSlice pack = null;

            do
            {
                pack = _connection.ReadStreamEventsForwardAsync("$streams", pack?.NextEventNumber ?? 0, 1000, false).GetAwaiter().GetResult();
                foreach (var s in pack.Events)
                {
                    string streamName = Encoding.UTF8.GetString(s.Event.Data);

                    int atIndex = streamName.IndexOf('@');
                    int dashIndex = streamName.IndexOf('-', atIndex);

                    string category = streamName.Substring(atIndex + 1, dashIndex - atIndex - 1);
                    Guid id = Guid.Parse(streamName.Substring(dashIndex + 1));

                    yield return GetStream(category, id,null);
                }
            } while (!pack.IsEndOfStream);
        }
    }
}