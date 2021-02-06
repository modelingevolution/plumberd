namespace ModelingEvolution.Plumberd.Querying
{
    public interface IViewFor<out T>
    {
        T Source { get; }
        
    }
}