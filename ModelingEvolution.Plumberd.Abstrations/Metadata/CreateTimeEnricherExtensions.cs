using System;

namespace ModelingEvolution.Plumberd.Metadata;

public static class CreateTimeEnricherExtensions
{
    public static DateTimeOffset Created(this IMetadata m)
    {
        return (DateTimeOffset)m[m.Schema.Enricher<CreateTimeEnricher>().Property];
    }

}