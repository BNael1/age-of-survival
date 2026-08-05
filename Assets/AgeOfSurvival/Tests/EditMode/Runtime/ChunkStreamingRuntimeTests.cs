using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class ChunkStreamingRuntimeTests
    {
        private GameObject _host;
        private DebugIsometricWorld _world;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Chunk Streaming Runtime Test");
            _world = _host.AddComponent<DebugIsometricWorld>();
            _host.AddComponent<DebugPlayerController>();
            _world.ConfigureGeneratedPopulation(
                new WorldSeed(0),
                new ChunkCoordinate(0, 0));
            _world.Rebuild();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void InitialWindowRendersNineAndQueuesOuterPreparationRing()
        {
            Assert.That(_world.UsesChunkStreaming, Is.True);
            Assert.That(_world.VisibleChunkCount, Is.EqualTo(9));
            Assert.That(_world.ActiveChunkViewCount, Is.EqualTo(9));
            Assert.That(_world.CreatedChunkViewCount, Is.EqualTo(9));
            Assert.That(_world.PreparedChunkCount, Is.EqualTo(9));
            Assert.That(_world.PreparedTargetCount, Is.EqualTo(25));
            Assert.That(_world.PendingPreparationCount, Is.EqualTo(16));
        }

        [Test]
        public void PreparationBudgetCompletesFiveByFiveWithoutCreatingViews()
        {
            int prepared = _world.PreparePendingChunks(16);

            Assert.That(prepared, Is.EqualTo(16));
            Assert.That(_world.PreparedChunkCount, Is.EqualTo(25));
            Assert.That(_world.PendingPreparationCount, Is.Zero);
            Assert.That(_world.VisibleChunkCount, Is.EqualTo(9));
            Assert.That(_world.CreatedChunkViewCount, Is.EqualTo(9));
        }

        [Test]
        public void CrossingPositiveBoundaryPreservesAbsoluteVisualMapping()
        {
            var sample = new WorldPosition(31.75, 2.25);
            Vector3 before = _world.LogicalToWorldPosition(sample, 0.18f, -0.1f);
            _world.PreparePendingChunks(16);

            bool changed = _world.SynchronizeStreaming(new WorldPosition(32.25, 2.25));
            Vector3 after = _world.LogicalToWorldPosition(sample, 0.18f, -0.1f);

            Assert.That(changed, Is.True);
            Assert.That(_world.StreamingCenter, Is.EqualTo(new ChunkCoordinate(1, 0)));
            Assert.That(Vector3.Distance(after, before), Is.LessThan(0.0001f));
            Assert.That(_world.LastWindowSynchronousGenerationCount, Is.Zero);
            Assert.That(_world.VisibleChunkCount, Is.EqualTo(9));
        }

        [Test]
        public void CrossingNegativeBoundaryUsesFloorChunkAddressing()
        {
            bool changed = _world.SynchronizeStreaming(new WorldPosition(-0.01, -0.01));

            Assert.That(changed, Is.True);
            Assert.That(_world.StreamingCenter, Is.EqualTo(new ChunkCoordinate(-1, -1)));
            Assert.That(_world.IsChunkVisible(new ChunkCoordinate(-2, -2)), Is.True);
            Assert.That(_world.IsChunkVisible(new ChunkCoordinate(0, 0)), Is.True);
        }

        [Test]
        public void VisibleViewsContainCompleteChunkTilemaps()
        {
            foreach (ChunkCoordinate coordinate in _world.VisibleChunks)
            {
                Assert.That(_world.TryGetVisibleTilemap(coordinate, out Tilemap tilemap), Is.True);
                Assert.That(tilemap, Is.Not.Null);
                Assert.That(tilemap.HasTile(Vector3Int.zero), Is.True);
                Assert.That(tilemap.HasTile(new Vector3Int(31, 31, 0)), Is.True);
                Assert.That(tilemap.cellBounds.size.x, Is.EqualTo(32));
                Assert.That(tilemap.cellBounds.size.y, Is.EqualTo(32));
            }
        }

        [Test]
        public void GeneratedResourcesAreUniqueAcrossVisibleChunks()
        {
            IReadOnlyList<ResourceState> resources = _world.CreateGeneratedResourceStates();
            var ids = new HashSet<ResourceId>();

            for (int index = 0; index < resources.Count; index++)
            {
                Assert.That(ids.Add(resources[index].Id), Is.True);
                Assert.That(_world.IsWorldPositionVisible(resources[index].Position), Is.True);
            }

            Assert.That(resources.Count, Is.EqualTo(_world.VisibleGeneratedResourceCount));
        }

        [Test]
        public void SessionSynchronizationPreservesMutableHarvestedState()
        {
            IReadOnlyList<ResourceState> generated = _world.CreateGeneratedResourceStates();
            Assert.That(generated.Count, Is.GreaterThan(0));
            var session = new InventoryPrototypeSession(generated);
            ResourceState original = session.Resources[0];
            ResourceInteractionResult harvest = ResourceInteraction.Apply(
                new ResourceInteractionCommand(),
                session.Resources,
                original.Position,
                0.0);
            Assert.That(harvest.Succeeded, Is.True);
            Assert.That(harvest.HarvestedResourceId, Is.EqualTo(original.Id));

            int added = session.SynchronizeGeneratedResources(
                _world.CreateGeneratedResourceStates());

            Assert.That(added, Is.Zero);
            Assert.That(session.FindResource(original.Id), Is.SameAs(original));
            Assert.That(
                session.FindResource(original.Id).Availability,
                Is.EqualTo(ResourceAvailability.Harvested));
        }

        [Test]
        public void SessionSynchronizationRejectsChangedStableResourcePosition()
        {
            IReadOnlyList<ResourceState> generated = _world.CreateGeneratedResourceStates();
            Assert.That(generated.Count, Is.GreaterThan(0));
            var session = new InventoryPrototypeSession(generated);
            ResourceState original = session.Resources[0];
            var moved = new ResourceState(
                original.Id,
                new WorldPosition(original.Position.X + 1.0, original.Position.Y));

            Assert.Throws<System.InvalidOperationException>(() =>
                session.SynchronizeGeneratedResources(new[] { moved }));
        }

        [Test]
        public void LongTraversalStabilizesAtFortyNineCachedChunks()
        {
            _world.PreparePendingChunks(64);
            _world.StabilizeRetention();

            for (int chunkX = 1; chunkX <= 24; chunkX++)
            {
                Assert.That(
                    _world.SynchronizeStreaming(new WorldPosition((chunkX * 32) + 0.5, 0.5)),
                    Is.True);
                _world.PreparePendingChunks(64);
                _world.StabilizeRetention();

                Assert.That(_world.CachedChunkCount, Is.LessThanOrEqualTo(49));
                Assert.That(_world.VisibleChunkCount, Is.EqualTo(9));
                Assert.That(_world.PendingPreparationCount, Is.Zero);
            }

            Assert.That(_world.EvictionCount, Is.GreaterThan(0));
            Assert.That(_world.CachedChunkCount, Is.EqualTo(30));
        }

        [Test]
        public void NegativeLongTraversalDoesNotGrowCacheContinuously()
        {
            _world.PreparePendingChunks(64);
            for (int step = 1; step <= 16; step++)
            {
                _world.SynchronizeStreaming(new WorldPosition((-step * 32) - 0.25, (-step * 32) - 0.25));
                _world.PreparePendingChunks(64);
                _world.StabilizeRetention();
                Assert.That(_world.CachedChunkCount, Is.LessThanOrEqualTo(49));
            }

            Assert.That(_world.StreamingCenter, Is.EqualTo(new ChunkCoordinate(-17, -17)));
        }

        [Test]
        public void HarvestAndGroundRemainderSurviveRuntimeEvictionAndReturn()
        {
            _world.PreparePendingChunks(64);
            _world.StabilizeRetention();
            IReadOnlyList<ResourceState> generated = _world.CreateCachedGeneratedResourceStates();
            var session = new InventoryPrototypeSession(_world.CreateGeneratedResourceStates());
            session.SynchronizeGeneratedChunkResources(
                generated,
                _world.CachedChunks,
                _world.PopulationChunk.Layout);
            ResourceState target = session.Resources[0];
            ResourceYieldResult yield = session.HarvestAndStartTransfer(target.Position, 0d, 1);
            Assert.That(yield.Succeeded, Is.True);
            session.AdvanceTransfer(10000, target.Position, false);
            int remainder = InventoryOperations.Count(
                yield.Ground.Container,
                InventoryPrototypeCatalog.Branches.Id);
            Assert.That(remainder, Is.GreaterThan(0));
            ResourceId targetId = target.Id;

            _world.SynchronizeStreaming(new WorldPosition(256.5, 0.5));
            _world.PreparePendingChunks(64);
            _world.StabilizeRetention();
            session.SynchronizeGeneratedChunkResources(
                _world.CreateCachedGeneratedResourceStates(),
                _world.CachedChunks,
                _world.PopulationChunk.Layout);
            Assert.That(session.FindResource(targetId), Is.Null);
            Assert.That(session.MutatedChunkCount, Is.GreaterThan(0));

            _world.SynchronizeStreaming(target.Position);
            _world.PreparePendingChunks(64);
            _world.StabilizeRetention();
            session.SynchronizeGeneratedChunkResources(
                _world.CreateCachedGeneratedResourceStates(),
                _world.CachedChunks,
                _world.PopulationChunk.Layout);

            Assert.That(session.FindResource(targetId).Availability, Is.EqualTo(ResourceAvailability.Harvested));
            GroundContainerState restored = session.FindGround(yield.Ground.Container.Id);
            Assert.That(restored, Is.Not.Null);
            Assert.That(
                InventoryOperations.Count(restored.Container, InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(remainder));
        }

        [Test]
        public void ActiveTransferDefersWorldAndSessionEvictionUntilItCompletes()
        {
            _world.PreparePendingChunks(64);
            _world.StabilizeRetention();
            var session = new InventoryPrototypeSession(_world.CreateGeneratedResourceStates());
            session.SynchronizeGeneratedChunkResources(
                _world.CreateCachedGeneratedResourceStates(),
                _world.CachedChunks,
                _world.PopulationChunk.Layout);
            ResourceState target = session.Resources[0];
            ResourceYieldResult yield = session.HarvestAndStartTransfer(target.Position, 0d, 1);
            Assert.That(yield.Succeeded, Is.True);

            _world.ChunkEvictionRequested += (generated, retained) =>
                session.TrySynchronizeGeneratedChunkResources(
                    generated,
                    retained,
                    _world.PopulationChunk.Layout);

            _world.SynchronizeStreaming(new WorldPosition(256.5, 0.5));
            _world.PreparePendingChunks(64);
            int cacheBeforeDeferredEviction = _world.CachedChunkCount;
            int worldEvictionsBefore = _world.EvictionCount;

            Assert.That(_world.StabilizeRetention(), Is.Zero);
            Assert.That(_world.CachedChunkCount, Is.EqualTo(cacheBeforeDeferredEviction));
            Assert.That(_world.EvictionCount, Is.EqualTo(worldEvictionsBefore));
            Assert.That(session.FindResource(target.Id), Is.SameAs(target));
            Assert.That(session.MutatedChunkCount, Is.Zero);

            session.AdvanceTransfer(10000, target.Position, false);
            Assert.That(_world.StabilizeRetention(), Is.GreaterThan(0));
            Assert.That(_world.CachedChunkCount, Is.LessThanOrEqualTo(49));
            Assert.That(session.FindResource(target.Id), Is.Null);
            Assert.That(session.MutatedChunkCount, Is.GreaterThan(0));
        }

        [Test]
        public void FailedSessionPrevalidationLeavesAllChunkStateUnchanged()
        {
            _world.PreparePendingChunks(64);
            _world.StabilizeRetention();
            var session = new InventoryPrototypeSession(_world.CreateGeneratedResourceStates());
            session.SynchronizeGeneratedChunkResources(
                _world.CreateCachedGeneratedResourceStates(),
                _world.CachedChunks,
                _world.PopulationChunk.Layout);

            var retained = new List<ChunkCoordinate>(_world.CachedChunks);
            ChunkCoordinate omitted = retained[0];
            retained.RemoveAt(0);
            IReadOnlyList<ResourceState> baseline = _world.CreateCachedGeneratedResourceStates();
            var changed = new List<ResourceState>();
            ResourceState movedOriginal = null;
            for (int index = 0; index < baseline.Count; index++)
            {
                ResourceState candidate = baseline[index];
                if (ChunkPositioning.Locate(
                        candidate.Position,
                        _world.PopulationChunk.Layout).Equals(omitted))
                {
                    continue;
                }

                if (movedOriginal == null)
                {
                    movedOriginal = session.FindResource(candidate.Id);
                    changed.Add(new ResourceState(
                        candidate.Id,
                        new WorldPosition(candidate.Position.X + 0.25, candidate.Position.Y)));
                }
                else
                {
                    changed.Add(candidate);
                }
            }

            Assert.That(movedOriginal, Is.Not.Null);
            int resourceCount = session.Resources.Count;
            int mutatedCount = session.MutatedChunkCount;
            int evictionCount = session.ChunkStateEvictionCount;
            int restorationCount = session.ChunkStateRestorationCount;

            Assert.Throws<System.InvalidOperationException>(() =>
                session.TrySynchronizeGeneratedChunkResources(
                    changed,
                    retained,
                    _world.PopulationChunk.Layout));

            Assert.That(session.Resources.Count, Is.EqualTo(resourceCount));
            Assert.That(session.FindResource(movedOriginal.Id), Is.SameAs(movedOriginal));
            Assert.That(session.MutatedChunkCount, Is.EqualTo(mutatedCount));
            Assert.That(session.ChunkStateEvictionCount, Is.EqualTo(evictionCount));
            Assert.That(session.ChunkStateRestorationCount, Is.EqualTo(restorationCount));
        }
    }
}
