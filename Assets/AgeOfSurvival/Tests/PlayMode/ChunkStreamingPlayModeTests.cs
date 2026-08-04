using System.Collections;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    public sealed class ChunkStreamingPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameplayStreamsPreparedWindowAndPreservesResourceIdentity()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
            DebugIsometricWorld world = Object.FindFirstObjectByType<DebugIsometricWorld>();
            DebugResourceInteraction resources =
                Object.FindFirstObjectByType<DebugResourceInteraction>();
            DebugPlayerController player =
                Object.FindFirstObjectByType<DebugPlayerController>();
            Assert.That(world, Is.Not.Null);
            Assert.That(resources, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.Player, Is.Not.Null);
            Assert.That(world.UsesChunkStreaming, Is.True);
            Assert.That(world.VisibleChunkCount, Is.EqualTo(9));

            for (int frame = 0; frame < 60 && world.PendingPreparationCount > 0; frame++)
            {
                yield return null;
            }

            Assert.That(world.PreparedChunkCount, Is.EqualTo(25));
            Assert.That(world.PendingPreparationCount, Is.Zero);

            var initialIds = new HashSet<ResourceId>();
            for (int index = 0; index < resources.Resources.Count; index++)
            {
                Assert.That(initialIds.Add(resources.Resources[index].Id), Is.True);
            }

            WorldPosition continuitySample = player.Player.Position;
            Vector3 mappedBefore = world.LogicalToWorldPosition(
                continuitySample,
                0.18f,
                -0.1f);
            double targetX = 32.5;
            double distance = targetX - player.Player.Position.X;
            Assert.That(distance, Is.GreaterThan(0.0));
            PlayerMovement.Step(player.Player, 1.0, 0.0, distance, 1.0);
            Assert.That(
                world.SynchronizeStreaming(player.Player.Position),
                Is.True);
            Vector3 mappedAfter = world.LogicalToWorldPosition(
                continuitySample,
                0.18f,
                -0.1f);

            yield return null;

            Assert.That(world.StreamingCenter, Is.EqualTo(new ChunkCoordinate(1, 0)));
            Assert.That(world.VisibleChunkCount, Is.EqualTo(9));
            Assert.That(world.LastWindowSynchronousGenerationCount, Is.Zero);
            Assert.That(Vector3.Distance(mappedAfter, mappedBefore), Is.LessThan(0.0001f));
            Assert.That(resources.MarkerCount, Is.EqualTo(world.VisibleGeneratedResourceCount));

            var allIds = new HashSet<ResourceId>();
            for (int index = 0; index < resources.Resources.Count; index++)
            {
                Assert.That(allIds.Add(resources.Resources[index].Id), Is.True);
            }

            Assert.That(allIds.Count, Is.GreaterThanOrEqualTo(initialIds.Count));
            Assert.That(
                resources.SortCoordinator.SaturatedEntryCount,
                Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
