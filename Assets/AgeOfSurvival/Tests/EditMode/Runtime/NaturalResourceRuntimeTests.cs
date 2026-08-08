using System;
using System.IO;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Persistence;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class NaturalResourceRuntimeTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "age-of-survival-natural-resource-"
                    + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(
                    _temporaryDirectory,
                    true);
            }

            InventoryPrototypeSessionProvider.ResetForNewGame();
        }

        [Test]
        public void GeneratedStatesCarryDefinitionIdentityFromPlacementKind()
        {
            var root = new GameObject(
                "Natural resource generated-state test");

            try
            {
                DebugIsometricWorld world =
                    root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(
                    new WorldSeed(0UL),
                    new ChunkCoordinate(0L, 0L));
                world.Rebuild();

                var states =
                    world.CreateGeneratedResourceStates();
                Assert.That(
                    states,
                    Has.Count.EqualTo(
                        world.PopulationChunk.Resources.Count));

                for (int index = 0;
                     index < states.Count;
                     index++)
                {
                    Assert.That(
                        states[index].DefinitionId,
                        Is.EqualTo(
                            ResourcePrototypeCatalog
                                .DefinitionIdFor(
                                    world.PopulationChunk
                                        .Resources[index].Kind)));
                }

                Assert.That(
                    world.PopulationChunk
                        .Settings.Profile.Revision,
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeadwoodHarvestProducesWoodAndBranchesAndTransfersEither()
        {
            var deadwood = new ResourceState(
                new ResourceId("deadwood-runtime"),
                ResourceDefinitionIds.Deadwood,
                new WorldPosition(0.0, 0.0));
            var session =
                new InventoryPrototypeSession(
                    new[] { deadwood });

            ResourceYieldResult result =
                session.HarvestAndStartTransfer(
                    new WorldPosition(0.0, 0.0),
                    1.5,
                    session.CurrentTick);

            Assert.That(result.Succeeded, Is.True);
            GroundContainerState ground = result.Ground;
            Assert.That(
                InventoryOperations.Count(
                    ground.Container,
                    InventoryPrototypeCatalog.Wood.Id),
                Is.EqualTo(2));
            Assert.That(
                InventoryOperations.Count(
                    ground.Container,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(2));

            session.AdvanceTransfer(
                100,
                new WorldPosition(0.0, 0.0),
                false);
            Assert.That(
                InventoryOperations.Count(
                    ground.Container,
                    InventoryPrototypeCatalog.Wood.Id),
                Is.EqualTo(1));

            TransferActionResult branches =
                session.StartGroundTransfer(
                    ground,
                    InventoryPrototypeCatalog.Branches.Id,
                    2,
                    100);
            Assert.That(branches.Succeeded, Is.True);

            session.AdvanceTransfer(
                200,
                new WorldPosition(0.0, 0.0),
                false);
            Assert.That(
                InventoryOperations.Count(
                    ground.Container,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(1));
        }

        [Test]
        public void GeneratedResourceDefinitionDriftIsRejected()
        {
            var id =
                new ResourceId("stable-generated-resource");
            var position = new WorldPosition(4.0, 5.0);
            var session =
                new InventoryPrototypeSession(
                    new[]
                    {
                        new ResourceState(
                            id,
                            ResourceDefinitionIds.Shrub,
                            position)
                    });

            Assert.Throws<InvalidOperationException>(() =>
                session.SynchronizeGeneratedResources(
                    new[]
                    {
                        new ResourceState(
                            id,
                            ResourceDefinitionIds.Tree,
                            position)
                    }));
        }

        [Test]
        public void PrototypeSaveRoundTripsPopulationRevisionTwoWithoutFormatBump()
        {
            WorldPopulationSettings world =
                WorldPopulationDefaults
                    .CreateTemperatePrototypeV2(
                        new WorldSeed(42UL));
            var session =
                new InventoryPrototypeSession(
                    Array.Empty<ResourceState>(),
                    world);
            var service =
                new PrototypeSaveService(
                    _temporaryDirectory);
            var slot = new SaveSlotId(1);

            service.Save(slot, session, 10d);
            CoordinatedGameLoadResult loaded =
                service.Load(slot, 0d, out _);

            Assert.That(
                loaded.State.World.Profile.Revision,
                Is.EqualTo(2));
            Assert.That(
                loaded.State.World.Generation.Seed,
                Is.EqualTo(new WorldSeed(42UL)));
            Assert.That(
                GameSaveBinaryCodec.CurrentVersion,
                Is.EqualTo(3));
        }

        [Test]
        public void PrototypeVisualCatalogContainsDistinctNaturalResourceSprites()
        {
            var root = new GameObject(
                "Natural resource visual catalog test");

            try
            {
                root.AddComponent<DebugIsometricWorld>();
                DebugResourceInteraction adapter =
                    root.AddComponent<DebugResourceInteraction>();
                adapter.Rebuild();

                Assert.That(
                    PrototypeVisualAssets
                        .AllRequiredTexturesExist(),
                    Is.True);
                Assert.That(
                    adapter.TryGetBodySprite(
                        ResourceDefinitionIds.Shrub,
                        out Sprite shrub),
                    Is.True);
                Assert.That(
                    adapter.TryGetBodySprite(
                        ResourceDefinitionIds.LooseStone,
                        out Sprite stone),
                    Is.True);
                Assert.That(
                    adapter.TryGetBodySprite(
                        ResourceDefinitionIds.Deadwood,
                        out Sprite deadwood),
                    Is.True);
                Assert.That(
                    adapter.TryGetBodySprite(
                        ResourceDefinitionIds.Tree,
                        out Sprite tree),
                    Is.True);

                Assert.That(stone, Is.Not.SameAs(shrub));
                Assert.That(deadwood, Is.Not.SameAs(shrub));
                Assert.That(tree, Is.Not.SameAs(shrub));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
