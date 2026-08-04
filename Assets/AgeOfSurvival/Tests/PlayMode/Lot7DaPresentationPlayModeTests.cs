using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    public sealed class Lot7DaPresentationPlayModeTests
    {
        private readonly List<string> _projectErrors = new List<string>();

        [UnityTest]
        public IEnumerator SampleSceneCapturesZoomAndGroundAnchorSorting()
        {
            string artifactDirectory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../TestResults/Review/Lot7DA"));
            Directory.CreateDirectory(artifactDirectory);
            Application.logMessageReceived += HandleLog;

            try
            {
                SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
                yield return null;
                yield return null;

                Camera camera = Camera.main;
                GroundAnchorCameraFollow follow =
                    Object.FindFirstObjectByType<GroundAnchorCameraFollow>();
                DebugPlayerController player =
                    Object.FindFirstObjectByType<DebugPlayerController>();
                DebugResourceInteraction resources =
                    Object.FindFirstObjectByType<DebugResourceInteraction>();
                GroundAnchorSortCoordinator sortCoordinator =
                    Object.FindFirstObjectByType<GroundAnchorSortCoordinator>();

                Assert.That(camera, Is.Not.Null);
                Assert.That(follow, Is.Not.Null);
                Assert.That(player, Is.Not.Null);
                Assert.That(player.Player, Is.Not.Null);
                Assert.That(player.VisualGroundAnchor, Is.Not.Null);
                Assert.That(player.VisualRenderer, Is.Not.Null);
                Assert.That(resources, Is.Not.Null);
                Assert.That(resources.Resources, Is.Not.Empty);
                Assert.That(sortCoordinator, Is.Not.Null);
                Assert.That(sortCoordinator.AppliedPassCount, Is.GreaterThan(0));
                Assert.That(sortCoordinator.ApplicationsInLastAppliedFrame, Is.EqualTo(1));

                float cameraDepth = camera.transform.position.z;
                float initialSize = camera.orthographicSize;
                Assert.That(initialSize, Is.EqualTo(4.0625f).Within(0.0001f));
                Capture(camera, artifactDirectory, "zoom-initial.png");

                follow.ApplyRawScrollPixels(
                    100f * GroundAnchorCameraFollow.DefaultScrollPixelsPerStep);
                float nearFirstStep = follow.AdvanceZoom(1f / 60f);
                Assert.That(nearFirstStep, Is.LessThan(initialSize));
                Assert.That(nearFirstStep, Is.GreaterThan(2.5f));
                AdvanceToTarget(follow);
                float nearSize = camera.orthographicSize;
                Assert.That(nearSize, Is.EqualTo(2.5f).Within(0.0001f));
                Capture(camera, artifactDirectory, "zoom-near.png");

                SetTarget(follow, initialSize);
                AdvanceToTarget(follow);
                float restoredInitialSize = camera.orthographicSize;
                Assert.That(restoredInitialSize, Is.EqualTo(initialSize).Within(0.0001f));

                follow.ApplyRawScrollPixels(
                    -100f * GroundAnchorCameraFollow.DefaultScrollPixelsPerStep);
                AdvanceToTarget(follow);
                float farSize = camera.orthographicSize;
                Assert.That(farSize, Is.EqualTo(8f).Within(0.0001f));
                Capture(camera, artifactDirectory, "zoom-far.png");

                SetTarget(follow, initialSize);
                AdvanceToTarget(follow);

                ResourceState resource = resources.Resources[0];
                Assert.That(
                    resources.TryGetVisualGroundAnchor(resource.Id, out Vector3 resourceAnchor),
                    Is.True);
                Assert.That(
                    resources.TryGetSortingOrder(resource.Id, out int resourceOrder),
                    Is.True);

                MovePlayerTo(
                    player,
                    new WorldPosition(
                        resource.Position.X - 0.75,
                        resource.Position.Y - 0.75));
                yield return null;
                yield return null;
                int playerFrontOrder = player.VisualRenderer.sortingOrder;
                Assert.That(
                    resources.TryGetSortingOrder(resource.Id, out resourceOrder),
                    Is.True);
                int resourceFrontOrder = resourceOrder;
                Assert.That(playerFrontOrder, Is.GreaterThan(resourceOrder));
                Capture(camera, artifactDirectory, "player-in-front.png");
                Vector3 playerFrontAnchor = player.VisualGroundAnchor.position;

                MovePlayerTo(
                    player,
                    new WorldPosition(
                        resource.Position.X + 0.75,
                        resource.Position.Y + 0.75));
                yield return null;
                yield return null;
                Assert.That(
                    resources.TryGetSortingOrder(resource.Id, out resourceOrder),
                    Is.True);
                int resourceBehindOrder = resourceOrder;
                int playerBehindOrder = player.VisualRenderer.sortingOrder;
                Assert.That(playerBehindOrder, Is.LessThan(resourceOrder));
                Capture(camera, artifactDirectory, "player-behind.png");
                Vector3 playerBehindAnchor = player.VisualGroundAnchor.position;

                Assert.That(camera.transform.position.x, Is.EqualTo(playerBehindAnchor.x));
                Assert.That(camera.transform.position.y, Is.EqualTo(playerBehindAnchor.y));
                Assert.That(camera.transform.position.z, Is.EqualTo(cameraDepth));
                Assert.That(player.VisualGroundAnchor.localScale.x, Is.EqualTo(1.2f));
                Assert.That(player.VisualGroundAnchor.localScale.y, Is.EqualTo(1.2f));
                Assert.That(
                    player.VisualRenderer.sprite.pivot.y
                    / player.VisualRenderer.sprite.rect.height,
                    Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(
                    resources.GeneratedBodySprite.pivot.y
                    / resources.GeneratedBodySprite.rect.height,
                    Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(sortCoordinator.ApplicationsInLastAppliedFrame, Is.EqualTo(1));
                Assert.That(
                    sortCoordinator.ActiveEntryCount,
                    Is.EqualTo(resources.RenderedMarkerCount + 1));
                Assert.That(_projectErrors, Is.Empty);

                var report = new StringBuilder();
                report.AppendLine("Lot 7D-A Play Mode validation");
                report.AppendLine($"Unity={Application.unityVersion}");
                report.AppendLine("scene=SampleScene");
                report.AppendLine($"orthographic.initial={Format(initialSize)}");
                report.AppendLine($"orthographic.near.firstStep={Format(nearFirstStep)}");
                report.AppendLine($"orthographic.near={Format(nearSize)}");
                report.AppendLine($"orthographic.restoredInitial={Format(restoredInitialSize)}");
                report.AppendLine($"orthographic.far={Format(farSize)}");
                report.AppendLine($"camera.z={Format(cameraDepth)}");
                report.AppendLine($"resource.id={resource.Id}");
                report.AppendLine($"resource.anchor={Format(resourceAnchor)}");
                report.AppendLine($"player.front.anchor={Format(playerFrontAnchor)}");
                report.AppendLine($"player.front.order={playerFrontOrder}");
                report.AppendLine($"resource.withPlayerFront.order={resourceFrontOrder}");
                report.AppendLine($"player.behind.anchor={Format(playerBehindAnchor)}");
                report.AppendLine($"player.behind.order={playerBehindOrder}");
                report.AppendLine($"resource.withPlayerBehind.order={resourceBehindOrder}");
                report.AppendLine("player.scale=1.2000");
                report.AppendLine("player.pivot=(0.5000,0.1200)");
                report.AppendLine("resource.pivot=(0.5000,0.1200)");
                report.AppendLine($"sorting.appliedPassCount={sortCoordinator.AppliedPassCount}");
                report.AppendLine($"sorting.lastFrameApplications={sortCoordinator.ApplicationsInLastAppliedFrame}");
                report.AppendLine($"sorting.activeEntries={sortCoordinator.ActiveEntryCount}");
                report.AppendLine($"project.errorCount={_projectErrors.Count}");
                File.WriteAllText(
                    Path.Combine(artifactDirectory, "playmode-report.txt"),
                    report.ToString());
            }
            finally
            {
                Application.logMessageReceived -= HandleLog;
            }
        }

        private static void AdvanceToTarget(GroundAnchorCameraFollow follow)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                follow.AdvanceZoom(1f / 60f);
            }

            Assert.That(
                follow.CurrentOrthographicSize,
                Is.EqualTo(follow.TargetOrthographicSize).Within(0.0001f));
        }

        private static void SetTarget(GroundAnchorCameraFollow follow, float desired)
        {
            float logicalSteps = -Mathf.Log(desired / follow.TargetOrthographicSize)
                / Mathf.Log(1.1f);
            follow.ApplyRawScrollPixels(
                logicalSteps * GroundAnchorCameraFollow.DefaultScrollPixelsPerStep);
        }

        private static void MovePlayerTo(DebugPlayerController controller, WorldPosition target)
        {
            double deltaX = target.X - controller.Player.Position.X;
            double deltaY = target.Y - controller.Player.Position.Y;
            double distance = System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= 0.0)
            {
                return;
            }

            PlayerMovement.Step(
                controller.Player,
                deltaX / distance,
                deltaY / distance,
                distance,
                1.0);
        }

        private static void Capture(Camera camera, string directory, string fileName)
        {
            const int width = 1280;
            const int height = 720;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(Path.Combine(directory, fileName), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static string Format(float value)
        {
            return value.ToString("F4", CultureInfo.InvariantCulture);
        }

        private static string Format(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F4},{1:F4},{2:F4})",
                value.x,
                value.y,
                value.z);
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _projectErrors.Add($"{type}: {condition}");
            }
        }
    }
}
