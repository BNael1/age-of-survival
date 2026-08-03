using System;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ChunkAddressingTests
    {
        [TestCase(0, 32)]
        [TestCase(-1, 32)]
        [TestCase(32, 0)]
        [TestCase(32, -1)]
        public void ChunkLayout_RejectsNonPositiveDimensions(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ChunkLayout(width, height));
        }

        [TestCase(0L, 0L, 0L, 0L, 0, 0)]
        [TestCase(31L, 31L, 0L, 0L, 31, 31)]
        [TestCase(32L, 0L, 1L, 0L, 0, 0)]
        [TestCase(33L, 65L, 1L, 2L, 1, 1)]
        [TestCase(-1L, 0L, -1L, 0L, 31, 0)]
        [TestCase(-32L, 0L, -1L, 0L, 0, 0)]
        [TestCase(-33L, 0L, -2L, 0L, 31, 0)]
        [TestCase(0L, -1L, 0L, -1L, 0, 31)]
        [TestCase(0L, -32L, 0L, -1L, 0, 0)]
        [TestCase(0L, -33L, 0L, -2L, 0, 31)]
        [TestCase(-65L, 96L, -3L, 3L, 31, 0)]
        [TestCase(96L, -65L, 3L, -3L, 0, 31)]
        public void Locate_MapsWorldCellsToExpectedChunkAndLocalPosition(
            long worldX,
            long worldY,
            long expectedChunkX,
            long expectedChunkY,
            int expectedLocalX,
            int expectedLocalY)
        {
            ChunkAddress result = ChunkAddressing.Locate(
                new WorldCellCoordinate(worldX, worldY),
                new ChunkLayout(32, 32));

            Assert.That(
                result.Chunk,
                Is.EqualTo(new ChunkCoordinate(expectedChunkX, expectedChunkY)));
            Assert.That(
                result.LocalPosition,
                Is.EqualTo(new GridPosition(expectedLocalX, expectedLocalY)));
        }

        [TestCase(0L, 0L)]
        [TestCase(31L, 31L)]
        [TestCase(32L, 32L)]
        [TestCase(33L, 65L)]
        [TestCase(-1L, -1L)]
        [TestCase(-32L, -32L)]
        [TestCase(-33L, -65L)]
        [TestCase(123456789L, -987654321L)]
        [TestCase(long.MaxValue - 31L, 0L)]
        [TestCase(long.MinValue + 31L, 0L)]
        public void LocateAndToWorld_RoundTripRepresentativeCoordinates(
            long worldX,
            long worldY)
        {
            var layout = new ChunkLayout(32, 32);
            var expected = new WorldCellCoordinate(worldX, worldY);

            ChunkAddress address = ChunkAddressing.Locate(expected, layout);
            WorldCellCoordinate actual = ChunkAddressing.ToWorld(
                address.Chunk,
                address.LocalPosition,
                layout);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(32, 0)]
        [TestCase(0, 32)]
        public void ToWorld_RejectsLocalPositionOutsideLayout(int localX, int localY)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ChunkAddressing.ToWorld(
                    new ChunkCoordinate(0, 0),
                    new GridPosition(localX, localY),
                    new ChunkLayout(32, 32)));
        }

        [TestCase(0L, 0L, 0L, 0L)]
        [TestCase(3L, -2L, 96L, -64L)]
        [TestCase(-3L, 2L, -96L, 64L)]
        public void GetWorldOrigin_UsesCheckedChunkMultiplication(
            long chunkX,
            long chunkY,
            long expectedX,
            long expectedY)
        {
            WorldCellCoordinate origin = ChunkAddressing.GetWorldOrigin(
                new ChunkCoordinate(chunkX, chunkY),
                new ChunkLayout(32, 32));

            Assert.That(origin, Is.EqualTo(new WorldCellCoordinate(expectedX, expectedY)));
        }
    }
}
