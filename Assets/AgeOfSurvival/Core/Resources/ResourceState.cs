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
    /// </summary>
    public sealed class ResourceState
    {
        public ResourceState(
            ResourceId id,
            WorldPosition position,
            ResourceAvailability availability = ResourceAvailability.Available)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A resource state requires a valid identifier.",
                    nameof(id));
            }

            Id = id;
            Position = position;
            Availability = availability;
        }

        public ResourceId Id { get; }
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
