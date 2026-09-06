using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Runtime.Rendering;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Construction
{
    public sealed class ConstructionWorldPresenter : IDisposable
    {
        private const int FlatConstructionSortingOrder = 92;

        private readonly Transform _root;
        private readonly DebugIsometricWorld _worldRenderer;
        private readonly ConstructionRuntimeSession _session;
        private readonly GroundAnchorSortCoordinator _sortCoordinator;
        private readonly Dictionary<ConstructionInstanceId, SpriteRenderer> _renderers =
            new Dictionary<ConstructionInstanceId, SpriteRenderer>();
        private readonly HashSet<ConstructionInstanceId> _sortRegistrations =
            new HashSet<ConstructionInstanceId>();
        private readonly Dictionary<string, Sprite> _sprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public ConstructionWorldPresenter(
            Transform host,
            DebugIsometricWorld worldRenderer,
            ConstructionRuntimeSession session,
            GroundAnchorSortCoordinator sortCoordinator)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            _worldRenderer = worldRenderer ?? throw new ArgumentNullException(nameof(worldRenderer));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _sortCoordinator = sortCoordinator;

            var rootObject = new GameObject("Construction World Visuals");
            rootObject.transform.SetParent(host, false);
            _root = rootObject.transform;
        }

        public int PresentedCount => _renderers.Count;
        public int SortingRegistrationCount => _sortRegistrations.Count;

        public bool TryGetRenderer(
            ConstructionInstanceId instanceId,
            out SpriteRenderer renderer) =>
            _renderers.TryGetValue(instanceId, out renderer);

        public static string StableVisualId(ConstructionInstanceId instanceId)
        {
            if (!instanceId.IsValid)
                throw new ArgumentException("A valid construction instance is required.", nameof(instanceId));
            return "construction:" + instanceId.Value;
        }

        public void Refresh()
        {
            var retained = new HashSet<ConstructionInstanceId>();
            ConstructionSiteState[] sites = _session.World.CaptureCanonicalSites();
            for (int index = 0; index < sites.Length; index++)
            {
                ConstructionSiteState site = sites[index];
                retained.Add(site.InstanceId);
                int deposited = TotalDeposited(site.InstanceId);
                int required = TotalRequired(site.DefinitionId);
                string asset = deposited == 0
                    ? "construction_site_empty"
                    : deposited < required
                        ? "construction_site_partial"
                        : "construction_site_ready";
                Present(site.InstanceId, site.Space, asset, new Color32(255, 255, 255, 255));
            }

            CompletedStructureState[] structures = _session.World.CaptureCanonicalStructures();
            for (int index = 0; index < structures.Length; index++)
            {
                CompletedStructureState structure = structures[index];
                retained.Add(structure.InstanceId);
                string asset = CompletedAsset(structure);
                Present(structure.InstanceId, structure.Space, asset,
                    structure.Space.Kind == ConstructionSpaceKind.Roof
                        ? new Color32(125, 186, 216, 125)
                        : structure.DefinitionId.Equals(ConstructionPrototypeCatalog.DoorId)
                            ? new Color32(235, 185, 110, 255) : Color.white);
            }

            var stale = new List<ConstructionInstanceId>();
            foreach (KeyValuePair<ConstructionInstanceId, SpriteRenderer> pair in _renderers)
                if (!retained.Contains(pair.Key)) stale.Add(pair.Key);
            for (int index = 0; index < stale.Count; index++)
            {
                UnregisterSorting(stale[index]);
                SpriteRenderer renderer = _renderers[stale[index]];
                DestroyObject(renderer.gameObject);
                _renderers.Remove(stale[index]);
            }
        }

        public void Dispose()
        {
            if (_sortCoordinator != null)
            {
                foreach (ConstructionInstanceId id in _sortRegistrations)
                    _sortCoordinator.Unregister(StableVisualId(id));
            }
            _sortRegistrations.Clear();
            foreach (Sprite sprite in _sprites.Values)
                PrototypeVisualAssets.DestroyRuntimeSprite(sprite);
            _sprites.Clear();
            _renderers.Clear();
            if (_root != null) DestroyObject(_root.gameObject);
        }

        private void Present(
            ConstructionInstanceId id,
            ConstructionSpaceKey space,
            string asset,
            Color color)
        {
            if (!_worldRenderer.TryMapConstructionSpaceCenter(space, out Vector3 position))
                return;

            if (!_renderers.TryGetValue(id, out SpriteRenderer renderer))
            {
                var visual = new GameObject($"Construction {id}");
                visual.transform.SetParent(_root, false);
                renderer = visual.AddComponent<SpriteRenderer>();
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                renderer.sortingOrder = FlatConstructionSortingOrder;
                _renderers.Add(id, renderer);
            }

            renderer.transform.position = new Vector3(position.x, position.y, -0.045f);
            renderer.sprite = RequireSprite(asset);
            renderer.color = color;
            SynchronizeSorting(id, renderer, asset);
            if (space.Kind == ConstructionSpaceKind.Roof)
            {
                UnregisterSorting(id);
                renderer.sortingOrder = FlatConstructionSortingOrder + 1;
            }
        }

        private void SynchronizeSorting(
            ConstructionInstanceId id,
            SpriteRenderer renderer,
            string asset)
        {
            bool participates = !string.Equals(
                asset,
                "construction_floor",
                StringComparison.Ordinal);
            if (!participates)
            {
                UnregisterSorting(id);
                renderer.sortingOrder = FlatConstructionSortingOrder;
                return;
            }

            if (_sortCoordinator == null)
            {
                renderer.sortingOrder = GroundAnchorSorting.OrderForRank(0, 1);
                return;
            }

            _sortCoordinator.Register(
                StableVisualId(id),
                renderer.transform,
                renderer.gameObject,
                new RendererOrderBinding(renderer, 1));
            _sortRegistrations.Add(id);
        }

        private void UnregisterSorting(ConstructionInstanceId id)
        {
            if (!_sortRegistrations.Remove(id)) return;
            _sortCoordinator?.Unregister(StableVisualId(id));
        }

        private Sprite RequireSprite(string asset)
        {
            if (_sprites.TryGetValue(asset, out Sprite sprite)) return sprite;

            Vector2 pivot = asset == "construction_floor"
                ? new Vector2(0.5f, 0.5f)
                : new Vector2(0.5f, 0.12f);
            sprite = PrototypeVisualAssets.CreateSprite(
                asset,
                pivot,
                PrototypeVisualAssets.PixelsPerUnit,
                asset);
            _sprites.Add(asset, sprite);
            return sprite;
        }

        private string CompletedAsset(CompletedStructureState structure)
        {
            string key = _session.Catalog.Require(structure.DefinitionId).VisualKey;
            if (_session.Doors.TryGet(structure.InstanceId, out ConstructionDoorState door))
                key = door.IsOpen ? "construction_opening" : "construction_wall";
            if (structure.Space.Kind != ConstructionSpaceKind.Edge) return key;
            return key + (structure.Space.EdgeAddress.Axis == ConstructionEdgeAxis.Vertical
                ? "_vertical"
                : "_horizontal");
        }

        private int TotalRequired(ConstructionDefinitionId definitionId)
        {
            ConstructionDefinition definition = _session.Catalog.Require(definitionId).Core;
            int total = 0;
            for (int index = 0; index < definition.Materials.Count; index++)
                total = checked(total + definition.Materials[index].Quantity);
            return total;
        }

        private int TotalDeposited(ConstructionInstanceId instanceId)
        {
            ConstructionMaterialQuantity[] materials =
                _session.World.CaptureDepositedMaterials(instanceId);
            int total = 0;
            for (int index = 0; index < materials.Length; index++)
                total = checked(total + materials[index].Quantity);
            return total;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }

    public sealed class ConstructionGhostPresenter : IDisposable
    {
        private readonly GameObject _root;
        private readonly SpriteRenderer _renderer;
        private readonly Dictionary<string, Sprite> _sprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public ConstructionGhostPresenter(Transform host)
        {
            _root = new GameObject("Construction Ghost");
            _root.transform.SetParent(host, false);
            _renderer = _root.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 105;
            _root.SetActive(false);
        }

        public bool IsVisible => _root.activeSelf;

        public void Show(
            Vector3 position,
            ConstructionPrototypeDefinition definition,
            ConstructionSpaceKey space,
            bool technicallyPlaceable)
        {
            if (definition == null || !space.IsValid)
            {
                Hide();
                return;
            }

            string asset = definition.VisualKey;
            if (space.Kind == ConstructionSpaceKind.Edge)
            {
                asset += space.EdgeAddress.Axis == ConstructionEdgeAxis.Vertical
                    ? "_vertical"
                    : "_horizontal";
            }

            _renderer.sprite = RequireSprite(asset);
            _renderer.color = technicallyPlaceable
                ? space.Kind == ConstructionSpaceKind.Roof
                    ? new Color(0.49f, 0.73f, 0.95f, 0.58f)
                    : new Color(0.22f, 0.95f, 0.78f, 0.58f)
                : new Color(1f, 0.28f, 0.25f, 0.58f);
            _root.transform.position = new Vector3(position.x, position.y, -0.055f);
            _root.SetActive(_renderer.sprite != null);
        }

        public void Hide() => _root.SetActive(false);

        public void Dispose()
        {
            foreach (Sprite sprite in _sprites.Values)
                PrototypeVisualAssets.DestroyRuntimeSprite(sprite);
            _sprites.Clear();
            if (_root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private Sprite RequireSprite(string asset)
        {
            if (_sprites.TryGetValue(asset, out Sprite sprite)) return sprite;
            Vector2 pivot = asset == "construction_floor"
                ? new Vector2(0.5f, 0.5f)
                : new Vector2(0.5f, 0.12f);
            sprite = PrototypeVisualAssets.CreateSprite(
                asset,
                pivot,
                PrototypeVisualAssets.PixelsPerUnit,
                asset + " ghost");
            _sprites.Add(asset, sprite);
            return sprite;
        }
    }
}
