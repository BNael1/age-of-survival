using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Food
{
    /// <summary>
    /// Component-like state for perishable quantities. Inventory keeps one ordinary
    /// aggregate stack per definition/container; this registry keeps homogeneous
    /// freshness batches without averaging them.
    /// </summary>
    public sealed class PerishableInventoryState
    {
        private readonly List<PerishableBatchState> _batches;
        private readonly IReadOnlyList<PerishableBatchState> _readOnlyBatches;

        public PerishableInventoryState()
            : this(Array.Empty<PerishableBatchState>())
        {
        }

        public PerishableInventoryState(IEnumerable<PerishableBatchState> batches)
        {
            if (batches == null) throw new ArgumentNullException(nameof(batches));
            _batches = new List<PerishableBatchState>();
            foreach (PerishableBatchState batch in batches)
            {
                if (batch == null) throw new ArgumentException("Perishable batches must not contain null values.", nameof(batches));
                if (ContainsBatch(batch.Id)) throw new ArgumentException($"Duplicate food batch '{batch.Id}'.", nameof(batches));
                _batches.Add(batch);
            }
            _readOnlyBatches = _batches.AsReadOnly();
        }

        public IReadOnlyList<PerishableBatchState> Batches => _readOnlyBatches;

        public bool ContainsBatch(FoodBatchId id)
        {
            if (!id.IsValid) return false;
            for (int index = 0; index < _batches.Count; index++)
            {
                if (_batches[index].Id.Equals(id)) return true;
            }
            return false;
        }

        public FoodFreshnessSummary GetSummary(
            ContainerId containerId,
            ItemDefinition definition,
            long currentTick,
            int decayRatePermille = FoodSpoilageOperations.NormalDecayRatePermille)
        {
            if (definition == null || definition.Perishable == null)
            {
                return default;
            }

            int fresh = 0;
            int stale = 0;
            int rotten = 0;
            for (int index = 0; index < _batches.Count; index++)
            {
                PerishableBatchState batch = _batches[index];
                if (!batch.ContainerId.Equals(containerId) || !batch.DefinitionId.Equals(definition.Id)) continue;
                switch (FoodSpoilageOperations.FreshnessAt(batch, definition.Perishable, currentTick, decayRatePermille))
                {
                    case FoodFreshness.Fresh:
                        fresh = checked(fresh + batch.Quantity);
                        break;
                    case FoodFreshness.Stale:
                        stale = checked(stale + batch.Quantity);
                        break;
                    default:
                        rotten = checked(rotten + batch.Quantity);
                        break;
                }
            }
            return new FoodFreshnessSummary(fresh, stale, rotten);
        }

        public void ValidateAgainst(PlayerInventoryState inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            for (int batchIndex = 0; batchIndex < _batches.Count; batchIndex++)
            {
                PerishableBatchState batch = _batches[batchIndex];
                ContainerState container = inventory.FindContainer(batch.ContainerId);
                ItemDefinition definition = inventory.FindDefinition(batch.DefinitionId);
                if (container == null || definition == null || definition.Perishable == null || definition.StateKind != ItemStateKind.Stackable)
                {
                    throw new InvalidOperationException($"Food batch '{batch.Id}' does not resolve to a carried perishable stack.");
                }
            }

            for (int containerIndex = 0; containerIndex < inventory.Containers.Count; containerIndex++)
            {
                ContainerState container = inventory.Containers[containerIndex];
                for (int entryIndex = 0; entryIndex < container.Entries.Count; entryIndex++)
                {
                    InventoryEntry entry = container.Entries[entryIndex];
                    if (entry.Kind != ItemStateKind.Stackable) continue;
                    ItemDefinition definition = inventory.FindDefinition(entry.DefinitionId);
                    if (definition == null || definition.Perishable == null) continue;
                    int batchQuantity = QuantityFor(container.Id, definition.Id);
                    if (batchQuantity != entry.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Perishable batches for '{definition.Id}' in '{container.Id}' total {batchQuantity} but inventory stores {entry.Quantity}.");
                    }
                }
            }

            for (int batchIndex = 0; batchIndex < _batches.Count; batchIndex++)
            {
                PerishableBatchState batch = _batches[batchIndex];
                int inventoryQuantity = InventoryOperations.Count(
                    inventory.FindContainer(batch.ContainerId),
                    batch.DefinitionId);
                if (inventoryQuantity <= 0)
                {
                    throw new InvalidOperationException($"Food batch '{batch.Id}' has no matching inventory stack.");
                }
            }
        }

        internal void AddBatch(PerishableBatchState batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (ContainsBatch(batch.Id)) throw new InvalidOperationException($"Food batch '{batch.Id}' already exists.");
            _batches.Add(batch);
        }

        internal void ReplaceWith(PerishableInventoryState replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            _batches.Clear();
            _batches.AddRange(replacement._batches);
        }

        internal PerishableBatchState FindOldest(
            ContainerId containerId,
            ItemDefinition definition,
            long currentTick)
        {
            PerishableBatchState selected = null;
            long selectedDecay = long.MinValue;
            for (int index = 0; index < _batches.Count; index++)
            {
                PerishableBatchState batch = _batches[index];
                if (!batch.ContainerId.Equals(containerId) || !batch.DefinitionId.Equals(definition.Id)) continue;
                long decay = FoodSpoilageOperations.ProjectDecayMilliTicks(batch, currentTick);
                if (selected == null
                    || decay > selectedDecay
                    || (decay == selectedDecay && batch.Id.CompareTo(selected.Id) < 0))
                {
                    selected = batch;
                    selectedDecay = decay;
                }
            }
            return selected;
        }

        internal PerishableInventoryState CreateAfterConsume(
            FoodBatchId batchId,
            long currentTick,
            out PerishableBatchState consumedState)
        {
            var copy = new List<PerishableBatchState>(_batches.Count);
            consumedState = null;
            for (int index = 0; index < _batches.Count; index++)
            {
                PerishableBatchState batch = _batches[index];
                if (!batch.Id.Equals(batchId))
                {
                    copy.Add(batch);
                    continue;
                }

                PerishableBatchState advanced = FoodSpoilageOperations.AdvanceToTick(batch, currentTick);
                consumedState = advanced;
                if (advanced.Quantity > 1)
                {
                    copy.Add(advanced.With(
                        advanced.ContainerId,
                        advanced.Quantity - 1,
                        advanced.AccumulatedDecayMilliTicks,
                        advanced.LastEvaluatedTick));
                }
            }

            if (consumedState == null) throw new InvalidOperationException($"Food batch '{batchId}' is missing.");
            return new PerishableInventoryState(copy);
        }

        internal PerishableInventoryState CreateAfterTransfer(
            ContainerId sourceId,
            ContainerId destinationId,
            ItemDefinition definition,
            int quantity,
            long currentTick,
            Func<FoodBatchId> nextBatchId)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (nextBatchId == null) throw new ArgumentNullException(nameof(nextBatchId));

            var ordered = new List<PerishableBatchState>();
            for (int index = 0; index < _batches.Count; index++)
            {
                PerishableBatchState batch = _batches[index];
                if (batch.ContainerId.Equals(sourceId) && batch.DefinitionId.Equals(definition.Id)) ordered.Add(batch);
            }
            ordered.Sort((left, right) =>
            {
                long leftDecay = FoodSpoilageOperations.ProjectDecayMilliTicks(left, currentTick);
                long rightDecay = FoodSpoilageOperations.ProjectDecayMilliTicks(right, currentTick);
                int comparison = rightDecay.CompareTo(leftDecay);
                return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
            });

            int available = 0;
            for (int index = 0; index < ordered.Count; index++) available = checked(available + ordered[index].Quantity);
            if (available < quantity) throw new InvalidOperationException("Perishable batch state is smaller than the requested transfer.");

            var copy = new List<PerishableBatchState>(_batches);
            int remaining = quantity;
            for (int orderedIndex = 0; orderedIndex < ordered.Count && remaining > 0; orderedIndex++)
            {
                PerishableBatchState original = ordered[orderedIndex];
                int take = Math.Min(original.Quantity, remaining);
                int copyIndex = FindIndex(copy, original.Id);
                PerishableBatchState advanced = FoodSpoilageOperations.AdvanceToTick(original, currentTick);

                if (take == original.Quantity)
                {
                    copy[copyIndex] = advanced.With(
                        destinationId,
                        advanced.Quantity,
                        advanced.AccumulatedDecayMilliTicks,
                        advanced.LastEvaluatedTick);
                }
                else
                {
                    FoodBatchId splitId = nextBatchId();
                    if (!splitId.IsValid || ContainsBatch(copy, splitId))
                    {
                        throw new InvalidOperationException("The food batch generator returned an invalid or duplicate identifier.");
                    }

                    copy[copyIndex] = advanced.With(
                        sourceId,
                        advanced.Quantity - take,
                        advanced.AccumulatedDecayMilliTicks,
                        advanced.LastEvaluatedTick);
                    copy.Add(new PerishableBatchState(
                        splitId,
                        destinationId,
                        definition.Id,
                        take,
                        advanced.AccumulatedDecayMilliTicks,
                        advanced.LastEvaluatedTick));
                }

                remaining -= take;
            }

            return new PerishableInventoryState(copy);
        }

        private int QuantityFor(ContainerId containerId, ItemDefinitionId definitionId)
        {
            int total = 0;
            for (int index = 0; index < _batches.Count; index++)
            {
                PerishableBatchState batch = _batches[index];
                if (batch.ContainerId.Equals(containerId) && batch.DefinitionId.Equals(definitionId))
                {
                    total = checked(total + batch.Quantity);
                }
            }
            return total;
        }

        private static int FindIndex(IReadOnlyList<PerishableBatchState> batches, FoodBatchId id)
        {
            for (int index = 0; index < batches.Count; index++) if (batches[index].Id.Equals(id)) return index;
            return -1;
        }

        private static bool ContainsBatch(IReadOnlyList<PerishableBatchState> batches, FoodBatchId id) => FindIndex(batches, id) >= 0;
    }

    public static class PerishableInventoryOperations
    {
        public static AddItemResult Add(
            ContainerState destination,
            ItemDefinition definition,
            PerishableInventoryState perishables,
            FoodBatchId batchId,
            int quantity,
            long currentTick)
        {
            RequirePerishable(definition);
            if (perishables == null) throw new ArgumentNullException(nameof(perishables));
            if (!batchId.IsValid) throw new ArgumentException("A valid food batch identifier is required.", nameof(batchId));
            if (perishables.ContainsBatch(batchId)) throw new InvalidOperationException($"Food batch '{batchId}' already exists.");
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
            if (currentTick < 0L) throw new ArgumentOutOfRangeException(nameof(currentTick));

            AddItemResult result = InventoryOperations.AddPerishableStack(destination, definition, quantity);
            if (result.Accepted > 0)
            {
                perishables.AddBatch(new PerishableBatchState(
                    batchId,
                    destination.Id,
                    definition.Id,
                    result.Accepted,
                    0L,
                    currentTick));
            }
            return result;
        }

        public static TransferResult Transfer(
            PlayerInventoryState inventory,
            PerishableInventoryState perishables,
            ContainerState source,
            ContainerState destination,
            ItemDefinition definition,
            int quantity,
            long currentTick,
            Func<FoodBatchId> nextBatchId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (perishables == null) throw new ArgumentNullException(nameof(perishables));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            RequirePerishable(definition);
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (source.Id.Equals(destination.Id)) return new TransferResult(quantity, 0, InventoryOperationOutcome.SameContainer);

            perishables.ValidateAgainst(inventory);
            int sourceQuantity = InventoryOperations.Count(source, definition.Id);
            if (sourceQuantity < quantity) return new TransferResult(quantity, 0, InventoryOperationOutcome.SourceInsufficient);

            int accepted = quantity;
            if (definition.UnitEncumbrance.Units > 0)
            {
                accepted = (int)Math.Min(quantity, destination.RemainingCapacity.Units / definition.UnitEncumbrance.Units);
            }
            if (accepted <= 0) return new TransferResult(quantity, 0, InventoryOperationOutcome.DestinationFull);

            PerishableInventoryState candidate = perishables.CreateAfterTransfer(
                source.Id,
                destination.Id,
                definition,
                accepted,
                currentTick,
                nextBatchId);
            TransferResult moved = InventoryOperations.TransferPerishableStack(
                source,
                destination,
                definition,
                accepted);
            if (moved.Transferred != accepted)
            {
                throw new InvalidOperationException("The inventory aggregate changed during a synchronous perishable transfer.");
            }

            perishables.ReplaceWith(candidate);
            perishables.ValidateAgainst(inventory);
            return new TransferResult(
                quantity,
                accepted,
                accepted == quantity ? InventoryOperationOutcome.Complete : InventoryOperationOutcome.Partial);
        }

        private static void RequirePerishable(ItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.StateKind != ItemStateKind.Stackable || definition.Perishable == null)
            {
                throw new ArgumentException("The definition must be a perishable stackable item.", nameof(definition));
            }
        }
    }
}
