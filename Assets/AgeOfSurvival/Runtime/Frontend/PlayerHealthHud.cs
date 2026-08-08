using System;
using AgeOfSurvival.Runtime.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    /// <summary>
    /// Programmatic UI Toolkit HUD for the first player-health loop.
    /// It observes the canonical session and never mutates health.
    /// </summary>
    public sealed class PlayerHealthHudDocument
    {
        private readonly InventoryPrototypeSession _session;

        public PlayerHealthHudDocument(
            VisualElement root,
            InventoryPrototypeSession session)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            Root.Clear();
            Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.right = 0;
            Root.style.top = 0;
            Root.style.bottom = 0;

            Panel = new VisualElement
            {
                name = "player-health-hud"
            };
            Panel.pickingMode = PickingMode.Ignore;
            Panel.style.position = Position.Absolute;
            Panel.style.left = 16;
            Panel.style.bottom = 16;
            Panel.style.width = 280;
            Panel.style.paddingLeft = 12;
            Panel.style.paddingRight = 12;
            Panel.style.paddingTop = 9;
            Panel.style.paddingBottom = 10;
            Panel.style.backgroundColor =
                new Color(0.07f, 0.085f, 0.085f, 0.94f);
            Panel.style.borderTopLeftRadius = 4;
            Panel.style.borderTopRightRadius = 4;
            Panel.style.borderBottomLeftRadius = 4;
            Panel.style.borderBottomRightRadius = 4;
            Root.Add(Panel);

            var headingRow = new VisualElement();
            headingRow.style.flexDirection = FlexDirection.Row;
            headingRow.style.justifyContent = Justify.SpaceBetween;
            headingRow.style.marginBottom = 5;
            Panel.Add(headingRow);

            var title = new Label("HEALTH")
            {
                name = "player-health-title"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            title.style.color = new Color(0.90f, 0.90f, 0.84f);
            headingRow.Add(title);

            HealthValue = new Label
            {
                name = "player-health-value"
            };
            HealthValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            HealthValue.style.fontSize = 15;
            HealthValue.style.color = new Color(0.96f, 0.96f, 0.90f);
            headingRow.Add(HealthValue);

            HealthBar = new ProgressBar
            {
                name = "player-health-bar",
                title = string.Empty,
                lowValue = 0f
            };
            HealthBar.pickingMode = PickingMode.Ignore;
            HealthBar.style.height = 18;
            HealthBar.style.minHeight = 18;
            Panel.Add(HealthBar);

            Refresh();
        }

        public VisualElement Root { get; }
        public VisualElement Panel { get; }
        public ProgressBar HealthBar { get; }
        public Label HealthValue { get; }

        public void Refresh()
        {
            HealthBar.highValue = _session.Health.MaximumHealth;
            HealthBar.value = _session.Health.CurrentHealth;
            HealthValue.text =
                $"{_session.Health.CurrentHealth} / "
                + $"{_session.Health.MaximumHealth}";
            Panel.tooltip = _session.Health.IsDead
                ? "Dead — respawn pending on the fixed simulation tick."
                : "Current and maximum player health.";
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(30)]
    public sealed class PlayerHealthHudBehaviour : MonoBehaviour
    {
        public const int SortingOrder = 210;
        private UIDocument _document;
        private PanelSettings _generatedPanelSettings;

        public PlayerHealthHudDocument Ui { get; private set; }

        private void Start()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            _generatedPanelSettings =
                ScriptableObject.CreateInstance<PanelSettings>();
            _generatedPanelSettings.name =
                "Generated Player Health HUD Panel Settings";
            _generatedPanelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>(
                    "InventoryPrototypeTheme");
            _generatedPanelSettings.scaleMode =
                PanelScaleMode.ScaleWithScreenSize;
            _generatedPanelSettings.referenceResolution =
                new Vector2Int(1280, 720);
            _generatedPanelSettings.match = 0.5f;
            _generatedPanelSettings.sortingOrder = SortingOrder;
            _document.panelSettings = _generatedPanelSettings;
            _document.sortingOrder = SortingOrder;
            _document.rootVisualElement.schedule.Execute(BuildUi);
        }

        private void Update()
        {
            Ui?.Refresh();
        }

        private void OnDestroy()
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
        }

        private void BuildUi()
        {
            Ui = new PlayerHealthHudDocument(
                _document.rootVisualElement,
                InventoryPrototypeSessionProvider.Current);
        }
    }
}
