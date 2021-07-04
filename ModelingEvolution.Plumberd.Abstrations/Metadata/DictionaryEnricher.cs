using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class DictionaryEnricher : IMetadataEnricher
    {
        internal MetadataProperty Property;
        public void RegisterSchema(IMetadataSchema register)
        {
            this.Property = register.Register<string>("ProcessingUnit",this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            // should merge dictionaries here...
            return m;
        }

        public IMetadataEnricher Clone()
        {
            return this;
        }
    }
}