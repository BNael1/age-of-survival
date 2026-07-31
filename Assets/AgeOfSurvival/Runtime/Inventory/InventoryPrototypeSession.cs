using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Inventory
{
    /// <summary>
    /// Plain C# prototype session that owns Core state. MonoBehaviours only adapt it.
    /// </summary>
    public sealed class InventoryPrototypeSession
    {
        private readonly List<ResourceState> _resources = new List<ResourceState>();
        private readonly List<GroundContainerState> _groundContainers = new List<GroundContainerState>();
        private readonly IReadOnlyList<ResourceState> _readOnlyResources;
        private readonly IReadOnlyList<GroundContainerState> _readOnlyGroundContainers;
        private int _nextTransferAction = 1;

        public InventoryPrototypeSession()
        {
            _readOnlyResources = _resources.AsReadOnly();
            _readOnlyGroundContainers = _groundContainers.AsReadOnly();
            MainContainer = new ContainerState(
                InventoryPrototypeCatalog.MainContainerId,
                new ContainerDefinition(
                    "player-main",
                    "Carried inventory",
                    new EncumbranceValue(InventoryPrototypeCatalog.MainCapacityUnits)));
            BagContainer = new ContainerState(
                InventoryPrototypeCatalog.BagContainerId,
                new ContainerDefinition(
                    "prototype-bag",
                    "Prototype backpack",
                    new EncumbranceValue(InventoryPrototypeCatalog.BagCapacityUnits)));

            Tool = new UniqueItemState(
                InventoryPrototypeCatalog.Tool.Id,
                new ItemInstanceId("prototype-tool-01"));
            Bag = new UniqueItemState(
                InventoryPrototypeCatalog.Bag.Id,
                new ItemInstanceId("prototype-bag-01"),
                BagContainer.Id);

            InventoryOperations.AddStack(MainContainer, InventoryPrototypeCatalog.Branches, 6);
            InventoryOperations.AddStack(MainContainer, InventoryPrototypeCatalog.Stones, 3);
            InventoryOperations.AddUnique(MainContainer, InventoryPrototypeCatalog.Tool, Tool);
            InventoryOperations.AddUnique(MainContainer, InventoryPrototypeCatalog.Bag, Bag);
            InventoryOperations.AddStack(BagContainer, InventoryPrototypeCatalog.Stones, 2);

            Inventory = new PlayerInventoryState(
                MainContainer.Id,
                InventoryPrototypeCatalog.Definitions,
                new[] { MainContainer, BagContainer });
            Commands = new InventoryPrototypeCommands(Inventory);

            for (int index = 0; index < InventoryPrototypeCatalog.ResourcePositions.Length; index++)
            {
                _resources.Add(new ResourceState(
                    new ResourceId($"debug-resource-{index + 1:00}"),
                    InventoryPrototypeCatalog.ResourcePositions[index]));
            }
        }

        public PlayerInventoryState Inventory { get; }
        public ContainerState MainContainer { get; }
        public ContainerState BagContainer { get; }
        public UniqueItemState Tool { get; }
        public UniqueItemState Bag { get; }
        public InventoryPrototypeCommands Commands { get; }
        public IReadOnlyList<ResourceState> Resources => _readOnlyResources;
        public IReadOnlyList<GroundContainerState> GroundContainers => _readOnlyGroundContainers;
        public TransferActionState TransferAction { get; private set; }
        public long CurrentTick { get; private set; }
        public WorldPosition CurrentPlayerPosition { get; private set; }

        public long BeginSimulationTick(WorldPosition playerPosition)
        {
            CurrentPlayerPosition = playerPosition;
            return ++CurrentTick;
        }

        public ResourceYieldResult HarvestAndStartTransfer(
            WorldPosition playerPosition,
            double interactionRadius,
            long currentTick)
        {
            CurrentPlayerPosition = playerPosition;
            ResourceYieldResult result = ResourceYieldOperations.HarvestToGround(
                _resources,
                _groundContainers,
                playerPosition,
                interactionRadius,
                InventoryPrototypeCatalog.Branches,
                InventoryPrototypeCatalog.ResourceYieldQuantity,
                new EncumbranceValue(InventoryPrototypeCatalog.GroundCapacityUnits));
            if (result.Succeeded)
            {
                StartGroundTransfer(result.Ground, result.Produced, currentTick);
            }
            return result;
        }

        public TransferActionResult StartGroundTransfer(
            GroundContainerState ground,
            int quantity,
            long currentTick)
        {
            if (TransferAction != null && TransferAction.Status == TransferActionStatus.Active)
                return new TransferActionResult(TransferAction, TransferActionReason.AnotherActionActive);
            if (ground == null || !_groundContainers.Contains(ground))
                return new TransferActionResult(null, TransferActionReason.InvalidRequest);

            var id = new TransferActionId($"transfer-{_nextTransferAction++:0000}");
            TransferActionResult result = TransferActionOperations.Start(
                id,
                ground.Container,
                MainContainer,
                InventoryPrototypeCatalog.Branches,
                quantity,
                currentTick,
                ground.Position,
                InventoryPrototypeCatalog.GroundTransferMaximumDistance,
                InventoryPrototypeCatalog.TransferTiming);
            if (result.Succeeded) TransferAction = result.Action;
            return result;
        }

        public bool CanStartGroundTransfer(GroundContainerState ground)
        {
            return ground != null
                && _groundContainers.Contains(ground)
                && !ground.IsEmpty
                && (TransferAction == null || TransferAction.Status != TransferActionStatus.Active)
                && CurrentPlayerPosition.DistanceSquaredTo(ground.Position)
                    <= InventoryPrototypeCatalog.GroundTransferMaximumDistance
                        * InventoryPrototypeCatalog.GroundTransferMaximumDistance
                && MainContainer.RemainingCapacity.Units
                    >= InventoryPrototypeCatalog.Branches.UnitEncumbrance.Units;
        }

        public TransferActionResult AdvanceTransfer(
            long currentTick,
            WorldPosition playerPosition,
            bool playerMoved)
        {
            CurrentPlayerPosition = playerPosition;
            if (TransferAction == null)
                return new TransferActionResult(null, TransferActionReason.InvalidRequest);
            GroundContainerState ground = FindGround(TransferAction.SourceId);
            ItemDefinition definition = Inventory.FindDefinition(TransferAction.DefinitionId);
            return TransferActionOperations.Advance(
                TransferAction,
                currentTick,
                playerPosition,
                playerMoved,
                ground?.Container,
                MainContainer,
                definition);
        }

        public GroundContainerState FindGround(ContainerId containerId)
        {
            for (int index = 0; index < _groundContainers.Count; index++)
                if (_groundContainers[index].Container.Id.Equals(containerId)) return _groundContainers[index];
            return null;
        }

        public GroundContainerState GroundForView()
        {
            if (TransferAction != null)
            {
                GroundContainerState actionGround = FindGround(TransferAction.SourceId);
                if (actionGround != null && !actionGround.IsEmpty)
                {
                    return actionGround;
                }
            }

            return FindNearestNonEmptyGround(CurrentPlayerPosition);
        }

        public GroundContainerState FindNearestNonEmptyGround(WorldPosition origin)
        {
            GroundContainerState nearest = null;
            double nearestDistanceSquared = double.PositiveInfinity;
            for (int index = 0; index < _groundContainers.Count; index++)
            {
                GroundContainerState candidate = _groundContainers[index];
                if (candidate.IsEmpty)
                {
                    continue;
                }

                double distanceSquared = origin.DistanceSquaredTo(candidate.Position);
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

        public GroundContainerState FirstNonEmptyGround() =>
            FindNearestNonEmptyGround(CurrentPlayerPosition);
    }

    /// <summary>
    /// Process-owned prototype state. It is reset at each Play Mode start and is
    /// deliberately not owned by a GameObject.
    /// </summary>
    public static class InventoryPrototypeSessionProvider
    {
        private static InventoryPrototypeSession _current;

        public static InventoryPrototypeSession Current =>
            _current ?? (_current = new InventoryPrototypeSession());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            _current = new InventoryPrototypeSession();
        }
    }
}
