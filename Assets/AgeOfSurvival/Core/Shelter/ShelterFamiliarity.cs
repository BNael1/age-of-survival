using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Shelter
{
    public enum ShelterFamiliarityLevel
    {
        Unknown = 0,
        Familiar = 1,
        VeryFamiliar = 2
    }

    /// <summary>
    /// Pure mapping of the validated sleep ceiling. Material comfort computation remains outside
    /// this contract until its gameplay rules are specified.
    /// </summary>
    public static class ShelterSleepRecoveryLimit
    {
        public const int UnknownPercent = 75;
        public const int FamiliarPercent = 85;
        public const int VeryFamiliarPercent = 90;
        public const int PrimaryHomePercent = 100;

        public static int MaximumPercent(
            ShelterFamiliarityLevel familiarity,
            bool isPrimaryHome)
        {
            if (!Enum.IsDefined(typeof(ShelterFamiliarityLevel), familiarity))
                throw new ArgumentOutOfRangeException(nameof(familiarity));
            if (isPrimaryHome) return PrimaryHomePercent;
            switch (familiarity)
            {
                case ShelterFamiliarityLevel.Unknown:
                    return UnknownPercent;
                case ShelterFamiliarityLevel.Familiar:
                    return FamiliarPercent;
                case ShelterFamiliarityLevel.VeryFamiliar:
                    return VeryFamiliarPercent;
                default:
                    throw new ArgumentOutOfRangeException(nameof(familiarity));
            }
        }
    }

    public sealed class ShelterFamiliarityRules
    {
        public const int MaximumHalfPoints = 200;
        public const int FamiliarThresholdHalfPoints = 60;
        public const int VeryFamiliarThresholdHalfPoints = 140;
        public const int RestRewardHalfPoints = 2;
        public const int NightRewardHalfPoints = 16;
        public const int RequiredHomeNights = 3;
        public const int RequiredHomeAdvantageHalfPoints = 30;

        public ShelterFamiliarityRules(long ticksPerGameHour)
        {
            if (ticksPerGameHour <= 0L) throw new ArgumentOutOfRangeException(nameof(ticksPerGameHour));
            TicksPerGameHour = ticksPerGameHour;
        }

        public long TicksPerGameHour { get; }
    }

    public sealed class ShelterFamiliarityState
    {
        public ShelterFamiliarityState(ShelterId shelterId)
            : this(shelterId, 0L, 0L, 0, 0, 0) { }

        public ShelterFamiliarityState(
            ShelterId shelterId,
            long totalPresenceTicks,
            long presenceProgressTicks,
            int completedRestCount,
            int completedNightCount,
            int familiarityHalfPoints)
        {
            if (!shelterId.IsValid) throw new ArgumentException("A valid shelter identifier is required.", nameof(shelterId));
            if (totalPresenceTicks < 0L) throw new ArgumentOutOfRangeException(nameof(totalPresenceTicks));
            if (presenceProgressTicks < 0L) throw new ArgumentOutOfRangeException(nameof(presenceProgressTicks));
            if (presenceProgressTicks > totalPresenceTicks)
                throw new ArgumentException(
                    "Presence progress cannot exceed total presence.",
                    nameof(presenceProgressTicks));
            if (completedRestCount < 0) throw new ArgumentOutOfRangeException(nameof(completedRestCount));
            if (completedNightCount < 0) throw new ArgumentOutOfRangeException(nameof(completedNightCount));
            if (familiarityHalfPoints < 0 || familiarityHalfPoints > ShelterFamiliarityRules.MaximumHalfPoints)
                throw new ArgumentOutOfRangeException(nameof(familiarityHalfPoints));
            ShelterId = shelterId;
            TotalPresenceTicks = totalPresenceTicks;
            PresenceProgressTicks = presenceProgressTicks;
            CompletedRestCount = completedRestCount;
            CompletedNightCount = completedNightCount;
            FamiliarityHalfPoints = familiarityHalfPoints;
        }

        public ShelterId ShelterId { get; }
        public long TotalPresenceTicks { get; private set; }
        public long PresenceProgressTicks { get; private set; }
        public int CompletedRestCount { get; private set; }
        public int CompletedNightCount { get; private set; }
        public int FamiliarityHalfPoints { get; private set; }
        public ShelterFamiliarityLevel Level => FamiliarityHalfPoints >= ShelterFamiliarityRules.VeryFamiliarThresholdHalfPoints
            ? ShelterFamiliarityLevel.VeryFamiliar
            : FamiliarityHalfPoints >= ShelterFamiliarityRules.FamiliarThresholdHalfPoints
                ? ShelterFamiliarityLevel.Familiar
                : ShelterFamiliarityLevel.Unknown;

        public void RecordPresence(long elapsedTicks, ShelterFamiliarityRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (elapsedTicks < 0L) throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            if (elapsedTicks == 0L) return;
            TotalPresenceTicks = checked(TotalPresenceTicks + elapsedTicks);
            long progress = checked(PresenceProgressTicks + elapsedTicks);
            long earnedHalfPoints = progress / rules.TicksPerGameHour;
            PresenceProgressTicks = progress % rules.TicksPerGameHour;
            AddHalfPoints(earnedHalfPoints);
        }

        public void RecordCompletedRest()
        {
            CompletedRestCount = checked(CompletedRestCount + 1);
            AddHalfPoints(ShelterFamiliarityRules.RestRewardHalfPoints);
        }

        public void RecordCompletedNight()
        {
            CompletedNightCount = checked(CompletedNightCount + 1);
            AddHalfPoints(ShelterFamiliarityRules.NightRewardHalfPoints);
        }

        private void AddHalfPoints(long value)
        {
            if (value <= 0L || FamiliarityHalfPoints == ShelterFamiliarityRules.MaximumHalfPoints) return;
            long result = FamiliarityHalfPoints + value;
            FamiliarityHalfPoints = result >= ShelterFamiliarityRules.MaximumHalfPoints
                ? ShelterFamiliarityRules.MaximumHalfPoints
                : (int)result;
        }
    }

    public sealed class ShelterHomeState
    {
        private readonly Dictionary<ShelterId, ShelterFamiliarityState> _histories =
            new Dictionary<ShelterId, ShelterFamiliarityState>();

        public ShelterHomeState(IEnumerable<ShelterFamiliarityState> histories)
            : this(histories, default, false) { }

        public ShelterHomeState(
            IEnumerable<ShelterFamiliarityState> histories,
            ShelterId primaryShelterId,
            bool hasPrimaryShelter)
        {
            if (histories == null) throw new ArgumentNullException(nameof(histories));
            foreach (ShelterFamiliarityState history in histories)
            {
                if (history == null) throw new ArgumentException("Shelter histories must not contain null entries.", nameof(histories));
                if (_histories.ContainsKey(history.ShelterId))
                    throw new ArgumentException("Shelter history identifiers must be unique.", nameof(histories));
                _histories.Add(history.ShelterId, history);
            }
            if (hasPrimaryShelter && (!primaryShelterId.IsValid || !_histories.ContainsKey(primaryShelterId)))
                throw new ArgumentException("The primary shelter must refer to a known history.", nameof(primaryShelterId));
            PrimaryShelterId = primaryShelterId;
            HasPrimaryShelter = hasPrimaryShelter;
        }

        public bool HasPrimaryShelter { get; private set; }
        public ShelterId PrimaryShelterId { get; private set; }

        public ShelterFamiliarityState GetOrCreate(ShelterId id)
        {
            if (!id.IsValid) throw new ArgumentException("A valid shelter identifier is required.", nameof(id));
            if (!_histories.TryGetValue(id, out ShelterFamiliarityState state))
            {
                state = new ShelterFamiliarityState(id);
                _histories.Add(id, state);
            }
            return state;
        }

        public bool TryFind(ShelterId id, out ShelterFamiliarityState state)
        {
            if (!id.IsValid)
            {
                state = null;
                return false;
            }
            return _histories.TryGetValue(id, out state);
        }

        public IReadOnlyList<ShelterFamiliarityState> CaptureCanonicalHistories()
        {
            var result = new List<ShelterFamiliarityState>(_histories.Values);
            result.Sort((left, right) => left.ShelterId.CompareTo(right.ShelterId));
            return result.AsReadOnly();
        }

        public bool RecalculatePrimary(IEnumerable<ShelterId> validShelterIds)
        {
            if (validShelterIds == null) throw new ArgumentNullException(nameof(validShelterIds));
            var valid = new HashSet<ShelterId>();
            foreach (ShelterId id in validShelterIds)
            {
                if (!id.IsValid) throw new ArgumentException("Valid shelter IDs must be valid.", nameof(validShelterIds));
                valid.Add(id);
            }

            ShelterId previous = PrimaryShelterId;
            bool previousPresent = HasPrimaryShelter;
            if (!HasPrimaryShelter
                || !valid.Contains(PrimaryShelterId)
                || !_histories.TryGetValue(
                    PrimaryShelterId,
                    out ShelterFamiliarityState current))
            {
                return false;
            }

            ShelterFamiliarityState selected = current;
            foreach (ShelterId id in valid)
            {
                if (!_histories.TryGetValue(id, out ShelterFamiliarityState candidate)
                    || candidate.CompletedNightCount < ShelterFamiliarityRules.RequiredHomeNights)
                    continue;
                if (candidate.ShelterId.Equals(current.ShelterId)
                    || candidate.FamiliarityHalfPoints
                        < current.FamiliarityHalfPoints
                            + ShelterFamiliarityRules.RequiredHomeAdvantageHalfPoints)
                    continue;
                if (selected == current || IsBetter(candidate, selected))
                    selected = candidate;
            }

            PrimaryShelterId = selected.ShelterId;
            return previousPresent != HasPrimaryShelter
                || !previous.Equals(PrimaryShelterId);
        }

        public static ShelterHomeState CreateInitialCamp(ShelterId shelterId)
        {
            var history = new ShelterFamiliarityState(shelterId, 0L, 0L, 0, 3, 140);
            return new ShelterHomeState(new[] { history }, shelterId, true);
        }

        private static bool IsBetter(ShelterFamiliarityState candidate, ShelterFamiliarityState selected)
        {
            int familiarity = candidate.FamiliarityHalfPoints.CompareTo(selected.FamiliarityHalfPoints);
            if (familiarity != 0) return familiarity > 0;
            int nights = candidate.CompletedNightCount.CompareTo(selected.CompletedNightCount);
            if (nights != 0) return nights > 0;
            return candidate.ShelterId.CompareTo(selected.ShelterId) < 0;
        }
    }
}
