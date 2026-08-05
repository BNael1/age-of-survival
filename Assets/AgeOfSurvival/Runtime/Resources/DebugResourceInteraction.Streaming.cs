using System.Collections.Generic;
using AgeOfSurvival.Core.Resources;

namespace AgeOfSurvival.Runtime.Resources
{
    public sealed partial class DebugResourceInteraction
    {
        private bool _streamingWorldAttached;

        private void AttachChunkStreaming()
        {
            if (_streamingWorldAttached
                || worldRenderer == null
                || !worldRenderer.UsesChunkStreaming)
            {
                return;
            }

            worldRenderer.ChunkEvictionRequested += HandleChunkEvictionRequested;
            worldRenderer.PopulationWindowChanged += HandlePopulationWindowChanged;
            _streamingWorldAttached = true;
        }

        private void DetachChunkStreaming()
        {
            if (!_streamingWorldAttached || worldRenderer == null)
            {
                return;
            }

            worldRenderer.ChunkEvictionRequested -= HandleChunkEvictionRequested;
            worldRenderer.PopulationWindowChanged -= HandlePopulationWindowChanged;
            _streamingWorldAttached = false;
        }


        private bool HandleChunkEvictionRequested(
            IReadOnlyList<ResourceState> retainedGeneratedResources,
            IReadOnlyList<AgeOfSurvival.Core.World.Generation.ChunkCoordinate> retainedChunks)
        {
            return _session == null
                || worldRenderer == null
                || _session.TrySynchronizeGeneratedChunkResources(
                    retainedGeneratedResources,
                    retainedChunks,
                    worldRenderer.PopulationChunk.Layout);
        }

        private void HandlePopulationWindowChanged()
        {
            if (_session == null || worldRenderer == null)
            {
                return;
            }

            IReadOnlyList<ResourceState> generated =
                worldRenderer.CreateCachedGeneratedResourceStates();
            _session.SynchronizeGeneratedChunkResources(
                generated,
                worldRenderer.CachedChunks,
                worldRenderer.PopulationChunk.Layout);

            DestroyGeneratedHierarchy();
            CreateMarkers();

            ResourceState target = ResourceTargeting.FindNearestAvailable(
                _session.Resources,
                _session.CurrentPlayerPosition,
                interactionRadius);
            CurrentTargetId = target != null ? target.Id : (ResourceId?)null;
            SynchronizeInteractionRadius(_session.CurrentPlayerPosition);
            SynchronizeVisuals(target);
        }
    }
}
