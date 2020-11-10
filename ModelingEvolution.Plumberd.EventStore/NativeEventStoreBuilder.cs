using System;
using System.Collections.Generic;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Serilog;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class NativeEventStoreBuilder
    {
        private IMetadataSerializerFactory _metadataSerializer;
        private IRecordSerializer _recordSerializer;
        private IMetadataFactory _metadataFactory;
        private Func<Type, string[]> _convention;
        private Uri _tcpUrl;
        private Uri _httpUrl;
        private ILogger _logger;
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
                    .WithMetadataEnricher(() => new RecordTypeEnricher(TypeNamePersistenceConvention.AssemblyQualifiedName), ContextScope.All)
                    .WithMetadataEnricher(() => new ProcessingUnitEnricher(TypeNamePersistenceConvention.Name), ContextScope.Event | ContextScope.Command)
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
                _convention,
                _logger);


            var es = new NativeEventStore(settings, 
                _tcpUrl,
                _httpUrl,
                _userName,
                _password,
                _ignoreCert, 
                _disableTls);
            
            // Temporary
            if(checkConnectivity)
                es.CheckConnectivity().GetAwaiter().GetResult();
            
            return es;
        }

        public void WithLogger(ILogger logger)
        {
            this._logger = logger;
        }
    }
}