// OfflineAudioRenderer: renders the walk s = s0..s1 at a plan speed deterministically with ThreadDsp, without an
// audio device (MASTER_PROMPT 14.3.1, 11.9, T-AUDIO). Footsteps are mixed exactly like the listener mix of the
// player (pre-rendered FootstepBank clips, one step per 0.78 m, pitch +-3 percent by linear resampling, pan
// +-0.15) before ThreadDsp.Process, so they pass through the reverb and the limiter. Stems: any layer mask,
// with or without footsteps. The goal trigger at s = 858 replaces the motion by the stop ramp of 14.11
// (v0 (1 - smoothstep(0, T, t)), T = 4 / v0) and starts the 6 s master fade.
// Output goes to IAudioBlockSink objects (WAV stream, meters), so a 10-minute render never sits in memory.
// RenderToWav also writes the JSON sidecar (s at every whole second). Pure C#, no UnityEngine.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Megastructure.Audio
{
    /// <summary>Where a rendered block sits on the walk.</summary>
    public struct AudioBlockInfo
    {
        public double t0, t1;          // seconds since the render started
        public double s0, s1;          // progress at the block start and end
        public double endingTime;      // seconds since the goal trigger at the block start (negative before it)
        public long frame0;            // index of the first frame
    }

    public interface IAudioBlockSink
    {
        void Begin(int sampleRate, int channels);
        /// <summary>interleaved stereo; frames valid frames.</summary>
        void Write(float[] interleaved, int frames, in AudioBlockInfo info);
        void End();
    }

    public sealed class OfflineRenderSettings
    {
        public double S0 = 0.0;
        public double S1 = ThreadConstants.PathLength;
        public double Speed = ThreadConstants.WalkSpeed;
        public int SampleRate = (int)ThreadConstants.AudioSampleRate;
        public int Seed = 1;
        public bool Footsteps = true;
        public bool Brisk;
        public double Lateral = 0.0;
        /// <summary>Intro clock at the start of the render (0 = the game just started; 10+ = intro done).</summary>
        public double IntroTime = 0.0;
        /// <summary>Fire the goal trigger at s = 858 (stop ramp + 6 s master fade) when S1 reaches it.</summary>
        public bool Ending = true;
        /// <summary>Seconds rendered after the motion stopped (or after the ending fade finished).</summary>
        public double TailSeconds = 0.5;
        public int LayerMask = AudioLayer.AllMask;
        /// <summary>Frames per parameter update (the player's DSP buffer is 1024 frames).</summary>
        public int BlockFrames = 1024;
        /// <summary>Start with every smoother at its target (stems that begin mid-walk).</summary>
        public bool SnapAtStart;

        public OfflineRenderSettings Clone() { return (OfflineRenderSettings)MemberwiseClone(); }
    }

    public sealed class OfflineRenderInfo
    {
        public int SampleRate, Channels = 2;
        public long Frames;
        public double Seconds;
        public double TriggerTime = -1.0;
        public int Steps;
        public int NanResets;
        public readonly List<double> SPerSecond = new List<double>();
    }

    public static class OfflineAudioRenderer
    {
        const int Channels = 2;
        const int MaxVoices = 8;

        struct Voice
        {
            public float[] clip;
            public double pos, rate;
            public float gl, gr;
            public int delay;
        }

        /// <summary>Progress at time t for a walk from s0 at speed v with an optional goal trigger (14.11 stop ramp).</summary>
        public static double MotionS(OfflineRenderSettings st, double t, out double endingTime, out bool moving)
        {
            double v = Math.Max(1e-6, st.Speed);
            double trig = ThreadConstants.BeaconTriggerS;
            bool useEnding = st.Ending && st.S1 >= trig && st.S0 < trig;
            endingTime = -1.0;
            if (!useEnding)
            {
                double s = st.S0 + v * t;
                moving = s < st.S1;
                return Math.Min(s, st.S1);
            }
            double tTrig = (trig - st.S0) / v;
            if (t < tTrig) { moving = true; return st.S0 + v * t; }
            double tp = t - tTrig, T = 4.0 / v;                // T = 4 / v0, distance v0 T / 2 = 2.0 m
            endingTime = tp;
            if (tp >= T) { moving = false; return trig + 0.5 * v * T; }
            double u = tp / T;
            moving = true;
            return trig + v * T * (u - u * u * u + 0.5 * u * u * u * u);   // integral of 1 - smoothstep
        }

        /// <summary>Total render length in seconds for the settings.</summary>
        public static double Duration(OfflineRenderSettings st)
        {
            double v = Math.Max(1e-6, st.Speed);
            double trig = ThreadConstants.BeaconTriggerS;
            if (st.Ending && st.S1 >= trig && st.S0 < trig)
                return (trig - st.S0) / v + Math.Max(4.0 / v, ThreadConstants.EndAudioFade) + st.TailSeconds;
            return Math.Max(0.0, st.S1 - st.S0) / v + st.TailSeconds;
        }

        public static OfflineRenderInfo Render(OfflineRenderSettings st, params IAudioBlockSink[] sinks)
        {
            int fs = st.SampleRate > 0 ? st.SampleRate : (int)ThreadConstants.AudioSampleRate;
            int B = Math.Max(16, st.BlockFrames);
            var info = new OfflineRenderInfo { SampleRate = fs };
            var dsp = new ThreadDsp(fs, st.Seed);
            float[] buf = new float[B * Channels];
            long totalFrames = (long)Math.Ceiling(Duration(st) * fs);

            // footsteps: the base bank plus metal sets per quantised brightness (rendered on demand, cached)
            FootstepBank bank = st.Footsteps ? new FootstepBank(fs, st.Seed, 0) : null;
            var metal = new Dictionary<int, float[][]>();
            var renderer = st.Footsteps ? new FootstepRenderer(fs) : null;
            float[] scratch = st.Footsteps ? new float[FootstepRules.ClipFrames(FootSurface.Metal, fs)] : null;
            var seq = FootstepSequencer.Create(st.Seed);
            var voices = new Voice[MaxVoices];
            int voiceCount = 0;

            for (int i = 0; i < sinks.Length; i++) sinks[i].Begin(fs, Channels);
            double nextSecond = 0.0;
            bool first = true;
            for (long f = 0; f < totalFrames; f += B)
            {
                int n = (int)Math.Min(B, totalFrames - f);
                double t0 = f / (double)fs, t1 = (f + n) / (double)fs;
                double s0 = MotionS(st, t0, out double end0, out _);
                double s1 = MotionS(st, t1, out _, out _);
                while (nextSecond <= t1 && nextSecond <= Duration(st))
                {
                    info.SPerSecond.Add(MotionS(st, nextSecond, out _, out _));
                    nextSecond += 1.0;
                }
                if (end0 >= 0.0 && info.TriggerTime < 0.0) info.TriggerTime = t0 - end0;

                var p = ThreadAudioParams.ComputeFull(s0, st.Lateral, st.IntroTime + t0, st.Brisk, end0, 0.0, st.LayerMask);
                if (first) { if (st.SnapAtStart) dsp.SnapTo(in p); first = false; }

                Array.Clear(buf, 0, n * Channels);
                if (bank != null)
                {
                    // steps placed at their exact frame inside the block (linear motion within 21 ms)
                    double walked = s1 - s0;
                    double sCur = s0, done = 0.0;
                    int guard = 0;
                    while (walked - done > 1e-12 && guard++ < 4)
                    {
                        double need = seq.DistanceToNext(sCur);
                        if (need > walked - done) { seq.Advance(walked - done, sCur, out _); break; }
                        done += need;
                        sCur = s0 + done;
                        if (seq.Advance(need + 1e-12, sCur, out StepEvent e))
                        {
                            int at = (int)Math.Min(n - 1, Math.Max(0, Math.Round(n * done / Math.Max(walked, 1e-12))));
                            float[] clip;
                            if (e.surface == FootSurface.Metal)
                            {
                                int q = FootstepRules.MetalBrightIndex(ThreadMath.Darkness(sCur));
                                if (!metal.TryGetValue(q, out float[][] set)) { set = RenderMetalSet(renderer, st.Seed, q, scratch); metal[q] = set; }
                                clip = set[e.variant];
                            }
                            else clip = bank.Get(e.surface, e.variant);
                            // balance-law pan (centre = unity on both channels), the step gain of 14.3.5 is in the clip
                            float gain = p.footGain;
                            float gl = gain * (e.pan > 0f ? 1f - e.pan : 1f);
                            float gr = gain * (e.pan < 0f ? 1f + e.pan : 1f);
                            if (voiceCount == MaxVoices) { for (int k = 1; k < voiceCount; k++) voices[k - 1] = voices[k]; voiceCount--; }
                            voices[voiceCount++] = new Voice { clip = clip, pos = 0.0, rate = e.pitch, gl = gl, gr = gr, delay = at };
                            info.Steps++;
                        }
                    }
                    // mix the voices (linear-interpolation resampling for the pitch)
                    for (int k = 0; k < voiceCount; k++)
                    {
                        Voice v = voices[k];
                        int len = v.clip.Length;
                        for (int i = v.delay; i < n; i++)
                        {
                            int ip = (int)v.pos;
                            if (ip >= len - 1) { v.pos = len; break; }
                            float fr = (float)(v.pos - ip);
                            float x = v.clip[ip] + fr * (v.clip[ip + 1] - v.clip[ip]);
                            buf[2 * i] += v.gl * x; buf[2 * i + 1] += v.gr * x;
                            v.pos += v.rate;
                        }
                        v.delay = 0;
                        voices[k] = v;
                    }
                    int w = 0;
                    for (int k = 0; k < voiceCount; k++) if (voices[k].pos < voices[k].clip.Length - 1) voices[w++] = voices[k];
                    voiceCount = w;
                }

                dsp.Process(buf, Channels, in p);
                var bi = new AudioBlockInfo { t0 = t0, t1 = t1, s0 = s0, s1 = s1, endingTime = end0, frame0 = f };
                for (int i = 0; i < sinks.Length; i++) sinks[i].Write(buf, n, in bi);
                info.Frames += n;
            }
            for (int i = 0; i < sinks.Length; i++) sinks[i].End();
            info.Seconds = info.Frames / (double)fs;
            info.NanResets = dsp.NanResets;
            return info;
        }

        /// <summary>The 6 metal variants at one quantised brightness (identical to the runtime's background re-render).</summary>
        static float[][] RenderMetalSet(FootstepRenderer r, int seed, int brightIndex, float[] scratch)
        {
            var set = new float[AudioSpec.FootstepVariants][];
            for (int v = 0; v < set.Length; v++)
            {
                int n = r.Render(FootSurface.Metal, v, seed, FootstepRules.MetalBrightDb(brightIndex), scratch);
                set[v] = new float[n];
                Array.Copy(scratch, set[v], n);
            }
            return set;
        }

        /// <summary>Renders to a WAV file (PCM16 or float32) plus a JSON sidecar next to it; extra sinks see the same blocks.</summary>
        public static OfflineRenderInfo RenderToWav(OfflineRenderSettings st, string wavPath, WavFormat format, params IAudioBlockSink[] extra)
        {
            OfflineRenderInfo info;
            using (var w = new WavStreamWriter(wavPath, Channels, st.SampleRate, format, st.Seed))
            {
                var all = new IAudioBlockSink[(extra != null ? extra.Length : 0) + 1];
                all[0] = w;
                if (extra != null) for (int i = 0; i < extra.Length; i++) all[i + 1] = extra[i];
                info = Render(st, all);
            }
            WriteSidecar(Path.ChangeExtension(wavPath, ".json"), st, info, wavPath);
            return info;
        }

        /// <summary>Renders each layer mask of `stems` to its own WAV (dir/prefix_name.wav) with the same walk.</summary>
        public static void RenderStems(OfflineRenderSettings st, string dir, string prefix, int[] stemMasks, string[] stemNames, bool footstepsInStems)
        {
            for (int i = 0; i < stemMasks.Length; i++)
            {
                var c = st.Clone();
                c.LayerMask = stemMasks[i];
                c.Footsteps = footstepsInStems;
                RenderToWav(c, Path.Combine(dir, prefix + "_" + stemNames[i] + ".wav"), WavFormat.Pcm16);
            }
        }

        /// <summary>
        /// Part 11.9: one WAV per beat (dir/beatNN_name.wav + sidecar + report line), each rendered from its own start
        /// with the intro done and every smoother snapped (so each file is the steady sound of that beat). Returns the
        /// per-beat reports.
        /// </summary>
        public static string RenderBeats(string dir, int seed, double speed, WavFormat format)
        {
            var sb = new StringBuilder();
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 0; i < ThreadConstants.BeatCount; i++)
            {
                var st = new OfflineRenderSettings
                {
                    S0 = b[i], S1 = b[i + 1], Speed = speed, Seed = seed, IntroTime = 100.0, SnapAtStart = true,
                    Ending = i == ThreadConstants.BeatCount - 1,
                };
                var m = new WalkLevelMeter();
                string name = "beat" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + ThreadConstants.BeatNames[i] + ".wav";
                RenderToWav(st, Path.Combine(dir, name), format, m);
                sb.Append(name).Append(": RMS ").Append(m.BeatRmsDb(i).ToString("F2", CultureInfo.InvariantCulture))
                  .Append(" dBFS (target ").Append(ThreadConstants.TargetRmsDb[i].ToString("F0", CultureInfo.InvariantCulture))
                  .Append("), peak ").Append(WalkLevelMeter.Db(m.Peak).ToString("F2", CultureInfo.InvariantCulture)).Append(" dBFS\n");
            }
            return sb.ToString();
        }

        public static void WriteSidecar(string path, OfflineRenderSettings st, OfflineRenderInfo info, string wavPath)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\n");
            sb.Append("  \"wav\": \"").Append(Path.GetFileName(wavPath)).Append("\",\n");
            sb.Append("  \"sampleRate\": ").Append(info.SampleRate.ToString(ci)).Append(",\n");
            sb.Append("  \"channels\": ").Append(info.Channels.ToString(ci)).Append(",\n");
            sb.Append("  \"seed\": ").Append(st.Seed.ToString(ci)).Append(",\n");
            sb.Append("  \"speed\": ").Append(st.Speed.ToString("R", ci)).Append(",\n");
            sb.Append("  \"s0\": ").Append(st.S0.ToString("R", ci)).Append(",\n");
            sb.Append("  \"s1\": ").Append(st.S1.ToString("R", ci)).Append(",\n");
            sb.Append("  \"layerMask\": ").Append(st.LayerMask.ToString(ci)).Append(",\n");
            sb.Append("  \"footsteps\": ").Append(st.Footsteps ? "true" : "false").Append(",\n");
            sb.Append("  \"frames\": ").Append(info.Frames.ToString(ci)).Append(",\n");
            sb.Append("  \"seconds\": ").Append(info.Seconds.ToString("F3", ci)).Append(",\n");
            sb.Append("  \"triggerTime\": ").Append(info.TriggerTime.ToString("F3", ci)).Append(",\n");
            sb.Append("  \"steps\": ").Append(info.Steps.ToString(ci)).Append(",\n");
            sb.Append("  \"nanResets\": ").Append(info.NanResets.ToString(ci)).Append(",\n");
            sb.Append("  \"beatBoundaries\": [");
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 0; i < b.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(b[i].ToString("R", ci)); }
            sb.Append("],\n");
            sb.Append("  \"sPerSecond\": [");
            for (int i = 0; i < info.SPerSecond.Count; i++)
            {
                if (i > 0) sb.Append(i % 16 == 0 ? ",\n    " : ", ");
                sb.Append(info.SPerSecond[i].ToString("F3", ci));
            }
            sb.Append("]\n}\n");
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString());
        }
    }

    /// <summary>
    /// The T-AUDIO meter (14.10.3 audio row, 14.3.3 master targets) over an offline walk: RMS per beat (beat 3
    /// without the inhale window s 268-302, beat 4 without the swell window s 330-345, nothing after the goal
    /// trigger), sample peak, peak outside the swell (the "momentary peak never above -9 dBFS" rule), DC per
    /// channel, largest sample step, 400 ms RMS maximum, RMS per second, RMS after a given s (stems), BS.1770
    /// integrated loudness (informational, R5 snippet B). Offline only (allocates its per-second lists).
    /// </summary>
    public sealed class WalkLevelMeter : IAudioBlockSink
    {
        public readonly double[] BeatSumSq = new double[ThreadConstants.BeatCount];
        public readonly long[] BeatFrames = new long[ThreadConstants.BeatCount];
        public readonly double[] BeatPeak = new double[ThreadConstants.BeatCount];
        public readonly double[] Sum = new double[2];
        public double Peak, PeakSwell, PeakOutsideSwell, MaxStep, MaxStepS, SumSqAll, PeakOutsideSwellS;
        public long Frames;
        public int NonFinite;
        public double AfterS = double.MaxValue;   // RMS of frames with s >= AfterS
        public double AfterSumSq; public long AfterFrames;
        public readonly List<double> RmsPerSecondDb = new List<double>();
        public readonly List<double> SPerSecond = new List<double>();
        public double MaxMomentaryRmsDb = double.NegativeInfinity, MaxMomentaryS;

        int m_fs;
        float m_pl, m_pr; bool m_havePrev;
        double m_secSum; int m_secN; double m_secS;
        // 400 ms window of 100 ms sub-blocks (momentary)
        readonly double[] m_sub = new double[4]; int m_subN, m_subFill; double m_subAcc;
        // K-weighting state (BS.1770), per channel: shelf + RLB high-pass (double biquads, transposed DF II)
        double[] m_kb, m_ka; readonly double[] m_kz = new double[8];
        readonly List<double> m_kSub = new List<double>(); double m_kAcc; int m_kN;

        public static bool InSwell(double s) { return s >= ThreadConstants.ExhaleS0 && s < ThreadConstants.SwellWindowEnd; }
        /// <summary>Frames that count toward the beat RMS (14.3.3 measurement windows).</summary>
        public static bool CountsForBeat(double s, double endingTime)
        {
            if (endingTime >= 0.0) return false;
            if (s >= ThreadConstants.InhaleS0 && s < ThreadConstants.InhaleS1) return false;
            if (InSwell(s)) return false;
            return true;
        }

        public double BeatRmsDb(int b) { return BeatFrames[b] > 0 ? 10.0 * Math.Log10(Math.Max(1e-30, BeatSumSq[b] / (2.0 * BeatFrames[b]))) : double.NegativeInfinity; }
        public double RmsDb { get { return Frames > 0 ? 10.0 * Math.Log10(Math.Max(1e-30, SumSqAll / (2.0 * Frames))) : double.NegativeInfinity; } }
        public double AfterRmsDb { get { return AfterFrames > 0 ? 10.0 * Math.Log10(Math.Max(1e-30, AfterSumSq / (2.0 * AfterFrames))) : double.NegativeInfinity; } }
        public static double Db(double lin) { return 20.0 * Math.Log10(Math.Max(1e-15, lin)); }
        public double Dc(int ch) { return Frames > 0 ? Sum[ch] / Frames : 0.0; }

        public void Begin(int sampleRate, int channels)
        {
            m_fs = sampleRate;
            KWeighting(sampleRate, out m_kb, out m_ka);
        }

        public void Write(float[] x, int frames, in AudioBlockInfo info)
        {
            int sub = Math.Max(1, m_fs / 10);
            for (int i = 0; i < frames; i++)
            {
                double s = info.s0 + (info.s1 - info.s0) * (i / (double)Math.Max(1, frames));
                float l = x[2 * i], r = x[2 * i + 1];
                if (float.IsNaN(l) || float.IsInfinity(l) || float.IsNaN(r) || float.IsInfinity(r)) { NonFinite++; continue; }
                double e = (double)l * l + (double)r * r;
                double pk = Math.Max(Math.Abs(l), Math.Abs(r));
                if (pk > Peak) Peak = pk;
                if (InSwell(s)) { if (pk > PeakSwell) PeakSwell = pk; }
                else if (pk > PeakOutsideSwell) { PeakOutsideSwell = pk; PeakOutsideSwellS = s; }
                if (m_havePrev)
                {
                    double d = Math.Max(Math.Abs(l - m_pl), Math.Abs(r - m_pr));
                    if (d > MaxStep) { MaxStep = d; MaxStepS = s; }
                }
                m_pl = l; m_pr = r; m_havePrev = true;
                Sum[0] += l; Sum[1] += r;
                SumSqAll += e;
                Frames++;
                int b = ThreadMath.BeatIndex(Math.Min(s, ThreadConstants.PathLength - 1e-9));
                if (CountsForBeat(s, info.endingTime))
                {
                    BeatSumSq[b] += e; BeatFrames[b]++;
                    if (pk > BeatPeak[b]) BeatPeak[b] = pk;
                }
                if (s >= AfterS) { AfterSumSq += e; AfterFrames++; }
                // per second
                m_secSum += e; m_secN++;
                if (m_secN == 1) m_secS = s;
                if (m_secN >= m_fs) { RmsPerSecondDb.Add(10.0 * Math.Log10(Math.Max(1e-30, m_secSum / (2.0 * m_secN)))); SPerSecond.Add(m_secS); m_secSum = 0; m_secN = 0; }
                // momentary (400 ms) RMS
                m_subAcc += e;
                if (++m_subN >= sub)
                {
                    m_sub[m_subFill % 4] = m_subAcc / (2.0 * m_subN); m_subFill++; m_subAcc = 0; m_subN = 0;
                    if (m_subFill >= 4)
                    {
                        double ms = 0.25 * (m_sub[0] + m_sub[1] + m_sub[2] + m_sub[3]);
                        double db = 10.0 * Math.Log10(Math.Max(1e-30, ms));
                        if (db > MaxMomentaryRmsDb) { MaxMomentaryRmsDb = db; MaxMomentaryS = s; }
                    }
                }
                // K-weighted power for LUFS
                double kl = KTick(l, 0), kr = KTick(r, 4);
                m_kAcc += kl * kl + kr * kr;
                if (++m_kN >= sub) { m_kSub.Add(m_kAcc / m_kN); m_kAcc = 0; m_kN = 0; }
            }
        }

        public void End() { }

        double KTick(double x, int z)
        {
            // stage 1 (shelf) then stage 2 (RLB), transposed direct form II
            double y = m_kb[0] * x + m_kz[z]; m_kz[z] = m_kb[1] * x - m_ka[0] * y + m_kz[z + 1]; m_kz[z + 1] = m_kb[2] * x - m_ka[1] * y;
            double y2 = m_kb[3] * y + m_kz[z + 2]; m_kz[z + 2] = m_kb[4] * y - m_ka[2] * y2 + m_kz[z + 3]; m_kz[z + 3] = m_kb[5] * y - m_ka[3] * y2;
            return y2;
        }

        static void KWeighting(double fs, out double[] b, out double[] a)
        {
            // libebur128 formulas (R5 snippet B): reproduce the BS.1770 48 kHz table
            double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
            double K = Math.Tan(Math.PI * f0 / fs), Vh = Math.Pow(10.0, G / 20.0), Vb = Math.Pow(Vh, 0.4996667741545416);
            double a0 = 1.0 + K / Q + K * K;
            double sb0 = (Vh + Vb * K / Q + K * K) / a0, sb1 = 2.0 * (K * K - Vh) / a0, sb2 = (Vh - Vb * K / Q + K * K) / a0;
            double sa1 = 2.0 * (K * K - 1.0) / a0, sa2 = (1.0 - K / Q + K * K) / a0;
            f0 = 38.13547087602444; Q = 0.5003270373238773; K = Math.Tan(Math.PI * f0 / fs);
            a0 = 1.0 + K / Q + K * K;
            double ha1 = 2.0 * (K * K - 1.0) / a0, ha2 = (1.0 - K / Q + K * K) / a0;
            b = new[] { sb0, sb1, sb2, 1.0, -2.0, 1.0 };
            a = new[] { sa1, sa2, ha1, ha2 };
        }

        /// <summary>Integrated loudness (400 ms blocks, 75 percent overlap, gates -70 LUFS and -10 LU).</summary>
        public double IntegratedLufs()
        {
            var blocks = new List<double>();
            for (int i = 0; i + 4 <= m_kSub.Count; i++) blocks.Add(0.25 * (m_kSub[i] + m_kSub[i + 1] + m_kSub[i + 2] + m_kSub[i + 3]));
            double absSum = 0; int absN = 0;
            for (int i = 0; i < blocks.Count; i++) if (Lk(blocks[i]) > -70.0) { absSum += blocks[i]; absN++; }
            if (absN == 0) return double.NegativeInfinity;
            double rel = Lk(absSum / absN) - 10.0, sum = 0; int cnt = 0;
            for (int i = 0; i < blocks.Count; i++) { double lk = Lk(blocks[i]); if (lk > -70.0 && lk > rel) { sum += blocks[i]; cnt++; } }
            return cnt == 0 ? double.NegativeInfinity : Lk(sum / cnt);
        }
        static double Lk(double z) { return -0.691 + 10.0 * Math.Log10(Math.Max(z, 1e-30)); }

        /// <summary>Human-readable report (test logs, QA/audio/*.txt).</summary>
        public string Report()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            for (int b = 0; b < ThreadConstants.BeatCount; b++)
                sb.Append("beat ").Append(b + 1).Append(": RMS ").Append(BeatRmsDb(b).ToString("F2", ci)).Append(" dBFS (target ")
                  .Append(ThreadConstants.TargetRmsDb[b].ToString("F0", ci)).Append("), peak ").Append(Db(BeatPeak[b]).ToString("F2", ci))
                  .Append(" dBFS, ").Append((BeatFrames[b] / (double)Math.Max(1, m_fs)).ToString("F1", ci)).Append(" s measured\n");
            sb.Append("sample peak ").Append(Db(Peak).ToString("F2", ci)).Append(" dBFS; outside the swell ").Append(Db(PeakOutsideSwell).ToString("F2", ci))
              .Append(" dBFS at s ").Append(PeakOutsideSwellS.ToString("F1", ci)).Append("; in the swell ").Append(Db(PeakSwell).ToString("F2", ci)).Append(" dBFS\n");
            sb.Append("DC ").Append(Dc(0).ToString("E2", ci)).Append(" / ").Append(Dc(1).ToString("E2", ci))
              .Append("; max step ").Append(MaxStep.ToString("F5", ci)).Append(" at s ").Append(MaxStepS.ToString("F1", ci))
              .Append("; non-finite ").Append(NonFinite).Append('\n');
            sb.Append("max 400 ms RMS ").Append(MaxMomentaryRmsDb.ToString("F2", ci)).Append(" dBFS at s ").Append(MaxMomentaryS.ToString("F1", ci))
              .Append("; overall RMS ").Append(RmsDb.ToString("F2", ci)).Append(" dBFS; integrated ").Append(IntegratedLufs().ToString("F2", ci)).Append(" LUFS\n");
            if (AfterFrames > 0) sb.Append("RMS after s ").Append(AfterS.ToString("F1", ci)).Append(": ").Append(AfterRmsDb.ToString("F2", ci)).Append(" dBFS\n");
            return sb.ToString();
        }
    }

    /// <summary>Global stats of a buffer or a stream: peak, RMS, DC, largest sample step, non-finite count.</summary>
    public sealed class AudioMeter : IAudioBlockSink
    {
        public double Peak, SumSq, MaxStep;
        public readonly double[] Sum = new double[2];
        public long Frames;
        public int NonFinite;
        readonly float[] m_prev = new float[2];
        bool m_havePrev;

        public double RmsDb { get { return Frames > 0 ? 10.0 * Math.Log10(Math.Max(1e-30, SumSq / (2.0 * Frames))) : double.NegativeInfinity; } }
        public double PeakDb { get { return 20.0 * Math.Log10(Math.Max(1e-15, Peak)); } }
        public double Dc(int ch) { return Frames > 0 ? Sum[ch] / Frames : 0.0; }

        public void Begin(int sampleRate, int channels) { }
        public void End() { }
        public void Write(float[] x, int frames, in AudioBlockInfo info) { Add(x, frames, 2); }

        /// <summary>Adds interleaved frames (channels 0 and 1 are measured; mono counts as both).</summary>
        public void Add(float[] x, int frames, int channels)
        {
            for (int i = 0; i < frames; i++)
            {
                float l = x[i * channels], r = channels > 1 ? x[i * channels + 1] : l;
                if (float.IsNaN(l) || float.IsInfinity(l) || float.IsNaN(r) || float.IsInfinity(r)) { NonFinite++; continue; }
                double al = Math.Abs(l), ar = Math.Abs(r);
                if (al > Peak) Peak = al;
                if (ar > Peak) Peak = ar;
                SumSq += (double)l * l + (double)r * r;
                Sum[0] += l; Sum[1] += r;
                if (m_havePrev)
                {
                    double d0 = Math.Abs(l - m_prev[0]), d1 = Math.Abs(r - m_prev[1]);
                    if (d0 > MaxStep) MaxStep = d0;
                    if (d1 > MaxStep) MaxStep = d1;
                }
                m_prev[0] = l; m_prev[1] = r; m_havePrev = true;
                Frames++;
            }
        }
    }
}
