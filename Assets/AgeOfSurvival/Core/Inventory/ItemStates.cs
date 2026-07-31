using System;

namespace AgeOfSurvival.Core.Inventory
{
    public readonly struct StackedItemState : IEquatable<StackedItemState>
    {
        public StackedItemState(ItemDefinitionId definitionId, int quantity)
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentException("A stack requires a valid definition identifier.", nameof(definitionId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A stack quantity must be positive.");
            }

            DefinitionId = definitionId;
            Quantity = quantity;
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }

        public bool Equals(StackedItemState other) =>
            DefinitionId.Equals(other.DefinitionId) && Quantity == other.Quantity;

        public override bool Equals(object obj) => obj is StackedItemState other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (DefinitionId.GetHashCode() * 397) ^ Quantity;
            }
        }
    }

    public readonly struct UniqueItemState : IEquatable<UniqueItemState>
    {
        public UniqueItemState(
            ItemDefinitionId definitionId,
            ItemInstanceId instanceId,
            ContainerId containedContainerId = default)
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentException("A unique item requires a valid definition identifier.", nameof(definitionId));
            }

            if (!instanceId.IsValid)
            {
                throw new ArgumentException("A unique item requires a valid instance identifier.", nameof(instanceId));
            }

            DefinitionId = definitionId;
            InstanceId = instanceId;
            ContainedContainerId = containedContainerId;
        }

        public ItemDefinitionId DefinitionId { get; }
        public ItemInstanceId InstanceId { get; }
        public ContainerId ContainedContainerId { get; }
        public bool HasContainedContainer => ContainedContainerId.IsValid;

        public bool Equals(UniqueItemState other) =>
            DefinitionId.Equals(other.DefinitionId)
            && InstanceId.Equals(other.InstanceId)
            && ContainedContainerId.Equals(other.ContainedContainerId);

        public override bool Equals(object obj) => obj is UniqueItemState other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DefinitionId.GetHashCode();
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                return (hash * 397) ^ ContainedContainerId.GetHashCode();
            }
        }
    }
}
