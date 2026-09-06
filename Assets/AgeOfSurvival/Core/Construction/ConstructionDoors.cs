using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Construction
{
    public sealed class ConstructionDoorPolicy
    {
        private readonly HashSet<ConstructionDefinitionId> _ids;
        public ConstructionDoorPolicy(IEnumerable<ConstructionDefinitionId> definitionIds)
        {
            if (definitionIds == null) throw new ArgumentNullException(nameof(definitionIds));
            _ids = new HashSet<ConstructionDefinitionId>();
            foreach (ConstructionDefinitionId id in definitionIds)
                if (!id.IsValid || !_ids.Add(id))
                    throw new ArgumentException("Door definition IDs must be valid and unique.", nameof(definitionIds));
        }
        public bool IsDoor(ConstructionDefinitionId id) => _ids.Contains(id);
    }

    public readonly struct ConstructionDoorState
    {
        public ConstructionDoorState(ConstructionInstanceId instanceId, bool isOpen)
        {
            if (!instanceId.IsValid) throw new ArgumentException("A valid Door ID is required.", nameof(instanceId));
            InstanceId = instanceId;
            IsOpen = isOpen;
        }
        public ConstructionInstanceId InstanceId { get; }
        public bool IsOpen { get; }
    }

    /// <summary>Core-owned mutable state. Only completed structures of a configured Door type qualify.</summary>
    public sealed class ConstructionDoorRegistry
    {
        private readonly ConstructionWorldState _world;
        private readonly ConstructionDoorPolicy _policy;
        private readonly Dictionary<ConstructionInstanceId, ConstructionDoorState> _states =
            new Dictionary<ConstructionInstanceId, ConstructionDoorState>();
        public ConstructionDoorRegistry(ConstructionWorldState world, ConstructionDoorPolicy policy)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }
        public int Count => _states.Count;
        public IEnumerable<ConstructionDoorState> States => _states.Values;
        public bool IsCompletedDoor(ConstructionInstanceId id) =>
            _world.TryFindStructure(id, out CompletedStructureState structure)
            && structure.Space.Kind == ConstructionSpaceKind.Edge && _policy.IsDoor(structure.DefinitionId);
        public bool TryRegisterClosed(ConstructionInstanceId id) => id.IsValid && TryRestore(new ConstructionDoorState(id, false));
        public bool TryRestore(ConstructionDoorState state)
        {
            if (!IsCompletedDoor(state.InstanceId) || _states.ContainsKey(state.InstanceId)) return false;
            _states.Add(state.InstanceId, state);
            return true;
        }
        public bool TryGet(ConstructionInstanceId id, out ConstructionDoorState state)
        {
            state = default;
            return IsCompletedDoor(id) && _states.TryGetValue(id, out state);
        }
        public bool TryToggle(ConstructionInstanceId id)
        {
            if (!TryGet(id, out ConstructionDoorState state)) return false;
            _states[id] = new ConstructionDoorState(id, !state.IsOpen);
            return true;
        }
        public bool TryRemove(ConstructionInstanceId id, out ConstructionDoorState removed)
        {
            if (!_states.TryGetValue(id, out removed)) return false;
            return _states.Remove(id);
        }
        public ConstructionDoorState[] CaptureCanonical()
        {
            var result = new List<ConstructionDoorState>(_states.Values);
            result.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
            return result.ToArray();
        }
    }
}
