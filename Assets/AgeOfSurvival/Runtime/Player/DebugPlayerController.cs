using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Simulation;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AgeOfSurvival.Runtime.Player
{
    /// <summary>
    /// Temporary Unity adapter for the first controllable player marker.
    /// It reads ZQSD through the Input System, advances the pure Core at a fixed tick,
    /// and mirrors the resulting continuous position into the isometric scene.
    /// </summary>
    public sealed class DebugPlayerController : MonoBehaviour
    {
        private const string VisualName = "Debug Player Marker";
        private const string DamageZoneVisualName =
            "Prototype Damage Zone";
        private const int MarkerSizePixels = 24;
        private const float PixelsPerUnit = 64f;
        public const float PlayerVisualScale = 1.2f;
        public const float PlayerGroundPivotX = 0.5f;
        public const float PlayerGroundPivotY = 0.12f;

        [SerializeField] private DebugIsometricWorld worldRenderer;
        [SerializeField] private DebugResourceInteraction resourceInteraction;
        [SerializeField] private GroundAnchorCameraFollow cameraFollow;
        [SerializeField] private Vector2 startPosition = new Vector2(4.5f, 4.5f);
        [SerializeField, Min(0f)] private float movementSpeed = 3f;
        [SerializeField, Min(1)] private int ticksPerSecond = 60;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 8;
        [SerializeField] private float visualYOffset = 0.18f;

        private FixedTickClock _clock;
        private PlayerState _player;
        private Transform _visual;
        private Texture2D _generatedTexture;
        private Sprite _visualSprite;
        private bool _usesPrototypeVisual;
        private GroundAnchorSortCoordinator _sortCoordinator;
        private WorldPosition _respawnPosition;
        private PrototypeDamageZoneState _damageZone;
        private GameObject _damageZoneVisual;
        private Sprite _damageZoneSprite;

        private Keyboard _keyboard;
        private KeyControl _upKey;
        private KeyControl _leftKey;
        private KeyControl _downKey;
        private KeyControl _rightKey;

        public PlayerState Player => _player;
        public Transform VisualGroundAnchor => _visual;
        public SpriteRenderer VisualRenderer { get; private set; }
        public double CurrentLoadRatio { get; private set; }
        public double CurrentMovementMultiplier { get; private set; } = 1.0;
        public WorldPosition RespawnPosition => _respawnPosition;
        public PrototypeDamageZoneState DamageZone => _damageZone;
        public GameObject DamageZoneVisual => _damageZoneVisual;

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
            ResolveKeyboardControls();
        }

        private void Start()
        {
            if (worldRenderer == null)
            {
                worldRenderer = GetComponent<DebugIsometricWorld>();
            }

            if (resourceInteraction == null)
            {
                resourceInteraction = GetComponent<DebugResourceInteraction>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<GroundAnchorCameraFollow>();
            }

            if (worldRenderer == null || worldRenderer.Tilemap == null)
            {
                Debug.LogError(
                    "DebugPlayerController requires an initialized DebugIsometricWorld.",
                    this);
                enabled = false;
                return;
            }

            InventoryPrototypeSession prototypeSession =
                ResolvePrototypeSession();
            WorldPosition fallbackSpawn =
                new WorldPosition(startPosition.x, startPosition.y);
            _respawnPosition = worldRenderer.TryGetGeneratedSpawnPosition(
                out WorldPosition generatedSpawn)
                ? generatedSpawn
                : fallbackSpawn;
            WorldPosition initialPosition = prototypeSession.RestoredFromSave
                ? prototypeSession.CurrentPlayerPosition
                : _respawnPosition;
            _player = new PlayerState(initialPosition);
            _damageZone =
                PrototypeDamageZoneRules.Create(_respawnPosition);
            _clock = new FixedTickClock(ticksPerSecond, maxTicksPerFrame);
            worldRenderer.SynchronizeStreaming(_player.Position);
            SynchronizeMovementState();

            CreateVisual();
            CreateDamageZoneVisual();
            _sortCoordinator = resourceInteraction != null
                ? resourceInteraction.SortCoordinator
                : FindFirstObjectByType<GroundAnchorSortCoordinator>();
            if (_sortCoordinator != null)
            {
                _sortCoordinator.Register(
                    GroundAnchorSortCoordinator.PlayerStableId,
                    _visual,
                    _visual.gameObject,
                    new RendererOrderBinding(VisualRenderer, 1));
            }
            else
            {
                VisualRenderer.sortingOrder = GroundAnchorSorting.OrderForRank(0);
            }

            SynchronizeVisual();
            cameraFollow?.Track(_visual);
            if (!GameplayInputGate.IsBlocked)
            {
                AdvanceSessionTick(
                    prototypeSession,
                    false);
                ResolveHealthStep(prototypeSession);
            }
        }

        private void Update()
        {
            if (_player == null || _clock == null)
            {
                return;
            }

            if (GameplayInputGate.IsBlocked)
            {
                resourceInteraction?.SimulateTick(_player.Position);
                SynchronizeMovementState();
                SynchronizeVisual();
                return;
            }

            Vector2 screenDirection = ReadScreenDirection();
            Vector2 worldDirection = ScreenToWorldDirection(screenDirection);
            double tickDuration = _clock.TickDurationSeconds;

            _clock.Advance(Time.deltaTime, () =>
            {
                InventoryPrototypeSession session = ResolvePrototypeSession();
                EncumbranceMovementState movementState = InventoryMovementStep.Step(
                    _player,
                    session,
                    worldDirection.x,
                    worldDirection.y,
                    movementSpeed,
                    tickDuration);
                CurrentLoadRatio = movementState.LoadRatio;
                CurrentMovementMultiplier = movementState.SpeedMultiplier;
                worldRenderer.SynchronizeStreaming(_player.Position);
                AdvanceSessionTick(
                    session,
                    worldDirection.sqrMagnitude > 0.0001f);
                ResolveHealthStep(session);
            });

            SynchronizeMovementState();
            SynchronizeVisual();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            ClearKeyboardControls();
        }

        private void OnDestroy()
        {
            DestroyGeneratedVisual();
        }

        /// <summary>
        /// Converts screen-oriented ZQSD input into the two axes of the isometric simulation plane.
        /// The Core performs the final magnitude normalization.
        /// </summary>
        public static Vector2 ScreenToWorldDirection(Vector2 screenDirection)
        {
            return new Vector2(
                screenDirection.y + screenDirection.x,
                screenDirection.y - screenDirection.x);
        }

        private InventoryPrototypeSession ResolvePrototypeSession() =>
            resourceInteraction?.PrototypeSession ?? InventoryPrototypeSessionProvider.Current;

        private void AdvanceSessionTick(
            InventoryPrototypeSession session,
            bool playerMoved)
        {
            if (resourceInteraction != null)
            {
                resourceInteraction.SimulateTick(
                    _player.Position,
                    playerMoved);
                return;
            }

            session.BeginSimulationTick(_player.Position);
        }

        private void ResolveHealthStep(
            InventoryPrototypeSession session)
        {
            if (_damageZone == null)
            {
                return;
            }

            PlayerHealthRuntimeResult result =
                PlayerHealthRuntimeStep.Step(
                    _player,
                    session,
                    _respawnPosition,
                    _damageZone);
            if (result.Respawned)
            {
                worldRenderer.SynchronizeStreaming(_player.Position);
            }
        }

        private void SynchronizeMovementState()
        {
            EncumbranceMovementState movementState = ResolvePrototypeSession().MovementState;
            CurrentLoadRatio = movementState.LoadRatio;
            CurrentMovementMultiplier = movementState.SpeedMultiplier;
        }

        private Vector2 ReadScreenDirection()
        {
            if (_keyboard == null || _keyboard != Keyboard.current)
            {
                ResolveKeyboardControls();
            }

            float horizontal = ReadPressed(_rightKey) - ReadPressed(_leftKey);
            float vertical = ReadPressed(_upKey) - ReadPressed(_downKey);
            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void ResolveKeyboardControls()
        {
            _keyboard = Keyboard.current;
            if (_keyboard == null)
            {
                ClearKeyboardControls();
                return;
            }

            _upKey = FindCharacterKey(_keyboard, "z", _keyboard.zKey);
            _leftKey = FindCharacterKey(_keyboard, "q", _keyboard.qKey);
            _downKey = FindCharacterKey(_keyboard, "s", _keyboard.sKey);
            _rightKey = FindCharacterKey(_keyboard, "d", _keyboard.dKey);
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
                    ResolveKeyboardControls();
                    break;
            }
        }

        private static KeyControl FindCharacterKey(
            Keyboard keyboard,
            string character,
            KeyControl fallback)
        {
            return keyboard.FindKeyOnCurrentKeyboardLayout(character) ?? fallback;
        }

        private static float ReadPressed(ButtonControl key)
        {
            return key != null && key.isPressed ? 1f : 0f;
        }

        private void ClearKeyboardControls()
        {
            _keyboard = null;
            _upKey = null;
            _leftKey = null;
            _downKey = null;
            _rightKey = null;
        }

        private void CreateVisual()
        {
            var visualObject = new GameObject(VisualName);
            visualObject.transform.SetParent(transform, false);
            _visual = visualObject.transform;

            _visualSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.PlayerSurvivor,
                new Vector2(PlayerGroundPivotX, PlayerGroundPivotY),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Survivor");
            _usesPrototypeVisual = _visualSprite != null;

            if (!_usesPrototypeVisual)
            {
                _generatedTexture = CreateMarkerTexture();
                _visualSprite = Sprite.Create(
                    _generatedTexture,
                    new Rect(0f, 0f, MarkerSizePixels, MarkerSizePixels),
                    new Vector2(PlayerGroundPivotX, PlayerGroundPivotY),
                    PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                _visualSprite.name = "Generated Debug Player Marker";
            }

            _visual.localScale = new Vector3(
                PlayerVisualScale,
                PlayerVisualScale,
                1f);
            VisualRenderer = visualObject.AddComponent<SpriteRenderer>();
            VisualRenderer.sprite = _visualSprite;
            VisualRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        private void CreateDamageZoneVisual()
        {
            _damageZoneSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.TargetRing,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Damage Zone Ring");
            if (_damageZoneSprite == null)
            {
                Debug.LogError(
                    "The prototype damage-zone sprite could not be created.",
                    this);
                return;
            }

            _damageZoneVisual = new GameObject(DamageZoneVisualName);
            _damageZoneVisual.transform.SetParent(transform, false);
            var renderer =
                _damageZoneVisual.AddComponent<SpriteRenderer>();
            renderer.sprite = _damageZoneSprite;
            renderer.color = new Color32(220, 48, 44, 220);
            renderer.sortingOrder = 70;

            float spriteWidth = Mathf.Max(
                0.001f,
                _damageZoneSprite.bounds.size.x);
            float diameter = (float)(_damageZone.Radius * 2.0);
            float scale = diameter / spriteWidth;
            _damageZoneVisual.transform.localScale =
                new Vector3(scale, scale, 1f);
            _damageZoneVisual.transform.position =
                worldRenderer.LogicalToWorldPosition(
                    _damageZone.Center,
                    0.01f,
                    -0.03f);
        }

        private void SynchronizeVisual()
        {
            if (_visual == null || _player == null || worldRenderer == null)
            {
                return;
            }

            _visual.position = worldRenderer.LogicalToWorldPosition(
                _player.Position,
                visualYOffset,
                -0.1f);
        }

        private static Texture2D CreateMarkerTexture()
        {
            var texture = new Texture2D(
                MarkerSizePixels,
                MarkerSizePixels,
                TextureFormat.RGBA32,
                false)
            {
                name = "Generated Debug Player Marker Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[MarkerSizePixels * MarkerSizePixels];
            float center = (MarkerSizePixels - 1) * 0.5f;
            float outerRadius = MarkerSizePixels * 0.42f;
            float innerRadius = outerRadius - 2f;
            float outerRadiusSquared = outerRadius * outerRadius;
            float innerRadiusSquared = innerRadius * innerRadius;

            for (int y = 0; y < MarkerSizePixels; y++)
            {
                for (int x = 0; x < MarkerSizePixels; x++)
                {
                    float deltaX = x - center;
                    float deltaY = y - center;
                    float distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);

                    Color32 color;
                    if (distanceSquared > outerRadiusSquared)
                    {
                        color = new Color32(0, 0, 0, 0);
                    }
                    else if (distanceSquared > innerRadiusSquared)
                    {
                        color = new Color32(45, 52, 55, 255);
                    }
                    else
                    {
                        color = new Color32(230, 218, 157, 255);
                    }

                    pixels[(y * MarkerSizePixels) + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DestroyGeneratedVisual()
        {
            if (_sortCoordinator != null)
            {
                _sortCoordinator.Unregister(GroundAnchorSortCoordinator.PlayerStableId);
                _sortCoordinator = null;
            }

            if (_visual != null)
            {
                DestroyUnityObject(_visual.gameObject);
                _visual = null;
                VisualRenderer = null;
            }

            if (_usesPrototypeVisual)
            {
                PrototypeVisualAssets.DestroyRuntimeSprite(_visualSprite);
            }
            else
            {
                DestroyUnityObject(_visualSprite);
                DestroyUnityObject(_generatedTexture);
            }

            if (_damageZoneVisual != null)
            {
                DestroyUnityObject(_damageZoneVisual);
                _damageZoneVisual = null;
            }

            PrototypeVisualAssets.DestroyRuntimeSprite(
                _damageZoneSprite);
            _damageZoneSprite = null;
            _damageZone = null;

            _visualSprite = null;
            _generatedTexture = null;
            _usesPrototypeVisual = false;
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
    }
}
