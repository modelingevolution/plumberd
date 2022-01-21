namespace Checkers.Common.RegionManagement
{
    public struct ControlToken : IEquatable<ControlToken>
    {
        internal long Id;
        internal ControlToken(long id)
        {
            this.Id = id;
        }

        public bool Equals(ControlToken other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is ControlToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(ControlToken left, ControlToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ControlToken left, ControlToken right)
        {
            return !left.Equals(right);
        }
    }
}