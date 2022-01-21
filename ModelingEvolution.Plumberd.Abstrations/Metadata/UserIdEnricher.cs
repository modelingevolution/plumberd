using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class UserIdEnricher : IMetadataEnricher
    {
        public MetadataProperty UserIdProperty { get; private set; }
        public void RegisterSchema(IMetadataSchema register)
        {
            this.UserIdProperty = register.Register<Guid>("UserId", this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext c:
                    m[UserIdProperty] = c.Metadata.UserId();
                   
                    break;
                case ICommandHandlerContext c:
                    m[UserIdProperty] = c.Metadata?.UserId() ?? Guid.Empty;

                    break;
                case ICommandInvocationContext c:
                    m[UserIdProperty] = c.UserId;
                    break;
                default:
                    throw new NotSupportedException("Unsupported context.");
            }
            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new UserIdEnricher();
        }
    }
}