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
        private readonly ISavePauseMenuActions _saveActions;
        private readonly VisualElement _pausePanel;
        private readonly VisualElement _optionsPanel;

        public PauseMenuDocument(
            VisualElement root,
            IPauseMenuActions actions)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _actions = actions
                ?? throw new ArgumentNullException(nameof(actions));
            _saveActions = actions as ISavePauseMenuActions;

            FrontendStyles.ConfigureRoot(Root);
            FrontendStyles.CreateBackdrop(Root, 0.76f);
            VisualElement shell =
                FrontendStyles.CreateCenteredShell(Root, "pause-menu-shell");

            _pausePanel = FrontendStyles.CreatePanel("pause-menu-home");
            _pausePanel.Add(FrontendStyles.CreateSectionTitle("PAUSE"));
            ResumeButton = FrontendStyles.CreateMenuButton(
                "resume-button", "Reprendre", _actions.Resume);
            SaveButton = FrontendStyles.CreateMenuButton(
                "save-game-button", "Sauvegarder", RequestSave);
            OptionsButton = FrontendStyles.CreateMenuButton(
                "pause-options-button", "Options", ShowOptions);
            MainMenuButton = FrontendStyles.CreateMenuButton(
                "return-main-menu-button",
                _saveActions == null
                    ? "Retour au menu principal"
                    : "Sauvegarder et retourner",
                RequestMainMenu);
            QuitButton = FrontendStyles.CreateMenuButton(
                "pause-quit-button",
                _saveActions == null ? "Quitter" : "Sauvegarder et quitter",
                RequestQuit);
            QuitWithoutSavingButton = FrontendStyles.CreateMenuButton(
                "pause-quit-without-saving-button",
                "Quitter sans sauvegarder",
                RequestQuitWithoutSaving);
            QuitWithoutSavingButton.style.display = DisplayStyle.None;
            SaveStatusLabel = FrontendStyles.CreateMutedLabel(
                _saveActions?.SaveStatus ?? string.Empty);
            SaveStatusLabel.name = "save-status";

            _pausePanel.Add(ResumeButton);
            if (_saveActions != null) _pausePanel.Add(SaveButton);
            _pausePanel.Add(OptionsButton);
            _pausePanel.Add(MainMenuButton);
            _pausePanel.Add(QuitButton);
            if (_saveActions != null)
            {
                _pausePanel.Add(QuitWithoutSavingButton);
                _pausePanel.Add(SaveStatusLabel);
            }
            shell.Add(_pausePanel);

            _optionsPanel = FrontendStyles.CreatePanel("pause-menu-options");
            _optionsPanel.Add(FrontendStyles.CreateSectionTitle("OPTIONS"));
            _optionsPanel.Add(FrontendStyles.CreateMutedLabel(
                "Les réglages seront ajoutés dans un lot dédié."));
            OptionsBackButton = FrontendStyles.CreateMenuButton(
                "pause-options-back-button", "Retour", ShowPause);
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
        public Button SaveButton { get; }
        public Button OptionsButton { get; }
        public Button MainMenuButton { get; }
        public Button QuitButton { get; }
        public Button QuitWithoutSavingButton { get; }
        public Button OptionsBackButton { get; }
        public Label SaveStatusLabel { get; }

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

        public void SetSaveStatus(string text)
        {
            SaveStatusLabel.text = text ?? string.Empty;
        }

        public void SetQuitWithoutSavingVisible(bool visible)
        {
            QuitWithoutSavingButton.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SetBusy(bool busy)
        {
            bool enabled = !busy;
            ResumeButton.SetEnabled(enabled);
            SaveButton.SetEnabled(enabled);
            OptionsButton.SetEnabled(enabled);
            MainMenuButton.SetEnabled(enabled);
            QuitButton.SetEnabled(enabled);
            QuitWithoutSavingButton.SetEnabled(enabled);
            OptionsBackButton.SetEnabled(enabled);
        }

        private void RequestSave()
        {
            _saveActions?.SaveGame();
            SetSaveStatus(_saveActions?.SaveStatus);
        }

        private void RequestMainMenu()
        {
            if (_saveActions != null)
            {
                _saveActions.SaveAndReturnToMainMenu();
                SetBusy(true);
                return;
            }

            if (_actions.ReturnToMainMenu()) SetBusy(true);
        }

        private void RequestQuit()
        {
            if (_saveActions != null)
            {
                _saveActions.SaveAndQuit();
                SetBusy(true);
                return;
            }

            _actions.Quit();
        }

        private void RequestQuitWithoutSaving()
        {
            _saveActions?.QuitWithoutSaving();
            SetBusy(true);
        }
    }
}
