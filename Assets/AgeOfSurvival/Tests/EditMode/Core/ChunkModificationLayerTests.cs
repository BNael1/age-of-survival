using System;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ChunkModificationLayerTests
    {
        [Test]
        public void Constructor_RejectsDefaultLayout()
        {
            Assert.Throws<ArgumentException>(() =>
                new ChunkModificationLayer<string>(
                    new ChunkCoordinate(0, 0),
                    default(ChunkLayout)));
        }

        [Test]
        public void SetAndTryGet_StoreSparseModification()
        {
            var layer = CreateLayer();
            var position = new GridPosition(2, 3);

            layer.Set(position, "dug");

            Assert.That(layer.TryGet(position, out string value), Is.True);
            Assert.That(value, Is.EqualTo("dug"));
            Assert.That(layer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Set_ReplacesExistingValueWithoutAddingEntry()
        {
            var layer = CreateLayer();
            var position = new GridPosition(1, 1);
            layer.Set(position, "first");

            layer.Set(position, "second");

            Assert.That(layer.TryGet(position, out string value), Is.True);
            Assert.That(value, Is.EqualTo("second"));
            Assert.That(layer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Remove_DeletesExistingEntry()
        {
            var layer = CreateLayer();
            var position = new GridPosition(4, 4);
            layer.Set(position, "removed");

            bool removed = layer.Remove(position);

            Assert.That(removed, Is.True);
            Assert.That(layer.TryGet(position, out string value), Is.False);
            Assert.That(value, Is.Null);
            Assert.That(layer.Count, Is.EqualTo(0));
        }

        [Test]
        public void Clear_RemovesEveryEntry()
        {
            var layer = CreateLayer();
            layer.Set(new GridPosition(0, 0), "a");
            layer.Set(new GridPosition(1, 1), "b");

            layer.Clear();

            Assert.That(layer.Count, Is.EqualTo(0));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(8, 0)]
        [TestCase(0, 8)]
        public void Set_RejectsPositionOutsideChunk(int x, int y)
        {
            var layer = CreateLayer();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                layer.Set(new GridPosition(x, y), "invalid"));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(8, 0)]
        [TestCase(0, 8)]
        public void TryGet_RejectsPositionOutsideChunk(int x, int y)
        {
            var layer = CreateLayer();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                layer.TryGet(new GridPosition(x, y), out string _));
        }

        [Test]
        public void CopyEntriesInStableOrder_UsesRowMajorLocalOrder()
        {
            var layer = CreateLayer();
            layer.Set(new GridPosition(7, 7), "last");
            layer.Set(new GridPosition(2, 0), "second");
            layer.Set(new GridPosition(0, 0), "first");
            layer.Set(new GridPosition(0, 1), "third");

            ChunkModification<string>[] entries = layer.CopyEntriesInStableOrder();

            Assert.That(entries[0].LocalPosition, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(entries[1].LocalPosition, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(entries[2].LocalPosition, Is.EqualTo(new GridPosition(0, 1)));
            Assert.That(entries[3].LocalPosition, Is.EqualTo(new GridPosition(7, 7)));
        }

        [Test]
        public void ModificationLayer_DoesNotMutateGeneratedChunk()
        {
            WorldGenerationSettings settings = new WorldGenerationSettings(
                new WorldSeed(15UL),
                WorldGeneratorVersions.FoundationV1,
                new ChunkLayout(8, 8));
            GeneratedChunk generated = new DeterministicChunkGenerator(settings)
                .Generate(new ChunkCoordinate(3, -2));
            var position = new GridPosition(5, 6);
            ulong original = generated.GetFoundationSample(position);
            var modifications = new ChunkModificationLayer<string>(
                generated.Coordinate,
                generated.Layout);

            modifications.Set(position, "player-built-floor");

            Assert.That(generated.GetFoundationSample(position), Is.EqualTo(original));
            Assert.That(modifications.TryGet(position, out string value), Is.True);
            Assert.That(value, Is.EqualTo("player-built-floor"));
        }

        [Test]
        public void DifferentChunkLayersRemainIndependent()
        {
            var first = new ChunkModificationLayer<int>(
                new ChunkCoordinate(0, 0),
                new ChunkLayout(8, 8));
            var second = new ChunkModificationLayer<int>(
                new ChunkCoordinate(1, 0),
                new ChunkLayout(8, 8));
            var localPosition = new GridPosition(0, 0);

            first.Set(localPosition, 7);

            Assert.That(first.TryGet(localPosition, out int firstValue), Is.True);
            Assert.That(firstValue, Is.EqualTo(7));
            Assert.That(second.TryGet(localPosition, out int secondValue), Is.False);
            Assert.That(secondValue, Is.EqualTo(0));
        }

        private static ChunkModificationLayer<string> CreateLayer()
        {
            return new ChunkModificationLayer<string>(
                new ChunkCoordinate(-4, 9),
                new ChunkLayout(8, 8));
        }
    }
}
