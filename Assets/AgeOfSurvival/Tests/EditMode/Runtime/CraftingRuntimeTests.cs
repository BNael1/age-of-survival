using System.Linq;
using AgeOfSurvival.Core.Crafting;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Inventory;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class CraftingRuntimeTests
    {
        [Test]
        public void PrototypeCatalogContainsThreeHandRecipesWithExactDurations()
        {
            Assert.That(CraftPrototypeCatalog.Recipes.Recipes, Has.Count.EqualTo(3));
            Assert.That(CraftPrototypeCatalog.Kindling.DurationTicks, Is.EqualTo(90));
            Assert.That(CraftPrototypeCatalog.StoneFlakes.DurationTicks, Is.EqualTo(120));
            Assert.That(CraftPrototypeCatalog.WoodenStakes.DurationTicks, Is.EqualTo(150));
            Assert.That(CraftPrototypeCatalog.Recipes.Recipes.All(r => r.MethodId.Equals(CraftMethodIds.Hand)), Is.True);
        }

        [Test]
        public void PrototypeCraftOutputsPreserveInputEncumbrance()
        {
            Assert.That(InventoryPrototypeCatalog.Kindling.UnitEncumbrance.Units,
                Is.EqualTo(InventoryPrototypeCatalog.Branches.UnitEncumbrance.Multiply(2).Units));
            Assert.That(InventoryPrototypeCatalog.StoneFlakes.UnitEncumbrance.Multiply(2).Units,
                Is.EqualTo(InventoryPrototypeCatalog.Stones.UnitEncumbrance.Units));
            Assert.That(InventoryPrototypeCatalog.WoodenStakes.UnitEncumbrance.Multiply(2).Units,
                Is.EqualTo(InventoryPrototypeCatalog.Wood.UnitEncumbrance.Units));
        }

        [Test]
        public void SessionCraftConsumesOnlyAtCompletion()
        {
            var session = new InventoryPrototypeSession();
            CraftActionResult started = session.StartCraft(CraftPrototypeCatalog.KindlingRecipeId, 0);
            Assert.That(started.Succeeded, Is.True);
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(6));
            session.AdvanceCraft(89, false);
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(6));
            session.AdvanceCraft(90, false);
            Assert.That(session.CraftAction.Status, Is.EqualTo(CraftActionStatus.Completed));
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(4));
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Kindling.Id), Is.EqualTo(1));
        }

        [Test]
        public void ActiveCraftBlocksGroundTransferAndHarvestMutation()
        {
            var session = new InventoryPrototypeSession();
            ResourceState target = session.Resources[0];
            session.StartCraft(CraftPrototypeCatalog.KindlingRecipeId, 0);
            ResourceYieldResult yield = session.HarvestAndStartTransfer(target.Position, 0.25, 1);
            Assert.That(yield.Succeeded, Is.False);
            Assert.That(target.Availability, Is.EqualTo(ResourceAvailability.Available));
            Assert.That(session.GroundContainers, Is.Empty);
        }

        [Test]
        public void ActiveGroundTransferBlocksCraft()
        {
            var session = new InventoryPrototypeSession();
            WorldPosition position = InventoryPrototypeCatalog.ResourcePositions[0];
            session.HarvestAndStartTransfer(position, 0.25, 1);
            CraftActionResult craft = session.StartCraft(CraftPrototypeCatalog.KindlingRecipeId, 1);
            Assert.That(craft.Succeeded, Is.False);
            Assert.That(craft.Reason, Is.EqualTo(CraftActionReason.AnotherActionActive));
        }

        [Test]
        public void MovingInterruptsCraftAndPreservesIngredients()
        {
            var session = new InventoryPrototypeSession();
            session.StartCraft(CraftPrototypeCatalog.KindlingRecipeId, 0);
            session.AdvanceCraft(1, true);
            Assert.That(session.CraftAction.Status, Is.EqualTo(CraftActionStatus.Interrupted));
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(6));
        }

        [Test]
        public void SaveAndQuitCancellationCancelsBothKindsOfTimedAction()
        {
            var craftSession = new InventoryPrototypeSession();
            craftSession.StartCraft(CraftPrototypeCatalog.KindlingRecipeId, 0);
            Assert.That(craftSession.CancelActiveActionsForSaveAndQuit(), Is.True);
            Assert.That(craftSession.CraftAction.Status, Is.EqualTo(CraftActionStatus.Interrupted));

            var transferSession = new InventoryPrototypeSession();
            WorldPosition position = InventoryPrototypeCatalog.ResourcePositions[0];
            long tick = transferSession.BeginSimulationTick(position);
            ResourceYieldResult startedTransfer =
                transferSession.HarvestAndStartTransfer(
                    position,
                    0.25,
                    tick);

            Assert.That(startedTransfer.Succeeded, Is.True);
            Assert.That(
                transferSession.TransferAction.Status,
                Is.EqualTo(TransferActionStatus.Active));

            Assert.That(
                transferSession.CancelActiveActionsForSaveAndQuit(),
                Is.True);
            Assert.That(
                transferSession.TransferAction.Status,
                Is.EqualTo(TransferActionStatus.Interrupted));
        }

        [Test]
        public void SaveSnapshotCarriesCraftOutputDefinitions()
        {
            var session = new InventoryPrototypeSession();
            var snapshot = session.CaptureGameSaveSnapshot(
                ConstructionSaveSnapshot.Empty);
            Assert.That(snapshot.Inventory.Definitions.Any(d => d.Id.Equals(InventoryPrototypeCatalog.Kindling.Id)), Is.True);
            Assert.That(snapshot.Inventory.Definitions.Any(d => d.Id.Equals(InventoryPrototypeCatalog.StoneFlakes.Id)), Is.True);
            Assert.That(snapshot.Inventory.Definitions.Any(d => d.Id.Equals(InventoryPrototypeCatalog.WoodenStakes.Id)), Is.True);
        }

        [Test]
        public void CraftUiShowsThreeRecipesAndReadyKindling()
        {
            var session = new InventoryPrototypeSession();
            var ui = new InventoryPrototypeUiDocument(new VisualElement(), session);
            Assert.That(ui.CraftList.itemsSource, Has.Count.EqualTo(3));
            ui.SelectCraftForTests(CraftPrototypeCatalog.KindlingRecipeId);
            Assert.That(ui.CraftButton.enabledSelf, Is.True);
            Assert.That(ui.CraftStatusText, Is.EqualTo("Craft: idle"));
        }

        [Test]
        public void OrdinaryInventoryTransferIsBlockedWhileCraftIsActive()
        {
            var session = new InventoryPrototypeSession();
            InventorySelection stones = InventoryPrototypeViewModelBuilder.Build(session.Inventory)
                .Main.Rows.Single(row => row.DisplayName == "Stones").Selection;
            session.StartCraft(CraftPrototypeCatalog.KindlingRecipeId, 0);
            Assert.That(session.Commands.CanTransfer(stones, session.BagContainer.Id), Is.False);
            Assert.That(session.Commands.Transfer(stones, session.BagContainer.Id).Changed, Is.False);
        }
    }
}
