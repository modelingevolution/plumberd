using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public static class SessionEnricherExtensions {
        public static Guid SessionId(this IMetadata ev)
        {
            return (Guid)ev[ev.Schema.Enricher<SessionEnricher>().SessionIdProperty];
        }
    }
    public class SessionEnricher : IMetadataEnricher
    {
        private readonly Guid _sessionId;
        private const string SESSION_ID_KEY = "SessionId";
        public SessionEnricher()
        {
            var sessionId = AppDomain.CurrentDomain.GetData(SESSION_ID_KEY);
            if (sessionId == null)
            {
                sessionId = this._sessionId = Guid.NewGuid();
                AppDomain.CurrentDomain.SetData(SESSION_ID_KEY, sessionId);
            }
            else
                _sessionId = (Guid) sessionId;
        }
        public MetadataProperty SessionIdProperty { get; set; }
        public void RegisterSchema(IMetadataSchema register)
        {
            this.SessionIdProperty = register.Register<Guid>("SessionId", this, true);
        }

        

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext epc:
                    m[SessionIdProperty] = epc.Metadata.SessionId();
                    break;
                case ICommandHandlerContext c:
                    m[SessionIdProperty] = c.Metadata.SessionId();
                    break;
                case ICommandInvocationContext c:
                    m[SessionIdProperty] = _sessionId;
                    break;
                default:
                    throw new NotSupportedException("Unsupported context.");
            }

            
            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new SessionEnricher();
        }
    }
}