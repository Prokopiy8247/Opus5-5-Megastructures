// ThreadAudioParams: every audio target of MASTER_PROMPT 14.3 as a PURE function of the plan distance s
// (plus the intro clock, the brisk flag, the ending clock and the pause amount). The main thread computes it
// once per frame (AudioDirector) and hands it to ThreadSynth; the offline renderer computes it per block, so
// an EditMode test renders s = 0..860 without an audio device (14.3.1, 11.9).
// Levels are linear amplitudes of unit-RMS layer generators (ThreadDsp calibrates every generator to unit
// stereo RMS at the master input), so a layer gain of Db(-40) yields -40 dBFS RMS at the master input.
// Per-beat values are blended with smootherstep over 24 m windows centred on each boundary (14.6), exactly
// like ThreadMath.BeatBlend, but each beat may also ramp inside itself (e.g. wind -36 -> -40 in beat 5).
// Pure C#, no UnityEngine dependency. Zero allocations (the struct is returned by value).
using System;

namespace Megastructure.Audio
{
    /// <summary>Layer indices of 14.3.2 (bit positions in <see cref="ThreadAudioParams.layerMask"/>).</summary>
    public static class AudioLayer
    {
        public const int Wind = 0, Drone = 1, Pad = 2, Breath = 3, Hush = 4, Hum = 5, Stars = 6, Beacon = 7;
        public const int Count = 8;
        public const int AllMask = (1 << Count) - 1;
        public static readonly string[] Names = { "wind", "drone", "pad", "breath", "hush", "hum", "stars", "beacon" };
        public static int Bit(int layer) { return 1 << layer; }
    }

    /// <summary>
    /// The numbers of MASTER_PROMPT 14.3 that are not (yet) in ThreadConstants. Each line cites the spec line.
    /// CONTRACT REQUEST: move these into ThreadConstants (audio section). Values marked "chosen" are not in the
    /// spec and were picked here (documented reasons).
    /// </summary>
    public static class AudioSpec
    {
        // ------------------------------------------------------------ 14.3.1 engine (line 513)
        public const double SmoothTau = 0.020;            // one-pole per-sample parameter smoothing (20 ms)
        public const double DcBlockHz = 10.0;             // master DC block, 1-pole high-pass
        public const int SineTableSize = 4096;
        /// <summary>
        /// Soft-limiter ceiling. 14.3.1 writes y = 0.89 tanh(x / 0.89) "ceiling about -1.5 dBFS", but 0.89 has a
        /// -1.01 dBFS asymptote (R5 decision 3); 0.84 gives -1.51 dBFS and meets 14.3.3 "sample peak below -1.5".
        /// CONTRACT REQUEST: ThreadConstants.LimiterCeiling = 0.84.
        /// </summary>
        public const double LimiterCeiling = 0.84;
        public const double ReverbBlendSeconds = 8.0;     // 14.3.4 (line 547): 8 s cross-blend of wet/feedback/damping
        public const double ReverbSize = 2.2;             // R5 section 4: FDN size factor (modal density up to ~6 s)
        public const double ReverbMaxPreDelay = 0.25;     // s (largest pre-delay of 14.3.4 is 200 ms)
        public const int FootstepVariants = 6;            // line 513: 6 variants per surface
        public const int FootstepPool = 4;                // line 513: pool of 4 AudioSources
        public const double FootPitchJitter = 0.03;       // line 513: pitch +-3 percent
        public const double FootPan = 0.15;               // line 559: alternate pan +-0.15

        // ------------------------------------------------------------ 14.3.2 layers (lines 520-527)
        public const double WindFc1 = 260.0, WindQ1 = 0.9, WindLfoDepth = 0.45, WindLfo1Hz = 0.07;
        public const double WindFc2 = 1100.0, WindQ2 = 2.0, WindBand2Db = -14.0, WindLfo2Hz = 0.11;
        public const double WindGustBase = 0.55, WindGustDepth = 0.45, WindGustTau = 4.0;
        public const double WindDensityExponent = 0.7;    // extra attenuation 20 log10(rho^0.7) above the deck
        public const double DroneLfoHz = 0.03, DroneLfoDepth = 0.30;
        public const double PadDetuneCents = 4.0, PadLowpassHz = 1100.0, PadLowpassQ = 0.7;
        public const double PadAttack = 8.0, PadRelease = 10.0;
        public const double BreathLowpassHz = 120.0, BreathBandLoHz = 55.0, BreathBandHiHz = 110.0;
        public const double BreathLfoHz = 0.11, BreathLfoDepth = 0.40, BreathDistanceScale = 40.0;
        public const double HushLowpassHz = 600.0;
        public static readonly int[] HumPartials = { 2, 3, 4, 5, 7 };
        public const double HumTremoloHz = 0.05;
        public const double StarMeanInterval = 3.5, StarAlphaGate = 0.2, StarAttack = 0.040, StarDecay = 1.4;
        public const double BeaconHz = 440.0, BeaconPulseDepth = 0.04, BeaconPulseHz = 0.25;
        public const double BeaconRange = 40.0, BeaconNear = 6.0, BeaconDbFar = -46.0, BeaconDbNear = -38.0;

        // chosen (not in the spec):
        public const double HumTremoloDepth = 0.25;       // depth of the independent 0.05 Hz tremolos
        public const double HushHighpassHz = 25.0;        // keeps sub-sonic pink energy out of the hush
        public const double BreathPanMax = 0.85;          // breath sources pan by bearing, at most +-0.85
        public const double BeaconFadeIn = 3.0;           // m: the tone fades in over 40 -> 37 m (no click at 40 m)
        public const double DroneEnterRamp = 1.5;         // s: the drone "enters at 2.0 s" with a 1.5 s ramp
        public const double PauseDuckDb = -20.0;          // Part 9: "the game audio ducks smoothly" on pause
        public const double BriskStepDb = 1.0;            // brisk footsteps 1 dB louder
        public const double StarMaxPan = 0.8;             // "random pan" range
        public const double DeckHushDb = -34.0;           // 14.3.6 item 4 "the hush returns" (level chosen)
        public const double DeckLowpassHz = 3500.0;       // the cloud deck muffles the world (master low-pass)

        // ------------------------------------------------------------ 14.3.3 levels (lines 531-541)
        public const double Off = double.NegativeInfinity;
        /// <summary>Layer level (dBFS RMS) at the start of each beat [layer, beat]; Off = layer silent.</summary>
        public static readonly double[,] LevelStart =
        {
            //  B1    B2    B3    B4    B5    B6    B7
            { -40,  -42,  -46,  -38,  -36,  -42,  -60 },   // L1 wind (B5 -36 -> -40, B6 -42 -> -60, B7 -> off at w = 0.80)
            { -42,  -36,  -38,  -30,  -34,  -36,  -44 },   // L2 drone
            { Off,  -40,  -36,  -30,  -34,  -34,  -42 },   // L3 pad (1, 2, 3, 3, 4, 4 voices)
            { Off,  -36,  Off,  Off,  Off,  Off,  Off },   // L4 colossus breath
            { Off,  Off,  -30,  -48,  Off,  Off,  Off },   // L5 mist hush
            { Off,  Off,  Off,  -32,  -36,  -36,  -48 },   // L6 thread hum
            { Off,  Off,  Off,  Off,  Off,  -48,  -52 },   // L7 star notes
            { Off,  Off,  Off,  Off,  Off,  Off,  Off },   // L8 beacon: distance law (BeaconDbFar -> BeaconDbNear), not the table
        };
        /// <summary>Layer level at the end of each beat (linear dB ramp across the beat).</summary>
        public static readonly double[,] LevelEnd =
        {
            { -40,  -42,  -46,  -38,  -40,  -60,  -60 },
            { -42,  -36,  -38,  -30,  -34,  -36,  -44 },
            { Off,  -40,  -36,  -30,  -34,  -34,  -42 },
            { Off,  -36,  Off,  Off,  Off,  Off,  Off },
            { Off,  Off,  -30,  -48,  Off,  Off,  Off },
            { Off,  Off,  Off,  -32,  -36,  -36,  -48 },
            { Off,  Off,  Off,  Off,  Off,  -48,  -52 },
            { Off,  Off,  Off,  Off,  Off,  Off,  Off },
        };
        /// <summary>
        /// Pad voices "enter one at a time per beat" (line 522): voice 1 at s = 150 (line 665), voice 2 with beat 3,
        /// voice 3 with the hum at the exhale s = 330 (line 698), voice 4 when "the pad opens" after the hole (line 574).
        /// </summary>
        public static readonly double[] PadVoiceEnterS = { 150.0, 240.0, 330.0, 600.5 };
        public const double HumEnterS = 330.0;            // line 698: the thread hum enters at s = 330

        // ------------------------------------------------------------ 14.3.4 reverb (lines 549-557)
        public static readonly double[] ReverbRt60 = { 0.9, 5.5, 7.0, 3.2, 1.6, 0.7, 0.15 };
        public static readonly double[] ReverbPreDelayMs = { 20, 90, 120, 200, 120, 60, 10 };
        public static readonly double[] ReverbWetDb = { -24, -14, -16, -18, -22, -30, -40 };
        public static readonly double[] ReverbDampHz = { 7000, 5000, 2200, 6000, 7000, 8000, 9000 };
        public const double PierWetDb = -60.0;            // line 576: "dry footsteps" on the pier (chosen value)

        // ------------------------------------------------------------ 14.3.5 footsteps (lines 559-567), index = FootSurface
        public static readonly double[] FootPeakDb = { -30, -28, -30, -29, -32 };
        public const double MetalBrightDbPerD = 10.0;     // +1 dB per 0.1 of D
        public const double MetalBrightQuantum = 0.05;    // D quantisation of the pre-rendered metal set (0.5 dB steps)

        // ------------------------------------------------------------ 14.3.6 silences and swells (lines 571-576)
        public const double IntroWindFromDb = -60.0, IntroWindPictureDb = -46.0, IntroWindDone = 4.0;
        public const double DroneEnterTime = 2.0;
        public const double InhaleDuckDb = -20.0, InhaleRamp = 2.0;         // ramp (m) on both sides of 268 / 302
        public const double SwellDb = 12.0;
        public const double DeckWindDb = -34.0, DeckRamp = 4.0;              // ramp (m) into / out of the deck
        public const double HoleDb = -12.0, HoleSeconds = 3.0;               // at the emergence s = 596
        public const double LowpassOpenHz = 14000.0, LowpassClosedHz = 2200.0;
        public const double BankSweepS0 = 228.0, BankSweepS1 = 268.0;        // 14.6: sweep starts with the 2 -> 3 blend
        public const double PierS0 = 838.0, PierS1 = 846.0;                  // "last 20 m: pad only" (eased over 8 m)
        public const double WindOffRamp = 8.0;                               // m: wind reaches exactly 0 at s = 752

        public static double Db(double db) { return double.IsNegativeInfinity(db) ? 0.0 : Math.Pow(10.0, db / 20.0); }
    }

    /// <summary>All audio targets for one moment (main thread -> audio thread). Plain floats, copied by value.</summary>
    public struct ThreadAudioParams
    {
        public float s;                 // progress these targets belong to (diagnostics)
        public float master;            // master gain: ending fade x pause duck
        public float stepBus;           // gain on the incoming listener mix (the footstep AudioSources)
        public float wind, drone, pad, hush, hum, stars, beacon;    // layer gains (linear, unit-RMS generators)
        public float breathLL, breathLR, breathRL, breathRR;        // breath: left source to L/R, right source to L/R
        public float pad0, pad1, pad2, pad3;                        // pad voice gates 0/1 (attack 8 s / release 10 s in the DSP)
        public float humUpper;          // weight of the 5th and 7th hum partials (= D)
        public float starGate;          // 1 while star alpha > 0.2
        public float lowpassHz;         // master low-pass cutoff
        public float rt60, preDelayMs, wetDb, dampHz;               // reverb targets (cross-blended over 8 s in the DSP)
        public float footGain;          // per-footstep gain the AudioDirector applies (brisk +1 dB)
        public int layerMask;           // bit per AudioLayer (stems); AudioLayer.AllMask in play

        public float PadGate(int i)
        {
            switch (i) { case 0: return pad0; case 1: return pad1; case 2: return pad2; default: return pad3; }
        }

        /// <summary>Convenience form of 14.3.1: a pure function of s and the brisk flag (lateral offset 0).</summary>
        public static ThreadAudioParams Compute(double s, double timeSinceStart, bool brisk, double endingTime, bool paused,
                                                double lateral = 0.0, int layerMask = AudioLayer.AllMask)
        {
            return ComputeFull(s, lateral, timeSinceStart, brisk, endingTime, paused ? 1.0 : 0.0, layerMask);
        }

        /// <param name="s">plan distance (m)</param>
        /// <param name="lateral">lateral offset to the right of the centre line (m); only the breath and beacon distances use it</param>
        /// <param name="timeSinceStart">seconds since the intro started (0 = black; use a large value to skip the intro)</param>
        /// <param name="brisk">brisk walk (footsteps +1 dB)</param>
        /// <param name="endingTime">seconds since the goal trigger, negative before it</param>
        /// <param name="pauseAmount">0..1 pause duck (the caller ramps it)</param>
        public static ThreadAudioParams ComputeFull(double s, double lateral, double timeSinceStart, bool brisk,
                                                    double endingTime, double pauseAmount, int layerMask)
        {
            var p = new ThreadAudioParams();
            p.s = (float)s;
            p.layerMask = layerMask;
            double t = Math.Max(0.0, timeSinceStart);

            // ---------------------------------------------------------- intro (14.3.6 item 1, 14.11)
            double picture = ThreadMath.Smoothstep(ThreadConstants.FadeInStart, ThreadConstants.FadeInEnd, t);
            double windRef = AudioSpec.LevelStart[AudioLayer.Wind, 0];
            double windIntroDb;
            if (t <= ThreadConstants.FadeInEnd)
                windIntroDb = ThreadMath.Lerp(AudioSpec.IntroWindFromDb - windRef, AudioSpec.IntroWindPictureDb - windRef, picture);
            else
                windIntroDb = ThreadMath.Lerp(AudioSpec.IntroWindPictureDb - windRef, 0.0,
                                              ThreadMath.Smoothstep(ThreadConstants.FadeInEnd, AudioSpec.IntroWindDone, t));
            double windIntro = AudioSpec.Db(windIntroDb) * ThreadMath.Smoothstep(0.0, ThreadConstants.FadeInStart, t);
            double droneIntro = ThreadMath.Smoothstep(AudioSpec.DroneEnterTime, AudioSpec.DroneEnterTime + AudioSpec.DroneEnterRamp, t);

            // ---------------------------------------------------------- shared envelopes (14.3.6)
            double inhaleEnv = ThreadMath.Smoothstep(ThreadConstants.InhaleS0 - AudioSpec.InhaleRamp, ThreadConstants.InhaleS0 + AudioSpec.InhaleRamp, s)
                             * (1.0 - ThreadMath.Smoothstep(ThreadConstants.InhaleS1 - AudioSpec.InhaleRamp, ThreadConstants.InhaleS1 + AudioSpec.InhaleRamp, s));
            double inhale = AudioSpec.Db(AudioSpec.InhaleDuckDb * inhaleEnv);
            double swellEnv = ThreadMath.Smoothstep(ThreadConstants.ExhaleS0, ThreadConstants.ExhaleS1, s)
                            * (1.0 - ThreadMath.Smoothstep(ThreadConstants.ExhaleS1, ThreadConstants.SwellWindowEnd, s));
            double swell = AudioSpec.Db(AudioSpec.SwellDb * swellEnv);
            double deckEnv = ThreadMath.Smoothstep(ThreadConstants.DeckPassS0 - AudioSpec.DeckRamp, ThreadConstants.DeckPassS0 + 2.0, s)
                           * (1.0 - ThreadMath.Smoothstep(ThreadConstants.DeckPassS1 - 2.0, ThreadConstants.DeckPassS1 + 1.0, s));
            // the 3 s hole at the emergence, as an s-window (3 s at walking speed = 4.5 m)
            double holeLen = AudioSpec.HoleSeconds * ThreadConstants.WalkSpeed;
            double holeEnv = ThreadMath.Smoothstep(ThreadConstants.DeckPassS1 - 1.5, ThreadConstants.DeckPassS1, s)
                           * (1.0 - ThreadMath.Smoothstep(ThreadConstants.DeckPassS1 + holeLen - 1.0, ThreadConstants.DeckPassS1 + holeLen + 2.0, s));
            double hole = AudioSpec.Db(AudioSpec.HoleDb * holeEnv);
            double pierEnv = ThreadMath.Smoothstep(AudioSpec.PierS0, AudioSpec.PierS1, s);
            double pierKeep = 1.0 - pierEnv;
            double common = picture * inhale * hole;   // everything except the drone

            // ---------------------------------------------------------- L1 wind
            double A = ThreadMath.VirtualAltitude(s);
            double rho = ThreadMath.AirDensity(Math.Max(0.0, A - ThreadConstants.CloudTop));
            double air = Math.Pow(rho, AudioSpec.WindDensityExponent);
            double windOff = 1.0 - ThreadMath.Smoothstep(ThreadConstants.WindOffS - AudioSpec.WindOffRamp, ThreadConstants.WindOffS, s);
            double wind = LayerAmp(AudioLayer.Wind, s) * air;
            wind = ThreadMath.Lerp(wind, AudioSpec.Db(AudioSpec.DeckWindDb), deckEnv);
            p.wind = (float)(wind * windOff * windIntro * inhale * hole);

            // ---------------------------------------------------------- L2 drone (the floor; only the hole and the pier touch it)
            p.drone = (float)(LayerAmp(AudioLayer.Drone, s) * droneIntro * hole * pierKeep);

            // ---------------------------------------------------------- L3 pad
            p.pad = (float)(LayerAmp(AudioLayer.Pad, s) * swell * common);
            var enter = AudioSpec.PadVoiceEnterS;
            p.pad0 = s >= enter[0] ? 1f : 0f;
            p.pad1 = s >= enter[1] ? 1f : 0f;
            p.pad2 = s >= enter[2] ? 1f : 0f;
            p.pad3 = s >= enter[3] ? 1f : 0f;

            // ---------------------------------------------------------- L4 colossus breath (distance and bearing per side)
            double breath = LayerAmp(AudioLayer.Breath, s) * common;
            if (breath > 0.0)
            {
                D3 eye = ThreadMath.CorridorToWorld(s, lateral, ThreadConstants.EyeHeight);
                ThreadMath.Right(s, out double rx, out double rz);
                double gRef = BreathReference;
                BreathSide(eye, rx, rz, -1.0, out double gl, out double panL);
                BreathSide(eye, rx, rz, +1.0, out double gr, out double panR);
                PanGains(panL, out double lL, out double lR);
                PanGains(panR, out double rL, out double rR);
                // two unit-RMS sources: the layer power breath^2 (gl^2 + gr^2) / (2 gRef^2) averages to breath^2
                double k = breath / (gRef * Math.Sqrt(2.0));
                double kl = k * gl, kr = k * gr;
                p.breathLL = (float)(kl * lL); p.breathLR = (float)(kl * lR);
                p.breathRL = (float)(kr * rL); p.breathRR = (float)(kr * rR);
            }

            // ---------------------------------------------------------- L5 mist hush (bank, and again inside the cloud deck)
            double hush = Math.Max(LayerAmp(AudioLayer.Hush, s), AudioSpec.Db(AudioSpec.DeckHushDb) * deckEnv);
            p.hush = (float)(hush * common);

            // ---------------------------------------------------------- L6 thread hum (enters at s = 330, swells with the pad)
            double humEntry = ThreadMath.Smoothstep(AudioSpec.HumEnterS - 2.0, AudioSpec.HumEnterS + 3.0, s);
            p.hum = (float)(LayerAmp(AudioLayer.Hum, s) * humEntry * swell * common * pierKeep);
            p.humUpper = (float)ThreadMath.Darkness(s);

            // ---------------------------------------------------------- L7 star notes
            p.stars = (float)(LayerAmp(AudioLayer.Stars, s) * common * pierKeep);
            p.starGate = ThreadMath.StarAlpha(s) > AudioSpec.StarAlphaGate ? 1f : 0f;

            // ---------------------------------------------------------- L8 beacon (within 40 m, -46 -> -38 dBFS at 6 m)
            {
                D3 eye = ThreadMath.CorridorToWorld(s, lateral, ThreadConstants.EyeHeight);
                double dx = eye.x, dy = eye.y - ThreadConstants.TopY, dz = eye.z - ThreadConstants.PadCentreZ;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                double near = ThreadMath.Clamp01((AudioSpec.BeaconRange - d) / (AudioSpec.BeaconRange - AudioSpec.BeaconNear));
                double gate = 1.0 - ThreadMath.Smoothstep(AudioSpec.BeaconRange - AudioSpec.BeaconFadeIn, AudioSpec.BeaconRange, d);
                double level = gate > 0.0 ? AudioSpec.Db(ThreadMath.Lerp(AudioSpec.BeaconDbFar, AudioSpec.BeaconDbNear, near)) * gate : 0.0;
                p.beacon = (float)(level * picture * hole);
            }

            // ---------------------------------------------------------- footstep bus (the player's own steps duck too)
            p.stepBus = (float)common;
            p.footGain = (float)(brisk ? AudioSpec.Db(AudioSpec.BriskStepDb) : 1.0);

            // ---------------------------------------------------------- master low-pass: bank sweep, inhale, exhale, cloud deck
            double bankM = ThreadMath.Smoothstep(AudioSpec.BankSweepS0, AudioSpec.BankSweepS1, s)
                         * (1.0 - ThreadMath.Smoothstep(ThreadConstants.ExhaleS0, ThreadConstants.ExhaleS1, s));
            double deckM = ThreadMath.Smoothstep(ThreadConstants.DeckPassS0 - 2.0, ThreadConstants.DeckPassS0 + 4.0, s)
                         * (1.0 - ThreadMath.Smoothstep(ThreadConstants.DeckPassS1 + 2.0, ThreadConstants.DeckPassS1 + 10.0, s));
            double lp = AudioSpec.LowpassOpenHz
                      * Math.Pow(AudioSpec.LowpassClosedHz / AudioSpec.LowpassOpenHz, bankM)
                      * Math.Pow(AudioSpec.DeckLowpassHz / AudioSpec.LowpassOpenHz, deckM);
            p.lowpassHz = (float)lp;

            // ---------------------------------------------------------- reverb per beat (14.3.4), dry on the pier
            p.rt60 = (float)ThreadMath.BeatBlend(AudioSpec.ReverbRt60, s);
            p.preDelayMs = (float)ThreadMath.BeatBlend(AudioSpec.ReverbPreDelayMs, s);
            p.wetDb = (float)ThreadMath.Lerp(ThreadMath.BeatBlend(AudioSpec.ReverbWetDb, s), AudioSpec.PierWetDb, pierEnv);
            p.dampHz = (float)ThreadMath.BeatBlend(AudioSpec.ReverbDampHz, s);

            // ---------------------------------------------------------- master: ending fade (6 s) and pause duck
            double end = endingTime >= 0.0 ? 1.0 - ThreadMath.Smootherstep(0.0, ThreadConstants.EndAudioFade, endingTime) : 1.0;
            double pause = AudioSpec.Db(AudioSpec.PauseDuckDb * ThreadMath.Clamp01(pauseAmount));
            p.master = (float)(end * pause);
            return p;
        }

        /// <summary>
        /// Table level of a layer at s as a linear amplitude: inside beat i the level ramps linearly in dB from its
        /// start to its end value; across each boundary the neighbouring beats cross-fade with smootherstep over
        /// the 24 m blend window (14.6). "Off" is amplitude 0, so a layer fades in or out over the window.
        /// </summary>
        public static double LayerAmp(int layer, double s)
        {
            var b = ThreadConstants.BeatBoundaries;
            double half = ThreadConstants.BlendWindow * 0.5;
            double v = BeatAmp(layer, 0, s);
            for (int i = 1; i < ThreadConstants.BeatCount; i++)
            {
                double w = ThreadMath.Smootherstep(b[i] - half, b[i] + half, s);
                if (w <= 0.0) break;   // boundaries are ordered: every later weight is 0 too
                v += (BeatAmp(layer, i, s) - BeatAmp(layer, i - 1, s)) * w;
            }
            return v;
        }

        static double BeatAmp(int layer, int beat, double s)
        {
            double a = AudioSpec.LevelStart[layer, beat], e = AudioSpec.LevelEnd[layer, beat];
            if (double.IsNegativeInfinity(a) || double.IsNegativeInfinity(e)) return 0.0;
            var b = ThreadConstants.BeatBoundaries;
            double u = ThreadMath.Clamp01((s - b[beat]) / (b[beat + 1] - b[beat]));
            return AudioSpec.Db(ThreadMath.Lerp(a, e, u));
        }

        /// <summary>Power-normalised pan law: (gl^2 + gr^2) / 2 = 1, centre = unity on both channels.</summary>
        public static void PanGains(double pan, out double gl, out double gr)
        {
            double th = (Math.Max(-1.0, Math.Min(1.0, pan)) + 1.0) * (Math.PI * 0.25);
            gl = Math.Sqrt(2.0) * Math.Cos(th);
            gr = Math.Sqrt(2.0) * Math.Sin(th);
        }

        /// <summary>
        /// Breath gain 1 / (1 + d / 40) of the nearest colossus on one side (14.3.2 L4), d measured from the eye to
        /// the elliptic footprint (22 x 18 m, x:z) of the robe; pan = bearing of that colossus in listener space.
        /// </summary>
        static void BreathSide(D3 eye, double rx, double rz, double side, out double gain, out double pan)
        {
            var pairs = ThreadConstants.ColossusPairZ;
            double best = double.MaxValue, bx = 0, bz = 1, bd = 1;
            double cx = side * ThreadConstants.ColossusCentreX;
            for (int i = 0; i < pairs.Length; i++)
            {
                double dx = cx - eye.x, dz = pairs[i] - eye.z;
                double dist = Math.Sqrt(dx * dx + dz * dz);
                double c = dist > 1e-6 ? dx / dist : 1.0, sn = dist > 1e-6 ? dz / dist : 0.0;
                double ex = c / ThreadConstants.ColossusFootprintX, ez = sn / ThreadConstants.ColossusFootprintZ;
                double rEll = 1.0 / Math.Sqrt(ex * ex + ez * ez);
                double d = Math.Max(0.0, dist - rEll);
                if (d < best) { best = d; bx = dx; bz = dz; bd = Math.Max(dist, 1e-6); }
            }
            gain = 1.0 / (1.0 + best / AudioSpec.BreathDistanceScale);
            // listener right vector (rx, rz): pan = cos of the angle between the source bearing and "right"
            pan = AudioSpec.BreathPanMax * (bx * rx + bz * rz) / bd;
        }

        static double s_breathRef = -1.0;

        /// <summary>
        /// RMS of the two-sided breath distance gain over beat 2 on the centre line, so the beat-2 average equals the
        /// -36 dBFS table level (computed once, deterministic).
        /// </summary>
        public static double BreathReference
        {
            get
            {
                if (s_breathRef > 0.0) return s_breathRef;
                var b = ThreadConstants.BeatBoundaries;
                double sum = 0.0; int n = 0;
                for (double s = b[1]; s <= b[2]; s += 0.5)
                {
                    D3 eye = ThreadMath.CorridorToWorld(s, 0.0, ThreadConstants.EyeHeight);
                    ThreadMath.Right(s, out double rx, out double rz);
                    BreathSide(eye, rx, rz, -1.0, out double gl, out _);
                    BreathSide(eye, rx, rz, +1.0, out double gr, out _);
                    sum += 0.5 * (gl * gl + gr * gr);
                    n++;
                }
                s_breathRef = Math.Sqrt(sum / Math.Max(1, n));
                return s_breathRef;
            }
        }
    }
}
