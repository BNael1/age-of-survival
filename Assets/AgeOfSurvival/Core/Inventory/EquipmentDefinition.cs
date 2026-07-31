using System;

namespace AgeOfSurvival.Core.Inventory
{
    public enum EquipmentSlot
    {
        LeftHand = 0,
        RightHand = 1,
        Back = 2
    }

    [Flags]
    public enum EquipmentSlotMask
    {
        None = 0,
        LeftHand = 1 << 0,
        RightHand = 1 << 1,
        Back = 1 << 2
    }

    /// <summary>
    /// Immutable equipment rules. Reduction is an integer percentage applied only
    /// to the contents of the unique item's associated container while equipped.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(
            EquipmentSlotMask compatibleSlots,
            int containedContainerReductionPercent = 0)
        {
            const EquipmentSlotMask allSlots =
                EquipmentSlotMask.LeftHand | EquipmentSlotMask.RightHand | EquipmentSlotMask.Back;

            if (compatibleSlots == EquipmentSlotMask.None || (compatibleSlots & ~allSlots) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compatibleSlots),
                    compatibleSlots,
                    "At least one known equipment slot is required.");
            }

            if (containedContainerReductionPercent < 0 || containedContainerReductionPercent > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(containedContainerReductionPercent),
                    containedContainerReductionPercent,
                    "Container reduction must be between zero and one hundred percent.");
            }

            CompatibleSlots = compatibleSlots;
            ContainedContainerReductionPercent = containedContainerReductionPercent;
        }

        public EquipmentSlotMask CompatibleSlots { get; }
        public int ContainedContainerReductionPercent { get; }

        public bool Supports(EquipmentSlot slot)
        {
            ValidateSlot(slot);
            return (CompatibleSlots & MaskFor(slot)) != 0;
        }

        internal static void ValidateSlot(EquipmentSlot slot)
        {
            if (!Enum.IsDefined(typeof(EquipmentSlot), slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown equipment slot.");
            }
        }

        private static EquipmentSlotMask MaskFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.LeftHand:
                    return EquipmentSlotMask.LeftHand;
                case EquipmentSlot.RightHand:
                    return EquipmentSlotMask.RightHand;
                case EquipmentSlot.Back:
                    return EquipmentSlotMask.Back;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown equipment slot.");
            }
        }
    }
}
