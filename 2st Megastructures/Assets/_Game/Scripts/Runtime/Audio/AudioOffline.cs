using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Megastructure
{
    /// <summary>
    /// Unity-free simulation of the walk for offline renders (Part 11.9): progress at 1.5 m/s (optionally with the 49 s of
    /// cinematic holds), the UI timeline (fade-in 0.6 -> 3.1 s, ending 3 s dwell + 4 s fade), the hatch, ark, sunset,
    /// river, beacon and footstep events, producing exactly the <see cref="AudioParams"/> the AudioDirector would.
    /// </summary>
    public sealed class AudioWalkSim
    {
        public bool Holds = true;
        public double Speed = AudioSpec.Walk;
        public double Accel = 0.75;              // m/s^2 when easing into / out of a cinematic hold
        public uint GustSeed = AudioParams.Default().GustSeed;

        public double T, S, V, Yaw;
        public double HatchT = -1, BeaconT0 = -1, GoalT = -1;
        public double T231 = double.NaN, T411 = double.NaN;
        public int Frame;
        public int StepsQueued;
        int _hold;
        double _holdLeft = -1;
        double _nextStrideS = AudioSpec.StrideWalk * 0.5;
        int _foot;
        readonly double[] _riverX, _riverZ;

        public AudioWalkSim(double startS = 0.0)
        {
            S = startS; T = 0; V = Speed;
            _hold = 0; while (_hold < AudioSpec.HoldS.Length && AudioSpec.HoldS[_hold] < startS - 1e-6) _hold++;
            _nextStrideS = startS + AudioSpec.StrideWalk * 0.5;
            // river centre extended along its end tangents to x = +-8 200 m (RingWorld.River)
            var rx = AudioSpec.RiverX; var rz = AudioSpec.RiverZ; int n = rx.Length;
            _riverX = new double[n + 2]; _riverZ = new double[n + 2];
            for (int i = 0; i < n; i++) { _riverX[i + 1] = rx[i]; _riverZ[i + 1] = rz[i]; }
            Ext(rx[0], rz[0], rx[1], rz[1], -8200.0, out _riverX[0], out _riverZ[0]);
            Ext(rx[n - 1], rz[n - 1], rx[n - 2], rz[n - 2], 8200.0, out _riverX[n + 1], out _riverZ[n + 1]);
            if (startS > AudioSpec.HatchTriggerS + 3) HatchT = -100;
        }

        static void Ext(double ax, double az, double bx, double bz, double toX, out double ox, out double oz)
        {
            double dx = ax - bx, dz = az - bz, l = Math.Sqrt(dx * dx + dz * dz); dx /= l; dz /= l;
            double k = (toX - ax) / (Math.Abs(dx) < 1e-6 ? 1e-6 : dx);
            ox = ax + dx * k; oz = az + dz * k;
        }

        // ---------------- path ----------------
        public static void PointAt(double s, out double x, out double z, out double dirX, out double dirZ)
        {
            var wx = AudioSpec.WaypointX; var wz = AudioSpec.WaypointZ;
            double acc = 0;
            for (int i = 1; i < wx.Length; i++)
            {
                double dx = wx[i] - wx[i - 1], dz = wz[i] - wz[i - 1], l = Math.Sqrt(dx * dx + dz * dz);
                if (s <= acc + l || i == wx.Length - 1)
                {
                    double t = DspMath.Clamp01((s - acc) / Math.Max(1e-9, l));
                    x = wx[i - 1] + dx * t; z = wz[i - 1] + dz * t; dirX = dx / l; dirZ = dz / l; return;
                }
                acc += l;
            }
            x = wx[0]; z = wz[0]; dirX = 0; dirZ = 1;
        }

        /// <summary>Bent-space range and azimuth (deg, + = right of yaw) from the listener to a point h above the floor
        /// at flat (px, pz) (14.7 H1: the floor curves up along Z with radius R).</summary>
        public static void BentRangeAz(double lx, double ly, double lz, double yawDeg, double px, double h, double pz, out double range, out double az)
        {
            double R = AudioSpec.RingR;
            double th = (pz - lz) / R;
            double dz = (R - h) * Math.Sin(th);
            double dy = R - (R - h) * Math.Cos(th) - ly;
            double dx = px - lx;
            range = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            az = WrapDeg(Math.Atan2(dx, dz) * (180.0 / Math.PI) - yawDeg);
        }

        public static double WrapDeg(double a) { a %= 360.0; if (a > 180.0) a -= 360.0; if (a < -180.0) a += 360.0; return a; }

        public void RiverAt(double x, double z, out double centreDist, out double az)
        {
            double best = double.MaxValue, bx = 0, bz = 0;
            for (int i = 0; i < _riverX.Length - 1; i++)
            {
                double ax = _riverX[i], azz = _riverZ[i], ex = _riverX[i + 1] - ax, ez = _riverZ[i + 1] - azz;
                double t = DspMath.Clamp01(((x - ax) * ex + (z - azz) * ez) / Math.Max(1e-9, ex * ex + ez * ez));
                double qx = ax + ex * t, qz = azz + ez * t, d = (qx - x) * (qx - x) + (qz - z) * (qz - z);
                if (d < best) { best = d; bx = qx; bz = qz; }
            }
            centreDist = Math.Sqrt(best);
            az = WrapDeg(Math.Atan2(bx - x, bz - z) * (180.0 / Math.PI) - Yaw);
        }

        /// <summary>Beat-3 felt cue envelope (centred at s 375, about 3 s, smootherstep in and out).</summary>
        public static double ArkCueAt(double s) => DspMath.SS(372.0, 373.5, s) * (1.0 - DspMath.SS(376.5, 378.0, s));

        public double SunsetU()
        {
            double p = (S - AudioSpec.SunsetS0) / AudioSpec.SunsetD;
            if (double.IsNaN(T411)) return p;
            return DspMath.Smax((T - T411) / AudioSpec.SunsetT, p);
        }

        public double ArkU()
        {
            if (double.IsNaN(T231)) return double.NegativeInfinity;
            return DspMath.Clamp01(DspMath.Smax((T - T231) / AudioSpec.ArkT, (S - AudioSpec.ArkTriggerS) / AudioSpec.ArkD));
        }

        // ---------------- stepping ----------------
        /// <summary>Advances the walk by dt, pushing footstep events (game-time stamped) into the engine queue.</summary>
        public void Step(double dt, RingAudioEngine eng)
        {
            double s0 = S, t0 = T;
            double target = Speed;
            double end = AudioSpec.BeatS[7];
            if (_holdLeft > 0)
            {
                target = 0; V = 0; _holdLeft -= dt;
                if (_holdLeft <= 0) { _holdLeft = -1; _hold++; }
            }
            else
            {
                double stopAt = end;
                if (Holds && _hold < AudioSpec.HoldS.Length) stopAt = Math.Min(stopAt, AudioSpec.HoldS[_hold]);
                double dist = stopAt - S;
                double vStop = Math.Sqrt(Math.Max(0.0, 2.0 * Accel * dist));
                target = Math.Min(Speed, vStop);
                if (V < target) V = Math.Min(target, V + Accel * dt); else V = target;
                if (dist <= 1e-3 || (V < 1e-3 && dist < 0.02))
                {
                    V = 0;
                    if (Holds && _hold < AudioSpec.HoldS.Length && stopAt == AudioSpec.HoldS[_hold]) _holdLeft = AudioSpec.HoldT[_hold];
                }
                else if (V < 0.05 && dist > 0.02) V = 0.05;
            }
            S = Math.Min(end, S + V * dt);
            T += dt;
            if (double.IsNaN(T231) && S >= AudioSpec.ArkTriggerS) T231 = Interp(t0, T, s0, S, AudioSpec.ArkTriggerS);
            if (double.IsNaN(T411) && S >= AudioSpec.SunsetS0) T411 = Interp(t0, T, s0, S, AudioSpec.SunsetS0);
            if (HatchT == -1 && S >= AudioSpec.HatchTriggerS) HatchT = Interp(t0, T, s0, S, AudioSpec.HatchTriggerS);
            if (BeaconT0 < 0 && S >= AudioSpec.BeaconFirstPingS) BeaconT0 = Interp(t0, T, s0, S, AudioSpec.BeaconFirstPingS);
            if (GoalT < 0 && S >= AudioSpec.GoalS) GoalT = Interp(t0, T, s0, S, AudioSpec.GoalS);
            // footsteps: one per stride (0.75 m at 1.5 m/s, 0.85 m at 2.5 m/s), alternate feet
            while (S >= _nextStrideS && _nextStrideS <= end)
            {
                double tc = Interp(t0, T, s0, S, _nextStrideS);
                if (eng != null)
                {
                    var e = new AudioEvent { Kind = AudioEventKind.Footstep, Surface = AudioSpec.SurfaceAt(_nextStrideS, false), Foot = _foot, Speed = (float)Math.Max(V, 0.5), T = tc };
                    if (eng.Events.TryEnqueue(e)) StepsQueued++;
                }
                _foot ^= 1;
                double stride = DspMath.Lerp(AudioSpec.StrideWalk, AudioSpec.StrideBrisk, DspMath.Clamp01((V - AudioSpec.Walk) / (AudioSpec.Brisk - AudioSpec.Walk)));
                _nextStrideS += stride;
            }
            if (V < 0.2 && _nextStrideS < S + 0.3) _nextStrideS = S + 0.3;   // first step after a hold comes after 0.3 m
        }

        static double Interp(double t0, double t1, double s0, double s1, double s)
        {
            if (s1 - s0 < 1e-9) return t1;
            return t0 + (t1 - t0) * DspMath.Clamp01((s - s0) / (s1 - s0));
        }

        /// <summary>Fills the params for the current instant (as the AudioDirector does every Update).</summary>
        public void Fill(ref AudioParams p)
        {
            PointAt(S, out double x, out double z, out double dx, out double dz);
            Yaw = Math.Atan2(dx, dz) * (180.0 / Math.PI);
            p.Frame = ++Frame;
            p.S = S; p.T = T; p.Speed = (float)V; p.Beat = AudioSpec.BeatAt(S);
            p.SunsetU = (float)SunsetU();
            p.GustSeed = GustSeed;
            p.PauseDuck = 0f;
            p.PictureFade = (float)DspMath.Smoothstep01((T - AudioSpec.FadeInStart) / (AudioSpec.FadeInEnd - AudioSpec.FadeInStart));
            p.EndingFade = GoalT >= 0 ? (float)(1.0 - DspMath.Smootherstep01((T - GoalT - AudioSpec.EndDwell) / AudioSpec.EndFade)) : 1f;
            p.HatchT = HatchT; p.BeaconT0 = BeaconT0;
            p.ArkCue = (float)ArkCueAt(S);
            double au = ArkU();
            if (double.IsNegativeInfinity(au)) { p.ArkRange = -1f; p.ArkAzimuth = 0f; }
            else
            {
                double xc = AudioSpec.ArkX0 + (AudioSpec.ArkX1 - AudioSpec.ArkX0) * au;
                BentRangeAz(x, AudioSpec.Eye, z, Yaw, xc, AudioSpec.ArkAltitude, AudioSpec.ArkTrackZ, out double rg, out double az);
                p.ArkRange = (float)rg; p.ArkAzimuth = (float)az;
            }
            RiverAt(x, z, out double cd, out double raz);
            p.RiverCentreDist = (float)cd; p.RiverDist = (float)Math.Max(0.0, cd - AudioSpec.RiverHalfWidth); p.RiverAzimuth = (float)raz;
        }

        public bool Finished => GoalT >= 0 && T > GoalT + AudioSpec.EndDwell + AudioSpec.EndFade + 1.5;
    }

    /// <summary>Streaming PCM WAV writer (16 or 24 bit, little-endian RIFF; sizes patched on close).</summary>
    public sealed class DspWavWriter : IDisposable
    {
        readonly FileStream _fs;
        readonly int _channels, _rate, _bytes;
        long _frames;
        readonly byte[] _buf = new byte[1 << 16];

        public DspWavWriter(string path, int channels, int sampleRate, int bits = 16)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            _fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            _channels = channels; _rate = sampleRate; _bytes = bits == 24 ? 3 : 2;
            WriteHeader(0);
        }

        void WriteHeader(long frames)
        {
            long data = frames * _channels * _bytes;
            var h = new byte[44];
            void Str(int o, string s) { for (int i = 0; i < 4; i++) h[o + i] = (byte)s[i]; }
            void I32(int o, int v) { h[o] = (byte)v; h[o + 1] = (byte)(v >> 8); h[o + 2] = (byte)(v >> 16); h[o + 3] = (byte)(v >> 24); }
            void I16(int o, int v) { h[o] = (byte)v; h[o + 1] = (byte)(v >> 8); }
            Str(0, "RIFF"); I32(4, (int)(36 + data + (data & 1))); Str(8, "WAVE");
            Str(12, "fmt "); I32(16, 16); I16(20, 1); I16(22, _channels); I32(24, _rate);
            I32(28, _rate * _channels * _bytes); I16(32, _channels * _bytes); I16(34, _bytes * 8);
            Str(36, "data"); I32(40, (int)data);
            _fs.Seek(0, SeekOrigin.Begin); _fs.Write(h, 0, 44);
            _fs.Seek(0, SeekOrigin.End);
        }

        /// <summary>Writes frames of interleaved float samples (channels must match the writer).</summary>
        public void Write(float[] x, int offsetFrames, int frames)
        {
            int n = frames * _channels, o = offsetFrames * _channels, p = 0;
            for (int i = 0; i < n; i++)
            {
                double v = x[o + i]; v = v > 1 ? 1 : (v < -1 ? -1 : v);
                if (_bytes == 2)
                {
                    int q = (int)Math.Round(v * 32767.0);
                    _buf[p++] = (byte)q; _buf[p++] = (byte)(q >> 8);
                }
                else
                {
                    int q = (int)Math.Round(v * 8388607.0);
                    _buf[p++] = (byte)q; _buf[p++] = (byte)(q >> 8); _buf[p++] = (byte)(q >> 16);
                }
                if (p > _buf.Length - 8) { _fs.Write(_buf, 0, p); p = 0; }
            }
            if (p > 0) _fs.Write(_buf, 0, p);
            _frames += frames;
        }

        public long Frames => _frames;

        public void Dispose()
        {
            if ((_frames * _channels * _bytes & 1) != 0) _fs.WriteByte(0);
            WriteHeader(_frames);
            _fs.Dispose();
        }

        /// <summary>One-shot float interleaved -> PCM file.</summary>
        public static void WriteFile(string path, float[] x, int frames, int channels, int rate, int bits = 16)
        {
            using (var w = new DspWavWriter(path, channels, rate, bits)) w.Write(x, 0, frames);
        }
    }

    /// <summary>
    /// Streaming analysis of the rendered output (14.3 "Master", 11.9, defect 25): per beat sample / true peak (4x
    /// oversampled where the sample peak exceeds -9 dBFS), DC, max sample step (the hatch sweep window excepted), NaN,
    /// clicks, and 0.1 s hop energies for the sliding 3 s RMS. Click detector: third difference d3 (+18 dB/oct, so the
    /// broadband energy of a discontinuity dominates while designed band-limited transients such as the 3 kHz rib tick
    /// do not), flagged when |d3| &gt; ClickAbs and |d3| &gt; ClickRatio x its centred 10 ms RMS.
    /// </summary>
    public sealed class AudioAnalyzer
    {
        public const double HopSec = 0.1;
        public const int WindowHops = 30;
        public readonly int Fs;
        readonly int _hopFrames;
        public readonly double[] Peak = new double[7], TruePeak = new double[7], MaxStep = new double[7];
        public readonly double[] SumL = new double[7], SumR = new double[7];
        public readonly long[] Frames = new long[7];
        public readonly int[] NaNs = new int[7], Clicks = new int[7];
        public const double ClickAbs = 0.005, ClickRatio = 8.0;
        public double ClickMaxRatio, ClickMaxT, ClickMaxS, ClickMaxAmp;
        public double FirstClickT = -1;

        // hops
        public double[] HopSum, HopS, HopT; public int[] HopBeat, HopFlags; public bool[] HopValid;
        public int HopCount;
        double _hopAcc; int _hopFill;

        // step / click state
        double _pl, _pr, _ppl, _ppr, _pppl, _pppr; int _havePrev;
        readonly double[] _d2Ring = new double[480]; readonly double[] _d2Val = new double[480]; int _d2i; double _d2Sum; long _nTotal;
        // true peak (4x polyphase windowed sinc, 16 taps per phase)
        readonly double[][] _tp;
        readonly double[] _histL = new double[32], _histR = new double[32]; int _hi;

        public AudioAnalyzer(int fs, double maxSeconds)
        {
            Fs = fs; _hopFrames = (int)Math.Round(HopSec * fs);
            int cap = (int)(maxSeconds / HopSec) + 8;
            HopSum = new double[cap]; HopS = new double[cap]; HopT = new double[cap]; HopBeat = new int[cap]; HopValid = new bool[cap]; HopFlags = new int[cap];
            _tp = TruePeakPhases(4, 16);
        }

        public static double[][] TruePeakPhases(int L, int tapsPerPhase)
        {
            int N = L * tapsPerPhase + 1; double c = (N - 1) / 2.0; var h = new double[N];
            for (int k = 0; k < N; k++)
            {
                double x = (k - c) / L, s = Math.Abs(x) < 1e-12 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);
                double a = 2 * Math.PI * k / (N - 1);
                h[k] = s * (0.35875 - 0.48829 * Math.Cos(a) + 0.14128 * Math.Cos(2 * a) - 0.01168 * Math.Cos(3 * a));
            }
            var ph = new double[L][];
            for (int p = 0; p < L; p++)
            {
                int cnt = (N - p + L - 1) / L; ph[p] = new double[cnt]; double sum = 0;
                for (int j = 0; j < cnt; j++) { ph[p][j] = h[p + j * L]; sum += ph[p][j]; }
                for (int j = 0; j < cnt; j++) ph[p][j] /= sum;
            }
            return ph;
        }

        /// <summary>Feeds a stereo block. stepExempt: inside the hatch sweep window. rmsValid: hop usable for the RMS check.</summary>
        public void Feed(float[] x, int frames, int beat, double s, double t, bool stepExempt, bool rmsValid) => Feed(x, frames, beat, s, t, stepExempt, rmsValid ? 0 : FlagBoundary);

        /// <summary>Hop flags: fade-in / ending, deliberate silence S1-S4, Rule-P boundary window.</summary>
        public const int FlagFade = 1, FlagSilence = 2, FlagBoundary = 4;

        public void Feed(float[] x, int frames, int beat, double s, double t, bool stepExempt, int flags)
        {
            bool rmsValid = flags == 0;
            if (beat < 0) beat = 0; if (beat > 6) beat = 6;
            double blockPeak = 0;
            for (int i = 0; i < frames; i++)
            {
                double l = x[2 * i], r = x[2 * i + 1];
                if (double.IsNaN(l) || double.IsNaN(r) || double.IsInfinity(l) || double.IsInfinity(r)) { NaNs[beat]++; l = 0; r = 0; }
                double al = Math.Abs(l), ar = Math.Abs(r);
                if (al > Peak[beat]) Peak[beat] = al; if (ar > Peak[beat]) Peak[beat] = ar;
                if (al > blockPeak) blockPeak = al; if (ar > blockPeak) blockPeak = ar;
                SumL[beat] += l; SumR[beat] += r; Frames[beat]++;
                if (_havePrev > 0 && !stepExempt)
                {
                    double st = Math.Max(Math.Abs(l - _pl), Math.Abs(r - _pr));
                    if (st > MaxStep[beat]) MaxStep[beat] = st;
                }
                // click detector: |d3| vs the centred 10 ms RMS of d3 (both channels, max)
                if (_havePrev >= 3)
                {
                    double d2 = Math.Max(Math.Abs(l - 3 * _pl + 3 * _ppl - _pppl), Math.Abs(r - 3 * _pr + 3 * _ppr - _pppr));
                    _d2Sum += d2 * d2 - _d2Ring[_d2i]; _d2Ring[_d2i] = d2 * d2; _d2Val[_d2i] = d2;
                    int mid = (_d2i + 240) % 480;           // the sample 240 frames ago is centred in the window
                    _d2i = (_d2i + 1) % 480;
                    if (_nTotal > 480)
                    {
                        double c = _d2Val[mid];
                        double rms = Math.Sqrt(Math.Max(1e-30, _d2Sum / 480.0));
                        if (c > ClickAbs && !stepExempt)
                        {
                            double ratio = c / rms;
                            if (ratio > ClickMaxRatio) { ClickMaxRatio = ratio; ClickMaxT = t; ClickMaxS = s; ClickMaxAmp = c; }
                            if (ratio > ClickRatio) { Clicks[beat]++; if (FirstClickT < 0) FirstClickT = t; }
                        }
                    }
                }
                _pppl = _ppl; _pppr = _ppr; _ppl = _pl; _ppr = _pr; _pl = l; _pr = r; if (_havePrev < 3) _havePrev++; _nTotal++;
                // true peak history
                _histL[_hi] = l; _histR[_hi] = r; _hi = (_hi + 1) & 31;
                if (blockPeak > 0.355) TruePeakAt(beat);
                // hops
                _hopAcc += l * l + r * r; _hopFill++;
                if (_hopFill >= _hopFrames)
                {
                    if (HopCount < HopSum.Length)
                    {
                        HopSum[HopCount] = _hopAcc; HopS[HopCount] = s; HopT[HopCount] = t; HopBeat[HopCount] = beat; HopValid[HopCount] = rmsValid; HopFlags[HopCount] = flags;
                        HopCount++;
                    }
                    _hopAcc = 0; _hopFill = 0;
                }
            }
            if (blockPeak <= 0.355) { if (blockPeak > TruePeak[beat]) TruePeak[beat] = blockPeak; }
        }

        void TruePeakAt(int beat)
        {
            for (int ch = 0; ch < 2; ch++)
            {
                var hist = ch == 0 ? _histL : _histR;
                for (int p = 0; p < _tp.Length; p++)
                {
                    var f = _tp[p]; double acc = 0;
                    for (int j = 0; j < f.Length; j++) acc += f[j] * hist[(_hi - 1 - j) & 31];
                    double a = Math.Abs(acc); if (a > TruePeak[beat]) TruePeak[beat] = a;
                }
            }
        }

        public double HopFrames => _hopFrames;

        /// <summary>RMS (dB) of the 3 s window starting at hop i.</summary>
        public double WindowDb(int i)
        {
            double acc = 0; for (int k = 0; k < WindowHops; k++) acc += HopSum[i + k];
            return 10.0 * Math.Log10(acc / (WindowHops * _hopFrames * 2.0) + 1e-30);
        }

        public bool WindowValid(int i)
        {
            if (i + WindowHops > HopCount) return false;
            int b = HopBeat[i];
            for (int k = 0; k < WindowHops; k++) if (!HopValid[i + k] || HopBeat[i + k] != b) return false;
            return true;
        }

        /// <summary>Largest change (dB) of the sliding 3 s RMS between consecutive 0.1 s hops over windows that contain no
        /// fade or deliberate silence (beat boundaries included): the "smooth transitions" check of 11.9.</summary>
        public double MaxRmsHopStepDb(out double atS)
        {
            double m = 0; atS = 0;
            for (int i = 0; i + WindowHops + 1 <= HopCount; i++)
            {
                bool ok = true;
                for (int k = 0; k <= WindowHops && ok; k++) if ((HopFlags[i + k] & (FlagFade | FlagSilence)) != 0) ok = false;
                if (!ok) continue;
                double d = Math.Abs(WindowDb(i + 1) - WindowDb(i));
                if (d > m) { m = d; atS = HopS[i + WindowHops / 2]; }
            }
            return m;
        }

        public struct BeatRms { public int Windows; public double MeanDev, MeanAbsDev, MaxAbsDev, Within, P05, P95, Median, MeanDb, Target; }

        public BeatRms[] RmsStats()
        {
            var res = new BeatRms[7];
            var devs = new double[HopCount + 1];
            for (int b = 0; b < 7; b++)
            {
                int n = 0; double sum = 0, sumAbs = 0, maxAbs = 0, within = 0, sumDb = 0, sumT = 0;
                for (int i = 0; i + WindowHops <= HopCount; i++)
                {
                    if (HopBeat[i] != b || !WindowValid(i)) continue;
                    double db = WindowDb(i);
                    double sc = HopS[i + WindowHops / 2];
                    double target = AudioSpec.TargetDb(b, sc);
                    double d = db - target;
                    devs[n++] = d; sum += d; sumAbs += Math.Abs(d); if (Math.Abs(d) > maxAbs) maxAbs = Math.Abs(d);
                    if (Math.Abs(d) <= AudioSpec.TargetToleranceDb) within++;
                    sumDb += db; sumT += target;
                }
                var r = new BeatRms { Windows = n };
                if (n > 0)
                {
                    Array.Sort(devs, 0, n);
                    r.MeanDev = sum / n; r.MeanAbsDev = sumAbs / n; r.MaxAbsDev = maxAbs; r.Within = within / n;
                    r.P05 = devs[(int)(0.05 * (n - 1))]; r.P95 = devs[(int)(0.95 * (n - 1))]; r.Median = devs[(n - 1) / 2];
                    r.MeanDb = sumDb / n; r.Target = sumT / n;
                }
                res[b] = r;
            }
            return res;
        }
    }

    /// <summary>One WAV excerpt of the continuous offline render (a window in game time, no splicing).</summary>
    public sealed class AudioExcerpt
    {
        public string Name;          // file name, e.g. "beat03.wav"
        public string Note;          // what the window shows
        public int Beat;             // 1..7 (0 = none)
        public double PlanT0, PlanT1;
        public double T0 = double.NaN, T1 = double.NaN, S0, S1;   // actual window (block aligned)
        public long Frames;
        internal DspWavWriter Writer;
        internal bool Done;
    }

    /// <summary>Options and result of one offline walk render.</summary>
    public sealed class AudioRenderResult
    {
        public double Seconds;
        public double Speed;
        public bool Holds;
        public double[] PeakDb = new double[7], TruePeakDb = new double[7], DcL = new double[7], DcR = new double[7], MaxStep = new double[7];
        public int[] NaNs = new int[7], Clicks = new int[7];
        public AudioAnalyzer.BeatRms[] Rms;
        public AudioAnalyzer Analyzer;
        public double LimiterMinGain;
        public double ArkRangeMin = double.MaxValue;
        public double ClickMaxRatio;
        public double MaxRmsHopStepDb, MaxRmsHopStepAtS;
        public double RenderMs;
        public int Pings, Ticks, Steps, StepsQueued;
        public double HatchT = -1, BeaconT0 = -1, GoalT = -1;
        /// <summary>RMS (dBFS) of the first and last 10 ms of the render (fade in with the picture, S5 fade out).</summary>
        public double FirstMsDb, LastMsDb;
        public List<AudioExcerpt> Excerpts = new List<AudioExcerpt>();
    }

    /// <summary>The offline renderer used by AudioTests and the trim fitter.</summary>
    public static class AudioOfflineRender
    {
        public const int BlockFrames = 1024;   // like the player's DSP buffer (21.3 ms at 48 kHz)
        /// <summary>Beat excerpts: from this long before the beat's first s (its Rule-P window and transition) ...</summary>
        public const double ExcerptPreRoll = 10.0;
        /// <summary>... to this long after it (20 s and more of the beat itself).</summary>
        public const double ExcerptPostRoll = 26.0;

        /// <summary>Hatch sweep window (the declared exception of the 0.25 sample-step rule, 14.3 Master).</summary>
        public static bool InHatchWindow(double t, double hatchT) => hatchT >= 0 && t >= hatchT - 0.05 && t <= hatchT + 1.6;

        /// <summary>
        /// Dry run of the walk (no audio) to find when the walker reaches the points that the excerpts are anchored to:
        /// returns the game time at which s first reaches each value of <paramref name="sPoints"/> (NaN if never), the
        /// time at which the sunset phase first reaches 0.55 (S2) and the end time of the render.
        /// </summary>
        public static double[] Timeline(bool holds, double speed, int rate, double[] sPoints, out double tTotality, out double tEnd)
        {
            var sim = new AudioWalkSim { Holds = holds, Speed = speed };
            var p = AudioParams.Default();
            double dt = (double)BlockFrames / rate;
            var res = new double[sPoints.Length];
            for (int i = 0; i < res.Length; i++) res[i] = double.NaN;
            tTotality = double.NaN;
            while (!sim.Finished && sim.T < 1000)
            {
                sim.Fill(ref p);
                for (int i = 0; i < res.Length; i++) if (double.IsNaN(res[i]) && sim.S >= sPoints[i]) res[i] = sim.T;
                if (double.IsNaN(tTotality) && p.SunsetU >= 0.55f) tTotality = sim.T;
                sim.Step(dt, null);
            }
            tEnd = sim.T;
            return res;
        }

        /// <summary>The excerpt plan: beatNN.wav for each beat (its entry transition and >= 20 s of the beat; beat 1 from
        /// the black start with S1, beat 7 to the end of the S5 fade), the mid-beat silences S2 and S3; walk.wav (1 s of
        /// every 5 m) is written separately.</summary>
        public static List<AudioExcerpt> Plan(bool holds, double speed, int rate)
        {
            var b = AudioSpec.BeatS;
            var pts = new[] { b[1], b[2], b[3], b[4], b[5], b[6], 831.0, 837.0 };
            var tt = Timeline(holds, speed, rate, pts, out double tTot, out double tEnd);
            var plan = new List<AudioExcerpt>();
            plan.Add(new AudioExcerpt { Name = "beat01.wav", Beat = 1, PlanT0 = 0, PlanT1 = ExcerptPreRoll + ExcerptPostRoll, Note = "black start, fade-in with the picture, C1, hatch + S1, wind rising, 1 -> 2" });
            string[] notes = { null, null, "2 -> 3 (reverb 1.2 -> 4.0 s, A1 in), the ark", "3 -> 4 (wind falls, P1 attack), sunset", "4 -> 5 (underpass, U1 out, H1 in, wall echo)", "5 -> 6 (door, S1 out, T1 in, rib ticks)" };
            for (int k = 2; k <= 6; k++)
            {
                double t = tt[k - 2];
                if (double.IsNaN(t)) continue;
                string note = k == 2 ? "1 -> 2 (gangway, the valley), W1 G1 S1" : notes[k - 1];
                plan.Add(new AudioExcerpt { Name = string.Format(CultureInfo.InvariantCulture, "beat{0:00}.wav", k), Beat = k, PlanT0 = Math.Max(0, t - ExcerptPreRoll), PlanT1 = t + ExcerptPostRoll, Note = note });
            }
            if (!double.IsNaN(tt[5]))
                plan.Add(new AudioExcerpt { Name = "beat07.wav", Beat = 7, PlanT0 = Math.Max(0, tt[5] - ExcerptPreRoll), PlanT1 = double.PositiveInfinity, Note = "tunnel exit + S4, X1 swell, B1 pings, goal, S5 ending fade" });
            if (!double.IsNaN(tTot))
                plan.Add(new AudioExcerpt { Name = "s2_totality.wav", Beat = 4, PlanT0 = tTot - 4.0, PlanT1 = tTot + 12.0, Note = "S2 totality (u 0.55): wind -12 dB, pad to -60 dBFS for 4 s" });
            if (!double.IsNaN(tt[6]) && !double.IsNaN(tt[7]))
                plan.Add(new AudioExcerpt { Name = "s3_tunnel.wav", Beat = 6, PlanT0 = tt[6] - 4.0, PlanT1 = tt[7] + 6.0, Note = "S3 tunnel s 831-837: only footsteps and tails" });
            return plan;
        }

        /// <summary>
        /// Renders the whole walk (start black -> goal -> ending fade) continuously at <paramref name="speed"/> (with the
        /// 49 s of cinematic holds when <paramref name="holds"/>), analysing every sample. When outDir is not null it
        /// writes the excerpts of <see cref="Plan"/> and walk.wav (1 s of every 5 m, 5 ms splice fades) as 16-bit WAVs.
        /// </summary>
        public static AudioRenderResult RenderWalk(string outDir, bool holds = true, double[] trimOverride = null, int rate = 48000,
                                                   Action<string> log = null, double speed = AudioSpec.Walk, uint gustSeed = 0u)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var eng = new RingAudioEngine(rate, 1u);
            if (trimOverride != null) eng.TrimKnotDb = (double[])trimOverride.Clone();
            var sim = new AudioWalkSim { Holds = holds, Speed = speed };
            if (gustSeed != 0u) sim.GustSeed = gustSeed;     // another W1 gust realisation (the player's comes from -seed)
            var an = new AudioAnalyzer(rate, 1000);
            var res = new AudioRenderResult { Analyzer = an, Speed = speed, Holds = holds };
            var buf = new float[BlockFrames * 2];
            var p = AudioParams.Default();
            double dt = (double)BlockFrames / rate;
            if (outDir != null) res.Excerpts = Plan(holds, speed, rate);
            DspWavWriter walkWav = outDir != null ? new DspWavWriter(Path.Combine(outDir, "walk.wav"), 2, rate) : null;
            int walkLeft = 0, walkTotal = rate; int fade = (int)(0.005 * rate);
            double nextWalkS = 0.0;
            var tmp = new float[BlockFrames * 2];
            int tenMs = rate / 100;
            double firstAcc = 0; int firstN = 0;
            var lastRing = new double[tenMs]; int lastI = 0, lastN = 0;
            try
            {
                while (!sim.Finished && sim.T < 1000)
                {
                    sim.Fill(ref p);
                    if (p.ArkRange > 0 && p.ArkRange < res.ArkRangeMin) res.ArkRangeMin = p.ArkRange;
                    eng.Render(buf, BlockFrames, 2, rate, in p);
                    int beat = AudioSpec.BeatAt(sim.S);
                    bool stepExempt = InHatchWindow(sim.T, sim.HatchT);
                    an.Feed(buf, BlockFrames, beat, sim.S, sim.T, stepExempt, RmsFlags(sim, p));
                    for (int i = 0; i < BlockFrames; i++)
                    {
                        double e = 0.5 * ((double)buf[2 * i] * buf[2 * i] + (double)buf[2 * i + 1] * buf[2 * i + 1]);
                        if (firstN < tenMs) { firstAcc += e; firstN++; }
                        lastRing[lastI] = e; lastI = (lastI + 1) % tenMs; if (lastN < tenMs) lastN++;
                    }
                    if (outDir != null)
                    {
                        foreach (var ex in res.Excerpts)
                        {
                            if (ex.Done) continue;
                            if (ex.Writer == null && sim.T + 1e-9 >= ex.PlanT0)
                            {
                                ex.Writer = new DspWavWriter(Path.Combine(outDir, ex.Name), 2, rate);
                                ex.T0 = sim.T; ex.S0 = sim.S;
                            }
                            if (ex.Writer != null)
                            {
                                ex.Writer.Write(buf, 0, BlockFrames);
                                ex.Frames += BlockFrames; ex.T1 = sim.T + dt; ex.S1 = sim.S;
                                if (sim.T + dt >= ex.PlanT1) { ex.Writer.Dispose(); ex.Writer = null; ex.Done = true; }
                            }
                        }
                        // walk.wav: 1 s of every 5 m
                        if (walkLeft <= 0 && sim.S >= nextWalkS) { walkLeft = walkTotal; nextWalkS = Math.Floor(sim.S / 5.0) * 5.0 + 5.0; }
                        if (walkLeft > 0)
                        {
                            int nCopy = Math.Min(BlockFrames, walkLeft);
                            for (int i = 0; i < nCopy; i++)
                            {
                                int k = walkTotal - walkLeft + i;
                                double g = DspMath.Clamp01(Math.Min(k / (double)fade, (walkTotal - 1 - k) / (double)fade));
                                tmp[2 * i] = (float)(buf[2 * i] * g); tmp[2 * i + 1] = (float)(buf[2 * i + 1] * g);
                            }
                            walkWav.Write(tmp, 0, nCopy);
                            walkLeft -= nCopy;
                        }
                    }
                    sim.Step(dt, eng);
                }
            }
            finally
            {
                foreach (var ex in res.Excerpts) if (ex.Writer != null) { ex.Writer.Dispose(); ex.Writer = null; ex.Done = true; }
                walkWav?.Dispose();
            }
            res.Seconds = sim.T;
            for (int b = 0; b < 7; b++)
            {
                res.PeakDb[b] = DspMath.LinToDb(an.Peak[b]);
                res.TruePeakDb[b] = DspMath.LinToDb(Math.Max(an.TruePeak[b], an.Peak[b]));
                res.DcL[b] = an.Frames[b] > 0 ? an.SumL[b] / an.Frames[b] : 0;
                res.DcR[b] = an.Frames[b] > 0 ? an.SumR[b] / an.Frames[b] : 0;
                res.MaxStep[b] = an.MaxStep[b]; res.NaNs[b] = an.NaNs[b]; res.Clicks[b] = an.Clicks[b];
            }
            res.Rms = an.RmsStats();
            res.LimiterMinGain = eng.LimiterMinGain;
            res.ClickMaxRatio = an.ClickMaxRatio;
            res.MaxRmsHopStepDb = an.MaxRmsHopStepDb(out res.MaxRmsHopStepAtS);
            res.Pings = eng.PingCount; res.Ticks = eng.TickCount; res.Steps = eng.StepCount; res.StepsQueued = sim.StepsQueued;
            res.HatchT = sim.HatchT; res.BeaconT0 = sim.BeaconT0; res.GoalT = sim.GoalT;
            double lastAcc = 0; for (int i = 0; i < lastN; i++) lastAcc += lastRing[i];
            res.FirstMsDb = 10.0 * Math.Log10(firstAcc / Math.Max(1, firstN) + 1e-30);
            res.LastMsDb = 10.0 * Math.Log10(lastAcc / Math.Max(1, lastN) + 1e-30);
            res.RenderMs = sw.Elapsed.TotalMilliseconds;
            log?.Invoke(string.Format(CultureInfo.InvariantCulture, "[Ring] Audio offline walk ({0:0.0} m/s{1}): {2:0.0} s rendered in {3:0} ms",
                speed, holds ? ", holds" : "", res.Seconds, res.RenderMs));
            return res;
        }

        /// <summary>Declared windows excluded from the per-beat RMS targets: fade-in, the deliberate silences S1-S5, the
        /// 24 m Rule-P boundary windows (checked for smoothness instead), and everything after the goal.</summary>
        public static bool RmsValid(AudioWalkSim sim, in AudioParams p) => RmsFlags(sim, p) == 0;

        public static int RmsFlags(AudioWalkSim sim, in AudioParams p)
        {
            double s = sim.S, t = sim.T;
            int f = 0;
            if (t < AudioSpec.FadeInEnd + 0.5) f |= AudioAnalyzer.FlagFade;
            if (sim.GoalT >= 0 || s >= 958.0) f |= AudioAnalyzer.FlagFade;                                             // S5
            if (sim.HatchT >= 0 && t >= sim.HatchT - 0.1 && t <= sim.HatchT + 6.0) f |= AudioAnalyzer.FlagSilence;    // S1
            double e = (p.SunsetU - 0.55) * AudioSpec.SunsetT; if (e > -0.5 && e < 8.5) f |= AudioAnalyzer.FlagSilence; // S2
            if (s >= 829.5 && s <= 840.0) f |= AudioAnalyzer.FlagSilence;                                             // S3
            if (s >= 892.0 && s <= 913.0) f |= AudioAnalyzer.FlagSilence;                                             // S4
            for (int b = 1; b <= 6; b++) if (Math.Abs(s - AudioSpec.BeatS[b]) < 12.0) f |= AudioAnalyzer.FlagBoundary; // Rule-P windows
            return f;
        }

        /// <summary>
        /// Iteratively fits the trim knots so the sliding 3 s RMS of each beat sits on its target: each knot moves by the
        /// mean deviation of the windows weighted by its linear-interpolation (hat) weight at the window centre.
        /// </summary>
        public static double[] FitTrims(int iterations, bool holds, Action<string> log)
        {
            var knots = (double[])AudioTrims.KnotDb.Clone();
            var ks = AudioTrims.KnotS; var kb = AudioTrims.KnotBeat;
            for (int it = 0; it < iterations; it++)
            {
                var res = RenderWalk(null, holds, knots);
                var an = res.Analyzer;
                var num = new double[knots.Length]; var den = new double[knots.Length];
                var bsum = new double[7]; var bn = new int[7];
                for (int i = 0; i + AudioAnalyzer.WindowHops <= an.HopCount; i++)
                {
                    int b = an.HopBeat[i];
                    if (!an.WindowValid(i)) continue;
                    double sc = an.HopS[i + AudioAnalyzer.WindowHops / 2];
                    double d = an.WindowDb(i) - AudioSpec.TargetDb(b, sc);
                    bsum[b] += d; bn[b]++;
                    for (int k = 0; k < knots.Length; k++)
                    {
                        if (kb[k] != b) continue;
                        double w = HatWeight(k, sc);
                        if (w <= 0) continue;
                        num[k] += w * d; den[k] += w;
                    }
                }
                var sb = new StringBuilder();
                for (int k = 0; k < knots.Length; k++)
                {
                    double d = den[k] > 1e-6 ? num[k] / den[k] : (bn[kb[k]] > 0 ? bsum[kb[k]] / bn[kb[k]] : 0);
                    knots[k] -= 0.9 * d;
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.00} ", knots[k]);
                }
                if (log != null)
                {
                    var r = res.Rms;
                    var sr = new StringBuilder();
                    for (int b = 0; b < 7; b++) sr.AppendFormat(CultureInfo.InvariantCulture, "B{0} {1:+0.00;-0.00} [{2:+0.0;-0.0},{3:+0.0;-0.0}] {4:0}% | ", b + 1, r[b].MeanDev, r[b].P05, r[b].P95, 100 * r[b].Within);
                    log("fit " + it + ": " + sr);
                    log("knots: " + sb);
                }
            }
            return knots;
        }

        static double HatWeight(int k, double s)
        {
            var ks = AudioTrims.KnotS; var kb = AudioTrims.KnotBeat;
            double x = ks[k];
            double left = (k > 0 && kb[k - 1] == kb[k]) ? ks[k - 1] : double.NaN;
            double right = (k < ks.Length - 1 && kb[k + 1] == kb[k]) ? ks[k + 1] : double.NaN;
            if (s <= x)
            {
                if (double.IsNaN(left)) return 1.0;     // before the first knot of the beat: full weight
                return s <= left ? 0.0 : (s - left) / (x - left);
            }
            if (double.IsNaN(right)) return 1.0;
            return s >= right ? 0.0 : (right - s) / (right - x);
        }

        // ---------------- reports ----------------
        public static string ReportJson(AudioRenderResult r, string extra = null)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.AppendFormat(ci, "  \"sampleRate\": {0},\n  \"speed\": {1:0.00},\n  \"cinematicHolds\": {2},\n  \"seconds\": {3:0.000},\n  \"renderMs\": {4:0},\n",
                r.Analyzer.Fs, r.Speed, r.Holds ? "true" : "false", r.Seconds, r.RenderMs);
            sb.Append("  \"rmsDefinition\": \"10*log10(mean(x^2)) over both channels, no +3.01 dB AES17 offset; sliding 3 s window, 0.1 s hop; windows touching the fades, the deliberate silences S1-S5 or a 24 m Rule-P boundary window are excluded from the per-beat targets\",\n");
            sb.AppendFormat(ci, "  \"limiterMinGain\": {0:0.0000},\n  \"arkRangeMin\": {1:0.0},\n  \"clickMaxRatio\": {2:0.00},\n  \"maxRmsHopStepDb\": {3:0.000},\n  \"maxRmsHopStepAtS\": {4:0.0},\n", r.LimiterMinGain, r.ArkRangeMin, r.ClickMaxRatio, r.MaxRmsHopStepDb, r.MaxRmsHopStepAtS);
            sb.AppendFormat(ci, "  \"events\": {{\"footstepsQueued\": {0}, \"footstepsStarted\": {1}, \"ribTicks\": {2}, \"beaconPings\": {3}, \"hatchT\": {4:0.000}, \"beaconT0\": {5:0.000}, \"goalT\": {6:0.000}}},\n",
                r.StepsQueued, r.Steps, r.Ticks, r.Pings, r.HatchT, r.BeaconT0, r.GoalT);
            sb.AppendFormat(ci, "  \"first10msRmsDb\": {0:0.0},\n  \"last10msRmsDb\": {1:0.0},\n", Math.Max(-200, r.FirstMsDb), Math.Max(-200, r.LastMsDb));
            sb.Append("  \"beats\": [\n");
            for (int b = 0; b < 7; b++)
            {
                var q = r.Rms[b];
                sb.AppendFormat(ci,
                    "    {{\"beat\": {0}, \"peakDbfs\": {1:0.00}, \"truePeakDbfs\": {2:0.00}, \"dcL\": {3:0.000000}, \"dcR\": {4:0.000000}, \"maxStep\": {5:0.00000}, \"nan\": {6}, \"clicks\": {7}, " +
                    "\"rmsWindows\": {8}, \"rmsMeanDb\": {9:0.00}, \"targetDb\": {10:0.00}, \"meanDevDb\": {11:0.00}, \"medianDevDb\": {12:0.00}, \"p05DevDb\": {13:0.00}, \"p95DevDb\": {14:0.00}, \"maxAbsDevDb\": {15:0.00}, \"withinTol\": {16:0.000}}}{17}\n",
                    b + 1, r.PeakDb[b], r.TruePeakDb[b], r.DcL[b], r.DcR[b], r.MaxStep[b], r.NaNs[b], r.Clicks[b],
                    q.Windows, q.MeanDb, q.Target, q.MeanDev, q.Median, q.P05, q.P95, q.MaxAbsDev, q.Within, b < 6 ? "," : "");
            }
            sb.Append("  ],\n  \"files\": [");
            for (int i = 0; i < r.Excerpts.Count; i++)
            {
                var e = r.Excerpts[i];
                sb.AppendFormat(ci, "{0}\n    {{\"file\": \"{1}\", \"beat\": {2}, \"t0\": {3:0.000}, \"t1\": {4:0.000}, \"s0\": {5:0.00}, \"s1\": {6:0.00}, \"frames\": {7}, \"note\": \"{8}\"}}",
                    i == 0 ? "" : ",", e.Name, e.Beat, e.T0, e.T1, e.S0, e.S1, e.Frames, e.Note);
            }
            sb.Append(r.Excerpts.Count > 0 ? "\n  ],\n  \"rmsCurve\": [" : "],\n  \"rmsCurve\": [");
            var an = r.Analyzer; bool first = true;
            for (int i = 0; i + AudioAnalyzer.WindowHops <= an.HopCount; i += 10)
            {
                sb.AppendFormat(ci, "{0}[{1:0.0}, {2:0.00}, {3:0.00}, {4}]", first ? "" : ", ", an.HopT[i + 15], an.HopS[i + 15], an.WindowDb(i), an.WindowValid(i) ? 1 : 0);
                first = false;
            }
            sb.Append("]");
            if (extra != null) sb.Append(",\n").Append(extra);
            sb.Append("\n}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Side-car for Tools/audio_analysis.py (QA/audio/excerpts.json): per WAV excerpt its game-time window, the hatch
        /// sweep window that is exempt from the 0.25 step rule (file-relative seconds) and the 0.1 s hops that fall into
        /// it (file-relative end time, s, beat, target dB, flags: 1 fade, 2 deliberate silence, 4 Rule-P window).
        /// </summary>
        public static string ExcerptsJson(AudioRenderResult r)
        {
            var ci = CultureInfo.InvariantCulture;
            var an = r.Analyzer;
            var sb = new StringBuilder();
            sb.AppendFormat(ci, "{{\n  \"sampleRate\": {0},\n  \"hopSec\": {1},\n  \"windowSec\": 3.0,\n  \"toleranceDb\": {2},\n  \"maxStep\": {3},\n  \"maxDc\": {4},\n  \"peakCeilingDbfs\": {5},\n  \"files\": [",
                an.Fs, AudioAnalyzer.HopSec, AudioSpec.TargetToleranceDb, AudioSpec.MaxStep, AudioSpec.MaxDc, AudioSpec.PeakCeilingDbfs);
            for (int i = 0; i < r.Excerpts.Count; i++)
            {
                var e = r.Excerpts[i];
                sb.AppendFormat(ci, "{0}\n    {{\"file\": \"{1}\", \"beat\": {2}, \"t0\": {3:0.0000}, \"t1\": {4:0.0000}, \"note\": \"{5}\", \"stepExempt\": [",
                    i == 0 ? "" : ",", e.Name, e.Beat, e.T0, e.T1, e.Note);
                if (r.HatchT >= 0 && r.HatchT + 1.6 > e.T0 && r.HatchT - 0.05 < e.T1)
                    sb.AppendFormat(ci, "[{0:0.000}, {1:0.000}]", Math.Max(0, r.HatchT - 0.05 - e.T0), r.HatchT + 1.6 - e.T0);
                sb.Append("], \"hops\": [");
                bool first = true;
                for (int h = 0; h < an.HopCount; h++)
                {
                    double t = an.HopT[h];
                    if (t < e.T0 || t > e.T1) continue;
                    int b = an.HopBeat[h];
                    sb.AppendFormat(ci, "{0}[{1:0.000}, {2:0.00}, {3}, {4:0.00}, {5}]", first ? "" : ", ", t - e.T0, an.HopS[h], b + 1, AudioSpec.TargetDb(b, an.HopS[h]), an.HopFlags[h]);
                    first = false;
                }
                sb.Append("]}");
            }
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>QA/audio-trims.json (14.3 Master): the trim knots, the targets and, when a render is given, what the
        /// trims achieved per beat on it.</summary>
        public static string TrimsJson(double[] knotDb, AudioRenderResult measured = null)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n  \"description\": \"Per-beat trims (MASTER_PROMPT 14.3 Master): four knots per beat over its steady region (seven in beat 2, six in beat 4), linear inside a beat, smootherstep across the 24 m boundary windows; constants in AudioTrims.KnotDb, fitted on the offline render (cinematic walk, 1.5 m/s, 48 kHz). Applied to every layer except footsteps and the hatch (absolute -30 dBFS peaks). The W1 gust gain 0.35 + 0.65 smooth-random(0.15 Hz) (and the beat-5 laminar 0.70 + 0.30 smooth-random) is scaled by (its long-run RMS / its centred 3 s RMS)^exponent (RingAudioEngine.GustComp, w1GustCompensation below) so every 3 s window stays within the tolerance.\",\n");
            sb.AppendFormat(ci, "  \"w1GustCompensation\": {{\"exponent\": {0:0.00}, \"windowSec\": {1:0.0}, \"taps\": {2}, \"gridSec\": {3:0.0}, \"gustGainRms\": {4:0.0000}}},\n",
                RingAudioEngine.GustCompExponent, 2 * RingAudioEngine.GustCompHalf * RingAudioEngine.GustCompStep, 2 * RingAudioEngine.GustCompHalf + 1,
                RingAudioEngine.GustCompStep, RingAudioEngine.GustGainRms);
            sb.Append("  \"rmsDefinition\": \"10*log10(mean(x^2)) over both channels (no +3.01 dB offset), sliding 3 s\",\n  \"knots\": [\n");
            for (int k = 0; k < knotDb.Length; k++)
                sb.AppendFormat(ci, "    {{\"beat\": {0}, \"s\": {1:0.0}, \"trimDb\": {2:0.00}}}{3}\n", AudioTrims.KnotBeat[k] + 1, AudioTrims.KnotS[k], knotDb[k], k < knotDb.Length - 1 ? "," : "");
            sb.Append("  ],\n  \"targets\": [");
            for (int b = 0; b < 7; b++) sb.AppendFormat(ci, "{0}{{\"beat\": {1}, \"startDb\": {2}, \"endDb\": {3}}}", b == 0 ? "" : ", ", b + 1, AudioSpec.TargetStartDb[b], AudioSpec.TargetEndDb[b]);
            sb.Append("],\n  \"toleranceDb\": 1.5");
            if (measured != null && measured.Rms != null)
            {
                sb.Append(",\n  \"measured\": [");
                for (int b = 0; b < 7; b++)
                {
                    var q = measured.Rms[b];
                    sb.AppendFormat(ci, "{0}\n    {{\"beat\": {1}, \"windows\": {2}, \"meanDb\": {3:0.00}, \"targetDb\": {4:0.00}, \"meanDevDb\": {5:0.00}, \"p05DevDb\": {6:0.00}, \"p95DevDb\": {7:0.00}, \"maxAbsDevDb\": {8:0.00}, \"withinTol\": {9:0.000}}}",
                        b == 0 ? "" : ",", b + 1, q.Windows, q.MeanDb, q.Target, q.MeanDev, q.P05, q.P95, q.MaxAbsDev, q.Within);
                }
                sb.Append("\n  ]");
            }
            sb.Append("\n}\n");
            return sb.ToString();
        }
    }
}
