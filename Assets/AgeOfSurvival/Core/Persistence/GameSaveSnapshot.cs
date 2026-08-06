using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Persistence
{
    public enum GameSaveSnapshotViolation
    {
        InvalidPopulationProfileIdentifier = 0,
        InvalidPopulationRevision = 1,
        NegativeFixedTick = 2,
        EmptyChunkMutation = 3,
        MutationLayoutMismatch = 4,
        DuplicateChunkCoordinate = 5,
        HealthTickMismatch = 6
    }

    public sealed class GameSaveSnapshotException : InvalidOperationException
    {
        internal GameSaveSnapshotException(
            GameSaveSnapshotViolation violation,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            Violation = violation;
        }

        public GameSaveSnapshotViolation Violation { get; }
    }

    /// <summary>
    /// Immutable identity required to reconstruct the generated world before
    /// applying sparse mutations.
    /// </summary>
    public readonly struct WorldIdentitySnapshot : IEquatable<WorldIdentitySnapshot>
    {
        public WorldIdentitySnapshot(
            WorldGenerationSettings generation,
            WorldPopulationProfileId populationProfileId,
            int populationRevision)
        {
            if (!generation.Version.IsValid || !generation.ChunkLayout.IsValid)
            {
                throw new ArgumentException(
                    "A valid world generation identity is required.",
                    nameof(generation));
            }

            if (!populationProfileId.IsValid)
            {
                throw Violation(
                    GameSaveSnapshotViolation.InvalidPopulationProfileIdentifier,
                    "A valid population profile identifier is required.");
            }

            try
            {
                StableIdentifierValidation.Validate(
                    populationProfileId.Value,
                    nameof(populationProfileId));
            }
            catch (ArgumentException exception)
            {
                throw Violation(
                    GameSaveSnapshotViolation.InvalidPopulationProfileIdentifier,
                    "The population profile identifier is not save-safe.",
                    exception);
            }

            if (populationRevision <= 0)
            {
                throw Violation(
                    GameSaveSnapshotViolation.InvalidPopulationRevision,
                    "The population profile revision must be greater than zero.");
            }

            Generation = generation;
            PopulationProfileId = populationProfileId;
            PopulationRevision = populationRevision;
        }

        public WorldGenerationSettings Generation { get; }
        public WorldPopulationProfileId PopulationProfileId { get; }
        public int PopulationRevision { get; }

        public bool Equals(WorldIdentitySnapshot other)
        {
            return Generation.Equals(other.Generation)
                && PopulationProfileId.Equals(other.PopulationProfileId)
                && PopulationRevision == other.PopulationRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldIdentitySnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Generation.GetHashCode();
                hash = (hash * 397) ^ PopulationProfileId.GetHashCode();
                return (hash * 397) ^ PopulationRevision;
            }
        }

        private static GameSaveSnapshotException Violation(
            GameSaveSnapshotViolation violation,
            string message,
            Exception innerException = null)
        {
            return new GameSaveSnapshotException(
                violation,
                message,
                innerException);
        }
    }

    /// <summary>
    /// Immutable canonical player-health capture stored in game saves.
    /// </summary>
    public readonly struct PlayerHealthSnapshot :
        IEquatable<PlayerHealthSnapshot>
    {
        public PlayerHealthSnapshot(PlayerHealthState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            MaximumHealth = state.MaximumHealth;
            CurrentHealth = state.CurrentHealth;
            CurrentTick = state.CurrentTick;
            NextRegenerationTick = state.NextRegenerationTick;
        }

        public PlayerHealthSnapshot(
            int maximumHealth,
            int currentHealth,
            long currentTick,
            long? nextRegenerationTick)
        {
            var validated = new PlayerHealthState(
                maximumHealth,
                currentHealth,
                currentTick,
                nextRegenerationTick);

            MaximumHealth = validated.MaximumHealth;
            CurrentHealth = validated.CurrentHealth;
            CurrentTick = validated.CurrentTick;
            NextRegenerationTick =
                validated.NextRegenerationTick;
        }

        public int MaximumHealth { get; }
        public int CurrentHealth { get; }
        public long CurrentTick { get; }
        public long? NextRegenerationTick { get; }

        public PlayerHealthState Restore()
        {
            return new PlayerHealthState(
                MaximumHealth,
                CurrentHealth,
                CurrentTick,
                NextRegenerationTick);
        }

        public bool Equals(PlayerHealthSnapshot other)
        {
            return MaximumHealth == other.MaximumHealth
                && CurrentHealth == other.CurrentHealth
                && CurrentTick == other.CurrentTick
                && NextRegenerationTick
                    == other.NextRegenerationTick;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerHealthSnapshot other
                && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaximumHealth;
                hash = (hash * 397) ^ CurrentHealth;
                hash = (hash * 397) ^ CurrentTick.GetHashCode();
                return (hash * 397)
                    ^ NextRegenerationTick.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Immutable canonical capture used as the sole input of the versioned binary
    /// codec. It contains no Runtime adapter and does not own generated chunks.
    /// </summary>
    public sealed class GameSaveSnapshot
    {
        private readonly ChunkMutationState[] _chunkMutations;
        private readonly IReadOnlyList<ChunkMutationState> _readOnlyChunkMutations;

        public GameSaveSnapshot(
            WorldIdentitySnapshot world,
            long fixedTick,
            WorldPosition playerPosition,
            PlayerHealthSnapshot health,
            PlayerInventorySnapshot inventory,
            IEnumerable<ChunkMutationState> chunkMutations)
        {
            if (!world.Generation.Version.IsValid
                || !world.Generation.ChunkLayout.IsValid
                || !world.PopulationProfileId.IsValid
                || world.PopulationRevision <= 0)
            {
                throw new ArgumentException(
                    "A valid world identity snapshot is required.",
                    nameof(world));
            }

            if (fixedTick < 0)
            {
                throw Violation(
                    GameSaveSnapshotViolation.NegativeFixedTick,
                    "The fixed simulation tick must be non-negative.");
            }

            PlayerHealthState validatedHealth = health.Restore();
            if (validatedHealth.CurrentTick != fixedTick)
            {
                throw Violation(
                    GameSaveSnapshotViolation.HealthTickMismatch,
                    "The health tick must match the fixed simulation tick.");
            }

            Health = health;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (chunkMutations == null)
            {
                throw new ArgumentNullException(nameof(chunkMutations));
            }

            var canonical = new List<ChunkMutationState>();
            foreach (ChunkMutationState mutation in chunkMutations)
            {
                if (mutation == null)
                {
                    throw new ArgumentException(
                        "Chunk mutations must not contain null values.",
                        nameof(chunkMutations));
                }

                if (mutation.IsEmpty)
                {
                    throw Violation(
                        GameSaveSnapshotViolation.EmptyChunkMutation,
                        $"Chunk mutation {mutation.Coordinate} is empty.");
                }

                if (!mutation.Layout.Equals(world.Generation.ChunkLayout))
                {
                    throw Violation(
                        GameSaveSnapshotViolation.MutationLayoutMismatch,
                        $"Chunk mutation {mutation.Coordinate} uses a different layout.");
                }

                canonical.Add(mutation);
            }

            canonical.Sort((left, right) =>
                left.Coordinate.CompareTo(right.Coordinate));
            for (int index = 1; index < canonical.Count; index++)
            {
                if (canonical[index - 1].Coordinate.Equals(
                    canonical[index].Coordinate))
                {
                    throw Violation(
                        GameSaveSnapshotViolation.DuplicateChunkCoordinate,
                        $"Chunk mutation {canonical[index].Coordinate} appears more than once.");
                }
            }

            World = world;
            FixedTick = fixedTick;
            PlayerPosition = new WorldPosition(
                NormalizeZero(playerPosition.X),
                NormalizeZero(playerPosition.Y));
            _chunkMutations = canonical.ToArray();
            _readOnlyChunkMutations = Array.AsReadOnly(_chunkMutations);
        }

        public WorldIdentitySnapshot World { get; }
        public long FixedTick { get; }
        public WorldPosition PlayerPosition { get; }
        public PlayerHealthSnapshot Health { get; }
        public PlayerInventorySnapshot Inventory { get; }
        public IReadOnlyList<ChunkMutationState> ChunkMutations =>
            _readOnlyChunkMutations;

        private static double NormalizeZero(double value)
        {
            return value == 0d ? 0d : value;
        }

        private static GameSaveSnapshotException Violation(
            GameSaveSnapshotViolation violation,
            string message)
        {
            return new GameSaveSnapshotException(violation, message);
        }
    }

    public static class GameSaveSnapshotCapture
    {
        public static GameSaveSnapshot Capture(
            WorldPopulationSettings world,
            long fixedTick,
            WorldPosition playerPosition,
            PlayerHealthState health,
            PlayerInventoryState inventory,
            ChunkStateLifecycle chunks)
        {
            if (!world.Generation.Version.IsValid
                || !world.Generation.ChunkLayout.IsValid
                || !world.Profile.IsValid)
            {
                throw new ArgumentException(
                    "Valid world population settings are required.",
                    nameof(world));
            }

            if (health == null)
            {
                throw new ArgumentNullException(nameof(health));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (chunks == null)
            {
                throw new ArgumentNullException(nameof(chunks));
            }

            var identity = new WorldIdentitySnapshot(
                world.Generation,
                world.Profile.Id,
                world.Profile.Revision);
            PlayerInventorySnapshot inventorySnapshot =
                inventory.CaptureSnapshot();

            return new GameSaveSnapshot(
                identity,
                fixedTick,
                playerPosition,
                new PlayerHealthSnapshot(health),
                inventorySnapshot,
                chunks.CaptureCanonicalMutations());
        }
    }
}
