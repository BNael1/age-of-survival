using System;
using AgeOfSurvival.Core.Characters;

namespace AgeOfSurvival.Core.Resources
{
    public enum ResourceAvailability
    {
        Available = 0,
        Harvested = 1
    }

    /// <summary>
    /// Mutable simulation state for one resource instance.
    /// The immutable definition identity is carried separately from availability.
    /// </summary>
    public sealed class ResourceState
    {
        public ResourceState(
            ResourceId id,
            WorldPosition position,
            ResourceAvailability availability = ResourceAvailability.Available)
            : this(
                id,
                ResourceDefinitionIds.Shrub,
                position,
                availability)
        {
        }

        public ResourceState(
            ResourceId id,
            ResourceDefinitionId definitionId,
            WorldPosition position,
            ResourceAvailability availability = ResourceAvailability.Available)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A resource state requires a valid identifier.",
                    nameof(id));
            }

            if (!definitionId.IsValid)
            {
                throw new ArgumentException(
                    "A resource state requires a valid definition identifier.",
                    nameof(definitionId));
            }

            if (!Enum.IsDefined(typeof(ResourceAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availability),
                    availability,
                    "Unknown resource availability.");
            }

            Id = id;
            DefinitionId = definitionId;
            Position = position;
            Availability = availability;
        }

        public ResourceId Id { get; }
        public ResourceDefinitionId DefinitionId { get; }
        public WorldPosition Position { get; }
        public ResourceAvailability Availability { get; private set; }

        internal bool TryHarvest()
        {
            if (Availability != ResourceAvailability.Available)
            {
                return false;
            }

            Availability = ResourceAvailability.Harvested;
            return true;
        }
    }
}
