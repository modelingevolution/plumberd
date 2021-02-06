using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class CorrelationEnricherExtensions
    {
        public static Guid CorrelationId(this IMetadata ev)
        {
            return (Guid)ev[ev.Schema.Enricher<CorrelationEnricher>().CorrelationId];
        }
        public static Guid CausationId(this IMetadata ev)
        {
            return (Guid)ev[ev.Schema.Enricher<CorrelationEnricher>().CausationId];
        }
    }

    public static class UserMetadataEnricherExtensions
    {
        public static Guid UserId(this IMetadata ev)
        {
            return (Guid)ev[ev.Schema.Enricher<UserIdEnricher>().UserIdProperty];
        }
       
    }
    public static class SystemMetadataEnricherExtensions
    {
        public static ulong StreamPosition(this IMetadata m)
        {
            return (ulong) m[MetadataProperty.StreamPosition];
        }
        public static Guid StreamId(this IMetadata m)
        {
            return (Guid) m[MetadataProperty.StreamId];
        }

        public static string Category(this IMetadata m)
        {
            return (string) m[MetadataProperty.Category];
        }
    }
    public class UserIdEnricher : IMetadataEnricher
    {
        internal MetadataProperty UserIdProperty;
        public void RegisterSchema(IMetadataSchema register)
        {
            this.UserIdProperty = register.Register<Guid>("UserId", this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext c:
                    m[UserIdProperty] = c.Metadata.UserId();
                   
                    break;
                case ICommandHandlerContext c:
                    m[UserIdProperty] = c.Metadata.UserId();

                    break;
                case ICommandInvocationContext c:
                    m[UserIdProperty] = c.UserId;
                    break;
                default:
                    throw new NotSupportedException("Unsupported context.");
            }
            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new UserIdEnricher();
        }
    }
    public class CorrelationEnricher : IMetadataEnricher
    {
        public MetadataProperty CorrelationId;
        public MetadataProperty CausationId;
        public void RegisterSchema(IMetadataSchema register)
        {
            this.CorrelationId = register.Register("$correlationId", typeof(Guid),this, true);
            this.CausationId = register.Register("$causationId", typeof(Guid), this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext epc:
                    m[CorrelationId] = epc.Metadata.CorrelationId();
                    m[CausationId] = epc.Record.Id;
                    break;
                case ICommandHandlerContext c:
                    m[CorrelationId] = c.Record.Id;
                    m[CausationId] = c.Record.Id;
                    break;
                case ICommandInvocationContext c:
                    m[CorrelationId] = c.Command.Id;
                    m[CausationId] = c.Command.Id;
                    break;
                default:
                    throw new NotSupportedException("Unsupported context.");
            }

            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new CorrelationEnricher();
        }
    }
}