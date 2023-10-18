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
        public IEnumerable<IEvent> When(Guid id, FaultyCommand cmd)
        {
            throw new Exception();
        }
    }

    public class FaultyCommand : ICommand
    {
        public Guid Id { get; set; }

        public FaultyCommand()
        {
            Id = Guid.NewGuid();
        }
    }
    [ProcessingUnitConfig(
        IsCommandEmitEnabled = false,
        IsEventEmitEnabled = true,
        IsPersistent = true,
        SubscribesFromBeginning = false)]
    public class FooLinkProjection
    {
        public Guid StreamId = Guid.NewGuid();
        public IMetadata Metadata;
        public FooEvent Event;

        public int Count = 0;

        public async Task<(Guid, IEvent)> Given(IMetadata m, FooEvent ev)
        {
            Count += 1;
            Metadata = m;
            Event = ev;
            return (StreamId, m.Link("/FooLink"));
        }
    }
    [ProcessingUnitConfig(IsEventEmitEnabled = false,
        IsPersistent = false,
        SubscribesFromBeginning = true)]
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