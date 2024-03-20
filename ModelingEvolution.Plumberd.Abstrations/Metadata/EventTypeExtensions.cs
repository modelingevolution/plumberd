using System;
using System.Collections.Concurrent;

namespace ModelingEvolution.Plumberd.Metadata;
public static class EventTypeResolver {
    private static ConcurrentDictionary<string, Type> _index = new ();
    public static Func<IMetadata, Type> CustomResolver;
    public static Type Resolve(IMetadata m)
    {
        if (m.Schema == null) return null;

        var enricher = m.Schema.Enricher<RecordTypeEnricher>();
        string typeStr = (string)m[enricher.Property];
        if (string.IsNullOrWhiteSpace(typeStr))
            throw new InvalidOperationException("Cannot find event-type in metadata.");

        return enricher.Convention switch
        {
            TypeNamePersistenceConvention.AssemblyQualifiedName => _index.GetOrAdd(typeStr, typeName =>
            {
                var result = System.Type.GetType(typeName);
                if (result != null) return result;

                string[] segments = typeName.Split(',');
                if (segments.Length <= 1) throw new InvalidOperationException($"Cannot find type '{typeName}'.");

                typeName = $"{segments[0]}, {segments[1]}";
                result = System.Type.GetType(typeName);
                if (result == null) throw new InvalidOperationException($"Cannot find type '{typeName}'.");

                return result;
            }),
            TypeNamePersistenceConvention.Custom => CustomResolver(m),
            _ => throw new NotSupportedException()
        };
    }
}
public static class EventTypeExtensions
{
    
    public static string Type(this IMetadata m)
    {
        return (string)m[m.Schema.Enricher<RecordTypeEnricher>().Property];
    }
    public static System.Type TryResolveNativeType(this IMetadata m, System.Type? fallback = null)
    {
        System.Type type = EventTypeResolver.Resolve(m);
        return (object)type != null ? type : fallback;
    }
}