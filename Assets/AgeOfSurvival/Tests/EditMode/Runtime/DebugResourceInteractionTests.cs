using System.Linq;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using AgeOfSurvival.Runtime.Inventory;
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
        public void PrototypeVisualsShowInteractionRadiusAndActiveTransferProgress()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                adapter.SimulateTick(StartPosition);

                Assert.That(adapter.UsesPrototypeVisuals, Is.True);
                Assert.That(adapter.InteractionRadiusVisible, Is.True);
                Assert.That(adapter.ActiveTransferProgressCount, Is.EqualTo(0));

                adapter.QueueInteraction();
                adapter.SimulateTick(StartPosition);

                Assert.That(adapter.ActiveTransferProgressCount, Is.EqualTo(1));
                Assert.That(adapter.RenderedGroundPileCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HarvestedResourceLeavesVisibleGroundYieldUntilTransferCompletes()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);

            try
            {
                adapter.QueueInteraction();
                adapter.SimulateTick(StartPosition);

                Assert.That(adapter.LastInteractionResult.Value.Succeeded, Is.True);
                Assert.That(adapter.RenderedMarkerCount, Is.EqualTo(3));
                Assert.That(adapter.GroundContainers, Has.Count.EqualTo(1));
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

        [Test]
        public void GroundMarkerRemainsForPartialRemainderThenHidesWhenEmpty()
        {
            GameObject root = CreateAdapter(out DebugResourceInteraction adapter);
            try
            {
                adapter.QueueInteraction();
                adapter.SimulateTick(StartPosition);
                for (int index = 0; index < 60; index++) adapter.SimulateTick(StartPosition);

                Assert.That(adapter.TransferAction.Status, Is.EqualTo(TransferActionStatus.Completed));
                Assert.That(adapter.RenderedMarkerCount, Is.EqualTo(3));
                GroundContainerState ground = adapter.GroundContainers[0];
                Assert.That(InventoryOperations.Count(ground.Container, InventoryPrototypeCatalog.Branches.Id), Is.EqualTo(3));

                InventorySelection branches = InventoryPrototypeViewModelBuilder.Build(adapter.PrototypeSession.Inventory)
                    .Main.Rows.Single(row => row.DisplayName == "Branches").Selection;
                adapter.PrototypeSession.Commands.Transfer(branches, adapter.PrototypeSession.BagContainer.Id);
                adapter.PrototypeSession.StartGroundTransfer(ground, 3, adapter.PrototypeSession.CurrentTick);
                for (int index = 0; index < 60; index++) adapter.SimulateTick(StartPosition);

                Assert.That(ground.IsEmpty, Is.True);
                Assert.That(adapter.RenderedMarkerCount, Is.EqualTo(2));
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
