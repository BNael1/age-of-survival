using System.Linq;
using AgeOfSurvival.Core.Inventory;
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
            Assert.That(view.Bag.Rows, Has.Count.EqualTo(1));
            Assert.That(view.Main.CapacityText, Is.EqualTo("10.500 / 12.000"));
            Assert.That(view.Bag.CapacityText, Is.EqualTo("3.000 / 20.000"));
            Assert.That(view.GrossLoadText, Is.EqualTo("13.500"));
            Assert.That(view.PerceivedLoadText, Is.EqualTo("13.500"));
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
            Assert.That(view.GrossLoadText, Is.EqualTo("13.500"));
            Assert.That(view.PerceivedLoadText, Is.EqualTo("11.400"));
            Assert.That(session.BagContainer.Definition.Capacity.Units, Is.EqualTo(20000));
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
            Assert.That(ui.BagList.itemsSource, Has.Count.EqualTo(1));
            Assert.That(ui.MainList.virtualizationMethod, Is.EqualTo(CollectionVirtualizationMethod.FixedHeight));
            Assert.That(ui.TransferButton.enabledSelf, Is.False);
            Assert.That(ui.EquipButtons.All(button => !button.enabledSelf), Is.True);
            Assert.That(ui.UnequipButtons.All(button => !button.enabledSelf), Is.True);
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
