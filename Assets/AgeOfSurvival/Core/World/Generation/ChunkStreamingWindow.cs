using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AgeOfSurvival.Core.World.Generation
{
    /// <summary>
    /// Pure Core configuration for one chunk streaming window.
    /// Radii use Chebyshev distance: radius 1 is a 3 x 3 square, radius 2 is 5 x 5.
    /// </summary>
    public readonly struct ChunkStreamingWindowSettings : IEquatable<ChunkStreamingWindowSettings>
    {
        // Defensive operational guard only. It is not a gameplay or world-size limit.
        public const int MaximumRadius = 64;

        public ChunkStreamingWindowSettings(int visibleRadius, int preparationRadius)
        {
            if (visibleRadius < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visibleRadius),
                    visibleRadius,
                    "Visible radius must be zero or greater.");
            }

            if (preparationRadius < visibleRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preparationRadius),
                    preparationRadius,
                    "Preparation radius must be greater than or equal to visible radius.");
            }

            if (preparationRadius > MaximumRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preparationRadius),
                    preparationRadius,
                    $"Preparation radius must not exceed the operational guard {MaximumRadius}.");
            }

            VisibleRadius = visibleRadius;
            PreparationRadius = preparationRadius;
        }

        public int VisibleRadius { get; }
        public int PreparationRadius { get; }

        public bool Equals(ChunkStreamingWindowSettings other)
        {
            return VisibleRadius == other.VisibleRadius
                && PreparationRadius == other.PreparationRadius;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkStreamingWindowSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (VisibleRadius * 397) ^ PreparationRadius;
            }
        }

        public override string ToString()
        {
            return $"visible={VisibleRadius}, prepared={PreparationRadius}";
        }

        public static bool operator ==(
            ChunkStreamingWindowSettings left,
            ChunkStreamingWindowSettings right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ChunkStreamingWindowSettings left,
            ChunkStreamingWindowSettings right)
        {
            return !left.Equals(right);
        }
    }

    public static class ChunkStreamingWindowDefaults
    {
        public const int VisibleRadius = 1;
        public const int PreparationRadius = 2;

        public static ChunkStreamingWindowSettings Settings =>
            new ChunkStreamingWindowSettings(VisibleRadius, PreparationRadius);
    }

    /// <summary>
    /// Immutable deterministic plan around one center chunk.
    /// Lists are ordered center first, then by increasing Chebyshev ring, then Y/X.
    /// Coordinates outside the Int64 domain are clipped rather than wrapped.
    /// </summary>
    public sealed class ChunkStreamingWindowPlan
    {
        private readonly ReadOnlyCollection<ChunkCoordinate> _visibleChunks;
        private readonly ReadOnlyCollection<ChunkCoordinate> _preparedChunks;

        internal ChunkStreamingWindowPlan(
            ChunkCoordinate center,
            ChunkStreamingWindowSettings settings,
            List<ChunkCoordinate> visibleChunks,
            List<ChunkCoordinate> preparedChunks)
        {
            Center = center;
            Settings = settings;
            _visibleChunks = Array.AsReadOnly(visibleChunks.ToArray());
            _preparedChunks = Array.AsReadOnly(preparedChunks.ToArray());
        }

        public ChunkCoordinate Center { get; }
        public ChunkStreamingWindowSettings Settings { get; }
        public IReadOnlyList<ChunkCoordinate> VisibleChunks => _visibleChunks;
        public IReadOnlyList<ChunkCoordinate> PreparedChunks => _preparedChunks;
    }

    public static class ChunkStreamingWindowPlanner
    {
        public static ChunkStreamingWindowPlan Create(
            ChunkCoordinate center,
            ChunkStreamingWindowSettings settings)
        {
            var visible = new List<ChunkCoordinate>(SquareCapacity(settings.VisibleRadius));
            var prepared = new List<ChunkCoordinate>(SquareCapacity(settings.PreparationRadius));

            for (int ring = 0; ring <= settings.PreparationRadius; ring++)
            {
                AppendRing(center, ring, ring <= settings.VisibleRadius, visible, prepared);
            }

            return new ChunkStreamingWindowPlan(center, settings, visible, prepared);
        }

        private static void AppendRing(
            ChunkCoordinate center,
            int ring,
            bool visible,
            List<ChunkCoordinate> visibleChunks,
            List<ChunkCoordinate> preparedChunks)
        {
            if (ring == 0)
            {
                preparedChunks.Add(center);
                if (visible)
                {
                    visibleChunks.Add(center);
                }

                return;
            }

            for (int deltaY = -ring; deltaY <= ring; deltaY++)
            {
                for (int deltaX = -ring; deltaX <= ring; deltaX++)
                {
                    if (Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) != ring)
                    {
                        continue;
                    }

                    if (!TryOffset(center, deltaX, deltaY, out ChunkCoordinate coordinate))
                    {
                        continue;
                    }

                    preparedChunks.Add(coordinate);
                    if (visible)
                    {
                        visibleChunks.Add(coordinate);
                    }
                }
            }
        }

        private static bool TryOffset(
            ChunkCoordinate center,
            int deltaX,
            int deltaY,
            out ChunkCoordinate coordinate)
        {
            try
            {
                coordinate = center.Offset(deltaX, deltaY);
                return true;
            }
            catch (OverflowException)
            {
                coordinate = default(ChunkCoordinate);
                return false;
            }
        }

        private static int SquareCapacity(int radius)
        {
            int diameter = checked((radius * 2) + 1);
            return checked(diameter * diameter);
        }
    }

    /// <summary>
    /// Deterministic difference between two immutable window plans.
    /// Add/show order follows the next plan; remove/hide order follows the previous plan.
    /// </summary>
    public sealed class ChunkStreamingWindowTransition
    {
        private readonly ReadOnlyCollection<ChunkCoordinate> _preparedToAdd;
        private readonly ReadOnlyCollection<ChunkCoordinate> _preparedToRemove;
        private readonly ReadOnlyCollection<ChunkCoordinate> _visibleToShow;
        private readonly ReadOnlyCollection<ChunkCoordinate> _visibleToHide;

        private ChunkStreamingWindowTransition(
            List<ChunkCoordinate> preparedToAdd,
            List<ChunkCoordinate> preparedToRemove,
            List<ChunkCoordinate> visibleToShow,
            List<ChunkCoordinate> visibleToHide)
        {
            _preparedToAdd = Array.AsReadOnly(preparedToAdd.ToArray());
            _preparedToRemove = Array.AsReadOnly(preparedToRemove.ToArray());
            _visibleToShow = Array.AsReadOnly(visibleToShow.ToArray());
            _visibleToHide = Array.AsReadOnly(visibleToHide.ToArray());
        }

        public IReadOnlyList<ChunkCoordinate> PreparedToAdd => _preparedToAdd;
        public IReadOnlyList<ChunkCoordinate> PreparedToRemove => _preparedToRemove;
        public IReadOnlyList<ChunkCoordinate> VisibleToShow => _visibleToShow;
        public IReadOnlyList<ChunkCoordinate> VisibleToHide => _visibleToHide;

        public static ChunkStreamingWindowTransition Create(
            ChunkStreamingWindowPlan previous,
            ChunkStreamingWindowPlan next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            var previousPrepared = previous == null
                ? new HashSet<ChunkCoordinate>()
                : new HashSet<ChunkCoordinate>(previous.PreparedChunks);
            var nextPrepared = new HashSet<ChunkCoordinate>(next.PreparedChunks);
            var previousVisible = previous == null
                ? new HashSet<ChunkCoordinate>()
                : new HashSet<ChunkCoordinate>(previous.VisibleChunks);
            var nextVisible = new HashSet<ChunkCoordinate>(next.VisibleChunks);

            return new ChunkStreamingWindowTransition(
                CollectMissing(next.PreparedChunks, previousPrepared),
                previous == null
                    ? new List<ChunkCoordinate>()
                    : CollectMissing(previous.PreparedChunks, nextPrepared),
                CollectMissing(next.VisibleChunks, previousVisible),
                previous == null
                    ? new List<ChunkCoordinate>()
                    : CollectMissing(previous.VisibleChunks, nextVisible));
        }

        private static List<ChunkCoordinate> CollectMissing(
            IReadOnlyList<ChunkCoordinate> source,
            HashSet<ChunkCoordinate> existing)
        {
            var result = new List<ChunkCoordinate>();
            for (int index = 0; index < source.Count; index++)
            {
                ChunkCoordinate coordinate = source[index];
                if (!existing.Contains(coordinate))
                {
                    result.Add(coordinate);
                }
            }

            return result;
        }
    }
}
