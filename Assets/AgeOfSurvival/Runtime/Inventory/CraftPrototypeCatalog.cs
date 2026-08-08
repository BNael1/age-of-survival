using AgeOfSurvival.Core.Crafting;

namespace AgeOfSurvival.Runtime.Inventory
{
    public static class CraftPrototypeCatalog
    {
        public static readonly CraftRecipeId KindlingRecipeId = new CraftRecipeId("hand-kindling");
        public static readonly CraftRecipeId StoneFlakesRecipeId = new CraftRecipeId("hand-stone-flakes");
        public static readonly CraftRecipeId WoodenStakesRecipeId = new CraftRecipeId("hand-wooden-stakes");

        public static readonly CraftRecipeDefinition Kindling = new CraftRecipeDefinition(
            KindlingRecipeId, "Kindling", CraftMethodIds.Hand, 90,
            new[] { new CraftIngredientDefinition(InventoryPrototypeCatalog.Branches.Id, 2) },
            new[] { new CraftOutputDefinition(InventoryPrototypeCatalog.Kindling.Id, 1) });

        public static readonly CraftRecipeDefinition StoneFlakes = new CraftRecipeDefinition(
            StoneFlakesRecipeId, "Stone flakes", CraftMethodIds.Hand, 120,
            new[] { new CraftIngredientDefinition(InventoryPrototypeCatalog.Stones.Id, 1) },
            new[] { new CraftOutputDefinition(InventoryPrototypeCatalog.StoneFlakes.Id, 2) });

        public static readonly CraftRecipeDefinition WoodenStakes = new CraftRecipeDefinition(
            WoodenStakesRecipeId, "Wooden stakes", CraftMethodIds.Hand, 150,
            new[] { new CraftIngredientDefinition(InventoryPrototypeCatalog.Wood.Id, 1) },
            new[] { new CraftOutputDefinition(InventoryPrototypeCatalog.WoodenStakes.Id, 2) });

        public static readonly CraftRecipeCatalog Recipes = new CraftRecipeCatalog(new[]
        {
            Kindling,
            StoneFlakes,
            WoodenStakes
        });
    }
}
