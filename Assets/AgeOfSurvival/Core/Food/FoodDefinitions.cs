using System;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Food
{
    public readonly struct FoodBatchId : IEquatable<FoodBatchId>, IComparable<FoodBatchId>
    {
        public FoodBatchId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A food batch identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(FoodBatchId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(FoodBatchId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FoodBatchId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum FoodFreshness
    {
        Fresh = 0,
        Stale = 1,
        Rotten = 2
    }

    public sealed class ConsumableDefinition
    {
        public ConsumableDefinition(
            int satietyGain,
            int staleSatietyPercent = 60,
            int rottenSatietyPercent = 20)
        {
            if (satietyGain <= 0 || satietyGain > PlayerFoodRules.DefaultMaximumSatiety)
            {
                throw new ArgumentOutOfRangeException(nameof(satietyGain));
            }

            ValidatePercent(staleSatietyPercent, nameof(staleSatietyPercent));
            ValidatePercent(rottenSatietyPercent, nameof(rottenSatietyPercent));
            if (rottenSatietyPercent > staleSatietyPercent)
            {
                throw new ArgumentException("Rotten food cannot restore more satiety than stale food.");
            }

            SatietyGain = satietyGain;
            StaleSatietyPercent = staleSatietyPercent;
            RottenSatietyPercent = rottenSatietyPercent;
        }

        public int SatietyGain { get; }
        public int StaleSatietyPercent { get; }
        public int RottenSatietyPercent { get; }

        public int SatietyFor(FoodFreshness freshness)
        {
            int percent;
            switch (freshness)
            {
                case FoodFreshness.Fresh:
                    percent = 100;
                    break;
                case FoodFreshness.Stale:
                    percent = StaleSatietyPercent;
                    break;
                case FoodFreshness.Rotten:
                    percent = RottenSatietyPercent;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(freshness));
            }

            return checked((SatietyGain * percent) / 100);
        }

        private static void ValidatePercent(int value, string name)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(name, value, "A percentage must be between zero and one hundred.");
            }
        }
    }

    public sealed class NutritionDefinition
    {
        public NutritionDefinition(int calories)
        {
            if (calories < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(calories));
            }

            Calories = calories;
        }

        public int Calories { get; }
    }

    public sealed class PerishableDefinition
    {
        public PerishableDefinition(long freshUntilTicks, long rottenAtTicks)
        {
            if (freshUntilTicks <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(freshUntilTicks));
            }

            if (rottenAtTicks <= freshUntilTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(rottenAtTicks));
            }

            FreshUntilTicks = freshUntilTicks;
            RottenAtTicks = rottenAtTicks;
        }

        public long FreshUntilTicks { get; }
        public long RottenAtTicks { get; }
    }

    /// <summary>
    /// One homogeneous perishable batch. Quantity is kept here instead of averaging
    /// freshness inside the inventory stack. Accumulated decay uses milli-ticks so
    /// future storage multipliers can remain integer and deterministic.
    /// </summary>
    public sealed class PerishableBatchState
    {
        public PerishableBatchState(
            FoodBatchId id,
            ContainerId containerId,
            ItemDefinitionId definitionId,
            int quantity,
            long accumulatedDecayMilliTicks,
            long lastEvaluatedTick)
        {
            if (!id.IsValid) throw new ArgumentException("A valid batch identifier is required.", nameof(id));
            if (!containerId.IsValid) throw new ArgumentException("A valid container identifier is required.", nameof(containerId));
            if (!definitionId.IsValid) throw new ArgumentException("A valid definition identifier is required.", nameof(definitionId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (accumulatedDecayMilliTicks < 0L) throw new ArgumentOutOfRangeException(nameof(accumulatedDecayMilliTicks));
            if (lastEvaluatedTick < 0L) throw new ArgumentOutOfRangeException(nameof(lastEvaluatedTick));

            Id = id;
            ContainerId = containerId;
            DefinitionId = definitionId;
            Quantity = quantity;
            AccumulatedDecayMilliTicks = accumulatedDecayMilliTicks;
            LastEvaluatedTick = lastEvaluatedTick;
        }

        public FoodBatchId Id { get; }
        public ContainerId ContainerId { get; }
        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
        public long AccumulatedDecayMilliTicks { get; }
        public long LastEvaluatedTick { get; }

        public PerishableBatchState With(
            ContainerId containerId,
            int quantity,
            long accumulatedDecayMilliTicks,
            long lastEvaluatedTick,
            FoodBatchId? id = null)
        {
            return new PerishableBatchState(
                id ?? Id,
                containerId,
                DefinitionId,
                quantity,
                accumulatedDecayMilliTicks,
                lastEvaluatedTick);
        }
    }

    public readonly struct FoodFreshnessSummary
    {
        public FoodFreshnessSummary(int fresh, int stale, int rotten)
        {
            Fresh = fresh;
            Stale = stale;
            Rotten = rotten;
        }

        public int Fresh { get; }
        public int Stale { get; }
        public int Rotten { get; }
        public int Total => checked(Fresh + Stale + Rotten);
    }

    public static class FoodSpoilageOperations
    {
        public const int NormalDecayRatePermille = 1000;

        public static long ProjectDecayMilliTicks(
            PerishableBatchState batch,
            long currentTick,
            int decayRatePermille = NormalDecayRatePermille)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (currentTick < batch.LastEvaluatedTick)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTick), currentTick, "Food simulation ticks cannot move backwards.");
            }

            if (decayRatePermille < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decayRatePermille));
            }

            long elapsed = currentTick - batch.LastEvaluatedTick;
            return checked(batch.AccumulatedDecayMilliTicks + checked(elapsed * decayRatePermille));
        }

        public static PerishableBatchState AdvanceToTick(
            PerishableBatchState batch,
            long currentTick,
            int decayRatePermille = NormalDecayRatePermille)
        {
            long projected = ProjectDecayMilliTicks(batch, currentTick, decayRatePermille);
            return batch.With(
                batch.ContainerId,
                batch.Quantity,
                projected,
                currentTick);
        }

        public static FoodFreshness FreshnessAt(
            PerishableBatchState batch,
            PerishableDefinition definition,
            long currentTick,
            int decayRatePermille = NormalDecayRatePermille)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            long decay = ProjectDecayMilliTicks(batch, currentTick, decayRatePermille);
            long freshLimit = checked(definition.FreshUntilTicks * 1000L);
            long rottenLimit = checked(definition.RottenAtTicks * 1000L);
            if (decay < freshLimit) return FoodFreshness.Fresh;
            if (decay < rottenLimit) return FoodFreshness.Stale;
            return FoodFreshness.Rotten;
        }
    }
}
