using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Binding
{
    public abstract class HandlerResultAdapter
    {
        public abstract Type ResultType { get; }
    }
    public class HandlerResultAdapter<TResult> : HandlerResultAdapter
    {
        public readonly Func<IMetadata, TResult, Task<ProcessingResults>> Returns;

        public HandlerResultAdapter(Func<IMetadata, TResult, Task<ProcessingResults>> returns)
        {
            Returns = returns;
        }

        public override Type ResultType
        {
            get => typeof(TResult);
        }
    }
}