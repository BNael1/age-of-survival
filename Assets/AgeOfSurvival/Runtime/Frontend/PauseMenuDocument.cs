using System;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    public enum PauseMenuPanel
    {
        Pause = 0,
        Options = 1
    }

    public sealed class PauseMenuDocument
    {
        private readonly IPauseMenuActions _actions;
        private readonly VisualElement _pausePanel;
        private readonly VisualElement _optionsPanel;

        public PauseMenuDocument(
            VisualElement root,
            IPauseMenuActions actions)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _actions = actions
                ?? throw new ArgumentNullException(nameof(actions));

            FrontendStyles.ConfigureRoot(Root);
            FrontendStyles.CreateBackdrop(Root, 0.76f);
            VisualElement shell =
                FrontendStyles.CreateCenteredShell(Root, "pause-menu-shell");

            _pausePanel = FrontendStyles.CreatePanel("pause-menu-home");
            _pausePanel.Add(FrontendStyles.CreateSectionTitle("PAUSE"));
            ResumeButton = FrontendStyles.CreateMenuButton(
                "resume-button",
                "Reprendre",
                _actions.Resume);
            OptionsButton = FrontendStyles.CreateMenuButton(
                "pause-options-button",
                "Options",
                ShowOptions);
            MainMenuButton = FrontendStyles.CreateMenuButton(
                "return-main-menu-button",
                "Retour au menu principal",
                RequestMainMenu);
            QuitButton = FrontendStyles.CreateMenuButton(
                "pause-quit-button",
                "Quitter",
                _actions.Quit);
            _pausePanel.Add(ResumeButton);
            _pausePanel.Add(OptionsButton);
            _pausePanel.Add(MainMenuButton);
            _pausePanel.Add(QuitButton);
            shell.Add(_pausePanel);

            _optionsPanel = FrontendStyles.CreatePanel("pause-menu-options");
            _optionsPanel.Add(FrontendStyles.CreateSectionTitle("OPTIONS"));
            _optionsPanel.Add(FrontendStyles.CreateMutedLabel(
                "Les réglages seront ajoutés dans un lot dédié."));
            OptionsBackButton = FrontendStyles.CreateMenuButton(
                "pause-options-back-button",
                "Retour",
                ShowPause);
            _optionsPanel.Add(OptionsBackButton);
            shell.Add(_optionsPanel);

            ShowPause();
            SetVisible(false);
        }

        public VisualElement Root { get; }
        public PauseMenuPanel CurrentPanel { get; private set; }
        public bool IsVisible =>
            Root.resolvedStyle.display != DisplayStyle.None
            && Root.style.display.value != DisplayStyle.None;
        public Button ResumeButton { get; }
        public Button OptionsButton { get; }
        public Button MainMenuButton { get; }
        public Button QuitButton { get; }
        public Button OptionsBackButton { get; }

        public void SetVisible(bool visible)
        {
            Root.style.display =
                visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowPause()
        {
            CurrentPanel = PauseMenuPanel.Pause;
            FrontendStyles.ShowOnly(
                _pausePanel,
                _pausePanel,
                _optionsPanel);
        }

        public void ShowOptions()
        {
            CurrentPanel = PauseMenuPanel.Options;
            FrontendStyles.ShowOnly(
                _optionsPanel,
                _pausePanel,
                _optionsPanel);
        }

        public void SetBusy(bool busy)
        {
            bool enabled = !busy;
            ResumeButton.SetEnabled(enabled);
            OptionsButton.SetEnabled(enabled);
            MainMenuButton.SetEnabled(enabled);
            QuitButton.SetEnabled(enabled);
            OptionsBackButton.SetEnabled(enabled);
        }

        private void RequestMainMenu()
        {
            if (_actions.ReturnToMainMenu())
            {
                SetBusy(true);
            }
        }
    }
}
