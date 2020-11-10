using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Binding
{
    public interface IInvocationAdapter
    {
        HandlerDispatcher Build(HandlerParameterAdapter parameterAdapter, HandlerResultAdapter resultAdapter);
    }
}