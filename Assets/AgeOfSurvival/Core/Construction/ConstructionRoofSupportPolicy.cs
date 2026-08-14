using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Construction
{
    /// <summary>
    /// Explicit, versionable policy selecting completed Edge definitions that can support roofs
    /// and the maximum number of cardinal Roof-to-Roof propagation steps.
    /// </summary>
    public sealed class ConstructionRoofSupportPolicy
    {
        private readonly IReadOnlyList<ConstructionDefinitionId> _supportingEdgeDefinitionIds;

        public ConstructionRoofSupportPolicy(
            IEnumerable<ConstructionDefinitionId> supportingEdgeDefinitionIds,
            int maximumRoofPropagationDistance)
        {
            if (supportingEdgeDefinitionIds == null)
            {
                throw new ArgumentNullException(nameof(supportingEdgeDefinitionIds));
            }

            if (maximumRoofPropagationDistance < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumRoofPropagationDistance),
                    maximumRoofPropagationDistance,
                    "Maximum roof propagation distance must not be negative.");
            }

            var canonicalIds = new List<ConstructionDefinitionId>();
            var uniqueIds = new HashSet<ConstructionDefinitionId>();
            foreach (ConstructionDefinitionId definitionId in supportingEdgeDefinitionIds)
            {
                if (!definitionId.IsValid)
                {
                    throw new ArgumentException(
                        "Supporting edge definition identifiers must be valid.",
                        nameof(supportingEdgeDefinitionIds));
                }

                if (!uniqueIds.Add(definitionId))
                {
                    throw new ArgumentException(
                        "Supporting edge definition identifiers must be unique.",
                        nameof(supportingEdgeDefinitionIds));
                }

                canonicalIds.Add(definitionId);
            }

            canonicalIds.Sort();
            _supportingEdgeDefinitionIds = canonicalIds.AsReadOnly();
            MaximumRoofPropagationDistance = maximumRoofPropagationDistance;
        }

        public IReadOnlyList<ConstructionDefinitionId> SupportingEdgeDefinitionIds =>
            _supportingEdgeDefinitionIds;

        public int MaximumRoofPropagationDistance { get; }
    }

    /// <summary>
    /// Builds the effective bounded support sub-graph from completed construction facts.
    /// Geometry remains delegated to ConstructionRoofBoundaryGeometry and reachability remains
    /// delegated to ConstructionSupportGraph.
    /// </summary>
    public static class ConstructionRoofSupportGraphBuilder
    {
        private static readonly ConstructionCellSide[] CardinalSides =
        {
            ConstructionCellSide.North,
            ConstructionCellSide.East,
            ConstructionCellSide.South,
            ConstructionCellSide.West
        };

        public static ConstructionSupportGraph Build(
            ConstructionDefinitionCatalog catalog,
            IEnumerable<CompletedStructureState> structures,
            ConstructionRoofSupportPolicy policy)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (structures == null) throw new ArgumentNullException(nameof(structures));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            HashSet<ConstructionDefinitionId> supportingDefinitions =
                ValidateSupportingDefinitions(catalog, policy);
            List<CompletedStructureState> canonicalStructures =
                ValidateAndCanonicalizeStructures(catalog, structures);

            ConstructionRoofBoundaryGeometry geometry =
                ConstructionRoofBoundaryGeometry.FromCompletedStructures(
                    canonicalStructures);

            var structuresById = new Dictionary<ConstructionInstanceId, CompletedStructureState>();
            for (int index = 0; index < canonicalStructures.Count; index++)
            {
                CompletedStructureState structure = canonicalStructures[index];
                structuresById.Add(structure.InstanceId, structure);
            }

            var roofsByCell = new Dictionary<WorldCellCoordinate, ConstructionRoofBoundary>();
            for (int index = 0; index < geometry.Roofs.Count; index++)
            {
                ConstructionRoofBoundary roof = geometry.Roofs[index];
                roofsByCell.Add(roof.RoofCell, roof);
            }

            var roots = new HashSet<ConstructionInstanceId>();
            var rootRoofIds = new HashSet<ConstructionInstanceId>();
            var links = new HashSet<ConstructionSupportLink>();

            for (int roofIndex = 0; roofIndex < geometry.Roofs.Count; roofIndex++)
            {
                ConstructionRoofBoundary roof = geometry.Roofs[roofIndex];
                for (int contactIndex = 0; contactIndex < roof.Contacts.Count; contactIndex++)
                {
                    ConstructionRoofBoundaryContact contact = roof.Contacts[contactIndex];
                    CompletedStructureState edge = structuresById[contact.EdgeInstanceId];
                    if (!supportingDefinitions.Contains(edge.DefinitionId))
                    {
                        continue;
                    }

                    roots.Add(edge.InstanceId);
                    rootRoofIds.Add(roof.RoofInstanceId);
                    links.Add(new ConstructionSupportLink(
                        edge.InstanceId,
                        roof.RoofInstanceId));
                }
            }

            Dictionary<ConstructionInstanceId, int> distances = CalculateMinimumDistances(
                geometry,
                roofsByCell,
                rootRoofIds);

            AddBoundedRoofLinks(
                geometry,
                roofsByCell,
                distances,
                policy.MaximumRoofPropagationDistance,
                links);

            var canonicalRoots = new List<ConstructionInstanceId>(roots);
            canonicalRoots.Sort();
            var canonicalLinks = new List<ConstructionSupportLink>(links);
            canonicalLinks.Sort();

            return ConstructionSupportGraph.FromCompletedStructures(
                canonicalStructures,
                canonicalRoots,
                canonicalLinks);
        }

        private static HashSet<ConstructionDefinitionId> ValidateSupportingDefinitions(
            ConstructionDefinitionCatalog catalog,
            ConstructionRoofSupportPolicy policy)
        {
            var result = new HashSet<ConstructionDefinitionId>();
            for (int index = 0; index < policy.SupportingEdgeDefinitionIds.Count; index++)
            {
                ConstructionDefinitionId definitionId =
                    policy.SupportingEdgeDefinitionIds[index];
                if (!catalog.TryFind(definitionId, out ConstructionDefinition definition))
                {
                    throw new ArgumentException(
                        $"Unknown supporting edge construction definition '{definitionId}'.",
                        nameof(policy));
                }

                if (definition.SpaceKind != ConstructionSpaceKind.Edge)
                {
                    throw new ArgumentException(
                        $"Supporting construction definition '{definitionId}' must occupy Edge space.",
                        nameof(policy));
                }

                result.Add(definitionId);
            }

            return result;
        }

        private static List<CompletedStructureState> ValidateAndCanonicalizeStructures(
            ConstructionDefinitionCatalog catalog,
            IEnumerable<CompletedStructureState> structures)
        {
            var result = new List<CompletedStructureState>();
            var identifiers = new HashSet<ConstructionInstanceId>();
            var occupiedSpaces = new HashSet<ConstructionSpaceKey>();

            foreach (CompletedStructureState structure in structures)
            {
                if (structure == null)
                {
                    throw new ArgumentException(
                        "Completed structures must not contain null entries.",
                        nameof(structures));
                }

                if (!structure.InstanceId.IsValid
                    || !structure.DefinitionId.IsValid
                    || !structure.Space.IsValid)
                {
                    throw new ArgumentException(
                        "Completed structures must expose valid identities and construction spaces.",
                        nameof(structures));
                }

                if (!identifiers.Add(structure.InstanceId))
                {
                    throw new ArgumentException(
                        "Completed structure identifiers must be unique.",
                        nameof(structures));
                }

                if (!occupiedSpaces.Add(structure.Space))
                {
                    throw new ArgumentException(
                        "Completed construction topology cannot be occupied more than once.",
                        nameof(structures));
                }

                if (!catalog.TryFind(
                        structure.DefinitionId,
                        out ConstructionDefinition definition))
                {
                    throw new ArgumentException(
                        $"Unknown completed construction definition '{structure.DefinitionId}'.",
                        nameof(structures));
                }

                if (structure.Space.Kind != definition.SpaceKind)
                {
                    throw new ArgumentException(
                        $"Completed construction '{structure.InstanceId}' occupies a space incompatible with its definition.",
                        nameof(structures));
                }

                result.Add(structure);
            }

            result.Sort(CompareStructures);
            return result;
        }

        private static Dictionary<ConstructionInstanceId, int> CalculateMinimumDistances(
            ConstructionRoofBoundaryGeometry geometry,
            IDictionary<WorldCellCoordinate, ConstructionRoofBoundary> roofsByCell,
            ISet<ConstructionInstanceId> rootRoofIds)
        {
            var distances = new Dictionary<ConstructionInstanceId, int>();
            var queue = new Queue<ConstructionRoofBoundary>();

            for (int index = 0; index < geometry.Roofs.Count; index++)
            {
                ConstructionRoofBoundary roof = geometry.Roofs[index];
                if (!rootRoofIds.Contains(roof.RoofInstanceId))
                {
                    continue;
                }

                distances.Add(roof.RoofInstanceId, 0);
                queue.Enqueue(roof);
            }

            while (queue.Count > 0)
            {
                ConstructionRoofBoundary provider = queue.Dequeue();
                int providerDistance = distances[provider.RoofInstanceId];
                if (providerDistance == int.MaxValue)
                {
                    continue;
                }

                for (int sideIndex = 0; sideIndex < CardinalSides.Length; sideIndex++)
                {
                    if (!TryGetNeighbourCell(
                            provider.RoofCell,
                            CardinalSides[sideIndex],
                            out WorldCellCoordinate neighbourCell)
                        || !roofsByCell.TryGetValue(
                            neighbourCell,
                            out ConstructionRoofBoundary neighbour)
                        || distances.ContainsKey(neighbour.RoofInstanceId))
                    {
                        continue;
                    }

                    distances.Add(neighbour.RoofInstanceId, providerDistance + 1);
                    queue.Enqueue(neighbour);
                }
            }

            return distances;
        }

        private static void AddBoundedRoofLinks(
            ConstructionRoofBoundaryGeometry geometry,
            IDictionary<WorldCellCoordinate, ConstructionRoofBoundary> roofsByCell,
            IDictionary<ConstructionInstanceId, int> distances,
            int maximumDistance,
            ISet<ConstructionSupportLink> links)
        {
            for (int index = 0; index < geometry.Roofs.Count; index++)
            {
                ConstructionRoofBoundary provider = geometry.Roofs[index];
                if (!distances.TryGetValue(
                        provider.RoofInstanceId,
                        out int providerDistance)
                    || providerDistance >= maximumDistance)
                {
                    continue;
                }

                for (int sideIndex = 0; sideIndex < CardinalSides.Length; sideIndex++)
                {
                    if (!TryGetNeighbourCell(
                            provider.RoofCell,
                            CardinalSides[sideIndex],
                            out WorldCellCoordinate neighbourCell)
                        || !roofsByCell.TryGetValue(
                            neighbourCell,
                            out ConstructionRoofBoundary dependent)
                        || !distances.TryGetValue(
                            dependent.RoofInstanceId,
                            out int dependentDistance)
                        || dependentDistance != providerDistance + 1)
                    {
                        continue;
                    }

                    links.Add(new ConstructionSupportLink(
                        provider.RoofInstanceId,
                        dependent.RoofInstanceId));
                }
            }
        }

        private static bool TryGetNeighbourCell(
            WorldCellCoordinate cell,
            ConstructionCellSide side,
            out WorldCellCoordinate neighbour)
        {
            if (!ConstructionEdgeAddress.TryCreate(
                    cell,
                    side,
                    out ConstructionEdgeAddress edge))
            {
                neighbour = default;
                return false;
            }

            WorldCellCoordinate first = edge.FirstCell;
            WorldCellCoordinate second = edge.SecondCell;
            neighbour = first.Equals(cell) ? second : first;
            return true;
        }

        private static int CompareStructures(
            CompletedStructureState left,
            CompletedStructureState right)
        {
            int identifierComparison = left.InstanceId.CompareTo(right.InstanceId);
            return identifierComparison != 0
                ? identifierComparison
                : left.Space.CompareTo(right.Space);
        }
    }
}
