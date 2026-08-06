using System;
using System.Collections.Generic;
using System.IO;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests.Persistence
{
    public sealed class GameSaveCoordinatorTests
    {
        private static readonly ItemDefinition Rations = new ItemDefinition(
            new ItemDefinitionId("rations"),
            "Rations",
            ItemStateKind.Stackable,
            new EncumbranceValue(500));

        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "aos-coordinator-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        [Test]
        public void CoordinatorSavesAndLoadsNewCoreState()
        {
            WorldPopulationSettings world = CreateWorld();
            var resolver = new Resolver(world);
            var coordinator = new GameSaveCoordinator(
                new AtomicGameSaveStorage(_temporaryDirectory),
                resolver,
                resolver);

            coordinator.Save(
                "prototype",
                world,
                33,
                new WorldPosition(4, 5),
                CreateHealth(33),
                CreateInventory(),
                new ChunkStateLifecycle(
                    new DeterministicWorldPopulationGenerator(world)));
            CoordinatedGameLoadResult loaded = coordinator.Load("prototype");

            Assert.That(loaded.Source, Is.EqualTo(GameSaveLoadSource.Primary));
            Assert.That(loaded.State.FixedTick, Is.EqualTo(33));
            Assert.That(loaded.State.Health.CurrentHealth, Is.EqualTo(65));
            Assert.That(loaded.State.Health.CurrentTick, Is.EqualTo(33L));
            Assert.That(
                loaded.State.Health.NextRegenerationTick,
                Is.EqualTo(543L));
            Assert.That(
                loaded.State.PlayerPosition,
                Is.EqualTo(new WorldPosition(4, 5)));
            Assert.That(loaded.State.Inventory.Containers.Count, Is.EqualTo(1));
        }

        [Test]
        public void CoordinatorReportsBackupRecovery()
        {
            WorldPopulationSettings world = CreateWorld();
            var resolver = new Resolver(world);
            var storage = new AtomicGameSaveStorage(_temporaryDirectory);
            var coordinator = new GameSaveCoordinator(
                storage,
                resolver,
                resolver);

            coordinator.Save(
                "prototype",
                world,
                10,
                new WorldPosition(0, 0),
                CreateHealth(10),
                CreateInventory(),
                new ChunkStateLifecycle(
                    new DeterministicWorldPopulationGenerator(world)));
            coordinator.Save(
                "prototype",
                world,
                20,
                new WorldPosition(0, 0),
                CreateHealth(20),
                CreateInventory(),
                new ChunkStateLifecycle(
                    new DeterministicWorldPopulationGenerator(world)));
            File.WriteAllBytes(
                storage.GetPrimaryPath("prototype"),
                new byte[] { 1, 2, 3 });

            CoordinatedGameLoadResult loaded = coordinator.Load("prototype");

            Assert.That(loaded.Source, Is.EqualTo(GameSaveLoadSource.Backup));
            Assert.That(loaded.State.FixedTick, Is.EqualTo(10));
        }

        [Test]
        public void UnitySavePathProviderUsesPersistentDataPath()
        {
            Assert.That(
                UnitySavePathProvider.GetRootDirectory(),
                Is.EqualTo(Path.Combine(
                    Application.persistentDataPath,
                    "Saves")));
        }

        private static WorldPopulationSettings CreateWorld()
        {
            return WorldPopulationDefaults.CreateTemperatePrototypeV1(
                new WorldSeed(0x0123456789ABCDEFUL));
        }

        private static PlayerHealthState CreateHealth(long tick)
        {
            return new PlayerHealthState(
                100,
                65,
                tick,
                checked(tick + 510L));
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

        private sealed class Resolver
            : IWorldPopulationSettingsResolver,
              IInventoryDefinitionResolver
        {
            private readonly WorldPopulationSettings _world;
            private readonly Dictionary<ItemDefinitionId, ItemDefinition> _items =
                new Dictionary<ItemDefinitionId, ItemDefinition>
                {
                    { Rations.Id, Rations }
                };

            public Resolver(WorldPopulationSettings world)
            {
                _world = world;
            }

            public bool TryResolve(
                WorldIdentitySnapshot identity,
                out WorldPopulationSettings settings)
            {
                settings = _world;
                return true;
            }

            public bool TryResolveItemDefinition(
                InventoryDefinitionSnapshot saved,
                out ItemDefinition definition)
            {
                return _items.TryGetValue(saved.Id, out definition);
            }

            public bool TryResolveContainerDefinition(
                InventoryContainerSnapshot saved,
                out ContainerDefinition definition)
            {
                definition = new ContainerDefinition(
                    saved.DefinitionKey,
                    saved.DefinitionKey,
                    saved.Capacity);
                return true;
            }
        }
    }
}
