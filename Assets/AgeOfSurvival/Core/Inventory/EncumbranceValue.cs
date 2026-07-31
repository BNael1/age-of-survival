using System;

namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Deterministic inventory weight. One thousand internal units are displayed as 1.000.
    /// </summary>
    public readonly struct EncumbranceValue : IEquatable<EncumbranceValue>, IComparable<EncumbranceValue>
    {
        public const long UnitsPerDisplayedUnit = 1000;

        public EncumbranceValue(long units)
        {
            if (units < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(units), units, "Encumbrance must be non-negative.");
            }

            Units = units;
        }

        public long Units { get; }
        public static EncumbranceValue Zero => default;

        public EncumbranceValue Multiply(int quantity)
        {
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be non-negative.");
            }

            return new EncumbranceValue(checked(Units * quantity));
        }

        public int CompareTo(EncumbranceValue other) => Units.CompareTo(other.Units);
        public bool Equals(EncumbranceValue other) => Units == other.Units;
        public override bool Equals(object obj) => obj is EncumbranceValue other && Equals(other);
        public override int GetHashCode() => Units.GetHashCode();
        public override string ToString() => (Units / (decimal)UnitsPerDisplayedUnit).ToString("0.000");

        public static EncumbranceValue operator +(EncumbranceValue left, EncumbranceValue right) =>
            new EncumbranceValue(checked(left.Units + right.Units));

        public static EncumbranceValue operator -(EncumbranceValue left, EncumbranceValue right)
        {
            if (right.Units > left.Units)
            {
                throw new InvalidOperationException("Encumbrance cannot become negative.");
            }

            return new EncumbranceValue(left.Units - right.Units);
        }
    }
}
