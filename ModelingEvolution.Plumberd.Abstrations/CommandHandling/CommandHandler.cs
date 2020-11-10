using System;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.CommandHandling
{
    public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        public abstract Task Execute(Guid id, TCommand c);
        public async Task Execute(Guid id, ICommand c)
        {
            await Execute(id, (TCommand)c);
        }
    }
}