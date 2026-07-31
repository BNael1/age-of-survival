using System;
using AgeOfSurvival.Core.Simulation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class FixedTickClockTests
    {
        [Test]
        public void Advance_AccumulatesFractionalTimeWithoutLosingTicks()
        {
            var clock = new FixedTickClock(10, 8);
            int calls = 0;

            Assert.That(clock.Advance(0.05, () => calls++), Is.EqualTo(0));
            Assert.That(clock.Advance(0.05, () => calls++), Is.EqualTo(1));
            Assert.That(clock.Advance(0.25, () => calls++), Is.EqualTo(2));

            Assert.That(calls, Is.EqualTo(3));
            Assert.That(clock.TotalTicks, Is.EqualTo(3));
            Assert.That(clock.PendingSeconds, Is.EqualTo(0.05).Within(1e-10));
        }

        [Test]
        public void Advance_CapsWorkButPreservesBacklog()
        {
            var clock = new FixedTickClock(10, 2);
            int calls = 0;

            Assert.That(clock.Advance(0.5, () => calls++), Is.EqualTo(2));
            Assert.That(clock.Advance(0.0, () => calls++), Is.EqualTo(2));
            Assert.That(clock.Advance(0.0, () => calls++), Is.EqualTo(1));

            Assert.That(calls, Is.EqualTo(5));
            Assert.That(clock.PendingSeconds, Is.EqualTo(0.0).Within(1e-10));
        }

        [Test]
        public void Advance_RejectsInvalidArguments()
        {
            var clock = new FixedTickClock(10, 8);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-0.1, () => { }));
            Assert.Throws<ArgumentNullException>(() => clock.Advance(0.1, null));
        }
    }
}
