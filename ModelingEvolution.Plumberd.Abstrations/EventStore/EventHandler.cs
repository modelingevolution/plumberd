using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.EventStore
{
    public delegate Task EventHandler(IProcessingContext context, IMetadata m, IRecord ev);
    
}