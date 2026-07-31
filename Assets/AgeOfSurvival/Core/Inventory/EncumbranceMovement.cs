using System;

namespace AgeOfSurvival.Core.Inventory
{
    /// <summary>
    /// Pure movement consequence derived from the effective carried load.
    /// The base capacity is the player's main carried-inventory capacity.
    /// </summary>
    public readonly struct EncumbranceMovementState
    {
        internal EncumbranceMovementState(
            EncumbranceValue effectiveLoad,
            EncumbranceValue baseCapacity,
            double loadRatio,
            double speedMultiplier)
        {
            EffectiveLoad = effectiveLoad;
            BaseCapacity = baseCapacity;
            LoadRatio = loadRatio;
            SpeedMultiplier = speedMultiplier;
        }

        public EncumbranceValue EffectiveLoad { get; }
        public EncumbranceValue BaseCapacity { get; }
        public double LoadRatio { get; }
        public double SpeedMultiplier { get; }
    }

    /// <summary>
    /// Validated progressive encumbrance curve inspired by Project Zomboid.
    /// Values between control points are linearly interpolated.
    /// </summary>
    public static class EncumbranceMovementOperations
    {
        public const double FullSpeedRatio = 1.0;
        public const double FairlyHeavyRatio = 1.25;
        public const double HeavyRatio = 1.5;
        public const double VeryHeavyRatio = 1.75;
        public const double MaximumPenaltyRatio = 2.0;

        public const double FullSpeedMultiplier = 1.0;
        public const double FairlyHeavyMultiplier = 0.81;
        public const double HeavyMultiplier = 0.63;
        public const double VeryHeavyMultiplier = 0.44;
        public const double MinimumSpeedMultiplier = 0.25;

        public static EncumbranceMovementState Calculate(PlayerInventoryState inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            CarriedLoad load = CarriedLoadOperations.Calculate(inventory);
            return Calculate(load.Perceived, inventory.MainContainer.Definition.Capacity);
        }

        public static EncumbranceMovementState Calculate(
            EncumbranceValue effectiveLoad,
            EncumbranceValue baseCapacity)
        {
            if (baseCapacity.Units <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseCapacity),
                    baseCapacity.Units,
                    "Base carrying capacity must be greater than zero.");
            }

            double loadRatio = effectiveLoad.Units / (double)baseCapacity.Units;
            double speedMultiplier = SpeedMultiplierForRatio(loadRatio);
            return new EncumbranceMovementState(
                effectiveLoad,
                baseCapacity,
                loadRatio,
                speedMultiplier);
        }

        private static double SpeedMultiplierForRatio(double loadRatio)
        {
            if (loadRatio <= FullSpeedRatio)
            {
                return FullSpeedMultiplier;
            }

            if (loadRatio < FairlyHeavyRatio)
            {
                return Interpolate(
                    loadRatio,
                    FullSpeedRatio,
                    FairlyHeavyRatio,
                    FullSpeedMultiplier,
                    FairlyHeavyMultiplier);
            }

            if (loadRatio < HeavyRatio)
            {
                return Interpolate(
                    loadRatio,
                    FairlyHeavyRatio,
                    HeavyRatio,
                    FairlyHeavyMultiplier,
                    HeavyMultiplier);
            }

            if (loadRatio < VeryHeavyRatio)
            {
                return Interpolate(
                    loadRatio,
                    HeavyRatio,
                    VeryHeavyRatio,
                    HeavyMultiplier,
                    VeryHeavyMultiplier);
            }

            if (loadRatio < MaximumPenaltyRatio)
            {
                return Interpolate(
                    loadRatio,
                    VeryHeavyRatio,
                    MaximumPenaltyRatio,
                    VeryHeavyMultiplier,
                    MinimumSpeedMultiplier);
            }

            return MinimumSpeedMultiplier;
        }

        private static double Interpolate(
            double value,
            double start,
            double end,
            double startMultiplier,
            double endMultiplier)
        {
            double progress = (value - start) / (end - start);
            return startMultiplier + ((endMultiplier - startMultiplier) * progress);
        }
    }
}
