using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Runtime.Inventory;
using NUnit.Framework;

namespace AgeOfSurvival.Runtime.Tests.Inventory
{
    public sealed class InventoryPrototypeHealthTests
    {
        private static readonly WorldPosition PlayerPosition =
            new WorldPosition(4.5, 4.5);

        [Test]
        public void NewSessionStartsWithFullHealthOnTickZero()
        {
            var session = new InventoryPrototypeSession();

            Assert.That(
                session.Health.MaximumHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(
                session.Health.CurrentHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(session.CurrentTick, Is.Zero);
            Assert.That(session.Health.CurrentTick, Is.Zero);
            Assert.That(session.Health.NextRegenerationTick, Is.Null);
        }

        [Test]
        public void BeginSimulationTickAdvancesHealthOnCanonicalSessionTick()
        {
            var session = new InventoryPrototypeSession();
            session.ApplyDamage(10);

            for (int index = 0; index < 509; index++)
            {
                session.BeginSimulationTick(PlayerPosition);
            }

            Assert.That(session.CurrentTick, Is.EqualTo(509L));
            Assert.That(session.Health.CurrentTick, Is.EqualTo(509L));
            Assert.That(session.Health.CurrentHealth, Is.EqualTo(90));

            long currentTick =
                session.BeginSimulationTick(PlayerPosition);

            Assert.That(currentTick, Is.EqualTo(510L));
            Assert.That(session.Health.CurrentTick, Is.EqualTo(510L));
            Assert.That(session.Health.CurrentHealth, Is.EqualTo(91));
            Assert.That(
                session.Health.NextRegenerationTick,
                Is.EqualTo(540L));
        }

        [Test]
        public void DamageUsesCurrentSessionTickAndResetsRegenerationDelay()
        {
            var session = new InventoryPrototypeSession();

            for (int index = 0; index < 100; index++)
            {
                session.BeginSimulationTick(PlayerPosition);
            }

            session.ApplyDamage(10);

            Assert.That(
                session.Health.NextRegenerationTick,
                Is.EqualTo(610L));

            for (int index = 0; index < 509; index++)
            {
                session.BeginSimulationTick(PlayerPosition);
            }

            Assert.That(session.CurrentTick, Is.EqualTo(609L));
            Assert.That(session.Health.CurrentHealth, Is.EqualTo(90));

            session.BeginSimulationTick(PlayerPosition);

            Assert.That(session.Health.CurrentHealth, Is.EqualTo(91));
        }

        [Test]
        public void SessionHealingCannotReviveButRespawnHealthCan()
        {
            var session = new InventoryPrototypeSession();

            session.ApplyDamage(100);
            PlayerHealthChangeResult healResult = session.Heal(50);

            Assert.That(session.Health.IsDead, Is.True);
            Assert.That(healResult.Changed, Is.False);

            PlayerHealthChangeResult respawnResult =
                session.RespawnHealth();

            Assert.That(respawnResult.Respawned, Is.True);
            Assert.That(
                session.Health.CurrentHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(session.Health.NextRegenerationTick, Is.Null);
            Assert.That(session.CurrentTick, Is.Zero);
        }
    }
}
