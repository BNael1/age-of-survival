using System;

namespace AgeOfSurvival.Core.World.Generation
{
    /// <summary>
    /// Integer-only smoothed fields derived from absolute world coordinates.
    /// No mutable random stream, floating point, chunk coordinate or load order is involved.
    /// </summary>
    public static class DeterministicWorldFields
    {
        private const uint FieldMaximum = 65535u;

        public static ushort Sample16(
            WorldGenerationSettings settings,
            WorldCellCoordinate worldCell,
            GenerationStream stream)
        {
            return unchecked((ushort)(DeterministicWorldSampler.Sample64(settings, worldCell, stream) >> 48));
        }

        public static ushort SampleSmoothed16(
            WorldGenerationSettings settings,
            WorldCellCoordinate worldCell,
            int scale,
            GenerationStream stream)
        {
            if (scale < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "A smoothed field scale must be at least two cells.");
            }

            DivideFloor(worldCell.X, scale, out long latticeX, out int localX);
            DivideFloor(worldCell.Y, scale, out long latticeY, out int localY);

            ushort bottomLeft = Sample16(
                settings,
                new WorldCellCoordinate(latticeX, latticeY),
                stream);
            ushort bottomRight = Sample16(
                settings,
                new WorldCellCoordinate(checked(latticeX + 1L), latticeY),
                stream);
            ushort topLeft = Sample16(
                settings,
                new WorldCellCoordinate(latticeX, checked(latticeY + 1L)),
                stream);
            ushort topRight = Sample16(
                settings,
                new WorldCellCoordinate(checked(latticeX + 1L), checked(latticeY + 1L)),
                stream);

            uint xWeight = SmoothStep(ToFieldWeight(localX, scale));
            uint yWeight = SmoothStep(ToFieldWeight(localY, scale));
            ushort bottom = Lerp(bottomLeft, bottomRight, xWeight);
            ushort top = Lerp(topLeft, topRight, xWeight);
            return Lerp(bottom, top, yWeight);
        }

        private static uint ToFieldWeight(int localCoordinate, int scale)
        {
            return checked((uint)(((ulong)localCoordinate * FieldMaximum) / (uint)scale));
        }

        private static uint SmoothStep(uint value)
        {
            ulong t = value;
            ulong tSquared = ((t * t) + (FieldMaximum / 2u)) / FieldMaximum;
            ulong factor = (3UL * FieldMaximum) - (2UL * t);
            return checked((uint)(((tSquared * factor) + (FieldMaximum / 2u)) / FieldMaximum));
        }

        private static ushort Lerp(ushort left, ushort right, uint weight)
        {
            ulong inverse = FieldMaximum - weight;
            ulong blended = ((ulong)left * inverse)
                + ((ulong)right * weight)
                + (FieldMaximum / 2u);
            return checked((ushort)(blended / FieldMaximum));
        }

        private static void DivideFloor(
            long value,
            int positiveDivisor,
            out long quotient,
            out int remainder)
        {
            quotient = value / positiveDivisor;
            long rawRemainder = value % positiveDivisor;
            if (rawRemainder < 0)
            {
                quotient--;
                rawRemainder += positiveDivisor;
            }

            remainder = checked((int)rawRemainder);
        }
    }
}
