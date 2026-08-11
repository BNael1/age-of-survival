using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Runtime.Rendering;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Resources
{
    public sealed partial class DebugResourceInteraction
    {
        private readonly Dictionary<GroundContainerId, StandaloneGroundMarker>
            _standaloneGroundMarkers =
                new Dictionary<GroundContainerId, StandaloneGroundMarker>();

        public int RenderedStandaloneGroundPileCount
        {
            get
            {
                int count = 0;
                foreach (StandaloneGroundMarker marker in _standaloneGroundMarkers.Values)
                    if (marker.Root.activeSelf) count++;
                return count;
            }
        }

        public bool TryGetStandaloneGroundVisual(
            GroundContainerId id,
            out GameObject visual)
        {
            if (_standaloneGroundMarkers.TryGetValue(id, out StandaloneGroundMarker marker))
            {
                visual = marker.Root;
                return visual != null;
            }
            visual = null;
            return false;
        }

        private void SynchronizeStandaloneGroundContainers(TransferActionState action)
        {
            if (_session == null || _generatedRoot == null) return;

            var retained = new HashSet<GroundContainerId>();
            for (int index = 0; index < _session.GroundContainers.Count; index++)
            {
                GroundContainerState ground = _session.GroundContainers[index];
                if (ground == null || ground.IsEmpty || IsGroundOwnedByResource(ground.Id))
                    continue;

                retained.Add(ground.Id);
                if (!_standaloneGroundMarkers.TryGetValue(
                        ground.Id,
                        out StandaloneGroundMarker marker))
                {
                    marker = CreateStandaloneGroundMarker(ground);
                    _standaloneGroundMarkers.Add(ground.Id, marker);
                }

                ItemDefinitionId displayedDefinition = FirstStackDefinition(ground);
                marker.BodyRenderer.sprite = GroundSpriteFor(displayedDefinition);
                marker.QuantityLabel.text = "x" + TotalGroundQuantity(ground);
                marker.Root.SetActive(true);
                if (worldRenderer != null)
                {
                    marker.Root.transform.position = worldRenderer.LogicalToWorldPosition(
                        ground.Position,
                        visualYOffset,
                        -0.05f);
                }

                bool active = action != null
                    && action.Status == TransferActionStatus.Active
                    && action.SourceId.Equals(ground.Container.Id);
                marker.ProgressRoot.SetActive(active && _uiSprite != null);
                if (active)
                    SetProgress(marker.ProgressFill, (float)action.ProgressAt(_session.CurrentTick));
            }

            var stale = new List<GroundContainerId>();
            foreach (GroundContainerId id in _standaloneGroundMarkers.Keys)
                if (!retained.Contains(id)) stale.Add(id);
            for (int index = 0; index < stale.Count; index++)
                DestroyStandaloneGroundMarker(stale[index]);
        }

        private StandaloneGroundMarker CreateStandaloneGroundMarker(
            GroundContainerState ground)
        {
            var markerObject = new GameObject("Ground " + ground.Id.Value);
            markerObject.transform.SetParent(_generatedRoot, false);

            var bodyRenderer = markerObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = GroundSpriteFor(FirstStackDefinition(ground));
            bodyRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            var quantityObject = new GameObject("Ground Quantity");
            quantityObject.transform.SetParent(markerObject.transform, false);
            quantityObject.transform.localPosition = new Vector3(0.24f, 0.18f, 0f);
            var quantityLabel = quantityObject.AddComponent<TextMesh>();
            quantityLabel.fontSize = 30;
            quantityLabel.fontStyle = FontStyle.Bold;
            quantityLabel.characterSize = 0.032f;
            quantityLabel.color = new Color32(250, 244, 215, 255);
            quantityLabel.anchor = TextAnchor.MiddleLeft;
            MeshRenderer quantityRenderer = quantityLabel.GetComponent<MeshRenderer>();

            CreateProgressBar(
                markerObject.transform,
                out GameObject progressRoot,
                out SpriteRenderer progressBackground,
                out SpriteRenderer progressFill);

            ResolveSortCoordinator().Register(
                StandaloneGroundStableId(ground.Id),
                markerObject.transform,
                markerObject,
                new RendererOrderBinding(bodyRenderer, 1),
                new RendererOrderBinding(quantityRenderer, 5),
                new RendererOrderBinding(progressBackground, 6),
                new RendererOrderBinding(progressFill, 7));

            return new StandaloneGroundMarker(
                markerObject,
                bodyRenderer,
                quantityLabel,
                progressRoot,
                progressFill);
        }

        private bool IsGroundOwnedByResource(GroundContainerId groundId)
        {
            for (int index = 0; index < _session.Resources.Count; index++)
            {
                if (ResourceYieldOperations.GroundIdFor(_session.Resources[index].Id)
                    .Equals(groundId))
                {
                    return true;
                }
            }
            return false;
        }

        private static ItemDefinitionId FirstStackDefinition(GroundContainerState ground)
        {
            for (int index = 0; index < ground.Container.Entries.Count; index++)
            {
                InventoryEntry entry = ground.Container.Entries[index];
                if (entry.Kind == ItemStateKind.Stackable && entry.Quantity > 0)
                    return entry.DefinitionId;
            }
            return default;
        }

        private static int TotalGroundQuantity(GroundContainerState ground)
        {
            int total = 0;
            for (int index = 0; index < ground.Container.Entries.Count; index++)
                total = checked(total + ground.Container.Entries[index].Quantity);
            return total;
        }

        private void DestroyStandaloneGroundMarkers()
        {
            var ids = new List<GroundContainerId>(_standaloneGroundMarkers.Keys);
            for (int index = 0; index < ids.Count; index++)
                DestroyStandaloneGroundMarker(ids[index]);
        }

        private void DestroyStandaloneGroundMarker(GroundContainerId id)
        {
            if (!_standaloneGroundMarkers.TryGetValue(id, out StandaloneGroundMarker marker))
                return;
            _sortCoordinator?.Unregister(StandaloneGroundStableId(id));
            DestroyUnityObject(marker.Root);
            _standaloneGroundMarkers.Remove(id);
        }

        private static string StandaloneGroundStableId(GroundContainerId id) =>
            "ground-container:" + id.Value;

        private sealed class StandaloneGroundMarker
        {
            public StandaloneGroundMarker(
                GameObject root,
                SpriteRenderer bodyRenderer,
                TextMesh quantityLabel,
                GameObject progressRoot,
                SpriteRenderer progressFill)
            {
                Root = root;
                BodyRenderer = bodyRenderer;
                QuantityLabel = quantityLabel;
                ProgressRoot = progressRoot;
                ProgressFill = progressFill;
            }

            public GameObject Root { get; }
            public SpriteRenderer BodyRenderer { get; }
            public TextMesh QuantityLabel { get; }
            public GameObject ProgressRoot { get; }
            public SpriteRenderer ProgressFill { get; }
        }
    }
}
