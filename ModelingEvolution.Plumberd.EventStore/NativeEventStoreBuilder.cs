using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Logging;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ModelingEvolution.Plumberd.EventStore
{

    public class NativeEventStoreBuilder
    {
        private readonly List<IProjectionConfig> _projectionConfigs;
        private IMetadataSerializerFactory _metadataSerializer;
        private IRecordSerializer _recordSerializer;
        private IMetadataFactory _metadataFactory;
        private Func<Type, string[]> _convention;
        private Uri _tcpUrl;
        private Uri _httpUrl;
        private static readonly ILogger _logger = LogFactory.GetLogger<NativeEventStoreBuilder>();
        private string _userName;
        private string _password;
        private bool _ignoreCert;
        private bool _disableTls;
        public bool _withoutDefaultEnrichers;

        public NativeEventStoreBuilder()
        {
            _enrichers = new List<(Func<IMetadataEnricher>, ContextScope)>();
            _userName = "admin";
            _password = "changeit";
            _httpUrl = new Uri("http://127.0.0.1:2113/");
            _metadataSerializer = null;
            _projectionConfigs = new List<IProjectionConfig>();
        }

        public NativeEventStoreBuilder WithConfig(IConfiguration c)
        {
            
            this.SetIfNotEmpty(ref _tcpUrl, c["EventStore:TcpUrl"]);
            this.SetIfNotEmpty(ref _httpUrl, c["EventStore:HttpUrl"]);
            this.SetIfNotEmpty(ref _userName, c["EventStore:User"]);
            this.SetIfNotEmpty(ref _password, c["EventStore:Password"]);

            var isInsecure = c["EventStore:Insecure"];
            if (!string.IsNullOrWhiteSpace(isInsecure))
            {
                if (bool.Parse(isInsecure))
                    this.InSecure();
            }
            _logger.LogInformation("EventStore TcpUrl: {tcpUrl}", _tcpUrl);
            _logger.LogInformation("EventStore HttpUrl: {httpUrl}", _httpUrl);
            return this;
        }
        private void SetIfNotEmpty(ref string dst, string src)
        {
            if (!string.IsNullOrWhiteSpace(src))
                dst = src;
        }
        private void SetIfNotEmpty(ref Uri dst, string url)
        {
            if(!string.IsNullOrWhiteSpace(url))
                dst = new Uri(url);
        }

        public NativeEventStoreBuilder WithProjectionsConfigFrom(Assembly a)
        {
            var configs = a.GetTypes()
                .Where(x => typeof(IProjectionConfig).IsAssignableFrom(x) && !x.IsAbstract && x.IsClass)
                .Select(x => Activator.CreateInstance(x))
                .Cast<IProjectionConfig>();
            foreach (var i in configs)
                WithProjectionsConfig(i);
            return this;
        }
        public NativeEventStoreBuilder WithProjectionsConfig<TProjectionConfig>()
        where TProjectionConfig: IProjectionConfig, new()
        {
            TProjectionConfig config = new TProjectionConfig();
            return WithProjectionsConfig(config);
        }
        private bool _logWrittenEventsToLog = false;
        public NativeEventStoreBuilder WithWrittenEventsToLog(bool isEnabled = true)
        {
            _logWrittenEventsToLog = isEnabled;
            return this;
        }
        public NativeEventStoreBuilder WithProjectionsConfig(IProjectionConfig config)
        {
            _projectionConfigs.Add(config);
            return this;
        }
        public NativeEventStoreBuilder WithMetadataFactory(IMetadataFactory f)
        {
            _metadataFactory = f;
            return this;
        }
        public NativeEventStoreBuilder WithRecordSerializer(IRecordSerializer serializer)
        {
            _recordSerializer = serializer;
            return this;
        }
        public NativeEventStoreBuilder WithMetadataSerializerFactory(IMetadataSerializerFactory f)
        {
            _metadataSerializer = f;
            return this;
        }
        public NativeEventStoreBuilder WithTcpUrl(Uri tcp)
        {
            _tcpUrl = tcp;
            return this;
        }
        public NativeEventStoreBuilder WithHttpUrl(Uri http)
        {
            _httpUrl = http;
            return this;
        }
        private readonly List<(Func<IMetadataEnricher>, ContextScope)> _enrichers;
        public NativeEventStoreBuilder WithMetadataEnricher<T>(ContextScope scope)
            where T: IMetadataEnricher
        {
            return WithMetadataEnricher(() => Activator.CreateInstance<T>(), scope);
        }

        private NativeEventStoreBuilder WithDefaultEnrichers()
        {
            return WithMetadataEnricher<CorrelationEnricher>(ContextScope.All)
                    .WithMetadataEnricher<UserIdEnricher>(ContextScope.All)
                    .WithMetadataEnricher<SessionEnricher>(ContextScope.All)
                    .WithMetadataEnricher(() => new RecordTypeEnricher(TypeNamePersistenceConvention.AssemblyQualifiedName), ContextScope.All)
                    .WithMetadataEnricher(() => new ProcessingUnitEnricher(TypeNamePersistenceConvention.Name), ContextScope.Event | ContextScope.Command)
                    .WithMetadataEnricher<VersionEnricher>(ContextScope.All)
                    .WithMetadataEnricher<CreateTimeEnricher>(ContextScope.All);
        }

        public NativeEventStoreBuilder WithoutDefaultEnrichers()
        {
            _withoutDefaultEnrichers = true;
            return this;
        }
        public NativeEventStoreBuilder WithEventNamingConvention(Func<Type, string[]> convention)
        {
            _convention = convention;
            return this;
        }
        public NativeEventStoreBuilder WithMetadataEnricher(Func<IMetadataEnricher> enricher, ContextScope scope)
        {
            _enrichers.Add((enricher, scope));
            return this;
        }

        public NativeEventStoreBuilder IgnoreServerCert()
        {
            _ignoreCert = true;
            return this;
        }
        public NativeEventStoreBuilder InSecure()
        {
            _ignoreCert = true;
            _disableTls = true;
            return this;
        }

        public NativeEventStoreBuilder WithConnectionCustomization(Action<ConnectionSettingsBuilder> customizaiton)
        {
            _connectionCustomizations = customizaiton;
            return this;
        }
        private Action<ConnectionSettingsBuilder> _connectionCustomizations;

        private bool _isDevelopment;
        public NativeEventStoreBuilder WithDevelopmentEnv(bool isDev)
        {
            _isDevelopment = isDev;
            return this;
        }
        public NativeEventStore Build(bool checkConnectivity = true)
        {
            if (!_withoutDefaultEnrichers)
                WithDefaultEnrichers();

            var eventMetadataFactory = _metadataFactory ?? new MetadataFactory();

            foreach(var (enricher, scope) in _enrichers)
                eventMetadataFactory.Register(enricher, scope);

            eventMetadataFactory.LockRegistration();

            var metadataSerializerFactory = _metadataSerializer ?? new MetadataSerializerFactory();

            metadataSerializerFactory.RegisterSchemaForContext(eventMetadataFactory.Schema(ContextScope.Command), ContextScope.Command);
            metadataSerializerFactory.RegisterSchemaForContext(eventMetadataFactory.Schema(ContextScope.Event), ContextScope.Event);
            metadataSerializerFactory.RegisterSchemaForContext(eventMetadataFactory.Schema(ContextScope.Invocation), ContextScope.Invocation);

            EventStoreSettings settings = new EventStoreSettings(eventMetadataFactory, 
                metadataSerializerFactory,
                _recordSerializer ?? new RecordSerializer(),
                _isDevelopment,
                _convention);


            var es = new NativeEventStore(settings, 
                _tcpUrl,
                _httpUrl,
                _userName,
                _password,
                _ignoreCert, 
                _disableTls,
                _connectionCustomizations,
                _projectionConfigs);
            if (_logWrittenEventsToLog)
                es.Connected += WireLog;
            // Temporary
            if(checkConnectivity)
                Task.Run(es.CheckConnectivity).GetAwaiter().GetResult();
            
            return es;
        }

        private void WireLog(NativeEventStore es)
        {
            es.Connection.SubscribeToAllAsync(false, onLog);
        }
        private Task onLog(EventStoreSubscription s, ResolvedEvent e)
        {
            if (!e.Event.EventStreamId.StartsWith("$"))
            {
                bool isProjection = e.Event.EventStreamId.StartsWith("/");
                bool isCommand = e.Event.EventStreamId.StartsWith(">") || e.Event.EventStreamId.StartsWith("/>");
                bool isFact = !isProjection && !isCommand;
                string subject = isFact ? "Fact" : (isCommand ? "Command" : "View");
                int index = e.Event.EventStreamId.IndexOf('-');
                string category = e.Event.EventStreamId.Remove(index);
                Guid id = Guid.Parse(e.Event.EventStreamId.Substring(index + 1));
                if (e.Event.EventType != "$>")
                {
                    // it's not a link
                    string data = Encoding.UTF8.GetString(e.Event.Data);                    
                    _logger.LogInformation("{subject} {eventType} written in {category}\t{id} with data:{eventData}",
                        subject,
                        e.Event.EventType,
                        category, 
                        id,
                        data);
                } else 
                {
                    // it's a link
                    string data = Encoding.UTF8.GetString(e.Event.Data);
                    _logger.LogInformation("Link for {subject} written in {category}\t{id} with data:{linkData}",
                        subject,
                        category,
                        id,
                        data);
                }
            }

            return Task.CompletedTask;
        }

        
    }
}