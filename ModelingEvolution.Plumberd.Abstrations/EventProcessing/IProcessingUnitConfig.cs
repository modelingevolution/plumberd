using System;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    public interface ILiveProjection
    {
        public bool IsLive { get; set; }
    }
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
        Action OnLive { get; set; }
        ProjectionSchema ProjectionSchema { get; }
        
    }
}