using System;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Compares visual ground anchors and maps their unique sorted ranks to 2D orders.
    /// Lower anchors receive greater ranks and therefore render in front.
    /// </summary>
    public static class GroundAnchorSorting
    {
        public const int DynamicLayerBaseOrder = 1000;
        public const int OrdersPerVisual = 8;
        public const int MinimumOrder = -32768;
        public const int MaximumOrder = 32767;

        public static int CompareBackToFront(
            float leftGroundAnchorY,
            string leftStableId,
            float rightGroundAnchorY,
            string rightStableId)
        {
            ValidateAnchor(leftGroundAnchorY, nameof(leftGroundAnchorY));
            ValidateAnchor(rightGroundAnchorY, nameof(rightGroundAnchorY));
            ValidateStableId(leftStableId, nameof(leftStableId));
            ValidateStableId(rightStableId, nameof(rightStableId));

            int anchorComparison = rightGroundAnchorY.CompareTo(leftGroundAnchorY);
            return anchorComparison != 0
                ? anchorComparison
                : string.Compare(leftStableId, rightStableId, StringComparison.Ordinal);
        }

        public static int OrderForRank(int backToFrontRank, int localOffset = 1)
        {
            if (backToFrontRank < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(backToFrontRank));
            }

            if (localOffset < 0 || localOffset >= OrdersPerVisual)
            {
                throw new ArgumentOutOfRangeException(nameof(localOffset));
            }

            long order = DynamicLayerBaseOrder
                + ((long)backToFrontRank * OrdersPerVisual)
                + localOffset;
            return (int)Math.Min(MaximumOrder, order);
        }

        private static void ValidateAnchor(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A ground anchor must be finite.");
            }
        }

        private static void ValidateStableId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A stable visual identifier is required.",
                    parameterName);
            }
        }
    }
}
