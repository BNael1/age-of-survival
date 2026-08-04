using System;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    public enum MainMenuPanel
    {
        Home = 0,
        Online = 1,
        Options = 2
    }

    /// <summary>
    /// Programmatic UI Toolkit document for the first frontend iteration.
    /// Navigation exists now; saves and networking remain explicit future services.
    /// </summary>
    public sealed class MainMenuDocument
    {
        private readonly IMainMenuActions _actions;
        private readonly VisualElement _homePanel;
        private readonly VisualElement _onlinePanel;
        private readonly VisualElement _optionsPanel;

        public MainMenuDocument(
            VisualElement root,
            IMainMenuActions actions)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _actions = actions
                ?? throw new ArgumentNullException(nameof(actions));

            FrontendStyles.ConfigureRoot(Root);
            FrontendStyles.CreateBackdrop(Root, 0.68f);
            VisualElement shell =
                FrontendStyles.CreateLeftShell(Root, "main-menu-shell");
            shell.Add(FrontendStyles.CreateGameTitle());
            shell.Add(FrontendStyles.CreateMutedLabel("PROTOTYPE"));

            _homePanel = FrontendStyles.CreatePanel("main-menu-home");
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

            LoadButton.SetEnabled(_actions.HasSave);
            _homePanel.Add(NewGameButton);
            _homePanel.Add(LoadButton);
            _homePanel.Add(OnlineButton);
            _homePanel.Add(OptionsButton);
            _homePanel.Add(QuitButton);
            shell.Add(_homePanel);

            _onlinePanel = BuildOnlinePanel();
            shell.Add(_onlinePanel);

            _optionsPanel = BuildOptionsPanel();
            shell.Add(_optionsPanel);

            ShowHome();
        }

        public VisualElement Root { get; }
        public MainMenuPanel CurrentPanel { get; private set; }
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

        public void ShowHome()
        {
            CurrentPanel = MainMenuPanel.Home;
            FrontendStyles.ShowOnly(
                _homePanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel);
        }

        public void ShowOnline()
        {
            CurrentPanel = MainMenuPanel.Online;
            FrontendStyles.ShowOnly(
                _onlinePanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel);
        }

        public void ShowOptions()
        {
            CurrentPanel = MainMenuPanel.Options;
            FrontendStyles.ShowOnly(
                _optionsPanel,
                _homePanel,
                _onlinePanel,
                _optionsPanel);
        }

        public void SetBusy(bool busy)
        {
            bool enabled = !busy;
            NewGameButton.SetEnabled(enabled);
            LoadButton.SetEnabled(enabled && _actions.HasSave);
            OnlineButton.SetEnabled(enabled);
            OptionsButton.SetEnabled(enabled);
            QuitButton.SetEnabled(enabled);
            OnlineBackButton.SetEnabled(enabled);
            OptionsBackButton.SetEnabled(enabled);
        }

        private VisualElement BuildOnlinePanel()
        {
            VisualElement panel =
                FrontendStyles.CreatePanel("main-menu-online");
            panel.Add(FrontendStyles.CreateSectionTitle("EN LIGNE"));
            panel.Add(FrontendStyles.CreateMutedLabel(
                "Architecture réservée au futur client et au serveur dédié VPS."));

            JoinButton = FrontendStyles.CreateMenuButton(
                "join-server-button",
                "Rejoindre un serveur",
                () => { });
            HostButton = FrontendStyles.CreateMenuButton(
                "host-server-button",
                "Héberger une partie",
                () => { });
            FavoritesButton = FrontendStyles.CreateMenuButton(
                "favorites-button",
                "Serveurs favoris",
                () => { });
            OnlineBackButton = FrontendStyles.CreateMenuButton(
                "online-back-button",
                "Retour",
                ShowHome);

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
                "options-back-button",
                "Retour",
                ShowHome);
            panel.Add(OptionsBackButton);
            return panel;
        }

        private void RequestNewGame()
        {
            if (_actions.StartNewGame())
            {
                SetBusy(true);
            }
        }

        private void RequestLoadGame()
        {
            if (_actions.LoadGame())
            {
                SetBusy(true);
            }
        }
    }
}
