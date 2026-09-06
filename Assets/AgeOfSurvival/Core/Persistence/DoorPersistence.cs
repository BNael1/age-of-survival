using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;

namespace AgeOfSurvival.Core.Persistence
{
    /// <summary>Canonical V6 durable state for completed Doors only.</summary>
    public sealed class DoorSaveSnapshot
    {
        private readonly IReadOnlyList<ConstructionDoorState> _states;
        public DoorSaveSnapshot(IEnumerable<ConstructionDoorState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            var result = new List<ConstructionDoorState>();
            var ids = new HashSet<ConstructionInstanceId>();
            foreach (ConstructionDoorState state in states)
            {
                if (!state.InstanceId.IsValid || !ids.Add(state.InstanceId))
                    throw new ArgumentException("Door state identifiers must be valid and unique.", nameof(states));
                result.Add(state);
            }
            result.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            _states = result.AsReadOnly();
        }
        public IReadOnlyList<ConstructionDoorState> States => _states;
        public static DoorSaveSnapshot Empty => new DoorSaveSnapshot(Array.Empty<ConstructionDoorState>());
        public static DoorSaveSnapshot Capture(ConstructionDoorRegistry registry) => registry == null
            ? Empty : new DoorSaveSnapshot(registry.CaptureCanonical());
        public void RestoreInto(ConstructionDoorRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            for (int i = 0; i < _states.Count; i++)
                if (!registry.TryRestore(_states[i]))
                    throw new InvalidOperationException("Door state is invalid, duplicate, orphaned, incomplete, or wrong type.");
        }
    }
}
