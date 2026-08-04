using System;

namespace AgeOfSurvival.Runtime.Frontend
{
    /// <summary>
    /// Testable frontend application commands. It owns no scene object and no UI.
    /// </summary>
    public sealed class FrontendController
    {
        private readonly IFrontendSceneLoader _sceneLoader;
        private readonly IApplicationQuitter _quitter;
        private readonly ISaveAvailability _saveAvailability;

        public FrontendController(
            IFrontendSceneLoader sceneLoader,
            IApplicationQuitter quitter,
            ISaveAvailability saveAvailability,
            IOnlineFrontendAvailability online)
        {
            _sceneLoader = sceneLoader
                ?? throw new ArgumentNullException(nameof(sceneLoader));
            _quitter = quitter
                ?? throw new ArgumentNullException(nameof(quitter));
            _saveAvailability = saveAvailability
                ?? throw new ArgumentNullException(nameof(saveAvailability));
            Online = online
                ?? throw new ArgumentNullException(nameof(online));
        }

        public bool HasSave => _saveAvailability.HasSave;
        public bool IsBusy => _sceneLoader.IsLoading;
        public IOnlineFrontendAvailability Online { get; }

        public bool StartNewGame()
        {
            return TryLoad(FrontendSceneNames.Gameplay);
        }

        public bool LoadGame()
        {
            return HasSave && TryLoad(FrontendSceneNames.Gameplay);
        }

        public bool ReturnToMainMenu()
        {
            return TryLoad(FrontendSceneNames.MainMenu);
        }

        public void Quit()
        {
            _quitter.Quit();
        }

        private bool TryLoad(string sceneName)
        {
            if (IsBusy)
            {
                return false;
            }

            bool wasBlocked = GameplayInputGate.IsBlocked;
            GameplayInputGate.SetBlocked(true);
            try
            {
                if (_sceneLoader.TryLoadScene(sceneName))
                {
                    return true;
                }
            }
            catch
            {
                GameplayInputGate.SetBlocked(wasBlocked);
                throw;
            }

            GameplayInputGate.SetBlocked(wasBlocked);
            return false;
        }
    }
}
