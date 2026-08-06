using System;
using System.Collections.Generic;
using AgeOfSurvival.Runtime.Persistence;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    public enum MainMenuPanel
    {
        Home = 0,
        Online = 1,
        Options = 2,
        NewGameSlots = 3,
        LoadSlots = 4
    }

    public sealed class MainMenuDocument
    {
        private readonly IMainMenuActions _actions;
        private readonly ISaveMainMenuActions _saveActions;
        private readonly VisualElement _homePanel;
        private readonly VisualElement _onlinePanel;
        private readonly VisualElement _optionsPanel;
        private readonly VisualElement _newGamePanel;
        private readonly VisualElement _loadPanel;
        private readonly List<Button> _slotButtons = new List<Button>();
        private Label _newSlotStatus;
        private Label _loadSlotStatus;
        private int _overwriteConfirmationSlot;

        public MainMenuDocument(
            VisualElement root,
            IMainMenuActions actions)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _actions = actions
                ?? throw new ArgumentNullException(nameof(actions));
            _saveActions = actions as ISaveMainMenuActions;

            FrontendStyles.ConfigureRoot(Root);
            FrontendStyles.CreateBackdrop(Root, 0.68f);
            VisualElement shell =
                FrontendStyles.CreateLeftShell(Root, "main-menu-shell");
            shell.Add(FrontendStyles.CreateGameTitle());
            shell.Add(FrontendStyles.CreateMutedLabel("PROTOTYPE"));

            _homePanel = FrontendStyles.CreatePanel("main-menu-home");
            ContinueButton = FrontendStyles.CreateMenuButton(
                "continue-button",
                "Continuer",
                RequestContinue);
            NewGameButton = FrontendStyles.CreateMenuButton(
                "new-game-button",
                "Nouvelle partie",
                RequestNewGame);
            LoadButton = FrontendStyles.CreateMenuButton(
                "load-game-button",
                "Charger",
                RequestLoadGame);
            OnlineButton = FrontendStyles.CreateMenuButton(
                "online-button",
                "En ligne",
                ShowOnline);
            OptionsButton = FrontendStyles.CreateMenuButton(
                "options-button",
                "Options",
                ShowOptions);
            QuitButton = FrontendStyles.CreateMenuButton(
                "quit-button",
                "Quitter",
                _actions.Quit);

            ContinueButton.SetEnabled(_saveActions != null && _actions.HasSave);
            LoadButton.SetEnabled(_actions.HasSave);
            if (_saveActions != null) _homePanel.Add(ContinueButton);
            _homePanel.Add(NewGameButton);
            _homePanel.Add(LoadButton);
            _homePanel.Add(OnlineButton);
            _homePanel.Add(OptionsButton);
            HomeStatus = FrontendStyles.CreateMutedLabel(string.Empty);
            HomeStatus.name = "main-menu-save-status";
            _homePanel.Add(HomeStatus);
            _homePanel.Add(QuitButton);
            shell.Add(_homePanel);

            _newGamePanel = BuildSlotPanel(true);
            _loadPanel = BuildSlotPanel(false);
            _onlinePanel = BuildOnlinePanel();
            _optionsPanel = BuildOptionsPanel();
            shell.Add(_newGamePanel);
            shell.Add(_loadPanel);
            shell.Add(_onlinePanel);
            shell.Add(_optionsPanel);

            ShowHome();
        }

        public VisualElement Root { get; }
        public MainMenuPanel CurrentPanel { get; private set; }
        public Button ContinueButton { get; }
        public Button NewGameButton { get; }
        public Button LoadButton { get; }
        public Button OnlineButton { get; }
        public Button OptionsButton { get; }
        public Button QuitButton { get; }
        public Button JoinButton { get; private set; }
        public Button HostButton { get; private set; }
        public Button FavoritesButton { get; private set; }
        public Button OnlineBackButton { get; private set; }
        public Button OptionsBackButton { get; private set; }
        public Label OnlineStatus { get; private set; }
        public Label HomeStatus { get; }
        public Label SlotStatus => CurrentPanel == MainMenuPanel.LoadSlots
            ? _loadSlotStatus
            : _newSlotStatus;
        public IReadOnlyList<Button> SlotButtons => _slotButtons.AsReadOnly();

        public void ShowHome()
        {
            CurrentPanel = MainMenuPanel.Home;
            _overwriteConfirmationSlot = 0;
            FrontendStyles.ShowOnly(
                _homePanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel,
                _newGamePanel,
                _loadPanel);
        }

        public void ShowOnline()
        {
            CurrentPanel = MainMenuPanel.Online;
            FrontendStyles.ShowOnly(
                _onlinePanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel,
                _newGamePanel,
                _loadPanel);
        }

        public void ShowOptions()
        {
            CurrentPanel = MainMenuPanel.Options;
            FrontendStyles.ShowOnly(
                _optionsPanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel,
                _newGamePanel,
                _loadPanel);
        }

        public void ShowNewGameSlots()
        {
            CurrentPanel = MainMenuPanel.NewGameSlots;
            _overwriteConfirmationSlot = 0;
            SetSlotStatus("Choisissez une chronologie.");
            FrontendStyles.ShowOnly(
                _newGamePanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel,
                _newGamePanel,
                _loadPanel);
        }

        public void ShowLoadSlots()
        {
            CurrentPanel = MainMenuPanel.LoadSlots;
            _overwriteConfirmationSlot = 0;
            SetSlotStatus("Choisissez une partie à charger.");
            FrontendStyles.ShowOnly(
                _loadPanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel,
                _newGamePanel,
                _loadPanel);
        }

        public void SetBusy(bool busy)
        {
            bool enabled = !busy;
            ContinueButton.SetEnabled(enabled && _actions.HasSave);
            NewGameButton.SetEnabled(enabled);
            LoadButton.SetEnabled(enabled && _actions.HasSave);
            OnlineButton.SetEnabled(enabled);
            OptionsButton.SetEnabled(enabled);
            QuitButton.SetEnabled(enabled);
            OnlineBackButton.SetEnabled(enabled);
            OptionsBackButton.SetEnabled(enabled);
            for (int index = 0; index < _slotButtons.Count; index++)
            {
                _slotButtons[index].SetEnabled(enabled);
            }
        }

        private VisualElement BuildSlotPanel(bool newGame)
        {
            VisualElement panel = FrontendStyles.CreatePanel(
                newGame ? "new-game-slots" : "load-game-slots");
            panel.Add(FrontendStyles.CreateSectionTitle(
                newGame ? "NOUVELLE PARTIE" : "CHARGER"));

            IReadOnlyList<SaveSlotView> slots = _saveActions?.SaveSlots
                ?? Array.Empty<SaveSlotView>();
            for (int index = 0; index < SaveSlotPolicy.SlotCount; index++)
            {
                int slotIndex = index + 1;
                SaveSlotView view = index < slots.Count
                    ? slots[index]
                    : new SaveSlotView(new SaveSlotId(slotIndex), false, default, string.Empty);
                Button button = FrontendStyles.CreateMenuButton(
                    (newGame ? "new" : "load") + $"-slot-{slotIndex}",
                    view.Describe(),
                    () => RequestSlot(slotIndex, newGame));
                if (!newGame) button.SetEnabled(view.IsReadable);
                _slotButtons.Add(button);
                panel.Add(button);
            }

            Label status = FrontendStyles.CreateMutedLabel(string.Empty);
            status.name = newGame
                ? "new-game-slot-status"
                : "load-game-slot-status";
            if (newGame) _newSlotStatus = status;
            else _loadSlotStatus = status;
            panel.Add(status);
            panel.Add(FrontendStyles.CreateMenuButton(
                (newGame ? "new" : "load") + "-slots-back",
                "Retour",
                ShowHome));
            return panel;
        }

        private VisualElement BuildOnlinePanel()
        {
            VisualElement panel =
                FrontendStyles.CreatePanel("main-menu-online");
            panel.Add(FrontendStyles.CreateSectionTitle("EN LIGNE"));
            panel.Add(FrontendStyles.CreateMutedLabel(
                "Architecture réservée au futur client et au serveur dédié VPS."));
            JoinButton = FrontendStyles.CreateMenuButton(
                "join-server-button", "Rejoindre un serveur", () => { });
            HostButton = FrontendStyles.CreateMenuButton(
                "host-server-button", "Héberger une partie", () => { });
            FavoritesButton = FrontendStyles.CreateMenuButton(
                "favorites-button", "Serveurs favoris", () => { });
            OnlineBackButton = FrontendStyles.CreateMenuButton(
                "online-back-button", "Retour", ShowHome);
            JoinButton.SetEnabled(_actions.Online.CanJoin);
            HostButton.SetEnabled(_actions.Online.CanHost);
            FavoritesButton.SetEnabled(_actions.Online.CanUseFavorites);
            panel.Add(JoinButton);
            panel.Add(HostButton);
            panel.Add(FavoritesButton);
            OnlineStatus = FrontendStyles.CreateMutedLabel(
                _actions.Online.StatusText);
            OnlineStatus.name = "online-status";
            panel.Add(OnlineStatus);
            panel.Add(OnlineBackButton);
            return panel;
        }

        private VisualElement BuildOptionsPanel()
        {
            VisualElement panel =
                FrontendStyles.CreatePanel("main-menu-options");
            panel.Add(FrontendStyles.CreateSectionTitle("OPTIONS"));
            panel.Add(FrontendStyles.CreateMutedLabel(
                "Les réglages audio, vidéo et contrôles seront ajoutés dans un lot dédié."));
            OptionsBackButton = FrontendStyles.CreateMenuButton(
                "options-back-button", "Retour", ShowHome);
            panel.Add(OptionsBackButton);
            return panel;
        }

        private void RequestContinue()
        {
            if (_saveActions != null && _saveActions.ContinueMostRecent())
            {
                SetBusy(true);
            }
        }

        private void RequestNewGame()
        {
            if (_saveActions != null)
            {
                ShowNewGameSlots();
                return;
            }

            if (_actions.StartNewGame()) SetBusy(true);
        }

        private void RequestLoadGame()
        {
            if (_saveActions != null)
            {
                ShowLoadSlots();
                return;
            }

            if (_actions.LoadGame()) SetBusy(true);
        }

        private void RequestSlot(int slotIndex, bool newGame)
        {
            if (_saveActions == null) return;
            IReadOnlyList<SaveSlotView> slots = _saveActions.SaveSlots;
            SaveSlotView view = slots[slotIndex - 1];
            if (newGame && view.Exists && _overwriteConfirmationSlot != slotIndex)
            {
                _overwriteConfirmationSlot = slotIndex;
                SetSlotStatus(
                    $"{view.Slot.DisplayName} est occupée. Cliquez à nouveau pour l’écraser.");
                return;
            }

            bool started = newGame
                ? _saveActions.StartNewGameInSlot(slotIndex)
                : _saveActions.LoadGameFromSlot(slotIndex);
            if (started) SetBusy(true);
        }

        public void SetHomeStatus(string text)
        {
            HomeStatus.text = text ?? string.Empty;
        }

        private void SetSlotStatus(string text)
        {
            string value = text ?? string.Empty;
            if (CurrentPanel == MainMenuPanel.LoadSlots)
            {
                if (_loadSlotStatus != null) _loadSlotStatus.text = value;
            }
            else if (_newSlotStatus != null)
            {
                _newSlotStatus.text = value;
            }
        }
    }
}
