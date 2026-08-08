using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryPrototypeUiBehaviour : MonoBehaviour
    {
        public const int SortingOrder = 220;
        private UIDocument _document;
        private PanelSettings _generatedPanelSettings;
        private InventoryPrototypeUiDocument _ui;

        public InventoryPrototypeUiDocument Ui => _ui;

        private void Start()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            _generatedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _generatedPanelSettings.name = "Generated Inventory Prototype Panel Settings";
            _generatedPanelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>("InventoryPrototypeTheme");
            _generatedPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _generatedPanelSettings.referenceResolution = new Vector2Int(1280, 720);
            _generatedPanelSettings.match = 0.5f;
            _generatedPanelSettings.sortingOrder = SortingOrder;
            _document.panelSettings = _generatedPanelSettings;
            _document.sortingOrder = SortingOrder;
            _document.rootVisualElement.schedule.Execute(BuildUi);
        }

        private void OnDestroy()
        {
            if (_generatedPanelSettings == null) return;
            if (Application.isPlaying)
            {
                Destroy(_generatedPanelSettings);
            }
            else
            {
                DestroyImmediate(_generatedPanelSettings);
            }
        }

        private void Update()
        {
            _ui?.Refresh();
        }

        private void BuildUi()
        {
            _ui = new InventoryPrototypeUiDocument(
                _document.rootVisualElement,
                InventoryPrototypeSessionProvider.Current);
        }
    }
}
