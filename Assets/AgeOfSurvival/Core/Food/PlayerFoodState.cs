using System;

namespace AgeOfSurvival.Core.Food
{
    /// <summary>
    /// Prototype hunger rules. Values are centralized and intentionally provisional.
    /// At the fixed 60 Hz runtime, 216,000 ticks correspond to one real hour; the
    /// current prototype loses 100 satiety points across that interval.
    /// </summary>
    public static class PlayerFoodRules
    {
        public const int DefaultMaximumSatiety = 100;
        public const long SatietyLossIntervalTicks = 2160L;

        public static long FirstSatietyLossTickAfter(long currentTick)
        {
            if (currentTick < 0L) throw new ArgumentOutOfRangeException(nameof(currentTick));
            return checked(currentTick + SatietyLossIntervalTicks);
        }
    }

    public sealed class PlayerFoodState
    {
        public PlayerFoodState(int maximumSatiety = PlayerFoodRules.DefaultMaximumSatiety)
            : this(
                maximumSatiety,
                maximumSatiety,
                0L,
                PlayerFoodRules.FirstSatietyLossTickAfter(0L))
        {
        }

        public PlayerFoodState(
            int maximumSatiety,
            int currentSatiety,
            long currentTick,
            long nextSatietyLossTick)
        {
            if (maximumSatiety <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSatiety));
            if (currentSatiety < 0 || currentSatiety > maximumSatiety) throw new ArgumentOutOfRangeException(nameof(currentSatiety));
            if (currentTick < 0L) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (nextSatietyLossTick <= currentTick) throw new ArgumentOutOfRangeException(nameof(nextSatietyLossTick));

            MaximumSatiety = maximumSatiety;
            CurrentSatiety = currentSatiety;
            CurrentTick = currentTick;
            NextSatietyLossTick = nextSatietyLossTick;
        }

        public int MaximumSatiety { get; }
        public int CurrentSatiety { get; private set; }
        public long CurrentTick { get; private set; }
        public long NextSatietyLossTick { get; private set; }
        public bool IsFull => CurrentSatiety == MaximumSatiety;
        public bool IsEmpty => CurrentSatiety == 0;

        public static PlayerFoodState CreateFullAt(long currentTick)
        {
            return new PlayerFoodState(
                PlayerFoodRules.DefaultMaximumSatiety,
                PlayerFoodRules.DefaultMaximumSatiety,
                currentTick,
                PlayerFoodRules.FirstSatietyLossTickAfter(currentTick));
        }

        internal void Set(int currentSatiety, long currentTick, long nextSatietyLossTick)
        {
            CurrentSatiety = currentSatiety;
            CurrentTick = currentTick;
            NextSatietyLossTick = nextSatietyLossTick;
        }
    }

    public readonly struct PlayerFoodChangeResult
    {
        public PlayerFoodChangeResult(int previousSatiety, int currentSatiety)
        {
            PreviousSatiety = previousSatiety;
            CurrentSatiety = currentSatiety;
        }

        public int PreviousSatiety { get; }
        public int CurrentSatiety { get; }
        public int AppliedDelta => CurrentSatiety - PreviousSatiety;
        public bool Changed => AppliedDelta != 0;
    }

    public static class PlayerFoodOperations
    {
        public static PlayerFoodChangeResult AdvanceToTick(PlayerFoodState state, long currentTick)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (currentTick < state.CurrentTick)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTick), currentTick, "Food-need simulation ticks cannot move backwards.");
            }

            int previous = state.CurrentSatiety;
            if (currentTick < state.NextSatietyLossTick)
            {
                if (currentTick != state.CurrentTick)
                {
                    state.Set(state.CurrentSatiety, currentTick, state.NextSatietyLossTick);
                }
                return new PlayerFoodChangeResult(previous, state.CurrentSatiety);
            }

            long due = checked(((currentTick - state.NextSatietyLossTick) / PlayerFoodRules.SatietyLossIntervalTicks) + 1L);
            int lost = due >= state.CurrentSatiety ? state.CurrentSatiety : checked((int)due);
            long next = checked(state.NextSatietyLossTick + checked(due * PlayerFoodRules.SatietyLossIntervalTicks));
            state.Set(state.CurrentSatiety - lost, currentTick, next);
            return new PlayerFoodChangeResult(previous, state.CurrentSatiety);
        }

        public static PlayerFoodChangeResult AddSatiety(PlayerFoodState state, int amount, long currentTick)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            AdvanceToTick(state, currentTick);
            int previous = state.CurrentSatiety;
            int missing = state.MaximumSatiety - state.CurrentSatiety;
            int applied = Math.Min(amount, missing);
            state.Set(checked(state.CurrentSatiety + applied), state.CurrentTick, state.NextSatietyLossTick);
            return new PlayerFoodChangeResult(previous, state.CurrentSatiety);
        }
    }
}
