using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Persistence
{
    public interface IWorldPopulationSettingsResolver
    {
        bool TryResolve(
            WorldIdentitySnapshot identity,
            out WorldPopulationSettings settings);
    }

    public interface IInventoryDefinitionResolver
    {
        bool TryResolveItemDefinition(
            InventoryDefinitionSnapshot saved,
            out ItemDefinition definition);

        bool TryResolveContainerDefinition(
            InventoryContainerSnapshot saved,
            out ContainerDefinition definition);
    }

    public sealed class RestoredGameState
    {
        public RestoredGameState(
            WorldPopulationSettings world,
            long fixedTick,
            WorldPosition playerPosition,
            PlayerInventoryState inventory,
            ChunkStateLifecycle chunks)
        {
            if (!world.Generation.Version.IsValid
                || !world.Generation.ChunkLayout.IsValid
                || !world.Profile.IsValid)
            {
                throw new ArgumentException(
                    "Valid restored world settings are required.",
                    nameof(world));
            }

            if (fixedTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixedTick),
                    fixedTick,
                    "The restored tick must be non-negative.");
            }

            World = world;
            FixedTick = fixedTick;
            PlayerPosition = playerPosition;
            Inventory = inventory
                ?? throw new ArgumentNullException(nameof(inventory));
            Chunks = chunks
                ?? throw new ArgumentNullException(nameof(chunks));
        }

        public WorldPopulationSettings World { get; }
        public long FixedTick { get; }
        public WorldPosition PlayerPosition { get; }
        public PlayerInventoryState Inventory { get; }
        public ChunkStateLifecycle Chunks { get; }
    }
}
