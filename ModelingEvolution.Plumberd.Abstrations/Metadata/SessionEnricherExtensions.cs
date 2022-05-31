using System;

namespace ModelingEvolution.Plumberd.Metadata;

public static class SessionEnricherExtensions {
    public static Guid SessionId(this IMetadata ev)
    {
        return (Guid)ev[ev.Schema.Enricher<SessionEnricher>().SessionIdProperty];
    }
}