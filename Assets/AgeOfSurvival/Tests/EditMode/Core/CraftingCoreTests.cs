using System;
using AgeOfSurvival.Core.Crafting;
using AgeOfSurvival.Core.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class CraftingCoreTests
    {
        private static readonly ItemDefinition Branches = Stack("test-branches", 500);
        private static readonly ItemDefinition Kindling = Stack("test-kindling", 1000);
        private static readonly ItemDefinition Stone = Stack("test-stone", 1500);
        private static readonly ItemDefinition Flake = Stack("test-flake", 750);

        [Test]
        public void RecipeRejectsDuplicateIngredientDefinitions()
        {
            Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(
                new CraftRecipeId("duplicate"), "Duplicate", CraftMethodIds.Hand, 10,
                new[] { new CraftIngredientDefinition(Branches.Id, 1), new CraftIngredientDefinition(Branches.Id, 1) },
                new[] { new CraftOutputDefinition(Kindling.Id, 1) }));
        }

        [Test]
        public void StartPlansWithoutConsumingAnything()
        {
            PlayerInventoryState inventory = Inventory(5000, 5000, mainBranches: 2);
            CraftRecipeDefinition recipe = Recipe("kindling", 90, Branches.Id, 2, Kindling.Id, 1);
            CraftActionResult result = CraftActionOperations.Start(recipe, inventory, Order(inventory), Order(inventory), 10);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Branches.Id), Is.EqualTo(2));
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Kindling.Id), Is.Zero);
        }

        [Test]
        public void CraftCompletesExactlyAtConfiguredTick()
        {
            PlayerInventoryState inventory = Inventory(5000, 5000, mainBranches: 2);
            CraftRecipeDefinition recipe = Recipe("kindling", 90, Branches.Id, 2, Kindling.Id, 1);
            CraftActionState action = CraftActionOperations.Start(recipe, inventory, Order(inventory), Order(inventory), 10).Action;
            CraftActionOperations.Advance(action, recipe, inventory, Order(inventory), Order(inventory), 99, false);
            Assert.That(action.Status, Is.EqualTo(CraftActionStatus.Active));
            CraftActionOperations.Advance(action, recipe, inventory, Order(inventory), Order(inventory), 100, false);
            Assert.That(action.Status, Is.EqualTo(CraftActionStatus.Completed));
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Branches.Id), Is.Zero);
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Kindling.Id), Is.EqualTo(1));
        }

        [Test]
        public void MovementInterruptsWithoutCraftMutation()
        {
            PlayerInventoryState inventory = Inventory(5000, 5000, mainBranches: 2);
            CraftRecipeDefinition recipe = Recipe("kindling", 90, Branches.Id, 2, Kindling.Id, 1);
            CraftActionState action = CraftActionOperations.Start(recipe, inventory, Order(inventory), Order(inventory), 1).Action;
            CraftActionOperations.Advance(action, recipe, inventory, Order(inventory), Order(inventory), 2, true);
            Assert.That(action.Status, Is.EqualTo(CraftActionStatus.Interrupted));
            Assert.That(action.Reason, Is.EqualTo(CraftActionReason.PlayerMoved));
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Branches.Id), Is.EqualTo(2));
        }

        [Test]
        public void FinalIngredientRevalidationFailsWithoutExtraMutation()
        {
            PlayerInventoryState inventory = Inventory(5000, 5000, mainBranches: 2);
            CraftRecipeDefinition recipe = Recipe("kindling", 90, Branches.Id, 2, Kindling.Id, 1);
            CraftActionState action = CraftActionOperations.Start(recipe, inventory, Order(inventory), Order(inventory), 0).Action;
            InventoryOperations.RemoveStack(inventory.MainContainer, Branches, 1);
            CraftActionOperations.Advance(action, recipe, inventory, Order(inventory), Order(inventory), 90, false);
            Assert.That(action.Status, Is.EqualTo(CraftActionStatus.Failed));
            Assert.That(action.Reason, Is.EqualTo(CraftActionReason.MissingIngredients));
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Branches.Id), Is.EqualTo(1));
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Kindling.Id), Is.Zero);
        }

        [Test]
        public void OutputCapacityIsEvaluatedAfterIngredientRemoval()
        {
            PlayerInventoryState inventory = Inventory(1000, 1, mainBranches: 2);
            CraftRecipeDefinition recipe = Recipe("kindling", 10, Branches.Id, 2, Kindling.Id, 1);
            CraftActionState action = CraftActionOperations.Start(recipe, inventory, Order(inventory), Order(inventory), 0).Action;
            Assert.That(action, Is.Not.Null);
            CraftActionOperations.Advance(action, recipe, inventory, Order(inventory), Order(inventory), 10, false);
            Assert.That(action.Status, Is.EqualTo(CraftActionStatus.Completed));
            Assert.That(inventory.MainContainer.UsedCapacity.Units, Is.EqualTo(1000));
        }

        [Test]
        public void IngredientAllocationUsesMainThenBagDeterministically()
        {
            PlayerInventoryState inventory = Inventory(
                5000,
                5000,
                mainBranches: 1,
                bagBranches: 1);
            CraftRecipeDefinition recipe = Recipe(
                "split-kindling",
                10,
                Branches.Id,
                2,
                Kindling.Id,
                1);

            bool planned = CraftInventoryOperations.TryCreatePlan(
                inventory,
                Order(inventory),
                Order(inventory),
                recipe,
                out CraftInventoryPlan plan,
                out CraftActionReason reason);

            Assert.That(planned, Is.True, reason.ToString());
            Assert.That(plan.Removals, Has.Count.EqualTo(2));

            Assert.That(
                plan.Removals[0].ContainerId,
                Is.EqualTo(inventory.MainContainer.Id));
            Assert.That(plan.Removals[0].Quantity, Is.EqualTo(1));

            Assert.That(
                plan.Removals[1].ContainerId,
                Is.EqualTo(inventory.Containers[1].Id));
            Assert.That(plan.Removals[1].Quantity, Is.EqualTo(1));

            CraftActionState action = CraftActionOperations.Start(
                recipe,
                inventory,
                Order(inventory),
                Order(inventory),
                0).Action;

            Assert.That(action, Is.Not.Null);

            CraftActionOperations.Advance(
                action,
                recipe,
                inventory,
                Order(inventory),
                Order(inventory),
                10,
                false);

            Assert.That(
                action.Status,
                Is.EqualTo(CraftActionStatus.Completed));
            Assert.That(
                InventoryOperations.Count(
                    inventory.MainContainer,
                    Branches.Id),
                Is.Zero);
            Assert.That(
                InventoryOperations.Count(
                    inventory.Containers[1],
                    Branches.Id),
                Is.Zero);
            Assert.That(
                InventoryOperations.Count(
                    inventory.MainContainer,
                    Kindling.Id),
                Is.EqualTo(1));
        }

        [Test]
        public void OutputAllocationUsesMainThenBagDeterministically()
        {
            PlayerInventoryState inventory = Inventory(2000, 3000, mainStone: 1);
            CraftRecipeDefinition recipe = Recipe("flakes", 10, Stone.Id, 1, Flake.Id, 4);
            bool planned = CraftInventoryOperations.TryCreatePlan(inventory, Order(inventory), Order(inventory), recipe,
                out CraftInventoryPlan plan, out CraftActionReason reason);
            Assert.That(planned, Is.True, reason.ToString());
            Assert.That(plan.Outputs, Has.Count.EqualTo(2));
            Assert.That(plan.Outputs[0].ContainerId, Is.EqualTo(inventory.MainContainer.Id));
            Assert.That(plan.Outputs[0].Quantity, Is.EqualTo(2));
            Assert.That(plan.Outputs[1].ContainerId, Is.EqualTo(inventory.Containers[1].Id));
            Assert.That(plan.Outputs[1].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void InsufficientOutputCapacityRejectsBeforeAnyMutation()
        {
            PlayerInventoryState inventory = Inventory(1000, 500, mainBranches: 1);
            CraftRecipeDefinition recipe = Recipe("too-large", 10, Branches.Id, 1, Kindling.Id, 2);
            CraftActionResult result = CraftActionOperations.Start(recipe, inventory, Order(inventory), Order(inventory), 0);
            Assert.That(result.Action, Is.Null);
            Assert.That(result.Reason, Is.EqualTo(CraftActionReason.OutputCapacityInsufficient));
            Assert.That(InventoryOperations.Count(inventory.MainContainer, Branches.Id), Is.EqualTo(1));
        }

        private static ItemDefinition Stack(string id, long units) => new ItemDefinition(
            new ItemDefinitionId(id), id, ItemStateKind.Stackable, new EncumbranceValue(units));

        private static CraftRecipeDefinition Recipe(string id, int ticks, ItemDefinitionId input, int inputQuantity,
            ItemDefinitionId output, int outputQuantity) => new CraftRecipeDefinition(
                new CraftRecipeId(id), id, CraftMethodIds.Hand, ticks,
                new[] { new CraftIngredientDefinition(input, inputQuantity) },
                new[] { new CraftOutputDefinition(output, outputQuantity) });

        private static PlayerInventoryState Inventory(
            long mainCapacity,
            long bagCapacity,
            int mainBranches = 0,
            int mainStone = 0,
            int bagBranches = 0)
        {
            var main = new ContainerState(
                new ContainerId("craft-main"),
                new ContainerDefinition(
                    "craft-main",
                    "Main",
                    new EncumbranceValue(mainCapacity)));
            var bag = new ContainerState(
                new ContainerId("craft-bag"),
                new ContainerDefinition(
                    "craft-bag",
                    "Bag",
                    new EncumbranceValue(bagCapacity)));

            if (mainBranches > 0)
            {
                InventoryOperations.AddStack(
                    main,
                    Branches,
                    mainBranches);
            }

            if (mainStone > 0)
            {
                InventoryOperations.AddStack(
                    main,
                    Stone,
                    mainStone);
            }

            if (bagBranches > 0)
            {
                InventoryOperations.AddStack(
                    bag,
                    Branches,
                    bagBranches);
            }

            return new PlayerInventoryState(
                main.Id,
                new[] { Branches, Kindling, Stone, Flake },
                new[] { main, bag });
        }

        private static ContainerId[] Order(PlayerInventoryState inventory) => new[] { inventory.MainContainer.Id, inventory.Containers[1].Id };
    }
}
