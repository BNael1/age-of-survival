using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Inventory;

namespace AgeOfSurvival.Runtime.Resources
{
    /// <summary>Candidate adapter only. Ordering and range policy live in Core.</summary>
    public static class ContextualRuntimeInteraction
    {
        public static bool TryResolve(InventoryPrototypeSession inventory, ConstructionRuntimeSession construction,
            WorldPosition position, double radius, out ContextualInteractionCandidate candidate) =>
            ContextualInteractionResolver.TryResolve(Candidates(inventory, construction), position, radius, out candidate);

        private static IEnumerable<ContextualInteractionCandidate> Candidates(
            InventoryPrototypeSession inventory, ConstructionRuntimeSession construction)
        {
            foreach (ConstructionDoorState door in construction.Doors.States)
                if (construction.World.TryFindStructure(door.InstanceId, out CompletedStructureState structure))
                    yield return new ContextualInteractionCandidate(ContextualInteractionKind.Door, door.InstanceId.Value,
                        ConstructionRuntimeSession.SpacePosition(structure.Space));
            if (inventory.IsCraftActionActive || (inventory.TransferAction != null &&
                inventory.TransferAction.Status == TransferActionStatus.Active)) yield break;
            foreach (GroundContainerState ground in inventory.GroundContainers)
                if (inventory.CanStartGroundTransfer(ground))
                    yield return new ContextualInteractionCandidate(ContextualInteractionKind.GroundContainer,
                        ground.Id.Value, ground.Position);
            foreach (ResourceState resource in inventory.Resources)
                if (resource.Availability == ResourceAvailability.Available)
                    yield return new ContextualInteractionCandidate(ContextualInteractionKind.NaturalResource,
                        resource.Id.Value, resource.Position);
        }

        public static ResourceInteractionResult? Execute(ContextualInteractionCandidate candidate,
            InventoryPrototypeSession inventory, ConstructionRuntimeSession construction,
            WorldPosition position, double radius, long tick)
        {
            switch (candidate.Kind)
            {
                case ContextualInteractionKind.Door:
                    construction.TryToggleDoor(new ConstructionInstanceId(candidate.StableId), position, radius);
                    break;
                case ContextualInteractionKind.GroundContainer:
                    foreach (GroundContainerState ground in inventory.GroundContainers)
                        if (ground.Id.Value == candidate.StableId && inventory.CanStartGroundTransfer(ground))
                        {
                            ItemDefinitionId item = ground.Container.Entries[0].DefinitionId;
                            inventory.StartGroundTransfer(ground, item,
                                InventoryOperations.Count(ground.Container, item), tick);
                            break;
                        }
                    break;
                case ContextualInteractionKind.NaturalResource:
                    // No mutation intervenes between selection and execution. The same nearest-resource
                    // rule is retained inside the existing transactional harvest operation.
                    return inventory.HarvestAndStartTransfer(position, radius, tick).Interaction;
            }
            return null;
        }
    }
}
