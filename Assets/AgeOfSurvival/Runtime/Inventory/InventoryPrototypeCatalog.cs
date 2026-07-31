using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    /// <summary>
    /// Centralized debug-only values. These are not final balance values.
    /// </summary>
    public static class InventoryPrototypeCatalog
    {
        public const long MainCapacityUnits = 12000;
        public const long BagCapacityUnits = 20000;
        public const int EquippedBagReductionPercent = 70;

        public static readonly ContainerId MainContainerId = new ContainerId("player-main");
        public static readonly ContainerId BagContainerId = new ContainerId("prototype-bag-container");

        public static readonly ItemDefinition Branches = new ItemDefinition(
            new ItemDefinitionId("branches"),
            "Branches",
            ItemStateKind.Stackable,
            new EncumbranceValue(500));

        public static readonly ItemDefinition Stones = new ItemDefinition(
            new ItemDefinitionId("stones"),
            "Stones",
            ItemStateKind.Stackable,
            new EncumbranceValue(1500));

        public static readonly ItemDefinition Tool = new ItemDefinition(
            new ItemDefinitionId("prototype-tool"),
            "Prototype tool",
            ItemStateKind.Unique,
            new EncumbranceValue(2000),
            new EquipmentDefinition(EquipmentSlotMask.LeftHand | EquipmentSlotMask.RightHand));

        public static readonly ItemDefinition Bag = new ItemDefinition(
            new ItemDefinitionId("prototype-bag"),
            "Prototype backpack",
            ItemStateKind.Unique,
            new EncumbranceValue(1000),
            new EquipmentDefinition(EquipmentSlotMask.Back, EquippedBagReductionPercent));

        public static readonly ItemDefinition[] Definitions =
        {
            Branches,
            Stones,
            Tool,
            Bag
        };
    }
}
