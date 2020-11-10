using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    public delegate Task<ProcessingResults> HandlerDispatcher(object processingUnit, IMetadata m, IRecord ev);
}