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
            if (groundContainers.IsReadOnly)
                throw new ArgumentException("Ground containers must be writable.", nameof(groundContainers));
            if (yieldDefinition == null) throw new ArgumentNullException(nameof(yieldDefinition));
            if (yieldDefinition.StateKind != ItemStateKind.Stackable)
                throw new ArgumentException("Resource yields must use a stackable item definition.", nameof(yieldDefinition));
            if (yieldQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(yieldQuantity));
            if (groundCapacity.Units < yieldDefinition.UnitEncumbrance.Multiply(yieldQuantity).Units)
                throw new ArgumentException("Ground capacity must hold the complete configured yield.", nameof(groundCapacity));

            ResourceState target = ResourceTargeting.FindNearestAvailable(
                resources, playerPosition, interactionRadius);
            if (target == null)
                return new ResourceYieldResult(ResourceInteractionResult.NoAvailableTarget(), null, 0);

            GroundContainerId groundId = GroundIdFor(target.Id);
            ContainerId containerId = ContainerIdFor(target.Id);
            ValidateIdentityAvailability(groundContainers, groundId, containerId);

            var container = new ContainerState(containerId,
                new ContainerDefinition("ground-" + target.Id.Value,
                    "Ground near " + target.Id.Value, groundCapacity));
            AddItemResult added = InventoryOperations.AddStack(container, yieldDefinition, yieldQuantity);
            if (added.Accepted != yieldQuantity)
                throw new InvalidOperationException("A prevalidated ground container rejected part of the configured yield.");

            var ground = new GroundContainerState(groundId, target.Position, container);
            groundContainers.Add(ground);
            if (!target.TryHarvest())
            {
                groundContainers.Remove(ground);
                return new ResourceYieldResult(ResourceInteractionResult.NoAvailableTarget(), null, 0);
            }

            return new ResourceYieldResult(ResourceInteractionResult.Success(target.Id), ground, added.Accepted);
        }

        public static GroundContainerId GroundIdFor(ResourceId resourceId)
        {
            if (!resourceId.IsValid)
                throw new ArgumentException("A valid resource identifier is required.", nameof(resourceId));
            return new GroundContainerId("ground-" + resourceId.Value);
        }

        public static ContainerId ContainerIdFor(ResourceId resourceId)
        {
            if (!resourceId.IsValid)
                throw new ArgumentException("A valid resource identifier is required.", nameof(resourceId));
            return new ContainerId("ground-container-" + resourceId.Value);
        }

        private static void ValidateIdentityAvailability(IList<GroundContainerState> groundContainers,
            GroundContainerId groundId, ContainerId containerId)
        {
            for (int index = 0; index < groundContainers.Count; index++)
            {
                GroundContainerState existing = groundContainers[index];
                if (existing == null)
                    throw new ArgumentException("Ground containers must not contain null entries.", nameof(groundContainers));
                if (existing.Id.Equals(groundId) || existing.Container.Id.Equals(containerId))
                    throw new InvalidOperationException("A ground container already exists for this resource identifier.");
            }
        }
    }
}
