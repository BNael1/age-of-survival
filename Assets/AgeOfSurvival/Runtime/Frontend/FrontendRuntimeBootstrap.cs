using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    /// <summary>
    /// Scene adapter only. It installs the relevant frontend without making
    /// simulation or persistence depend on a GameObject singleton.
    /// </summary>
    public static class FrontendRuntimeBootstrap
    {
        private const string HostName = "Age of Survival Frontend";

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHandler()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapInitialScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Bootstrap(scene);
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            Bootstrap(scene);
        }

        private static void Bootstrap(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (scene.name == FrontendSceneNames.MainMenu)
            {
                GameplayInputGate.SetBlocked(true);
                DisableInventoryFrontend(scene);
                EnsureHost<MainMenuBehaviour>(scene);
                return;
            }

            if (scene.name == FrontendSceneNames.Gameplay)
            {
                GameplayInputGate.SetBlocked(false);
                EnsureHost<PrototypeSaveRuntimeBehaviour>(scene);
                EnsureHost<PauseMenuBehaviour>(scene);
                EnsureHost<PlayerHealthHudBehaviour>(scene);
            }
        }

        private static void DisableInventoryFrontend(Scene scene)
        {
            InventoryPrototypeUiBehaviour[] behaviours =
                Object.FindObjectsByType<InventoryPrototypeUiBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                InventoryPrototypeUiBehaviour behaviour =
                    behaviours[index];
                if (behaviour.gameObject.scene != scene)
                {
                    continue;
                }

                behaviour.enabled = false;
                if (behaviour.TryGetComponent(
                    out UIDocument document))
                {
                    document.enabled = false;
                }
            }
        }

        private static void EnsureHost<T>(Scene scene)
            where T : MonoBehaviour
        {
            T[] existing = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < existing.Length; index++)
            {
                if (existing[index].gameObject.scene == scene)
                {
                    return;
                }
            }

            var host = new GameObject(HostName);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<T>();
        }
    }
}
