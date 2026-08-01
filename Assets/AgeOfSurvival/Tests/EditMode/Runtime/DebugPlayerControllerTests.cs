using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class DebugPlayerControllerTests
    {
        [Test]
        public void InventoryMovementStepRecalculatesLoadForEveryTick()
        {
            var session = new InventoryPrototypeSession();
            var player = new PlayerState(new WorldPosition(0.0, 0.0));

            EncumbranceMovementState first = InventoryMovementStep.Step(
                player,
                session,
                1.0,
                0.0,
                1.0,
                1.0);
            EquipmentOperations.Equip(
                session.Inventory,
                session.Bag.InstanceId,
                EquipmentSlot.Back);
            EncumbranceMovementState second = InventoryMovementStep.Step(
                player,
                session,
                1.0,
                0.0,
                1.0,
                1.0);

            Assert.That(first.LoadRatio, Is.EqualTo(1.125).Within(0.000001));
            Assert.That(first.SpeedMultiplier, Is.EqualTo(0.905).Within(0.000001));
            Assert.That(second.LoadRatio, Is.EqualTo(0.95).Within(0.000001));
            Assert.That(second.SpeedMultiplier, Is.EqualTo(1.0).Within(0.000001));
            Assert.That(player.Position.X, Is.EqualTo(1.905).Within(0.000001));
            Assert.That(player.Position.Y, Is.EqualTo(0.0).Within(0.000001));
        }

        [TestCase(0f, 1f, 1f, 1f)]
        [TestCase(1f, 0f, 1f, -1f)]
        [TestCase(-1f, -1f, -2f, 0f)]
        [TestCase(0f, 0f, 0f, 0f)]
        public void ScreenDirectionMapsToIsometricWorldAxes(
            float screenX,
            float screenY,
            float expectedWorldX,
            float expectedWorldY)
        {
            Vector2 result = DebugPlayerController.ScreenToWorldDirection(
                new Vector2(screenX, screenY));

            Assert.That(result.x, Is.EqualTo(expectedWorldX));
            Assert.That(result.y, Is.EqualTo(expectedWorldY));
        }
    }
}
