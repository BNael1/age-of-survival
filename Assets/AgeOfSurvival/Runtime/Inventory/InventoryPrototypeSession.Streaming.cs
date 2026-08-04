using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Resources;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        public ResourceState FindResource(ResourceId id)
        {
            for (int index = 0; index < _resources.Count; index++)
            {
                if (_resources[index].Id.Equals(id))
                {
                    return _resources[index];
                }
            }

            return null;
        }

        public int SynchronizeGeneratedResources(IEnumerable<ResourceState> resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            int added = 0;
            var incomingIds = new HashSet<ResourceId>();
            foreach (ResourceState candidate in resources)
            {
                if (candidate == null)
                {
                    throw new ArgumentException(
                        "Generated resources must not contain null entries.",
                        nameof(resources));
                }

                if (!incomingIds.Add(candidate.Id))
                {
                    throw new ArgumentException(
                        $"Generated resources contain duplicate identifier {candidate.Id}.",
                        nameof(resources));
                }

                ResourceState existing = FindResource(candidate.Id);
                if (existing != null)
                {
                    if (!existing.Position.Equals(candidate.Position))
                    {
                        throw new InvalidOperationException(
                            $"Generated resource {candidate.Id} changed position from "
                            + $"{existing.Position} to {candidate.Position}.");
                    }

                    continue;
                }

                _resources.Add(candidate);
                added++;
            }

            return added;
        }
    }
}
