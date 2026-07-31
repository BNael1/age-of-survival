using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed class InventoryRowViewModel
    {
        public InventoryRowViewModel(
            InventorySelection selection,
            string displayName,
            int quantity,
            string encumbranceText)
        {
            Selection = selection;
            DisplayName = displayName;
            Quantity = quantity;
            EncumbranceText = encumbranceText;
        }

        public InventorySelection Selection { get; }
        public string DisplayName { get; }
        public int Quantity { get; }
        public string EncumbranceText { get; }
    }

    public sealed class InventoryContainerViewModel
    {
        public InventoryContainerViewModel(
            ContainerId id,
            string displayName,
            string capacityText,
            IReadOnlyList<InventoryRowViewModel> rows)
        {
            Id = id;
            DisplayName = displayName;
            CapacityText = capacityText;
            Rows = rows;
        }

        public ContainerId Id { get; }
        public string DisplayName { get; }
        public string CapacityText { get; }
        public IReadOnlyList<InventoryRowViewModel> Rows { get; }
    }

    public sealed class InventoryPrototypeViewModel
    {
        public InventoryPrototypeViewModel(
            InventoryContainerViewModel main,
            InventoryContainerViewModel bag,
            IReadOnlyList<string> equipmentLabels,
            string grossLoadText,
            string perceivedLoadText,
            string reductionText)
        {
            Main = main;
            Bag = bag;
            EquipmentLabels = equipmentLabels;
            GrossLoadText = grossLoadText;
            PerceivedLoadText = perceivedLoadText;
            ReductionText = reductionText;
        }

        public InventoryContainerViewModel Main { get; }
        public InventoryContainerViewModel Bag { get; }
        public IReadOnlyList<string> EquipmentLabels { get; }
        public string GrossLoadText { get; }
        public string PerceivedLoadText { get; }
        public string ReductionText { get; }
    }

    public static class InventoryPrototypeViewModelBuilder
    {
        private static readonly EquipmentSlot[] StableSlots =
        {
            EquipmentSlot.LeftHand,
            EquipmentSlot.RightHand,
            EquipmentSlot.Back
        };

        public static InventoryPrototypeViewModel Build(PlayerInventoryState inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            ContainerState bag = inventory.FindContainer(InventoryPrototypeCatalog.BagContainerId);
            CarriedLoad load = CarriedLoadOperations.Calculate(inventory);

            var equipment = new List<string>(StableSlots.Length);
            for (int index = 0; index < StableSlots.Length; index++)
            {
                EquipmentSlot slot = StableSlots[index];
                ItemInstanceId instanceId = inventory.Equipment.Get(slot);
                string itemName = "Empty";
                if (instanceId.IsValid
                    && inventory.TryFindUnique(instanceId, out _, out ItemDefinition definition, out _))
                {
                    itemName = definition.DisplayName;
                }

                equipment.Add($"{SlotName(slot)}: {itemName}");
            }

            return new InventoryPrototypeViewModel(
                BuildContainer(inventory, inventory.MainContainer),
                BuildContainer(inventory, bag),
                equipment.AsReadOnly(),
                load.Gross.ToString(),
                load.Perceived.ToString(),
                $"{InventoryPrototypeCatalog.EquippedBagReductionPercent}% while backpack is equipped");
        }

        private static InventoryContainerViewModel BuildContainer(
            PlayerInventoryState inventory,
            ContainerState container)
        {
            var rows = new List<InventoryRowViewModel>();
            if (container != null)
            {
                for (int index = 0; index < container.Entries.Count; index++)
                {
                    InventoryEntry entry = container.Entries[index];
                    ItemDefinition definition = inventory.FindDefinition(entry.DefinitionId);
                    bool unique = entry.Kind == ItemStateKind.Unique;
                    rows.Add(new InventoryRowViewModel(
                        new InventorySelection(
                            container.Id,
                            entry.DefinitionId,
                            unique ? entry.UniqueItem.InstanceId : default,
                            unique),
                        definition?.DisplayName ?? entry.DefinitionId.ToString(),
                        entry.Quantity,
                        entry.TotalEncumbrance.ToString()));
                }
            }

            string capacity = container == null
                ? "0.000 / 0.000"
                : $"{container.UsedCapacity} / {container.Definition.Capacity}";
            return new InventoryContainerViewModel(
                container?.Id ?? default,
                container?.Definition.DisplayName ?? "No container",
                capacity,
                rows.AsReadOnly());
        }

        private static string SlotName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.LeftHand:
                    return "Left hand";
                case EquipmentSlot.RightHand:
                    return "Right hand";
                default:
                    return "Back";
            }
        }
    }
}
