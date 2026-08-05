using System;
using AgeOfSurvival.Core.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Inventory
{
    public sealed class InventoryAggregateSnapshotTests
    {
        private static readonly ItemDefinition Tool = new ItemDefinition(
            new ItemDefinitionId("tool"),
            "Tool",
            ItemStateKind.Unique,
            new EncumbranceValue(2000),
            new EquipmentDefinition(
                EquipmentSlotMask.LeftHand | EquipmentSlotMask.RightHand));

        private static readonly ItemDefinition ContradictoryTool = new ItemDefinition(
            Tool.Id,
            "Tool",
            ItemStateKind.Unique,
            Tool.UnitEncumbrance,
            new EquipmentDefinition(EquipmentSlotMask.Back));

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
        public void ConstructorRejectsDuplicateUniqueInstanceAcrossContainers()
        {
            ContainerState main = CreateContainer("main", "main");
            ContainerState secondary = CreateContainer("secondary", "secondary");
            var duplicated = new UniqueItemState(Tool.Id, new ItemInstanceId("tool-01"));
            InventoryOperations.AddUnique(main, Tool, duplicated);
            InventoryOperations.AddUnique(secondary, Tool, duplicated);

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                new PlayerInventoryState(
                    main.Id,
                    new[] { Tool },
                    new[] { main, secondary }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.DuplicateItemInstance));
        }

        [Test]
        public void ConstructorRejectsMissingContainedContainer()
        {
            ContainerState main = CreateContainer("main", "main");
            var bag = new UniqueItemState(
                Bag.Id,
                new ItemInstanceId("bag-01"),
                new ContainerId("missing"));
            InventoryOperations.AddUnique(main, Bag, bag);

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                new PlayerInventoryState(main.Id, new[] { Bag }, new[] { main }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.MissingContainedContainer));
        }

        [Test]
        public void ConstructorRejectsTwoOwnersForSameContainedContainer()
        {
            ContainerState main = CreateContainer("main", "main");
            ContainerState bagContainer = CreateContainer("bag-container", "bag");
            InventoryOperations.AddUnique(
                main,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-01"),
                    bagContainer.Id));
            InventoryOperations.AddUnique(
                main,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-02"),
                    bagContainer.Id));

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                new PlayerInventoryState(
                    main.Id,
                    new[] { Bag },
                    new[] { main, bagContainer }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.DuplicateContainedContainerOwner));
        }

        [Test]
        public void ConstructorRejectsMainContainerAsContainedContainer()
        {
            ContainerState main = CreateContainer("main", "main");
            InventoryOperations.AddUnique(
                main,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-01"),
                    main.Id));

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                new PlayerInventoryState(main.Id, new[] { Bag }, new[] { main }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.MainContainerContained));
        }

        [Test]
        public void ConstructorRejectsContainedContainerOwningItself()
        {
            ContainerState main = CreateContainer("main", "main");
            ContainerState bagContainer = CreateContainer("bag-container", "bag");
            InventoryOperations.AddUnique(
                bagContainer,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-01"),
                    bagContainer.Id));

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                new PlayerInventoryState(
                    main.Id,
                    new[] { Bag },
                    new[] { main, bagContainer }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.ContainedContainerCycle));
        }

        [Test]
        public void ConstructorRejectsContainedContainerOwnershipCycle()
        {
            ContainerState main = CreateContainer("main", "main");
            ContainerState first = CreateContainer("first", "bag");
            ContainerState second = CreateContainer("second", "bag");
            InventoryOperations.AddUnique(
                first,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-01"),
                    second.Id));
            InventoryOperations.AddUnique(
                second,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-02"),
                    first.Id));

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                new PlayerInventoryState(
                    main.Id,
                    new[] { Bag },
                    new[] { main, first, second }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.ContainedContainerCycle));
        }

        [Test]
        public void ConstructorAcceptsAcyclicNestedContainers()
        {
            ContainerState main = CreateContainer("main", "main");
            ContainerState first = CreateContainer("first", "bag");
            ContainerState second = CreateContainer("second", "bag");
            InventoryOperations.AddUnique(
                main,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-01"),
                    first.Id));
            InventoryOperations.AddUnique(
                first,
                Bag,
                new UniqueItemState(
                    Bag.Id,
                    new ItemInstanceId("bag-02"),
                    second.Id));

            Assert.DoesNotThrow(() =>
                new PlayerInventoryState(
                    main.Id,
                    new[] { Bag },
                    new[] { main, first, second }));
        }

        [Test]
        public void ConstructorRestoresCompatibleEquipment()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out _,
                out UniqueItemState tool,
                out UniqueItemState bag);

            PlayerInventoryState restored = RecreateWithEquipment(
                inventory,
                new InventoryEquipmentSnapshot(
                    tool.InstanceId,
                    default,
                    bag.InstanceId));

            Assert.That(
                restored.Equipment.Get(EquipmentSlot.LeftHand),
                Is.EqualTo(tool.InstanceId));
            Assert.That(
                restored.Equipment.Get(EquipmentSlot.Back),
                Is.EqualTo(bag.InstanceId));
        }

        [Test]
        public void ConstructorRejectsMissingEquippedItem()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out _,
                out _,
                out _);

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                RecreateWithEquipment(
                    inventory,
                    new InventoryEquipmentSnapshot(
                        new ItemInstanceId("missing"),
                        default,
                        default)));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.EquipmentItemMissing));
        }

        [Test]
        public void ConstructorRejectsSameItemInTwoEquipmentSlots()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out _,
                out UniqueItemState tool,
                out _);

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                RecreateWithEquipment(
                    inventory,
                    new InventoryEquipmentSnapshot(
                        tool.InstanceId,
                        tool.InstanceId,
                        default)));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.EquipmentItemDuplicated));
        }

        [Test]
        public void ConstructorRejectsIncompatibleEquipmentSlot()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out _,
                out _,
                out UniqueItemState bag);

            InventoryAggregateException exception = Assert.Throws<InventoryAggregateException>(() =>
                RecreateWithEquipment(
                    inventory,
                    new InventoryEquipmentSnapshot(
                        bag.InstanceId,
                        default,
                        default)));

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.EquipmentItemIncompatible));
        }

        [Test]
        public void CaptureRejectsDuplicateIntroducedAfterConstruction()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out ContainerState bagContainer,
                out UniqueItemState tool,
                out _);
            InventoryOperations.AddUnique(bagContainer, Tool, tool);

            InventoryAggregateException exception =
                Assert.Throws<InventoryAggregateException>(() => inventory.CaptureSnapshot());

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.DuplicateItemInstance));
        }

        [Test]
        public void CaptureRejectsOrphanedEquipmentReference()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out ContainerState main,
                out _,
                out UniqueItemState tool,
                out _);
            EquipmentOperations.Equip(
                inventory,
                tool.InstanceId,
                EquipmentSlot.LeftHand);
            InventoryOperations.RemoveUnique(main, Tool, tool.InstanceId);

            InventoryAggregateException exception =
                Assert.Throws<InventoryAggregateException>(() => inventory.CaptureSnapshot());

            Assert.That(
                exception.Violation,
                Is.EqualTo(InventoryAggregateViolation.EquipmentItemMissing));
        }

        [Test]
        public void EquivalentInsertionOrdersProduceEquivalentCanonicalSnapshots()
        {
            PlayerInventoryState first = CreateInventory(
                false,
                false,
                false,
                out _,
                out _,
                out UniqueItemState firstTool,
                out UniqueItemState firstBag);
            PlayerInventoryState second = CreateInventory(
                true,
                true,
                true,
                out _,
                out _,
                out UniqueItemState secondTool,
                out UniqueItemState secondBag);

            first = RecreateWithEquipment(
                first,
                new InventoryEquipmentSnapshot(
                    firstTool.InstanceId,
                    default,
                    firstBag.InstanceId));
            second = RecreateWithEquipment(
                second,
                new InventoryEquipmentSnapshot(
                    secondTool.InstanceId,
                    default,
                    secondBag.InstanceId));

            AssertSnapshotsEqual(first.CaptureSnapshot(), second.CaptureSnapshot());
        }

        [Test]
        public void SnapshotRemainsUnchangedAfterLiveInventoryMutation()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out ContainerState bagContainer,
                out _,
                out _);
            PlayerInventorySnapshot snapshot = inventory.CaptureSnapshot();

            InventoryOperations.RemoveStack(bagContainer, Stones, 2);

            InventoryContainerSnapshot capturedBag =
                FindContainer(snapshot, bagContainer.Id);
            Assert.That(capturedBag, Is.Not.Null);
            Assert.That(capturedBag.Entries.Count, Is.EqualTo(1));
            Assert.That(capturedBag.Entries[0].DefinitionId, Is.EqualTo(Stones.Id));
            Assert.That(capturedBag.Entries[0].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotDefinitionFingerprintIncludesEquipmentRules()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out _,
                out _,
                out _,
                out _);

            PlayerInventorySnapshot snapshot = inventory.CaptureSnapshot();
            InventoryDefinitionSnapshot tool = FindDefinition(snapshot, Tool.Id);

            Assert.That(tool.HasEquipment, Is.True);
            Assert.That(
                tool.CompatibleSlots,
                Is.EqualTo(EquipmentSlotMask.LeftHand | EquipmentSlotMask.RightHand));
            Assert.That(tool.ContainedContainerReductionPercent, Is.EqualTo(0));
        }

        [Test]
        public void CanonicalRegistryRejectsContradictoryEquipmentRules()
        {
            PlayerInventoryState inventory = CreateInventory(
                false,
                false,
                false,
                out ContainerState main,
                out _,
                out _,
                out _);
            var contradictory = new UniqueItemState(
                ContradictoryTool.Id,
                new ItemInstanceId("tool-contradictory"));

            Assert.Throws<ArgumentException>(() =>
                InventoryOperations.AddUnique(
                    main,
                    ContradictoryTool,
                    contradictory));
            Assert.That(
                inventory.TryFindUnique(
                    contradictory.InstanceId,
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        private static PlayerInventoryState CreateInventory(
            bool reverseDefinitions,
            bool reverseContainers,
            bool reverseEntries,
            out ContainerState main,
            out ContainerState bagContainer,
            out UniqueItemState tool,
            out UniqueItemState bag)
        {
            main = CreateContainer("main", "main");
            bagContainer = CreateContainer("bag-container", "bag");
            tool = new UniqueItemState(Tool.Id, new ItemInstanceId("tool-01"));
            bag = new UniqueItemState(
                Bag.Id,
                new ItemInstanceId("bag-01"),
                bagContainer.Id);

            if (reverseEntries)
            {
                InventoryOperations.AddUnique(main, Bag, bag);
                InventoryOperations.AddUnique(main, Tool, tool);
            }
            else
            {
                InventoryOperations.AddUnique(main, Tool, tool);
                InventoryOperations.AddUnique(main, Bag, bag);
            }

            InventoryOperations.AddStack(bagContainer, Stones, 2);

            ItemDefinition[] definitions = reverseDefinitions
                ? new[] { Stones, Bag, Tool }
                : new[] { Tool, Bag, Stones };
            ContainerState[] containers = reverseContainers
                ? new[] { bagContainer, main }
                : new[] { main, bagContainer };

            return new PlayerInventoryState(main.Id, definitions, containers);
        }

        private static PlayerInventoryState RecreateWithEquipment(
            PlayerInventoryState source,
            InventoryEquipmentSnapshot equipment) =>
            new PlayerInventoryState(
                source.MainContainer.Id,
                source.Definitions,
                source.Containers,
                equipment);

        private static ContainerState CreateContainer(string id, string key) =>
            new ContainerState(
                new ContainerId(id),
                new ContainerDefinition(
                    key,
                    key,
                    new EncumbranceValue(30000)));

        private static InventoryContainerSnapshot FindContainer(
            PlayerInventorySnapshot snapshot,
            ContainerId id)
        {
            for (int index = 0; index < snapshot.Containers.Count; index++)
            {
                if (snapshot.Containers[index].Id.Equals(id))
                {
                    return snapshot.Containers[index];
                }
            }

            return null;
        }

        private static InventoryDefinitionSnapshot FindDefinition(
            PlayerInventorySnapshot snapshot,
            ItemDefinitionId id)
        {
            for (int index = 0; index < snapshot.Definitions.Count; index++)
            {
                if (snapshot.Definitions[index].Id.Equals(id))
                {
                    return snapshot.Definitions[index];
                }
            }

            Assert.Fail($"Definition '{id}' was not captured.");
            return default;
        }

        private static void AssertSnapshotsEqual(
            PlayerInventorySnapshot expected,
            PlayerInventorySnapshot actual)
        {
            Assert.That(actual.MainContainerId, Is.EqualTo(expected.MainContainerId));
            Assert.That(actual.Equipment, Is.EqualTo(expected.Equipment));
            Assert.That(actual.Definitions.Count, Is.EqualTo(expected.Definitions.Count));
            Assert.That(actual.Containers.Count, Is.EqualTo(expected.Containers.Count));

            for (int index = 0; index < expected.Definitions.Count; index++)
            {
                Assert.That(
                    actual.Definitions[index],
                    Is.EqualTo(expected.Definitions[index]));
            }

            for (int containerIndex = 0;
                 containerIndex < expected.Containers.Count;
                 containerIndex++)
            {
                InventoryContainerSnapshot expectedContainer =
                    expected.Containers[containerIndex];
                InventoryContainerSnapshot actualContainer =
                    actual.Containers[containerIndex];

                Assert.That(actualContainer.Id, Is.EqualTo(expectedContainer.Id));
                Assert.That(
                    actualContainer.DefinitionKey,
                    Is.EqualTo(expectedContainer.DefinitionKey));
                Assert.That(
                    actualContainer.Capacity,
                    Is.EqualTo(expectedContainer.Capacity));
                Assert.That(
                    actualContainer.Entries.Count,
                    Is.EqualTo(expectedContainer.Entries.Count));

                for (int entryIndex = 0;
                     entryIndex < expectedContainer.Entries.Count;
                     entryIndex++)
                {
                    Assert.That(
                        actualContainer.Entries[entryIndex],
                        Is.EqualTo(expectedContainer.Entries[entryIndex]));
                }
            }
        }
    }
}
