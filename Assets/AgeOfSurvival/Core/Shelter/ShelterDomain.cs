using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Shelter
{
    public readonly struct ShelterId : IEquatable<ShelterId>, IComparable<ShelterId>
    {
        public ShelterId(string value)
        {
            StableIdentifierValidation.Validate(value, nameof(value));
            Value = value;
        }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public int CompareTo(ShelterId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(ShelterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ShelterId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ShelterId left, ShelterId right) => left.Equals(right);
        public static bool operator !=(ShelterId left, ShelterId right) => !left.Equals(right);
    }

    /// <summary>
    /// Candidate-only identity strategy used to exercise the technical shelter pipeline before
    /// gameplay identity and split/merge semantics are approved. ShelterId and persistence do not
    /// depend on this Construction-based provenance.
    /// </summary>
    public interface IShelterCandidateIdentityStrategy
    {
        ShelterId CreateCandidateId(CompletedStructureState candidateSource);
    }

    /// <summary>
    /// Synthetic candidate strategy: derives an ID from a completed fixture source instance.
    /// This is not the universal ShelterId contract and is not an active gameplay decision.
    /// </summary>
    public sealed class AnchorBasedShelterIdentityCandidateStrategy :
        IShelterCandidateIdentityStrategy
    {
        public ShelterId CreateCandidateId(CompletedStructureState candidateSource)
        {
            if (candidateSource == null)
                throw new ArgumentNullException(nameof(candidateSource));
            if (!candidateSource.InstanceId.IsValid)
                throw new ArgumentException(
                    "A valid candidate source instance is required.",
                    nameof(candidateSource));
            return new ShelterId("candidate-anchor:" + candidateSource.InstanceId.Value);
        }
    }

    /// <summary>
    /// Configurable technical candidate policy. Its definition lists and local predicates are
    /// test fixtures, not validated gameplay rules for what constitutes a real shelter.
    /// </summary>
    public sealed class ShelterCandidateEvaluationPolicy
    {
        private readonly IReadOnlyList<ConstructionDefinitionId> _localSurfaceIds;
        private readonly IReadOnlyList<ConstructionDefinitionId> _candidateSourceIds;
        private readonly IReadOnlyList<ConstructionDefinitionId> _boundaryIds;

        public ShelterCandidateEvaluationPolicy(
            IEnumerable<ConstructionDefinitionId> localSurfaceDefinitionIds,
            IEnumerable<ConstructionDefinitionId> candidateSourceDefinitionIds,
            IEnumerable<ConstructionDefinitionId> boundaryDefinitionIds,
            bool requireConfiguredSurfaceAtCandidateCell,
            int minimumAdjacentBoundaryCount,
            bool requireFullySupportedRoof)
        {
            if (minimumAdjacentBoundaryCount < 0 || minimumAdjacentBoundaryCount > 4)
                throw new ArgumentOutOfRangeException(nameof(minimumAdjacentBoundaryCount));
            _localSurfaceIds = Canonical(
                localSurfaceDefinitionIds,
                nameof(localSurfaceDefinitionIds),
                allowEmpty: !requireConfiguredSurfaceAtCandidateCell);
            _candidateSourceIds = Canonical(
                candidateSourceDefinitionIds,
                nameof(candidateSourceDefinitionIds),
                allowEmpty: false);
            _boundaryIds = Canonical(
                boundaryDefinitionIds,
                nameof(boundaryDefinitionIds),
                allowEmpty: minimumAdjacentBoundaryCount == 0);
            RequireConfiguredSurfaceAtCandidateCell = requireConfiguredSurfaceAtCandidateCell;
            MinimumAdjacentBoundaryCount = minimumAdjacentBoundaryCount;
            RequireFullySupportedRoof = requireFullySupportedRoof;
        }

        public IReadOnlyList<ConstructionDefinitionId> LocalSurfaceDefinitionIds => _localSurfaceIds;
        public IReadOnlyList<ConstructionDefinitionId> CandidateSourceDefinitionIds => _candidateSourceIds;
        public IReadOnlyList<ConstructionDefinitionId> BoundaryDefinitionIds => _boundaryIds;
        public bool RequireConfiguredSurfaceAtCandidateCell { get; }
        public int MinimumAdjacentBoundaryCount { get; }
        public bool RequireFullySupportedRoof { get; }

        private static IReadOnlyList<ConstructionDefinitionId> Canonical(
            IEnumerable<ConstructionDefinitionId> source,
            string name,
            bool allowEmpty)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new List<ConstructionDefinitionId>();
            var unique = new HashSet<ConstructionDefinitionId>();
            foreach (ConstructionDefinitionId id in source)
            {
                if (!id.IsValid) throw new ArgumentException("Shelter policy definition identifiers must be valid.", name);
                if (!unique.Add(id)) throw new ArgumentException("Shelter policy definition identifiers must be unique.", name);
                result.Add(id);
            }
            if (!allowEmpty && result.Count == 0)
                throw new ArgumentException(
                    "This shelter candidate policy definition list must not be empty.",
                    name);
            result.Sort();
            return result.AsReadOnly();
        }
    }

    public enum ShelterInvalidReason
    {
        None = 0,
        MultipleCandidateSourcesInRoom = 1,
        MissingConfiguredLocalSurface = 2,
        MissingSupportedRoof = 3,
        InsufficientConfiguredAdjacentBoundaries = 4
    }

    public sealed class ShelterAssessment
    {
        internal ShelterAssessment(
            ShelterId id,
            ConstructionInstanceId candidateSourceId,
            ConstructionRoomCoverage room,
            ShelterInvalidReason invalidReason,
            int adjacentBoundaryCount)
        {
            Id = id;
            CandidateSourceInstanceId = candidateSourceId;
            Room = room;
            InvalidReason = invalidReason;
            AdjacentBoundaryCount = adjacentBoundaryCount;
        }
        public ShelterId Id { get; }
        public ConstructionInstanceId CandidateSourceInstanceId { get; }
        public ConstructionRoomCoverage Room { get; }
        public ShelterInvalidReason InvalidReason { get; }
        public int AdjacentBoundaryCount { get; }
        public bool IsValid => InvalidReason == ShelterInvalidReason.None;
    }

    public sealed class ShelterEvaluation
    {
        public ShelterEvaluation(IEnumerable<ShelterAssessment> assessments)
        {
            var copy = new List<ShelterAssessment>(assessments);
            copy.Sort((left, right) => left.Id.CompareTo(right.Id));
            Assessments = copy.AsReadOnly();
        }
        public IReadOnlyList<ShelterAssessment> Assessments { get; }
        public bool TryFindValidAt(WorldCellCoordinate cell, out ShelterAssessment shelter)
        {
            for (int i = 0; i < Assessments.Count; i++)
                if (Assessments[i].IsValid && Assessments[i].Room.Room.Contains(cell))
                { shelter = Assessments[i]; return true; }
            shelter = null;
            return false;
        }
    }

    public static class ShelterEvaluator
    {
        private static readonly ConstructionCellSide[] Sides =
        {
            ConstructionCellSide.North, ConstructionCellSide.East,
            ConstructionCellSide.South, ConstructionCellSide.West
        };

        public static ShelterEvaluation Evaluate(
            ConstructionDefinitionCatalog catalog,
            IEnumerable<CompletedStructureState> structures,
            ConstructionDerivedRooms derivedRooms,
            ShelterCandidateEvaluationPolicy policy,
            IShelterCandidateIdentityStrategy identityStrategy)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (structures == null) throw new ArgumentNullException(nameof(structures));
            if (derivedRooms == null) throw new ArgumentNullException(nameof(derivedRooms));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (identityStrategy == null) throw new ArgumentNullException(nameof(identityStrategy));

            ValidatePolicyDefinitions(catalog, policy);
            var localSurfaces = new HashSet<WorldCellCoordinate>();
            var boundaries = new HashSet<ConstructionEdgeAddress>();
            var candidateSources = new List<CompletedStructureState>();
            var localSurfaceIds = new HashSet<ConstructionDefinitionId>(
                policy.LocalSurfaceDefinitionIds);
            var candidateSourceIds = new HashSet<ConstructionDefinitionId>(
                policy.CandidateSourceDefinitionIds);
            var boundaryIds = new HashSet<ConstructionDefinitionId>(policy.BoundaryDefinitionIds);
            var seenIds = new HashSet<ConstructionInstanceId>();
            var seenSpaces = new HashSet<ConstructionSpaceKey>();

            foreach (CompletedStructureState structure in structures)
            {
                if (structure == null) throw new ArgumentException("Structures must not contain null entries.", nameof(structures));
                if (!seenIds.Add(structure.InstanceId) || !seenSpaces.Add(structure.Space))
                    throw new ArgumentException("Structures must have unique identities and occupations.", nameof(structures));
                ConstructionDefinition definition = catalog.Require(structure.DefinitionId);
                if (definition.SpaceKind != structure.Space.Kind)
                    throw new ArgumentException("Structure space must match its definition.", nameof(structures));
                if (localSurfaceIds.Contains(structure.DefinitionId))
                    localSurfaces.Add(structure.Space.AnchorCell);
                if (boundaryIds.Contains(structure.DefinitionId)) boundaries.Add(structure.Space.EdgeAddress);
                if (candidateSourceIds.Contains(structure.DefinitionId))
                    candidateSources.Add(structure);
            }

            candidateSources.Sort((left, right) =>
                left.InstanceId.CompareTo(right.InstanceId));
            var assessments = new List<ShelterAssessment>();
            var candidateIds = new HashSet<ShelterId>();
            for (int roomIndex = 0; roomIndex < derivedRooms.Rooms.Count; roomIndex++)
            {
                ConstructionRoomCoverage room = derivedRooms.Rooms[roomIndex];
                var roomSources = new List<CompletedStructureState>();
                for (int i = 0; i < candidateSources.Count; i++)
                    if (room.Room.Contains(candidateSources[i].Space.AnchorCell))
                        roomSources.Add(candidateSources[i]);
                if (roomSources.Count == 0) continue;

                for (int i = 0; i < roomSources.Count; i++)
                {
                    CompletedStructureState source = roomSources[i];
                    ShelterId candidateId = identityStrategy.CreateCandidateId(source);
                    if (!candidateId.IsValid)
                        throw new InvalidOperationException(
                            "Shelter candidate identity strategy returned an invalid identifier.");
                    if (!candidateIds.Add(candidateId))
                        throw new InvalidOperationException(
                            "Shelter candidate identity strategy returned a duplicate identifier.");
                    int boundaryCount = CountAdjacentBoundaries(
                        source.Space.AnchorCell,
                        boundaries);
                    ShelterInvalidReason reason = roomSources.Count > 1
                        ? ShelterInvalidReason.MultipleCandidateSourcesInRoom
                        : policy.RequireConfiguredSurfaceAtCandidateCell
                            && !localSurfaces.Contains(source.Space.AnchorCell)
                            ? ShelterInvalidReason.MissingConfiguredLocalSurface
                            : policy.RequireFullySupportedRoof && !room.IsFullySupportedRoofed
                                ? ShelterInvalidReason.MissingSupportedRoof
                                : boundaryCount < policy.MinimumAdjacentBoundaryCount
                                    ? ShelterInvalidReason.InsufficientConfiguredAdjacentBoundaries
                                    : ShelterInvalidReason.None;
                    assessments.Add(new ShelterAssessment(
                        candidateId,
                        source.InstanceId,
                        room,
                        reason,
                        boundaryCount));
                }
            }
            return new ShelterEvaluation(assessments);
        }

        private static int CountAdjacentBoundaries(WorldCellCoordinate cell, ISet<ConstructionEdgeAddress> boundaries)
        {
            int count = 0;
            for (int i = 0; i < Sides.Length; i++)
                if (ConstructionEdgeAddress.TryCreate(cell, Sides[i], out ConstructionEdgeAddress edge) && boundaries.Contains(edge)) count++;
            return count;
        }

        private static void ValidatePolicyDefinitions(
            ConstructionDefinitionCatalog catalog,
            ShelterCandidateEvaluationPolicy policy)
        {
            for (int i = 0; i < policy.LocalSurfaceDefinitionIds.Count; i++)
                if (catalog.Require(policy.LocalSurfaceDefinitionIds[i]).SpaceKind
                    != ConstructionSpaceKind.Surface)
                    throw new ArgumentException(
                        "Configured local surface definitions must occupy Surface space.",
                        nameof(policy));
            for (int i = 0; i < policy.CandidateSourceDefinitionIds.Count; i++)
                catalog.Require(policy.CandidateSourceDefinitionIds[i]);
            for (int i = 0; i < policy.BoundaryDefinitionIds.Count; i++)
                if (catalog.Require(policy.BoundaryDefinitionIds[i]).SpaceKind != ConstructionSpaceKind.Edge)
                    throw new ArgumentException("Shelter boundary definitions must occupy Edge space.", nameof(policy));
        }
    }
}
