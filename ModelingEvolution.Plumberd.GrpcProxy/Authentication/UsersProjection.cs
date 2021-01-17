using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.GrpcProxy.Authentication
{
    [ProcessingUnitConfig(IsEventEmitEnabled = false, 
        IsCommandEmitEnabled = false, 
        IsPersistent = false, 
        SubscribesFromBeginning = true, 
        ProcessingMode = ProcessingMode.EventHandler)]
    public class UsersProjection
    {
        private readonly UsersModel _model;
        public void Given(IMetadata m, AuthorizationDataRetrieved ev)
        {
            _model.Given(m, ev);
        }

        public UsersProjection(UsersModel model)
        {
            _model = model;
        }
    }
}