using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class MainMenuBehaviour : MonoBehaviour, IMainMenuActions
    {
        private UIDocument _document;
        private PanelSettings _generatedPanelSettings;
        private FrontendController _controller;

        public MainMenuDocument Ui { get; private set; }
        public bool HasSave => _controller?.HasSave ?? false;
        public bool IsBusy => _controller?.IsBusy ?? false;
        public IOnlineFrontendAvailability Online =>
            _controller?.Online
            ?? DeferredOnlineFrontendAvailability.Instance;

        private void Awake()
        {
            GameplayInputGate.SetBlocked(true);
        }

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

        private void OnDestroy()
        {
            DestroyGeneratedPanelSettings();
        }

        public bool StartNewGame()
        {
            bool started = _controller != null
                && _controller.StartNewGame();
            if (started)
            {
                Ui?.SetBusy(true);
            }

            return started;
        }

        public bool LoadGame()
        {
            bool started = _controller != null
                && _controller.LoadGame();
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
                "Generated Main Menu Panel Settings";
            _generatedPanelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>(
                    "InventoryPrototypeTheme");
            _generatedPanelSettings.scaleMode =
                PanelScaleMode.ScaleWithScreenSize;
            _generatedPanelSettings.referenceResolution =
                new Vector2Int(1280, 720);
            _generatedPanelSettings.match = 0.5f;
            _document.panelSettings = _generatedPanelSettings;
            _document.sortingOrder = 1000;
        }

        private void BuildUi()
        {
            Ui = new MainMenuDocument(
                _document.rootVisualElement,
                this);
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
