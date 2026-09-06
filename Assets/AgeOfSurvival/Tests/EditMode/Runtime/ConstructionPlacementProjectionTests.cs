using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Construction;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class ConstructionPlacementProjectionTests
    {
        private const double Tolerance = 0.000001d;

        [Test]
        public void FrameRejectsNonFiniteOrigin()
        {
            bool created = ConstructionProjectionFrame.TryCreate(
                new WorldCellCoordinate(0L, 0L),
                new Vector3(float.NaN, 0f, 0f),
                Vector3.right,
                Vector3.up,
                out ConstructionProjectionFrame frame);

            Assert.That(created, Is.False);
            Assert.That(frame.IsValid, Is.False);
        }

        [Test]
        public void FrameRejectsSingularBasis()
        {
            bool created = ConstructionProjectionFrame.TryCreate(
                new WorldCellCoordinate(0L, 0L),
                Vector3.zero,
                new Vector3(1f, 0.5f, 0f),
                new Vector3(2f, 1f, 0f),
                out ConstructionProjectionFrame frame);

            Assert.That(created, Is.False);
            Assert.That(frame.IsValid, Is.False);
        }

        [Test]
        public void DefaultFrameCannotSample()
        {
            bool sampled = ConstructionPlacementProjection.TrySample(
                default,
                Vector3.zero,
                out ConstructionPlacementSample sample);

            Assert.That(sampled, Is.False);
            Assert.That(sample.IsValid, Is.False);
        }

        [Test]
        public void SamplesOriginIntoLogicalOriginCell()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(17L, -9L));

            bool sampled = ConstructionPlacementProjection.TrySample(
                frame,
                Vector3.zero,
                out ConstructionPlacementSample sample);

            Assert.That(sampled, Is.True);
            Assert.That(sample.Cell, Is.EqualTo(new WorldCellCoordinate(17L, -9L)));
            Assert.That(sample.OffsetX, Is.Zero.Within(Tolerance));
            Assert.That(sample.OffsetY, Is.Zero.Within(Tolerance));
        }

        [Test]
        public void SamplesPositiveInteriorToNearestCell()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(10L, 20L));

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(1.2f, 2.4f, 0f),
                    out ConstructionPlacementSample sample),
                Is.True);
            Assert.That(sample.Cell, Is.EqualTo(new WorldCellCoordinate(11L, 22L)));
            Assert.That(sample.OffsetX, Is.EqualTo(0.2d).Within(Tolerance));
            Assert.That(sample.OffsetY, Is.EqualTo(0.4d).Within(Tolerance));
        }

        [Test]
        public void SamplesNegativeInteriorToNearestCell()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(10L, 20L));

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(-1.2f, -2.4f, 0f),
                    out ConstructionPlacementSample sample),
                Is.True);
            Assert.That(sample.Cell, Is.EqualTo(new WorldCellCoordinate(9L, 18L)));
            Assert.That(sample.OffsetX, Is.EqualTo(-0.2d).Within(Tolerance));
            Assert.That(sample.OffsetY, Is.EqualTo(-0.4d).Within(Tolerance));
        }

        [Test]
        public void PositiveHalfBoundaryChoosesLowerCell()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(0L, 0L));

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(0.5f, 0f, 0f),
                    out ConstructionPlacementSample sample),
                Is.True);
            Assert.That(sample.Cell.X, Is.Zero);
            Assert.That(sample.OffsetX, Is.EqualTo(0.5d).Within(Tolerance));
        }

        [Test]
        public void NegativeHalfBoundaryChoosesLowerCell()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(0L, 0L));

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(-0.5f, 0f, 0f),
                    out ConstructionPlacementSample sample),
                Is.True);
            Assert.That(sample.Cell.X, Is.EqualTo(-1L));
            Assert.That(sample.OffsetX, Is.EqualTo(0.5d).Within(Tolerance));
        }

        [Test]
        public void SamplePreservesLargeLogicalOriginWithoutFloatRoundTrip()
        {
            var origin = new WorldCellCoordinate(long.MaxValue - 4L, long.MinValue + 4L);
            ConstructionProjectionFrame frame = CreateIdentityFrame(origin);

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(1.2f, -2.4f, 0f),
                    out ConstructionPlacementSample sample),
                Is.True);
            Assert.That(
                sample.Cell,
                Is.EqualTo(new WorldCellCoordinate(long.MaxValue - 3L, long.MinValue + 2L)));
        }

        [Test]
        public void NonFinitePointerRejected()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(0L, 0L));

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(float.PositiveInfinity, 0f, 0f),
                    out _),
                Is.False);
            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    new Vector3(0f, float.NaN, 0f),
                    out _),
                Is.False);
        }

        [Test]
        public void SurfaceResolvesNearestCell()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(-4L, 8L));

            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    frame,
                    new Vector3(1.2f, -2.4f, 0f),
                    ConstructionSpaceKind.Surface,
                    out ConstructionPlacementTarget target),
                Is.True);
            Assert.That(target.IsValid, Is.True);
            Assert.That(
                target.Space,
                Is.EqualTo(ConstructionSpaceKey.Surface(new WorldCellCoordinate(-3L, 6L))));
            Assert.That(target.SelectedSide, Is.Null);
        }

        [Test]
        public void InteriorIsNotAPlacementKindInB1A()
        {
            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    CreateIdentityFrame(new WorldCellCoordinate(0L, 0L)),
                    Vector3.zero,
                    ConstructionSpaceKind.Interior,
                    out ConstructionPlacementTarget target),
                Is.False);
            Assert.That(target.IsValid, Is.False);
        }

        [Test]
        public void RoofUsesTheSurfaceCellProjectionIn7LE()
        {
            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    CreateIdentityFrame(new WorldCellCoordinate(0L, 0L)),
                    Vector3.zero,
                    ConstructionSpaceKind.Roof,
                    out ConstructionPlacementTarget target),
                Is.True);
            Assert.That(target.IsValid, Is.True);
            Assert.That(target.Space.Kind, Is.EqualTo(ConstructionSpaceKind.Roof));
        }

        [TestCase(-17L, -16L)]
        [TestCase(long.MaxValue, long.MinValue)]
        [TestCase(9007199254740993L, -9007199254740993L)]
        public void RoofRoundTripKeepsLocalInt64Origin(long x, long y)
        {
            var cell = new WorldCellCoordinate(x, y);
            ConstructionProjectionFrame frame = CreateIdentityFrame(cell);
            Assert.That(ConstructionPlacementProjection.TryResolve(frame, Vector3.zero,
                ConstructionSpaceKind.Roof, out ConstructionPlacementTarget target), Is.True);
            Assert.That(target.Space, Is.EqualTo(ConstructionSpaceKey.Roof(cell)));
            Assert.That(ConstructionPlacementProjection.TryMapSpaceCenter(frame, target.Space,
                out Vector3 position), Is.True);
            Assert.That(position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void EdgeResolvesNorth()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0f, 0.45f, 0f));

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.North));
        }

        [Test]
        public void EdgeResolvesEast()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0.45f, 0f, 0f));

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.East));
        }

        [Test]
        public void EdgeResolvesSouth()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0f, -0.45f, 0f));

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.South));
        }

        [Test]
        public void EdgeResolvesWest()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(-0.45f, 0f, 0f));

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.West));
        }

        [Test]
        public void CellCenterEdgeTieUsesStableNorthPriority()
        {
            ConstructionPlacementTarget target = ResolveEdge(Vector3.zero);

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.North));
        }

        [Test]
        public void NorthEastEdgeTieUsesStableNorthPriority()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0.45f, 0.45f, 0f));

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.North));
        }

        [Test]
        public void SouthEastEdgeTieUsesStableEastPriority()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0.45f, -0.45f, 0f));

            Assert.That(target.SelectedSide, Is.EqualTo(ConstructionCellSide.East));
        }

        [Test]
        public void ExactVerticalBoundaryProducesCanonicalSharedEdge()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0.5f, 0f, 0f));
            ConstructionSpaceKey oppositeDescription = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(1L, 0L),
                ConstructionCellSide.West);

            Assert.That(target.Space, Is.EqualTo(oppositeDescription));
        }

        [Test]
        public void ExactHorizontalBoundaryProducesCanonicalSharedEdge()
        {
            ConstructionPlacementTarget target = ResolveEdge(new Vector3(0f, 0.5f, 0f));
            ConstructionSpaceKey oppositeDescription = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(0L, 1L),
                ConstructionCellSide.South);

            Assert.That(target.Space, Is.EqualTo(oppositeDescription));
        }

        [Test]
        public void NegativeBoundaryProducesCanonicalSharedEdge()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(-2L, -3L));
            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    frame,
                    new Vector3(0.5f, 0f, 0f),
                    ConstructionSpaceKind.Edge,
                    out ConstructionPlacementTarget target),
                Is.True);
            ConstructionSpaceKey oppositeDescription = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(-1L, -3L),
                ConstructionCellSide.West);

            Assert.That(target.Space, Is.EqualTo(oppositeDescription));
        }

        [Test]
        public void MapSurfaceCenterUsesLogicalOriginOffset()
        {
            ConstructionProjectionFrame frame = CreateFrame(
                new WorldCellCoordinate(10L, -20L),
                new Vector3(3f, 7f, 2f),
                new Vector3(2f, 1f, 0f),
                new Vector3(-1f, 1f, 0f));
            ConstructionSpaceKey surface = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(12L, -23L));

            Assert.That(
                ConstructionPlacementProjection.TryMapSpaceCenter(
                    frame,
                    surface,
                    out Vector3 mapped),
                Is.True);
            AssertVector(mapped, new Vector3(10f, 6f, 2f));
        }

        [Test]
        public void MapVerticalEdgeCenterUsesHalfXBasis()
        {
            ConstructionProjectionFrame frame = CreateFrame(
                new WorldCellCoordinate(10L, -20L),
                new Vector3(3f, 7f, 2f),
                new Vector3(2f, 1f, 0f),
                new Vector3(-1f, 1f, 0f));
            ConstructionSpaceKey edge = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(12L, -20L),
                ConstructionCellSide.East);

            Assert.That(
                ConstructionPlacementProjection.TryMapSpaceCenter(frame, edge, out Vector3 mapped),
                Is.True);
            AssertVector(mapped, new Vector3(8f, 9.5f, 2f));
        }

        [Test]
        public void MapHorizontalEdgeCenterUsesHalfYBasis()
        {
            ConstructionProjectionFrame frame = CreateFrame(
                new WorldCellCoordinate(10L, -20L),
                new Vector3(3f, 7f, 2f),
                new Vector3(2f, 1f, 0f),
                new Vector3(-1f, 1f, 0f));
            ConstructionSpaceKey edge = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(10L, -18L),
                ConstructionCellSide.North);

            Assert.That(
                ConstructionPlacementProjection.TryMapSpaceCenter(frame, edge, out Vector3 mapped),
                Is.True);
            AssertVector(mapped, new Vector3(0.5f, 9.5f, 2f));
        }

        [Test]
        public void MapLargeOriginKeepsSmallVisualDeltaExact()
        {
            var origin = new WorldCellCoordinate(long.MaxValue - 1L, long.MinValue + 1L);
            ConstructionProjectionFrame frame = CreateIdentityFrame(origin);
            ConstructionSpaceKey surface = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(long.MaxValue, long.MinValue + 1L));

            Assert.That(
                ConstructionPlacementProjection.TryMapSpaceCenter(frame, surface, out Vector3 mapped),
                Is.True);
            AssertVector(mapped, Vector3.right);
        }

        [Test]
        public void ResolveRejectsEastEdgeBeyondLongMax()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(long.MaxValue, 0L));

            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    frame,
                    new Vector3(0.45f, 0f, 0f),
                    ConstructionSpaceKind.Edge,
                    out ConstructionPlacementTarget target),
                Is.False);
            Assert.That(target.IsValid, Is.False);
        }

        [Test]
        public void ResolveRejectsWestEdgeBeyondLongMin()
        {
            ConstructionProjectionFrame frame = CreateIdentityFrame(
                new WorldCellCoordinate(long.MinValue, 0L));

            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    frame,
                    new Vector3(-0.45f, 0f, 0f),
                    ConstructionSpaceKind.Edge,
                    out ConstructionPlacementTarget target),
                Is.False);
            Assert.That(target.IsValid, Is.False);
        }

        [Test]
        public void IsometricFrameInvertsPointerCoordinates()
        {
            Vector3 xBasis = new Vector3(0.5f, 0.25f, 0f);
            Vector3 yBasis = new Vector3(-0.5f, 0.25f, 0f);
            ConstructionProjectionFrame frame = CreateFrame(
                new WorldCellCoordinate(0L, 0L),
                new Vector3(4f, -3f, 0f),
                xBasis,
                yBasis);
            const double expectedLocalX = 1.2d;
            const double expectedLocalY = -2.4d;
            Vector3 pointer = frame.VisualOrigin
                + (xBasis * (float)expectedLocalX)
                + (yBasis * (float)expectedLocalY);

            Assert.That(
                ConstructionPlacementProjection.TrySample(
                    frame,
                    pointer,
                    out ConstructionPlacementSample sample),
                Is.True);
            double actualLocalX = (double)sample.Cell.X + sample.OffsetX;
            double actualLocalY = (double)sample.Cell.Y + sample.OffsetY;
            Assert.That(actualLocalX, Is.EqualTo(expectedLocalX).Within(Tolerance));
            Assert.That(actualLocalY, Is.EqualTo(expectedLocalY).Within(Tolerance));
        }

        private static ConstructionProjectionFrame CreateIdentityFrame(
            WorldCellCoordinate logicalOrigin)
        {
            return CreateFrame(logicalOrigin, Vector3.zero, Vector3.right, Vector3.up);
        }

        private static ConstructionProjectionFrame CreateFrame(
            WorldCellCoordinate logicalOrigin,
            Vector3 visualOrigin,
            Vector3 xBasis,
            Vector3 yBasis)
        {
            Assert.That(
                ConstructionProjectionFrame.TryCreate(
                    logicalOrigin,
                    visualOrigin,
                    xBasis,
                    yBasis,
                    out ConstructionProjectionFrame frame),
                Is.True);
            return frame;
        }

        private static ConstructionPlacementTarget ResolveEdge(Vector3 pointer)
        {
            Assert.That(
                ConstructionPlacementProjection.TryResolve(
                    CreateIdentityFrame(new WorldCellCoordinate(0L, 0L)),
                    pointer,
                    ConstructionSpaceKind.Edge,
                    out ConstructionPlacementTarget target),
                Is.True);
            Assert.That(target.IsValid, Is.True);
            return target;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
