using System;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        public PlayerFoodState Food { get; private set; } = new PlayerFoodState();
        public PerishableInventoryState PerishableItems { get; private set; } = new PerishableInventoryState();
        public string LastFoodActionText { get; private set; } = string.Empty;
        public string FoodStatusText => string.IsNullOrEmpty(LastFoodActionText)
            ? $"Satiety: {Food.CurrentSatiety}/{Food.MaximumSatiety}"
            : $"Satiety: {Food.CurrentSatiety}/{Food.MaximumSatiety} — {LastFoodActionText}";

        private void InitializePrototypeFood()
        {
            PerishableInventoryOperations.Add(
                BagContainer,
                InventoryPrototypeCatalog.Apple,
                PerishableItems,
                new FoodBatchId("prototype-apple-batch-0001"),
                InventoryPrototypeCatalog.InitialAppleQuantity,
                0L);
        }

        private void InstallFoodState(PlayerFoodState food, PerishableInventoryState perishables)
        {
            Food = food ?? throw new ArgumentNullException(nameof(food));
            PerishableItems = perishables ?? throw new ArgumentNullException(nameof(perishables));
            PerishableItems.ValidateAgainst(Inventory);
        }

        public bool IsPerishable(InventorySelection selection)
        {
            ItemDefinition definition = Inventory.FindDefinition(selection.DefinitionId);
            return selection.IsValid && definition != null && definition.Perishable != null;
        }

        public bool CanTransferPerishable(InventorySelection selection, ContainerId destinationId)
        {
            if (!IsPerishable(selection) || !destinationId.IsValid || selection.SourceContainerId.Equals(destinationId)) return false;
            ContainerState source = Inventory.FindContainer(selection.SourceContainerId);
            ContainerState destination = Inventory.FindContainer(destinationId);
            ItemDefinition definition = Inventory.FindDefinition(selection.DefinitionId);
            if (source == null || destination == null || definition == null) return false;
            int quantity = InventoryOperations.Count(source, definition.Id);
            return quantity > 0
                && (definition.UnitEncumbrance.Units == 0 || destination.RemainingCapacity.Units >= definition.UnitEncumbrance.Units);
        }

        public TransferResult TransferPerishable(InventorySelection selection, ContainerId destinationId)
        {
            if (!CanTransferPerishable(selection, destinationId)) return default;
            ContainerState source = Inventory.FindContainer(selection.SourceContainerId);
            ContainerState destination = Inventory.FindContainer(destinationId);
            ItemDefinition definition = Inventory.FindDefinition(selection.DefinitionId);
            int quantity = InventoryOperations.Count(source, definition.Id);
            return PerishableInventoryOperations.Transfer(
                Inventory,
                PerishableItems,
                source,
                destination,
                definition,
                quantity,
                CurrentTick,
                NextFoodBatchId);
        }

        public bool CanEat(InventorySelection selection)
        {
            return selection.IsValid && FoodConsumptionOperations.CanConsume(
                Food,
                Inventory,
                PerishableItems,
                selection.SourceContainerId,
                selection.DefinitionId);
        }

        public FoodConsumptionResult Eat(InventorySelection selection)
        {
            FoodConsumptionResult result = FoodConsumptionOperations.Consume(
                Food,
                Inventory,
                PerishableItems,
                selection.SourceContainerId,
                selection.DefinitionId,
                CurrentTick);
            if (result.Succeeded)
            {
                string condition = result.Freshness == FoodFreshness.Fresh
                    ? "fresh"
                    : result.Freshness == FoodFreshness.Stale ? "stale" : "rotten";
                LastFoodActionText = result.UnsafeFood
                    ? $"Ate {condition} food (+{result.SatietyApplied}); unsafe"
                    : $"Ate {condition} food (+{result.SatietyApplied})";
            }
            return result;
        }

        public string DescribeFreshness(ContainerId containerId, ItemDefinition definition)
        {
            FoodFreshnessSummary summary = PerishableItems.GetSummary(containerId, definition, CurrentTick);
            if (summary.Total == 0) return string.Empty;
            if (summary.Stale == 0 && summary.Rotten == 0) return $"fresh ×{summary.Fresh}";
            if (summary.Fresh == 0 && summary.Rotten == 0) return $"stale ×{summary.Stale}";
            if (summary.Fresh == 0 && summary.Stale == 0) return $"rotten ×{summary.Rotten}";
            return $"fresh {summary.Fresh} / stale {summary.Stale} / rotten {summary.Rotten}";
        }

        private FoodBatchId NextFoodBatchId()
        {
            for (int sequence = 1; ; sequence++)
            {
                var candidate = new FoodBatchId($"prototype-food-batch-{sequence:000000}");
                if (!PerishableItems.ContainsBatch(candidate)) return candidate;
            }
        }
    }
}
