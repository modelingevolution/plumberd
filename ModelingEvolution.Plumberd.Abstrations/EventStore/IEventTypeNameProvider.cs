using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    public interface IEventTypeNameProvider
    {
        public string GetName(Type recordType);
    }
}