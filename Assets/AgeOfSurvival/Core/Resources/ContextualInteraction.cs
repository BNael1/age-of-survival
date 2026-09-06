using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;

namespace AgeOfSurvival.Core.Resources
{
    // PROTOTYPE / NON GAMEPLAY FINAL: exact-distance ties only; never override proximity.
    public enum ContextualInteractionKind { Door = 0, GroundContainer = 1, NaturalResource = 2 }

    public readonly struct ContextualInteractionCandidate
    {
        public ContextualInteractionCandidate(ContextualInteractionKind kind, string stableId, WorldPosition position)
        {
            if (kind < ContextualInteractionKind.Door || kind > ContextualInteractionKind.NaturalResource)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("A stable ID is required.", nameof(stableId));
            Kind = kind;
            StableId = stableId;
            Position = position;
        }
        public ContextualInteractionKind Kind { get; }
        public string StableId { get; }
        public WorldPosition Position { get; }
    }

    public static class ContextualInteractionResolver
    {
        public static bool TryResolve(IEnumerable<ContextualInteractionCandidate> candidates,
            WorldPosition origin, double radius, out ContextualInteractionCandidate target)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            bool found = false;
            double bestDistance = radius * radius;
            target = default;
            foreach (ContextualInteractionCandidate candidate in candidates)
            {
                if (candidate.StableId == null) throw new ArgumentException("Invalid candidate.", nameof(candidates));
                double distance = origin.DistanceSquaredTo(candidate.Position);
                if (double.IsInfinity(distance) || distance > bestDistance) continue;
                if (found && distance == bestDistance &&
                    (candidate.Kind > target.Kind || (candidate.Kind == target.Kind &&
                     string.CompareOrdinal(candidate.StableId, target.StableId) >= 0))) continue;
                target = candidate;
                bestDistance = distance;
                found = true;
            }
            return found;
        }
    }
}
