using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Runtime.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class GroundAnchorCameraFollowTests
    {
        [Test]
        public void Configure_AppliesExplicitFixedOrthographicZoom()
        {
            using (var harness = new CameraHarness())
            {
                harness.Follow.Configure(
                    harness.Camera,
                    harness.Anchor,
                    GroundAnchorCameraFollow.ProvisionalOrthographicSize);

                Assert.That(harness.Camera.orthographic, Is.True);
                Assert.That(
                    harness.Camera.orthographicSize,
                    Is.EqualTo(4.0625f));
                Assert.That(harness.Follow.FixedOrthographicSize, Is.EqualTo(4.0625f));
            }
        }

        [Test]
        public void Synchronize_CentersCameraOnVisualGroundAnchor()
        {
            using (var harness = new CameraHarness())
            {
                harness.Anchor.position = new Vector3(8.25f, -3.75f, -0.1f);

                harness.Follow.Synchronize();

                Assert.That(harness.Camera.transform.position.x, Is.EqualTo(8.25f));
                Assert.That(harness.Camera.transform.position.y, Is.EqualTo(-3.75f));
            }
        }

        [Test]
        public void Synchronize_MovesCameraBySameDeltaAsVisualGroundAnchor()
        {
            using (var harness = new CameraHarness())
            {
                harness.Anchor.position = new Vector3(-2f, 7f, -0.1f);
                harness.Follow.Synchronize();
                Vector3 firstCameraPosition = harness.Camera.transform.position;

                var visualDelta = new Vector3(6.5f, -4.25f, 0f);
                harness.Anchor.position += visualDelta;
                harness.Follow.Synchronize();

                Vector3 cameraDelta = harness.Camera.transform.position - firstCameraPosition;
                Assert.That(cameraDelta.x, Is.EqualTo(visualDelta.x));
                Assert.That(cameraDelta.y, Is.EqualTo(visualDelta.y));
            }
        }

        [Test]
        public void Synchronize_PreservesCurrentCameraDepth()
        {
            using (var harness = new CameraHarness())
            {
                harness.Camera.transform.position = new Vector3(0f, 0f, -37f);
                harness.Anchor.position = new Vector3(4f, 9f, 12f);

                harness.Follow.Synchronize();

                Assert.That(harness.Camera.transform.position.z, Is.EqualTo(-37f));
            }
        }

        [Test]
        public void Synchronize_DoesNotMoveVisualGroundAnchor()
        {
            using (var harness = new CameraHarness())
            {
                var expectedAnchorPosition = new Vector3(17f, 4f, -0.1f);
                harness.Anchor.position = expectedAnchorPosition;

                harness.Follow.Synchronize();

                Assert.That(
                    harness.Anchor.position,
                    Is.EqualTo(expectedAnchorPosition));
            }
        }

        [Test]
        public void Synchronize_IgnoresTilemapBoundsChanges()
        {
            using (var harness = new CameraHarness())
            {
                var gridObject = new GameObject("Unobserved tilemap bounds");
                try
                {
                    gridObject.AddComponent<Grid>();
                    var tilemapObject = new GameObject("Unobserved tilemap");
                    tilemapObject.transform.SetParent(gridObject.transform, false);
                    var tilemap = tilemapObject.AddComponent<Tilemap>();
                    harness.Anchor.position = new Vector3(3f, 11f, -0.1f);
                    harness.Follow.Synchronize();
                    Vector3 expectedPosition = harness.Camera.transform.position;
                    float expectedZoom = harness.Camera.orthographicSize;

                    tilemap.origin = new Vector3Int(-500, -700, 0);
                    tilemap.size = new Vector3Int(2000, 3000, 1);
                    harness.Follow.Synchronize();

                    Assert.That(harness.Camera.transform.position, Is.EqualTo(expectedPosition));
                    Assert.That(harness.Camera.orthographicSize, Is.EqualTo(expectedZoom));
                }
                finally
                {
                    Object.DestroyImmediate(gridObject);
                }
            }
        }

        [Test]
        public void Synchronize_FollowsLogicalPositionFarFromOrigin()
        {
            using (var harness = new CameraHarness())
            {
                var worldObject = new GameObject("Far logical position world");
                try
                {
                    var world = worldObject.AddComponent<DebugIsometricWorld>();
                    var logicalPosition = new WorldPosition(100.0, 100.0);
                    Vector3 visualGroundAnchor = world.LogicalToWorldPosition(
                        logicalPosition,
                        0.18f,
                        -0.1f);
                    harness.Anchor.position = visualGroundAnchor;

                    harness.Follow.Synchronize();

                    Assert.That(harness.Camera.transform.position.x, Is.EqualTo(visualGroundAnchor.x));
                    Assert.That(harness.Camera.transform.position.y, Is.EqualTo(visualGroundAnchor.y));
                    Assert.That(harness.Camera.orthographicSize, Is.EqualTo(4.0625f));
                }
                finally
                {
                    Object.DestroyImmediate(worldObject);
                }
            }
        }

        [Test]
        public void Synchronize_IsDeterministicForSameVisualGroundAnchor()
        {
            using (var harness = new CameraHarness())
            {
                harness.Anchor.position = new Vector3(42.125f, -19.875f, -0.1f);
                harness.Follow.Synchronize();
                Vector3 firstPosition = harness.Camera.transform.position;

                harness.Camera.transform.position = new Vector3(-600f, 900f, firstPosition.z);
                harness.Follow.Synchronize();

                Assert.That(harness.Camera.transform.position, Is.EqualTo(firstPosition));
            }
        }

        private sealed class CameraHarness : System.IDisposable
        {
            private readonly GameObject _cameraObject;
            private readonly GameObject _anchorObject;

            public CameraHarness()
            {
                _cameraObject = new GameObject("Ground anchor camera test");
                _anchorObject = new GameObject("Visual ground anchor test");
                Camera = _cameraObject.AddComponent<Camera>();
                Follow = _cameraObject.AddComponent<GroundAnchorCameraFollow>();
                Anchor = _anchorObject.transform;
                Camera.transform.position = new Vector3(0f, 0f, -10f);
                Follow.Configure(
                    Camera,
                    Anchor,
                    GroundAnchorCameraFollow.ProvisionalOrthographicSize);
            }

            public Camera Camera { get; }
            public GroundAnchorCameraFollow Follow { get; }
            public Transform Anchor { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(_anchorObject);
                Object.DestroyImmediate(_cameraObject);
            }
        }
    }
}
