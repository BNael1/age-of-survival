using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Mutable inventory state. Entries are exposed through a read-only wrapper.
    /// Mutations are restricted to InventoryOperations.
    /// </summary>
    public sealed class ContainerState
    {
        private readonly List<InventoryEntry> _entries = new List<InventoryEntry>();
        private readonly IReadOnlyList<InventoryEntry> _readOnlyEntries;

        public ContainerState(ContainerId id, ContainerDefinition definition)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A container state requires a valid identifier.", nameof(id));
            }

            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Id = id;
            _readOnlyEntries = _entries.AsReadOnly();
        }

        public ContainerId Id { get; }
        public ContainerDefinition Definition { get; }
        public IReadOnlyList<InventoryEntry> Entries => _readOnlyEntries;
        public EncumbranceValue UsedCapacity { get; private set; }
        public EncumbranceValue RemainingCapacity => Definition.Capacity - UsedCapacity;

        internal int FindStackIndex(ItemDefinitionId definitionId)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                InventoryEntry entry = _entries[index];
                if (entry.Kind == ItemStateKind.Stackable && entry.DefinitionId.Equals(definitionId))
                {
                    return index;
                }
            }

            return -1;
        }

        internal int FindUniqueIndex(ItemInstanceId instanceId)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                InventoryEntry entry = _entries[index];
                if (entry.Kind == ItemStateKind.Unique && entry.UniqueItem.InstanceId.Equals(instanceId))
                {
                    return index;
                }
            }

            return -1;
        }

        internal InventoryEntry EntryAt(int index) => _entries[index];

        internal void Append(InventoryEntry entry)
        {
            _entries.Add(entry);
            UsedCapacity += entry.TotalEncumbrance;
        }

        internal void ReplaceStack(int index, int quantity)
        {
            InventoryEntry previous = _entries[index];
            InventoryEntry replacement = previous.WithQuantity(quantity);
            _entries[index] = replacement;
            UsedCapacity = UsedCapacity - previous.TotalEncumbrance + replacement.TotalEncumbrance;
        }

        internal InventoryEntry RemoveAt(int index)
        {
            InventoryEntry removed = _entries[index];
            _entries.RemoveAt(index);
            UsedCapacity -= removed.TotalEncumbrance;
            return removed;
        }
    }
}
