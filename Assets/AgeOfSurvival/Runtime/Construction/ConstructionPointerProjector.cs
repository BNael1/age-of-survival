using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Construction
{
    public static class ConstructionPointerProjector
    {
        public static bool TryProjectToWorldPlane(
            Camera camera,
            Vector2 screenPosition,
            float worldPlaneZ,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (camera == null || !IsFinite(screenPosition) || !IsFinite(worldPlaneZ))
                return false;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            float denominator = ray.direction.z;
            if (!IsFinite(denominator) || Mathf.Abs(denominator) <= 0.000001f)
                return false;

            float distance = (worldPlaneZ - ray.origin.z) / denominator;
            if (!IsFinite(distance) || distance < 0f)
                return false;

            worldPosition = ray.GetPoint(distance);
            return IsFinite(worldPosition);
        }

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public interface IRuntimeUiPointerBlocker
    {
        bool IsBlocked(Vector2 screenPosition);
    }

    public sealed class UiToolkitRuntimePointerBlocker : IRuntimeUiPointerBlocker
    {
        public bool IsBlocked(Vector2 screenPosition)
        {
            if (float.IsNaN(screenPosition.x)
                || float.IsInfinity(screenPosition.x)
                || float.IsNaN(screenPosition.y)
                || float.IsInfinity(screenPosition.y))
                return true;

            UIDocument[] documents = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < documents.Length; index++)
            {
                UIDocument document = documents[index];
                if (!document.enabled || !document.gameObject.activeInHierarchy)
                    continue;

                VisualElement root = document.rootVisualElement;
                if (root == null || root.panel == null
                    || root.resolvedStyle.display == DisplayStyle.None)
                    continue;

                Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(
                    root.panel,
                    screenPosition);
                VisualElement picked = root.panel.Pick(panelPosition);
                if (picked != null && IsInsideDocument(picked, root))
                    return true;
            }

            return false;
        }

        private static bool IsInsideDocument(VisualElement element, VisualElement root)
        {
            for (VisualElement current = element; current != null; current = current.parent)
                if (ReferenceEquals(current, root)) return true;
            return false;
        }
    }
}
