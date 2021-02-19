using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class CorrelationEnricherExtensions
    {
        public static Guid CorrelationId(this IMetadata ev)
        {
            return (Guid)ev[ev.Schema.Enricher<CorrelationEnricher>().CorrelationId];
        }
        public static Guid CausationId(this IMetadata ev)
        {
            return (Guid)ev[ev.Schema.Enricher<CorrelationEnricher>().CausationId];
        }
    }
}