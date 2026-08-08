using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Core.Resources
{
    public readonly struct ResourceYieldResult
    {
        public ResourceYieldResult(
            ResourceInteractionResult interaction,
            GroundContainerState ground,
            int produced,
            ResourceDefinition definition = null)
        {
            Interaction = interaction;
            Ground = ground;
            Produced = produced;
            Definition = definition;
        }

        public ResourceInteractionResult Interaction { get; }
        public GroundContainerState Ground { get; }
        public int Produced { get; }
        public ResourceDefinition Definition { get; }
        public bool Succeeded =>
            Interaction.Succeeded
            && Ground != null
            && Produced > 0
            && Definition != null;
    }

    public static class ResourceYieldOperations
    {
        public static ResourceYieldResult HarvestToGround(
            IReadOnlyList<ResourceState> resources,
            IList<GroundContainerState> groundContainers,
            WorldPosition playerPosition,
            double interactionRadius,
            ResourceDefinitionCatalog definitions,
            EncumbranceValue groundCapacity)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            ValidateGroundCollection(groundContainers);
            ResourceState target = ResourceTargeting.FindNearestAvailable(
                resources,
                playerPosition,
                interactionRadius);
            if (target == null)
            {
                return new ResourceYieldResult(
                    ResourceInteractionResult.NoAvailableTarget(),
                    null,
                    0);
            }

            ResourceDefinition definition =
                definitions.Require(target.DefinitionId);
            return HarvestTargetToGround(
                target,
                groundContainers,
                definition,
                groundCapacity);
        }

        // Compatibility overload for original one-yield prototype callers.
        public static ResourceYieldResult HarvestToGround(
            IReadOnlyList<ResourceState> resources,
            IList<GroundContainerState> groundContainers,
            WorldPosition playerPosition,
            double interactionRadius,
            ItemDefinition yieldDefinition,
            int yieldQuantity,
            EncumbranceValue groundCapacity)
        {
            ValidateGroundCollection(groundContainers);
            if (yieldDefinition == null)
            {
                throw new ArgumentNullException(nameof(yieldDefinition));
            }

            var legacyYield = new ResourceYieldDefinition(
                yieldDefinition,
                yieldQuantity);
            long requiredUnits = legacyYield.Definition.UnitEncumbrance
                .Multiply(legacyYield.Quantity).Units;
            if (groundCapacity.Units < requiredUnits)
            {
                throw new ArgumentException(
                    "Ground capacity must hold the complete configured resource yield.",
                    nameof(groundCapacity));
            }

            ResourceState target = ResourceTargeting.FindNearestAvailable(
                resources,
                playerPosition,
                interactionRadius);
            if (target == null)
            {
                return new ResourceYieldResult(
                    ResourceInteractionResult.NoAvailableTarget(),
                    null,
                    0);
            }

            var definition = new ResourceDefinition(
                target.DefinitionId,
                "Legacy resource yield",
                new[]
                {
                    legacyYield
                });
            return HarvestTargetToGround(
                target,
                groundContainers,
                definition,
                groundCapacity);
        }

        public static GroundContainerId GroundIdFor(ResourceId resourceId)
        {
            if (!resourceId.IsValid)
            {
                throw new ArgumentException(
                    "A valid resource identifier is required.",
                    nameof(resourceId));
            }

            return new GroundContainerId(
                "ground-" + resourceId.Value);
        }

        public static ContainerId ContainerIdFor(ResourceId resourceId)
        {
            if (!resourceId.IsValid)
            {
                throw new ArgumentException(
                    "A valid resource identifier is required.",
                    nameof(resourceId));
            }

            return new ContainerId(
                "ground-container-" + resourceId.Value);
        }

        private static ResourceYieldResult HarvestTargetToGround(
            ResourceState target,
            IList<GroundContainerState> groundContainers,
            ResourceDefinition definition,
            EncumbranceValue groundCapacity)
        {
            long requiredUnits = 0L;
            int produced = 0;
            for (int index = 0;
                 index < definition.Yields.Count;
                 index++)
            {
                ResourceYieldDefinition yield =
                    definition.Yields[index];
                requiredUnits = checked(
                    requiredUnits
                    + yield.Definition.UnitEncumbrance
                        .Multiply(yield.Quantity).Units);
                produced = checked(
                    produced + yield.Quantity);
            }

            if (groundCapacity.Units < requiredUnits)
            {
                throw new ArgumentException(
                    "Ground capacity must hold the complete configured resource yield.",
                    nameof(groundCapacity));
            }

            GroundContainerId groundId =
                GroundIdFor(target.Id);
            ContainerId containerId =
                ContainerIdFor(target.Id);
            ValidateIdentityAvailability(
                groundContainers,
                groundId,
                containerId);

            var container = new ContainerState(
                containerId,
                new ContainerDefinition(
                    "ground-" + target.Id.Value,
                    "Ground near " + target.Id.Value,
                    groundCapacity));

            for (int index = 0;
                 index < definition.Yields.Count;
                 index++)
            {
                ResourceYieldDefinition yield =
                    definition.Yields[index];
                AddItemResult added =
                    InventoryOperations.AddStack(
                        container,
                        yield.Definition,
                        yield.Quantity);
                if (added.Accepted != yield.Quantity)
                {
                    throw new InvalidOperationException(
                        "A prevalidated ground container rejected part of a configured resource yield.");
                }
            }

            var ground = new GroundContainerState(
                groundId,
                target.Position,
                container);
            groundContainers.Add(ground);
            if (!target.TryHarvest())
            {
                groundContainers.Remove(ground);
                return new ResourceYieldResult(
                    ResourceInteractionResult.NoAvailableTarget(),
                    null,
                    0);
            }

            return new ResourceYieldResult(
                ResourceInteractionResult.Success(target.Id),
                ground,
                produced,
                definition);
        }

        private static void ValidateGroundCollection(
            IList<GroundContainerState> groundContainers)
        {
            if (groundContainers == null)
            {
                throw new ArgumentNullException(
                    nameof(groundContainers));
            }

            if (groundContainers.IsReadOnly)
            {
                throw new ArgumentException(
                    "Ground containers must be writable.",
                    nameof(groundContainers));
            }
        }

        private static void ValidateIdentityAvailability(
            IList<GroundContainerState> groundContainers,
            GroundContainerId groundId,
            ContainerId containerId)
        {
            for (int index = 0;
                 index < groundContainers.Count;
                 index++)
            {
                GroundContainerState existing =
                    groundContainers[index];
                if (existing == null)
                {
                    throw new ArgumentException(
                        "Ground containers must not contain null entries.",
                        nameof(groundContainers));
                }

                if (existing.Id.Equals(groundId)
                    || existing.Container.Id.Equals(containerId))
                {
                    throw new InvalidOperationException(
                        "A ground container already exists for this resource identifier.");
                }
            }
        }
    }
}
