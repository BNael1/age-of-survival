using System;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class DeterministicWorldGenerationTests
    {
        [Test]
        public void Sampler_SupportsFoundationVersionOne()
        {
            Assert.That(
                DeterministicWorldSampler.Supports(WorldGeneratorVersions.FoundationV1),
                Is.True);
        }

        [Test]
        public void Sampler_RejectsUnsupportedGeneratorVersion()
        {
            var settings = new WorldGenerationSettings(
                new WorldSeed(1UL),
                new WorldGeneratorVersion(3),
                new ChunkLayout(32, 32));

            Assert.Throws<NotSupportedException>(() =>
                DeterministicWorldSampler.Sample64(
                    settings,
                    new WorldCellCoordinate(0, 0),
                    GenerationStreams.Foundation));
        }

        [Test]
        public void ChunkGenerator_RejectsUnsupportedGeneratorVersion()
        {
            var settings = new WorldGenerationSettings(
                new WorldSeed(1UL),
                new WorldGeneratorVersion(3),
                new ChunkLayout(32, 32));

            Assert.Throws<NotSupportedException>(() =>
                new DeterministicChunkGenerator(settings));
        }

        [TestCase("0", 0L, 0L, 0L, "0x5D880D0A2F63439F")]
        [TestCase("42", 0L, 0L, 0L, "0xB02F67DD87A0EC32")]
        [TestCase("42", 1L, 0L, 0L, "0x39A2F74C188F7237")]
        [TestCase("42", 0L, 1L, 0L, "0x05F6D4570FF4D9AF")]
        [TestCase("42", -1L, -1L, 0L, "0x1DD4F9965F966014")]
        [TestCase("0xFFFFFFFFFFFFFFFF", 123456789L, -987654321L, 0xA5A5A5A5L, "0x169C0EC8C52E2B4C")]
        public void Sample64_MatchesFoundationVersionOneFixtures(
            string seedText,
            long worldX,
            long worldY,
            long streamValue,
            string expectedText)
        {
            WorldGenerationSettings settings = CreateSettings(
                WorldSeed.Parse(seedText),
                new ChunkLayout(32, 32));

            ulong actual = DeterministicWorldSampler.Sample64(
                settings,
                new WorldCellCoordinate(worldX, worldY),
                new GenerationStream(checked((uint)streamValue)));

            ulong expected = Convert.ToUInt64(expectedText.Substring(2), 16);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(0L, 0L)]
        [TestCase(1L, -1L)]
        [TestCase(1000000L, -1000000L)]
        [TestCase(long.MinValue, long.MaxValue)]
        public void SampleUnitInterval_IsRepeatableAndInsideHalfOpenUnitRange(long x, long y)
        {
            WorldGenerationSettings settings = CreateSettings(
                new WorldSeed(918273645UL),
                new ChunkLayout(32, 32));
            var coordinate = new WorldCellCoordinate(x, y);

            double first = DeterministicWorldSampler.SampleUnitInterval(
                settings,
                coordinate,
                GenerationStreams.Foundation);
            double second = DeterministicWorldSampler.SampleUnitInterval(
                settings,
                coordinate,
                GenerationStreams.Foundation);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(first, Is.LessThan(1.0));
        }

        [Test]
        public void Generate_SameSettingsAndCoordinateProduceIdenticalChunk()
        {
            WorldGenerationSettings settings = CreateSettings(
                new WorldSeed(42UL),
                new ChunkLayout(8, 8));
            var coordinate = new ChunkCoordinate(4, -7);

            GeneratedChunk first = new DeterministicChunkGenerator(settings).Generate(coordinate);
            GeneratedChunk second = new DeterministicChunkGenerator(settings).Generate(coordinate);

            CollectionAssert.AreEqual(
                first.CopyFoundationSamples(),
                second.CopyFoundationSamples());
        }

        [Test]
        public void Generate_DifferentSeedsChangeChunkContent()
        {
            var coordinate = new ChunkCoordinate(2, 3);
            GeneratedChunk first = new DeterministicChunkGenerator(CreateSettings(
                new WorldSeed(1UL),
                new ChunkLayout(8, 8))).Generate(coordinate);
            GeneratedChunk second = new DeterministicChunkGenerator(CreateSettings(
                new WorldSeed(2UL),
                new ChunkLayout(8, 8))).Generate(coordinate);

            CollectionAssert.AreNotEqual(
                first.CopyFoundationSamples(),
                second.CopyFoundationSamples());
        }

        [Test]
        public void Sample64_DifferentStreamsProduceIndependentValues()
        {
            WorldGenerationSettings settings = CreateSettings(
                new WorldSeed(42UL),
                new ChunkLayout(32, 32));
            var cell = new WorldCellCoordinate(11, -9);

            ulong first = DeterministicWorldSampler.Sample64(
                settings,
                cell,
                new GenerationStream(1u));
            ulong second = DeterministicWorldSampler.Sample64(
                settings,
                cell,
                new GenerationStream(2u));

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void Sample64_WorldCellContentDoesNotDependOnChunkPartition()
        {
            var seed = new WorldSeed(42UL);
            var cell = new WorldCellCoordinate(-1234, 5678);
            WorldGenerationSettings smallChunks = CreateSettings(seed, new ChunkLayout(8, 8));
            WorldGenerationSettings largeChunks = CreateSettings(seed, new ChunkLayout(64, 64));

            ulong first = DeterministicWorldSampler.Sample64(
                smallChunks,
                cell,
                GenerationStreams.Foundation);
            ulong second = DeterministicWorldSampler.Sample64(
                largeChunks,
                cell,
                GenerationStreams.Foundation);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void Generate_ChunkOrderDoesNotAffectContent()
        {
            var generator = new DeterministicChunkGenerator(CreateSettings(
                new WorldSeed(123UL),
                new ChunkLayout(8, 8)));
            var firstCoordinate = new ChunkCoordinate(-2, 5);
            var secondCoordinate = new ChunkCoordinate(11, -4);

            GeneratedChunk firstBefore = generator.Generate(firstCoordinate);
            generator.Generate(secondCoordinate);
            GeneratedChunk firstAfter = generator.Generate(firstCoordinate);

            CollectionAssert.AreEqual(
                firstBefore.CopyFoundationSamples(),
                firstAfter.CopyFoundationSamples());
        }

        [TestCase(0L, 0L, 7, 0, 1L, 0L, 0, 0)]
        [TestCase(0L, 0L, 0, 7, 0L, 1L, 0, 0)]
        [TestCase(0L, 0L, 0, 0, -1L, 0L, 7, 0)]
        [TestCase(0L, 0L, 0, 0, 0L, -1L, 0, 7)]
        public void Generate_BorderCellsMatchAbsoluteWorldSampler(
            long chunkX,
            long chunkY,
            int localX,
            int localY,
            long neighboringChunkX,
            long neighboringChunkY,
            int neighboringLocalX,
            int neighboringLocalY)
        {
            WorldGenerationSettings settings = CreateSettings(
                new WorldSeed(777UL),
                new ChunkLayout(8, 8));
            var generator = new DeterministicChunkGenerator(settings);
            GeneratedChunk first = generator.Generate(new ChunkCoordinate(chunkX, chunkY));
            GeneratedChunk neighbor = generator.Generate(
                new ChunkCoordinate(neighboringChunkX, neighboringChunkY));
            var firstLocal = new GridPosition(localX, localY);
            var neighborLocal = new GridPosition(neighboringLocalX, neighboringLocalY);

            Assert.That(
                first.GetFoundationSample(firstLocal),
                Is.EqualTo(DeterministicWorldSampler.Sample64(
                    settings,
                    first.GetWorldCell(firstLocal),
                    GenerationStreams.Foundation)));
            Assert.That(
                neighbor.GetFoundationSample(neighborLocal),
                Is.EqualTo(DeterministicWorldSampler.Sample64(
                    settings,
                    neighbor.GetWorldCell(neighborLocal),
                    GenerationStreams.Foundation)));
        }

        [Test]
        public void Generate_NegativeBorderCoordinatesRemainContiguous()
        {
            WorldGenerationSettings settings = CreateSettings(
                new WorldSeed(3UL),
                new ChunkLayout(8, 8));
            var generator = new DeterministicChunkGenerator(settings);
            GeneratedChunk left = generator.Generate(new ChunkCoordinate(-1, 0));
            GeneratedChunk right = generator.Generate(new ChunkCoordinate(0, 0));

            WorldCellCoordinate leftBorder = left.GetWorldCell(new GridPosition(7, 0));
            WorldCellCoordinate rightBorder = right.GetWorldCell(new GridPosition(0, 0));

            Assert.That(leftBorder, Is.EqualTo(new WorldCellCoordinate(-1, 0)));
            Assert.That(rightBorder, Is.EqualTo(new WorldCellCoordinate(0, 0)));
        }

        [Test]
        public void OnDemandWorld_ReturnsSameLoadedChunkInstance()
        {
            var world = CreateWorld();
            var coordinate = new ChunkCoordinate(5, -9);

            GeneratedChunk first = world.GetOrGenerateChunk(coordinate);
            GeneratedChunk second = world.GetOrGenerateChunk(coordinate);

            Assert.That(second, Is.SameAs(first));
            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
        }

        [Test]
        public void OnDemandWorld_GeneratesOnlyRequestedChunks()
        {
            var world = CreateWorld();

            Assert.That(world.LoadedChunkCount, Is.EqualTo(0));
            world.GetOrGenerateChunk(new ChunkCoordinate(1, 2));

            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(
                world.TryGetLoadedChunk(new ChunkCoordinate(9, 9), out GeneratedChunk missing),
                Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void OnDemandWorld_UnloadThenRegeneratePreservesContent()
        {
            var world = CreateWorld();
            var coordinate = new ChunkCoordinate(-6, 7);
            GeneratedChunk first = world.GetOrGenerateChunk(coordinate);
            ulong[] expected = first.CopyFoundationSamples();

            bool unloaded = world.UnloadChunk(coordinate);
            GeneratedChunk second = world.GetOrGenerateChunk(coordinate);

            Assert.That(unloaded, Is.True);
            Assert.That(second, Is.Not.SameAs(first));
            CollectionAssert.AreEqual(expected, second.CopyFoundationSamples());
        }

        [Test]
        public void OnDemandWorld_ClearRemovesAllLoadedChunks()
        {
            var world = CreateWorld();
            world.GetOrGenerateChunk(new ChunkCoordinate(0, 0));
            world.GetOrGenerateChunk(new ChunkCoordinate(1, 0));

            world.Clear();

            Assert.That(world.LoadedChunkCount, Is.EqualTo(0));
        }

        [Test]
        public void GeneratedChunk_CopyCannotMutateGeneratedBaseline()
        {
            GeneratedChunk chunk = new DeterministicChunkGenerator(CreateSettings(
                new WorldSeed(91UL),
                new ChunkLayout(4, 4))).Generate(new ChunkCoordinate(0, 0));
            ulong original = chunk.GetFoundationSample(new GridPosition(0, 0));

            ulong[] copy = chunk.CopyFoundationSamples();
            copy[0] ^= ulong.MaxValue;

            Assert.That(
                chunk.GetFoundationSample(new GridPosition(0, 0)),
                Is.EqualTo(original));
        }

        private static OnDemandGeneratedWorld CreateWorld()
        {
            return new OnDemandGeneratedWorld(
                new DeterministicChunkGenerator(CreateSettings(
                    new WorldSeed(8080UL),
                    new ChunkLayout(8, 8))));
        }

        private static WorldGenerationSettings CreateSettings(
            WorldSeed seed,
            ChunkLayout layout)
        {
            return new WorldGenerationSettings(
                seed,
                WorldGeneratorVersions.FoundationV1,
                layout);
        }
    }
}
