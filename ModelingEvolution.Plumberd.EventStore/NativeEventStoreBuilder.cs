using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ModelingEvolution.Plumberd.EventStore
{

    public class ConfigurationBuilder
    {
        private readonly List<IProjectionConfig> _projectionConfigs;
        private IMetadataSerializerFactory _metadataSerializer;
        private IRecordSerializer _recordSerializer;
        private IMetadataFactory _metadataFactory;
        
        private Func<Type, string[]> _convention;
        private Uri _httpUrl;
        
        private string _userName;
        private string _password;
        private bool _ignoreCert;
        private bool _checkConnectivityAsync;
        
        public bool _withoutDefaultEnrichers;

        public ConfigurationBuilder()
        {
            _enrichers = new List<(Func<IMetadataEnricher>, ContextScope)>();
            _userName = "admin";
            _password = "changeit";
            _httpUrl = new Uri("http://127.0.0.1:2113/");
            _metadataSerializer = null;
            _projectionConfigs = new List<IProjectionConfig>();
        }

        public ConfigurationBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            this._loggerFactory = loggerFactory;
            return this;
        }
        public ConfigurationBuilder WithConfig(IConfiguration c)
        {
            return WithConfig(c, "EventStore");
        }

        public ConfigurationBuilder WithCredentials(string username, string password)
        {
            this.SetIfNotEmpty(ref _userName, username);
            this.SetIfNotEmpty(ref _password, password);
            return this;
        }
        public ConfigurationBuilder WithConfig(IConfiguration c, string sectionKey)
        {
            
            
            this.SetIfNotEmpty(ref _httpUrl, c[$"{sectionKey}:HttpUrl"]);
            this.SetIfNotEmpty(ref _userName, c[$"{sectionKey}:User"]);
            this.SetIfNotEmpty(ref _password, c[$"{sectionKey}:Password"]);

            var isInsecure = c[$"{sectionKey}:Insecure"];
            if (!string.IsNullOrWhiteSpace(isInsecure))
            {
                if (bool.Parse(isInsecure))
                    this.InSecure();
            }
            
            logger.LogInformation("EventStore HttpUrl: {httpUrl}", _httpUrl);
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

        public ConfigurationBuilder WithProjectionsConfigFrom(Assembly a)
        {
            var configs = a.GetTypes()
                .Where(x => typeof(IProjectionConfig).IsAssignableFrom(x) && !x.IsAbstract && x.IsClass)
                .Select(x => Activator.CreateInstance(x))
                .Cast<IProjectionConfig>();
            foreach (var i in configs)
                WithProjectionsConfig(i);
            return this;
        }
        public ConfigurationBuilder WithProjectionsConfig<TProjectionConfig>()
        where TProjectionConfig: IProjectionConfig, new()
        {
            TProjectionConfig config = new TProjectionConfig();
            return WithProjectionsConfig(config);
        }
        private bool _logWrittenEventsToLog = false;
        public ConfigurationBuilder WithWrittenEventsToLog(bool isEnabled = true)
        {
            _logWrittenEventsToLog = isEnabled;
            return this;
        }
        public ConfigurationBuilder WithProjectionsConfig(IProjectionConfig config)
        {
            _projectionConfigs.Add(config);
            return this;
        }
        public ConfigurationBuilder WithMetadataFactory(IMetadataFactory f)
        {
            _metadataFactory = f;
            return this;
        }
        public ConfigurationBuilder WithRecordSerializer(IRecordSerializer serializer)
        {
            _recordSerializer = serializer;
            return this;
        }
        public ConfigurationBuilder WithMetadataSerializerFactory(IMetadataSerializerFactory f)
        {
            _metadataSerializer = f;
            return this;
        }
        public ConfigurationBuilder CheckConnectivityAsync()
        {
            _checkConnectivityAsync = true;
            return this;
        }
        public ConfigurationBuilder WithHttpUrl(Uri http)
        {
            _httpUrl = http;
            return this;
        }
        private readonly List<(Func<IMetadataEnricher>, ContextScope)> _enrichers;
        public ConfigurationBuilder WithMetadataEnricher<T>(ContextScope scope)
            where T: IMetadataEnricher
        {
            return WithMetadataEnricher(() => Activator.CreateInstance<T>(), scope);
        }

        private ConfigurationBuilder WithDefaultEnrichers()
        {
            return WithMetadataEnricher<CorrelationEnricher>(ContextScope.All)
                    .WithMetadataEnricher<UserIdEnricher>(ContextScope.All)
                    .WithMetadataEnricher<SessionEnricher>(ContextScope.All)
                    .WithMetadataEnricher(() => new RecordTypeEnricher(TypeNamePersistenceConvention.AssemblyQualifiedName), ContextScope.All)
                    .WithMetadataEnricher(() => new ProcessingUnitEnricher(TypeNamePersistenceConvention.Name), ContextScope.Event | ContextScope.Command)
                    .WithMetadataEnricher<VersionEnricher>(ContextScope.All)
                    .WithMetadataEnricher<CreateTimeEnricher>(ContextScope.All);
        }

        public ConfigurationBuilder WithoutDefaultEnrichers()
        {
            _withoutDefaultEnrichers = true;
            return this;
        }
        public ConfigurationBuilder WithEventNamingConvention(Func<Type, string[]> convention)
        {
            _convention = convention;
            return this;
        }
        public ConfigurationBuilder WithMetadataEnricher(Func<IMetadataEnricher> enricher, ContextScope scope)
        {
            _enrichers.Add((enricher, scope));
            return this;
        }

        public ConfigurationBuilder IgnoreServerCert()
        {
            _ignoreCert = true;
            return this;
        }
        public ConfigurationBuilder InSecure()
        {
            _ignoreCert = true;
            return this;
        }
        
        
        

        private bool _isDevelopment;
        private ILoggerFactory _loggerFactory;

        public ConfigurationBuilder WithDevelopmentEnv(bool isDev)
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
                _recordSerializer ?? new RecordSerializerDispatcher(),
                _isDevelopment,_loggerFactory,
                _convention);

            EventStoreClientSettings sw = new EventStoreClientSettings();
            sw.DefaultCredentials = new UserCredentials(this._userName ?? "admin", this._password ?? "changeit");
            sw.ConnectivitySettings.Address = this._httpUrl;
            sw.ConnectivitySettings.Insecure = this._ignoreCert;
            sw.ConnectivitySettings.TlsVerifyCert = !this._ignoreCert;
            if(_ignoreCert)
                sw.CreateHttpMessageHandler = () =>
                {
                    if (this._checkConnectivityAsync)
                    {
                        return new SocketsHttpHandler()
                        {
                            EnableMultipleHttp2Connections = true,
                            SslOptions = new SslClientAuthenticationOptions()
                                { RemoteCertificateValidationCallback = (s, c, chain, e) => true }
                        };
                    }
                    else
                    {
                        return new GrpcHttpClientHandler();
                    }
                   
                };
            var es = new NativeEventStore(sw, sw.DefaultCredentials, settings, () => _loggerFactory.CreateLogger<NativeEventStore>(), _projectionConfigs);
            if (_logWrittenEventsToLog)
                es.Connected += WireLog;
            // Temporary
            if (!checkConnectivity) return es;

            if (_checkConnectivityAsync)
            {
                Task.Run(es.CheckConnectivity);
            }
            else
                Task.Run(es.CheckConnectivity).GetAwaiter().GetResult();
            

            return es;
        }
        private ILogger loggerValue;

        private ILogger logger
        {
            get
            {
                if (loggerValue != null) return loggerValue;
                loggerValue = _loggerFactory?.CreateLogger<ConfigurationBuilder>();
                return loggerValue;
            }
        }
        private void WireLog(NativeEventStore es)
        {
            es.Connection.SubscribeToAllAsync(FromAll.End, onLog);
        }
        private Task onLog(StreamSubscription s, ResolvedEvent e, CancellationToken t)
        {
            if (!e.Event.EventStreamId.StartsWith("$"))
            {
                bool isProjection = e.Event.EventStreamId.StartsWith("/");
                bool isCommand = e.Event.EventStreamId.StartsWith(">") || e.Event.EventStreamId.StartsWith("/>");
                bool isFact = !isProjection && !isCommand;
                string subject = isFact ? "Fact" : (isCommand ? "Command" : "View");
                int index = e.Event.EventStreamId.IndexOf('-');
                string category = e.Event.EventStreamId.Remove(index);

                string id = e.Event.EventStreamId.Substring(index + 1);
                //Guid id = Guid.Parse(e.Event.EventStreamId.Substring(index + 1));
                if (e.Event.EventType != "$>")
                {
                    // it's not a link
                    string data = Encoding.UTF8.GetString(e.Event.Data.Span);
                    logger.LogInformation("{subject} {eventType} written in {category}\t{id} with data:{eventData}",
                        subject,
                        e.Event.EventType,
                        category, 
                        id,
                        data);
                } else 
                {
                    // it's a link
                    string data = Encoding.UTF8.GetString(e.Event.Data.Span);
                    logger.LogInformation("Link for {subject} written in {category}\t{id} with data:{linkData}",
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