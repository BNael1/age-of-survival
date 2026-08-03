using System;
using AgeOfSurvival.Core.World;

namespace AgeOfSurvival.Core.World.Generation
{
    /// <summary>
    /// Signed cell coordinate in the unbounded logical world.
    /// </summary>
    public readonly struct WorldCellCoordinate : IEquatable<WorldCellCoordinate>, IComparable<WorldCellCoordinate>
    {
        public WorldCellCoordinate(long x, long y)
        {
            X = x;
            Y = y;
        }

        public long X { get; }
        public long Y { get; }

        public WorldCellCoordinate Offset(long deltaX, long deltaY)
        {
            return new WorldCellCoordinate(
                checked(X + deltaX),
                checked(Y + deltaY));
        }

        public int CompareTo(WorldCellCoordinate other)
        {
            int yComparison = Y.CompareTo(other.Y);
            return yComparison != 0 ? yComparison : X.CompareTo(other.X);
        }

        public bool Equals(WorldCellCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldCellCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FoldToInt(X) * 397) ^ FoldToInt(Y);
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public static bool operator ==(WorldCellCoordinate left, WorldCellCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldCellCoordinate left, WorldCellCoordinate right)
        {
            return !left.Equals(right);
        }

        private static int FoldToInt(long value)
        {
            return unchecked((int)(value ^ (value >> 32)));
        }
    }

    /// <summary>
    /// Signed coordinate of one chunk. Coordinates remain stable regardless of load order.
    /// </summary>
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>, IComparable<ChunkCoordinate>
    {
        public ChunkCoordinate(long x, long y)
        {
            X = x;
            Y = y;
        }

        public long X { get; }
        public long Y { get; }

        public ChunkCoordinate Offset(long deltaX, long deltaY)
        {
            return new ChunkCoordinate(
                checked(X + deltaX),
                checked(Y + deltaY));
        }

        public int CompareTo(ChunkCoordinate other)
        {
            int yComparison = Y.CompareTo(other.Y);
            return yComparison != 0 ? yComparison : X.CompareTo(other.X);
        }

        public bool Equals(ChunkCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FoldToInt(X) * 397) ^ FoldToInt(Y);
            }
        }

        public override string ToString()
        {
            return $"[{X}, {Y}]";
        }

        public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right)
        {
            return !left.Equals(right);
        }

        private static int FoldToInt(long value)
        {
            return unchecked((int)(value ^ (value >> 32)));
        }
    }

    /// <summary>
    /// Explicit chunk dimensions. The prototype default is 32 x 32, but generation
    /// samples are addressed by world cell and therefore do not depend on this partition.
    /// </summary>
    public readonly struct ChunkLayout : IEquatable<ChunkLayout>
    {
        public ChunkLayout(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Chunk width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Chunk height must be greater than zero.");
            }

            Bounds = new GridBounds(width, height);
        }

        public GridBounds Bounds { get; }
        public int Width => Bounds.Width;
        public int Height => Bounds.Height;
        public int CellCount => Bounds.CellCount;
        public bool IsValid => Width > 0 && Height > 0;

        public bool Equals(ChunkLayout other)
        {
            return Bounds.Equals(other.Bounds);
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkLayout other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Bounds.GetHashCode();
        }

        public override string ToString()
        {
            return Bounds.ToString();
        }

        public static bool operator ==(ChunkLayout left, ChunkLayout right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkLayout left, ChunkLayout right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ChunkAddress : IEquatable<ChunkAddress>
    {
        public ChunkAddress(ChunkCoordinate chunk, GridPosition localPosition)
        {
            Chunk = chunk;
            LocalPosition = localPosition;
        }

        public ChunkCoordinate Chunk { get; }
        public GridPosition LocalPosition { get; }

        public bool Equals(ChunkAddress other)
        {
            return Chunk.Equals(other.Chunk)
                && LocalPosition.Equals(other.LocalPosition);
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Chunk.GetHashCode() * 397) ^ LocalPosition.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Chunk}:{LocalPosition}";
        }
    }

    public static class ChunkAddressing
    {
        public static ChunkAddress Locate(
            WorldCellCoordinate worldCell,
            ChunkLayout layout)
        {
            ValidateLayout(layout);

            DivideFloor(worldCell.X, layout.Width, out long chunkX, out int localX);
            DivideFloor(worldCell.Y, layout.Height, out long chunkY, out int localY);

            return new ChunkAddress(
                new ChunkCoordinate(chunkX, chunkY),
                new GridPosition(localX, localY));
        }

        public static WorldCellCoordinate ToWorld(
            ChunkCoordinate chunk,
            GridPosition localPosition,
            ChunkLayout layout)
        {
            ValidateLayout(layout);
            if (!layout.Bounds.Contains(localPosition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localPosition),
                    localPosition,
                    "The local position is outside the chunk layout.");
            }

            return new WorldCellCoordinate(
                ComposeWorldAxis(chunk.X, localPosition.X, layout.Width),
                ComposeWorldAxis(chunk.Y, localPosition.Y, layout.Height));
        }

        public static WorldCellCoordinate GetWorldOrigin(
            ChunkCoordinate chunk,
            ChunkLayout layout)
        {
            ValidateLayout(layout);
            return new WorldCellCoordinate(
                ComposeWorldAxis(chunk.X, 0, layout.Width),
                ComposeWorldAxis(chunk.Y, 0, layout.Height));
        }

        private static long ComposeWorldAxis(
            long chunkCoordinate,
            int localCoordinate,
            int chunkSize)
        {
            if (chunkCoordinate >= 0)
            {
                return checked((chunkCoordinate * chunkSize) + localCoordinate);
            }

            // Keep the full Int64 world domain representable without requiring
            // the intermediate chunk origin multiplication to fit on its own.
            return checked(
                ((chunkCoordinate + 1L) * chunkSize)
                + (localCoordinate - chunkSize));
        }

        private static void DivideFloor(
            long value,
            int positiveDivisor,
            out long quotient,
            out int remainder)
        {
            quotient = value / positiveDivisor;
            long rawRemainder = value % positiveDivisor;
            if (rawRemainder < 0)
            {
                quotient--;
                rawRemainder += positiveDivisor;
            }

            remainder = checked((int)rawRemainder);
        }

        private static void ValidateLayout(ChunkLayout layout)
        {
            if (!layout.IsValid)
            {
                throw new ArgumentException("A valid chunk layout is required.", nameof(layout));
            }
        }
    }
}
