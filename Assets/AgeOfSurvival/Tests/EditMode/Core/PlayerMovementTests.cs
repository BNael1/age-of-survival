using System;
using AgeOfSurvival.Core.Characters;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class PlayerMovementTests
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void PlayerStateStartsAtRequestedContinuousPosition()
        {
            var player = new PlayerState(new WorldPosition(2.25, 7.75));

            Assert.That(player.Position.X, Is.EqualTo(2.25));
            Assert.That(player.Position.Y, Is.EqualTo(7.75));
        }

        [Test]
        public void ZeroInputDoesNotMove()
        {
            var player = CreatePlayer();

            PlayerMovement.Step(player, 0.0, 0.0, 3.0, 1.0);

            AssertPosition(player, 4.5, 4.5);
        }

        [Test]
        public void CardinalInputMovesAtConfiguredSpeed()
        {
            var player = CreatePlayer();

            PlayerMovement.Step(player, 1.0, 0.0, 3.0, 0.5);

            AssertPosition(player, 6.0, 4.5);
        }

        [Test]
        public void DiagonalInputIsNormalized()
        {
            var player = CreatePlayer();

            PlayerMovement.Step(player, 1.0, 1.0, 2.0, 1.0);

            double expectedDelta = Math.Sqrt(2.0);
            AssertPosition(player, 4.5 + expectedDelta, 4.5 + expectedDelta);
        }

        [Test]
        public void SubUnitInputPreservesAnalogMagnitude()
        {
            var player = CreatePlayer();

            PlayerMovement.Step(player, 0.5, 0.0, 4.0, 1.0);

            AssertPosition(player, 6.5, 4.5);
        }

        [Test]
        public void RepeatedFixedStepsProduceExpectedPosition()
        {
            var player = CreatePlayer();

            for (int index = 0; index < 60; index++)
            {
                PlayerMovement.Step(player, 1.0, 0.0, 3.0, 1.0 / 60.0);
            }

            AssertPosition(player, 7.5, 4.5);
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            var player = CreatePlayer();

            Assert.Throws<ArgumentNullException>(
                () => PlayerMovement.Step(null, 0.0, 0.0, 1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerMovement.Step(player, double.NaN, 0.0, 1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerMovement.Step(player, 0.0, 0.0, -1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerMovement.Step(player, 0.0, 0.0, 1.0, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WorldPosition(double.PositiveInfinity, 0.0));
        }

        private static PlayerState CreatePlayer()
        {
            return new PlayerState(new WorldPosition(4.5, 4.5));
        }

        private static void AssertPosition(PlayerState player, double expectedX, double expectedY)
        {
            Assert.That(player.Position.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(player.Position.Y, Is.EqualTo(expectedY).Within(Tolerance));
        }
    }
}
