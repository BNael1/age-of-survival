using System.Collections;
using AgeOfSurvival.Core.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Inventory
{
    public sealed class EquipmentTests
    {
        private static readonly ItemDefinition Tool = new ItemDefinition(
            new ItemDefinitionId("tool"),
            "Tool",
            ItemStateKind.Unique,
            new EncumbranceValue(2000),
            new EquipmentDefinition(EquipmentSlotMask.LeftHand | EquipmentSlotMask.RightHand));

        private static readonly ItemDefinition Bag = new ItemDefinition(
            new ItemDefinitionId("bag"),
            "Bag",
            ItemStateKind.Unique,
            new EncumbranceValue(1000),
            new EquipmentDefinition(EquipmentSlotMask.Back, 70));

        private static readonly ItemDefinition Stones = new ItemDefinition(
            new ItemDefinitionId("stones"),
            "Stones",
            ItemStateKind.Stackable,
            new EncumbranceValue(1500));

        [Test]
        public void CompatibleUniqueItemCanBeEquipped()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out UniqueItemState tool, out _);

            EquipmentOperationResult result = EquipmentOperations.Equip(
                inventory,
                tool.InstanceId,
                EquipmentSlot.LeftHand);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.Equipment.Get(EquipmentSlot.LeftHand), Is.EqualTo(tool.InstanceId));
        }

        [Test]
        public void IncompatibleSlotIsRejectedWithoutMutation()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out _, out UniqueItemState bag);

            EquipmentOperationResult result = EquipmentOperations.Equip(
                inventory,
                bag.InstanceId,
                EquipmentSlot.LeftHand);

            Assert.That(result.Outcome, Is.EqualTo(EquipmentOperationOutcome.IncompatibleSlot));
            Assert.That(inventory.Equipment.Get(EquipmentSlot.LeftHand).IsValid, Is.False);
        }

        [Test]
        public void OccupiedSlotRejectsSecondItem()
        {
            PlayerInventoryState inventory = CreateInventory(out ContainerState main, out _, out UniqueItemState tool, out _);
            var second = new UniqueItemState(Tool.Id, new ItemInstanceId("tool-02"));
            InventoryOperations.AddUnique(main, Tool, second);
            EquipmentOperations.Equip(inventory, tool.InstanceId, EquipmentSlot.LeftHand);

            EquipmentOperationResult result = EquipmentOperations.Equip(
                inventory,
                second.InstanceId,
                EquipmentSlot.LeftHand);

            Assert.That(result.Outcome, Is.EqualTo(EquipmentOperationOutcome.SlotOccupied));
            Assert.That(inventory.Equipment.Get(EquipmentSlot.LeftHand), Is.EqualTo(tool.InstanceId));
        }

        [Test]
        public void SameItemCannotOccupyTwoSlots()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out UniqueItemState tool, out _);
            EquipmentOperations.Equip(inventory, tool.InstanceId, EquipmentSlot.LeftHand);

            EquipmentOperationResult result = EquipmentOperations.Equip(
                inventory,
                tool.InstanceId,
                EquipmentSlot.RightHand);

            Assert.That(result.Outcome, Is.EqualTo(EquipmentOperationOutcome.AlreadyEquipped));
            Assert.That(inventory.Equipment.Get(EquipmentSlot.RightHand).IsValid, Is.False);
        }

        [Test]
        public void UnequipClearsSlotAndKeepsItemInContainer()
        {
            PlayerInventoryState inventory = CreateInventory(out ContainerState main, out _, out UniqueItemState tool, out _);
            EquipmentOperations.Equip(inventory, tool.InstanceId, EquipmentSlot.LeftHand);

            EquipmentOperationResult result = EquipmentOperations.Unequip(
                inventory,
                EquipmentSlot.LeftHand);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.Equipment.Get(EquipmentSlot.LeftHand).IsValid, Is.False);
            Assert.That(InventoryOperations.Count(main, Tool.Id), Is.EqualTo(1));
        }

        [Test]
        public void BagContentsAndRawCapacityStayUnchangedAcrossEquipment()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out ContainerState bagContainer, out _, out UniqueItemState bag);
            EncumbranceValue capacity = bagContainer.Definition.Capacity;
            int stones = InventoryOperations.Count(bagContainer, Stones.Id);

            EquipmentOperations.Equip(inventory, bag.InstanceId, EquipmentSlot.Back);
            EquipmentOperations.Unequip(inventory, EquipmentSlot.Back);

            Assert.That(bagContainer.Definition.Capacity, Is.EqualTo(capacity));
            Assert.That(InventoryOperations.Count(bagContainer, Stones.Id), Is.EqualTo(stones));
        }

        [Test]
        public void BagReductionIsInactiveWhenNotEquipped()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out _, out _);

            CarriedLoad load = CarriedLoadOperations.Calculate(inventory);

            Assert.That(load.Gross.Units, Is.EqualTo(6000));
            Assert.That(load.Perceived, Is.EqualTo(load.Gross));
            Assert.That(load.Reduction, Is.EqualTo(EncumbranceValue.Zero));
        }

        [Test]
        public void EquippedBackBagReducesOnlyItsContents()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out ContainerState bagContainer, out _, out UniqueItemState bag);
            EquipmentOperations.Equip(inventory, bag.InstanceId, EquipmentSlot.Back);

            CarriedLoad load = CarriedLoadOperations.Calculate(inventory);

            Assert.That(bagContainer.UsedCapacity.Units, Is.EqualTo(3000));
            Assert.That(load.Gross.Units, Is.EqualTo(6000));
            Assert.That(load.Reduction.Units, Is.EqualTo(2100));
            Assert.That(load.Perceived.Units, Is.EqualTo(3900));
        }

        [Test]
        public void UnequippingBagImmediatelyRemovesReduction()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out _, out UniqueItemState bag);
            EquipmentOperations.Equip(inventory, bag.InstanceId, EquipmentSlot.Back);
            EquipmentOperations.Unequip(inventory, EquipmentSlot.Back);

            CarriedLoad load = CarriedLoadOperations.Calculate(inventory);

            Assert.That(load.Perceived, Is.EqualTo(load.Gross));
        }

        [Test]
        public void MissingItemAndEmptySlotReturnExplicitResults()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out _, out _);

            EquipmentOperationResult equip = EquipmentOperations.Equip(
                inventory,
                new ItemInstanceId("missing"),
                EquipmentSlot.Back);
            EquipmentOperationResult unequip = EquipmentOperations.Unequip(
                inventory,
                EquipmentSlot.Back);

            Assert.That(equip.Outcome, Is.EqualTo(EquipmentOperationOutcome.ItemNotFound));
            Assert.That(unequip.Outcome, Is.EqualTo(EquipmentOperationOutcome.SlotEmpty));
        }

        [Test]
        public void AggregateRegistriesAreReadOnlyCopies()
        {
            PlayerInventoryState inventory = CreateInventory(out _, out _, out _, out _);

            Assert.That(((IList)inventory.Definitions).IsReadOnly, Is.True);
            Assert.That(((IList)inventory.Containers).IsReadOnly, Is.True);
        }

        private static PlayerInventoryState CreateInventory(
            out ContainerState main,
            out ContainerState bagContainer,
            out UniqueItemState tool,
            out UniqueItemState bag)
        {
            main = new ContainerState(
                new ContainerId("main"),
                new ContainerDefinition("main", "Main", new EncumbranceValue(12000)));
            bagContainer = new ContainerState(
                new ContainerId("bag-container"),
                new ContainerDefinition("bag", "Bag", new EncumbranceValue(20000)));
            tool = new UniqueItemState(Tool.Id, new ItemInstanceId("tool-01"));
            bag = new UniqueItemState(
                Bag.Id,
                new ItemInstanceId("bag-01"),
                bagContainer.Id);
            InventoryOperations.AddUnique(main, Tool, tool);
            InventoryOperations.AddUnique(main, Bag, bag);
            InventoryOperations.AddStack(bagContainer, Stones, 2);

            return new PlayerInventoryState(
                main.Id,
                new[] { Tool, Bag, Stones },
                new[] { main, bagContainer });
        }
    }
}
