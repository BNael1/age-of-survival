using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class DeterministicWorldPopulationTests
    {
        [Test]
        public void Generate_SeedZeroMatchesPrototypeGoldenCounts()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(0UL);
            PopulatedChunk chunk = generator.Generate(new ChunkCoordinate(0L, 0L));

            int grass = 0;
            int dirt = 0;
            int water = 0;
            int wooded = 0;
            int open = 0;
            int noZone = 0;
            foreach (GeneratedCellData cell in chunk.CopyCells())
            {
                switch (cell.Terrain)
                {
                    case GeneratedTerrainKind.Grass:
                        grass++;
                        break;
                    case GeneratedTerrainKind.Dirt:
                        dirt++;
                        break;
                    case GeneratedTerrainKind.Water:
                        water++;
                        break;
                }

                switch (cell.Zone)
                {
                    case GeneratedLandscapeZone.None:
                        noZone++;
                        break;
                    case GeneratedLandscapeZone.Open:
                        open++;
                        break;
                    case GeneratedLandscapeZone.Wooded:
                        wooded++;
                        break;
                }
            }

            Assert.That(grass, Is.EqualTo(790));
            Assert.That(dirt, Is.EqualTo(91));
            Assert.That(water, Is.EqualTo(143));
            Assert.That(wooded, Is.EqualTo(220));
            Assert.That(open, Is.EqualTo(661));
            Assert.That(noZone, Is.EqualTo(143));
            Assert.That(chunk.Resources.Count, Is.EqualTo(41));
        }

        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(42UL)]
        public void Generate_SameSettingsProduceIdenticalPopulation(ulong seed)
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(seed);

            PopulatedChunk first = generator.Generate(new ChunkCoordinate(-2L, 3L));
            PopulatedChunk second = generator.Generate(new ChunkCoordinate(-2L, 3L));

            CollectionAssert.AreEqual(first.CopyCells(), second.CopyCells());
            CollectionAssert.AreEqual(first.CopyResources(), second.CopyResources());
        }

        [Test]
        public void Generate_DifferentSeedsChangeTerrainAndResources()
        {
            PopulatedChunk first = CreateGenerator(0UL).Generate(new ChunkCoordinate(0L, 0L));
            PopulatedChunk second = CreateGenerator(1UL).Generate(new ChunkCoordinate(0L, 0L));

            Assert.That(second.CopyCells().SequenceEqual(first.CopyCells()), Is.False);
            Assert.That(second.CopyResources().SequenceEqual(first.CopyResources()), Is.False);
        }

        [Test]
        public void SampleCell_DoesNotDependOnChunkPartition()
        {
            var seed = new WorldSeed(987654321UL);
            WorldPopulationProfile profile = WorldPopulationDefaults.TemperatePrototypeV1;
            var small = new DeterministicWorldPopulationGenerator(new WorldPopulationSettings(
                new WorldGenerationSettings(seed, WorldGeneratorVersions.PopulationV1, new ChunkLayout(8, 8)),
                profile));
            var large = new DeterministicWorldPopulationGenerator(new WorldPopulationSettings(
                new WorldGenerationSettings(seed, WorldGeneratorVersions.PopulationV1, new ChunkLayout(64, 64)),
                profile));
            var cell = new WorldCellCoordinate(-123L, 456L);

            Assert.That(large.SampleCell(cell), Is.EqualTo(small.SampleCell(cell)));
            Assert.That(
                large.HasGeneratedResourceAt(cell),
                Is.EqualTo(small.HasGeneratedResourceAt(cell)));
        }

        [Test]
        public void Generate_OrderDoesNotAffectAdjacentChunks()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(42UL);
            var firstCoordinate = new ChunkCoordinate(-1L, 0L);
            var secondCoordinate = new ChunkCoordinate(0L, 0L);

            PopulatedChunk firstA = generator.Generate(firstCoordinate);
            PopulatedChunk secondA = generator.Generate(secondCoordinate);
            PopulatedChunk secondB = generator.Generate(secondCoordinate);
            PopulatedChunk firstB = generator.Generate(firstCoordinate);

            CollectionAssert.AreEqual(firstA.CopyCells(), firstB.CopyCells());
            CollectionAssert.AreEqual(firstA.CopyResources(), firstB.CopyResources());
            CollectionAssert.AreEqual(secondA.CopyCells(), secondB.CopyCells());
            CollectionAssert.AreEqual(secondA.CopyResources(), secondB.CopyResources());
        }

        [Test]
        public void GeneratedResourcesAppearOnlyOnLand()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(0UL);
            PopulatedChunk chunk = generator.Generate(new ChunkCoordinate(0L, 0L));

            foreach (GeneratedResourcePlacement resource in chunk.Resources)
            {
                Assert.That(generator.SampleCell(resource.Cell).IsLand, Is.True, resource.ToString());
                Assert.That(generator.HasGeneratedResourceAt(resource.Cell), Is.True, resource.ToString());
            }
        }

        [Test]
        public void GeneratedResourcesRespectMinimumDistanceAcrossChunkBorders()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(0UL);
            var resources = new List<GeneratedResourcePlacement>();
            resources.AddRange(generator.Generate(new ChunkCoordinate(0L, 0L)).Resources);
            resources.AddRange(generator.Generate(new ChunkCoordinate(1L, 0L)).Resources);
            resources.AddRange(generator.Generate(new ChunkCoordinate(0L, 1L)).Resources);
            resources.AddRange(generator.Generate(new ChunkCoordinate(1L, 1L)).Resources);

            int radius = generator.Settings.Profile.ResourceExclusionRadius;
            long minimumSquared = (long)radius * radius;
            for (int first = 0; first < resources.Count; first++)
            {
                for (int second = first + 1; second < resources.Count; second++)
                {
                    long deltaX = resources[first].Cell.X - resources[second].Cell.X;
                    long deltaY = resources[first].Cell.Y - resources[second].Cell.Y;
                    long distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                    Assert.That(
                        distanceSquared,
                        Is.GreaterThan(minimumSquared),
                        $"{resources[first]} conflicts with {resources[second]}");
                }
            }
        }

        [Test]
        public void BorderResourceDecisionMatchesAbsoluteSampler()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(1UL);
            PopulatedChunk left = generator.Generate(new ChunkCoordinate(-1L, 0L));
            PopulatedChunk right = generator.Generate(new ChunkCoordinate(0L, 0L));

            AssertChunkResourcesMatchSampler(generator, left);
            AssertChunkResourcesMatchSampler(generator, right);
        }

        [Test]
        public void GeneratedResourceIdentifiersAreUniqueAndStable()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(0UL);
            PopulatedChunk first = generator.Generate(new ChunkCoordinate(0L, 0L));
            PopulatedChunk second = generator.Generate(new ChunkCoordinate(0L, 0L));
            ResourceId[] firstIds = first.Resources.Select(resource => resource.Id).ToArray();
            ResourceId[] secondIds = second.Resources.Select(resource => resource.Id).ToArray();

            CollectionAssert.AllItemsAreUnique(firstIds);
            CollectionAssert.AreEqual(firstIds, secondIds);
            StringAssert.StartsWith(
                "generated:0000000000000000:2:temperate-prototype:1:0:",
                firstIds[0].Value);
        }

        [Test]
        public void GeneratedResourceIdentifiersIncludeWorldSeed()
        {
            var cell = new WorldCellCoordinate(12L, -34L);
            ResourceId first = GeneratedResourceIds.Create(
                WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(1UL)),
                GeneratedResourceKind.Shrub,
                cell);
            ResourceId second = GeneratedResourceIds.Create(
                WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(2UL)),
                GeneratedResourceKind.Shrub,
                cell);

            Assert.That(second, Is.Not.EqualTo(first));
            StringAssert.StartsWith("generated:0000000000000001:", first.Value);
            StringAssert.StartsWith("generated:0000000000000002:", second.Value);
        }

        [Test]
        public void PopulatedChunkCopiesCannotMutateGeneratedBaseline()
        {
            PopulatedChunk chunk = CreateGenerator(0UL).Generate(new ChunkCoordinate(0L, 0L));
            GeneratedCellData originalCell = chunk.GetCell(new GridPosition(0, 0));
            GeneratedResourcePlacement originalResource = chunk.Resources[0];

            GeneratedCellData[] cells = chunk.CopyCells();
            GeneratedResourcePlacement[] resources = chunk.CopyResources();
            cells[0] = new GeneratedCellData(GeneratedTerrainKind.Water, GeneratedLandscapeZone.Open);
            resources[0] = default(GeneratedResourcePlacement);

            Assert.That(chunk.GetCell(new GridPosition(0, 0)), Is.EqualTo(originalCell));
            Assert.That(chunk.Resources[0], Is.EqualTo(originalResource));
        }

        [Test]
        public void Spawn_SeedZeroUsesCenterWhenItIsValid()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(0UL);

            bool found = generator.TryFindSpawnNear(
                new WorldCellCoordinate(16L, 16L),
                out GeneratedSpawnPoint spawn);

            Assert.That(found, Is.True);
            Assert.That(spawn.Cell, Is.EqualTo(new WorldCellCoordinate(16L, 16L)));
            Assert.That(generator.SampleCell(spawn.Cell).IsOpenLand, Is.True);
            AssertSpawnClearance(generator, spawn);
        }

        [TestCase(1UL, 16L, 16L, 23L, 23L)]
        [TestCase(42UL, 16L, 16L, 23L, 9L)]
        public void Spawn_MatchesDeterministicNearestRingFixtures(
            ulong seed,
            long preferredX,
            long preferredY,
            long expectedX,
            long expectedY)
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(seed);

            Assert.That(generator.TryFindSpawnNear(
                new WorldCellCoordinate(preferredX, preferredY),
                out GeneratedSpawnPoint spawn), Is.True);
            Assert.That(spawn.Cell, Is.EqualTo(new WorldCellCoordinate(expectedX, expectedY)));
            Assert.That(generator.SampleCell(spawn.Cell).IsOpenLand, Is.True);
            AssertSpawnClearance(generator, spawn);
        }

        [Test]
        public void Spawn_ReturnsFalseWhenOnlyPreferredWaterCellMayBeExamined()
        {
            WorldPopulationProfile source = WorldPopulationDefaults.TemperatePrototypeV1;
            var noSearchProfile = new WorldPopulationProfile(
                source.Id,
                source.Revision,
                source.TerrainScale,
                source.SoilScale,
                source.ZoneScale,
                source.WaterThreshold,
                source.DirtThreshold,
                source.WoodedThreshold,
                source.OpenResourceChance,
                source.WoodedResourceChance,
                source.ResourceExclusionRadius,
                source.SpawnClearanceRadius,
                spawnSearchRadius: 0);
            var generator = new DeterministicWorldPopulationGenerator(
                new WorldPopulationSettings(
                    WorldGenerationDefaults.CreatePopulationV1(new WorldSeed(0UL)),
                    noSearchProfile));

            Assert.That(generator.SampleCell(new WorldCellCoordinate(0L, 20L)).Terrain,
                Is.EqualTo(GeneratedTerrainKind.Water));
            Assert.That(generator.TryFindSpawnNear(
                new WorldCellCoordinate(0L, 20L),
                out GeneratedSpawnPoint _), Is.False);
        }

        [TestCase(long.MinValue, long.MinValue)]
        [TestCase(long.MaxValue, long.MaxValue)]
        [TestCase(long.MinValue, long.MaxValue)]
        public void AbsoluteSamplingHandlesInt64WorldEdges(long x, long y)
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(7UL);
            var cell = new WorldCellCoordinate(x, y);

            Assert.DoesNotThrow(() => generator.SampleCell(cell));
            Assert.DoesNotThrow(() => generator.HasGeneratedResourceAt(cell));
        }

        [Test]
        public void OnDemandWorldGeneratesCachesUnloadsAndRegenerates()
        {
            DeterministicWorldPopulationGenerator generator = CreateGenerator(99UL);
            var world = new OnDemandPopulatedWorld(generator);
            var coordinate = new ChunkCoordinate(3L, -4L);

            PopulatedChunk first = world.GetOrGenerateChunk(coordinate);
            PopulatedChunk cached = world.GetOrGenerateChunk(coordinate);
            Assert.That(cached, Is.SameAs(first));
            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(world.UnloadChunk(coordinate), Is.True);
            Assert.That(world.LoadedChunkCount, Is.Zero);

            PopulatedChunk regenerated = world.GetOrGenerateChunk(coordinate);
            Assert.That(regenerated, Is.Not.SameAs(first));
            CollectionAssert.AreEqual(first.CopyCells(), regenerated.CopyCells());
            CollectionAssert.AreEqual(first.CopyResources(), regenerated.CopyResources());

            world.Clear();
            Assert.That(world.LoadedChunkCount, Is.Zero);
        }

        private static DeterministicWorldPopulationGenerator CreateGenerator(ulong seed)
        {
            return new DeterministicWorldPopulationGenerator(
                WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(seed)));
        }

        private static void AssertChunkResourcesMatchSampler(
            DeterministicWorldPopulationGenerator generator,
            PopulatedChunk chunk)
        {
            var generatedCells = new HashSet<WorldCellCoordinate>(
                chunk.Resources.Select(resource => resource.Cell));
            ChunkLayout layout = chunk.Layout;
            for (int index = 0; index < layout.CellCount; index++)
            {
                GridPosition local = layout.Bounds.FromIndex(index);
                WorldCellCoordinate worldCell = chunk.GetWorldCell(local);
                Assert.That(
                    generatedCells.Contains(worldCell),
                    Is.EqualTo(generator.HasGeneratedResourceAt(worldCell)),
                    worldCell.ToString());
            }
        }

        private static void AssertSpawnClearance(
            DeterministicWorldPopulationGenerator generator,
            GeneratedSpawnPoint spawn)
        {
            int radius = generator.Settings.Profile.SpawnClearanceRadius;
            int radiusSquared = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if ((x * x) + (y * y) <= radiusSquared)
                    {
                        Assert.That(
                            generator.HasGeneratedResourceAt(spawn.Cell.Offset(x, y)),
                            Is.False);
                    }
                }
            }
        }
    }
}
