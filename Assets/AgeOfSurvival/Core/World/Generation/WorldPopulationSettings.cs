using System;

namespace AgeOfSurvival.Core.World.Generation
{
    /// <summary>
    /// Stable identifier for one authored population profile. Saved worlds must retain
    /// both the identifier and its revision alongside the generator version.
    /// </summary>
    public readonly struct WorldPopulationProfileId : IEquatable<WorldPopulationProfileId>, IComparable<WorldPopulationProfileId>
    {
        public WorldPopulationProfileId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A population profile identifier must not be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public int CompareTo(WorldPopulationProfileId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(WorldPopulationProfileId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPopulationProfileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(WorldPopulationProfileId left, WorldPopulationProfileId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldPopulationProfileId left, WorldPopulationProfileId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Immutable, save-facing values that describe one deterministic population profile.
    /// Thresholds use unsigned Q16 space: field values are in [0, 65535] and chance
    /// thresholds are in [0, 65536], where 0 means never and 65536 means always.
    /// </summary>
    public readonly struct WorldPopulationProfile : IEquatable<WorldPopulationProfile>
    {
        public const uint ProbabilityScale = 65536u;

        public WorldPopulationProfile(
            WorldPopulationProfileId id,
            int revision,
            int terrainScale,
            int soilScale,
            int zoneScale,
            ushort waterThreshold,
            ushort dirtThreshold,
            ushort woodedThreshold,
            uint openResourceChance,
            uint woodedResourceChance,
            int resourceExclusionRadius,
            int spawnClearanceRadius,
            int spawnSearchRadius)
            : this(
                id,
                revision,
                terrainScale,
                soilScale,
                zoneScale,
                waterThreshold,
                dirtThreshold,
                woodedThreshold,
                openResourceChance,
                woodedResourceChance,
                resourceExclusionRadius,
                spawnClearanceRadius,
                spawnSearchRadius,
                ResourceKindDistribution.ShrubsOnly)
        {
        }

        public WorldPopulationProfile(
            WorldPopulationProfileId id,
            int revision,
            int terrainScale,
            int soilScale,
            int zoneScale,
            ushort waterThreshold,
            ushort dirtThreshold,
            ushort woodedThreshold,
            uint openResourceChance,
            uint woodedResourceChance,
            int resourceExclusionRadius,
            int spawnClearanceRadius,
            int spawnSearchRadius,
            ResourceKindDistribution resourceKinds)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A valid population profile identifier is required.", nameof(id));
            }

            if (revision <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), revision, "A profile revision must be greater than zero.");
            }

            ValidateScale(terrainScale, nameof(terrainScale));
            ValidateScale(soilScale, nameof(soilScale));
            ValidateScale(zoneScale, nameof(zoneScale));
            ValidateChance(openResourceChance, nameof(openResourceChance));
            ValidateChance(woodedResourceChance, nameof(woodedResourceChance));
            if (!resourceKinds.IsValid)
            {
                throw new ArgumentException(
                    "A valid natural-resource kind distribution is required.",
                    nameof(resourceKinds));
            }

            if (resourceExclusionRadius < 1 || resourceExclusionRadius > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resourceExclusionRadius),
                    resourceExclusionRadius,
                    "The resource exclusion radius must be between 1 and 64 cells.");
            }

            if (spawnClearanceRadius < 0 || spawnClearanceRadius > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnClearanceRadius),
                    spawnClearanceRadius,
                    "The spawn clearance radius must be between 0 and 64 cells.");
            }

            if (spawnSearchRadius < 0 || spawnSearchRadius > 4096)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnSearchRadius),
                    spawnSearchRadius,
                    "The spawn search radius must be between 0 and 4096 cells.");
            }

            Id = id;
            Revision = revision;
            TerrainScale = terrainScale;
            SoilScale = soilScale;
            ZoneScale = zoneScale;
            WaterThreshold = waterThreshold;
            DirtThreshold = dirtThreshold;
            WoodedThreshold = woodedThreshold;
            OpenResourceChance = openResourceChance;
            WoodedResourceChance = woodedResourceChance;
            ResourceKinds = resourceKinds;
            ResourceExclusionRadius = resourceExclusionRadius;
            SpawnClearanceRadius = spawnClearanceRadius;
            SpawnSearchRadius = spawnSearchRadius;
        }

        public WorldPopulationProfileId Id { get; }
        public int Revision { get; }
        public int TerrainScale { get; }
        public int SoilScale { get; }
        public int ZoneScale { get; }
        public ushort WaterThreshold { get; }
        public ushort DirtThreshold { get; }
        public ushort WoodedThreshold { get; }
        public uint OpenResourceChance { get; }
        public uint WoodedResourceChance { get; }
        public ResourceKindDistribution ResourceKinds { get; }
        public int ResourceExclusionRadius { get; }
        public int SpawnClearanceRadius { get; }
        public int SpawnSearchRadius { get; }

        public bool IsValid => Id.IsValid
            && Revision > 0
            && TerrainScale >= 2
            && TerrainScale <= 4096
            && SoilScale >= 2
            && SoilScale <= 4096
            && ZoneScale >= 2
            && ZoneScale <= 4096
            && OpenResourceChance <= ProbabilityScale
            && WoodedResourceChance <= ProbabilityScale
            && ResourceKinds.IsValid
            && ResourceExclusionRadius >= 1
            && ResourceExclusionRadius <= 64
            && SpawnClearanceRadius >= 0
            && SpawnClearanceRadius <= 64
            && SpawnSearchRadius >= 0
            && SpawnSearchRadius <= 4096;

        public bool Equals(WorldPopulationProfile other)
        {
            return Id.Equals(other.Id)
                && Revision == other.Revision
                && TerrainScale == other.TerrainScale
                && SoilScale == other.SoilScale
                && ZoneScale == other.ZoneScale
                && WaterThreshold == other.WaterThreshold
                && DirtThreshold == other.DirtThreshold
                && WoodedThreshold == other.WoodedThreshold
                && OpenResourceChance == other.OpenResourceChance
                && WoodedResourceChance == other.WoodedResourceChance
                && ResourceKinds.Equals(other.ResourceKinds)
                && ResourceExclusionRadius == other.ResourceExclusionRadius
                && SpawnClearanceRadius == other.SpawnClearanceRadius
                && SpawnSearchRadius == other.SpawnSearchRadius;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPopulationProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ Revision;
                hash = (hash * 397) ^ TerrainScale;
                hash = (hash * 397) ^ SoilScale;
                hash = (hash * 397) ^ ZoneScale;
                hash = (hash * 397) ^ WaterThreshold;
                hash = (hash * 397) ^ DirtThreshold;
                hash = (hash * 397) ^ WoodedThreshold;
                hash = (hash * 397) ^ (int)OpenResourceChance;
                hash = (hash * 397) ^ (int)WoodedResourceChance;
                hash = (hash * 397) ^ ResourceKinds.GetHashCode();
                hash = (hash * 397) ^ ResourceExclusionRadius;
                hash = (hash * 397) ^ SpawnClearanceRadius;
                hash = (hash * 397) ^ SpawnSearchRadius;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Id}@{Revision}";
        }

        public static bool operator ==(WorldPopulationProfile left, WorldPopulationProfile right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldPopulationProfile left, WorldPopulationProfile right)
        {
            return !left.Equals(right);
        }

        private static void ValidateScale(int value, string parameterName)
        {
            if (value < 2 || value > 4096)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A population field scale must be between 2 and 4096 cells.");
            }
        }

        private static void ValidateChance(uint value, string parameterName)
        {
            if (value > ProbabilityScale)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"A probability threshold must be between 0 and {ProbabilityScale}.");
            }
        }
    }

    /// <summary>
    /// Complete immutable contract for deterministic terrain, population and spawn.
    /// </summary>
    public readonly struct WorldPopulationSettings : IEquatable<WorldPopulationSettings>
    {
        public WorldPopulationSettings(
            WorldGenerationSettings generation,
            WorldPopulationProfile profile)
        {
            if (!generation.Version.Equals(WorldGeneratorVersions.PopulationV1))
            {
                throw new ArgumentException(
                    $"Population V1 requires generator version {WorldGeneratorVersions.PopulationV1}.",
                    nameof(generation));
            }

            if (!profile.IsValid)
            {
                throw new ArgumentException("A valid population profile is required.", nameof(profile));
            }

            Generation = generation;
            Profile = profile;
        }

        public WorldGenerationSettings Generation { get; }
        public WorldPopulationProfile Profile { get; }

        public bool Equals(WorldPopulationSettings other)
        {
            return Generation.Equals(other.Generation)
                && Profile.Equals(other.Profile);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPopulationSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Generation.GetHashCode() * 397) ^ Profile.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Generation}; profile={Profile}";
        }

        public static bool operator ==(WorldPopulationSettings left, WorldPopulationSettings right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldPopulationSettings left, WorldPopulationSettings right)
        {
            return !left.Equals(right);
        }
    }

    public static class WorldPopulationDefaults
    {
        public static readonly WorldPopulationProfileId TemperatePrototypeV1Id =
            new WorldPopulationProfileId("temperate-prototype");

        /// <summary>
        /// Provisional values for the first readable biome. They are deliberately isolated
        /// from the algorithm so Naël can rebalance them without changing Runtime adapters.
        /// Any persisted change requires a new profile revision or generator version.
        /// </summary>
        public static readonly WorldPopulationProfile TemperatePrototypeV1 =
            new WorldPopulationProfile(
                TemperatePrototypeV1Id,
                1,
                terrainScale: 24,
                soilScale: 11,
                zoneScale: 16,
                waterThreshold: 12288,
                dirtThreshold: 19660,
                woodedThreshold: 34406,
                openResourceChance: 2621,
                woodedResourceChance: 7864,
                resourceExclusionRadius: 2,
                spawnClearanceRadius: 1,
                spawnSearchRadius: 48);

        public static readonly WorldPopulationProfile TemperatePrototypeV2 =
            new WorldPopulationProfile(
                TemperatePrototypeV1Id,
                2,
                TemperatePrototypeV1.TerrainScale,
                TemperatePrototypeV1.SoilScale,
                TemperatePrototypeV1.ZoneScale,
                TemperatePrototypeV1.WaterThreshold,
                TemperatePrototypeV1.DirtThreshold,
                TemperatePrototypeV1.WoodedThreshold,
                TemperatePrototypeV1.OpenResourceChance,
                TemperatePrototypeV1.WoodedResourceChance,
                TemperatePrototypeV1.ResourceExclusionRadius,
                TemperatePrototypeV1.SpawnClearanceRadius,
                TemperatePrototypeV1.SpawnSearchRadius,
                ResourceKindDistribution.TemperateNaturalV2);

        public static WorldPopulationSettings CreateTemperatePrototypeV1(WorldSeed seed)
        {
            return new WorldPopulationSettings(
                WorldGenerationDefaults.CreatePopulationV1(seed),
                TemperatePrototypeV1);
        }

        public static WorldPopulationSettings CreateTemperatePrototypeV2(WorldSeed seed)
        {
            return new WorldPopulationSettings(
                WorldGenerationDefaults.CreatePopulationV1(seed),
                TemperatePrototypeV2);
        }

        public static WorldPopulationSettings CreateTemperatePrototypeCurrent(
            WorldSeed seed)
        {
            return CreateTemperatePrototypeV2(seed);
        }
    }
}
