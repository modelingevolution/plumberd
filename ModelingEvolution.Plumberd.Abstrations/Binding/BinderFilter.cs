using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Binding
{
    internal static class BindingExtensions
    {
        public static bool ReturnsCommands(this MethodInfo i)
        {
            if (i.ReturnType == typeof(IEnumerable<(Guid, ICommand)>) ||
                i.ReturnType == typeof(IAsyncEnumerable<(Guid, ICommand)>) ||
                i.ReturnType == typeof(Task<(Guid, ICommand)[]>))
            {
                return true;
            }
            if (i.ReturnType.IsGenericType)
            {
                var def = i.ReturnType.GetGenericTypeDefinition();
                if (def == typeof(Task<>))
                {
                    var tuple = i.ReturnType.GetGenericArguments()[0];
                    if (tuple.IsGenericType)
                    {
                        var tupleDef = tuple.GetGenericTypeDefinition();
                        if (tupleDef == typeof(ValueTuple<,>))
                        {
                            var tupleArgs = tuple.GetGenericArguments();
                            if (tupleArgs[0] == typeof(Guid) &&
                                     typeof(ICommand).IsAssignableFrom(tupleArgs[1]))
                            {
                                return true;
                            }
                        }
                    }
                } 
                else if (def == typeof(ValueTuple<,>))
                {
                    var tupleDef = i.ReturnType.GetGenericTypeDefinition();
                    if (tupleDef == typeof(ValueTuple<,>))
                    {
                        var tupleArgs = i.ReturnType.GetGenericArguments();
                        if (tupleArgs[0] == typeof(Guid) &&
                            typeof(ICommand).IsAssignableFrom(tupleArgs[1]))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool ReturnsNothing(this MethodInfo i)
        {
            return i.ReturnType == typeof(Task) || 
                   i.ReturnType == typeof(void);
        }
        public static bool ReturnsEvents(this MethodInfo i)
        {
            if (i.ReturnType == typeof(IEnumerable<IEvent>) || // done
                i.ReturnType == typeof(IEnumerable<(Guid, IEvent)>) || // done
                i.ReturnType == typeof(IAsyncEnumerable<IEvent>) ||
                i.ReturnType == typeof(IAsyncEnumerable<(Guid, IEvent)>) ||
                i.ReturnType == typeof(Task<IEvent[]>) || // done
                i.ReturnType == typeof(Task<(Guid, IEvent)[]>) || // done
                i.ReturnType == typeof(IEvent[]) || //done
                i.ReturnType == typeof((Guid, IEvent)[])) //done
                return true;
            else
            {
                if (typeof(IEvent).IsAssignableFrom(i.ReturnType) && !i.ReturnType.IsInterface)
                    return true;
                else if (i.ReturnType.IsGenericType)
                {
                    var def = i.ReturnType.GetGenericTypeDefinition();
                    if (def == typeof(Task<>))
                    {
                        var taskArgType = i.ReturnType.GetGenericArguments()[0];

                        if (typeof(IEvent).IsAssignableFrom(taskArgType))
                            return true;
                        else if (taskArgType.IsGenericType)
                        {
                            var tupleDef = taskArgType.GetGenericTypeDefinition();
                            if (tupleDef == typeof(ValueTuple<,>))
                            {
                                var tupleArgs = taskArgType.GetGenericArguments();
                                if (tupleArgs[0] == typeof(Guid) &&
                                    typeof(IEvent).IsAssignableFrom(tupleArgs[1]))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    else if (def == typeof(ValueTuple<,>))
                    {
                        var tupleArgs = i.ReturnType.GetGenericArguments();
                        if (tupleArgs[0] == typeof(Guid) &&
                            typeof(IEvent).IsAssignableFrom(tupleArgs[1]))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        public static bool HasEventHandlerParameters(this MethodInfo i)
        {
            var args = i.GetParameters();
            return args.Length == 2 &&
                   !args[1].ParameterType.IsInterface &&
                   args[0].ParameterType == typeof(IMetadata) &&
                   typeof(IEvent).IsAssignableFrom(args[1].ParameterType);
        }
        public static bool HasCommandHandlerParameters(this MethodInfo i)
        {
            var args = i.GetParameters();
            return (args.Length == 2 &&
                    !args[1].ParameterType.IsInterface &&
                    args[0].ParameterType == typeof(Guid) &&
                    typeof(ICommand).IsAssignableFrom(args[1].ParameterType))
                   ||
                   (args.Length == 3 &&
                    !args[1].ParameterType.IsInterface &&
                    args[0].ParameterType == typeof(Guid) &&
                    typeof(ICommand).IsAssignableFrom(args[1].ParameterType) &&
                    args[2].ParameterType == typeof(IMetadata));
        }
    }
    readonly struct BinderFilter
    {
        private readonly BindingFlags _value;

        public BinderFilter(BindingFlags value)
        {
            _value = value;
        }

        public bool Filter(MethodInfo method)
        {
            bool? returnResults = null;
            bool? processResults = null;
            if (_value.HasFlag(BindingFlags.ProcessCommands))
            {
                var v = method.HasCommandHandlerParameters();
                processResults = v;
            }
            if (_value.HasFlag(BindingFlags.ProcessEvents))
            {
                var v = method.HasEventHandlerParameters();
                if (processResults.HasValue)
                    processResults = v || processResults.Value;
                else
                    processResults = v;
            }

            if (_value.HasFlag(BindingFlags.ReturnEvents))
            {
                var v = method.ReturnsEvents();
                returnResults = v;
            }
            if (_value.HasFlag(BindingFlags.ReturnCommands))
            {
                var v = method.ReturnsCommands();
                if (returnResults.HasValue)
                    returnResults = v || returnResults.Value;
                else
                    returnResults = v;
            }
            if (_value.HasFlag(BindingFlags.ReturnNothing))
            {
                var v = method.ReturnsNothing();
                if (returnResults.HasValue)
                    returnResults = v || returnResults.Value;
                else
                    returnResults = v;
            }

            return (returnResults ?? false) && (processResults ?? false);
        }
    }
}