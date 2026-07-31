namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Mutable equipment state with a fixed deterministic slot order.
    /// </summary>
    public sealed class EquipmentState
    {
        private ItemInstanceId _leftHand;
        private ItemInstanceId _rightHand;
        private ItemInstanceId _back;

        public ItemInstanceId Get(EquipmentSlot slot)
        {
            EquipmentDefinition.ValidateSlot(slot);
            switch (slot)
            {
                case EquipmentSlot.LeftHand:
                    return _leftHand;
                case EquipmentSlot.RightHand:
                    return _rightHand;
                default:
                    return _back;
            }
        }

        public bool IsEquipped(ItemInstanceId instanceId)
        {
            return instanceId.IsValid
                && (_leftHand.Equals(instanceId)
                    || _rightHand.Equals(instanceId)
                    || _back.Equals(instanceId));
        }

        internal void Set(EquipmentSlot slot, ItemInstanceId instanceId)
        {
            switch (slot)
            {
                case EquipmentSlot.LeftHand:
                    _leftHand = instanceId;
                    break;
                case EquipmentSlot.RightHand:
                    _rightHand = instanceId;
                    break;
                case EquipmentSlot.Back:
                    _back = instanceId;
                    break;
                default:
                    EquipmentDefinition.ValidateSlot(slot);
                    break;
            }
        }
    }
}
