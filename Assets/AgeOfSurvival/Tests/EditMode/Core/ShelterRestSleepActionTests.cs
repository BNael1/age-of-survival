using AgeOfSurvival.Core.Shelter;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ShelterRestSleepActionTests
    {
        [TestCase(ShelterRestSleepKind.Rest, 180, 1, 0)]
        [TestCase(ShelterRestSleepKind.Sleep, 360, 8, 1)]
        public void CompletesOnceWithOnlyItsValidatedCredit(ShelterRestSleepKind kind, long duration, int points, int nights)
        {
            var id = new ShelterId("room-v1-sha256:test");
            ShelterRestSleepAction action = ShelterRestSleepOperations.Start(kind, id, 5, duration);
            int credits = 0;
            Assert.That(ShelterRestSleepOperations.Advance(action, 5 + duration - 1, id, true, false, _ => true,
                _ => credits++), Is.EqualTo(ShelterRestSleepReason.None));
            Assert.That(action.Status, Is.EqualTo(ShelterRestSleepStatus.Active));
            ShelterRestSleepOperations.Advance(action, 5 + duration, id, true, false, _ => true, _ => credits++);
            Assert.That(action.Status, Is.EqualTo(ShelterRestSleepStatus.Completed));
            Assert.That(credits, Is.EqualTo(1));
            Assert.That(ShelterRestSleepOperations.Advance(action, 999, id, true, false, _ => true, _ => credits++),
                Is.EqualTo(ShelterRestSleepReason.AlreadyFinalized));
            Assert.That(credits, Is.EqualTo(1));
            var history = new ShelterFamiliarityState(id);
            if (kind == ShelterRestSleepKind.Rest) history.RecordCompletedRest(); else history.RecordCompletedNight();
            Assert.That(history.FamiliarityHalfPoints, Is.EqualTo(points * 2));
            Assert.That(history.CompletedNightCount, Is.EqualTo(nights));
        }
        [TestCase(true, true, ShelterRestSleepReason.PlayerMoved)]
        [TestCase(false, false, ShelterRestSleepReason.ShelterChanged)]
        public void MovementOrShelterChangeCancelsWithoutCredit(bool moved, bool current, ShelterRestSleepReason expected)
        {
            var id = new ShelterId("room-v1-sha256:test");
            var action = ShelterRestSleepOperations.Start(ShelterRestSleepKind.Rest, id, 0, 1);
            int credits = 0;
            Assert.That(ShelterRestSleepOperations.Advance(action, 1, id, current, moved, _ => true, _ => credits++), Is.EqualTo(expected));
            Assert.That(action.Status, Is.EqualTo(ShelterRestSleepStatus.Interrupted)); Assert.That(credits, Is.Zero);
        }
        [Test]
        public void ComfortIsOneOnlyForValidShelter()
        {
            var policy = new BaselineShelterComfortPolicy(); var id = new ShelterId("room-v1-sha256:test");
            Assert.That(policy.Score(id, true), Is.EqualTo(1)); Assert.That(policy.Score(id, false), Is.Zero);
            Assert.That(policy.Score(default, true), Is.Zero);
        }
    }
}
