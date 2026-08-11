using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Construction
{
    [DisallowMultipleComponent]
    public sealed class ConstructionRuntimeBehaviour : MonoBehaviour
    {
        public const int SortingOrder = 230;
        public const string PrototypeInputBindingNotice = "PROTOTYPE_INPUT_BINDING";

        private ConstructionRuntimeSession _session;
        private DebugIsometricWorld _worldRenderer;
        private Camera _camera;
        private IRuntimeUiPointerBlocker _uiPointerBlocker;
        private ConstructionGhostPresenter _ghost;
        private ConstructionWorldPresenter _worldPresenter;
        private UIDocument _document;
        private PanelSettings _generatedPanelSettings;
        private KeyControl _toggleKey;
        private InventoryPrototypeUiBehaviour _inventoryUi;
        private bool _workPointerHeld;

        public ConstructionRuntimeSession Session => _session;
        public ConstructionUiDocument Ui { get; private set; }
        public ConstructionGhostPresenter Ghost => _ghost;
        public ConstructionWorldPresenter WorldPresenter => _worldPresenter;

        private void Start()
        {
            _session = ConstructionRuntimeSessionProvider.Current;
            _worldRenderer = Object.FindFirstObjectByType<DebugIsometricWorld>();
            _camera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            _inventoryUi = Object.FindFirstObjectByType<InventoryPrototypeUiBehaviour>();
            _uiPointerBlocker = new UiToolkitRuntimePointerBlocker();
            ResolveToggleKey();

            if (_worldRenderer == null || _camera == null)
            {
                enabled = false;
                return;
            }

            _ghost = new ConstructionGhostPresenter(transform);
            _worldPresenter = new ConstructionWorldPresenter(
                transform,
                _worldRenderer,
                _session,
                Object.FindFirstObjectByType<GroundAnchorSortCoordinator>());
            CreateDocument();
        }

        private void Update()
        {
            if (_session == null) return;

            if (InventoryPanelIsOpen() && _session.Mode.IsActive)
                CloseMode();

            if (!GameplayInputGate.IsBlocked)
                ProcessPrototypeInput();
            else
            {
                EndWorkAction();
                _ghost?.Hide();
            }

            Mouse mouse = Mouse.current;
            if (_workPointerHeld
                && (mouse == null || !mouse.leftButton.isPressed))
            {
                EndWorkAction();
            }
            UpdatePreview();
            _worldPresenter?.Refresh();
            Ui?.Refresh();
        }

        private void OnDestroy()
        {
            EndWorkAction();
            _ghost?.Dispose();
            _worldPresenter?.Dispose();
            if (_generatedPanelSettings != null)
            {
                if (Application.isPlaying) Destroy(_generatedPanelSettings);
                else DestroyImmediate(_generatedPanelSettings);
            }
        }

        public void OpenMode()
        {
            CloseInventoryPanels();
            _session.Execute(ConstructionCommand.Open());
            if (!_session.Mode.HasSelection && _session.Catalog.Definitions.Count > 0)
                _session.Execute(ConstructionCommand.Select(_session.Catalog.Definitions[0].Core.Id));
            Ui?.Refresh(force: true);
        }

        public void CloseMode()
        {
            EndWorkAction();
            _session.Execute(ConstructionCommand.Close());
            _ghost?.Hide();
            Ui?.Refresh(force: true);
        }

        public ConstructionRuntimeResult ConfirmCurrentPreview()
        {
            ConstructionPreviewState preview = _session.Mode.Preview;
            if (!preview.IsTechnicallyPlaceable)
                return new ConstructionRuntimeResult(ConstructionRuntimeReason.ProjectionInvalid, default);
            return _session.ExecutePlacement(ConstructionCommand.Confirm(preview.Space));
        }

        public bool PreviewAtScreenPositionForTests(Vector2 screenPosition)
        {
            if (_session == null || !_session.Mode.IsActive || !_session.Mode.HasSelection)
                return false;
            UpdatePreviewAt(screenPosition);
            return _ghost != null && _ghost.IsVisible;
        }

        public bool IsPointerBlockedForTests(Vector2 screenPosition) =>
            _uiPointerBlocker != null && _uiPointerBlocker.IsBlocked(screenPosition);

        public ConstructionRuntimeResult BeginWorkAction(
            ConstructionInstanceId instanceId)
        {
            InventoryPrototypeSession inventory = InventoryPrototypeSessionProvider.Current;
            ConstructionRuntimeResult result = _session.BeginWork(
                instanceId,
                inventory.CurrentTick,
                inventory.CurrentPlayerPosition);
            _workPointerHeld = result.Succeeded;
            return result;
        }

        public void EndWorkAction()
        {
            _workPointerHeld = false;
            if (_session != null && _session.ActiveWorkSite.IsValid)
                _session.CancelWork();
        }

        /// <summary>
        /// Consumes the existing player fixed tick. No Update-time or secondary
        /// construction clock advances work.
        /// </summary>
        public void SimulateFixedTick(
            long currentTick,
            WorldPosition playerPosition,
            bool playerMoved)
        {
            if (_session == null || !_session.ActiveWorkSite.IsValid) return;
            bool contextCompatible = enabled
                && isActiveAndEnabled
                && _session.Mode.IsActive
                && !GameplayInputGate.IsBlocked
                && !InventoryPanelIsOpen();
            _session.AdvanceToTick(
                currentTick,
                playerPosition,
                playerMoved,
                _workPointerHeld,
                contextCompatible);
            if (!_session.ActiveWorkSite.IsValid) _workPointerHeld = false;
        }

        private void CreateDocument()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();

            _generatedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _generatedPanelSettings.name = "Generated Construction Prototype Panel Settings";
            _generatedPanelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>("InventoryPrototypeTheme");
            _generatedPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _generatedPanelSettings.referenceResolution = new Vector2Int(1280, 720);
            _generatedPanelSettings.match = 0.5f;
            _generatedPanelSettings.sortingOrder = SortingOrder;
            _document.panelSettings = _generatedPanelSettings;
            _document.sortingOrder = SortingOrder;
            _document.rootVisualElement.schedule.Execute(() =>
            {
                Ui = new ConstructionUiDocument(
                    _document.rootVisualElement,
                    _session,
                    OpenMode,
                    CloseMode,
                    BeginWorkAction,
                    EndWorkAction);
            });
        }

        private void ProcessPrototypeInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (_toggleKey == null || _toggleKey.device != keyboard))
                ResolveToggleKey();
            if (_toggleKey != null && _toggleKey.wasPressedThisFrame)
            {
                if (_session.Mode.IsActive) CloseMode();
                else OpenMode();
            }

            if (!_session.Mode.IsActive) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                CloseMode();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;
            Vector2 screenPosition = mouse.position.ReadValue();
            if (_uiPointerBlocker.IsBlocked(screenPosition)) return;

            UpdatePreviewAt(screenPosition);
            ConfirmCurrentPreview();
        }

        private void UpdatePreview()
        {
            if (!_session.Mode.IsActive || !_session.Mode.HasSelection || GameplayInputGate.IsBlocked)
            {
                _ghost?.Hide();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _session.ClearPreview(ConstructionPreviewStatus.NotProjectable);
                _ghost?.Hide();
                return;
            }
            UpdatePreviewAt(mouse.position.ReadValue());
        }

        private void UpdatePreviewAt(Vector2 screenPosition)
        {
            if (_uiPointerBlocker.IsBlocked(screenPosition))
            {
                _session.ClearPreview(ConstructionPreviewStatus.PointerBlockedByUi);
                _ghost.Hide();
                return;
            }

            ConstructionPrototypeDefinition definition =
                _session.Catalog.Require(_session.Mode.SelectedDefinitionId);
            if (!ConstructionPointerProjector.TryProjectToWorldPlane(
                    _camera,
                    screenPosition,
                    0f,
                    out Vector3 worldPosition)
                || !_worldRenderer.TryResolveConstructionTarget(
                    worldPosition,
                    definition.Core.SpaceKind,
                    out ConstructionPlacementTarget target)
                || !_worldRenderer.TryMapConstructionSpaceCenter(
                    target.Space,
                    out Vector3 center))
            {
                _session.ClearPreview(ConstructionPreviewStatus.NotProjectable);
                _ghost.Hide();
                return;
            }

            ConstructionPreviewState preview = _session.EvaluatePreview(target.Space);
            _ghost.Show(center, definition, target.Space, preview.IsTechnicallyPlaceable);
        }

        private void ResolveToggleKey()
        {
            Keyboard keyboard = Keyboard.current;
            _toggleKey = keyboard != null
                ? keyboard.FindKeyOnCurrentKeyboardLayout("b") ?? keyboard.bKey
                : null;
        }

        private bool InventoryPanelIsOpen()
        {
            if (_inventoryUi == null)
                _inventoryUi = Object.FindFirstObjectByType<InventoryPrototypeUiBehaviour>();
            return _inventoryUi != null
                && _inventoryUi.Ui != null
                && (_inventoryUi.Ui.IsPanelOpen || _inventoryUi.Ui.IsCraftPanelOpen);
        }

        private void CloseInventoryPanels()
        {
            if (_inventoryUi == null)
                _inventoryUi = Object.FindFirstObjectByType<InventoryPrototypeUiBehaviour>();
            if (_inventoryUi?.Ui == null) return;
            if (_inventoryUi.Ui.IsPanelOpen) _inventoryUi.Ui.TogglePanel();
            if (_inventoryUi.Ui.IsCraftPanelOpen) _inventoryUi.Ui.ToggleCraftPanel();
        }
    }
}
