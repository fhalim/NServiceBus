namespace EasyNetQ
{
    using System;

    public class HostConfiguration : IHostConfiguration, IEquatable<HostConfiguration>
    {
        public string Host { get; set; }
        public ushort Port { get; set; }
        public bool IsFailover { get; set; }

        public bool Equals(HostConfiguration other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(Host, other.Host) && Port == other.Port && IsFailover.Equals(other.IsFailover);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((HostConfiguration) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Host != null ? Host.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Port.GetHashCode();
                hashCode = (hashCode*397) ^ IsFailover.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(HostConfiguration left, HostConfiguration right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(HostConfiguration left, HostConfiguration right)
        {
            return !Equals(left, right);
        }
    }
}