using System;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    public interface IProcessingUnitConfig : ISubscriptionConfig
    {
        Type Type { get; }
        bool IsNameOverriden { get; }
        bool IsEventEmitEnabled { get; }
        bool IsCommandEmitEnabled { get; }
        ProcessingMode ProcessingMode { get; }
        BindingFlags BindingFlags { get; }
        TimeSpan ProcessingLag { get; set; }
        AfterDispatchHandler OnAfterDispatch { get; }
        ProjectionSchema ProjectionSchema { get; }
    }
}