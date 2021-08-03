namespace ModelingEvolution.Plumberd.EventStore
{
    public enum ApiScope
    {
        /// <summary>
        /// Means that the command/event is used in public-api
        /// </summary>
        Public = 3,
        /// <summary>
        /// Means that the command/event is used internally, can be on client and server.
        /// </summary>
        Internal = 2,
        
        /// <summary>
        /// Means that the command/event should be used only on server, within module. 
        /// </summary>
        Private = 1
    }
}