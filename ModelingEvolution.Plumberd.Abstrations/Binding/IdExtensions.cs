using System;
using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("ModelingEvolution.Plumberd.Tests")]

namespace ModelingEvolution.Plumberd.Binding
{
    internal static class IdExtensions
    {
        class IdInvoker<T>
        {
            private static Func<T, Guid> _func;
            public static void Init()
            {
                var property = typeof(T).GetProperty("Id");
                if (property == null || property.ReflectedType != typeof(Guid) && !property.CanRead)
                    _func = (x) => Guid.NewGuid();
                else
                {
                    _func = (Func<T, Guid>) property.GetGetMethod(true)
                        .CreateDelegate(typeof(Func<T, Guid>));
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Guid Get(T obj)
            {
                return _func(obj);
            }
        }

        public static void InitIdAccessor<T>()
        {
            IdInvoker<T>.Init();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid Id<T>(this T obj)
        {
            return IdInvoker<T>.Get(obj);
        }
    }
}