using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beacon pulse (Part 14.5.7 Motion, 14.3 B1). Lens and light rest at 0.775 until s 905. From the game time t0 at
    /// which s 905 was first reached the amplitude eases in 0 -> 0.225 over 1 s (smootherstep) and the intensity is
    /// I = 0.775 + amp sin(2 pi (t - t0) / 4), so the peaks are at t0 + 1 + 4 k: exactly the B1 ping times of the audio
    /// (RingAudioEngine.BeaconIntensity / PingTime use the same t0 and the same formula; the first ping plays at the
    /// first peak, 1 s after s 905). t0 follows the AudioDirector's rule: the first tick back-dates it at walking speed
    /// when the walk starts beyond s 905 (-progress), otherwise it is the time of the first tick with s >= 905, and it
    /// is never reset during the walk. Capture stills (time branch = progress branch, 14.7 H8): t - t0 = (s - 905) / 1.5.
    /// Writes the global _BeaconPulse (RingBeaconLens reads it; no MaterialPropertyBlock, SRP Batcher intact) and the
    /// point light intensity 3.0 x I. Also the beacon light's <see cref="IRingDrawable"/>: the BeatDirector switches it
    /// with RingSet.Outside (it toggles renderers only, not lights).
    /// </summary>
    public sealed class BeaconPulse : MonoBehaviour, IRingAnimated, IRingDrawable
    {
        public Light Lamp;

        /// <summary>RingState.T at which s 905 was first reached (NaN until then). Pings and peaks at T0 + 1 + 4 k.</summary>
        public static double T0 = double.NaN;
        /// <summary>The current pulse value 0.55..1.0.</summary>
        public static float Value = OutsideLayout.PulseBase;

        static readonly int IdPulse = Shader.PropertyToID("_BeaconPulse");
        bool _visible = true;
        bool _ticked;

        public RingSet Set => RingSet.Outside;

        public bool Visible
        {
            get => _visible;
            set { _visible = value; if (Lamp != null) Lamp.enabled = value; }
        }

        /// <summary>Called by the builder at load (enter-play-mode without domain reload keeps statics).</summary>
        public static void ResetStatics()
        {
            T0 = double.NaN;
            Value = OutsideLayout.PulseBase;
            Shader.SetGlobalFloat(IdPulse, Value);
        }

        /// <summary>Pulse value for e = seconds since s 905 was first reached (negative: not yet).</summary>
        public static float Evaluate(double e)
        {
            if (!(e >= 0.0)) return OutsideLayout.PulseBase;
            double period = RingConstants.BeaconPeriod;
            double ph = e - period * System.Math.Floor(e / period);                       // wrapped: no float drift in long runs
            double amp = OutsideLayout.PulseAmp * RingMath.Smootherstep01((float)System.Math.Min(e / OutsideLayout.PulseEaseIn, 2.0));
            return (float)(OutsideLayout.PulseBase + amp * System.Math.Sin(2.0 * System.Math.PI * ph / period));
        }

        /// <summary>Game time of the k-th pulse peak (= B1 ping), NaN before s 905.</summary>
        public static double PeakTime(int k) => T0 + OutsideLayout.PulseEaseIn + k * (double)RingConstants.BeaconPeriod;

        public void Tick(float s, double t, float dt)
        {
            double e;
            float first = RingConstants.BeaconFirstPingS;
            if (RingState.Capture && !RingState.SequenceMode)
            {
                e = (s - first) / RingConstants.Walk;
            }
            else
            {
                if (!_ticked)
                {
                    _ticked = true;
                    if (s > first && double.IsNaN(T0)) T0 = t - (s - first) / RingConstants.Walk;   // started beyond s 905
                }
                if (double.IsNaN(T0) && s >= first) T0 = t;
                e = double.IsNaN(T0) ? -1.0 : t - T0;
            }
            float v = Evaluate(e);
            Value = v;
            Shader.SetGlobalFloat(IdPulse, v);
            if (Lamp != null) Lamp.intensity = OutsideLayout.BeaconLightIntensity * v * RingConstants.LightCal;
        }
    }
}
