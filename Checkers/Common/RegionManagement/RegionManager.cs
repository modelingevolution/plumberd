using Checkers.Common.Controls;
using Microsoft.AspNetCore.Components;

namespace Checkers.Common.RegionManagement
{
    public class RegionManager : IRegionManager
    {
        private readonly Dictionary<string, IRegion> _regions;
        private readonly Dictionary<string, List<Type>> _registeredViews;
        public RegionManager()
        {
            _regions = new Dictionary<string, IRegion>();
            _registeredViews = new Dictionary<string, List<Type>>();
        }

        public IRegionManager RegisterViewWithRegion<TControl>(string regionName) where TControl : IComponent
        {
            if (!_registeredViews.TryGetValue(regionName, out var list))
                _registeredViews.Add(regionName, list = new List<Type>());
            list.Add(typeof(TControl));
            return this;
        }
        public IRegionManager SetRegionName(IItemsControl itemsControl, string regionName)
        {
            var adapter = new ItemsControlRegionAdapter();
            IRegion region = adapter.Create(regionName);
            adapter.Associate(region, itemsControl);
            if(_registeredViews.TryGetValue(regionName, out var controls))
                foreach (var i in controls)
                    region.Add(i);
            _regions.Add(regionName, region);
            return this;
        }
        public IRegionManager SetRegionName(IContentControl contentControl, string regionName)
        {
            var adapter = new ContentControlRegionAdapter();
            IRegion region = adapter.Create(regionName);
            adapter.Associate(region, contentControl);
            if (_registeredViews.TryGetValue(regionName, out var controls))
                foreach (var i in controls)
                    region.Add(i);
            _regions.Add(regionName, region);
            return this;
        }

        public IRegion this[string regionName] => _regions[regionName];
        public IRegionManager RemoveRegion(string regionName)
        {
            var region = this._regions[regionName];
            region.Clear();
            _regions.Remove(regionName);
            return this;
        }
    }
}