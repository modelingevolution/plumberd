using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    public interface IMetadataEnricher
    {
        void RegisterSchema(IMetadataSchema register);
        IMetadata Enrich(IMetadata m, IRecord e, IContext context);
        IMetadataEnricher Clone();
    }
    [Flags]
    public enum ContextScope
    {
        Command = 0x1,
        Event = 0x2,
        Invocation = 0x4,
        All = 0x1 | 0x2 | 0x4
    }

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
                f.RegisterSystem(MetadataProperty.Category);
                f.RegisterSystem(MetadataProperty.StreamId);
            }
        }
    }

    public static class MetadataSchemaExtensions
    {
        public static T Enricher<T>(this IMetadataSchema schema) where T : IMetadataEnricher
        {
            return (T)schema.Enrichers[typeof(T)];
        }
    }
    public readonly struct Metadata : IMetadata
    {
        private readonly IMetadataSchema _schema;
        private readonly object[] _data; // fixed?
        public Metadata(IMetadataSchema schema, bool read)
        {
            _schema = schema;
            _data = new object[read ? _schema.Count : _schema.WriteProperties.Count];
        }

        public IMetadataSchema Schema => _schema;
        public object this[int index]
        {
            get => _data[index];
            set
            {
                if (_data[index] != null)
                    throw new InvalidOperationException("Cannot override metadata.");
                _data[index] = value;
            }
        }
        public object this[MetadataProperty property]
        {
            get => this[property.Order];
            set => this[property.Order] = value;
        }
    }
    public interface IMetadata
    {
        IMetadataSchema Schema { get; }
        object this[MetadataProperty property] { get; set; }
    }

    public sealed class MetadataProperty
    {
        public static readonly MetadataProperty StreamId = new MetadataProperty("StreamId", typeof(Guid), -1, null, false);
        public static readonly MetadataProperty Category = new MetadataProperty("Category", typeof(String),-1, null, false);
        public MetadataProperty(string name, 
            Type type, 
            int order, 
            IMetadataEnricher enricher, 
            bool isPersistable)
        {
            Name = name;
            Type = type;
            Order = order;
            Enricher = enricher;
            IsPersistable = isPersistable;
        }
        public bool IsPersistable { get; }
        public string Name { get;  }
        public Type Type { get; }
        public IMetadataEnricher Enricher { get; }
        public int Order { get; internal set; }
    }
}