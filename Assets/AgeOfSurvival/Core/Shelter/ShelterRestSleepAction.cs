using System;

namespace AgeOfSurvival.Core.Shelter
{
    public enum ShelterRestSleepKind { Rest = 0, Sleep = 1 }
    public enum ShelterRestSleepStatus { Active = 0, Completed = 1, Interrupted = 2 }
    public enum ShelterRestSleepReason { None = 0, InvalidRequest = 1, PlayerMoved = 2, ShelterChanged = 3, ShelterInvalidated = 4, SaveAndQuit = 5, AlreadyFinalized = 6 }

    /// <summary>Transient fixed-tick action. It deliberately has no persistence representation.</summary>
    public sealed class ShelterRestSleepAction
    {
        internal ShelterRestSleepAction(ShelterRestSleepKind kind, ShelterId shelterId, long startTick, long durationTicks)
        {
            if (!shelterId.IsValid || startTick < 0 || durationTicks <= 0) throw new ArgumentException("Invalid transient shelter action.");
            Kind = kind; ShelterId = shelterId; StartTick = startTick; DurationTicks = durationTicks; Status = ShelterRestSleepStatus.Active;
        }
        public ShelterRestSleepKind Kind { get; }
        public ShelterId ShelterId { get; }
        public long StartTick { get; }
        public long DurationTicks { get; }
        public ShelterRestSleepStatus Status { get; internal set; }
        public ShelterRestSleepReason Reason { get; internal set; }
        public long? FinalizedTick { get; internal set; }
        public double ProgressAt(long tick) => Status == ShelterRestSleepStatus.Completed ? 1d :
            Math.Max(0d, Math.Min(1d, (double)(Math.Min(tick, FinalizedTick ?? tick) - StartTick) / DurationTicks));
    }

    public static class ShelterRestSleepOperations
    {
        public static ShelterRestSleepAction Start(ShelterRestSleepKind kind, ShelterId shelterId, long tick, long duration)
        {
            if (!shelterId.IsValid || tick < 0 || duration <= 0) return null;
            return new ShelterRestSleepAction(kind, shelterId, tick, duration);
        }
        public static ShelterRestSleepReason Advance(ShelterRestSleepAction action, long tick, ShelterId current,
            bool hasCurrent, bool playerMoved, Func<ShelterId, bool> isValid, Action<ShelterRestSleepKind> complete)
        {
            if (action == null || action.Status != ShelterRestSleepStatus.Active) return ShelterRestSleepReason.AlreadyFinalized;
            ShelterRestSleepReason reason = playerMoved ? ShelterRestSleepReason.PlayerMoved :
                !hasCurrent || !current.Equals(action.ShelterId) ? ShelterRestSleepReason.ShelterChanged :
                isValid == null || !isValid(action.ShelterId) ? ShelterRestSleepReason.ShelterInvalidated : ShelterRestSleepReason.None;
            if (reason != ShelterRestSleepReason.None) { action.Status = ShelterRestSleepStatus.Interrupted; action.Reason = reason; action.FinalizedTick = tick; return reason; }
            if (tick < action.StartTick + action.DurationTicks) return ShelterRestSleepReason.None;
            action.Status = ShelterRestSleepStatus.Completed; action.FinalizedTick = tick; complete?.Invoke(action.Kind); return ShelterRestSleepReason.None;
        }
        public static void Cancel(ShelterRestSleepAction action, long tick, ShelterRestSleepReason reason)
        {
            if (action == null || action.Status != ShelterRestSleepStatus.Active) return;
            action.Status = ShelterRestSleepStatus.Interrupted; action.Reason = reason; action.FinalizedTick = tick;
        }
    }

    public interface IShelterComfortPolicy { int Score(ShelterId shelterId, bool isValid); }
    /// <summary>PROTOTYPE / NON GAMEPLAY FINAL: valid shelter = one comfort point.</summary>
    public sealed class BaselineShelterComfortPolicy : IShelterComfortPolicy
    { public int Score(ShelterId shelterId, bool isValid) => shelterId.IsValid && isValid ? 1 : 0; }
}
