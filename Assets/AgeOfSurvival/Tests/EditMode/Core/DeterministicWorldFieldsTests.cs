using System;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class DeterministicWorldFieldsTests
    {
        [TestCase("0", 0L, 0L, 0L, "0xA04BC1D55AE7CAE6")]
        [TestCase("42", 0L, 0L, 0L, "0xA9B50D2F950739B6")]
        [TestCase("42", 1L, 0L, 0L, "0x310E3F74D6D8728C")]
        [TestCase("42", 0L, 1L, 0L, "0x636DC987A797F58E")]
        [TestCase("42", -1L, -1L, 0L, "0x5AA0A46BD151B087")]
        [TestCase("0xFFFFFFFFFFFFFFFF", 123456789L, -987654321L, 0xA5A5A5A5L, "0xE2BA8C94417654D2")]
        public void PopulationSampler_MatchesVersionTwoFixtures(
            string seedText,
            long x,
            long y,
            long streamValue,
            string expectedHex)
        {
            WorldGenerationSettings settings = WorldGenerationDefaults.CreatePopulationV1(
                WorldSeed.Parse(seedText));
            ulong actual = DeterministicWorldSampler.Sample64(
                settings,
                new WorldCellCoordinate(x, y),
                new GenerationStream(unchecked((uint)streamValue)));

            Assert.That($"0x{actual:X16}", Is.EqualTo(expectedHex));
        }

        [Test]
        public void Sampler_SupportsBothPersistedVersions()
        {
            Assert.That(DeterministicWorldSampler.Supports(WorldGeneratorVersions.FoundationV1), Is.True);
            Assert.That(DeterministicWorldSampler.Supports(WorldGeneratorVersions.PopulationV1), Is.True);
        }

        [Test]
        public void SameSeedAndCell_DifferentGeneratorVersionsRemainSeparated()
        {
            var cell = new WorldCellCoordinate(19L, -27L);
            var foundation = WorldGenerationDefaults.CreateFoundationV1(new WorldSeed(42UL));
            var population = WorldGenerationDefaults.CreatePopulationV1(new WorldSeed(42UL));

            ulong first = DeterministicWorldSampler.Sample64(
                foundation,
                cell,
                GenerationStreams.Foundation);
            ulong second = DeterministicWorldSampler.Sample64(
                population,
                cell,
                GenerationStreams.Foundation);

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [TestCase(0L, 0L)]
        [TestCase(-1L, -1L)]
        [TestCase(123456789L, -987654321L)]
        [TestCase(long.MinValue, long.MaxValue)]
        public void SmoothedField_IsRepeatableAndInsideUnsignedSixteenBitRange(long x, long y)
        {
            WorldGenerationSettings settings = WorldGenerationDefaults.CreatePopulationV1(
                new WorldSeed(1234UL));
            var cell = new WorldCellCoordinate(x, y);

            ushort first = DeterministicWorldFields.SampleSmoothed16(
                settings,
                cell,
                24,
                GenerationStreams.TerrainElevation);
            ushort second = DeterministicWorldFields.SampleSmoothed16(
                settings,
                cell,
                24,
                GenerationStreams.TerrainElevation);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.InRange(ushort.MinValue, ushort.MaxValue));
        }

        [TestCase(-25L, -1L)]
        [TestCase(-24L, 0L)]
        [TestCase(-1L, 23L)]
        [TestCase(23L, 24L)]
        public void SmoothedField_NegativeAndPositiveBoundarySamplesRemainDefined(
            long leftX,
            long rightX)
        {
            WorldGenerationSettings settings = WorldGenerationDefaults.CreatePopulationV1(
                new WorldSeed(9UL));

            Assert.DoesNotThrow(() => DeterministicWorldFields.SampleSmoothed16(
                settings,
                new WorldCellCoordinate(leftX, -7L),
                24,
                GenerationStreams.LandscapeZone));
            Assert.DoesNotThrow(() => DeterministicWorldFields.SampleSmoothed16(
                settings,
                new WorldCellCoordinate(rightX, -7L),
                24,
                GenerationStreams.LandscapeZone));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SmoothedField_RejectsScaleBelowTwo(int scale)
        {
            WorldGenerationSettings settings = WorldGenerationDefaults.CreatePopulationV1(
                new WorldSeed(0UL));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeterministicWorldFields.SampleSmoothed16(
                    settings,
                    new WorldCellCoordinate(0L, 0L),
                    scale,
                    GenerationStreams.TerrainElevation));
        }

        [Test]
        public void NamedPopulationStreamsAreDistinct()
        {
            GenerationStream[] streams =
            {
                GenerationStreams.TerrainElevation,
                GenerationStreams.TerrainSoil,
                GenerationStreams.LandscapeZone,
                GenerationStreams.ResourceCandidate,
                GenerationStreams.ResourcePriority,
                GenerationStreams.SpawnPriority
            };

            CollectionAssert.AllItemsAreUnique(streams);
        }
    }
}
