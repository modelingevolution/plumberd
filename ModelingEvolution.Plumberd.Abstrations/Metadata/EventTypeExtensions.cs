using System;

namespace ModelingEvolution.Plumberd.Metadata;

public static class EventTypeExtensions
{
    public static string Type(this IMetadata m)
    {
        return (string)m[m.Schema.Enricher<RecordTypeEnricher>().Property];
    }
    public static Type TryResolveNativeType(this IMetadata m)
    {
        var enricher = m.Schema.Enricher<RecordTypeEnricher>();
        string typeStr = (string)m[enricher.Property];
        if (enricher.Convention == TypeNamePersistenceConvention.AssemblyQualifiedName)
            return System.Type.GetType(typeStr);
        return null;
    }
}