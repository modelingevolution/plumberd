using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.CommandHandling;

namespace ModelingEvolution.Plumberd
{
    public interface ICommandHandler<in TCommand> : ICommandHandler where TCommand : ICommand
    {
        Task Execute(Guid id, TCommand c);
    }
}
