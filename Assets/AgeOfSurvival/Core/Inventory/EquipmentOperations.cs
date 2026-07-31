using System;

namespace AgeOfSurvival.Core.Inventory
{
    public enum EquipmentOperationOutcome
    {
        Rejected = 0,
        Complete = 1,
        ItemNotFound = 2,
        IncompatibleSlot = 3,
        SlotOccupied = 4,
        AlreadyEquipped = 5,
        SlotEmpty = 6
    }

    public readonly struct EquipmentOperationResult
    {
        internal EquipmentOperationResult(EquipmentOperationOutcome outcome, ItemInstanceId instanceId)
        {
            Outcome = outcome;
            InstanceId = instanceId;
        }

        public EquipmentOperationOutcome Outcome { get; }
        public ItemInstanceId InstanceId { get; }
        public bool Succeeded => Outcome == EquipmentOperationOutcome.Complete;
    }

    public static class EquipmentOperations
    {
        public static EquipmentOperationResult Equip(
            PlayerInventoryState inventory,
            ItemInstanceId instanceId,
            EquipmentSlot slot)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!instanceId.IsValid) throw new ArgumentException("A valid instance identifier is required.", nameof(instanceId));
            EquipmentDefinition.ValidateSlot(slot);

            if (!inventory.TryFindUnique(instanceId, out _, out ItemDefinition definition, out _))
            {
                return Result(EquipmentOperationOutcome.ItemNotFound, instanceId);
            }

            if (definition.Equipment == null || !definition.Equipment.Supports(slot))
            {
                return Result(EquipmentOperationOutcome.IncompatibleSlot, instanceId);
            }

            if (inventory.Equipment.IsEquipped(instanceId))
            {
                return Result(EquipmentOperationOutcome.AlreadyEquipped, instanceId);
            }

            if (inventory.Equipment.Get(slot).IsValid)
            {
                return Result(EquipmentOperationOutcome.SlotOccupied, instanceId);
            }

            inventory.Equipment.Set(slot, instanceId);
            return Result(EquipmentOperationOutcome.Complete, instanceId);
        }

        public static EquipmentOperationResult Unequip(
            PlayerInventoryState inventory,
            EquipmentSlot slot)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            EquipmentDefinition.ValidateSlot(slot);

            ItemInstanceId equipped = inventory.Equipment.Get(slot);
            if (!equipped.IsValid)
            {
                return Result(EquipmentOperationOutcome.SlotEmpty, default);
            }

            inventory.Equipment.Set(slot, default);
            return Result(EquipmentOperationOutcome.Complete, equipped);
        }

        private static EquipmentOperationResult Result(
            EquipmentOperationOutcome outcome,
            ItemInstanceId instanceId) =>
            new EquipmentOperationResult(outcome, instanceId);
    }
}
