using AgeOfSurvival.Core.Crafting;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        public CraftRecipeCatalog CraftRecipes => CraftPrototypeCatalog.Recipes;
        public CraftActionState CraftAction { get; private set; }
        public bool IsCraftActionActive => CraftAction != null && CraftAction.Status == CraftActionStatus.Active;

        public CraftActionReason CraftAvailability(CraftRecipeId recipeId)
        {
            if (IsCraftActionActive || (TransferAction != null && TransferAction.Status == TransferActionStatus.Active))
                return CraftActionReason.AnotherActionActive;
            if (!CraftRecipes.TryFind(recipeId, out CraftRecipeDefinition recipe)) return CraftActionReason.InvalidRequest;
            return CraftInventoryOperations.TryCreatePlan(
                Inventory, CraftContainerOrder(), CraftContainerOrder(), recipe,
                out _, out CraftActionReason reason)
                ? CraftActionReason.None
                : reason;
        }

        public bool CanStartCraft(CraftRecipeId recipeId) => CraftAvailability(recipeId) == CraftActionReason.None;

        public CraftActionResult StartCraft(CraftRecipeId recipeId, long currentTick)
        {
            CraftActionReason availability = CraftAvailability(recipeId);
            if (availability != CraftActionReason.None)
                return new CraftActionResult(IsCraftActionActive ? CraftAction : null, availability);

            CraftActionResult result = CraftActionOperations.Start(
                CraftRecipes.Require(recipeId), Inventory,
                CraftContainerOrder(), CraftContainerOrder(), currentTick);
            if (result.Succeeded) CraftAction = result.Action;
            return result;
        }

        public CraftActionResult AdvanceCraft(long currentTick, bool playerMoved)
        {
            if (CraftAction == null) return new CraftActionResult(null, CraftActionReason.InvalidRequest);
            if (!CraftRecipes.TryFind(CraftAction.RecipeId, out CraftRecipeDefinition recipe))
                return new CraftActionResult(CraftAction, CraftActionReason.InvalidRequest);
            return CraftActionOperations.Advance(
                CraftAction, recipe, Inventory,
                CraftContainerOrder(), CraftContainerOrder(), currentTick, playerMoved);
        }

        public bool CancelActiveCraftForSaveAndQuit()
        {
            if (!IsCraftActionActive) return false;
            CraftActionResult result = AdvanceCraft(CurrentTick, true);
            return result.Action != null && result.Action.Status == CraftActionStatus.Interrupted;
        }

        private ContainerId[] CraftContainerOrder() => new[] { MainContainer.Id, BagContainer.Id };
    }
}
