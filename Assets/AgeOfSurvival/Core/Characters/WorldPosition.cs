using System;

namespace AgeOfSurvival.Core.Characters
{
    /// <summary>
    /// Continuous position on the horizontal simulation plane.
    /// It deliberately contains no Unity-specific vector type.
    /// </summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public WorldPosition(double x, double y)
        {
            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "The X coordinate must be finite.");
            }

            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "The Y coordinate must be finite.");
            }

            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        /// <summary>Logical containment uses floor, never Unity sprite bounds or nearest-cell rounding.</summary>
        public bool TryToWorldCell(out AgeOfSurvival.Core.World.Generation.WorldCellCoordinate cell)
        {
            double x = Math.Floor(X);
            double y = Math.Floor(Y);
            const double exclusiveMaximum = 9223372036854775808d;
            cell = default;
            if (x < -exclusiveMaximum || x >= exclusiveMaximum || y < -exclusiveMaximum || y >= exclusiveMaximum)
                return false;
            cell = new AgeOfSurvival.Core.World.Generation.WorldCellCoordinate((long)x, (long)y);
            return true;
        }

        public WorldPosition Translate(double deltaX, double deltaY)
        {
            if (!IsFinite(deltaX))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaX), deltaX, "The X displacement must be finite.");
            }

            if (!IsFinite(deltaY))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaY), deltaY, "The Y displacement must be finite.");
            }

            return new WorldPosition(X + deltaX, Y + deltaY);
        }

        public double DistanceSquaredTo(WorldPosition other)
        {
            double deltaX = X - other.X;
            double deltaY = Y - other.Y;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        public bool Equals(WorldPosition other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({X:0.###}, {Y:0.###})";
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
