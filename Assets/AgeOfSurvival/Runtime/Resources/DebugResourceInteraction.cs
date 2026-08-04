using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AgeOfSurvival.Runtime.Resources
{
    /// <summary>
    /// Temporary Unity adapter for resource input and presentation.
    /// The Core owns target selection and harvesting; this component only queues E,
    /// invokes the command on a fixed simulation tick, and mirrors Core state.
    /// </summary>
    // ExecuteAlways is limited to EditMode cleanup of generated assets.
    // Automatic construction, input subscription and Update remain Play Mode-only.
    [ExecuteAlways]
    [DefaultExecutionOrder(-10)]
    public sealed partial class DebugResourceInteraction : MonoBehaviour
    {
        private const string GeneratedRootName = "Debug Resource Markers";
        private const int MarkerSizePixels = 24;
        private const int IndicatorSizePixels = 32;
        private const float PixelsPerUnit = 64f;
        private const float DefaultRadiusArtwork = 1.5f;
        private const float ProgressFullScaleX = 8f;
        public const float ResourceGroundPivotX = 0.5f;
        public const float ResourceGroundPivotY = 0.12f;
        public const float GroundPilePivotY = 0.2f;

        [SerializeField] private DebugIsometricWorld worldRenderer;
        [SerializeField, Min(0f)] private float interactionRadius = 1.5f;
        [SerializeField] private float visualYOffset = 0.14f;

        private readonly List<ResourceMarker> _markers = new List<ResourceMarker>();
        private InventoryPrototypeSession _session;
        private GroundAnchorSortCoordinator _sortCoordinator;
        private Transform _generatedRoot;
        private GameObject _interactionRadiusObject;
        private Texture2D _bodyTexture;
        private Texture2D _indicatorTexture;
        private Sprite _resourceSprite;
        private Sprite _groundSprite;
        private Sprite _indicatorSprite;
        private Sprite _radiusSprite;
        private Sprite _uiSprite;
        private Keyboard _keyboard;
        private KeyControl _interactionKey;
        private bool _interactionRequested;
        private bool _inputSubscribed;
        private bool _hasPlayerPosition;

        public IReadOnlyList<ResourceState> Resources => _session?.Resources
            ?? (IReadOnlyList<ResourceState>)System.Array.Empty<ResourceState>();
        public IReadOnlyList<GroundContainerState> GroundContainers => _session?.GroundContainers
            ?? (IReadOnlyList<GroundContainerState>)System.Array.Empty<GroundContainerState>();
        public TransferActionState TransferAction => _session?.TransferAction;
        public InventoryPrototypeSession PrototypeSession => _session;
        public ResourceId? CurrentTargetId { get; private set; }
        public ResourceInteractionResult? LastInteractionResult { get; private set; }
        public bool UsesPrototypeVisuals { get; private set; }
        public bool InteractionRadiusVisible => _interactionRadiusObject != null
            && _interactionRadiusObject.activeSelf;
        public int MarkerCount => _markers.Count;
        public int RenderedMarkerCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _markers.Count; index++)
                {
                    if (_markers[index].Root.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ActiveTargetIndicatorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _markers.Count; index++)
                {
                    if (_markers[index].TargetIndicator.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ActiveTransferProgressCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _markers.Count; index++)
                {
                    if (_markers[index].ProgressRoot.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int RenderedGroundPileCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _markers.Count; index++)
                {
                    ResourceMarker marker = _markers[index];
                    if (marker.Root.activeSelf && marker.BodyRenderer.sprite == _groundSprite)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public GameObject GeneratedRoot => _generatedRoot != null
            ? _generatedRoot.gameObject
            : null;
        public Sprite GeneratedBodySprite => _resourceSprite;
        public Sprite GeneratedGroundSprite => _groundSprite;
        public Sprite GeneratedIndicatorSprite => _indicatorSprite;
        public GroundAnchorSortCoordinator SortCoordinator => ResolveSortCoordinator();

        public bool TryGetVisualGroundAnchor(ResourceId id, out Vector3 anchor)
        {
            for (int index = 0; index < _markers.Count; index++)
            {
                ResourceMarker marker = _markers[index];
                if (marker.Resource.Id.Equals(id))
                {
                    anchor = marker.Root.transform.position;
                    return true;
                }
            }

            anchor = default;
            return false;
        }

        public bool TryGetSortingOrder(ResourceId id, out int sortingOrder)
        {
            for (int index = 0; index < _markers.Count; index++)
            {
                ResourceMarker marker = _markers[index];
                if (marker.Resource.Id.Equals(id))
                {
                    sortingOrder = marker.BodyRenderer.sortingOrder;
                    return true;
                }
            }

            sortingOrder = default;
            return false;
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                ResolveSortCoordinator();
                Rebuild();
                AttachChunkStreaming();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            InputSystem.onDeviceChange += HandleDeviceChange;
            _inputSubscribed = true;
            ResolveInteractionKey();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (_keyboard == null || _keyboard != Keyboard.current)
            {
                ResolveInteractionKey();
            }

            if (_interactionKey != null && _interactionKey.wasPressedThisFrame)
            {
                QueueInteraction();
            }
        }

        private void OnDisable()
        {
            if (_inputSubscribed)
            {
                InputSystem.onDeviceChange -= HandleDeviceChange;
                _inputSubscribed = false;
            }

            ClearInteractionKey();
        }

        private void OnDestroy()
        {
            DetachChunkStreaming();
            DestroyGeneratedHierarchy();
            DestroyGeneratedAssets();
        }

        [ContextMenu("Rebuild Debug Resources")]
        public void Rebuild()
        {
            DestroyGeneratedHierarchy();
            DestroyGeneratedAssets();
            _markers.Clear();
            _interactionRequested = false;
            _hasPlayerPosition = false;
            CurrentTargetId = null;
            LastInteractionResult = null;
            if (worldRenderer == null)
            {
                worldRenderer = GetComponent<DebugIsometricWorld>();
            }

            IReadOnlyList<ResourceState> generatedResources = worldRenderer != null
                && worldRenderer.UsesGeneratedPopulation
                ? worldRenderer.CreateGeneratedResourceStates()
                : null;
            if (generatedResources != null)
            {
                _session = Application.isPlaying
                    ? InventoryPrototypeSessionProvider.ConfigureResources(generatedResources)
                    : new InventoryPrototypeSession(generatedResources);
            }
            else
            {
                _session = Application.isPlaying
                    ? InventoryPrototypeSessionProvider.Current
                    : new InventoryPrototypeSession();
            }

            CreateVisualAssets();
            CreateMarkers();
            SynchronizeVisuals(null);
        }

        public void QueueInteraction()
        {
            if (!GameplayInputGate.IsBlocked)
            {
                _interactionRequested = true;
            }
        }

        /// <summary>
        /// Called by the player adapter from its fixed-tick callback.
        /// Tests call this directly so no physical keyboard is required.
        /// </summary>
        public void SimulateTick(WorldPosition playerPosition)
        {
            SimulateTick(playerPosition, false);
        }

        public void SimulateTick(WorldPosition playerPosition, bool playerMoved)
        {
            if (GameplayInputGate.IsBlocked)
            {
                _interactionRequested = false;
                return;
            }

            long simulationTick = _session.BeginSimulationTick(playerPosition);
            _hasPlayerPosition = true;
            SynchronizeInteractionRadius(playerPosition);

            if (_interactionRequested)
            {
                _interactionRequested = false;
                ResourceYieldResult yield = _session.HarvestAndStartTransfer(
                    playerPosition,
                    interactionRadius,
                    simulationTick);
                LastInteractionResult = yield.Interaction;
            }

            if (_session.TransferAction != null
                && _session.TransferAction.Status == TransferActionStatus.Active)
            {
                _session.AdvanceTransfer(simulationTick, playerPosition, playerMoved);
            }

            ResourceState target = ResourceTargeting.FindNearestAvailable(
                _session.Resources,
                playerPosition,
                interactionRadius);
            CurrentTargetId = target != null ? target.Id : (ResourceId?)null;
            SynchronizeVisuals(target);
        }

        private void CreateVisualAssets()
        {
            _resourceSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.ResourceShrub,
                new Vector2(ResourceGroundPivotX, ResourceGroundPivotY),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Resource Shrub");
            _groundSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.GroundBranches,
                new Vector2(ResourceGroundPivotX, GroundPilePivotY),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Ground Branches");
            _indicatorSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.TargetRing,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Target Ring");
            _radiusSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.InteractionRadius,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Interaction Radius");
            _uiSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.UiPixel,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype UI Pixel");

            UsesPrototypeVisuals = _resourceSprite != null
                && _groundSprite != null
                && _indicatorSprite != null
                && _radiusSprite != null
                && _uiSprite != null;

            if (UsesPrototypeVisuals)
            {
                return;
            }

            DestroyPrototypeSprites();
            _bodyTexture = CreateDiamondTexture(
                MarkerSizePixels,
                new Color32(55, 83, 61, 255),
                new Color32(127, 174, 101, 255));
            _bodyTexture.name = "Generated Debug Resource Texture";
            CreateFallbackBodySprites(
                _bodyTexture,
                out _resourceSprite,
                out _groundSprite);

            _indicatorTexture = CreateDiamondOutlineTexture(
                IndicatorSizePixels,
                new Color32(255, 221, 87, 255));
            _indicatorTexture.name = "Generated Debug Resource Target Texture";
            _indicatorSprite = CreateSprite(
                _indicatorTexture,
                IndicatorSizePixels,
                "Generated Debug Resource Target Sprite",
                new Vector2(0.5f, 0.5f));
        }

        private void CreateMarkers()
        {
            var rootObject = new GameObject(GeneratedRootName);
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;

            CreateInteractionRadius();

            for (int index = 0; index < _session.Resources.Count; index++)
            {
                ResourceState resource = _session.Resources[index];
                if (worldRenderer != null
                    && worldRenderer.UsesChunkStreaming
                    && !worldRenderer.IsWorldPositionVisible(resource.Position))
                {
                    continue;
                }

                var markerObject = new GameObject($"Resource {resource.Id}");
                markerObject.transform.SetParent(_generatedRoot, false);

                var bodyRenderer = markerObject.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = _resourceSprite;
                bodyRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

                var targetObject = new GameObject("Target Indicator");
                targetObject.transform.SetParent(markerObject.transform, false);
                targetObject.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                var targetRenderer = targetObject.AddComponent<SpriteRenderer>();
                targetRenderer.sprite = _indicatorSprite;
                targetObject.SetActive(false);

                var quantityObject = new GameObject("Ground Quantity");
                quantityObject.transform.SetParent(markerObject.transform, false);
                quantityObject.transform.localPosition = new Vector3(0.24f, 0.18f, 0f);
                var quantityLabel = quantityObject.AddComponent<TextMesh>();
                quantityLabel.fontSize = 30;
                quantityLabel.fontStyle = FontStyle.Bold;
                quantityLabel.characterSize = 0.032f;
                quantityLabel.color = new Color32(250, 244, 215, 255);
                quantityLabel.anchor = TextAnchor.MiddleLeft;
                MeshRenderer quantityRenderer = quantityLabel.GetComponent<MeshRenderer>();

                CreateProgressBar(
                    markerObject.transform,
                    out GameObject progressRoot,
                    out SpriteRenderer progressBackground,
                    out SpriteRenderer progressFill);

                var marker = new ResourceMarker(
                    resource,
                    markerObject,
                    bodyRenderer,
                    targetObject,
                    targetRenderer,
                    quantityLabel,
                    quantityRenderer,
                    progressRoot,
                    progressBackground,
                    progressFill);
                _markers.Add(marker);
                ResolveSortCoordinator().Register(
                    resource.Id.Value,
                    markerObject.transform,
                    markerObject,
                    new RendererOrderBinding(targetRenderer, 0),
                    new RendererOrderBinding(bodyRenderer, 1),
                    new RendererOrderBinding(quantityRenderer, 5),
                    new RendererOrderBinding(progressBackground, 6),
                    new RendererOrderBinding(progressFill, 7));
            }
        }

        private void CreateInteractionRadius()
        {
            _interactionRadiusObject = new GameObject("Interaction Radius");
            _interactionRadiusObject.transform.SetParent(_generatedRoot, false);
            var renderer = _interactionRadiusObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _radiusSprite;
            renderer.sortingOrder = 80;

            float scale = interactionRadius <= 0f
                ? 0f
                : interactionRadius / DefaultRadiusArtwork * (4f / 3f);
            _interactionRadiusObject.transform.localScale = new Vector3(scale, scale, 1f);
            _interactionRadiusObject.SetActive(false);
        }

        private void CreateProgressBar(
            Transform marker,
            out GameObject progressRoot,
            out SpriteRenderer progressBackground,
            out SpriteRenderer progressFill)
        {
            progressRoot = new GameObject("Transfer Progress");
            progressRoot.transform.SetParent(marker, false);
            progressRoot.transform.localPosition = new Vector3(0f, 0.72f, 0f);

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(progressRoot.transform, false);
            progressBackground = backgroundObject.AddComponent<SpriteRenderer>();
            progressBackground.sprite = _uiSprite;
            progressBackground.color = new Color32(24, 29, 31, 230);
            backgroundObject.transform.localScale = new Vector3(
                ProgressFullScaleX + 0.8f,
                1.6f,
                1f);

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(progressRoot.transform, false);
            progressFill = fillObject.AddComponent<SpriteRenderer>();
            progressFill.sprite = _uiSprite;
            progressFill.color = new Color32(235, 188, 79, 255);
            progressFill.sortingOrder = 96;
            SetProgress(progressFill, 0f);
            progressRoot.SetActive(false);
        }

        private void SynchronizeInteractionRadius(WorldPosition playerPosition)
        {
            if (_interactionRadiusObject == null || worldRenderer == null)
            {
                return;
            }

            _interactionRadiusObject.SetActive(
                _hasPlayerPosition && _radiusSprite != null && interactionRadius > 0f);
            _interactionRadiusObject.transform.position =
                worldRenderer.LogicalToWorldPosition(playerPosition, 0f, -0.01f);
        }

        private void SynchronizeVisuals(ResourceState target)
        {
            TransferActionState action = _session.TransferAction;

            for (int index = 0; index < _markers.Count; index++)
            {
                ResourceMarker marker = _markers[index];
                bool available = marker.Resource.Availability == ResourceAvailability.Available;
                GroundContainerState ground = FindGroundFor(marker.Resource);
                int groundQuantity = ground == null ? 0 : InventoryOperations.Count(
                    ground.Container,
                    InventoryPrototypeCatalog.Branches.Id);
                bool visible = available || groundQuantity > 0;
                marker.Root.SetActive(visible);
                marker.BodyRenderer.sprite = available ? _resourceSprite : _groundSprite;
                marker.TargetIndicator.SetActive(
                    available && ReferenceEquals(marker.Resource, target));
                marker.QuantityLabel.gameObject.SetActive(groundQuantity > 0);
                marker.QuantityLabel.text = groundQuantity > 0 ? $"x{groundQuantity}" : string.Empty;

                bool activeTransferSource = visible
                    && ground != null
                    && action != null
                    && action.Status == TransferActionStatus.Active
                    && ground.Container.Id.Equals(action.SourceId);
                marker.ProgressRoot.SetActive(activeTransferSource && _uiSprite != null);
                if (activeTransferSource)
                {
                    SetProgress(marker.ProgressFill, (float)action.ProgressAt(_session.CurrentTick));
                }

                if (visible && worldRenderer != null)
                {
                    marker.Root.transform.position =
                        worldRenderer.LogicalToWorldPosition(
                            marker.Resource.Position,
                            visualYOffset,
                            -0.05f);
                }
            }

        }

        private static void SetProgress(SpriteRenderer fill, float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            float visibleProgress = Mathf.Max(0.02f, clamped);
            fill.transform.localScale = new Vector3(
                ProgressFullScaleX * visibleProgress,
                1f,
                1f);
            fill.transform.localPosition = new Vector3(
                -0.25f + (0.25f * visibleProgress),
                0f,
                0f);
        }

        private GroundContainerState FindGroundFor(ResourceState resource)
        {
            GroundContainerId expectedId = ResourceYieldOperations.GroundIdFor(resource.Id);
            for (int index = 0; index < _session.GroundContainers.Count; index++)
            {
                GroundContainerState ground = _session.GroundContainers[index];
                if (ground.Id.Equals(expectedId))
                {
                    return ground;
                }
            }

            return null;
        }

        private void ResolveInteractionKey()
        {
            _keyboard = Keyboard.current;
            _interactionKey = _keyboard != null
                ? _keyboard.FindKeyOnCurrentKeyboardLayout("e") ?? _keyboard.eKey
                : null;
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Keyboard))
            {
                return;
            }

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Removed:
                    ResolveInteractionKey();
                    break;
            }
        }

        private void ClearInteractionKey()
        {
            _keyboard = null;
            _interactionKey = null;
        }

        private void DestroyGeneratedHierarchy()
        {
            if (_sortCoordinator != null)
            {
                for (int index = 0; index < _markers.Count; index++)
                {
                    _sortCoordinator.Unregister(_markers[index].Resource.Id.Value);
                }
            }

            if (_generatedRoot != null)
            {
                DestroyUnityObject(_generatedRoot.gameObject);
                _generatedRoot = null;
            }

            Transform staleRoot = transform.Find(GeneratedRootName);
            if (staleRoot != null)
            {
                DestroyUnityObject(staleRoot.gameObject);
            }

            _interactionRadiusObject = null;
            _markers.Clear();
        }

        private void DestroyGeneratedAssets()
        {
            if (UsesPrototypeVisuals)
            {
                DestroyPrototypeSprites();
            }
            else
            {
                DestroyUnityObject(_resourceSprite);
                DestroyUnityObject(_groundSprite);
                DestroyUnityObject(_indicatorSprite);
                DestroyUnityObject(_bodyTexture);
                DestroyUnityObject(_indicatorTexture);
                _resourceSprite = null;
                _groundSprite = null;
                _indicatorSprite = null;
            }

            _radiusSprite = null;
            _uiSprite = null;
            _bodyTexture = null;
            _indicatorTexture = null;
            UsesPrototypeVisuals = false;
        }

        private void DestroyPrototypeSprites()
        {
            PrototypeVisualAssets.DestroyRuntimeSprite(_resourceSprite);
            PrototypeVisualAssets.DestroyRuntimeSprite(_groundSprite);
            PrototypeVisualAssets.DestroyRuntimeSprite(_indicatorSprite);
            PrototypeVisualAssets.DestroyRuntimeSprite(_radiusSprite);
            PrototypeVisualAssets.DestroyRuntimeSprite(_uiSprite);
            _resourceSprite = null;
            _groundSprite = null;
            _indicatorSprite = null;
            _radiusSprite = null;
            _uiSprite = null;
        }

        private static Sprite CreateSprite(
            Texture2D texture,
            int size,
            string spriteName,
            Vector2 pivot)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                pivot,
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            return sprite;
        }

        public static void CreateFallbackBodySprites(
            Texture2D sharedTexture,
            out Sprite resourceSprite,
            out Sprite groundSprite)
        {
            if (sharedTexture == null)
            {
                throw new System.ArgumentNullException(nameof(sharedTexture));
            }

            resourceSprite = CreateSprite(
                sharedTexture,
                sharedTexture.width,
                "Generated Debug Resource Sprite",
                new Vector2(ResourceGroundPivotX, ResourceGroundPivotY));
            groundSprite = CreateSprite(
                sharedTexture,
                sharedTexture.width,
                "Generated Debug Ground Pile Sprite",
                new Vector2(ResourceGroundPivotX, GroundPilePivotY));
        }

        private GroundAnchorSortCoordinator ResolveSortCoordinator()
        {
            if (_sortCoordinator == null)
            {
                _sortCoordinator = GetComponent<GroundAnchorSortCoordinator>();
                if (_sortCoordinator == null)
                {
                    _sortCoordinator = gameObject.AddComponent<GroundAnchorSortCoordinator>();
                }
            }

            return _sortCoordinator;
        }

        private static Texture2D CreateDiamondTexture(
            int size,
            Color32 border,
            Color32 fill)
        {
            var texture = CreateTexture(size);
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float diamondDistance =
                        Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    Color32 color = new Color32(0, 0, 0, 0);
                    if (diamondDistance <= radius)
                    {
                        color = diamondDistance >= radius - 2f ? border : fill;
                    }

                    pixels[(y * size) + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateDiamondOutlineTexture(int size, Color32 color)
        {
            var texture = CreateTexture(size);
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.43f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float diamondDistance =
                        Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    pixels[(y * size) + x] =
                        diamondDistance <= radius && diamondDistance >= radius - 2f
                            ? color
                            : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private sealed class ResourceMarker
        {
            public ResourceMarker(
                ResourceState resource,
                GameObject root,
                SpriteRenderer bodyRenderer,
                GameObject targetIndicator,
                SpriteRenderer targetRenderer,
                TextMesh quantityLabel,
                MeshRenderer quantityRenderer,
                GameObject progressRoot,
                SpriteRenderer progressBackground,
                SpriteRenderer progressFill)
            {
                Resource = resource;
                Root = root;
                BodyRenderer = bodyRenderer;
                TargetIndicator = targetIndicator;
                TargetRenderer = targetRenderer;
                QuantityLabel = quantityLabel;
                QuantityRenderer = quantityRenderer;
                ProgressRoot = progressRoot;
                ProgressBackground = progressBackground;
                ProgressFill = progressFill;
            }

            public ResourceState Resource { get; }
            public GameObject Root { get; }
            public SpriteRenderer BodyRenderer { get; }
            public GameObject TargetIndicator { get; }
            public SpriteRenderer TargetRenderer { get; }
            public TextMesh QuantityLabel { get; }
            public MeshRenderer QuantityRenderer { get; }
            public GameObject ProgressRoot { get; }
            public SpriteRenderer ProgressBackground { get; }
            public SpriteRenderer ProgressFill { get; }
        }

    }
}
