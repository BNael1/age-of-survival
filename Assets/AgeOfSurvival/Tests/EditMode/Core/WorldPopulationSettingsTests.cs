using System;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class WorldPopulationSettingsTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ProfileId_RejectsEmptyValues(string value)
        {
            Assert.Throws<ArgumentException>(() => new WorldPopulationProfileId(value));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Profile_RejectsNonPositiveRevision(int revision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfile(revision: revision));
        }

        [TestCase(1, 11, 16)]
        [TestCase(24, 1, 16)]
        [TestCase(24, 11, 1)]
        [TestCase(4097, 11, 16)]
        public void Profile_RejectsInvalidFieldScales(
            int terrainScale,
            int soilScale,
            int zoneScale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfile(
                terrainScale: terrainScale,
                soilScale: soilScale,
                zoneScale: zoneScale));
        }

        [TestCase(65537u, 1u)]
        [TestCase(1u, 65537u)]
        public void Profile_RejectsChanceAboveQ16Scale(
            uint openChance,
            uint woodedChance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfile(
                openResourceChance: openChance,
                woodedResourceChance: woodedChance));
        }

        [TestCase(0)]
        [TestCase(65)]
        public void Profile_RejectsInvalidResourceExclusionRadius(int radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfile(
                resourceExclusionRadius: radius));
        }

        [TestCase(-1)]
        [TestCase(65)]
        public void Profile_RejectsInvalidSpawnClearanceRadius(int radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfile(
                spawnClearanceRadius: radius));
        }

        [TestCase(-1)]
        [TestCase(4097)]
        public void Profile_RejectsInvalidSpawnSearchRadius(int radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfile(
                spawnSearchRadius: radius));
        }

        [Test]
        public void PopulationSettings_RejectFoundationOnlyGeneratorVersion()
        {
            WorldGenerationSettings foundation =
                WorldGenerationDefaults.CreateFoundationV1(new WorldSeed(42UL));

            Assert.Throws<ArgumentException>(() => new WorldPopulationSettings(
                foundation,
                WorldPopulationDefaults.TemperatePrototypeV1));
        }

        [Test]
        public void Defaults_UseExplicitPopulationVersionAndProfileRevision()
        {
            WorldPopulationSettings settings =
                WorldPopulationDefaults.CreateTemperatePrototypeV1(new WorldSeed(42UL));

            Assert.That(settings.Generation.Version, Is.EqualTo(WorldGeneratorVersions.PopulationV1));
            Assert.That(settings.Generation.ChunkLayout, Is.EqualTo(new ChunkLayout(32, 32)));
            Assert.That(settings.Profile.Id, Is.EqualTo(WorldPopulationDefaults.TemperatePrototypeV1Id));
            Assert.That(settings.Profile.Revision, Is.EqualTo(1));
            Assert.That(settings.Profile.IsValid, Is.True);
        }

        [Test]
        public void Settings_EqualityIncludesEveryProfileValue()
        {
            WorldGenerationSettings generation =
                WorldGenerationDefaults.CreatePopulationV1(new WorldSeed(7UL));
            WorldPopulationProfile firstProfile = CreateProfile(openResourceChance: 100u);
            WorldPopulationProfile secondProfile = CreateProfile(openResourceChance: 101u);

            var first = new WorldPopulationSettings(generation, firstProfile);
            var equal = new WorldPopulationSettings(generation, firstProfile);
            var different = new WorldPopulationSettings(generation, secondProfile);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(different));
        }

        private static WorldPopulationProfile CreateProfile(
            int revision = 1,
            int terrainScale = 24,
            int soilScale = 11,
            int zoneScale = 16,
            uint openResourceChance = 100u,
            uint woodedResourceChance = 200u,
            int resourceExclusionRadius = 2,
            int spawnClearanceRadius = 1,
            int spawnSearchRadius = 48)
        {
            return new WorldPopulationProfile(
                new WorldPopulationProfileId("test-profile"),
                revision,
                terrainScale,
                soilScale,
                zoneScale,
                waterThreshold: 10000,
                dirtThreshold: 20000,
                woodedThreshold: 30000,
                openResourceChance,
                woodedResourceChance,
                resourceExclusionRadius,
                spawnClearanceRadius,
                spawnSearchRadius);
        }
    }
}
