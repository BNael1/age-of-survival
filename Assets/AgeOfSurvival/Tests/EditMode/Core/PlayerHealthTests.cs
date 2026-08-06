using System;
using AgeOfSurvival.Core.Characters;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class PlayerHealthTests
    {
        [Test]
        public void NewHealthStartsFullAndAlive()
        {
            var health = CreateFull();

            Assert.That(health.MaximumHealth, Is.EqualTo(100));
            Assert.That(health.CurrentHealth, Is.EqualTo(100));
            Assert.That(health.CurrentTick, Is.Zero);
            Assert.That(health.NextRegenerationTick, Is.Null);
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.IsFullHealth, Is.True);
        }

        [Test]
        public void ConstructorRejectsInvalidState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerHealthState(0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerHealthState(100, -1, 0L, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerHealthState(100, 101, 0L, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerHealthState(100, 100, -1L, null));
            Assert.Throws<ArgumentException>(
                () => new PlayerHealthState(100, 90, 0L, null));
            Assert.Throws<ArgumentException>(
                () => new PlayerHealthState(100, 100, 0L, 510L));
            Assert.Throws<ArgumentException>(
                () => new PlayerHealthState(100, 0, 0L, 510L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerHealthState(100, 90, 10L, 10L));
        }

        [Test]
        public void DamageReducesHealthAndSchedulesRegeneration()
        {
            var health = CreateFull();

            PlayerHealthChangeResult result =
                PlayerHealthOperations.ApplyDamage(health, 25, 0L);

            Assert.That(health.CurrentHealth, Is.EqualTo(75));
            Assert.That(health.NextRegenerationTick, Is.EqualTo(510L));
            Assert.That(result.AppliedDelta, Is.EqualTo(-25));
            Assert.That(result.Died, Is.False);
        }

        [Test]
        public void DamageIsBoundedByRemainingHealth()
        {
            var health = CreateFull();

            PlayerHealthChangeResult result =
                PlayerHealthOperations.ApplyDamage(
                    health,
                    int.MaxValue,
                    0L);

            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.NextRegenerationTick, Is.Null);
            Assert.That(result.AppliedDelta, Is.EqualTo(-100));
            Assert.That(result.Died, Is.True);
        }

        [Test]
        public void LethalDamageTransitionsToDeadState()
        {
            var health = CreateFull();

            PlayerHealthChangeResult result =
                PlayerHealthOperations.ApplyDamage(health, 100, 12L);

            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentTick, Is.EqualTo(12L));
            Assert.That(result.Died, Is.True);
        }

        [Test]
        public void AdditionalDamageOnDeadPlayerDoesNothing()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 100, 0L);

            PlayerHealthChangeResult result =
                PlayerHealthOperations.ApplyDamage(health, 10, 20L);

            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.CurrentTick, Is.EqualTo(20L));
            Assert.That(result.Changed, Is.False);
        }

        [Test]
        public void DirectHealingRestoresHealthWithoutExceedingMaximum()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 30, 0L);

            PlayerHealthChangeResult first =
                PlayerHealthOperations.Heal(health, 10, 100L);
            PlayerHealthChangeResult second =
                PlayerHealthOperations.Heal(health, 100, 100L);

            Assert.That(first.AppliedDelta, Is.EqualTo(10));
            Assert.That(second.AppliedDelta, Is.EqualTo(20));
            Assert.That(health.CurrentHealth, Is.EqualTo(100));
            Assert.That(health.NextRegenerationTick, Is.Null);
        }

        [Test]
        public void OrdinaryHealingDoesNotResurrect()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 100, 0L);

            PlayerHealthChangeResult result =
                PlayerHealthOperations.Heal(health, 50, 10L);

            Assert.That(health.IsDead, Is.True);
            Assert.That(result.Changed, Is.False);
        }

        [Test]
        public void RespawnOnlyAffectsDeadPlayerAndRestoresFullHealth()
        {
            var living = CreateFull();
            PlayerHealthChangeResult livingResult =
                PlayerHealthOperations.Respawn(living, 10L);

            var dead = CreateFull();
            PlayerHealthOperations.ApplyDamage(dead, 100, 0L);
            PlayerHealthChangeResult deadResult =
                PlayerHealthOperations.Respawn(dead, 10L);

            Assert.That(livingResult.Changed, Is.False);
            Assert.That(deadResult.Respawned, Is.True);
            Assert.That(dead.CurrentHealth, Is.EqualTo(100));
            Assert.That(dead.NextRegenerationTick, Is.Null);
            Assert.That(dead.IsDead, Is.False);
        }

        [Test]
        public void RegenerationDoesNotStartBeforeFullDelayAndInterval()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 10, 0L);

            PlayerHealthOperations.AdvanceToTick(health, 509L);

            Assert.That(health.CurrentHealth, Is.EqualTo(90));
            Assert.That(health.NextRegenerationTick, Is.EqualTo(510L));
        }

        [Test]
        public void FirstRegeneratedPointArrivesAtTick510()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 10, 0L);

            PlayerHealthChangeResult result =
                PlayerHealthOperations.AdvanceToTick(health, 510L);

            Assert.That(result.AppliedDelta, Is.EqualTo(1));
            Assert.That(health.CurrentHealth, Is.EqualTo(91));
            Assert.That(health.NextRegenerationTick, Is.EqualTo(540L));
        }

        [Test]
        public void RegenerationRunsAtTwoHealthPerSecond()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 10, 0L);

            PlayerHealthOperations.AdvanceToTick(health, 540L);

            Assert.That(health.CurrentHealth, Is.EqualTo(92));
            Assert.That(health.NextRegenerationTick, Is.EqualTo(570L));
        }

        [Test]
        public void DamageResetsTheEntireRegenerationDelay()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 10, 0L);
            PlayerHealthOperations.AdvanceToTick(health, 510L);

            PlayerHealthOperations.ApplyDamage(health, 1, 510L);
            PlayerHealthOperations.AdvanceToTick(health, 1019L);

            Assert.That(health.CurrentHealth, Is.EqualTo(90));
            Assert.That(health.NextRegenerationTick, Is.EqualTo(1020L));

            PlayerHealthOperations.AdvanceToTick(health, 1020L);

            Assert.That(health.CurrentHealth, Is.EqualTo(91));
        }

        [Test]
        public void AdvanceCatchesUpEveryDueRegenerationInterval()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 50, 0L);

            PlayerHealthChangeResult result =
                PlayerHealthOperations.AdvanceToTick(health, 600L);

            Assert.That(result.AppliedDelta, Is.EqualTo(4));
            Assert.That(health.CurrentHealth, Is.EqualTo(54));
            Assert.That(health.NextRegenerationTick, Is.EqualTo(630L));
        }

        [Test]
        public void RegenerationStopsAndClearsScheduleAtMaximum()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 2, 0L);

            PlayerHealthOperations.AdvanceToTick(health, 1000L);

            Assert.That(health.CurrentHealth, Is.EqualTo(100));
            Assert.That(health.NextRegenerationTick, Is.Null);
        }

        [Test]
        public void TickCannotMoveBackwards()
        {
            var health = CreateFull();
            PlayerHealthOperations.AdvanceToTick(health, 20L);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.AdvanceToTick(
                    health,
                    19L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.ApplyDamage(
                    health,
                    1,
                    19L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.Heal(
                    health,
                    1,
                    19L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.Respawn(
                    health,
                    19L));
        }

        [Test]
        public void DirectAndFragmentedAdvancementProduceSameState()
        {
            var direct = CreateFull();
            var fragmented = CreateFull();
            PlayerHealthOperations.ApplyDamage(direct, 50, 0L);
            PlayerHealthOperations.ApplyDamage(fragmented, 50, 0L);

            PlayerHealthOperations.AdvanceToTick(direct, 600L);

            long[] ticks = { 100L, 300L, 510L, 540L, 570L, 600L };
            for (int index = 0; index < ticks.Length; index++)
            {
                PlayerHealthOperations.AdvanceToTick(
                    fragmented,
                    ticks[index]);
            }

            Assert.That(
                fragmented.CurrentHealth,
                Is.EqualTo(direct.CurrentHealth));
            Assert.That(
                fragmented.CurrentTick,
                Is.EqualTo(direct.CurrentTick));
            Assert.That(
                fragmented.NextRegenerationTick,
                Is.EqualTo(direct.NextRegenerationTick));
        }

        [Test]
        public void InvalidAmountsAndNullStatesAreRejected()
        {
            var health = CreateFull();

            Assert.Throws<ArgumentNullException>(
                () => PlayerHealthOperations.AdvanceToTick(
                    null,
                    0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.ApplyDamage(
                    health,
                    0,
                    0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.ApplyDamage(
                    health,
                    -1,
                    0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerHealthOperations.Heal(
                    health,
                    0,
                    0L));
        }

        [Test]
        public void DamageScheduleOverflowIsAtomicAndOnlyNeededForSurvivors()
        {
            var surviving = CreateFull();

            Assert.Throws<OverflowException>(() =>
                PlayerHealthOperations.ApplyDamage(
                    surviving,
                    1,
                    long.MaxValue));

            Assert.That(surviving.CurrentHealth, Is.EqualTo(100));
            Assert.That(surviving.CurrentTick, Is.Zero);
            Assert.That(
                surviving.NextRegenerationTick,
                Is.Null);

            var lethal = CreateFull();

            PlayerHealthChangeResult lethalResult =
                PlayerHealthOperations.ApplyDamage(
                    lethal,
                    100,
                    long.MaxValue);

            Assert.That(lethalResult.Died, Is.True);
            Assert.That(lethal.CurrentHealth, Is.Zero);
            Assert.That(lethal.CurrentTick, Is.EqualTo(long.MaxValue));
            Assert.That(lethal.NextRegenerationTick, Is.Null);

            PlayerHealthChangeResult deadResult =
                PlayerHealthOperations.ApplyDamage(
                    lethal,
                    1,
                    long.MaxValue);

            Assert.That(deadResult.Changed, Is.False);
            Assert.That(lethal.CurrentHealth, Is.Zero);
            Assert.That(lethal.CurrentTick, Is.EqualTo(long.MaxValue));
            Assert.That(lethal.NextRegenerationTick, Is.Null);
        }

        [Test]
        public void DeadPlayerNeverRegenerates()
        {
            var health = CreateFull();
            PlayerHealthOperations.ApplyDamage(health, 100, 0L);

            PlayerHealthOperations.AdvanceToTick(health, 100000L);

            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.NextRegenerationTick, Is.Null);
            Assert.That(health.IsDead, Is.True);
        }

        private static PlayerHealthState CreateFull()
        {
            return new PlayerHealthState(
                PlayerHealthRules.DefaultMaximumHealth);
        }
    }
}
