using System;
using System.Runtime.Serialization;

namespace ModelingEvolution.Plumberd
{
    public class ControllerRegistrationFailed : Exception
    {
        public ControllerRegistrationFailed()
        {
        }

        protected ControllerRegistrationFailed(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public ControllerRegistrationFailed(string? message) : base(message)
        {
        }

        public ControllerRegistrationFailed(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}