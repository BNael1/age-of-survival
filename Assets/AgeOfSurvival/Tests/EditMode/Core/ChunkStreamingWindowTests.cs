using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ChunkStreamingWindowTests
    {
        [Test]
        public void Defaults_AreThreeByThreeVisibleAndFiveByFivePrepared()
        {
            ChunkStreamingWindowSettings settings = ChunkStreamingWindowDefaults.Settings;

            Assert.That(settings.VisibleRadius, Is.EqualTo(1));
            Assert.That(settings.PreparationRadius, Is.EqualTo(2));
        }

        [Test]
        public void Settings_RejectsNegativeVisibleRadius()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ChunkStreamingWindowSettings(-1, 2));
        }

        [Test]
        public void Settings_RejectsPreparationSmallerThanVisible()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ChunkStreamingWindowSettings(2, 1));
        }

        [Test]
        public void Settings_RejectsRadiusAboveOperationalGuard()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ChunkStreamingWindowSettings(
                    ChunkStreamingWindowSettings.MaximumRadius,
                    ChunkStreamingWindowSettings.MaximumRadius + 1));
        }

        [Test]
        public void Planner_DefaultWindowContainsNineVisibleAndTwentyFivePreparedChunks()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(0, 0),
                ChunkStreamingWindowDefaults.Settings);

            Assert.That(plan.VisibleChunks.Count, Is.EqualTo(9));
            Assert.That(plan.PreparedChunks.Count, Is.EqualTo(25));
        }

        [Test]
        public void Planner_OrdersCenterThenChebyshevRings()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(0, 0),
                ChunkStreamingWindowDefaults.Settings);

            var expectedStart = new[]
            {
                new ChunkCoordinate(0, 0),
                new ChunkCoordinate(-1, -1),
                new ChunkCoordinate(0, -1),
                new ChunkCoordinate(1, -1),
                new ChunkCoordinate(-1, 0),
                new ChunkCoordinate(1, 0),
                new ChunkCoordinate(-1, 1),
                new ChunkCoordinate(0, 1),
                new ChunkCoordinate(1, 1)
            };

            for (int index = 0; index < expectedStart.Length; index++)
            {
                Assert.That(plan.PreparedChunks[index], Is.EqualTo(expectedStart[index]));
                Assert.That(plan.VisibleChunks[index], Is.EqualTo(expectedStart[index]));
            }
        }

        [Test]
        public void Planner_VisibleChunksArePreparedWithoutDuplicates()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(11, -9),
                ChunkStreamingWindowDefaults.Settings);
            var prepared = new HashSet<ChunkCoordinate>(plan.PreparedChunks);
            var visible = new HashSet<ChunkCoordinate>(plan.VisibleChunks);

            Assert.That(prepared.Count, Is.EqualTo(plan.PreparedChunks.Count));
            Assert.That(visible.Count, Is.EqualTo(plan.VisibleChunks.Count));
            foreach (ChunkCoordinate coordinate in plan.VisibleChunks)
            {
                Assert.That(prepared.Contains(coordinate), Is.True);
            }
        }

        [Test]
        public void Planner_NegativeCenterUsesStableCoordinates()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(-5, -7),
                ChunkStreamingWindowDefaults.Settings);

            Assert.That(plan.PreparedChunks[0], Is.EqualTo(new ChunkCoordinate(-5, -7)));
            Assert.That(plan.PreparedChunks, Does.Contain(new ChunkCoordinate(-7, -9)));
            Assert.That(plan.PreparedChunks, Does.Contain(new ChunkCoordinate(-3, -5)));
        }

        [Test]
        public void Planner_LongMaximumClipsOverflowingCoordinates()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(long.MaxValue, long.MaxValue),
                ChunkStreamingWindowDefaults.Settings);

            Assert.That(plan.VisibleChunks.Count, Is.EqualTo(4));
            Assert.That(plan.PreparedChunks.Count, Is.EqualTo(9));
            Assert.That(plan.PreparedChunks, Does.Contain(
                new ChunkCoordinate(long.MaxValue - 2, long.MaxValue - 2)));
        }

        [Test]
        public void Planner_LongMinimumClipsOverflowingCoordinates()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(long.MinValue, long.MinValue),
                ChunkStreamingWindowDefaults.Settings);

            Assert.That(plan.VisibleChunks.Count, Is.EqualTo(4));
            Assert.That(plan.PreparedChunks.Count, Is.EqualTo(9));
            Assert.That(plan.PreparedChunks, Does.Contain(
                new ChunkCoordinate(long.MinValue + 2, long.MinValue + 2)));
        }

        [Test]
        public void Transition_FromNothingAddsEveryChunkInPlanOrder()
        {
            ChunkStreamingWindowPlan next = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(2, 3),
                ChunkStreamingWindowDefaults.Settings);

            ChunkStreamingWindowTransition transition =
                ChunkStreamingWindowTransition.Create(null, next);

            CollectionAssert.AreEqual(next.PreparedChunks, transition.PreparedToAdd);
            CollectionAssert.AreEqual(next.VisibleChunks, transition.VisibleToShow);
            Assert.That(transition.PreparedToRemove.Count, Is.Zero);
            Assert.That(transition.VisibleToHide.Count, Is.Zero);
        }

        [Test]
        public void Transition_SameWindowIsEmpty()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(2, 3),
                ChunkStreamingWindowDefaults.Settings);

            ChunkStreamingWindowTransition transition =
                ChunkStreamingWindowTransition.Create(plan, plan);

            Assert.That(transition.PreparedToAdd.Count, Is.Zero);
            Assert.That(transition.PreparedToRemove.Count, Is.Zero);
            Assert.That(transition.VisibleToShow.Count, Is.Zero);
            Assert.That(transition.VisibleToHide.Count, Is.Zero);
        }

        [Test]
        public void Transition_OneChunkEastAddsAndRemovesExpectedColumns()
        {
            ChunkStreamingWindowSettings settings = ChunkStreamingWindowDefaults.Settings;
            ChunkStreamingWindowPlan previous = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(0, 0),
                settings);
            ChunkStreamingWindowPlan next = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(1, 0),
                settings);

            ChunkStreamingWindowTransition transition =
                ChunkStreamingWindowTransition.Create(previous, next);

            CollectionAssert.AreEqual(
                new[]
                {
                    new ChunkCoordinate(3, -2),
                    new ChunkCoordinate(3, -1),
                    new ChunkCoordinate(3, 0),
                    new ChunkCoordinate(3, 1),
                    new ChunkCoordinate(3, 2)
                },
                transition.PreparedToAdd);
            CollectionAssert.AreEqual(
                new[]
                {
                    new ChunkCoordinate(-2, -2),
                    new ChunkCoordinate(-2, -1),
                    new ChunkCoordinate(-2, 0),
                    new ChunkCoordinate(-2, 1),
                    new ChunkCoordinate(-2, 2)
                },
                transition.PreparedToRemove);
            Assert.That(transition.VisibleToShow.Count, Is.EqualTo(3));
            Assert.That(transition.VisibleToHide.Count, Is.EqualTo(3));
        }

        [Test]
        public void ExposedCollections_AreReadOnly()
        {
            ChunkStreamingWindowPlan plan = ChunkStreamingWindowPlanner.Create(
                new ChunkCoordinate(0, 0),
                ChunkStreamingWindowDefaults.Settings);
            var prepared = plan.PreparedChunks as IList<ChunkCoordinate>;

            Assert.That(prepared, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() =>
                prepared.Add(new ChunkCoordinate(99, 99)));
        }
    }
}
