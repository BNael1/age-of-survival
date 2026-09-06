using System.Collections;
using System.IO;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Shelter;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Shelter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    /// <summary>1280x720 visual evidence for the prototype loop; images are review artifacts, not assets.</summary>
    public sealed class Lot7LePresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator CapturesPlayableRoofDoorShelterRestSleepAndSaveLoad()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/Review/Lot7LE"));
            Directory.CreateDirectory(directory);
            Screen.SetResolution(1280, 720, false);
            InventoryPrototypeSessionProvider.ResetForNewGame();
            ConstructionRuntimeSessionProvider.ResetForNewGame();
            yield return SceneManager.LoadSceneAsync(FrontendSceneNames.Gameplay, LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<DebugPlayerController>().enabled = false;
            var construction = Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>();
            Assert.That(construction, Is.Not.Null);
            Object.FindFirstObjectByType<GroundAnchorCameraFollow>().enabled = false;
            var world = Object.FindFirstObjectByType<DebugIsometricWorld>();
            Assert.That(world.TryMapConstructionSpaceCenter(
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0, 0)), out Vector3 center), Is.True);
            Camera.main.transform.position = new Vector3(center.x, center.y, Camera.main.transform.position.z);
            ConstructionRuntimeSession session = construction.Session;
            InventoryPrototypeSession inventory = InventoryPrototypeSessionProvider.Current;
            PlayableShelterRuntime shelter = ShelterRuntimeSessionProvider.Current;
            WorldPosition inside = new WorldPosition(0.1d, 0.1d);
            ClearStackables(inventory);

            construction.OpenMode();
            session.Execute(ConstructionCommand.Select(ConstructionPrototypeCatalog.RoofId));
            Assert.That(construction.PreviewAtScreenPositionForTests(new Vector2(Screen.width * .5f, Screen.height * .5f)), Is.True);
            yield return Capture(directory, "01-roof-ghost.png");
            construction.CloseMode();
            session.Execute(ConstructionCommand.Open());
            ConstructionInstanceId roof = Build(session, inventory, ConstructionPrototypeCatalog.RoofId,
                ConstructionSpaceKey.Roof(new WorldCellCoordinate(0, 0)), 0L);
            construction.WorldPresenter.Refresh();
            yield return Capture(directory, "02-roof-complete.png");

            ConstructionInstanceId floor = Build(session, inventory, ConstructionPrototypeCatalog.FloorId,
                ConstructionSpaceKey.Surface(new WorldCellCoordinate(0, 0)), 250L);
            ConstructionInstanceId door = Build(session, inventory, ConstructionPrototypeCatalog.DoorId,
                ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), ConstructionCellSide.East), 500L);
            foreach (ConstructionCellSide side in new[] { ConstructionCellSide.North, ConstructionCellSide.South, ConstructionCellSide.West })
                Build(session, inventory, ConstructionPrototypeCatalog.WallId,
                    ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), side), 750L + (long)side * 250L);
            construction.WorldPresenter.Refresh();
            Assert.That(session.Doors.TryGet(door, out ConstructionDoorState closed), Is.True);
            Assert.That(closed.IsOpen, Is.False);
            yield return Capture(directory, "03-door-closed.png");

            Advance(inventory, shelter, inside, 1);
            Assert.That(shelter.Session.HasCurrentShelter, Is.True);
            yield return Capture(directory, "04-shelter-recognized.png");
            yield return Capture(directory, "05-shelter-ui.png");

            Assert.That(shelter.TryStart(ShelterRestSleepKind.Rest), Is.True);
            yield return Capture(directory, "06-rest-active.png");
            Advance(inventory, shelter, inside, (int)ShelterPrototypeProfile.RestTicks);
            Assert.That(shelter.Action.Status, Is.EqualTo(ShelterRestSleepStatus.Completed));

            Assert.That(shelter.TryStart(ShelterRestSleepKind.Sleep), Is.True);
            yield return Capture(directory, "07-sleep-active.png");
            Advance(inventory, shelter, inside, (int)ShelterPrototypeProfile.SleepTicks);
            Assert.That(shelter.Action.Status, Is.EqualTo(ShelterRestSleepStatus.Completed));
            yield return Capture(directory, "08-familiarity-after-sleep.png");

            Assert.That(session.TryToggleDoor(door, inside), Is.True);
            construction.WorldPresenter.Refresh();
            Advance(inventory, shelter, inside, 1);
            Assert.That(shelter.Session.HasCurrentShelter, Is.False);
            yield return Capture(directory, "09-door-open.png");
            Assert.That(session.TryToggleDoor(door, inside), Is.True);
            Advance(inventory, shelter, inside, 1);
            Assert.That(shelter.Session.HasCurrentShelter, Is.True);
            byte[] saved = GameSaveBinaryCodec.Encode(inventory.CaptureGameSaveSnapshot(
                session.CaptureSaveSnapshot(), session.CaptureDoorSaveSnapshot()));
            GameSaveSnapshot loaded = GameSaveBinaryCodec.Decode(saved);
            Assert.That(loaded.Doors.States, Has.Count.EqualTo(1));
            Assert.That(loaded.Doors.States[0].IsOpen, Is.False);
            yield return Capture(directory, "10-save-load.png");
            Assert.That(floor.IsValid && roof.IsValid, Is.True);
            construction.CloseMode();
        }

        private static ConstructionInstanceId Build(ConstructionRuntimeSession session, InventoryPrototypeSession inventory,
            ConstructionDefinitionId definition, ConstructionSpaceKey space, long tick)
        {
            session.Execute(ConstructionCommand.Select(definition));
            ConstructionRuntimeResult placed = session.TryPlaceSelected(space);
            Assert.That(placed.Succeeded, Is.True);
            ConstructionDefinition core = session.Catalog.Require(definition).Core;
            for (int i = 0; i < core.Materials.Count; i++)
            {
                ConstructionMaterialRequirement requirement = core.Materials[i];
                Seed(inventory, inventory.Inventory.FindDefinition(requirement.DefinitionId), requirement.Quantity);
                Assert.That(session.DepositAvailable(placed.InstanceId, requirement.DefinitionId).Accepted,
                    Is.EqualTo(requirement.Quantity));
            }
            WorldPosition position = ConstructionRuntimeSession.SpacePosition(space);
            Assert.That(session.BeginWork(placed.InstanceId, tick, position).Succeeded, Is.True);
            Assert.That(session.AdvanceToTick(tick + 500L, position, false, true, true).Succeeded, Is.True);
            Assert.That(session.World.TryFindStructure(placed.InstanceId, out _), Is.True);
            return placed.InstanceId;
        }

        private static void Seed(InventoryPrototypeSession inventory, ItemDefinition definition, int quantity)
        {
            Assert.That(InventoryOperations.AddStack(inventory.MainContainer, definition, quantity).Accepted, Is.EqualTo(quantity));
        }

        private static void ClearStackables(InventoryPrototypeSession inventory)
        {
            var entries = new System.Collections.Generic.List<InventoryEntry>(inventory.MainContainer.Entries);
            for (int i = 0; i < entries.Count; i++)
            {
                ItemDefinition definition = inventory.Inventory.FindDefinition(entries[i].DefinitionId);
                if (definition != null && definition.StateKind == ItemStateKind.Stackable)
                    InventoryOperations.RemoveStack(inventory.MainContainer, definition, entries[i].Quantity);
            }
        }

        private static void Advance(InventoryPrototypeSession inventory, PlayableShelterRuntime shelter,
            WorldPosition position, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                long tick = inventory.BeginSimulationTick(position);
                shelter.AdvanceFixedTick(tick, position, false);
            }
        }

        private static IEnumerator Capture(string directory, string name)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(renderTexture);
            }
            yield return null;
        }
    }
}
