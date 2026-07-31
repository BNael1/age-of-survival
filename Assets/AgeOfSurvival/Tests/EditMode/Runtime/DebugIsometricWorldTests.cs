using System.Linq;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Runtime.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class DebugIsometricWorldTests
    {
        [Test]
        public void DebugPattern_IsDeterministicAndKeepsTheBorderDistinct()
        {
            DenseGrid<byte> first = DebugWorldPattern.Create(10, 10);
            DenseGrid<byte> second = DebugWorldPattern.Create(10, 10);

            CollectionAssert.AreEqual(first.CopyCells(), second.CopyCells());
            Assert.That(first[new GridPosition(0, 5)], Is.EqualTo(DebugWorldPattern.BorderCell));
            Assert.That(first[new GridPosition(4, 4)], Is.EqualTo(DebugWorldPattern.AccentCell));
            Assert.That(first[new GridPosition(3, 4)], Is.EqualTo(DebugWorldPattern.BaseCell));
        }

        [Test]
        public void Rebuild_CreatesOneIsometricTilemapWithOneTilePerCoreCell()
        {
            var root = new GameObject("Debug world test");

            try
            {
                var presenter = root.AddComponent<DebugIsometricWorld>();
                presenter.Rebuild();

                Grid grid = root.GetComponentInChildren<Grid>();
                Tilemap tilemap = presenter.Tilemap;
                int populatedCells = tilemap
                    .GetTilesBlock(tilemap.cellBounds)
                    .Count(tile => tile != null);

                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.cellLayout, Is.EqualTo(GridLayout.CellLayout.Isometric));
                Assert.That(presenter.World.Bounds, Is.EqualTo(new GridBounds(10, 10)));
                Assert.That(populatedCells, Is.EqualTo(presenter.World.Count));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rebuild_TwiceDoesNotDuplicateTheGeneratedHierarchy()
        {
            var root = new GameObject("Debug world rebuild test");

            try
            {
                var presenter = root.AddComponent<DebugIsometricWorld>();
                presenter.Rebuild();
                presenter.Rebuild();

                Assert.That(root.GetComponentsInChildren<Grid>(true), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Tilemap>(true), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<TilemapRenderer>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
