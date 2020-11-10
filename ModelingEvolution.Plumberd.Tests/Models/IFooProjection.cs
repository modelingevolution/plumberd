using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Tests.Models
{
    [ProcessingUnitConfig(IsEventEmitEnabled = true, 
        IsPersistent = true, 
        SubscribesFromBeginning = false)]
    public class FooCommandHandler
    {
        public int Count = 0;
        public FooCommand Command;
        public FooEvent ReturningEvent;
        public IEnumerable<IEvent> When(Guid id, FooCommand cmd)
        {
            Command = cmd;
            Count += 1;
            yield return ReturningEvent ??= new FooEvent();
        }
        
    }

    public class FooProjection 
    {
        public IMetadata Metadata;
        public FooEvent Event;

        public int Count = 0;

        public async Task Given(IMetadata m, FooEvent ev)
        {
            Count += 1;
            Metadata=m;
            Event = ev;
        }
    }
}