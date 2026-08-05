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

        private InventoryDefinitionSnapshot(
            ItemDefinitionId id,
            ItemStateKind stateKind,
            EncumbranceValue unitEncumbrance,
            bool hasEquipment,
            EquipmentSlotMask compatibleSlots,
            int containedContainerReductionPercent)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A restored inventory definition requires a valid identifier.",
                    nameof(id));
            }

            if (!Enum.IsDefined(typeof(ItemStateKind), stateKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stateKind),
                    stateKind,
                    "Unknown item state kind.");
            }

            const EquipmentSlotMask allSlots =
                EquipmentSlotMask.LeftHand
                | EquipmentSlotMask.RightHand
                | EquipmentSlotMask.Back;
            if (!hasEquipment)
            {
                if (compatibleSlots != EquipmentSlotMask.None
                    || containedContainerReductionPercent != 0)
                {
                    throw new ArgumentException(
                        "A non-equippable definition cannot contain equipment rules.");
                }
            }
            else
            {
                if (compatibleSlots == EquipmentSlotMask.None
                    || (compatibleSlots & ~allSlots) != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(compatibleSlots),
                        compatibleSlots,
                        "At least one known equipment slot is required.");
                }

                if (containedContainerReductionPercent < 0
                    || containedContainerReductionPercent > 100)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(containedContainerReductionPercent),
                        containedContainerReductionPercent,
                        "Container reduction must be between zero and one hundred percent.");
                }
            }

            Id = id;
            StateKind = stateKind;
            UnitEncumbrance = unitEncumbrance;
            HasEquipment = hasEquipment;
            CompatibleSlots = compatibleSlots;
            ContainedContainerReductionPercent =
                containedContainerReductionPercent;
        }

        internal static InventoryDefinitionSnapshot Restore(
            ItemDefinitionId id,
            ItemStateKind stateKind,
            EncumbranceValue unitEncumbrance,
            bool hasEquipment,
            EquipmentSlotMask compatibleSlots,
            int containedContainerReductionPercent)
        {
            return new InventoryDefinitionSnapshot(
                id,
                stateKind,
                unitEncumbrance,
                hasEquipment,
                compatibleSlots,
                containedContainerReductionPercent);
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

        private InventoryEntrySnapshot(
            ItemStateKind kind,
            ItemDefinitionId definitionId,
            int quantity,
            ItemInstanceId instanceId,
            ContainerId containedContainerId)
        {
            if (!Enum.IsDefined(typeof(ItemStateKind), kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown item state kind.");
            }

            if (!definitionId.IsValid)
            {
                throw new ArgumentException(
                    "A restored entry requires a valid definition identifier.",
                    nameof(definitionId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "A restored entry quantity must be positive.");
            }

            if (kind == ItemStateKind.Stackable)
            {
                if (instanceId.IsValid || containedContainerId.IsValid)
                {
                    throw new ArgumentException(
                        "A stackable restored entry cannot contain unique-item data.");
                }
            }
            else if (quantity != 1 || !instanceId.IsValid)
            {
                throw new ArgumentException(
                    "A unique restored entry requires one valid instance.");
            }

            Kind = kind;
            DefinitionId = definitionId;
            Quantity = quantity;
            InstanceId = instanceId;
            ContainedContainerId = containedContainerId;
        }

        internal static InventoryEntrySnapshot Restore(
            ItemStateKind kind,
            ItemDefinitionId definitionId,
            int quantity,
            ItemInstanceId instanceId,
            ContainerId containedContainerId)
        {
            return new InventoryEntrySnapshot(
                kind,
                definitionId,
                quantity,
                instanceId,
                containedContainerId);
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

        private InventoryContainerSnapshot(
            ContainerId id,
            string definitionKey,
            EncumbranceValue capacity,
            IEnumerable<InventoryEntrySnapshot> entries)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A restored container requires a valid identifier.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(definitionKey))
            {
                throw new ArgumentException(
                    "A restored container requires a definition key.",
                    nameof(definitionKey));
            }

            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var canonical = new List<InventoryEntrySnapshot>(entries);
            canonical.Sort(InventoryEntrySnapshot.Compare);
            var stackDefinitions = new HashSet<ItemDefinitionId>();
            var uniqueInstances = new HashSet<ItemInstanceId>();
            for (int index = 0; index < canonical.Count; index++)
            {
                InventoryEntrySnapshot entry = canonical[index];
                if (entry.Kind == ItemStateKind.Stackable)
                {
                    if (!stackDefinitions.Add(entry.DefinitionId))
                    {
                        throw new ArgumentException(
                            "A restored container contains duplicate stack entries.",
                            nameof(entries));
                    }
                }
                else if (!uniqueInstances.Add(entry.InstanceId))
                {
                    throw new ArgumentException(
                        "A restored container contains duplicate unique instances.",
                        nameof(entries));
                }
            }

            Id = id;
            DefinitionKey = definitionKey;
            Capacity = capacity;
            _entries = Array.AsReadOnly(canonical.ToArray());
        }

        internal static InventoryContainerSnapshot Restore(
            ContainerId id,
            string definitionKey,
            EncumbranceValue capacity,
            IEnumerable<InventoryEntrySnapshot> entries)
        {
            return new InventoryContainerSnapshot(
                id,
                definitionKey,
                capacity,
                entries);
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

        internal static PlayerInventorySnapshot Restore(
            ContainerId mainContainerId,
            IEnumerable<InventoryDefinitionSnapshot> definitions,
            IEnumerable<InventoryContainerSnapshot> containers,
            InventoryEquipmentSnapshot equipment)
        {
            if (!mainContainerId.IsValid)
            {
                throw new ArgumentException(
                    "A restored inventory requires a valid main container.",
                    nameof(mainContainerId));
            }

            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (containers == null) throw new ArgumentNullException(nameof(containers));

            var canonicalDefinitions =
                new List<InventoryDefinitionSnapshot>(definitions);
            canonicalDefinitions.Sort((left, right) =>
                left.Id.CompareTo(right.Id));
            var definitionsById =
                new Dictionary<ItemDefinitionId, InventoryDefinitionSnapshot>(
                    canonicalDefinitions.Count);
            for (int index = 0; index < canonicalDefinitions.Count; index++)
            {
                InventoryDefinitionSnapshot definition =
                    canonicalDefinitions[index];
                if (!definition.Id.IsValid)
                {
                    throw new ArgumentException(
                        "Restored definitions require valid identifiers.",
                        nameof(definitions));
                }

                if (definitionsById.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        "Restored definition identifiers must be unique.",
                        nameof(definitions));
                }

                definitionsById.Add(definition.Id, definition);
            }

            var canonicalContainers =
                new List<InventoryContainerSnapshot>();
            foreach (InventoryContainerSnapshot container in containers)
            {
                canonicalContainers.Add(container ?? throw new ArgumentException(
                    "Restored containers must not contain null values.",
                    nameof(containers)));
            }

            canonicalContainers.Sort((left, right) =>
                left.Id.CompareTo(right.Id));
            var containerIds = new HashSet<ContainerId>();
            bool mainFound = false;
            for (int index = 0; index < canonicalContainers.Count; index++)
            {
                InventoryContainerSnapshot container = canonicalContainers[index];
                if (!containerIds.Add(container.Id))
                {
                    throw new ArgumentException(
                        "Restored container identifiers must be unique.",
                        nameof(containers));
                }

                if (container.Id.Equals(mainContainerId)) mainFound = true;
            }

            if (!mainFound)
            {
                throw new ArgumentException(
                    "The restored main container is not registered.",
                    nameof(mainContainerId));
            }

            var uniqueDefinitions =
                new Dictionary<ItemInstanceId, ItemDefinitionId>();
            var containedOwners = new Dictionary<ContainerId, ContainerId>();
            for (int containerIndex = 0;
                 containerIndex < canonicalContainers.Count;
                 containerIndex++)
            {
                InventoryContainerSnapshot container =
                    canonicalContainers[containerIndex];
                for (int entryIndex = 0;
                     entryIndex < container.Entries.Count;
                     entryIndex++)
                {
                    InventoryEntrySnapshot entry = container.Entries[entryIndex];
                    if (!definitionsById.TryGetValue(
                        entry.DefinitionId,
                        out InventoryDefinitionSnapshot definition))
                    {
                        throw new ArgumentException(
                            $"Restored inventory references unknown definition '{entry.DefinitionId}'.",
                            nameof(containers));
                    }

                    if (definition.StateKind != entry.Kind)
                    {
                        throw new ArgumentException(
                            "A restored entry does not match its definition kind.",
                            nameof(containers));
                    }

                    if (entry.Kind != ItemStateKind.Unique) continue;
                    if (uniqueDefinitions.ContainsKey(entry.InstanceId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.DuplicateItemInstance,
                            $"Unique item instance '{entry.InstanceId}' appears more than once.");
                    }

                    uniqueDefinitions.Add(entry.InstanceId, entry.DefinitionId);
                    if (!entry.ContainedContainerId.IsValid) continue;
                    if (entry.ContainedContainerId.Equals(mainContainerId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.MainContainerContained,
                            "The main container cannot be owned by a contained item.");
                    }

                    if (!containerIds.Contains(entry.ContainedContainerId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.MissingContainedContainer,
                            $"Contained container '{entry.ContainedContainerId}' is not registered.");
                    }

                    if (containedOwners.ContainsKey(entry.ContainedContainerId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.DuplicateContainedContainerOwner,
                            $"Contained container '{entry.ContainedContainerId}' has more than one owning item.");
                    }

                    containedOwners.Add(
                        entry.ContainedContainerId,
                        container.Id);
                }
            }

            ValidateContainedContainerGraph(containedOwners);
            ValidateEquipment(
                equipment,
                definitionsById,
                uniqueDefinitions);

            return new PlayerInventorySnapshot(
                mainContainerId,
                Array.AsReadOnly(canonicalDefinitions.ToArray()),
                Array.AsReadOnly(canonicalContainers.ToArray()),
                equipment);
        }

        private static void ValidateContainedContainerGraph(
            IReadOnlyDictionary<ContainerId, ContainerId> owners)
        {
            var validated = new HashSet<ContainerId>();
            foreach (ContainerId start in owners.Keys)
            {
                var path = new HashSet<ContainerId>();
                ContainerId current = start;
                while (owners.TryGetValue(current, out ContainerId owner))
                {
                    if (validated.Contains(current)) break;
                    if (!path.Add(current))
                    {
                        throw Violation(
                            InventoryAggregateViolation.ContainedContainerCycle,
                            $"Contained container ownership contains a cycle through '{current}'.");
                    }

                    current = owner;
                }

                foreach (ContainerId id in path) validated.Add(id);
            }
        }

        private static void ValidateEquipment(
            InventoryEquipmentSnapshot equipment,
            IReadOnlyDictionary<ItemDefinitionId, InventoryDefinitionSnapshot> definitions,
            IReadOnlyDictionary<ItemInstanceId, ItemDefinitionId> uniqueDefinitions)
        {
            var used = new HashSet<ItemInstanceId>();
            ValidateEquipmentSlot(
                equipment.LeftHand,
                EquipmentSlot.LeftHand,
                EquipmentSlotMask.LeftHand,
                definitions,
                uniqueDefinitions,
                used);
            ValidateEquipmentSlot(
                equipment.RightHand,
                EquipmentSlot.RightHand,
                EquipmentSlotMask.RightHand,
                definitions,
                uniqueDefinitions,
                used);
            ValidateEquipmentSlot(
                equipment.Back,
                EquipmentSlot.Back,
                EquipmentSlotMask.Back,
                definitions,
                uniqueDefinitions,
                used);
        }

        private static void ValidateEquipmentSlot(
            ItemInstanceId instanceId,
            EquipmentSlot slot,
            EquipmentSlotMask mask,
            IReadOnlyDictionary<ItemDefinitionId, InventoryDefinitionSnapshot> definitions,
            IReadOnlyDictionary<ItemInstanceId, ItemDefinitionId> uniqueDefinitions,
            ISet<ItemInstanceId> used)
        {
            if (!instanceId.IsValid) return;
            if (!used.Add(instanceId))
            {
                throw Violation(
                    InventoryAggregateViolation.EquipmentItemDuplicated,
                    $"Unique item instance '{instanceId}' occupies more than one equipment slot.");
            }

            if (!uniqueDefinitions.TryGetValue(
                instanceId,
                out ItemDefinitionId definitionId))
            {
                throw Violation(
                    InventoryAggregateViolation.EquipmentItemMissing,
                    $"Equipped item instance '{instanceId}' is missing from the aggregate.");
            }

            InventoryDefinitionSnapshot definition =
                definitions[definitionId];
            if (!definition.HasEquipment
                || (definition.CompatibleSlots & mask) == 0)
            {
                throw Violation(
                    InventoryAggregateViolation.EquipmentItemIncompatible,
                    $"Equipped item instance '{instanceId}' is incompatible with slot '{slot}'.");
            }
        }

        private static InventoryAggregateException Violation(
            InventoryAggregateViolation violation,
            string message)
        {
            return new InventoryAggregateException(violation, message);
        }

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
