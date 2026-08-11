using System.Reflection;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Tests
{
    public sealed class ConstructionUiRuntimeTests
    {
        [Test]
        public void ConstructionPanel_IsAboveInventoryAndBelowPause()
        {
            Assert.That(ConstructionRuntimeBehaviour.SortingOrder,
                Is.GreaterThan(InventoryPrototypeUiBehaviour.SortingOrder));
            Assert.That(ConstructionRuntimeBehaviour.SortingOrder,
                Is.LessThan(PauseMenuBehaviour.SortingOrder));
        }

        [Test]
        public void PointerProjector_ProjectsOrthographicCameraToWorldPlane()
        {
            var cameraObject = new GameObject("construction-camera-test");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.transform.position = new Vector3(2f, 3f, -10f);

                bool projected = ConstructionPointerProjector.TryProjectToWorldPlane(
                    camera,
                    new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.5f),
                    0f,
                    out Vector3 world);

                Assert.That(projected, Is.True);
                Assert.That(world.x, Is.EqualTo(2f).Within(0.001f));
                Assert.That(world.y, Is.EqualTo(3f).Within(0.001f));
                Assert.That(world.z, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void PrototypeVisualCatalog_IncludesConstructionSprites()
        {
            Assert.That(PrototypeVisualAssets.ConstructionFloor, Is.EqualTo("construction_floor"));
            Assert.That(PrototypeVisualAssets.ConstructionSiteReady, Is.EqualTo("construction_site_ready"));
        }

        [Test]
        public void GeneratedConstructionPanel_UsesDeclaredSortingOrder()
        {
            var host = new GameObject("construction-ui-sorting-test");
            var world = new GameObject("construction-ui-world-test");
            var cameraHost = new GameObject("construction-ui-camera-test");
            try
            {
                world.AddComponent<DebugIsometricWorld>();
                cameraHost.tag = "MainCamera";
                cameraHost.AddComponent<Camera>();
                ConstructionRuntimeBehaviour behaviour = host.AddComponent<ConstructionRuntimeBehaviour>();
                MethodInfo start = typeof(ConstructionRuntimeBehaviour).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(start, Is.Not.Null);
                start.Invoke(behaviour, null);

                UIDocument document = host.GetComponent<UIDocument>();
                Assert.That(document, Is.Not.Null);
                Assert.That(document.panelSettings.sortingOrder,
                    Is.EqualTo(ConstructionRuntimeBehaviour.SortingOrder));
                Assert.That(document.sortingOrder,
                    Is.EqualTo(ConstructionRuntimeBehaviour.SortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(world);
                Object.DestroyImmediate(cameraHost);
            }
        }
    }
}
