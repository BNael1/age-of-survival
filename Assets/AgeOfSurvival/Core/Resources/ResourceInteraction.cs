using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;

namespace AgeOfSurvival.Core.Resources
{
    /// <summary>
    /// Explicit gameplay request. The command is intentionally data-free in this
    /// minimal lot because the valid target is resolved when the tick applies it.
    /// </summary>
    public readonly struct ResourceInteractionCommand
    {
    }

    public enum ResourceInteractionOutcome
    {
        NoAvailableTarget = 0,
        Succeeded = 1
    }

    public readonly struct ResourceInteractionResult
    {
        private ResourceInteractionResult(
            ResourceInteractionOutcome outcome,
            ResourceId? harvestedResourceId)
        {
            Outcome = outcome;
            HarvestedResourceId = harvestedResourceId;
        }

        public ResourceInteractionOutcome Outcome { get; }
        public ResourceId? HarvestedResourceId { get; }
        public bool Succeeded => Outcome == ResourceInteractionOutcome.Succeeded;

        public static ResourceInteractionResult Success(ResourceId resourceId)
        {
            return new ResourceInteractionResult(
                ResourceInteractionOutcome.Succeeded,
                resourceId);
        }

        public static ResourceInteractionResult NoAvailableTarget()
        {
            return new ResourceInteractionResult(
                ResourceInteractionOutcome.NoAvailableTarget,
                null);
        }
    }

    /// <summary>
    /// Deterministic resource selection shared by preview and command application.
    /// </summary>
    public static class ResourceTargeting
    {
        public static ResourceState FindNearestAvailable(
            IReadOnlyList<ResourceState> resources,
            WorldPosition origin,
            double interactionRadius)
        {
            ValidateArguments(resources, interactionRadius);

            double radiusSquared = interactionRadius * interactionRadius;
            ResourceState nearest = null;
            double nearestDistanceSquared = double.PositiveInfinity;

            for (int index = 0; index < resources.Count; index++)
            {
                ResourceState candidate = resources[index];
                if (candidate == null
                    || candidate.Availability != ResourceAvailability.Available)
                {
                    continue;
                }

                double distanceSquared = origin.DistanceSquaredTo(candidate.Position);
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                if (nearest == null
                    || distanceSquared < nearestDistanceSquared
                    || (distanceSquared.Equals(nearestDistanceSquared)
                        && candidate.Id.CompareTo(nearest.Id) < 0))
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        internal static void ValidateArguments(
            IReadOnlyList<ResourceState> resources,
            double interactionRadius)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            if (double.IsNaN(interactionRadius)
                || double.IsInfinity(interactionRadius)
                || interactionRadius < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interactionRadius),
                    interactionRadius,
                    "The interaction radius must be finite and non-negative.");
            }
        }
    }

    public static class ResourceInteraction
    {
        public static ResourceInteractionResult Apply(
            ResourceInteractionCommand command,
            IReadOnlyList<ResourceState> resources,
            WorldPosition playerPosition,
            double interactionRadius)
        {
            ResourceState target = ResourceTargeting.FindNearestAvailable(
                resources,
                playerPosition,
                interactionRadius);

            if (target == null || !target.TryHarvest())
            {
                return ResourceInteractionResult.NoAvailableTarget();
            }

            return ResourceInteractionResult.Success(target.Id);
        }
    }
}
