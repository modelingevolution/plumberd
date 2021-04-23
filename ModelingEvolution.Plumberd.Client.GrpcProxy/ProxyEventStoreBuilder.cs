using System;
using System.Collections.Generic;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    public class ProxyEventStoreBuilder
    {
        private IMetadataSerializerFactory _metadataSerializer;
        private IRecordSerializer _recordSerializer;
        private IMetadataFactory _metadataFactory;
        
        private Serilog.ILogger _logger;
        
        public bool _withoutDefaultEnrichers;

        public ProxyEventStoreBuilder()
        {
            _enrichers = new List<(Func<IMetadataEnricher>, ContextScope)>();
           
            _metadataSerializer = null;
        }

        public ProxyEventStoreBuilder WithMetadataFactory(IMetadataFactory f)
        {
            _metadataFactory = f;
            return this;
        }
        public ProxyEventStoreBuilder WithRecordSerializer(IRecordSerializer serializer)
        {
            _recordSerializer = serializer;
            return this;
        }
        public ProxyEventStoreBuilder WithMetadataSerializerFactory(IMetadataSerializerFactory f)
        {
            _metadataSerializer = f;
            return this;
        }
       
        private readonly List<(Func<IMetadataEnricher>, ContextScope)> _enrichers;
        private GrpcChannel _channel;
        private TypeRegister _typeRegister;
        private Uri _channelAddress;
        private bool _isDevelopment;

        public ProxyEventStoreBuilder WithMetadataEnricher<T>(ContextScope scope)
            where T : IMetadataEnricher
        {
            return WithMetadataEnricher(() => Activator.CreateInstance<T>(), scope);
        }

        private ProxyEventStoreBuilder WithDefaultEnrichers()
        {
            return WithMetadataEnricher<CorrelationEnricher>(ContextScope.All)
                .WithMetadataEnricher<UserIdEnricher>(ContextScope.All)
                .WithMetadataEnricher(() => new RecordTypeEnricher(TypeNamePersistenceConvention.AssemblyQualifiedName), ContextScope.All)
                .WithMetadataEnricher(() => new ProcessingUnitEnricher(TypeNamePersistenceConvention.Name), ContextScope.Event | ContextScope.Command)
                .WithMetadataEnricher<CreateTimeEnricher>(ContextScope.All);
        }

        public ProxyEventStoreBuilder WithTypeRegister(TypeRegister tp)
        {
            this._typeRegister = tp;
            return this;
        }
        public ProxyEventStoreBuilder WithChannel(GrpcChannel channel, Uri address)
        {
            this._channelAddress = address;
            this._channel = channel;
            return this;
        }
        public ProxyEventStoreBuilder WithoutDefaultEnrichers()
        {
            _withoutDefaultEnrichers = true;
            return this;
        }
        
        public ProxyEventStoreBuilder WithMetadataEnricher(Func<IMetadataEnricher> enricher, ContextScope scope)
        {
            _enrichers.Add((enricher, scope));
            return this;
        }

       

        public ProxyEventStoreBuilder WithDevelopmentEnv(bool isDev)
        {
            _isDevelopment = isDev;
            return this;
        }
        public GrpcEventStoreFacade Build(IServiceProvider sp)
        {
            
            if (!_withoutDefaultEnrichers)
                WithDefaultEnrichers();

            var eventMetadataFactory = _metadataFactory ?? new MetadataFactory();

            foreach (var (enricher, scope) in _enrichers)
                eventMetadataFactory.Register(enricher, scope);

            var metadataSerializerFactory = _metadataSerializer ?? new MetadataSerializerFactory();

            metadataSerializerFactory.RegisterSchemaForContext(eventMetadataFactory.Schema(ContextScope.Command), ContextScope.Command);
            metadataSerializerFactory.RegisterSchemaForContext(eventMetadataFactory.Schema(ContextScope.Event), ContextScope.Event);
            metadataSerializerFactory.RegisterSchemaForContext(eventMetadataFactory.Schema(ContextScope.Invocation), ContextScope.Invocation);

            Func<Channel> channelFactory = null;
            if (_channel != null) channelFactory = () => new Channel(_channel, _channelAddress);
            else channelFactory = sp.GetService<Channel>;
            Func<ISessionManager> sessionManager = sp.GetService<ISessionManager>;
            eventMetadataFactory.LockRegistration();
            var es = new GrpcEventStoreFacade(channelFactory, 
                eventMetadataFactory, metadataSerializerFactory, sessionManager, _typeRegister, 
                this._isDevelopment);
            
            return es;
        }

        public void WithLogger(Serilog.ILogger logger)
        {
            this._logger = logger;
        }
    }
}