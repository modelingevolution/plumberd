using System;

namespace ModelingEvolution.Plumberd
{
    public abstract class Command : ICommand
    {
        public Guid Id { get; set; }
        public DateTimeOffset Created { get; set; }
        protected Command()
        {
            Id = Guid.NewGuid();
            Created = DateTimeOffset.Now;
        }
    }
}