using System;
using System.Collections.Generic;
using System.Globalization;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World;

namespace AgeOfSurvival.Core.World.Generation
{
    public enum GeneratedTerrainKind : byte
    {
        Grass = 0,
        Dirt = 1,
        Water = 2
    }

    public enum GeneratedLandscapeZone : byte
    {
        None = 0,
        Open = 1,
        Wooded = 2
    }

    public enum GeneratedResourceKind : byte
    {
        Shrub = 0,
        LooseStone = 1,
        Deadwood = 2,
        Tree = 3
    }

    public readonly struct GeneratedCellData : IEquatable<GeneratedCellData>
    {
        public GeneratedCellData(
            GeneratedTerrainKind terrain,
            GeneratedLandscapeZone zone)
        {
            if ((byte)terrain < (byte)GeneratedTerrainKind.Grass
                || (byte)terrain > (byte)GeneratedTerrainKind.Water)
            {
                throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unknown generated terrain kind.");
            }

            if ((byte)zone < (byte)GeneratedLandscapeZone.None
                || (byte)zone > (byte)GeneratedLandscapeZone.Wooded)
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "Unknown generated landscape zone.");
            }

            if (terrain != GeneratedTerrainKind.Water && zone == GeneratedLandscapeZone.None)
            {
                throw new ArgumentException("A generated land cell requires an open or wooded zone.", nameof(zone));
            }

            Terrain = terrain;
            Zone = terrain == GeneratedTerrainKind.Water
                ? GeneratedLandscapeZone.None
                : zone;
        }

        public GeneratedTerrainKind Terrain { get; }
        public GeneratedLandscapeZone Zone { get; }
        public bool IsLand => Terrain != GeneratedTerrainKind.Water;
        public bool IsOpenLand => IsLand && Zone == GeneratedLandscapeZone.Open;

        public bool Equals(GeneratedCellData other)
        {
            return Terrain == other.Terrain && Zone == other.Zone;
        }

        public override bool Equals(object obj)
        {
            return obj is GeneratedCellData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Terrain * 397) ^ (int)Zone;
        }

        public override string ToString()
        {
            return $"{Terrain}/{Zone}";
        }

        public static bool operator ==(GeneratedCellData left, GeneratedCellData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeneratedCellData left, GeneratedCellData right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct GeneratedResourcePlacement : IEquatable<GeneratedResourcePlacement>
    {
        public GeneratedResourcePlacement(
            ResourceId id,
            GeneratedResourceKind kind,
            WorldCellCoordinate cell)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A generated resource requires a valid stable identifier.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(GeneratedResourceKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown generated resource kind.");
            }

            Id = id;
            Kind = kind;
            Cell = cell;
        }

        public ResourceId Id { get; }
        public GeneratedResourceKind Kind { get; }
        public WorldCellCoordinate Cell { get; }

        public bool Equals(GeneratedResourcePlacement other)
        {
            return Id.Equals(other.Id)
                && Kind == other.Kind
                && Cell.Equals(other.Cell);
        }

        public override bool Equals(object obj)
        {
            return obj is GeneratedResourcePlacement other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Cell.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Id}:{Kind}@{Cell}";
        }

        public static bool operator ==(GeneratedResourcePlacement left, GeneratedResourcePlacement right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeneratedResourcePlacement left, GeneratedResourcePlacement right)
        {
            return !left.Equals(right);
        }
    }

    public static class GeneratedResourceIds
    {
        public static ResourceId Create(
            WorldPopulationSettings settings,
            GeneratedResourceKind kind,
            WorldCellCoordinate cell)
        {
            string value = string.Format(
                CultureInfo.InvariantCulture,
                "generated:{0:X16}:{1}:{2}:{3}:{4}:{5}:{6}",
                settings.Generation.Seed.Value,
                settings.Generation.Version.Value,
                settings.Profile.Id.Value,
                settings.Profile.Revision,
                (byte)kind,
                cell.X,
                cell.Y);
            return new ResourceId(value);
        }
    }

    public readonly struct GeneratedSpawnPoint : IEquatable<GeneratedSpawnPoint>
    {
        public GeneratedSpawnPoint(WorldCellCoordinate cell)
        {
            Cell = cell;
        }

        public WorldCellCoordinate Cell { get; }

        public bool Equals(GeneratedSpawnPoint other)
        {
            return Cell.Equals(other.Cell);
        }

        public override bool Equals(object obj)
        {
            return obj is GeneratedSpawnPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Cell.GetHashCode();
        }

        public override string ToString()
        {
            return $"spawn@{Cell}";
        }

        public static bool operator ==(GeneratedSpawnPoint left, GeneratedSpawnPoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeneratedSpawnPoint left, GeneratedSpawnPoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Immutable generated terrain and resource baseline for one chunk.
    /// Player changes remain in separate modification/state layers.
    /// </summary>
    public sealed class PopulatedChunk
    {
        private readonly GeneratedCellData[] _cells;
        private readonly GeneratedResourcePlacement[] _resources;
        private readonly IReadOnlyList<GeneratedResourcePlacement> _readOnlyResources;

        internal PopulatedChunk(
            WorldPopulationSettings settings,
            ChunkCoordinate coordinate,
            GeneratedCellData[] cells,
            GeneratedResourcePlacement[] resources)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            if (cells.Length != settings.Generation.ChunkLayout.CellCount)
            {
                throw new ArgumentException("The cell count must match the chunk layout.", nameof(cells));
            }

            Settings = settings;
            Coordinate = coordinate;
            _cells = new GeneratedCellData[cells.Length];
            Array.Copy(cells, _cells, cells.Length);
            _resources = new GeneratedResourcePlacement[resources.Length];
            Array.Copy(resources, _resources, resources.Length);
            _readOnlyResources = Array.AsReadOnly(_resources);
        }

        public WorldPopulationSettings Settings { get; }
        public ChunkCoordinate Coordinate { get; }
        public ChunkLayout Layout => Settings.Generation.ChunkLayout;
        public int Count => _cells.Length;
        public IReadOnlyList<GeneratedResourcePlacement> Resources => _readOnlyResources;

        public GeneratedCellData GetCell(GridPosition localPosition)
        {
            return _cells[Layout.Bounds.ToIndex(localPosition)];
        }

        public WorldCellCoordinate GetWorldCell(GridPosition localPosition)
        {
            return ChunkAddressing.ToWorld(Coordinate, localPosition, Layout);
        }

        public GeneratedCellData[] CopyCells()
        {
            var copy = new GeneratedCellData[_cells.Length];
            Array.Copy(_cells, copy, _cells.Length);
            return copy;
        }

        public GeneratedResourcePlacement[] CopyResources()
        {
            var copy = new GeneratedResourcePlacement[_resources.Length];
            Array.Copy(_resources, copy, _resources.Length);
            return copy;
        }
    }
}
