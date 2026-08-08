using System.Collections;
using AgeOfSurvival.Core.Crafting;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    public sealed class CraftingPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameplayBuildsCraftUiAndFixedTickAdapterAdvancesCraft()
        {
            yield return SceneManager.LoadSceneAsync(FrontendSceneNames.Gameplay, LoadSceneMode.Single);
            InventoryPrototypeUiBehaviour inventoryUi = null;
            DebugResourceInteraction resources = null;
            float timeout = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < timeout)
            {
                inventoryUi = Object.FindFirstObjectByType<InventoryPrototypeUiBehaviour>();
                resources = Object.FindFirstObjectByType<DebugResourceInteraction>();
                if (inventoryUi != null && inventoryUi.Ui != null && resources != null && resources.PrototypeSession != null) break;
                yield return null;
            }

            Assert.That(inventoryUi, Is.Not.Null);
            Assert.That(inventoryUi.Ui, Is.Not.Null);
            Assert.That(resources, Is.Not.Null);
            Assert.That(inventoryUi.Ui.CraftList.itemsSource, Has.Count.EqualTo(3));
            Assert.That(inventoryUi.Ui.IsPanelOpen, Is.False);
            Assert.That(inventoryUi.Ui.IsCraftPanelOpen, Is.False);

            inventoryUi.Ui.TogglePanel();
            Assert.That(inventoryUi.Ui.IsPanelOpen, Is.True);
            Assert.That(inventoryUi.Ui.IsCraftPanelOpen, Is.False);

            inventoryUi.Ui.ToggleCraftPanel();
            Assert.That(inventoryUi.Ui.IsPanelOpen, Is.False);
            Assert.That(inventoryUi.Ui.IsCraftPanelOpen, Is.True);

            InventoryPrototypeSession session = resources.PrototypeSession;
            int before = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches.Id);

            inventoryUi.Ui.SelectCraftForTests(
                CraftPrototypeCatalog.KindlingRecipeId);

            Assert.That(
                inventoryUi.Ui.CraftButton.enabledSelf,
                Is.True);
            Assert.That(
                inventoryUi.Ui.ClickCraftForTests(),
                Is.True);

            Assert.That(session.CraftAction, Is.Not.Null);
            Assert.That(
                session.CraftAction.RecipeId,
                Is.EqualTo(CraftPrototypeCatalog.KindlingRecipeId));
            Assert.That(
                session.CraftAction.Status,
                Is.EqualTo(CraftActionStatus.Active));
            Assert.That(
                inventoryUi.Ui.CraftButton.enabledSelf,
                Is.False);

            inventoryUi.Ui.ToggleCraftPanel();
            Assert.That(inventoryUi.Ui.IsCraftPanelOpen, Is.False);
            Assert.That(
                session.CraftAction.Status,
                Is.EqualTo(CraftActionStatus.Active));

            inventoryUi.Ui.ToggleCraftPanel();
            Assert.That(inventoryUi.Ui.IsCraftPanelOpen, Is.True);

            long start = session.CraftAction.StartTick;
            while (session.CurrentTick - start < 90)
            {
                resources.SimulateTick(
                    session.CurrentPlayerPosition,
                    false);
            }

            inventoryUi.Ui.Refresh();

            Assert.That(
                session.CraftAction.Status,
                Is.EqualTo(CraftActionStatus.Completed));
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(before - 2));
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Kindling.Id),
                Is.EqualTo(1));
            StringAssert.Contains(
                "Craft completed:",
                inventoryUi.Ui.CraftStatusText);
        }
    }
}
