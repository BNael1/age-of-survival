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
        private readonly List<ItemDefinition> _definitions;
        private readonly List<ContainerState> _containers;
        private readonly IReadOnlyList<ItemDefinition> _readOnlyDefinitions;
        private readonly IReadOnlyList<ContainerState> _readOnlyContainers;

        public PlayerInventoryState(
            ContainerId mainContainerId,
            IEnumerable<ItemDefinition> definitions,
            IEnumerable<ContainerState> containers)
        {
            if (!mainContainerId.IsValid)
            {
                throw new ArgumentException("A valid main container identifier is required.", nameof(mainContainerId));
            }

            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (containers == null) throw new ArgumentNullException(nameof(containers));

            _definitions = new List<ItemDefinition>(definitions);
            _containers = new List<ContainerState>(containers);
            ValidateRegistries();

            MainContainer = FindContainer(mainContainerId)
                ?? throw new ArgumentException("The main container must be registered.", nameof(mainContainerId));
            Equipment = new EquipmentState();
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

        private void ValidateRegistries()
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

            for (int containerIndex = 0; containerIndex < _containers.Count; containerIndex++)
            {
                _containers[containerIndex].BindCanonicalDefinitions(_definitions);
            }
        }
    }
}
