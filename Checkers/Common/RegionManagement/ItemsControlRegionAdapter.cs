using Checkers.Common.Controls;

namespace Checkers.Common.RegionManagement
{
    internal class ItemsControlRegionAdapter
    {
        public IRegion Create(string regionName)
        {
            return new Region(regionName);
        }

        public void Associate(IRegion region, IItemsControl itemsControl)
        {
            ((Region) region).Control = itemsControl;
        }

        private class Region : IRegion
        {
            private long _id;
            private readonly Dictionary<long, int> _items;
            private int _seq;

            public IItemsControl Control;

            public Region(string regionName)
            {
                RegionName = regionName;
                _items = new Dictionary<long, int>();
            }

            public string RegionName { get; }

            public void Clear()
            {
                Control.Children.Clear();
                _seq = 0;
            }

            public ControlToken Add(Type controlType, params ControlParameter[] args)
            {
                var id = ++_id;
                Control.AddContent(controlType, id, _seq, args);
                _seq += 1 + args.Length;
                _items.Add(id, args.Length + 1);
                return new ControlToken(_id);
            }

            
            public void Remove(ControlToken token)
            {
                for (var i = 0; i < Control.Children.Count; i++)
                    if (Control.Children[i].Id == token.Id)
                    {
                        Control.Children.RemoveAt(i--);
                        _seq -= _items[token.Id];
                        _items.Remove(token.Id);
                    }
            }
        }
    }
}