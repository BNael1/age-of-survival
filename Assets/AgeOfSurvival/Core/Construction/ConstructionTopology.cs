using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Construction
{
    /// <summary>
    /// Stable cardinal side order, aligned with the logical grid convention:
    /// north, east, south, west.
    /// </summary>
    public enum ConstructionCellSide
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    /// <summary>
    /// Canonical axis of one undirected cell edge.
    /// Horizontal edges separate south/north cells; vertical edges separate west/east cells.
    /// </summary>
    public enum ConstructionEdgeAxis
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>
    /// Independent occupancy channels within the same world cell.
    /// Assignment of concrete construction definitions to these channels belongs to later lots.
    /// </summary>
    public enum ConstructionSpaceKind
    {
        Surface = 0,
        Interior = 1,
        Edge = 2,
        Roof = 3
    }

    /// <summary>
    /// Canonical undirected edge between two adjacent world cells.
    /// East/West aliases and North/South aliases collapse to the same value.
    /// </summary>
    public readonly struct ConstructionEdgeAddress :
        IEquatable<ConstructionEdgeAddress>,
        IComparable<ConstructionEdgeAddress>
    {
        private readonly byte _initialized;

        private ConstructionEdgeAddress(
            WorldCellCoordinate anchorCell,
            ConstructionEdgeAxis axis)
        {
            AnchorCell = anchorCell;
            Axis = axis;
            _initialized = 1;
        }

        public WorldCellCoordinate AnchorCell { get; }
        public ConstructionEdgeAxis Axis { get; }
        public bool IsValid => _initialized == 1 && Enum.IsDefined(typeof(ConstructionEdgeAxis), Axis);

        /// <summary>
        /// Creates one canonical edge from a cell and one of its four sides.
        /// The anchor is always the south cell for a horizontal edge and the west cell for a vertical edge.
        /// </summary>
        public static ConstructionEdgeAddress Create(
            WorldCellCoordinate cell,
            ConstructionCellSide side)
        {
            if (!Enum.IsDefined(typeof(ConstructionCellSide), side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown construction cell side.");
            }

            switch (side)
            {
                case ConstructionCellSide.North:
                    // Validate that the adjacent cell remains representable in the Int64 world domain.
                    cell.Offset(0L, 1L);
                    return new ConstructionEdgeAddress(cell, ConstructionEdgeAxis.Horizontal);

                case ConstructionCellSide.East:
                    cell.Offset(1L, 0L);
                    return new ConstructionEdgeAddress(cell, ConstructionEdgeAxis.Vertical);

                case ConstructionCellSide.South:
                    return new ConstructionEdgeAddress(
                        cell.Offset(0L, -1L),
                        ConstructionEdgeAxis.Horizontal);

                case ConstructionCellSide.West:
                    return new ConstructionEdgeAddress(
                        cell.Offset(-1L, 0L),
                        ConstructionEdgeAxis.Vertical);

                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown construction cell side.");
            }
        }

        /// <summary>
        /// Attempts to create a canonical edge while treating a neighbour outside the Int64 world domain
        /// as non-representable. Unknown sides remain programming errors and are rejected.
        /// </summary>
        public static bool TryCreate(
            WorldCellCoordinate cell,
            ConstructionCellSide side,
            out ConstructionEdgeAddress edge)
        {
            try
            {
                edge = Create(cell, side);
                return true;
            }
            catch (OverflowException)
            {
                edge = default;
                return false;
            }
        }

        public WorldCellCoordinate FirstCell
        {
            get
            {
                EnsureValid();
                return AnchorCell;
            }
        }

        public WorldCellCoordinate SecondCell
        {
            get
            {
                EnsureValid();
                return Axis == ConstructionEdgeAxis.Horizontal
                    ? AnchorCell.Offset(0L, 1L)
                    : AnchorCell.Offset(1L, 0L);
            }
        }

        /// <summary>
        /// Shared edges belong to the chunk containing the canonical anchor cell.
        /// This prevents duplicate ownership at chunk seams.
        /// </summary>
        public ChunkCoordinate GetOwningChunk(ChunkLayout layout)
        {
            EnsureValid();
            return ChunkAddressing.Locate(AnchorCell, layout).Chunk;
        }

        public int CompareTo(ConstructionEdgeAddress other)
        {
            EnsureValid();
            other.EnsureValid();
            int cellComparison = AnchorCell.CompareTo(other.AnchorCell);
            return cellComparison != 0 ? cellComparison : Axis.CompareTo(other.Axis);
        }

        public bool Equals(ConstructionEdgeAddress other)
        {
            return _initialized == other._initialized
                && AnchorCell.Equals(other.AnchorCell)
                && Axis == other.Axis;
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructionEdgeAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _initialized;
                hash = (hash * 397) ^ AnchorCell.GetHashCode();
                return (hash * 397) ^ (int)Axis;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"{AnchorCell}:{Axis}" : "<invalid-edge>";
        }

        public static bool operator ==(ConstructionEdgeAddress left, ConstructionEdgeAddress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConstructionEdgeAddress left, ConstructionEdgeAddress right)
        {
            return !left.Equals(right);
        }

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("A valid construction edge address is required.");
            }
        }
    }

    /// <summary>
    /// Canonical key for one independently occupiable construction space.
    /// Surface, interior and roof spaces may coexist on the same cell; edges use canonical edge addressing.
    /// </summary>
    public readonly struct ConstructionSpaceKey :
        IEquatable<ConstructionSpaceKey>,
        IComparable<ConstructionSpaceKey>
    {
        private readonly byte _initialized;
        private readonly WorldCellCoordinate _cell;
        private readonly ConstructionEdgeAddress _edge;

        private ConstructionSpaceKey(
            ConstructionSpaceKind kind,
            WorldCellCoordinate cell,
            ConstructionEdgeAddress edge)
        {
            Kind = kind;
            _cell = cell;
            _edge = edge;
            _initialized = 1;
        }

        public ConstructionSpaceKind Kind { get; }
        public bool IsValid =>
            _initialized == 1
            && Enum.IsDefined(typeof(ConstructionSpaceKind), Kind)
            && (Kind != ConstructionSpaceKind.Edge || _edge.IsValid);

        public WorldCellCoordinate AnchorCell
        {
            get
            {
                EnsureValid();
                return Kind == ConstructionSpaceKind.Edge ? _edge.AnchorCell : _cell;
            }
        }

        public ConstructionEdgeAddress EdgeAddress
        {
            get
            {
                EnsureValid();
                if (Kind != ConstructionSpaceKind.Edge)
                {
                    throw new InvalidOperationException("Only an edge construction space exposes an edge address.");
                }

                return _edge;
            }
        }

        public static ConstructionSpaceKey Surface(WorldCellCoordinate cell)
        {
            return new ConstructionSpaceKey(ConstructionSpaceKind.Surface, cell, default);
        }

        public static ConstructionSpaceKey Interior(WorldCellCoordinate cell)
        {
            return new ConstructionSpaceKey(ConstructionSpaceKind.Interior, cell, default);
        }

        public static ConstructionSpaceKey Roof(WorldCellCoordinate cell)
        {
            return new ConstructionSpaceKey(ConstructionSpaceKind.Roof, cell, default);
        }

        public static ConstructionSpaceKey Edge(ConstructionEdgeAddress edge)
        {
            if (!edge.IsValid)
            {
                throw new ArgumentException("A valid construction edge is required.", nameof(edge));
            }

            return new ConstructionSpaceKey(ConstructionSpaceKind.Edge, default, edge);
        }

        public static ConstructionSpaceKey Edge(
            WorldCellCoordinate cell,
            ConstructionCellSide side)
        {
            return Edge(ConstructionEdgeAddress.Create(cell, side));
        }

        public ChunkCoordinate GetOwningChunk(ChunkLayout layout)
        {
            EnsureValid();
            return Kind == ConstructionSpaceKind.Edge
                ? _edge.GetOwningChunk(layout)
                : ChunkAddressing.Locate(_cell, layout).Chunk;
        }

        public int CompareTo(ConstructionSpaceKey other)
        {
            EnsureValid();
            other.EnsureValid();

            int cellComparison = AnchorCell.CompareTo(other.AnchorCell);
            if (cellComparison != 0)
            {
                return cellComparison;
            }

            int kindComparison = Kind.CompareTo(other.Kind);
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            return Kind == ConstructionSpaceKind.Edge
                ? _edge.Axis.CompareTo(other._edge.Axis)
                : 0;
        }

        public bool Equals(ConstructionSpaceKey other)
        {
            if (_initialized != other._initialized || Kind != other.Kind)
            {
                return false;
            }

            if (_initialized == 0)
            {
                return true;
            }

            return Kind == ConstructionSpaceKind.Edge
                ? _edge.Equals(other._edge)
                : _cell.Equals(other._cell);
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructionSpaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _initialized;
                hash = (hash * 397) ^ (int)Kind;
                return Kind == ConstructionSpaceKind.Edge
                    ? (hash * 397) ^ _edge.GetHashCode()
                    : (hash * 397) ^ _cell.GetHashCode();
            }
        }

        public override string ToString()
        {
            if (!IsValid)
            {
                return "<invalid-construction-space>";
            }

            return Kind == ConstructionSpaceKind.Edge
                ? $"{Kind}:{_edge}"
                : $"{Kind}:{_cell}";
        }

        public static bool operator ==(ConstructionSpaceKey left, ConstructionSpaceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConstructionSpaceKey left, ConstructionSpaceKey right)
        {
            return !left.Equals(right);
        }

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("A valid construction space key is required.");
            }
        }
    }

    /// <summary>
    /// Minimal deterministic occupancy registry for construction spaces.
    /// It deliberately stores no gameplay state; sites and completed structures are introduced later.
    /// </summary>
    public sealed class ConstructionOccupancyRegistry
    {
        private readonly HashSet<ConstructionSpaceKey> _occupied =
            new HashSet<ConstructionSpaceKey>();

        public int Count => _occupied.Count;

        public bool IsOccupied(ConstructionSpaceKey space)
        {
            Validate(space);
            return _occupied.Contains(space);
        }

        public bool TryOccupy(ConstructionSpaceKey space)
        {
            Validate(space);
            return _occupied.Add(space);
        }

        public bool TryVacate(ConstructionSpaceKey space)
        {
            Validate(space);
            return _occupied.Remove(space);
        }

        public ConstructionSpaceKey[] CaptureCanonicalSpaces()
        {
            var spaces = new List<ConstructionSpaceKey>(_occupied);
            spaces.Sort();
            return spaces.ToArray();
        }

        private static void Validate(ConstructionSpaceKey space)
        {
            if (!space.IsValid)
            {
                throw new ArgumentException("A valid construction space is required.", nameof(space));
            }
        }
    }
}
