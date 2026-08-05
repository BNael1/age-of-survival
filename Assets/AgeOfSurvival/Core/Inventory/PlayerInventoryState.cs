using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Pure C# aggregate for the containers currently carried by one player.
    /// It owns copies of registry lists and exposes only read-only views.
    /// </summary>
    public sealed class PlayerInventoryState
    {
        private static readonly EquipmentSlot[] StableEquipmentSlotOrder =
        {
            EquipmentSlot.LeftHand,
            EquipmentSlot.RightHand,
            EquipmentSlot.Back
        };

        private readonly List<ItemDefinition> _definitions;
        private readonly List<ContainerState> _containers;
        private readonly IReadOnlyList<ItemDefinition> _readOnlyDefinitions;
        private readonly IReadOnlyList<ContainerState> _readOnlyContainers;

        public PlayerInventoryState(
            ContainerId mainContainerId,
            IEnumerable<ItemDefinition> definitions,
            IEnumerable<ContainerState> containers)
            : this(
                mainContainerId,
                definitions,
                containers,
                default)
        {
        }

        public PlayerInventoryState(
            ContainerId mainContainerId,
            IEnumerable<ItemDefinition> definitions,
            IEnumerable<ContainerState> containers,
            InventoryEquipmentSnapshot equipment)
        {
            if (!mainContainerId.IsValid)
            {
                throw new ArgumentException("A valid main container identifier is required.", nameof(mainContainerId));
            }

            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (containers == null) throw new ArgumentNullException(nameof(containers));

            _definitions = new List<ItemDefinition>(definitions);
            _containers = new List<ContainerState>(containers);
            ValidateRegistries(mainContainerId, equipment);

            MainContainer = FindContainer(mainContainerId);
            Equipment = new EquipmentState();
            Equipment.Restore(equipment);
            _readOnlyDefinitions = _definitions.AsReadOnly();
            _readOnlyContainers = _containers.AsReadOnly();
        }

        public ContainerState MainContainer { get; }
        public EquipmentState Equipment { get; }
        public IReadOnlyList<ItemDefinition> Definitions => _readOnlyDefinitions;
        public IReadOnlyList<ContainerState> Containers => _readOnlyContainers;

        public ItemDefinition FindDefinition(ItemDefinitionId id)
        {
            if (!id.IsValid) return null;
            for (int index = 0; index < _definitions.Count; index++)
            {
                if (_definitions[index].Id.Equals(id)) return _definitions[index];
            }

            return null;
        }

        public ContainerState FindContainer(ContainerId id)
        {
            if (!id.IsValid) return null;
            for (int index = 0; index < _containers.Count; index++)
            {
                if (_containers[index].Id.Equals(id)) return _containers[index];
            }

            return null;
        }

        public bool TryFindUnique(
            ItemInstanceId instanceId,
            out UniqueItemState item,
            out ItemDefinition definition,
            out ContainerState container)
        {
            for (int containerIndex = 0; containerIndex < _containers.Count; containerIndex++)
            {
                ContainerState candidateContainer = _containers[containerIndex];
                for (int entryIndex = 0; entryIndex < candidateContainer.Entries.Count; entryIndex++)
                {
                    InventoryEntry entry = candidateContainer.Entries[entryIndex];
                    if (entry.Kind != ItemStateKind.Unique
                        || !entry.UniqueItem.InstanceId.Equals(instanceId))
                    {
                        continue;
                    }

                    item = entry.UniqueItem;
                    definition = FindDefinition(entry.DefinitionId);
                    container = candidateContainer;
                    return definition != null;
                }
            }

            item = default;
            definition = null;
            container = null;
            return false;
        }

        public PlayerInventorySnapshot CaptureSnapshot()
        {
            InventoryEquipmentSnapshot equipment = Equipment.CaptureSnapshot();
            ValidateCurrentAggregate(equipment);
            return PlayerInventorySnapshot.Capture(this, equipment);
        }

        private void ValidateRegistries(
            ContainerId mainContainerId,
            InventoryEquipmentSnapshot equipment)
        {
            for (int index = 0; index < _definitions.Count; index++)
            {
                ItemDefinition current = _definitions[index]
                    ?? throw new ArgumentException("Definitions must not contain null values.", "definitions");
                for (int previous = 0; previous < index; previous++)
                {
                    if (_definitions[previous].Id.Equals(current.Id))
                    {
                        throw new ArgumentException("Definition identifiers must be unique.", "definitions");
                    }
                }
            }

            for (int index = 0; index < _containers.Count; index++)
            {
                ContainerState current = _containers[index]
                    ?? throw new ArgumentException("Containers must not contain null values.", "containers");
                for (int previous = 0; previous < index; previous++)
                {
                    if (_containers[previous].Id.Equals(current.Id))
                    {
                        throw new ArgumentException("Container identifiers must be unique.", "containers");
                    }
                }
            }

            if (FindContainer(mainContainerId) == null)
            {
                throw new ArgumentException(
                    "The main container must be registered.",
                    nameof(mainContainerId));
            }

            ValidateCanonicalDefinitions();
            ValidateAggregateInvariants(mainContainerId, equipment);

            for (int containerIndex = 0; containerIndex < _containers.Count; containerIndex++)
            {
                _containers[containerIndex].BindCanonicalDefinitions(_definitions);
            }
        }

        private void ValidateCurrentAggregate(InventoryEquipmentSnapshot equipment)
        {
            ValidateCanonicalDefinitions();
            ValidateAggregateInvariants(MainContainer.Id, equipment);
        }

        private void ValidateCanonicalDefinitions()
        {
            for (int containerIndex = 0; containerIndex < _containers.Count; containerIndex++)
            {
                try
                {
                    _containers[containerIndex].ValidateCanonicalDefinitions(_definitions);
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException(
                        "Containers must match the canonical item definition registry.",
                        "containers",
                        exception);
                }
            }
        }

        private void ValidateAggregateInvariants(
            ContainerId mainContainerId,
            InventoryEquipmentSnapshot equipment)
        {
            var uniqueInstances = new List<ItemInstanceId>();
            var containedContainers = new List<ContainerId>();
            var containerOwners = new Dictionary<ContainerId, ContainerId>();

            for (int containerIndex = 0; containerIndex < _containers.Count; containerIndex++)
            {
                ContainerState container = _containers[containerIndex];
                for (int entryIndex = 0; entryIndex < container.Entries.Count; entryIndex++)
                {
                    InventoryEntry entry = container.Entries[entryIndex];
                    if (entry.Kind != ItemStateKind.Unique)
                    {
                        continue;
                    }

                    UniqueItemState item = entry.UniqueItem;
                    if (Contains(uniqueInstances, item.InstanceId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.DuplicateItemInstance,
                            $"Unique item instance '{item.InstanceId}' appears more than once.");
                    }

                    uniqueInstances.Add(item.InstanceId);
                    if (!item.HasContainedContainer)
                    {
                        continue;
                    }

                    if (item.ContainedContainerId.Equals(mainContainerId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.MainContainerContained,
                            "The main container cannot be owned by a contained item.");
                    }

                    if (FindContainer(item.ContainedContainerId) == null)
                    {
                        throw Violation(
                            InventoryAggregateViolation.MissingContainedContainer,
                            $"Contained container '{item.ContainedContainerId}' is not registered.");
                    }

                    if (Contains(containedContainers, item.ContainedContainerId))
                    {
                        throw Violation(
                            InventoryAggregateViolation.DuplicateContainedContainerOwner,
                            $"Contained container '{item.ContainedContainerId}' has more than one owning item.");
                    }

                    containedContainers.Add(item.ContainedContainerId);
                    containerOwners.Add(item.ContainedContainerId, container.Id);
                }
            }

            ValidateContainedContainerGraph(
                containedContainers,
                containerOwners);

            var equippedInstances = new List<ItemInstanceId>();
            for (int slotIndex = 0; slotIndex < StableEquipmentSlotOrder.Length; slotIndex++)
            {
                EquipmentSlot slot = StableEquipmentSlotOrder[slotIndex];
                ItemInstanceId instanceId = equipment.Get(slot);
                if (!instanceId.IsValid)
                {
                    continue;
                }

                if (Contains(equippedInstances, instanceId))
                {
                    throw Violation(
                        InventoryAggregateViolation.EquipmentItemDuplicated,
                        $"Unique item instance '{instanceId}' occupies more than one equipment slot.");
                }

                if (!TryFindUnique(
                    instanceId,
                    out _,
                    out ItemDefinition definition,
                    out _))
                {
                    throw Violation(
                        InventoryAggregateViolation.EquipmentItemMissing,
                        $"Equipped item instance '{instanceId}' is missing from the aggregate.");
                }

                if (definition.Equipment == null || !definition.Equipment.Supports(slot))
                {
                    throw Violation(
                        InventoryAggregateViolation.EquipmentItemIncompatible,
                        $"Equipped item instance '{instanceId}' is incompatible with slot '{slot}'.");
                }

                equippedInstances.Add(instanceId);
            }
        }

        private static void ValidateContainedContainerGraph(
            IReadOnlyList<ContainerId> containedContainers,
            IReadOnlyDictionary<ContainerId, ContainerId> containerOwners)
        {
            var validated = new HashSet<ContainerId>();
            for (int startIndex = 0; startIndex < containedContainers.Count; startIndex++)
            {
                var path = new HashSet<ContainerId>();
                ContainerId current = containedContainers[startIndex];

                while (containerOwners.TryGetValue(current, out ContainerId owner))
                {
                    if (validated.Contains(current))
                    {
                        break;
                    }

                    if (!path.Add(current))
                    {
                        throw Violation(
                            InventoryAggregateViolation.ContainedContainerCycle,
                            $"Contained container ownership contains a cycle through '{current}'.");
                    }

                    current = owner;
                }

                foreach (ContainerId containerId in path)
                {
                    validated.Add(containerId);
                }
            }
        }

        private static bool Contains(
            IReadOnlyList<ItemInstanceId> ids,
            ItemInstanceId id)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (ids[index].Equals(id)) return true;
            }

            return false;
        }

        private static bool Contains(
            IReadOnlyList<ContainerId> ids,
            ContainerId id)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (ids[index].Equals(id)) return true;
            }

            return false;
        }

        private static InventoryAggregateException Violation(
            InventoryAggregateViolation violation,
            string message) =>
            new InventoryAggregateException(violation, message);
    }
}
