namespace Megastructure
{
    /// <summary>
    /// Optional QA hooks read by core and beat code. Inert defaults; only the QA harness writes them, and only in capture mode.
    /// Each consumer needs one line added by its owner (listed in docs/QA_RUNTIME.md, "Hooks").
    /// </summary>
    public static class QaHooks
    {
        /// <summary>
        /// Detail-tier coverage override for the tier crossover tests (14.10 b, c): -1 = off (normal), 1 = near tier only,
        /// 0 = far / shader tier only. Consumers: <c>DetailTier.Update</c> (c = TierOverride when &gt;= 0) and the beat-7
        /// honeycomb tier-1 / tier-2 cross-fade.
        /// </summary>
        public static float TierOverride = -1f;

        /// <summary>True while the harness renders a capture (beats may skip debug-only work).</summary>
        public static bool Capturing;
    }
}
