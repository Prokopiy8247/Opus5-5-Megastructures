// AudioTests: T-AUDIO (MASTER_PROMPT 14.10.1, 14.10.3 audio row, 14.3.3 master targets, 14.5.7 acceptance 5)
// plus unit checks of the DSP, the parameter schedule (14.3.6 silences and swells), the footstep bank and the
// WAV writer. Everything is rendered offline through the pure ThreadDsp (no audio device, deterministic seed).
// The full walk is written to QA/audio/full_walk.wav (+ .json sidecar, + _report.txt) relative to the
// project root (the editor's working directory).
using System;
using System.IO;
using System.Threading.Tasks;
using Megastructure.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;   // Unity's documented pattern: NUnit's Is plus AllocatingGCMemory

namespace Megastructure.Tests
{
    public class AudioTests
    {
        const int Seed = 1;
        const int Fs = 48000;
        static double Db(double lin) { return 20.0 * Math.Log10(Math.Max(1e-15, lin)); }

        static string QaDir()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "QA", "audio");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ============================================================ T-AUDIO: the whole walk

        [Test, Timeout(900000)]
        public void TAudio_FullWalk_MeetsMasterTargets()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string dir = QaDir();
            string wav = Path.Combine(dir, "full_walk.wav");

            // three independent renders in parallel: the full mix (written to disk), the mix without footsteps (step
            // check, 14.10.3 "no sample step above 0.25 full scale except footsteps") and the wind stem (14.5.7 (5))
            var noSteps = new WalkLevelMeter();
            Task<OfflineRenderInfo> tNoSteps = Task.Run(() =>
                OfflineAudioRenderer.Render(new OfflineRenderSettings { Seed = Seed, SampleRate = Fs, Footsteps = false }, noSteps));
            var wind = new WalkLevelMeter { AfterS = ThreadConstants.WindOffS };
            Task<OfflineRenderInfo> tWind = Task.Run(() =>
                OfflineAudioRenderer.Render(new OfflineRenderSettings
                {
                    Seed = Seed, SampleRate = Fs, S0 = 700.0, S1 = ThreadConstants.PathLength, Footsteps = false,
                    LayerMask = AudioLayer.Bit(AudioLayer.Wind), IntroTime = 100.0, SnapAtStart = true,
                }, wind));

            var full = new WalkLevelMeter();
            var st = new OfflineRenderSettings { Seed = Seed, SampleRate = Fs, Speed = ThreadConstants.WalkSpeed, Footsteps = true };
            OfflineRenderInfo info = OfflineAudioRenderer.RenderToWav(st, wav, WavFormat.Pcm16, full);
            Task.WaitAll(tNoSteps, tWind);
            OfflineRenderInfo infoNo = tNoSteps.Result, infoWind = tWind.Result;

            string report = "T-AUDIO full walk s = 0..860 at 1.5 m/s, 48 kHz, seed " + Seed + ", " + info.Steps + " footsteps, "
                          + info.Seconds.ToString("F1") + " s rendered in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s\n"
                          + "--- full mix\n" + full.Report()
                          + "--- without footsteps\n" + noSteps.Report()
                          + "--- wind stem (s 700..860)\n" + wind.Report();
            File.WriteAllText(Path.Combine(dir, "full_walk_report.txt"), report);
            Debug.Log("[THREAD] " + report);

            Assert.That(File.Exists(wav), "full_walk.wav was not written");
            Assert.That(info.NanResets + infoNo.NanResets + infoWind.NanResets, Is.EqualTo(0), "DSP hit a non-finite block");
            Assert.That(full.NonFinite + noSteps.NonFinite, Is.EqualTo(0), "non-finite samples");
            Assert.That(info.Steps, Is.GreaterThan(1000), "footsteps missing from the render");
            Assert.That(info.TriggerTime, Is.EqualTo((ThreadConstants.BeaconTriggerS) / ThreadConstants.WalkSpeed).Within(0.05), "goal trigger time");

            // per-beat RMS within 3 dB of the 14.3.3 totals (beat 3 outside s 268-302, beat 4 outside s 330-345)
            for (int b = 0; b < ThreadConstants.BeatCount; b++)
            {
                double rms = full.BeatRmsDb(b), target = ThreadConstants.TargetRmsDb[b];
                Assert.That(full.BeatFrames[b], Is.GreaterThan(Fs * 10), "beat " + (b + 1) + " measured too briefly");
                Assert.That(Math.Abs(rms - target), Is.LessThanOrEqualTo(3.0),
                    "beat " + (b + 1) + " RMS " + rms.ToString("F2") + " dBFS vs target " + target + " +-3");
            }
            // sample peak below -1.5 dBFS everywhere
            Assert.That(Db(full.Peak), Is.LessThan(-1.5), "sample peak");
            // momentary peak never above -9 dBFS except the beat-4 swell (about -6 dBFS)
            Assert.That(Db(full.PeakOutsideSwell), Is.LessThan(-9.0), "peak outside the swell at s " + full.PeakOutsideSwellS.ToString("F1"));
            Assert.That(Db(full.PeakSwell), Is.InRange(-12.0, -1.5), "the exhale swell is the loudest moment, about -6 dBFS");
            Assert.That(full.PeakSwell, Is.GreaterThan(full.PeakOutsideSwell), "the swell must be the peak of the walk");
            // DC below 0.001
            Assert.That(Math.Abs(full.Dc(0)), Is.LessThan(0.001), "DC left");
            Assert.That(Math.Abs(full.Dc(1)), Is.LessThan(0.001), "DC right");
            // no sample step above 0.25 full scale (footsteps excluded by rendering without them; with them it holds too)
            Assert.That(noSteps.MaxStep, Is.LessThan(0.25), "click in the score at s " + noSteps.MaxStepS.ToString("F1"));
            Assert.That(full.MaxStep, Is.LessThan(0.25), "click in the mix at s " + full.MaxStepS.ToString("F1"));
            // the wind stem alone reads off from s = 752
            Assert.That(wind.AfterFrames, Is.GreaterThan(Fs * 10));
            Assert.That(wind.AfterRmsDb, Is.LessThan(-70.0), "wind stem after s = 752");
            // quiet by design (14.3.3: integrated loudness about -30 LUFS); generous band, the number is in the report
            double lufs = full.IntegratedLufs();
            Assert.That(lufs, Is.InRange(-36.0, -24.0), "integrated loudness " + lufs.ToString("F1") + " LUFS");
            // the sidecar maps seconds to s
            Assert.That(File.Exists(Path.ChangeExtension(wav, ".json")), "sidecar json missing");
        }

        // ============================================================ DSP unit checks

        static ThreadAudioParams At(double s) { return ThreadAudioParams.ComputeFull(s, 0.0, 100.0, false, -1.0, 0.0, AudioLayer.AllMask); }

        [Test]
        public void Dsp_ProcessIsAllocationFree()
        {
            var dsp = new ThreadDsp(Fs, Seed);
            var p = At(338.0);   // the busiest moment: drone, pad, hum, wind, swell
            dsp.SnapTo(in p);
            var buf = new float[2048];
            for (int i = 0; i < 8; i++) dsp.Process(buf, 2, in p);   // warm up (JIT, statics)
            Assert.That(() => { for (int i = 0; i < 16; i++) dsp.Process(buf, 2, in p); },
                        Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Dsp_IsDeterministicPerSeed()
        {
            float[] a = RenderSeconds(Seed, 2.0, 120.0), b = RenderSeconds(Seed, 2.0, 120.0), c = RenderSeconds(Seed + 1, 2.0, 120.0);
            Assert.That(a, Is.EqualTo(b), "same seed must give identical samples");
            int diff = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] != c[i]) diff++;
            Assert.That(diff, Is.GreaterThan(a.Length / 2), "another seed must change the noise");
        }

        static float[] RenderSeconds(int seed, double seconds, double s, int fs = Fs)
        {
            var dsp = new ThreadDsp(fs, seed);
            var p = At(s);
            dsp.SnapTo(in p);
            int frames = (int)(seconds * fs);
            var outBuf = new float[frames * 2];
            var buf = new float[1024 * 2];
            for (int f = 0; f < frames; f += 1024)
            {
                int n = Math.Min(1024, frames - f);
                Array.Clear(buf, 0, buf.Length);
                dsp.Process(buf, 2, in p);
                Array.Copy(buf, 0, outBuf, f * 2, n * 2);
            }
            return outBuf;
        }

        [Test]
        public void Dsp_StableAtDeviceRates([Values(44100, 48000, 96000)] int fs)
        {
            // the hardest settings: open 14 kHz low-pass with the swell, the 2.2 kHz inhale, the 7 s reverb, the deck
            double[] spots = { 338.0, 285.0, 255.0, 590.0, 598.0, 857.0 };
            for (int k = 0; k < spots.Length; k++)
            {
                float[] x = RenderSeconds(Seed, 3.0, spots[k], fs);
                double peak = 0.0;
                for (int i = 0; i < x.Length; i++)
                {
                    Assert.That(float.IsNaN(x[i]) || float.IsInfinity(x[i]), Is.False, "non-finite at " + fs + " Hz, s " + spots[k]);
                    peak = Math.Max(peak, Math.Abs(x[i]));
                }
                Assert.That(Db(peak), Is.LessThan(-1.5), "peak at " + fs + " Hz, s " + spots[k]);
                Assert.That(Db(peak), Is.GreaterThan(-60.0), "silent at " + fs + " Hz, s " + spots[k]);
            }
        }

        [Test]
        public void Dsp_LayerLevelsAreCalibrated()
        {
            // each layer alone at gain Db(-30) must read -30 dBFS RMS (+-1 dB) at the master input (reverb and limiter
            // are effectively transparent at this level; noise layers need a few seconds to converge)
            int[] layers = { AudioLayer.Wind, AudioLayer.Drone, AudioLayer.Pad, AudioLayer.Hush, AudioLayer.Hum, AudioLayer.Beacon };
            for (int k = 0; k < layers.Length; k++)
            {
                var p = new ThreadAudioParams
                {
                    master = 1f, stepBus = 1f, lowpassHz = 14000f, rt60 = 0.2f, preDelayMs = 10f, wetDb = -90f, dampHz = 9000f,
                    pad0 = 1f, pad1 = 1f, pad2 = 1f, pad3 = 1f, humUpper = 1f, layerMask = AudioLayer.Bit(layers[k]),
                };
                float g = (float)AudioSpec.Db(-30.0);
                p.wind = g; p.drone = g; p.pad = g; p.hush = g; p.hum = g; p.beacon = g;
                var dsp = new ThreadDsp(Fs, Seed);
                dsp.SnapTo(in p);
                var buf = new float[2048];
                double sum = 0.0; long n = 0;
                int blocks = Fs * 20 / 1024;   // 20 s (the drone beats at 0.17 Hz and the LFO runs at 0.03 Hz)
                for (int i = 0; i < blocks; i++)
                {
                    Array.Clear(buf, 0, buf.Length);
                    dsp.Process(buf, 2, in p);
                    if (i < blocks / 10) continue;
                    for (int j = 0; j < buf.Length; j++) sum += (double)buf[j] * buf[j];
                    n += buf.Length;
                }
                double rms = 10.0 * Math.Log10(sum / n);
                Assert.That(rms, Is.EqualTo(-30.0).Within(1.0), AudioLayer.Names[layers[k]] + " calibration");
            }
        }

        [Test]
        public void Limiter_CeilingBelowMinus1p5dBFS()
        {
            Assert.That(Db(ThreadDsp.Limit(100f)), Is.LessThan(-1.5));
            Assert.That(Db(ThreadDsp.Limit(-100f)), Is.LessThan(-1.5));
            Assert.That(ThreadDsp.Limit(0.01f), Is.EqualTo(0.01f).Within(1e-6f), "transparent at -40 dBFS");
            // continuous across the series / tanh switch at |x / c| = 0.1
            float c = (float)AudioSpec.LimiterCeiling;
            Assert.That(ThreadDsp.Limit(0.1f * c - 1e-6f), Is.EqualTo(ThreadDsp.Limit(0.1f * c + 1e-6f)).Within(1e-5f));
        }

        [Test]
        public void Svf_StableNearNyquist([Values(44100, 48000)] int fs)
        {
            var f = new SvfTpt();
            f.Set(AudioSpec.LowpassOpenHz, 0.7071, fs);
            var rng = new XorShift32(7);
            float peak = 0f;
            for (int i = 0; i < fs * 2; i++) peak = Math.Max(peak, Math.Abs(f.Low(rng.NextBipolar())));
            Assert.That(peak, Is.LessThan(4f), "the trapezoidal SVF must not blow up at 14 kHz (Chamberlin does)");
        }

        // ============================================================ parameter schedule (14.3.3, 14.3.6)

        [Test]
        public void Params_SilencesAndSwells()
        {
            const double tol = 0.05;   // dB
            // inhale: everything except the drone ducked by 20 dB inside the bank core
            var core = At(285.0);
            Assert.That(Db(core.hush) - Db(ThreadAudioParams.LayerAmp(AudioLayer.Hush, 285.0)), Is.EqualTo(-20.0).Within(tol));
            Assert.That(Db(core.drone) - Db(ThreadAudioParams.LayerAmp(AudioLayer.Drone, 285.0)), Is.EqualTo(0.0).Within(tol));
            Assert.That(core.lowpassHz, Is.EqualTo((float)AudioSpec.LowpassClosedHz).Within(1f), "master low-pass at 2.2 kHz in the inhale");
            // exhale: pad and hum +12 dB at s = 336, low-pass open
            var ex = At(336.0);
            Assert.That(Db(ex.pad) - Db(ThreadAudioParams.LayerAmp(AudioLayer.Pad, 336.0)), Is.EqualTo(12.0).Within(tol));
            Assert.That(ex.lowpassHz, Is.EqualTo((float)AudioSpec.LowpassOpenHz).Within(1f));
            // the hole at the emergence: everything -12 dB
            var hole = At(598.0);
            Assert.That(Db(hole.drone) - Db(ThreadAudioParams.LayerAmp(AudioLayer.Drone, 598.0)), Is.EqualTo(-12.0).Within(tol));
            // wind -34 dBFS inside the cloud deck
            Assert.That(Db(At(588.0).wind), Is.EqualTo(-34.0).Within(0.5));
            // wind exactly off from s = 752
            for (double s = ThreadConstants.WindOffS; s <= ThreadConstants.PathLength; s += 0.5) Assert.That(At(s).wind, Is.EqualTo(0f), "wind at s " + s);
            // last 20 m: pad and beacon only (drone, hum and stars gone), dry
            var pier = At(850.0);
            Assert.That(pier.drone, Is.EqualTo(0f)); Assert.That(pier.hum, Is.EqualTo(0f)); Assert.That(pier.stars, Is.EqualTo(0f));
            Assert.That(pier.pad, Is.GreaterThan(0f)); Assert.That(pier.beacon, Is.GreaterThan(0f));
            Assert.That(pier.wetDb, Is.LessThanOrEqualTo(-59f));
            // beacon: silent beyond 40 m, -38 dBFS at 6 m (s = 858)
            Assert.That(At(800.0).beacon, Is.EqualTo(0f));
            Assert.That(Db(At(ThreadConstants.BeaconTriggerS).beacon), Is.EqualTo(AudioSpec.BeaconDbNear).Within(0.3));
            // pad voices enter one at a time
            Assert.That(At(149.0).pad0, Is.EqualTo(0f)); Assert.That(At(151.0).pad0, Is.EqualTo(1f));
            Assert.That(At(329.0).pad2, Is.EqualTo(0f)); Assert.That(At(331.0).pad2, Is.EqualTo(1f));
            // stars only once star alpha > 0.2 (and the beat-6/7 levels)
            Assert.That(At(500.0).starGate, Is.EqualTo(0f)); Assert.That(At(650.0).starGate, Is.EqualTo(1f));
            Assert.That(Db(At(650.0).stars), Is.EqualTo(-48.0).Within(tol));
            // reverb per beat (14.3.4) away from the blend windows
            Assert.That(At(45.0).rt60, Is.EqualTo(0.9f).Within(1e-4f)); Assert.That(At(165.0).rt60, Is.EqualTo(5.5f).Within(1e-4f));
            Assert.That(At(365.0).preDelayMs, Is.EqualTo(200f).Within(1e-3f)); Assert.That(At(800.0).wetDb, Is.EqualTo(-40f).Within(1e-3f));
        }

        [Test]
        public void Params_IntroEndingAndPause()
        {
            // 0 to 0.5 s black and silent; wind -60 -> -46 dBFS with the picture (0.5 to 3.0 s); table value by 4.0 s
            Assert.That(ThreadAudioParams.ComputeFull(0.0, 0.0, 0.0, false, -1.0, 0.0, AudioLayer.AllMask).wind, Is.EqualTo(0f));
            Assert.That(Db(ThreadAudioParams.ComputeFull(0.0, 0.0, 0.5, false, -1.0, 0.0, AudioLayer.AllMask).wind), Is.EqualTo(-60.0).Within(0.1));
            Assert.That(Db(ThreadAudioParams.ComputeFull(0.0, 0.0, 3.0, false, -1.0, 0.0, AudioLayer.AllMask).wind), Is.EqualTo(-46.0).Within(0.1));
            Assert.That(Db(ThreadAudioParams.ComputeFull(0.0, 0.0, 4.0, false, -1.0, 0.0, AudioLayer.AllMask).wind), Is.EqualTo(-40.0).Within(0.1));
            // the drone enters at 2.0 s
            Assert.That(ThreadAudioParams.ComputeFull(0.0, 0.0, 1.99, false, -1.0, 0.0, AudioLayer.AllMask).drone, Is.EqualTo(0f));
            Assert.That(Db(ThreadAudioParams.ComputeFull(0.0, 0.0, 5.0, false, -1.0, 0.0, AudioLayer.AllMask).drone), Is.EqualTo(-42.0).Within(0.05));
            // the master fades out over 6 s after the goal trigger
            Assert.That(ThreadAudioParams.ComputeFull(858.0, 0.0, 600.0, false, 0.0, 0.0, AudioLayer.AllMask).master, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(ThreadAudioParams.ComputeFull(858.0, 0.0, 600.0, false, ThreadConstants.EndAudioFade, 0.0, AudioLayer.AllMask).master, Is.EqualTo(0f).Within(1e-6f));
            // pause ducks the master by 20 dB
            Assert.That(Db(ThreadAudioParams.Compute(100.0, 100.0, false, -1.0, true).master), Is.EqualTo(AudioSpec.PauseDuckDb).Within(0.01));
            // brisk footsteps +1 dB
            Assert.That(Db(ThreadAudioParams.Compute(100.0, 100.0, true, -1.0, false).footGain), Is.EqualTo(AudioSpec.BriskStepDb).Within(0.01));
        }

        [Test]
        public void Params_AreContinuousInS()
        {
            // no parameter jumps between neighbouring centimetres (the 20 ms smoothing is a second line of defence)
            ThreadAudioParams prev = At(0.0);
            for (double s = 0.01; s <= ThreadConstants.PathLength; s += 0.01)
            {
                ThreadAudioParams p = At(s);
                float[] a = { prev.wind, prev.drone, prev.pad, prev.hush, prev.hum, prev.stars, prev.beacon, prev.breathLL, prev.breathRR };
                float[] b = { p.wind, p.drone, p.pad, p.hush, p.hum, p.stars, p.beacon, p.breathLL, p.breathRR };
                for (int k = 0; k < a.Length; k++)
                    Assert.That(Math.Abs(b[k] - a[k]), Is.LessThan(0.002f), "gain " + k + " jumps at s " + s.ToString("F2"));
                Assert.That(Math.Abs(Math.Log(p.lowpassHz / prev.lowpassHz)), Is.LessThan(0.02), "low-pass jumps at s " + s.ToString("F2"));
                prev = p;
            }
        }

        // ============================================================ footsteps (14.3.5)

        [Test]
        public void Footsteps_BankMeetsRecipe()
        {
            var bank = new FootstepBank(Fs, Seed, 0);
            for (int s = 0; s < FootstepRules.SurfaceCount; s++)
            {
                for (int v = 0; v < AudioSpec.FootstepVariants; v++)
                {
                    float[] x = bank.Get((FootSurface)s, v);
                    double peak = 0.0;
                    for (int i = 0; i < x.Length; i++)
                    {
                        Assert.That(float.IsNaN(x[i]) || float.IsInfinity(x[i]), Is.False);
                        peak = Math.Max(peak, Math.Abs(x[i]));
                    }
                    string tag = ((FootSurface)s) + " #" + v;
                    Assert.That(Db(peak), Is.EqualTo(AudioSpec.FootPeakDb[s]).Within(0.05), tag + " peak level");
                    Assert.That(x[0], Is.EqualTo(0f), tag + " starts at zero");
                    Assert.That(x[x.Length - 1], Is.EqualTo(0f), tag + " ends at zero");
                    Assert.That(x.Length / (double)Fs, Is.LessThanOrEqualTo(1.2), tag + " pool rule (4 voices at 3.2 steps/s)");
                }
                // variants differ
                Assert.That(bank.Get((FootSurface)s, 0), Is.Not.EqualTo(bank.Get((FootSurface)s, 1)));
            }
            // deterministic, and the metal brightness (+1 dB per 0.1 of D) changes the timbre
            var again = new FootstepBank(Fs, Seed, 0);
            Assert.That(again.Get(FootSurface.Metal, 3), Is.EqualTo(bank.Get(FootSurface.Metal, 3)));
            var bright = new FootstepBank(Fs, Seed, FootstepRules.MetalBrightIndex(1.0));
            Assert.That(HighBandRatio(bright.Get(FootSurface.Metal, 0)), Is.GreaterThan(HighBandRatio(bank.Get(FootSurface.Metal, 0)) * 2.0));
        }

        /// <summary>Energy above ~700 Hz relative to the total (first-difference weighting is enough for the comparison).</summary>
        static double HighBandRatio(float[] x)
        {
            var hp = new SvfTpt(); hp.Set(700.0, 0.7071, Fs);
            double hi = 0.0, all = 0.0;
            for (int i = 0; i < x.Length; i++) { float h = hp.High(x[i]); hi += h * h; all += x[i] * x[i]; }
            return hi / Math.Max(1e-30, all);
        }

        [Test]
        public void Footsteps_CadenceAndSurfaces()
        {
            Assert.That(FootstepRules.SurfaceAt(10.0), Is.EqualTo(FootSurface.Salt));
            Assert.That(FootstepRules.SurfaceAt(200.0), Is.EqualTo(FootSurface.Salt));
            Assert.That(FootstepRules.SurfaceAt(260.0), Is.EqualTo(FootSurface.Stone));
            Assert.That(FootstepRules.SurfaceAt(350.0), Is.EqualTo(FootSurface.Terrace));
            Assert.That(FootstepRules.SurfaceAt(500.0), Is.EqualTo(FootSurface.Metal));
            Assert.That(FootstepRules.SurfaceAt(850.0), Is.EqualTo(FootSurface.Pier));
            // 1.92 steps per second at 1.5 m/s on the flat (one per 0.78 m), first step on the first movement
            var q = FootstepSequencer.Create(Seed);
            int steps = 0; double firstAt = -1.0;
            float lastPan = 0f;
            for (int i = 0; i < 6000; i++)   // 100 s at 60 fps
            {
                double s = 10.0 + i * ThreadConstants.WalkSpeed / 60.0;
                if (q.Advance(ThreadConstants.WalkSpeed / 60.0, s, out StepEvent e))
                {
                    if (firstAt < 0.0) firstAt = i / 60.0;
                    Assert.That(Math.Abs(e.pitch - 1f), Is.LessThanOrEqualTo((float)AudioSpec.FootPitchJitter + 1e-6f));
                    Assert.That(Math.Abs(e.pan), Is.EqualTo((float)AudioSpec.FootPan).Within(1e-6f));
                    Assert.That(e.pan, Is.Not.EqualTo(lastPan), "pan alternates");
                    lastPan = e.pan;
                    steps++;
                }
            }
            Assert.That(firstAt, Is.LessThan(0.1), "first footstep as soon as the player moves");
            Assert.That(steps / 100.0, Is.EqualTo(ThreadConstants.WalkSpeed / ThreadConstants.StepLength).Within(0.05));
        }

        // ============================================================ WAV writer

        [Test]
        public void Wav_RoundTrip([Values(WavFormat.Pcm16, WavFormat.Float32)] WavFormat format)
        {
            string path = Path.Combine(Path.GetTempPath(), "thread_wav_" + format + ".wav");
            var x = new float[4800 * 2];
            for (int i = 0; i < 4800; i++) { x[2 * i] = (float)(0.5 * Math.Sin(2 * Math.PI * 440 * i / 48000.0)); x[2 * i + 1] = -x[2 * i]; }
            WavWriter.Write(path, x, x.Length, 2, 48000, format);
            float[] y = WavWriter.Read(path, out int ch, out int rate);
            Assert.That(ch, Is.EqualTo(2)); Assert.That(rate, Is.EqualTo(48000)); Assert.That(y.Length, Is.EqualTo(x.Length));
            float tol = format == WavFormat.Pcm16 ? 2.5f / 32768f : 0f;
            for (int i = 0; i < x.Length; i++) Assert.That(y[i], Is.EqualTo(x[i]).Within(tol));
            File.Delete(path);
        }
    }
}
