using System;

namespace AgeOfSurvival.Core.Inventory
{
    public readonly struct ItemDefinitionId : IEquatable<ItemDefinitionId>, IComparable<ItemDefinitionId>
    {
        public ItemDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An item definition identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ItemDefinitionId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(ItemDefinitionId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ItemDefinitionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>, IComparable<ItemInstanceId>
    {
        public ItemInstanceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An item instance identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ItemInstanceId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(ItemInstanceId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ItemInstanceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct ContainerId : IEquatable<ContainerId>, IComparable<ContainerId>
    {
        public ContainerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A container identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(ContainerId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(ContainerId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ContainerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }
}
