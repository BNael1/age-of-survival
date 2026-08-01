using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Inventory;

namespace AgeOfSurvival.Runtime.Player
{
    /// <summary>
    /// Runtime composition point that resolves carried load inside each fixed simulation tick.
    /// </summary>
    public static class InventoryMovementStep
    {
        public static EncumbranceMovementState Step(
            PlayerState player,
            InventoryPrototypeSession session,
            double directionX,
            double directionY,
            double movementSpeed,
            double tickDurationSeconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (session == null) throw new ArgumentNullException(nameof(session));

            EncumbranceMovementState movementState = session.MovementState;
            PlayerMovement.Step(
                player,
                directionX,
                directionY,
                movementSpeed,
                movementState.SpeedMultiplier,
                tickDurationSeconds);
            return movementState;
        }
    }
}
