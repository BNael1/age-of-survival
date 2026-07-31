using System;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    public readonly struct InventorySelection
    {
        public InventorySelection(
            ContainerId sourceContainerId,
            ItemDefinitionId definitionId,
            ItemInstanceId instanceId,
            bool isUnique)
        {
            SourceContainerId = sourceContainerId;
            DefinitionId = definitionId;
            InstanceId = instanceId;
            IsUnique = isUnique;
        }

        public ContainerId SourceContainerId { get; }
        public ItemDefinitionId DefinitionId { get; }
        public ItemInstanceId InstanceId { get; }
        public bool IsUnique { get; }
        public bool IsValid => SourceContainerId.IsValid
            && DefinitionId.IsValid
            && (IsUnique ? InstanceId.IsValid : !InstanceId.IsValid);
    }

    public sealed class InventoryPrototypeCommands
    {
        private readonly PlayerInventoryState _inventory;

        public InventoryPrototypeCommands(PlayerInventoryState inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public bool CanTransfer(InventorySelection selection, ContainerId destinationId)
        {
            if (!selection.IsValid || !destinationId.IsValid
                || selection.SourceContainerId.Equals(destinationId))
            {
                return false;
            }

            ContainerState source = _inventory.FindContainer(selection.SourceContainerId);
            ContainerState destination = _inventory.FindContainer(destinationId);
            ItemDefinition definition = _inventory.FindDefinition(selection.DefinitionId);
            if (source == null || destination == null || definition == null)
            {
                return false;
            }

            bool definitionIsUnique = definition.StateKind == ItemStateKind.Unique;
            if (selection.IsUnique != definitionIsUnique)
            {
                return false;
            }

            if (selection.IsUnique)
            {
                if (!_inventory.TryFindUnique(
                        selection.InstanceId,
                        out UniqueItemState item,
                        out ItemDefinition actualDefinition,
                        out ContainerState owner)
                    || !owner.Id.Equals(source.Id)
                    || !actualDefinition.Id.Equals(selection.DefinitionId)
                    || item.ContainedContainerId.Equals(destinationId))
                {
                    return false;
                }

                return destination.RemainingCapacity.Units >= actualDefinition.UnitEncumbrance.Units;
            }

            return InventoryOperations.Count(source, definition.Id) > 0
                && (definition.UnitEncumbrance.Units == 0
                    || destination.RemainingCapacity.Units >= definition.UnitEncumbrance.Units);
        }

        public TransferResult Transfer(InventorySelection selection, ContainerId destinationId)
        {
            if (!CanTransfer(selection, destinationId))
            {
                return default;
            }

            ContainerState source = _inventory.FindContainer(selection.SourceContainerId);
            ContainerState destination = _inventory.FindContainer(destinationId);
            ItemDefinition definition = _inventory.FindDefinition(selection.DefinitionId);
            if (selection.IsUnique)
            {
                return InventoryOperations.TransferUnique(
                    source,
                    destination,
                    definition,
                    selection.InstanceId);
            }

            int quantity = InventoryOperations.Count(source, definition.Id);
            return InventoryOperations.TransferStack(source, destination, definition, quantity);
        }

        public bool CanEquip(InventorySelection selection, EquipmentSlot slot)
        {
            if (!selection.IsValid || !selection.IsUnique || _inventory.Equipment.Get(slot).IsValid)
            {
                return false;
            }

            ContainerState source = _inventory.FindContainer(selection.SourceContainerId);
            if (source == null
                || !_inventory.TryFindUnique(
                    selection.InstanceId,
                    out _,
                    out ItemDefinition definition,
                    out ContainerState owner)
                || !owner.Id.Equals(source.Id)
                || !definition.Id.Equals(selection.DefinitionId))
            {
                return false;
            }

            return definition.Equipment != null
                && definition.Equipment.Supports(slot)
                && !_inventory.Equipment.IsEquipped(selection.InstanceId);
        }

        public EquipmentOperationResult Equip(InventorySelection selection, EquipmentSlot slot)
        {
            if (!CanEquip(selection, slot)) return default;
            return EquipmentOperations.Equip(_inventory, selection.InstanceId, slot);
        }

        public bool CanUnequip(EquipmentSlot slot) => _inventory.Equipment.Get(slot).IsValid;

        public EquipmentOperationResult Unequip(EquipmentSlot slot) =>
            EquipmentOperations.Unequip(_inventory, slot);
    }
}
