using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
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
    public sealed class DebugResourceInteraction : MonoBehaviour
    {
        private const string GeneratedRootName = "Debug Resource Markers";
        private const int MarkerSizePixels = 24;
        private const int IndicatorSizePixels = 32;
        private const float PixelsPerUnit = 64f;
        private static readonly Vector2[] DefaultResourcePositions =
        {
            new Vector2(5.25f, 4.5f),
            new Vector2(4.5f, 5.75f),
            new Vector2(3.25f, 4.5f)
        };

        [SerializeField] private DebugIsometricWorld worldRenderer;
        [SerializeField, Min(0f)] private float interactionRadius = 1.5f;
        [SerializeField] private float visualYOffset = 0.14f;
        [SerializeField] private Vector2[] resourcePositions;

        private readonly List<ResourceState> _resources = new List<ResourceState>();
        private readonly List<ResourceMarker> _markers = new List<ResourceMarker>();

        private Transform _generatedRoot;
        private Texture2D _bodyTexture;
        private Texture2D _indicatorTexture;
        private Sprite _bodySprite;
        private Sprite _indicatorSprite;
        private Keyboard _keyboard;
        private KeyControl _interactionKey;
        private bool _interactionRequested;
        private bool _inputSubscribed;

        public IReadOnlyList<ResourceState> Resources => _resources;
        public ResourceId? CurrentTargetId { get; private set; }
        public ResourceInteractionResult? LastInteractionResult { get; private set; }
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

        public GameObject GeneratedRoot => _generatedRoot != null
            ? _generatedRoot.gameObject
            : null;
        public Sprite GeneratedBodySprite => _bodySprite;
        public Sprite GeneratedIndicatorSprite => _indicatorSprite;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Rebuild();
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
            DestroyGeneratedHierarchy();
            DestroyGeneratedAssets();
        }

        [ContextMenu("Rebuild Debug Resources")]
        public void Rebuild()
        {
            DestroyGeneratedHierarchy();
            DestroyGeneratedAssets();
            _resources.Clear();
            _markers.Clear();
            _interactionRequested = false;
            CurrentTargetId = null;
            LastInteractionResult = null;

            if (worldRenderer == null)
            {
                worldRenderer = GetComponent<DebugIsometricWorld>();
            }

            BuildCoreResources();
            CreateGeneratedAssets();
            CreateMarkers();
            SynchronizeVisuals(null);
        }

        public void QueueInteraction()
        {
            _interactionRequested = true;
        }

        /// <summary>
        /// Called by the player adapter from its fixed-tick callback.
        /// Tests call this directly so no physical keyboard is required.
        /// </summary>
        public void SimulateTick(WorldPosition playerPosition)
        {
            if (_interactionRequested)
            {
                _interactionRequested = false;
                LastInteractionResult = ResourceInteraction.Apply(
                    new ResourceInteractionCommand(),
                    _resources,
                    playerPosition,
                    interactionRadius);
            }

            ResourceState target = ResourceTargeting.FindNearestAvailable(
                _resources,
                playerPosition,
                interactionRadius);
            CurrentTargetId = target != null ? target.Id : (ResourceId?)null;
            SynchronizeVisuals(target);
        }

        private void BuildCoreResources()
        {
            Vector2[] positions = resourcePositions != null
                && resourcePositions.Length > 0
                    ? resourcePositions
                    : DefaultResourcePositions;

            for (int index = 0; index < positions.Length; index++)
            {
                Vector2 position = positions[index];
                _resources.Add(new ResourceState(
                    new ResourceId($"debug-resource-{index + 1:00}"),
                    new WorldPosition(position.x, position.y)));
            }
        }

        private void CreateGeneratedAssets()
        {
            _bodyTexture = CreateDiamondTexture(
                MarkerSizePixels,
                new Color32(55, 83, 61, 255),
                new Color32(127, 174, 101, 255));
            _bodyTexture.name = "Generated Debug Resource Texture";
            _bodySprite = CreateSprite(
                _bodyTexture,
                MarkerSizePixels,
                "Generated Debug Resource Sprite");

            _indicatorTexture = CreateDiamondOutlineTexture(
                IndicatorSizePixels,
                new Color32(255, 221, 87, 255));
            _indicatorTexture.name = "Generated Debug Resource Target Texture";
            _indicatorSprite = CreateSprite(
                _indicatorTexture,
                IndicatorSizePixels,
                "Generated Debug Resource Target Sprite");
        }

        private void CreateMarkers()
        {
            var rootObject = new GameObject(GeneratedRootName);
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;

            for (int index = 0; index < _resources.Count; index++)
            {
                ResourceState resource = _resources[index];
                var markerObject = new GameObject($"Resource {resource.Id}");
                markerObject.transform.SetParent(_generatedRoot, false);

                var bodyRenderer = markerObject.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = _bodySprite;
                bodyRenderer.sortingOrder = 90;

                var targetObject = new GameObject("Target Indicator");
                targetObject.transform.SetParent(markerObject.transform, false);
                var targetRenderer = targetObject.AddComponent<SpriteRenderer>();
                targetRenderer.sprite = _indicatorSprite;
                targetRenderer.sortingOrder = 89;
                targetObject.SetActive(false);

                _markers.Add(new ResourceMarker(
                    resource,
                    markerObject,
                    targetObject));
            }
        }

        private void SynchronizeVisuals(ResourceState target)
        {
            for (int index = 0; index < _markers.Count; index++)
            {
                ResourceMarker marker = _markers[index];
                bool available =
                    marker.Resource.Availability == ResourceAvailability.Available;
                marker.Root.SetActive(available);
                marker.TargetIndicator.SetActive(
                    available && ReferenceEquals(marker.Resource, target));

                if (available && worldRenderer != null)
                {
                    marker.Root.transform.position =
                        worldRenderer.LogicalToWorldPosition(
                            marker.Resource.Position,
                            visualYOffset,
                            -0.05f);
                }
            }
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

            _markers.Clear();
        }

        private void DestroyGeneratedAssets()
        {
            DestroyUnityObject(_bodySprite);
            DestroyUnityObject(_indicatorSprite);
            DestroyUnityObject(_bodyTexture);
            DestroyUnityObject(_indicatorTexture);
            _bodySprite = null;
            _indicatorSprite = null;
            _bodyTexture = null;
            _indicatorTexture = null;
        }

        private static Sprite CreateSprite(
            Texture2D texture,
            int size,
            string spriteName)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            return sprite;
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
                GameObject targetIndicator)
            {
                Resource = resource;
                Root = root;
                TargetIndicator = targetIndicator;
            }

            public ResourceState Resource { get; }
            public GameObject Root { get; }
            public GameObject TargetIndicator { get; }
        }
    }
}
