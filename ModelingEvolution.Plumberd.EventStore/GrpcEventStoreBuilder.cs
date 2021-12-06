using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Modellution.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ModelingEvolution.Plumberd.EventStore
{

    public class GrpcEventStoreBuilder
    {
        private readonly List<IProjectionConfig> _projectionConfigs;
        private IMetadataSerializerFactory _metadataSerializer;
        private IRecordSerializer _recordSerializer;
        private IMetadataFactory _metadataFactory;
        private Func<Type, string[]> _convention;
        
        private Uri _httpUrl;
        private static readonly ILogger _logger = LogFactory.GetLogger<GrpcEventStoreBuilder>();
        private string _userName;
        private string _password;
        private bool _isInsecure;
        
        public bool _withoutDefaultEnrichers;
        private Action<EventStoreClientSettings> _connectionCustomizations;
        private bool _isDevelopment;

        public GrpcEventStoreBuilder()
        {
            _enrichers = new List<(Func<IMetadataEnricher>, ContextScope)>();
            _userName = "admin";
            _password = "changeit";
            _httpUrl = new Uri("http://127.0.0.1:2113/");
            _metadataSerializer = null;
            _projectionConfigs = new List<IProjectionConfig>();
        }

        public GrpcEventStoreBuilder WithConfig(IConfiguration c)
        {

           
            this.SetIfNotEmpty(ref _httpUrl, c["EventStore:HttpUrl"]);
            this.SetIfNotEmpty(ref _userName, c["EventStore:User"]);
            this.SetIfNotEmpty(ref _password, c["EventStore:Password"]);

            var isInsecure = c["EventStore:Insecure"];
            if (!string.IsNullOrWhiteSpace(isInsecure))
            {
                if (bool.Parse(isInsecure))
                    this.InSecure();
            }
            
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
            if (!string.IsNullOrWhiteSpace(url))
                dst = new Uri(url);
        }

        public GrpcEventStoreBuilder WithProjectionsConfigFrom(Assembly a)
        {
            var configs = a.GetTypes()
                .Where(x => typeof(IProjectionConfig).IsAssignableFrom(x) && !x.IsAbstract && x.IsClass)
                .Select(x => Activator.CreateInstance(x))
                .Cast<IProjectionConfig>();
            foreach (var i in configs)
                WithProjectionsConfig(i);
            return this;
        }
        public GrpcEventStoreBuilder WithProjectionsConfig<TProjectionConfig>()
        where TProjectionConfig : IProjectionConfig, new()
        {
            TProjectionConfig config = new TProjectionConfig();
            return WithProjectionsConfig(config);
        }
        private bool _logWrittenEventsToLog = false;
        public GrpcEventStoreBuilder WithWrittenEventsToLog(bool isEnabled = true)
        {
            _logWrittenEventsToLog = isEnabled;
            return this;
        }
        public GrpcEventStoreBuilder WithProjectionsConfig(IProjectionConfig config)
        {
            _projectionConfigs.Add(config);
            return this;
        }
        public GrpcEventStoreBuilder WithMetadataFactory(IMetadataFactory f)
        {
            _metadataFactory = f;
            return this;
        }
        public GrpcEventStoreBuilder WithRecordSerializer(IRecordSerializer serializer)
        {
            _recordSerializer = serializer;
            return this;
        }
        public GrpcEventStoreBuilder WithMetadataSerializerFactory(IMetadataSerializerFactory f)
        {
            _metadataSerializer = f;
            return this;
        }
       
        public GrpcEventStoreBuilder WithHttpUrl(Uri http)
        {
            _httpUrl = http;
            return this;
        }
        private readonly List<(Func<IMetadataEnricher>, ContextScope)> _enrichers;
        public GrpcEventStoreBuilder WithMetadataEnricher<T>(ContextScope scope)
            where T : IMetadataEnricher
        {
            return WithMetadataEnricher(() => Activator.CreateInstance<T>(), scope);
        }

        private GrpcEventStoreBuilder WithDefaultEnrichers()
        {
            return WithMetadataEnricher<CorrelationEnricher>(ContextScope.All)
                    .WithMetadataEnricher<UserIdEnricher>(ContextScope.All)
                    .WithMetadataEnricher<SessionEnricher>(ContextScope.All)
                    .WithMetadataEnricher(() => new RecordTypeEnricher(TypeNamePersistenceConvention.AssemblyQualifiedName), ContextScope.All)
                    .WithMetadataEnricher(() => new ProcessingUnitEnricher(TypeNamePersistenceConvention.Name), ContextScope.Event | ContextScope.Command)
                    .WithMetadataEnricher<CreateTimeEnricher>(ContextScope.All);
        }

        public GrpcEventStoreBuilder WithoutDefaultEnrichers()
        {
            _withoutDefaultEnrichers = true;
            return this;
        }
        public GrpcEventStoreBuilder WithEventNamingConvention(Func<Type, string[]> convention)
        {
            _convention = convention;
            return this;
        }
        public GrpcEventStoreBuilder WithMetadataEnricher(Func<IMetadataEnricher> enricher, ContextScope scope)
        {
            _enrichers.Add((enricher, scope));
            return this;
        }

        public GrpcEventStoreBuilder IgnoreServerCert()
        {
            _isInsecure = true;
            return this;
        }
        public GrpcEventStoreBuilder InSecure()
        {
            _isInsecure = true;
            return this;
        }

        public GrpcEventStoreBuilder WithConnectionCustomization(Action<EventStoreClientSettings> customizaiton)
        {
            
            _connectionCustomizations = customizaiton;
            return this;
        }

        public GrpcEventStoreBuilder WithDevelopmentEnv(bool isDev)
        {
            _isDevelopment = isDev;
            return this;
        }
        public GrpcEventStore Build(bool checkConnectivity = true)
        {
            if (!_withoutDefaultEnrichers)
                WithDefaultEnrichers();

            var eventMetadataFactory = _metadataFactory ?? new MetadataFactory();

            foreach (var (enricher, scope) in _enrichers)
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
            
            return new GrpcEventStore(settings, isInsecure: _isInsecure);
            //var es = new GrpcEventStore(new UserCredentials(_userName, _password), settings);
            //if (_logWrittenEventsToLog)
            //    es.CheckConnectivity += WireLog;

            //if (checkConnectivity)
            //    Task.Run(es.CheckConnectivity).GetAwaiter().GetResult();

            //return es;
        }

        void WireLog(GrpcEventStore es)
        {

            es.Connection.SubscribeToAllAsync(onLog,false);


        }

        private Task onLog(StreamSubscription s, ResolvedEvent e, CancellationToken arg3)
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
                    string data = Encoding.UTF8.GetString(e.Event.Data.ToArray());
                    _logger.LogInformation("{subject} {eventType} written in {category}\t{id} with data:{eventData}",
                        subject,
                        e.Event.EventType,
                        category,
                        id,
                        data);
                }
                else
                {
                    // it's a link
                    string data = Encoding.UTF8.GetString(e.Event.Data.ToArray());
                    _logger.LogInformation("Link for {subject} written in {category}\t{id} with data:{linkData}",
                        subject,
                        category,
                        id,
                        data);
                }
            }

            return Task.CompletedTask;
        }

        private Task onLog(StreamSubscription s, ResolvedEvent e)
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
                    string data = Encoding.UTF8.GetString(e.Event.Data.ToArray());
                    _logger.LogInformation("{subject} {eventType} written in {category}\t{id} with data:{eventData}",
                        subject,
                        e.Event.EventType,
                        category,
                        id,
                        data);
                }
                else
                {
                    // it's a link
                    string data = Encoding.UTF8.GetString(e.Event.Data.ToArray());
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
