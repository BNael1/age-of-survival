using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Shelter;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Shelter;
using NUnit.Framework;

namespace AgeOfSurvival.Tests.EditMode.Runtime
{
    public sealed class ShelterRuntimeSessionTests
    {
        private static readonly ItemDefinitionId Material = InventoryPrototypeCatalog.Branches.Id;
        private static readonly ConstructionDefinitionId Floor = new ConstructionDefinitionId("floor");
        private static readonly ConstructionDefinitionId Wall = new ConstructionDefinitionId("wall");
        private static readonly ConstructionDefinitionId Roof = new ConstructionDefinitionId("roof");
        private static readonly ConstructionDefinitionId Bed = new ConstructionDefinitionId("bed");

        [Test]
        public void SyntheticRuntimeReconcilesInvalidCurrentShelterOnNextTick()
        {
            ConstructionDefinitionCatalog catalog = Catalog();
            var world = new ConstructionWorldState(catalog);
            var cell = new WorldCellCoordinate(0, 0);
            ConstructionInstanceId removedWall = default;
            foreach (ConstructionCellSide side in new[] { ConstructionCellSide.North, ConstructionCellSide.East, ConstructionCellSide.South, ConstructionCellSide.West })
            {
                ConstructionInstanceId id = Complete(world, Wall, ConstructionSpaceKey.Edge(cell, side), "wall-" + side).InstanceId;
                if (side == ConstructionCellSide.North) removedWall = id;
            }
            Complete(world, Floor, ConstructionSpaceKey.Surface(cell), "floor");
            Complete(world, Roof, ConstructionSpaceKey.Roof(cell), "roof");
            Complete(world, Bed, ConstructionSpaceKey.Interior(cell), "bed");

            var runtime = new ShelterRuntimeSession(
                catalog,
                new ConstructionEnclosureBlockingPolicy(new[] { Wall }),
                new ConstructionRoomAnalysisLimits(4, 64),
                new ConstructionRoofSupportPolicy(new[] { Wall }, 2),
                new ShelterCandidateEvaluationPolicy(
                    new[] { Floor }, new[] { Bed }, new[] { Wall }, true, 2, true),
                new AnchorBasedShelterIdentityCandidateStrategy(),
                new ShelterFamiliarityRules(2),
                new ShelterHomeState(System.Array.Empty<ShelterFamiliarityState>()));

            runtime.Recalculate(world.CaptureCanonicalStructures());
            ShelterPresenceTransition entered = runtime.AdvanceFixedTick(cell, 2);
            Assert.That(entered.Kind, Is.EqualTo(ShelterPresenceTransitionKind.Entered));
            Assert.That(runtime.HomeState.TryFind(runtime.CurrentShelterId, out ShelterFamiliarityState history), Is.True);
            Assert.That(history.FamiliarityHalfPoints, Is.EqualTo(1));
            runtime.RecordCompletedRest();
            runtime.RecordCompletedNight();
            runtime.RecordCompletedNight();
            runtime.RecordCompletedNight();
            Assert.That(runtime.HomeState.HasPrimaryShelter, Is.False,
                "Runtime must not invent the first primary home.");

            Assert.That(world.TryDismantleStructure(removedWall, out _, out _), Is.True);
            runtime.Recalculate(world.CaptureCanonicalStructures());
            Assert.That(runtime.HasCurrentShelter, Is.True,
                "Recalculate has no player cell and defers the presence transition to the next tick.");
            int restsBefore = history.CompletedRestCount;
            int nightsBefore = history.CompletedNightCount;
            Assert.That(runtime.RecordCompletedRest(), Is.False);
            Assert.That(runtime.RecordCompletedNight(), Is.False);
            Assert.That(history.CompletedRestCount, Is.EqualTo(restsBefore));
            Assert.That(history.CompletedNightCount, Is.EqualTo(nightsBefore));
            ShelterPresenceTransition exited = runtime.AdvanceFixedTick(cell, 1);
            Assert.That(exited.Kind, Is.EqualTo(ShelterPresenceTransitionKind.Exited));
            Assert.That(runtime.HasCurrentShelter, Is.False);
            Assert.That(runtime.HomeState.TryFind(history.ShelterId, out ShelterFamiliarityState preserved), Is.True);
            Assert.That(preserved.FamiliarityHalfPoints, Is.EqualTo(51));
        }

        [Test]
        public void LoadedHistorySurvivesValidityRecalculationAndContinuesOnEntry()
        {
            ConstructionDefinitionCatalog catalog = Catalog();
            var world = new ConstructionWorldState(catalog);
            var cell = new WorldCellCoordinate(0, 0);
            foreach (ConstructionCellSide side in new[]
                     {
                         ConstructionCellSide.North,
                         ConstructionCellSide.East,
                         ConstructionCellSide.South,
                         ConstructionCellSide.West
                     })
                Complete(world, Wall, ConstructionSpaceKey.Edge(cell, side), "wall-" + side);
            Complete(world, Floor, ConstructionSpaceKey.Surface(cell), "floor");
            Complete(world, Roof, ConstructionSpaceKey.Roof(cell), "roof");
            Complete(world, Bed, ConstructionSpaceKey.Interior(cell), "bed");

            var savedHistory = new ShelterFamiliarityState(
                new ShelterId("candidate-anchor:bed"), 20, 0, 2, 3, 80);
            var savedHome = new ShelterHomeState(
                new[] { savedHistory }, savedHistory.ShelterId, true);
            ShelterHomeState loadedHome = ShelterSaveSnapshot.Capture(savedHome).RestoreState();
            var runtime = new ShelterRuntimeSession(
                catalog,
                new ConstructionEnclosureBlockingPolicy(new[] { Wall }),
                new ConstructionRoomAnalysisLimits(4, 64),
                new ConstructionRoofSupportPolicy(new[] { Wall }, 2),
                new ShelterCandidateEvaluationPolicy(
                    new[] { Floor }, new[] { Bed }, new[] { Wall }, true, 2, true),
                new AnchorBasedShelterIdentityCandidateStrategy(),
                new ShelterFamiliarityRules(2),
                loadedHome);

            runtime.Recalculate(world.CaptureCanonicalStructures());
            Assert.That(runtime.HomeState.HasPrimaryShelter, Is.True);
            Assert.That(runtime.AdvanceFixedTick(cell, 2).Kind,
                Is.EqualTo(ShelterPresenceTransitionKind.Entered));
            Assert.That(
                runtime.HomeState.TryFind(
                    savedHistory.ShelterId,
                    out ShelterFamiliarityState continued),
                Is.True);
            Assert.That(continued.FamiliarityHalfPoints, Is.EqualTo(81));
            Assert.That(continued.CompletedNightCount, Is.EqualTo(3));
        }

        [Test]
        public void BridgeRefreshesOnlyAfterCompletedStructureRevisionChanges()
        {
            ConstructionPrototypeCatalog catalog = RuntimeCatalog();
            var inventory = new InventoryPrototypeSession(Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            var construction = new ConstructionRuntimeSession(
                catalog,
                new MonotonicConstructionInstanceIdAllocator("shelter-bridge"),
                inventory);
            var shelters = new ShelterRuntimeSession(
                catalog.CoreCatalog,
                new ConstructionEnclosureBlockingPolicy(new[] { Wall }),
                new ConstructionRoomAnalysisLimits(4, 64),
                new ConstructionRoofSupportPolicy(new[] { Wall }, 2),
                new ShelterCandidateEvaluationPolicy(
                    new[] { Floor }, new[] { Bed }, new[] { Wall }, true, 2, true),
                new AnchorBasedShelterIdentityCandidateStrategy(),
                new ShelterFamiliarityRules(2),
                inventory.Shelters);
            var bridge = new ConstructionShelterRuntimeBridge(construction, shelters);
            Assert.That(bridge.RefreshIfChanged(), Is.False);

            Assert.That(construction.Execute(ConstructionCommand.Select(Wall)), Is.True);
            ConstructionRuntimeResult placed = construction.TryPlaceSelected(
                ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), ConstructionCellSide.North));
            Assert.That(placed.Succeeded, Is.True);
            Assert.That(construction.DepositAllAvailable(placed.InstanceId).Succeeded, Is.True);
            Assert.That(construction.BeginWork(placed.InstanceId, 0L, new WorldPosition(0, 0)).Succeeded, Is.True);
            Assert.That(construction.AdvanceToTick(5L, new WorldPosition(0, 0), false, true, true).Succeeded, Is.True);
            Assert.That(construction.CompletedStructureRevision, Is.EqualTo(1));
            Assert.That(bridge.RefreshIfChanged(), Is.True);
            Assert.That(bridge.RefreshIfChanged(), Is.False);

            Assert.That(construction.TryDismantle(placed.InstanceId).Succeeded, Is.True);
            Assert.That(construction.CompletedStructureRevision, Is.EqualTo(2));
            Assert.That(bridge.RefreshIfChanged(), Is.True);
        }

        private static ConstructionDefinitionCatalog Catalog() => new ConstructionDefinitionCatalog(new[]
        {
            Definition(Floor, ConstructionSpaceKind.Surface), Definition(Wall, ConstructionSpaceKind.Edge),
            Definition(Roof, ConstructionSpaceKind.Roof), Definition(Bed, ConstructionSpaceKind.Interior)
        });

        private static ConstructionPrototypeCatalog RuntimeCatalog() => new ConstructionPrototypeCatalog(
            new[]
            {
                Prototype(Floor, ConstructionSpaceKind.Surface),
                Prototype(Wall, ConstructionSpaceKind.Edge),
                Prototype(Roof, ConstructionSpaceKind.Roof),
                Prototype(Bed, ConstructionSpaceKind.Interior)
            },
            "shelter-test-catalog",
            1);

        private static ConstructionPrototypeDefinition Prototype(
            ConstructionDefinitionId id,
            ConstructionSpaceKind kind) => new ConstructionPrototypeDefinition(
                Definition(id, kind),
                "test",
                id.Value,
                string.Empty,
                "test");

        private static ConstructionDefinition Definition(ConstructionDefinitionId id, ConstructionSpaceKind kind) =>
            new ConstructionDefinition(id, kind, 1, new[] { new ConstructionMaterialRequirement(Material, 1) });

        private static CompletedStructureState Complete(
            ConstructionWorldState world, ConstructionDefinitionId definition,
            ConstructionSpaceKey space, string idValue)
        {
            var id = new ConstructionInstanceId(idValue);
            Assert.That(world.TryStartSite(id, definition, space, out _, out _), Is.True);
            world.DepositMaterial(id, Material, 1);
            world.AddWork(id, 1);
            Assert.That(world.TryComplete(id, out CompletedStructureState result, out _), Is.True);
            return result;
        }
    }
}
