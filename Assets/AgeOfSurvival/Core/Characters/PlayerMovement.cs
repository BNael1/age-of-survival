using System;

namespace AgeOfSurvival.Core.Characters
{
    /// <summary>
    /// Deterministic movement rule for the player on a continuous 2D plane.
    /// Input magnitude is preserved below one and normalized above one.
    /// </summary>
    public static class PlayerMovement
    {
        public static void Step(
            PlayerState player,
            double inputX,
            double inputY,
            double speedUnitsPerSecond,
            double deltaSeconds)
        {
            Step(
                player,
                inputX,
                inputY,
                speedUnitsPerSecond,
                1.0,
                deltaSeconds);
        }

        public static void Step(
            PlayerState player,
            double inputX,
            double inputY,
            double speedUnitsPerSecond,
            double movementMultiplier,
            double deltaSeconds)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            ValidateFinite(inputX, nameof(inputX));
            ValidateFinite(inputY, nameof(inputY));
            ValidateFinite(speedUnitsPerSecond, nameof(speedUnitsPerSecond));
            ValidateFinite(movementMultiplier, nameof(movementMultiplier));
            ValidateFinite(deltaSeconds, nameof(deltaSeconds));

            if (speedUnitsPerSecond < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedUnitsPerSecond),
                    speedUnitsPerSecond,
                    "Movement speed must be non-negative.");
            }

            if (movementMultiplier < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementMultiplier),
                    movementMultiplier,
                    "Movement multiplier must be non-negative.");
            }

            if (deltaSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    deltaSeconds,
                    "Delta time must be non-negative.");
            }

            double magnitudeSquared = (inputX * inputX) + (inputY * inputY);
            if (magnitudeSquared <= 0.0
                || speedUnitsPerSecond == 0.0
                || movementMultiplier == 0.0
                || deltaSeconds == 0.0)
            {
                return;
            }

            if (magnitudeSquared > 1.0)
            {
                double inverseMagnitude = 1.0 / Math.Sqrt(magnitudeSquared);
                inputX *= inverseMagnitude;
                inputY *= inverseMagnitude;
            }

            double distance = speedUnitsPerSecond * movementMultiplier * deltaSeconds;
            player.Translate(inputX * distance, inputY * distance);
        }

        public static void Reposition(
            PlayerState player,
            WorldPosition position)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            player.SetPosition(position);
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite.");
            }
        }
    }
}
