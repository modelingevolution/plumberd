namespace ModelingEvolution.Plumberd.EventProcessing
{
    public interface IProcessingUnitConfig<TProcessingUnit> : IProcessingUnitConfig
    {

    }


    /// <summary>
    /// ProcessingUnit can return (Guid, ICommand)
    /// or can return (IEventMetadata, Event)
    /// </summary>
    /// <typeparam name="TProcessingUnit"></typeparam>
    public class ProcessingUnitConfig<TProcessingUnit> : ProcessingUnitConfig,
        IProcessingUnitConfig<TProcessingUnit>
    {
        public ProcessingUnitConfig() : base(typeof(TProcessingUnit))
        {
        }
    }


}