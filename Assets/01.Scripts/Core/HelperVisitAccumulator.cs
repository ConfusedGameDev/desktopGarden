using System;

namespace CONFUSEDGAMEDEV.PollenGarden.Core
{
    /// <summary>
    /// Aggregate visit clock for one helper type: N owned helpers on a T-second interval produce
    /// N/T visits per second, accumulated continuously so fractional progress is never lost
    /// between frames. Pure C# — the same math will replay elapsed offline time in M3 (which is
    /// why it takes doubles: hours of offline seconds would shred float precision).
    /// </summary>
    public sealed class HelperVisitAccumulator
    {
        private readonly double intervalSeconds;
        private double progress;

        public HelperVisitAccumulator(double intervalSeconds)
        {
            this.intervalSeconds = Math.Max(intervalSeconds, 0.001);
        }

        /// <summary>Advances time and returns how many visits came due.</summary>
        public int Advance(double deltaSeconds, int helperCount)
        {
            if (deltaSeconds <= 0 || helperCount <= 0)
            {
                return 0;
            }

            progress += deltaSeconds * helperCount;
            int visits = (int)(progress / intervalSeconds);
            progress -= visits * intervalSeconds;
            return visits;
        }
    }
}
