using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.Plumberd
{
    public struct ImplementedInterface
    {
        public Type ArgumentType { get; init; }
        public Type ConcreteInterface { get; init; }
        public Type ImplementationType { get; init; }
        public bool IsImplemented => ArgumentType != null;

    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSingletons(this IServiceCollection services, IEnumerable<Type> types)
        {
            foreach (var s in types) services.AddSingleton(s);
            return services;
        }
        public static IServiceCollection AddScopedServices(this IServiceCollection services, IEnumerable<Type> types)
        {
            foreach (var s in types) services.AddScoped(s);
            return services;
        }
        public static IServiceCollection AddScopedServices(this IServiceCollection services, Type openGeneric, IEnumerable<Type> types)
        {
            foreach (var concreteType in types.Where(x=>x.IsClass && !x.IsAbstract))
            {
                var implementedInterfaces = concreteType.GetInterfaces()
                    .Where(j => j.IsGenericType && !j.IsGenericTypeDefinition &&
                    j.GetGenericTypeDefinition() == openGeneric).ToArray();
                if(implementedInterfaces.Any())
                    foreach(var service in implementedInterfaces)
                        services.AddScoped(service, concreteType);
            }
            return services;
        }
    }
    public static class TypeExtensions
    {
      
        public static IEnumerable<Type> HavingAttribute<T>(this IEnumerable<Type> items)
        where T:Attribute
        {
            return items.Where(x => x.GetCustomAttribute<T>() != null);
        }
        public static IEnumerable<Type> HavingAttribute<T>(this IEnumerable<Type> items, Predicate<T> filter)
            where T:Attribute
        {
            return items.Select(x => new { Attr = typeof(T).GetCustomAttribute<T>(), Type =x})
                .Where(x=>x.Attr != null && filter(x.Attr))
                .Select(x=>x.Type);
        }
        public static IEnumerable<Type> IsAssignableToClass<T>(this IEnumerable<Type> items)
        {
            return items.Where(x => typeof(T).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract);
        }
        public static ImplementedInterface GetGenericInterfaceArgument(this Type type, Type genericType)
        {
            var implementedInterface = type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == genericType);
            if (implementedInterface != null)
            {
                var argType = implementedInterface.GetGenericArguments()[0];
                return new ImplementedInterface()
                {
                    ArgumentType = argType,
                    ConcreteInterface = genericType.MakeGenericType(argType),
                    ImplementationType = type
                };
            }

            return new ImplementedInterface() { ImplementationType = type };
        }
        public static byte[] ToHash(this string t)
        {
            using (SHA256 h = SHA256.Create())
            {
                var hash = h.ComputeHash(Encoding.Default.GetBytes(t));

                ulong n1 = BitConverter.ToUInt64(hash, 0);
                ulong n2 = BitConverter.ToUInt64(hash, 8);
                ulong n3 = BitConverter.ToUInt64(hash, 16);
                ulong n4 = BitConverter.ToUInt64(hash, 24);

                n1 ^= n3;
                n2 ^= n4;

                Memory<byte> m = new Memory<byte>(new byte[16]);
                BitConverter.TryWriteBytes(m.Span, n1);
                BitConverter.TryWriteBytes(m.Slice(8, 8).Span, n2);
                return m.ToArray();
            }
        }

        public static Guid ToGuid(this string t)
        {
            return new Guid(t.ToHash());
        }
        private static ConcurrentDictionary<Type, byte[]> _hashCache = new ConcurrentDictionary<Type, byte[]>();
        public static byte[] NameHash(this Type t)
        {
            return _hashCache.GetOrAdd(t, t =>
            {
                return t.FullName.ToHash();
            });
        }
        public static Guid NameId(this Type t)
        {
            return new Guid(NameHash(t));
        }
    }
}