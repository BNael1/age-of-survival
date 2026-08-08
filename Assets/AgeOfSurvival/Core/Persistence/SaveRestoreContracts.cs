using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Food;
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

    /// <summary>
    /// Optional current-content catalog used during restore to add definitions
    /// introduced after an older save was written. Saved definitions are still
    /// resolved and compatibility-checked first.
    /// </summary>
    public interface IInventoryDefinitionCatalog
    {
        IReadOnlyList<ItemDefinition> CurrentItemDefinitions { get; }
    }

    public sealed class RestoredGameState
    {
        public RestoredGameState(
            WorldPopulationSettings world,
            long fixedTick,
            WorldPosition playerPosition,
            PlayerHealthState health,
            PlayerInventoryState inventory,
            ChunkStateLifecycle chunks)
            : this(
                world,
                fixedTick,
                playerPosition,
                health,
                CreateDefaultFoodState(fixedTick),
                new PerishableInventoryState(),
                inventory,
                chunks)
        {
        }

        public RestoredGameState(
            WorldPopulationSettings world,
            long fixedTick,
            WorldPosition playerPosition,
            PlayerHealthState health,
            PlayerFoodState food,
            PerishableInventoryState perishables,
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

            if (health == null)
            {
                throw new ArgumentNullException(nameof(health));
            }

            if (health.CurrentTick != fixedTick)
            {
                throw new ArgumentException(
                    "The restored health tick must match the restored fixed tick.",
                    nameof(health));
            }

            if (food == null) throw new ArgumentNullException(nameof(food));
            if (food.CurrentTick != fixedTick)
            {
                throw new ArgumentException(
                    "The restored food-need tick must match the restored fixed tick.",
                    nameof(food));
            }

            Inventory = inventory
                ?? throw new ArgumentNullException(nameof(inventory));
            Perishables = perishables
                ?? throw new ArgumentNullException(nameof(perishables));
            Perishables.ValidateAgainst(Inventory);

            World = world;
            FixedTick = fixedTick;
            PlayerPosition = playerPosition;
            Health = health;
            Food = food;
            Chunks = chunks
                ?? throw new ArgumentNullException(nameof(chunks));
        }

        private static PlayerFoodState CreateDefaultFoodState(long fixedTick)
        {
            // Keep the legacy overload's validation contract: the canonical
            // constructor owns the fixedTick error before food validation.
            return fixedTick < 0L
                ? null
                : PlayerFoodState.CreateFullAt(fixedTick);
        }

        public WorldPopulationSettings World { get; }
        public long FixedTick { get; }
        public WorldPosition PlayerPosition { get; }
        public PlayerHealthState Health { get; }
        public PlayerFoodState Food { get; }
        public PerishableInventoryState Perishables { get; }
        public PlayerInventoryState Inventory { get; }
        public ChunkStateLifecycle Chunks { get; }
    }
}
