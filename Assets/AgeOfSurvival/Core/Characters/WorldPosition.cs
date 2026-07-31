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
