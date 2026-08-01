using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using AgeOfSurvival.Core.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Inventory
{
    public sealed class InventoryOperationsTests
    {
        private static readonly ItemDefinition Branches = new ItemDefinition(
            new ItemDefinitionId("branches"),
            "Branches",
            ItemStateKind.Stackable,
            new EncumbranceValue(1000));

        private static readonly ItemDefinition Stones = new ItemDefinition(
            new ItemDefinitionId("stones"),
            "Stones",
            ItemStateKind.Stackable,
            new EncumbranceValue(1500));

        private static readonly ItemDefinition Tool = new ItemDefinition(
            new ItemDefinitionId("tool"),
            "Tool",
            ItemStateKind.Unique,
            new EncumbranceValue(2000));

        [Test]
        public void DefaultIdentifiersAreInvalidSafeAndComparable()
        {
            var definitionId = default(ItemDefinitionId);
            var instanceId = default(ItemInstanceId);
            var containerId = default(ContainerId);

            Assert.That(definitionId.IsValid, Is.False);
            Assert.That(instanceId.IsValid, Is.False);
            Assert.That(containerId.IsValid, Is.False);
            Assert.That(definitionId.ToString(), Is.Empty);
            Assert.That(instanceId.ToString(), Is.Empty);
            Assert.That(containerId.ToString(), Is.Empty);
            Assert.That(definitionId.GetHashCode(), Is.Zero);
            Assert.That(instanceId.GetHashCode(), Is.Zero);
            Assert.That(containerId.GetHashCode(), Is.Zero);
            Assert.That(() => new ContainerState(containerId, Definition(1000)), Throws.ArgumentException);
        }

        [Test]
        public void IdentifiersUseOrdinalEqualityOrderingAndHashing()
        {
            AssertIdentifierContract(
                new ItemDefinitionId("item-a"),
                new ItemDefinitionId("item-a"),
                new ItemDefinitionId("item-b"));
            AssertIdentifierContract(
                new ItemInstanceId("instance-a"),
                new ItemInstanceId("instance-a"),
                new ItemInstanceId("instance-b"));
            AssertIdentifierContract(
                new ContainerId("container-a"),
                new ContainerId("container-a"),
                new ContainerId("container-b"));
        }

        [Test]
        public void EncumbranceIsNonNegativeDeterministicAndFormatted()
        {
            var value = new EncumbranceValue(1250);

            Assert.That(default(EncumbranceValue), Is.EqualTo(EncumbranceValue.Zero));
            Assert.That(value.ToString(), Is.EqualTo("1.250"));
            Assert.That(value.Multiply(3).Units, Is.EqualTo(3750));
            Assert.That(() => new EncumbranceValue(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DefaultAddResultIsRejectedAndSafe()
        {
            AddItemResult result = default;

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.Rejected));
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Requested, Is.Zero);
            Assert.That(result.Accepted, Is.Zero);
        }

        [Test]
        public void EncumbranceFormattingIsCultureInvariant()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(new EncumbranceValue(1250).ToString(), Is.EqualTo("1.250"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ExactCapacityAcceptsEntireStack()
        {
            ContainerState container = Container("carried", 3000);

            AddItemResult result = InventoryOperations.AddStack(container, Branches, 3);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.Complete));
            Assert.That(result.Accepted, Is.EqualTo(3));
            Assert.That(result.Remaining, Is.Zero);
            Assert.That(container.UsedCapacity.Units, Is.EqualTo(3000));
            Assert.That(container.RemainingCapacity.Units, Is.Zero);
        }

        [Test]
        public void CapacityExceededProducesExplicitPartialResult()
        {
            ContainerState container = Container("carried", 2500);

            AddItemResult result = InventoryOperations.AddStack(container, Branches, 4);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.Partial));
            Assert.That(result.Requested, Is.EqualTo(4));
            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(result.Remaining, Is.EqualTo(2));
            Assert.That(InventoryOperations.Count(container, Branches.Id), Is.EqualTo(2));
        }

        [Test]
        public void RemainingCapacitySmallerThanOneUnitAcceptsNothing()
        {
            ContainerState container = Container("carried", 1999);
            InventoryOperations.AddStack(container, Branches, 1);

            AddItemResult result = InventoryOperations.AddStack(container, Branches, 1);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.DestinationFull));
            Assert.That(result.Accepted, Is.Zero);
            Assert.That(container.UsedCapacity.Units, Is.EqualTo(1000));
        }

        [Test]
        public void CompatibleStackMergesBeforeCreatingNewEntry()
        {
            ContainerState container = Container("carried", 10000);
            InventoryOperations.AddStack(container, Branches, 2);

            InventoryOperations.AddStack(container, Branches, 3);

            Assert.That(container.Entries, Has.Count.EqualTo(1));
            Assert.That(container.Entries[0].StackedItem.Quantity, Is.EqualTo(5));
        }

        [Test]
        public void EntryOrderRemainsStableAcrossMergeAndAppend()
        {
            ContainerState container = Container("carried", 20000);
            InventoryOperations.AddStack(container, Branches, 1);
            InventoryOperations.AddStack(container, Stones, 1);
            InventoryOperations.AddStack(container, Branches, 1);

            Assert.That(
                container.Entries.Select(entry => entry.DefinitionId.Value),
                Is.EqualTo(new[] { "branches", "stones" }));
            Assert.That(container.Entries[0].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void TwoUniqueItemsOfSameDefinitionStayDistinct()
        {
            ContainerState container = Container("carried", 10000);
            var first = Unique(Tool, "tool-01");
            var second = Unique(Tool, "tool-02");

            InventoryOperations.AddUnique(container, Tool, first);
            InventoryOperations.AddUnique(container, Tool, second);

            Assert.That(container.Entries, Has.Count.EqualTo(2));
            Assert.That(container.Entries[0].UniqueItem.InstanceId, Is.EqualTo(first.InstanceId));
            Assert.That(container.Entries[1].UniqueItem.InstanceId, Is.EqualTo(second.InstanceId));
        }

        [Test]
        public void EntriesCannotBeMutatedThroughPublicCollection()
        {
            ContainerState container = Container("carried", 10000);
            InventoryOperations.AddStack(container, Branches, 1);

            var collection = (IList)container.Entries;

            Assert.That(collection.IsReadOnly, Is.True);
            Assert.That(() => collection.Clear(), Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void FullStackRemovalUpdatesCapacityAndRemovesEntry()
        {
            ContainerState container = Container("carried", 10000);
            InventoryOperations.AddStack(container, Branches, 3);

            RemoveItemResult result = InventoryOperations.RemoveStack(container, Branches, 3);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(container.Entries, Is.Empty);
            Assert.That(container.UsedCapacity, Is.EqualTo(EncumbranceValue.Zero));
        }

        [Test]
        public void ImpossibleRemovalIsAtomic()
        {
            ContainerState container = Container("carried", 10000);
            InventoryOperations.AddStack(container, Branches, 2);

            RemoveItemResult result = InventoryOperations.RemoveStack(container, Branches, 3);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.SourceInsufficient));
            Assert.That(result.Removed, Is.Zero);
            Assert.That(InventoryOperations.Count(container, Branches.Id), Is.EqualTo(2));
            Assert.That(container.UsedCapacity.Units, Is.EqualTo(2000));
        }

        [Test]
        public void CompleteTransferConservesQuantity()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            InventoryOperations.AddStack(source, Branches, 5);

            TransferResult result = InventoryOperations.TransferStack(source, destination, Branches, 3);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.Complete));
            Assert.That(result.Transferred, Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(2));
            Assert.That(InventoryOperations.Count(destination, Branches.Id), Is.EqualTo(3));
            Assert.That(Total(source, destination, Branches.Id), Is.EqualTo(5));
        }

        [Test]
        public void PartialTransferOnlyRemovesAcceptedQuantity()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 2500);
            InventoryOperations.AddStack(source, Branches, 5);

            TransferResult result = InventoryOperations.TransferStack(source, destination, Branches, 5);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.Partial));
            Assert.That(result.Transferred, Is.EqualTo(2));
            Assert.That(result.Remaining, Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(destination, Branches.Id), Is.EqualTo(2));
            Assert.That(Total(source, destination, Branches.Id), Is.EqualTo(5));
        }

        [Test]
        public void FullDestinationLeavesSourceUnchanged()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 1000);
            InventoryOperations.AddStack(source, Branches, 4);
            InventoryOperations.AddStack(destination, Branches, 1);

            TransferResult result = InventoryOperations.TransferStack(source, destination, Branches, 4);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.DestinationFull));
            Assert.That(result.Transferred, Is.Zero);
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(4));
            Assert.That(Total(source, destination, Branches.Id), Is.EqualTo(5));
        }

        [Test]
        public void SameContainerTransferIsSafeNoOp()
        {
            ContainerState container = Container("carried", 10000);
            InventoryOperations.AddStack(container, Branches, 4);

            TransferResult result = InventoryOperations.TransferStack(container, container, Branches, 2);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.SameContainer));
            Assert.That(InventoryOperations.Count(container, Branches.Id), Is.EqualTo(4));
        }

        [Test]
        public void UniqueContainerItemKeepsStableContainerIdentifierAcrossTransfer()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            var bagContainerId = new ContainerId("bag-container-01");
            var bag = new UniqueItemState(
                Tool.Id,
                new ItemInstanceId("bag-item-01"),
                bagContainerId);
            InventoryOperations.AddUnique(source, Tool, bag);

            TransferResult result = InventoryOperations.TransferUnique(
                source,
                destination,
                Tool,
                bag.InstanceId);

            Assert.That(result.Outcome, Is.EqualTo(InventoryOperationOutcome.Complete));
            Assert.That(source.Entries, Is.Empty);
            Assert.That(destination.Entries.Single().UniqueItem.ContainedContainerId, Is.EqualTo(bagContainerId));
        }

        [Test]
        public void UniqueRemovalIsAtomicAndDefinitionChecked()
        {
            ContainerState container = Container("carried", 10000);
            UniqueItemState tool = Unique(Tool, "tool-01");
            InventoryOperations.AddUnique(container, Tool, tool);

            RemoveItemResult missing = InventoryOperations.RemoveUnique(
                container,
                Tool,
                new ItemInstanceId("tool-02"));
            RemoveItemResult removed = InventoryOperations.RemoveUnique(container, Tool, tool.InstanceId);

            Assert.That(missing.Succeeded, Is.False);
            Assert.That(removed.Succeeded, Is.True);
            Assert.That(container.Entries, Is.Empty);
        }

        [Test]
        public void ContradictoryStackDefinitionIsRejectedWithoutMutation()
        {
            ContainerState container = Container("carried", 10000);
            InventoryOperations.AddStack(container, Branches, 2);
            var contradictory = new ItemDefinition(
                Branches.Id,
                "Contradictory branches",
                ItemStateKind.Stackable,
                new EncumbranceValue(1));

            Assert.That(
                () => InventoryOperations.AddStack(container, contradictory, 100),
                Throws.ArgumentException);
            Assert.That(InventoryOperations.Count(container, Branches.Id), Is.EqualTo(2));
            Assert.That(container.UsedCapacity.Units, Is.EqualTo(2000));
        }

        [Test]
        public void ContradictoryUniqueDefinitionIsRejectedWithoutMutation()
        {
            ContainerState container = Container("carried", 10000);
            UniqueItemState first = Unique(Tool, "tool-01");
            InventoryOperations.AddUnique(container, Tool, first);
            var contradictory = new ItemDefinition(
                Tool.Id,
                "Contradictory tool",
                ItemStateKind.Unique,
                new EncumbranceValue(1));

            Assert.That(
                () => InventoryOperations.AddUnique(
                    container,
                    contradictory,
                    Unique(contradictory, "tool-02")),
                Throws.ArgumentException);
            Assert.That(container.Entries, Has.Count.EqualTo(1));
            Assert.That(container.UsedCapacity.Units, Is.EqualTo(2000));
        }

        [Test]
        public void ContradictoryTransferDefinitionIsRejectedAtomically()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            InventoryOperations.AddStack(source, Branches, 4);
            var contradictory = new ItemDefinition(
                Branches.Id,
                "Contradictory branches",
                ItemStateKind.Stackable,
                new EncumbranceValue(1));

            Assert.That(
                () => InventoryOperations.TransferStack(source, destination, contradictory, 4),
                Throws.ArgumentException);
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(4));
            Assert.That(destination.Entries, Is.Empty);
            Assert.That(source.UsedCapacity.Units, Is.EqualTo(4000));
        }

        [Test]
        public void PlayerAggregateRejectsEntriesThatContradictCanonicalDefinitions()
        {
            ContainerState main = Container("main", 10000);
            InventoryOperations.AddStack(main, Branches, 2);
            var contradictory = new ItemDefinition(
                Branches.Id,
                "Contradictory branches",
                ItemStateKind.Stackable,
                new EncumbranceValue(500));

            Assert.That(
                () => new PlayerInventoryState(
                    main.Id,
                    new[] { contradictory },
                    new[] { main }),
                Throws.ArgumentException);
            Assert.That(main.UsedCapacity.Units, Is.EqualTo(2000));
        }

        [Test]
        public void CoreInventoryAssemblyHasNoUnityDependency()
        {
            string[] references = typeof(ContainerState).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("UnityEngine"));
            Assert.That(references, Does.Not.Contain("UnityEngine.CoreModule"));
        }

        private static ContainerState Container(string id, long capacity) =>
            new ContainerState(new ContainerId(id), Definition(capacity));

        private static ContainerDefinition Definition(long capacity) =>
            new ContainerDefinition("prototype", "Prototype", new EncumbranceValue(capacity));

        private static UniqueItemState Unique(ItemDefinition definition, string instanceId) =>
            new UniqueItemState(definition.Id, new ItemInstanceId(instanceId));

        private static int Total(
            ContainerState first,
            ContainerState second,
            ItemDefinitionId definitionId) =>
            InventoryOperations.Count(first, definitionId)
            + InventoryOperations.Count(second, definitionId);

        private static void AssertIdentifierContract<T>(T first, T same, T later)
            where T : struct, IEquatable<T>, IComparable<T>
        {
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first.CompareTo(same), Is.Zero);
            Assert.That(first.CompareTo(later), Is.LessThan(0));
            Assert.That(later.CompareTo(first), Is.GreaterThan(0));
        }
    }
}
