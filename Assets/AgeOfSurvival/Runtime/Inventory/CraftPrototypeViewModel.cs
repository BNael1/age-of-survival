using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Crafting;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed class CraftRecipeRowViewModel
    {
        public CraftRecipeRowViewModel(CraftRecipeId id, string displayName, string ingredientText,
            string outputText, int durationTicks, CraftActionReason availability)
        {
            Id = id; DisplayName = displayName ?? string.Empty; IngredientText = ingredientText ?? string.Empty;
            OutputText = outputText ?? string.Empty; DurationTicks = durationTicks; Availability = availability;
        }
        public CraftRecipeId Id { get; }
        public string DisplayName { get; }
        public string IngredientText { get; }
        public string OutputText { get; }
        public int DurationTicks { get; }
        public CraftActionReason Availability { get; }
        public bool CanCraft => Availability == CraftActionReason.None;
        public string DisplayText => $"{DisplayName} — {(CanCraft ? "ready" : Availability.ToString())}";
        public string DetailsText => $"{IngredientText}  →  {OutputText}  •  {DurationTicks} ticks";
    }

    public sealed class CraftPrototypeViewModel
    {
        public CraftPrototypeViewModel(IReadOnlyList<CraftRecipeRowViewModel> recipes, string statusText, double progress)
        { Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes)); StatusText = statusText ?? string.Empty; Progress = progress; }
        public IReadOnlyList<CraftRecipeRowViewModel> Recipes { get; }
        public string StatusText { get; }
        public double Progress { get; }
    }

    public static class CraftPrototypeViewModelBuilder
    {
        public static CraftPrototypeViewModel Build(InventoryPrototypeSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var rows = new List<CraftRecipeRowViewModel>();
            for (int i = 0; i < session.CraftRecipes.Recipes.Count; i++)
            {
                CraftRecipeDefinition recipe = session.CraftRecipes.Recipes[i];
                rows.Add(new CraftRecipeRowViewModel(recipe.Id, recipe.DisplayName,
                    FormatIngredients(session.Inventory, recipe), FormatOutputs(session.Inventory, recipe),
                    recipe.DurationTicks, session.CraftAvailability(recipe.Id)));
            }
            return new CraftPrototypeViewModel(rows.AsReadOnly(), Status(session.CraftAction),
                session.CraftAction?.ProgressAt(session.CurrentTick) ?? 0.0);
        }

        private static string FormatIngredients(PlayerInventoryState inventory, CraftRecipeDefinition recipe)
        {
            var parts = new string[recipe.Ingredients.Count];
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftIngredientDefinition ingredient = recipe.Ingredients[i];
                ItemDefinition definition = inventory.FindDefinition(ingredient.DefinitionId);
                parts[i] = $"{definition?.DisplayName ?? ingredient.DefinitionId.ToString()} ×{ingredient.Quantity}";
            }
            return string.Join(" + ", parts);
        }

        private static string FormatOutputs(PlayerInventoryState inventory, CraftRecipeDefinition recipe)
        {
            var parts = new string[recipe.Outputs.Count];
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                CraftOutputDefinition output = recipe.Outputs[i];
                ItemDefinition definition = inventory.FindDefinition(output.DefinitionId);
                parts[i] = $"{definition?.DisplayName ?? output.DefinitionId.ToString()} ×{output.Quantity}";
            }
            return string.Join(" + ", parts);
        }

        private static string Status(CraftActionState action)
        {
            if (action == null) return "Craft: idle";
            switch (action.Status)
            {
                case CraftActionStatus.Active: return $"Craft: {action.RecipeId} in progress";
                case CraftActionStatus.Completed: return $"Craft completed: {action.RecipeId}";
                case CraftActionStatus.Interrupted: return $"Craft interrupted: {action.Reason}";
                default: return $"Craft failed: {action.Reason}";
            }
        }
    }
}
