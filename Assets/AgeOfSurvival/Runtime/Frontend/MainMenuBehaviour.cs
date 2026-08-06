using System.Collections.Generic;
using AgeOfSurvival.Runtime.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class MainMenuBehaviour :
        MonoBehaviour,
        IMainMenuActions,
        ISaveMainMenuActions
    {
        private sealed class SaveAvailability : ISaveAvailability
        {
            public bool HasSave => PrototypeSaveRuntime.HasAnySave();
        }

        private UIDocument _document;
        private PanelSettings _generatedPanelSettings;
        private FrontendController _controller;

        public MainMenuDocument Ui { get; private set; }
        public bool HasSave => PrototypeSaveRuntime.HasAnySave();
        public bool IsBusy => _controller?.IsBusy ?? false;
        public IOnlineFrontendAvailability Online =>
            _controller?.Online
            ?? DeferredOnlineFrontendAvailability.Instance;
        public IReadOnlyList<SaveSlotView> SaveSlots =>
            PrototypeSaveRuntime.ReadSlots();

        private void Awake()
        {
            GameplayInputGate.SetBlocked(true);
        }

        private void Start()
        {
            _controller = new FrontendController(
                new UnityFrontendSceneLoader(),
                new UnityApplicationQuitter(),
                new SaveAvailability(),
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
            return StartNewGameInSlot(1);
        }

        public bool LoadGame()
        {
            return ContinueMostRecent();
        }

        public bool ContinueMostRecent()
        {
            return PrototypeSaveRuntime.TryGetMostRecent(out SaveSlotId slot)
                && LoadGameFromSlot(slot.Index);
        }

        public bool StartNewGameInSlot(int slotIndex)
        {
            if (_controller == null) return false;
            var slot = new SaveSlotId(slotIndex);
            bool started = _controller.StartNewGame();
            if (started)
            {
                PrototypeSaveRuntime.BeginNewGame(slot);
                Ui?.SetBusy(true);
            }

            return started;
        }

        public bool LoadGameFromSlot(int slotIndex)
        {
            if (_controller == null) return false;
            var slot = new SaveSlotId(slotIndex);
            if (!PrototypeSaveRuntime.Exists(slot)) return false;
            bool started = _controller.LoadGame();
            if (started)
            {
                PrototypeSaveRuntime.BeginLoadGame(slot);
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
            Ui.SetHomeStatus(PrototypeSaveRuntime.ConsumeFrontendMessage());
        }

        private void DestroyGeneratedPanelSettings()
        {
            if (_generatedPanelSettings == null) return;
            if (Application.isPlaying) Destroy(_generatedPanelSettings);
            else DestroyImmediate(_generatedPanelSettings);
            _generatedPanelSettings = null;
        }
    }
}
