using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Resources
{
    public readonly struct ResourceYieldResult
    {
        public ResourceYieldResult(ResourceInteractionResult interaction, GroundContainerState ground, int produced)
        { Interaction = interaction; Ground = ground; Produced = produced; }
        public ResourceInteractionResult Interaction { get; }
        public GroundContainerState Ground { get; }
        public int Produced { get; }
        public bool Succeeded => Interaction.Succeeded && Ground != null && Produced > 0;
    }

    public static class ResourceYieldOperations
    {
        public static ResourceYieldResult HarvestToGround(IReadOnlyList<ResourceState> resources,
            IList<GroundContainerState> groundContainers, WorldPosition playerPosition, double interactionRadius,
            ItemDefinition yieldDefinition, int yieldQuantity, EncumbranceValue groundCapacity)
        {
            if (groundContainers == null) throw new ArgumentNullException(nameof(groundContainers));
            if (yieldDefinition == null) throw new ArgumentNullException(nameof(yieldDefinition));
            if (yieldQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(yieldQuantity));
            if (groundCapacity.Units < yieldDefinition.UnitEncumbrance.Multiply(yieldQuantity).Units)
                throw new ArgumentException("Ground capacity must hold the complete configured yield.", nameof(groundCapacity));
            ResourceInteractionResult interaction = ResourceInteraction.Apply(new ResourceInteractionCommand(),
                resources, playerPosition, interactionRadius);
            if (!interaction.Succeeded) return new ResourceYieldResult(interaction, null, 0);

            ResourceState resource = Find(resources, interaction.HarvestedResourceId.Value);
            string suffix = resource.Id.Value;
            var container = new ContainerState(new ContainerId("ground-container-" + suffix),
                new ContainerDefinition("ground-" + suffix, "Ground near " + suffix, groundCapacity));
            AddItemResult added = InventoryOperations.AddStack(container, yieldDefinition, yieldQuantity);
            var ground = new GroundContainerState(new GroundContainerId("ground-" + suffix), resource.Position, container);
            groundContainers.Add(ground);
            return new ResourceYieldResult(interaction, ground, added.Accepted);
        }

        private static ResourceState Find(IReadOnlyList<ResourceState> resources, ResourceId id)
        {
            for (int i = 0; i < resources.Count; i++) if (resources[i].Id.Equals(id)) return resources[i];
            throw new InvalidOperationException("Harvested resource was not found.");
        }
    }
}
