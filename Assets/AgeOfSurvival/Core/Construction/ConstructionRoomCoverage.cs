using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Construction
{
    public readonly struct ConstructionCoverageRatio : IEquatable<ConstructionCoverageRatio>
    {
        public ConstructionCoverageRatio(int coveredCells, int totalCells)
        {
            if (totalCells <= 0) throw new ArgumentOutOfRangeException(nameof(totalCells));
            if (coveredCells < 0 || coveredCells > totalCells) throw new ArgumentOutOfRangeException(nameof(coveredCells));
            CoveredCells = coveredCells;
            TotalCells = totalCells;
        }

        public int CoveredCells { get; }
        public int TotalCells { get; }
        public bool IsComplete => CoveredCells == TotalCells;
        public int CompareTo(ConstructionCoverageRatio other)
        {
            long left = (long)CoveredCells * other.TotalCells;
            long right = (long)other.CoveredCells * TotalCells;
            return left.CompareTo(right);
        }
        public bool Equals(ConstructionCoverageRatio other) => CoveredCells == other.CoveredCells && TotalCells == other.TotalCells;
        public override bool Equals(object obj) => obj is ConstructionCoverageRatio other && Equals(other);
        public override int GetHashCode() => (CoveredCells * 397) ^ TotalCells;
        public override string ToString() => $"{CoveredCells}/{TotalCells}";
    }

    public sealed class ConstructionRoomCoverage
    {
        internal ConstructionRoomCoverage(
            ConstructionRoom room,
            int completedRoofCellCount,
            int supportedRoofCellCount)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
            if (completedRoofCellCount < 0 || completedRoofCellCount > room.Cells.Count)
                throw new ArgumentOutOfRangeException(nameof(completedRoofCellCount));
            if (supportedRoofCellCount < 0 || supportedRoofCellCount > completedRoofCellCount)
                throw new ArgumentOutOfRangeException(nameof(supportedRoofCellCount));
            CompletedRoofCellCount = completedRoofCellCount;
            SupportedRoofCellCount = supportedRoofCellCount;
            SupportedCoverage = new ConstructionCoverageRatio(supportedRoofCellCount, room.Cells.Count);
        }

        public ConstructionRoom Room { get; }
        public int CellCount => Room.Cells.Count;
        public int CompletedRoofCellCount { get; }
        public int SupportedRoofCellCount { get; }
        public ConstructionCoverageRatio SupportedCoverage { get; }
        public bool IsFullySupportedRoofed => SupportedCoverage.IsComplete;
    }

    public sealed class ConstructionDerivedRooms
    {
        internal ConstructionDerivedRooms(
            IEnumerable<ConstructionEdgeAddress> blockingEdges,
            IEnumerable<ConstructionRoomCoverage> rooms, int scopesAnalyzed = 0, long cellsAnalyzed = 0)
        {
            ScopesAnalyzed = scopesAnalyzed;
            CellsAnalyzed = cellsAnalyzed;
            BlockingEdges = new List<ConstructionEdgeAddress>(blockingEdges).AsReadOnly();
            var copy = new List<ConstructionRoomCoverage>(rooms);
            copy.Sort((left, right) => left.Room.CanonicalCell.CompareTo(right.Room.CanonicalCell));
            Rooms = copy.AsReadOnly();
        }

        public IReadOnlyList<ConstructionEdgeAddress> BlockingEdges { get; }
        public IReadOnlyList<ConstructionRoomCoverage> Rooms { get; }
        public int ScopesAnalyzed { get; }
        public long CellsAnalyzed { get; }
    }

    public static class ConstructionDerivedRoomBuilder
    {
        public static ConstructionDerivedRooms Build(
            ConstructionDefinitionCatalog catalog,
            IEnumerable<CompletedStructureState> structures,
            ConstructionEnclosureBlockingPolicy enclosurePolicy,
            ConstructionRoomAnalysisLimits limits,
            ConstructionRoofSupportPolicy roofSupportPolicy,
            ConstructionDoorRegistry doors = null,
            IEnumerable<ConstructionEdgeAddress> affectedEdges = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (structures == null) throw new ArgumentNullException(nameof(structures));
            if (enclosurePolicy == null) throw new ArgumentNullException(nameof(enclosurePolicy));
            if (roofSupportPolicy == null) throw new ArgumentNullException(nameof(roofSupportPolicy));

            var completed = new List<CompletedStructureState>();
            foreach (CompletedStructureState structure in structures) completed.Add(structure);
            IReadOnlyList<ConstructionEdgeAddress> blockers =
                ConstructionEnclosureBlockingEdgeBuilder.Build(catalog, completed, enclosurePolicy, doors);
            ConstructionRoomAnalysis analysis = ConstructionRoomAnalyzer.AnalyzeAffected(blockers, affectedEdges, limits);

            ConstructionSupportEvaluation support =
                ConstructionRoofSupportGraphBuilder.Build(catalog, completed, roofSupportPolicy).Evaluate();
            var roofsByCell = new Dictionary<WorldCellCoordinate, ConstructionRoofSupportState>();
            for (int i = 0; i < completed.Count; i++)
            {
                CompletedStructureState structure = completed[i];
                if (structure.Space.Kind != ConstructionSpaceKind.Roof) continue;
                roofsByCell.Add(
                    structure.Space.AnchorCell,
                    support.RequireRoofState(structure.InstanceId));
            }

            var result = new List<ConstructionRoomCoverage>();
            for (int i = 0; i < analysis.Rooms.Count; i++)
            {
                ConstructionRoom room = analysis.Rooms[i];
                int completedCount = 0;
                int supportedCount = 0;
                for (int c = 0; c < room.Cells.Count; c++)
                {
                    if (!roofsByCell.TryGetValue(room.Cells[c], out ConstructionRoofSupportState roof)) continue;
                    completedCount++;
                    if (roof.CountsTowardShelter) supportedCount++;
                }
                result.Add(new ConstructionRoomCoverage(room, completedCount, supportedCount));
            }

            return new ConstructionDerivedRooms(blockers, result, analysis.ScopesAnalyzed, analysis.CellsAnalyzed);
        }
    }
}
