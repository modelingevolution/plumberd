using System;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Binding
{
    public class CommandHandlerVoidParameterAdapter<TUnit, TCommand> : VoidHandlerParameterAdapter<TUnit, Guid, TCommand>
        where TCommand : ICommand
    {
        public override void Invoke(object unit, IMetadata m, IRecord r)
        {
            Func((TUnit)unit, m.StreamId(), (TCommand)r);
        }
    }
    public class CommandHandlerParameterAdapter<TUnit, TResult, TCommand> : HandlerParameterAdapter<TUnit, Guid, TCommand, TResult>
        where TCommand : ICommand
    {
        public override TResult Convert(object unit, IMetadata m, IRecord r)
        {
            return Func((TUnit)unit, m.StreamId(), (TCommand)r);
        }
    }
    public class CommandHandlerVoidParameterAdapterExt<TUnit, TCommand> : VoidHandlerParameterAdapter<TUnit, Guid, TCommand, IMetadata>
        where TCommand : ICommand
    {
        public override void Invoke(object unit, IMetadata m, IRecord r)
        {
            Func((TUnit)unit, m.StreamId(), (TCommand)r, m);
        }
    }
    public class CommandHandlerParameterAdapterExt<TUnit, TResult, TCommand> : HandlerParameterAdapter<TUnit, Guid, TCommand, IMetadata, TResult>
        where TCommand : ICommand
    {
        public override TResult Convert(object unit, IMetadata m, IRecord r)
        {
            return Func((TUnit)unit, m.StreamId(), (TCommand)r, m);
        }
    }
}