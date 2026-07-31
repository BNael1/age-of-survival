using System;
using AgeOfSurvival.Core.Characters;

namespace AgeOfSurvival.Core.Inventory
{
    public readonly struct GroundContainerId : IEquatable<GroundContainerId>, IComparable<GroundContainerId>
    {
        public GroundContainerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A ground container identifier must not be empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public int CompareTo(GroundContainerId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(GroundContainerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GroundContainerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    /// <summary>Stable Core-owned item source at a world position.</summary>
    public sealed class GroundContainerState
    {
        public GroundContainerState(GroundContainerId id, WorldPosition position, ContainerState container)
        {
            if (!id.IsValid) throw new ArgumentException("A valid ground container id is required.", nameof(id));
            Id = id;
            Position = position;
            Container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public GroundContainerId Id { get; }
        public WorldPosition Position { get; }
        public ContainerState Container { get; }
        public bool IsEmpty => Container.Entries.Count == 0;
    }
}
