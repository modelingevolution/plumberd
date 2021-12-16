using Checkers.Common.Controls;

namespace Checkers.Common.RegionManagement
{
    internal class ContentControlRegionAdapter
    {
        class Region : IRegion
        {
            private int _id;
            public IContentControl Control;
            public Region(string regionName)
            {
                this.RegionName = regionName;
            }

            public string RegionName { get; private set; }
            public void Clear()
            {
                Control.Control = null;
            }

            public ControlToken Add(Type controlType, params ControlParameter[] args)
            {
                Control.SetContent(controlType, ++_id, args);
                return new ControlToken(_id);
            }

            

            public void Remove(ControlToken token)
            {
                if (token.Id == _id)
                    Control.Control = null;
            }
        }
        public IRegion Create(string regionName)
        {
            return new Region(regionName);
        }

        public void Associate(IRegion region, IContentControl contentControl)
        {
            ((Region) region).Control = contentControl;
        }
    }
}