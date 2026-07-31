using AgeOfSurvival.Core.Inventory;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Inventory
{
    /// <summary>
    /// Plain C# prototype session that owns Core state. MonoBehaviours only adapt it.
    /// </summary>
    public sealed class InventoryPrototypeSession
    {
        public InventoryPrototypeSession()
        {
            MainContainer = new ContainerState(
                InventoryPrototypeCatalog.MainContainerId,
                new ContainerDefinition(
                    "player-main",
                    "Carried inventory",
                    new EncumbranceValue(InventoryPrototypeCatalog.MainCapacityUnits)));
            BagContainer = new ContainerState(
                InventoryPrototypeCatalog.BagContainerId,
                new ContainerDefinition(
                    "prototype-bag",
                    "Prototype backpack",
                    new EncumbranceValue(InventoryPrototypeCatalog.BagCapacityUnits)));

            Tool = new UniqueItemState(
                InventoryPrototypeCatalog.Tool.Id,
                new ItemInstanceId("prototype-tool-01"));
            Bag = new UniqueItemState(
                InventoryPrototypeCatalog.Bag.Id,
                new ItemInstanceId("prototype-bag-01"),
                BagContainer.Id);

            InventoryOperations.AddStack(MainContainer, InventoryPrototypeCatalog.Branches, 6);
            InventoryOperations.AddStack(MainContainer, InventoryPrototypeCatalog.Stones, 3);
            InventoryOperations.AddUnique(MainContainer, InventoryPrototypeCatalog.Tool, Tool);
            InventoryOperations.AddUnique(MainContainer, InventoryPrototypeCatalog.Bag, Bag);
            InventoryOperations.AddStack(BagContainer, InventoryPrototypeCatalog.Stones, 2);

            Inventory = new PlayerInventoryState(
                MainContainer.Id,
                InventoryPrototypeCatalog.Definitions,
                new[] { MainContainer, BagContainer });
            Commands = new InventoryPrototypeCommands(Inventory);
        }

        public PlayerInventoryState Inventory { get; }
        public ContainerState MainContainer { get; }
        public ContainerState BagContainer { get; }
        public UniqueItemState Tool { get; }
        public UniqueItemState Bag { get; }
        public InventoryPrototypeCommands Commands { get; }
    }

    /// <summary>
    /// Process-owned prototype state. It is reset at each Play Mode start and is
    /// deliberately not owned by a GameObject.
    /// </summary>
    public static class InventoryPrototypeSessionProvider
    {
        private static InventoryPrototypeSession _current;

        public static InventoryPrototypeSession Current =>
            _current ?? (_current = new InventoryPrototypeSession());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            _current = new InventoryPrototypeSession();
        }
    }
}
