using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Temporary Unity adapter that renders a Core DenseGrid as an isometric Tilemap.
    /// Project-owned prototype sprites improve readability while generated diamonds remain
    /// available as a safe fallback. This adapter contains no gameplay rules.
    /// </summary>
    public sealed class DebugIsometricWorld : MonoBehaviour
    {
        private const string GridRootName = "Debug Isometric Grid";
        private const string TilemapName = "Ground";
        private const int SpriteWidthPixels = 64;
        private const int SpriteHeightPixels = 32;
        private const float PixelsPerUnit = 64f;

        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 10;
        [SerializeField, Min(0f)] private float cameraPadding = 1.25f;
        [SerializeField] private Camera targetCamera;

        private Texture2D _generatedTexture;
        private Sprite _generatedSprite;
        private Sprite _grassSprite;
        private Sprite _dirtSprite;
        private Sprite _waterSprite;
        private Tile _baseTile;
        private Tile _accentTile;
        private Tile _borderTile;
        private DenseGrid<byte> _world;
        private Tilemap _tilemap;

        public DenseGrid<byte> World => _world;
        public Tilemap Tilemap => _tilemap;
        public bool UsesPrototypeVisuals { get; private set; }

        public Vector3 LogicalToWorldPosition(
            WorldPosition logicalPosition,
            float visualYOffset,
            float visualZ)
        {
            if (_tilemap == null)
            {
                return transform.position;
            }

            Vector3 origin = _tilemap.GetCellCenterWorld(Vector3Int.zero);
            Vector3 xBasis = _tilemap.GetCellCenterWorld(Vector3Int.right) - origin;
            Vector3 yBasis = _tilemap.GetCellCenterWorld(Vector3Int.up) - origin;

            Vector3 renderedPosition = origin
                + (xBasis * (float)logicalPosition.X)
                + (yBasis * (float)logicalPosition.Y);
            renderedPosition.y += visualYOffset;
            renderedPosition.z = visualZ;
            return renderedPosition;
        }

        private void Awake()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild Debug Isometric World")]
        public void Rebuild()
        {
            DestroyGeneratedHierarchy();
            DestroyGeneratedAssets();

            _world = DebugWorldPattern.Create(width, height);

            var gridObject = new GameObject(GridRootName);
            gridObject.transform.SetParent(transform, false);

            var unityGrid = gridObject.AddComponent<Grid>();
            unityGrid.cellLayout = GridLayout.CellLayout.Isometric;
            unityGrid.cellSize = new Vector3(1f, 0.5f, 1f);
            unityGrid.cellGap = new Vector3(0f, -1f / 32f, 0f);

            var tilemapObject = new GameObject(TilemapName);
            tilemapObject.transform.SetParent(gridObject.transform, false);

            _tilemap = tilemapObject.AddComponent<Tilemap>();
            var tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            // The source diamonds carry one opaque edge pixel beyond their visual
            // surface. Sort each tile vertically so the one-pixel overlap stays hidden.
            tilemapRenderer.mode = TilemapRenderer.Mode.Individual;
            tilemapRenderer.sortOrder = TilemapRenderer.SortOrder.TopRight;

            CreateTiles();
            PopulateTilemap();
            ConfigureCamera();
        }

        private void OnDestroy()
        {
            DestroyGeneratedAssets();
        }

        private void CreateTiles()
        {
            _grassSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.GroundGrass,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Ground Grass");
            _dirtSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.GroundDirt,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Ground Dirt");
            _waterSprite = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.GroundWater,
                new Vector2(0.5f, 0.5f),
                PrototypeVisualAssets.PixelsPerUnit,
                "Prototype Ground Water");

            UsesPrototypeVisuals = _grassSprite != null
                && _dirtSprite != null
                && _waterSprite != null;

            if (!UsesPrototypeVisuals)
            {
                PrototypeVisualAssets.DestroyRuntimeSprite(_grassSprite);
                PrototypeVisualAssets.DestroyRuntimeSprite(_dirtSprite);
                PrototypeVisualAssets.DestroyRuntimeSprite(_waterSprite);
                _grassSprite = null;
                _dirtSprite = null;
                _waterSprite = null;
                CreateGeneratedFallbackSprite();
                _grassSprite = _generatedSprite;
                _dirtSprite = _generatedSprite;
                _waterSprite = _generatedSprite;
            }

            _baseTile = CreateTile("Prototype Base Ground Tile", _grassSprite);
            _accentTile = CreateTile("Prototype Accent Ground Tile", _dirtSprite);
            _borderTile = CreateTile("Prototype Border Ground Tile", _waterSprite);
        }

        private void CreateGeneratedFallbackSprite()
        {
            _generatedTexture = CreateDiamondTexture();
            _generatedSprite = Sprite.Create(
                _generatedTexture,
                new Rect(0f, 0f, SpriteWidthPixels, SpriteHeightPixels),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _generatedSprite.name = "Generated Debug Diamond";
        }

        private static Tile CreateTile(string name, Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private void PopulateTilemap()
        {
            GridBounds bounds = _world.Bounds;

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    var logicalPosition = new GridPosition(x, y);
                    var cellPosition = new Vector3Int(x, y, 0);
                    byte value = _world[logicalPosition];

                    _tilemap.SetTile(cellPosition, TileFor(value));
                    _tilemap.SetTileFlags(cellPosition, TileFlags.None);
                    _tilemap.SetColor(
                        cellPosition,
                        UsesPrototypeVisuals ? Color.white : ColorFor(value));
                }
            }

            _tilemap.CompressBounds();
        }

        private Tile TileFor(byte value)
        {
            switch (value)
            {
                case DebugWorldPattern.BaseCell:
                    return _baseTile;
                case DebugWorldPattern.AccentCell:
                    return _accentTile;
                case DebugWorldPattern.BorderCell:
                    return _borderTile;
                default:
                    return _baseTile;
            }
        }

        private void ConfigureCamera()
        {
            Camera camera = targetCamera;
            if (camera == null)
            {
                return;
            }

            Vector3 worldCenter = _tilemap.transform.TransformPoint(_tilemap.localBounds.center);
            camera.orthographic = true;
            camera.backgroundColor = new Color32(30, 38, 43, 255);
            camera.transform.position = new Vector3(worldCenter.x, worldCenter.y, -10f);

            float safeAspect = Mathf.Max(camera.aspect, 0.01f);
            float verticalHalfSize = _tilemap.localBounds.extents.y;
            float horizontalHalfSizeAsVertical = _tilemap.localBounds.extents.x / safeAspect;
            camera.orthographicSize = Mathf.Max(verticalHalfSize, horizontalHalfSizeAsVertical) + cameraPadding;
        }

        private static Color ColorFor(byte value)
        {
            switch (value)
            {
                case DebugWorldPattern.BaseCell:
                    return new Color32(153, 166, 139, 255);
                case DebugWorldPattern.AccentCell:
                    return new Color32(161, 139, 111, 255);
                case DebugWorldPattern.BorderCell:
                    return new Color32(95, 128, 142, 255);
                default:
                    return Color.magenta;
            }
        }

        private static Texture2D CreateDiamondTexture()
        {
            var texture = new Texture2D(
                SpriteWidthPixels,
                SpriteHeightPixels,
                TextureFormat.RGBA32,
                false)
            {
                name = "Generated Debug Diamond Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[SpriteWidthPixels * SpriteHeightPixels];
            float halfWidth = SpriteWidthPixels * 0.5f;
            float halfHeight = SpriteHeightPixels * 0.5f;

            for (int y = 0; y < SpriteHeightPixels; y++)
            {
                for (int x = 0; x < SpriteWidthPixels; x++)
                {
                    float normalizedX = Mathf.Abs((x + 0.5f - halfWidth) / halfWidth);
                    float normalizedY = Mathf.Abs((y + 0.5f - halfHeight) / halfHeight);
                    float distance = normalizedX + normalizedY;

                    Color32 color;
                    if (distance > 1f)
                    {
                        color = new Color32(0, 0, 0, 0);
                    }
                    else if (distance > 0.91f)
                    {
                        color = new Color32(185, 185, 185, 255);
                    }
                    else
                    {
                        color = new Color32(255, 255, 255, 255);
                    }

                    pixels[(y * SpriteWidthPixels) + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DestroyGeneratedHierarchy()
        {
            Transform existing = transform.Find(GridRootName);
            if (existing != null)
            {
                DestroyUnityObject(existing.gameObject);
            }

            _tilemap = null;
            _world = null;
        }

        private void DestroyGeneratedAssets()
        {
            DestroyUnityObject(_baseTile);
            DestroyUnityObject(_accentTile);
            DestroyUnityObject(_borderTile);

            if (UsesPrototypeVisuals)
            {
                PrototypeVisualAssets.DestroyRuntimeSprite(_grassSprite);
                PrototypeVisualAssets.DestroyRuntimeSprite(_dirtSprite);
                PrototypeVisualAssets.DestroyRuntimeSprite(_waterSprite);
            }
            else
            {
                DestroyUnityObject(_generatedSprite);
                DestroyUnityObject(_generatedTexture);
            }

            _baseTile = null;
            _accentTile = null;
            _borderTile = null;
            _grassSprite = null;
            _dirtSprite = null;
            _waterSprite = null;
            _generatedSprite = null;
            _generatedTexture = null;
            UsesPrototypeVisuals = false;
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
