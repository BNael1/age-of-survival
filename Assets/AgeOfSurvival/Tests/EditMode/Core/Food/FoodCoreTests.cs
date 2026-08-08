using System;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Food
{
    public sealed class FoodCoreTests
    {
        private static readonly ItemDefinition Apple = new ItemDefinition(
            new ItemDefinitionId("apple-test"),
            "Apple",
            ItemStateKind.Stackable,
            new EncumbranceValue(200),
            consumable: new ConsumableDefinition(20, 60, 20),
            nutrition: new NutritionDefinition(95),
            perishable: new PerishableDefinition(500, 800));

        private static readonly ItemDefinition Ration = new ItemDefinition(
            new ItemDefinitionId("ration-test"),
            "Ration",
            ItemStateKind.Stackable,
            new EncumbranceValue(300),
            consumable: new ConsumableDefinition(10),
            nutrition: new NutritionDefinition(250));

        [Test]
        public void SatietyCatchUpUsesFixedTicksWithoutPerTickLoop()
        {
            var state = new PlayerFoodState();
            long target = PlayerFoodRules.SatietyLossIntervalTicks * 17L;
            PlayerFoodOperations.AdvanceToTick(state, target);
            Assert.That(state.CurrentSatiety, Is.EqualTo(83));
            Assert.That(state.CurrentTick, Is.EqualTo(target));
        }

        [Test]
        public void SpoilageUsesTwoThresholdsAndDoesNotMutateOnProjection()
        {
            var batch = new PerishableBatchState(
                new FoodBatchId("batch-a"),
                new ContainerId("main"),
                Apple.Id,
                2,
                0,
                0);
            Assert.That(FoodSpoilageOperations.FreshnessAt(batch, Apple.Perishable, 499), Is.EqualTo(FoodFreshness.Fresh));
            Assert.That(FoodSpoilageOperations.FreshnessAt(batch, Apple.Perishable, 500), Is.EqualTo(FoodFreshness.Stale));
            Assert.That(FoodSpoilageOperations.FreshnessAt(batch, Apple.Perishable, 800), Is.EqualTo(FoodFreshness.Rotten));
            Assert.That(batch.LastEvaluatedTick, Is.EqualTo(0));
        }

        [Test]
        public void TransferSplitsOnlyTheBoundaryBatchAndPreservesDecay()
        {
            ContainerState main = Container("main", 10000);
            ContainerState bag = Container("bag", 10000);
            var perishables = new PerishableInventoryState();
            PerishableInventoryOperations.Add(main, Apple, perishables, new FoodBatchId("old"), 2, 0);
            PerishableInventoryOperations.Add(main, Apple, perishables, new FoodBatchId("new"), 3, 200);
            var inventory = new PlayerInventoryState(main.Id, new[] { Apple }, new[] { main, bag });

            TransferResult result = PerishableInventoryOperations.Transfer(
                inventory,
                perishables,
                main,
                bag,
                Apple,
                3,
                400,
                () => new FoodBatchId("split"));

            Assert.That(result.Transferred, Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(main, Apple.Id), Is.EqualTo(2));
            Assert.That(InventoryOperations.Count(bag, Apple.Id), Is.EqualTo(3));
            Assert.That(perishables.ContainsBatch(new FoodBatchId("old")), Is.True);
            Assert.That(perishables.ContainsBatch(new FoodBatchId("split")), Is.True);
            perishables.ValidateAgainst(inventory);
        }

        [Test]
        public void EatingUsesOldestBatchAndRottenFoodRemainsConsumableButUnsafe()
        {
            ContainerState main = Container("main", 10000);
            var perishables = new PerishableInventoryState();
            PerishableInventoryOperations.Add(main, Apple, perishables, new FoodBatchId("old"), 1, 0);
            var inventory = new PlayerInventoryState(main.Id, new[] { Apple }, new[] { main });
            var food = new PlayerFoodState(100, 50, 800, 900);

            FoodConsumptionResult result = FoodConsumptionOperations.Consume(
                food,
                inventory,
                perishables,
                main.Id,
                Apple.Id,
                800);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Freshness, Is.EqualTo(FoodFreshness.Rotten));
            Assert.That(result.UnsafeFood, Is.True);
            Assert.That(result.SatietyApplied, Is.EqualTo(4));
            Assert.That(food.CurrentSatiety, Is.EqualTo(54));
            Assert.That(InventoryOperations.Count(main, Apple.Id), Is.EqualTo(0));
        }

        [Test]
        public void NonPerishableConsumableUsesOrdinaryStackAndCountsAsFresh()
        {
            ContainerState main = Container("main", 10000);
            InventoryOperations.AddStack(main, Ration, 2);
            var inventory = new PlayerInventoryState(
                main.Id,
                new[] { Ration },
                new[] { main });
            var perishables = new PerishableInventoryState();
            var food = new PlayerFoodState(
                100,
                50,
                0,
                PlayerFoodRules.SatietyLossIntervalTicks);

            Assert.That(
                FoodConsumptionOperations.CanConsume(
                    food,
                    inventory,
                    perishables,
                    main.Id,
                    Ration.Id),
                Is.True);

            FoodConsumptionResult result = FoodConsumptionOperations.Consume(
                food,
                inventory,
                perishables,
                main.Id,
                Ration.Id,
                0);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Freshness, Is.EqualTo(FoodFreshness.Fresh));
            Assert.That(result.UnsafeFood, Is.False);
            Assert.That(result.SatietyApplied, Is.EqualTo(10));
            Assert.That(food.CurrentSatiety, Is.EqualTo(60));
            Assert.That(
                InventoryOperations.Count(main, Ration.Id),
                Is.EqualTo(1));
            Assert.That(perishables.Batches, Is.Empty);
        }

        [Test]
        public void OrdinaryStackApiRejectsPerishableDefinition()
        {
            ContainerState main = Container("main", 10000);
            Assert.Throws<InvalidOperationException>(() => InventoryOperations.AddStack(main, Apple, 1));
        }

        [Test]
        public void PerishableAddRejectsNegativeTickWithoutMutatingInventory()
        {
            ContainerState main = Container("main", 10000);
            var perishables = new PerishableInventoryState();

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    PerishableInventoryOperations.Add(
                        main,
                        Apple,
                        perishables,
                        new FoodBatchId("negative-tick"),
                        1,
                        -1));

            Assert.That(exception.ParamName, Is.EqualTo("currentTick"));
            Assert.That(InventoryOperations.Count(main, Apple.Id), Is.EqualTo(0));
            Assert.That(perishables.Batches, Is.Empty);
        }

        private static ContainerState Container(string id, long capacity) =>
            new ContainerState(
                new ContainerId(id),
                new ContainerDefinition(id, id, new EncumbranceValue(capacity)));
    }
}
