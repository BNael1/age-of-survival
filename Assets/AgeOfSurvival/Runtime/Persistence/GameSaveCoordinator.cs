using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Runtime.Persistence
{
    public readonly struct CoordinatedGameLoadResult
    {
        public CoordinatedGameLoadResult(
            RestoredGameState state,
            GameSaveLoadSource source)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (!Enum.IsDefined(typeof(GameSaveLoadSource), source))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(source),
                    source,
                    "Unknown save source.");
            }

            Source = source;
        }

        public RestoredGameState State { get; }
        public GameSaveLoadSource Source { get; }
    }

    /// <summary>
    /// Runtime orchestration boundary without UI policy. The caller supplies a
    /// coherent main-thread capture and installs a restored session only after
    /// Load returns successfully.
    /// </summary>
    public sealed class GameSaveCoordinator
    {
        private readonly AtomicGameSaveStorage _storage;
        private readonly IWorldPopulationSettingsResolver _worldResolver;
        private readonly IInventoryDefinitionResolver _inventoryResolver;

        public GameSaveCoordinator(
            AtomicGameSaveStorage storage,
            IWorldPopulationSettingsResolver worldResolver,
            IInventoryDefinitionResolver inventoryResolver)
        {
            _storage = storage
                ?? throw new ArgumentNullException(nameof(storage));
            _worldResolver = worldResolver
                ?? throw new ArgumentNullException(nameof(worldResolver));
            _inventoryResolver = inventoryResolver
                ?? throw new ArgumentNullException(nameof(inventoryResolver));
        }

        public bool Exists(string slot)
        {
            return _storage.Exists(slot);
        }

        public void Save(
            string slot,
            WorldPopulationSettings world,
            long fixedTick,
            WorldPosition playerPosition,
            PlayerInventoryState inventory,
            ChunkStateLifecycle chunks)
        {
            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                fixedTick,
                playerPosition,
                inventory,
                chunks);
            _storage.Save(slot, snapshot);
        }

        public CoordinatedGameLoadResult Load(string slot)
        {
            GameSaveLoadResult loaded = _storage.Load(slot);
            RestoredGameState restored = GameSaveSnapshotRestorer.Restore(
                loaded.Snapshot,
                _worldResolver,
                _inventoryResolver);
            return new CoordinatedGameLoadResult(restored, loaded.Source);
        }
    }
}
