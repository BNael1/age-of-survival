using System;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Construction
{
    /// <summary>
    /// Local rendered frame paired with one exact logical world origin.
    /// Floating-point values describe only the rendered neighbourhood.
    /// </summary>
    public readonly struct ConstructionProjectionFrame
    {
        private const double MinimumAbsoluteDeterminant = 1e-12d;

        private readonly byte _initialized;

        private ConstructionProjectionFrame(
            WorldCellCoordinate logicalOrigin,
            Vector3 visualOrigin,
            Vector3 xBasis,
            Vector3 yBasis)
        {
            LogicalOrigin = logicalOrigin;
            VisualOrigin = visualOrigin;
            XBasis = xBasis;
            YBasis = yBasis;
            _initialized = 1;
        }

        public WorldCellCoordinate LogicalOrigin { get; }
        public Vector3 VisualOrigin { get; }
        public Vector3 XBasis { get; }
        public Vector3 YBasis { get; }
        public bool IsValid => _initialized == 1;

        public static bool TryCreate(
            WorldCellCoordinate logicalOrigin,
            Vector3 visualOrigin,
            Vector3 xBasis,
            Vector3 yBasis,
            out ConstructionProjectionFrame frame)
        {
            frame = default;
            if (!IsFinite(visualOrigin) || !IsFinite(xBasis) || !IsFinite(yBasis))
            {
                return false;
            }

            double determinant = ((double)xBasis.x * yBasis.y)
                - ((double)xBasis.y * yBasis.x);
            if (double.IsNaN(determinant)
                || double.IsInfinity(determinant)
                || Math.Abs(determinant) <= MinimumAbsoluteDeterminant)
            {
                return false;
            }

            frame = new ConstructionProjectionFrame(
                logicalOrigin,
                visualOrigin,
                xBasis,
                yBasis);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Pure projection result before selection of a construction occupancy space.
    /// </summary>
    public readonly struct ConstructionPlacementSample
    {
        private readonly byte _initialized;

        internal ConstructionPlacementSample(
            WorldCellCoordinate cell,
            double offsetX,
            double offsetY)
        {
            Cell = cell;
            OffsetX = offsetX;
            OffsetY = offsetY;
            _initialized = 1;
        }

        public WorldCellCoordinate Cell { get; }
        public double OffsetX { get; }
        public double OffsetY { get; }
        public bool IsValid => _initialized == 1;
    }

    /// <summary>
    /// Canonical Core placement address plus Runtime-only snapping information.
    /// </summary>
    public readonly struct ConstructionPlacementTarget
    {
        private readonly byte _initialized;

        internal ConstructionPlacementTarget(
            ConstructionPlacementSample sample,
            ConstructionSpaceKey space,
            ConstructionCellSide? selectedSide)
        {
            Sample = sample;
            Space = space;
            SelectedSide = selectedSide;
            _initialized = 1;
        }

        public ConstructionPlacementSample Sample { get; }
        public ConstructionSpaceKey Space { get; }
        public ConstructionCellSide? SelectedSide { get; }
        public bool IsValid => _initialized == 1 && Sample.IsValid && Space.IsValid;
    }

    /// <summary>
    /// Converts between a local Unity rendering frame and canonical Core construction addresses.
    /// It owns no input, gameplay validation, occupancy, or world mutation policy.
    /// </summary>
    public static class ConstructionPlacementProjection
    {
        private const double LongUpperExclusive = 9223372036854775808d;

        public static bool TrySample(
            ConstructionProjectionFrame frame,
            Vector3 worldPosition,
            out ConstructionPlacementSample sample)
        {
            sample = default;
            if (!frame.IsValid || !IsFinite(worldPosition))
            {
                return false;
            }

            double deltaX = (double)worldPosition.x - frame.VisualOrigin.x;
            double deltaY = (double)worldPosition.y - frame.VisualOrigin.y;
            double determinant = ((double)frame.XBasis.x * frame.YBasis.y)
                - ((double)frame.XBasis.y * frame.YBasis.x);
            double localX = ((deltaX * frame.YBasis.y) - (deltaY * frame.YBasis.x))
                / determinant;
            double localY = (((double)frame.XBasis.x * deltaY)
                - ((double)frame.XBasis.y * deltaX))
                / determinant;

            if (!IsFinite(localX)
                || !IsFinite(localY)
                || !TrySnapDelta(localX, out long cellDeltaX)
                || !TrySnapDelta(localY, out long cellDeltaY))
            {
                return false;
            }

            WorldCellCoordinate cell;
            try
            {
                cell = frame.LogicalOrigin.Offset(cellDeltaX, cellDeltaY);
            }
            catch (OverflowException)
            {
                return false;
            }

            double offsetX = localX - cellDeltaX;
            double offsetY = localY - cellDeltaY;
            if (!IsFinite(offsetX) || !IsFinite(offsetY))
            {
                return false;
            }

            sample = new ConstructionPlacementSample(cell, offsetX, offsetY);
            return true;
        }

        public static bool TryResolve(
            ConstructionProjectionFrame frame,
            Vector3 worldPosition,
            ConstructionSpaceKind kind,
            out ConstructionPlacementTarget target)
        {
            target = default;
            if (kind != ConstructionSpaceKind.Surface
                && kind != ConstructionSpaceKind.Edge)
            {
                return false;
            }

            if (!TrySample(frame, worldPosition, out ConstructionPlacementSample sample))
            {
                return false;
            }

            if (kind == ConstructionSpaceKind.Surface)
            {
                target = new ConstructionPlacementTarget(
                    sample,
                    ConstructionSpaceKey.Surface(sample.Cell),
                    null);
                return true;
            }

            ConstructionCellSide side = SelectNearestSide(sample.OffsetX, sample.OffsetY);
            try
            {
                target = new ConstructionPlacementTarget(
                    sample,
                    ConstructionSpaceKey.Edge(sample.Cell, side),
                    side);
                return true;
            }
            catch (OverflowException)
            {
                target = default;
                return false;
            }
        }

        public static bool TryMapSpaceCenter(
            ConstructionProjectionFrame frame,
            ConstructionSpaceKey space,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (!frame.IsValid || !space.IsValid)
            {
                return false;
            }

            WorldCellCoordinate anchor;
            ConstructionEdgeAxis? edgeAxis = null;
            if (space.Kind == ConstructionSpaceKind.Surface)
            {
                anchor = space.AnchorCell;
            }
            else if (space.Kind == ConstructionSpaceKind.Edge)
            {
                ConstructionEdgeAddress edge = space.EdgeAddress;
                anchor = edge.AnchorCell;
                edgeAxis = edge.Axis;
            }
            else
            {
                return false;
            }

            double deltaX = (double)((decimal)anchor.X - frame.LogicalOrigin.X);
            double deltaY = (double)((decimal)anchor.Y - frame.LogicalOrigin.Y);
            double mappedX = frame.VisualOrigin.x
                + (deltaX * frame.XBasis.x)
                + (deltaY * frame.YBasis.x);
            double mappedY = frame.VisualOrigin.y
                + (deltaX * frame.XBasis.y)
                + (deltaY * frame.YBasis.y);
            double mappedZ = frame.VisualOrigin.z
                + (deltaX * frame.XBasis.z)
                + (deltaY * frame.YBasis.z);

            if (edgeAxis == ConstructionEdgeAxis.Vertical)
            {
                mappedX += 0.5d * frame.XBasis.x;
                mappedY += 0.5d * frame.XBasis.y;
                mappedZ += 0.5d * frame.XBasis.z;
            }
            else if (edgeAxis == ConstructionEdgeAxis.Horizontal)
            {
                mappedX += 0.5d * frame.YBasis.x;
                mappedY += 0.5d * frame.YBasis.y;
                mappedZ += 0.5d * frame.YBasis.z;
            }

            return TryCreateVector(mappedX, mappedY, mappedZ, out worldPosition);
        }

        private static ConstructionCellSide SelectNearestSide(double offsetX, double offsetY)
        {
            ConstructionCellSide bestSide = ConstructionCellSide.North;
            double bestDistance = 0.5d - offsetY;

            double distance = 0.5d - offsetX;
            if (distance < bestDistance)
            {
                bestSide = ConstructionCellSide.East;
                bestDistance = distance;
            }

            distance = 0.5d + offsetY;
            if (distance < bestDistance)
            {
                bestSide = ConstructionCellSide.South;
                bestDistance = distance;
            }

            distance = 0.5d + offsetX;
            if (distance < bestDistance)
            {
                bestSide = ConstructionCellSide.West;
            }

            return bestSide;
        }

        private static bool TrySnapDelta(double value, out long snapped)
        {
            double lowerCell = Math.Ceiling(value - 0.5d);
            if (!IsFinite(lowerCell)
                || lowerCell < long.MinValue
                || lowerCell >= LongUpperExclusive)
            {
                snapped = 0L;
                return false;
            }

            snapped = (long)lowerCell;
            return true;
        }

        private static bool TryCreateVector(
            double x,
            double y,
            double z,
            out Vector3 value)
        {
            value = default;
            if (!IsRepresentableFloat(x)
                || !IsRepresentableFloat(y)
                || !IsRepresentableFloat(z))
            {
                return false;
            }

            value = new Vector3((float)x, (float)y, (float)z);
            return IsFinite(value);
        }

        private static bool IsRepresentableFloat(double value)
        {
            return IsFinite(value) && value >= -float.MaxValue && value <= float.MaxValue;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
