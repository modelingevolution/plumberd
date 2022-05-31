using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class VersionEnricherExtensions
    {
        private static readonly System.Version _defaultVersion = new Version(0, 0);
        public static Version Version(this IMetadata ev)
        {
            string version =(String)ev[ev.Schema.Enricher<VersionEnricher>().Version];
            return string.IsNullOrWhiteSpace(version) ? _defaultVersion :  System.Version.Parse(version);
        }
    }
    public class VersionEnricher : IMetadataEnricher
    {
        public MetadataProperty Version;
        

        public void RegisterSchema(IMetadataSchema register)
        {
            this.Version = register.Register("Version", typeof(string), this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext epc:
                    m[Version] = epc.Metadata.Version().ToString();
                    break;
                case ICommandHandlerContext c:
                    m[Version] = (c.Metadata?.Version() ?? c.Version).ToString(); //  c.Record.Id
                   
                    break;
                case ICommandInvocationContext c:
                    m[Version] = c.Version.ToString();
                    break;
                default:
                    throw new NotSupportedException("Unsupported context.");
            }

            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new VersionEnricher();
        }
    }
    public class CorrelationEnricher : IMetadataEnricher
    {
        public MetadataProperty CorrelationId;
        public MetadataProperty CausationId;
        public MetadataProperty Hop;
        public void RegisterSchema(IMetadataSchema register)
        {
            this.CorrelationId = register.Register("$correlationId", typeof(Guid),this, true);
            this.CausationId = register.Register("$causationId", typeof(Guid), this, true);
            this.Hop = register.Register("Hop", typeof(long), this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext epc:
                    m[CorrelationId] = epc.Metadata.CorrelationId();
                    m[CausationId] = epc.Record.Id;
                    m[Hop] = epc.Metadata.Hop() + 1;
                    break;
                case ICommandHandlerContext c:
                    m[CorrelationId] = c.Metadata?.CorrelationId() ?? Guid.Empty; //  c.Record.Id
                    m[CausationId] = c.Record?.Id ?? Guid.Empty;
                    m[Hop] = (c.Metadata?.Hop() ?? 0) + 1;
                    break;
                case ICommandInvocationContext c:
                    m[CorrelationId] = c.Command.Id;
                    m[CausationId] = c.Command.Id;
                    m[Hop] = 1L;
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