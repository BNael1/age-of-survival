using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Construction
{
    /// <summary>
    /// One canonical connected component within a bounded enclosure analysis scope.
    /// </summary>
    public sealed class ConstructionEnclosureRegion
    {
        private readonly IReadOnlyList<WorldCellCoordinate> _cells;

        internal ConstructionEnclosureRegion(
            IEnumerable<WorldCellCoordinate> cells,
            bool touchesAnalysisBoundary)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            var canonicalCells = new List<WorldCellCoordinate>(cells);
            if (canonicalCells.Count == 0)
            {
                throw new ArgumentException(
                    "An enclosure region must contain at least one cell.",
                    nameof(cells));
            }

            canonicalCells.Sort();
            _cells = canonicalCells.AsReadOnly();
            TouchesAnalysisBoundary = touchesAnalysisBoundary;
        }

        public IReadOnlyList<WorldCellCoordinate> Cells => _cells;
        public bool TouchesAnalysisBoundary { get; }
        public bool IsEnclosed => !TouchesAnalysisBoundary;
    }

    /// <summary>
    /// Immutable canonical result of one bounded enclosure analysis.
    /// </summary>
    public sealed class ConstructionEnclosureAnalysis
    {
        private readonly IReadOnlyList<ConstructionEnclosureRegion> _regions;

        internal ConstructionEnclosureAnalysis(
            IEnumerable<ConstructionEnclosureRegion> regions)
        {
            if (regions == null)
            {
                throw new ArgumentNullException(nameof(regions));
            }

            var canonicalRegions = new List<ConstructionEnclosureRegion>(regions);
            canonicalRegions.Sort(CompareRegions);
            _regions = canonicalRegions.AsReadOnly();
        }

        public IReadOnlyList<ConstructionEnclosureRegion> Regions => _regions;

        private static int CompareRegions(
            ConstructionEnclosureRegion left,
            ConstructionEnclosureRegion right)
        {
            if (left == null || right == null)
            {
                return left == null ? (right == null ? 0 : -1) : 1;
            }

            return left.Cells[0].CompareTo(right.Cells[0]);
        }
    }

    /// <summary>
    /// Pure bounded flood-fill over world cells and canonical blocking edges.
    /// It proves enclosure only inside the supplied scope and owns no persistent state.
    /// </summary>
    public static class ConstructionEnclosureAnalyzer
    {
        private static readonly ConstructionCellSide[] CardinalSides =
        {
            ConstructionCellSide.North,
            ConstructionCellSide.East,
            ConstructionCellSide.South,
            ConstructionCellSide.West
        };

        public static ConstructionEnclosureAnalysis Analyze(
            IEnumerable<WorldCellCoordinate> cells,
            IEnumerable<ConstructionEdgeAddress> blockingEdges)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (blockingEdges == null)
            {
                throw new ArgumentNullException(nameof(blockingEdges));
            }

            var scope = new HashSet<WorldCellCoordinate>(cells);
            var blockers = new HashSet<ConstructionEdgeAddress>();
            foreach (ConstructionEdgeAddress blockingEdge in blockingEdges)
            {
                if (!blockingEdge.IsValid)
                {
                    throw new ArgumentException(
                        "Blocking edges must contain only valid canonical addresses.",
                        nameof(blockingEdges));
                }

                blockers.Add(blockingEdge);
            }

            var canonicalCells = new List<WorldCellCoordinate>(scope);
            canonicalCells.Sort();

            var visited = new HashSet<WorldCellCoordinate>();
            var regions = new List<ConstructionEnclosureRegion>();
            for (int index = 0; index < canonicalCells.Count; index++)
            {
                WorldCellCoordinate seed = canonicalCells[index];
                if (!visited.Add(seed))
                {
                    continue;
                }

                regions.Add(AnalyzeRegion(seed, scope, blockers, visited));
            }

            return new ConstructionEnclosureAnalysis(regions);
        }

        private static ConstructionEnclosureRegion AnalyzeRegion(
            WorldCellCoordinate seed,
            ISet<WorldCellCoordinate> scope,
            ISet<ConstructionEdgeAddress> blockers,
            ISet<WorldCellCoordinate> visited)
        {
            var queue = new Queue<WorldCellCoordinate>();
            var cells = new List<WorldCellCoordinate>();
            bool touchesAnalysisBoundary = false;
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                WorldCellCoordinate cell = queue.Dequeue();
                cells.Add(cell);

                for (int sideIndex = 0; sideIndex < CardinalSides.Length; sideIndex++)
                {
                    if (!ConstructionEdgeAddress.TryCreate(
                            cell,
                            CardinalSides[sideIndex],
                            out ConstructionEdgeAddress edge))
                    {
                        touchesAnalysisBoundary = true;
                        continue;
                    }

                    if (blockers.Contains(edge))
                    {
                        continue;
                    }

                    WorldCellCoordinate neighbour = edge.FirstCell.Equals(cell)
                        ? edge.SecondCell
                        : edge.FirstCell;
                    if (!scope.Contains(neighbour))
                    {
                        touchesAnalysisBoundary = true;
                        continue;
                    }

                    if (visited.Add(neighbour))
                    {
                        queue.Enqueue(neighbour);
                    }
                }
            }

            return new ConstructionEnclosureRegion(cells, touchesAnalysisBoundary);
        }
    }
}
