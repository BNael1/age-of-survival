using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Food;

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
        public const long GroundCapacityUnits = 100000;
        public const double GroundTransferMaximumDistance = 1.5;
        public const int TransferBaseTicks = 15;
        public const int TransferTicksPerEncumbranceUnit = 30;
        public const long PrototypeFoodDayTicks = 216000L;
        public const int InitialAppleQuantity = 3;

        public static readonly TransferTimingDefinition TransferTiming =
            new TransferTimingDefinition(
                TransferBaseTicks,
                TransferTicksPerEncumbranceUnit,
                (int)EncumbranceValue.UnitsPerDisplayedUnit,
                TransferBaseTicks);

        public static readonly WorldPosition[] ResourcePositions =
        {
            new WorldPosition(5.25, 4.5),
            new WorldPosition(4.5, 5.75),
            new WorldPosition(3.25, 4.5)
        };

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

        public static readonly ItemDefinition Wood = new ItemDefinition(
            new ItemDefinitionId("wood"),
            "Wood",
            ItemStateKind.Stackable,
            new EncumbranceValue(1000));

        public static readonly ItemDefinition Apple = new ItemDefinition(
            new ItemDefinitionId("apple"),
            "Apple",
            ItemStateKind.Stackable,
            new EncumbranceValue(200),
            consumable: new ConsumableDefinition(20, 60, 20),
            nutrition: new NutritionDefinition(95),
            perishable: new PerishableDefinition(
                5L * PrototypeFoodDayTicks,
                8L * PrototypeFoodDayTicks));

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
            Wood,
            Apple,
            Tool,
            Bag
        };
    }
}
