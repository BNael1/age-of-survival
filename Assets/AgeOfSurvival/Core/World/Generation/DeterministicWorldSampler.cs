using System;

namespace AgeOfSurvival.Core.World.Generation
{
    /// <summary>
    /// Stateless, integer-only sampler for stable world generation.
    /// It intentionally does not use System.Random because its implementation is not
    /// guaranteed to remain identical across major .NET versions.
    /// </summary>
    public static class DeterministicWorldSampler
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixMultiplierA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplierB = 0x94D049BB133111EBUL;
        private const ulong WorldDomain = 0x414F535F574F524CUL;
        private const ulong FoundationV1Domain = 0x445F47454E5F5631UL;
        private const ulong PopulationV1Domain = 0x445F504F505F5631UL;
        private const double UnitScale = 1.0 / 9007199254740992.0;

        public static bool Supports(WorldGeneratorVersion version)
        {
            return version == WorldGeneratorVersions.FoundationV1
                || version == WorldGeneratorVersions.PopulationV1;
        }

        public static ulong Sample64(
            WorldGenerationSettings settings,
            WorldCellCoordinate worldCell,
            GenerationStream stream)
        {
            if (!Supports(settings.Version))
            {
                throw new NotSupportedException(
                    $"World generator version {settings.Version} is not supported by this build.");
            }

            if (settings.Version == WorldGeneratorVersions.FoundationV1)
            {
                return SampleVersion(
                    settings.Seed,
                    settings.Version,
                    worldCell,
                    stream,
                    FoundationV1Domain);
            }

            return SampleVersion(
                settings.Seed,
                settings.Version,
                worldCell,
                stream,
                PopulationV1Domain);
        }

        /// <summary>
        /// Returns a deterministic value in the half-open interval [0, 1).
        /// </summary>
        public static double SampleUnitInterval(
            WorldGenerationSettings settings,
            WorldCellCoordinate worldCell,
            GenerationStream stream)
        {
            ulong sample = Sample64(settings, worldCell, stream);
            return (sample >> 11) * UnitScale;
        }

        private static ulong SampleVersion(
            WorldSeed seed,
            WorldGeneratorVersion version,
            WorldCellCoordinate worldCell,
            GenerationStream stream,
            ulong versionDomain)
        {
            unchecked
            {
                ulong state = Mix64(seed.Value ^ WorldDomain);
                state = Mix64(state ^ ((ulong)version.Value + GoldenGamma));
                state = Mix64(state ^ (ZigZag(worldCell.X) + (GoldenGamma * 2UL)));
                state = Mix64(state ^ (ZigZag(worldCell.Y) + (GoldenGamma * 3UL)));
                state = Mix64(state ^ ((ulong)stream.Value + (GoldenGamma * 4UL)));
                return Mix64(state ^ versionDomain);
            }
        }

        private static ulong Mix64(ulong value)
        {
            unchecked
            {
                value ^= value >> 30;
                value *= MixMultiplierA;
                value ^= value >> 27;
                value *= MixMultiplierB;
                value ^= value >> 31;
                return value;
            }
        }

        private static ulong ZigZag(long value)
        {
            unchecked
            {
                return (ulong)((value << 1) ^ (value >> 63));
            }
        }
    }
}
