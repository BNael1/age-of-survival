using System;
using AgeOfSurvival.Core.Shelter;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Shelter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(35)]
    public sealed class ShelterHudBehaviour : MonoBehaviour
    {
        public const int SortingOrder = 240;
        private UIDocument _document;
        private PanelSettings _settings;
        private VisualElement _panel;
        private Label _summary;
        private Label _details;
        private Button _rest;
        private Button _sleep;
        private void Start()
        {
            _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.scaleMode = PanelScaleMode.ScaleWithScreenSize; _settings.referenceResolution = new Vector2Int(1280, 720);
            _settings.themeStyleSheet = UnityEngine.Resources.Load<ThemeStyleSheet>("InventoryPrototypeTheme");
            _settings.match = .5f; _settings.sortingOrder = SortingOrder;
            _document.panelSettings = _settings; _document.sortingOrder = SortingOrder;
            _document.rootVisualElement.schedule.Execute(Build);
        }
        private void Build()
        {
            var root = _document.rootVisualElement; root.Clear(); root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0; root.pickingMode = PickingMode.Ignore;
            _panel = new VisualElement { name = "shelter-panel" }; _panel.style.position = Position.Absolute;
            _panel.style.right = 16; _panel.style.bottom = 16; _panel.style.width = 300; _panel.style.paddingLeft = 10; _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8; _panel.style.paddingBottom = 8; _panel.style.backgroundColor = new Color(.06f,.09f,.11f,.93f);
            _panel.pickingMode = PickingMode.Position; root.Add(_panel);
            _summary = new Label { name = "shelter-summary" }; _summary.style.unityFontStyleAndWeight = FontStyle.Bold; _panel.Add(_summary);
            _details = new Label { name = "shelter-details" }; _details.style.whiteSpace = WhiteSpace.Normal; _panel.Add(_details);
            _rest = new Button(() => ShelterRuntimeSessionProvider.Current.TryStart(ShelterRestSleepKind.Rest)) { text = "Se reposer" };
            _sleep = new Button(() => ShelterRuntimeSessionProvider.Current.TryStart(ShelterRestSleepKind.Sleep)) { text = "Dormir" };
            _panel.Add(_rest); _panel.Add(_sleep); Refresh();
        }
        private void Update() => Refresh();
        private void Refresh()
        {
            if (_panel == null) return;
            var runtime = ShelterRuntimeSessionProvider.Current; var session = runtime.Session;
            bool reduced = UnityEngine.Object.FindFirstObjectByType<ConstructionRuntimeBehaviour>()?.Session.Mode.IsActive == true
                || UnityEngine.Object.FindFirstObjectByType<InventoryPrototypeUiBehaviour>()?.Ui?.IsPanelOpen == true
                || UnityEngine.Object.FindFirstObjectByType<InventoryPrototypeUiBehaviour>()?.Ui?.IsCraftPanelOpen == true;
            bool valid = session.HasCurrentShelter && session.ContainsValid(session.CurrentShelterId);
            _summary.text = valid ? "REFUGE" : "Extérieur";
            _details.style.display = valid && !reduced ? DisplayStyle.Flex : DisplayStyle.None;
            _rest.style.display = valid && !reduced ? DisplayStyle.Flex : DisplayStyle.None;
            _sleep.style.display = valid && !reduced ? DisplayStyle.Flex : DisplayStyle.None;
            if (!valid) return;
            session.HomeState.TryFind(session.CurrentShelterId, out ShelterFamiliarityState history);
            bool home = session.HomeState.HasPrimaryShelter && session.HomeState.PrimaryShelterId.Equals(session.CurrentShelterId);
            _details.text = $"Familiarité : {(history?.FamiliarityHalfPoints ?? 0) / 2f:0.#}\nTier : {history?.Level}\nConfort prototype : {runtime.ComfortScore}\nPlafond sommeil : {ShelterSleepRecoveryLimit.MaximumPercent(history?.Level ?? ShelterFamiliarityLevel.Unknown, home)} %\nFoyer : {(home ? "principal" : "Aucun foyer principal")}";
            bool action = runtime.IsActionActive; _rest.SetEnabled(!action); _sleep.SetEnabled(!action);
        }
        private void OnDestroy() { if (_settings != null) Destroy(_settings); }
    }
}
