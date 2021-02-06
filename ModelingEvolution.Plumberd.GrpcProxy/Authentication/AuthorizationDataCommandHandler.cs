using System;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.GrpcProxy.Authentication
{
    [ProcessingUnitConfig(IsEventEmitEnabled = true,
        IsCommandEmitEnabled = false,
        IsPersistent = true,
        SubscribesFromBeginning = false,
        ProcessingMode = ProcessingMode.CommandHandler)]
    public class AuthorizationDataCommandHandler
    {
        public AuthorizationDataRetrieved When(Guid id, RetrieveAuthorizationData cmd)
        {
            return new AuthorizationDataRetrieved()
            {
                Name =  cmd.Name,
                Email = cmd.Email
            };
        }
    }
}