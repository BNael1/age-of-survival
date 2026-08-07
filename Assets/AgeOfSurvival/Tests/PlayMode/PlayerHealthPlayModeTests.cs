using System;
using System.Collections;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using AgeOfSurvival.Runtime.Persistence;
using AgeOfSurvival.Runtime.Player;
using AgeOfSurvival.Runtime.Rendering;
using AgeOfSurvival.Runtime.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Presentation.PlayMode.Tests
{
    public sealed class PlayerHealthPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameplayInputGate.SetBlocked(false);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayBootstrapCreatesHealthHudAndDamageZone()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadGameplay();

            PlayerHealthHudBehaviour hud = null;
            DebugPlayerController controller = null;
            yield return WaitUntil(() =>
            {
                hud = FindInActiveScene<PlayerHealthHudBehaviour>();
                controller = FindInActiveScene<DebugPlayerController>();
                return hud != null
                    && hud.Ui != null
                    && controller != null
                    && controller.DamageZone != null
                    && controller.DamageZoneVisual != null;
            });

            Assert.That(hud.Ui.HealthValue.text, Is.EqualTo("100 / 100"));
            Assert.That(hud.Ui.HealthBar.value, Is.EqualTo(100f));
            Assert.That(controller.DamageZoneVisual.activeSelf, Is.True);
            Assert.That(
                controller.DamageZone.Center,
                Is.EqualTo(
                    controller.RespawnPosition.Translate(
                        PrototypeDamageZoneRules.OffsetX,
                        PrototypeDamageZoneRules.OffsetY)));
        }

        [UnityTest]
        public IEnumerator DamageZoneUpdatesCanonicalHealthAndHud()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadGameplay();

            PlayerHealthHudBehaviour hud = null;
            DebugPlayerController controller = null;
            yield return WaitUntil(() =>
            {
                hud = FindInActiveScene<PlayerHealthHudBehaviour>();
                controller = FindInActiveScene<DebugPlayerController>();
                return hud != null
                    && hud.Ui != null
                    && controller != null
                    && controller.Player != null
                    && controller.DamageZone != null;
            });

            InventoryPrototypeSession session =
                InventoryPrototypeSessionProvider.Current;
            PlayerMovement.Reposition(
                controller.Player,
                controller.DamageZone.Center);

            yield return WaitUntil(() =>
                session.Health.CurrentHealth
                    < PlayerHealthRules.DefaultMaximumHealth);
            yield return null;

            Assert.That(session.Health.CurrentHealth, Is.EqualTo(75));
            Assert.That(hud.Ui.HealthValue.text, Is.EqualTo("75 / 100"));
            Assert.That(hud.Ui.HealthBar.value, Is.EqualTo(75f));
        }

        [UnityTest]
        public IEnumerator DeathRespawnsAtGeneratedSpawnWithoutInventoryLoss()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadGameplay();

            DebugPlayerController controller = null;
            yield return WaitUntil(() =>
            {
                controller = FindInActiveScene<DebugPlayerController>();
                return controller != null
                    && controller.Player != null
                    && controller.DamageZone != null;
            });

            InventoryPrototypeSession session =
                InventoryPrototypeSessionProvider.Current;
            int branchesBefore = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches.Id);
            long tickBefore = session.CurrentTick;

            session.ApplyDamage(
                PlayerHealthRules.DefaultMaximumHealth);

            yield return WaitUntil(() =>
                session.CurrentTick > tickBefore
                && !session.Health.IsDead
                && controller.Player.Position.Equals(
                    controller.RespawnPosition));

            Assert.That(
                session.Health.CurrentHealth,
                Is.EqualTo(PlayerHealthRules.DefaultMaximumHealth));
            Assert.That(
                session.CurrentPlayerPosition,
                Is.EqualTo(controller.RespawnPosition));
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(branchesBefore));
        }


        [UnityTest]
        public IEnumerator HealthHudLayoutAndDamageZonePresentationStayStable()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadGameplay();

            PlayerHealthHudBehaviour hud = null;
            DebugPlayerController controller = null;
            DebugResourceInteraction interaction = null;
            yield return WaitUntil(() =>
            {
                hud = FindInActiveScene<PlayerHealthHudBehaviour>();
                controller = FindInActiveScene<DebugPlayerController>();
                interaction = FindInActiveScene<DebugResourceInteraction>();
                return hud != null
                    && hud.Ui != null
                    && controller != null
                    && controller.DamageZoneVisual != null
                    && interaction != null;
            });
            yield return null;

            UIDocument document = hud.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.panelSettings, Is.Not.Null);
            Assert.That(
                document.panelSettings.scaleMode,
                Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(
                document.panelSettings.referenceResolution,
                Is.EqualTo(new Vector2Int(1280, 720)));
            Assert.That(document.sortingOrder, Is.EqualTo(210));

            Assert.That(
                hud.Ui.Panel.style.position.value,
                Is.EqualTo(Position.Absolute));
            Assert.That(
                hud.Ui.Panel.style.left.value.value,
                Is.EqualTo(16f).Within(0.1f));
            Assert.That(
                hud.Ui.Panel.style.bottom.value.value,
                Is.EqualTo(16f).Within(0.1f));
            Assert.That(
                hud.Ui.Panel.style.width.value.value,
                Is.EqualTo(280f).Within(0.1f));
            Assert.That(hud.Ui.HealthValue.text, Is.EqualTo("100 / 100"));
            Assert.That(hud.Ui.HealthBar.lowValue, Is.Zero);
            Assert.That(hud.Ui.HealthBar.highValue, Is.EqualTo(100f));
            Assert.That(hud.Ui.HealthBar.value, Is.EqualTo(100f));

            SpriteRenderer damageRenderer =
                controller.DamageZoneVisual.GetComponent<SpriteRenderer>();
            SpriteRenderer targetRenderer =
                FindResourceTargetRenderer(interaction);
            Assert.That(damageRenderer, Is.Not.Null);
            Assert.That(targetRenderer, Is.Not.Null);

            Color32 damageTint = damageRenderer.color;
            Color32 targetTint = targetRenderer.color;
            Assert.That(
                damageTint,
                Is.EqualTo(new Color32(220, 48, 44, 220)));
            Assert.That(
                targetTint,
                Is.EqualTo(new Color32(255, 255, 255, 255)));
            Assert.That(damageTint, Is.Not.EqualTo(targetTint));
            Assert.That(
                damageRenderer.sprite.name,
                Is.EqualTo("Prototype Damage Zone Ring"));
            Assert.That(
                targetRenderer.sprite.name,
                Is.EqualTo("Prototype Target Ring"));
            Assert.That(
                controller.DamageZoneVisual.name,
                Is.Not.EqualTo(targetRenderer.gameObject.name));
        }

        [UnityTest]
        public IEnumerator FullDamageSequenceRespawnsWithCameraAndInventoryIntact()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadGameplay();

            PlayerHealthHudBehaviour hud = null;
            DebugPlayerController controller = null;
            GroundAnchorCameraFollow cameraFollow = null;
            yield return WaitUntil(() =>
            {
                hud = FindInActiveScene<PlayerHealthHudBehaviour>();
                controller = FindInActiveScene<DebugPlayerController>();
                cameraFollow = FindInActiveScene<GroundAnchorCameraFollow>();
                return hud != null
                    && hud.Ui != null
                    && controller != null
                    && controller.Player != null
                    && controller.VisualGroundAnchor != null
                    && controller.DamageZone != null
                    && cameraFollow != null;
            });

            InventoryPrototypeSession session =
                InventoryPrototypeSessionProvider.Current;
            Assert.That(
                session.Commands.Equip(
                    new InventorySelection(
                        session.MainContainer.Id,
                        InventoryPrototypeCatalog.Tool.Id,
                        session.Tool.InstanceId,
                        true),
                    EquipmentSlot.RightHand).Succeeded,
                Is.True);
            Assert.That(
                session.Commands.Equip(
                    new InventorySelection(
                        session.MainContainer.Id,
                        InventoryPrototypeCatalog.Bag.Id,
                        session.Bag.InstanceId,
                        true),
                    EquipmentSlot.Back).Succeeded,
                Is.True);

            InventoryEquipmentSnapshot equipmentBefore =
                session.Inventory.CaptureSnapshot().Equipment;
            int branchesBefore = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Branches.Id);
            int mainStonesBefore = InventoryOperations.Count(
                session.MainContainer,
                InventoryPrototypeCatalog.Stones.Id);
            int bagStonesBefore = InventoryOperations.Count(
                session.BagContainer,
                InventoryPrototypeCatalog.Stones.Id);
            ContainerId toolOwnerBefore =
                FindOwnerId(session, session.Tool.InstanceId);
            ContainerId bagOwnerBefore =
                FindOwnerId(session, session.Bag.InstanceId);

            PlayerMovement.Reposition(
                controller.Player,
                controller.DamageZone.Center);

            yield return WaitForHealthAndHud(session, hud, 75);
            yield return WaitForHealthAndHud(session, hud, 50);
            yield return WaitForHealthAndHud(session, hud, 25);

            long tickAtTwentyFive = session.CurrentTick;
            yield return WaitUntil(() =>
                session.CurrentTick > tickAtTwentyFive
                && session.Health.CurrentHealth
                    == PlayerHealthRules.DefaultMaximumHealth
                && controller.Player.Position.Equals(
                    controller.RespawnPosition)
                && session.CurrentPlayerPosition.Equals(
                    controller.RespawnPosition));
            yield return null;

            Assert.That(hud.Ui.HealthValue.text, Is.EqualTo("100 / 100"));
            Assert.That(hud.Ui.HealthBar.value, Is.EqualTo(100f));
            Assert.That(
                session.Inventory.CaptureSnapshot().Equipment,
                Is.EqualTo(equipmentBefore));
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Branches.Id),
                Is.EqualTo(branchesBefore));
            Assert.That(
                InventoryOperations.Count(
                    session.MainContainer,
                    InventoryPrototypeCatalog.Stones.Id),
                Is.EqualTo(mainStonesBefore));
            Assert.That(
                InventoryOperations.Count(
                    session.BagContainer,
                    InventoryPrototypeCatalog.Stones.Id),
                Is.EqualTo(bagStonesBefore));
            Assert.That(
                FindOwnerId(session, session.Tool.InstanceId),
                Is.EqualTo(toolOwnerBefore));
            Assert.That(
                FindOwnerId(session, session.Bag.InstanceId),
                Is.EqualTo(bagOwnerBefore));

            Assert.That(
                cameraFollow.GroundAnchor,
                Is.SameAs(controller.VisualGroundAnchor));
            Camera trackedCamera = cameraFollow.GetComponent<Camera>();
            Assert.That(trackedCamera, Is.Not.Null);
            Assert.That(
                trackedCamera.transform.position.x,
                Is.EqualTo(
                    controller.VisualGroundAnchor.position.x).Within(0.001f));
            Assert.That(
                trackedCamera.transform.position.y,
                Is.EqualTo(
                    controller.VisualGroundAnchor.position.y).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PauseFreezesDamageScheduleAndResumeContinuesIt()
        {
            PrototypeSaveRuntime.BeginNewGame(default);
            yield return LoadGameplay();

            PlayerHealthHudBehaviour hud = null;
            DebugPlayerController controller = null;
            PauseMenuBehaviour pause = null;
            yield return WaitUntil(() =>
            {
                hud = FindInActiveScene<PlayerHealthHudBehaviour>();
                controller = FindInActiveScene<DebugPlayerController>();
                pause = FindInActiveScene<PauseMenuBehaviour>();
                return hud != null
                    && hud.Ui != null
                    && controller != null
                    && controller.Player != null
                    && controller.DamageZone != null
                    && pause != null;
            });

            InventoryPrototypeSession session =
                InventoryPrototypeSessionProvider.Current;
            PlayerMovement.Reposition(
                controller.Player,
                controller.DamageZone.Center);
            yield return WaitForHealthAndHud(session, hud, 75);

            long tickBeforePause = session.CurrentTick;
            int healthBeforePause = session.Health.CurrentHealth;
            long? nextDamageBeforePause =
                controller.DamageZone.NextDamageTick;

            pause.SetPaused(true);
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(GameplayInputGate.IsBlocked, Is.True);

            yield return new WaitForSecondsRealtime(2.25f);

            Assert.That(session.CurrentTick, Is.EqualTo(tickBeforePause));
            Assert.That(
                session.Health.CurrentHealth,
                Is.EqualTo(healthBeforePause));
            Assert.That(
                controller.DamageZone.NextDamageTick,
                Is.EqualTo(nextDamageBeforePause));
            Assert.That(hud.Ui.HealthValue.text, Is.EqualTo("75 / 100"));
            Assert.That(hud.Ui.HealthBar.value, Is.EqualTo(75f));

            pause.SetPaused(false);
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(GameplayInputGate.IsBlocked, Is.False);

            yield return WaitForHealthAndHud(session, hud, 50);
            Assert.That(session.CurrentTick, Is.GreaterThan(tickBeforePause));
        }

        private static IEnumerator LoadGameplay()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                FrontendSceneNames.Gameplay,
                LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(
            Func<bool> predicate,
            float timeoutSeconds = 5f)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            if (float.IsNaN(timeoutSeconds)
                || float.IsInfinity(timeoutSeconds)
                || timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    timeoutSeconds,
                    "The PlayMode timeout must be finite and greater than zero.");
            }

            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Condition not met after {timeoutSeconds:0.###} real seconds.");
        }


        private static IEnumerator WaitForHealthAndHud(
            InventoryPrototypeSession session,
            PlayerHealthHudBehaviour hud,
            int expectedHealth)
        {
            yield return WaitUntil(() =>
                session.Health.CurrentHealth == expectedHealth
                && hud.Ui.HealthValue.text
                    == $"{expectedHealth} / "
                    + $"{PlayerHealthRules.DefaultMaximumHealth}"
                && Mathf.Approximately(
                    hud.Ui.HealthBar.value,
                    expectedHealth));
        }

        private static SpriteRenderer FindResourceTargetRenderer(
            DebugResourceInteraction interaction)
        {
            SpriteRenderer[] renderers =
                interaction.GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].gameObject.name == "Target Indicator")
                {
                    return renderers[index];
                }
            }

            return null;
        }

        private static ContainerId FindOwnerId(
            InventoryPrototypeSession session,
            ItemInstanceId instanceId)
        {
            bool found = session.Inventory.TryFindUnique(
                instanceId,
                out _,
                out _,
                out ContainerState owner);
            Assert.That(found, Is.True);
            Assert.That(owner, Is.Not.Null);
            return owner.Id;
        }

        private static T FindInActiveScene<T>()
            where T : Component
        {
            Scene activeScene = SceneManager.GetActiveScene();
            T[] candidates = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].gameObject.scene == activeScene)
                {
                    return candidates[index];
                }
            }

            return null;
        }
    }
}
