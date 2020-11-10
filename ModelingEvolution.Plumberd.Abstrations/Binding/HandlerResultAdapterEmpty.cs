using System;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Binding
{
    public class HandlerResultAdapterEmpty : HandlerResultAdapter
    {
        public readonly  ProcessingResults Empty = new ProcessingResults();
        public override Type ResultType { get => typeof(void); }
    }
}