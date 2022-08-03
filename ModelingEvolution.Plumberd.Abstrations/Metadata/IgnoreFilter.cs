using System;
using System.Collections;
using System.Collections.Generic;
using ModelingEvolution.Plumberd.Collections;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Metadata;

[ProcessingUnitConfig(IsEventEmitEnabled = false,
    SubscribesFromBeginning = true,
    IsPersistent = false,
    ProcessingMode = ProcessingMode.EventHandler)]
class IgnoreFilter : IIgnoreFilterModel
{
    public ConcurrentHashSet<Guid> _index;

    public IgnoreFilter()
    {
        _index = new ConcurrentHashSet<Guid>();
    }

    public bool IsFiltered(Guid correlationId)
    {
        return _index.Contains(correlationId);
    }
    public void Given(IMetadata m, ByCorrelationIdIgnored ev)
    {
        _index.Add(ev.CorrelationId);
    }
}

[ProcessingUnitConfig(IsEventEmitEnabled = true,
    SubscribesFromBeginning = false,
    IsPersistent = false,
    ProcessingMode = ProcessingMode.CommandHandler)]
class IgnoreFilterCommandHandler
{
    public ByCorrelationIdIgnored When(Guid id, IgnoreByCorrelationId cmd)
    {
        if(cmd.CorrelationId != Guid.Empty)
            return new ByCorrelationIdIgnored()
            {
                CorrelationId = cmd.CorrelationId
            };
        return null;
    }
}