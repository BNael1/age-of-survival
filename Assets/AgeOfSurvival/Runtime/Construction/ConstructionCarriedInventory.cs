using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Inventory;

namespace AgeOfSurvival.Runtime.Construction
{
    /// <summary>
    /// Reusable gameplay boundary for containers that are physically carried and
    /// eligible for Construction. The order is deterministic: main inventory, then
    /// the contained bag only while that exact bag is equipped on the back.
    /// Ground containers are deliberately excluded from material sourcing.
    /// </summary>
    public sealed class ConstructionCarriedInventory
    {
        private readonly InventoryPrototypeSession _owner;

        public ConstructionCarriedInventory(InventoryPrototypeSession owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public InventoryPrototypeSession Owner => _owner;

        public ContainerState[] CaptureEligibleContainers()
        {
            if (EquippedBagIsEligible())
                return new[] { _owner.MainContainer, _owner.BagContainer };
            return new[] { _owner.MainContainer };
        }

        public int Count(ItemDefinitionId definitionId)
        {
            int total = 0;
            ContainerState[] containers = CaptureEligibleContainers();
            for (int index = 0; index < containers.Length; index++)
            {
                total = checked(total + InventoryOperations.Count(
                    containers[index],
                    definitionId));
            }
            return total;
        }

        public bool TryRemove(
            ItemDefinition definition,
            int quantity,
            out ConstructionInventoryRemoval removal)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

            removal = null;
            if (Count(definition.Id) < quantity) return false;

            var debits = new List<ConstructionContainerQuantity>();
            int remaining = quantity;
            ContainerState[] containers = CaptureEligibleContainers();
            try
            {
                for (int index = 0; index < containers.Length && remaining > 0; index++)
                {
                    int available = InventoryOperations.Count(containers[index], definition.Id);
                    int requested = Math.Min(available, remaining);
                    if (requested <= 0) continue;

                    RemoveItemResult result = InventoryOperations.RemoveStack(
                        containers[index],
                        definition,
                        requested);
                    if (!result.Succeeded || result.Removed != requested)
                        throw new InvalidOperationException(
                            "A preflighted construction debit changed before it could be applied.");

                    debits.Add(new ConstructionContainerQuantity(
                        containers[index],
                        definition,
                        requested));
                    remaining -= requested;
                }

                if (remaining != 0)
                    throw new InvalidOperationException(
                        "A preflighted construction debit did not consume its complete request.");
            }
            catch
            {
                RestoreDebits(debits);
                throw;
            }

            removal = new ConstructionInventoryRemoval(debits.ToArray(), quantity);
            return true;
        }

        public void Restore(ConstructionInventoryRemoval removal)
        {
            if (removal == null) throw new ArgumentNullException(nameof(removal));
            RestoreDebits(removal.Debits);
        }

        public ConstructionRecoveryPlan PlanRecovery(
            ConstructionInstanceId instanceId,
            WorldPosition groundPosition,
            IReadOnlyList<ConstructionMaterialQuantity> materials,
            Func<ItemDefinitionId, ItemDefinition> itemResolver)
        {
            if (!instanceId.IsValid)
                throw new ArgumentException("A valid construction instance is required.", nameof(instanceId));
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (itemResolver == null) throw new ArgumentNullException(nameof(itemResolver));

            ContainerState[] containers = CaptureEligibleContainers();
            var remainingCapacity = new long[containers.Length];
            for (int index = 0; index < containers.Length; index++)
                remainingCapacity[index] = containers[index].RemainingCapacity.Units;

            var carried = new List<ConstructionContainerQuantity>();
            var overflow = new List<ConstructionMaterialQuantity>();
            var expected = new List<ConstructionMaterialQuantity>();
            int total = 0;
            int carriedTotal = 0;
            long overflowUnits = 0L;

            for (int materialIndex = 0; materialIndex < materials.Count; materialIndex++)
            {
                ConstructionMaterialQuantity material = materials[materialIndex];
                if (!material.DefinitionId.IsValid || material.Quantity <= 0)
                    throw new ArgumentException("Recovery materials must be valid positive quantities.", nameof(materials));

                ItemDefinition definition = RequireOrdinaryStack(
                    material.DefinitionId,
                    itemResolver);
                expected.Add(material);
                total = checked(total + material.Quantity);
                int remaining = material.Quantity;
                long unit = definition.UnitEncumbrance.Units;

                for (int containerIndex = 0;
                     containerIndex < containers.Length && remaining > 0;
                     containerIndex++)
                {
                    int fitting = unit == 0L
                        ? remaining
                        : (int)Math.Min(remaining, remainingCapacity[containerIndex] / unit);
                    if (fitting <= 0) continue;

                    carried.Add(new ConstructionContainerQuantity(
                        containers[containerIndex],
                        definition,
                        fitting));
                    carriedTotal = checked(carriedTotal + fitting);
                    remaining -= fitting;
                    remainingCapacity[containerIndex] = checked(
                        remainingCapacity[containerIndex] - (unit * fitting));
                }

                if (remaining <= 0) continue;
                overflow.Add(new ConstructionMaterialQuantity(material.DefinitionId, remaining));
                overflowUnits = checked(overflowUnits + (unit * remaining));
            }

            GroundContainerState preparedGround = null;
            if (overflow.Count > 0)
            {
                if (overflowUnits > InventoryPrototypeCatalog.GroundCapacityUnits)
                    throw new InvalidOperationException(
                        "Construction recovery exceeds the existing prototype ground-container capacity.");

                string suffix = instanceId.Value;
                var groundContainer = new ContainerState(
                    new ContainerId("ground-container-construction-recovery-" + suffix),
                    new ContainerDefinition(
                        "construction-recovery",
                        "Recovery near construction " + suffix,
                        new EncumbranceValue(InventoryPrototypeCatalog.GroundCapacityUnits)));
                for (int index = 0; index < overflow.Count; index++)
                {
                    ConstructionMaterialQuantity material = overflow[index];
                    ItemDefinition definition = RequireOrdinaryStack(
                        material.DefinitionId,
                        itemResolver);
                    AddItemResult added = InventoryOperations.AddStack(
                        groundContainer,
                        definition,
                        material.Quantity);
                    if (added.Accepted != material.Quantity)
                        throw new InvalidOperationException(
                            "A prepared construction overflow container rejected recovery items.");
                }

                preparedGround = new GroundContainerState(
                    new GroundContainerId("ground-construction-recovery-" + suffix),
                    groundPosition,
                    groundContainer);
                if (!_owner.CanRegisterGroundContainer(preparedGround))
                    throw new InvalidOperationException(
                        "The stable construction recovery ground-container identity is already registered.");
            }

            return new ConstructionRecoveryPlan(
                expected.ToArray(),
                carried.ToArray(),
                preparedGround,
                total,
                carriedTotal,
                total - carriedTotal);
        }

        public bool TryApplyRecovery(ConstructionRecoveryPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var applied = new List<ConstructionContainerQuantity>();
            bool groundRegistered = false;
            try
            {
                for (int index = 0; index < plan.Carried.Length; index++)
                {
                    ConstructionContainerQuantity destination = plan.Carried[index];
                    AddItemResult added = InventoryOperations.AddStack(
                        destination.Container,
                        destination.Definition,
                        destination.Quantity);
                    if (added.Accepted != destination.Quantity)
                        throw new InvalidOperationException(
                            "A preflighted construction recovery destination rejected items.");
                    applied.Add(destination);
                }

                if (plan.Ground != null)
                {
                    groundRegistered = _owner.TryRegisterGroundContainer(plan.Ground);
                    if (!groundRegistered)
                        throw new InvalidOperationException(
                            "A preflighted construction overflow could not be registered on the ground.");
                }

                return true;
            }
            catch
            {
                if (groundRegistered) _owner.TryUnregisterGroundContainer(plan.Ground);
                RollbackCredits(applied);
                return false;
            }
        }

        private bool EquippedBagIsEligible()
        {
            ItemInstanceId equipped = _owner.Inventory.Equipment.Get(EquipmentSlot.Back);
            return equipped.IsValid
                && equipped.Equals(_owner.Bag.InstanceId)
                && _owner.Bag.HasContainedContainer
                && _owner.Bag.ContainedContainerId.Equals(_owner.BagContainer.Id);
        }

        private static ItemDefinition RequireOrdinaryStack(
            ItemDefinitionId id,
            Func<ItemDefinitionId, ItemDefinition> itemResolver)
        {
            ItemDefinition definition = itemResolver(id);
            if (definition == null
                || !definition.Id.Equals(id)
                || definition.StateKind != ItemStateKind.Stackable
                || definition.Perishable != null)
            {
                throw new InvalidOperationException(
                    $"Construction material '{id}' is not an ordinary stackable item.");
            }
            return definition;
        }

        private static void RestoreDebits(IReadOnlyList<ConstructionContainerQuantity> debits)
        {
            for (int index = debits.Count - 1; index >= 0; index--)
            {
                ConstructionContainerQuantity debit = debits[index];
                AddItemResult restored = InventoryOperations.AddStack(
                    debit.Container,
                    debit.Definition,
                    debit.Quantity);
                if (restored.Accepted != debit.Quantity)
                    throw new InvalidOperationException(
                        "A construction transaction could not restore a carried-container debit.");
            }
        }

        private static void RollbackCredits(IReadOnlyList<ConstructionContainerQuantity> credits)
        {
            for (int index = credits.Count - 1; index >= 0; index--)
            {
                ConstructionContainerQuantity credit = credits[index];
                RemoveItemResult removed = InventoryOperations.RemoveStack(
                    credit.Container,
                    credit.Definition,
                    credit.Quantity);
                if (!removed.Succeeded || removed.Removed != credit.Quantity)
                    throw new InvalidOperationException(
                        "A failed construction recovery could not roll back its inventory credit.");
            }
        }
    }

    public sealed class ConstructionInventoryRemoval
    {
        internal ConstructionInventoryRemoval(
            ConstructionContainerQuantity[] debits,
            int quantity)
        {
            Debits = debits;
            Quantity = quantity;
        }

        internal ConstructionContainerQuantity[] Debits { get; }
        public int Quantity { get; }
    }

    public sealed class ConstructionRecoveryPlan
    {
        internal ConstructionRecoveryPlan(
            ConstructionMaterialQuantity[] expected,
            ConstructionContainerQuantity[] carried,
            GroundContainerState ground,
            int total,
            int carriedTotal,
            int groundTotal)
        {
            Expected = expected;
            Carried = carried;
            Ground = ground;
            Total = total;
            CarriedTotal = carriedTotal;
            GroundTotal = groundTotal;
        }

        public IReadOnlyList<ConstructionMaterialQuantity> Expected { get; }
        internal ConstructionContainerQuantity[] Carried { get; }
        public GroundContainerState Ground { get; }
        public int Total { get; }
        public int CarriedTotal { get; }
        public int GroundTotal { get; }
    }

    internal readonly struct ConstructionContainerQuantity
    {
        public ConstructionContainerQuantity(
            ContainerState container,
            ItemDefinition definition,
            int quantity)
        {
            Container = container ?? throw new ArgumentNullException(nameof(container));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Quantity = quantity;
        }

        public ContainerState Container { get; }
        public ItemDefinition Definition { get; }
        public int Quantity { get; }
    }
}
