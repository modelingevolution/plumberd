using System;

namespace ModelingEvolution.Plumberd
{
    public interface IEvent : IRecord
    {
        
    }

    public interface IRecord
    {
        public Guid Id { get; }
    }
}