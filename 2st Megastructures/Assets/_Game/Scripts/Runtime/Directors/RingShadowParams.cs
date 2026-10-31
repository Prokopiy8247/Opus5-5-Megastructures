using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Analytic shadow parameters computed every frame by the ProgressDirector and written to the shaders by the
    /// AtmosphereDriver (_ShipShadow, _CloudShadowA/B). Flat (x, z) in metres.
    /// </summary>
    public static class RingShadowParams
    {
        /// <summary>Ship shadow centre (x_c + 3 422, 3 180 - 1 383) (14.5.3).</summary>
        public static Vector2 ShipCentre;
        /// <summary>smoothstep(0, 0.06, ArkU) (0 until s 231 is first reached; kept when the player backs up below it
        /// afterwards, since the phase keeps its time branch). The AtmosphereDriver multiplies it by the Ark set state
        /// of the same frame (0 while the Ark set is off) when it writes _ShipShadow.z.</summary>
        public static float ShipStrength;
        /// <summary>Felt-cue key dip 0.2 x envelope (14.5.3). RingSchedule already applies it to the key intensity;
        /// it is published in _ShipShadow.w for analytic terms only.</summary>
        public static float KeyDip;
        /// <summary>Cloud shadow centres (14.5.2), drifting +X at 12 m/s.</summary>
        public static Vector2 CloudA, CloudB;
        /// <summary>0.25, ramped in over s 90..105, 0 after beat 3 (faded over the Rule P window 399..423).</summary>
        public static float CloudStrength;

        public static void Reset()
        {
            ShipCentre = ArkPhase.ShadowCentre(0f);
            ShipStrength = 0f; KeyDip = 0f;
            CloudA = RingConstants.CloudShadowA0; CloudB = RingConstants.CloudShadowB0; CloudStrength = 0f;
        }
    }

    /// <summary>
    /// Phase maths of the ark (14.5.3, 14.7 H8) used by the directors. The ark builder owns the geometry and its
    /// IRingAnimated motion (Megastructure.Beats.ArkMotion); the roll published in _ArkParams.w follows the same
    /// definition (0.4 degrees, 40 s period, time branch = progress branch in capture stills).
    /// </summary>
    public static class ArkPhase
    {
        const float RollDeg = 0.4f, RollPeriod = 40f;     // 14.5.3: roll 0.4 degrees (40 s)

        /// <summary>Ship centre x_c(u) = -9 000 + 15 000 u (u clamped to 0..1).</summary>
        public static float XCentre(float u)
        {
            float uc = u < 0f ? 0f : (u > 1f ? 1f : u);
            return RingConstants.ArkX0 + (RingConstants.ArkX1 - RingConstants.ArkX0) * uc;
        }

        /// <summary>Progress branch of the ark phase, (s - 231) / 285.</summary>
        public static float ProgressBranch(float s) => (s - RingConstants.ArkTriggerS) / RingConstants.ArkD;

        /// <summary>Time branch of the ark phase, (t - t231) / 190.</summary>
        public static float TimeBranch(double t, double t231) => (float)((t - t231) / RingConstants.ArkT);

        /// <summary>Roll in radians. t = game time; in capture stills the time branch equals the progress branch
        /// (t = (s - 231) / 1.5, 14.7 H8).</summary>
        public static float Roll(double t, float s, bool capture)
        {
            double ta = capture ? (s - RingConstants.ArkTriggerS) / RingConstants.Walk : t;
            double p = ta / RollPeriod;
            float ph = (float)(p - System.Math.Floor(p));
            return RollDeg * Mathf.Deg2Rad * Mathf.Sin(2f * Mathf.PI * ph);
        }

        /// <summary>Ship shadow centre for phase u (14.12 SHIP_SHADOW offset (+3 422, -1 383)).</summary>
        public static Vector2 ShadowCentre(float u) => new Vector2(XCentre(u) + RingConstants.ShipShadowOffset.x, RingConstants.ArkTrackZ + RingConstants.ShipShadowOffset.y);
    }
}
