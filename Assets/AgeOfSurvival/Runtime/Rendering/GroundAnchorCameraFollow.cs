using System;
using AgeOfSurvival.Runtime.Frontend;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Runtime-only adapter that keeps an orthographic camera on a visual ground anchor.
    /// It deliberately knows nothing about the simulated world or its rendered bounds.
    /// </summary>
    public sealed class GroundAnchorCameraFollow : MonoBehaviour
    {
        public const float ProvisionalOrthographicSize = 4.0625f;
        public const float MinimumOrthographicSize = 2.5f;
        public const float MaximumOrthographicSize = 8f;
        public const float DefaultZoomStepFraction = 0.1f;
        public const float DefaultZoomSmoothTime = 0.12f;
        public const float DefaultZoomSensitivity = 1f;
        public const float DefaultScrollPixelsPerStep = 120f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform groundAnchor;
        [FormerlySerializedAs("fixedOrthographicSize")]
        [SerializeField, Min(float.Epsilon)]
        private float initialOrthographicSize = ProvisionalOrthographicSize;
        [SerializeField, Min(float.Epsilon)]
        private float minimumOrthographicSize = MinimumOrthographicSize;
        [SerializeField, Min(float.Epsilon)]
        private float maximumOrthographicSize = MaximumOrthographicSize;
        [SerializeField, Min(float.Epsilon)]
        private float zoomStepFraction = DefaultZoomStepFraction;
        [SerializeField, Min(float.Epsilon)]
        private float zoomSmoothTime = DefaultZoomSmoothTime;
        [SerializeField, Min(float.Epsilon)]
        private float zoomSensitivity = DefaultZoomSensitivity;
        [SerializeField, Min(float.Epsilon)]
        private float scrollPixelsPerStep = DefaultScrollPixelsPerStep;

        private OrthographicZoomState _zoom;

        public float FixedOrthographicSize => initialOrthographicSize;
        public float CurrentOrthographicSize => _zoom?.CurrentSize ?? initialOrthographicSize;
        public float TargetOrthographicSize => _zoom?.TargetSize ?? initialOrthographicSize;
        public float ZoomSensitivity => _zoom?.Sensitivity ?? zoomSensitivity;
        public float ScrollPixelsPerStep => scrollPixelsPerStep;
        public Transform GroundAnchor => groundAnchor;

        private void Awake()
        {
            ResolveCamera();
            ResolveZoom();
            Synchronize();
        }

        private void Update()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                ApplyRawScrollPixels(mouse.scroll.ReadValue().y);
            }
        }

        private void LateUpdate()
        {
            if (!GameplayInputGate.IsBlocked)
            {
                AdvanceZoom(Time.deltaTime);
            }
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
            initialOrthographicSize = orthographicSize;
            _zoom = null;
            ResolveZoom();
            Synchronize();
        }

        public void ApplyRawScrollPixels(float rawPixelDelta)
        {
            if (float.IsNaN(rawPixelDelta) || float.IsInfinity(rawPixelDelta))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rawPixelDelta),
                    rawPixelDelta,
                    "Raw scroll delta must be finite.");
            }

            ValidateScrollPixelsPerStep(scrollPixelsPerStep);
            float logicalSteps = rawPixelDelta / scrollPixelsPerStep;
            ResolveZoom().ApplyLogicalSteps(logicalSteps);
        }

        public void SetZoomSensitivity(float sensitivity)
        {
            ResolveZoom().SetSensitivity(sensitivity);
            zoomSensitivity = sensitivity;
        }

        public void SetScrollPixelsPerStep(float pixelsPerStep)
        {
            ValidateScrollPixelsPerStep(pixelsPerStep);
            scrollPixelsPerStep = pixelsPerStep;
        }

        public float AdvanceZoom(float deltaTime)
        {
            float size = ResolveZoom().Advance(deltaTime);
            Camera camera = ResolveCamera();
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = size;
            }

            return size;
        }

        public void Synchronize()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = ResolveZoom().CurrentSize;

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

        private OrthographicZoomState ResolveZoom()
        {
            if (_zoom == null)
            {
                float minimum = Mathf.Min(minimumOrthographicSize, maximumOrthographicSize);
                float maximum = Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);
                _zoom = new OrthographicZoomState(
                    initialOrthographicSize,
                    minimum,
                    maximum,
                    zoomStepFraction,
                    zoomSmoothTime,
                    zoomSensitivity);
            }

            return _zoom;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ValidateScrollPixelsPerStep(float value)
        {
            if (!IsFinitePositive(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Scroll pixels per logical step must be finite and positive.");
            }
        }
    }
}
