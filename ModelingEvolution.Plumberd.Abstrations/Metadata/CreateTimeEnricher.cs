using System;
using System.Reflection;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class CreateTimeEnricher : IMetadataEnricher
    {
        internal MetadataProperty Property;

        public void RegisterSchema(IMetadataSchema register)
        {
            this.Property = register.Register("Created", typeof(DateTimeOffset), this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            m[Property] = DateTimeOffset.Now;
            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new CreateTimeEnricher();
        }
    }
    public static class CreateTimeEnricherExtensions
    {
        public static DateTimeOffset Created(this IMetadata m)
        {
            return (DateTimeOffset)m[m.Schema.Enricher<CreateTimeEnricher>().Property];
        }

    }
}