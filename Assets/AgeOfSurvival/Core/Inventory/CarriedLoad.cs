using System;
using System.Collections.Generic;

namespace AgeOfSurvival.Core.Inventory
{
    public readonly struct CarriedLoad
    {
        public CarriedLoad(EncumbranceValue gross, EncumbranceValue perceived)
        {
            Gross = gross;
            Perceived = perceived;
        }

        public EncumbranceValue Gross { get; }
        public EncumbranceValue Perceived { get; }
        public EncumbranceValue Reduction => Gross - Perceived;
    }

    public static class CarriedLoadOperations
    {
        private static readonly EquipmentSlot[] StableSlotOrder =
        {
            EquipmentSlot.LeftHand,
            EquipmentSlot.RightHand,
            EquipmentSlot.Back
        };

        public static CarriedLoad Calculate(PlayerInventoryState inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            EncumbranceValue gross = EncumbranceValue.Zero;
            for (int index = 0; index < inventory.Containers.Count; index++)
            {
                gross += inventory.Containers[index].UsedCapacity;
            }

            EncumbranceValue perceived = gross;
            var reducedContainers = new List<ContainerId>();
            for (int slotIndex = 0; slotIndex < StableSlotOrder.Length; slotIndex++)
            {
                ItemInstanceId instanceId = inventory.Equipment.Get(StableSlotOrder[slotIndex]);
                if (!instanceId.IsValid
                    || !inventory.TryFindUnique(instanceId, out UniqueItemState item, out ItemDefinition definition, out _)
                    || !item.HasContainedContainer
                    || definition.Equipment == null
                    || definition.Equipment.ContainedContainerReductionPercent == 0
                    || Contains(reducedContainers, item.ContainedContainerId))
                {
                    continue;
                }

                ContainerState container = inventory.FindContainer(item.ContainedContainerId);
                if (container == null) continue;

                long reductionUnits = checked(
                    container.UsedCapacity.Units
                    * definition.Equipment.ContainedContainerReductionPercent
                    / 100);
                perceived -= new EncumbranceValue(reductionUnits);
                reducedContainers.Add(container.Id);
            }

            return new CarriedLoad(gross, perceived);
        }

        private static bool Contains(IReadOnlyList<ContainerId> ids, ContainerId id)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (ids[index].Equals(id)) return true;
            }

            return false;
        }
    }
}
