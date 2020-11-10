using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.CommandHandling
{
    public interface ICommandHandler
    {
        Task Execute(Guid id, ICommand c);
    }

    public static class HandlerTypeExtensions
    {
        public static IEnumerable<Type> IsAssignableTo<T>(this IEnumerable<Type> types)
        {
            return types.Where(x => typeof(T).IsAssignableFrom(x));
        }
        public static IEnumerable<Type> Instanceable(this IEnumerable<Type> types)
        {
            return types.Where(x => x.IsClass && !x.IsAbstract);
        }
        public static IEnumerable<ProcessingUnitType> WithHandlerAttribute(this IEnumerable<Type> types)
        {
            return types.Select(x =>
            {
                var attrs = x.GetCustomAttributes<ProcessingUnitConfigAttribute>().ToArray();
                return new ProcessingUnitType() { Attributes = attrs, Type = x};
            }).Where(x=>x.Attributes.Length > 0);
        }
        public static IEnumerable<HandlerType> WithCommandHandlerInterface(this IEnumerable<Type> types)
        {
            Type commandHandlerDef = typeof(ICommandHandler<>);
            return types.Where(x => typeof(ICommandHandler).IsAssignableFrom(x)).Select(x =>
            {
                var interfaceType = x.GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == commandHandlerDef);

                return new { Inteface = interfaceType, Type = x };
            }).Where(x => x.Inteface != null)
                .Select(x =>
                {
                    var t = x.Inteface.GetGenericArguments()[0];

                    return new HandlerType
                    {
                        Type = x.Type,
                        RecordType = t,
                        InterfaceType = x.Inteface
                    };
                });
        }
    }

    public class ProcessingUnitType
    {
        public Type Type { get; set; }
        public ProcessingUnitConfigAttribute[] Attributes {
            get;
            set;
        }
    }
    public class HandlerType {
        public Type RecordType { get; set; }
        public Type InterfaceType { get; set; }
        public Type Type { get; set; }
    }
}