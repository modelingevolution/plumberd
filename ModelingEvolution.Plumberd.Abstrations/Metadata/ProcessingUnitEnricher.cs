using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class ProcessingUnitEnricher : IMetadataEnricher
    {
        internal MetadataProperty Property;
        internal TypeNamePersistenceConvention Convention => _convention;
        private readonly TypeNamePersistenceConvention _convention;
        private readonly Func<Type, string> _converter;
        public ProcessingUnitEnricher(TypeNamePersistenceConvention convention)
        {
            _convention = convention;
            _converter = _convention.GetConverter();
        }

        public void RegisterSchema(IMetadataSchema register)
        {
            this.Property = register.Register<string>("ProcessingUnit",this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            if(context is IProcessingContext cp)
                m[Property] = _converter(cp.ProcessingUnit.GetType());
           
            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new ProcessingUnitEnricher(_convention);
        }
    }
}