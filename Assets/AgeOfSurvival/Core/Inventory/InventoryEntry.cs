using System;

namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Immutable read model for one stable entry in a container.
    /// </summary>
    public sealed class InventoryEntry
    {
        private InventoryEntry(
            ItemStateKind kind,
            StackedItemState stackedItem,
            UniqueItemState uniqueItem,
            EncumbranceValue unitEncumbrance)
        {
            Kind = kind;
            StackedItem = stackedItem;
            UniqueItem = uniqueItem;
            UnitEncumbrance = unitEncumbrance;
        }

        public ItemStateKind Kind { get; }
        public StackedItemState StackedItem { get; }
        public UniqueItemState UniqueItem { get; }
        public EncumbranceValue UnitEncumbrance { get; }
        public ItemDefinitionId DefinitionId => Kind == ItemStateKind.Stackable
            ? StackedItem.DefinitionId
            : UniqueItem.DefinitionId;
        public int Quantity => Kind == ItemStateKind.Stackable ? StackedItem.Quantity : 1;
        public EncumbranceValue TotalEncumbrance => UnitEncumbrance.Multiply(Quantity);

        internal static InventoryEntry CreateStack(ItemDefinition definition, int quantity) =>
            new InventoryEntry(
                ItemStateKind.Stackable,
                new StackedItemState(definition.Id, quantity),
                default,
                definition.UnitEncumbrance);

        internal static InventoryEntry CreateUnique(ItemDefinition definition, UniqueItemState item) =>
            new InventoryEntry(
                ItemStateKind.Unique,
                default,
                item,
                definition.UnitEncumbrance);

        internal bool MatchesDefinition(ItemDefinition definition) =>
            definition != null
            && DefinitionId.Equals(definition.Id)
            && Kind == definition.StateKind
            && UnitEncumbrance.Equals(definition.UnitEncumbrance);

        internal InventoryEntry WithQuantity(int quantity)
        {
            if (Kind != ItemStateKind.Stackable)
            {
                throw new InvalidOperationException("Only stacked entries have a mutable quantity.");
            }

            return new InventoryEntry(
                Kind,
                new StackedItemState(StackedItem.DefinitionId, quantity),
                default,
                UnitEncumbrance);
        }
    }
}
