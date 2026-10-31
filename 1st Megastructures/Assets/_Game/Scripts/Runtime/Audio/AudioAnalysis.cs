using System;

namespace Megastructure.Audio
{
    /// <summary>
    /// Offline measurements on interleaved stereo float buffers (Part 10 item 25, 11.9): sample peak, true peak (4x
    /// oversampled, windowed-sinc interpolation), largest sample-to-sample step, DC offset and the RMS curve in 0.4 s
    /// windows. Used by the EditMode audio tests, the player audio dump log and the trim measurement. Tools/audio_analyze.py
    /// implements the same metrics for WAV files.
    /// </summary>
    public static class AudioAnalysis
    {
        public struct Report
        {
            public double PeakDb;
            public double TruePeakDb;
            public double MaxStep;
            public double DcL, DcR;
            public double RmsDb;
            public double MinWindowDb, MaxWindowDb, MedianWindowDb;
            public int Windows;
        }

        public static Report Analyze(float[] stereo, int sampleRate, double windowSeconds = AudioMix.RmsWindowSeconds)
        {
            var r = new Report();
            int frames = stereo.Length / 2;
            double peak = 0.0, step = 0.0, sl = 0.0, sr = 0.0, pw = 0.0;
            float pl = 0f, prv = 0f;
            for (int i = 0; i < frames; i++)
            {
                float l = stereo[2 * i], rr = stereo[2 * i + 1];
                double al = Math.Abs(l), ar = Math.Abs(rr);
                if (al > peak) peak = al;
                if (ar > peak) peak = ar;
                if (i > 0)
                {
                    double dl = Math.Abs(l - pl), dr = Math.Abs(rr - prv);
                    if (dl > step) step = dl;
                    if (dr > step) step = dr;
                }
                pl = l; prv = rr;
                sl += l; sr += rr;
                pw += (double)l * l + (double)rr * rr;
            }
            r.PeakDb = DspMath.LinToDb(peak);
            r.TruePeakDb = DspMath.LinToDb(TruePeak(stereo));
            r.MaxStep = step;
            r.DcL = frames > 0 ? sl / frames : 0.0;
            r.DcR = frames > 0 ? sr / frames : 0.0;
            r.RmsDb = frames > 0 ? 10.0 * Math.Log10(Math.Max(pw / (2.0 * frames), 1e-30)) : -300.0;
            double[] w = WindowRmsDb(stereo, sampleRate, windowSeconds);
            r.Windows = w.Length;
            if (w.Length > 0)
            {
                double mn = double.MaxValue, mx = double.MinValue;
                for (int i = 0; i < w.Length; i++) { if (w[i] < mn) mn = w[i]; if (w[i] > mx) mx = w[i]; }
                r.MinWindowDb = mn; r.MaxWindowDb = mx;
                r.MedianWindowDb = Median(w);
            }
            return r;
        }

        /// <summary>RMS (both channels, power mean) of consecutive non-overlapping windows, in dBFS.</summary>
        public static double[] WindowRmsDb(float[] stereo, int sampleRate, double windowSeconds)
        {
            int frames = stereo.Length / 2;
            int win = Math.Max(1, (int)Math.Round(windowSeconds * sampleRate));
            int count = frames / win;
            var res = new double[count];
            for (int k = 0; k < count; k++)
            {
                double s = 0.0;
                int a = k * win * 2, b = a + win * 2;
                for (int i = a; i < b; i++) s += (double)stereo[i] * stereo[i];
                res[k] = 10.0 * Math.Log10(Math.Max(s / (2.0 * win), 1e-30));
            }
            return res;
        }

        public static double Median(double[] v)
        {
            if (v.Length == 0) return double.NaN;
            var c = (double[])v.Clone();
            Array.Sort(c);
            int m = c.Length / 2;
            return (c.Length & 1) == 1 ? c[m] : 0.5 * (c[m - 1] + c[m]);
        }

        // 4x oversampling interpolator: Blackman-windowed sinc, 12 taps per phase (ITU-R BS.1770-style estimate).
        const int Taps = 12;
        static readonly double[][] Phases = BuildPhases();

        static double[][] BuildPhases()
        {
            var ph = new double[3][];
            for (int p = 1; p <= 3; p++)
            {
                double frac = p / 4.0;
                var c = new double[Taps];
                double sum = 0.0;
                for (int k = 0; k < Taps; k++)
                {
                    double x = k - (Taps / 2 - 1) - frac;   // sample offsets -5..+6 around the interpolation point
                    double sinc = Math.Abs(x) < 1e-12 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
                    double wpos = (x + Taps / 2.0) / Taps;   // 0..1 across the window
                    double win = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * wpos) + 0.08 * Math.Cos(4.0 * Math.PI * wpos);
                    c[k] = sinc * win;
                    sum += c[k];
                }
                for (int k = 0; k < Taps; k++) c[k] /= sum;
                ph[p - 1] = c;
            }
            return ph;
        }

        /// <summary>Linear true-peak estimate: max over the samples and three interpolated points between each pair.</summary>
        public static double TruePeak(float[] stereo)
        {
            int frames = stereo.Length / 2;
            double peak = 0.0;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < frames; i++)
                {
                    double a = Math.Abs(stereo[2 * i + ch]);
                    if (a > peak) peak = a;
                    int start = i - (Taps / 2 - 1);
                    if (start < 0 || start + Taps > frames) continue;
                    for (int p = 0; p < 3; p++)
                    {
                        double[] c = Phases[p];
                        double s = 0.0;
                        for (int k = 0; k < Taps; k++) s += c[k] * stereo[2 * (start + k) + ch];
                        s = Math.Abs(s);
                        if (s > peak) peak = s;
                    }
                }
            }
            return peak;
        }

        /// <summary>Largest absolute sample over a frame range (both channels).</summary>
        public static double PeakAbs(float[] stereo, int frameStart, int frameEnd)
        {
            double peak = 0.0;
            int a = Math.Max(0, frameStart) * 2, b = Math.Min(stereo.Length / 2, frameEnd) * 2;
            for (int i = a; i < b; i++) { double v = Math.Abs(stereo[i]); if (v > peak) peak = v; }
            return peak;
        }

        /// <summary>RMS in dBFS over a frame range (both channels).</summary>
        public static double RmsDb(float[] stereo, int frameStart, int frameEnd)
        {
            double s = 0.0;
            int a = Math.Max(0, frameStart) * 2, b = Math.Min(stereo.Length / 2, frameEnd) * 2;
            for (int i = a; i < b; i++) s += (double)stereo[i] * stereo[i];
            int n = Math.Max(1, b - a);
            return 10.0 * Math.Log10(Math.Max(s / n, 1e-30));
        }
    }
}
