namespace ModelingEvolution.Plumberd
{
    public interface ISubscriptionConfig
    {
        bool SubscribesFromBeginning { get; }
        bool IsPersistent { get; }
        string Name { get; set; }
    }
}