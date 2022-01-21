namespace Checkers.Common.Controls
{
    public static class DataTemplateDictionary
    {
        private static Dictionary<Type, Type> _index = new Dictionary<Type, Type>();

        public static Type FindControlForContentType(Type contentType)
        {
            return _index.TryGetValue(contentType, out var v) ? v : null;
        }

        public static void Add(Type contentType, Type controlType)
        {
            _index.Add(contentType, controlType);
        }
        public static void Add<TDataContext, TControl>()
            where TControl : IDataContextControl<TDataContext>
        {
            Add(typeof(TDataContext), typeof(TControl));
        }
    }
}