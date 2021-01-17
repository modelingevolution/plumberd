namespace ModelingEvolution.Plumberd.Querying
{
    public interface ISingleResult<out TResult> { TResult Result { get; } }
}