using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Resources;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        public InventoryPrototypeSession(
            IEnumerable<ResourceState> resources,
            WorldPopulationSettings persistenceWorld)
            : this(resources)
        {
            if (!persistenceWorld.Generation.Version.IsValid
                || !persistenceWorld.Generation.ChunkLayout.IsValid
                || !persistenceWorld.Profile.IsValid)
            {
                throw new ArgumentException(
                    "A valid persistence world is required.",
                    nameof(persistenceWorld));
            }

            _persistenceWorld = persistenceWorld;
        }

        private ResourceYieldResult HarvestNaturalResourceAndStartTransfer(
            WorldPosition playerPosition,
            double interactionRadius,
            long currentTick)
        {
            ResourceYieldResult result =
                ResourceYieldOperations.HarvestToGround(
                    _resources,
                    _groundContainers,
                    playerPosition,
                    interactionRadius,
                    ResourcePrototypeCatalog.Definitions,
                    new EncumbranceValue(
                        InventoryPrototypeCatalog.GroundCapacityUnits));

            if (result.Succeeded)
            {
                ResourceYieldDefinition primary =
                    result.Definition.PrimaryYield;
                StartGroundTransfer(
                    result.Ground,
                    primary.Definition.Id,
                    primary.Quantity,
                    currentTick);
            }

            return result;
        }

        public TransferActionResult StartGroundTransfer(
            GroundContainerState ground,
            ItemDefinitionId definitionId,
            int quantity,
            long currentTick)
        {
            if (TransferAction != null
                && TransferAction.Status == TransferActionStatus.Active)
            {
                return new TransferActionResult(
                    TransferAction,
                    TransferActionReason.AnotherActionActive);
            }

            if (ground == null || !_groundContainers.Contains(ground))
            {
                return new TransferActionResult(
                    null,
                    TransferActionReason.InvalidRequest);
            }

            ItemDefinition definition =
                Inventory.FindDefinition(definitionId);
            if (definition == null
                || definition.StateKind != ItemStateKind.Stackable
                || definition.Perishable != null)
            {
                return new TransferActionResult(
                    null,
                    TransferActionReason.InvalidRequest);
            }

            var id = new TransferActionId(
                $"transfer-{_nextTransferAction++:0000}");
            TransferActionResult result =
                TransferActionOperations.Start(
                    id,
                    ground.Container,
                    MainContainer,
                    definition,
                    quantity,
                    currentTick,
                    ground.Position,
                    InventoryPrototypeCatalog
                        .GroundTransferMaximumDistance,
                    InventoryPrototypeCatalog.TransferTiming);
            if (result.Succeeded)
            {
                TransferAction = result.Action;
            }

            return result;
        }

        public bool CanStartGroundTransfer(
            GroundContainerState ground,
            ItemDefinitionId definitionId)
        {
            if (ground == null
                || !_groundContainers.Contains(ground)
                || ground.IsEmpty
                || (TransferAction != null
                    && TransferAction.Status == TransferActionStatus.Active))
            {
                return false;
            }

            ItemDefinition definition =
                Inventory.FindDefinition(definitionId);
            if (definition == null
                || definition.StateKind != ItemStateKind.Stackable
                || definition.Perishable != null
                || InventoryOperations.Count(
                    ground.Container,
                    definition.Id) <= 0)
            {
                return false;
            }

            if (CurrentPlayerPosition.DistanceSquaredTo(ground.Position)
                > InventoryPrototypeCatalog
                    .GroundTransferMaximumDistance
                    * InventoryPrototypeCatalog
                        .GroundTransferMaximumDistance)
            {
                return false;
            }

            return definition.UnitEncumbrance.Units == 0
                || MainContainer.RemainingCapacity.Units
                    >= definition.UnitEncumbrance.Units;
        }

        private static ItemDefinitionId FirstGroundDefinitionId(
            GroundContainerState ground)
        {
            return ground != null
                && ground.Container != null
                && ground.Container.Entries.Count > 0
                ? ground.Container.Entries[0].DefinitionId
                : default;
        }
    }
}
