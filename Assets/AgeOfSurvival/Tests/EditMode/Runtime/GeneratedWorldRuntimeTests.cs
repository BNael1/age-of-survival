using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class GeneratedWorldRuntimeTests
    {
        [Test]
        public void GeneratedMode_RendersOneThirtyTwoByThirtyTwoPopulationChunk()
        {
            var root = new GameObject("Generated world runtime test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(0L, 0L));
                world.Rebuild();

                Assert.That(world.UsesGeneratedPopulation, Is.True);
                Assert.That(world.World.Bounds, Is.EqualTo(new GridBounds(32, 32)));
                Assert.That(world.PopulationChunk.Count, Is.EqualTo(1024));
                Assert.That(world.PopulationChunk.Resources.Count, Is.EqualTo(41));
                Assert.That(world.RenderedWorldOrigin, Is.EqualTo(new WorldCellCoordinate(0L, 0L)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GeneratedMode_MapsGrassDirtAndWaterToExistingPrototypeSprites()
        {
            var root = new GameObject("Generated terrain sprite mapping test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(0L, 0L));
                world.Rebuild();

                Assert.That(world.Tilemap.GetSprite(new Vector3Int(2, 0, 0)).name,
                    Is.EqualTo("Prototype Ground Grass"));
                Assert.That(world.Tilemap.GetSprite(new Vector3Int(0, 0, 0)).name,
                    Is.EqualTo("Prototype Ground Dirt"));
                var waterCell = new Vector3Int(0, 4, 0);
                Assert.That(world.Tilemap.GetSprite(waterCell).name,
                    Is.EqualTo("Prototype Ground Water"));
                Assert.That(
                    world.Tilemap.GetColor(waterCell),
                    Is.EqualTo((Color)new Color32(0, 64, 255, 255)));
                Assert.That(
                    world.Tilemap.GetColor(new Vector3Int(2, 0, 0)),
                    Is.EqualTo(Color.white));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GeneratedMode_ExposesStableResourceStatesAtCellCenters()
        {
            var root = new GameObject("Generated resource state test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(0L, 0L));
                world.Rebuild();

                IReadOnlyList<ResourceState> states = world.CreateGeneratedResourceStates();
                Assert.That(states.Count, Is.EqualTo(world.PopulationChunk.Resources.Count));
                CollectionAssert.AllItemsAreUnique(states.Select(state => state.Id).ToArray());

                GeneratedResourcePlacement placement = world.PopulationChunk.Resources[0];
                ResourceState state = states[0];
                Assert.That(state.Id, Is.EqualTo(placement.Id));
                Assert.That(state.Position.X, Is.EqualTo(placement.Cell.X));
                Assert.That(state.Position.Y, Is.EqualTo(placement.Cell.Y));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GeneratedMode_UsesDeterministicValidSpawnPosition()
        {
            var root = new GameObject("Generated spawn runtime test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(0L, 0L));
                world.Rebuild();

                Assert.That(world.TryGetGeneratedSpawnPosition(out WorldPosition spawn), Is.True);
                Assert.That(spawn.X, Is.EqualTo(16.0));
                Assert.That(spawn.Y, Is.EqualTo(16.0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LogicalProjectionSubtractsRenderedChunkOrigin()
        {
            var root = new GameObject("Generated chunk projection test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(2L, -1L));
                world.Rebuild();
                WorldCellCoordinate origin = world.RenderedWorldOrigin;
                Vector3 expected = world.Tilemap.GetCellCenterWorld(Vector3Int.zero);
                Vector3 actual = world.LogicalToWorldPosition(
                    new WorldPosition(origin.X, origin.Y),
                    0f,
                    expected.z);

                Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
                Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResourceAdapterBuildsMarkersFromGeneratedPopulation()
        {
            var root = new GameObject("Generated resource adapter test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(0L, 0L));
                world.Rebuild();
                DebugResourceInteraction interaction = root.AddComponent<DebugResourceInteraction>();
                interaction.Rebuild();

                Assert.That(interaction.Resources.Count, Is.EqualTo(41));
                Assert.That(interaction.MarkerCount, Is.EqualTo(41));
                Assert.That(interaction.RenderedMarkerCount, Is.EqualTo(41));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerAdapterStartsAtGeneratedSpawnInsteadOfLegacySerializedPosition()
        {
            var root = new GameObject("Generated player spawn test");

            try
            {
                DebugIsometricWorld world = root.AddComponent<DebugIsometricWorld>();
                world.ConfigureGeneratedPopulation(new WorldSeed(0UL), new ChunkCoordinate(0L, 0L));
                world.Rebuild();
                DebugResourceInteraction interaction = root.AddComponent<DebugResourceInteraction>();
                interaction.Rebuild();
                DebugPlayerController player = root.AddComponent<DebugPlayerController>();

                MethodInfo start = typeof(DebugPlayerController).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(start, Is.Not.Null);
                start.Invoke(player, null);

                Assert.That(player.Player.Position.X, Is.EqualTo(16.0));
                Assert.That(player.Player.Position.Y, Is.EqualTo(16.0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SessionRejectsDuplicateGeneratedResourceIdentifiers()
        {
            var id = new ResourceId("generated-test");
            var resources = new[]
            {
                new ResourceState(id, new WorldPosition(0.5, 0.5)),
                new ResourceState(id, new WorldPosition(1.5, 1.5))
            };

            Assert.Throws<ArgumentException>(() => new InventoryPrototypeSession(resources));
        }

        [Test]
        public void SampleSceneEnablesGeneratedPopulationWithoutChangingCameraCalibration()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/SampleScene.unity",
                OpenSceneMode.Additive);

            try
            {
                DebugIsometricWorld world = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DebugIsometricWorld>(true))
                    .Single();
                var serializedWorld = new SerializedObject(world);
                Camera camera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Single();

                Assert.That(serializedWorld.FindProperty("useGeneratedPopulation").boolValue, Is.True);
                Assert.That(serializedWorld.FindProperty("worldSeed").stringValue, Is.EqualTo("0"));
                Assert.That(camera.orthographicSize, Is.EqualTo(4.0625f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
