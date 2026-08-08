using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Runtime.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Runtime.Tests.Inventory
{
    public sealed class InventoryPrototypeFoodTests
    {
        [Test]
        public void NewSessionContainsPrototypeAppleAndMatchingBatchState()
        {
            var session = new InventoryPrototypeSession(System.Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            Assert.That(InventoryOperations.Count(session.BagContainer, InventoryPrototypeCatalog.Apple.Id), Is.EqualTo(3));
            Assert.That(session.PerishableItems.Batches.Count, Is.EqualTo(1));
            Assert.That(session.Food.CurrentSatiety, Is.EqualTo(100));
            session.PerishableItems.ValidateAgainst(session.Inventory);
        }

        [Test]
        public void FixedTickLoopAdvancesFoodNeed()
        {
            var session = new InventoryPrototypeSession(System.Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            for (long tick = 0; tick < PlayerFoodRules.SatietyLossIntervalTicks; tick++)
            {
                session.BeginSimulationTick(new WorldPosition(0, 0));
            }
            Assert.That(session.Food.CurrentSatiety, Is.EqualTo(99));
        }

        [Test]
        public void EatSelectedAppleRestoresSatietyAndConsumesOne()
        {
            var session = new InventoryPrototypeSession(System.Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            for (long tick = 0; tick < PlayerFoodRules.SatietyLossIntervalTicks; tick++)
            {
                session.BeginSimulationTick(new WorldPosition(0, 0));
            }
            var selection = new InventorySelection(
                InventoryPrototypeCatalog.BagContainerId,
                InventoryPrototypeCatalog.Apple.Id,
                default,
                false);

            FoodConsumptionResult result = session.Eat(selection);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.Food.CurrentSatiety, Is.EqualTo(100));
            Assert.That(InventoryOperations.Count(session.BagContainer, InventoryPrototypeCatalog.Apple.Id), Is.EqualTo(2));
        }

        [Test]
        public void V3CodecRoundTripPreservesFoodNeedAndPerishableBatches()
        {
            var session = new InventoryPrototypeSession(System.Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            session.BeginSimulationTick(new WorldPosition(1, 2));
            GameSaveSnapshot snapshot = session.CaptureGameSaveSnapshot();

            byte[] data = GameSaveBinaryCodec.Encode(snapshot);
            GameSaveSnapshot decoded = GameSaveBinaryCodec.Decode(data);

            Assert.That(GameSaveBinaryCodec.CurrentVersion, Is.EqualTo(3));
            Assert.That(decoded.Food, Is.EqualTo(snapshot.Food));
            Assert.That(decoded.Perishables.Batches.Count, Is.EqualTo(1));
            Assert.That(decoded.Perishables.Batches[0], Is.EqualTo(snapshot.Perishables.Batches[0]));
        }

        [Test]
        public void GeneratedBatchIdsReuseFirstFreeIdentifierFromLiveState()
        {
            var session = new InventoryPrototypeSession(
                System.Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            InventoryOperations.AddStack(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches,
                2);
            var appleSelection = new InventorySelection(
                InventoryPrototypeCatalog.BagContainerId,
                InventoryPrototypeCatalog.Apple.Id,
                default,
                false);
            var firstGeneratedId =
                new FoodBatchId("prototype-food-batch-000001");

            TransferResult firstTransfer = session.TransferPerishable(
                appleSelection,
                InventoryPrototypeCatalog.MainContainerId);

            Assert.That(firstTransfer.Transferred, Is.EqualTo(2));
            Assert.That(session.PerishableItems.ContainsBatch(firstGeneratedId), Is.True);

            var hungry = new PlayerFoodState(
                100,
                0,
                session.CurrentTick,
                PlayerFoodRules.FirstSatietyLossTickAfter(session.CurrentTick));
            for (int index = 0; index < 2; index++)
            {
                FoodConsumptionResult eaten = FoodConsumptionOperations.Consume(
                    hungry,
                    session.Inventory,
                    session.PerishableItems,
                    InventoryPrototypeCatalog.MainContainerId,
                    InventoryPrototypeCatalog.Apple.Id,
                    session.CurrentTick);
                Assert.That(eaten.Succeeded, Is.True);
            }

            Assert.That(session.PerishableItems.ContainsBatch(firstGeneratedId), Is.False);
            PerishableInventoryOperations.Add(
                session.BagContainer,
                InventoryPrototypeCatalog.Apple,
                session.PerishableItems,
                new FoodBatchId("replacement-batch"),
                3,
                session.CurrentTick);

            TransferResult secondTransfer = session.TransferPerishable(
                appleSelection,
                InventoryPrototypeCatalog.MainContainerId);

            Assert.That(secondTransfer.Transferred, Is.EqualTo(2));
            Assert.That(session.PerishableItems.ContainsBatch(firstGeneratedId), Is.True);
            Assert.That(
                session.PerishableItems.ContainsBatch(
                    new FoodBatchId("prototype-food-batch-000002")),
                Is.False);
            session.PerishableItems.ValidateAgainst(session.Inventory);
        }
    }
}
