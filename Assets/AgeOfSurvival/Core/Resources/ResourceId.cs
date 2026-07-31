using System;

namespace AgeOfSurvival.Core.Resources
{
    /// <summary>
    /// Reproducible logical identity for one resource instance.
    /// </summary>
    public readonly struct ResourceId : IEquatable<ResourceId>, IComparable<ResourceId>
    {
        public ResourceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A resource identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ResourceId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(ResourceId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }
}
