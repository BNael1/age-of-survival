using System.Collections;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    public sealed class FrontendPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenuBuildsOverDarkenedWorldAndBlocksGameplay()
        {
            yield return LoadScene(FrontendSceneNames.MainMenu);

            MainMenuBehaviour menu = null;
            yield return WaitUntil(() =>
            {
                menu = FindInActiveScene<MainMenuBehaviour>();
                return menu != null && menu.Ui != null;
            });

            Assert.That(GameplayInputGate.IsBlocked, Is.True);
            Assert.That(menu.Ui.LoadButton.enabledSelf, Is.False);
            Assert.That(menu.Ui.OnlineButton.enabledSelf, Is.True);

            InventoryPrototypeUiBehaviour inventory =
                FindInActiveScene<InventoryPrototypeUiBehaviour>();
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.enabled, Is.False);

            DebugResourceInteraction resources =
                FindInActiveScene<DebugResourceInteraction>();
            Assert.That(resources, Is.Not.Null);
            Assert.That(resources.PrototypeSession.CurrentTick, Is.Zero);
            yield return null;
            yield return null;
            Assert.That(resources.PrototypeSession.CurrentTick, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NewGameTransitionsFromMenuToGameplay()
        {
            yield return LoadScene(FrontendSceneNames.MainMenu);

            MainMenuBehaviour menu = null;
            yield return WaitUntil(() =>
            {
                menu = FindInActiveScene<MainMenuBehaviour>();
                return menu != null && menu.Ui != null;
            });

            Assert.That(menu.StartNewGame(), Is.True);

            yield return WaitUntil(() =>
                SceneManager.GetActiveScene().name
                    == FrontendSceneNames.Gameplay);

            PauseMenuBehaviour pause = null;
            yield return WaitUntil(() =>
            {
                pause = FindInActiveScene<PauseMenuBehaviour>();
                return pause != null && pause.Ui != null;
            });

            DebugIsometricWorld world =
                FindInActiveScene<DebugIsometricWorld>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.PopulationChunk, Is.Not.Null);
            Assert.That(
                world.PopulationChunk.Settings.Generation.Seed,
                Is.EqualTo(new WorldSeed(0)));

            Assert.That(GameplayInputGate.IsBlocked, Is.False);
            Assert.That(pause.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseMenuBlocksAndRestoresGameplayInput()
        {
            yield return LoadScene(FrontendSceneNames.Gameplay);

            PauseMenuBehaviour pause = null;
            yield return WaitUntil(() =>
            {
                pause = FindInActiveScene<PauseMenuBehaviour>();
                return pause != null && pause.Ui != null;
            });

            DebugResourceInteraction resources =
                FindInActiveScene<DebugResourceInteraction>();
            Assert.That(resources, Is.Not.Null);
            long pausedTick = resources.PrototypeSession.CurrentTick;

            pause.SetPaused(true);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(pause.IsPaused, Is.True);
            Assert.That(GameplayInputGate.IsBlocked, Is.True);
            Assert.That(
                pause.Ui.Root.style.display.value,
                Is.EqualTo(UnityEngine.UIElements.DisplayStyle.Flex));
            Assert.That(
                resources.PrototypeSession.CurrentTick,
                Is.EqualTo(pausedTick));

            pause.Resume();
            yield return WaitUntil(() =>
                resources.PrototypeSession.CurrentTick > pausedTick);

            Assert.That(pause.IsPaused, Is.False);
            Assert.That(GameplayInputGate.IsBlocked, Is.False);
            Assert.That(
                resources.PrototypeSession.CurrentTick,
                Is.GreaterThan(pausedTick));
        }

        [UnityTest]
        public IEnumerator PauseDiscardsQueuedInteractionBeforeResume()
        {
            yield return LoadScene(FrontendSceneNames.Gameplay);

            PauseMenuBehaviour pause = null;
            yield return WaitUntil(() =>
            {
                pause = FindInActiveScene<PauseMenuBehaviour>();
                return pause != null && pause.Ui != null;
            });

            DebugResourceInteraction resources =
                FindInActiveScene<DebugResourceInteraction>();
            Assert.That(resources, Is.Not.Null);

            resources.QueueInteraction();
            pause.SetPaused(true);
            yield return null;

            long pausedTick = resources.PrototypeSession.CurrentTick;
            Assert.That(resources.LastInteractionResult, Is.Null);

            pause.Resume();
            yield return WaitUntil(() =>
                resources.PrototypeSession.CurrentTick > pausedTick);

            Assert.That(resources.LastInteractionResult, Is.Null);
        }

        [UnityTest]
        public IEnumerator PauseMenuReturnsToMainMenu()
        {
            yield return LoadScene(FrontendSceneNames.Gameplay);

            PauseMenuBehaviour pause = null;
            yield return WaitUntil(() =>
            {
                pause = FindInActiveScene<PauseMenuBehaviour>();
                return pause != null && pause.Ui != null;
            });

            pause.SetPaused(true);
            Assert.That(pause.ReturnToMainMenu(), Is.True);

            yield return WaitUntil(() =>
                SceneManager.GetActiveScene().name
                    == FrontendSceneNames.MainMenu);

            MainMenuBehaviour menu = null;
            yield return WaitUntil(() =>
            {
                menu = FindInActiveScene<MainMenuBehaviour>();
                return menu != null && menu.Ui != null;
            });

            Assert.That(GameplayInputGate.IsBlocked, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseRequestedBeforeDocumentBuildBecomesVisible()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene testScene = SceneManager.CreateScene(
                "Frontend Pause Build Race");
            SceneManager.SetActiveScene(testScene);

            var host = new GameObject("Pause Menu Race Host");
            SceneManager.MoveGameObjectToScene(host, testScene);
            var pause = host.AddComponent<PauseMenuBehaviour>();

            pause.SetPaused(true);

            yield return WaitUntil(() => pause != null && pause.Ui != null);

            bool isPaused = pause.IsPaused;
            bool inputBlocked = GameplayInputGate.IsBlocked;
            UnityEngine.UIElements.DisplayStyle display =
                pause.Ui.Root.style.display.value;

            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }

            AsyncOperation unload =
                SceneManager.UnloadSceneAsync(testScene);
            Assert.That(unload, Is.Not.Null);
            while (!unload.isDone)
            {
                yield return null;
            }

            Assert.That(isPaused, Is.True);
            Assert.That(inputBlocked, Is.True);
            Assert.That(
                display,
                Is.EqualTo(UnityEngine.UIElements.DisplayStyle.Flex));
            Assert.That(GameplayInputGate.IsBlocked, Is.False);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator WaitUntil(
            System.Func<bool> predicate)
        {
            const int maximumFrames = 180;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Frontend condition was not reached within 180 frames.");
        }

        private static T FindInActiveScene<T>()
            where T : Component
        {
            Scene active = SceneManager.GetActiveScene();
            T[] found = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < found.Length; index++)
            {
                if (found[index].gameObject.scene == active)
                {
                    return found[index];
                }
            }

            return null;
        }
    }
}
