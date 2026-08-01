using System;

namespace AgeOfSurvival.Core.Inventory
{
    public static class InventoryOperations
    {
        public static AddItemResult AddStack(
            ContainerState destination,
            ItemDefinition definition,
            int quantity)
        {
            ValidateContainerAndDefinition(destination, definition);
            ValidatePositiveQuantity(quantity);
            RequireKind(definition, ItemStateKind.Stackable);
            RequireCompatibleDefinition(destination, definition);

            int accepted = QuantityThatFits(destination, definition.UnitEncumbrance, quantity);
            if (accepted == 0)
            {
                return new AddItemResult(quantity, 0);
            }

            destination.BindDefinition(definition);
            int existingIndex = destination.FindStackIndex(definition.Id);
            if (existingIndex >= 0)
            {
                int mergedQuantity = checked(destination.EntryAt(existingIndex).Quantity + accepted);
                destination.ReplaceStack(existingIndex, mergedQuantity);
            }
            else
            {
                destination.Append(InventoryEntry.CreateStack(definition, accepted));
            }

            return new AddItemResult(quantity, accepted);
        }

        public static AddItemResult AddUnique(
            ContainerState destination,
            ItemDefinition definition,
            UniqueItemState item)
        {
            ValidateContainerAndDefinition(destination, definition);
            RequireKind(definition, ItemStateKind.Unique);
            RequireCompatibleDefinition(destination, definition);

            if (!item.InstanceId.IsValid || !item.DefinitionId.Equals(definition.Id))
            {
                throw new ArgumentException("The unique item must be valid and match its definition.", nameof(item));
            }

            if (destination.FindUniqueIndex(item.InstanceId) >= 0)
            {
                throw new InvalidOperationException("The unique item is already present in the destination.");
            }

            if (QuantityThatFits(destination, definition.UnitEncumbrance, 1) == 0)
            {
                return new AddItemResult(1, 0);
            }

            destination.BindDefinition(definition);
            destination.Append(InventoryEntry.CreateUnique(definition, item));
            return new AddItemResult(1, 1);
        }

        public static RemoveItemResult RemoveStack(
            ContainerState source,
            ItemDefinition definition,
            int quantity)
        {
            ValidateContainerAndDefinition(source, definition);
            ValidatePositiveQuantity(quantity);
            RequireKind(definition, ItemStateKind.Stackable);
            RequireCompatibleDefinition(source, definition);

            int index = source.FindStackIndex(definition.Id);
            if (index < 0 || source.EntryAt(index).Quantity < quantity)
            {
                return new RemoveItemResult(quantity, 0);
            }

            int remaining = source.EntryAt(index).Quantity - quantity;
            if (remaining == 0)
            {
                source.RemoveAt(index);
            }
            else
            {
                source.ReplaceStack(index, remaining);
            }

            return new RemoveItemResult(quantity, quantity);
        }

        public static RemoveItemResult RemoveUnique(
            ContainerState source,
            ItemDefinition definition,
            ItemInstanceId instanceId)
        {
            ValidateContainerAndDefinition(source, definition);
            RequireKind(definition, ItemStateKind.Unique);
            RequireCompatibleDefinition(source, definition);

            if (!instanceId.IsValid)
            {
                throw new ArgumentException("A valid instance identifier is required.", nameof(instanceId));
            }

            int index = source.FindUniqueIndex(instanceId);
            if (index < 0 || !source.EntryAt(index).DefinitionId.Equals(definition.Id))
            {
                return new RemoveItemResult(1, 0);
            }

            source.RemoveAt(index);
            return new RemoveItemResult(1, 1);
        }

        public static TransferResult TransferStack(
            ContainerState source,
            ContainerState destination,
            ItemDefinition definition,
            int quantity)
        {
            ValidateTransferArguments(source, destination, definition, quantity);
            RequireKind(definition, ItemStateKind.Stackable);
            RequireCompatibleDefinition(source, definition);
            RequireCompatibleDefinition(destination, definition);

            if (source.Id.Equals(destination.Id))
            {
                return new TransferResult(quantity, 0, InventoryOperationOutcome.SameContainer);
            }

            int sourceIndex = source.FindStackIndex(definition.Id);
            if (sourceIndex < 0 || source.EntryAt(sourceIndex).Quantity < quantity)
            {
                return new TransferResult(quantity, 0, InventoryOperationOutcome.SourceInsufficient);
            }

            AddItemResult added = AddStack(destination, definition, quantity);
            if (added.Accepted == 0)
            {
                return new TransferResult(quantity, 0, InventoryOperationOutcome.DestinationFull);
            }

            RemoveItemResult removed = RemoveStack(source, definition, added.Accepted);
            if (!removed.Succeeded)
            {
                throw new InvalidOperationException("Source changed during a synchronous transfer.");
            }

            return new TransferResult(
                quantity,
                added.Accepted,
                added.Accepted == quantity
                    ? InventoryOperationOutcome.Complete
                    : InventoryOperationOutcome.Partial);
        }

        public static TransferResult TransferUnique(
            ContainerState source,
            ContainerState destination,
            ItemDefinition definition,
            ItemInstanceId instanceId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            RequireKind(definition, ItemStateKind.Unique);
            RequireCompatibleDefinition(source, definition);
            RequireCompatibleDefinition(destination, definition);

            if (source.Id.Equals(destination.Id))
            {
                return new TransferResult(1, 0, InventoryOperationOutcome.SameContainer);
            }

            int sourceIndex = source.FindUniqueIndex(instanceId);
            if (sourceIndex < 0 || !source.EntryAt(sourceIndex).DefinitionId.Equals(definition.Id))
            {
                return new TransferResult(1, 0, InventoryOperationOutcome.SourceInsufficient);
            }

            UniqueItemState item = source.EntryAt(sourceIndex).UniqueItem;
            AddItemResult added = AddUnique(destination, definition, item);
            if (!added.Changed)
            {
                return new TransferResult(1, 0, InventoryOperationOutcome.DestinationFull);
            }

            RemoveItemResult removed = RemoveUnique(source, definition, instanceId);
            if (!removed.Succeeded)
            {
                throw new InvalidOperationException("Source changed during a synchronous transfer.");
            }

            return new TransferResult(1, 1, InventoryOperationOutcome.Complete);
        }

        public static int Count(ContainerState container, ItemDefinitionId definitionId)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (!definitionId.IsValid) throw new ArgumentException("A valid definition identifier is required.", nameof(definitionId));

            int total = 0;
            for (int index = 0; index < container.Entries.Count; index++)
            {
                InventoryEntry entry = container.Entries[index];
                if (entry.DefinitionId.Equals(definitionId))
                {
                    total = checked(total + entry.Quantity);
                }
            }

            return total;
        }

        private static int QuantityThatFits(
            ContainerState container,
            EncumbranceValue unitEncumbrance,
            int requested)
        {
            if (unitEncumbrance.Units == 0)
            {
                return requested;
            }

            long fittingUnits = container.RemainingCapacity.Units / unitEncumbrance.Units;
            return (int)Math.Min(requested, fittingUnits);
        }

        private static void ValidateTransferArguments(
            ContainerState source,
            ContainerState destination,
            ItemDefinition definition,
            int quantity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            ValidatePositiveQuantity(quantity);
        }

        private static void ValidateContainerAndDefinition(
            ContainerState container,
            ItemDefinition definition)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
        }

        internal static bool IsDefinitionCompatible(
            ContainerState container,
            ItemDefinition definition) =>
            container != null
            && definition != null
            && container.IsDefinitionCompatible(definition);

        private static void RequireCompatibleDefinition(
            ContainerState container,
            ItemDefinition definition)
        {
            if (!IsDefinitionCompatible(container, definition))
            {
                throw new ArgumentException(
                    "The item definition contradicts the container definition registry.",
                    nameof(definition));
            }
        }

        private static void ValidatePositiveQuantity(int quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
            }
        }

        private static void RequireKind(ItemDefinition definition, ItemStateKind expected)
        {
            if (definition.StateKind != expected)
            {
                throw new ArgumentException($"Item definition must be {expected}.", nameof(definition));
            }
        }
    }
}
