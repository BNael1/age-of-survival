using System;

namespace AgeOfSurvival.Core.World.Generation
{
    public readonly struct ResourceKindWeights :
        IEquatable<ResourceKindWeights>
    {
        public ResourceKindWeights(
            uint shrub,
            uint looseStone,
            uint deadwood,
            uint tree)
        {
            ulong total = (ulong)shrub + looseStone + deadwood + tree;
            if (total != WorldPopulationProfile.ProbabilityScale)
            {
                throw new ArgumentException(
                    $"Natural resource weights must sum to {WorldPopulationProfile.ProbabilityScale}.");
            }

            Shrub = shrub;
            LooseStone = looseStone;
            Deadwood = deadwood;
            Tree = tree;
        }

        public uint Shrub { get; }
        public uint LooseStone { get; }
        public uint Deadwood { get; }
        public uint Tree { get; }

        public bool IsValid =>
            (ulong)Shrub + LooseStone + Deadwood + Tree
                == WorldPopulationProfile.ProbabilityScale;

        public GeneratedResourceKind Select(ushort sample)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Cannot select from an invalid natural-resource distribution.");
            }

            uint cursor = sample;
            if (cursor < Shrub) return GeneratedResourceKind.Shrub;
            cursor -= Shrub;
            if (cursor < LooseStone) return GeneratedResourceKind.LooseStone;
            cursor -= LooseStone;
            if (cursor < Deadwood) return GeneratedResourceKind.Deadwood;
            return GeneratedResourceKind.Tree;
        }

        public bool Equals(ResourceKindWeights other)
        {
            return Shrub == other.Shrub
                && LooseStone == other.LooseStone
                && Deadwood == other.Deadwood
                && Tree == other.Tree;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKindWeights other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Shrub;
                hash = (hash * 397) ^ (int)LooseStone;
                hash = (hash * 397) ^ (int)Deadwood;
                hash = (hash * 397) ^ (int)Tree;
                return hash;
            }
        }
    }

    public readonly struct ResourceKindDistribution :
        IEquatable<ResourceKindDistribution>
    {
        public ResourceKindDistribution(
            ResourceKindWeights open,
            ResourceKindWeights wooded)
        {
            if (!open.IsValid)
            {
                throw new ArgumentException(
                    "Open-land resource weights are invalid.",
                    nameof(open));
            }

            if (!wooded.IsValid)
            {
                throw new ArgumentException(
                    "Wooded resource weights are invalid.",
                    nameof(wooded));
            }

            Open = open;
            Wooded = wooded;
        }

        public ResourceKindWeights Open { get; }
        public ResourceKindWeights Wooded { get; }
        public bool IsValid => Open.IsValid && Wooded.IsValid;

        public GeneratedResourceKind Select(
            GeneratedLandscapeZone zone,
            ushort sample)
        {
            switch (zone)
            {
                case GeneratedLandscapeZone.Open:
                    return Open.Select(sample);
                case GeneratedLandscapeZone.Wooded:
                    return Wooded.Select(sample);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(zone),
                        zone,
                        "Natural resources can only be selected on land.");
            }
        }

        public bool Equals(ResourceKindDistribution other)
        {
            return Open.Equals(other.Open)
                && Wooded.Equals(other.Wooded);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKindDistribution other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Open.GetHashCode() * 397) ^ Wooded.GetHashCode();
            }
        }

        public static readonly ResourceKindDistribution ShrubsOnly =
            new ResourceKindDistribution(
                new ResourceKindWeights(65536u, 0u, 0u, 0u),
                new ResourceKindWeights(65536u, 0u, 0u, 0u));

        // Provisional revision-2 tuning.
        public static readonly ResourceKindDistribution TemperateNaturalV2 =
            new ResourceKindDistribution(
                new ResourceKindWeights(29491u, 19661u, 13107u, 3277u),
                new ResourceKindWeights(16384u, 6554u, 13107u, 29491u));
    }
}
