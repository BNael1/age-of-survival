using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Runtime.Inventory;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Construction
{
    /// <summary>
    /// Plain C# owner of the active ConstructionWorldState and its commands.
    /// Unity adapters only project input and present snapshots from this session.
    /// </summary>
    public sealed class ConstructionRuntimeSession
    {
        public const int PrototypeWorkTickInterval = 5;
        public const double PrototypeMaximumWorkDistance =
            InventoryPrototypeCatalog.GroundTransferMaximumDistance;

        private readonly IConstructionInstanceIdAllocator _idAllocator;
        private readonly InventoryPrototypeSession _inventoryOwner;
        private readonly ConstructionCarriedInventory _carriedInventory;
        private readonly Func<ConstructionInstanceId, ItemDefinitionId, int, ConstructionQuantityResult>
            _depositMaterial;
        private ConstructionInstanceId _activeWorkSite;
        private long _lastWorkTick;
        private bool _workActionHeld;

        public ConstructionRuntimeSession(
            ConstructionPrototypeCatalog catalog,
            IConstructionInstanceIdAllocator idAllocator,
            InventoryPrototypeSession inventoryOwner,
            Func<ConstructionInstanceId, ItemDefinitionId, int, ConstructionQuantityResult>
                depositMaterial = null)
            : this(
                catalog,
                idAllocator,
                inventoryOwner,
                new ConstructionWorldState(
                    (catalog ?? throw new ArgumentNullException(nameof(catalog))).CoreCatalog),
                depositMaterial)
        {
        }

        internal ConstructionRuntimeSession(
            ConstructionPrototypeCatalog catalog,
            IConstructionInstanceIdAllocator idAllocator,
            InventoryPrototypeSession inventoryOwner,
            ConstructionWorldState world,
            Func<ConstructionInstanceId, ItemDefinitionId, int, ConstructionQuantityResult>
                depositMaterial = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            _inventoryOwner = inventoryOwner ?? throw new ArgumentNullException(nameof(inventoryOwner));
            _carriedInventory = new ConstructionCarriedInventory(_inventoryOwner);
            World = world ?? throw new ArgumentNullException(nameof(world));
            _depositMaterial = depositMaterial ?? World.DepositMaterial;
            Mode = new ConstructionModeState();
        }

        public ConstructionPrototypeCatalog Catalog { get; }
        public ConstructionWorldState World { get; }
        public ConstructionModeState Mode { get; }
        public ConstructionRuntimeResult LastResult { get; private set; }
        public ConstructionInstanceId ActiveWorkSite => _activeWorkSite;
        public bool IsWorkActionActive => _activeWorkSite.IsValid && _workActionHeld;
        public ConstructionCarriedInventory CarriedInventory => _carriedInventory;
        public long CompletedStructureRevision { get; private set; }

        public ConstructionSaveSnapshot CaptureSaveSnapshot()
        {
            return ConstructionSaveSnapshot.Capture(
                Catalog.PersistenceCatalogId,
                Catalog.PersistenceCatalogRevision,
                _idAllocator.InstanceNamespace,
                _idAllocator.NextSequence,
                World);
        }

        public static ConstructionRuntimeSession Restore(
            ConstructionPrototypeCatalog catalog,
            RestoredConstructionState restored,
            InventoryPrototypeSession inventory)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (restored == null) throw new ArgumentNullException(nameof(restored));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!string.Equals(
                    restored.CatalogId,
                    catalog.PersistenceCatalogId,
                    StringComparison.Ordinal)
                || restored.CatalogRevision != catalog.PersistenceCatalogRevision)
            {
                throw new NotSupportedException(
                    "The restored construction catalog does not match the Runtime catalog.");
            }

            return new ConstructionRuntimeSession(
                catalog,
                new MonotonicConstructionInstanceIdAllocator(
                    restored.InstanceNamespace,
                    restored.NextInstanceSequence),
                inventory,
                restored.World);
        }

        public bool Execute(ConstructionCommand command)
        {
            bool executed = Mode.Execute(command, Catalog);
            if (executed && !Mode.IsActive && _activeWorkSite.IsValid)
                CancelWork(ConstructionRuntimeReason.ContextInterrupted);
            return executed;
        }

        public ConstructionRuntimeResult ExecutePlacement(ConstructionCommand command)
        {
            return command.Kind == ConstructionCommandKind.ConfirmPlacement
                ? TryPlaceSelected(command.Space)
                : Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.InvalidRequest,
                    default));
        }

        public ConstructionPreviewState EvaluatePreview(ConstructionSpaceKey space)
        {
            ConstructionPreviewStatus status = !space.IsValid
                ? ConstructionPreviewStatus.NotProjectable
                : World.IsOccupied(space)
                    ? ConstructionPreviewStatus.Occupied
                    : ConstructionPreviewStatus.Projectable;
            var preview = new ConstructionPreviewState(space, status);
            Mode.SetPreview(preview);
            return preview;
        }

        public void ClearPreview(ConstructionPreviewStatus status)
        {
            Mode.SetPreview(new ConstructionPreviewState(default, status));
        }

        public ConstructionRuntimeResult TryPlaceSelected(ConstructionSpaceKey space)
        {
            if (!Mode.IsActive || !Mode.HasSelection)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.NoSelection, default));
            if (!space.IsValid)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.ProjectionInvalid, default));
            if (!Catalog.TryFind(Mode.SelectedDefinitionId, out ConstructionPrototypeDefinition definition))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.UnknownDefinition, default));
            if (space.Kind != definition.Core.SpaceKind)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.ProjectionInvalid, default));
            if (World.IsOccupied(space))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.SpaceOccupied, default));

            bool IsAvailable(ConstructionInstanceId id) =>
                !World.TryFindSite(id, out _) && !World.TryFindStructure(id, out _);
            if (!_idAllocator.TryPeekNext(IsAvailable, out ConstructionInstanceId instanceId))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.IdExhausted, default));

            if (!World.TryStartSite(
                    instanceId,
                    definition.Core.Id,
                    space,
                    out _,
                    out ConstructionOperationReason reason))
            {
                return Store(new ConstructionRuntimeResult(
                    MapCoreReason(reason),
                    default,
                    coreReason: reason));
            }

            _idAllocator.Commit(instanceId);
            Mode.SetPreview(new ConstructionPreviewState(space, ConstructionPreviewStatus.Occupied));
            return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.None, instanceId));
        }

        public ConstructionRuntimeResult DepositAvailable(
            ConstructionInstanceId instanceId,
            ItemDefinitionId materialId)
        {
            if (!World.TryFindSite(instanceId, out ConstructionSiteState site))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.SiteNotFound, instanceId));

            ConstructionDefinition definition = Catalog.Require(site.DefinitionId).Core;
            if (!definition.TryFindMaterial(materialId, out ConstructionMaterialRequirement requirement))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.MaterialNotRequired, instanceId));

            int deposited = DepositedQuantity(instanceId, materialId);
            int missing = requirement.Quantity - deposited;
            if (missing <= 0)
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.MaterialNotRequired,
                    instanceId));

            ItemDefinition item = RequireOrdinaryStack(materialId);
            int available = _carriedInventory.Count(materialId);
            int quantity = Math.Min(missing, available);
            if (quantity <= 0)
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.InventoryInsufficient,
                    instanceId));

            if (!_carriedInventory.TryRemove(item, quantity, out ConstructionInventoryRemoval removal))
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.InventoryInsufficient,
                    instanceId));

            ConstructionQuantityResult depositedResult =
                _depositMaterial(instanceId, materialId, quantity);
            if (!depositedResult.Succeeded || depositedResult.Accepted != quantity)
            {
                _carriedInventory.Restore(removal);
                return Store(new ConstructionRuntimeResult(
                    MapCoreReason(depositedResult.Reason),
                    instanceId,
                    coreReason: depositedResult.Reason));
            }

            return Store(new ConstructionRuntimeResult(
                ConstructionRuntimeReason.None,
                instanceId,
                quantity));
        }

        public ConstructionRuntimeResult DepositAllAvailable(ConstructionInstanceId instanceId)
        {
            if (!World.TryFindSite(instanceId, out ConstructionSiteState site))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.SiteNotFound, instanceId));

            ConstructionDefinition definition = Catalog.Require(site.DefinitionId).Core;
            int accepted = 0;
            ConstructionRuntimeReason lastFailure = ConstructionRuntimeReason.InventoryInsufficient;
            for (int index = 0; index < definition.Materials.Count; index++)
            {
                ConstructionRuntimeResult result = DepositAvailable(
                    instanceId,
                    definition.Materials[index].DefinitionId);
                if (result.Succeeded)
                    accepted = checked(accepted + result.Accepted);
                else
                    lastFailure = result.Reason;
            }

            return Store(accepted > 0
                ? new ConstructionRuntimeResult(ConstructionRuntimeReason.None, instanceId, accepted)
                : new ConstructionRuntimeResult(lastFailure, instanceId));
        }

        public ConstructionRuntimeResult BeginWork(
            ConstructionInstanceId instanceId,
            long currentTick)
        {
            return BeginWork(
                instanceId,
                currentTick,
                _inventoryOwner.CurrentPlayerPosition);
        }

        public ConstructionRuntimeResult BeginWork(
            ConstructionInstanceId instanceId,
            long currentTick,
            WorldPosition playerPosition)
        {
            if (!Mode.IsActive)
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.ContextInterrupted,
                    instanceId));
            if (!World.TryFindSite(instanceId, out ConstructionSiteState site))
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.SiteNotFound, instanceId));
            if (currentTick < 0L)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.InvalidRequest, instanceId));
            if (!MaterialsComplete(site))
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.MaterialsIncomplete,
                    instanceId));
            if (_activeWorkSite.IsValid)
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.AnotherActionActive,
                    instanceId));
            if (HasConflictingAction())
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.AnotherActionActive,
                    instanceId));
            if (!IsInWorkRange(site.Space, playerPosition))
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.OutOfRange,
                    instanceId));

            _activeWorkSite = instanceId;
            _lastWorkTick = currentTick;
            _workActionHeld = true;
            return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.None, instanceId));
        }

        public ConstructionRuntimeResult AdvanceToTick(long currentTick)
        {
            return AdvanceToTick(
                currentTick,
                _inventoryOwner.CurrentPlayerPosition,
                playerMoved: false,
                actionHeld: _workActionHeld,
                contextCompatible: Mode.IsActive);
        }

        public ConstructionRuntimeResult AdvanceToTick(
            long currentTick,
            WorldPosition playerPosition,
            bool playerMoved,
            bool actionHeld,
            bool contextCompatible)
        {
            if (!_activeWorkSite.IsValid)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.InvalidRequest, default));
            if (!contextCompatible)
                return CancelWork(ConstructionRuntimeReason.ContextInterrupted);
            if (!actionHeld || !_workActionHeld)
                return CancelWork(ConstructionRuntimeReason.WorkActionInactive);
            if (playerMoved)
                return CancelWork(ConstructionRuntimeReason.PlayerMoved);
            if (HasConflictingAction())
                return CancelWork(ConstructionRuntimeReason.AnotherActionActive);
            if (currentTick <= _lastWorkTick)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.InvalidRequest, _activeWorkSite));
            if (!World.TryFindSite(_activeWorkSite, out ConstructionSiteState site))
            {
                ConstructionInstanceId missing = _activeWorkSite;
                ClearWorkAction();
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.SiteNotFound, missing));
            }
            if (!MaterialsComplete(site))
                return CancelWork(ConstructionRuntimeReason.MaterialsIncomplete);
            if (!IsInWorkRange(site.Space, playerPosition))
                return CancelWork(ConstructionRuntimeReason.OutOfRange);

            long elapsed = currentTick - _lastWorkTick;
            int requested = (int)Math.Min(int.MaxValue, elapsed / PrototypeWorkTickInterval);
            if (requested <= 0)
                return Store(new ConstructionRuntimeResult(ConstructionRuntimeReason.None, _activeWorkSite));

            ConstructionInstanceId instanceId = _activeWorkSite;
            ConstructionQuantityResult work = World.AddWork(instanceId, requested);
            _lastWorkTick = checked(_lastWorkTick + ((long)Math.Max(1, work.Accepted) * PrototypeWorkTickInterval));
            if (!work.Succeeded)
            {
                ClearWorkAction();
                return Store(new ConstructionRuntimeResult(
                    MapCoreReason(work.Reason),
                    instanceId,
                    coreReason: work.Reason));
            }

            if (World.IsReadyToComplete(instanceId))
            {
                if (!World.TryComplete(instanceId, out _, out ConstructionOperationReason reason))
                    throw new InvalidOperationException($"A ready construction could not complete: {reason}.");
                ClearWorkAction();
                CompletedStructureRevision = checked(CompletedStructureRevision + 1L);
            }

            return Store(new ConstructionRuntimeResult(
                ConstructionRuntimeReason.None,
                instanceId,
                work.Accepted));
        }

        public ConstructionRuntimeResult CancelWork(
            ConstructionRuntimeReason reason = ConstructionRuntimeReason.WorkActionInactive)
        {
            ConstructionInstanceId instanceId = _activeWorkSite;
            ClearWorkAction();
            return Store(new ConstructionRuntimeResult(reason, instanceId));
        }

        public ConstructionRuntimeResult TryDismantle(ConstructionInstanceId instanceId)
        {
            bool isSite = World.TryFindSite(instanceId, out ConstructionSiteState site);
            ConstructionMaterialQuantity[] expected;
            ConstructionWorldRollback rollback;
            if (isSite)
            {
                expected = World.CaptureDepositedMaterials(instanceId);
                rollback = ConstructionWorldRollback.ForSite(site, expected);
            }
            else if (World.TryFindStructure(instanceId, out CompletedStructureState structure))
            {
                expected = CompletedRecovery(Catalog.Require(structure.DefinitionId).Core);
                rollback = ConstructionWorldRollback.ForStructure(structure);
            }
            else
            {
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.StructureNotFound,
                    instanceId));
            }

            ConstructionRecoveryPlan plan;
            try
            {
                plan = _carriedInventory.PlanRecovery(
                    instanceId,
                    SpacePosition(rollback.Space),
                    expected,
                    FindItem);
            }
            catch (InvalidOperationException)
            {
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.CoreRejected,
                    instanceId));
            }

            bool dismantled = isSite
                ? World.TryDismantleSite(instanceId, out ConstructionRecovery recovery, out ConstructionOperationReason reason)
                : World.TryDismantleStructure(instanceId, out recovery, out reason);
            if (!dismantled)
                return Store(new ConstructionRuntimeResult(MapCoreReason(reason), instanceId, coreReason: reason));

            if (!RecoveryMatches(plan.Expected, recovery.Materials)
                || !_carriedInventory.TryApplyRecovery(plan))
            {
                RestoreWorld(rollback);
                return Store(new ConstructionRuntimeResult(
                    ConstructionRuntimeReason.CoreRejected,
                    instanceId));
            }

            if (_activeWorkSite.Equals(instanceId)) ClearWorkAction();
            if (!isSite) CompletedStructureRevision = checked(CompletedStructureRevision + 1L);
            return Store(new ConstructionRuntimeResult(
                ConstructionRuntimeReason.None,
                instanceId,
                plan.Total,
                groundOverflow: plan.GroundTotal));
        }

        public ConstructionSiteView[] CaptureSites()
        {
            ConstructionSiteState[] sites = World.CaptureCanonicalSites();
            var result = new ConstructionSiteView[sites.Length];
            for (int index = 0; index < sites.Length; index++)
            {
                ConstructionSiteState site = sites[index];
                result[index] = new ConstructionSiteView(
                    site,
                    Catalog.Require(site.DefinitionId),
                    World.CaptureDepositedMaterials(site.InstanceId));
            }
            return result;
        }

        public int DepositedQuantity(
            ConstructionInstanceId instanceId,
            ItemDefinitionId definitionId)
        {
            ConstructionMaterialQuantity[] materials = World.CaptureDepositedMaterials(instanceId);
            for (int index = 0; index < materials.Length; index++)
                if (materials[index].DefinitionId.Equals(definitionId)) return materials[index].Quantity;
            return 0;
        }

        public bool AreMaterialsComplete(ConstructionInstanceId instanceId)
        {
            return World.TryFindSite(instanceId, out ConstructionSiteState site)
                && MaterialsComplete(site);
        }

        private ItemDefinition RequireOrdinaryStack(ItemDefinitionId id)
        {
            ItemDefinition definition = FindItem(id);
            if (definition == null
                || !definition.Id.Equals(id)
                || definition.StateKind != ItemStateKind.Stackable
                || definition.Perishable != null)
            {
                throw new InvalidOperationException($"Construction material '{id}' is not an ordinary stackable item.");
            }
            return definition;
        }

        private ItemDefinition FindItem(ItemDefinitionId id) =>
            _inventoryOwner.Inventory.FindDefinition(id);

        private bool MaterialsComplete(ConstructionSiteState site)
        {
            ConstructionDefinition definition = Catalog.Require(site.DefinitionId).Core;
            for (int index = 0; index < definition.Materials.Count; index++)
            {
                ConstructionMaterialRequirement requirement = definition.Materials[index];
                if (DepositedQuantity(site.InstanceId, requirement.DefinitionId)
                    < requirement.Quantity)
                {
                    return false;
                }
            }
            return true;
        }

        private bool HasConflictingAction()
        {
            return _inventoryOwner.IsCraftActionActive
                || (_inventoryOwner.TransferAction != null
                    && _inventoryOwner.TransferAction.Status == TransferActionStatus.Active);
        }

        private static bool IsInWorkRange(
            ConstructionSpaceKey space,
            WorldPosition playerPosition)
        {
            double maximum = PrototypeMaximumWorkDistance;
            return playerPosition.DistanceSquaredTo(SpacePosition(space))
                <= maximum * maximum;
        }

        public static WorldPosition SpacePosition(ConstructionSpaceKey space)
        {
            if (!space.IsValid)
                throw new ArgumentException("A valid construction space is required.", nameof(space));

            double x = space.AnchorCell.X;
            double y = space.AnchorCell.Y;
            if (space.Kind == ConstructionSpaceKind.Edge)
            {
                if (space.EdgeAddress.Axis == ConstructionEdgeAxis.Vertical) x += 0.5d;
                else y += 0.5d;
            }
            return new WorldPosition(x, y);
        }

        private void ClearWorkAction()
        {
            _activeWorkSite = default;
            _workActionHeld = false;
        }

        private static bool RecoveryMatches(
            IReadOnlyList<ConstructionMaterialQuantity> expected,
            IReadOnlyList<ConstructionMaterialQuantity> actual)
        {
            if (expected.Count != actual.Count) return false;
            for (int expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
            {
                ConstructionMaterialQuantity wanted = expected[expectedIndex];
                bool found = false;
                for (int actualIndex = 0; actualIndex < actual.Count; actualIndex++)
                {
                    ConstructionMaterialQuantity candidate = actual[actualIndex];
                    if (candidate.DefinitionId.Equals(wanted.DefinitionId)
                        && candidate.Quantity == wanted.Quantity)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        private void RestoreWorld(ConstructionWorldRollback rollback)
        {
            if (!World.TryStartSite(
                    rollback.InstanceId,
                    rollback.DefinitionId,
                    rollback.Space,
                    out _,
                    out ConstructionOperationReason startReason))
            {
                throw new InvalidOperationException(
                    $"A failed construction recovery could not restore its world site: {startReason}.");
            }

            ConstructionDefinition definition = Catalog.Require(rollback.DefinitionId).Core;
            IReadOnlyList<ConstructionMaterialQuantity> materials = rollback.WasCompleted
                ? RequirementsAsQuantities(definition)
                : rollback.Deposited;
            for (int index = 0; index < materials.Count; index++)
            {
                ConstructionMaterialQuantity material = materials[index];
                ConstructionQuantityResult deposit = World.DepositMaterial(
                    rollback.InstanceId,
                    material.DefinitionId,
                    material.Quantity);
                if (!deposit.Succeeded || deposit.Accepted != material.Quantity)
                    throw new InvalidOperationException(
                        "A failed construction recovery could not restore deposited materials.");
            }

            int work = rollback.WasCompleted
                ? definition.RequiredWorkUnits
                : rollback.WorkCompleted;
            if (work > 0)
            {
                ConstructionQuantityResult restoredWork = World.AddWork(
                    rollback.InstanceId,
                    work);
                if (!restoredWork.Succeeded || restoredWork.Accepted != work)
                    throw new InvalidOperationException(
                        "A failed construction recovery could not restore construction work.");
            }

            if (rollback.WasCompleted
                && !World.TryComplete(
                    rollback.InstanceId,
                    out _,
                    out ConstructionOperationReason completeReason))
            {
                throw new InvalidOperationException(
                    $"A failed construction recovery could not restore its completed structure: {completeReason}.");
            }
        }

        private static ConstructionMaterialQuantity[] RequirementsAsQuantities(
            ConstructionDefinition definition)
        {
            var result = new ConstructionMaterialQuantity[definition.Materials.Count];
            for (int index = 0; index < definition.Materials.Count; index++)
            {
                ConstructionMaterialRequirement requirement = definition.Materials[index];
                result[index] = new ConstructionMaterialQuantity(
                    requirement.DefinitionId,
                    requirement.Quantity);
            }
            return result;
        }

        private static ConstructionMaterialQuantity[] CompletedRecovery(ConstructionDefinition definition)
        {
            var result = new List<ConstructionMaterialQuantity>();
            for (int index = 0; index < definition.Materials.Count; index++)
            {
                ConstructionMaterialRequirement requirement = definition.Materials[index];
                int recovered = (int)(((long)requirement.Quantity
                    * ConstructionWorldState.CompletedRecoveryPercent) / 100L);
                if (recovered > 0)
                    result.Add(new ConstructionMaterialQuantity(requirement.DefinitionId, recovered));
            }
            return result.ToArray();
        }

        private sealed class ConstructionWorldRollback
        {
            private ConstructionWorldRollback(
                ConstructionInstanceId instanceId,
                ConstructionDefinitionId definitionId,
                ConstructionSpaceKey space,
                ConstructionMaterialQuantity[] deposited,
                int workCompleted,
                bool wasCompleted)
            {
                InstanceId = instanceId;
                DefinitionId = definitionId;
                Space = space;
                Deposited = deposited;
                WorkCompleted = workCompleted;
                WasCompleted = wasCompleted;
            }

            public ConstructionInstanceId InstanceId { get; }
            public ConstructionDefinitionId DefinitionId { get; }
            public ConstructionSpaceKey Space { get; }
            public ConstructionMaterialQuantity[] Deposited { get; }
            public int WorkCompleted { get; }
            public bool WasCompleted { get; }

            public static ConstructionWorldRollback ForSite(
                ConstructionSiteState site,
                ConstructionMaterialQuantity[] deposited)
            {
                return new ConstructionWorldRollback(
                    site.InstanceId,
                    site.DefinitionId,
                    site.Space,
                    deposited,
                    site.WorkCompletedUnits,
                    false);
            }

            public static ConstructionWorldRollback ForStructure(
                CompletedStructureState structure)
            {
                return new ConstructionWorldRollback(
                    structure.InstanceId,
                    structure.DefinitionId,
                    structure.Space,
                    Array.Empty<ConstructionMaterialQuantity>(),
                    0,
                    true);
            }
        }

        private ConstructionRuntimeResult Store(ConstructionRuntimeResult result)
        {
            LastResult = result;
            return result;
        }

        private static ConstructionRuntimeReason MapCoreReason(ConstructionOperationReason reason)
        {
            switch (reason)
            {
                case ConstructionOperationReason.None:
                    return ConstructionRuntimeReason.None;
                case ConstructionOperationReason.SpaceOccupied:
                    return ConstructionRuntimeReason.SpaceOccupied;
                case ConstructionOperationReason.SiteNotFound:
                    return ConstructionRuntimeReason.SiteNotFound;
                case ConstructionOperationReason.StructureNotFound:
                    return ConstructionRuntimeReason.StructureNotFound;
                case ConstructionOperationReason.MaterialNotRequired:
                case ConstructionOperationReason.MaterialRequirementSatisfied:
                    return ConstructionRuntimeReason.MaterialNotRequired;
                case ConstructionOperationReason.WorkRequirementSatisfied:
                    return ConstructionRuntimeReason.WorkAlreadyComplete;
                default:
                    return ConstructionRuntimeReason.CoreRejected;
            }
        }
    }

    public static class ConstructionRuntimeSessionProvider
    {
        private static ConstructionRuntimeSession _current;
        private static InventoryPrototypeSession _inventoryOwner;

        public static ConstructionRuntimeSession Current
        {
            get
            {
                InventoryPrototypeSession inventory = InventoryPrototypeSessionProvider.Current;
                if (_current == null || !ReferenceEquals(inventory, _inventoryOwner))
                {
                    _inventoryOwner = inventory;
                    _current = Create(inventory);
                }
                return _current;
            }
        }

        public static void ResetForNewGame()
        {
            _inventoryOwner = InventoryPrototypeSessionProvider.Current;
            _current = Create(_inventoryOwner);
        }

        internal static ConstructionRuntimeSession PrepareRestored(
            RestoredConstructionState restored,
            InventoryPrototypeSession inventory)
        {
            if (restored == null) throw new ArgumentNullException(nameof(restored));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            ConstructionPrototypeCatalog catalog =
                ConstructionPrototypeCatalog.CreateDefault();
            return ConstructionRuntimeSession.Restore(
                catalog,
                restored,
                inventory);
        }

        internal static void InstallPrepared(
            ConstructionRuntimeSession session,
            InventoryPrototypeSession inventory)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            _current = session;
            _inventoryOwner = inventory;
        }

        private static ConstructionRuntimeSession Create(InventoryPrototypeSession inventory)
        {
            return new ConstructionRuntimeSession(
                ConstructionPrototypeCatalog.CreateDefault(),
                new MonotonicConstructionInstanceIdAllocator(
                    ConstructionSaveDefaults.PrototypeInstanceNamespace),
                inventory);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            _current = null;
            _inventoryOwner = null;
        }
    }
}
