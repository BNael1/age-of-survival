using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Construction
{
    public sealed class ConstructionEnclosureBlockingPolicy
    {
        private readonly IReadOnlyList<ConstructionDefinitionId> _definitionIds;

        public ConstructionEnclosureBlockingPolicy(
            IEnumerable<ConstructionDefinitionId> blockingEdgeDefinitionIds)
        {
            if (blockingEdgeDefinitionIds == null)
                throw new ArgumentNullException(nameof(blockingEdgeDefinitionIds));

            var ids = new List<ConstructionDefinitionId>();
            var unique = new HashSet<ConstructionDefinitionId>();
            foreach (ConstructionDefinitionId id in blockingEdgeDefinitionIds)
            {
                if (!id.IsValid)
                    throw new ArgumentException("Blocking definition identifiers must be valid.", nameof(blockingEdgeDefinitionIds));
                if (!unique.Add(id))
                    throw new ArgumentException("Blocking definition identifiers must be unique.", nameof(blockingEdgeDefinitionIds));
                ids.Add(id);
            }

            ids.Sort();
            _definitionIds = ids.AsReadOnly();
        }

        public IReadOnlyList<ConstructionDefinitionId> BlockingEdgeDefinitionIds => _definitionIds;
    }

    public static class ConstructionEnclosureBlockingEdgeBuilder
    {
        public static IReadOnlyList<ConstructionEdgeAddress> Build(
            ConstructionDefinitionCatalog catalog,
            IEnumerable<CompletedStructureState> structures,
            ConstructionEnclosureBlockingPolicy policy)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (structures == null) throw new ArgumentNullException(nameof(structures));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            var blockingIds = new HashSet<ConstructionDefinitionId>();
            for (int i = 0; i < policy.BlockingEdgeDefinitionIds.Count; i++)
            {
                ConstructionDefinitionId id = policy.BlockingEdgeDefinitionIds[i];
                ConstructionDefinition definition = catalog.Require(id);
                if (definition.SpaceKind != ConstructionSpaceKind.Edge)
                    throw new ArgumentException($"Blocking definition '{id}' must occupy Edge space.", nameof(policy));
                blockingIds.Add(id);
            }

            var identities = new HashSet<ConstructionInstanceId>();
            var spaces = new HashSet<ConstructionSpaceKey>();
            var blockers = new HashSet<ConstructionEdgeAddress>();
            foreach (CompletedStructureState structure in structures)
            {
                if (structure == null)
                    throw new ArgumentException("Completed structures must not contain null entries.", nameof(structures));
                if (!structure.InstanceId.IsValid || !structure.DefinitionId.IsValid || !structure.Space.IsValid)
                    throw new ArgumentException("Completed structures must be valid.", nameof(structures));
                if (!identities.Add(structure.InstanceId))
                    throw new ArgumentException("Completed structure identifiers must be unique.", nameof(structures));
                if (!spaces.Add(structure.Space))
                    throw new ArgumentException("Completed structure occupations must be unique.", nameof(structures));

                ConstructionDefinition definition = catalog.Require(structure.DefinitionId);
                if (definition.SpaceKind != structure.Space.Kind)
                    throw new ArgumentException("Completed structure space must match its definition.", nameof(structures));
                if (blockingIds.Contains(structure.DefinitionId))
                    blockers.Add(structure.Space.EdgeAddress);
            }

            var result = new List<ConstructionEdgeAddress>(blockers);
            result.Sort();
            return result.AsReadOnly();
        }
    }

    public readonly struct ConstructionRoomAnalysisLimits
    {
        public ConstructionRoomAnalysisLimits(int maximumScopes, int maximumCellsPerScope)
        {
            if (maximumScopes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumScopes));
            if (maximumCellsPerScope <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCellsPerScope));
            MaximumScopes = maximumScopes;
            MaximumCellsPerScope = maximumCellsPerScope;
        }

        public int MaximumScopes { get; }
        public int MaximumCellsPerScope { get; }
        public static ConstructionRoomAnalysisLimits Default => new ConstructionRoomAnalysisLimits(4096, 1_048_576);
    }

    public sealed class ConstructionRoom
    {
        private readonly IReadOnlyList<WorldCellCoordinate> _cells;
        private readonly ISet<WorldCellCoordinate> _cellSet;

        internal ConstructionRoom(IEnumerable<WorldCellCoordinate> cells)
        {
            var copy = new List<WorldCellCoordinate>(cells ?? throw new ArgumentNullException(nameof(cells)));
            if (copy.Count == 0) throw new ArgumentException("A room requires cells.", nameof(cells));
            copy.Sort();
            _cells = copy.AsReadOnly();
            _cellSet = new HashSet<WorldCellCoordinate>(copy);
        }

        public IReadOnlyList<WorldCellCoordinate> Cells => _cells;
        public WorldCellCoordinate CanonicalCell => _cells[0];
        public bool Contains(WorldCellCoordinate cell) => _cellSet.Contains(cell);
    }

    public sealed class ConstructionRoomAnalysis
    {
        internal ConstructionRoomAnalysis(IEnumerable<ConstructionRoom> rooms)
        {
            var copy = new List<ConstructionRoom>(rooms ?? throw new ArgumentNullException(nameof(rooms)));
            copy.Sort((left, right) => left.CanonicalCell.CompareTo(right.CanonicalCell));
            Rooms = copy.AsReadOnly();
        }

        public IReadOnlyList<ConstructionRoom> Rooms { get; }
    }

    /// <summary>
    /// Builds finite scopes from connected blocker components. Each scope is the component bounds,
    /// including both cells incident to every edge. Overlapping scopes are merged. Hard limits are
    /// checked before allocating cells, so distant structures never imply a world-sized rectangle.
    /// </summary>
    public static class ConstructionRoomAnalyzer
    {
        public static ConstructionRoomAnalysis Analyze(
            IEnumerable<ConstructionEdgeAddress> blockingEdges,
            ConstructionRoomAnalysisLimits limits)
        {
            return AnalyzeAffected(blockingEdges, null, limits);
        }

        public static ConstructionRoomAnalysis AnalyzeAffected(
            IEnumerable<ConstructionEdgeAddress> blockingEdges,
            IEnumerable<ConstructionEdgeAddress> affectedEdges,
            ConstructionRoomAnalysisLimits limits)
        {
            if (blockingEdges == null) throw new ArgumentNullException(nameof(blockingEdges));
            if (limits.MaximumScopes <= 0 || limits.MaximumCellsPerScope <= 0)
                throw new ArgumentException("Valid room analysis limits are required.", nameof(limits));

            var blockers = new HashSet<ConstructionEdgeAddress>();
            foreach (ConstructionEdgeAddress edge in blockingEdges)
            {
                if (!edge.IsValid) throw new ArgumentException("Blocking edges must be valid.", nameof(blockingEdges));
                blockers.Add(edge);
            }

            var affected = new List<ConstructionEdgeAddress>();
            if (affectedEdges != null)
            {
                foreach (ConstructionEdgeAddress edge in affectedEdges)
                {
                    if (!edge.IsValid) throw new ArgumentException("Affected edges must be valid.", nameof(affectedEdges));
                    affected.Add(edge);
                }
            }

            List<Scope> scopes = BuildScopes(blockers, limits.MaximumScopes);
            if (affectedEdges != null)
                scopes.RemoveAll(scope => !scope.IntersectsAny(affected));

            var rooms = new List<ConstructionRoom>();
            for (int i = 0; i < scopes.Count; i++)
            {
                Scope scope = scopes[i];
                IReadOnlyList<WorldCellCoordinate> cells = scope.CreateCells(limits.MaximumCellsPerScope);
                ConstructionEnclosureAnalysis analysis = ConstructionEnclosureAnalyzer.Analyze(cells, blockers);
                for (int r = 0; r < analysis.Regions.Count; r++)
                {
                    ConstructionEnclosureRegion region = analysis.Regions[r];
                    if (!region.IsEnclosed) continue;
                    rooms.Add(new ConstructionRoom(region.Cells));
                }
            }

            return new ConstructionRoomAnalysis(rooms);
        }

        private static List<Scope> BuildScopes(ISet<ConstructionEdgeAddress> blockers, int maximumScopes)
        {
            var edgesByVertex = new Dictionary<GridVertex, List<ConstructionEdgeAddress>>();
            foreach (ConstructionEdgeAddress edge in blockers)
            {
                AddEdgeAtVertex(edgesByVertex, GridVertex.First(edge), edge);
                AddEdgeAtVertex(edgesByVertex, GridVertex.Second(edge), edge);
            }

            var remaining = new HashSet<ConstructionEdgeAddress>(blockers);
            var scopes = new List<Scope>();
            while (remaining.Count > 0)
            {
                ConstructionEdgeAddress seed = default;
                foreach (ConstructionEdgeAddress edge in remaining) { seed = edge; break; }
                remaining.Remove(seed);
                var queue = new Queue<ConstructionEdgeAddress>();
                var incidentCells = new HashSet<WorldCellCoordinate>();
                queue.Enqueue(seed);
                incidentCells.Add(seed.FirstCell);
                incidentCells.Add(seed.SecondCell);

                while (queue.Count > 0)
                {
                    ConstructionEdgeAddress edge = queue.Dequeue();
                    EnqueueConnected(edgesByVertex[GridVertex.First(edge)], remaining, queue, incidentCells);
                    EnqueueConnected(edgesByVertex[GridVertex.Second(edge)], remaining, queue, incidentCells);
                }

                scopes.Add(Scope.FromCells(incidentCells));
            }

            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < scopes.Count && !merged; i++)
                    for (int j = i + 1; j < scopes.Count; j++)
                        if (scopes[i].Intersects(scopes[j]))
                        {
                            scopes[i] = scopes[i].Union(scopes[j]);
                            scopes.RemoveAt(j);
                            merged = true;
                            break;
                        }
            } while (merged);

            if (scopes.Count > maximumScopes)
                throw new InvalidOperationException(
                    "Room analysis exceeded the configured scope count.");

            scopes.Sort((left, right) => left.Min.CompareTo(right.Min));
            return scopes;
        }

        private static void AddEdgeAtVertex(
            IDictionary<GridVertex, List<ConstructionEdgeAddress>> edgesByVertex,
            GridVertex vertex,
            ConstructionEdgeAddress edge)
        {
            if (!edgesByVertex.TryGetValue(vertex, out List<ConstructionEdgeAddress> edges))
            {
                edges = new List<ConstructionEdgeAddress>();
                edgesByVertex.Add(vertex, edges);
            }
            edges.Add(edge);
        }

        private static void EnqueueConnected(
            IReadOnlyList<ConstructionEdgeAddress> candidates,
            ISet<ConstructionEdgeAddress> remaining,
            Queue<ConstructionEdgeAddress> queue,
            ISet<WorldCellCoordinate> incidentCells)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                ConstructionEdgeAddress candidate = candidates[index];
                if (!remaining.Remove(candidate)) continue;
                incidentCells.Add(candidate.FirstCell);
                incidentCells.Add(candidate.SecondCell);
                queue.Enqueue(candidate);
            }
        }

        private readonly struct GridVertex : IEquatable<GridVertex>
        {
            private GridVertex(decimal x, decimal y) { X = x; Y = y; }
            private decimal X { get; }
            private decimal Y { get; }

            public static GridVertex First(ConstructionEdgeAddress edge)
            {
                WorldCellCoordinate anchor = edge.AnchorCell;
                return edge.Axis == ConstructionEdgeAxis.Horizontal
                    ? new GridVertex(anchor.X, (decimal)anchor.Y + 1m)
                    : new GridVertex((decimal)anchor.X + 1m, anchor.Y);
            }

            public static GridVertex Second(ConstructionEdgeAddress edge)
            {
                WorldCellCoordinate anchor = edge.AnchorCell;
                return edge.Axis == ConstructionEdgeAxis.Horizontal
                    ? new GridVertex((decimal)anchor.X + 1m, (decimal)anchor.Y + 1m)
                    : new GridVertex((decimal)anchor.X + 1m, (decimal)anchor.Y + 1m);
            }

            public bool Equals(GridVertex other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is GridVertex other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        private readonly struct Scope
        {
            public Scope(WorldCellCoordinate min, WorldCellCoordinate max) { Min = min; Max = max; }
            public WorldCellCoordinate Min { get; }
            public WorldCellCoordinate Max { get; }

            public static Scope FromCells(IEnumerable<WorldCellCoordinate> cells)
            {
                bool any = false; long minX = 0, maxX = 0, minY = 0, maxY = 0;
                foreach (WorldCellCoordinate cell in cells)
                {
                    if (!any) { minX = maxX = cell.X; minY = maxY = cell.Y; any = true; }
                    else { minX = Math.Min(minX, cell.X); maxX = Math.Max(maxX, cell.X); minY = Math.Min(minY, cell.Y); maxY = Math.Max(maxY, cell.Y); }
                }
                if (!any) throw new ArgumentException("A scope requires cells.", nameof(cells));
                return new Scope(new WorldCellCoordinate(minX, minY), new WorldCellCoordinate(maxX, maxY));
            }

            public bool Intersects(Scope other) =>
                Min.X <= other.Max.X && other.Min.X <= Max.X && Min.Y <= other.Max.Y && other.Min.Y <= Max.Y;
            public Scope Union(Scope other) => new Scope(
                new WorldCellCoordinate(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y)),
                new WorldCellCoordinate(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y)));
            public bool IntersectsAny(IReadOnlyList<ConstructionEdgeAddress> edges)
            {
                for (int i = 0; i < edges.Count; i++)
                    if (Contains(edges[i].FirstCell) || Contains(edges[i].SecondCell)) return true;
                return false;
            }
            private bool Contains(WorldCellCoordinate cell) => cell.X >= Min.X && cell.X <= Max.X && cell.Y >= Min.Y && cell.Y <= Max.Y;

            public IReadOnlyList<WorldCellCoordinate> CreateCells(int maximumCells)
            {
                decimal widthValue = (decimal)Max.X - Min.X + 1m;
                decimal heightValue = (decimal)Max.Y - Min.Y + 1m;
                if (widthValue > maximumCells
                    || heightValue > maximumCells
                    || widthValue * heightValue > maximumCells)
                    throw new InvalidOperationException("Room analysis scope exceeds the configured cell limit.");
                int width = (int)widthValue;
                int height = (int)heightValue;
                var cells = new List<WorldCellCoordinate>(checked(width * height));
                for (long y = Min.Y; ; y++)
                {
                    for (long x = Min.X; ; x++)
                    {
                        cells.Add(new WorldCellCoordinate(x, y));
                        if (x == Max.X) break;
                    }
                    if (y == Max.Y) break;
                }
                return cells.AsReadOnly();
            }
        }
    }
}
