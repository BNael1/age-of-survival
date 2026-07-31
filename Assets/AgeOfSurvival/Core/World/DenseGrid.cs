using System;

namespace AgeOfSurvival.Core.World
{
    /// <summary>
    /// Dense row-major grid storage for simulation data.
    /// </summary>
    public sealed class DenseGrid<T>
    {
        private readonly T[] _cells;

        public DenseGrid(GridBounds bounds)
        {
            Bounds = bounds;
            _cells = new T[bounds.CellCount];
        }

        public GridBounds Bounds { get; }
        public int Count => _cells.Length;

        public T this[GridPosition position]
        {
            get => _cells[Bounds.ToIndex(position)];
            set => _cells[Bounds.ToIndex(position)] = value;
        }

        public T this[int index]
        {
            get
            {
                ValidateIndex(index);
                return _cells[index];
            }
            set
            {
                ValidateIndex(index);
                _cells[index] = value;
            }
        }

        public void Fill(T value)
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                _cells[index] = value;
            }
        }

        public T[] CopyCells()
        {
            var copy = new T[_cells.Length];
            Array.Copy(_cells, copy, _cells.Length);
            return copy;
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _cells.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the grid.");
            }
        }
    }
}
