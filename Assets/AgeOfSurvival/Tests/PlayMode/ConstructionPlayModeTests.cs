using System.Collections;
using System;
using System.IO;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Persistence;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    public sealed class ConstructionPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameplayBootstrap_ProvidesPlayableConstructionFlow()
        {
            yield return SceneManager.LoadSceneAsync(
                FrontendSceneNames.Gameplay,
                LoadSceneMode.Single);

            ConstructionRuntimeBehaviour construction = null;
            DebugResourceInteraction resources = null;
            float timeout = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < timeout)
            {
                construction = Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>();
                resources = Object.FindFirstObjectByType<DebugResourceInteraction>();
                if (construction != null
                    && construction.Ui != null
                    && construction.Session != null
                    && resources != null
                    && resources.PrototypeSession != null)
                    break;
                yield return null;
            }

            Assert.That(construction, Is.Not.Null);
            Assert.That(construction.Ui, Is.Not.Null);
            Assert.That(resources, Is.Not.Null);
            Assert.That(PrototypeVisualAssets.AllConstructionTexturesExist(), Is.True);

            long tickBeforeMode = resources.PrototypeSession.CurrentTick;
            construction.OpenMode();
            yield return null;
            yield return null;
            Assert.That(construction.Session.Mode.IsActive, Is.True);
            Assert.That(GameplayInputGate.IsBlocked, Is.False,
                "Construction must not reuse the global gameplay gate, so ZQSD remains available.");
            Assert.That(resources.PrototypeSession.CurrentTick, Is.GreaterThan(tickBeforeMode),
                "The fixed simulation continues while Construction is open.");

            construction.Session.Execute(ConstructionCommand.Select(
                ConstructionPrototypeCatalog.FloorId));
            Assert.That(construction.PreviewAtScreenPositionForTests(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True);
            Assert.That(construction.Ghost.IsVisible, Is.True);
            Assert.That(construction.Session.Mode.Preview.Space.Kind,
                Is.EqualTo(ConstructionSpaceKind.Surface));

            ConstructionRuntimeResult placed = construction.ConfirmCurrentPreview();
            Assert.That(placed.Succeeded, Is.True);
            Assert.That(construction.Session.Mode.SelectedDefinitionId,
                Is.EqualTo(ConstructionPrototypeCatalog.FloorId));
            yield return null;
            Assert.That(construction.WorldPresenter.PresentedCount, Is.EqualTo(1));

            GroundAnchorSortCoordinator sortCoordinator = resources.SortCoordinator;
            DebugPlayerController player = Object.FindFirstObjectByType<DebugPlayerController>();
            Assert.That(player, Is.Not.Null);
            Assert.That(construction.WorldPresenter.TryGetRenderer(
                placed.InstanceId,
                out SpriteRenderer siteRenderer), Is.True);
            string siteStableId = ConstructionWorldPresenter.StableVisualId(placed.InstanceId);
            Assert.That(sortCoordinator.IsRegistered(siteStableId), Is.True);
            int registeredBeforeRebuild = sortCoordinator.RegisteredEntryCount;
            construction.WorldPresenter.Refresh();
            construction.WorldPresenter.Refresh();
            Assert.That(sortCoordinator.RegisteredEntryCount, Is.EqualTo(registeredBeforeRebuild),
                "Repeated presentation rebuilds must overwrite the same stable registration.");

            player.VisualGroundAnchor.position = new Vector3(
                player.VisualGroundAnchor.position.x,
                siteRenderer.transform.position.y + 1f,
                player.VisualGroundAnchor.position.z);
            sortCoordinator.ApplySortingForFrame(sortCoordinator.LastAppliedFrame + 1);
            Assert.That(player.VisualRenderer.sortingOrder, Is.LessThan(siteRenderer.sortingOrder),
                "A player behind the wall-height site must render behind it.");

            player.VisualGroundAnchor.position = new Vector3(
                player.VisualGroundAnchor.position.x,
                siteRenderer.transform.position.y - 1f,
                player.VisualGroundAnchor.position.z);
            sortCoordinator.ApplySortingForFrame(sortCoordinator.LastAppliedFrame + 1);
            Assert.That(player.VisualRenderer.sortingOrder, Is.GreaterThan(siteRenderer.sortingOrder),
                "A player in front of the wall-height site must render in front of it.");

            player.VisualGroundAnchor.position = new Vector3(
                player.VisualGroundAnchor.position.x,
                siteRenderer.transform.position.y,
                player.VisualGroundAnchor.position.z);
            sortCoordinator.ApplySortingForFrame(sortCoordinator.LastAppliedFrame + 1);
            Assert.That(player.VisualRenderer.sortingOrder, Is.GreaterThan(siteRenderer.sortingOrder),
                "Equal Y must use construction:<id> then player:local as a stable tie break.");

            int beforeUiClick = construction.Session.World.SiteCount;
            Assert.That(construction.IsPointerBlockedForTests(
                new Vector2(Screen.width - 30f, Screen.height - 30f)), Is.True);
            Assert.That(construction.Session.World.SiteCount, Is.EqualTo(beforeUiClick));

            construction.Session.Execute(ConstructionCommand.Select(
                ConstructionPrototypeCatalog.WallId));
            Assert.That(construction.PreviewAtScreenPositionForTests(
                new Vector2(Screen.width * 0.45f, Screen.height * 0.5f)), Is.True);
            Assert.That(construction.Session.Mode.Preview.Space.Kind,
                Is.EqualTo(ConstructionSpaceKind.Edge));

            ConstructionRuntimeResult wall = construction.ConfirmCurrentPreview();
            Assert.That(wall.Succeeded, Is.True);
            construction.WorldPresenter.Refresh();
            string wallStableId = ConstructionWorldPresenter.StableVisualId(wall.InstanceId);
            Assert.That(sortCoordinator.IsRegistered(wallStableId), Is.True);
            Assert.That(construction.Session.TryDismantle(wall.InstanceId).Succeeded, Is.True);
            construction.WorldPresenter.Refresh();
            Assert.That(sortCoordinator.IsRegistered(wallStableId), Is.False,
                "Dismantling must remove the central sorting registration immediately.");

            InventoryPrototypeSession inventory = resources.PrototypeSession;
            ClearStack(inventory.MainContainer, InventoryPrototypeCatalog.Branches);
            ClearStack(inventory.MainContainer, InventoryPrototypeCatalog.Stones);
            ClearStack(inventory.BagContainer, InventoryPrototypeCatalog.Branches);
            ClearStack(inventory.BagContainer, InventoryPrototypeCatalog.Stones);
            AddStack(inventory.BagContainer, InventoryPrototypeCatalog.Branches, 2);
            AddStack(inventory.BagContainer, InventoryPrototypeCatalog.Stones, 1);
            EquipBagIfNeeded(inventory);

            ConstructionRuntimeResult supplied = construction.Session.DepositAllAvailable(
                placed.InstanceId);
            Assert.That(supplied.Accepted, Is.EqualTo(3));
            Assert.That(InventoryOperations.Count(
                inventory.MainContainer,
                InventoryPrototypeCatalog.Branches.Id), Is.Zero);
            Assert.That(InventoryOperations.Count(
                inventory.BagContainer,
                InventoryPrototypeCatalog.Branches.Id), Is.Zero,
                "The floor materials must have been consumed from the equipped bag.");
            Assert.That(construction.Session.BeginWork(
                placed.InstanceId,
                inventory.CurrentTick,
                inventory.CurrentPlayerPosition).Succeeded, Is.True);
            for (int index = 0; index < 160; index++)
            {
                resources.SimulateTick(
                    inventory.CurrentPlayerPosition,
                    false);
                construction.Session.AdvanceToTick(
                    inventory.CurrentTick,
                    inventory.CurrentPlayerPosition,
                    false,
                    true,
                    true);
            }
            yield return null;
            Assert.That(construction.Session.World.SiteCount, Is.Zero);
            Assert.That(construction.Session.World.StructureCount, Is.EqualTo(1));
            Assert.That(construction.WorldPresenter.PresentedCount, Is.EqualTo(1));

            FillBelowBranchCapacity(inventory.MainContainer);
            FillBelowBranchCapacity(inventory.BagContainer);

            ConstructionRuntimeResult dismantled = construction.Session.TryDismantle(
                placed.InstanceId);
            Assert.That(dismantled.Succeeded, Is.True);
            Assert.That(dismantled.GroundOverflow, Is.EqualTo(1));
            Assert.That(construction.Session.World.OccupiedSpaceCount, Is.Zero);
            resources.SimulateTick(inventory.CurrentPlayerPosition, false);
            var overflowId = new GroundContainerId(
                "ground-construction-recovery-" + placed.InstanceId.Value);
            Assert.That(resources.TryGetStandaloneGroundVisual(
                overflowId,
                out GameObject overflowVisual), Is.True);
            Assert.That(overflowVisual.activeSelf, Is.True,
                "A full carried inventory must leave an interactive visible ground pile.");
            yield return null;
            Assert.That(construction.WorldPresenter.PresentedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RoofGhostCompletionAndFlatSortingUseRealRuntime()
        {
            InventoryPrototypeSessionProvider.ResetForNewGame();
            ConstructionRuntimeSessionProvider.ResetForNewGame();
            yield return SceneManager.LoadSceneAsync(FrontendSceneNames.Gameplay, LoadSceneMode.Single);
            yield return null;
            var runtime = Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>();
            var inventory = InventoryPrototypeSessionProvider.Current;
            runtime.OpenMode();
            runtime.Session.Execute(ConstructionCommand.Select(ConstructionPrototypeCatalog.RoofId));
            Assert.That(runtime.PreviewAtScreenPositionForTests(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True);
            Assert.That(runtime.Ghost.IsVisible, Is.True);
            Assert.That(runtime.Session.Mode.Preview.Space.Kind, Is.EqualTo(ConstructionSpaceKind.Roof));
            ConstructionRuntimeResult placed = runtime.ConfirmCurrentPreview();
            Assert.That(placed.Succeeded, Is.True);
            ClearStack(inventory.MainContainer, InventoryPrototypeCatalog.Branches);
            ClearStack(inventory.MainContainer, InventoryPrototypeCatalog.Wood);
            AddStack(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 3);
            AddStack(inventory.MainContainer, InventoryPrototypeCatalog.Wood, 1);
            Assert.That(runtime.Session.DepositAllAvailable(placed.InstanceId).Accepted, Is.EqualTo(4));
            Assert.That(runtime.Session.BeginWork(placed.InstanceId, inventory.CurrentTick,
                inventory.CurrentPlayerPosition).Succeeded, Is.True);
            runtime.Session.AdvanceToTick(inventory.CurrentTick + 200,
                inventory.CurrentPlayerPosition, false, true, true);
            runtime.WorldPresenter.Refresh();
            Assert.That(runtime.WorldPresenter.TryGetRenderer(placed.InstanceId, out SpriteRenderer roof), Is.True);
            Assert.That(roof.color.a, Is.LessThan(0.6f));
            Assert.That(roof.sortingOrder, Is.EqualTo(93));
            Assert.That(runtime.WorldPresenter.SortingRegistrationCount, Is.Zero);
            Assert.That(runtime.Session.TryDismantle(placed.InstanceId).Succeeded, Is.True);
            runtime.CloseMode();
        }

        [UnityTest]
        public IEnumerator DoorUsesQueuedContextualEAndChangesBothEdgeSprites()
        {
            InventoryPrototypeSessionProvider.ResetForNewGame();
            ConstructionRuntimeSessionProvider.ResetForNewGame();
            yield return SceneManager.LoadSceneAsync(FrontendSceneNames.Gameplay, LoadSceneMode.Single);
            yield return null;
            var runtime = Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>();
            var resources = Object.FindFirstObjectByType<DebugResourceInteraction>();
            Object.FindFirstObjectByType<DebugPlayerController>().enabled = false;
            var inventory = InventoryPrototypeSessionProvider.Current;
            runtime.OpenMode();
            foreach (ConstructionCellSide side in new[] { ConstructionCellSide.East, ConstructionCellSide.North })
            {
                runtime.Session.Execute(ConstructionCommand.Select(ConstructionPrototypeCatalog.DoorId));
                ConstructionSpaceKey space = ConstructionSpaceKey.Edge(new WorldCellCoordinate(0, 0), side);
                var position = ConstructionRuntimeSession.SpacePosition(space);
                var placed = runtime.Session.TryPlaceSelected(space);
                ClearStack(inventory.MainContainer, InventoryPrototypeCatalog.Branches);
                ClearStack(inventory.MainContainer, InventoryPrototypeCatalog.Wood);
                AddStack(inventory.MainContainer, InventoryPrototypeCatalog.Branches, 2);
                AddStack(inventory.MainContainer, InventoryPrototypeCatalog.Wood, 2);
                runtime.Session.DepositAllAvailable(placed.InstanceId);
                Assert.That(runtime.Session.BeginWork(placed.InstanceId, inventory.CurrentTick, position).Succeeded, Is.True);
                runtime.Session.AdvanceToTick(inventory.CurrentTick + 200, position, false, true, true);
                runtime.WorldPresenter.Refresh();
                Assert.That(runtime.WorldPresenter.TryGetRenderer(placed.InstanceId, out SpriteRenderer renderer), Is.True);
                Sprite closed = renderer.sprite;
                resources.QueueInteraction();
                resources.SimulateTick(position, false);
                Assert.That(runtime.Session.Doors.TryGet(placed.InstanceId, out ConstructionDoorState door), Is.True);
                Assert.That(door.IsOpen, Is.True);
                runtime.WorldPresenter.Refresh();
                Assert.That(renderer.sprite, Is.Not.SameAs(closed));
                resources.QueueInteraction();
                resources.SimulateTick(position, false);
                runtime.WorldPresenter.Refresh();
                Assert.That(renderer.sprite, Is.SameAs(closed));
                Assert.That(runtime.Session.TryDismantle(placed.InstanceId).Succeeded, Is.True);
            }
            runtime.CloseMode();
        }

        private static void EquipBagIfNeeded(InventoryPrototypeSession inventory)
        {
            if (inventory.Inventory.Equipment.Get(EquipmentSlot.Back)
                .Equals(inventory.Bag.InstanceId))
            {
                return;
            }

            var selection = new InventorySelection(
                inventory.MainContainer.Id,
                InventoryPrototypeCatalog.Bag.Id,
                inventory.Bag.InstanceId,
                true);
            Assert.That(inventory.Commands.Equip(selection, EquipmentSlot.Back).Succeeded, Is.True);
        }

        private static void ClearStack(ContainerState container, ItemDefinition definition)
        {
            int quantity = InventoryOperations.Count(container, definition.Id);
            if (quantity <= 0) return;
            Assert.That(InventoryOperations.RemoveStack(container, definition, quantity).Succeeded, Is.True);
        }

        private static void AddStack(
            ContainerState container,
            ItemDefinition definition,
            int quantity)
        {
            Assert.That(InventoryOperations.AddStack(container, definition, quantity).Accepted,
                Is.EqualTo(quantity));
        }

        private static void FillBelowBranchCapacity(ContainerState container)
        {
            long unit = InventoryPrototypeCatalog.Branches.UnitEncumbrance.Units;
            int fitting = (int)(container.RemainingCapacity.Units / unit);
            if (fitting > 0)
                AddStack(container, InventoryPrototypeCatalog.Branches, fitting);
            Assert.That(container.RemainingCapacity.Units, Is.LessThan(unit));
        }

        [UnityTest]
        public IEnumerator SaveLoad_RebuildsCanonicalConstructionPresentation()
        {
            InventoryPrototypeSessionProvider.ResetForNewGame();
            ConstructionRuntimeSessionProvider.ResetForNewGame();
            yield return SceneManager.LoadSceneAsync(
                FrontendSceneNames.Gameplay,
                LoadSceneMode.Single);

            ConstructionRuntimeBehaviour construction = null;
            float timeout = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < timeout)
            {
                construction = Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>();
                if (construction != null
                    && construction.Session != null
                    && construction.WorldPresenter != null)
                    break;
                yield return null;
            }
            Assert.That(construction, Is.Not.Null);

            construction.OpenMode();
            construction.Session.Execute(ConstructionCommand.Select(
                ConstructionPrototypeCatalog.FloorId));
            ConstructionSpaceKey space = ConstructionSpaceKey.Surface(
                new WorldCellCoordinate(23L, -17L));
            ConstructionRuntimeResult placed = construction.Session.TryPlaceSelected(space);
            Assert.That(placed.Succeeded, Is.True);
            construction.WorldPresenter.Refresh();
            Assert.That(construction.WorldPresenter.PresentedCount, Is.EqualTo(1));

            string directory = Path.Combine(
                Path.GetTempPath(),
                "aos-construction-playmode-" + Guid.NewGuid().ToString("N"));
            try
            {
                var service = new PrototypeSaveService(directory);
                var slot = new SaveSlotId(1);
                service.Save(
                    slot,
                    InventoryPrototypeSessionProvider.Current,
                    construction.Session,
                    0d);

                Assert.That(construction.Session.TryDismantle(placed.InstanceId).Succeeded,
                    Is.True);
                construction.WorldPresenter.Refresh();
                Assert.That(construction.WorldPresenter.PresentedCount, Is.Zero);

                CoordinatedGameLoadResult loaded = service.Load(slot, 0d, out _);
                PrototypeSaveRuntime.InstallRestoredState(loaded.State);
                yield return SceneManager.LoadSceneAsync(
                    FrontendSceneNames.Gameplay,
                    LoadSceneMode.Single);

                construction = null;
                timeout = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < timeout)
                {
                    construction = Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>();
                    if (construction != null
                        && construction.Session != null
                        && construction.WorldPresenter != null
                        && construction.WorldPresenter.PresentedCount == 1)
                        break;
                    yield return null;
                }

                Assert.That(construction, Is.Not.Null);
                Assert.That(construction.Session.World.TryFindSite(
                    placed.InstanceId,
                    out ConstructionSiteState restoredSite), Is.True);
                Assert.That(restoredSite.Space, Is.EqualTo(space));
                Assert.That(restoredSite.WorkCompletedUnits, Is.Zero);
                Assert.That(construction.Session.IsWorkActionActive, Is.False);
                Assert.That(construction.WorldPresenter.TryGetRenderer(
                    placed.InstanceId,
                    out SpriteRenderer renderer), Is.True);
                Assert.That(renderer.gameObject.activeSelf, Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                InventoryPrototypeSessionProvider.ResetForNewGame();
                ConstructionRuntimeSessionProvider.ResetForNewGame();
            }
        }

        [UnityTest]
        public IEnumerator MainMenu_DoesNotBootstrapConstructionRuntime()
        {
            yield return SceneManager.LoadSceneAsync(
                FrontendSceneNames.MainMenu,
                LoadSceneMode.Single);
            yield return null;

            Assert.That(
                Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>(),
                Is.Null);
        }
    }
}
