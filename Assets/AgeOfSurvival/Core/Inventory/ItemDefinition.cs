using System;

namespace AgeOfSurvival.Core.Inventory
{
    public enum ItemStateKind
    {
        Stackable = 0,
        Unique = 1
    }

    /// <summary>
    /// Immutable editorial data used by inventory operations.
    /// </summary>
    public sealed class ItemDefinition
    {
        public ItemDefinition(
            ItemDefinitionId id,
            string displayName,
            ItemStateKind stateKind,
            EncumbranceValue unitEncumbrance,
            EquipmentDefinition equipment = null)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("An item definition requires a valid identifier.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("An item definition requires a display name.", nameof(displayName));
            }

            if (!Enum.IsDefined(typeof(ItemStateKind), stateKind))
            {
                throw new ArgumentOutOfRangeException(nameof(stateKind), stateKind, "Unknown item state kind.");
            }

            Id = id;
            DisplayName = displayName;
            StateKind = stateKind;
            UnitEncumbrance = unitEncumbrance;
            Equipment = equipment;
        }

        public ItemDefinitionId Id { get; }
        public string DisplayName { get; }
        public ItemStateKind StateKind { get; }
        public EncumbranceValue UnitEncumbrance { get; }
        public EquipmentDefinition Equipment { get; }
    }
}
