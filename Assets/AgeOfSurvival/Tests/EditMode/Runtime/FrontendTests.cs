using AgeOfSurvival.Runtime.Frontend;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class FrontendTests
    {
        [SetUp]
        public void SetUp()
        {
            GameplayInputGate.SetBlocked(false);
        }

        [TearDown]
        public void TearDown()
        {
            GameplayInputGate.SetBlocked(false);
        }

        [Test]
        public void GameplayInputGateBlocksAndReleases()
        {
            GameplayInputGate.SetBlocked(true);
            Assert.That(GameplayInputGate.IsBlocked, Is.True);

            GameplayInputGate.SetBlocked(false);
            Assert.That(GameplayInputGate.IsBlocked, Is.False);
        }

        [Test]
        public void ControllerStartsGameplaySceneAndBlocksInput()
        {
            var loader = new RecordingSceneLoader();
            var controller = CreateController(loader);

            bool started = controller.StartNewGame();

            Assert.That(started, Is.True);
            Assert.That(loader.LastScene, Is.EqualTo(FrontendSceneNames.Gameplay));
            Assert.That(GameplayInputGate.IsBlocked, Is.True);
        }

        [Test]
        public void ControllerRejectsASecondTransitionWhileBusy()
        {
            var loader = new RecordingSceneLoader { IsLoadingValue = true };
            var controller = CreateController(loader);

            bool started = controller.StartNewGame();

            Assert.That(started, Is.False);
            Assert.That(loader.CallCount, Is.Zero);
        }

        [Test]
        public void ControllerRestoresPreviousInputGateWhenTransitionCannotStart()
        {
            foreach (bool initiallyBlocked in new[] { false, true })
            {
                GameplayInputGate.SetBlocked(initiallyBlocked);
                var loader = new RecordingSceneLoader { ShouldStart = false };
                var controller = CreateController(loader);

                bool started = controller.StartNewGame();

                Assert.That(started, Is.False);
                Assert.That(loader.CallCount, Is.EqualTo(1));
                Assert.That(
                    GameplayInputGate.IsBlocked,
                    Is.EqualTo(initiallyBlocked));
            }
        }

        [Test]
        public void ControllerReturnsToMainMenu()
        {
            var loader = new RecordingSceneLoader();
            var controller = CreateController(loader);

            bool started = controller.ReturnToMainMenu();

            Assert.That(started, Is.True);
            Assert.That(loader.LastScene, Is.EqualTo(FrontendSceneNames.MainMenu));
            Assert.That(GameplayInputGate.IsBlocked, Is.True);
        }

        [Test]
        public void ControllerDoesNotLoadWithoutASave()
        {
            var loader = new RecordingSceneLoader();
            var controller = CreateController(loader);

            bool started = controller.LoadGame();

            Assert.That(started, Is.False);
            Assert.That(loader.CallCount, Is.Zero);
        }

        [Test]
        public void ControllerQuitsThroughInjectedAdapter()
        {
            var quitter = new RecordingQuitter();
            var controller = new FrontendController(
                new RecordingSceneLoader(),
                quitter,
                NoSaveAvailability.Instance,
                DeferredOnlineFrontendAvailability.Instance);

            controller.Quit();

            Assert.That(quitter.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void MainMenuShowsRequiredRoutesAndDisabledFutureActions()
        {
            var actions = new RecordingMainMenuActions();
            var ui = new MainMenuDocument(
                new VisualElement(),
                actions);

            Assert.That(ui.NewGameButton.text, Is.EqualTo("Nouvelle partie"));
            Assert.That(ui.LoadButton.text, Is.EqualTo("Charger"));
            Assert.That(ui.LoadButton.enabledSelf, Is.False);
            Assert.That(ui.OnlineButton.text, Is.EqualTo("En ligne"));
            Assert.That(ui.OptionsButton.text, Is.EqualTo("Options"));
            Assert.That(ui.QuitButton.text, Is.EqualTo("Quitter"));

            ui.ShowOnline();

            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.Online));
            Assert.That(ui.JoinButton.enabledSelf, Is.False);
            Assert.That(ui.HostButton.enabledSelf, Is.False);
            Assert.That(ui.FavoritesButton.enabledSelf, Is.False);
            Assert.That(ui.OnlineStatus.text, Is.Not.Empty);
        }

        [Test]
        public void MainMenuOptionsAndOnlineCanReturnHome()
        {
            var ui = new MainMenuDocument(
                new VisualElement(),
                new RecordingMainMenuActions());

            ui.ShowOptions();
            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.Options));

            ui.ShowHome();
            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.Home));

            ui.ShowOnline();
            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.Online));

            ui.ShowHome();
            Assert.That(ui.CurrentPanel, Is.EqualTo(MainMenuPanel.Home));
        }

        [Test]
        public void PauseMenuStartsHiddenAndContainsRequiredActions()
        {
            var ui = new PauseMenuDocument(
                new VisualElement(),
                new RecordingPauseActions());

            Assert.That(
                ui.Root.style.display.value,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(ui.ResumeButton.text, Is.EqualTo("Reprendre"));
            Assert.That(ui.OptionsButton.text, Is.EqualTo("Options"));
            Assert.That(
                ui.MainMenuButton.text,
                Is.EqualTo("Retour au menu principal"));
            Assert.That(ui.QuitButton.text, Is.EqualTo("Quitter"));

            ui.SetVisible(true);
            ui.ShowOptions();
            Assert.That(ui.CurrentPanel, Is.EqualTo(PauseMenuPanel.Options));

            ui.ShowPause();
            Assert.That(ui.CurrentPanel, Is.EqualTo(PauseMenuPanel.Pause));
        }

        private static FrontendController CreateController(
            RecordingSceneLoader loader)
        {
            return new FrontendController(
                loader,
                new RecordingQuitter(),
                NoSaveAvailability.Instance,
                DeferredOnlineFrontendAvailability.Instance);
        }

        private sealed class RecordingSceneLoader : IFrontendSceneLoader
        {
            public bool IsLoadingValue;
            public bool ShouldStart = true;
            public bool IsLoading => IsLoadingValue;
            public string LastScene { get; private set; }
            public int CallCount { get; private set; }

            public bool TryLoadScene(string sceneName)
            {
                if (IsLoading)
                {
                    return false;
                }

                LastScene = sceneName;
                CallCount++;
                if (!ShouldStart)
                {
                    return false;
                }

                IsLoadingValue = true;
                return true;
            }
        }

        private sealed class RecordingQuitter : IApplicationQuitter
        {
            public int CallCount { get; private set; }

            public void Quit()
            {
                CallCount++;
            }
        }

        private sealed class RecordingMainMenuActions
            : IMainMenuActions
        {
            public bool HasSave => false;
            public bool IsBusy => false;
            public IOnlineFrontendAvailability Online =>
                DeferredOnlineFrontendAvailability.Instance;

            public bool StartNewGame() => true;
            public bool LoadGame() => false;
            public void Quit()
            {
            }
        }

        private sealed class RecordingPauseActions
            : IPauseMenuActions
        {
            public bool IsBusy => false;
            public void Resume()
            {
            }

            public bool ReturnToMainMenu() => true;
            public void Quit()
            {
            }
        }
    }
}
