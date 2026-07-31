using System.Linq;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class DebugResourceInteractionTests
    {
        private static readonly WorldPosition StartPosition =
            new WorldPosition(4.5, 4.5);

        [Test]
        public void RebuildCreatesExpectedNumberOfMarkers()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                Assert.That(adapter.Resources, Has.Count.EqualTo(3));
                Assert.That(adapter.MarkerCount, Is.EqualTo(3));
                Assert.That(adapter.RenderedMarkerCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExactlyOneTargetIndicatorIsActive()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                adapter.SimulateTick(StartPosition);

                Assert.That(adapter.CurrentTargetId, Is.Not.Null);
                Assert.That(adapter.ActiveTargetIndicatorCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HarvestedResourceIsNoLongerRendered()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                adapter.QueueInteraction();
                adapter.SimulateTick(StartPosition);

                Assert.That(adapter.LastInteractionResult.Value.Succeeded, Is.True);
                Assert.That(adapter.RenderedMarkerCount, Is.EqualTo(2));
                Assert.That(
                    adapter.Resources.Count(resource =>
                        resource.Availability == ResourceAvailability.Harvested),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TwoRebuildsDoNotDuplicateGeneratedHierarchy()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                adapter.Rebuild();
                adapter.Rebuild();

                Assert.That(
                    root.transform.Cast<Transform>()
                        .Count(child => child.name == "Debug Resource Markers"),
                    Is.EqualTo(1));
                Assert.That(adapter.MarkerCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyingAdapterCleansGeneratedObjectsAndAssets()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);
            GameObject generatedRoot = adapter.GeneratedRoot;
            Sprite bodySprite = adapter.GeneratedBodySprite;
            Sprite indicatorSprite = adapter.GeneratedIndicatorSprite;

            Object.DestroyImmediate(root);

            Assert.That(generatedRoot == null, Is.True);
            Assert.That(bodySprite == null, Is.True);
            Assert.That(indicatorSprite == null, Is.True);
        }

        [Test]
        public void TestHookQueuesInteractionWithoutPhysicalKeyboard()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                adapter.QueueInteraction();

                Assert.That(adapter.LastInteractionResult, Is.Null);
                Assert.That(
                    adapter.Resources.All(resource =>
                        resource.Availability == ResourceAvailability.Available),
                    Is.True);

                adapter.SimulateTick(StartPosition);

                Assert.That(adapter.LastInteractionResult, Is.Not.Null);
                Assert.That(adapter.LastInteractionResult.Value.Succeeded, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateAdapter(
            out DebugResourceInteraction adapter)
        {
            var root = new GameObject("Debug resource interaction test");
            root.AddComponent<DebugIsometricWorld>();
            adapter = root.AddComponent<DebugResourceInteraction>();
            adapter.Rebuild();
            return root;
        }
    }
}
