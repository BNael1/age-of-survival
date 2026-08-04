using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgeOfSurvival.Runtime.Frontend
{
    public interface IFrontendSceneLoader
    {
        bool IsLoading { get; }
        bool TryLoadScene(string sceneName);
    }

    public sealed class UnityFrontendSceneLoader : IFrontendSceneLoader
    {
        public bool IsLoading { get; private set; }

        public bool TryLoadScene(string sceneName)
        {
            if (IsLoading || string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single);
            if (operation == null)
            {
                return false;
            }

            IsLoading = true;
            operation.completed += _ => IsLoading = false;
            return true;
        }
    }

    public interface IApplicationQuitter
    {
        void Quit();
    }

    public sealed class UnityApplicationQuitter : IApplicationQuitter
    {
        public void Quit()
        {
            Application.Quit();
        }
    }

    public interface ISaveAvailability
    {
        bool HasSave { get; }
    }

    public sealed class NoSaveAvailability : ISaveAvailability
    {
        public static readonly NoSaveAvailability Instance =
            new NoSaveAvailability();

        private NoSaveAvailability()
        {
        }

        public bool HasSave => false;
    }

    public interface IOnlineFrontendAvailability
    {
        bool CanJoin { get; }
        bool CanHost { get; }
        bool CanUseFavorites { get; }
        string StatusText { get; }
    }

    /// <summary>
    /// Keeps the online route visible without pretending a transport exists.
    /// A future VPS-backed client/server implementation replaces this service.
    /// </summary>
    public sealed class DeferredOnlineFrontendAvailability
        : IOnlineFrontendAvailability
    {
        public static readonly DeferredOnlineFrontendAvailability Instance =
            new DeferredOnlineFrontendAvailability();

        private DeferredOnlineFrontendAvailability()
        {
        }

        public bool CanJoin => false;
        public bool CanHost => false;
        public bool CanUseFavorites => false;
        public string StatusText =>
            "Client et serveur dédiés prévus pour une version ultérieure.";
    }

    public interface IMainMenuActions
    {
        bool HasSave { get; }
        bool IsBusy { get; }
        IOnlineFrontendAvailability Online { get; }
        bool StartNewGame();
        bool LoadGame();
        void Quit();
    }

    public interface IPauseMenuActions
    {
        bool IsBusy { get; }
        void Resume();
        bool ReturnToMainMenu();
        void Quit();
    }
}
