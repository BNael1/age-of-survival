using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ChunkMutationLifecycleTests
    {
        private static readonly ItemDefinition Branches = new ItemDefinition(
            new ItemDefinitionId("test-branches"),
            "Branches",
            ItemStateKind.Stackable,
            new EncumbranceValue(1000));

        [Test]
        public void UnmodifiedChunkIsEvictedWithoutSparseRecord()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle);

            Assert.That(lifecycle.Evict(active.Coordinate), Is.True);
            Assert.That(lifecycle.ActiveCount, Is.Zero);
            Assert.That(lifecycle.Store.Count, Is.Zero);

            ActiveChunkState regenerated = lifecycle.Activate(active.Coordinate);
            Assert.That(regenerated.Resources.Count, Is.EqualTo(active.Resources.Count));
            Assert.That(regenerated.Resources, Has.All.Property("Availability").EqualTo(ResourceAvailability.Available));
        }

        [Test]
        public void HarvestedResourceRemainsHarvestedAfterEvictionAndRestoration()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle);
            ResourceState target = active.Resources[0];

            Assert.That(Harvest(target, active.Resources).Succeeded, Is.True);
            lifecycle.Evict(active.Coordinate);
            Assert.That(lifecycle.Store.Count, Is.EqualTo(1));

            ActiveChunkState restored = lifecycle.Activate(active.Coordinate);
            Assert.That(restored.FindResource(target.Id).Availability, Is.EqualTo(ResourceAvailability.Harvested));
            Assert.That(lifecycle.Store.Count, Is.Zero);
        }

        [Test]
        public void PartiallyFilledGroundContainerRestoresExactQuantityWithoutDuplication()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle);
            GroundContainerState ground = CreateGround(active, 3);
            active.AddGroundContainer(ground);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                lifecycle.Evict(active.Coordinate);
                active = lifecycle.Activate(active.Coordinate);
                Assert.That(active.GroundContainers.Count, Is.EqualTo(1));
                Assert.That(
                    InventoryOperations.Count(active.GroundContainers[0].Container, Branches.Id),
                    Is.EqualTo(3));
            }
        }

        [Test]
        public void ActiveAndStoredOwnershipCannotExistAtTheSameTime()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle);
            Assert.That(Harvest(active.Resources[0], active.Resources).Succeeded, Is.True);
            lifecycle.Evict(active.Coordinate);

            Assert.That(lifecycle.Store.Contains(active.Coordinate), Is.True);
            ActiveChunkState restored = lifecycle.Activate(active.Coordinate);
            Assert.That(lifecycle.Store.Contains(active.Coordinate), Is.False);
            Assert.Throws<InvalidOperationException>(() => lifecycle.Activate(restored.Coordinate));
        }

        [Test]
        public void MutationDataUsesCanonicalIdentifierOrder()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle, 2);
            ResourceState first = active.Resources[0];
            ResourceState second = active.Resources[1];
            HarvestedResourceMutation high = first.Id.CompareTo(second.Id) > 0
                ? new HarvestedResourceMutation(first.Id, first.Position)
                : new HarvestedResourceMutation(second.Id, second.Position);
            HarvestedResourceMutation low = first.Id.CompareTo(second.Id) < 0
                ? new HarvestedResourceMutation(first.Id, first.Position)
                : new HarvestedResourceMutation(second.Id, second.Position);

            var state = new ChunkMutationState(
                active.Coordinate,
                active.Baseline.Layout,
                new[] { high, low },
                Array.Empty<GroundContainerMutation>());

            Assert.That(state.HarvestedResources[0].Id, Is.EqualTo(low.Id));
            Assert.That(state.HarvestedResources[1].Id, Is.EqualTo(high.Id));
        }

        [Test]
        public void NegativeCoordinatesBelongToTheirFloorDividedChunk()
        {
            ChunkLayout layout = WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(0)).Generation.ChunkLayout;
            var coordinate = new ChunkCoordinate(-1, -1);
            var mutation = new ChunkMutationState(
                coordinate,
                layout,
                Array.Empty<HarvestedResourceMutation>(),
                new[] { CreateGroundMutation(new WorldPosition(-0.25, -0.5), 2) });

            Assert.That(mutation.Coordinate, Is.EqualTo(coordinate));
            Assert.That(mutation.GroundContainers.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownGeneratedIdentifierIsRejectedAtomically()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out DeterministicWorldPopulationGenerator generator);
            var coordinate = new ChunkCoordinate(0, 0);
            PopulatedChunk baseline = generator.Generate(coordinate);
            var unknown = new HarvestedResourceMutation(
                new ResourceId("unknown-resource"),
                new WorldPosition(0, 0));
            lifecycle.Store.Put(new ChunkMutationState(
                coordinate,
                baseline.Layout,
                new[] { unknown },
                Array.Empty<GroundContainerMutation>()));

            Assert.Throws<ArgumentException>(() => lifecycle.Activate(coordinate));
            Assert.That(lifecycle.ActiveCount, Is.Zero);
            Assert.That(lifecycle.Store.Contains(coordinate), Is.True);
        }

        [Test]
        public void MutationOutsideOwningChunkIsRejected()
        {
            ChunkLayout layout = WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(0)).Generation.ChunkLayout;
            var resource = new HarvestedResourceMutation(
                new ResourceId("outside"),
                new WorldPosition(layout.Width, 0));

            Assert.Throws<ArgumentException>(() => new ChunkMutationState(
                new ChunkCoordinate(0, 0),
                layout,
                new[] { resource },
                Array.Empty<GroundContainerMutation>()));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidStoredQuantityIsRejected(int quantity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StoredItemMutation(
                Branches.Id,
                ItemStateKind.Stackable,
                Branches.UnitEncumbrance,
                quantity));
        }

        [Test]
        public void DifferentInsertionOrdersProduceTheSameCanonicalMutation()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle, 2);
            var a = new HarvestedResourceMutation(active.Resources[0].Id, active.Resources[0].Position);
            var b = new HarvestedResourceMutation(active.Resources[1].Id, active.Resources[1].Position);
            var first = new ChunkMutationState(
                active.Coordinate,
                active.Baseline.Layout,
                new[] { a, b },
                Array.Empty<GroundContainerMutation>());
            var second = new ChunkMutationState(
                active.Coordinate,
                active.Baseline.Layout,
                new[] { b, a },
                Array.Empty<GroundContainerMutation>());

            Assert.That(second.HarvestedResources.Count, Is.EqualTo(first.HarvestedResources.Count));
            for (int index = 0; index < first.HarvestedResources.Count; index++)
            {
                Assert.That(second.HarvestedResources[index], Is.EqualTo(first.HarvestedResources[index]));
            }
        }

        [Test]
        public void EvictionPlanIsCanonicalAndHandlesInt64Edges()
        {
            var loaded = new[]
            {
                new ChunkCoordinate(long.MaxValue, long.MaxValue),
                new ChunkCoordinate(0, 1),
                new ChunkCoordinate(long.MinValue, long.MinValue),
                new ChunkCoordinate(0, 0)
            };

            IReadOnlyList<ChunkCoordinate> plan = ChunkEvictionPlanner.Create(
                loaded,
                new ChunkCoordinate(0, 0),
                1);

            Assert.That(plan, Is.EqualTo(new[]
            {
                new ChunkCoordinate(long.MinValue, long.MinValue),
                new ChunkCoordinate(long.MaxValue, long.MaxValue)
            }));
        }

        [Test]
        public void EmptyGroundContainerIsNotStored()
        {
            ChunkStateLifecycle lifecycle = CreateLifecycle(out _);
            ActiveChunkState active = ActivateChunkWithResources(lifecycle);
            GroundContainerState ground = CreateGround(active, 1);
            InventoryOperations.RemoveStack(ground.Container, Branches, 1);
            active.AddGroundContainer(ground);

            lifecycle.Evict(active.Coordinate);

            Assert.That(lifecycle.Store.Count, Is.Zero);
        }

        private static ChunkStateLifecycle CreateLifecycle(
            out DeterministicWorldPopulationGenerator generator)
        {
            generator = new DeterministicWorldPopulationGenerator(
                WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(0)));
            return new ChunkStateLifecycle(generator);
        }

        private static ActiveChunkState ActivateChunkWithResources(
            ChunkStateLifecycle lifecycle,
            int minimumResources = 1)
        {
            for (long y = -4; y <= 4; y++)
            {
                for (long x = -4; x <= 4; x++)
                {
                    var coordinate = new ChunkCoordinate(x, y);
                    ActiveChunkState active = lifecycle.Activate(coordinate);
                    if (active.Resources.Count >= minimumResources) return active;
                    lifecycle.Evict(coordinate);
                }
            }

            throw new AssertionException("The deterministic test world did not contain enough resources.");
        }

        private static ResourceInteractionResult Harvest(
            ResourceState target,
            IReadOnlyList<ResourceState> resources)
        {
            return ResourceInteraction.Apply(
                default,
                resources,
                target.Position,
                0d);
        }

        private static GroundContainerState CreateGround(ActiveChunkState active, int quantity)
        {
            WorldPosition position = active.Resources.Count > 0
                ? active.Resources[0].Position
                : new WorldPosition(
                    ChunkAddressing.GetWorldOrigin(active.Coordinate, active.Baseline.Layout).X,
                    ChunkAddressing.GetWorldOrigin(active.Coordinate, active.Baseline.Layout).Y);
            GroundContainerMutation mutation = CreateGroundMutation(position, quantity);
            var container = new ContainerState(
                mutation.ContainerId,
                new ContainerDefinition(
                    mutation.DefinitionKey,
                    mutation.DisplayName,
                    mutation.Capacity));
            InventoryOperations.AddStack(container, Branches, quantity);
            return new GroundContainerState(mutation.Id, position, container);
        }

        private static GroundContainerMutation CreateGroundMutation(
            WorldPosition position,
            int quantity)
        {
            return new GroundContainerMutation(
                new GroundContainerId("ground-test"),
                new ContainerId("ground-container-test"),
                position,
                "ground-test",
                "Ground test",
                new EncumbranceValue(10000),
                new[]
                {
                    new StoredItemMutation(
                        Branches.Id,
                        ItemStateKind.Stackable,
                        Branches.UnitEncumbrance,
                        quantity)
                });
        }
    }
}
