using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World;

namespace AgeOfSurvival.Core.World.Generation
{
    public interface IWorldPopulationGenerator
    {
        WorldPopulationSettings Settings { get; }
        GeneratedCellData SampleCell(WorldCellCoordinate worldCell);
        bool HasGeneratedResourceAt(WorldCellCoordinate worldCell);
        PopulatedChunk Generate(ChunkCoordinate coordinate);
        bool TryFindSpawnNear(
            WorldCellCoordinate preferredCell,
            out GeneratedSpawnPoint spawnPoint);
    }

    /// <summary>
    /// Stateless deterministic terrain, resource and spawn generator.
    /// Every decision is addressed by absolute world cell and named generation stream.
    /// Resource spacing uses deterministic priority thinning: among nearby candidates,
    /// only the lowest priority (then stable coordinate tie-break) survives. This keeps
    /// results independent from chunk boundaries and generation order.
    /// </summary>
    public sealed class DeterministicWorldPopulationGenerator : IWorldPopulationGenerator
    {
        public DeterministicWorldPopulationGenerator(WorldPopulationSettings settings)
        {
            if (!DeterministicWorldSampler.Supports(settings.Generation.Version))
            {
                throw new NotSupportedException(
                    $"World generator version {settings.Generation.Version} is not supported by this build.");
            }

            if (!settings.Generation.Version.Equals(WorldGeneratorVersions.PopulationV1))
            {
                throw new NotSupportedException(
                    $"Population generation requires version {WorldGeneratorVersions.PopulationV1}.");
            }

            Settings = settings;
        }

        public WorldPopulationSettings Settings { get; }

        public GeneratedCellData SampleCell(WorldCellCoordinate worldCell)
        {
            WorldGenerationSettings generation = Settings.Generation;
            WorldPopulationProfile profile = Settings.Profile;

            ushort terrainField = DeterministicWorldFields.SampleSmoothed16(
                generation,
                worldCell,
                profile.TerrainScale,
                GenerationStreams.TerrainElevation);
            if (terrainField < profile.WaterThreshold)
            {
                return new GeneratedCellData(
                    GeneratedTerrainKind.Water,
                    GeneratedLandscapeZone.None);
            }

            ushort soilField = DeterministicWorldFields.SampleSmoothed16(
                generation,
                worldCell,
                profile.SoilScale,
                GenerationStreams.TerrainSoil);
            GeneratedTerrainKind terrain = soilField < profile.DirtThreshold
                ? GeneratedTerrainKind.Dirt
                : GeneratedTerrainKind.Grass;

            ushort zoneField = DeterministicWorldFields.SampleSmoothed16(
                generation,
                worldCell,
                profile.ZoneScale,
                GenerationStreams.LandscapeZone);
            GeneratedLandscapeZone zone = zoneField >= profile.WoodedThreshold
                ? GeneratedLandscapeZone.Wooded
                : GeneratedLandscapeZone.Open;

            return new GeneratedCellData(terrain, zone);
        }

        public bool HasGeneratedResourceAt(WorldCellCoordinate worldCell)
        {
            if (!IsResourceCandidate(worldCell))
            {
                return false;
            }

            ulong priority = SampleResourcePriority(worldCell);
            int radius = Settings.Profile.ResourceExclusionRadius;
            int radiusSquared = checked(radius * radius);

            for (int deltaY = -radius; deltaY <= radius; deltaY++)
            {
                for (int deltaX = -radius; deltaX <= radius; deltaX++)
                {
                    if (deltaX == 0 && deltaY == 0)
                    {
                        continue;
                    }

                    int distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    if (!TryOffset(worldCell, deltaX, deltaY, out WorldCellCoordinate neighbour)
                        || !IsResourceCandidate(neighbour))
                    {
                        continue;
                    }

                    ulong neighbourPriority = SampleResourcePriority(neighbour);
                    if (neighbourPriority < priority
                        || (neighbourPriority == priority && neighbour.CompareTo(worldCell) < 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public PopulatedChunk Generate(ChunkCoordinate coordinate)
        {
            ChunkLayout layout = Settings.Generation.ChunkLayout;
            var cells = new GeneratedCellData[layout.CellCount];
            var resources = new List<GeneratedResourcePlacement>();

            for (int index = 0; index < cells.Length; index++)
            {
                GridPosition localPosition = layout.Bounds.FromIndex(index);
                WorldCellCoordinate worldCell = ChunkAddressing.ToWorld(
                    coordinate,
                    localPosition,
                    layout);
                cells[index] = SampleCell(worldCell);

                if (HasGeneratedResourceAt(worldCell))
                {
                    GeneratedResourceKind kind =
                        SampleGeneratedResourceKind(worldCell);
                    resources.Add(new GeneratedResourcePlacement(
                        GeneratedResourceIds.Create(
                            Settings,
                            kind,
                            worldCell),
                        kind,
                        worldCell));
                }
            }

            return new PopulatedChunk(
                Settings,
                coordinate,
                cells,
                resources.ToArray());
        }

        public bool TryFindSpawnNear(
            WorldCellCoordinate preferredCell,
            out GeneratedSpawnPoint spawnPoint)
        {
            int maximumRadius = Settings.Profile.SpawnSearchRadius;
            for (int radius = 0; radius <= maximumRadius; radius++)
            {
                bool found = false;
                WorldCellCoordinate bestCell = default(WorldCellCoordinate);
                ulong bestPriority = ulong.MaxValue;

                VisitRing(preferredCell, radius, candidate =>
                {
                    if (!IsValidSpawnCell(candidate))
                    {
                        return;
                    }

                    ulong priority = DeterministicWorldSampler.Sample64(
                        Settings.Generation,
                        candidate,
                        GenerationStreams.SpawnPriority);
                    if (!found
                        || priority < bestPriority
                        || (priority == bestPriority && candidate.CompareTo(bestCell) < 0))
                    {
                        found = true;
                        bestCell = candidate;
                        bestPriority = priority;
                    }
                });

                if (found)
                {
                    spawnPoint = new GeneratedSpawnPoint(bestCell);
                    return true;
                }
            }

            spawnPoint = default(GeneratedSpawnPoint);
            return false;
        }

        private bool IsResourceCandidate(WorldCellCoordinate worldCell)
        {
            GeneratedCellData cell = SampleCell(worldCell);
            if (!cell.IsLand)
            {
                return false;
            }

            uint chance = cell.Zone == GeneratedLandscapeZone.Wooded
                ? Settings.Profile.WoodedResourceChance
                : Settings.Profile.OpenResourceChance;
            if (chance == 0u)
            {
                return false;
            }

            if (chance >= WorldPopulationProfile.ProbabilityScale)
            {
                return true;
            }

            ushort sample = DeterministicWorldFields.Sample16(
                Settings.Generation,
                worldCell,
                GenerationStreams.ResourceCandidate);
            return sample < chance;
        }

        public GeneratedResourceKind SampleGeneratedResourceKind(
            WorldCellCoordinate worldCell)
        {
            GeneratedCellData cell = SampleCell(worldCell);
            if (!cell.IsLand)
            {
                throw new ArgumentException(
                    "A generated natural-resource kind can only be sampled on land.",
                    nameof(worldCell));
            }

            ushort sample = DeterministicWorldFields.Sample16(
                Settings.Generation,
                worldCell,
                GenerationStreams.ResourceKind);
            return Settings.Profile.ResourceKinds.Select(
                cell.Zone,
                sample);
        }

        private ulong SampleResourcePriority(WorldCellCoordinate worldCell)
        {
            return DeterministicWorldSampler.Sample64(
                Settings.Generation,
                worldCell,
                GenerationStreams.ResourcePriority);
        }

        private bool IsValidSpawnCell(WorldCellCoordinate worldCell)
        {
            if (!SampleCell(worldCell).IsOpenLand)
            {
                return false;
            }

            int clearance = Settings.Profile.SpawnClearanceRadius;
            int clearanceSquared = checked(clearance * clearance);
            for (int deltaY = -clearance; deltaY <= clearance; deltaY++)
            {
                for (int deltaX = -clearance; deltaX <= clearance; deltaX++)
                {
                    int distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
                    if (distanceSquared > clearanceSquared)
                    {
                        continue;
                    }

                    if (TryOffset(worldCell, deltaX, deltaY, out WorldCellCoordinate neighbour)
                        && HasGeneratedResourceAt(neighbour))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void VisitRing(
            WorldCellCoordinate center,
            int radius,
            Action<WorldCellCoordinate> visitor)
        {
            if (radius == 0)
            {
                visitor(center);
                return;
            }

            for (int deltaX = -radius; deltaX <= radius; deltaX++)
            {
                if (TryOffset(center, deltaX, -radius, out WorldCellCoordinate bottom))
                {
                    visitor(bottom);
                }

                if (TryOffset(center, deltaX, radius, out WorldCellCoordinate top))
                {
                    visitor(top);
                }
            }

            for (int deltaY = -radius + 1; deltaY <= radius - 1; deltaY++)
            {
                if (TryOffset(center, -radius, deltaY, out WorldCellCoordinate left))
                {
                    visitor(left);
                }

                if (TryOffset(center, radius, deltaY, out WorldCellCoordinate right))
                {
                    visitor(right);
                }
            }
        }

        private static bool TryOffset(
            WorldCellCoordinate origin,
            int deltaX,
            int deltaY,
            out WorldCellCoordinate result)
        {
            try
            {
                result = origin.Offset(deltaX, deltaY);
                return true;
            }
            catch (OverflowException)
            {
                result = default(WorldCellCoordinate);
                return false;
            }
        }
    }

    /// <summary>
    /// Caller-owned cache for immutable populated chunks. It owns no player mutations,
    /// harvested state, save data or Unity objects.
    /// </summary>
    public sealed class OnDemandPopulatedWorld
    {
        private readonly IWorldPopulationGenerator _generator;
        private readonly Dictionary<ChunkCoordinate, PopulatedChunk> _loadedChunks =
            new Dictionary<ChunkCoordinate, PopulatedChunk>();

        public OnDemandPopulatedWorld(IWorldPopulationGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public WorldPopulationSettings Settings => _generator.Settings;
        public int LoadedChunkCount => _loadedChunks.Count;

        public PopulatedChunk GetOrGenerateChunk(ChunkCoordinate coordinate)
        {
            if (_loadedChunks.TryGetValue(coordinate, out PopulatedChunk existing))
            {
                return existing;
            }

            PopulatedChunk generated = _generator.Generate(coordinate);
            _loadedChunks.Add(coordinate, generated);
            return generated;
        }

        public bool TryGetLoadedChunk(
            ChunkCoordinate coordinate,
            out PopulatedChunk chunk)
        {
            return _loadedChunks.TryGetValue(coordinate, out chunk);
        }

        public bool UnloadChunk(ChunkCoordinate coordinate)
        {
            return _loadedChunks.Remove(coordinate);
        }

        public IReadOnlyList<ChunkCoordinate> CopyLoadedCoordinates()
        {
            var coordinates = new List<ChunkCoordinate>(_loadedChunks.Keys);
            coordinates.Sort();
            return coordinates.AsReadOnly();
        }

        public void Clear()
        {
            _loadedChunks.Clear();
        }
    }
}
