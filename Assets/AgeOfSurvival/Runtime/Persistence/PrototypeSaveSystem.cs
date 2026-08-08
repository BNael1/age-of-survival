using System;
using System.Collections.Generic;
using System.IO;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Persistence
{
    internal sealed class PrototypeWorldResolver : IWorldPopulationSettingsResolver
    {
        public bool TryResolve(
            WorldIdentitySnapshot identity,
            out WorldPopulationSettings settings)
        {
            if (!identity.PopulationProfileId.Equals(
                    WorldPopulationDefaults.TemperatePrototypeV1Id))
            {
                settings = default;
                return false;
            }

            switch (identity.PopulationRevision)
            {
                case 1:
                    settings =
                        WorldPopulationDefaults.CreateTemperatePrototypeV1(
                            identity.Generation.Seed);
                    break;
                case 2:
                    settings =
                        WorldPopulationDefaults.CreateTemperatePrototypeV2(
                            identity.Generation.Seed);
                    break;
                default:
                    settings = default;
                    return false;
            }

            return settings.Generation.Equals(identity.Generation)
                && settings.Profile.Id.Equals(identity.PopulationProfileId)
                && settings.Profile.Revision == identity.PopulationRevision;
        }
    }

    internal sealed class PrototypeInventoryResolver :
        IInventoryDefinitionResolver,
        IInventoryDefinitionCatalog
    {
        public IReadOnlyList<ItemDefinition> CurrentItemDefinitions =>
            InventoryPrototypeCatalog.Definitions;

        public bool TryResolveItemDefinition(
            InventoryDefinitionSnapshot saved,
            out ItemDefinition definition)
        {
            for (int index = 0;
                 index < InventoryPrototypeCatalog.Definitions.Length;
                 index++)
            {
                ItemDefinition candidate =
                    InventoryPrototypeCatalog.Definitions[index];
                if (candidate.Id.Equals(saved.Id))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryResolveContainerDefinition(
            InventoryContainerSnapshot saved,
            out ContainerDefinition definition)
        {
            if (saved.Id.Equals(InventoryPrototypeCatalog.MainContainerId)
                && string.Equals(
                    saved.DefinitionKey,
                    "player-main",
                    StringComparison.Ordinal)
                && saved.Capacity.Equals(new EncumbranceValue(
                    InventoryPrototypeCatalog.MainCapacityUnits)))
            {
                definition = new ContainerDefinition(
                    "player-main",
                    "Carried inventory",
                    new EncumbranceValue(
                        InventoryPrototypeCatalog.MainCapacityUnits));
                return true;
            }

            if (saved.Id.Equals(InventoryPrototypeCatalog.BagContainerId)
                && string.Equals(
                    saved.DefinitionKey,
                    "prototype-bag",
                    StringComparison.Ordinal)
                && saved.Capacity.Equals(new EncumbranceValue(
                    InventoryPrototypeCatalog.BagCapacityUnits)))
            {
                definition = new ContainerDefinition(
                    "prototype-bag",
                    "Prototype backpack",
                    new EncumbranceValue(
                        InventoryPrototypeCatalog.BagCapacityUnits));
                return true;
            }

            definition = null;
            return false;
        }
    }

    public sealed class PrototypeSaveService
    {
        private readonly AtomicGameSaveStorage _storage;
        private readonly GameSaveCoordinator _coordinator;
        private readonly SaveSlotMetadataStore _metadata;

        public PrototypeSaveService(string rootDirectory)
        {
            _storage = new AtomicGameSaveStorage(rootDirectory);
            _coordinator = new GameSaveCoordinator(
                _storage,
                new PrototypeWorldResolver(),
                new PrototypeInventoryResolver());
            _metadata = new SaveSlotMetadataStore(rootDirectory);
        }

        public IReadOnlyList<SaveSlotView> ReadSlots()
        {
            var result = new List<SaveSlotView>(SaveSlotPolicy.SlotCount);
            for (int index = 0; index < SaveSlotPolicy.All.Count; index++)
            {
                SaveSlotId slot = SaveSlotPolicy.All[index];
                bool exists = _coordinator.Exists(slot.StorageKey);
                try
                {
                    if (exists
                        && _metadata.TryRead(
                            slot,
                            out SaveSlotMetadata metadata))
                    {
                        result.Add(new SaveSlotView(
                            slot,
                            true,
                            metadata,
                            string.Empty));
                    }
                    else
                    {
                        result.Add(new SaveSlotView(
                            slot,
                            exists,
                            default,
                            string.Empty));
                    }
                }
                catch (Exception exception)
                {
                    result.Add(new SaveSlotView(
                        slot,
                        exists,
                        default,
                        exception.Message));
                }
            }

            return result.AsReadOnly();
        }

        public bool Exists(SaveSlotId slot) =>
            _coordinator.Exists(slot.StorageKey);

        public void Save(
            SaveSlotId slot,
            InventoryPrototypeSession session,
            double playedSeconds)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            GameSaveSnapshot snapshot = session.CaptureGameSaveSnapshot();
            _storage.Save(slot.StorageKey, snapshot);
            TryWriteMetadata(new SaveSlotMetadata(
                slot,
                DateTime.UtcNow,
                playedSeconds,
                snapshot.World.Generation.Seed,
                false));
        }

        public CoordinatedGameLoadResult Load(
            SaveSlotId slot,
            double previousPlayedSeconds,
            out double playedSeconds)
        {
            CoordinatedGameLoadResult result =
                _coordinator.Load(slot.StorageKey);
            playedSeconds = 0d;
            if (TryReadMetadata(slot, out SaveSlotMetadata metadata))
            {
                playedSeconds = metadata.PlayedSeconds;
            }

            if (result.Source == GameSaveLoadSource.Backup)
            {
                TryWriteMetadata(new SaveSlotMetadata(
                    slot,
                    DateTime.UtcNow,
                    playedSeconds,
                    result.State.World.Generation.Seed,
                    true));
            }

            return result;
        }

        private bool TryReadMetadata(
            SaveSlotId slot,
            out SaveSlotMetadata metadata)
        {
            try
            {
                return _metadata.TryRead(slot, out metadata);
            }
            catch (Exception exception) when (IsMetadataFailure(exception))
            {
                metadata = default;
                return false;
            }
        }

        private void TryWriteMetadata(SaveSlotMetadata metadata)
        {
            try
            {
                _metadata.Write(metadata);
            }
            catch (Exception exception) when (IsMetadataFailure(exception))
            {
                // The sidecar is informational. The authoritative save remains valid.
            }
        }

        private static bool IsMetadataFailure(Exception exception)
        {
            return exception is InvalidDataException
                || exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException
                || exception is System.Text.DecoderFallbackException;
        }
    }

    public enum PendingGameStart
    {
        None = 0,
        NewGame = 1,
        LoadGame = 2
    }

    public static class PrototypeSaveRuntime
    {
        private static readonly PrototypeSaveService Service =
            new PrototypeSaveService(
                UnitySavePathProvider.GetRootDirectory());
        private static SaveSlotId _selectedSlot;
        private static PendingGameStart _pendingStart;
        private static double _playedSeconds;
        private static string _frontendMessage = string.Empty;

        public static bool HasCurrentSlot => _selectedSlot.Index > 0;
        public static SaveSlotId CurrentSlot => _selectedSlot;
        public static double PlayedSeconds => _playedSeconds;

        public static IReadOnlyList<SaveSlotView> ReadSlots() =>
            Service.ReadSlots();

        public static bool Exists(SaveSlotId slot) =>
            Service.Exists(slot);

        public static bool HasAnySave()
        {
            IReadOnlyList<SaveSlotView> slots = ReadSlots();
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].Exists) return true;
            }

            return false;
        }

        public static bool TryGetMostRecent(out SaveSlotId slot)
        {
            IReadOnlyList<SaveSlotView> slots = ReadSlots();
            bool foundMetadata = false;
            DateTime latest = DateTime.MinValue;
            SaveSlotId fallback = default;
            bool hasFallback = false;
            slot = default;

            for (int index = 0; index < slots.Count; index++)
            {
                SaveSlotView candidate = slots[index];
                if (!candidate.Exists) continue;
                if (!hasFallback)
                {
                    hasFallback = true;
                    fallback = candidate.Slot;
                }

                if (!candidate.HasMetadata) continue;
                if (!foundMetadata
                    || candidate.Metadata.SavedAtUtc > latest)
                {
                    foundMetadata = true;
                    latest = candidate.Metadata.SavedAtUtc;
                    slot = candidate.Slot;
                }
            }

            if (foundMetadata) return true;
            slot = fallback;
            return hasFallback;
        }

        public static void BeginNewGame(SaveSlotId slot)
        {
            _selectedSlot = slot;
            _pendingStart = PendingGameStart.NewGame;
            _playedSeconds = 0d;
        }

        public static void BeginLoadGame(SaveSlotId slot)
        {
            _selectedSlot = slot;
            _pendingStart = PendingGameStart.LoadGame;
            _playedSeconds = 0d;
        }

        public static string ConsumeFrontendMessage()
        {
            string message = _frontendMessage;
            _frontendMessage = string.Empty;
            return message;
        }

        internal static void SetFrontendMessage(string message)
        {
            _frontendMessage = message ?? string.Empty;
        }

        internal static string ApplyPendingStart()
        {
            if (_pendingStart == PendingGameStart.None)
            {
                return string.Empty;
            }

            PendingGameStart pending = _pendingStart;
            _pendingStart = PendingGameStart.None;

            if (pending == PendingGameStart.NewGame)
            {
                InventoryPrototypeSessionProvider.ResetForNewGame();
                _playedSeconds = 0d;
                return string.Empty;
            }

            CoordinatedGameLoadResult loaded = Service.Load(
                _selectedSlot,
                _playedSeconds,
                out _playedSeconds);
            InventoryPrototypeSessionProvider.Install(loaded.State);
            return loaded.Source == GameSaveLoadSource.Backup
                ? "Backup récupéré — sauvegardez pour recréer le fichier principal."
                : string.Empty;
        }

        internal static void AddPlayedSeconds(double seconds)
        {
            if (seconds > 0d
                && !double.IsNaN(seconds)
                && !double.IsInfinity(seconds))
            {
                _playedSeconds += seconds;
            }
        }

        internal static void SaveCurrent()
        {
            if (!HasCurrentSlot)
            {
                throw new InvalidOperationException(
                    "No active save slot is selected.");
            }

            Service.Save(
                _selectedSlot,
                InventoryPrototypeSessionProvider.Current,
                _playedSeconds);
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-950)]
    public sealed class PrototypeSaveRuntimeBehaviour : MonoBehaviour
    {
        private readonly SaveRequestScheduler _scheduler =
            new SaveRequestScheduler();
        private UIDocument _statusDocument;
        private PanelSettings _generatedPanelSettings;
        private Label _statusLabel;
        private float _clearStatusAt;
        private bool _loadFailed;
        private bool _skipQuittingSave;

        public static PrototypeSaveRuntimeBehaviour Instance { get; private set; }
        public string StatusText { get; private set; } = string.Empty;
        public bool IsBusy { get; private set; }
        public bool HasSaveFailure { get; private set; }

        private void Awake()
        {
            Instance = this;
            try
            {
                StatusText = PrototypeSaveRuntime.ApplyPendingStart();
                RebindSceneSession();
            }
            catch (Exception exception)
            {
                _loadFailed = true;
                string message = "Échec du chargement : " + exception.Message;
                StatusText = message;
                PrototypeSaveRuntime.SetFrontendMessage(message);
                GameplayInputGate.SetBlocked(true);
                Debug.LogException(exception, this);
                SceneManager.LoadSceneAsync(FrontendSceneNames.MainMenu);
            }
        }

        private static void RebindSceneSession()
        {
            DebugResourceInteraction resources =
                UnityEngine.Object.FindFirstObjectByType<
                    DebugResourceInteraction>();
            if (resources == null
                || ReferenceEquals(
                    resources.PrototypeSession,
                    InventoryPrototypeSessionProvider.Current))
            {
                return;
            }

            resources.Rebuild();
        }

        private void OnEnable()
        {
            Application.quitting += HandleQuitting;
        }

        private void Start()
        {
            if (_loadFailed) return;
            CreateStatusOverlay();
            if (!string.IsNullOrEmpty(StatusText))
            {
                SetStatus(StatusText, 8f);
            }
        }

        private void OnDisable()
        {
            Application.quitting -= HandleQuitting;
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void OnDestroy()
        {
            DestroyGeneratedPanelSettings();
        }

        private void Update()
        {
            if (_loadFailed || !PrototypeSaveRuntime.HasCurrentSlot)
            {
                ClearExpiredStatus();
                return;
            }

            if (!GameplayInputGate.IsBlocked)
            {
                PrototypeSaveRuntime.AddPlayedSeconds(
                    Time.unscaledDeltaTime);
            }

            _scheduler.Advance(Time.unscaledDeltaTime);
            TryRunPending();
            ClearExpiredStatus();
        }

        public void RequestManualSave()
        {
            if (!PrototypeSaveRuntime.HasCurrentSlot)
            {
                SetStatus("Aucune chronologie active.", 5f);
                return;
            }

            Request(SaveRequestKind.Manual);
        }

        public void RequestSaveAndReturnToMainMenu()
        {
            if (!PrototypeSaveRuntime.HasCurrentSlot)
            {
                GameplayInputGate.SetBlocked(true);
                SceneManager.LoadSceneAsync(FrontendSceneNames.MainMenu);
                return;
            }

            InventoryPrototypeSessionProvider.Current
                .CancelActiveTransferForSaveAndQuit();
            Request(SaveRequestKind.ReturnToMainMenu);
        }

        public void RequestSaveAndQuit()
        {
            if (!PrototypeSaveRuntime.HasCurrentSlot)
            {
                Application.Quit();
                return;
            }

            InventoryPrototypeSessionProvider.Current
                .CancelActiveTransferForSaveAndQuit();
            Request(SaveRequestKind.Quit);
        }

        public void QuitWithoutSaving()
        {
            _skipQuittingSave = true;
            Application.Quit();
        }

        private void Request(SaveRequestKind kind)
        {
            HasSaveFailure = false;
            _scheduler.Request(kind);
            if (IsSafePoint())
            {
                SetStatus("Sauvegarde…", 0f);
            }
            else
            {
                SetStatus("Sauvegarde en attente…", 0f);
            }

            TryRunPending();
        }

        private void TryRunPending()
        {
            if (IsBusy
                || !_scheduler.TryConsume(
                    IsSafePoint(),
                    out SaveRequestKind kind))
            {
                return;
            }

            IsBusy = true;
            bool saved = false;
            try
            {
                PrototypeSaveRuntime.SaveCurrent();
                _scheduler.MarkSaved();
                HasSaveFailure = false;
                SetStatus("Sauvegarde terminée", 3f);
                saved = true;
            }
            catch (Exception exception)
            {
                HasSaveFailure = true;
                SetStatus(
                    "Échec de la sauvegarde : " + exception.Message,
                    8f);
                Debug.LogException(exception, this);
            }
            finally
            {
                IsBusy = false;
            }

            if (!saved) return;
            if (kind == SaveRequestKind.ReturnToMainMenu)
            {
                GameplayInputGate.SetBlocked(true);
                SceneManager.LoadSceneAsync(FrontendSceneNames.MainMenu);
            }
            else if (kind == SaveRequestKind.Quit)
            {
                _skipQuittingSave = true;
                Application.Quit();
            }
        }

        private static bool IsSafePoint()
        {
            InventoryPrototypeSession session =
                InventoryPrototypeSessionProvider.Current;
            return session.TransferAction == null
                || session.TransferAction.Status
                    != TransferActionStatus.Active;
        }

        private void HandleQuitting()
        {
            if (_skipQuittingSave
                || _loadFailed
                || Application.isEditor
                || !PrototypeSaveRuntime.HasCurrentSlot)
            {
                return;
            }

            try
            {
                InventoryPrototypeSessionProvider.Current
                    .CancelActiveTransferForSaveAndQuit();
                PrototypeSaveRuntime.SaveCurrent();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void CreateStatusOverlay()
        {
            _statusDocument = GetComponent<UIDocument>();
            if (_statusDocument == null)
            {
                _statusDocument = gameObject.AddComponent<UIDocument>();
            }

            _generatedPanelSettings =
                ScriptableObject.CreateInstance<PanelSettings>();
            _generatedPanelSettings.name =
                "Generated Save Status Panel Settings";
            _generatedPanelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>(
                    "InventoryPrototypeTheme");
            _generatedPanelSettings.scaleMode =
                PanelScaleMode.ScaleWithScreenSize;
            _generatedPanelSettings.referenceResolution =
                new Vector2Int(1280, 720);
            _generatedPanelSettings.match = 0.5f;
            _statusDocument.panelSettings = _generatedPanelSettings;
            _statusDocument.sortingOrder = 1200;

            VisualElement root = _statusDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.right = 0f;
            root.style.top = 0f;
            root.style.bottom = 0f;

            _statusLabel = new Label();
            _statusLabel.name = "save-status-overlay";
            _statusLabel.pickingMode = PickingMode.Ignore;
            _statusLabel.style.alignSelf = Align.FlexEnd;
            _statusLabel.style.marginTop = 20f;
            _statusLabel.style.marginRight = 24f;
            _statusLabel.style.paddingLeft = 12f;
            _statusLabel.style.paddingRight = 12f;
            _statusLabel.style.paddingTop = 7f;
            _statusLabel.style.paddingBottom = 7f;
            _statusLabel.style.color = Color.white;
            _statusLabel.style.backgroundColor =
                new Color(0.04f, 0.05f, 0.06f, 0.88f);
            _statusLabel.style.display = DisplayStyle.None;
            root.Add(_statusLabel);
        }

        private void SetStatus(string text, float visibleSeconds)
        {
            StatusText = text ?? string.Empty;
            if (_statusLabel != null)
            {
                _statusLabel.text = StatusText;
                _statusLabel.style.display = string.IsNullOrEmpty(StatusText)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            _clearStatusAt = visibleSeconds > 0f
                ? Time.unscaledTime + visibleSeconds
                : 0f;
        }

        private void ClearExpiredStatus()
        {
            if (_clearStatusAt <= 0f
                || Time.unscaledTime < _clearStatusAt)
            {
                return;
            }

            _clearStatusAt = 0f;
            SetStatus(string.Empty, 0f);
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
