using System.Collections;
using System.IO;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Persistence;
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
            Assert.That(
                menu.Ui.LoadButton.enabledSelf,
                Is.EqualTo(menu.HasSave));
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
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadScene(FrontendSceneNames.Gameplay);
            Assert.That(PrototypeSaveRuntime.HasCurrentSlot, Is.False);

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
        public IEnumerator GameplayBootstrapBindsResourceAdapterToCurrentSession()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadScene(
                FrontendSceneNames.Gameplay);

            DebugResourceInteraction resources = null;
            yield return WaitUntil(() =>
            {
                resources =
                    FindInActiveScene<DebugResourceInteraction>();
                return resources != null
                    && resources.PrototypeSession != null;
            });

            InventoryPrototypeSession current =
                InventoryPrototypeSessionProvider.Current;
            Assert.That(
                resources.PrototypeSession,
                Is.SameAs(current),
                "The scene adapter must use the session selected by the save bootstrap.");

            var expectedPosition =
                new WorldPosition(83.5d, -27.25d);
            long previousTick = current.CurrentTick;

            resources.SimulateTick(expectedPosition);

            Assert.That(
                current.CurrentTick,
                Is.EqualTo(previousTick + 1L));
            Assert.That(
                current.CurrentPlayerPosition,
                Is.EqualTo(expectedPosition));
        }

        [UnityTest]
        public IEnumerator SaveLoadRoundTripPreservesCompleteRuntimeState()
        {
            string rootDirectory = Path.Combine(
                Path.GetTempPath(),
                "age-of-survival-runtime-roundtrip-"
                    + System.Guid.NewGuid().ToString("N"));

            try
            {
                PrototypeSaveRuntime.BeginNewGame(default);
                yield return LoadScene(
                    FrontendSceneNames.Gameplay);

                DebugResourceInteraction resources = null;
                DebugIsometricWorld world = null;
                yield return WaitUntil(() =>
                {
                    resources =
                        FindInActiveScene<DebugResourceInteraction>();
                    world = FindInActiveScene<DebugIsometricWorld>();
                    return resources != null
                        && resources.PrototypeSession != null
                        && resources.Resources.Count > 0
                        && world != null
                        && world.PopulationChunk != null;
                });

                Assert.That(world.UsesChunkStreaming, Is.True);

                ResourceState target = null;
                for (int index = 0;
                    index < resources.Resources.Count;
                    index++)
                {
                    ResourceState candidate =
                        resources.Resources[index];
                    if (resources.TryGetVisualGroundAnchor(
                            candidate.Id,
                            out _))
                    {
                        target = candidate;
                        break;
                    }
                }

                Assert.That(target, Is.Not.Null);

                InventoryPrototypeSession session =
                    resources.PrototypeSession;
                long tick = session.BeginSimulationTick(
                    target.Position);
                ResourceYieldResult yield =
                    session.HarvestAndStartTransfer(
                        target.Position,
                        0d,
                        tick);
                Assert.That(yield.Succeeded, Is.True);

                session.AdvanceTransfer(
                    tick + 10000L,
                    target.Position,
                    false);
                Assert.That(
                    session.TransferAction.Status,
                    Is.Not.EqualTo(TransferActionStatus.Active));

                int expectedGroundQuantity =
                    InventoryOperations.Count(
                        yield.Ground.Container,
                        InventoryPrototypeCatalog.Branches.Id);
                int expectedCarriedQuantity =
                    InventoryOperations.Count(
                        session.MainContainer,
                        InventoryPrototypeCatalog.Branches.Id);
                Assert.That(
                    expectedGroundQuantity,
                    Is.GreaterThan(0));

                GameSaveSnapshot expectedSnapshot =
                    session.CaptureGameSaveSnapshot();
                byte[] expectedBytes =
                    GameSaveBinaryCodec.Encode(expectedSnapshot);
                Assert.That(
                    expectedSnapshot.ChunkMutations.Count,
                    Is.GreaterThan(0),
                    "The initial runtime window must own its active chunks before saving.");

                var service =
                    new PrototypeSaveService(rootDirectory);
                var slot = new SaveSlotId(1);
                service.Save(slot, session, 12d);

                CoordinatedGameLoadResult loaded = service.Load(
                    slot,
                    0d,
                    out double playedSeconds);
                InventoryPrototypeSession restored =
                    InventoryPrototypeSessionProvider.Install(
                        loaded.State);

                resources.Rebuild();

                Assert.That(
                    resources.PrototypeSession,
                    Is.SameAs(restored));
                Assert.That(restored.RestoredFromSave, Is.True);
                Assert.That(playedSeconds, Is.EqualTo(12d));
                Assert.That(
                    restored.CurrentPlayerPosition,
                    Is.EqualTo(target.Position));

                ResourceState restoredResource =
                    restored.FindResource(target.Id);
                GroundContainerState restoredGround =
                    restored.FindGround(yield.Ground.Container.Id);
                Assert.That(restoredResource, Is.Not.Null);
                Assert.That(
                    restoredResource.Availability,
                    Is.EqualTo(ResourceAvailability.Harvested));
                Assert.That(restoredGround, Is.Not.Null);
                Assert.That(
                    InventoryOperations.Count(
                        restoredGround.Container,
                        InventoryPrototypeCatalog.Branches.Id),
                    Is.EqualTo(expectedGroundQuantity));
                Assert.That(
                    InventoryOperations.Count(
                        restored.MainContainer,
                        InventoryPrototypeCatalog.Branches.Id),
                    Is.EqualTo(expectedCarriedQuantity));
                Assert.That(
                    resources.RenderedGroundPileCount,
                    Is.GreaterThan(0));
                Assert.That(
                    GameSaveBinaryCodec.Encode(
                        restored.CaptureGameSaveSnapshot()),
                    Is.EqualTo(expectedBytes));

                service.Save(slot, restored, 24d);
                CoordinatedGameLoadResult loadedAgain =
                    service.Load(slot, 0d, out _);
                InventoryPrototypeSession restoredAgain =
                    InventoryPrototypeSessionProvider.Install(
                        loadedAgain.State);
                resources.Rebuild();

                Assert.That(
                    GameSaveBinaryCodec.Encode(
                        restoredAgain.CaptureGameSaveSnapshot()),
                    Is.EqualTo(expectedBytes));
                Assert.That(
                    restoredAgain.FindResource(target.Id)
                        .Availability,
                    Is.EqualTo(ResourceAvailability.Harvested));
            }
            finally
            {
                InventoryPrototypeSessionProvider.ResetForNewGame();

                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, true);
                }
            }
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
