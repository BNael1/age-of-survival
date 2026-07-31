using System;
using AgeOfSurvival.Core.World;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class GridPositionTests
    {
        [Test]
        public void CardinalNeighbors_HaveStableNorthEastSouthWestOrder()
        {
            var origin = new GridPosition(10, 20);

            Assert.That(origin.GetCardinalNeighbor(0), Is.EqualTo(new GridPosition(10, 21)));
            Assert.That(origin.GetCardinalNeighbor(1), Is.EqualTo(new GridPosition(11, 20)));
            Assert.That(origin.GetCardinalNeighbor(2), Is.EqualTo(new GridPosition(10, 19)));
            Assert.That(origin.GetCardinalNeighbor(3), Is.EqualTo(new GridPosition(9, 20)));
        }

        [Test]
        public void CardinalNeighbor_RejectsInvalidIndex()
        {
            var origin = new GridPosition(0, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => origin.GetCardinalNeighbor(4));
        }

        [Test]
        public void ManhattanDistance_IsSymmetric()
        {
            var first = new GridPosition(-2, 5);
            var second = new GridPosition(4, -3);

            Assert.That(first.ManhattanDistanceTo(second), Is.EqualTo(14));
            Assert.That(second.ManhattanDistanceTo(first), Is.EqualTo(14));
        }
    }
}
