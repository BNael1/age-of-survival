using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Construction
{
    /// <summary>
    /// One geometric contact between a completed roof and a completed edge occupying one of its
    /// representable canonical boundaries. This value does not state that the edge transmits support.
    /// </summary>
    public readonly struct ConstructionRoofBoundaryContact :
        IEquatable<ConstructionRoofBoundaryContact>,
        IComparable<ConstructionRoofBoundaryContact>
    {
        internal ConstructionRoofBoundaryContact(
            ConstructionInstanceId roofInstanceId,
            ConstructionInstanceId edgeInstanceId,
            ConstructionEdgeAddress edgeAddress)
        {
            if (!roofInstanceId.IsValid)
            {
                throw new ArgumentException(
                    "A roof boundary contact requires a valid roof identifier.",
                    nameof(roofInstanceId));
            }

            if (!edgeInstanceId.IsValid)
            {
                throw new ArgumentException(
                    "A roof boundary contact requires a valid edge occupant identifier.",
                    nameof(edgeInstanceId));
            }

            if (roofInstanceId.Equals(edgeInstanceId))
            {
                throw new ArgumentException(
                    "A roof and its edge occupant must be distinct construction instances.",
                    nameof(edgeInstanceId));
            }

            if (!edgeAddress.IsValid)
            {
                throw new ArgumentException(
                    "A roof boundary contact requires a valid canonical edge address.",
                    nameof(edgeAddress));
            }

            RoofInstanceId = roofInstanceId;
            EdgeInstanceId = edgeInstanceId;
            EdgeAddress = edgeAddress;
        }

        public ConstructionInstanceId RoofInstanceId { get; }
        public ConstructionInstanceId EdgeInstanceId { get; }
        public ConstructionEdgeAddress EdgeAddress { get; }

        public bool IsValid =>
            RoofInstanceId.IsValid
            && EdgeInstanceId.IsValid
            && !RoofInstanceId.Equals(EdgeInstanceId)
            && EdgeAddress.IsValid;

        public int CompareTo(ConstructionRoofBoundaryContact other)
        {
            EnsureValid();
            other.EnsureValid();

            int roofComparison = RoofInstanceId.CompareTo(other.RoofInstanceId);
            if (roofComparison != 0)
            {
                return roofComparison;
            }

            int edgeAddressComparison = EdgeAddress.CompareTo(other.EdgeAddress);
            return edgeAddressComparison != 0
                ? edgeAddressComparison
                : EdgeInstanceId.CompareTo(other.EdgeInstanceId);
        }

        public bool Equals(ConstructionRoofBoundaryContact other)
        {
            return RoofInstanceId.Equals(other.RoofInstanceId)
                && EdgeInstanceId.Equals(other.EdgeInstanceId)
                && EdgeAddress.Equals(other.EdgeAddress);
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructionRoofBoundaryContact other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RoofInstanceId.GetHashCode();
                hash = (hash * 397) ^ EdgeAddress.GetHashCode();
                return (hash * 397) ^ EdgeInstanceId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return IsValid
                ? $"{RoofInstanceId}<->{EdgeInstanceId}@{EdgeAddress}"
                : "<invalid-roof-boundary-contact>";
        }

        public static bool operator ==(
            ConstructionRoofBoundaryContact left,
            ConstructionRoofBoundaryContact right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ConstructionRoofBoundaryContact left,
            ConstructionRoofBoundaryContact right)
        {
            return !left.Equals(right);
        }

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "A valid roof boundary contact is required.");
            }
        }
    }

    /// <summary>
    /// Immutable canonical boundary capture for one completed roof, including roofs with no contacts.
    /// </summary>
    public sealed class ConstructionRoofBoundary
    {
        private readonly IReadOnlyList<ConstructionRoofBoundaryContact> _contacts;

        internal ConstructionRoofBoundary(
            ConstructionInstanceId roofInstanceId,
            WorldCellCoordinate roofCell,
            IEnumerable<ConstructionRoofBoundaryContact> contacts)
        {
            if (!roofInstanceId.IsValid)
            {
                throw new ArgumentException(
                    "A roof boundary requires a valid roof identifier.",
                    nameof(roofInstanceId));
            }

            if (contacts == null)
            {
                throw new ArgumentNullException(nameof(contacts));
            }

            var canonicalContacts = new List<ConstructionRoofBoundaryContact>();
            var edgeIds = new HashSet<ConstructionInstanceId>();
            var edgeAddresses = new HashSet<ConstructionEdgeAddress>();
            foreach (ConstructionRoofBoundaryContact contact in contacts)
            {
                if (!contact.IsValid
                    || !contact.RoofInstanceId.Equals(roofInstanceId))
                {
                    throw new ArgumentException(
                        "Roof boundary contacts must be valid and refer to their containing roof.",
                        nameof(contacts));
                }

                if (!contact.EdgeAddress.FirstCell.Equals(roofCell)
                    && !contact.EdgeAddress.SecondCell.Equals(roofCell))
                {
                    throw new ArgumentException(
                        "A roof boundary contact must touch the roof cell.",
                        nameof(contacts));
                }

                if (!edgeIds.Add(contact.EdgeInstanceId)
                    || !edgeAddresses.Add(contact.EdgeAddress))
                {
                    throw new ArgumentException(
                        "Roof boundary contacts must be unique by occupant and canonical edge.",
                        nameof(contacts));
                }

                canonicalContacts.Add(contact);
            }

            canonicalContacts.Sort();
            RoofInstanceId = roofInstanceId;
            RoofCell = roofCell;
            _contacts = canonicalContacts.AsReadOnly();
        }

        public ConstructionInstanceId RoofInstanceId { get; }
        public WorldCellCoordinate RoofCell { get; }
        public IReadOnlyList<ConstructionRoofBoundaryContact> Contacts => _contacts;
    }

    /// <summary>
    /// Derived and rebuildable geometric Roof-to-Edge boundary capture. It uses only completed
    /// construction state and deliberately applies no structural-support or shelter policy.
    /// </summary>
    public sealed class ConstructionRoofBoundaryGeometry
    {
        private readonly IReadOnlyList<ConstructionRoofBoundary> _roofs;

        private ConstructionRoofBoundaryGeometry(
            IEnumerable<ConstructionRoofBoundary> roofs)
        {
            var canonicalRoofs = new List<ConstructionRoofBoundary>(
                roofs ?? throw new ArgumentNullException(nameof(roofs)));
            canonicalRoofs.Sort(CompareRoofs);
            _roofs = canonicalRoofs.AsReadOnly();
        }

        public IReadOnlyList<ConstructionRoofBoundary> Roofs => _roofs;

        public static ConstructionRoofBoundaryGeometry FromCompletedStructures(
            IEnumerable<CompletedStructureState> structures)
        {
            if (structures == null)
            {
                throw new ArgumentNullException(nameof(structures));
            }

            var identifiers = new HashSet<ConstructionInstanceId>();
            var occupiedSpaces = new HashSet<ConstructionSpaceKey>();
            var edgeOccupants =
                new Dictionary<ConstructionEdgeAddress, CompletedStructureState>();
            var roofStructures = new List<CompletedStructureState>();

            foreach (CompletedStructureState structure in structures)
            {
                ValidateCompletedStructure(structure, nameof(structures));

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

                switch (structure.Space.Kind)
                {
                    case ConstructionSpaceKind.Edge:
                        edgeOccupants.Add(structure.Space.EdgeAddress, structure);
                        break;

                    case ConstructionSpaceKind.Roof:
                        roofStructures.Add(structure);
                        break;
                }
            }

            roofStructures.Sort(CompareStructures);
            var roofs = new List<ConstructionRoofBoundary>(roofStructures.Count);
            for (int index = 0; index < roofStructures.Count; index++)
            {
                CompletedStructureState roof = roofStructures[index];
                WorldCellCoordinate roofCell = roof.Space.AnchorCell;
                var contacts = new List<ConstructionRoofBoundaryContact>(4);

                TryAddContact(
                    roof,
                    roofCell,
                    ConstructionCellSide.North,
                    edgeOccupants,
                    contacts);
                TryAddContact(
                    roof,
                    roofCell,
                    ConstructionCellSide.East,
                    edgeOccupants,
                    contacts);
                TryAddContact(
                    roof,
                    roofCell,
                    ConstructionCellSide.South,
                    edgeOccupants,
                    contacts);
                TryAddContact(
                    roof,
                    roofCell,
                    ConstructionCellSide.West,
                    edgeOccupants,
                    contacts);

                roofs.Add(new ConstructionRoofBoundary(
                    roof.InstanceId,
                    roofCell,
                    contacts));
            }

            return new ConstructionRoofBoundaryGeometry(roofs);
        }

        private static void TryAddContact(
            CompletedStructureState roof,
            WorldCellCoordinate roofCell,
            ConstructionCellSide side,
            IDictionary<ConstructionEdgeAddress, CompletedStructureState> edgeOccupants,
            ICollection<ConstructionRoofBoundaryContact> contacts)
        {
            if (!ConstructionEdgeAddress.TryCreate(
                    roofCell,
                    side,
                    out ConstructionEdgeAddress edgeAddress)
                || !edgeOccupants.TryGetValue(
                    edgeAddress,
                    out CompletedStructureState edgeOccupant))
            {
                return;
            }

            contacts.Add(new ConstructionRoofBoundaryContact(
                roof.InstanceId,
                edgeOccupant.InstanceId,
                edgeAddress));
        }

        private static void ValidateCompletedStructure(
            CompletedStructureState structure,
            string parameterName)
        {
            if (structure == null)
            {
                throw new ArgumentException(
                    "Completed structures must not contain null entries.",
                    parameterName);
            }

            if (!structure.InstanceId.IsValid
                || !structure.DefinitionId.IsValid
                || !structure.Space.IsValid)
            {
                throw new ArgumentException(
                    "Completed structures must expose valid identities and construction spaces.",
                    parameterName);
            }
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

        private static int CompareRoofs(
            ConstructionRoofBoundary left,
            ConstructionRoofBoundary right)
        {
            if (left == null || right == null)
            {
                return left == null ? (right == null ? 0 : -1) : 1;
            }

            int identifierComparison =
                left.RoofInstanceId.CompareTo(right.RoofInstanceId);
            return identifierComparison != 0
                ? identifierComparison
                : left.RoofCell.CompareTo(right.RoofCell);
        }
    }
}
