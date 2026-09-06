using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Shelter;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Construction;

namespace AgeOfSurvival.Runtime.Shelter
{
    public enum ShelterPresenceTransitionKind
    {
        None = 0,
        Entered = 1,
        Exited = 2,
        Changed = 3
    }

    public readonly struct ShelterPresenceTransition
    {
        public ShelterPresenceTransition(
            ShelterPresenceTransitionKind kind,
            ShelterId previousShelterId,
            ShelterId currentShelterId)
        {
            Kind = kind;
            PreviousShelterId = previousShelterId;
            CurrentShelterId = currentShelterId;
        }
        public ShelterPresenceTransitionKind Kind { get; }
        public ShelterId PreviousShelterId { get; }
        public ShelterId CurrentShelterId { get; }
    }

    /// <summary>
    /// Runtime composition seam for a caller that explicitly installs an approved shelter policy.
    /// Such a caller supplies fixed-tick player cells and construction-change notifications; every
    /// rule and all mutable history remain in the Core domain. This seam is not installed in the
    /// SampleScene gameplay composition while shelter qualification remains undecided.
    /// </summary>
    public sealed class ShelterRuntimeSession
    {
        private readonly ConstructionDefinitionCatalog _catalog;
        private readonly ConstructionEnclosureBlockingPolicy _enclosurePolicy;
        private readonly ConstructionRoomAnalysisLimits _limits;
        private readonly ConstructionRoofSupportPolicy _roofPolicy;
        private readonly ShelterCandidateEvaluationPolicy _candidatePolicy;
        private readonly IShelterCandidateIdentityStrategy _candidateIdentityStrategy;
        private readonly ShelterFamiliarityRules _familiarityRules;
        private readonly IShelterRoomQualificationPolicy _roomPolicy;
        private readonly IRoomShelterIdentityStrategy _roomIdentity;
        private readonly ConstructionDefinitionId _floorId;
        private ShelterEvaluation _evaluation = new ShelterEvaluation(Array.Empty<ShelterAssessment>());

        public ShelterRuntimeSession(
            ConstructionDefinitionCatalog catalog,
            ConstructionEnclosureBlockingPolicy enclosurePolicy,
            ConstructionRoomAnalysisLimits limits,
            ConstructionRoofSupportPolicy roofPolicy,
            ShelterCandidateEvaluationPolicy candidatePolicy,
            IShelterCandidateIdentityStrategy candidateIdentityStrategy,
            ShelterFamiliarityRules familiarityRules,
            ShelterHomeState homeState)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _enclosurePolicy = enclosurePolicy ?? throw new ArgumentNullException(nameof(enclosurePolicy));
            _limits = limits;
            _roofPolicy = roofPolicy ?? throw new ArgumentNullException(nameof(roofPolicy));
            _candidatePolicy = candidatePolicy ?? throw new ArgumentNullException(nameof(candidatePolicy));
            _candidateIdentityStrategy = candidateIdentityStrategy
                ?? throw new ArgumentNullException(nameof(candidateIdentityStrategy));
            _familiarityRules = familiarityRules ?? throw new ArgumentNullException(nameof(familiarityRules));
            HomeState = homeState ?? throw new ArgumentNullException(nameof(homeState));
        }

        public ShelterHomeState HomeState { get; }
        public long FullRecalculations { get; private set; }
        public long LocalRecalculations { get; private set; }
        public long ScopesAnalyzed { get; private set; }
        public long CellsAnalyzed { get; private set; }
        public int LastScopesAnalyzed { get; private set; }
        public long LastCellsAnalyzed { get; private set; }
        public string LastInvalidationReason { get; private set; }
        public ShelterEvaluation Evaluation => _evaluation;
        public bool HasCurrentShelter { get; private set; }
        public ShelterId CurrentShelterId { get; private set; }

        public ShelterRuntimeSession(ConstructionDefinitionCatalog catalog,
            ConstructionEnclosureBlockingPolicy enclosurePolicy, ConstructionRoomAnalysisLimits limits,
            ConstructionRoofSupportPolicy roofPolicy, ConstructionDefinitionId floorId,
            IShelterRoomQualificationPolicy roomPolicy, IRoomShelterIdentityStrategy roomIdentity,
            ShelterFamiliarityRules familiarityRules, ShelterHomeState homeState)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _enclosurePolicy = enclosurePolicy ?? throw new ArgumentNullException(nameof(enclosurePolicy));
            _limits = limits;
            _roofPolicy = roofPolicy ?? throw new ArgumentNullException(nameof(roofPolicy));
            _floorId = floorId;
            if (catalog.Require(floorId).SpaceKind != ConstructionSpaceKind.Surface)
                throw new ArgumentException("Shelter Floor must occupy Surface space.", nameof(floorId));
            _roomPolicy = roomPolicy ?? throw new ArgumentNullException(nameof(roomPolicy));
            _roomIdentity = roomIdentity ?? throw new ArgumentNullException(nameof(roomIdentity));
            _familiarityRules = familiarityRules ?? throw new ArgumentNullException(nameof(familiarityRules));
            HomeState = homeState ?? throw new ArgumentNullException(nameof(homeState));
        }

        public void Recalculate(IEnumerable<CompletedStructureState> completedStructures,
            ConstructionDoorRegistry doors = null, ConstructionEdgeAddress? affectedEdge = null,
            string reason = "Construction changed")
        {
            if (completedStructures == null) throw new ArgumentNullException(nameof(completedStructures));
            var structures = new List<CompletedStructureState>();
            foreach (CompletedStructureState structure in completedStructures) structures.Add(structure);
            ConstructionDerivedRooms rooms = ConstructionDerivedRoomBuilder.Build(
                _catalog, structures, _enclosurePolicy, _limits, _roofPolicy, doors,
                affectedEdge.HasValue ? new[] { affectedEdge.Value } : null);
            ShelterEvaluation updated = _roomPolicy != null
                ? RoomShelterEvaluator.Evaluate(structures, _floorId, rooms, _roomPolicy, _roomIdentity)
                : ShelterEvaluator.Evaluate(
                _catalog,
                structures,
                rooms,
                _candidatePolicy,
                _candidateIdentityStrategy);
            if (affectedEdge.HasValue)
            {
                // Only adjacent old candidates can change topology when this Edge toggles.
                // Other rooms returned by the affected scope replace themselves by stable ID.
                ConstructionEdgeAddress edge = affectedEdge.Value;
                var merged = new Dictionary<ShelterId, ShelterAssessment>();
                foreach (ShelterAssessment old in _evaluation.Assessments)
                    if (!old.Room.Room.Contains(edge.FirstCell) && !old.Room.Room.Contains(edge.SecondCell))
                        merged.Add(old.Id, old);
                foreach (ShelterAssessment candidate in updated.Assessments) merged[candidate.Id] = candidate;
                updated = new ShelterEvaluation(merged.Values);
                LocalRecalculations++;
            }
            else FullRecalculations++;
            _evaluation = updated;
            LastInvalidationReason = reason;
            LastScopesAnalyzed = rooms.ScopesAnalyzed;
            LastCellsAnalyzed = rooms.CellsAnalyzed;
            ScopesAnalyzed += rooms.ScopesAnalyzed;
            CellsAnalyzed += rooms.CellsAnalyzed;
            HomeState.RecalculatePrimary(CaptureValidIds());
            // Current presence is intentionally reconciled by the next fixed tick, where the
            // player's cell is available and an Exited/Changed transition can be emitted. Until
            // then, rest/night credit is guarded by ContainsValid.
        }

        public ShelterPresenceTransition AdvanceFixedTick(
            WorldCellCoordinate playerCell,
            long elapsedTicks)
        {
            if (elapsedTicks < 0L) throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            ShelterId previous = CurrentShelterId;
            bool hadPrevious = HasCurrentShelter;
            HasCurrentShelter = _evaluation.TryFindValidAt(playerCell, out ShelterAssessment shelter);
            CurrentShelterId = HasCurrentShelter ? shelter.Id : default;
            if (HasCurrentShelter)
                HomeState.GetOrCreate(CurrentShelterId).RecordPresence(elapsedTicks, _familiarityRules);

            if (hadPrevious && HasCurrentShelter && !previous.Equals(CurrentShelterId))
                return new ShelterPresenceTransition(ShelterPresenceTransitionKind.Changed, previous, CurrentShelterId);
            if (!hadPrevious && HasCurrentShelter)
                return new ShelterPresenceTransition(ShelterPresenceTransitionKind.Entered, default, CurrentShelterId);
            if (hadPrevious && !HasCurrentShelter)
                return new ShelterPresenceTransition(ShelterPresenceTransitionKind.Exited, previous, default);
            return new ShelterPresenceTransition(ShelterPresenceTransitionKind.None, previous, CurrentShelterId);
        }

        public bool RecordCompletedRest()
        {
            if (!HasCurrentShelter || !ContainsValid(CurrentShelterId)) return false;
            HomeState.GetOrCreate(CurrentShelterId).RecordCompletedRest();
            HomeState.RecalculatePrimary(CaptureValidIds());
            return true;
        }

        public bool RecordCompletedNight()
        {
            if (!HasCurrentShelter || !ContainsValid(CurrentShelterId)) return false;
            HomeState.GetOrCreate(CurrentShelterId).RecordCompletedNight();
            HomeState.RecalculatePrimary(CaptureValidIds());
            return true;
        }

        public bool ContainsValid(ShelterId id)
        {
            for (int i = 0; i < _evaluation.Assessments.Count; i++)
                if (_evaluation.Assessments[i].IsValid && _evaluation.Assessments[i].Id.Equals(id)) return true;
            return false;
        }

        private IReadOnlyList<ShelterId> CaptureValidIds()
        {
            var ids = new List<ShelterId>();
            for (int i = 0; i < _evaluation.Assessments.Count; i++)
                if (_evaluation.Assessments[i].IsValid) ids.Add(_evaluation.Assessments[i].Id);
            ids.Sort();
            return ids.AsReadOnly();
        }

        public void ClearPresence()
        {
            HasCurrentShelter = false;
            CurrentShelterId = default;
        }
    }

    /// <summary>
    /// Optional, reversible bridge for a composition that elects to keep derived shelter geometry
    /// synchronized with completed construction changes. It owns neither Construction nor Shelter
    /// state and is not active merely because this type exists.
    /// </summary>
    public sealed class ConstructionShelterRuntimeBridge
    {
        private readonly ConstructionRuntimeSession _construction;
        private readonly ShelterRuntimeSession _shelters;
        private long _observedRevision = -1L;

        public ConstructionShelterRuntimeBridge(
            ConstructionRuntimeSession construction,
            ShelterRuntimeSession shelters)
        {
            _construction = construction ?? throw new ArgumentNullException(nameof(construction));
            _shelters = shelters ?? throw new ArgumentNullException(nameof(shelters));
            RefreshIfChanged();
        }

        public bool RefreshIfChanged()
        {
            if (_observedRevision == _construction.ShelterRevision) return false;
            bool local = _observedRevision >= 0 && _observedRevision + 1 == _construction.ShelterRevision;
            _shelters.Recalculate(_construction.World.CaptureCanonicalStructures(), _construction.Doors,
                local ? _construction.ShelterAffectedEdge : null, _construction.ShelterInvalidationReason);
            _observedRevision = _construction.ShelterRevision;
            return true;
        }

        public ShelterPresenceTransition AdvanceFixedTick(
            WorldCellCoordinate playerCell,
            long elapsedTicks)
        {
            RefreshIfChanged();
            return _shelters.AdvanceFixedTick(playerCell, elapsedTicks);
        }
    }
}
