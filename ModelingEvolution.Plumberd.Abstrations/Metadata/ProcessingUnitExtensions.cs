using System;

namespace ModelingEvolution.Plumberd.Metadata;

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