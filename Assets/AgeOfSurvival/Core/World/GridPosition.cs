using System;

namespace AgeOfSurvival.Core.World
{
    /// <summary>
    /// Integer position in the logical world grid. It deliberately has no Unity dependency.
    /// </summary>
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public const int CardinalNeighborCount = 4;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public GridPosition Offset(int deltaX, int deltaY)
        {
            return new GridPosition(checked(X + deltaX), checked(Y + deltaY));
        }

        /// <summary>
        /// Returns neighbors in a stable order: north, east, south, west.
        /// </summary>
        public GridPosition GetCardinalNeighbor(int index)
        {
            switch (index)
            {
                case 0: return Offset(0, 1);
                case 1: return Offset(1, 0);
                case 2: return Offset(0, -1);
                case 3: return Offset(-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A cardinal neighbor index must be between 0 and 3.");
            }
        }

        public int ManhattanDistanceTo(GridPosition other)
        {
            long dx = Math.Abs((long)X - other.X);
            long dy = Math.Abs((long)Y - other.Y);
            return checked((int)(dx + dy));
        }

        public bool Equals(GridPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public static bool operator ==(GridPosition left, GridPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPosition left, GridPosition right)
        {
            return !left.Equals(right);
        }
    }
}
