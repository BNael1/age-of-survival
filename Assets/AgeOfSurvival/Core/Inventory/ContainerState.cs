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
        private readonly List<DefinitionFingerprint> _knownDefinitions =
            new List<DefinitionFingerprint>();
        private readonly IReadOnlyList<InventoryEntry> _readOnlyEntries;
        private bool _hasCanonicalDefinitionRegistry;

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

        internal bool IsDefinitionCompatible(ItemDefinition definition)
        {
            if (definition == null) return false;

            int knownIndex = FindKnownDefinition(definition.Id);
            if (knownIndex >= 0)
            {
                return _knownDefinitions[knownIndex].Matches(definition);
            }

            if (_hasCanonicalDefinitionRegistry)
            {
                return false;
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                InventoryEntry entry = _entries[index];
                if (entry.DefinitionId.Equals(definition.Id)
                    && !entry.MatchesDefinition(definition))
                {
                    return false;
                }
            }

            return true;
        }

        internal void BindDefinition(ItemDefinition definition)
        {
            if (!IsDefinitionCompatible(definition))
            {
                throw new ArgumentException(
                    "The item definition contradicts the container definition registry.",
                    nameof(definition));
            }

            if (FindKnownDefinition(definition.Id) < 0)
            {
                _knownDefinitions.Add(new DefinitionFingerprint(definition));
            }
        }

        internal void ValidateCanonicalDefinitions(IReadOnlyList<ItemDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                if (definitions[definitionIndex] == null)
                {
                    throw new ArgumentException(
                        "Definitions must not contain null values.",
                        nameof(definitions));
                }
            }

            for (int knownIndex = 0; knownIndex < _knownDefinitions.Count; knownIndex++)
            {
                DefinitionFingerprint known = _knownDefinitions[knownIndex];
                ItemDefinition canonical = FindDefinition(definitions, known.Id);
                if (canonical == null || !known.Matches(canonical))
                {
                    throw new ArgumentException(
                        "A container was previously bound to a definition outside the canonical registry.",
                        nameof(definitions));
                }
            }

            for (int entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
            {
                InventoryEntry entry = _entries[entryIndex];
                ItemDefinition canonical = FindDefinition(definitions, entry.DefinitionId);
                if (canonical == null || !entry.MatchesDefinition(canonical))
                {
                    throw new ArgumentException(
                        "Container entries must match the canonical item definition registry.",
                        nameof(definitions));
                }
            }
        }

        internal void BindCanonicalDefinitions(IReadOnlyList<ItemDefinition> definitions)
        {
            ValidateCanonicalDefinitions(definitions);
            _knownDefinitions.Clear();
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                _knownDefinitions.Add(new DefinitionFingerprint(definitions[definitionIndex]));
            }

            _hasCanonicalDefinitionRegistry = true;
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

        private int FindKnownDefinition(ItemDefinitionId id)
        {
            for (int index = 0; index < _knownDefinitions.Count; index++)
            {
                if (_knownDefinitions[index].Id.Equals(id)) return index;
            }

            return -1;
        }

        private static ItemDefinition FindDefinition(
            IReadOnlyList<ItemDefinition> definitions,
            ItemDefinitionId id)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                ItemDefinition definition = definitions[index];
                if (definition != null && definition.Id.Equals(id)) return definition;
            }

            return null;
        }

        private readonly struct DefinitionFingerprint
        {
            public DefinitionFingerprint(ItemDefinition definition)
            {
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
            private ItemStateKind StateKind { get; }
            private EncumbranceValue UnitEncumbrance { get; }
            private bool HasEquipment { get; }
            private EquipmentSlotMask CompatibleSlots { get; }
            private int ContainedContainerReductionPercent { get; }

            public bool Matches(ItemDefinition definition)
            {
                if (definition == null
                    || !Id.Equals(definition.Id)
                    || StateKind != definition.StateKind
                    || !UnitEncumbrance.Equals(definition.UnitEncumbrance)
                    || HasEquipment != (definition.Equipment != null))
                {
                    return false;
                }

                return !HasEquipment
                    || (CompatibleSlots == definition.Equipment.CompatibleSlots
                        && ContainedContainerReductionPercent
                            == definition.Equipment.ContainedContainerReductionPercent);
            }
        }
    }
}
