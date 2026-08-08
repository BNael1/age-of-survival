using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class NaturalResourceCoreTests
    {
        [Test]
        public void LegacyResourceStateConstructorRemainsShrubCompatible()
        {
            var state = new ResourceState(
                new ResourceId("legacy-shrub"),
                new WorldPosition(1.0, 2.0));

            Assert.That(
                state.DefinitionId,
                Is.EqualTo(ResourceDefinitionIds.Shrub));
        }

        [Test]
        public void ResourceDefinitionRejectsDuplicateYieldDefinitions()
        {
            ItemDefinition branches = Stack("branches-test", 500);

            Assert.Throws<ArgumentException>(() =>
                new ResourceDefinition(
                    ResourceDefinitionIds.Tree,
                    "Tree",
                    new[]
                    {
                        new ResourceYieldDefinition(branches, 1),
                        new ResourceYieldDefinition(branches, 2)
                    }));
        }

        [Test]
        public void MultiYieldHarvestCreatesOneAtomicGroundPile()
        {
            ItemDefinition branches = Stack("branches-test", 500);
            ItemDefinition wood = Stack("wood-test", 1000);
            var tree = new ResourceDefinition(
                ResourceDefinitionIds.Tree,
                "Tree",
                new[]
                {
                    new ResourceYieldDefinition(wood, 6),
                    new ResourceYieldDefinition(branches, 3)
                });
            var catalog =
                new ResourceDefinitionCatalog(new[] { tree });
            var resource = new ResourceState(
                new ResourceId("tree-01"),
                tree.Id,
                new WorldPosition(0.0, 0.0));
            var resources = new List<ResourceState> { resource };
            var ground = new List<GroundContainerState>();

            ResourceYieldResult result =
                ResourceYieldOperations.HarvestToGround(
                    resources,
                    ground,
                    new WorldPosition(0.0, 0.0),
                    1.5,
                    catalog,
                    new EncumbranceValue(100000));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Definition, Is.SameAs(tree));
            Assert.That(result.Produced, Is.EqualTo(9));
            Assert.That(
                resource.Availability,
                Is.EqualTo(ResourceAvailability.Harvested));
            Assert.That(ground, Has.Count.EqualTo(1));
            Assert.That(
                InventoryOperations.Count(
                    ground[0].Container,
                    wood.Id),
                Is.EqualTo(6));
            Assert.That(
                InventoryOperations.Count(
                    ground[0].Container,
                    branches.Id),
                Is.EqualTo(3));
        }

        [Test]
        public void InsufficientGroundCapacityCannotPartiallyHarvest()
        {
            ItemDefinition wood = Stack("wood-test", 1000);
            var tree = new ResourceDefinition(
                ResourceDefinitionIds.Tree,
                "Tree",
                new[]
                {
                    new ResourceYieldDefinition(wood, 6)
                });
            var catalog =
                new ResourceDefinitionCatalog(new[] { tree });
            var resource = new ResourceState(
                new ResourceId("tree-capacity"),
                tree.Id,
                new WorldPosition(0.0, 0.0));
            var resources = new List<ResourceState> { resource };
            var ground = new List<GroundContainerState>();

            Assert.Throws<ArgumentException>(() =>
                ResourceYieldOperations.HarvestToGround(
                    resources,
                    ground,
                    new WorldPosition(0.0, 0.0),
                    1.5,
                    catalog,
                    new EncumbranceValue(5000)));

            Assert.That(
                resource.Availability,
                Is.EqualTo(ResourceAvailability.Available));
            Assert.That(ground, Is.Empty);
        }

        [Test]
        public void PopulationV1GoldenResourceIdentityStaysShrubOnly()
        {
            var generator =
                new DeterministicWorldPopulationGenerator(
                    WorldPopulationDefaults
                        .CreateTemperatePrototypeV1(
                            new WorldSeed(0UL)));
            PopulatedChunk chunk =
                generator.Generate(
                    new ChunkCoordinate(0L, 0L));

            Assert.That(chunk.Resources, Has.Count.EqualTo(41));
            Assert.That(
                chunk.Resources.All(
                    resource =>
                        resource.Kind
                            == GeneratedResourceKind.Shrub),
                Is.True);
            StringAssert.StartsWith(
                "generated:0000000000000000:2:temperate-prototype:1:0:",
                chunk.Resources[0].Id.Value);
        }

        [Test]
        public void PopulationV2PreservesLocationsAndAddsAllNaturalKinds()
        {
            var v1 =
                new DeterministicWorldPopulationGenerator(
                    WorldPopulationDefaults
                        .CreateTemperatePrototypeV1(
                            new WorldSeed(0UL)));
            var v2 =
                new DeterministicWorldPopulationGenerator(
                    WorldPopulationDefaults
                        .CreateTemperatePrototypeV2(
                            new WorldSeed(0UL)));

            PopulatedChunk v1Chunk =
                v1.Generate(new ChunkCoordinate(0L, 0L));
            PopulatedChunk v2Chunk =
                v2.Generate(new ChunkCoordinate(0L, 0L));

            CollectionAssert.AreEqual(
                v1Chunk.Resources
                    .Select(r => r.Cell).ToArray(),
                v2Chunk.Resources
                    .Select(r => r.Cell).ToArray());
            Assert.That(
                v2Chunk.Resources
                    .Select(r => r.Id)
                    .SequenceEqual(
                        v1Chunk.Resources.Select(r => r.Id)),
                Is.False);

            var kinds =
                new HashSet<GeneratedResourceKind>();
            for (long y = -2; y <= 2; y++)
            {
                for (long x = -2; x <= 2; x++)
                {
                    foreach (
                        GeneratedResourcePlacement resource in
                        v2.Generate(
                            new ChunkCoordinate(x, y))
                            .Resources)
                    {
                        kinds.Add(resource.Kind);
                    }
                }
            }

            CollectionAssert.AreEquivalent(
                new[]
                {
                    GeneratedResourceKind.Shrub,
                    GeneratedResourceKind.LooseStone,
                    GeneratedResourceKind.Deadwood,
                    GeneratedResourceKind.Tree
                },
                kinds);
        }

        [Test]
        public void LegacyYieldArgumentsAreValidatedWithoutAvailableTarget()
        {
            var ground = new List<GroundContainerState>();
            var unique = new ItemDefinition(
                new ItemDefinitionId("unique-yield-test"),
                "Unique yield",
                ItemStateKind.Unique,
                new EncumbranceValue(500));
            ItemDefinition stack = Stack("stack-yield-test", 500);

            Assert.Throws<ArgumentException>(() =>
                ResourceYieldOperations.HarvestToGround(
                    Array.Empty<ResourceState>(),
                    ground,
                    new WorldPosition(0.0, 0.0),
                    1.5,
                    unique,
                    1,
                    new EncumbranceValue(1000)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ResourceYieldOperations.HarvestToGround(
                    Array.Empty<ResourceState>(),
                    ground,
                    new WorldPosition(0.0, 0.0),
                    1.5,
                    stack,
                    0,
                    new EncumbranceValue(1000)));

            Assert.Throws<ArgumentException>(() =>
                ResourceYieldOperations.HarvestToGround(
                    Array.Empty<ResourceState>(),
                    ground,
                    new WorldPosition(0.0, 0.0),
                    1.5,
                    stack,
                    1,
                    new EncumbranceValue(499)));

            Assert.That(ground, Is.Empty);
        }

        [Test]
        public void PopulationV2KindAndIdentifierFixtureIsStable()
        {
            var generator =
                new DeterministicWorldPopulationGenerator(
                    WorldPopulationDefaults
                        .CreateTemperatePrototypeV2(
                            new WorldSeed(0UL)));
            PopulatedChunk chunk =
                generator.Generate(new ChunkCoordinate(0L, 0L));

            WorldCellCoordinate[] cells =
            {
                new WorldCellCoordinate(30L, 0L),
                new WorldCellCoordinate(24L, 1L),
                new WorldCellCoordinate(2L, 4L),
                new WorldCellCoordinate(9L, 6L)
            };
            GeneratedResourceKind[] kinds =
            {
                GeneratedResourceKind.Shrub,
                GeneratedResourceKind.Tree,
                GeneratedResourceKind.Deadwood,
                GeneratedResourceKind.LooseStone
            };
            string[] identifiers =
            {
                "generated:0000000000000000:2:temperate-prototype:2:0:30:0",
                "generated:0000000000000000:2:temperate-prototype:2:3:24:1",
                "generated:0000000000000000:2:temperate-prototype:2:2:2:4",
                "generated:0000000000000000:2:temperate-prototype:2:1:9:6"
            };

            for (int index = 0; index < cells.Length; index++)
            {
                GeneratedResourcePlacement placement =
                    chunk.Resources.Single(
                        resource => resource.Cell.Equals(cells[index]));

                Assert.That(
                    placement.Kind,
                    Is.EqualTo(kinds[index]),
                    cells[index].ToString());
                Assert.That(
                    placement.Id.Value,
                    Is.EqualTo(identifiers[index]),
                    cells[index].ToString());
            }
        }

        [Test]
        public void CurrentTemperateProfileIsRevisionTwo()
        {
            WorldPopulationSettings current =
                WorldPopulationDefaults
                    .CreateTemperatePrototypeCurrent(
                        new WorldSeed(42UL));

            Assert.That(
                current.Profile.Revision,
                Is.EqualTo(2));
            Assert.That(
                current.Profile.Id,
                Is.EqualTo(
                    WorldPopulationDefaults
                        .TemperatePrototypeV1Id));
        }

        private static ItemDefinition Stack(
            string id,
            long encumbrance)
        {
            return new ItemDefinition(
                new ItemDefinitionId(id),
                id,
                ItemStateKind.Stackable,
                new EncumbranceValue(encumbrance));
        }
    }
}
