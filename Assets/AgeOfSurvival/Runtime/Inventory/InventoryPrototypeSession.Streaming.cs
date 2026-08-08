using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        private readonly ChunkMutationStore _chunkMutationStore = new ChunkMutationStore();
        private readonly HashSet<ChunkCoordinate> _activeGeneratedChunks =
            new HashSet<ChunkCoordinate>();

        public int MutatedChunkCount => _chunkMutationStore.Count;
        public int ChunkStateEvictionCount { get; private set; }
        public int ChunkStateRestorationCount { get; private set; }

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
                    if (!existing.Position.Equals(candidate.Position)
                        || !existing.DefinitionId.Equals(candidate.DefinitionId))
                    {
                        throw new InvalidOperationException(
                            $"Generated resource {candidate.Id} changed definition or position.");
                    }

                    continue;
                }

                _resources.Add(candidate);
                added++;
            }

            return added;
        }

        public void SynchronizeGeneratedChunkResources(
            IEnumerable<ResourceState> generatedResources,
            IEnumerable<ChunkCoordinate> retainedChunks,
            ChunkLayout layout)
        {
            if (!TrySynchronizeGeneratedChunkResources(
                    generatedResources,
                    retainedChunks,
                    layout))
            {
                throw new InvalidOperationException(
                    "A chunk with an active transfer cannot be evicted.");
            }
        }

        public bool TrySynchronizeGeneratedChunkResources(
            IEnumerable<ResourceState> generatedResources,
            IEnumerable<ChunkCoordinate> retainedChunks,
            ChunkLayout layout)
        {
            if (generatedResources == null) throw new ArgumentNullException(nameof(generatedResources));
            if (retainedChunks == null) throw new ArgumentNullException(nameof(retainedChunks));
            if (!layout.IsValid) throw new ArgumentException("A valid chunk layout is required.", nameof(layout));

            var retained = new HashSet<ChunkCoordinate>();
            foreach (ChunkCoordinate coordinate in retainedChunks)
            {
                if (!retained.Add(coordinate))
                {
                    throw new ArgumentException("Retained chunk coordinates must be unique.", nameof(retainedChunks));
                }
            }

            var incoming = new Dictionary<ChunkCoordinate, List<ResourceState>>();
            var incomingIds = new HashSet<ResourceId>();
            foreach (ResourceState resource in generatedResources)
            {
                if (resource == null)
                {
                    throw new ArgumentException("Generated resources must not contain null values.", nameof(generatedResources));
                }

                if (!incomingIds.Add(resource.Id))
                {
                    throw new ArgumentException("Generated resource identifiers must be unique.", nameof(generatedResources));
                }

                ChunkCoordinate coordinate = ChunkPositioning.Locate(resource.Position, layout);
                if (!retained.Contains(coordinate))
                {
                    throw new ArgumentException("A generated resource is outside the retained chunks.", nameof(generatedResources));
                }

                if (!incoming.TryGetValue(coordinate, out List<ResourceState> chunkResources))
                {
                    chunkResources = new List<ResourceState>();
                    incoming.Add(coordinate, chunkResources);
                }

                chunkResources.Add(resource);
            }

            var effectiveActive = new HashSet<ChunkCoordinate>(_activeGeneratedChunks);
            if (effectiveActive.Count == 0)
            {
                for (int index = 0; index < _resources.Count; index++)
                {
                    ResourceState existing = _resources[index];
                    if (!incomingIds.Contains(existing.Id)) continue;
                    ChunkCoordinate coordinate = ChunkPositioning.Locate(existing.Position, layout);
                    if (retained.Contains(coordinate)) effectiveActive.Add(coordinate);
                }
            }

            var toEvict = new List<ChunkCoordinate>();
            foreach (ChunkCoordinate activeCoordinate in effectiveActive)
            {
                if (!retained.Contains(activeCoordinate)) toEvict.Add(activeCoordinate);
            }

            toEvict.Sort();
            var toEvictSet = new HashSet<ChunkCoordinate>(toEvict);
            if (TransferAction != null && TransferAction.Status == TransferActionStatus.Active)
            {
                for (int index = 0; index < _groundContainers.Count; index++)
                {
                    GroundContainerState candidate = _groundContainers[index];
                    if (candidate.Container.Id.Equals(TransferAction.SourceId)
                        && toEvictSet.Contains(ChunkPositioning.Locate(candidate.Position, layout)))
                    {
                        return false;
                    }
                }
            }

            var captured = new List<ChunkMutationState>(toEvict.Count);
            for (int index = 0; index < toEvict.Count; index++)
            {
                if (_chunkMutationStore.Contains(toEvict[index]))
                {
                    throw new InvalidOperationException(
                        "An active chunk cannot also own a stored mutation.");
                }

                captured.Add(CaptureChunkMutation(toEvict[index], layout));
            }

            var toRestore = new List<ChunkCoordinate>();
            foreach (ChunkCoordinate coordinate in retained)
            {
                if (!effectiveActive.Contains(coordinate)) toRestore.Add(coordinate);
            }

            toRestore.Sort();
            var toRestoreSet = new HashSet<ChunkCoordinate>(toRestore);
            var restoredResources = new List<ResourceState>();
            var restoredGround = new List<GroundContainerState>();
            var restoredMutations = new List<ChunkMutationState>();
            for (int index = 0; index < toRestore.Count; index++)
            {
                ChunkCoordinate coordinate = toRestore[index];
                incoming.TryGetValue(coordinate, out List<ResourceState> candidates);
                candidates = candidates ?? new List<ResourceState>();
                _chunkMutationStore.TryPeek(coordinate, out ChunkMutationState mutation);
                RestoreChunkMutation(
                    coordinate,
                    candidates,
                    mutation,
                    restoredResources,
                    restoredGround);
                if (mutation != null) restoredMutations.Add(mutation);
            }

            foreach (KeyValuePair<ChunkCoordinate, List<ResourceState>> pair in incoming)
            {
                if (!effectiveActive.Contains(pair.Key) || toRestoreSet.Contains(pair.Key)) continue;
                for (int index = 0; index < pair.Value.Count; index++)
                {
                    ResourceState existing = FindResource(pair.Value[index].Id);
                    if (existing == null
                        || !existing.Position.Equals(pair.Value[index].Position)
                        || !existing.DefinitionId.Equals(
                            pair.Value[index].DefinitionId))
                    {
                        throw new InvalidOperationException(
                            "A retained generated resource changed identity, definition or position.");
                    }
                }
            }

            for (int index = 0; index < _resources.Count; index++)
            {
                ResourceState existing = _resources[index];
                ChunkCoordinate coordinate = ChunkPositioning.Locate(existing.Position, layout);
                if (effectiveActive.Contains(coordinate)
                    && retained.Contains(coordinate)
                    && !incomingIds.Contains(existing.Id))
                {
                    throw new InvalidOperationException(
                        "A retained generated resource disappeared from deterministic generation.");
                }
            }

            var replaced = new HashSet<ChunkCoordinate>(toEvictSet);
            replaced.UnionWith(toRestoreSet);
            var nextResources = new List<ResourceState>(_resources.Count + restoredResources.Count);
            for (int index = 0; index < _resources.Count; index++)
            {
                ResourceState resource = _resources[index];
                if (!replaced.Contains(ChunkPositioning.Locate(resource.Position, layout)))
                {
                    nextResources.Add(resource);
                }
            }

            nextResources.AddRange(restoredResources);
            ValidateUniqueResources(nextResources);

            var nextGround = new List<GroundContainerState>(_groundContainers.Count + restoredGround.Count);
            for (int index = 0; index < _groundContainers.Count; index++)
            {
                GroundContainerState ground = _groundContainers[index];
                if (!replaced.Contains(ChunkPositioning.Locate(ground.Position, layout)))
                {
                    nextGround.Add(ground);
                }
            }

            nextGround.AddRange(restoredGround);
            ValidateUniqueGroundOwnership(nextGround);

            var nextActive = new HashSet<ChunkCoordinate>(effectiveActive);
            nextActive.ExceptWith(toEvictSet);
            nextActive.UnionWith(toRestoreSet);

            for (int index = 0; index < captured.Count; index++)
            {
                _chunkMutationStore.Put(captured[index]);
            }

            for (int index = 0; index < restoredMutations.Count; index++)
            {
                _chunkMutationStore.TryTake(restoredMutations[index].Coordinate, out _);
            }

            _resources.Clear();
            _resources.AddRange(nextResources);
            _groundContainers.Clear();
            _groundContainers.AddRange(nextGround);
            _activeGeneratedChunks.Clear();
            foreach (ChunkCoordinate coordinate in nextActive)
            {
                _activeGeneratedChunks.Add(coordinate);
            }

            ChunkStateEvictionCount += captured.Count;
            ChunkStateRestorationCount += restoredMutations.Count;
            return true;
        }

        private static void ValidateUniqueResources(IReadOnlyList<ResourceState> resources)
        {
            var identifiers = new HashSet<ResourceId>();
            for (int index = 0; index < resources.Count; index++)
            {
                if (resources[index] == null || !identifiers.Add(resources[index].Id))
                {
                    throw new InvalidOperationException(
                        "Projected generated resource ownership is invalid.");
                }
            }
        }

        private static void ValidateUniqueGroundOwnership(IReadOnlyList<GroundContainerState> groundContainers)
        {
            var groundIds = new HashSet<GroundContainerId>();
            var containerIds = new HashSet<ContainerId>();
            for (int index = 0; index < groundContainers.Count; index++)
            {
                GroundContainerState ground = groundContainers[index];
                if (ground == null
                    || !groundIds.Add(ground.Id)
                    || !containerIds.Add(ground.Container.Id))
                {
                    throw new InvalidOperationException(
                        "Projected ground-container ownership is invalid.");
                }
            }
        }

        private ChunkMutationState CaptureChunkMutation(
            ChunkCoordinate coordinate,
            ChunkLayout layout)
        {
            var harvested = new List<HarvestedResourceMutation>();
            for (int index = 0; index < _resources.Count; index++)
            {
                ResourceState resource = _resources[index];
                if (ChunkPositioning.Locate(resource.Position, layout).Equals(coordinate)
                    && resource.Availability == ResourceAvailability.Harvested)
                {
                    harvested.Add(new HarvestedResourceMutation(resource.Id, resource.Position));
                }
            }

            var ground = new List<GroundContainerMutation>();
            for (int index = 0; index < _groundContainers.Count; index++)
            {
                GroundContainerState candidate = _groundContainers[index];
                if (!ChunkPositioning.Locate(candidate.Position, layout).Equals(coordinate)) continue;
                if (TransferAction != null
                    && TransferAction.Status == TransferActionStatus.Active
                    && TransferAction.SourceId.Equals(candidate.Container.Id))
                {
                    throw new InvalidOperationException("A chunk with an active transfer cannot be evicted.");
                }

                if (!candidate.IsEmpty) ground.Add(GroundContainerMutation.Capture(candidate));
            }

            return new ChunkMutationState(coordinate, layout, harvested, ground);
        }

        private static void RestoreChunkMutation(
            ChunkCoordinate coordinate,
            List<ResourceState> generated,
            ChunkMutationState mutation,
            List<ResourceState> restoredResources,
            List<GroundContainerState> restoredGround)
        {
            var generatedById = new Dictionary<ResourceId, ResourceState>();
            for (int index = 0; index < generated.Count; index++)
            {
                generatedById.Add(generated[index].Id, generated[index]);
            }

            var harvestedIds = new HashSet<ResourceId>();
            if (mutation != null)
            {
                if (!mutation.Coordinate.Equals(coordinate))
                {
                    throw new InvalidOperationException("A stored mutation has the wrong chunk owner.");
                }

                for (int index = 0; index < mutation.HarvestedResources.Count; index++)
                {
                    HarvestedResourceMutation harvested = mutation.HarvestedResources[index];
                    if (!generatedById.TryGetValue(harvested.Id, out ResourceState baseline)
                        || !baseline.Position.Equals(harvested.Position))
                    {
                        throw new InvalidOperationException("A stored resource no longer matches generation.");
                    }

                    harvestedIds.Add(harvested.Id);
                }

                for (int index = 0; index < mutation.GroundContainers.Count; index++)
                {
                    restoredGround.Add(ChunkStateTransfer.RestoreGround(mutation.GroundContainers[index]));
                }
            }

            for (int index = 0; index < generated.Count; index++)
            {
                ResourceState baseline = generated[index];
                restoredResources.Add(new ResourceState(
                    baseline.Id,
                    baseline.DefinitionId,
                    baseline.Position,
                    harvestedIds.Contains(baseline.Id)
                        ? ResourceAvailability.Harvested
                        : ResourceAvailability.Available));
            }
        }
    }
}
