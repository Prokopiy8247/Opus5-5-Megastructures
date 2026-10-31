namespace Megastructure
{
    /// <summary>
    /// The global scene clock (seconds, double). Drives every looping machine (hoist, pistons, fan, freight lights) so that
    /// capture mode can freeze or step it and long play never loses precision. Advanced once per frame by
    /// <see cref="ClockDriver"/> (execution order -1000) unless <see cref="Frozen"/>.
    /// t = 0 is the start of the walk (first frame after the load fade begins, the title timeline starts here).
    /// </summary>
    public static class SceneClock
    {
        /// <summary>Global scene time in seconds.</summary>
        public static double Time;

        /// <summary>When true the clock does not advance (capture mode, -freeze-clock).</summary>
        public static bool Frozen;

        /// <summary>Time since the walk started that never freezes (UI fades, title timeline).</summary>
        public static double RealTime;

        /// <summary>Wrapped clock for a loop of the given period, computed in double (no drift after hours).</summary>
        public static double Wrapped(double period) => Time - period * System.Math.Floor(Time / period);

        public static void Reset(double t)
        {
            Time = t;
        }
    }
}
