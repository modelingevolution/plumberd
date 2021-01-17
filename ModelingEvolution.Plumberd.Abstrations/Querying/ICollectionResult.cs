using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface ICollectionResult<TResult>
    {
        IList<TResult> Items { get; }
        event Func<Task> Changed;
    }
}