using AgeOfSurvival.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class DebugPlayerControllerTests
    {
        [TestCase(0f, 1f, 1f, 1f)]
        [TestCase(1f, 0f, 1f, -1f)]
        [TestCase(-1f, -1f, -2f, 0f)]
        [TestCase(0f, 0f, 0f, 0f)]
        public void ScreenDirectionMapsToIsometricWorldAxes(
            float screenX,
            float screenY,
            float expectedWorldX,
            float expectedWorldY)
        {
            Vector2 result = DebugPlayerController.ScreenToWorldDirection(
                new Vector2(screenX, screenY));

            Assert.That(result.x, Is.EqualTo(expectedWorldX));
            Assert.That(result.y, Is.EqualTo(expectedWorldY));
        }
    }
}
