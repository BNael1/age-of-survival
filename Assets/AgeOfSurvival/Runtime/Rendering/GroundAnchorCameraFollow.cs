using System;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Runtime-only adapter that keeps an orthographic camera on a visual ground anchor.
    /// It deliberately knows nothing about the simulated world or its rendered bounds.
    /// </summary>
    public sealed class GroundAnchorCameraFollow : MonoBehaviour
    {
        public const float ProvisionalOrthographicSize = 4.0625f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform groundAnchor;
        [SerializeField, Min(float.Epsilon)]
        private float fixedOrthographicSize = ProvisionalOrthographicSize;

        public float FixedOrthographicSize => fixedOrthographicSize;
        public Transform GroundAnchor => groundAnchor;

        private void Awake()
        {
            ResolveCamera();
            Synchronize();
        }

        private void LateUpdate()
        {
            Synchronize();
        }

        public void Track(Transform anchor)
        {
            groundAnchor = anchor;
            Synchronize();
        }

        public void Configure(Camera camera, Transform anchor, float orthographicSize)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!IsFinitePositive(orthographicSize))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orthographicSize),
                    orthographicSize,
                    "The fixed orthographic size must be finite and positive.");
            }

            targetCamera = camera;
            groundAnchor = anchor;
            fixedOrthographicSize = orthographicSize;
            Synchronize();
        }

        public void Synchronize()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = fixedOrthographicSize;

            if (groundAnchor == null)
            {
                return;
            }

            Vector3 cameraPosition = camera.transform.position;
            Vector3 anchorPosition = groundAnchor.position;
            camera.transform.position = new Vector3(
                anchorPosition.x,
                anchorPosition.y,
                cameraPosition.z);
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            return targetCamera;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
