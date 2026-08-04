using System;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Testable Runtime state for a multiplicative, damped orthographic zoom.
    /// Input sampling and Camera mutation remain responsibilities of the Unity adapter.
    /// </summary>
    public sealed class OrthographicZoomState
    {
        private float _velocity;

        public OrthographicZoomState(
            float initialSize,
            float minimumSize,
            float maximumSize,
            float stepFraction,
            float smoothTime,
            float sensitivity)
        {
            ValidateConfiguration(
                initialSize,
                minimumSize,
                maximumSize,
                stepFraction,
                smoothTime,
                sensitivity);

            MinimumSize = minimumSize;
            MaximumSize = maximumSize;
            StepFraction = stepFraction;
            SmoothTime = smoothTime;
            Sensitivity = sensitivity;
            CurrentSize = Mathf.Clamp(initialSize, minimumSize, maximumSize);
            TargetSize = CurrentSize;
        }

        public float CurrentSize { get; private set; }
        public float TargetSize { get; private set; }
        public float MinimumSize { get; }
        public float MaximumSize { get; }
        public float StepFraction { get; }
        public float SmoothTime { get; }
        public float Sensitivity { get; private set; }

        public void SetSensitivity(float sensitivity)
        {
            if (!IsFinitePositive(sensitivity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sensitivity),
                    sensitivity,
                    "Zoom sensitivity must be finite and positive.");
            }

            Sensitivity = sensitivity;
        }

        public void ApplyLogicalSteps(float logicalSteps)
        {
            if (logicalSteps == 0f)
            {
                return;
            }

            if (float.IsNaN(logicalSteps) || float.IsInfinity(logicalSteps))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(logicalSteps),
                    logicalSteps,
                    "Logical scroll steps must be finite.");
            }

            float effectiveSteps = logicalSteps * Sensitivity;
            float multiplier = Mathf.Pow(
                1f + StepFraction,
                -effectiveSteps);
            TargetSize = Mathf.Clamp(
                TargetSize * multiplier,
                MinimumSize,
                MaximumSize);
        }

        public float Advance(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    deltaTime,
                    "Delta time must be finite and non-negative.");
            }

            if (deltaTime == 0f || Mathf.Approximately(CurrentSize, TargetSize))
            {
                if (Mathf.Approximately(CurrentSize, TargetSize))
                {
                    CurrentSize = TargetSize;
                    _velocity = 0f;
                }

                return CurrentSize;
            }

            float previous = CurrentSize;
            float next = Mathf.SmoothDamp(
                previous,
                TargetSize,
                ref _velocity,
                SmoothTime,
                Mathf.Infinity,
                deltaTime);

            bool passedTarget = previous < TargetSize
                ? next > TargetSize
                : next < TargetSize;
            if (passedTarget)
            {
                next = TargetSize;
                _velocity = 0f;
            }

            CurrentSize = Mathf.Clamp(next, MinimumSize, MaximumSize);
            return CurrentSize;
        }

        private static void ValidateConfiguration(
            float initialSize,
            float minimumSize,
            float maximumSize,
            float stepFraction,
            float smoothTime,
            float sensitivity)
        {
            if (!IsFinitePositive(initialSize))
            {
                throw new ArgumentOutOfRangeException(nameof(initialSize));
            }

            if (!IsFinitePositive(minimumSize))
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSize));
            }

            if (!IsFinitePositive(maximumSize) || maximumSize < minimumSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSize));
            }

            if (!IsFinitePositive(stepFraction))
            {
                throw new ArgumentOutOfRangeException(nameof(stepFraction));
            }

            if (!IsFinitePositive(smoothTime))
            {
                throw new ArgumentOutOfRangeException(nameof(smoothTime));
            }

            if (!IsFinitePositive(sensitivity))
            {
                throw new ArgumentOutOfRangeException(nameof(sensitivity));
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
