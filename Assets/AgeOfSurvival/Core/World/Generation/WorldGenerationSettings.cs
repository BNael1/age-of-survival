using System;
using System.Globalization;

namespace AgeOfSurvival.Core.World.Generation
{
    /// <summary>
    /// Canonical 64-bit seed used to identify one generated world.
    /// </summary>
    public readonly struct WorldSeed : IEquatable<WorldSeed>, IComparable<WorldSeed>
    {
        public WorldSeed(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public int CompareTo(WorldSeed other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(WorldSeed other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldSeed other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(Value ^ (Value >> 32)));
        }

        public override string ToString()
        {
            return $"0x{Value:X16}";
        }

        public static WorldSeed Parse(string text)
        {
            if (!TryParse(text, out WorldSeed seed))
            {
                throw new FormatException("A world seed must be an unsigned decimal integer or a hexadecimal value prefixed with 0x.");
            }

            return seed;
        }

        public static bool TryParse(string text, out WorldSeed seed)
        {
            seed = default(WorldSeed);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim();
            NumberStyles style = NumberStyles.Integer;
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(2);
                style = NumberStyles.AllowHexSpecifier;
            }

            if (normalized.Length == 0
                || !ulong.TryParse(normalized, style, CultureInfo.InvariantCulture, out ulong value))
            {
                return false;
            }

            seed = new WorldSeed(value);
            return true;
        }

        public static bool operator ==(WorldSeed left, WorldSeed right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldSeed left, WorldSeed right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Explicit algorithm contract. A saved world must retain this value.
    /// </summary>
    public readonly struct WorldGeneratorVersion : IEquatable<WorldGeneratorVersion>, IComparable<WorldGeneratorVersion>
    {
        public WorldGeneratorVersion(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A generator version must be greater than zero.");
            }

            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(WorldGeneratorVersion other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(WorldGeneratorVersion other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldGeneratorVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        public static bool operator ==(WorldGeneratorVersion left, WorldGeneratorVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldGeneratorVersion left, WorldGeneratorVersion right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Stable domain identifier for independent generation decisions.
    /// </summary>
    public readonly struct GenerationStream : IEquatable<GenerationStream>, IComparable<GenerationStream>
    {
        public GenerationStream(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(GenerationStream other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(GenerationStream other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GenerationStream other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)Value);
        }

        public override string ToString()
        {
            return $"0x{Value:X8}";
        }

        public static bool operator ==(GenerationStream left, GenerationStream right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GenerationStream left, GenerationStream right)
        {
            return !left.Equals(right);
        }
    }

    public static class WorldGeneratorVersions
    {
        public static readonly WorldGeneratorVersion FoundationV1 = new WorldGeneratorVersion(1);
        public static readonly WorldGeneratorVersion PopulationV1 = new WorldGeneratorVersion(2);
    }

    public static class GenerationStreams
    {
        public static readonly GenerationStream Foundation = new GenerationStream(0u);
        public static readonly GenerationStream TerrainElevation = new GenerationStream(0x54455241u);
        public static readonly GenerationStream TerrainSoil = new GenerationStream(0x534F494Cu);
        public static readonly GenerationStream LandscapeZone = new GenerationStream(0x5A4F4E45u);
        public static readonly GenerationStream ResourceCandidate = new GenerationStream(0x5243414Eu);
        public static readonly GenerationStream ResourcePriority = new GenerationStream(0x52505249u);
        public static readonly GenerationStream SpawnPriority = new GenerationStream(0x53505249u);
    }

    /// <summary>
    /// Immutable identity and layout contract for one generated world.
    /// </summary>
    public readonly struct WorldGenerationSettings : IEquatable<WorldGenerationSettings>
    {
        public WorldGenerationSettings(
            WorldSeed seed,
            WorldGeneratorVersion version,
            ChunkLayout chunkLayout)
        {
            if (!version.IsValid)
            {
                throw new ArgumentException("A valid generator version is required.", nameof(version));
            }

            if (!chunkLayout.IsValid)
            {
                throw new ArgumentException("A valid chunk layout is required.", nameof(chunkLayout));
            }

            Seed = seed;
            Version = version;
            ChunkLayout = chunkLayout;
        }

        public WorldSeed Seed { get; }
        public WorldGeneratorVersion Version { get; }
        public ChunkLayout ChunkLayout { get; }

        public bool Equals(WorldGenerationSettings other)
        {
            return Seed.Equals(other.Seed)
                && Version.Equals(other.Version)
                && ChunkLayout.Equals(other.ChunkLayout);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldGenerationSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Seed.GetHashCode();
                hash = (hash * 397) ^ Version.GetHashCode();
                hash = (hash * 397) ^ ChunkLayout.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"seed={Seed}; version={Version}; chunks={ChunkLayout}";
        }

        public static bool operator ==(WorldGenerationSettings left, WorldGenerationSettings right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldGenerationSettings left, WorldGenerationSettings right)
        {
            return !left.Equals(right);
        }
    }

    public static class WorldGenerationDefaults
    {
        public static readonly ChunkLayout PrototypeChunkLayout = new ChunkLayout(32, 32);

        public static WorldGenerationSettings CreateFoundationV1(WorldSeed seed)
        {
            return new WorldGenerationSettings(
                seed,
                WorldGeneratorVersions.FoundationV1,
                PrototypeChunkLayout);
        }

        public static WorldGenerationSettings CreatePopulationV1(WorldSeed seed)
        {
            return new WorldGenerationSettings(
                seed,
                WorldGeneratorVersions.PopulationV1,
                PrototypeChunkLayout);
        }
    }
}
