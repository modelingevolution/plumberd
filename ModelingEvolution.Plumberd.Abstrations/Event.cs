using System;

namespace ModelingEvolution.Plumberd
{
    public abstract class Event : IEvent
    {
        public Guid Id { get; set; }

        protected Event()
        {
            Id = Guid.NewGuid();
        }
    }
    public interface IModel
    {

    }
}