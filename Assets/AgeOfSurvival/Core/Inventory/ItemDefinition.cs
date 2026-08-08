using System;
using AgeOfSurvival.Core.Food;

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
            EquipmentDefinition equipment = null,
            ConsumableDefinition consumable = null,
            NutritionDefinition nutrition = null,
            PerishableDefinition perishable = null)
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

            if (consumable != null && stateKind != ItemStateKind.Stackable)
            {
                throw new ArgumentException("Consumable items currently require stackable inventory storage.", nameof(consumable));
            }

            if (perishable != null && stateKind != ItemStateKind.Stackable)
            {
                throw new ArgumentException("Perishable items currently require stackable inventory storage.", nameof(perishable));
            }

            if (nutrition != null && consumable == null)
            {
                throw new ArgumentException("Nutrition data requires a consumable definition.", nameof(nutrition));
            }

            Id = id;
            DisplayName = displayName;
            StateKind = stateKind;
            UnitEncumbrance = unitEncumbrance;
            Equipment = equipment;
            Consumable = consumable;
            Nutrition = nutrition;
            Perishable = perishable;
        }

        public ItemDefinitionId Id { get; }
        public string DisplayName { get; }
        public ItemStateKind StateKind { get; }
        public EncumbranceValue UnitEncumbrance { get; }
        public EquipmentDefinition Equipment { get; }
        public ConsumableDefinition Consumable { get; }
        public NutritionDefinition Nutrition { get; }
        public PerishableDefinition Perishable { get; }
    }
}
