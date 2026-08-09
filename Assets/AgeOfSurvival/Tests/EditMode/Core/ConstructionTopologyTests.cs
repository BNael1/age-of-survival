using System;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ConstructionTopologyTests
    {
        private static readonly ChunkLayout Layout = new ChunkLayout(32, 32);

        [Test]
        public void DefaultEdge_IsInvalidAndCannotBeUsed()
        {
            ConstructionEdgeAddress edge = default;

            Assert.That(edge.IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => edge.GetOwningChunk(Layout));
            Assert.Throws<InvalidOperationException>(() => { _ = edge.FirstCell; });
        }

        [TestCase(0L, 0L, ConstructionCellSide.East, 1L, 0L, ConstructionCellSide.West)]
        [TestCase(0L, 0L, ConstructionCellSide.North, 0L, 1L, ConstructionCellSide.South)]
        [TestCase(-1L, -1L, ConstructionCellSide.East, 0L, -1L, ConstructionCellSide.West)]
        [TestCase(-4L, -7L, ConstructionCellSide.North, -4L, -6L, ConstructionCellSide.South)]
        public void EdgeAliases_CollapseToSameCanonicalAddress(
            long firstX,
            long firstY,
            ConstructionCellSide firstSide,
            long secondX,
            long secondY,
            ConstructionCellSide secondSide)
        {
            ConstructionEdgeAddress first = ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(firstX, firstY),
                firstSide);
            ConstructionEdgeAddress second = ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(secondX, secondY),
                secondSide);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [TestCase(0L, 0L, ConstructionCellSide.North, 0L, 0L, ConstructionEdgeAxis.Horizontal)]
        [TestCase(0L, 0L, ConstructionCellSide.East, 0L, 0L, ConstructionEdgeAxis.Vertical)]
        [TestCase(0L, 0L, ConstructionCellSide.South, 0L, -1L, ConstructionEdgeAxis.Horizontal)]
        [TestCase(0L, 0L, ConstructionCellSide.West, -1L, 0L, ConstructionEdgeAxis.Vertical)]
        public void EdgeAddress_UsesSouthOrWestCanonicalAnchor(
            long cellX,
            long cellY,
            ConstructionCellSide side,
            long expectedAnchorX,
            long expectedAnchorY,
            ConstructionEdgeAxis expectedAxis)
        {
            ConstructionEdgeAddress edge = ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(cellX, cellY),
                side);

            Assert.That(edge.IsValid, Is.True);
            Assert.That(
                edge.AnchorCell,
                Is.EqualTo(new WorldCellCoordinate(expectedAnchorX, expectedAnchorY)));
            Assert.That(edge.Axis, Is.EqualTo(expectedAxis));
        }

        [Test]
        public void EdgeAddress_RejectsUnknownSide()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ConstructionEdgeAddress.Create(
                    new WorldCellCoordinate(0L, 0L),
                    (ConstructionCellSide)99));
        }

        [TestCase(long.MaxValue, 0L, ConstructionCellSide.East)]
        [TestCase(0L, long.MaxValue, ConstructionCellSide.North)]
        [TestCase(long.MinValue, 0L, ConstructionCellSide.West)]
        [TestCase(0L, long.MinValue, ConstructionCellSide.South)]
        public void EdgeAddress_RejectsNeighborOutsideInt64World(
            long x,
            long y,
            ConstructionCellSide side)
        {
            Assert.Throws<OverflowException>(() =>
                ConstructionEdgeAddress.Create(new WorldCellCoordinate(x, y), side));
        }

        [Test]
        public void EdgeAddress_ExposesBothAdjacentCells()
        {
            ConstructionEdgeAddress edge = ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(-33L, 7L),
                ConstructionCellSide.East);

            Assert.That(edge.FirstCell, Is.EqualTo(new WorldCellCoordinate(-33L, 7L)));
            Assert.That(edge.SecondCell, Is.EqualTo(new WorldCellCoordinate(-32L, 7L)));
        }

        [Test]
        public void DefaultConstructionSpace_IsInvalidAndRejectedByRegistry()
        {
            ConstructionSpaceKey space = default;
            var registry = new ConstructionOccupancyRegistry();

            Assert.That(space.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => registry.TryOccupy(space));
        }

        [Test]
        public void SurfaceInteriorAndRoof_AreIndependentSpacesOnSameCell()
        {
            var cell = new WorldCellCoordinate(12L, -9L);
            ConstructionSpaceKey surface = ConstructionSpaceKey.Surface(cell);
            ConstructionSpaceKey interior = ConstructionSpaceKey.Interior(cell);
            ConstructionSpaceKey roof = ConstructionSpaceKey.Roof(cell);
            var registry = new ConstructionOccupancyRegistry();

            Assert.That(surface, Is.Not.EqualTo(interior));
            Assert.That(surface, Is.Not.EqualTo(roof));
            Assert.That(interior, Is.Not.EqualTo(roof));
            Assert.That(registry.TryOccupy(surface), Is.True);
            Assert.That(registry.TryOccupy(interior), Is.True);
            Assert.That(registry.TryOccupy(roof), Is.True);
            Assert.That(registry.Count, Is.EqualTo(3));
        }

        [Test]
        public void EdgeSpace_UsesCanonicalEdgeIdentity()
        {
            ConstructionSpaceKey east = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(31L, 5L),
                ConstructionCellSide.East);
            ConstructionSpaceKey west = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(32L, 5L),
                ConstructionCellSide.West);

            Assert.That(east, Is.EqualTo(west));
            Assert.That(east.EdgeAddress.AnchorCell, Is.EqualTo(new WorldCellCoordinate(31L, 5L)));
        }

        [Test]
        public void NonEdgeSpace_DoesNotExposeEdgeAddress()
        {
            ConstructionSpaceKey surface = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(0L, 0L));

            Assert.Throws<InvalidOperationException>(() => { _ = surface.EdgeAddress; });
        }

        [TestCase(0L, 0L, 0L, 0L)]
        [TestCase(31L, 31L, 0L, 0L)]
        [TestCase(32L, 0L, 1L, 0L)]
        [TestCase(-1L, 0L, -1L, 0L)]
        [TestCase(-33L, -65L, -2L, -3L)]
        public void CellSpaces_AreOwnedByContainingChunk(
            long x,
            long y,
            long expectedChunkX,
            long expectedChunkY)
        {
            ConstructionSpaceKey space = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(x, y));

            Assert.That(
                space.GetOwningChunk(Layout),
                Is.EqualTo(new ChunkCoordinate(expectedChunkX, expectedChunkY)));
        }

        [TestCase(31L, 5L, ConstructionCellSide.East, 32L, 5L, ConstructionCellSide.West, 0L, 0L)]
        [TestCase(-1L, 5L, ConstructionCellSide.East, 0L, 5L, ConstructionCellSide.West, -1L, 0L)]
        [TestCase(5L, 31L, ConstructionCellSide.North, 5L, 32L, ConstructionCellSide.South, 0L, 0L)]
        [TestCase(5L, -1L, ConstructionCellSide.North, 5L, 0L, ConstructionCellSide.South, 0L, -1L)]
        public void EdgeOwnership_IsStableAcrossChunkSeams(
            long firstX,
            long firstY,
            ConstructionCellSide firstSide,
            long secondX,
            long secondY,
            ConstructionCellSide secondSide,
            long expectedChunkX,
            long expectedChunkY)
        {
            ConstructionEdgeAddress first = ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(firstX, firstY),
                firstSide);
            ConstructionEdgeAddress second = ConstructionEdgeAddress.Create(
                new WorldCellCoordinate(secondX, secondY),
                secondSide);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(
                first.GetOwningChunk(Layout),
                Is.EqualTo(new ChunkCoordinate(expectedChunkX, expectedChunkY)));
            Assert.That(second.GetOwningChunk(Layout), Is.EqualTo(first.GetOwningChunk(Layout)));
        }

        [Test]
        public void OccupancyRegistry_RejectsDuplicateOccupationWithoutMutation()
        {
            var registry = new ConstructionOccupancyRegistry();
            ConstructionSpaceKey space = ConstructionSpaceKey.Interior(
                new WorldCellCoordinate(4L, 8L));

            Assert.That(registry.TryOccupy(space), Is.True);
            Assert.That(registry.TryOccupy(space), Is.False);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.IsOccupied(space), Is.True);
        }

        [Test]
        public void OccupancyRegistry_VacateIsExplicitAndIdempotent()
        {
            var registry = new ConstructionOccupancyRegistry();
            ConstructionSpaceKey space = ConstructionSpaceKey.Roof(
                new WorldCellCoordinate(-4L, -8L));
            registry.TryOccupy(space);

            Assert.That(registry.TryVacate(space), Is.True);
            Assert.That(registry.TryVacate(space), Is.False);
            Assert.That(registry.IsOccupied(space), Is.False);
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        public void CaptureCanonicalSpaces_IsIndependentOfInsertionOrder()
        {
            ConstructionSpaceKey[] expected =
            {
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(-1L, -1L)),
                ConstructionSpaceKey.Interior(new WorldCellCoordinate(0L, 0L)),
                ConstructionSpaceKey.Edge(new WorldCellCoordinate(0L, 0L), ConstructionCellSide.North),
                ConstructionSpaceKey.Roof(new WorldCellCoordinate(0L, 0L)),
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(1L, 0L))
            };
            Array.Sort(expected);

            var registry = new ConstructionOccupancyRegistry();
            registry.TryOccupy(expected[4]);
            registry.TryOccupy(expected[1]);
            registry.TryOccupy(expected[3]);
            registry.TryOccupy(expected[0]);
            registry.TryOccupy(expected[2]);

            Assert.That(registry.CaptureCanonicalSpaces(), Is.EqualTo(expected));
        }
    }
}
