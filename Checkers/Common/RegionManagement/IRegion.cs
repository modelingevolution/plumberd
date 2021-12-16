namespace Checkers.Common.RegionManagement
{
    public interface IRegion
    {
        ControlToken Add(Type controlType, params ControlParameter[] args);

        ControlToken Add(Type controlType, object dataContext) =>
            Add(controlType, new ControlParameter("DataContext", dataContext));

        ControlToken Add<TControl>(params ControlParameter[] args) => Add(typeof(TControl), args);
        ControlToken Add<TControl>(object dataContext) => Add(typeof(TControl), dataContext);
        void Remove(ControlToken token);
        string RegionName { get; }
        void Clear();
    }

    public readonly struct ControlParameter
    {
        public readonly string Name;
        public readonly object Value;

        public ControlParameter(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}