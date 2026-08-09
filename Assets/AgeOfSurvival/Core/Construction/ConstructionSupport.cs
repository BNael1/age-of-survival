using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Construction
{
    /// <summary>
    /// Directed structural support relation. The provider can transmit support to the dependent.
    /// Geometry and gameplay policy decide which links exist; this Core type only evaluates them.
    /// </summary>
    public readonly struct ConstructionSupportLink :
        IEquatable<ConstructionSupportLink>,
        IComparable<ConstructionSupportLink>
    {
        public ConstructionSupportLink(
            ConstructionInstanceId providerId,
            ConstructionInstanceId dependentId)
        {
            if (!providerId.IsValid)
            {
                throw new ArgumentException("A support link requires a valid provider identifier.", nameof(providerId));
            }

            if (!dependentId.IsValid)
            {
                throw new ArgumentException("A support link requires a valid dependent identifier.", nameof(dependentId));
            }

            if (providerId.Equals(dependentId))
            {
                throw new ArgumentException("A construction instance cannot support itself directly.", nameof(dependentId));
            }

            ProviderId = providerId;
            DependentId = dependentId;
        }

        public ConstructionInstanceId ProviderId { get; }
        public ConstructionInstanceId DependentId { get; }
        public bool IsValid => ProviderId.IsValid && DependentId.IsValid && !ProviderId.Equals(DependentId);

        public int CompareTo(ConstructionSupportLink other)
        {
            EnsureValid();
            other.EnsureValid();
            int providerComparison = ProviderId.CompareTo(other.ProviderId);
            return providerComparison != 0
                ? providerComparison
                : DependentId.CompareTo(other.DependentId);
        }

        public bool Equals(ConstructionSupportLink other) =>
            ProviderId.Equals(other.ProviderId) && DependentId.Equals(other.DependentId);

        public override bool Equals(object obj) => obj is ConstructionSupportLink other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (ProviderId.GetHashCode() * 397) ^ DependentId.GetHashCode();
            }
        }

        public override string ToString() => IsValid ? $"{ProviderId}->{DependentId}" : "<invalid-support-link>";

        public static bool operator ==(ConstructionSupportLink left, ConstructionSupportLink right) => left.Equals(right);
        public static bool operator !=(ConstructionSupportLink left, ConstructionSupportLink right) => !left.Equals(right);

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("A valid construction support link is required.");
            }
        }
    }

    /// <summary>
    /// Derived support state for one roof construction. The roof remains a world structure even when unsupported;
    /// only its shelter contribution is disabled.
    /// </summary>
    public readonly struct ConstructionRoofSupportState : IEquatable<ConstructionRoofSupportState>
    {
        public ConstructionRoofSupportState(ConstructionInstanceId instanceId, bool isSupported)
        {
            if (!instanceId.IsValid)
            {
                throw new ArgumentException("A roof support state requires a valid construction instance identifier.", nameof(instanceId));
            }

            InstanceId = instanceId;
            IsSupported = isSupported;
        }

        public ConstructionInstanceId InstanceId { get; }
        public bool IsSupported { get; }
        public bool CountsTowardShelter => IsSupported;

        public bool Equals(ConstructionRoofSupportState other) =>
            InstanceId.Equals(other.InstanceId) && IsSupported == other.IsSupported;

        public override bool Equals(object obj) => obj is ConstructionRoofSupportState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (InstanceId.GetHashCode() * 397) ^ IsSupported.GetHashCode();
            }
        }

        public override string ToString() => $"{InstanceId}:{(IsSupported ? "Supported" : "Unsupported")}";
    }

    /// <summary>
    /// Immutable result of one support graph evaluation.
    /// Support is graph reachability from explicit roots; cycles without a root do not support themselves.
    /// </summary>
    public sealed class ConstructionSupportEvaluation
    {
        private readonly HashSet<ConstructionInstanceId> _nodes;
        private readonly HashSet<ConstructionInstanceId> _supported;
        private readonly Dictionary<ConstructionInstanceId, ConstructionRoofSupportState> _roofById;
        private readonly IReadOnlyList<ConstructionInstanceId> _supportedIds;
        private readonly IReadOnlyList<ConstructionRoofSupportState> _roofStates;

        internal ConstructionSupportEvaluation(
            IEnumerable<ConstructionInstanceId> nodeIds,
            IEnumerable<ConstructionInstanceId> supportedIds,
            IEnumerable<ConstructionInstanceId> roofIds)
        {
            _nodes = new HashSet<ConstructionInstanceId>(nodeIds ?? throw new ArgumentNullException(nameof(nodeIds)));
            _supported = new HashSet<ConstructionInstanceId>(supportedIds ?? throw new ArgumentNullException(nameof(supportedIds)));

            var supported = new List<ConstructionInstanceId>(_supported);
            supported.Sort();
            _supportedIds = supported.AsReadOnly();

            _roofById = new Dictionary<ConstructionInstanceId, ConstructionRoofSupportState>();
            var roofs = new List<ConstructionRoofSupportState>();
            if (roofIds == null)
            {
                throw new ArgumentNullException(nameof(roofIds));
            }

            foreach (ConstructionInstanceId roofId in roofIds)
            {
                var state = new ConstructionRoofSupportState(roofId, _supported.Contains(roofId));
                _roofById.Add(roofId, state);
                roofs.Add(state);
            }

            roofs.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            _roofStates = roofs.AsReadOnly();
        }

        public IReadOnlyList<ConstructionInstanceId> SupportedInstanceIds => _supportedIds;
        public IReadOnlyList<ConstructionRoofSupportState> RoofStates => _roofStates;

        public bool ContainsNode(ConstructionInstanceId instanceId) =>
            instanceId.IsValid && _nodes.Contains(instanceId);

        public bool IsSupported(ConstructionInstanceId instanceId) =>
            instanceId.IsValid && _supported.Contains(instanceId);

        public bool TryGetRoofState(
            ConstructionInstanceId instanceId,
            out ConstructionRoofSupportState state)
        {
            if (!instanceId.IsValid)
            {
                state = default;
                return false;
            }

            return _roofById.TryGetValue(instanceId, out state);
        }

        public ConstructionRoofSupportState RequireRoofState(ConstructionInstanceId instanceId)
        {
            if (!TryGetRoofState(instanceId, out ConstructionRoofSupportState state))
            {
                throw new KeyNotFoundException($"Unknown roof construction '{instanceId}'.");
            }

            return state;
        }
    }

    /// <summary>
    /// Deterministic structural support graph. It owns no mutable world state and can be rebuilt from the current
    /// construction state whenever supports change. Geometry decides roots and links outside this evaluator.
    /// </summary>
    public sealed class ConstructionSupportGraph
    {
        private readonly IReadOnlyList<ConstructionInstanceId> _nodeIds;
        private readonly IReadOnlyList<ConstructionInstanceId> _rootIds;
        private readonly IReadOnlyList<ConstructionInstanceId> _roofIds;
        private readonly IReadOnlyList<ConstructionSupportLink> _links;

        public ConstructionSupportGraph(
            IEnumerable<ConstructionInstanceId> nodeIds,
            IEnumerable<ConstructionInstanceId> rootIds,
            IEnumerable<ConstructionSupportLink> links,
            IEnumerable<ConstructionInstanceId> roofIds)
        {
            _nodeIds = CanonicalNodes(nodeIds);
            var nodes = new HashSet<ConstructionInstanceId>(_nodeIds);
            _rootIds = CanonicalSubset(rootIds, nodes, "support roots", nameof(rootIds));
            _roofIds = CanonicalSubset(roofIds, nodes, "roof identifiers", nameof(roofIds));
            _links = CanonicalLinks(links, nodes);
        }

        public IReadOnlyList<ConstructionInstanceId> NodeIds => _nodeIds;
        public IReadOnlyList<ConstructionInstanceId> RootIds => _rootIds;
        public IReadOnlyList<ConstructionInstanceId> RoofIds => _roofIds;
        public IReadOnlyList<ConstructionSupportLink> Links => _links;

        /// <summary>
        /// Convenience factory for the current completed-world slice. It derives nodes from completed structures and
        /// roof identifiers from structures occupying Roof space. Callers still provide the gameplay-specific roots
        /// and support links.
        /// </summary>
        public static ConstructionSupportGraph FromCompletedStructures(
            IEnumerable<CompletedStructureState> structures,
            IEnumerable<ConstructionInstanceId> rootIds,
            IEnumerable<ConstructionSupportLink> links)
        {
            if (structures == null)
            {
                throw new ArgumentNullException(nameof(structures));
            }

            var nodes = new List<ConstructionInstanceId>();
            var roofs = new List<ConstructionInstanceId>();
            var ids = new HashSet<ConstructionInstanceId>();
            foreach (CompletedStructureState structure in structures)
            {
                if (structure == null)
                {
                    throw new ArgumentException("Completed structures must not contain null entries.", nameof(structures));
                }

                if (!ids.Add(structure.InstanceId))
                {
                    throw new ArgumentException("Completed structure identifiers must be unique.", nameof(structures));
                }

                nodes.Add(structure.InstanceId);
                if (structure.Space.Kind == ConstructionSpaceKind.Roof)
                {
                    roofs.Add(structure.InstanceId);
                }
            }

            return new ConstructionSupportGraph(nodes, rootIds, links, roofs);
        }

        public ConstructionSupportEvaluation Evaluate()
        {
            var adjacency = new Dictionary<ConstructionInstanceId, List<ConstructionInstanceId>>();
            for (int i = 0; i < _links.Count; i++)
            {
                ConstructionSupportLink link = _links[i];
                if (!adjacency.TryGetValue(link.ProviderId, out List<ConstructionInstanceId> dependents))
                {
                    dependents = new List<ConstructionInstanceId>();
                    adjacency.Add(link.ProviderId, dependents);
                }

                dependents.Add(link.DependentId);
            }

            var supported = new HashSet<ConstructionInstanceId>();
            var queue = new Queue<ConstructionInstanceId>();
            for (int i = 0; i < _rootIds.Count; i++)
            {
                ConstructionInstanceId root = _rootIds[i];
                supported.Add(root);
                queue.Enqueue(root);
            }

            while (queue.Count > 0)
            {
                ConstructionInstanceId provider = queue.Dequeue();
                if (!adjacency.TryGetValue(provider, out List<ConstructionInstanceId> dependents))
                {
                    continue;
                }

                for (int i = 0; i < dependents.Count; i++)
                {
                    ConstructionInstanceId dependent = dependents[i];
                    if (supported.Add(dependent))
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            return new ConstructionSupportEvaluation(_nodeIds, supported, _roofIds);
        }

        private static IReadOnlyList<ConstructionInstanceId> CanonicalNodes(
            IEnumerable<ConstructionInstanceId> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var result = new List<ConstructionInstanceId>();
            var ids = new HashSet<ConstructionInstanceId>();
            foreach (ConstructionInstanceId value in values)
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("Support graph node identifiers must be valid.", nameof(values));
                }

                if (!ids.Add(value))
                {
                    throw new ArgumentException("Support graph node identifiers must be unique.", nameof(values));
                }

                result.Add(value);
            }

            result.Sort();
            return result.AsReadOnly();
        }

        private static IReadOnlyList<ConstructionInstanceId> CanonicalSubset(
            IEnumerable<ConstructionInstanceId> values,
            HashSet<ConstructionInstanceId> nodes,
            string description,
            string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var result = new List<ConstructionInstanceId>();
            var ids = new HashSet<ConstructionInstanceId>();
            foreach (ConstructionInstanceId value in values)
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException($"Construction {description} must use valid identifiers.", parameterName);
                }

                if (!nodes.Contains(value))
                {
                    throw new ArgumentException($"Construction {description} must refer to graph nodes.", parameterName);
                }

                if (!ids.Add(value))
                {
                    throw new ArgumentException($"Construction {description} must be unique.", parameterName);
                }

                result.Add(value);
            }

            result.Sort();
            return result.AsReadOnly();
        }

        private static IReadOnlyList<ConstructionSupportLink> CanonicalLinks(
            IEnumerable<ConstructionSupportLink> values,
            HashSet<ConstructionInstanceId> nodes)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var result = new List<ConstructionSupportLink>();
            var links = new HashSet<ConstructionSupportLink>();
            foreach (ConstructionSupportLink value in values)
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("Construction support links must be valid.", nameof(values));
                }

                if (!nodes.Contains(value.ProviderId) || !nodes.Contains(value.DependentId))
                {
                    throw new ArgumentException("Construction support links must refer to graph nodes.", nameof(values));
                }

                if (!links.Add(value))
                {
                    throw new ArgumentException("Construction support links must be unique.", nameof(values));
                }

                result.Add(value);
            }

            result.Sort();
            return result.AsReadOnly();
        }
    }
}
