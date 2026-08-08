using System.Linq;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Runtime.Inventory;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Tests.Inventory
{
    public sealed class InventoryPrototypeTests
    {
        [Test]
        public void ViewModelReflectsContainersRowsCapacityAndLoad()
        {
            var session = new InventoryPrototypeSession();

            InventoryPrototypeViewModel view =
                InventoryPrototypeViewModelBuilder.Build(session.Inventory);

            Assert.That(view.Main.Rows, Has.Count.EqualTo(4));
            Assert.That(view.Bag.Rows, Has.Count.EqualTo(2));
            Assert.That(view.Main.CapacityText, Is.EqualTo("10.500 / 12.000"));
            Assert.That(view.Bag.CapacityText, Is.EqualTo("3.600 / 20.000"));
            Assert.That(view.GrossLoadText, Is.EqualTo("14.100"));
            Assert.That(view.PerceivedLoadText, Is.EqualTo("14.100"));
            Assert.That(view.MovementLoadText, Is.EqualTo("117.5%"));
            Assert.That(view.MovementMultiplierText, Is.EqualTo("×0.87"));
            Assert.That(view.EquipmentLabels, Is.EqualTo(new[]
            {
                "Left hand: Empty",
                "Right hand: Empty",
                "Back: Empty"
            }));
        }

        [Test]
        public void ViewModelRowsAreCopiesRatherThanCoreCollections()
        {
            var session = new InventoryPrototypeSession();
            InventoryPrototypeViewModel before =
                InventoryPrototypeViewModelBuilder.Build(session.Inventory);

            InventoryOperations.RemoveStack(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches,
                2);

            Assert.That(
                before.Main.Rows.Single(row => row.DisplayName == "Branches").Quantity,
                Is.EqualTo(6));
            Assert.That(
                InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(4));
        }

        [Test]
        public void CommandAdapterTransfersThroughCoreOperations()
        {
            var session = new InventoryPrototypeSession();
            InventorySelection branches = FindSelection(session, "Branches");

            TransferResult result = session.Commands.Transfer(
                branches,
                session.BagContainer.Id);

            Assert.That(result.Changed, Is.True);
            Assert.That(
                InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id),
                Is.Zero);
            Assert.That(
                InventoryOperations.Count(session.BagContainer, InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(6));
        }

        [Test]
        public void BagCannotBeTransferredIntoItsOwnContainedContainer()
        {
            var session = new InventoryPrototypeSession();
            InventorySelection bag = FindSelection(session, "Prototype backpack");

            bool canTransfer = session.Commands.CanTransfer(bag, session.BagContainer.Id);
            TransferResult result = session.Commands.Transfer(bag, session.BagContainer.Id);

            Assert.That(canTransfer, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Bag.Id), Is.EqualTo(1));
        }

        [Test]
        public void EquipCommandUpdatesLoadAndViewModel()
        {
            var session = new InventoryPrototypeSession();
            InventorySelection bag = FindSelection(session, "Prototype backpack");

            EquipmentOperationResult result = session.Commands.Equip(bag, EquipmentSlot.Back);
            InventoryPrototypeViewModel view =
                InventoryPrototypeViewModelBuilder.Build(session.Inventory);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(view.EquipmentLabels[2], Is.EqualTo("Back: Prototype backpack"));
            Assert.That(view.GrossLoadText, Is.EqualTo("14.100"));
            Assert.That(view.PerceivedLoadText, Is.EqualTo("11.580"));
            Assert.That(view.MovementLoadText, Is.EqualTo("96.5%"));
            Assert.That(view.MovementMultiplierText, Is.EqualTo("×1.00"));
            Assert.That(session.BagContainer.Definition.Capacity.Units, Is.EqualTo(20000));
        }

        [Test]
        public void SessionMovementStateTracksEquipmentChanges()
        {
            var session = new InventoryPrototypeSession();
            InventorySelection bag = FindSelection(session, "Prototype backpack");

            Assert.That(session.MovementState.LoadRatio, Is.EqualTo(1.175).Within(1e-9));
            Assert.That(session.MovementState.SpeedMultiplier, Is.EqualTo(0.867).Within(1e-9));

            session.Commands.Equip(bag, EquipmentSlot.Back);

            Assert.That(session.MovementState.LoadRatio, Is.EqualTo(0.965).Within(1e-9));
            Assert.That(session.MovementState.SpeedMultiplier, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void UnequipCommandImmediatelyRestoresPerceivedLoad()
        {
            var session = new InventoryPrototypeSession();
            InventorySelection bag = FindSelection(session, "Prototype backpack");
            session.Commands.Equip(bag, EquipmentSlot.Back);

            session.Commands.Unequip(EquipmentSlot.Back);
            CarriedLoad load = CarriedLoadOperations.Calculate(session.Inventory);

            Assert.That(load.Perceived, Is.EqualTo(load.Gross));
        }

        [Test]
        public void UiBuildsTwoScrollableListsAndStartsWithInvalidActionsDisabled()
        {
            var root = new VisualElement();
            var session = new InventoryPrototypeSession();

            var ui = new InventoryPrototypeUiDocument(root, session);

            Assert.That(ui.MainList.itemsSource, Has.Count.EqualTo(4));
            Assert.That(ui.BagList.itemsSource, Has.Count.EqualTo(2));
            Assert.That(ui.MainList.virtualizationMethod, Is.EqualTo(CollectionVirtualizationMethod.FixedHeight));
            Assert.That(root.Q<Label>("movement-load").text, Is.EqualTo("Movement load: 117.5%"));
            Assert.That(root.Q<Label>("movement-multiplier").text, Is.EqualTo("Movement speed: ×0.87"));
            Assert.That(ui.TransferButton.enabledSelf, Is.False);
            Assert.That(ui.EquipButtons.All(button => !button.enabledSelf), Is.True);
            Assert.That(ui.UnequipButtons.All(button => !button.enabledSelf), Is.True);
        }

        [Test]
        public void UiStartsClosedAndToggleChangesExplicitVisibility()
        {
            var root = new VisualElement();
            var session = new InventoryPrototypeSession();

            var ui = new InventoryPrototypeUiDocument(root, session);

            Assert.That(ui.IsPanelOpen, Is.False);
            Assert.That(
                ui.Panel.style.display.value,
                Is.EqualTo(DisplayStyle.None));

            ui.TogglePanel();

            Assert.That(ui.IsPanelOpen, Is.True);
            Assert.That(
                ui.Panel.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));

            ui.TogglePanel();

            Assert.That(ui.IsPanelOpen, Is.False);
            Assert.That(
                ui.Panel.style.display.value,
                Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void UiButtonAvailabilityIsDerivedFromCommandPolicy()
        {
            var session = new InventoryPrototypeSession();
            var ui = new InventoryPrototypeUiDocument(new VisualElement(), session);
            InventorySelection bag = FindSelection(session, "Prototype backpack");

            ui.SelectForTests(bag);

            Assert.That(ui.TransferButton.enabledSelf, Is.False);
            Assert.That(ui.EquipButtons[0].enabledSelf, Is.False);
            Assert.That(ui.EquipButtons[1].enabledSelf, Is.False);
            Assert.That(ui.EquipButtons[2].enabledSelf, Is.True);
        }

        [Test]
        public void PrototypeSessionOwnerIsNotAMonoBehaviour()
        {
            Assert.That(typeof(InventoryPrototypeSession).IsSubclassOf(typeof(UnityEngine.MonoBehaviour)), Is.False);
        }

        [Test]
        public void HarvestCreatesGroundAndOneTimedAction()
        {
            var session = new InventoryPrototypeSession();
            long tick = session.BeginSimulationTick(new WorldPosition(4.5, 4.5));
            var result = session.HarvestAndStartTransfer(new WorldPosition(4.5, 4.5), 1.5, tick);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.GroundContainers, Has.Count.EqualTo(1));
            Assert.That(session.TransferAction.Status, Is.EqualTo(TransferActionStatus.Active));
            Assert.That(session.TransferAction.PlannedQuantity, Is.EqualTo(3));
        }

        [Test]
        public void TimedHarvestTransfersOnlyCapacityAndLeavesExactGroundRemainder()
        {
            var session = new InventoryPrototypeSession();
            WorldPosition position = new WorldPosition(4.5, 4.5);
            session.HarvestAndStartTransfer(position, 1.5, 1);
            session.AdvanceTransfer(61, position, false);

            Assert.That(session.TransferAction.Status, Is.EqualTo(TransferActionStatus.Completed));
            Assert.That(session.TransferAction.TransferredQuantity, Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(session.MainContainer, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(9));
            Assert.That(InventoryOperations.Count(session.GroundContainers[0].Container, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(3));
        }

        [Test]
        public void MovementInterruptsSessionActionWithoutMovingYield()
        {
            var session = new InventoryPrototypeSession();
            WorldPosition position = new WorldPosition(4.5, 4.5);
            session.HarvestAndStartTransfer(position, 1.5, 1);
            session.AdvanceTransfer(2, position, true);
            Assert.That(session.TransferAction.Status, Is.EqualTo(TransferActionStatus.Interrupted));
            Assert.That(InventoryOperations.Count(session.GroundContainers[0].Container, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(6));
        }

        [Test]
        public void SessionRejectsSecondActiveTransfer()
        {
            var session = new InventoryPrototypeSession();
            WorldPosition position = new WorldPosition(4.5, 4.5);
            session.HarvestAndStartTransfer(position, 1.5, 1);
            TransferActionResult second = session.StartGroundTransfer(session.GroundContainers[0], 1, 2);
            Assert.That(second.Reason, Is.EqualTo(TransferActionReason.AnotherActionActive));
        }

        [Test]
        public void ViewModelShowsNearbyGroundAndCoreProgress()
        {
            var session = new InventoryPrototypeSession();
            WorldPosition position = new WorldPosition(4.5, 4.5);
            session.HarvestAndStartTransfer(position, 1.5, 1);
            for (int i = 0; i < 30; i++) session.BeginSimulationTick(position);
            InventoryPrototypeViewModel view = InventoryPrototypeViewModelBuilder.Build(session);
            Assert.That(view.Ground.Rows, Has.Count.EqualTo(1));
            Assert.That(view.Ground.Rows[0].Quantity, Is.EqualTo(6));
            Assert.That(view.TransferStatusText, Does.Contain("3 planned"));
            Assert.That(view.TransferProgress, Is.GreaterThan(0.4).And.LessThan(0.6));
        }

        [Test]
        public void CraftedInconsistentSelectionsAreRejectedWithoutThrowing()
        {
            var session = new InventoryPrototypeSession();
            var stackMarkedUnique = new InventorySelection(
                session.MainContainer.Id,
                InventoryPrototypeCatalog.Branches.Id,
                session.Tool.InstanceId,
                true);
            var bagWithWrongOwner = new InventorySelection(
                session.BagContainer.Id,
                InventoryPrototypeCatalog.Bag.Id,
                session.Bag.InstanceId,
                true);

            Assert.That(
                session.Commands.CanTransfer(stackMarkedUnique, session.BagContainer.Id),
                Is.False);
            Assert.That(
                session.Commands.Transfer(stackMarkedUnique, session.BagContainer.Id).Outcome,
                Is.EqualTo(InventoryOperationOutcome.Rejected));
            Assert.That(
                session.Commands.CanEquip(bagWithWrongOwner, EquipmentSlot.Back),
                Is.False);
        }

        [Test]
        public void GroundViewFollowsActiveTransferSourceInsteadOfFirstHarvestedGround()
        {
            var session = new InventoryPrototypeSession();
            WorldPosition firstPosition = InventoryPrototypeCatalog.ResourcePositions[0];
            WorldPosition secondPosition = InventoryPrototypeCatalog.ResourcePositions[1];

            session.HarvestAndStartTransfer(firstPosition, 0.1, 1);
            session.AdvanceTransfer(2, firstPosition, true);
            session.HarvestAndStartTransfer(secondPosition, 0.1, 3);

            InventoryPrototypeViewModel view = InventoryPrototypeViewModelBuilder.Build(session);

            Assert.That(session.GroundContainers, Has.Count.EqualTo(2));
            Assert.That(session.TransferAction.SourceId, Is.EqualTo(session.GroundContainers[1].Container.Id));
            Assert.That(view.Ground.Id, Is.EqualTo(session.GroundContainers[1].Container.Id));
        }

        private static InventorySelection FindSelection(
            InventoryPrototypeSession session,
            string displayName)
        {
            return InventoryPrototypeViewModelBuilder.Build(session.Inventory)
                .Main.Rows.Single(row => row.DisplayName == displayName)
                .Selection;
        }
    }
}
