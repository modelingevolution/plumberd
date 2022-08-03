namespace ModelingEvolution.Plumberd.Threading;

internal readonly struct EmptyStruct
{
    /// <summary>
    /// Gets an instance of the empty struct.
    /// </summary>
    internal static EmptyStruct Instance
    {
        get { return default(EmptyStruct); }
    }
}