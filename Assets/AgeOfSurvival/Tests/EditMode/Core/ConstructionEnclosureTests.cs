using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ConstructionEnclosureTests
    {
        private static readonly ConstructionCellSide[] Sides =
        {
            ConstructionCellSide.North,
            ConstructionCellSide.East,
            ConstructionCellSide.South,
            ConstructionCellSide.West
        };

        [Test]
        public void Analyze_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ConstructionEnclosureAnalyzer.Analyze(
                    null,
                    Array.Empty<ConstructionEdgeAddress>()));
            Assert.Throws<ArgumentNullException>(() =>
                ConstructionEnclosureAnalyzer.Analyze(
                    Array.Empty<WorldCellCoordinate>(),
                    null));
        }

        [Test]
        public void Analyze_RejectsInvalidBlockingEdge()
        {
            Assert.Throws<ArgumentException>(() =>
                ConstructionEnclosureAnalyzer.Analyze(
                    Array.Empty<WorldCellCoordinate>(),
                    new[] { default(ConstructionEdgeAddress) }));
        }

        [Test]
        public void EmptyScope_ProducesNoRegion()
        {
            ConstructionEnclosureAnalysis analysis = Analyze(
                Array.Empty<WorldCellCoordinate>(),
                Array.Empty<ConstructionEdgeAddress>());

            Assert.That(analysis.Regions, Is.Empty);
        }

        [Test]
        public void SingleCellWithoutBlockers_IsNotEnclosed()
        {
            ConstructionEnclosureRegion region = SingleRegion(
                new[] { Cell(0L, 0L) },
                Array.Empty<ConstructionEdgeAddress>());

            Assert.That(region.IsEnclosed, Is.False);
            Assert.That(region.TouchesAnalysisBoundary, Is.True);
        }

        [Test]
        public void SingleCellWithFourBlockingEdges_IsEnclosed()
        {
            WorldCellCoordinate cell = Cell(0L, 0L);

            ConstructionEnclosureRegion region = SingleRegion(
                new[] { cell },
                Perimeter(new[] { cell }));

            Assert.That(region.Cells, Is.EqualTo(new[] { cell }));
            Assert.That(region.IsEnclosed, Is.True);
            Assert.That(region.TouchesAnalysisBoundary, Is.False);
        }

        [Test]
        public void SingleCellWithOneMissingBlockingEdge_IsNotEnclosed()
        {
            WorldCellCoordinate cell = Cell(0L, 0L);
            var blockers = new List<ConstructionEdgeAddress>(Perimeter(new[] { cell }));
            blockers.Remove(Edge(cell, ConstructionCellSide.West));

            ConstructionEnclosureRegion region = SingleRegion(new[] { cell }, blockers);

            Assert.That(region.IsEnclosed, Is.False);
            Assert.That(region.TouchesAnalysisBoundary, Is.True);
        }

        [Test]
        public void AdjacentCellsWithOpenSharedEdgeAndClosedPerimeter_FormOneEnclosedRegion()
        {
            WorldCellCoordinate[] cells = { Cell(0L, 0L), Cell(1L, 0L) };

            ConstructionEnclosureRegion region = SingleRegion(cells, Perimeter(cells));

            Assert.That(region.Cells, Is.EqualTo(cells));
            Assert.That(region.IsEnclosed, Is.True);
        }

        [Test]
        public void AdjacentCellsWithBlockedSharedEdge_FormTwoEnclosedRegions()
        {
            WorldCellCoordinate[] cells = { Cell(0L, 0L), Cell(1L, 0L) };
            var blockers = new List<ConstructionEdgeAddress>(Perimeter(cells))
            {
                Edge(cells[0], ConstructionCellSide.East)
            };

            ConstructionEnclosureAnalysis analysis = Analyze(cells, blockers);

            Assert.That(analysis.Regions.Count, Is.EqualTo(2));
            AssertRegion(analysis.Regions[0], true, cells[0]);
            AssertRegion(analysis.Regions[1], true, cells[1]);
        }

        [Test]
        public void InternalBarrierWithAlternatePath_DoesNotSplitComponent()
        {
            WorldCellCoordinate[] cells =
            {
                Cell(0L, 0L), Cell(1L, 0L),
                Cell(0L, 1L), Cell(1L, 1L)
            };
            var blockers = new List<ConstructionEdgeAddress>(Perimeter(cells))
            {
                Edge(cells[0], ConstructionCellSide.East)
            };

            ConstructionEnclosureRegion region = SingleRegion(cells, blockers);

            Assert.That(region.Cells, Is.EqualTo(new[]
            {
                Cell(0L, 0L), Cell(1L, 0L), Cell(0L, 1L), Cell(1L, 1L)
            }));
            Assert.That(region.IsEnclosed, Is.True);
        }

        [Test]
        public void NegativeCoordinates_UseTheSameEnclosureRules()
        {
            WorldCellCoordinate[] cells = { Cell(-3L, -5L), Cell(-2L, -5L) };

            ConstructionEnclosureRegion region = SingleRegion(cells, Perimeter(cells));

            Assert.That(region.Cells, Is.EqualTo(cells));
            Assert.That(region.IsEnclosed, Is.True);
        }

        [Test]
        public void ChunkSeam31To32_DoesNotChangeAnalysis()
        {
            var layout = new ChunkLayout(32, 32);
            WorldCellCoordinate[] seamCells = { Cell(31L, 7L), Cell(32L, 7L) };
            WorldCellCoordinate[] ordinaryCells = { Cell(10L, 7L), Cell(11L, 7L) };

            Assert.That(ChunkAddressing.Locate(seamCells[0], layout).Chunk,
                Is.Not.EqualTo(ChunkAddressing.Locate(seamCells[1], layout).Chunk));
            AssertEquivalent(
                Analyze(ordinaryCells, Perimeter(ordinaryCells)),
                Analyze(seamCells, Perimeter(seamCells)),
                21L,
                0L);
        }

        [Test]
        public void ReversedCellOrder_ProducesIdenticalCanonicalResult()
        {
            WorldCellCoordinate[] cells = { Cell(2L, 0L), Cell(0L, 0L), Cell(1L, 0L) };
            ConstructionEdgeAddress[] blockers = Perimeter(cells);
            ConstructionEnclosureAnalysis expected = Analyze(cells, blockers);
            Array.Reverse(cells);

            AssertEquivalent(expected, Analyze(cells, blockers));
        }

        [Test]
        public void ReversedBlockerOrder_ProducesIdenticalCanonicalResult()
        {
            WorldCellCoordinate[] cells = { Cell(0L, 0L), Cell(1L, 0L) };
            ConstructionEdgeAddress[] blockers = Perimeter(cells);
            ConstructionEnclosureAnalysis expected = Analyze(cells, blockers);
            Array.Reverse(blockers);

            AssertEquivalent(expected, Analyze(cells, blockers));
        }

        [Test]
        public void DuplicateInputs_DoNotChangeResult()
        {
            WorldCellCoordinate[] cells = { Cell(0L, 0L), Cell(1L, 0L) };
            ConstructionEdgeAddress[] blockers = Perimeter(cells);
            var duplicateCells = new List<WorldCellCoordinate>(cells) { cells[0], cells[1] };
            var duplicateBlockers = new List<ConstructionEdgeAddress>(blockers)
            {
                blockers[0], blockers[blockers.Length - 1]
            };

            AssertEquivalent(
                Analyze(cells, blockers),
                Analyze(duplicateCells, duplicateBlockers));
        }

        [Test]
        public void BlockingEdgeTowardCellOutsideScope_ClosesThatExit()
        {
            WorldCellCoordinate cell = Cell(4L, 9L);
            ConstructionEdgeAddress east = Edge(cell, ConstructionCellSide.East);
            ConstructionEdgeAddress[] blockers =
            {
                Edge(cell, ConstructionCellSide.North), east,
                Edge(cell, ConstructionCellSide.South),
                Edge(cell, ConstructionCellSide.West)
            };

            ConstructionEnclosureRegion region = SingleRegion(new[] { cell }, blockers);

            Assert.That(east.SecondCell, Is.Not.EqualTo(cell));
            Assert.That(region.IsEnclosed, Is.True);
            Assert.That(region.TouchesAnalysisBoundary, Is.False);
        }

        [Test]
        public void UnblockedExitOutsideScope_PreventsEnclosure()
        {
            WorldCellCoordinate[] cells = { Cell(0L, 0L), Cell(1L, 0L) };
            var blockers = new List<ConstructionEdgeAddress>(Perimeter(cells));
            blockers.Remove(Edge(cells[1], ConstructionCellSide.East));

            ConstructionEnclosureRegion region = SingleRegion(cells, blockers);

            Assert.That(region.IsEnclosed, Is.False);
            Assert.That(region.TouchesAnalysisBoundary, Is.True);
        }

        [Test]
        public void Int64Bounds_AreUnresolvedWithoutOverflowOrWrap()
        {
            WorldCellCoordinate minimum = Cell(long.MinValue, long.MinValue);
            WorldCellCoordinate maximum = Cell(long.MaxValue, long.MaxValue);
            ConstructionEdgeAddress[] blockers =
            {
                Edge(minimum, ConstructionCellSide.North),
                Edge(minimum, ConstructionCellSide.East),
                Edge(maximum, ConstructionCellSide.South),
                Edge(maximum, ConstructionCellSide.West)
            };

            ConstructionEnclosureAnalysis analysis = Analyze(
                new[] { maximum, minimum },
                blockers);

            Assert.That(analysis.Regions.Count, Is.EqualTo(2));
            AssertRegion(analysis.Regions[0], false, minimum);
            AssertRegion(analysis.Regions[1], false, maximum);
        }

        [Test]
        public void ClosedAndUnresolvedRegions_CoexistInCanonicalOrder()
        {
            WorldCellCoordinate closedFirst = Cell(-10L, -4L);
            WorldCellCoordinate unresolved = Cell(0L, 0L);
            WorldCellCoordinate closedLast = Cell(12L, 8L);
            var blockers = new List<ConstructionEdgeAddress>();
            blockers.AddRange(Perimeter(new[] { closedFirst }));
            blockers.AddRange(Perimeter(new[] { closedLast }));

            ConstructionEnclosureAnalysis analysis = Analyze(
                new[] { closedLast, unresolved, closedFirst },
                blockers);

            Assert.That(analysis.Regions.Count, Is.EqualTo(3));
            AssertRegion(analysis.Regions[0], true, closedFirst);
            AssertRegion(analysis.Regions[1], false, unresolved);
            AssertRegion(analysis.Regions[2], true, closedLast);
        }

        [Test]
        public void PublicCollections_AreReadOnly()
        {
            WorldCellCoordinate cell = Cell(0L, 0L);
            ConstructionEnclosureAnalysis analysis = Analyze(
                new[] { cell },
                Perimeter(new[] { cell }));

            Assert.Throws<NotSupportedException>(() =>
                ((IList<ConstructionEnclosureRegion>)analysis.Regions).Add(
                    analysis.Regions[0]));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<WorldCellCoordinate>)analysis.Regions[0].Cells).Add(Cell(1L, 0L)));
        }

        private static ConstructionEnclosureAnalysis Analyze(
            IEnumerable<WorldCellCoordinate> cells,
            IEnumerable<ConstructionEdgeAddress> blockers)
        {
            return ConstructionEnclosureAnalyzer.Analyze(cells, blockers);
        }

        private static ConstructionEnclosureRegion SingleRegion(
            IEnumerable<WorldCellCoordinate> cells,
            IEnumerable<ConstructionEdgeAddress> blockers)
        {
            ConstructionEnclosureAnalysis analysis = Analyze(cells, blockers);
            Assert.That(analysis.Regions.Count, Is.EqualTo(1));
            return analysis.Regions[0];
        }

        private static ConstructionEdgeAddress[] Perimeter(
            IEnumerable<WorldCellCoordinate> cells)
        {
            var scope = new HashSet<WorldCellCoordinate>(cells);
            var perimeter = new HashSet<ConstructionEdgeAddress>();
            foreach (WorldCellCoordinate cell in scope)
            {
                for (int index = 0; index < Sides.Length; index++)
                {
                    ConstructionEdgeAddress edge = Edge(cell, Sides[index]);
                    WorldCellCoordinate neighbour = edge.FirstCell.Equals(cell)
                        ? edge.SecondCell
                        : edge.FirstCell;
                    if (!scope.Contains(neighbour))
                    {
                        perimeter.Add(edge);
                    }
                }
            }

            var result = new List<ConstructionEdgeAddress>(perimeter);
            result.Sort();
            return result.ToArray();
        }

        private static ConstructionEdgeAddress Edge(
            WorldCellCoordinate cell,
            ConstructionCellSide side)
        {
            return ConstructionEdgeAddress.Create(cell, side);
        }

        private static WorldCellCoordinate Cell(long x, long y)
        {
            return new WorldCellCoordinate(x, y);
        }

        private static void AssertRegion(
            ConstructionEnclosureRegion region,
            bool enclosed,
            params WorldCellCoordinate[] cells)
        {
            Assert.That(region.Cells, Is.EqualTo(cells));
            Assert.That(region.IsEnclosed, Is.EqualTo(enclosed));
            Assert.That(region.TouchesAnalysisBoundary, Is.EqualTo(!enclosed));
        }

        private static void AssertEquivalent(
            ConstructionEnclosureAnalysis expected,
            ConstructionEnclosureAnalysis actual,
            long actualOffsetX = 0L,
            long actualOffsetY = 0L)
        {
            Assert.That(actual.Regions.Count, Is.EqualTo(expected.Regions.Count));
            for (int regionIndex = 0; regionIndex < expected.Regions.Count; regionIndex++)
            {
                ConstructionEnclosureRegion expectedRegion = expected.Regions[regionIndex];
                ConstructionEnclosureRegion actualRegion = actual.Regions[regionIndex];
                Assert.That(actualRegion.IsEnclosed, Is.EqualTo(expectedRegion.IsEnclosed));
                Assert.That(actualRegion.TouchesAnalysisBoundary,
                    Is.EqualTo(expectedRegion.TouchesAnalysisBoundary));
                Assert.That(actualRegion.Cells.Count, Is.EqualTo(expectedRegion.Cells.Count));
                for (int cellIndex = 0; cellIndex < expectedRegion.Cells.Count; cellIndex++)
                {
                    Assert.That(
                        actualRegion.Cells[cellIndex],
                        Is.EqualTo(expectedRegion.Cells[cellIndex].Offset(
                            actualOffsetX,
                            actualOffsetY)));
                }
            }
        }
    }
}
