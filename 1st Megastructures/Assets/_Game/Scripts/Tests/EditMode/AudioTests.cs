using System;
using System.IO;
using Megastructure.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Audio verification without an audio device (MASTER_PROMPT 11.9, Part 10 item 25, 14.10 Functional): per-beat
    /// renders of the nominal walk through the Unity-free ProceduralAudioEngine, checked for true peak, clicks, DC,
    /// the 14.3 RMS targets with the per-beat trims, the deliberate ending silence, determinism and zero allocation.
    /// Renders are also written to QA/audio/editmode/*.wav for Tools/audio_analyze.py.
    /// </summary>
    public sealed class AudioTests
    {
        const uint Seed = 1u;
        const float BeatSeconds = 20f;
        const int Fs = AbyssScore.SampleRate;

        static string QaDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "QA", "audio", "editmode"));

        static void TryWriteWav(string name, float[] buf)
        {
            try { WavWriter.WriteFloat32(Path.Combine(QaDir, name), buf, 2, Fs); }
            catch (Exception ex) { Debug.Log("[ABYSS] audio test: could not write " + name + ": " + ex.Message); }
        }

        // ------------------------------------------------------------------ per-beat renders (14.10 Functional)

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void BeatRender_MeetsPeakClickDcAndRmsTargets(int zone)
        {
            float[] buf = AbyssScore.RenderBeat(zone, BeatSeconds, Seed);
            TryWriteWav("zone" + zone + ".wav", buf);
            AudioAnalysis.Report r = AudioAnalysis.Analyze(buf, Fs);
            string name = AudioMix.ZoneNames[zone - 1];

            Assert.Less(r.TruePeakDb, -1.0, name + ": true peak must stay below -1 dBFS");
            Assert.LessOrEqual(r.PeakDb, -3.0 + 1e-3, name + ": sample peak above the -3 dBFS ceiling");
            Assert.LessOrEqual(r.MaxStep, 0.1, name + ": sample-to-sample step (click) above 0.1");
            Assert.Less(Math.Abs(r.DcL), 1e-3, name + ": DC offset L");
            Assert.Less(Math.Abs(r.DcR), 1e-3, name + ": DC offset R");

            double target = AudioMix.TargetRmsDb[zone - 1];
            double[] w = AudioAnalysis.WindowRmsDb(buf, Fs, AudioMix.RmsWindowSeconds);
            Assert.Greater(w.Length, 40, "too few RMS windows");
            for (int i = 0; i < w.Length; i++)
                Assert.LessOrEqual(Math.Abs(w[i] - target), AudioMix.RmsToleranceDb,
                    name + ": 0.4 s window " + i + " at " + w[i].ToString("F2") + " dBFS, target " + target + " +/- 3 dB");
            Assert.LessOrEqual(Math.Abs(AudioMix.MasterTrimDb[zone - 1]), AudioMix.MaxTrimDb, name + ": |T_b| above 6 dB");
        }

        [Test]
        public void MasterTrims_AreWithinSixDbAndMatchTheLayerSum()
        {
            for (int z = 0; z < AudioMix.ZoneCount; z++)
            {
                Assert.LessOrEqual(Math.Abs(AudioMix.MasterTrimDb[z]), AudioMix.MaxTrimDb, AudioMix.ZoneNames[z]);
                // the analytic layer sum of the 14.3 table is within 3 dB of the target (the table is self-consistent)
                Assert.LessOrEqual(Math.Abs(AudioMix.LayerSumDb(z) - AudioMix.TargetRmsDb[z]), 3.0, AudioMix.ZoneNames[z]);
            }
        }

        // ------------------------------------------------------------------ title and ending (14.11, 14.3 d)

        [Test]
        public void Title_IsSilentUntilTheDoorThudAtPointEightSeconds()
        {
            float[] buf = AbyssScore.RenderSpan(0.0, 4.0, Seed, Fs, false, out ProceduralAudioEngine eng);
            TryWriteWav("title.wav", buf);
            double before = AudioAnalysis.PeakAbs(buf, 0, (int)(0.79 * Fs));
            Assert.Less(before, 1e-9, "the screen is black and the audio silent (below -180 dBFS) before 0.8 s");
            double thudPeakDb = DspMath.LinToDb(AudioAnalysis.PeakAbs(buf, (int)(0.8 * Fs), (int)(1.6 * Fs)));
            Assert.AreEqual(AudioMix.ThudPeakDb, thudPeakDb, 1.5, "door thud peak -24 dBFS");
            Assert.AreEqual(1, eng.ThudsFired);
        }

        [Test]
        public void Ending_FadesToAbsoluteSilenceThenPlaysTheSubTone()
        {
            float[] buf = AbyssScore.RenderBeat(AbyssScore.ZoneEnding, 20f, Seed); // starts 2 s before GoalReached
            TryWriteWav("ending.wav", buf);
            AudioAnalysis.Report r = AudioAnalysis.Analyze(buf, Fs);
            Assert.Less(r.TruePeakDb, -1.0);
            Assert.LessOrEqual(r.MaxStep, 0.1);
            const double g = 2.0;
            int s0 = (int)((g + AudioMix.EndingFadeDelay + AudioMix.EndingFade + 0.5) * Fs);
            int s1 = (int)((g + AudioMix.EndToneStart - 0.05) * Fs);
            Assert.Less(AudioAnalysis.PeakAbs(buf, s0, s1), 1e-5, "absolute silence between the fade-out and the tone");
            double top = AudioAnalysis.RmsDb(buf, (int)((g + AudioMix.EndToneStart + AudioMix.EndToneIn - 0.5) * Fs),
                                             (int)((g + AudioMix.EndToneStart + AudioMix.EndToneIn + 0.5) * Fs));
            Assert.AreEqual(AudioMix.EndToneDb, top, 1.5, "31 Hz ending tone at -34 dBFS");
            double after = AudioAnalysis.PeakAbs(buf, (int)((g + AudioMix.EndToneStart + AudioMix.EndToneIn + AudioMix.EndToneOut + 0.5) * Fs), buf.Length / 2);
            Assert.Less(after, 1e-4, "silent after the tone");
        }

        // ------------------------------------------------------------------ determinism and allocation

        [Test]
        public void Render_IsDeterministicForASeed()
        {
            float[] a = AbyssScore.RenderBeat(4, 6f, 7u);
            float[] b = AbyssScore.RenderBeat(4, 6f, 7u);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) Assert.Fail("sample " + i + " differs between two renders with the same seed");
            float[] c = AbyssScore.RenderBeat(4, 6f, 8u);
            bool differs = false;
            for (int i = 0; i < a.Length && !differs; i++) differs = a[i] != c[i];
            Assert.IsTrue(differs, "a different seed must give different noise");
        }

        [Test]
        public void SteadyStateRender_AllocatesNothing()
        {
            var eng = new ProceduralAudioEngine(Fs, Seed);
            var p = new AudioParams();
            var input = new ScoreInput();
            var buf = new float[1024 * 2];
            double t = AbyssScore.ZoneWindowStart(4);
            for (int i = 0; i < 64; i++)
            {
                AbyssScore.Nominal(t, ref input);
                AbyssScore.Evaluate(in input, ref p);
                eng.Exchange.Publish(in p);
                eng.RenderInterleaved(buf, 2);
                if (i % 8 == 0) eng.TriggerFootstep(SurfaceKind.Grating);
                t += 1024.0 / Fs;
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 512; i++)
            {
                AbyssScore.Nominal(t, ref input);
                AbyssScore.Evaluate(in input, ref p);
                eng.Exchange.Publish(in p);
                eng.RenderInterleaved(buf, 2);
                if (i % 8 == 0) eng.TriggerFootstep((SurfaceKind)(i / 8 % 5));
                t += 1024.0 / Fs;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "the steady-state render loop must not allocate");
        }

        // ------------------------------------------------------------------ building blocks

        [Test]
        public void Limiter_HasMinusThreeDbCeiling()
        {
            Assert.AreEqual(0.708, SoftLimiter.Process(1.0), 1e-9, "full scale maps to -3 dBFS");
            // absolute bound 0.708 / tanh(1.2) = 0.849 (-1.42 dBFS): no input can reach the -1 dBFS true-peak limit
            Assert.Less(SoftLimiter.Process(100.0), 0.8493);
            Assert.Less(DspMath.LinToDb(SoftLimiter.Process(1e6)), -1.0);
            Assert.AreEqual(-SoftLimiter.Process(0.3), SoftLimiter.Process(-0.3), 1e-12, "odd symmetric (no DC)");
            double smallGainDb = DspMath.LinToDb(SoftLimiter.Process(1e-4) / 1e-4);
            Assert.AreEqual(0.16, smallGainDb, 0.02, "small-signal gain 0.708 * 1.2 / tanh(1.2)");
        }

        [Test]
        public void Fdn_DelaysAndLineGainsFollowTheSpec()
        {
            var fdn = new FdnReverb(48000);
            fdn.SetParams(7.5, 2500, 0.12);
            for (int i = 0; i < FdnReverb.Lines; i++)
            {
                Assert.AreEqual(FdnReverb.BaseDelays48k[i], fdn.DelayLength(i));
                Assert.AreEqual(Math.Pow(10.0, -3.0 * FdnReverb.BaseDelays48k[i] / (48000 * 7.5)), fdn.LineGain(i), 1e-12);
            }
            var fdn44 = new FdnReverb(44100);
            Assert.AreEqual((int)Math.Round(2503 * 44100.0 / 48000.0), fdn44.DelayLength(7), "delays scale with the device rate");

            // impulse decay: energy 1 s after the impulse is about 60 / RT60 dB down
            fdn.Clear();
            fdn.SetParams(2.0, 24000, 0.0, true);   // damping off: the loop gains alone set the decay
            double e0 = 0, e1 = 0;
            for (int n = 0; n < 3 * 48000; n++)
            {
                fdn.Process(n == 0 ? 1.0 : 0.0, 0.0, out double l, out double r);
                double e = l * l + r * r;
                if (n >= 4800 && n < 9600) e0 += e;          // 0.1 - 0.2 s
                if (n >= 52800 && n < 57600) e1 += e;        // 1.1 - 1.2 s
            }
            double decayDb = 10.0 * Math.Log10(e0 / e1);
            Assert.AreEqual(30.0, decayDb, 4.0, "a 2 s RT60 decays 30 dB per second");
        }

        /// <summary>
        /// Band-limited RT60 of the FDN impulse response: stereo impulse, two cascaded TPT band-passes (Q 4.32, about a third
        /// of an octave each, so the slow low-frequency tail cannot leak into a fast-decaying band), Schroeder backward
        /// integration, least-squares slope of the energy decay curve from -5 to -35 dB (T30), extrapolated to 60 dB.
        /// </summary>
        public static double MeasureBandRt60(ReverbPreset preset, double hz, int fs)
        {
            var fdn = new FdnReverb(fs);
            fdn.SetParams(preset.Rt60, preset.Damping, 0.0, true);
            int n = (int)(1.2 * preset.Rt60 * fs);
            var aL = new Svf(hz, 4.32, fs); var bL = new Svf(hz, 4.32, fs);
            var aR = new Svf(hz, 4.32, fs); var bR = new Svf(hz, 4.32, fs);
            var edc = new double[n];
            for (int i = 0; i < n; i++)
            {
                double x = i == 0 ? 1.0 : 0.0;
                fdn.Process(x, x, out double l, out double r);
                double yl = bL.Process(aL.Process(l, SvfMode.BandPassNormalized), SvfMode.BandPassNormalized);
                double yr = bR.Process(aR.Process(r, SvfMode.BandPassNormalized), SvfMode.BandPassNormalized);
                edc[i] = yl * yl + yr * yr;
            }
            for (int i = n - 2; i >= 0; i--) edc[i] += edc[i + 1];
            double e0 = edc[0], sx = 0.0, sy = 0.0, sxx = 0.0, sxy = 0.0;
            int count = 0, hop = fs / 1000;
            for (int i = 0; i < n; i += hop)
            {
                double db = 10.0 * Math.Log10(edc[i] / e0);
                if (db > -5.0) continue;
                if (db < -35.0) break;
                double t = (double)i / fs;
                sx += t; sy += db; sxx += t * t; sxy += t * db; count++;
            }
            Assert.Greater(count, 100, "the decay curve must span -5 .. -35 dB");
            double slope = (count * sxy - sx * sy) / (count * sxx - sx * sx);   // dB per second (negative)
            return -60.0 / slope;
        }

        /// <summary>
        /// The bridge preset (RT60 7.5 s, damping 2.5 kHz) keeps its RT60 in the mid band (per-line Jot damping, 14.3): within
        /// +/-20 % of 7.5 s at 500 Hz and 1 kHz, and RT60 / 2 at the damping frequency (in-loop HF damping still works).
        /// The former fixed one-pole per pass measured 5.1 s at 500 Hz and below 3.6 s at 1 kHz.
        /// </summary>
        [TestCase(500.0, 1.0)]
        [TestCase(1000.0, 1.0)]
        [TestCase(2500.0, FdnReverb.DampingRt60Ratio)]
        public void Fdn_BridgeBandRt60FollowsTheJotDesign(double hz, double ratio)
        {
            ReverbPreset bridge = AudioMix.Reverb[1];
            Assert.AreEqual(7.5f, bridge.Rt60);
            Assert.AreEqual(2500f, bridge.Damping);
            double expected = ratio * bridge.Rt60;
            double rt = MeasureBandRt60(bridge, hz, Fs);
            Assert.AreEqual(expected, rt, 0.2 * expected, "bridge RT60 at " + hz + " Hz measured " + rt.ToString("F2") + " s");
            double rt44 = MeasureBandRt60(bridge, hz, 44100);
            Assert.AreEqual(rt, rt44, 0.05 * expected, "the damping design follows the device rate");
        }

        [Test]
        public void Fdn_JotPoleMeetsTheGainAtTheDampingFrequency()
        {
            // research check (docs/research/audio.md): L 2503, RT60 7.5 s, f_d 2.5 kHz gives p = 0.3732
            var fdn = new FdnReverb(48000);
            fdn.SetParams(7.5, 2500, 0.0, true);
            Assert.AreEqual(0.3732, fdn.DampingPole(7), 5e-4);
            double c = Math.Cos(2.0 * Math.PI * 2500.0 / 48000.0);
            for (int i = 0; i < FdnReverb.Lines; i++)
            {
                double g0 = fdn.LineGain(i), p = fdn.DampingPole(i);
                double gd = Math.Pow(10.0, -3.0 * fdn.DelayLength(i) / (48000 * 7.5 * FdnReverb.DampingRt60Ratio));
                double mag = (1.0 - p) / Math.Sqrt(1.0 - 2.0 * p * c + p * p);
                Assert.AreEqual(gd, g0 * mag, 1e-9, "line " + i + ": loop gain at f_d is G_d (RT60 / 2)");
            }
            fdn.SetParams(7.5, 24000, 0.0, true);
            for (int i = 0; i < FdnReverb.Lines; i++) Assert.AreEqual(0.0, fdn.DampingPole(i), "a corner at 0.45 fs or above disables the damping");
        }

        [Test]
        public void ReverbBlend_TakesFourSecondsAndRespectsTheWetRate()
        {
            var eng = new ProceduralAudioEngine(Fs, Seed);
            var input = new ScoreInput();
            var p = new AudioParams();
            var buf = new float[ProceduralAudioEngine.ControlBlock * 2];
            AbyssScore.Nominal(20.0, ref input);                 // gallery preset
            AbyssScore.Evaluate(in input, ref p);
            eng.Exchange.Publish(in p);
            eng.RenderInterleaved(buf, 2);
            Assert.AreEqual(AudioMix.Reverb[0].Rt60, eng.ReverbRt60, 1e-4);
            ReverbPreset to = AudioMix.Reverb[7];                // outside: RT60 11 s, wet -20 dB
            p.RevRt60 = to.Rt60; p.RevPreDelay = to.PreDelay; p.RevDamping = to.Damping; p.RevWetDb = to.WetDb; p.RevBlendSeconds = to.BlendSeconds;
            eng.Exchange.Publish(in p);
            double prevWet = eng.ReverbWetDb, maxRate = 0.0, dt = (double)ProceduralAudioEngine.ControlBlock / Fs;
            double rtMid = 0.0, rtLate = 0.0, rtEnd = 0.0;
            int blocks = (int)(6.0 / dt);
            for (int i = 0; i < blocks; i++)
            {
                eng.RenderInterleaved(buf, 2);
                double w = eng.ReverbWetDb;
                maxRate = Math.Max(maxRate, Math.Abs(w - prevWet) / dt);
                prevWet = w;
                double t = (i + 1) * dt;
                if (rtMid == 0.0 && t >= 2.0) rtMid = eng.ReverbRt60;
                if (rtLate == 0.0 && t >= 3.8) rtLate = eng.ReverbRt60;
                if (rtEnd == 0.0 && t >= 4.01) rtEnd = eng.ReverbRt60;
            }
            double from = AudioMix.Reverb[0].Rt60;
            Assert.AreEqual(Math.Sqrt(from * to.Rt60), rtMid, 0.05, "half way through the 4 s blend (RT60 blends in log domain)");
            Assert.Less(rtLate, to.Rt60 - 1e-4, "still blending at 3.8 s");
            Assert.AreEqual(to.Rt60, rtEnd, 1e-4, "the reverb blend completes after 4 s");
            Assert.LessOrEqual(maxRate, AudioMix.WetRateDbPerSecond + 1e-6, "wet changes limited to 6 dB/s");
        }

        [Test]
        public void HallWindow_FiresOneMachinePulsePerDeadCentre()
        {
            double t0 = AbyssConst.BEAT_START_T[3] + 30.0;
            AbyssScore.RenderSpan(t0, 60.0, Seed, Fs, false, out ProceduralAudioEngine eng);
            Assert.AreEqual(4, eng.PulsesFired, "one dead centre every 15 s");
        }

        [Test]
        public void Ride_FiresARailTickEveryTwelveMetres()
        {
            AbyssScore.RenderSpan(AbyssConst.T_RIDE_START - 1.0, AbyssConst.LIFT_DURATION + 2.0, Seed, Fs, false, out ProceduralAudioEngine eng);
            Assert.AreEqual((int)(AbyssConst.LIFT_DROP / AudioMix.LiftTickSpacing), eng.TicksFired, "480 m / 12 m");
        }

        // ------------------------------------------------------------------ event robustness (extrapolation jitter, load, restart)

        static void FeedNominal(ProceduralAudioEngine eng, ref ScoreInput s, ref AudioParams p, double t, int frames, float[] buf)
        {
            AbyssScore.Nominal(t, ref s);
            AbyssScore.Evaluate(in s, ref p);
            p.Version++;
            eng.Exchange.Publish(in p);
            if (frames > 0) eng.RenderInterleavedStereo(buf, 0, frames);
        }

        /// <summary>First time after t0 at which floor(f(t)) increments (bisection to 1 microsecond).</summary>
        static double NextBoundary(Func<double, double> f, double t0)
        {
            double k = Math.Floor(f(t0));
            double hi = t0;
            while (Math.Floor(f(hi)) == k) hi += 0.05;
            double lo = hi - 0.05;
            while (hi - lo > 1e-6)
            {
                double m = 0.5 * (lo + hi);
                if (Math.Floor(f(m)) == k) lo = m; else hi = m;
            }
            return hi;
        }

        /// <summary>
        /// Feed the engine at 60 Hz up to just before a boundary, then let the audio thread extrapolate 10 ms past it and land
        /// the next (late) snapshot 4 ms behind it, as happens when the audio thread renders a burst before the frame arrives.
        /// The extrapolated clock crosses the boundary twice; the event must fire once.
        /// </summary>
        static void CrossTwice(ProceduralAudioEngine eng, double tb, float[] buf)
        {
            var s = new ScoreInput();
            var p = new AudioParams();
            const int frame = Fs / 60;
            for (double t = tb - 1.0; t < tb - 0.03; t += 1.0 / 60) FeedNominal(eng, ref s, ref p, t, frame, buf);
            FeedNominal(eng, ref s, ref p, tb - 0.010, (int)(0.020 * Fs), buf);   // extrapolates to tb + 10 ms
            FeedNominal(eng, ref s, ref p, tb - 0.004, (int)(0.020 * Fs), buf);   // late snapshot behind the boundary
            for (double t = tb + 0.016; t < tb + 1.0; t += 1.0 / 60) FeedNominal(eng, ref s, ref p, t, frame, buf);
        }

        [Test]
        public void MachinePulse_FiresOncePerDeadCentreDespiteExtrapolationJitter()
        {
            double tb = NextBoundary(t => AbyssConst.CrankThetaIL(t) / ProceduralAudioEngine.PulseStepDegrees, AbyssConst.BEAT_START_T[3] + 30.0);
            var eng = new ProceduralAudioEngine(Fs, Seed);
            var buf = new float[Fs * 2];
            CrossTwice(eng, tb, buf);
            Assert.AreEqual(1, eng.PulsesFired, "one pulse for one dead centre even when the clock steps back across it");

            // a real seek back (2.5 dead centres, 37.5 s) resynchronises silently; the next real crossing fires again
            var s = new ScoreInput();
            var p = new AudioParams();
            double ts = tb - 37.5;
            for (double t = ts; t < ts + 0.5; t += 1.0 / 60) FeedNominal(eng, ref s, ref p, t, Fs / 60, buf);
            Assert.AreEqual(1, eng.PulsesFired, "no pulse on a seek");
            for (double t = ts + 0.5; t < tb - 29.5; t += 1.0 / 60) FeedNominal(eng, ref s, ref p, t, Fs / 60, buf);
            Assert.AreEqual(2, eng.PulsesFired, "the dead centre after the seek (tb - 30 s) fires");
        }

        [Test]
        public void RailTick_FiresOncePerTwelveMetresDespiteExtrapolationJitter()
        {
            double r0 = AbyssConst.LIFT_DURATION * 0.5;
            double rb = NextBoundary(r => AbyssConst.LiftS(r) / AudioMix.LiftTickSpacing + 1e-6, r0);
            var eng = new ProceduralAudioEngine(Fs, Seed);
            var buf = new float[Fs * 2];
            CrossTwice(eng, AbyssConst.T_RIDE_START + rb, buf);
            Assert.AreEqual(1, eng.TicksFired, "one rail tick for one 12 m mark even when the travel steps back across it");
        }

        static void FeedTitle(ProceduralAudioEngine eng, ref ScoreInput s, ref AudioParams p, double realT, bool walk, int frames, float[] buf)
        {
            AbyssScore.Nominal(walk ? realT : 0.0, ref s);
            s.RealTime = realT;
            s.WalkStarted = walk;
            if (!walk) { s.PictureFade = 0f; s.ClockRunning = false; }
            AbyssScore.Evaluate(in s, ref p);
            p.Version++;
            eng.Exchange.Publish(in p);
            if (frames > 0) eng.RenderInterleavedStereo(buf, 0, frames);
        }

        /// <summary>
        /// Feed a walk start (RealTime restarted at 0; the first snapshot arrives at <paramref name="realT0"/>) with late
        /// snapshots: the audio thread alternately renders 1.5 and 0.5 frames per 60 Hz snapshot, so its extrapolated real time
        /// runs 8 ms ahead and then steps back. Rendered in control blocks; returns the walk real time of the first thud trigger
        /// (block resolution, 0.67 ms) and of the first output sample above 1e-6 (-1: none).
        /// </summary>
        static double FeedWalk(ProceduralAudioEngine eng, double realT0, double seconds, float[] buf, out double onset)
        {
            var s = new ScoreInput();
            var p = new AudioParams();
            const int block = ProceduralAudioEngine.ControlBlock;
            double realT = realT0, thudAt = -1.0;
            onset = -1.0;
            int k = 0, fired = eng.ThudsFired;
            long rendered = 0;
            while (realT < seconds)
            {
                int frames = (k++ & 1) == 0 ? 1200 : 400;
                FeedTitle(eng, ref s, ref p, realT, true, 0, buf);
                for (int done = 0; done < frames; done += block)
                {
                    int n = Math.Min(block, frames - done);
                    eng.RenderInterleavedStereo(buf, 0, n);
                    if (thudAt < 0.0 && eng.ThudsFired != fired) thudAt = realT0 + (double)rendered / Fs;
                    if (onset < 0.0)
                        for (int i = 0; i < n; i++)
                            if (Math.Abs(buf[2 * i]) > 1e-6f || Math.Abs(buf[2 * i + 1]) > 1e-6f) { onset = realT0 + (double)(rendered + i) / Fs; break; }
                    rendered += n;
                }
                realT += 1.0 / 60;
            }
            return thudAt;
        }

        [Test]
        public void DoorThud_WaitsForTheWalkAndFiresOncePerRunAtPointEightSeconds()
        {
            var eng = new ProceduralAudioEngine(Fs, Seed);
            var buf = new float[Fs * 2];
            var s = new ScoreInput();
            var p = new AudioParams();
            // the black load: SceneClock.RealTime runs from 0 while ClockDriver holds the walk clock
            for (int k = 0; k < 6 * 60; k++) FeedTitle(eng, ref s, ref p, k / 60.0, false, Fs / 60, buf);
            Assert.AreEqual(0, eng.ThudsFired, "no thud while loading, although the load's RealTime passes 0.8 s");
            Assert.Less(eng.PeakAbs, 1e-9, "silent while loading");

            // ClockDriver.Begin: RealTime restarts at 0 and the walk runs; the first frame lands 13.3 ms in, which puts 0.8 s
            // inside the 8 ms overshoot of a late snapshot (the extrapolated real time crosses 0.8 s, steps back, crosses again)
            double realT0 = AudioMix.TitleBlack - 0.02 - 46.0 / 60.0;
            double thudAt = FeedWalk(eng, realT0, 4.0, buf, out double onset);
            Assert.AreEqual(1, eng.ThudsFired, "exactly one thud per run, also with late snapshots");
            Assert.AreEqual(AudioMix.TitleBlack, thudAt, 0.002, "the thud fires at walk real time 0.8 s");
            Assert.AreEqual(AudioMix.TitleBlack, onset, 0.002, "silent until the thud: the first sound is the thud at 0.8 s");

            // a second Begin without a scene reload (RealTime back to 0): the thud is re-armed and fires once more at 0.8 s
            double thudAt2 = FeedWalk(eng, realT0, 3.0, buf, out _);
            Assert.AreEqual(2, eng.ThudsFired, "re-armed by the RealTime reset");
            Assert.AreEqual(AudioMix.TitleBlack, thudAt2, 0.002, "second run: thud at walk real time 0.8 s");
        }

        /// <summary>
        /// 14.3 "fade-in with the picture" and the pause duck: the master gain tracks GameState.PictureFade (title fade 0.8-3.3 s)
        /// through the 50 ms smoother, and GameState.Paused ducks the whole mix by 15 dB with a 0.25 s one-pole, without steps.
        /// </summary>
        [Test]
        public void Master_FollowsThePictureFadeAndDucksSmoothlyOnPause()
        {
            var eng = new ProceduralAudioEngine(Fs, Seed);
            var s = new ScoreInput();
            var p = new AudioParams();
            const int block = ProceduralAudioEngine.ControlBlock;
            var buf = new float[block * 2];
            double maxMasterStep = 0.0;
            double prevMaster = 0.0;
            for (long n = 0; n < (long)(4.0 * Fs); n += block)
            {
                double t = (double)n / Fs;
                if (n % (Fs / 60) < block) FeedNominal(eng, ref s, ref p, t, 0, buf);
                eng.RenderInterleavedStereo(buf, 0, block);
                double m = eng.MasterGain;
                maxMasterStep = Math.Max(maxMasterStep, Math.Abs(m - prevMaster));
                prevMaster = m;
                if (Math.Abs(t - 0.75) < 0.5 * block / (double)Fs) Assert.AreEqual(0.0, m, 1e-12, "silent before the picture fades in");
                if (Math.Abs(t - 2.05) < 0.5 * block / (double)Fs) Assert.AreEqual(0.5, m, 0.05, "half way through the 2.5 s fade-in");
            }
            Assert.Greater(eng.MasterGain, 0.999, "picture fully in at 3.3 s, master at 1 by 4 s");
            Assert.Less(maxMasterStep, 0.002, "the master fade moves in small control-rate steps (ramped per sample)");

            // pause: duck -15 dB, smoothly; resume: back to 1
            double t0 = AbyssScore.ZoneWindowStart(4);
            var big = new float[Fs * 2];
            FeedNominal(eng, ref s, ref p, t0 - 5.0, 0, big);
            for (double t = t0 - 5.0; t < t0; t += 1.0 / 60) FeedNominal(eng, ref s, ref p, t, Fs / 60, big);
            double prevDuck = eng.DuckGain, maxDuckStep = 0.0;
            Assert.AreEqual(1.0, prevDuck, 1e-9);
            AbyssScore.Nominal(t0, ref s);
            s.Paused = true;
            s.ClockRunning = false;              // ClockDriver.FreezeOnPause
            AbyssScore.Evaluate(in s, ref p);
            for (int i = 0; i < 2 * Fs / block; i++)
            {
                if (i % 25 == 0) eng.Exchange.Publish(in p);
                eng.RenderInterleavedStereo(buf, 0, block);
                double d = eng.DuckGain;
                Assert.LessOrEqual(d, prevDuck + 1e-12, "the duck falls monotonically");
                maxDuckStep = Math.Max(maxDuckStep, prevDuck - d);
                prevDuck = d;
            }
            Assert.AreEqual(AudioMix.PauseDuckDb, DspMath.LinToDb(eng.DuckGain), 0.05, "-15 dB after 2 s of pause");
            Assert.Less(maxDuckStep, 0.005, "no step: about 0.002 of linear gain per 32-sample block (tau 0.25 s), ramped per sample");
            s.Paused = false;
            s.ClockRunning = true;
            AbyssScore.Evaluate(in s, ref p);
            for (int i = 0; i < 2 * Fs / block; i++)
            {
                if (i % 25 == 0) eng.Exchange.Publish(in p);
                eng.RenderInterleavedStereo(buf, 0, block);
            }
            Assert.AreEqual(1.0, eng.DuckGain, 1e-3, "resume restores the full level");
        }

        [Test]
        public void Engine_AdaptsToFortyFourOneKilohertz()
        {
            float[] buf = AbyssScore.RenderSpan(AbyssScore.ZoneWindowStart(6), 8.0, Seed, 44100, false, out _);
            AudioAnalysis.Report r = AudioAnalysis.Analyze(buf, 44100);
            Assert.Less(r.TruePeakDb, -1.0);
            Assert.AreEqual(AudioMix.TargetRmsDb[5], r.MedianWindowDb, 1.5, "levels are rate independent");
        }

        [Test]
        public void ParamsExchange_DeliversTheLatestSnapshotOnce()
        {
            var x = new AudioParamsExchange();
            var got = new AudioParams();
            Assert.IsFalse(x.TryAcquire(ref got));
            var p = new AudioParams { Version = 1, Sub = 0.5f };
            x.Publish(in p);
            p.Version = 2; p.Sub = 0.25f;
            x.Publish(in p);
            Assert.IsTrue(x.TryAcquire(ref got));
            Assert.AreEqual(2, got.Version);
            Assert.AreEqual(0.25f, got.Sub);
            Assert.IsFalse(x.TryAcquire(ref got), "no new snapshot");
            p.Version = 3;
            x.Publish(in p);
            Assert.IsTrue(x.TryAcquire(ref got));
            Assert.AreEqual(3, got.Version);
        }

        [Test]
        public void FootstepQueue_RoundTripsInOrder()
        {
            AudioEvents.DrainFootsteps();
            AudioEvents.Footstep(SurfaceKind.Stair);
            AudioEvents.Footstep(SurfaceKind.Grating);
            AudioEvents.Footstep(SurfaceKind.LiftDeck);
            Assert.IsTrue(AudioEvents.TryDequeueFootstep(out SurfaceKind a));
            Assert.IsTrue(AudioEvents.TryDequeueFootstep(out SurfaceKind b));
            Assert.IsTrue(AudioEvents.TryDequeueFootstep(out SurfaceKind c));
            Assert.IsFalse(AudioEvents.TryDequeueFootstep(out _));
            Assert.AreEqual(SurfaceKind.Stair, a);
            Assert.AreEqual(SurfaceKind.Grating, b);
            Assert.AreEqual(SurfaceKind.LiftDeck, c);
        }

        [Test]
        public void Score_ExtendedProgressIsContinuousAcrossTheRide()
        {
            var s = new ScoreInput();
            AbyssScore.Nominal(AbyssConst.T_RIDE_START - 0.01, ref s);
            float before = AbyssScore.ExtendedProgress(s);
            AbyssScore.Nominal(AbyssConst.T_RIDE_START + 0.01, ref s);
            float start = AbyssScore.ExtendedProgress(s);
            AbyssScore.Nominal(AbyssConst.T_GATE_OPEN - 0.01, ref s);
            float gate = AbyssScore.ExtendedProgress(s);
            AbyssScore.Nominal(AbyssConst.T_GATE_OPEN + 0.01, ref s);
            float after = AbyssScore.ExtendedProgress(s);
            Assert.AreEqual(AbyssConst.D_LIFT, before, 0.02f);
            Assert.AreEqual(AbyssConst.D_LIFT, start, 0.02f);
            Assert.AreEqual(after, gate, 0.05f, "E continuous at the gate (d + 121)");
            Assert.AreEqual(AbyssConst.TOTAL_DURATION, AbyssScore.GoalTime + 1.2 / AbyssConst.V_WALK, 0.05, "goal 1.2 m before the door");
        }
    }
}
