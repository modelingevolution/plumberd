namespace Checkers.Common.Reactive
{
    public interface IJsInteropTypeRegister
    {
        void RegisterTypes(IEnumerable<Type> types, Func<Type, string> func = null);
        bool TryGetTypeByName(string name, out Type type);
        bool TryGetNameByType(Type eventType, out string name);

    }
}