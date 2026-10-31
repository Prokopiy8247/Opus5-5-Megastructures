using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Ark motion (MASTER_PROMPT 14.5.3): x_c(u) = -9 000 + 15 000 u along the track z = 3 180 at altitude 1 800,
    /// heading +X, u = RingState.ArkU (the ProgressDirector's smax phase after its follower, 14.7 H8; the same x_c the
    /// AtmosphereDriver writes to _ArkParams.x); roll 0.4 deg (period 40 s, the definition published in _ArkParams.w)
    /// and pitch 0.2 deg (period 55 s) from the time branch RingState.T, or from progress in capture stills
    /// (t = (s - 231) / 1.5: the time branch equal to the progress branch) so stills are reproducible; frame sequences
    /// keep the time branch. The root carries only the translation (the registry markers hang there and keep exact,
    /// unrotated dimensions); the body below it carries the roll and pitch. No allocation per frame.
    /// </summary>
    public sealed class ArkMotion : IRingAnimated
    {
        readonly Transform _root, _body;

        public ArkMotion(Transform root, Transform body)
        {
            _root = root;
            _body = body;
        }

        /// <summary>Ship centre x_c(u) = -9 000 + 15 000 u, u clamped to 0..1.</summary>
        public static float CentreX(float u)
        {
            float uc = u < 0f ? 0f : (u > 1f ? 1f : u);
            return RingConstants.ArkX0 + (RingConstants.ArkX1 - RingConstants.ArkX0) * uc;
        }

        public void Tick(float s, double t, float dt)
        {
            if (_root == null || _body == null) return;
            _root.localPosition = new Vector3(CentreX(RingState.ArkU), RingConstants.ArkAltitude, RingConstants.ArkTrackZ);
            bool still = RingState.Capture && !RingState.SequenceMode;
            double ta = still ? (s - RingConstants.ArkTriggerS) / RingConstants.Walk : t;
            float roll = ArkSpec.RollDeg * Mathf.Sin(2f * Mathf.PI * Phase(ta, ArkSpec.RollPeriod));
            float pitch = ArkSpec.PitchDeg * Mathf.Sin(2f * Mathf.PI * Phase(ta, ArkSpec.PitchPeriod) + 1.3f);
            _body.localRotation = Quaternion.Euler(roll, 0f, pitch);   // roll about the heading (+X), pitch about +Z
        }

        /// <summary>Fractional phase t / period in [0, 1), computed in double (long-run safe, Part 10 #31).</summary>
        static float Phase(double t, double period)
        {
            double p = t / period;
            return (float)(p - System.Math.Floor(p));
        }
    }
}
