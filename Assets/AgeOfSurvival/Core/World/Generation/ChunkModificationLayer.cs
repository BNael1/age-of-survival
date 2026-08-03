using System;
using AgeOfSurvival.Core.World;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.World.Generation
{
    public readonly struct ChunkModification<T>
    {
        public ChunkModification(GridPosition localPosition, T value)
        {
            LocalPosition = localPosition;
            Value = value;
        }

        public GridPosition LocalPosition { get; }
        public T Value { get; }
    }

    /// <summary>
    /// Sparse mutable overlay kept separate from the immutable generated chunk.
    /// Its stable row-major copy is suitable for a future versioned save DTO.
    /// </summary>
    public sealed class ChunkModificationLayer<T>
    {
        private readonly Dictionary<GridPosition, T> _entries =
            new Dictionary<GridPosition, T>();

        public ChunkModificationLayer(
            ChunkCoordinate coordinate,
            ChunkLayout layout)
        {
            if (!layout.IsValid)
            {
                throw new ArgumentException("A valid chunk layout is required.", nameof(layout));
            }

            Coordinate = coordinate;
            Layout = layout;
        }

        public ChunkCoordinate Coordinate { get; }
        public ChunkLayout Layout { get; }
        public int Count => _entries.Count;

        public void Set(GridPosition localPosition, T value)
        {
            ValidateLocalPosition(localPosition);
            _entries[localPosition] = value;
        }

        public bool TryGet(GridPosition localPosition, out T value)
        {
            ValidateLocalPosition(localPosition);
            return _entries.TryGetValue(localPosition, out value);
        }

        public bool Remove(GridPosition localPosition)
        {
            ValidateLocalPosition(localPosition);
            return _entries.Remove(localPosition);
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public ChunkModification<T>[] CopyEntriesInStableOrder()
        {
            var positions = new List<GridPosition>(_entries.Keys);
            positions.Sort((left, right) =>
                Layout.Bounds.ToIndex(left).CompareTo(Layout.Bounds.ToIndex(right)));

            var copy = new ChunkModification<T>[positions.Count];
            for (int index = 0; index < positions.Count; index++)
            {
                GridPosition position = positions[index];
                copy[index] = new ChunkModification<T>(position, _entries[position]);
            }

            return copy;
        }

        private void ValidateLocalPosition(GridPosition localPosition)
        {
            if (!Layout.Bounds.Contains(localPosition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localPosition),
                    localPosition,
                    "The local position is outside the modification layer's chunk.");
            }
        }
    }
}
