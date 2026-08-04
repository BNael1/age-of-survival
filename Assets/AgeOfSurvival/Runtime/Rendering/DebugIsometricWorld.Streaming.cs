using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Runtime.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Rendering
{
    public sealed partial class DebugIsometricWorld
    {
        private const string StreamingViewNamePrefix = "Ground Chunk";
        private const double LongUpperExclusive = 9223372036854775808.0;

        private static readonly ProfilerMarker PrepareChunkMarker =
            new ProfilerMarker("AgeOfSurvival.WorldStreaming.PrepareChunk");
        private static readonly ProfilerMarker RenderChunkMarker =
            new ProfilerMarker("AgeOfSurvival.WorldStreaming.RenderChunk");
        private static readonly ProfilerMarker ShiftWindowMarker =
            new ProfilerMarker("AgeOfSurvival.WorldStreaming.ShiftWindow");

        [SerializeField, Min(0)] private int visibleChunkRadius =
            ChunkStreamingWindowDefaults.VisibleRadius;
        [SerializeField, Min(0)] private int preparationChunkRadius =
            ChunkStreamingWindowDefaults.PreparationRadius;
        [SerializeField, Min(1)] private int preparedChunksPerFrame = 2;

        private readonly Dictionary<ChunkCoordinate, StreamingChunkView> _visibleChunkViews =
            new Dictionary<ChunkCoordinate, StreamingChunkView>();
        private readonly Stack<StreamingChunkView> _availableChunkViews =
            new Stack<StreamingChunkView>();
        private readonly Queue<ChunkCoordinate> _preparationQueue =
            new Queue<ChunkCoordinate>();

        private OnDemandPopulatedWorld _streamingWorld;
        private ChunkStreamingWindowPlan _streamingPlan;
        private Grid _streamingGrid;
        private Vector3 _streamingVisualOriginWorld;
        private Vector3 _streamingXBasis;
        private Vector3 _streamingYBasis;
        private bool _streamingInitialized;
        private int _createdChunkViewCount;

        public event Action PopulationWindowChanged;

        public bool UsesChunkStreaming => _streamingInitialized;
        public ChunkCoordinate StreamingCenter => _streamingPlan != null
            ? _streamingPlan.Center
            : new ChunkCoordinate(chunkX, chunkY);
        public IReadOnlyList<ChunkCoordinate> VisibleChunks => _streamingPlan != null
            ? _streamingPlan.VisibleChunks
            : (IReadOnlyList<ChunkCoordinate>)Array.Empty<ChunkCoordinate>();
        public int VisibleChunkCount => _visibleChunkViews.Count;
        public int ActiveChunkViewCount => CountActiveChunkViews();
        public int CreatedChunkViewCount => _createdChunkViewCount;
        public int CachedChunkCount => _streamingWorld?.LoadedChunkCount ?? 0;
        public int PreparedChunkCount => CountPreparedChunks();
        public int PreparedTargetCount => _streamingPlan?.PreparedChunks.Count ?? 0;
        public int PendingPreparationCount => _preparationQueue.Count;
        public int LastPreparedChunksThisFrame { get; private set; }
        public int SynchronousFallbackGenerationCount { get; private set; }
        public int LastWindowSynchronousGenerationCount { get; private set; }
        public int WindowRevision { get; private set; }
        public int VisibleGeneratedResourceCount => CountVisibleGeneratedResources();

        private void Update()
        {
            if (!_streamingInitialized || !Application.isPlaying)
            {
                return;
            }

            LastPreparedChunksThisFrame = PreparePendingChunks(preparedChunksPerFrame);
        }

        public bool SynchronizeStreaming(WorldPosition playerPosition)
        {
            if (!_streamingInitialized
                || !TryLocateChunk(playerPosition, out ChunkCoordinate center)
                || center.Equals(_streamingPlan.Center))
            {
                return false;
            }

            using (ShiftWindowMarker.Auto())
            {
                ApplyStreamingCenter(center);
            }

            return true;
        }

        public int PreparePendingChunks(int maximumChunks)
        {
            if (maximumChunks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumChunks));
            }

            if (!_streamingInitialized || maximumChunks == 0)
            {
                return 0;
            }

            int prepared = 0;
            while (prepared < maximumChunks && _preparationQueue.Count > 0)
            {
                ChunkCoordinate coordinate = _preparationQueue.Dequeue();
                if (_streamingWorld.TryGetLoadedChunk(coordinate, out _))
                {
                    continue;
                }

                using (PrepareChunkMarker.Auto())
                {
                    _streamingWorld.GetOrGenerateChunk(coordinate);
                }

                prepared++;
            }

            return prepared;
        }

        public bool IsChunkVisible(ChunkCoordinate coordinate)
        {
            return _visibleChunkViews.ContainsKey(coordinate);
        }

        public bool IsWorldPositionVisible(WorldPosition position)
        {
            return TryLocateChunk(position, out ChunkCoordinate coordinate)
                && IsChunkVisible(coordinate);
        }

        public bool TryGetVisibleTilemap(
            ChunkCoordinate coordinate,
            out Tilemap visibleTilemap)
        {
            if (_visibleChunkViews.TryGetValue(coordinate, out StreamingChunkView view))
            {
                visibleTilemap = view.Tilemap;
                return true;
            }

            visibleTilemap = null;
            return false;
        }

        private void InitializeChunkStreaming()
        {
            if (!useGeneratedPopulation
                || _populationChunk == null
                || _tilemap == null
                || GetComponent<DebugPlayerController>() == null
                || _streamingInitialized)
            {
                return;
            }

            ChunkStreamingWindowSettings windowSettings = CreateWindowSettings();
            _streamingGrid = _tilemap.GetComponentInParent<Grid>();
            if (_streamingGrid == null)
            {
                throw new InvalidOperationException(
                    "Chunk streaming requires the generated Tilemap to belong to a Grid.");
            }

            _streamingVisualOriginWorld = _tilemap.GetCellCenterWorld(Vector3Int.zero);
            _streamingXBasis =
                _tilemap.GetCellCenterWorld(Vector3Int.right) - _streamingVisualOriginWorld;
            _streamingYBasis =
                _tilemap.GetCellCenterWorld(Vector3Int.up) - _streamingVisualOriginWorld;

            var generator = new DeterministicWorldPopulationGenerator(
                _populationChunk.Settings);
            _streamingWorld = new OnDemandPopulatedWorld(generator);
            _streamingPlan = ChunkStreamingWindowPlanner.Create(
                _populationChunk.Coordinate,
                windowSettings);

            var centerView = new StreamingChunkView(
                _tilemap.gameObject,
                _tilemap,
                _tilemap.GetComponent<TilemapRenderer>());
            _createdChunkViewCount = 1;
            _streamingInitialized = true;

            PopulatedChunk centerChunk = GetOrGenerateVisibleChunk(
                _populationChunk.Coordinate,
                false);
            BindView(centerView, centerChunk);

            for (int index = 0; index < _streamingPlan.VisibleChunks.Count; index++)
            {
                ChunkCoordinate coordinate = _streamingPlan.VisibleChunks[index];
                if (coordinate.Equals(_populationChunk.Coordinate))
                {
                    continue;
                }

                PopulatedChunk chunk = GetOrGenerateVisibleChunk(coordinate, false);
                BindView(AcquireChunkView(), chunk);
            }

            UpdateCenterChunkState(centerChunk);
            RebuildPreparationQueue();
            WindowRevision = 1;
            PopulationWindowChanged?.Invoke();
        }

        private ChunkStreamingWindowSettings CreateWindowSettings()
        {
            int visibleRadius = Math.Max(0, visibleChunkRadius);
            int preparationRadius = Math.Max(visibleRadius, preparationChunkRadius);
            preparationRadius = Math.Min(
                preparationRadius,
                ChunkStreamingWindowSettings.MaximumRadius);
            return new ChunkStreamingWindowSettings(
                visibleRadius,
                preparationRadius);
        }

        private void ApplyStreamingCenter(ChunkCoordinate center)
        {
            ChunkStreamingWindowPlan next = ChunkStreamingWindowPlanner.Create(
                center,
                _streamingPlan.Settings);
            ChunkStreamingWindowTransition transition =
                ChunkStreamingWindowTransition.Create(_streamingPlan, next);

            LastWindowSynchronousGenerationCount = 0;
            for (int index = 0; index < transition.VisibleToHide.Count; index++)
            {
                ReleaseChunkView(transition.VisibleToHide[index]);
            }

            WorldCellCoordinate nextOrigin = ChunkAddressing.GetWorldOrigin(
                center,
                _populationChunk.Settings.Generation.ChunkLayout);
            Vector3 nextVisualOrigin = MapStreamingCellCenter(
                nextOrigin.X,
                nextOrigin.Y);
            _renderedWorldOrigin = nextOrigin;
            _streamingVisualOriginWorld = nextVisualOrigin;
            _streamingPlan = next;

            for (int index = 0; index < transition.VisibleToShow.Count; index++)
            {
                ChunkCoordinate coordinate = transition.VisibleToShow[index];
                PopulatedChunk chunk = GetOrGenerateVisibleChunk(coordinate, true);
                BindView(AcquireChunkView(), chunk);
            }

            foreach (StreamingChunkView retained in _visibleChunkViews.Values)
            {
                PositionView(retained);
            }

            PopulatedChunk centerChunk = GetOrGenerateVisibleChunk(center, true);
            UpdateCenterChunkState(centerChunk);
            RebuildPreparationQueue();
            WindowRevision++;
            PopulationWindowChanged?.Invoke();
        }

        private PopulatedChunk GetOrGenerateVisibleChunk(
            ChunkCoordinate coordinate,
            bool countFallback)
        {
            if (_streamingWorld.TryGetLoadedChunk(coordinate, out PopulatedChunk loaded))
            {
                return loaded;
            }

            if (countFallback)
            {
                SynchronousFallbackGenerationCount++;
                LastWindowSynchronousGenerationCount++;
            }

            using (PrepareChunkMarker.Auto())
            {
                return _streamingWorld.GetOrGenerateChunk(coordinate);
            }
        }

        private StreamingChunkView AcquireChunkView()
        {
            if (_availableChunkViews.Count > 0)
            {
                return _availableChunkViews.Pop();
            }

            var tilemapObject = new GameObject(
                $"{StreamingViewNamePrefix} View {_createdChunkViewCount + 1}");
            tilemapObject.transform.SetParent(_streamingGrid.transform, false);
            Tilemap streamingTilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Individual;
            renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
            _createdChunkViewCount++;
            return new StreamingChunkView(tilemapObject, streamingTilemap, renderer);
        }

        private void BindView(StreamingChunkView view, PopulatedChunk chunk)
        {
            view.Coordinate = chunk.Coordinate;
            view.Root.SetActive(true);
            PositionView(view);
            PopulateStreamingTilemap(view.Tilemap, chunk);
            _visibleChunkViews.Add(chunk.Coordinate, view);
        }

        private void ReleaseChunkView(ChunkCoordinate coordinate)
        {
            if (!_visibleChunkViews.TryGetValue(coordinate, out StreamingChunkView view))
            {
                return;
            }

            _visibleChunkViews.Remove(coordinate);
            view.Root.SetActive(false);
            _availableChunkViews.Push(view);
        }

        private void PositionView(StreamingChunkView view)
        {
            WorldCellCoordinate origin = ChunkAddressing.GetWorldOrigin(
                view.Coordinate,
                _populationChunk.Settings.Generation.ChunkLayout);
            Vector3 targetCellCenter = MapStreamingCellCenter(origin.X, origin.Y);
            Vector3 currentCellCenter = view.Tilemap.GetCellCenterWorld(Vector3Int.zero);
            view.Root.transform.position += targetCellCenter - currentCellCenter;
        }

        private void PopulateStreamingTilemap(Tilemap target, PopulatedChunk chunk)
        {
            using (RenderChunkMarker.Auto())
            {
                ChunkLayout layout = chunk.Settings.Generation.ChunkLayout;
                var tiles = new TileBase[layout.CellCount];
                for (int index = 0; index < tiles.Length; index++)
                {
                    GridPosition local = layout.Bounds.FromIndex(index);
                    byte value = ToDebugCellValue(chunk.GetCell(local).Terrain);
                    tiles[index] = TileFor(value);
                }

                target.ClearAllTiles();
                target.SetTilesBlock(
                    new BoundsInt(0, 0, 0, layout.Width, layout.Height, 1),
                    tiles);

                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        var local = new GridPosition(x, y);
                        var cell = new Vector3Int(x, y, 0);
                        byte value = ToDebugCellValue(chunk.GetCell(local).Terrain);
                        target.SetTileFlags(cell, TileFlags.None);
                        target.SetColor(cell, TileColorFor(value));
                    }
                }

                target.CompressBounds();
            }
        }

        private void UpdateCenterChunkState(PopulatedChunk centerChunk)
        {
            _populationChunk = centerChunk;
            _world = CreateDenseGrid(centerChunk);
            if (_visibleChunkViews.TryGetValue(
                centerChunk.Coordinate,
                out StreamingChunkView centerView))
            {
                _tilemap = centerView.Tilemap;
            }
        }

        private static DenseGrid<byte> CreateDenseGrid(PopulatedChunk chunk)
        {
            ChunkLayout layout = chunk.Settings.Generation.ChunkLayout;
            var result = new DenseGrid<byte>(layout.Bounds);
            for (int index = 0; index < result.Count; index++)
            {
                GridPosition local = layout.Bounds.FromIndex(index);
                result[local] = ToDebugCellValue(chunk.GetCell(local).Terrain);
            }

            return result;
        }

        private void RebuildPreparationQueue()
        {
            _preparationQueue.Clear();
            for (int index = 0; index < _streamingPlan.PreparedChunks.Count; index++)
            {
                ChunkCoordinate coordinate = _streamingPlan.PreparedChunks[index];
                if (!_streamingWorld.TryGetLoadedChunk(coordinate, out _))
                {
                    _preparationQueue.Enqueue(coordinate);
                }
            }
        }

        private IReadOnlyList<ResourceState> CreateStreamingGeneratedResourceStates()
        {
            if (!_streamingInitialized)
            {
                return Array.Empty<ResourceState>();
            }

            var resources = new List<ResourceState>();
            for (int chunkIndex = 0; chunkIndex < _streamingPlan.VisibleChunks.Count; chunkIndex++)
            {
                ChunkCoordinate coordinate = _streamingPlan.VisibleChunks[chunkIndex];
                if (!_streamingWorld.TryGetLoadedChunk(
                    coordinate,
                    out PopulatedChunk chunk))
                {
                    continue;
                }

                for (int resourceIndex = 0; resourceIndex < chunk.Resources.Count; resourceIndex++)
                {
                    GeneratedResourcePlacement placement = chunk.Resources[resourceIndex];
                    resources.Add(new ResourceState(
                        placement.Id,
                        new WorldPosition(placement.Cell.X, placement.Cell.Y)));
                }
            }

            return resources.AsReadOnly();
        }

        private Vector3 StreamingLogicalToWorldPosition(
            WorldPosition logicalPosition,
            float visualYOffset,
            float visualZ)
        {
            Vector3 result = MapStreamingCellCenter(
                logicalPosition.X,
                logicalPosition.Y);
            result.y += visualYOffset;
            result.z = visualZ;
            return result;
        }

        private Vector3 MapStreamingCellCenter(double worldX, double worldY)
        {
            double localX = worldX - _renderedWorldOrigin.X;
            double localY = worldY - _renderedWorldOrigin.Y;
            return _streamingVisualOriginWorld
                + (_streamingXBasis * (float)localX)
                + (_streamingYBasis * (float)localY);
        }

        private bool TryLocateChunk(
            WorldPosition position,
            out ChunkCoordinate coordinate)
        {
            double floorX = Math.Floor(position.X);
            double floorY = Math.Floor(position.Y);
            if (floorX < long.MinValue
                || floorX >= LongUpperExclusive
                || floorY < long.MinValue
                || floorY >= LongUpperExclusive)
            {
                coordinate = default;
                return false;
            }

            var cell = new WorldCellCoordinate((long)floorX, (long)floorY);
            coordinate = ChunkAddressing.Locate(
                cell,
                _populationChunk.Settings.Generation.ChunkLayout).Chunk;
            return true;
        }

        private int CountPreparedChunks()
        {
            if (!_streamingInitialized)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < _streamingPlan.PreparedChunks.Count; index++)
            {
                if (_streamingWorld.TryGetLoadedChunk(
                    _streamingPlan.PreparedChunks[index],
                    out _))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountVisibleGeneratedResources()
        {
            if (!_streamingInitialized)
            {
                return _populationChunk?.Resources.Count ?? 0;
            }

            int count = 0;
            for (int index = 0; index < _streamingPlan.VisibleChunks.Count; index++)
            {
                if (_streamingWorld.TryGetLoadedChunk(
                    _streamingPlan.VisibleChunks[index],
                    out PopulatedChunk chunk))
                {
                    count += chunk.Resources.Count;
                }
            }

            return count;
        }

        private int CountActiveChunkViews()
        {
            int count = 0;
            foreach (StreamingChunkView view in _visibleChunkViews.Values)
            {
                if (view.Root.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private void ResetChunkStreamingState()
        {
            _streamingInitialized = false;
            _streamingWorld = null;
            _streamingPlan = null;
            _streamingGrid = null;
            _visibleChunkViews.Clear();
            _availableChunkViews.Clear();
            _preparationQueue.Clear();
            _createdChunkViewCount = 0;
            LastPreparedChunksThisFrame = 0;
            SynchronousFallbackGenerationCount = 0;
            LastWindowSynchronousGenerationCount = 0;
            WindowRevision = 0;
        }

        private sealed class StreamingChunkView
        {
            public StreamingChunkView(
                GameObject root,
                Tilemap tilemap,
                TilemapRenderer renderer)
            {
                Root = root ?? throw new ArgumentNullException(nameof(root));
                Tilemap = tilemap ?? throw new ArgumentNullException(nameof(tilemap));
                Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            }

            public GameObject Root { get; }
            public Tilemap Tilemap { get; }
            public TilemapRenderer Renderer { get; }
            public ChunkCoordinate Coordinate { get; set; }
        }
    }
}
