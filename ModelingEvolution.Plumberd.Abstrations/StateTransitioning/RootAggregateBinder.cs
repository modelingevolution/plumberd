using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public class RootAggregateBinder<TState>
    {
        private const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod;
        private readonly Type _type;
        private readonly Dictionary<Type, StateTransition<TState>.Given> _givens;
        private readonly Dictionary<Type, StateTransition<TState>.When> _whens;

        public StateTransition<TState>.Given Given
        {
            get => (st, ev) => _givens.TryGetValue(ev.GetType(), out var given) ? given(st, ev) : st;
        }

        public StateTransition<TState>.When When
        {
            get { return (st, cmd) => _whens[cmd.GetType()](st, cmd); }
        }

        public RootAggregateBinder(Type type)
        {
            _type = type;
            _givens = new Dictionary<Type, StateTransition<TState>.Given>();
            _whens = new Dictionary<Type, StateTransition<TState>.When>();
        }

        public RootAggregateBinder<TState> Discover()
        {
            var methods = _type.GetMethods(FLAGS);
            foreach (var i in methods.Where(x => (x.Name == "When" || x.Name == "Given")))
            {
                if (i.Name == "When")
                {
                    var args = i.GetParameters();
                    if (args.Length == 2 &&
                        args[0].ParameterType == typeof(TState) &&
                        typeof(ICommand).IsAssignableFrom(args[1].ParameterType) &&
                        args[1].ParameterType.IsClass)
                    {
                        var func = BindWhen(args[1].ParameterType, i);
                        _whens.Add(args[1].ParameterType, func);
                    }
                }
                else if (i.Name == "Given")
                {
                    var args = i.GetParameters();
                    if (args.Length == 2 &&
                        args[0].ParameterType == typeof(TState) &&
                        typeof(IEvent).IsAssignableFrom(args[1].ParameterType) &&
                        args[1].ParameterType.IsClass)
                    {
                        var func = BindGiven(args[1].ParameterType, i);
                        _givens.Add(args[1].ParameterType, func);
                    }
                }
            }
            return this;
        }
        private StateTransition<TState>.Given BindGiven(Type eventType, MethodInfo methodInfo)
        {
            var stateParam = Expression.Parameter(typeof(TState), "state");
            var eventParam = Expression.Parameter(typeof(IEvent), "event");

            var callExpression = Expression.Call(methodInfo, stateParam, Expression.Convert(eventParam, eventType));
            var lambda = Expression.Lambda<StateTransition<TState>.Given>(callExpression, stateParam, eventParam);
            return lambda.Compile();
        }

        private StateTransition<TState>.When BindWhen(Type commandType, MethodInfo methodInfo)
        {
            var stateParam = Expression.Parameter(typeof(TState), "state");
            var commandParam = Expression.Parameter(typeof(ICommand), "command");

            var retParamType = methodInfo.ReturnParameter.ParameterType;

            if (typeof(Event).IsAssignableFrom(retParamType) && retParamType.IsClass)
            {
                var methodExpression =
                    Expression.Call(methodInfo, stateParam, Expression.Convert(commandParam, commandType));
                var callExpression = Expression.Convert(methodExpression, typeof(IEvent));
                var lambda =
                    Expression.Lambda<Func<TState, ICommand, IEvent>>(callExpression, stateParam,
                        commandParam);
                var func = lambda.Compile();
                return (st, cmd) => new IEvent[1] { func(st, cmd) };
            }

            if (retParamType == typeof(IEvent[]))
            {
                var callExpression =
                    Expression.Call(methodInfo, stateParam, Expression.Convert(commandParam, commandType));
                var lambda =
                    Expression.Lambda<StateTransition<TState>.When>(callExpression, stateParam,
                        commandParam);
                return lambda.Compile();
            }

            if (retParamType == typeof(IEnumerable<IEvent>))
            {

                var callExpression =
                    Expression.Call(methodInfo, stateParam, Expression.Convert(commandParam, commandType));

                var lambda =
                    Expression.Lambda<Func<TState, ICommand, IEnumerable<IEvent>>>(callExpression, stateParam,
                        commandParam);
                var func = lambda.Compile();
                return (st, cmd) => func(st, cmd).ToArray();
            }

            throw new NotSupportedException();
        }


    }
}