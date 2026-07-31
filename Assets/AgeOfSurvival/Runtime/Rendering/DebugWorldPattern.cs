using System;
using AgeOfSurvival.Core.World;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Creates deterministic placeholder data used only to validate the Core-to-Unity rendering boundary.
    /// The byte values are debug categories, not production terrain definitions.
    /// </summary>
    public static class DebugWorldPattern
    {
        public const byte BaseCell = 0;
        public const byte AccentCell = 1;
        public const byte BorderCell = 2;

        public static DenseGrid<byte> Create(int width, int height)
        {
            var bounds = new GridBounds(width, height);
            var grid = new DenseGrid<byte>(bounds);

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    grid[position] = SelectCellValue(position, bounds);
                }
            }

            return grid;
        }

        public static byte SelectCellValue(GridPosition position, GridBounds bounds)
        {
            if (!bounds.Contains(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "The position is outside the debug world bounds.");
            }

            bool isBorder = position.X == 0
                || position.Y == 0
                || position.X == bounds.Width - 1
                || position.Y == bounds.Height - 1;

            if (isBorder)
            {
                return BorderCell;
            }

            return (position.X + position.Y) % 4 == 0
                ? AccentCell
                : BaseCell;
        }
    }
}
