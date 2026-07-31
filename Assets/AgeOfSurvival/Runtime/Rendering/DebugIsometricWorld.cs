using AgeOfSurvival.Core.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Temporary Unity adapter that renders a Core DenseGrid as an isometric Tilemap.
    /// It generates neutral debug visuals at runtime and contains no production art or gameplay rules.
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
        private Tile _generatedTile;
        private DenseGrid<byte> _world;
        private Tilemap _tilemap;

        public DenseGrid<byte> World => _world;
        public Tilemap Tilemap => _tilemap;

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

            var tilemapObject = new GameObject(TilemapName);
            tilemapObject.transform.SetParent(gridObject.transform, false);

            _tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();

            CreateGeneratedTile();
            PopulateTilemap();
            ConfigureCamera();
        }

        private void OnDestroy()
        {
            DestroyGeneratedAssets();
        }

        private void CreateGeneratedTile()
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

            _generatedTile = ScriptableObject.CreateInstance<Tile>();
            _generatedTile.name = "Generated Debug Tile";
            _generatedTile.sprite = _generatedSprite;
            _generatedTile.colliderType = Tile.ColliderType.None;
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

                    _tilemap.SetTile(cellPosition, _generatedTile);
                    _tilemap.SetTileFlags(cellPosition, TileFlags.None);
                    _tilemap.SetColor(cellPosition, ColorFor(_world[logicalPosition]));
                }
            }

            _tilemap.CompressBounds();
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
            DestroyUnityObject(_generatedTile);
            DestroyUnityObject(_generatedSprite);
            DestroyUnityObject(_generatedTexture);

            _generatedTile = null;
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
