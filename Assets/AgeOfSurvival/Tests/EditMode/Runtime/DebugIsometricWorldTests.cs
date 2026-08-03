using System.IO;
using System.Linq;
using AgeOfSurvival.Core.World;
using AgeOfSurvival.Runtime.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class DebugIsometricWorldTests
    {
        private const int GroundTileWidthPixels = 64;
        private const int GroundTileHeightPixels = 32;
        private const int SeamTestMapSize = 10;

        [Test]
        public void DebugPattern_IsDeterministicAndKeepsTheBorderDistinct()
        {
            DenseGrid<byte> first = DebugWorldPattern.Create(10, 10);
            DenseGrid<byte> second = DebugWorldPattern.Create(10, 10);

            CollectionAssert.AreEqual(first.CopyCells(), second.CopyCells());
            Assert.That(first[new GridPosition(0, 5)], Is.EqualTo(DebugWorldPattern.BorderCell));
            Assert.That(first[new GridPosition(4, 4)], Is.EqualTo(DebugWorldPattern.AccentCell));
            Assert.That(first[new GridPosition(3, 4)], Is.EqualTo(DebugWorldPattern.BaseCell));
        }

        [Test]
        public void PrototypeVisualTexturesAreAvailableToRuntimeAdapters()
        {
            Assert.That(PrototypeVisualAssets.AllRequiredTexturesExist(), Is.True);
        }

        [Test]
        public void PrototypeGroundImportsPreservePixelArtSettings()
        {
            AssertGroundImportSettings("ground_grass.png");
            AssertGroundImportSettings("ground_dirt.png");
            AssertGroundImportSettings("ground_water.png");
        }

        [Test]
        public void PrototypeGroundTiles_CoverTenByTenPavingWithoutInternalGaps()
        {
            AlphaMask grass = LoadGroundAlphaMask("ground_grass.png");
            AlphaMask dirt = LoadGroundAlphaMask("ground_dirt.png");
            AlphaMask water = LoadGroundAlphaMask("ground_water.png");

            float transparentGapFraction = MeasureTransparentGapFraction(
                grass,
                dirt,
                water);

            Assert.That(
                transparentGapFraction,
                Is.LessThan(0.01f),
                $"The central {SeamTestMapSize}x{SeamTestMapSize} isometric paving "
                + $"contains {transparentGapFraction:P6} uncovered pixels.");
        }

        [Test]
        public void Rebuild_UsesDistinctPrototypeGroundSprites()
        {
            var root = new GameObject("Prototype ground visual test");

            try
            {
                var presenter = root.AddComponent<DebugIsometricWorld>();
                presenter.Rebuild();

                Assert.That(presenter.UsesPrototypeVisuals, Is.True);
                Assert.That(
                    presenter.Tilemap.GetSprite(new Vector3Int(3, 4, 0)).name,
                    Is.EqualTo("Prototype Ground Grass"));
                Assert.That(
                    presenter.Tilemap.GetSprite(new Vector3Int(4, 4, 0)).name,
                    Is.EqualTo("Prototype Ground Dirt"));
                Assert.That(
                    presenter.Tilemap.GetSprite(new Vector3Int(0, 5, 0)).name,
                    Is.EqualTo("Prototype Ground Water"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rebuild_CreatesOneIsometricTilemapWithOneTilePerCoreCell()
        {
            var root = new GameObject("Debug world test");

            try
            {
                var presenter = root.AddComponent<DebugIsometricWorld>();
                presenter.Rebuild();

                Grid grid = root.GetComponentInChildren<Grid>();
                Tilemap tilemap = presenter.Tilemap;
                int populatedCells = tilemap
                    .GetTilesBlock(tilemap.cellBounds)
                    .Count(tile => tile != null);

                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.cellLayout, Is.EqualTo(GridLayout.CellLayout.Isometric));
                Assert.That(presenter.World.Bounds, Is.EqualTo(new GridBounds(10, 10)));
                Assert.That(populatedCells, Is.EqualTo(presenter.World.Count));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rebuild_YSortsAndOverlapsEveryOpaqueGroundEdge()
        {
            AlphaMask grass = LoadGroundAlphaMask("ground_grass.png");
            var root = new GameObject("Isometric ground overlap order test");

            try
            {
                var presenter = root.AddComponent<DebugIsometricWorld>();
                presenter.Rebuild();

                var renderer = presenter.Tilemap.GetComponent<TilemapRenderer>();
                ScriptableObject rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Settings/Renderer2D.asset");
                Assert.That(rendererData, Is.Not.Null);
                var serializedRendererData = new SerializedObject(rendererData);
                SerializedProperty sortMode = serializedRendererData.FindProperty(
                    "m_TransparencySortMode");
                SerializedProperty sortAxis = serializedRendererData.FindProperty(
                    "m_TransparencySortAxis");
                int opaqueOverlapPixels = CountOpaqueOverlapPixels(
                    grass,
                    GroundTileWidthPixels / 2,
                    GroundTileHeightPixels / 2);
                Vector3 cellOrigin = presenter.Tilemap.GetCellCenterLocal(Vector3Int.zero);
                float diagonalStepPixels = Mathf.Abs(
                    presenter.Tilemap.GetCellCenterLocal(Vector3Int.right).y - cellOrigin.y)
                    * PrototypeVisualAssets.PixelsPerUnit;

                Assert.That(
                    opaqueOverlapPixels,
                    Is.GreaterThan(0),
                    "The source tile must retain the opaque overlap that makes draw order observable.");
                Assert.That(
                    diagonalStepPixels,
                    Is.EqualTo(15f).Within(0.001f),
                    "Neighbouring diamonds must overlap by one pixel instead of exposing their opaque edge.");
                Assert.That(renderer.mode, Is.EqualTo(TilemapRenderer.Mode.Individual));
                Assert.That(
                    renderer.sortOrder,
                    Is.EqualTo(TilemapRenderer.SortOrder.TopRight),
                    "Overlapping isometric ground must draw nearer cells over the lower opaque edge of their neighbours.");
                Assert.That(sortMode, Is.Not.Null);
                Assert.That(
                    sortMode.intValue,
                    Is.EqualTo((int)TransparencySortMode.CustomAxis));
                Assert.That(sortAxis, Is.Not.Null);
                Assert.That(sortAxis.vector3Value, Is.EqualTo(Vector3.up));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rebuild_TwiceDoesNotDuplicateTheGeneratedHierarchy()
        {
            var root = new GameObject("Debug world rebuild test");

            try
            {
                var presenter = root.AddComponent<DebugIsometricWorld>();
                presenter.Rebuild();
                presenter.Rebuild();

                Assert.That(root.GetComponentsInChildren<Grid>(true), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Tilemap>(true), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<TilemapRenderer>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rebuild_DoesNotFrameCameraFromGridDimensions()
        {
            const float fixedZoom = 4.0625f;
            var root = new GameObject("World independent camera test");
            var cameraObject = new GameObject("Camera independent from world test");

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var presenter = root.AddComponent<DebugIsometricWorld>();
                var serializedPresenter = new SerializedObject(presenter);
                SerializedProperty legacyTargetCamera = serializedPresenter.FindProperty("targetCamera");
                if (legacyTargetCamera != null)
                {
                    legacyTargetCamera.objectReferenceValue = camera;
                }

                SetDimensions(serializedPresenter, 10, 10);
                camera.orthographicSize = fixedZoom;
                camera.transform.position = new Vector3(37f, -12f, -23f);
                presenter.Rebuild();

                Assert.That(camera.orthographicSize, Is.EqualTo(fixedZoom));
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(37f, -12f, -23f)));

                SetDimensions(serializedPresenter, 100, 100);
                presenter.Rebuild();

                Assert.That(camera.orthographicSize, Is.EqualTo(fixedZoom));
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(37f, -12f, -23f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void SetDimensions(SerializedObject presenter, int width, int height)
        {
            presenter.FindProperty("width").intValue = width;
            presenter.FindProperty("height").intValue = height;
            presenter.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertGroundImportSettings(string fileName)
        {
            string assetPath = GroundAssetPath(fileName);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            Assert.That(importer, Is.Not.Null, $"Missing texture importer for {assetPath}.");
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(64f));
            Assert.That(importer.alphaIsTransparency, Is.True);
        }

        private static AlphaMask LoadGroundAlphaMask(string fileName)
        {
            string absolutePath = Path.Combine(
                Application.dataPath,
                "AgeOfSurvival",
                "Runtime",
                "Resources",
                "PrototypeVisuals",
                fileName);
            byte[] pngBytes = File.ReadAllBytes(absolutePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                Assert.That(ImageConversion.LoadImage(texture, pngBytes, false), Is.True);
                Assert.That(texture.width, Is.EqualTo(GroundTileWidthPixels));
                Assert.That(texture.height, Is.EqualTo(GroundTileHeightPixels));

                Color32[] pixels = texture.GetPixels32();
                var opaquePixels = new bool[pixels.Length];
                for (int index = 0; index < pixels.Length; index++)
                {
                    opaquePixels[index] = pixels[index].a > 0;
                }

                return new AlphaMask(opaquePixels);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static float MeasureTransparentGapFraction(
            AlphaMask grass,
            AlphaMask dirt,
            AlphaMask water)
        {
            const int paddingCells = 1;
            int paddedMinimum = -paddingCells;
            int paddedMaximum = SeamTestMapSize + paddingCells - 1;
            int minimumCenterX = int.MaxValue;
            int maximumCenterX = int.MinValue;
            int minimumCenterY = int.MaxValue;
            int maximumCenterY = int.MinValue;

            for (int cellY = paddedMinimum; cellY <= paddedMaximum; cellY++)
            {
                for (int cellX = paddedMinimum; cellX <= paddedMaximum; cellX++)
                {
                    int centerX = (GroundTileWidthPixels / 2) * (cellX - cellY);
                    int centerY = (GroundTileHeightPixels / 2) * (cellX + cellY);
                    minimumCenterX = Mathf.Min(minimumCenterX, centerX);
                    maximumCenterX = Mathf.Max(maximumCenterX, centerX);
                    minimumCenterY = Mathf.Min(minimumCenterY, centerY);
                    maximumCenterY = Mathf.Max(maximumCenterY, centerY);
                }
            }

            int canvasWidth = maximumCenterX - minimumCenterX + GroundTileWidthPixels;
            int canvasHeight = maximumCenterY - minimumCenterY + GroundTileHeightPixels;
            var covered = new bool[canvasWidth * canvasHeight];
            var expectedInterior = new bool[canvasWidth * canvasHeight];
            var bounds = new GridBounds(SeamTestMapSize, SeamTestMapSize);

            for (int cellY = paddedMinimum; cellY <= paddedMaximum; cellY++)
            {
                for (int cellX = paddedMinimum; cellX <= paddedMaximum; cellX++)
                {
                    int wrappedX = PositiveModulo(cellX, SeamTestMapSize);
                    int wrappedY = PositiveModulo(cellY, SeamTestMapSize);
                    byte cellValue = DebugWorldPattern.SelectCellValue(
                        new GridPosition(wrappedX, wrappedY),
                        bounds);
                    AlphaMask tile = AlphaMaskFor(cellValue, grass, dirt, water);
                    PlaceAlphaMask(
                        tile,
                        cellX,
                        cellY,
                        minimumCenterX,
                        minimumCenterY,
                        canvasWidth,
                        covered);
                }
            }

            for (int cellY = 0; cellY < SeamTestMapSize; cellY++)
            {
                for (int cellX = 0; cellX < SeamTestMapSize; cellX++)
                {
                    PlaceIdealDiamond(
                        cellX,
                        cellY,
                        minimumCenterX,
                        minimumCenterY,
                        canvasWidth,
                        expectedInterior);
                }
            }

            int expectedPixelCount = 0;
            int uncoveredPixelCount = 0;
            for (int index = 0; index < expectedInterior.Length; index++)
            {
                if (!expectedInterior[index])
                {
                    continue;
                }

                expectedPixelCount++;
                if (!covered[index])
                {
                    uncoveredPixelCount++;
                }
            }

            Assert.That(
                expectedPixelCount,
                Is.EqualTo(SeamTestMapSize * SeamTestMapSize * 1024));
            return (float)uncoveredPixelCount / expectedPixelCount;
        }

        private static int CountOpaqueOverlapPixels(
            AlphaMask tile,
            int horizontalOffset,
            int verticalOffset)
        {
            int overlapCount = 0;

            for (int pixelY = verticalOffset; pixelY < GroundTileHeightPixels; pixelY++)
            {
                for (int pixelX = horizontalOffset; pixelX < GroundTileWidthPixels; pixelX++)
                {
                    int firstIndex = (pixelY * GroundTileWidthPixels) + pixelX;
                    int secondIndex = ((pixelY - verticalOffset) * GroundTileWidthPixels)
                        + pixelX
                        - horizontalOffset;

                    if (tile.OpaquePixels[firstIndex] && tile.OpaquePixels[secondIndex])
                    {
                        overlapCount++;
                    }
                }
            }

            return overlapCount;
        }

        private static void PlaceAlphaMask(
            AlphaMask tile,
            int cellX,
            int cellY,
            int minimumCenterX,
            int minimumCenterY,
            int canvasWidth,
            bool[] covered)
        {
            int centerX = (GroundTileWidthPixels / 2) * (cellX - cellY);
            int centerY = (GroundTileHeightPixels / 2) * (cellX + cellY);
            int left = centerX - minimumCenterX;
            int bottom = centerY - minimumCenterY;

            for (int pixelY = 0; pixelY < GroundTileHeightPixels; pixelY++)
            {
                for (int pixelX = 0; pixelX < GroundTileWidthPixels; pixelX++)
                {
                    if (!tile.OpaquePixels[(pixelY * GroundTileWidthPixels) + pixelX])
                    {
                        continue;
                    }

                    int canvasIndex = ((bottom + pixelY) * canvasWidth) + left + pixelX;
                    covered[canvasIndex] = true;
                }
            }
        }

        private static void PlaceIdealDiamond(
            int cellX,
            int cellY,
            int minimumCenterX,
            int minimumCenterY,
            int canvasWidth,
            bool[] expectedInterior)
        {
            int centerX = (GroundTileWidthPixels / 2) * (cellX - cellY);
            int centerY = (GroundTileHeightPixels / 2) * (cellX + cellY);
            int left = centerX - minimumCenterX;
            int bottom = centerY - minimumCenterY;

            for (int pixelY = 0; pixelY < GroundTileHeightPixels; pixelY++)
            {
                for (int pixelX = 0; pixelX < GroundTileWidthPixels; pixelX++)
                {
                    float normalizedX = Mathf.Abs(
                        (pixelX + 0.5f - (GroundTileWidthPixels * 0.5f))
                        / (GroundTileWidthPixels * 0.5f));
                    float normalizedY = Mathf.Abs(
                        (pixelY + 0.5f - (GroundTileHeightPixels * 0.5f))
                        / (GroundTileHeightPixels * 0.5f));
                    if (normalizedX + normalizedY > 1f)
                    {
                        continue;
                    }

                    int canvasIndex = ((bottom + pixelY) * canvasWidth) + left + pixelX;
                    expectedInterior[canvasIndex] = true;
                }
            }
        }

        private static AlphaMask AlphaMaskFor(
            byte cellValue,
            AlphaMask grass,
            AlphaMask dirt,
            AlphaMask water)
        {
            switch (cellValue)
            {
                case DebugWorldPattern.AccentCell:
                    return dirt;
                case DebugWorldPattern.BorderCell:
                    return water;
                default:
                    return grass;
            }
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static string GroundAssetPath(string fileName) =>
            $"Assets/AgeOfSurvival/Runtime/Resources/PrototypeVisuals/{fileName}";

        private sealed class AlphaMask
        {
            public AlphaMask(bool[] opaquePixels)
            {
                OpaquePixels = opaquePixels;
            }

            public bool[] OpaquePixels { get; }
        }
    }
}
