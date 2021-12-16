using Microsoft.AspNetCore.Components;

namespace Checkers.Common.Controls
{
    public interface IItemsControl
    {
        IList<RenderControl> Children { get; }
    }

    public readonly struct RenderControl : IEquatable<RenderControl>
    {
        public readonly RenderFragment RenderFragment;
        public readonly long Id;

        public RenderControl(long id, RenderFragment action)
        {
            this.Id = id;
            this.RenderFragment = action;
        }

        public bool Equals(RenderControl other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderControl other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(RenderControl left, RenderControl right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RenderControl left, RenderControl right)
        {
            return !left.Equals(right);
        }
    }
}