using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Inventory
{
    public enum InventoryAggregateViolation
    {
        DuplicateItemInstance = 0,
        MissingContainedContainer = 1,
        DuplicateContainedContainerOwner = 2,
        MainContainerContained = 3,
        EquipmentItemMissing = 4,
        EquipmentItemDuplicated = 5,
        EquipmentItemIncompatible = 6,
        ContainedContainerCycle = 7
    }

    public sealed class InventoryAggregateException : InvalidOperationException
    {
        internal InventoryAggregateException(
            InventoryAggregateViolation violation,
            string message)
            : base(message)
        {
            Violation = violation;
        }

        public InventoryAggregateViolation Violation { get; }
    }

    /// <summary>
    /// Immutable equipment references in the fixed canonical slot order.
    /// Validation against an inventory aggregate is performed by PlayerInventoryState.
    /// </summary>
    public readonly struct InventoryEquipmentSnapshot : IEquatable<InventoryEquipmentSnapshot>
    {
        public InventoryEquipmentSnapshot(
            ItemInstanceId leftHand,
            ItemInstanceId rightHand,
            ItemInstanceId back)
        {
            LeftHand = leftHand;
            RightHand = rightHand;
            Back = back;
        }

        public ItemInstanceId LeftHand { get; }
        public ItemInstanceId RightHand { get; }
        public ItemInstanceId Back { get; }

        public ItemInstanceId Get(EquipmentSlot slot)
        {
            EquipmentDefinition.ValidateSlot(slot);
            switch (slot)
            {
                case EquipmentSlot.LeftHand:
                    return LeftHand;
                case EquipmentSlot.RightHand:
                    return RightHand;
                default:
                    return Back;
            }
        }

        public bool Equals(InventoryEquipmentSnapshot other) =>
            LeftHand.Equals(other.LeftHand)
            && RightHand.Equals(other.RightHand)
            && Back.Equals(other.Back);

        public override bool Equals(object obj) =>
            obj is InventoryEquipmentSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = LeftHand.GetHashCode();
                hash = (hash * 397) ^ RightHand.GetHashCode();
                return (hash * 397) ^ Back.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Canonical compatibility fingerprint for one editorial item definition.
    /// Display text is deliberately excluded.
    /// </summary>
    public readonly struct InventoryDefinitionSnapshot : IEquatable<InventoryDefinitionSnapshot>
    {
        internal InventoryDefinitionSnapshot(ItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            Id = definition.Id;
            StateKind = definition.StateKind;
            UnitEncumbrance = definition.UnitEncumbrance;
            HasEquipment = definition.Equipment != null;
            CompatibleSlots = definition.Equipment == null
                ? EquipmentSlotMask.None
                : definition.Equipment.CompatibleSlots;
            ContainedContainerReductionPercent = definition.Equipment == null
                ? 0
                : definition.Equipment.ContainedContainerReductionPercent;
        }

        public ItemDefinitionId Id { get; }
        public ItemStateKind StateKind { get; }
        public EncumbranceValue UnitEncumbrance { get; }
        public bool HasEquipment { get; }
        public EquipmentSlotMask CompatibleSlots { get; }
        public int ContainedContainerReductionPercent { get; }

        public bool Equals(InventoryDefinitionSnapshot other) =>
            Id.Equals(other.Id)
            && StateKind == other.StateKind
            && UnitEncumbrance.Equals(other.UnitEncumbrance)
            && HasEquipment == other.HasEquipment
            && CompatibleSlots == other.CompatibleSlots
            && ContainedContainerReductionPercent == other.ContainedContainerReductionPercent;

        public override bool Equals(object obj) =>
            obj is InventoryDefinitionSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (int)StateKind;
                hash = (hash * 397) ^ UnitEncumbrance.GetHashCode();
                hash = (hash * 397) ^ HasEquipment.GetHashCode();
                hash = (hash * 397) ^ (int)CompatibleSlots;
                return (hash * 397) ^ ContainedContainerReductionPercent;
            }
        }
    }

    public readonly struct InventoryEntrySnapshot : IEquatable<InventoryEntrySnapshot>
    {
        internal InventoryEntrySnapshot(InventoryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            Kind = entry.Kind;
            DefinitionId = entry.DefinitionId;
            Quantity = entry.Quantity;
            InstanceId = entry.Kind == ItemStateKind.Unique
                ? entry.UniqueItem.InstanceId
                : default;
            ContainedContainerId = entry.Kind == ItemStateKind.Unique
                ? entry.UniqueItem.ContainedContainerId
                : default;
        }

        public ItemStateKind Kind { get; }
        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
        public ItemInstanceId InstanceId { get; }
        public ContainerId ContainedContainerId { get; }

        public bool Equals(InventoryEntrySnapshot other) =>
            Kind == other.Kind
            && DefinitionId.Equals(other.DefinitionId)
            && Quantity == other.Quantity
            && InstanceId.Equals(other.InstanceId)
            && ContainedContainerId.Equals(other.ContainedContainerId);

        public override bool Equals(object obj) =>
            obj is InventoryEntrySnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ DefinitionId.GetHashCode();
                hash = (hash * 397) ^ Quantity;
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                return (hash * 397) ^ ContainedContainerId.GetHashCode();
            }
        }

        internal static int Compare(
            InventoryEntrySnapshot left,
            InventoryEntrySnapshot right)
        {
            int comparison = left.DefinitionId.CompareTo(right.DefinitionId);
            if (comparison != 0) return comparison;

            comparison = ((int)left.Kind).CompareTo((int)right.Kind);
            if (comparison != 0) return comparison;

            comparison = left.InstanceId.CompareTo(right.InstanceId);
            if (comparison != 0) return comparison;

            comparison = left.ContainedContainerId.CompareTo(right.ContainedContainerId);
            if (comparison != 0) return comparison;

            return left.Quantity.CompareTo(right.Quantity);
        }
    }

    public sealed class InventoryContainerSnapshot
    {
        private readonly IReadOnlyList<InventoryEntrySnapshot> _entries;

        internal InventoryContainerSnapshot(ContainerState container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            Id = container.Id;
            DefinitionKey = container.Definition.Key;
            Capacity = container.Definition.Capacity;

            var entries = new List<InventoryEntrySnapshot>(container.Entries.Count);
            for (int index = 0; index < container.Entries.Count; index++)
            {
                entries.Add(new InventoryEntrySnapshot(container.Entries[index]));
            }

            entries.Sort(InventoryEntrySnapshot.Compare);
            _entries = Array.AsReadOnly(entries.ToArray());
        }

        public ContainerId Id { get; }
        public string DefinitionKey { get; }
        public EncumbranceValue Capacity { get; }
        public IReadOnlyList<InventoryEntrySnapshot> Entries => _entries;
    }

    /// <summary>
    /// Immutable canonical inventory capture. Registry, container and entry order
    /// are stable and independent from insertion order in the live aggregate.
    /// </summary>
    public sealed class PlayerInventorySnapshot
    {
        private readonly IReadOnlyList<InventoryDefinitionSnapshot> _definitions;
        private readonly IReadOnlyList<InventoryContainerSnapshot> _containers;

        private PlayerInventorySnapshot(
            ContainerId mainContainerId,
            IReadOnlyList<InventoryDefinitionSnapshot> definitions,
            IReadOnlyList<InventoryContainerSnapshot> containers,
            InventoryEquipmentSnapshot equipment)
        {
            MainContainerId = mainContainerId;
            _definitions = definitions;
            _containers = containers;
            Equipment = equipment;
        }

        public ContainerId MainContainerId { get; }
        public IReadOnlyList<InventoryDefinitionSnapshot> Definitions => _definitions;
        public IReadOnlyList<InventoryContainerSnapshot> Containers => _containers;
        public InventoryEquipmentSnapshot Equipment { get; }

        internal static PlayerInventorySnapshot Capture(
            PlayerInventoryState inventory,
            InventoryEquipmentSnapshot equipment)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            var definitions = new List<InventoryDefinitionSnapshot>(
                inventory.Definitions.Count);
            for (int index = 0; index < inventory.Definitions.Count; index++)
            {
                definitions.Add(new InventoryDefinitionSnapshot(
                    inventory.Definitions[index]));
            }

            definitions.Sort((left, right) => left.Id.CompareTo(right.Id));

            var containers = new List<InventoryContainerSnapshot>(
                inventory.Containers.Count);
            for (int index = 0; index < inventory.Containers.Count; index++)
            {
                containers.Add(new InventoryContainerSnapshot(
                    inventory.Containers[index]));
            }

            containers.Sort((left, right) => left.Id.CompareTo(right.Id));

            return new PlayerInventorySnapshot(
                inventory.MainContainer.Id,
                Array.AsReadOnly(definitions.ToArray()),
                Array.AsReadOnly(containers.ToArray()),
                equipment);
        }
    }
}
