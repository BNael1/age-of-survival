using System;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Food
{
    public enum FoodConsumptionOutcome
    {
        None = 0,
        Consumed = 1,
        InvalidSelection = 2,
        NotConsumable = 3,
        NotHungry = 4
    }

    public readonly struct FoodConsumptionResult
    {
        public FoodConsumptionResult(
            FoodConsumptionOutcome outcome,
            FoodFreshness freshness,
            int satietyApplied,
            bool unsafeFood)
        {
            Outcome = outcome;
            Freshness = freshness;
            SatietyApplied = satietyApplied;
            UnsafeFood = unsafeFood;
        }

        public FoodConsumptionOutcome Outcome { get; }
        public FoodFreshness Freshness { get; }
        public int SatietyApplied { get; }
        public bool UnsafeFood { get; }
        public bool Succeeded => Outcome == FoodConsumptionOutcome.Consumed;
    }

    public static class FoodConsumptionOperations
    {
        public static bool CanConsume(
            PlayerFoodState food,
            PlayerInventoryState inventory,
            PerishableInventoryState perishables,
            ContainerId sourceId,
            ItemDefinitionId definitionId)
        {
            if (food == null || inventory == null || perishables == null || food.IsFull) return false;
            ContainerState source = inventory.FindContainer(sourceId);
            ItemDefinition definition = inventory.FindDefinition(definitionId);
            return source != null
                && definition != null
                && definition.StateKind == ItemStateKind.Stackable
                && definition.Consumable != null
                && InventoryOperations.Count(source, definitionId) > 0;
        }

        public static FoodConsumptionResult Consume(
            PlayerFoodState food,
            PlayerInventoryState inventory,
            PerishableInventoryState perishables,
            ContainerId sourceId,
            ItemDefinitionId definitionId,
            long currentTick)
        {
            if (food == null) throw new ArgumentNullException(nameof(food));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (perishables == null) throw new ArgumentNullException(nameof(perishables));

            PlayerFoodOperations.AdvanceToTick(food, currentTick);
            ContainerState source = inventory.FindContainer(sourceId);
            ItemDefinition definition = inventory.FindDefinition(definitionId);
            if (source == null || definition == null)
            {
                return new FoodConsumptionResult(FoodConsumptionOutcome.InvalidSelection, default, 0, false);
            }
            if (definition.StateKind != ItemStateKind.Stackable
                || definition.Consumable == null)
            {
                return new FoodConsumptionResult(FoodConsumptionOutcome.NotConsumable, default, 0, false);
            }
            if (food.IsFull)
            {
                return new FoodConsumptionResult(FoodConsumptionOutcome.NotHungry, default, 0, false);
            }

            if (definition.Perishable == null)
            {
                RemoveItemResult stackRemoved = InventoryOperations.RemoveStack(
                    source,
                    definition,
                    1);
                if (!stackRemoved.Succeeded)
                {
                    return new FoodConsumptionResult(
                        FoodConsumptionOutcome.InvalidSelection,
                        default,
                        0,
                        false);
                }

                int freshSatiety = definition.Consumable.SatietyFor(
                    FoodFreshness.Fresh);
                PlayerFoodChangeResult freshFoodChange =
                    PlayerFoodOperations.AddSatiety(
                        food,
                        freshSatiety,
                        currentTick);
                return new FoodConsumptionResult(
                    FoodConsumptionOutcome.Consumed,
                    FoodFreshness.Fresh,
                    freshFoodChange.AppliedDelta,
                    false);
            }

            perishables.ValidateAgainst(inventory);
            PerishableBatchState oldest = perishables.FindOldest(sourceId, definition, currentTick);
            if (oldest == null || InventoryOperations.Count(source, definitionId) <= 0)
            {
                return new FoodConsumptionResult(FoodConsumptionOutcome.InvalidSelection, default, 0, false);
            }

            PerishableInventoryState candidate = perishables.CreateAfterConsume(oldest.Id, currentTick, out PerishableBatchState consumed);
            FoodFreshness freshness = FoodSpoilageOperations.FreshnessAt(
                consumed,
                definition.Perishable,
                currentTick);
            int satiety = definition.Consumable.SatietyFor(freshness);

            RemoveItemResult removed = InventoryOperations.RemovePerishableStack(source, definition, 1);
            if (!removed.Succeeded)
            {
                throw new InvalidOperationException("The inventory aggregate changed during synchronous food consumption.");
            }

            perishables.ReplaceWith(candidate);
            perishables.ValidateAgainst(inventory);
            PlayerFoodChangeResult foodChange = satiety > 0
                ? PlayerFoodOperations.AddSatiety(food, satiety, currentTick)
                : PlayerFoodOperations.AdvanceToTick(food, currentTick);
            return new FoodConsumptionResult(
                FoodConsumptionOutcome.Consumed,
                freshness,
                foodChange.AppliedDelta,
                freshness == FoodFreshness.Rotten);
        }
    }
}
