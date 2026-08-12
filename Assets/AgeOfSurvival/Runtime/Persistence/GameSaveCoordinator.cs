using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Food;
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
        private readonly IConstructionDefinitionResolver _constructionResolver;

        public GameSaveCoordinator(
            AtomicGameSaveStorage storage,
            IWorldPopulationSettingsResolver worldResolver,
            IInventoryDefinitionResolver inventoryResolver,
            IConstructionDefinitionResolver constructionResolver)
        {
            _storage = storage
                ?? throw new ArgumentNullException(nameof(storage));
            _worldResolver = worldResolver
                ?? throw new ArgumentNullException(nameof(worldResolver));
            _inventoryResolver = inventoryResolver
                ?? throw new ArgumentNullException(nameof(inventoryResolver));
            _constructionResolver = constructionResolver
                ?? throw new ArgumentNullException(nameof(constructionResolver));
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
            PlayerHealthState health,
            PlayerFoodState food,
            PerishableInventoryState perishables,
            PlayerInventoryState inventory,
            ChunkStateLifecycle chunks,
            ConstructionSaveSnapshot construction)
        {
            GameSaveSnapshot snapshot = GameSaveSnapshotCapture.Capture(
                world,
                fixedTick,
                playerPosition,
                health,
                food,
                perishables,
                inventory,
                chunks,
                construction);
            _storage.Save(slot, snapshot);
        }

        public CoordinatedGameLoadResult Load(string slot)
        {
            GameSaveLoadResult loaded = _storage.Load(slot);
            RestoredGameState restored = GameSaveSnapshotRestorer.Restore(
                loaded.Snapshot,
                _worldResolver,
                _inventoryResolver,
                _constructionResolver);
            return new CoordinatedGameLoadResult(restored, loaded.Source);
        }
    }
}
