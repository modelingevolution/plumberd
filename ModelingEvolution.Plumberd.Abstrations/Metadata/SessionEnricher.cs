using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class SessionEnricher : IMetadataEnricher
    {
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
                    m[SessionIdProperty] = c.Metadata?.SessionId() ?? Guid.Empty;
                    break;
                case ICommandInvocationContext c:
                    m[SessionIdProperty] = c.ClientSessionId;
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