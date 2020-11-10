namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public static class StateTransition<TState>
    {
        public delegate TState Given(TState st, IEvent ev);

        public delegate IEvent[] When(TState st, ICommand cmd);
    }
}