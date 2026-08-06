using System;

namespace AgeOfSurvival.Core.Characters
{
    /// <summary>
    /// Active rules for the first player-health implementation.
    /// All durations are expressed in fixed simulation ticks.
    /// </summary>
    public static class PlayerHealthRules
    {
        public const int DefaultMaximumHealth = 100;
        public const long RegenerationDelayTicks = 480L;
        public const long RegenerationIntervalTicks = 30L;

        public static long FirstRegenerationTickAfter(long damageTick)
        {
            if (damageTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageTick),
                    damageTick,
                    "The damage tick must be non-negative.");
            }

            return checked(
                damageTick
                + RegenerationDelayTicks
                + RegenerationIntervalTicks);
        }
    }

    /// <summary>
    /// Mutable health state owned by the pure simulation.
    /// Unity adapters may observe it but do not own it.
    /// </summary>
    public sealed class PlayerHealthState
    {
        public PlayerHealthState(int maximumHealth)
            : this(maximumHealth, maximumHealth, 0L, null)
        {
        }

        public PlayerHealthState(
            int maximumHealth,
            int currentHealth,
            long currentTick,
            long? nextRegenerationTick)
        {
            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHealth),
                    maximumHealth,
                    "Maximum health must be greater than zero.");
            }

            if (currentHealth < 0 || currentHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentHealth),
                    currentHealth,
                    "Current health must be between zero and maximum health.");
            }

            if (currentTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTick),
                    currentTick,
                    "The current tick must be non-negative.");
            }

            bool requiresRegeneration =
                currentHealth > 0 && currentHealth < maximumHealth;
            if (requiresRegeneration != nextRegenerationTick.HasValue)
            {
                throw new ArgumentException(
                    requiresRegeneration
                        ? "An injured living player requires a regeneration tick."
                        : "A dead or fully healed player cannot have a regeneration tick.",
                    nameof(nextRegenerationTick));
            }

            if (nextRegenerationTick.HasValue
                && nextRegenerationTick.Value <= currentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextRegenerationTick),
                    nextRegenerationTick,
                    "The next regeneration tick must be after the current tick.");
            }

            MaximumHealth = maximumHealth;
            CurrentHealth = currentHealth;
            CurrentTick = currentTick;
            NextRegenerationTick = nextRegenerationTick;
        }

        public int MaximumHealth { get; }
        public int CurrentHealth { get; private set; }
        public long CurrentTick { get; private set; }
        public long? NextRegenerationTick { get; private set; }
        public bool IsDead => CurrentHealth == 0;
        public bool IsFullHealth => CurrentHealth == MaximumHealth;

        internal void SetCurrentTick(long currentTick)
        {
            CurrentTick = currentTick;
        }

        internal void SetHealth(
            int currentHealth,
            long? nextRegenerationTick)
        {
            CurrentHealth = currentHealth;
            NextRegenerationTick = nextRegenerationTick;
        }
    }

    /// <summary>
    /// Immutable description of one health operation.
    /// AppliedDelta is negative for damage and positive for healing.
    /// </summary>
    public readonly struct PlayerHealthChangeResult
    {
        public PlayerHealthChangeResult(
            int previousHealth,
            int currentHealth,
            bool respawned)
        {
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            Respawned = respawned;
        }

        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public int AppliedDelta => CurrentHealth - PreviousHealth;
        public bool Changed => AppliedDelta != 0 || Respawned;
        public bool Died => PreviousHealth > 0 && CurrentHealth == 0;
        public bool Respawned { get; }
    }

    public static class PlayerHealthOperations
    {
        public static PlayerHealthChangeResult AdvanceToTick(
            PlayerHealthState state,
            long currentTick)
        {
            RequireState(state);
            RequireCurrentOrFutureTick(state, currentTick);

            int previousHealth = state.CurrentHealth;
            if (currentTick == state.CurrentTick)
            {
                return Result(previousHealth, state, false);
            }

            if (state.IsDead
                || state.IsFullHealth
                || !state.NextRegenerationTick.HasValue)
            {
                state.SetCurrentTick(currentTick);
                return Result(previousHealth, state, false);
            }

            long nextRegenerationTick =
                state.NextRegenerationTick.Value;
            if (nextRegenerationTick > currentTick)
            {
                state.SetCurrentTick(currentTick);
                return Result(previousHealth, state, false);
            }

            long dueIntervals =
                ((currentTick - nextRegenerationTick)
                    / PlayerHealthRules.RegenerationIntervalTicks)
                + 1L;
            int missingHealth =
                state.MaximumHealth - state.CurrentHealth;
            int regeneratedHealth = dueIntervals >= missingHealth
                ? missingHealth
                : checked((int)dueIntervals);
            int updatedHealth =
                checked(state.CurrentHealth + regeneratedHealth);

            long? updatedNextTick = null;
            if (updatedHealth < state.MaximumHealth)
            {
                long intervalOffset = checked(
                    dueIntervals
                    * PlayerHealthRules.RegenerationIntervalTicks);
                updatedNextTick = checked(
                    nextRegenerationTick + intervalOffset);
            }

            state.SetHealth(updatedHealth, updatedNextTick);
            state.SetCurrentTick(currentTick);
            return Result(previousHealth, state, false);
        }

        public static PlayerHealthChangeResult ApplyDamage(
            PlayerHealthState state,
            int amount,
            long currentTick)
        {
            RequireState(state);
            RequirePositiveAmount(amount);
            RequireCurrentOrFutureTick(state, currentTick);

            int originalHealth = state.CurrentHealth;
            long originalTick = state.CurrentTick;
            long? originalNextRegenerationTick =
                state.NextRegenerationTick;

            try
            {
                AdvanceToTick(state, currentTick);

                int previousHealth = state.CurrentHealth;
                if (state.IsDead)
                {
                    return Result(previousHealth, state, false);
                }

                int appliedDamage =
                    Math.Min(amount, state.CurrentHealth);
                int updatedHealth =
                    state.CurrentHealth - appliedDamage;
                long? nextRegenerationTick = updatedHealth > 0
                    ? PlayerHealthRules.FirstRegenerationTickAfter(
                        currentTick)
                    : (long?)null;

                state.SetHealth(
                    updatedHealth,
                    nextRegenerationTick);
                return Result(previousHealth, state, false);
            }
            catch (OverflowException)
            {
                state.SetHealth(
                    originalHealth,
                    originalNextRegenerationTick);
                state.SetCurrentTick(originalTick);
                throw;
            }
        }

        public static PlayerHealthChangeResult Heal(
            PlayerHealthState state,
            int amount,
            long currentTick)
        {
            RequirePositiveAmount(amount);
            AdvanceToTick(state, currentTick);

            int previousHealth = state.CurrentHealth;
            if (state.IsDead || state.IsFullHealth)
            {
                return Result(previousHealth, state, false);
            }

            int missingHealth =
                state.MaximumHealth - state.CurrentHealth;
            int appliedHealing = Math.Min(amount, missingHealth);
            int updatedHealth =
                checked(state.CurrentHealth + appliedHealing);
            long? nextRegenerationTick =
                updatedHealth == state.MaximumHealth
                    ? (long?)null
                    : state.NextRegenerationTick;

            state.SetHealth(updatedHealth, nextRegenerationTick);
            return Result(previousHealth, state, false);
        }

        public static PlayerHealthChangeResult Respawn(
            PlayerHealthState state,
            long currentTick)
        {
            AdvanceToTick(state, currentTick);

            int previousHealth = state.CurrentHealth;
            if (!state.IsDead)
            {
                return Result(previousHealth, state, false);
            }

            state.SetHealth(state.MaximumHealth, null);
            return Result(previousHealth, state, true);
        }

        private static PlayerHealthChangeResult Result(
            int previousHealth,
            PlayerHealthState state,
            bool respawned)
        {
            return new PlayerHealthChangeResult(
                previousHealth,
                state.CurrentHealth,
                respawned);
        }

        private static void RequireState(PlayerHealthState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
        }

        private static void RequireCurrentOrFutureTick(
            PlayerHealthState state,
            long currentTick)
        {
            if (currentTick < state.CurrentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTick),
                    currentTick,
                    "Health simulation ticks cannot move backwards.");
            }
        }

        private static void RequirePositiveAmount(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Health operation amounts must be greater than zero.");
            }
        }
    }
}
