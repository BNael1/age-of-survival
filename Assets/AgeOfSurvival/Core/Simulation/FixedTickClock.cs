using System;

namespace AgeOfSurvival.Core.Simulation
{
    /// <summary>
    /// Converts elapsed wall time into deterministic fixed simulation steps.
    /// The clock never discards backlog; callers choose the per-advance cap.
    /// </summary>
    public sealed class FixedTickClock
    {
        private const double TickEpsilon = 1e-12;
        private double _accumulatorSeconds;

        public FixedTickClock(int ticksPerSecond, int maxTicksPerAdvance)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Ticks per second must be greater than zero.");
            }

            if (maxTicksPerAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance), maxTicksPerAdvance, "The tick cap must be greater than zero.");
            }

            TicksPerSecond = ticksPerSecond;
            MaxTicksPerAdvance = maxTicksPerAdvance;
            TickDurationSeconds = 1.0 / ticksPerSecond;
        }

        public int TicksPerSecond { get; }
        public int MaxTicksPerAdvance { get; }
        public double TickDurationSeconds { get; }
        public long TotalTicks { get; private set; }
        public double PendingSeconds => _accumulatorSeconds;

        public double InterpolationAlpha
        {
            get
            {
                double alpha = _accumulatorSeconds / TickDurationSeconds;
                if (alpha < 0.0) return 0.0;
                if (alpha > 1.0) return 1.0;
                return alpha;
            }
        }

        public int Advance(double elapsedSeconds, Action tick)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "Elapsed time must be finite and non-negative.");
            }

            if (tick == null)
            {
                throw new ArgumentNullException(nameof(tick));
            }

            _accumulatorSeconds += elapsedSeconds;
            int availableTicks = (int)Math.Floor((_accumulatorSeconds + TickEpsilon) / TickDurationSeconds);
            int ticksToRun = Math.Min(availableTicks, MaxTicksPerAdvance);

            for (int index = 0; index < ticksToRun; index++)
            {
                tick();
                TotalTicks = checked(TotalTicks + 1);
            }

            _accumulatorSeconds -= ticksToRun * TickDurationSeconds;
            if (_accumulatorSeconds < 0.0 && _accumulatorSeconds > -TickEpsilon)
            {
                _accumulatorSeconds = 0.0;
            }

            return ticksToRun;
        }

        public void Reset()
        {
            _accumulatorSeconds = 0.0;
            TotalTicks = 0;
        }
    }
}
