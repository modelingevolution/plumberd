using System;
using ModelingEvolution.Plumberd.Binding;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ProcessingUnitConfigAttribute : Attribute
    {
        public bool SubscribesFromBeginning { get; set; }
        public bool IsPersistent { get; set; }
        public bool IsEventEmitEnabled { get; set; }
        public bool IsCommandEmitEnabled { get; set; }
        public string StreamName { get; set; }
        public ProcessingMode ProcessingMode { get; set; }
        public BindingFlags BindingFlags { get; set; }
        public TimeSpan ProcessingLag { get; set; }

        public ProcessingUnitConfigAttribute()
        {
            ProcessingLag = TimeSpan.Zero;
            ProcessingMode = ProcessingMode.Both;
            BindingFlags = BindingFlags.ProcessCommands |
                                     BindingFlags.ProcessEvents |
                                     BindingFlags.ReturnCommands |
                                     BindingFlags.ReturnEvents |
                                     BindingFlags.ReturnNothing;
        }
    }
}