using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Persistence
{
    public readonly struct PlayerFoodSnapshot : IEquatable<PlayerFoodSnapshot>
    {
        public PlayerFoodSnapshot(PlayerFoodState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            MaximumSatiety = state.MaximumSatiety;
            CurrentSatiety = state.CurrentSatiety;
            CurrentTick = state.CurrentTick;
            NextSatietyLossTick = state.NextSatietyLossTick;
        }

        public PlayerFoodSnapshot(int maximumSatiety, int currentSatiety, long currentTick, long nextSatietyLossTick)
        {
            var validated = new PlayerFoodState(maximumSatiety, currentSatiety, currentTick, nextSatietyLossTick);
            MaximumSatiety = validated.MaximumSatiety;
            CurrentSatiety = validated.CurrentSatiety;
            CurrentTick = validated.CurrentTick;
            NextSatietyLossTick = validated.NextSatietyLossTick;
        }

        public int MaximumSatiety { get; }
        public int CurrentSatiety { get; }
        public long CurrentTick { get; }
        public long NextSatietyLossTick { get; }

        public static PlayerFoodSnapshot CreateFull(long fixedTick) => new PlayerFoodSnapshot(PlayerFoodState.CreateFullAt(fixedTick));
        public PlayerFoodState Restore() => new PlayerFoodState(MaximumSatiety, CurrentSatiety, CurrentTick, NextSatietyLossTick);

        public bool Equals(PlayerFoodSnapshot other) => MaximumSatiety == other.MaximumSatiety
            && CurrentSatiety == other.CurrentSatiety
            && CurrentTick == other.CurrentTick
            && NextSatietyLossTick == other.NextSatietyLossTick;
        public override bool Equals(object obj) => obj is PlayerFoodSnapshot other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaximumSatiety;
                hash = (hash * 397) ^ CurrentSatiety;
                hash = (hash * 397) ^ CurrentTick.GetHashCode();
                return (hash * 397) ^ NextSatietyLossTick.GetHashCode();
            }
        }
    }

    public readonly struct PerishableBatchSnapshot : IEquatable<PerishableBatchSnapshot>
    {
        internal PerishableBatchSnapshot(PerishableBatchState batch)
        {
            Id = batch.Id;
            ContainerId = batch.ContainerId;
            DefinitionId = batch.DefinitionId;
            Quantity = batch.Quantity;
            AccumulatedDecayMilliTicks = batch.AccumulatedDecayMilliTicks;
            LastEvaluatedTick = batch.LastEvaluatedTick;
        }

        private PerishableBatchSnapshot(
            FoodBatchId id,
            ContainerId containerId,
            ItemDefinitionId definitionId,
            int quantity,
            long accumulatedDecayMilliTicks,
            long lastEvaluatedTick)
        {
            var validated = new PerishableBatchState(id, containerId, definitionId, quantity, accumulatedDecayMilliTicks, lastEvaluatedTick);
            Id = validated.Id;
            ContainerId = validated.ContainerId;
            DefinitionId = validated.DefinitionId;
            Quantity = validated.Quantity;
            AccumulatedDecayMilliTicks = validated.AccumulatedDecayMilliTicks;
            LastEvaluatedTick = validated.LastEvaluatedTick;
        }

        public FoodBatchId Id { get; }
        public ContainerId ContainerId { get; }
        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
        public long AccumulatedDecayMilliTicks { get; }
        public long LastEvaluatedTick { get; }

        internal static PerishableBatchSnapshot Restore(FoodBatchId id, ContainerId containerId, ItemDefinitionId definitionId, int quantity, long decay, long lastTick)
            => new PerishableBatchSnapshot(id, containerId, definitionId, quantity, decay, lastTick);

        internal PerishableBatchState RestoreState() => new PerishableBatchState(Id, ContainerId, DefinitionId, Quantity, AccumulatedDecayMilliTicks, LastEvaluatedTick);

        internal static int Compare(PerishableBatchSnapshot left, PerishableBatchSnapshot right)
        {
            int comparison = left.ContainerId.CompareTo(right.ContainerId);
            if (comparison != 0) return comparison;
            comparison = left.DefinitionId.CompareTo(right.DefinitionId);
            if (comparison != 0) return comparison;
            return left.Id.CompareTo(right.Id);
        }

        public bool Equals(PerishableBatchSnapshot other) => Id.Equals(other.Id)
            && ContainerId.Equals(other.ContainerId)
            && DefinitionId.Equals(other.DefinitionId)
            && Quantity == other.Quantity
            && AccumulatedDecayMilliTicks == other.AccumulatedDecayMilliTicks
            && LastEvaluatedTick == other.LastEvaluatedTick;
        public override bool Equals(object obj) => obj is PerishableBatchSnapshot other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }

    public sealed class PerishableInventorySnapshot
    {
        private readonly IReadOnlyList<PerishableBatchSnapshot> _batches;

        public PerishableInventorySnapshot(PerishableInventoryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var batches = new List<PerishableBatchSnapshot>(state.Batches.Count);
            for (int index = 0; index < state.Batches.Count; index++) batches.Add(new PerishableBatchSnapshot(state.Batches[index]));
            batches.Sort(PerishableBatchSnapshot.Compare);
            _batches = Array.AsReadOnly(batches.ToArray());
        }

        private PerishableInventorySnapshot(IEnumerable<PerishableBatchSnapshot> batches)
        {
            var canonical = new List<PerishableBatchSnapshot>(batches ?? throw new ArgumentNullException(nameof(batches)));
            canonical.Sort(PerishableBatchSnapshot.Compare);
            var ids = new HashSet<FoodBatchId>();
            for (int index = 0; index < canonical.Count; index++)
            {
                if (!ids.Add(canonical[index].Id)) throw new ArgumentException($"Duplicate food batch '{canonical[index].Id}'.", nameof(batches));
            }
            _batches = Array.AsReadOnly(canonical.ToArray());
        }

        public IReadOnlyList<PerishableBatchSnapshot> Batches => _batches;
        public static PerishableInventorySnapshot Empty => new PerishableInventorySnapshot(Array.Empty<PerishableBatchSnapshot>());

        internal static PerishableInventorySnapshot Restore(IEnumerable<PerishableBatchSnapshot> batches) => new PerishableInventorySnapshot(batches);

        public PerishableInventoryState RestoreState()
        {
            var batches = new List<PerishableBatchState>(_batches.Count);
            for (int index = 0; index < _batches.Count; index++) batches.Add(_batches[index].RestoreState());
            return new PerishableInventoryState(batches);
        }
    }
}
