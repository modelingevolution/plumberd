using System;
using System.Runtime.Serialization;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    [Serializable]
    public abstract class ProcessingException : Exception
    {
        protected readonly IErrorEvent _payload;
        public IErrorEvent Payload => _payload;
        protected ProcessingException(IErrorEvent payload)
        {
            _payload = payload;
        }
    }

    [Serializable]
    public class ProcessingException<TPayload> : ProcessingException
    where TPayload: IErrorEvent
    {
        public new TPayload Payload => (TPayload) _payload;

        public ProcessingException(TPayload payload) : base(payload)
        {

        }
    }
}