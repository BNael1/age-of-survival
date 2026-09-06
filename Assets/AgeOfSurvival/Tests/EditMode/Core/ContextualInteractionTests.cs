using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ContextualInteractionTests
    {
        [TestCase(ContextualInteractionKind.Door, ContextualInteractionKind.GroundContainer)]
        [TestCase(ContextualInteractionKind.Door, ContextualInteractionKind.NaturalResource)]
        [TestCase(ContextualInteractionKind.GroundContainer, ContextualInteractionKind.NaturalResource)]
        public void ExactTieUsesExplicitPrototypePriority(ContextualInteractionKind expected, ContextualInteractionKind other)
        {
            var a = new ContextualInteractionCandidate(expected, "z", new WorldPosition(-1, 0));
            var b = new ContextualInteractionCandidate(other, "a", new WorldPosition(1, 0));
            foreach (var candidates in new[] { new[] { a, b }, new[] { b, a } })
            {
                Assert.That(ContextualInteractionResolver.TryResolve(candidates, default, 1, out var target), Is.True);
                Assert.That(target.Kind, Is.EqualTo(expected));
            }
        }
        [Test]
        public void DistancePrecedesKindAndOrdinalIdBreaksSameKindTie()
        {
            var door = new ContextualInteractionCandidate(ContextualInteractionKind.Door, "a", new WorldPosition(1, 0));
            var resource = new ContextualInteractionCandidate(ContextualInteractionKind.NaturalResource, "z", new WorldPosition(0.99, 0));
            Assert.That(ContextualInteractionResolver.TryResolve(new[] { door, resource }, default, 2, out var target), Is.True);
            Assert.That(target.Kind, Is.EqualTo(ContextualInteractionKind.NaturalResource));
            var other = new ContextualInteractionCandidate(ContextualInteractionKind.Door, "b", new WorldPosition(-1, 0));
            Assert.That(ContextualInteractionResolver.TryResolve(new[] { other, door }, default, 1, out target), Is.True);
            Assert.That(target.StableId, Is.EqualTo("a"));
            Assert.That(ContextualInteractionResolver.TryResolve(new[] { other, door }, default, 0.99, out _), Is.False);
        }
        [Test]
        public void InvalidRadiusAndCandidateRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ContextualInteractionResolver.TryResolve(
                Array.Empty<ContextualInteractionCandidate>(), default, double.NaN, out _));
            Assert.Throws<ArgumentException>(() => ContextualInteractionResolver.TryResolve(
                new[] { default(ContextualInteractionCandidate) }, default, 1, out _));
        }
    }
}
