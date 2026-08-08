using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Crafting
{
    public enum CraftActionStatus { Invalid = 0, Active = 1, Completed = 2, Interrupted = 3, Failed = 4 }
    public enum CraftActionReason { None = 0, InvalidRequest = 1, MissingIngredients = 2, OutputCapacityInsufficient = 3, PlayerMoved = 4, AlreadyFinalized = 5, AnotherActionActive = 6 }

    public readonly struct CraftItemAllocation
    {
        public CraftItemAllocation(ContainerId containerId, ItemDefinitionId definitionId, int quantity)
        {
            if (!containerId.IsValid) throw new ArgumentException("A valid container is required.", nameof(containerId));
            if (!definitionId.IsValid) throw new ArgumentException("A valid item definition is required.", nameof(definitionId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            ContainerId = containerId; DefinitionId = definitionId; Quantity = quantity;
        }
        public ContainerId ContainerId { get; }
        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
    }

    public sealed class CraftInventoryPlan
    {
        internal CraftInventoryPlan(CraftRecipeDefinition recipe, IList<CraftItemAllocation> removals, IList<CraftItemAllocation> outputs)
        {
            Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            Removals = Array.AsReadOnly(ToArray(removals));
            Outputs = Array.AsReadOnly(ToArray(outputs));
        }
        public CraftRecipeDefinition Recipe { get; }
        public IReadOnlyList<CraftItemAllocation> Removals { get; }
        public IReadOnlyList<CraftItemAllocation> Outputs { get; }
        private static CraftItemAllocation[] ToArray(IList<CraftItemAllocation> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var result = new CraftItemAllocation[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }
    }

    public sealed class CraftActionState
    {
        internal CraftActionState(CraftRecipeDefinition recipe, long startTick)
        {
            RecipeId = recipe.Id; StartTick = startTick; DurationTicks = recipe.DurationTicks; Status = CraftActionStatus.Active;
        }
        public CraftRecipeId RecipeId { get; }
        public long StartTick { get; }
        public int DurationTicks { get; }
        public CraftActionStatus Status { get; internal set; }
        public CraftActionReason Reason { get; internal set; }
        public long? FinalizedTick { get; internal set; }
        public double ProgressAt(long tick)
        {
            if (Status == CraftActionStatus.Completed) return 1.0;
            long effective = FinalizedTick.HasValue && tick > FinalizedTick.Value ? FinalizedTick.Value : tick;
            if (effective <= StartTick) return 0.0;
            return Math.Min(1.0, (double)(effective - StartTick) / DurationTicks);
        }
    }

    public readonly struct CraftActionResult
    {
        public CraftActionResult(CraftActionState action, CraftActionReason reason) { Action = action; Reason = reason; }
        public CraftActionState Action { get; }
        public CraftActionReason Reason { get; }
        public bool Succeeded => Action != null && Reason == CraftActionReason.None;
    }

    public static class CraftInventoryOperations
    {
        public static bool TryCreatePlan(PlayerInventoryState inventory, IReadOnlyList<ContainerId> ingredientOrder,
            IReadOnlyList<ContainerId> outputOrder, CraftRecipeDefinition recipe, out CraftInventoryPlan plan, out CraftActionReason reason)
        {
            plan = null; reason = CraftActionReason.InvalidRequest;
            if (inventory == null || recipe == null || ingredientOrder == null || outputOrder == null || ingredientOrder.Count == 0 || outputOrder.Count == 0) return false;
            if (!TryResolveContainers(inventory, ingredientOrder, out ContainerState[] ingredientContainers) || !TryResolveContainers(inventory, outputOrder, out ContainerState[] outputContainers)) return false;
            if (!ValidateRecipeDefinitions(inventory, recipe)) return false;

            var removals = new List<CraftItemAllocation>();
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftIngredientDefinition ingredient = recipe.Ingredients[i];
                int remaining = ingredient.Quantity;
                for (int c = 0; c < ingredientContainers.Length && remaining > 0; c++)
                {
                    int available = InventoryOperations.Count(ingredientContainers[c], ingredient.DefinitionId);
                    int taken = Math.Min(remaining, available);
                    if (taken <= 0) continue;
                    removals.Add(new CraftItemAllocation(ingredientContainers[c].Id, ingredient.DefinitionId, taken));
                    remaining -= taken;
                }
                if (remaining > 0) { reason = CraftActionReason.MissingIngredients; return false; }
            }

            var used = new Dictionary<ContainerId, long>();
            for (int i = 0; i < outputContainers.Length; i++) used.Add(outputContainers[i].Id, outputContainers[i].UsedCapacity.Units);
            for (int i = 0; i < removals.Count; i++)
            {
                CraftItemAllocation allocation = removals[i];
                if (!used.ContainsKey(allocation.ContainerId)) continue;
                ItemDefinition definition = inventory.FindDefinition(allocation.DefinitionId);
                used[allocation.ContainerId] = checked(used[allocation.ContainerId] - definition.UnitEncumbrance.Multiply(allocation.Quantity).Units);
            }

            var outputs = new List<CraftItemAllocation>();
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                CraftOutputDefinition output = recipe.Outputs[i];
                ItemDefinition definition = inventory.FindDefinition(output.DefinitionId);
                int remaining = output.Quantity;
                for (int c = 0; c < outputContainers.Length && remaining > 0; c++)
                {
                    ContainerState container = outputContainers[c];
                    long free = container.Definition.Capacity.Units - used[container.Id];
                    int accepted = definition.UnitEncumbrance.Units == 0
                        ? remaining
                        : (int)Math.Min(remaining, free / definition.UnitEncumbrance.Units);
                    if (accepted <= 0) continue;
                    outputs.Add(new CraftItemAllocation(container.Id, output.DefinitionId, accepted));
                    used[container.Id] = checked(used[container.Id] + definition.UnitEncumbrance.Multiply(accepted).Units);
                    remaining -= accepted;
                }
                if (remaining > 0) { reason = CraftActionReason.OutputCapacityInsufficient; return false; }
            }

            plan = new CraftInventoryPlan(recipe, removals, outputs); reason = CraftActionReason.None; return true;
        }

        public static void Apply(PlayerInventoryState inventory, CraftInventoryPlan plan)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            ValidatePlanBeforeMutation(inventory, plan);
            for (int i = 0; i < plan.Removals.Count; i++)
            {
                CraftItemAllocation a = plan.Removals[i];
                RemoveItemResult removed = InventoryOperations.RemoveStack(inventory.FindContainer(a.ContainerId), inventory.FindDefinition(a.DefinitionId), a.Quantity);
                if (!removed.Succeeded) throw new InvalidOperationException("A prevalidated craft ingredient removal failed.");
            }
            for (int i = 0; i < plan.Outputs.Count; i++)
            {
                CraftItemAllocation a = plan.Outputs[i];
                AddItemResult added = InventoryOperations.AddStack(inventory.FindContainer(a.ContainerId), inventory.FindDefinition(a.DefinitionId), a.Quantity);
                if (added.Accepted != a.Quantity) throw new InvalidOperationException("A prevalidated craft output insertion failed.");
            }
        }

        private static void ValidatePlanBeforeMutation(PlayerInventoryState inventory, CraftInventoryPlan plan)
        {
            var finalUsed = new Dictionary<ContainerId, long>();
            for (int i = 0; i < inventory.Containers.Count; i++) finalUsed.Add(inventory.Containers[i].Id, inventory.Containers[i].UsedCapacity.Units);
            for (int i = 0; i < plan.Removals.Count; i++)
            {
                CraftItemAllocation a = plan.Removals[i];
                ContainerState c = inventory.FindContainer(a.ContainerId) ?? throw new InvalidOperationException("A planned ingredient container disappeared.");
                ItemDefinition d = inventory.FindDefinition(a.DefinitionId) ?? throw new InvalidOperationException("A planned ingredient definition disappeared.");
                RequireOrdinaryStack(d);
                if (InventoryOperations.Count(c, d.Id) < a.Quantity) throw new InvalidOperationException("A planned ingredient is no longer available.");
                finalUsed[c.Id] = checked(finalUsed[c.Id] - d.UnitEncumbrance.Multiply(a.Quantity).Units);
            }
            for (int i = 0; i < plan.Outputs.Count; i++)
            {
                CraftItemAllocation a = plan.Outputs[i];
                ContainerState c = inventory.FindContainer(a.ContainerId) ?? throw new InvalidOperationException("A planned output container disappeared.");
                ItemDefinition d = inventory.FindDefinition(a.DefinitionId) ?? throw new InvalidOperationException("A planned output definition disappeared.");
                RequireOrdinaryStack(d);
                finalUsed[c.Id] = checked(finalUsed[c.Id] + d.UnitEncumbrance.Multiply(a.Quantity).Units);
            }
            foreach (KeyValuePair<ContainerId, long> pair in finalUsed)
            {
                ContainerState c = inventory.FindContainer(pair.Key);
                if (pair.Value < 0 || pair.Value > c.Definition.Capacity.Units) throw new InvalidOperationException("A planned craft would exceed container capacity.");
            }
        }

        private static bool TryResolveContainers(PlayerInventoryState inventory, IReadOnlyList<ContainerId> order, out ContainerState[] containers)
        {
            containers = new ContainerState[order.Count];
            var ids = new HashSet<ContainerId>();
            for (int i = 0; i < order.Count; i++)
            {
                if (!order[i].IsValid || !ids.Add(order[i])) { containers = null; return false; }
                containers[i] = inventory.FindContainer(order[i]);
                if (containers[i] == null) { containers = null; return false; }
            }
            return true;
        }

        private static bool ValidateRecipeDefinitions(PlayerInventoryState inventory, CraftRecipeDefinition recipe)
        {
            for (int i = 0; i < recipe.Ingredients.Count; i++) if (!IsOrdinaryStack(inventory.FindDefinition(recipe.Ingredients[i].DefinitionId))) return false;
            for (int i = 0; i < recipe.Outputs.Count; i++) if (!IsOrdinaryStack(inventory.FindDefinition(recipe.Outputs[i].DefinitionId))) return false;
            return true;
        }
        private static bool IsOrdinaryStack(ItemDefinition d) => d != null && d.StateKind == ItemStateKind.Stackable && d.Perishable == null;
        private static void RequireOrdinaryStack(ItemDefinition d) { if (!IsOrdinaryStack(d)) throw new InvalidOperationException("The first craft slice supports ordinary stackable items only."); }
    }

    public static class CraftActionOperations
    {
        public static CraftActionResult Start(CraftRecipeDefinition recipe, PlayerInventoryState inventory,
            IReadOnlyList<ContainerId> ingredientOrder, IReadOnlyList<ContainerId> outputOrder, long startTick)
        {
            if (recipe == null || inventory == null || startTick < 0) return new CraftActionResult(null, CraftActionReason.InvalidRequest);
            if (!CraftInventoryOperations.TryCreatePlan(inventory, ingredientOrder, outputOrder, recipe, out _, out CraftActionReason reason)) return new CraftActionResult(null, reason);
            return new CraftActionResult(new CraftActionState(recipe, startTick), CraftActionReason.None);
        }

        public static CraftActionResult Advance(CraftActionState action, CraftRecipeDefinition recipe, PlayerInventoryState inventory,
            IReadOnlyList<ContainerId> ingredientOrder, IReadOnlyList<ContainerId> outputOrder, long currentTick, bool playerMoved)
        {
            if (action == null) return new CraftActionResult(null, CraftActionReason.InvalidRequest);
            if (action.Status != CraftActionStatus.Active) return new CraftActionResult(action, CraftActionReason.AlreadyFinalized);
            if (recipe == null || inventory == null || !action.RecipeId.Equals(recipe.Id) || currentTick < action.StartTick) return Fail(action, currentTick, CraftActionReason.InvalidRequest);
            if (playerMoved) return Interrupt(action, currentTick, CraftActionReason.PlayerMoved);
            if (currentTick - action.StartTick < action.DurationTicks) return new CraftActionResult(action, CraftActionReason.None);
            if (!CraftInventoryOperations.TryCreatePlan(inventory, ingredientOrder, outputOrder, recipe, out CraftInventoryPlan plan, out CraftActionReason reason)) return Fail(action, currentTick, reason);
            CraftInventoryOperations.Apply(inventory, plan);
            action.Status = CraftActionStatus.Completed; action.Reason = CraftActionReason.None; action.FinalizedTick = currentTick;
            return new CraftActionResult(action, CraftActionReason.None);
        }

        private static CraftActionResult Interrupt(CraftActionState action, long tick, CraftActionReason reason)
        { action.Status = CraftActionStatus.Interrupted; action.Reason = reason; action.FinalizedTick = tick; return new CraftActionResult(action, reason); }
        private static CraftActionResult Fail(CraftActionState action, long tick, CraftActionReason reason)
        { action.Status = CraftActionStatus.Failed; action.Reason = reason; action.FinalizedTick = tick; return new CraftActionResult(action, reason); }
    }
}
