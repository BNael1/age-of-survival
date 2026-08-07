using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Player;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class PlayerHealthRuntimeTests
    {
        private static readonly WorldPosition Spawn =
            new WorldPosition(4.5, 4.5);
        private static readonly WorldPosition ZoneCenter =
            new WorldPosition(7.5, 4.5);

        [Test]
        public void DamageZoneRejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrototypeDamageZoneState(
                    ZoneCenter,
                    0.0,
                    25,
                    60L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrototypeDamageZoneState(
                    ZoneCenter,
                    1.0,
                    0,
                    60L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrototypeDamageZoneState(
                    ZoneCenter,
                    1.0,
                    25,
                    0L));
        }

        [Test]
        public void DamageZoneAppliesImmediatelyOnEntry()
        {
            var zone = CreateZone();

            Assert.That(
                zone.TryConsumeDamage(ZoneCenter, 12L),
                Is.True);
            Assert.That(
                zone.NextDamageTick,
                Is.EqualTo(72L));
        }

        [Test]
        public void DamageZoneWaitsForFixedInterval()
        {
            var zone = CreateZone();

            Assert.That(zone.TryConsumeDamage(ZoneCenter, 12L), Is.True);
            Assert.That(zone.TryConsumeDamage(ZoneCenter, 71L), Is.False);
            Assert.That(zone.TryConsumeDamage(ZoneCenter, 72L), Is.True);
            Assert.That(zone.NextDamageTick, Is.EqualTo(132L));
        }

        [Test]
        public void LeavingDamageZoneClearsSchedule()
        {
            var zone = CreateZone();
            var outside = new WorldPosition(20.0, 20.0);

            Assert.That(zone.TryConsumeDamage(ZoneCenter, 12L), Is.True);
            Assert.That(zone.TryConsumeDamage(outside, 13L), Is.False);
            Assert.That(zone.NextDamageTick, Is.Null);
            Assert.That(zone.TryConsumeDamage(ZoneCenter, 14L), Is.True);
        }

        [Test]
        public void RuntimeStepRejectsUnsynchronizedPosition()
        {
            var session = new InventoryPrototypeSession();
            var player = new PlayerState(ZoneCenter);
            var zone = CreateZone();

            Assert.Throws<InvalidOperationException>(
                () => PlayerHealthRuntimeStep.Step(
                    player,
                    session,
                    Spawn,
                    zone));
        }

        [Test]
        public void RuntimeStepAppliesZoneDamageOnCanonicalTick()
        {
            var session = new InventoryPrototypeSession();
            var player = new PlayerState(ZoneCenter);
            var zone = CreateZone();
            session.BeginSimulationTick(player.Position);

            PlayerHealthRuntimeResult result =
                PlayerHealthRuntimeStep.Step(
                    player,
                    session,
                    Spawn,
                    zone);

            Assert.That(result.DamageApplied, Is.EqualTo(25));
            Assert.That(result.Respawned, Is.False);
            Assert.That(session.Health.CurrentHealth, Is.EqualTo(75));
            Assert.That(player.Position, Is.EqualTo(ZoneCenter));
        }

        [Test]
        public void LethalRuntimeStepRespawnsAtomicallyWithoutInventoryLoss()
        {
            var session = new InventoryPrototypeSession();
            var player = new PlayerState(ZoneCenter);
            var zone = new PrototypeDamageZoneState(
                ZoneCenter,
                1.0,
                100,
                60L);
            int branchesBefore = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches.Id);
            session.BeginSimulationTick(player.Position);

            PlayerHealthRuntimeResult result =
                PlayerHealthRuntimeStep.Step(
                    player,
                    session,
                    Spawn,
                    zone);

            Assert.That(result.DamageApplied, Is.EqualTo(100));
            Assert.That(result.Respawned, Is.True);
            Assert.That(
                session.Health.CurrentHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(player.Position, Is.EqualTo(Spawn));
            Assert.That(session.CurrentPlayerPosition, Is.EqualTo(Spawn));
            Assert.That(zone.NextDamageTick, Is.Null);
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(branchesBefore));
        }

        [Test]
        public void HealthHudShowsNumericAndBarValues()
        {
            var session = new InventoryPrototypeSession();
            var root = new VisualElement();
            var hud = new PlayerHealthHudDocument(root, session);

            Assert.That(hud.HealthValue.text, Is.EqualTo("100 / 100"));
            Assert.That(hud.HealthBar.lowValue, Is.Zero);
            Assert.That(hud.HealthBar.highValue, Is.EqualTo(100f));
            Assert.That(hud.HealthBar.value, Is.EqualTo(100f));
            Assert.That(root.Q("player-health-hud"), Is.SameAs(hud.Panel));
        }

        [Test]
        public void HealthHudRefreshesFromCanonicalSession()
        {
            var session = new InventoryPrototypeSession();
            var root = new VisualElement();
            var hud = new PlayerHealthHudDocument(root, session);

            session.ApplyDamage(25);
            hud.Refresh();

            Assert.That(hud.HealthValue.text, Is.EqualTo("75 / 100"));
            Assert.That(hud.HealthBar.value, Is.EqualTo(75f));
        }


        [Test]
        public void DamageZoneBoundaryIsInclusiveAndOutsideIsSafe()
        {
            var zone = CreateZone();
            WorldPosition boundary =
                ZoneCenter.Translate(zone.Radius, 0.0);
            WorldPosition outside =
                ZoneCenter.Translate(zone.Radius + 0.0001, 0.0);

            Assert.That(zone.Contains(boundary), Is.True);
            Assert.That(zone.Contains(outside), Is.False);
            Assert.That(
                zone.TryConsumeDamage(outside, 12L),
                Is.False);
            Assert.That(zone.NextDamageTick, Is.Null);
        }

        [Test]
        public void RuntimeStepOutsideZoneLeavesHealthAndScheduleUntouched()
        {
            var session = new InventoryPrototypeSession();
            var outside = new WorldPosition(20.0, 20.0);
            var player = new PlayerState(outside);
            var zone = CreateZone();
            session.BeginSimulationTick(player.Position);

            PlayerHealthRuntimeResult result =
                PlayerHealthRuntimeStep.Step(
                    player,
                    session,
                    Spawn,
                    zone);

            Assert.That(result.DamageApplied, Is.Zero);
            Assert.That(result.Respawned, Is.False);
            Assert.That(
                session.Health.CurrentHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(player.Position, Is.EqualTo(outside));
            Assert.That(session.CurrentPlayerPosition, Is.EqualTo(outside));
            Assert.That(zone.NextDamageTick, Is.Null);
        }

        private static PrototypeDamageZoneState CreateZone()
        {
            return new PrototypeDamageZoneState(
                ZoneCenter,
                1.0,
                25,
                60L);
        }
    }
}
