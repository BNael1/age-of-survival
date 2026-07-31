using System;
using AgeOfSurvival.Core.World;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class GridBoundsTests
    {
        [Test]
        public void IndexConversion_RoundTripsEveryCell()
        {
            var bounds = new GridBounds(7, 5);

            for (int index = 0; index < bounds.CellCount; index++)
            {
                GridPosition position = bounds.FromIndex(index);
                Assert.That(bounds.ToIndex(position), Is.EqualTo(index));
            }
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(7, 0)]
        [TestCase(0, 5)]
        public void Contains_RejectsPositionsOutsideBounds(int x, int y)
        {
            var bounds = new GridBounds(7, 5);
            Assert.That(bounds.Contains(new GridPosition(x, y)), Is.False);
        }

        [Test]
        public void Constructor_RejectsNonPositiveDimensions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridBounds(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridBounds(1, 0));
        }
    }
}
