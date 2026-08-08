using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Food;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Persistence
{
    /// <summary>
    /// Restores a validated snapshot into a new Core state. The caller replaces
    /// the active Runtime session only after this operation succeeds completely.
    /// </summary>
    public static class GameSaveSnapshotRestorer
    {
        public static RestoredGameState Restore(
            GameSaveSnapshot snapshot,
            IWorldPopulationSettingsResolver worldResolver,
            IInventoryDefinitionResolver inventoryResolver)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (worldResolver == null) throw new ArgumentNullException(nameof(worldResolver));
            if (inventoryResolver == null) throw new ArgumentNullException(nameof(inventoryResolver));

            if (!worldResolver.TryResolve(
                snapshot.World,
                out WorldPopulationSettings world))
            {
                throw new NotSupportedException(
                    "The saved world identity is not supported by this build.");
            }

            ValidateResolvedWorld(snapshot.World, world);
            PlayerInventoryState inventory = RestoreInventory(
                snapshot.Inventory,
                inventoryResolver);
            PlayerHealthState health = snapshot.Health.Restore();
            PlayerFoodState food = snapshot.Food.Restore();
            PerishableInventoryState perishables = snapshot.Perishables.RestoreState();
            perishables.ValidateAgainst(inventory);

            var store = new ChunkMutationStore();
            for (int index = 0; index < snapshot.ChunkMutations.Count; index++)
            {
                store.Put(snapshot.ChunkMutations[index]);
            }

            var chunks = new ChunkStateLifecycle(
                new DeterministicWorldPopulationGenerator(world),
                store);
            return new RestoredGameState(
                world,
                snapshot.FixedTick,
                snapshot.PlayerPosition,
                health,
                food,
                perishables,
                inventory,
                chunks);
        }

        private static void ValidateResolvedWorld(
            WorldIdentitySnapshot identity,
            WorldPopulationSettings world)
        {
            if (!world.Generation.Equals(identity.Generation)
                || !world.Profile.Id.Equals(identity.PopulationProfileId)
                || world.Profile.Revision != identity.PopulationRevision)
            {
                throw new NotSupportedException(
                    "The resolved world does not match the saved identity.");
            }
        }

        private static PlayerInventoryState RestoreInventory(
            PlayerInventorySnapshot saved,
            IInventoryDefinitionResolver resolver)
        {
            var definitions = new List<ItemDefinition>(
                saved.Definitions.Count);
            var definitionsById =
                new Dictionary<ItemDefinitionId, ItemDefinition>(
                    saved.Definitions.Count);
            for (int index = 0; index < saved.Definitions.Count; index++)
            {
                InventoryDefinitionSnapshot fingerprint =
                    saved.Definitions[index];
                if (!resolver.TryResolveItemDefinition(
                    fingerprint,
                    out ItemDefinition definition)
                    || !Matches(fingerprint, definition))
                {
                    throw new NotSupportedException(
                        $"Item definition '{fingerprint.Id}' is unavailable or incompatible.");
                }

                definitions.Add(definition);
                definitionsById.Add(fingerprint.Id, definition);
            }

            if (resolver is IInventoryDefinitionCatalog catalog)
            {
                IReadOnlyList<ItemDefinition> currentDefinitions =
                    catalog.CurrentItemDefinitions
                    ?? throw new InvalidOperationException(
                        "The current inventory definition catalog is unavailable.");
                for (int index = 0; index < currentDefinitions.Count; index++)
                {
                    ItemDefinition definition = currentDefinitions[index]
                        ?? throw new InvalidOperationException(
                            "The current inventory definition catalog contains a null entry.");
                    if (definitionsById.ContainsKey(definition.Id))
                    {
                        continue;
                    }

                    definitions.Add(definition);
                    definitionsById.Add(definition.Id, definition);
                }
            }

            var containers = new List<ContainerState>(
                saved.Containers.Count);
            for (int containerIndex = 0;
                 containerIndex < saved.Containers.Count;
                 containerIndex++)
            {
                InventoryContainerSnapshot stored =
                    saved.Containers[containerIndex];
                if (!resolver.TryResolveContainerDefinition(
                    stored,
                    out ContainerDefinition definition)
                    || definition == null
                    || !string.Equals(
                        definition.Key,
                        stored.DefinitionKey,
                        StringComparison.Ordinal)
                    || !definition.Capacity.Equals(stored.Capacity))
                {
                    throw new NotSupportedException(
                        $"Container definition '{stored.DefinitionKey}' is unavailable or incompatible.");
                }

                var container = new ContainerState(stored.Id, definition);
                for (int entryIndex = 0;
                     entryIndex < stored.Entries.Count;
                     entryIndex++)
                {
                    InventoryEntrySnapshot entry = stored.Entries[entryIndex];
                    ItemDefinition itemDefinition =
                        FindDefinition(definitionsById, entry.DefinitionId);

                    AddItemResult result;
                    if (entry.Kind == ItemStateKind.Stackable)
                    {
                        result = itemDefinition.Perishable == null
                            ? InventoryOperations.AddStack(
                                container,
                                itemDefinition,
                                entry.Quantity)
                            : InventoryOperations.AddPerishableStack(
                                container,
                                itemDefinition,
                                entry.Quantity);
                    }
                    else
                    {
                        result = InventoryOperations.AddUnique(
                            container,
                            itemDefinition,
                            new UniqueItemState(
                                entry.DefinitionId,
                                entry.InstanceId,
                                entry.ContainedContainerId));
                    }

                    if (result.Accepted != entry.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Saved contents exceed container '{stored.Id}'.");
                    }
                }

                containers.Add(container);
            }

            return new PlayerInventoryState(
                saved.MainContainerId,
                definitions,
                containers,
                saved.Equipment);
        }

        private static bool Matches(
            InventoryDefinitionSnapshot saved,
            ItemDefinition definition)
        {
            if (definition == null
                || !definition.Id.Equals(saved.Id)
                || definition.StateKind != saved.StateKind
                || !definition.UnitEncumbrance.Equals(saved.UnitEncumbrance))
            {
                return false;
            }

            if (!saved.HasEquipment) return definition.Equipment == null;
            return definition.Equipment != null
                && definition.Equipment.CompatibleSlots == saved.CompatibleSlots
                && definition.Equipment.ContainedContainerReductionPercent
                    == saved.ContainedContainerReductionPercent;
        }

        private static ItemDefinition FindDefinition(
            IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> definitions,
            ItemDefinitionId id)
        {
            if (definitions.TryGetValue(id, out ItemDefinition definition))
            {
                return definition;
            }

            throw new InvalidOperationException(
                $"Saved inventory references unknown definition '{id}'.");
        }
    }
}
