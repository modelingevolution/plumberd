using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Metadata;

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