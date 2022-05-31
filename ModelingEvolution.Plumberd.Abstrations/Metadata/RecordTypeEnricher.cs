using System;

namespace ModelingEvolution.Plumberd.Metadata
{
   
    public class RecordTypeEnricher : IMetadataEnricher
    {
        internal MetadataProperty Property;
        internal TypeNamePersistenceConvention Convention => _convention;
        private readonly TypeNamePersistenceConvention _convention;
        private readonly Func<Type, string> _converter;
        public RecordTypeEnricher(TypeNamePersistenceConvention convention)
        {
            _convention = convention;
            _converter = _convention.GetConverter();
        }

        public void RegisterSchema(IMetadataSchema register)
        {
            this.Property = register.Register<string>("Type",this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            m[Property] = _converter(e.GetType());

            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new RecordTypeEnricher(_convention);
        }
    }
}