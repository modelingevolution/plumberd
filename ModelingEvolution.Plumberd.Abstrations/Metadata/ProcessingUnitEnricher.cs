﻿using System;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class ProcessingUnitExtensions
    {
        public static string ProcessingUnitType(this IMetadata m)
        {
            return (string)m[m.Schema.Enricher<ProcessingUnitEnricher>().Property];
        }
        public static Type TryResolveNativeProcessingUnitType(this IMetadata m)
        {
            var enricher = m.Schema.Enricher<ProcessingUnitEnricher>();
            string typeStr = (string)m[enricher.Property];
            if(enricher.Convention == TypeNamePersistenceConvention.AssemblyQualifiedName)
                return System.Type.GetType(typeStr);
            return null;
        }
    }

    public static  class DictionaryEnricherExtensions
    {
        public static bool Contains(this IMetadata m, string dynamicProperty)
        {
            var enricher = m.Schema.Enricher<DictionaryEnricher>();
            if (m[enricher.Property] is IDictionary<string, string> dict)
            {
                return dict.ContainsKey(dynamicProperty);
            }

            return false;
        }

        public static bool TryGet(this IMetadata m, string dynamicProperty, out string value)
        {
            var enricher = m.Schema.Enricher<DictionaryEnricher>();
            if (m[enricher.Property] is IDictionary<string, string> dict)
            {
                return dict.TryGetValue(dynamicProperty, out value);
            }

            value = null;
            return false;
        }
    }

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