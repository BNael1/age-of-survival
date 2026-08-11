using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class ConstructionRuntimeSessionTests
    {
        private static readonly WorldPosition Near = new WorldPosition(0d, 0d);
        private static readonly WorldPosition Far = new WorldPosition(10d, 10d);

        [Test]
        public void PrototypeCatalog_ProvidesSurfaceAndEdgeDefinitions()
        {
            ConstructionPrototypeCatalog catalog = ConstructionPrototypeCatalog.CreateDefault();

            Assert.That(catalog.Definitions, Has.Count.EqualTo(3));
            Assert.That(catalog.Require(ConstructionPrototypeCatalog.FloorId).Core.SpaceKind,
                Is.EqualTo(ConstructionSpaceKind.Surface));
            Assert.That(catalog.Require(ConstructionPrototypeCatalog.WallId).Core.SpaceKind,
                Is.EqualTo(ConstructionSpaceKind.Edge));
            Assert.That(catalog.Require(ConstructionPrototypeCatalog.OpeningId).Core.SpaceKind,
                Is.EqualTo(ConstructionSpaceKind.Edge));
        }

        [Test]
        public void MonotonicAllocator_AdvancesOnlyAfterCommittedSuccess()
        {
            var allocator = new MonotonicConstructionInstanceIdAllocator("test", 7L);

            Assert.That(allocator.TryPeekNext(_ => true, out ConstructionInstanceId first), Is.True);
            Assert.That(first.Value, Is.EqualTo("test:0000000007"));
            Assert.That(allocator.NextSequence, Is.EqualTo(7L));
            Assert.That(allocator.TryPeekNext(_ => true, out ConstructionInstanceId repeated), Is.True);
            Assert.That(repeated, Is.EqualTo(first));

            allocator.Commit(first);
            Assert.That(allocator.NextSequence, Is.EqualTo(8L));
            Assert.Throws<InvalidOperationException>(() => allocator.Commit(first));
        }

        [Test]
        public void MonotonicAllocator_SkipsKnownCollisionWithoutCommittingIt()
        {
            var allocator = new MonotonicConstructionInstanceIdAllocator("test", 1L);

            Assert.That(allocator.TryPeekNext(
                candidate => candidate.Value != "test:0000000001",
                out ConstructionInstanceId candidate), Is.True);
            Assert.That(candidate.Value, Is.EqualTo("test:0000000002"));
            Assert.That(allocator.NextSequence, Is.EqualTo(1L));

            allocator.Commit(candidate);
            Assert.That(allocator.NextSequence, Is.EqualTo(3L));
        }

        [Test]
        public void ModeState_SelectsAndClosesWithoutGameplayGateOrWorldMutation()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out _);

            Select(session, ConstructionPrototypeCatalog.FloorId);
            Assert.That(session.Mode.IsActive, Is.True);
            Assert.That(session.World.SiteCount, Is.Zero);

            session.Execute(ConstructionCommand.Close());
            Assert.That(session.Mode.IsActive, Is.False);
            Assert.That(session.Mode.SelectedDefinitionId,
                Is.EqualTo(ConstructionPrototypeCatalog.FloorId));
            Assert.That(session.World.SiteCount, Is.Zero);
        }

        [Test]
        public void Preview_DetectsOccupancyWithoutMutatingWorld()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out _);
            Select(session, ConstructionPrototypeCatalog.FloorId);
            ConstructionSpaceKey space = Surface(3L, 4L);

            Assert.That(session.EvaluatePreview(space).IsTechnicallyPlaceable, Is.True);
            Assert.That(session.World.SiteCount, Is.Zero);
            Assert.That(session.TryPlaceSelected(space).Succeeded, Is.True);
            Assert.That(session.EvaluatePreview(space).Status,
                Is.EqualTo(ConstructionPreviewStatus.Occupied));
        }

        [Test]
        public void Placement_StartsEmptySitesAndKeepsSelectionForRepeatPlacement()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out _);
            Select(session, ConstructionPrototypeCatalog.FloorId);

            ConstructionRuntimeResult first = session.TryPlaceSelected(Surface(0L, 0L));
            ConstructionRuntimeResult second = session.TryPlaceSelected(Surface(1L, 0L));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.InstanceId, Is.Not.EqualTo(first.InstanceId));
            Assert.That(session.World.CaptureDepositedMaterials(first.InstanceId), Is.Empty);
            Assert.That(session.Mode.SelectedDefinitionId,
                Is.EqualTo(ConstructionPrototypeCatalog.FloorId));
        }

        [Test]
        public void FailedOccupiedPlacement_DoesNotConsumeInstanceSequence()
        {
            ConstructionRuntimeSession session = CreateSession(
                out MonotonicConstructionInstanceIdAllocator allocator,
                out _);
            Select(session, ConstructionPrototypeCatalog.FloorId);
            ConstructionSpaceKey space = Surface(0L, 0L);

            Assert.That(session.TryPlaceSelected(space).Succeeded, Is.True);
            Assert.That(session.TryPlaceSelected(space).Reason,
                Is.EqualTo(ConstructionRuntimeReason.SpaceOccupied));
            Assert.That(allocator.NextSequence, Is.EqualTo(2L));
        }

        [Test]
        public void EdgeAlias_UsesCanonicalCoreOccupancy()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out _);
            Select(session, ConstructionPrototypeCatalog.WallId);
            ConstructionSpaceKey east = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(31L, 5L), ConstructionCellSide.East);
            ConstructionSpaceKey west = ConstructionSpaceKey.Edge(
                new WorldCellCoordinate(32L, 5L), ConstructionCellSide.West);

            Assert.That(session.TryPlaceSelected(east).Succeeded, Is.True);
            Assert.That(session.TryPlaceSelected(west).Reason,
                Is.EqualTo(ConstructionRuntimeReason.SpaceOccupied));
        }

        [Test]
        public void DepositAvailable_ConsumesMainContainerFirst()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
            Seed(inventory.BagContainer, InventoryPrototypeCatalog.Branches, 2);
            EquipBag(inventory);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult deposit = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(deposit.Accepted, Is.EqualTo(2));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.Zero);
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(2));
        }

        [Test]
        public void DepositAvailable_UsesEquippedBagWhenMainIsEmpty()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.BagContainer, InventoryPrototypeCatalog.Branches, 2);
            EquipBag(inventory);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult deposit = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(deposit.Succeeded, Is.True);
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.Zero);
            Assert.That(session.DepositedQuantity(site, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(2));
        }

        [Test]
        public void DepositAvailable_SplitsDeterministicallyAcrossMainAndEquippedBag()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 1);
            Seed(inventory.BagContainer, InventoryPrototypeCatalog.Branches, 1);
            EquipBag(inventory);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult deposit = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(deposit.Accepted, Is.EqualTo(2));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.Zero);
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.Zero);
        }

        [Test]
        public void DepositAvailable_IgnoresUnequippedBag()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.BagContainer, InventoryPrototypeCatalog.Branches, 2);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult deposit = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(deposit.Reason, Is.EqualTo(ConstructionRuntimeReason.InventoryInsufficient));
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(2));
            Assert.That(session.World.CaptureDepositedMaterials(site), Is.Empty);
        }

        [Test]
        public void DepositAvailable_InsufficientAcrossCarriedContainersDepositsExactPartialQuantity()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 1);
            EquipBag(inventory);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult deposit = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(deposit.Succeeded, Is.True);
            Assert.That(deposit.Accepted, Is.EqualTo(1));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.Zero);
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.Zero);
            Assert.That(session.DepositedQuantity(site, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(1));
        }

        [Test]
        public void DepositAvailable_NeverConsumesGroundContainersAutomatically()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            GroundContainerState ground = GroundWith(
                "construction-source-test",
                InventoryPrototypeCatalog.Branches,
                2);
            Assert.That(inventory.TryRegisterGroundContainer(ground), Is.True);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult deposit = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(deposit.Reason, Is.EqualTo(ConstructionRuntimeReason.InventoryInsufficient));
            Assert.That(Count(ground.Container, InventoryPrototypeCatalog.Branches), Is.EqualTo(2));
        }

        [Test]
        public void DepositAvailable_SatisfiedOrWrongMaterialLeavesInventoryUnchanged()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 4);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Wood, 1);
            ConstructionInstanceId site = PlaceFloor(session);

            Assert.That(session.DepositAvailable(site, InventoryPrototypeCatalog.Wood.Id).Reason,
                Is.EqualTo(ConstructionRuntimeReason.MaterialNotRequired));
            Assert.That(session.DepositAvailable(site, InventoryPrototypeCatalog.Branches.Id).Accepted,
                Is.EqualTo(2));
            Assert.That(session.DepositAvailable(site, InventoryPrototypeCatalog.Branches.Id).Reason,
                Is.EqualTo(ConstructionRuntimeReason.MaterialNotRequired));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(2));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Wood), Is.EqualTo(1));
        }

        [Test]
        public void DepositAvailable_RollsBackEveryCarriedDebitWhenCoreRejects()
        {
            ConstructionRuntimeSession session = CreateSession(
                out _,
                out InventoryPrototypeSession inventory,
                (instanceId, materialId, quantity) => new ConstructionQuantityResult(
                    quantity,
                    0,
                    ConstructionOperationReason.InvalidRequest));
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 1);
            Seed(inventory.BagContainer, InventoryPrototypeCatalog.Branches, 1);
            EquipBag(inventory);
            ConstructionInstanceId site = PlaceFloor(session);

            ConstructionRuntimeResult result = session.DepositAvailable(
                site,
                InventoryPrototypeCatalog.Branches.Id);

            Assert.That(result.Reason, Is.EqualTo(ConstructionRuntimeReason.CoreRejected));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(1));
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(1));
            Assert.That(session.World.CaptureDepositedMaterials(site), Is.Empty);
        }

        [Test]
        public void BeginWork_RefusesUntilEveryMaterialIsDeposited()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
            ConstructionInstanceId site = PlaceFloor(session);
            session.DepositAvailable(site, InventoryPrototypeCatalog.Branches.Id);

            ConstructionRuntimeResult result = session.BeginWork(site, 0L, Near);

            Assert.That(result.Reason, Is.EqualTo(ConstructionRuntimeReason.MaterialsIncomplete));
            Assert.That(session.ActiveWorkSite.IsValid, Is.False);
            AssertWork(session, site, 0);
        }

        [Test]
        public void ActiveWork_AdvancesOnlyOnExistingFixedTickWhileHeld()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);

            Assert.That(session.BeginWork(site, 10L, Near).Succeeded, Is.True);
            Assert.That(session.AdvanceToTick(14L, Near, false, true, true).Accepted, Is.Zero);
            AssertWork(session, site, 0);
            Assert.That(session.AdvanceToTick(15L, Near, false, true, true).Accepted, Is.EqualTo(1));
            AssertWork(session, site, 1);
        }

        [Test]
        public void BeginWork_RefusesASecondConstructionActionWhileOneIsActive()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId first, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Stones, 1);
            Select(session, ConstructionPrototypeCatalog.FloorId);
            ConstructionRuntimeResult second = session.TryPlaceSelected(Surface(1L, 0L));
            Assert.That(second.Succeeded, Is.True);
            Assert.That(session.DepositAllAvailable(second.InstanceId).Accepted, Is.EqualTo(3));

            Assert.That(session.BeginWork(first, 0L, Near).Succeeded, Is.True);
            Assert.That(session.BeginWork(second.InstanceId, 0L, new WorldPosition(1d, 0d)).Reason,
                Is.EqualTo(ConstructionRuntimeReason.AnotherActionActive));
            Assert.That(session.ActiveWorkSite, Is.EqualTo(first));
        }

        [Test]
        public void WorkActionRelease_InterruptsAndPreventsBackgroundProgress()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);
            session.BeginWork(site, 0L, Near);

            ConstructionRuntimeResult interrupted = session.AdvanceToTick(
                5L, Near, false, actionHeld: false, contextCompatible: true);
            ConstructionRuntimeResult later = session.AdvanceToTick(100L);

            Assert.That(interrupted.Reason, Is.EqualTo(ConstructionRuntimeReason.WorkActionInactive));
            Assert.That(later.Reason, Is.EqualTo(ConstructionRuntimeReason.InvalidRequest));
            AssertWork(session, site, 0);
        }

        [Test]
        public void PlayerMovement_InterruptsWorkBeforeProgress()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);
            session.BeginWork(site, 0L, Near);

            ConstructionRuntimeResult interrupted = session.AdvanceToTick(
                5L, Near, playerMoved: true, actionHeld: true, contextCompatible: true);

            Assert.That(interrupted.Reason, Is.EqualTo(ConstructionRuntimeReason.PlayerMoved));
            AssertWork(session, site, 0);
        }

        [Test]
        public void WorkRange_IsCheckedAtStartAndEveryTick()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);

            Assert.That(session.BeginWork(site, 0L, Far).Reason,
                Is.EqualTo(ConstructionRuntimeReason.OutOfRange));
            Assert.That(session.BeginWork(site, 0L, Near).Succeeded, Is.True);
            Assert.That(session.AdvanceToTick(5L, Far, false, true, true).Reason,
                Is.EqualTo(ConstructionRuntimeReason.OutOfRange));
            AssertWork(session, site, 0);
        }

        [Test]
        public void ClosingConstructionContext_InterruptsActiveWork()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);
            session.BeginWork(site, 0L, Near);

            session.Execute(ConstructionCommand.Close());

            Assert.That(session.ActiveWorkSite.IsValid, Is.False);
            Assert.That(session.LastResult.Reason, Is.EqualTo(ConstructionRuntimeReason.ContextInterrupted));
            AssertWork(session, site, 0);
        }

        [Test]
        public void MissingSite_InterruptsActiveWork()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);
            session.BeginWork(site, 0L, Near);
            Assert.That(session.World.TryDismantleSite(
                site, out _, out ConstructionOperationReason reason), Is.True, reason.ToString());

            ConstructionRuntimeResult result = session.AdvanceToTick(5L, Near, false, true, true);

            Assert.That(result.Reason, Is.EqualTo(ConstructionRuntimeReason.SiteNotFound));
            Assert.That(session.ActiveWorkSite.IsValid, Is.False);
        }

        [Test]
        public void NoActiveAction_NeverAdvancesSuppliedSite()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);

            Assert.That(session.AdvanceToTick(500L).Reason,
                Is.EqualTo(ConstructionRuntimeReason.InvalidRequest));
            AssertWork(session, site, 0);
        }

        [Test]
        public void FullySuppliedActiveWork_CompletesExactlyOnce()
        {
            ConstructionRuntimeSession session = SuppliedFloor(out ConstructionInstanceId site, out _);
            session.BeginWork(site, 10L, Near);

            session.AdvanceToTick(
                10L + (29L * ConstructionRuntimeSession.PrototypeWorkTickInterval),
                Near, false, true, true);
            Assert.That(session.World.SiteCount, Is.EqualTo(1));

            session.AdvanceToTick(
                10L + (30L * ConstructionRuntimeSession.PrototypeWorkTickInterval),
                Near, false, true, true);
            Assert.That(session.World.SiteCount, Is.Zero);
            Assert.That(session.World.StructureCount, Is.EqualTo(1));
            Assert.That(session.AdvanceToTick(1000L).Reason,
                Is.EqualTo(ConstructionRuntimeReason.InvalidRequest));
            Assert.That(session.World.StructureCount, Is.EqualTo(1));
        }

        [Test]
        public void DismantleIncompleteSite_RecoversOneHundredPercentIntoMain()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
            ConstructionInstanceId site = PlaceFloor(session);
            session.DepositAvailable(site, InventoryPrototypeCatalog.Branches.Id);

            ConstructionRuntimeResult result = session.TryDismantle(site);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(result.GroundOverflow, Is.Zero);
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(2));
            Assert.That(session.World.OccupiedSpaceCount, Is.Zero);
        }

        [Test]
        public void Dismantle_SplitsRecoveryAcrossMainThenEquippedBag()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
            ConstructionInstanceId site = PlaceFloor(session);
            session.DepositAvailable(site, InventoryPrototypeCatalog.Branches.Id);
            FillMain(inventory, 750L);
            EquipBag(inventory);
            int mainBefore = Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches);

            ConstructionRuntimeResult result = session.TryDismantle(site);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.GroundOverflow, Is.Zero);
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches),
                Is.EqualTo(mainBefore + 1));
            Assert.That(Count(inventory.BagContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(1));
        }

        [Test]
        public void Dismantle_PartialOverflowGoesExactlyToGround()
        {
            ConstructionRuntimeSession session = CreateSession(out _, out InventoryPrototypeSession inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 3);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Wood, 1);
            Select(session, ConstructionPrototypeCatalog.WallId);
            ConstructionRuntimeResult placed = session.TryPlaceSelected(
                ConstructionSpaceKey.Edge(new WorldCellCoordinate(0L, 0L), ConstructionCellSide.East));
            session.DepositAllAvailable(placed.InstanceId);
            Complete(session, placed.InstanceId, new WorldPosition(0.5d, 0d));
            FillMain(inventory, 750L);
            int carriedBefore = Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches);

            ConstructionRuntimeResult result = session.TryDismantle(placed.InstanceId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(result.GroundOverflow, Is.EqualTo(1));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches),
                Is.EqualTo(carriedBefore + 1));
            AssertGroundRecovery(inventory, placed.InstanceId, InventoryPrototypeCatalog.Branches, 1);
        }

        [Test]
        public void Dismantle_TotalOverflowPreservesEveryRecoveredItemOnGround()
        {
            ConstructionRuntimeSession session = SuppliedFloor(
                out ConstructionInstanceId site,
                out InventoryPrototypeSession inventory);
            Complete(session, site, Near);
            FillMain(inventory, 250L);
            int carriedBefore = Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches);

            ConstructionRuntimeResult result = session.TryDismantle(site);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Accepted, Is.EqualTo(1));
            Assert.That(result.GroundOverflow, Is.EqualTo(1));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches),
                Is.EqualTo(carriedBefore));
            AssertGroundRecovery(inventory, site, InventoryPrototypeCatalog.Branches, 1);
            Assert.That(session.World.OccupiedSpaceCount, Is.Zero);
        }

        [Test]
        public void Dismantle_OverflowIdentityCollisionLeavesStructureAndItemsUntouched()
        {
            ConstructionRuntimeSession session = SuppliedFloor(
                out ConstructionInstanceId site,
                out InventoryPrototypeSession inventory);
            Complete(session, site, Near);
            FillMain(inventory, 250L);
            GroundContainerState collision = GroundWith(
                "construction-recovery-" + site.Value,
                InventoryPrototypeCatalog.Branches,
                1);
            Assert.That(inventory.TryRegisterGroundContainer(collision), Is.True);
            int carriedBefore = Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches);

            ConstructionRuntimeResult result = session.TryDismantle(site);

            Assert.That(result.Reason, Is.EqualTo(ConstructionRuntimeReason.CoreRejected));
            Assert.That(session.World.TryFindStructure(site, out _), Is.True,
                "Destination planning must fail before the destructive Core mutation.");
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches),
                Is.EqualTo(carriedBefore));
            Assert.That(Count(collision.Container, InventoryPrototypeCatalog.Branches), Is.EqualTo(1));
            Assert.That(inventory.GroundContainers, Has.Count.EqualTo(1));
        }

        [Test]
        public void CompletedStructureRecovery_IsSeventyPercentFlooredPerMaterial()
        {
            ConstructionRuntimeSession session = SuppliedFloor(
                out ConstructionInstanceId site,
                out InventoryPrototypeSession inventory);
            Complete(session, site, Near);

            ConstructionRuntimeResult result = session.TryDismantle(site);

            Assert.That(result.Accepted, Is.EqualTo(1));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Branches), Is.EqualTo(1));
            Assert.That(Count(inventory.MainContainer, InventoryPrototypeCatalog.Stones), Is.Zero,
                "floor(1 * 70%) must remain zero");
        }

        private static ConstructionRuntimeSession CreateSession(
            out MonotonicConstructionInstanceIdAllocator allocator,
            out InventoryPrototypeSession inventory,
            Func<ConstructionInstanceId, ItemDefinitionId, int, ConstructionQuantityResult>
                depositMaterial = null)
        {
            allocator = new MonotonicConstructionInstanceIdAllocator("test");
            inventory = new InventoryPrototypeSession(Array.Empty<AgeOfSurvival.Core.Resources.ResourceState>());
            Clear(inventory.MainContainer, InventoryPrototypeCatalog.Branches);
            Clear(inventory.MainContainer, InventoryPrototypeCatalog.Stones);
            Clear(inventory.MainContainer, InventoryPrototypeCatalog.Wood);
            Clear(inventory.BagContainer, InventoryPrototypeCatalog.Branches);
            Clear(inventory.BagContainer, InventoryPrototypeCatalog.Stones);
            Clear(inventory.BagContainer, InventoryPrototypeCatalog.Wood);
            return new ConstructionRuntimeSession(
                ConstructionPrototypeCatalog.CreateDefault(),
                allocator,
                inventory,
                depositMaterial);
        }

        private static ConstructionRuntimeSession SuppliedFloor(
            out ConstructionInstanceId site,
            out InventoryPrototypeSession inventory)
        {
            ConstructionRuntimeSession session = CreateSession(out _, out inventory);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
            Seed(inventory.MainContainer, InventoryPrototypeCatalog.Stones, 1);
            site = PlaceFloor(session);
            Assert.That(session.DepositAllAvailable(site).Accepted, Is.EqualTo(3));
            return session;
        }

        private static ConstructionInstanceId PlaceFloor(ConstructionRuntimeSession session)
        {
            Select(session, ConstructionPrototypeCatalog.FloorId);
            ConstructionRuntimeResult placed = session.TryPlaceSelected(Surface(0L, 0L));
            Assert.That(placed.Succeeded, Is.True);
            return placed.InstanceId;
        }

        private static void Complete(
            ConstructionRuntimeSession session,
            ConstructionInstanceId site,
            WorldPosition position)
        {
            Assert.That(session.BeginWork(site, 0L, position).Succeeded, Is.True);
            ConstructionDefinition definition = session.Catalog.Require(
                session.World.TryFindSite(site, out ConstructionSiteState state)
                    ? state.DefinitionId
                    : default).Core;
            ConstructionRuntimeResult work = session.AdvanceToTick(
                definition.RequiredWorkUnits * ConstructionRuntimeSession.PrototypeWorkTickInterval,
                position,
                false,
                true,
                true);
            Assert.That(work.Succeeded, Is.True);
            Assert.That(session.World.TryFindStructure(site, out _), Is.True);
        }

        private static void AssertWork(
            ConstructionRuntimeSession session,
            ConstructionInstanceId site,
            int expected)
        {
            Assert.That(session.World.TryFindSite(site, out ConstructionSiteState state), Is.True);
            Assert.That(state.WorkCompletedUnits, Is.EqualTo(expected));
        }

        private static void EquipBag(InventoryPrototypeSession inventory)
        {
            var selection = new InventorySelection(
                inventory.MainContainer.Id,
                InventoryPrototypeCatalog.Bag.Id,
                inventory.Bag.InstanceId,
                true);
            Assert.That(inventory.Commands.Equip(selection, EquipmentSlot.Back).Succeeded, Is.True);
        }

        private static void FillMain(InventoryPrototypeSession inventory, long remainingUnits)
        {
            long current = inventory.MainContainer.RemainingCapacity.Units;
            Assert.That(current, Is.GreaterThanOrEqualTo(remainingUnits));
            long toFill = current - remainingUnits;
            int flakes = toFill % InventoryPrototypeCatalog.Branches.UnitEncumbrance.Units == 0L
                ? 0
                : 1;
            if (flakes > 0)
            {
                Seed(inventory.MainContainer, InventoryPrototypeCatalog.StoneFlakes, flakes);
                toFill -= InventoryPrototypeCatalog.StoneFlakes.UnitEncumbrance.Units;
            }
            int branches = (int)(toFill / InventoryPrototypeCatalog.Branches.UnitEncumbrance.Units);
            if (branches > 0) Seed(inventory.MainContainer, InventoryPrototypeCatalog.Branches, branches);
            Assert.That(inventory.MainContainer.RemainingCapacity.Units, Is.EqualTo(remainingUnits));
        }

        private static GroundContainerState GroundWith(
            string id,
            ItemDefinition definition,
            int quantity)
        {
            var container = new ContainerState(
                new ContainerId("container-" + id),
                new ContainerDefinition(id, id, new EncumbranceValue(100000L)));
            Seed(container, definition, quantity);
            return new GroundContainerState(
                new GroundContainerId("ground-" + id),
                Near,
                container);
        }

        private static void AssertGroundRecovery(
            InventoryPrototypeSession inventory,
            ConstructionInstanceId site,
            ItemDefinition definition,
            int quantity)
        {
            string expected = "ground-construction-recovery-" + site.Value;
            GroundContainerState found = null;
            for (int index = 0; index < inventory.GroundContainers.Count; index++)
            {
                if (inventory.GroundContainers[index].Id.Value == expected)
                    found = inventory.GroundContainers[index];
            }
            Assert.That(found, Is.Not.Null);
            Assert.That(Count(found.Container, definition), Is.EqualTo(quantity));
            Assert.That(inventory.GroundContainers, Has.Count.EqualTo(1));
        }

        private static void Clear(ContainerState container, ItemDefinition definition)
        {
            int quantity = Count(container, definition);
            if (quantity <= 0) return;
            Assert.That(InventoryOperations.RemoveStack(container, definition, quantity).Succeeded, Is.True);
        }

        private static int Count(ContainerState container, ItemDefinition definition) =>
            InventoryOperations.Count(container, definition.Id);

        private static void Seed(ContainerState container, ItemDefinition definition, int quantity)
        {
            Assert.That(InventoryOperations.AddStack(container, definition, quantity).Accepted,
                Is.EqualTo(quantity));
        }

        private static void Select(ConstructionRuntimeSession session, ConstructionDefinitionId id)
        {
            Assert.That(session.Execute(ConstructionCommand.Select(id)), Is.True);
        }

        private static ConstructionSpaceKey Surface(long x, long y) =>
            ConstructionSpaceKey.Surface(new WorldCellCoordinate(x, y));
    }
}
