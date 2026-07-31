using System;

namespace AgeOfSurvival.Core.World
{
    /// <summary>
    /// Rectangular zero-based bounds for a dense logical grid.
    /// </summary>
    public readonly struct GridBounds : IEquatable<GridBounds>
    {
        public GridBounds(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
            }

            Width = width;
            Height = height;
            CellCount = checked(width * height);
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount { get; }

        public bool Contains(GridPosition position)
        {
            return position.X >= 0
                && position.Y >= 0
                && position.X < Width
                && position.Y < Height;
        }

        public int ToIndex(GridPosition position)
        {
            if (!Contains(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "The position is outside the grid bounds.");
            }

            return checked((position.Y * Width) + position.X);
        }

        public GridPosition FromIndex(int index)
        {
            if (index < 0 || index >= CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the grid bounds.");
            }

            return new GridPosition(index % Width, index / Width);
        }

        public bool Equals(GridBounds other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is GridBounds other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }
}
