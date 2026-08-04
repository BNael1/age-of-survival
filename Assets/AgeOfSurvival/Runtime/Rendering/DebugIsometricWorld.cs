using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Temporary Unity adapter that renders either the legacy debug pattern or one
    /// immutable generated population chunk as an isometric Tilemap. It owns no mutable
    /// simulation state and contains no gameplay rules.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed partial class DebugIsometricWorld : MonoBehaviour
    {
        private const string GridRootName = "Debug Isometric Grid";
        private const string TilemapName = "Ground";
        private const int SpriteWidthPixels = 64;
        private const int SpriteHeightPixels = 32;
        private const float PixelsPerUnit = 64f;

        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 10;
        [SerializeField] private bool useGeneratedPopulation;
        [SerializeField] private string worldSeed = "0";
        [SerializeField] private long chunkX;
        [SerializeField] private long chunkY;

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
        private PopulatedChunk _populationChunk;
        private GeneratedSpawnPoint? _spawnPoint;
        private WorldCellCoordinate _renderedWorldOrigin;

        public DenseGrid<byte> World => _world;
        public Tilemap Tilemap => _tilemap;
        public bool UsesPrototypeVisuals { get; private set; }
        public bool UsesGeneratedPopulation => useGeneratedPopulation && _populationChunk != null;
        public PopulatedChunk PopulationChunk => _populationChunk;
        public GeneratedSpawnPoint? SpawnPoint => _spawnPoint;
        public WorldCellCoordinate RenderedWorldOrigin => _renderedWorldOrigin;

        public Vector3 LogicalToWorldPosition(
            WorldPosition logicalPosition,
            float visualYOffset,
            float visualZ)
        {
            if (_streamingInitialized)
            {
                return StreamingLogicalToWorldPosition(logicalPosition, visualYOffset, visualZ);
            }

            if (_tilemap == null)
            {
                return transform.position;
            }

            Vector3 origin = _tilemap.GetCellCenterWorld(Vector3Int.zero);
            Vector3 xBasis = _tilemap.GetCellCenterWorld(Vector3Int.right) - origin;
            Vector3 yBasis = _tilemap.GetCellCenterWorld(Vector3Int.up) - origin;
            double localX = logicalPosition.X - _renderedWorldOrigin.X;
            double localY = logicalPosition.Y - _renderedWorldOrigin.Y;

            Vector3 renderedPosition = origin
                + (xBasis * (float)localX)
                + (yBasis * (float)localY);
            renderedPosition.y += visualYOffset;
            renderedPosition.z = visualZ;
            return renderedPosition;
        }

        public void ConfigureGeneratedPopulation(
            WorldSeed seed,
            ChunkCoordinate coordinate)
        {
            useGeneratedPopulation = true;
            worldSeed = seed.ToString();
            chunkX = coordinate.X;
            chunkY = coordinate.Y;
        }

        public bool TryGetGeneratedSpawnPosition(out WorldPosition position)
        {
            if (!_spawnPoint.HasValue)
            {
                position = default(WorldPosition);
                return false;
            }

            WorldCellCoordinate cell = _spawnPoint.Value.Cell;
            position = new WorldPosition(cell.X, cell.Y);
            return true;
        }

        public IReadOnlyList<ResourceState> CreateGeneratedResourceStates()
        {
            if (_streamingInitialized)
            {
                return CreateStreamingGeneratedResourceStates();
            }

            if (_populationChunk == null)
            {
                return Array.Empty<ResourceState>();
            }

            var resources = new List<ResourceState>(_populationChunk.Resources.Count);
            for (int index = 0; index < _populationChunk.Resources.Count; index++)
            {
                GeneratedResourcePlacement placement = _populationChunk.Resources[index];
                resources.Add(new ResourceState(
                    placement.Id,
                    new WorldPosition(placement.Cell.X, placement.Cell.Y)));
            }

            return resources.AsReadOnly();
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

            _world = useGeneratedPopulation
                ? CreateGeneratedPopulationWorld()
                : DebugWorldPattern.Create(width, height);

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
            tilemapRenderer.mode = TilemapRenderer.Mode.Individual;
            tilemapRenderer.sortOrder = TilemapRenderer.SortOrder.TopRight;

            CreateTiles();
            PopulateTilemap();
            InitializeChunkStreaming();
        }

        private DenseGrid<byte> CreateGeneratedPopulationWorld()
        {
            WorldSeed seed = WorldSeed.Parse(worldSeed);
            WorldPopulationSettings settings =
                WorldPopulationDefaults.CreateTemperatePrototypeV1(seed);
            var generator = new DeterministicWorldPopulationGenerator(settings);
            var coordinate = new ChunkCoordinate(chunkX, chunkY);
            _populationChunk = generator.Generate(coordinate);
            _renderedWorldOrigin = ChunkAddressing.GetWorldOrigin(
                coordinate,
                settings.Generation.ChunkLayout);

            ChunkLayout layout = settings.Generation.ChunkLayout;
            var generatedWorld = new DenseGrid<byte>(layout.Bounds);
            for (int index = 0; index < generatedWorld.Count; index++)
            {
                GridPosition localPosition = layout.Bounds.FromIndex(index);
                generatedWorld[localPosition] = ToDebugCellValue(
                    _populationChunk.GetCell(localPosition).Terrain);
            }

            var preferredSpawn = new WorldCellCoordinate(
                checked(_renderedWorldOrigin.X + (layout.Width / 2)),
                checked(_renderedWorldOrigin.Y + (layout.Height / 2)));
            if (!generator.TryFindSpawnNear(preferredSpawn, out GeneratedSpawnPoint spawn))
            {
                throw new InvalidOperationException(
                    $"No valid spawn was found near {preferredSpawn} for {settings}.");
            }

            _spawnPoint = spawn;
            return generatedWorld;
        }

        private static byte ToDebugCellValue(GeneratedTerrainKind terrain)
        {
            switch (terrain)
            {
                case GeneratedTerrainKind.Grass:
                    return DebugWorldPattern.BaseCell;
                case GeneratedTerrainKind.Dirt:
                    return DebugWorldPattern.AccentCell;
                case GeneratedTerrainKind.Water:
                    return DebugWorldPattern.BorderCell;
                default:
                    throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unknown generated terrain kind.");
            }
        }

        private void OnDestroy()
        {
            ResetChunkStreamingState();
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
            var tiles = new TileBase[bounds.CellCount];

            for (int index = 0; index < tiles.Length; index++)
            {
                GridPosition logicalPosition = bounds.FromIndex(index);
                tiles[index] = TileFor(_world[logicalPosition]);
            }

            _tilemap.SetTilesBlock(
                new BoundsInt(0, 0, 0, bounds.Width, bounds.Height, 1),
                tiles);

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    var logicalPosition = new GridPosition(x, y);
                    var cellPosition = new Vector3Int(x, y, 0);
                    byte value = _world[logicalPosition];
                    _tilemap.SetTileFlags(cellPosition, TileFlags.None);
                    _tilemap.SetColor(
                        cellPosition,
                        TileColorFor(value));
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


        private Color TileColorFor(byte value)
        {
            if (!UsesPrototypeVisuals)
            {
                return ColorFor(value);
            }

            // ground_water.png intentionally remains the provisional grass duplicate.
            // A debug tint keeps generated water readable without introducing a final asset.
            return useGeneratedPopulation && value == DebugWorldPattern.BorderCell
                ? new Color32(0, 64, 255, 255)
                : Color.white;
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
            ResetChunkStreamingState();
            Transform existing = transform.Find(GridRootName);
            if (existing != null)
            {
                DestroyUnityObject(existing.gameObject);
            }

            _tilemap = null;
            _world = null;
            _populationChunk = null;
            _spawnPoint = null;
            _renderedWorldOrigin = default(WorldCellCoordinate);
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

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
