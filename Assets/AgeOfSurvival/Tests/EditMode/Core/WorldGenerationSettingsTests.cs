using System;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class WorldGenerationSettingsTests
    {
        [TestCase("0", "0x0000000000000000")]
        [TestCase("42", "0x000000000000002A")]
        [TestCase("0x2A", "0x000000000000002A")]
        [TestCase("0XFFFFFFFFFFFFFFFF", "0xFFFFFFFFFFFFFFFF")]
        public void WorldSeed_ParseAcceptsCanonicalFormats(string text, string expected)
        {
            WorldSeed seed = WorldSeed.Parse(text);

            Assert.That(seed.ToString(), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("-1")]
        [TestCase("0x")]
        [TestCase("xyz")]
        [TestCase("18446744073709551616")]
        public void WorldSeed_TryParseRejectsInvalidText(string text)
        {
            bool parsed = WorldSeed.TryParse(text, out WorldSeed seed);

            Assert.That(parsed, Is.False);
            Assert.That(seed, Is.EqualTo(default(WorldSeed)));
        }

        [Test]
        public void WorldSeed_ToStringRoundTripsCanonicalHexadecimalValue()
        {
            var seed = new WorldSeed(0x0123456789ABCDEFUL);

            string text = seed.ToString();

            Assert.That(text, Is.EqualTo("0x0123456789ABCDEF"));
            Assert.That(WorldSeed.Parse(text), Is.EqualTo(seed));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void WorldGeneratorVersion_RejectsNonPositiveValues(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WorldGeneratorVersion(value));
        }

        [Test]
        public void WorldGenerationSettings_RejectsDefaultVersion()
        {
            Assert.Throws<ArgumentException>(() => new WorldGenerationSettings(
                new WorldSeed(1UL),
                default(WorldGeneratorVersion),
                new ChunkLayout(32, 32)));
        }

        [Test]
        public void WorldGenerationDefaults_UsesExplicitFoundationContract()
        {
            WorldGenerationSettings settings =
                WorldGenerationDefaults.CreateFoundationV1(new WorldSeed(73UL));

            Assert.That(settings.Seed, Is.EqualTo(new WorldSeed(73UL)));
            Assert.That(settings.Version, Is.EqualTo(WorldGeneratorVersions.FoundationV1));
            Assert.That(settings.ChunkLayout, Is.EqualTo(new ChunkLayout(32, 32)));
            Assert.That(settings.ToString(), Does.Contain("version=1"));
        }
    }
}
