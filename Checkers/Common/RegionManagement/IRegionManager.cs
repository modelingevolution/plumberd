using Checkers.Common.Controls;
using Microsoft.AspNetCore.Components;

namespace Checkers.Common.RegionManagement
{
    public interface IRegionManager
    {
        IRegionManager RegisterViewWithRegion<TControl>(string regionName) where TControl : IComponent;
        IRegionManager SetRegionName(IContentControl contentControl, string regionName);
        IRegion this[string regionName] { get; }
        IRegionManager RemoveRegion(string regionName);
    }
}