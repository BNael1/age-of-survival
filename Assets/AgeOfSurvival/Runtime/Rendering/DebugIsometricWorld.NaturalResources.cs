using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Resources;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Rendering
{
    public sealed partial class DebugIsometricWorld
    {
        private static ResourceState CreateGeneratedNaturalResourceState(
            GeneratedResourcePlacement placement)
        {
            return new ResourceState(
                placement.Id,
                ResourcePrototypeCatalog.DefinitionIdFor(
                    placement.Kind),
                new WorldPosition(
                    placement.Cell.X,
                    placement.Cell.Y));
        }

        private static WorldPopulationSettings ResolvePopulationSettings(
            WorldSeed configuredSeed)
        {
            if (Application.isPlaying)
            {
                InventoryPrototypeSession current =
                    InventoryPrototypeSessionProvider.Current;
                if (current.RestoredFromSave)
                {
                    return current.PersistenceWorld;
                }
            }

            return WorldPopulationDefaults
                .CreateTemperatePrototypeCurrent(configuredSeed);
        }
    }
}
