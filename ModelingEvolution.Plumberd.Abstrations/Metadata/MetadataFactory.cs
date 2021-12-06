using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class MetadataFactory : IMetadataFactory
    {
        class ContextFactory
        {
            private readonly IMetadataSchema _schema;
            private readonly List<IMetadataEnricher> _enrichers;
            public ContextScope Scope { get; }
            public IMetadataSchema Schema => _schema;

            public ContextFactory(ContextScope scope)
            {
                Scope = scope;
                _enrichers = new List<IMetadataEnricher>();
                _schema = new MetadataSchema();
            }

            public void Add(IMetadataEnricher enricher)
            {
                _enrichers.Add(enricher);
                enricher.RegisterSchema(_schema);
            }

            public void RegisterSystem(MetadataProperty property)
            {
                _schema.RegisterSystem(property);
            }

            public IMetadata Create(IContext context, IRecord record)
            {
                IMetadata m = new Metadata(_schema, false);
                foreach (var c in _enrichers)
                {
                    m = c.Enrich(m, record, context);
                }
                return m;
            }
        }

        private IEnumerable<ContextFactory> Factories
        {
            get
            {
                yield return _commandInvocationFactory;
                yield return _commandHandlerFactory;
                yield return _eventHandlerContextFactory;
            }
        }
        private readonly ContextFactory _eventHandlerContextFactory;
        private readonly ContextFactory _commandHandlerFactory;
        private readonly ContextFactory _commandInvocationFactory;


        public IMetadataSchema Schema(ContextScope scope)
        {
            switch (scope)
            {
                case ContextScope.Command:
                    return _commandHandlerFactory.Schema;

                case ContextScope.Event:
                    return _eventHandlerContextFactory.Schema;
                    
                case ContextScope.Invocation:
                    return _commandInvocationFactory.Schema;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
            }
        }

        public void Register(Func<IMetadataEnricher> enricher, 
            ContextScope scope = ContextScope.All)
        {
            if ((scope & ContextScope.Command) == ContextScope.Command)
                _commandHandlerFactory.Add(enricher());

            if ((scope & ContextScope.Event) == ContextScope.Event)
                _eventHandlerContextFactory.Add(enricher());

            if ((scope & ContextScope.Invocation) == ContextScope.Invocation)
                _commandInvocationFactory.Add(enricher());

        }
        public MetadataFactory()
        {
            _commandHandlerFactory = new ContextFactory(ContextScope.Command);
            _commandInvocationFactory = new ContextFactory(ContextScope.Invocation);
            _eventHandlerContextFactory = new ContextFactory(ContextScope.Event);
        }

        public IMetadata Create(IRecord r, IContext context)
        {
            switch (context)
            {
                case ICommandHandlerContext h:
                    return _commandHandlerFactory.Create(context, r);
                case IEventHandlerContext h:
                    return _eventHandlerContextFactory.Create(context, r);
                case ICommandInvocationContext h:
                    return _commandInvocationFactory.Create(context, r);
                default:
                    throw new NotSupportedException("Unkown context.");
            }
        }
        

        public void LockRegistration()
        {
            foreach (var f in Factories)
            {
                f.RegisterSystem(MetadataProperty.Category());
                f.RegisterSystem(MetadataProperty.StreamId());
                f.RegisterSystem(MetadataProperty.StreamPosition());
            }
        }
    }
}