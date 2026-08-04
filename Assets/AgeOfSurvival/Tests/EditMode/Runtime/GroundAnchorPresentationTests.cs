using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class GroundAnchorPresentationTests
    {
        [Test]
        public void PlayerCalibrationUsesFeetPivotAndTwentyPercentVisualScale()
        {
            Assert.That(DebugPlayerController.PlayerGroundPivotX, Is.EqualTo(0.5f));
            Assert.That(DebugPlayerController.PlayerGroundPivotY, Is.EqualTo(0.12f));
            Assert.That(DebugPlayerController.PlayerVisualScale, Is.EqualTo(1.2f));
        }

        [Test]
        public void ResourceCalibrationUsesExplicitGroundPivots()
        {
            Assert.That(DebugResourceInteraction.ResourceGroundPivotX, Is.EqualTo(0.5f));
            Assert.That(DebugResourceInteraction.ResourceGroundPivotY, Is.EqualTo(0.12f));
            Assert.That(DebugResourceInteraction.GroundPilePivotY, Is.EqualTo(0.2f));
        }

        [Test]
        public void LowerGroundAnchorSortsInFront()
        {
            int comparison = GroundAnchorSorting.CompareBackToFront(
                2f,
                "resource:stable",
                3f,
                "resource:stable");

            Assert.That(comparison, Is.GreaterThan(0));
            Assert.That(
                GroundAnchorSorting.OrderForRank(2),
                Is.GreaterThan(GroundAnchorSorting.OrderForRank(1)));
        }

        [Test]
        public void EqualGroundAnchorAndStableIdAlwaysProduceSameOrder()
        {
            int first = GroundAnchorSorting.CompareBackToFront(
                1.25f,
                "resource:stable",
                1.25f,
                "resource:stable");
            int second = GroundAnchorSorting.CompareBackToFront(
                1.25f,
                "resource:stable",
                1.25f,
                "resource:stable");

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EqualGroundAnchorsUseStableIdentifierAsTieBreak()
        {
            int first = GroundAnchorSorting.CompareBackToFront(
                1.25f,
                "resource:a",
                1.25f,
                "resource:c");
            int second = GroundAnchorSorting.CompareBackToFront(
                1.25f,
                "resource:c",
                1.25f,
                "resource:a");

            Assert.That(first, Is.LessThan(0));
            Assert.That(second, Is.GreaterThan(0));
            Assert.That(second, Is.EqualTo(-first));
        }

        [Test]
        public void VisualCalibrationAndSortingDoNotMutateCorePlayerPosition()
        {
            var player = new PlayerState(new WorldPosition(12.5, -4.25));
            WorldPosition before = player.Position;

            _ = GroundAnchorSorting.CompareBackToFront(
                -3.5f,
                "player:local",
                0f,
                "resource:stable");
            _ = DebugPlayerController.PlayerVisualScale;

            Assert.That(player.Position, Is.EqualTo(before));
        }

        [Test]
        public void PrototypeSpritesExposeConfiguredGroundPivots()
        {
            Sprite player = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.PlayerSurvivor,
                new Vector2(
                    DebugPlayerController.PlayerGroundPivotX,
                    DebugPlayerController.PlayerGroundPivotY),
                PrototypeVisualAssets.PixelsPerUnit,
                "Player pivot test");
            Sprite resource = PrototypeVisualAssets.CreateSprite(
                PrototypeVisualAssets.ResourceShrub,
                new Vector2(
                    DebugResourceInteraction.ResourceGroundPivotX,
                    DebugResourceInteraction.ResourceGroundPivotY),
                PrototypeVisualAssets.PixelsPerUnit,
                "Resource pivot test");

            try
            {
                Assert.That(player, Is.Not.Null);
                Assert.That(resource, Is.Not.Null);
                Assert.That(
                    player.pivot.y / player.rect.height,
                    Is.EqualTo(DebugPlayerController.PlayerGroundPivotY).Within(0.0001f));
                Assert.That(
                    resource.pivot.y / resource.rect.height,
                    Is.EqualTo(DebugResourceInteraction.ResourceGroundPivotY).Within(0.0001f));
            }
            finally
            {
                PrototypeVisualAssets.DestroyRuntimeSprite(player);
                PrototypeVisualAssets.DestroyRuntimeSprite(resource);
            }
        }

        [Test]
        public void FallbackResourceSpritesShareTextureButUseDistinctGroundPivots()
        {
            var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            DebugResourceInteraction.CreateFallbackBodySprites(
                texture,
                out Sprite resource,
                out Sprite groundPile);

            try
            {
                Assert.That(resource, Is.Not.SameAs(groundPile));
                Assert.That(resource.texture, Is.SameAs(texture));
                Assert.That(groundPile.texture, Is.SameAs(texture));
                Assert.That(
                    resource.pivot.y / resource.rect.height,
                    Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(
                    groundPile.pivot.y / groundPile.rect.height,
                    Is.EqualTo(0.2f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(resource);
                Object.DestroyImmediate(groundPile);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CoordinatorProducesSameResultForBothVisualSynchronizationOrders()
        {
            using (var resourceFirst = new SortHarness(false))
            using (var playerFirst = new SortHarness(false))
            {
                resourceFirst.SetResourceThenPlayer(2f, 1f);
                playerFirst.SetPlayerThenResource(1f, 2f);

                resourceFirst.Coordinator.ApplySortingForFrame(1);
                playerFirst.Coordinator.ApplySortingForFrame(1);

                Assert.That(playerFirst.PlayerOrder, Is.EqualTo(resourceFirst.PlayerOrder));
                Assert.That(playerFirst.ResourceOrder, Is.EqualTo(resourceFirst.ResourceOrder));
            }
        }

        [Test]
        public void CoordinatorAppliesAtMostOneFinalPassPerFrame()
        {
            using (var harness = new SortHarness(false))
            {
                Assert.That(harness.Coordinator.ApplySortingForFrame(42), Is.True);
                Assert.That(harness.Coordinator.ApplySortingForFrame(42), Is.False);
                Assert.That(harness.Coordinator.AppliedPassCount, Is.EqualTo(1));
                Assert.That(harness.Coordinator.ApplicationsInLastAppliedFrame, Is.EqualTo(1));
            }
        }

        [Test]
        public void CoordinatorSortsPlayerInFrontAndBehindResource()
        {
            using (var harness = new SortHarness(false))
            {
                harness.SetResourceThenPlayer(2f, 1f);
                harness.Coordinator.ApplySortingForFrame(1);
                Assert.That(harness.PlayerOrder, Is.GreaterThan(harness.ResourceOrder));

                harness.SetPlayerThenResource(3f, 2f);
                harness.Coordinator.ApplySortingForFrame(2);
                Assert.That(harness.PlayerOrder, Is.LessThan(harness.ResourceOrder));
            }
        }

        [Test]
        public void CoordinatorExcludesHiddenResource()
        {
            using (var harness = new SortHarness(false))
            {
                harness.ResourceOrder = 777;
                harness.HideResource();

                harness.Coordinator.ApplySortingForFrame(1);

                Assert.That(harness.Coordinator.ActiveEntryCount, Is.EqualTo(1));
                Assert.That(harness.ResourceOrder, Is.EqualTo(777));
            }
        }

        [Test]
        public void CoordinatorOrderDoesNotDependOnGameObjectCreationOrder()
        {
            using (var playerCreatedFirst = new SortHarness(false))
            using (var resourceCreatedFirst = new SortHarness(true))
            {
                playerCreatedFirst.SetResourceThenPlayer(1.5f, 1.5f);
                resourceCreatedFirst.SetResourceThenPlayer(1.5f, 1.5f);

                playerCreatedFirst.Coordinator.ApplySortingForFrame(1);
                resourceCreatedFirst.Coordinator.ApplySortingForFrame(1);

                Assert.That(
                    resourceCreatedFirst.PlayerOrder,
                    Is.EqualTo(playerCreatedFirst.PlayerOrder));
                Assert.That(
                    resourceCreatedFirst.ResourceOrder,
                    Is.EqualTo(playerCreatedFirst.ResourceOrder));
            }
        }

        private sealed class SortHarness : System.IDisposable
        {
            private readonly GameObject _root;
            private readonly GameObject _player;
            private readonly GameObject _resource;
            private readonly SpriteRenderer _playerRenderer;
            private readonly SpriteRenderer _resourceRenderer;

            public SortHarness(bool createResourceFirst)
            {
                _root = new GameObject("Ground anchor sort coordinator test");
                Coordinator = _root.AddComponent<GroundAnchorSortCoordinator>();

                if (createResourceFirst)
                {
                    _resource = CreateVisual("Resource", out _resourceRenderer);
                    _player = CreateVisual("Player", out _playerRenderer);
                }
                else
                {
                    _player = CreateVisual("Player", out _playerRenderer);
                    _resource = CreateVisual("Resource", out _resourceRenderer);
                }

                if (createResourceFirst)
                {
                    RegisterResource();
                    RegisterPlayer();
                }
                else
                {
                    RegisterPlayer();
                    RegisterResource();
                }
            }

            public GroundAnchorSortCoordinator Coordinator { get; }
            public int PlayerOrder => _playerRenderer.sortingOrder;
            public int ResourceOrder
            {
                get => _resourceRenderer.sortingOrder;
                set => _resourceRenderer.sortingOrder = value;
            }

            public void SetResourceThenPlayer(float resourceY, float playerY)
            {
                _resource.transform.position = new Vector3(0f, resourceY, 0f);
                _player.transform.position = new Vector3(0f, playerY, 0f);
            }

            public void SetPlayerThenResource(float playerY, float resourceY)
            {
                _player.transform.position = new Vector3(0f, playerY, 0f);
                _resource.transform.position = new Vector3(0f, resourceY, 0f);
            }

            public void HideResource()
            {
                _resource.SetActive(false);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_player);
                Object.DestroyImmediate(_resource);
                Object.DestroyImmediate(_root);
            }

            private static GameObject CreateVisual(
                string name,
                out SpriteRenderer renderer)
            {
                var visual = new GameObject(name);
                renderer = visual.AddComponent<SpriteRenderer>();
                return visual;
            }

            private void RegisterPlayer()
            {
                Coordinator.Register(
                    GroundAnchorSortCoordinator.PlayerStableId,
                    _player.transform,
                    _player,
                    new RendererOrderBinding(_playerRenderer, 1));
            }

            private void RegisterResource()
            {
                Coordinator.Register(
                    "resource:stable",
                    _resource.transform,
                    _resource,
                    new RendererOrderBinding(_resourceRenderer, 1));
            }
        }
    }
}
