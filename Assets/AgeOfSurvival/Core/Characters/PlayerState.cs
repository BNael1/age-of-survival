using System;

namespace AgeOfSurvival.Core.Characters
{
    /// <summary>
    /// Mutable player state owned by the pure simulation layer.
    /// Unity objects only mirror this state.
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerState(WorldPosition position)
        {
            Position = position;
        }

        public WorldPosition Position { get; private set; }

        internal void Translate(double deltaX, double deltaY)
        {
            Position = Position.Translate(deltaX, deltaY);
        }

        internal void SetPosition(WorldPosition position)
        {
            Position = position;
        }
    }
}
