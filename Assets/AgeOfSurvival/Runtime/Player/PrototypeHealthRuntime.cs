using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Runtime.Inventory;

namespace AgeOfSurvival.Runtime.Player
{
    /// <summary>
    /// Temporary values used only to exercise the first complete health loop.
    /// They are not permanent combat or hazard balance.
    /// </summary>
    public static class PrototypeDamageZoneRules
    {
        public const double OffsetX = 3.0;
        public const double OffsetY = 0.0;
        public const double Radius = 0.75;
        public const int DamageAmount = 25;
        public const long DamageIntervalTicks = 60L;

        public static PrototypeDamageZoneState Create(WorldPosition spawnPosition)
        {
            return new PrototypeDamageZoneState(
                spawnPosition.Translate(OffsetX, OffsetY),
                Radius,
                DamageAmount,
                DamageIntervalTicks);
        }
    }

    /// <summary>
    /// Deterministic state for the temporary damage zone.
    /// Entering the zone applies damage immediately, then at a fixed interval.
    /// Leaving clears the schedule so re-entry is explicit and testable.
    /// </summary>
    public sealed class PrototypeDamageZoneState
    {
        private readonly double _radiusSquared;

        public PrototypeDamageZoneState(
            WorldPosition center,
            double radius,
            int damageAmount,
            long damageIntervalTicks)
        {
            if (double.IsNaN(radius)
                || double.IsInfinity(radius)
                || radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    radius,
                    "The damage-zone radius must be finite and greater than zero.");
            }

            if (damageAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageAmount),
                    damageAmount,
                    "Damage must be greater than zero.");
            }

            if (damageIntervalTicks <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageIntervalTicks),
                    damageIntervalTicks,
                    "The damage interval must be greater than zero.");
            }

            Center = center;
            Radius = radius;
            DamageAmount = damageAmount;
            DamageIntervalTicks = damageIntervalTicks;
            _radiusSquared = radius * radius;
        }

        public WorldPosition Center { get; }
        public double Radius { get; }
        public int DamageAmount { get; }
        public long DamageIntervalTicks { get; }
        public long? NextDamageTick { get; private set; }

        public bool Contains(WorldPosition position)
        {
            return Center.DistanceSquaredTo(position) <= _radiusSquared;
        }

        public bool TryConsumeDamage(WorldPosition position, long currentTick)
        {
            if (currentTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTick),
                    currentTick,
                    "The damage-zone tick must be non-negative.");
            }

            if (!Contains(position))
            {
                NextDamageTick = null;
                return false;
            }

            if (NextDamageTick.HasValue
                && currentTick < NextDamageTick.Value)
            {
                return false;
            }

            long nextDamageTick = checked(
                currentTick + DamageIntervalTicks);
            NextDamageTick = nextDamageTick;
            return true;
        }

        public void Reset()
        {
            NextDamageTick = null;
        }
    }

    public readonly struct PlayerHealthRuntimeResult
    {
        public PlayerHealthRuntimeResult(
            int damageApplied,
            bool respawned,
            int currentHealth,
            WorldPosition currentPosition)
        {
            DamageApplied = damageApplied;
            Respawned = respawned;
            CurrentHealth = currentHealth;
            CurrentPosition = currentPosition;
        }

        public int DamageApplied { get; }
        public bool Respawned { get; }
        public int CurrentHealth { get; }
        public WorldPosition CurrentPosition { get; }
    }

    /// <summary>
    /// Runtime composition point for the temporary hazard and atomic respawn.
    /// The session remains the canonical owner of health, inventory and saved position.
    /// </summary>
    public static class PlayerHealthRuntimeStep
    {
        public static PlayerHealthRuntimeResult Step(
            PlayerState player,
            InventoryPrototypeSession session,
            WorldPosition respawnPosition,
            PrototypeDamageZoneState damageZone)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (damageZone == null)
            {
                throw new ArgumentNullException(nameof(damageZone));
            }

            if (!session.CurrentPlayerPosition.Equals(player.Position))
            {
                throw new InvalidOperationException(
                    "The runtime player and the canonical session position must "
                    + "be synchronized before resolving health.");
            }

            int damageApplied = 0;
            if (damageZone.TryConsumeDamage(
                player.Position,
                session.CurrentTick))
            {
                PlayerHealthChangeResult damage =
                    session.ApplyDamage(damageZone.DamageAmount);
                damageApplied = -Math.Min(0, damage.AppliedDelta);
            }

            bool respawned = false;
            if (session.Health.IsDead)
            {
                PlayerHealthChangeResult respawn =
                    session.RespawnAt(respawnPosition);
                if (respawn.Respawned)
                {
                    PlayerMovement.Reposition(
                        player,
                        respawnPosition);
                    damageZone.Reset();
                    respawned = true;
                }
            }

            return new PlayerHealthRuntimeResult(
                damageApplied,
                respawned,
                session.Health.CurrentHealth,
                player.Position);
        }
    }
}
