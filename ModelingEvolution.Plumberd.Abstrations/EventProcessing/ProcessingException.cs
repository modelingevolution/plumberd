using System;
using System.Runtime.Serialization;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    [Serializable]
    public abstract class ProcessingException : Exception
    {
        protected readonly object _payload;
        public object Payload => _payload;
        protected ProcessingException(object payload)
        {
            _payload = payload;
        }
    }

    [Serializable]
    public class ProcessingException<TPayload> : ProcessingException
    {
        public new TPayload Payload => (TPayload) _payload;

        public ProcessingException(TPayload payload) : base(payload)
        {

        }
    }
}