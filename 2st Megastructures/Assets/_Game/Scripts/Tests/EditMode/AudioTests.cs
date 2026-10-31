using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Procedural audio (MASTER_PROMPT 14.3, Part 9 "Audio", 11.9, Part 10 #25). Everything runs on the Unity-free DSP
    /// (RingAudioEngine and friends), so no audio device is needed. The offline walk render writes the excerpts
    /// QA/audio/beatNN.wav, s2_totality.wav, s3_tunnel.wav, walk.wav (1 s of every 5 m), QA/audio/report.json,
    /// QA/audio/excerpts.json (side-car for Tools/audio_analysis.py) and the per-beat trims QA/audio-trims.json, then
    /// asserts the 14.3 "Master" rules: true peak &lt;= -1 dBFS, |DC| &lt; 0.001, sample step &lt;= 0.25 outside the hatch
    /// sweep, no NaN, no clicks, the sliding 3 s RMS on the per-beat targets, smooth transitions, silent first / last
    /// 10 ms. RMS = 10 log10(mean(x^2)) over both channels (no +3 dB AES17 offset).
    /// </summary>
    public class AudioTests
    {
        const int Fs = 48000;
        const int Block = AudioOfflineRender.BlockFrames;

        /// <summary>Hooks for running these tests outside the Editor (a plain .NET runner): log sink and project root.</summary>
        internal static Action<string> LogSink;
        internal static string RootOverride;

        static void Log(string m) { if (LogSink != null) LogSink(m); else Debug.Log(m); }
        static string ProjectRoot => RootOverride ?? Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string QaDir => Path.Combine(ProjectRoot, "QA");
        static string AudioDir => Path.Combine(QaDir, "audio");
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        // =========================================================================================================
        // contracts: the Unity-free mirrors equal their sources
        // =========================================================================================================
        [Test]
        public void SpecMirrorsRingConstants()
        {
            for (int i = 0; i < RingConstants.BeatS.Length; i++) Assert.AreEqual(RingConstants.BeatS[i], AudioSpec.BeatS[i], 1e-6, "BeatS[" + i + "]");
            Assert.AreEqual(RingConstants.BeatS.Length, AudioSpec.BeatS.Length);
            Assert.AreEqual(RingConstants.GoalS, AudioSpec.GoalS, 1e-6);
            Assert.AreEqual(RingConstants.Walk, AudioSpec.Walk, 1e-6);
            Assert.AreEqual(RingConstants.Brisk, AudioSpec.Brisk, 1e-6);
            Assert.AreEqual(RingConstants.StrideWalk, AudioSpec.StrideWalk, 1e-6);
            Assert.AreEqual(RingConstants.StrideBrisk, AudioSpec.StrideBrisk, 1e-6);
            Assert.AreEqual(RingConstants.Eye, AudioSpec.Eye, 1e-6);
            Assert.AreEqual(RingConstants.RingR, AudioSpec.RingR, 1e-6);
            Assert.AreEqual(RingConstants.HatchTriggerS, AudioSpec.HatchTriggerS, 1e-6);
            Assert.AreEqual(RingConstants.BridgeZ0, AudioSpec.BridgeZ0, 1e-6);
            Assert.AreEqual(RingConstants.BridgeZ1, AudioSpec.BridgeZ1, 1e-6);
            Assert.AreEqual(RingConstants.RiverWidth * 0.5, AudioSpec.RiverHalfWidth, 1e-6);
            Assert.AreEqual(RingConstants.ArkX0, AudioSpec.ArkX0, 1e-6);
            Assert.AreEqual(RingConstants.ArkX1, AudioSpec.ArkX1, 1e-6);
            Assert.AreEqual(RingConstants.ArkT, AudioSpec.ArkT, 1e-6);
            Assert.AreEqual(RingConstants.ArkD, AudioSpec.ArkD, 1e-6);
            Assert.AreEqual(RingConstants.ArkTriggerS, AudioSpec.ArkTriggerS, 1e-6);
            Assert.AreEqual(RingConstants.ArkAltitude, AudioSpec.ArkAltitude, 1e-6);
            Assert.AreEqual(RingConstants.ArkTrackZ, AudioSpec.ArkTrackZ, 1e-6);
            Assert.AreEqual(RingConstants.SunsetS0, AudioSpec.SunsetS0, 1e-6);
            Assert.AreEqual(RingConstants.SunsetT, AudioSpec.SunsetT, 1e-6);
            Assert.AreEqual(RingConstants.SunsetD, AudioSpec.SunsetD, 1e-6);
            Assert.AreEqual(RingConstants.SwapS, AudioSpec.SwapS, 1e-6);
            Assert.AreEqual(RingConstants.WallX, AudioSpec.WallX, 1e-6);
            Assert.AreEqual(RingConstants.TunnelRibX0, AudioSpec.TunnelRibX0, 1e-6);
            Assert.AreEqual(RingConstants.TunnelRibPitch, AudioSpec.TunnelRibPitch, 1e-6);
            Assert.AreEqual(RingConstants.TunnelRibs, AudioSpec.TunnelRibs);
            Assert.AreEqual(RingConstants.BeaconFirstPingS, AudioSpec.BeaconFirstPingS, 1e-6);
            Assert.AreEqual(RingConstants.BeaconPeriod, AudioSpec.BeaconPeriod, 1e-6);
            Assert.AreEqual(RingConstants.FadeInStart, AudioSpec.FadeInStart, 1e-6);
            Assert.AreEqual(RingConstants.FadeInEnd, AudioSpec.FadeInEnd, 1e-6);
            Assert.AreEqual(RingConstants.EndDwell, AudioSpec.EndDwell, 1e-6);
            Assert.AreEqual(RingConstants.EndFade, AudioSpec.EndFade, 1e-6);

            var w = RingConstants.Waypoints;
            Assert.AreEqual(w.Length, AudioSpec.WaypointX.Length);
            for (int i = 0; i < w.Length; i++)
            {
                Assert.AreEqual(w[i].x, AudioSpec.WaypointX[i], 1e-6, "waypoint x " + i);
                Assert.AreEqual(w[i].y, AudioSpec.WaypointZ[i], 1e-6, "waypoint z " + i);
            }
            var rc = RingConstants.RiverCentre;
            Assert.AreEqual(rc.Length, AudioSpec.RiverX.Length);
            for (int i = 0; i < rc.Length; i++)
            {
                Assert.AreEqual(rc[i].x, AudioSpec.RiverX[i], 1e-6, "river x " + i);
                Assert.AreEqual(rc[i].y, AudioSpec.RiverZ[i], 1e-6, "river z " + i);
            }
            var h = RingConstants.CinematicHolds;
            Assert.AreEqual(h.Length, AudioSpec.HoldS.Length);
            for (int i = 0; i < h.Length; i++)
            {
                Assert.AreEqual(h[i].x, AudioSpec.HoldS[i], 1e-4, "hold s " + i);
                Assert.AreEqual(h[i].y, AudioSpec.HoldT[i], 1e-4, "hold t " + i);
            }

            // footstep surfaces: enum values and the 14.4 table
            Assert.AreEqual((int)RingSurface.Metal, AudioSpec.SurfMetal);
            Assert.AreEqual((int)RingSurface.MetalRamp, AudioSpec.SurfMetalRamp);
            Assert.AreEqual((int)RingSurface.Ceramic, AudioSpec.SurfCeramic);
            Assert.AreEqual((int)RingSurface.Grass, AudioSpec.SurfGrass);
            Assert.AreEqual((int)RingSurface.Bridge, AudioSpec.SurfBridge);
            Assert.AreEqual((int)RingSurface.Underpass, AudioSpec.SurfUnderpass);
            Assert.AreEqual((int)RingSurface.Stone, AudioSpec.SurfStone);
            Assert.AreEqual((int)RingSurface.Tunnel, AudioSpec.SurfTunnel);
            Assert.AreEqual((int)RingSurface.Deck, AudioSpec.SurfDeck);
            for (float s = 0f; s <= 960f; s += 0.25f)
            {
                Assert.AreEqual((int)RingEvents.SurfaceAt(s, false), AudioSpec.SurfaceAt(s, false), "surface at s " + s);
                Assert.AreEqual((int)RingEvents.SurfaceAt(s, true), AudioSpec.SurfaceAt(s, true), "grass surface at s " + s);
                Assert.AreEqual(RingConstants.BeatAt(s), AudioSpec.BeatAt(s), "beat at s " + s);
            }

            // the offline path and river equal RingWorld's
            for (float s = 0f; s <= 960f; s += 1.7f)
            {
                var pt = RingWorld.PointAt(s);
                AudioWalkSim.PointAt(s, out double x, out double z, out _, out _);
                Assert.AreEqual(pt.x, x, 1e-3, "path x at s " + s);
                Assert.AreEqual(pt.y, z, 1e-3, "path z at s " + s);
            }
            var sim = new AudioWalkSim();
            for (int i = 0; i < 40; i++)
            {
                float x = -300f + 23.7f * i, z = 40f + 11.3f * i;
                sim.RiverAt(x, z, out double cd, out _);
                Assert.AreEqual(RingWorld.RiverDist(x, z), cd, 0.05, "river distance at " + x + ", " + z);
            }
        }

        [Test]
        public void TrimTableIsConsistent()
        {
            var ks = AudioTrims.KnotS; var kb = AudioTrims.KnotBeat; var kd = AudioTrims.KnotDb;
            Assert.AreEqual(ks.Length, kb.Length);
            Assert.AreEqual(ks.Length, kd.Length);
            for (int i = 0; i < ks.Length; i++)
            {
                if (i > 0) Assert.Greater(ks[i], ks[i - 1], "knots ascending");
                Assert.AreEqual(AudioSpec.BeatAt(Math.Min(ks[i], 959.9)), kb[i], "knot " + i + " beat");
                Assert.LessOrEqual(Math.Abs(kd[i]), 9.0, "trim " + i + " is a trim, not a mix");
            }
            for (int b = 0; b < 7; b++)
            {
                int n = 0; for (int i = 0; i < kb.Length; i++) if (kb[i] == b) n++;
                Assert.GreaterOrEqual(n, 2, "beat " + (b + 1) + " has trim knots");
            }
            Assert.AreEqual(7, AudioSpec.TargetStartDb.Length);
            Assert.AreEqual(7, AudioSpec.TargetEndDb.Length);
            double[] start = { -38, -27, -24, -28, -36, -40, -28 }, end = { -38, -27, -24, -31, -36, -32, -28 };
            for (int b = 0; b < 7; b++)
            {
                Assert.AreEqual(start[b], AudioSpec.TargetStartDb[b], 1e-9, "14.3 Master target start, beat " + (b + 1));
                Assert.AreEqual(end[b], AudioSpec.TargetEndDb[b], 1e-9, "14.3 Master target end, beat " + (b + 1));
            }
        }

        // =========================================================================================================
        // DSP building blocks
        // =========================================================================================================
        [Test]
        public void TruePeakMeterReadsInterSamplePeaks()
        {
            // research 05 section 9 self-test: a sine at fs/4 with phase pi/4 has sample peaks of 0.7071 but a true peak of 1
            var an = new AudioAnalyzer(Fs, 10);
            int n = Fs;
            var x = new float[n * 2];
            for (int i = 0; i < n; i++) { float v = (float)Math.Sin(0.5 * Math.PI * i + 0.25 * Math.PI); x[2 * i] = v; x[2 * i + 1] = v; }
            an.Feed(x, n, 1, 100, 1, false, 0);
            Assert.AreEqual(-3.0103, DspMath.LinToDb(an.Peak[1]), 0.01, "sample peak");
            // the 65-tap windowed-sinc interpolator ripples by about +0.1 dB at fs/4: it may over-read, never under-read
            double tp = DspMath.LinToDb(an.TruePeak[1]);
            Assert.That(tp, Is.InRange(-0.1, 0.25), "true peak (4x oversampled)");
        }

        [Test]
        public void LimiterHoldsTheCeilingAndIsTransparentBelowThreshold()
        {
            var lim = new DspLimiter();
            lim.Configure(Fs, AudioSpec.LimiterThresholdDb, AudioSpec.LimiterCeilingDb, AudioSpec.LimiterLookaheadMs, AudioSpec.LimiterReleaseMs);
            Assert.AreEqual((int)Math.Round(0.005 * Fs) - 1, lim.LatencySamples);
            var rng = new DspRng(7u);
            double ceil = DspMath.DbToLin(AudioSpec.LimiterCeilingDb), maxOut = 0;
            for (int i = 0; i < Fs * 4; i++)
            {
                // 4 s of bursts up to +12 dBFS with single-sample spikes
                double env = (i / 4800) % 2 == 0 ? 4.0 : 0.3;
                double l = env * rng.Bipolar(), r = env * Math.Sin(2 * Math.PI * 440.0 * i / Fs);
                if (i % 9973 == 0) l = 8.0;
                lim.Process(ref l, ref r);
                maxOut = Math.Max(maxOut, Math.Max(Math.Abs(l), Math.Abs(r)));
            }
            Assert.LessOrEqual(maxOut, ceil + 1e-9, "sample peak above the -1.2 dBFS internal ceiling");

            // below the -3 dB threshold the limiter is a pure delay
            lim.Configure(Fs, AudioSpec.LimiterThresholdDb, AudioSpec.LimiterCeilingDb, AudioSpec.LimiterLookaheadMs, AudioSpec.LimiterReleaseMs);
            lim.MinGain = 1.0;
            int lat = lim.LatencySamples;
            var hist = new double[Fs];
            double maxErr = 0;
            for (int i = 0; i < Fs; i++)
            {
                double v = 0.5 * Math.Sin(2 * Math.PI * 1000.0 * i / Fs);    // -6 dBFS peak
                hist[i] = v;
                double l = v, r = v;
                lim.Process(ref l, ref r);
                if (i >= lat) maxErr = Math.Max(maxErr, Math.Abs(l - hist[i - lat]));
            }
            Assert.Less(maxErr, 1e-12, "not transparent below the threshold");
            Assert.AreEqual(1.0, lim.MinGain, 1e-12);
        }

        [Test]
        public void SvfStaysStableUnderPerSampleModulation()
        {
            var svf = new DspSvf();
            var rng = new DspRng(3u);
            double maxAbs = 0;
            int nan = 0;
            for (int i = 0; i < Fs * 2; i++)
            {
                double ph = (double)i / (Fs * 2);
                double fc = 20.0 * Math.Pow(1000.0, 0.5 + 0.5 * Math.Sin(2 * Math.PI * 7.0 * ph));   // 20 Hz .. 20 kHz
                double q = 0.5 + 9.5 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 3.0 * ph));
                svf.Set(fc, q, Fs);
                svf.Tick(rng.Bipolar(), out double lp, out double bp, out double hp);
                if (double.IsNaN(lp) || double.IsNaN(bp) || double.IsNaN(hp)) nan++;
                maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(lp), Math.Max(Math.Abs(bp), Math.Abs(hp))));
            }
            Assert.AreEqual(0, nan, "NaN samples");
            Assert.Less(maxAbs, 50.0, "TPT SVF blew up under modulation");
        }

        static double BandRms(Func<double> src, double fc, double q, int n)
        {
            var bp = new DspSvf(); bp.Set(fc, q, Fs);
            double acc = 0;
            for (int i = 0; i < Fs / 2; i++) bp.BandNorm(src());
            for (int i = 0; i < n; i++) { double v = bp.BandNorm(src()); acc += v * v; }
            return Math.Sqrt(acc / n);
        }

        [Test]
        public void PinkIsFlatPerOctaveAndBrownFallsSixDb()
        {
            int n = Fs * 20;
            var r1 = new DspRng(11u); var pink = new DspPink();
            var r2 = new DspRng(11u); var pink2 = new DspPink();
            double lo = BandRms(() => pink.Next(r1.Bipolar()), 300.0, 1.41, n);
            double hi = BandRms(() => pink2.Next(r2.Bipolar()), 4800.0, 1.41, n);
            Assert.AreEqual(0.0, DspMath.LinToDb(hi / lo), 1.5, "Kellet pink: equal energy per constant-Q band");

            var r3 = new DspRng(12u); var br = new DspBrown();
            var r4 = new DspRng(12u); var br2 = new DspBrown();
            double b500 = BandRms(() => br.Next(r3.Bipolar()), 500.0, 1.41, n);
            double b2000 = BandRms(() => br2.Next(r4.Bipolar()), 2000.0, 1.41, n);
            Assert.AreEqual(-6.0, DspMath.LinToDb(b2000 / b500), 1.5, "leaky brown: -6 dB/oct (-3 dB per octave in constant-Q bands, two octaves)");
        }

        [Test]
        public void DcBlockerRemovesOffset()
        {
            var dc = new DspDcBlock(); dc.Set(AudioSpec.DcBlockHz, Fs);
            double sum = 0; int n = 0;
            for (int i = 0; i < Fs * 3; i++)
            {
                double y = dc.Tick(0.5 + 0.1 * Math.Sin(2 * Math.PI * 200.0 * i / Fs));
                if (i >= Fs) { sum += y; n++; }
            }
            Assert.Less(Math.Abs(sum / n), 1e-4);
        }

        [Test]
        public void GustIsDeterministicSmoothAndShared()
        {
            uint seed = AudioParams.Default().GustSeed;
            double prev = DspGust.Gust01(seed, 0), maxD = 0, min = 1, max = 0;
            for (double t = 0.01; t < 600; t += 0.01)
            {
                double g = DspGust.Gust01(seed, t);
                Assert.That(g, Is.InRange(0.0, 1.0));
                Assert.AreEqual(g, DspGust.Gust01(seed, t), 0.0, "pure function of time");
                Assert.AreEqual((float)g, RingAudioEngine.GustAt(seed, t), 0f, "main thread and audio thread share the W1 gust");
                maxD = Math.Max(maxD, Math.Abs(g - prev)); prev = g;
                min = Math.Min(min, g); max = Math.Max(max, g);
            }
            Assert.Less(maxD, 0.02, "smooth random must be smooth at 100 Hz");
            Assert.Greater(max - min, 0.4, "gusts must actually vary");
            Assert.AreNotEqual(DspGust.Gust01(seed, 123.4), DspGust.Gust01(seed + 1, 123.4));
            Assert.AreEqual(DspGust.Floor + DspGust.Depth, 1.0, 1e-12, "W1 gust gain 0.35 + 0.65 smooth-random");
            Assert.AreEqual(0.15, DspGust.Rate, 1e-12);
        }

        [Test]
        public void GustLoudnessCompensationHoldsTheThreeSecondLevel()
        {
            // 14.3 Master wins over the W1 layer level: the 3 s level of the gust gain is brought into a band that fits
            // the +-1.5 dB tolerance, while the gust itself keeps moving
            uint seed = AudioParams.Default().GustSeed;
            const int n = 20000;                      // 2 000 s at the 0.1 s grid
            var raw = new double[n]; var comp = new double[n];
            double prevC = double.NaN, maxGridStep = 0;
            for (int i = 0; i < n; i++)
            {
                double t = 500.0 + RingAudioEngine.GustCompStep * i;
                double g = DspGust.Gain(seed, t), c = RingAudioEngine.GustCompAt(seed, t);
                Assert.That(c, Is.InRange(0.5, 2.5), "compensation factor at t " + t);
                Assert.AreEqual(c, RingAudioEngine.GustCompAt(seed, t), 0.0, "pure function of time");
                if (!double.IsNaN(prevC)) maxGridStep = Math.Max(maxGridStep, Math.Abs(c / prevC - 1.0));
                prevC = c;
                raw[i] = g * g; comp[i] = g * g * c * c;
            }
            // the engine interpolates linearly between the 0.1 s grid points: a slow factor, never a step
            Assert.Less(maxGridStep, 0.03, "compensation changes by less than 3 % per 0.1 s");
            double rLo = 99, rHi = -99, cLo = 99, cHi = -99, sum = 0, sum2 = 0; int m = 0;
            int h = RingAudioEngine.GustCompHalf;
            for (int i = h; i < n - h; i++)
            {
                double a = 0, b = 0;
                for (int j = -h; j <= h; j++) { a += raw[i + j]; b += comp[i + j]; }
                double ra = 10.0 * Math.Log10(a / (2 * h + 1)), cb = 10.0 * Math.Log10(b / (2 * h + 1));
                rLo = Math.Min(rLo, ra); rHi = Math.Max(rHi, ra); cLo = Math.Min(cLo, cb); cHi = Math.Max(cHi, cb);
                double inst = 10.0 * Math.Log10(comp[i]); sum += inst; sum2 += inst * inst; m++;
            }
            double std = Math.Sqrt(Math.Max(0.0, sum2 / m - (sum / m) * (sum / m)));
            Log(F("[Ring] W1 3 s gust level: {0:0.00} dB band uncompensated, {1:0.00} dB compensated (exponent {2:0.00}); gain std {3:0.00} dB; grid step {4:0.0000}",
                rHi - rLo, cHi - cLo, RingAudioEngine.GustCompExponent, std, maxGridStep));
            Assert.Greater(rHi - rLo, 6.0, "the gust as specified swings the 3 s level beyond the +-1.5 dB tolerance");
            Assert.Less(cHi - cLo, 3.0, "compensated 3 s wind level inside a 3 dB band");
            Assert.Greater(std, 0.25, "the compensated gusts still move the wind level (full compensation leaves 0.21 dB)");
        }

        [Test]
        public void ArkPanIsContinuousThroughTheBack()
        {
            // 14.3 A1 "pan = azimuth / 60" in the front half plane, folded through the back (stereo has no rear)
            for (double az = -90; az <= 90; az += 0.5) Assert.AreEqual(DspMath.Clamp(az / 60.0, -1.0, 1.0), RingAudioEngine.ArkPan(az), 1e-12, "az " + az);
            double prev = RingAudioEngine.ArkPan(-540);
            for (double az = -540; az <= 540; az += 0.25)
            {
                double pan = RingAudioEngine.ArkPan(az);
                Assert.LessOrEqual(Math.Abs(pan - prev), 0.25 / 60.0 + 1e-9, "A1 pan jumps at azimuth " + az);
                prev = pan;
            }
            Assert.AreEqual(0.0, RingAudioEngine.ArkPan(180), 1e-12);
            Assert.AreEqual(0.0, RingAudioEngine.ArkPan(-180), 1e-12);
            Assert.AreEqual(0.0, RingAudioEngine.ArkPan(double.NaN), 0.0);
            Assert.AreEqual(AudioSpec.LayerRampTau, RingAudioEngine.PanTau, 1e-12, "pan followers ramp like the layers (50 ms)");

            // a free-look player facing away from the ark: the azimuth wraps +179 -> -179; the image stays centred
            var eng = new RingAudioEngine(Fs, 1u);
            var p = Steady(380, -0.5);
            p.ArkRange = 3300f;
            var buf = new float[Block * 2];
            double t = 0;
            var lDb = new double[2]; var rDb = new double[2];
            for (int half = 0; half < 2; half++)
            {
                p.ArkAzimuth = half == 0 ? 179f : -179f;
                double el = 0, er = 0; long cnt = 0;
                for (int b = 0; b < 3 * Fs / Block; b++)
                {
                    p.Frame++; p.T = t;
                    eng.Render(buf, Block, 2, Fs, in p);
                    t += (double)Block / Fs;
                    if (b < Fs / Block) continue;           // the first second settles
                    for (int i = 0; i < Block; i++) { el += (double)buf[2 * i] * buf[2 * i]; er += (double)buf[2 * i + 1] * buf[2 * i + 1]; }
                    cnt += Block;
                }
                lDb[half] = 10.0 * Math.Log10(el / cnt + 1e-30); rDb[half] = 10.0 * Math.Log10(er / cnt + 1e-30);
                Assert.AreEqual(lDb[half], rDb[half], 1.5, "ark straight behind is centred (azimuth " + p.ArkAzimuth + ")");
            }
            Assert.AreEqual(lDb[0], lDb[1], 1.0, "left channel level across the +-180 wrap");
            Assert.AreEqual(rDb[0], rDb[1], 1.0, "right channel level across the +-180 wrap");
        }

        [Test]
        public void SeqlockHandOffNeverTears()
        {
            var ch = new AudioParamChannel();
            int stop = 0, reads = 0, bad = 0;
            var writer = new Thread(() =>
            {
                var p = AudioParams.Default();
                for (int k = 1; Volatile.Read(ref stop) == 0; k++)
                {
                    p.Frame = k; p.S = k; p.T = 2.0 * k; p.Speed = k & 1023; p.SunsetU = -k; p.BeaconT0 = 3.0 * k;
                    ch.Publish(in p);
                }
            });
            writer.Start();
            var q = AudioParams.Default();
            var deadline = DateTime.UtcNow.AddMilliseconds(400);
            while (DateTime.UtcNow < deadline)
            {
                if (!ch.TryRead(ref q)) continue;
                reads++;
                if (q.T != 2.0 * q.S || q.BeaconT0 != 3.0 * q.S || q.Frame != (int)q.S || q.SunsetU != -(float)q.Frame) bad++;
            }
            Volatile.Write(ref stop, 1);
            writer.Join();
            Assert.Greater(reads, 100);
            Assert.AreEqual(0, bad, "torn AudioParams snapshots");

            var queue = new AudioEventQueue(8);
            for (int i = 0; i < 8; i++) Assert.IsTrue(queue.TryEnqueue(new AudioEvent { Kind = 1, Surface = i }));
            Assert.IsFalse(queue.TryEnqueue(new AudioEvent { Kind = 1 }), "capacity");
            for (int i = 0; i < 8; i++) { Assert.IsTrue(queue.TryDequeue(out var e)); Assert.AreEqual(i, e.Surface, "FIFO"); }
            Assert.IsFalse(queue.TryDequeue(out _));
        }

        [Test]
        public void FootstepsPeakAtMinus30DbfsOnEverySurface()
        {
            for (int surf = 0; surf < AudioSpec.SurfCount; surf++)
            {
                double lo = 0, hi = -200, sum = 0;
                for (int k = 0; k < 48; k++)
                {
                    var v = new DspFootstep();
                    v.Start(Fs, surf, k & 1, AudioSpec.Walk, DspHash.Seed(5u, (uint)surf, (uint)k));
                    double pk = 0; int len = 0;
                    while (v.Active && len < Fs * 2) { v.Next(out double l, out double r); pk = Math.Max(pk, Math.Max(Math.Abs(l), Math.Abs(r))); len++; }
                    Assert.Less(len, Fs * 2, "a footstep voice never ends");
                    double db = DspMath.LinToDb(pk);
                    lo = Math.Min(lo, db); hi = Math.Max(hi, db); sum += db;
                }
                Assert.AreEqual(AudioSpec.FootPeakDb, sum / 48, 0.3, "mean step peak, surface " + surf);
                Assert.GreaterOrEqual(lo, AudioSpec.FootPeakDb - 0.85, "+-0.8 dB variation, surface " + surf);
                Assert.LessOrEqual(hi, AudioSpec.FootPeakDb + 0.85, "+-0.8 dB variation, surface " + surf);
            }
        }

        [Test]
        public void HatchSweepPeaksAtMinus30Dbfs()
        {
            foreach (uint seed in new[] { 1u, 99u, 123u, 0xA7u })
            {
                var h = new DspHatch(); h.Start(Fs, seed);
                double pkSweep = 0, pk = 0, t = 0;
                while (h.Active)
                {
                    h.Next(out double l, out double r);
                    pkSweep = Math.Max(pkSweep, Math.Max(Math.Abs(h.LastSweepL), Math.Abs(h.LastSweepR)));
                    pk = Math.Max(pk, Math.Max(Math.Abs(l), Math.Abs(r)));
                    t += 1.0 / Fs;
                }
                Assert.AreEqual(AudioSpec.HatchSweepPeakDb, DspMath.LinToDb(pkSweep), 0.05, "hatch noise sweep peak, seed " + seed);
                Assert.Less(DspMath.LinToDb(pk), -24.0, "sweep + servo");
                Assert.AreEqual(1.45, t, 0.01, "sweep 1.4 s + release");
            }
        }

        // =========================================================================================================
        // engine behaviour
        // =========================================================================================================
        static AudioParams Steady(double s, double u = -4)
        {
            var p = AudioParams.Default();
            p.S = s; p.SunsetU = (float)u; p.PictureFade = 1f; p.HatchT = -100; p.Speed = 0f;
            return p;
        }

        static double RenderRms(RingAudioEngine eng, ref AudioParams p, ref double t, double seconds, float[] buf, bool measure)
        {
            int blocks = (int)Math.Round(seconds * Fs / Block);
            double acc = 0; long n = 0; int nan = 0;
            for (int b = 0; b < blocks; b++)
            {
                p.Frame++; p.T = t;
                eng.Render(buf, Block, 2, Fs, in p);
                t += (double)Block / Fs;
                if (!measure) continue;
                for (int i = 0; i < Block * 2; i++) { if (float.IsNaN(buf[i])) nan++; acc += (double)buf[i] * buf[i]; }
                n += Block * 2;
            }
            Assert.AreEqual(0, nan, "NaN samples");
            return n > 0 ? 10.0 * Math.Log10(acc / n + 1e-30) : double.NaN;
        }

        [Test]
        public void BeaconPingsLockToThePulsePeaks()
        {
            var eng = new RingAudioEngine(Fs, 1u);
            var p = Steady(930, 3);
            var buf = new float[Block * 2];
            const double t0 = 7.3;
            int seen = 0;
            for (int b = 0; b < 30 * Fs / Block; b++)
            {
                double t = (double)b * Block / Fs;
                p.Frame = b; p.T = t; p.BeaconT0 = t >= t0 ? t0 : -1.0;
                eng.Render(buf, Block, 2, Fs, in p);
                if (eng.PingCount == seen) continue;
                Assert.AreEqual(seen + 1, eng.PingCount, "one ping per period");
                double tp = eng.LastPingT;
                Assert.AreEqual(t0 + 1.0 + AudioSpec.BeaconPeriod * seen, tp, 1e-6, "ping " + seen + " at t0 + 1 + 4 k (the pulse peaks)");
                Assert.AreEqual(RingAudioEngine.PingTime(t0, seen), tp, 1e-6);
                double ph = RingAudioEngine.PingPhase(tp, t0);
                Assert.Less(Math.Min(ph, 1.0 - ph), 1e-6, "ping phase 0 at the ping");
                // a pulse peak: the maximum of 0.775 + amp sin(2 pi (t - t0) / 4), the ease-in complete at the first one
                float at = RingAudioEngine.BeaconIntensity(tp, t0);
                Assert.AreEqual(1.0f, at, 1e-4f, "pulse peak at the ping");
                Assert.Less(RingAudioEngine.BeaconIntensity(tp - 0.25, t0), at);
                Assert.Less(RingAudioEngine.BeaconIntensity(tp + 0.25, t0), at);
                seen++;
            }
            Assert.AreEqual(6, seen, "pings at 8.3, 12.3, ... 28.3 s");
            Assert.AreEqual((float)RingAudioEngine.BeaconBase, RingAudioEngine.BeaconIntensity(5.0, t0), 1e-6f, "steady before s 905");
            Assert.AreEqual((float)RingAudioEngine.BeaconBase, RingAudioEngine.BeaconIntensity(t0, t0), 1e-6f, "the pulse eases in from s 905");
            Assert.AreEqual(-1.0, RingAudioEngine.PingPhase(t0 + 0.5, t0), 0.0, "no ping before the first peak");

            // an origin that moves by float rounding must not re-play the ping
            var eng2 = new RingAudioEngine(Fs, 1u);
            var p2 = Steady(930, 3);
            for (int b = 0; b < 3 * Fs / Block; b++)
            {
                double t = (double)b * Block / Fs;
                p2.Frame = b; p2.T = t; p2.BeaconT0 = (b & 1) == 0 ? 1.0 : 1.0 + 1e-6;
                eng2.Render(buf, Block, 2, Fs, in p2);
            }
            Assert.AreEqual(1, eng2.PingCount, "jittering origin re-played a ping");
        }

        [Test]
        public void RibTicksOnePerRibPassed()
        {
            var eng = new RingAudioEngine(Fs, 1u);
            var p = Steady(770, 3);
            var buf = new float[Block * 2];
            double t = 0, dt = (double)Block / Fs;
            for (double s = 770; s < 900; s += AudioSpec.Walk * dt)
            {
                p.Frame++; p.S = s; p.T = t; p.Speed = (float)AudioSpec.Walk;
                eng.Render(buf, Block, 2, Fs, in p);
                t += dt;
            }
            Assert.AreEqual(AudioSpec.TunnelRibs, eng.TickCount, "rib ticks at s 777 + 6 k");
        }

        [Test]
        public void RenderIsAllocationFree()
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096];
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            if (a1 - a0 <= 0 || probe.Length == 0) Assert.Inconclusive("GC.GetAllocatedBytesForCurrentThread is not supported by this runtime");

            var eng = new RingAudioEngine(Fs, 1u);
            var buf = new float[Block * 2];
            var p = Steady(905.5, 3);
            p.BeaconT0 = 0.1;
            double t = 0;
            for (int s = 0; s < AudioSpec.SurfCount; s++)       // warm-up: JIT every voice once
                eng.Events.TryEnqueue(new AudioEvent { Kind = AudioEventKind.Footstep, Surface = s, Foot = s & 1, Speed = 1.5f, T = 0.05 * s });
            RenderRms(eng, ref p, ref t, 2.0, buf, false);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int b = 0; b < 400; b++)
            {
                if ((b & 7) == 0) eng.Events.TryEnqueue(new AudioEvent { Kind = AudioEventKind.Footstep, Surface = b % AudioSpec.SurfCount, Foot = b & 1, Speed = 1.5f, T = t });
                p.Frame++; p.T = t; p.S = 905.5 + 0.03 * b;
                eng.Render(buf, Block, 2, Fs, in p);
                t += (double)Block / Fs;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before, "RingAudioEngine.Render allocated");
        }

        [Test]
        public void PauseDucksTwelveDbInPointFourSeconds()
        {
            var eng = new RingAudioEngine(Fs, 1u);
            var buf = new float[Block * 2];
            var p = Steady(700, 3);       // steady laminar wind + H1
            double t = 0;
            RenderRms(eng, ref p, ref t, 4.0, buf, false);
            double open = RenderRms(eng, ref p, ref t, 4.0, buf, true);
            p.PauseDuck = 1f;
            RenderRms(eng, ref p, ref t, 0.5, buf, false);
            double ducked = RenderRms(eng, ref p, ref t, 4.0, buf, true);
            Assert.AreEqual(AudioSpec.PauseDuckDb, ducked - open, 1.5, "pause duck");
            p.PauseDuck = 0f;
            RenderRms(eng, ref p, ref t, 0.5, buf, false);
            double back = RenderRms(eng, ref p, ref t, 4.0, buf, true);
            Assert.AreEqual(open, back, 1.5, "level after resume");
        }

        [Test]
        public void SilentWhenTheFadeIsClosedAndAnyChannelCountWorks()
        {
            var eng = new RingAudioEngine(Fs, 1u);
            var p = Steady(150);
            p.PictureFade = 0f;
            double t = 0;
            var buf = new float[Block * 2];
            double db = RenderRms(eng, ref p, ref t, 1.0, buf, true);
            Assert.Less(db, -120.0, "picture fade 0 must be silent");

            foreach (int ch in new[] { 1, 2, 6 })
            {
                var e = new RingAudioEngine(Fs, 2u);
                var q = Steady(150);
                var data = new float[Block * ch];
                for (int b = 0; b < 100; b++) { q.Frame = b; q.T = (double)b * Block / Fs; e.Render(data, ch, Fs, in q); }
                double sum = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    Assert.IsFalse(float.IsNaN(data[i]));
                    if (ch > 2 && i % ch >= 2) Assert.AreEqual(0f, data[i], "channels above L/R are silent");
                    sum += data[i] * data[i];
                }
                Assert.Greater(sum, 0.0, ch + " channels produce sound");
            }
        }

        [Test]
        public void SampleRateChangeMidStreamIsClean()
        {
            var eng = new RingAudioEngine(Fs, 1u);
            var p = Steady(380, -0.5);
            p.ArkRange = 3300f; p.ArkAzimuth = -20f;
            var an = new AudioAnalyzer(Fs, 20);
            var buf = new float[Block * 2];
            double t = 0;
            for (int b = 0; b < 400; b++)
            {
                int rate = b < 200 ? Fs : 44100;
                p.Frame = b; p.T = t;
                eng.Render(buf, Block, 2, rate, in p);
                t += (double)Block / rate;
                an.Feed(buf, Block, 2, 380, t, false, 0);
            }
            Assert.AreEqual(44100.0, eng.SampleRate, 1e-9);
            Assert.AreEqual(0, an.NaNs[2]);
            Assert.LessOrEqual(DspMath.LinToDb(Math.Max(an.TruePeak[2], an.Peak[2])), AudioSpec.PeakCeilingDbfs);
            Assert.LessOrEqual(an.MaxStep[2], AudioSpec.MaxStep);
        }

        [Test]
        public void ReverbZonesFollowTheSoundBible()
        {
            var z = ReverbZones.Zone;
            Assert.AreEqual(ReverbZones.Count, z.Length);
            double[] decay = { 0.6, 1.2, 4.0, 3.0, 1.1, 2.4, 3.6, 0.8 };
            for (int i = 0; i < z.Length; i++) Assert.AreEqual(decay[i], z[i].DecayTime, 1e-9, "14.3 decay of " + ReverbZones.Names[i]);
            Assert.AreEqual(-1200, z[ReverbZones.Capsule].Room, 1e-9);
            Assert.AreEqual(-2500, z[ReverbZones.Valley].Room, 1e-9);
            Assert.AreEqual(-2000, z[ReverbZones.Ark].Room, 1e-9);
            Assert.AreEqual(0.4, z[ReverbZones.Ark].DecayHFRatio, 1e-9);
            Assert.AreEqual(0.08, z[ReverbZones.Wall].ReflectionsDelay, 1e-9, "wall echo 28 m / 343 m/s");
            Assert.GreaterOrEqual(z[ReverbZones.Wall].ReflectionsLevel, -1500.0, "reflectionsLevel raised so the 0.08 s echo is audible");
            Assert.GreaterOrEqual(3.0 * ReverbZones.FollowTau, 2.0, "reverb blends over >= 2 s");
            for (double s = 0; s <= 960; s += 0.5)
            {
                var r = ReverbZones.Target(s, s > 8 ? 1000.0 : double.NegativeInfinity);
                Assert.That(r.Room, Is.InRange(-10000.0, 0.0)); Assert.That(r.RoomHF, Is.InRange(-10000.0, 0.0));
                Assert.That(r.DecayTime, Is.InRange(0.1, 20.0)); Assert.That(r.DecayHFRatio, Is.InRange(0.1, 2.0));
                Assert.That(r.ReflectionsLevel, Is.InRange(-10000.0, 1000.0)); Assert.That(r.ReflectionsDelay, Is.InRange(0.0, 0.3));
                Assert.That(r.ReverbLevel, Is.InRange(-10000.0, 2000.0)); Assert.That(r.ReverbDelay, Is.InRange(0.0, 0.1));
                Assert.That(r.Diffusion, Is.InRange(0.0, 100.0)); Assert.That(r.Density, Is.InRange(0.0, 100.0));
            }
            Assert.AreEqual(2.4, ReverbZones.Target(700, 1000).DecayTime, 1e-6, "wall");
            Assert.AreEqual(3.6, ReverbZones.Target(830, 1000).DecayTime, 1e-6, "tunnel");
            Assert.AreEqual(0.8, ReverbZones.Target(930, 1000).DecayTime, 1e-6, "platform");
        }

        [Test]
        public void WavWriterRoundTrips()
        {
            string path = Path.Combine(Path.GetTempPath(), "ring_audio_rt.wav");
            var x = new float[2000];
            for (int i = 0; i < 1000; i++) { x[2 * i] = (float)Math.Sin(i * 0.01); x[2 * i + 1] = -0.5f; }
            DspWavWriter.WriteFile(path, x, 1000, 2, Fs, 16);
            ReadWav(path, out int ch, out int rate, out int bits, out var data);
            Assert.AreEqual(2, ch); Assert.AreEqual(Fs, rate); Assert.AreEqual(16, bits);
            Assert.AreEqual(2000, data.Length);
            for (int i = 0; i < data.Length; i++) Assert.AreEqual(x[i], data[i], 1.0 / 32767.0 + 1e-6);
            File.Delete(path);
        }

        // =========================================================================================================
        // offline renders (Part 11.9)
        // =========================================================================================================
        [Test, Timeout(1800000)]
        public void OfflineWalkMeetsTheMasterSpec()
        {
            Directory.CreateDirectory(AudioDir);
            var r = AudioOfflineRender.RenderWalk(AudioDir, true, null, Fs, m => Log(m));
            File.WriteAllText(Path.Combine(AudioDir, "report.json"), AudioOfflineRender.ReportJson(r));
            File.WriteAllText(Path.Combine(AudioDir, "excerpts.json"), AudioOfflineRender.ExcerptsJson(r));
            File.WriteAllText(Path.Combine(QaDir, "audio-trims.json"), AudioOfflineRender.TrimsJson(AudioTrims.KnotDb, r));
            Log("[Ring] Audio QA: " + Summary(r));

            var fails = new StringBuilder();
            CheckClean(r, fails, "cinematic walk");
            for (int b = 0; b < 7; b++)
            {
                var q = r.Rms[b];
                string tag = "beat " + (b + 1) + " RMS";
                if (q.Windows < 20) { fails.AppendLine(tag + ": only " + q.Windows + " valid 3 s windows"); continue; }
                // 14.3 Master: every valid sliding 3 s window of the beat within +-1.5 dB of its target (per-beat trims
                // plus the W1 3 s gust loudness compensation, RingAudioEngine.GustComp); the mean check names drifted trims
                if (Math.Abs(q.MeanDev) > 0.5) fails.AppendLine(F("{0}: mean {1:0.00} dB vs target {2:0.00} (dev {3:+0.00;-0.00}, trims drifted)", tag, q.MeanDb, q.Target, q.MeanDev));
                CheckWindows(q, fails, tag);
            }
            if (r.MaxRmsHopStepDb > 1.0) fails.AppendLine(F("transition: the sliding 3 s RMS jumps {0:0.00} dB in 0.1 s at s {1:0.0}", r.MaxRmsHopStepDb, r.MaxRmsHopStepAtS));
            if (r.FirstMsDb > -60.0) fails.AppendLine(F("first 10 ms at {0:0.0} dBFS (must fade in with the picture)", r.FirstMsDb));
            if (r.LastMsDb > -60.0) fails.AppendLine(F("last 10 ms at {0:0.0} dBFS (S5 must fade with the picture)", r.LastMsDb));
            if (r.Ticks != AudioSpec.TunnelRibs) fails.AppendLine("rib ticks " + r.Ticks + " (expected one per rib, " + AudioSpec.TunnelRibs + ")");
            if (r.StepsQueued < 1000 || r.Steps != r.StepsQueued) fails.AppendLine("footsteps: queued " + r.StepsQueued + ", started " + r.Steps);
            if (r.BeaconT0 < 0) fails.AppendLine("the walk never reached s 905");
            else
            {
                int expected = (int)Math.Floor((r.Seconds - r.BeaconT0 - RingAudioEngine.BeaconEaseIn) / AudioSpec.BeaconPeriod) + 1;
                if (Math.Abs(r.Pings - expected) > 1) fails.AppendLine("beacon pings " + r.Pings + ", expected about " + expected);
            }
            if (r.LimiterMinGain < 0.5) fails.AppendLine(F("the limiter is compressing ({0:0.000} min gain): fix the mix, not the limiter", r.LimiterMinGain));

            // the excerpts were written and are readable
            Assert.GreaterOrEqual(r.Excerpts.Count, 9, "beat01..07, s2_totality, s3_tunnel");
            foreach (var e in r.Excerpts)
            {
                string path = Path.Combine(AudioDir, e.Name);
                Assert.IsTrue(File.Exists(path), path);
                ReadWav(path, out int ch, out int rate, out _, out var data);
                Assert.AreEqual(2, ch); Assert.AreEqual(Fs, rate);
                Assert.AreEqual(e.Frames * 2, data.Length, e.Name);
                if (e.Name.StartsWith("beat", StringComparison.Ordinal)) Assert.GreaterOrEqual(e.Frames, (long)(20 * Fs), e.Name + " shorter than 20 s");
                else Assert.GreaterOrEqual(e.Frames, (long)(10 * Fs), e.Name + " shorter than 10 s");
            }
            ReadWav(Path.Combine(AudioDir, "walk.wav"), out _, out _, out _, out var walk);
            Assert.AreEqual(192.0, walk.Length / 2.0 / Fs, 2.0, "walk.wav: 1 s of every 5 m");
            Assert.IsTrue(File.Exists(Path.Combine(QaDir, "audio-trims.json")));

            if (fails.Length > 0) Assert.Fail(fails.ToString());
        }

        [Test, Timeout(1800000)]
        public void ManualAndBriskWalksAreClean()
        {
            var fails = new StringBuilder();
            foreach (double speed in new[] { AudioSpec.Walk, AudioSpec.Brisk })
            {
                var r = AudioOfflineRender.RenderWalk(null, false, null, Fs, m => Log(m), speed);
                string tag = F("{0:0.0} m/s walk", speed);
                Log("[Ring] Audio QA " + tag + ": " + Summary(r));
                CheckClean(r, fails, tag);
                for (int b = 0; b < 7; b++)
                {
                    var q = r.Rms[b];
                    if (q.Windows < 50) continue;
                    if (Math.Abs(q.MeanDev) > AudioSpec.TargetToleranceDb)
                        fails.AppendLine(F("{0}, beat {1}: mean RMS {2:+0.00;-0.00} dB off target", tag, b + 1, q.MeanDev));
                    // the W1 gust compensation is a function of game time, so the walks without holds (other gust phase
                    // at every s) meet the same +-1.5 dB window rule with the trims fitted on the cinematic walk
                    CheckWindows(q, fails, F("{0}, beat {1} RMS", tag, b + 1));
                }
                if (r.Ticks != AudioSpec.TunnelRibs) fails.AppendLine(tag + ": rib ticks " + r.Ticks);
                if (r.Pings < 3) fails.AppendLine(tag + ": beacon pings " + r.Pings);
                if (r.FirstMsDb > -60.0 || r.LastMsDb > -60.0) fails.AppendLine(F("{0}: first / last 10 ms {1:0.0} / {2:0.0} dBFS", tag, r.FirstMsDb, r.LastMsDb));
            }
            if (fails.Length > 0) Assert.Fail(fails.ToString());
        }

        /// <summary>Re-fits the per-beat trims (run by hand after a mix change; paste the printed knots into AudioTrims).</summary>
        [Test, Explicit("re-fits the trims; paste the result into AudioTrims.KnotDb"), Timeout(3600000)]
        public void FitTrims()
        {
            var sb = new StringBuilder();
            var knots = AudioOfflineRender.FitTrims(5, true, m => { Log("[Ring] " + m); sb.AppendLine(m); });
            Directory.CreateDirectory(AudioDir);
            File.WriteAllText(Path.Combine(AudioDir, "trims-fit.json"), AudioOfflineRender.TrimsJson(knots));
            var k = new StringBuilder();
            for (int i = 0; i < knots.Length; i++) k.Append(knots[i].ToString("0.00", Ci)).Append(i < knots.Length - 1 ? ", " : "");
            Log("[Ring] Audio trims fitted: " + k);
        }

        // ---------------- helpers ----------------
        /// <summary>14.3 Master tolerance: no valid 3 s window more than +-1.5 dB from its target.</summary>
        static void CheckWindows(AudioAnalyzer.BeatRms q, StringBuilder fails, string tag)
        {
            if (q.MaxAbsDev > AudioSpec.TargetToleranceDb)
                fails.AppendLine(F("{0}: {1:0.0} % of {2} windows within +-1.5 dB (worst {3:0.00} dB, 5-95 % band {4:+0.00;-0.00} .. {5:+0.00;-0.00})",
                    tag, 100 * q.Within, q.Windows, q.MaxAbsDev, q.P05, q.P95));
        }

        static void CheckClean(AudioRenderResult r, StringBuilder fails, string tag)
        {
            for (int b = 0; b < 7; b++)
            {
                string bt = tag + ", beat " + (b + 1) + ": ";
                if (r.TruePeakDb[b] > AudioSpec.PeakCeilingDbfs) fails.AppendLine(F("{0}true peak {1:0.00} dBFS > -1", bt, r.TruePeakDb[b]));
                if (Math.Abs(r.DcL[b]) >= AudioSpec.MaxDc || Math.Abs(r.DcR[b]) >= AudioSpec.MaxDc) fails.AppendLine(F("{0}DC {1:0.00000} / {2:0.00000}", bt, r.DcL[b], r.DcR[b]));
                if (r.MaxStep[b] > AudioSpec.MaxStep) fails.AppendLine(F("{0}sample step {1:0.000} > 0.25", bt, r.MaxStep[b]));
                if (r.NaNs[b] > 0) fails.AppendLine(bt + r.NaNs[b] + " NaN / Inf samples");
                if (r.Clicks[b] > 0) fails.AppendLine(bt + r.Clicks[b] + " clicks");
            }
        }

        static string Summary(AudioRenderResult r)
        {
            var sb = new StringBuilder();
            sb.Append(F("{0:0.0} s in {1:0} ms;", r.Seconds, r.RenderMs));
            for (int b = 0; b < 7; b++)
            {
                var q = r.Rms[b];
                sb.Append(F(" B{0} tp {1:0.0} rms {2:0.0}/{3:0.0} ({4:0}% in tol);", b + 1, r.TruePeakDb[b], q.MeanDb, q.Target, 100 * q.Within));
            }
            sb.Append(F(" pings {0}, ticks {1}, steps {2}", r.Pings, r.Ticks, r.Steps));
            return sb.ToString();
        }

        static string F(string fmt, params object[] a) => string.Format(Ci, fmt, a);

        /// <summary>Minimal RIFF/WAVE PCM reader (16 / 24 bit) for the round-trip checks.</summary>
        static void ReadWav(string path, out int channels, out int rate, out int bits, out float[] data)
        {
            var b = File.ReadAllBytes(path);
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(b, 0, 4));
            Assert.AreEqual("WAVE", Encoding.ASCII.GetString(b, 8, 4));
            channels = 0; rate = 0; bits = 0; data = null;
            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    Assert.AreEqual(1, BitConverter.ToInt16(b, body), "PCM");
                    channels = BitConverter.ToInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToInt16(b, body + 14);
                }
                else if (id == "data")
                {
                    int bps = bits / 8, n = size / bps;
                    data = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        int o = body + i * bps;
                        int v = bps == 2 ? BitConverter.ToInt16(b, o) : ((b[o] | (b[o + 1] << 8) | (b[o + 2] << 16)) << 8) >> 8;
                        data[i] = bps == 2 ? v / 32767f : v / 8388607f;
                    }
                    return;
                }
                pos = body + size + (size & 1);
            }
            Assert.Fail("no data chunk in " + path);
        }
    }
}
