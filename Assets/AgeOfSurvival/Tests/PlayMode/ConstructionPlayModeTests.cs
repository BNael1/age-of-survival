using System.Collections;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;
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
