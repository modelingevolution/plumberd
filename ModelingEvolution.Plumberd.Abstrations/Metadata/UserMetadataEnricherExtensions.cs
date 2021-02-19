using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class UserMetadataEnricherExtensions
    {
        public static Guid UserId(this IMetadata ev)
        {
            var enricher = ev.Schema.Enricher<UserIdEnricher>();
            return (Guid)ev[enricher.UserIdProperty];
        }
       
    }
}