using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Inventory
{
    public sealed class TimedResourceTransferTests
    {
        private static readonly ItemDefinition Branches = new ItemDefinition(
            new ItemDefinitionId("branches"), "Branches", ItemStateKind.Stackable, new EncumbranceValue(500));
        private static readonly WorldPosition Origin = new WorldPosition(4.5, 4.5);
        private static readonly TransferTimingDefinition Timing = new TransferTimingDefinition(15, 30, 1000, 15);

        [Test]
        public void HarvestCreatesStableGroundYieldAtResourcePosition()
        {
            var resource = new ResourceState(new ResourceId("tree-01"), new WorldPosition(5, 4.5));
            var ground = new List<GroundContainerState>();
            ResourceYieldResult result = ResourceYieldOperations.HarvestToGround(
                new[] { resource }, ground, Origin, 1.5, Branches, 6, new EncumbranceValue(10000));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Ground.Id.ToString(), Is.EqualTo("ground-tree-01"));
            Assert.That(result.Ground.Position, Is.EqualTo(resource.Position));
            Assert.That(InventoryOperations.Count(result.Ground.Container, Branches.Id), Is.EqualTo(6));
            Assert.That(resource.Availability, Is.EqualTo(ResourceAvailability.Harvested));
        }

        [Test]
        public void DistinctResourcesCreateDistinctGroundIdentifiers()
        {
            var resources = new[]
            {
                new ResourceState(new ResourceId("tree-a"), Origin),
                new ResourceState(new ResourceId("tree-b"), new WorldPosition(4.6, 4.5))
            };
            var ground = new List<GroundContainerState>();
            ResourceYieldOperations.HarvestToGround(resources, ground, Origin, 1.5, Branches, 1, new EncumbranceValue(10000));
            ResourceYieldOperations.HarvestToGround(resources, ground, Origin, 1.5, Branches, 1, new EncumbranceValue(10000));
            Assert.That(ground[0].Id, Is.Not.EqualTo(ground[1].Id));
            Assert.That(ground[0].Container.Id, Is.Not.EqualTo(ground[1].Container.Id));
        }

        [Test]
        public void InsufficientGroundCapacityIsRejectedBeforeHarvest()
        {
            var resource = new ResourceState(new ResourceId("tree-01"), Origin);
            var ground = new List<GroundContainerState>();
            Assert.That(() => ResourceYieldOperations.HarvestToGround(
                new[] { resource }, ground, Origin, 1.5, Branches, 6, new EncumbranceValue(2999)),
                Throws.ArgumentException);
            Assert.That(resource.Availability, Is.EqualTo(ResourceAvailability.Available));
            Assert.That(ground, Is.Empty);
        }

        [Test]
        public void DefaultGroundAndTransferIdentifiersAreSafe()
        {
            Assert.That(default(GroundContainerId).IsValid, Is.False);
            Assert.That(default(TransferActionId).IsValid, Is.False);
            Assert.That(() => new GroundContainerState(default, Origin, Container("x", 1000)), Throws.ArgumentException);
        }

        [Test]
        public void StartPlansOnlyWhatFitsAndUsesIntegerTickDuration()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 1500);
            InventoryOperations.AddStack(source, Branches, 6);
            TransferActionResult result = Start(source, destination, 6);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Action.PlannedQuantity, Is.EqualTo(3));
            Assert.That(result.Action.DurationTicks, Is.EqualTo(60));
        }

        [Test]
        public void ProgressIsDerivedFromTicksAndClamped()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            InventoryOperations.AddStack(source, Branches, 1);
            TransferActionState action = Start(source, destination, 1, 10).Action;
            Assert.That(action.ProgressAt(10), Is.Zero);
            Assert.That(action.ProgressAt(25), Is.EqualTo(0.5).Within(0.0001));
            Assert.That(action.ProgressAt(1000), Is.EqualTo(1.0));
        }

        [Test]
        public void CompletionTransfersOnceAndConservesTotal()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            InventoryOperations.AddStack(source, Branches, 4);
            TransferActionState action = Start(source, destination, 4).Action;
            TransferActionResult first = AdvanceAtEnd(action, source, destination);
            TransferActionResult second = AdvanceAtEnd(action, source, destination);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Reason, Is.EqualTo(TransferActionReason.AlreadyFinalized));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.Zero);
            Assert.That(InventoryOperations.Count(destination, Branches.Id), Is.EqualTo(4));
        }

        [TestCase(true, 0.0, TransferActionReason.PlayerMoved)]
        [TestCase(false, 2.0, TransferActionReason.OutOfRange)]
        public void InterruptionLeavesSourceUntouched(bool moved, double playerXOffset, TransferActionReason expected)
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            InventoryOperations.AddStack(source, Branches, 4);
            TransferActionState action = Start(source, destination, 4).Action;
            TransferActionResult result = TransferActionOperations.Advance(action, 1,
                new WorldPosition(Origin.X + playerXOffset, Origin.Y), moved, source, destination, Branches);
            Assert.That(result.Reason, Is.EqualTo(expected));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(4));
            Assert.That(InventoryOperations.Count(destination, Branches.Id), Is.Zero);
        }

        [Test]
        public void DestinationBecomingFullFailsWithoutLoss()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 2000);
            var filler = new ItemDefinition(new ItemDefinitionId("filler"), "Filler", ItemStateKind.Stackable, new EncumbranceValue(1000));
            InventoryOperations.AddStack(source, Branches, 4);
            TransferActionState action = Start(source, destination, 4).Action;
            InventoryOperations.AddStack(destination, filler, 2);
            TransferActionResult result = AdvanceAtEnd(action, source, destination);
            Assert.That(result.Reason, Is.EqualTo(TransferActionReason.DestinationFull));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(4));
        }

        [Test]
        public void CapacityChangeCanProduceConservativePartialCompletion()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 2000);
            var filler = new ItemDefinition(new ItemDefinitionId("filler"), "Filler", ItemStateKind.Stackable, new EncumbranceValue(1500));
            InventoryOperations.AddStack(source, Branches, 4);
            TransferActionState action = Start(source, destination, 4).Action;
            InventoryOperations.AddStack(destination, filler, 1);
            AdvanceAtEnd(action, source, destination);
            Assert.That(action.TransferredQuantity, Is.EqualTo(1));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(destination, Branches.Id), Is.EqualTo(1));
        }

        [Test]
        public void SourceChangeIsRevalidatedWithoutDuplication()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 10000);
            InventoryOperations.AddStack(source, Branches, 4);
            TransferActionState action = Start(source, destination, 4).Action;
            InventoryOperations.RemoveStack(source, Branches, 3);
            AdvanceAtEnd(action, source, destination);
            Assert.That(action.TransferredQuantity, Is.EqualTo(1));
            Assert.That(InventoryOperations.Count(source, Branches.Id), Is.Zero);
            Assert.That(InventoryOperations.Count(destination, Branches.Id), Is.EqualTo(1));
        }

        [Test]
        public void FullDestinationIsRejectedAtStart()
        {
            ContainerState source = Container("source", 10000);
            ContainerState destination = Container("destination", 0);
            InventoryOperations.AddStack(source, Branches, 1);
            Assert.That(Start(source, destination, 1).Reason, Is.EqualTo(TransferActionReason.DestinationFull));
        }

        private static TransferActionResult Start(ContainerState source, ContainerState destination, int quantity, long tick = 0)
            => TransferActionOperations.Start(new TransferActionId("action-1"), source, destination, Branches,
                quantity, tick, Origin, 1.5, Timing);

        private static TransferActionResult AdvanceAtEnd(TransferActionState action, ContainerState source, ContainerState destination)
            => TransferActionOperations.Advance(action, action.StartTick + action.DurationTicks, Origin,
                false, source, destination, Branches);

        private static ContainerState Container(string id, long capacity)
            => new ContainerState(new ContainerId(id), new ContainerDefinition(id, id, new EncumbranceValue(capacity)));
    }
}
