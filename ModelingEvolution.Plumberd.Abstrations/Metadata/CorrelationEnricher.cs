﻿using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    public class CorrelationEnricher : IMetadataEnricher
    {
        public MetadataProperty CorrelationId;
        public MetadataProperty CausationId;
        public void RegisterSchema(IMetadataSchema register)
        {
            this.CorrelationId = register.Register("$correlationId", typeof(Guid),this, true);
            this.CausationId = register.Register("$causationId", typeof(Guid), this, true);
        }

        public IMetadata Enrich(IMetadata m, IRecord e, IContext context)
        {
            switch (context)
            {
                case IEventHandlerContext epc:
                    m[CorrelationId] = epc.Metadata.CorrelationId();
                    m[CausationId] = epc.Record.Id;
                    break;
                case ICommandHandlerContext c:
                    m[CorrelationId] = c.Metadata.CorrelationId(); //  c.Record.Id
                    m[CausationId] = c.Record.Id;
                    break;
                case ICommandInvocationContext c:
                    m[CorrelationId] = c.Command.Id;
                    m[CausationId] = c.Command.Id;
                    break;
                default:
                    throw new NotSupportedException("Unsupported context.");
            }

            return m;
        }

        public IMetadataEnricher Clone()
        {
            return new CorrelationEnricher();
        }
    }
}