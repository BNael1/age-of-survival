using System;
using AgeOfSurvival.Core.World;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.World.Generation
{
    public interface IChunkGenerator
    {
        WorldGenerationSettings Settings { get; }
        GeneratedChunk Generate(ChunkCoordinate coordinate);
    }

    /// <summary>
    /// Immutable generated baseline for one chunk. Persistent player/world changes
    /// must live in a separate modification layer.
    /// </summary>
    public sealed class GeneratedChunk
    {
        private readonly ulong[] _foundationSamples;

        internal GeneratedChunk(
            WorldGenerationSettings settings,
            ChunkCoordinate coordinate,
            ulong[] foundationSamples)
        {
            if (foundationSamples == null)
            {
                throw new ArgumentNullException(nameof(foundationSamples));
            }

            if (foundationSamples.Length != settings.ChunkLayout.CellCount)
            {
                throw new ArgumentException(
                    "The sample count must match the chunk layout.",
                    nameof(foundationSamples));
            }

            Settings = settings;
            Coordinate = coordinate;
            _foundationSamples = new ulong[foundationSamples.Length];
            Array.Copy(foundationSamples, _foundationSamples, foundationSamples.Length);
        }

        public WorldGenerationSettings Settings { get; }
        public ChunkCoordinate Coordinate { get; }
        public ChunkLayout Layout => Settings.ChunkLayout;
        public int Count => _foundationSamples.Length;

        public ulong GetFoundationSample(GridPosition localPosition)
        {
            return _foundationSamples[Layout.Bounds.ToIndex(localPosition)];
        }

        public WorldCellCoordinate GetWorldCell(GridPosition localPosition)
        {
            return ChunkAddressing.ToWorld(Coordinate, localPosition, Layout);
        }

        public ulong[] CopyFoundationSamples()
        {
            var copy = new ulong[_foundationSamples.Length];
            Array.Copy(_foundationSamples, copy, _foundationSamples.Length);
            return copy;
        }
    }

    /// <summary>
    /// Stateless chunk generator. Every cell is sampled from its absolute world
    /// coordinate, so generation order and chunk partition do not affect content.
    /// </summary>
    public sealed class DeterministicChunkGenerator : IChunkGenerator
    {
        public DeterministicChunkGenerator(WorldGenerationSettings settings)
        {
            if (!DeterministicWorldSampler.Supports(settings.Version))
            {
                throw new NotSupportedException(
                    $"World generator version {settings.Version} is not supported by this build.");
            }

            Settings = settings;
        }

        public WorldGenerationSettings Settings { get; }

        public GeneratedChunk Generate(ChunkCoordinate coordinate)
        {
            ChunkLayout layout = Settings.ChunkLayout;
            var samples = new ulong[layout.CellCount];

            for (int index = 0; index < samples.Length; index++)
            {
                GridPosition localPosition = layout.Bounds.FromIndex(index);
                WorldCellCoordinate worldCell = ChunkAddressing.ToWorld(
                    coordinate,
                    localPosition,
                    layout);
                samples[index] = DeterministicWorldSampler.Sample64(
                    Settings,
                    worldCell,
                    GenerationStreams.Foundation);
            }

            return new GeneratedChunk(Settings, coordinate, samples);
        }
    }

    /// <summary>
    /// Caller-owned cache that generates chunks only when requested.
    /// It contains generated baselines only and does not own persistent modifications.
    /// </summary>
    public sealed class OnDemandGeneratedWorld
    {
        private readonly IChunkGenerator _generator;
        private readonly Dictionary<ChunkCoordinate, GeneratedChunk> _loadedChunks =
            new Dictionary<ChunkCoordinate, GeneratedChunk>();

        public OnDemandGeneratedWorld(IChunkGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public WorldGenerationSettings Settings => _generator.Settings;
        public int LoadedChunkCount => _loadedChunks.Count;

        public GeneratedChunk GetOrGenerateChunk(ChunkCoordinate coordinate)
        {
            if (_loadedChunks.TryGetValue(coordinate, out GeneratedChunk existing))
            {
                return existing;
            }

            GeneratedChunk generated = _generator.Generate(coordinate);
            _loadedChunks.Add(coordinate, generated);
            return generated;
        }

        public bool TryGetLoadedChunk(
            ChunkCoordinate coordinate,
            out GeneratedChunk chunk)
        {
            return _loadedChunks.TryGetValue(coordinate, out chunk);
        }

        public bool UnloadChunk(ChunkCoordinate coordinate)
        {
            return _loadedChunks.Remove(coordinate);
        }

        public void Clear()
        {
            _loadedChunks.Clear();
        }
    }
}
