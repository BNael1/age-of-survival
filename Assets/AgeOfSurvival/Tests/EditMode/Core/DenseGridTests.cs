using AgeOfSurvival.Core.World;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class DenseGridTests
    {
        [Test]
        public void PositionAndIndexAccess_ReferToSameCell()
        {
            var grid = new DenseGrid<int>(new GridBounds(4, 3));
            var position = new GridPosition(2, 1);

            grid[position] = 42;

            Assert.That(grid[6], Is.EqualTo(42));
        }

        [Test]
        public void CopyCells_ReturnsIndependentSnapshot()
        {
            var grid = new DenseGrid<int>(new GridBounds(2, 2));
            grid.Fill(3);

            int[] copy = grid.CopyCells();
            copy[0] = 99;

            Assert.That(grid[0], Is.EqualTo(3));
            Assert.That(copy[0], Is.EqualTo(99));
        }
    }
}
