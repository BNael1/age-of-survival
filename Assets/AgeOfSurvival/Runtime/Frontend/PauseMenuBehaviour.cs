using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class PauseMenuBehaviour : MonoBehaviour, IPauseMenuActions
    {
        private UIDocument _document;
        private PanelSettings _generatedPanelSettings;
        private FrontendController _controller;
        private bool _isPaused;

        public PauseMenuDocument Ui { get; private set; }
        public bool IsPaused => _isPaused;
        public bool IsBusy => _controller?.IsBusy ?? false;

        private void Start()
        {
            _controller = new FrontendController(
                new UnityFrontendSceneLoader(),
                new UnityApplicationQuitter(),
                NoSaveAvailability.Instance,
                DeferredOnlineFrontendAvailability.Instance);
            CreateDocument();
            _document.rootVisualElement.schedule.Execute(BuildUi);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null
                || IsBusy
                || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (_isPaused
                && Ui != null
                && Ui.CurrentPanel == PauseMenuPanel.Options)
            {
                Ui.ShowPause();
                return;
            }

            SetPaused(!_isPaused);
        }

        private void OnDisable()
        {
            if (!IsBusy)
            {
                GameplayInputGate.SetBlocked(false);
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedPanelSettings();
        }

        public void SetPaused(bool paused)
        {
            if (IsBusy && !paused)
            {
                return;
            }

            _isPaused = paused;
            GameplayInputGate.SetBlocked(paused);
            Ui?.SetVisible(paused);
            if (paused)
            {
                Ui?.ShowPause();
            }
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public bool ReturnToMainMenu()
        {
            if (_controller == null)
            {
                return false;
            }

            GameplayInputGate.SetBlocked(true);
            bool started = _controller.ReturnToMainMenu();
            if (started)
            {
                Ui?.SetBusy(true);
            }

            return started;
        }

        public void Quit()
        {
            _controller?.Quit();
        }

        private void CreateDocument()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            _generatedPanelSettings =
                ScriptableObject.CreateInstance<PanelSettings>();
            _generatedPanelSettings.name =
                "Generated Pause Menu Panel Settings";
            _generatedPanelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>(
                    "InventoryPrototypeTheme");
            _generatedPanelSettings.scaleMode =
                PanelScaleMode.ScaleWithScreenSize;
            _generatedPanelSettings.referenceResolution =
                new Vector2Int(1280, 720);
            _generatedPanelSettings.match = 0.5f;
            _document.panelSettings = _generatedPanelSettings;
            _document.sortingOrder = 1100;
        }

        private void BuildUi()
        {
            Ui = new PauseMenuDocument(
                _document.rootVisualElement,
                this);
            Ui.SetVisible(_isPaused);
            if (_isPaused)
            {
                Ui.ShowPause();
            }
        }

        private void DestroyGeneratedPanelSettings()
        {
            if (_generatedPanelSettings == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_generatedPanelSettings);
            }
            else
            {
                DestroyImmediate(_generatedPanelSettings);
            }

            _generatedPanelSettings = null;
        }
    }
}
