using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Rendering
{
    /// <summary>
    /// Owns the single back-to-front sorting pass after visual Update synchronization.
    /// Registered renderers are presentation-only views and never own Core state.
    /// </summary>
    public sealed class GroundAnchorSortCoordinator : MonoBehaviour
    {
        public const string PlayerStableId = "player:local";

        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<Entry> _activeEntries = new List<Entry>();

        public int AppliedPassCount { get; private set; }
        public int LastAppliedFrame { get; private set; } = -1;
        public int ApplicationsInLastAppliedFrame { get; private set; }
        public int ActiveEntryCount { get; private set; }

        private void LateUpdate()
        {
            ApplySortingForFrame(Time.frameCount);
        }

        public void Register(
            string stableId,
            Transform groundAnchor,
            GameObject visibilityRoot,
            params RendererOrderBinding[] renderers)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A stable visual identifier is required.", nameof(stableId));
            }

            if (groundAnchor == null)
            {
                throw new ArgumentNullException(nameof(groundAnchor));
            }

            if (visibilityRoot == null)
            {
                throw new ArgumentNullException(nameof(visibilityRoot));
            }

            if (renderers == null || renderers.Length == 0)
            {
                throw new ArgumentException("At least one renderer binding is required.", nameof(renderers));
            }

            var copy = new RendererOrderBinding[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].Renderer == null)
                {
                    throw new ArgumentException("Renderer bindings must not contain null.", nameof(renderers));
                }

                copy[index] = renderers[index];
            }

            _entries[stableId] = new Entry(
                stableId,
                groundAnchor,
                visibilityRoot,
                copy);
        }

        public void Unregister(string stableId)
        {
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                _entries.Remove(stableId);
            }
        }

        public bool ApplySortingForFrame(int frameIndex)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }

            if (LastAppliedFrame == frameIndex)
            {
                return false;
            }

            LastAppliedFrame = frameIndex;
            ApplicationsInLastAppliedFrame = 0;
            _activeEntries.Clear();
            foreach (Entry entry in _entries.Values)
            {
                if (entry.GroundAnchor != null
                    && entry.VisibilityRoot != null
                    && entry.VisibilityRoot.activeInHierarchy)
                {
                    _activeEntries.Add(entry);
                }
            }

            _activeEntries.Sort(CompareEntries);
            for (int rank = 0; rank < _activeEntries.Count; rank++)
            {
                RendererOrderBinding[] bindings = _activeEntries[rank].Renderers;
                for (int index = 0; index < bindings.Length; index++)
                {
                    bindings[index].Renderer.sortingOrder =
                        GroundAnchorSorting.OrderForRank(rank, bindings[index].LocalOffset);
                }
            }

            ActiveEntryCount = _activeEntries.Count;
            ApplicationsInLastAppliedFrame = 1;
            AppliedPassCount++;
            return true;
        }

        private static int CompareEntries(Entry left, Entry right)
        {
            return GroundAnchorSorting.CompareBackToFront(
                left.GroundAnchor.position.y,
                left.StableId,
                right.GroundAnchor.position.y,
                right.StableId);
        }

        private sealed class Entry
        {
            public Entry(
                string stableId,
                Transform groundAnchor,
                GameObject visibilityRoot,
                RendererOrderBinding[] renderers)
            {
                StableId = stableId;
                GroundAnchor = groundAnchor;
                VisibilityRoot = visibilityRoot;
                Renderers = renderers;
            }

            public string StableId { get; }
            public Transform GroundAnchor { get; }
            public GameObject VisibilityRoot { get; }
            public RendererOrderBinding[] Renderers { get; }
        }
    }

    public readonly struct RendererOrderBinding
    {
        public RendererOrderBinding(Renderer renderer, int localOffset)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (localOffset < 0 || localOffset >= GroundAnchorSorting.OrdersPerVisual)
            {
                throw new ArgumentOutOfRangeException(nameof(localOffset));
            }

            Renderer = renderer;
            LocalOffset = localOffset;
        }

        public Renderer Renderer { get; }
        public int LocalOffset { get; }
    }
}
