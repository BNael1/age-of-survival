using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Simulation;
using AgeOfSurvival.Runtime.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Tilemaps;

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
        private const int MarkerSizePixels = 24;
        private const float PixelsPerUnit = 64f;

        [SerializeField] private DebugIsometricWorld worldRenderer;
        [SerializeField] private Vector2 startPosition = new Vector2(4.5f, 4.5f);
        [SerializeField, Min(0f)] private float movementSpeed = 3f;
        [SerializeField, Min(1)] private int ticksPerSecond = 60;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 8;
        [SerializeField] private float visualYOffset = 0.18f;

        private FixedTickClock _clock;
        private PlayerState _player;
        private Transform _visual;
        private Texture2D _generatedTexture;
        private Sprite _generatedSprite;

        private Keyboard _keyboard;
        private KeyControl _upKey;
        private KeyControl _leftKey;
        private KeyControl _downKey;
        private KeyControl _rightKey;

        public PlayerState Player => _player;

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

            if (worldRenderer == null || worldRenderer.Tilemap == null)
            {
                Debug.LogError(
                    "DebugPlayerController requires an initialized DebugIsometricWorld.",
                    this);
                enabled = false;
                return;
            }

            _player = new PlayerState(new WorldPosition(startPosition.x, startPosition.y));
            _clock = new FixedTickClock(ticksPerSecond, maxTicksPerFrame);

            CreateVisual();
            SynchronizeVisual();
        }

        private void Update()
        {
            if (_player == null || _clock == null)
            {
                return;
            }

            Vector2 screenDirection = ReadScreenDirection();
            Vector2 worldDirection = ScreenToWorldDirection(screenDirection);
            double tickDuration = _clock.TickDurationSeconds;

            _clock.Advance(
                Time.deltaTime,
                () => PlayerMovement.Step(
                    _player,
                    worldDirection.x,
                    worldDirection.y,
                    movementSpeed,
                    tickDuration));

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

            _generatedTexture = CreateMarkerTexture();
            _generatedSprite = Sprite.Create(
                _generatedTexture,
                new Rect(0f, 0f, MarkerSizePixels, MarkerSizePixels),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _generatedSprite.name = "Generated Debug Player Marker";

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _generatedSprite;
            renderer.sortingOrder = 100;
        }

        private void SynchronizeVisual()
        {
            if (_visual == null || _player == null || worldRenderer == null)
            {
                return;
            }

            Tilemap tilemap = worldRenderer.Tilemap;
            if (tilemap == null)
            {
                return;
            }

            Vector3 origin = tilemap.GetCellCenterWorld(Vector3Int.zero);
            Vector3 xBasis = tilemap.GetCellCenterWorld(Vector3Int.right) - origin;
            Vector3 yBasis = tilemap.GetCellCenterWorld(Vector3Int.up) - origin;
            WorldPosition position = _player.Position;

            Vector3 renderedPosition = origin
                + (xBasis * (float)position.X)
                + (yBasis * (float)position.Y);
            renderedPosition.y += visualYOffset;
            renderedPosition.z = -0.1f;

            _visual.position = renderedPosition;
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
            if (_visual != null)
            {
                DestroyUnityObject(_visual.gameObject);
                _visual = null;
            }

            DestroyUnityObject(_generatedSprite);
            DestroyUnityObject(_generatedTexture);
            _generatedSprite = null;
            _generatedTexture = null;
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
