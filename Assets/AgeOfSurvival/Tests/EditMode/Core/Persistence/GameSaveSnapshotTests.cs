using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Persistence
{
    public sealed class GameSaveSnapshotTests
    {
        private static readonly ItemDefinition Rations = new ItemDefinition(
            new ItemDefinitionId("rations"),
            "Rations",
            ItemStateKind.Stackable,
            new EncumbranceValue(500));

        [Test]
        public void CaptureBuildsCompleteCanonicalSnapshot()
        {
            WorldPopulationSettings world = CreateWorld();
            PlayerInventoryState inventory = CreateInventory();
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            ChunkMutationState mutation = CreateMutation(
                world.Generation.ChunkLayout,
                new ChunkCoordinate(1, 0),
                "resource-b");
            chunks.Store.Put(mutation);

            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                42,
                new WorldPosition(12.5, -3.25),
                inventory,
                chunks);

            Assert.That(snapshot.World.Generation, Is.EqualTo(world.Generation));
            Assert.That(
                snapshot.World.PopulationProfileId,
                Is.EqualTo(world.Profile.Id));
            Assert.That(
                snapshot.World.PopulationRevision,
                Is.EqualTo(world.Profile.Revision));
            Assert.That(snapshot.FixedTick, Is.EqualTo(42));
            Assert.That(
                snapshot.PlayerPosition,
                Is.EqualTo(new WorldPosition(12.5, -3.25)));
            Assert.That(snapshot.Inventory.MainContainerId, Is.EqualTo(
                inventory.MainContainer.Id));
            Assert.That(snapshot.ChunkMutations.Count, Is.EqualTo(1));
            Assert.That(snapshot.ChunkMutations[0], Is.SameAs(mutation));
        }

        [Test]
        public void CaptureDoesNotConsumeStoredMutations()
        {
            WorldPopulationSettings world = CreateWorld();
            PlayerInventoryState inventory = CreateInventory();
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            ChunkMutationState mutation = CreateMutation(
                world.Generation.ChunkLayout,
                new ChunkCoordinate(0, 0),
                "resource-a");
            chunks.Store.Put(mutation);

            GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                inventory,
                chunks);

            Assert.That(chunks.Store.Count, Is.EqualTo(1));
            Assert.That(
                chunks.Store.TryPeek(
                    mutation.Coordinate,
                    out ChunkMutationState stored),
                Is.True);
            Assert.That(stored, Is.SameAs(mutation));
        }

        [Test]
        public void EmptyLifecycleProducesEmptyCanonicalList()
        {
            WorldPopulationSettings world = CreateWorld();

            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                CreateInventory(),
                CreateLifecycle(world));

            Assert.That(snapshot.ChunkMutations, Is.Empty);
        }

        [Test]
        public void EquivalentInsertionOrdersProduceSameMutationOrder()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkLayout layout = world.Generation.ChunkLayout;
            ChunkMutationState first = CreateMutation(
                layout,
                new ChunkCoordinate(0, 0),
                "resource-a");
            ChunkMutationState second = CreateMutation(
                layout,
                new ChunkCoordinate(1, 0),
                "resource-b");
            ChunkStateLifecycle forward = CreateLifecycle(world);
            forward.Store.Put(first);
            forward.Store.Put(second);
            ChunkStateLifecycle reverse = CreateLifecycle(world);
            reverse.Store.Put(second);
            reverse.Store.Put(first);

            GameSaveSnapshot forwardSnapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                CreateInventory(),
                forward);
            GameSaveSnapshot reverseSnapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                CreateInventory(),
                reverse);

            Assert.That(forwardSnapshot.ChunkMutations.Count, Is.EqualTo(2));
            Assert.That(reverseSnapshot.ChunkMutations.Count, Is.EqualTo(2));
            for (int index = 0; index < 2; index++)
            {
                Assert.That(
                    reverseSnapshot.ChunkMutations[index].Coordinate,
                    Is.EqualTo(forwardSnapshot.ChunkMutations[index].Coordinate));
            }

            Assert.That(
                forwardSnapshot.ChunkMutations[0].Coordinate,
                Is.EqualTo(new ChunkCoordinate(0, 0)));
            Assert.That(
                forwardSnapshot.ChunkMutations[1].Coordinate,
                Is.EqualTo(new ChunkCoordinate(1, 0)));
        }

        [Test]
        public void CapturedMutationListDoesNotFollowLaterStoredReplacement()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkLayout layout = world.Generation.ChunkLayout;
            ChunkCoordinate coordinate = new ChunkCoordinate(0, 0);
            ChunkMutationState original = CreateMutation(
                layout,
                coordinate,
                "resource-a");
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            chunks.Store.Put(original);
            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                CreateInventory(),
                chunks);

            chunks.Store.Put(CreateMutation(layout, coordinate, "resource-b"));

            Assert.That(snapshot.ChunkMutations.Count, Is.EqualTo(1));
            Assert.That(snapshot.ChunkMutations[0], Is.SameAs(original));
            Assert.That(
                snapshot.ChunkMutations[0].HarvestedResources[0].Id,
                Is.EqualTo(new ResourceId("resource-a")));
        }

        [Test]
        public void CaptureIncludesModifiedActiveChunkWithoutEviction()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            ActiveChunkState active = chunks.Activate(
                new ChunkCoordinate(0, 0));
            AddGroundMutation(active, "ground-active");

            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                7,
                new WorldPosition(1, 2),
                CreateInventory(),
                chunks);

            Assert.That(snapshot.ChunkMutations.Count, Is.EqualTo(1));
            Assert.That(
                snapshot.ChunkMutations[0].Coordinate,
                Is.EqualTo(active.Coordinate));
            Assert.That(
                snapshot.ChunkMutations[0].GroundContainers.Count,
                Is.EqualTo(1));
            Assert.That(chunks.ActiveCount, Is.EqualTo(1));
            Assert.That(chunks.Store.Count, Is.EqualTo(0));
            Assert.That(
                chunks.TryGetActive(active.Coordinate, out ActiveChunkState retained),
                Is.True);
            Assert.That(retained, Is.SameAs(active));
        }

        [Test]
        public void CaptureCombinesStoredAndActiveMutationsInCanonicalOrder()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            ChunkMutationState stored = CreateMutation(
                world.Generation.ChunkLayout,
                new ChunkCoordinate(1, 0),
                "resource-stored");
            chunks.Store.Put(stored);

            ActiveChunkState active = chunks.Activate(
                new ChunkCoordinate(0, 0));
            AddGroundMutation(active, "ground-active");

            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                CreateInventory(),
                chunks);

            Assert.That(snapshot.ChunkMutations.Count, Is.EqualTo(2));
            Assert.That(
                snapshot.ChunkMutations[0].Coordinate,
                Is.EqualTo(new ChunkCoordinate(0, 0)));
            Assert.That(
                snapshot.ChunkMutations[1].Coordinate,
                Is.EqualTo(new ChunkCoordinate(1, 0)));
            Assert.That(chunks.ActiveCount, Is.EqualTo(1));
            Assert.That(chunks.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void CaptureOmitsUnmodifiedActiveChunkWithoutEvictingIt()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            ChunkCoordinate coordinate = new ChunkCoordinate(0, 0);
            chunks.Activate(coordinate);

            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(0, 0),
                CreateInventory(),
                chunks);

            Assert.That(snapshot.ChunkMutations, Is.Empty);
            Assert.That(chunks.ActiveCount, Is.EqualTo(1));
            Assert.That(chunks.Store.Count, Is.EqualTo(0));
            Assert.That(chunks.TryGetActive(coordinate, out _), Is.True);
        }

        [Test]
        public void CaptureRejectsCoordinateOwnedByStoreAndActiveChunk()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkStateLifecycle chunks = CreateLifecycle(world);
            ChunkCoordinate coordinate = new ChunkCoordinate(0, 0);
            ActiveChunkState active = chunks.Activate(coordinate);
            AddGroundMutation(active, "ground-active");
            chunks.Store.Put(CreateMutation(
                world.Generation.ChunkLayout,
                coordinate,
                "resource-stored"));

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    GameSaveSnapshotCapture.Capture(
                        world,
                        0,
                        new WorldPosition(0, 0),
                        CreateInventory(),
                        chunks));

            Assert.That(
                exception.Message,
                Does.Contain("active and stored"));
            Assert.That(chunks.ActiveCount, Is.EqualTo(1));
            Assert.That(chunks.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorCopiesMutationCollection()
        {
            WorldPopulationSettings world = CreateWorld();
            var source = new List<ChunkMutationState>
            {
                CreateMutation(
                    world.Generation.ChunkLayout,
                    new ChunkCoordinate(0, 0),
                    "resource-a")
            };

            var snapshot = new GameSaveSnapshot(
                CreateIdentity(world),
                0,
                new WorldPosition(0, 0),
                CreateInventory().CaptureSnapshot(),
                source);
            source.Clear();

            Assert.That(snapshot.ChunkMutations.Count, Is.EqualTo(1));
        }

        [Test]
        public void CaptureNormalizesNegativeZeroPlayerPosition()
        {
            WorldPopulationSettings world = CreateWorld();

            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                0,
                new WorldPosition(-0.0d, -0.0d),
                CreateInventory(),
                CreateLifecycle(world));

            Assert.That(
                BitConverter.DoubleToInt64Bits(snapshot.PlayerPosition.X),
                Is.EqualTo(0L));
            Assert.That(
                BitConverter.DoubleToInt64Bits(snapshot.PlayerPosition.Y),
                Is.EqualTo(0L));
        }

        [Test]
        public void WorldIdentityRejectsDefaultProfileIdentifier()
        {
            WorldPopulationSettings world = CreateWorld();

            GameSaveSnapshotException exception =
                Assert.Throws<GameSaveSnapshotException>(() =>
                    new WorldIdentitySnapshot(
                        world.Generation,
                        default,
                        1));

            Assert.That(
                exception.Violation,
                Is.EqualTo(
                    GameSaveSnapshotViolation.InvalidPopulationProfileIdentifier));
        }

        [Test]
        public void WorldIdentityRejectsNonPositiveRevision()
        {
            WorldPopulationSettings world = CreateWorld();

            GameSaveSnapshotException exception =
                Assert.Throws<GameSaveSnapshotException>(() =>
                    new WorldIdentitySnapshot(
                        world.Generation,
                        world.Profile.Id,
                        0));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveSnapshotViolation.InvalidPopulationRevision));
        }

        [Test]
        public void ConstructorRejectsNegativeFixedTick()
        {
            WorldPopulationSettings world = CreateWorld();

            GameSaveSnapshotException exception =
                Assert.Throws<GameSaveSnapshotException>(() =>
                    new GameSaveSnapshot(
                        CreateIdentity(world),
                        -1,
                        new WorldPosition(0, 0),
                        CreateInventory().CaptureSnapshot(),
                        Array.Empty<ChunkMutationState>()));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveSnapshotViolation.NegativeFixedTick));
        }

        [Test]
        public void ConstructorRejectsEmptyChunkMutation()
        {
            WorldPopulationSettings world = CreateWorld();
            var empty = new ChunkMutationState(
                new ChunkCoordinate(0, 0),
                world.Generation.ChunkLayout,
                Array.Empty<HarvestedResourceMutation>(),
                Array.Empty<GroundContainerMutation>());

            GameSaveSnapshotException exception =
                Assert.Throws<GameSaveSnapshotException>(() =>
                    new GameSaveSnapshot(
                        CreateIdentity(world),
                        0,
                        new WorldPosition(0, 0),
                        CreateInventory().CaptureSnapshot(),
                        new[] { empty }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveSnapshotViolation.EmptyChunkMutation));
        }

        [Test]
        public void ConstructorRejectsMutationLayoutMismatch()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkMutationState mutation = CreateMutation(
                new ChunkLayout(16, 16),
                new ChunkCoordinate(0, 0),
                "resource-a");

            GameSaveSnapshotException exception =
                Assert.Throws<GameSaveSnapshotException>(() =>
                    new GameSaveSnapshot(
                        CreateIdentity(world),
                        0,
                        new WorldPosition(0, 0),
                        CreateInventory().CaptureSnapshot(),
                        new[] { mutation }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveSnapshotViolation.MutationLayoutMismatch));
        }

        [Test]
        public void ConstructorRejectsDuplicateChunkCoordinates()
        {
            WorldPopulationSettings world = CreateWorld();
            ChunkLayout layout = world.Generation.ChunkLayout;
            ChunkCoordinate coordinate = new ChunkCoordinate(0, 0);
            ChunkMutationState first = CreateMutation(
                layout,
                coordinate,
                "resource-a");
            ChunkMutationState second = CreateMutation(
                layout,
                coordinate,
                "resource-b");

            GameSaveSnapshotException exception =
                Assert.Throws<GameSaveSnapshotException>(() =>
                    new GameSaveSnapshot(
                        CreateIdentity(world),
                        0,
                        new WorldPosition(0, 0),
                        CreateInventory().CaptureSnapshot(),
                        new[] { first, second }));

            Assert.That(
                exception.Violation,
                Is.EqualTo(GameSaveSnapshotViolation.DuplicateChunkCoordinate));
        }

        private static WorldPopulationSettings CreateWorld()
        {
            return WorldPopulationDefaults.CreateTemperatePrototypeV1(
                new WorldSeed(0x0123456789ABCDEFUL));
        }

        private static WorldIdentitySnapshot CreateIdentity(
            WorldPopulationSettings world)
        {
            return new WorldIdentitySnapshot(
                world.Generation,
                world.Profile.Id,
                world.Profile.Revision);
        }

        private static PlayerInventoryState CreateInventory()
        {
            var main = new ContainerState(
                new ContainerId("player-main"),
                new ContainerDefinition(
                    "player-main",
                    "Player",
                    new EncumbranceValue(30000)));
            InventoryOperations.AddStack(main, Rations, 2);
            return new PlayerInventoryState(
                main.Id,
                new[] { Rations },
                new[] { main });
        }

        private static ChunkStateLifecycle CreateLifecycle(
            WorldPopulationSettings world)
        {
            return new ChunkStateLifecycle(
                new DeterministicWorldPopulationGenerator(world));
        }

        private static void AddGroundMutation(
            ActiveChunkState active,
            string groundId)
        {
            WorldCellCoordinate origin = ChunkAddressing.GetWorldOrigin(
                active.Coordinate,
                active.Baseline.Layout);
            var container = new ContainerState(
                new ContainerId($"{groundId}-container"),
                new ContainerDefinition(
                    "ground-cache",
                    "Ground cache",
                    new EncumbranceValue(30000)));
            InventoryOperations.AddStack(container, Rations, 1);
            active.AddGroundContainer(new GroundContainerState(
                new GroundContainerId(groundId),
                new WorldPosition(origin.X, origin.Y),
                container));
        }

        private static ChunkMutationState CreateMutation(
            ChunkLayout layout,
            ChunkCoordinate coordinate,
            string resourceId)
        {
            WorldCellCoordinate origin =
                ChunkAddressing.GetWorldOrigin(coordinate, layout);
            return new ChunkMutationState(
                coordinate,
                layout,
                new[]
                {
                    new HarvestedResourceMutation(
                        new ResourceId(resourceId),
                        new WorldPosition(origin.X, origin.Y))
                },
                Array.Empty<GroundContainerMutation>());
        }
    }
}
