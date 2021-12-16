namespace Checkers.Common.Reactive
{
    public class JsInteropTypeRegister : IJsInteropTypeRegister
    {
        private readonly Dictionary<string, Type> _indexByName;
        private readonly Dictionary<Type, string> _indexByType;
        public JsInteropTypeRegister()
        {
            _indexByName = new Dictionary<string, Type>();
            _indexByType = new Dictionary<Type, string>();
        }
        public void RegisterTypes(IEnumerable<Type> types, Func<Type, string> func = null)
        {
            func ??= x => x.Name;
            foreach (var i in types)
            {
                var name = func(i);
                _indexByName.Add(name, i);
                _indexByType.Add(i,name);
            }
        }

        public bool TryGetTypeByName(string name, out Type type)
        {
            return _indexByName.TryGetValue(name, out type);
        }

        public bool TryGetNameByType(Type eventType, out string name)
        {
            return _indexByType.TryGetValue(eventType, out name);
        }
    }
}