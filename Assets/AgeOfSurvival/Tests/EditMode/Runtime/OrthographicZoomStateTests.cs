using AgeOfSurvival.Runtime.Rendering;
using NUnit.Framework;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class OrthographicZoomStateTests
    {
        private const float Initial = GroundAnchorCameraFollow.ProvisionalOrthographicSize;

        [Test]
        public void PositiveScrollDecreasesTargetAndNegativeScrollIncreasesTarget()
        {
            OrthographicZoomState zoomIn = CreateState();
            OrthographicZoomState zoomOut = CreateState();

            zoomIn.ApplyLogicalSteps(1f);
            zoomOut.ApplyLogicalSteps(-1f);

            Assert.That(zoomIn.TargetSize, Is.LessThan(Initial));
            Assert.That(zoomOut.TargetSize, Is.GreaterThan(Initial));
        }

        [Test]
        public void OneScrollStepUsesTenPercentMultiplicativeVariation()
        {
            OrthographicZoomState zoom = CreateState();

            zoom.ApplyLogicalSteps(1f);

            Assert.That(
                zoom.TargetSize,
                Is.EqualTo(Initial / 1.1f).Within(0.000001f));
        }

        [Test]
        public void RepeatedScrollStepsAccumulateFromPreviousTarget()
        {
            OrthographicZoomState combined = CreateState();
            OrthographicZoomState repeated = CreateState();

            combined.ApplyLogicalSteps(3f);
            repeated.ApplyLogicalSteps(1f);
            repeated.ApplyLogicalSteps(1f);
            repeated.ApplyLogicalSteps(1f);

            Assert.That(
                combined.TargetSize,
                Is.EqualTo(repeated.TargetSize).Within(0.000001f));
            Assert.That(
                combined.TargetSize,
                Is.EqualTo(Initial / (1.1f * 1.1f * 1.1f)).Within(0.000001f));
        }

        [Test]
        public void TargetClampsToNearAndFarLimits()
        {
            OrthographicZoomState zoom = CreateState();

            zoom.ApplyLogicalSteps(100f);
            Assert.That(
                zoom.TargetSize,
                Is.EqualTo(GroundAnchorCameraFollow.MinimumOrthographicSize));

            zoom.ApplyLogicalSteps(-200f);
            Assert.That(
                zoom.TargetSize,
                Is.EqualTo(GroundAnchorCameraFollow.MaximumOrthographicSize));
        }

        [Test]
        public void AdvanceConvergesProgressivelyWithoutImmediateJump()
        {
            OrthographicZoomState zoom = CreateState();
            zoom.ApplyLogicalSteps(-1f);
            float target = zoom.TargetSize;

            float first = zoom.Advance(1f / 60f);

            Assert.That(first, Is.GreaterThan(Initial));
            Assert.That(first, Is.LessThan(target));

            for (int frame = 0; frame < 180; frame++)
            {
                zoom.Advance(1f / 60f);
            }

            Assert.That(zoom.CurrentSize, Is.EqualTo(target).Within(0.0001f));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void AdvanceNeverOvershootsTargetOrConfiguredLimits(float scroll)
        {
            OrthographicZoomState zoom = CreateState();
            zoom.ApplyLogicalSteps(scroll);
            float target = zoom.TargetSize;
            float previous = zoom.CurrentSize;

            for (int frame = 0; frame < 240; frame++)
            {
                float current = zoom.Advance(1f / 120f);
                if (target < Initial)
                {
                    Assert.That(current, Is.LessThanOrEqualTo(previous));
                    Assert.That(current, Is.GreaterThanOrEqualTo(target));
                }
                else
                {
                    Assert.That(current, Is.GreaterThanOrEqualTo(previous));
                    Assert.That(current, Is.LessThanOrEqualTo(target));
                }

                Assert.That(
                    current,
                    Is.InRange(
                        GroundAnchorCameraFollow.MinimumOrthographicSize,
                        GroundAnchorCameraFollow.MaximumOrthographicSize));
                previous = current;
            }
        }

        [Test]
        public void SensitivityScalesMultiplicativeScrollExponent()
        {
            OrthographicZoomState zoom = CreateState();
            zoom.SetSensitivity(2f);

            zoom.ApplyLogicalSteps(1f);

            Assert.That(
                zoom.TargetSize,
                Is.EqualTo(Initial / (1.1f * 1.1f)).Within(0.000001f));
        }

        private static OrthographicZoomState CreateState()
        {
            return new OrthographicZoomState(
                Initial,
                GroundAnchorCameraFollow.MinimumOrthographicSize,
                GroundAnchorCameraFollow.MaximumOrthographicSize,
                GroundAnchorCameraFollow.DefaultZoomStepFraction,
                GroundAnchorCameraFollow.DefaultZoomSmoothTime,
                GroundAnchorCameraFollow.DefaultZoomSensitivity);
        }
    }
}
