using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Runtime.Construction;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Rendering
{
    public sealed partial class DebugIsometricWorld
    {
        public bool TryResolveConstructionTarget(
            Vector3 worldPosition,
            ConstructionSpaceKind kind,
            out ConstructionPlacementTarget target)
        {
            target = default;
            return TryCreateConstructionProjectionFrame(out ConstructionProjectionFrame frame)
                && ConstructionPlacementProjection.TryResolve(
                    frame,
                    worldPosition,
                    kind,
                    out target);
        }

        public bool TryMapConstructionSpaceCenter(
            ConstructionSpaceKey space,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            return TryCreateConstructionProjectionFrame(out ConstructionProjectionFrame frame)
                && ConstructionPlacementProjection.TryMapSpaceCenter(
                    frame,
                    space,
                    out worldPosition);
        }

        private bool TryCreateConstructionProjectionFrame(
            out ConstructionProjectionFrame frame)
        {
            if (_streamingInitialized)
            {
                return ConstructionProjectionFrame.TryCreate(
                    _renderedWorldOrigin,
                    _streamingVisualOriginWorld,
                    _streamingXBasis,
                    _streamingYBasis,
                    out frame);
            }

            if (_tilemap == null)
            {
                frame = default;
                return false;
            }

            Vector3 visualOrigin = _tilemap.GetCellCenterWorld(Vector3Int.zero);
            Vector3 xBasis = _tilemap.GetCellCenterWorld(Vector3Int.right) - visualOrigin;
            Vector3 yBasis = _tilemap.GetCellCenterWorld(Vector3Int.up) - visualOrigin;
            return ConstructionProjectionFrame.TryCreate(
                _renderedWorldOrigin,
                visualOrigin,
                xBasis,
                yBasis,
                out frame);
        }
    }
}
