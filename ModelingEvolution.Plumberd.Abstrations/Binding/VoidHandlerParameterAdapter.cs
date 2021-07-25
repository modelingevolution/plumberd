using System;
using System.Linq.Expressions;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Binding
{
    public abstract class HandlerParameterAdapter
    {
        public abstract void Compile(Expression call, ParameterExpression[] parameters);

    }
    public abstract class VoidHandlerParameterAdapter<TArg0, TArg1, TArg2> : HandlerParameterAdapter
    {
        protected Action<TArg0, TArg1, TArg2> Func;
        public override void Compile(Expression call, ParameterExpression[] parameters)
        {
            Func = Expression.Lambda<Action<TArg0, TArg1, TArg2>>(call, parameters).Compile();
        }
        public abstract void Invoke(object unit, IMetadata m, IRecord r);

    }
    public abstract class VoidHandlerParameterAdapter<TArg0, TArg1, TArg2, TArg3> : HandlerParameterAdapter
    {
        protected Action<TArg0, TArg1, TArg2,TArg3> Func;
        public override void Compile(Expression call, ParameterExpression[] parameters)
        {
            Func = Expression.Lambda<Action<TArg0, TArg1, TArg2,TArg3>>(call, parameters).Compile();
        }
        public abstract void Invoke(object unit, IMetadata m, IRecord r);

    }
    public abstract class HandlerParameterAdapter<TArg0, TArg1, TArg2, TResult> : HandlerParameterAdapter
    {
        protected Func<TArg0, TArg1, TArg2, TResult> Func;
        public override void Compile(Expression call, ParameterExpression[] parameters)
        {
            Func = Expression.Lambda<Func<TArg0, TArg1, TArg2, TResult>>(call, parameters).Compile();
        }
        public abstract TResult Convert(object unit, IMetadata m, IRecord r);

    }
    public abstract class HandlerParameterAdapter<TArg0, TArg1, TArg2, TArg3, TResult> : HandlerParameterAdapter
    {
        protected Func<TArg0, TArg1, TArg2, TArg3,TResult> Func;
        public override void Compile(Expression call, ParameterExpression[] parameters)
        {
            Func = Expression.Lambda<Func<TArg0, TArg1, TArg2, TArg3, TResult>>(call, parameters).Compile();
        }
        public abstract TResult Convert(object unit, IMetadata m, IRecord r);

    }
}