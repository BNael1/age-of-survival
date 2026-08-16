using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Shelter;

namespace AgeOfSurvival.Core.Persistence
{
    public readonly struct ShelterHistorySnapshot
    {
        public ShelterHistorySnapshot(
            ShelterId shelterId,
            long totalPresenceTicks,
            long presenceProgressTicks,
            int completedRestCount,
            int completedNightCount,
            int familiarityHalfPoints)
        {
            // Reuse the domain constructor as the canonical validation boundary.
            var validated = new ShelterFamiliarityState(
                shelterId, totalPresenceTicks, presenceProgressTicks,
                completedRestCount, completedNightCount, familiarityHalfPoints);
            ShelterId = validated.ShelterId;
            TotalPresenceTicks = validated.TotalPresenceTicks;
            PresenceProgressTicks = validated.PresenceProgressTicks;
            CompletedRestCount = validated.CompletedRestCount;
            CompletedNightCount = validated.CompletedNightCount;
            FamiliarityHalfPoints = validated.FamiliarityHalfPoints;
        }

        public ShelterId ShelterId { get; }
        public long TotalPresenceTicks { get; }
        public long PresenceProgressTicks { get; }
        public int CompletedRestCount { get; }
        public int CompletedNightCount { get; }
        public int FamiliarityHalfPoints { get; }
        public ShelterFamiliarityState Restore() => new ShelterFamiliarityState(
            ShelterId, TotalPresenceTicks, PresenceProgressTicks,
            CompletedRestCount, CompletedNightCount, FamiliarityHalfPoints);
    }

    public sealed class ShelterSaveSnapshot
    {
        private readonly IReadOnlyList<ShelterHistorySnapshot> _histories;

        public ShelterSaveSnapshot(
            IEnumerable<ShelterHistorySnapshot> histories,
            bool hasPrimaryShelter,
            ShelterId primaryShelterId)
        {
            if (histories == null) throw new ArgumentNullException(nameof(histories));
            var copy = new List<ShelterHistorySnapshot>();
            var ids = new HashSet<ShelterId>();
            foreach (ShelterHistorySnapshot history in histories)
            {
                if (!history.ShelterId.IsValid)
                    throw new ArgumentException("Shelter history identifiers must be valid.", nameof(histories));
                if (!ids.Add(history.ShelterId))
                    throw new ArgumentException("Shelter history identifiers must be unique.", nameof(histories));
                copy.Add(history);
            }
            copy.Sort((left, right) => left.ShelterId.CompareTo(right.ShelterId));
            if (hasPrimaryShelter && (!primaryShelterId.IsValid || !ids.Contains(primaryShelterId)))
                throw new ArgumentException("The primary shelter must refer to a saved history.", nameof(primaryShelterId));
            _histories = copy.AsReadOnly();
            HasPrimaryShelter = hasPrimaryShelter;
            PrimaryShelterId = primaryShelterId;
        }

        public IReadOnlyList<ShelterHistorySnapshot> Histories => _histories;
        public bool HasPrimaryShelter { get; }
        public ShelterId PrimaryShelterId { get; }
        public static ShelterSaveSnapshot Empty => new ShelterSaveSnapshot(Array.Empty<ShelterHistorySnapshot>(), false, default);

        public static ShelterSaveSnapshot Capture(ShelterHomeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            IReadOnlyList<ShelterFamiliarityState> histories = state.CaptureCanonicalHistories();
            var snapshots = new List<ShelterHistorySnapshot>(histories.Count);
            for (int i = 0; i < histories.Count; i++)
            {
                ShelterFamiliarityState history = histories[i];
                snapshots.Add(new ShelterHistorySnapshot(
                    history.ShelterId, history.TotalPresenceTicks, history.PresenceProgressTicks,
                    history.CompletedRestCount, history.CompletedNightCount, history.FamiliarityHalfPoints));
            }
            return new ShelterSaveSnapshot(snapshots, state.HasPrimaryShelter, state.PrimaryShelterId);
        }

        public ShelterHomeState RestoreState()
        {
            var histories = new List<ShelterFamiliarityState>(_histories.Count);
            for (int i = 0; i < _histories.Count; i++) histories.Add(_histories[i].Restore());
            return new ShelterHomeState(histories, PrimaryShelterId, HasPrimaryShelter);
        }
    }
}
