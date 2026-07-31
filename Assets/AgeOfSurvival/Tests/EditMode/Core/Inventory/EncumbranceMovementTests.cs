using System;
using AgeOfSurvival.Core.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests.Inventory
{
    public sealed class EncumbranceMovementTests
    {
        private const double Tolerance = 1e-9;

        [TestCase(0)]
        [TestCase(500)]
        [TestCase(1000)]
        public void LoadAtOrBelowBaseCapacityKeepsFullSpeed(long loadUnits)
        {
            EncumbranceMovementState state = Calculate(loadUnits);

            Assert.That(state.LoadRatio, Is.EqualTo(loadUnits / 1000.0).Within(Tolerance));
            Assert.That(state.SpeedMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [TestCase(1250, 0.81)]
        [TestCase(1500, 0.63)]
        [TestCase(1750, 0.44)]
        [TestCase(2000, 0.25)]
        [TestCase(2500, 0.25)]
        public void ValidatedControlPointsAndUpperClampAreExact(
            long loadUnits,
            double expectedMultiplier)
        {
            EncumbranceMovementState state = Calculate(loadUnits);

            Assert.That(state.SpeedMultiplier, Is.EqualTo(expectedMultiplier).Within(Tolerance));
        }

        [TestCase(1125, 0.905)]
        [TestCase(1375, 0.72)]
        [TestCase(1625, 0.535)]
        [TestCase(1875, 0.345)]
        public void MultiplierIsLinearlyInterpolatedBetweenControlPoints(
            long loadUnits,
            double expectedMultiplier)
        {
            EncumbranceMovementState state = Calculate(loadUnits);

            Assert.That(state.SpeedMultiplier, Is.EqualTo(expectedMultiplier).Within(Tolerance));
        }

        [Test]
        public void InventoryCalculationUsesPerceivedLoadAndMainCapacity()
        {
            var bagDefinition = new ItemDefinition(
                new ItemDefinitionId("bag"),
                "Bag",
                ItemStateKind.Unique,
                new EncumbranceValue(1000),
                new EquipmentDefinition(EquipmentSlotMask.Back, 70));
            var stones = new ItemDefinition(
                new ItemDefinitionId("stones"),
                "Stones",
                ItemStateKind.Stackable,
                new EncumbranceValue(1500));
            var main = new ContainerState(
                new ContainerId("main"),
                new ContainerDefinition("main", "Main", new EncumbranceValue(2000)));
            var bagContainer = new ContainerState(
                new ContainerId("bag-container"),
                new ContainerDefinition("bag", "Bag", new EncumbranceValue(20000)));
            var bag = new UniqueItemState(
                bagDefinition.Id,
                new ItemInstanceId("bag-01"),
                bagContainer.Id);
            InventoryOperations.AddUnique(main, bagDefinition, bag);
            InventoryOperations.AddStack(bagContainer, stones, 2);
            var inventory = new PlayerInventoryState(
                main.Id,
                new[] { bagDefinition, stones },
                new[] { main, bagContainer });
            EquipmentOperations.Equip(inventory, bag.InstanceId, EquipmentSlot.Back);

            EncumbranceMovementState state =
                EncumbranceMovementOperations.Calculate(inventory);

            Assert.That(state.EffectiveLoad.Units, Is.EqualTo(1900));
            Assert.That(state.BaseCapacity.Units, Is.EqualTo(2000));
            Assert.That(state.LoadRatio, Is.EqualTo(0.95).Within(Tolerance));
            Assert.That(state.SpeedMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void ZeroBaseCapacityIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EncumbranceMovementOperations.Calculate(
                    new EncumbranceValue(1000),
                    EncumbranceValue.Zero));
        }

        private static EncumbranceMovementState Calculate(long loadUnits)
        {
            return EncumbranceMovementOperations.Calculate(
                new EncumbranceValue(loadUnits),
                new EncumbranceValue(1000));
        }
    }
}
