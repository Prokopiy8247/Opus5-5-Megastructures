// CaptureWriter: PNG encoding and file writing off the main thread for the capture harness
// (MASTER_PROMPT 11.2). The main thread renders, reads back and computes the image metrics; this
// writer encodes RGB PNGs on worker threads with a bounded queue (the main thread blocks when the
// queue holds more than its byte budget, so captured frames never depend on encode speed).
// A start-up probe proves that ImageConversion.EncodeArrayToPNG accepts RGB data and runs off the
// main thread; otherwise the writer falls back to RGBA and/or synchronous encoding.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Megastructure.QA
{
    /// <summary>Result of one captured image (still or sequence frame). Fields written by the writer are guarded by its lock.</summary>
    public sealed class CaptureResult
    {
        public string file;
        public int width, height;
        public bool rendered, written;
        public int pngWidth, pngHeight;
        public string error;
        public double renderMs, statsMs, encodeMs;
        public long fileBytes;
        public double meanLuma = double.NaN, meanAbsDiff = double.NaN;
        public double lumaP01 = double.NaN, lumaP50 = double.NaN, lumaP99 = double.NaN;
        public double clippedFraction = double.NaN;
        public int magentaPixels;

        public bool SizeOk => rendered && written && pngWidth == width && pngHeight == height;
        public bool Black => !double.IsNaN(meanLuma) && meanLuma < CaptureMetrics.BlackLuma;

        public void Fail(string why) { if (error == null) error = why; else error += "; " + why; }
    }

    public sealed class WriteJob
    {
        public byte[] data;
        public int width, height, length;
        public string path;
        public CaptureResult result;
    }

    public sealed class CaptureWriter : IDisposable
    {
        readonly object m_lock = new object();
        readonly Queue<WriteJob> m_queue = new Queue<WriteJob>(64);
        readonly List<Thread> m_threads = new List<Thread>(4);
        readonly Dictionary<int, Stack<byte[]>> m_pool = new Dictionary<int, Stack<byte[]>>();
        readonly long m_maxPendingBytes;
        readonly int m_threadCount;
        long m_pendingBytes;
        int m_inFlight;
        bool m_stop;

        const int PoolDepth = 12;

        /// <summary>True when encoding runs on the main thread (probe failed or 0 threads requested).</summary>
        public bool Synchronous { get; private set; } = true;
        /// <summary>True when PNGs are written as 24-bit RGB (smaller files, no alpha).</summary>
        public bool Rgb { get; private set; } = true;
        public string ProbeNote { get; private set; } = "";
        public int Threads => Synchronous ? 0 : m_threadCount;

        public CaptureWriter(int threads, long maxPendingBytes)
        {
            m_threadCount = Math.Max(0, Math.Min(8, threads));
            m_maxPendingBytes = Math.Max(64L << 20, maxPendingBytes);
        }

        /// <summary>Decides the PNG channel layout and whether encoding may run on worker threads.</summary>
        public void Probe()
        {
            var tiny = new byte[2 * 2 * 3];
            try
            {
                byte[] png = ImageConversion.EncodeArrayToPNG(tiny, GraphicsFormat.R8G8B8_SRGB, 2, 2, 6);
                Rgb = png != null && png.Length > 8;
            }
            catch (Exception e) { Rgb = false; ProbeNote += "RGB encode unsupported (" + e.Message + "); "; }

            if (m_threadCount == 0) { Synchronous = true; ProbeNote += "threads=0 requested"; return; }

            bool threadOk = false;
            string threadErr = null;
            bool rgb = Rgb;
            var probe = new Thread(() =>
            {
                try
                {
                    byte[] data = new byte[2 * 2 * (rgb ? 3 : 4)];
                    byte[] png = rgb ? ImageConversion.EncodeArrayToPNG(data, GraphicsFormat.R8G8B8_SRGB, 2, 2, 6)
                                     : ImageConversion.EncodeArrayToPNG(data, GraphicsFormat.R8G8B8A8_SRGB, 2, 2, 8);
                    threadOk = png != null && png.Length > 8;
                }
                catch (Exception e) { threadErr = e.GetType().Name + ": " + e.Message; }
            });
            probe.IsBackground = true;
            probe.Start();
            bool joined = probe.Join(10000);
            Synchronous = !(joined && threadOk);
            ProbeNote += Synchronous ? ("worker encode unavailable (" + (threadErr ?? (joined ? "no data" : "timeout")) + "), encoding on the main thread")
                                     : ("encoding on " + m_threadCount + " worker threads");
            if (Synchronous) return;
            for (int i = 0; i < m_threadCount; i++)
            {
                var t = new Thread(Loop) { IsBackground = true, Name = "ThreadCaptureWriter" + i };
                m_threads.Add(t);
                t.Start();
            }
        }

        public byte[] Rent(int length)
        {
            lock (m_lock)
            {
                if (m_pool.TryGetValue(length, out Stack<byte[]> st) && st.Count > 0) return st.Pop();
            }
            return new byte[length];
        }

        public void Return(byte[] b)
        {
            if (b == null) return;
            lock (m_lock)
            {
                if (!m_pool.TryGetValue(b.Length, out Stack<byte[]> st)) { st = new Stack<byte[]>(PoolDepth); m_pool[b.Length] = st; }
                if (st.Count < PoolDepth) st.Push(b);
            }
        }

        /// <summary>Drops pooled buffers of other sizes (after a resolution change).</summary>
        public void TrimPool(int keepA, int keepB)
        {
            lock (m_lock)
            {
                var remove = new List<int>();
                foreach (var kv in m_pool) if (kv.Key != keepA && kv.Key != keepB) remove.Add(kv.Key);
                for (int i = 0; i < remove.Count; i++) m_pool.Remove(remove[i]);
            }
        }

        public void Enqueue(WriteJob j)
        {
            if (Synchronous) { Process(j); return; }
            lock (m_lock)
            {
                while (m_pendingBytes + j.length > m_maxPendingBytes && (m_queue.Count > 0 || m_inFlight > 0))
                    Monitor.Wait(m_lock, 50);
                m_queue.Enqueue(j);
                m_pendingBytes += j.length;
                Monitor.PulseAll(m_lock);
            }
        }

        /// <summary>Blocks until every queued image is on disk. Returns false on timeout.</summary>
        public bool WaitIdle(double timeoutSeconds = 900)
        {
            var sw = Stopwatch.StartNew();
            lock (m_lock)
            {
                while (m_queue.Count > 0 || m_inFlight > 0)
                {
                    if (sw.Elapsed.TotalSeconds > timeoutSeconds) return false;
                    Monitor.Wait(m_lock, 100);
                }
            }
            return true;
        }

        void Loop()
        {
            while (true)
            {
                WriteJob j;
                lock (m_lock)
                {
                    while (m_queue.Count == 0 && !m_stop) Monitor.Wait(m_lock);
                    if (m_queue.Count == 0) return;
                    j = m_queue.Dequeue();
                    m_inFlight++;
                }
                Process(j);
                lock (m_lock)
                {
                    m_inFlight--;
                    m_pendingBytes -= j.length;
                    Monitor.PulseAll(m_lock);
                }
            }
        }

        void Process(WriteJob j)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                byte[] png = Rgb
                    ? ImageConversion.EncodeArrayToPNG(j.data, GraphicsFormat.R8G8B8_SRGB, (uint)j.width, (uint)j.height, (uint)(j.width * 3))
                    : ImageConversion.EncodeArrayToPNG(j.data, GraphicsFormat.R8G8B8A8_SRGB, (uint)j.width, (uint)j.height, (uint)(j.width * 4));
                if (png == null || png.Length < 24) throw new InvalidOperationException("PNG encoder returned no data");
                string dir = Path.GetDirectoryName(j.path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(j.path, png);
                // the written size, read back from the PNG header (IHDR width/height, big-endian at bytes 16..23)
                int pw = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
                int ph = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
                lock (m_lock)
                {
                    j.result.written = true;
                    j.result.fileBytes = png.Length;
                    j.result.pngWidth = pw;
                    j.result.pngHeight = ph;
                    j.result.encodeMs = sw.Elapsed.TotalMilliseconds;
                }
            }
            catch (Exception e)
            {
                lock (m_lock) j.result.Fail("write: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                Return(j.data);
                j.data = null;
            }
        }

        public void Dispose()
        {
            lock (m_lock) { m_stop = true; Monitor.PulseAll(m_lock); }
            for (int i = 0; i < m_threads.Count; i++) m_threads[i].Join(5000);
            m_threads.Clear();
            lock (m_lock) m_pool.Clear();
        }
    }

    /// <summary>Per-image metrics computed on the main thread from the sRGB readback (11.3 subset).</summary>
    public static class CaptureMetrics
    {
        /// <summary>Mean luma below this is a black frame (11.2 "not black").</summary>
        public const double BlackLuma = 0.02;
        // Rec. 709 luma weights on the sRGB-encoded values (luma Y', 0..1), the convention of the 14.10.2 luma ranges.
        public const double WR = 0.2126, WG = 0.7152, WB = 0.0722;
        // integer weights summing to 256 for the luma histogram
        const int IR = 54, IG = 183, IB = 19;

        /// <summary>
        /// Mean luma, luma percentiles, clipped-highlight fraction, magenta pixels and the mean absolute
        /// difference to the previous frame (grey levels, mean over R, G, B). Packs the pixels into rgbOut
        /// (3 bytes per pixel, rows optionally flipped) or into rgbaOut (alpha forced to 255).
        /// </summary>
        public static void Compute(byte[] rgba, byte[] prev, int w, int h, byte[] rgbOut, byte[] rgbaOut, bool flipRows, int[] hist, CaptureResult r)
        {
            long sr = 0, sg = 0, sb = 0, sd = 0;
            int magenta = 0, clipped = 0;
            Array.Clear(hist, 0, hist.Length);
            bool diff = prev != null && prev.Length == rgba.Length;
            int stride4 = w * 4, stride3 = w * 3;
            for (int y = 0; y < h; y++)
            {
                int src = y * stride4;
                int dy = flipRows ? h - 1 - y : y;
                int d3 = dy * stride3, d4 = dy * stride4;
                for (int x = 0; x < w; x++, src += 4)
                {
                    int cr = rgba[src], cg = rgba[src + 1], cb = rgba[src + 2];
                    sr += cr; sg += cg; sb += cb;
                    hist[(IR * cr + IG * cg + IB * cb) >> 8]++;
                    if (cr == 255 || cg == 255 || cb == 255) clipped++;
                    if (cr >= 200 && cg <= 60 && cb >= 200) magenta++;
                    if (diff)
                    {
                        int a = cr - prev[src], b = cg - prev[src + 1], c = cb - prev[src + 2];
                        sd += (a < 0 ? -a : a) + (b < 0 ? -b : b) + (c < 0 ? -c : c);
                    }
                    if (rgbOut != null)
                    {
                        rgbOut[d3] = (byte)cr; rgbOut[d3 + 1] = (byte)cg; rgbOut[d3 + 2] = (byte)cb;
                        d3 += 3;
                    }
                    else if (rgbaOut != null)
                    {
                        rgbaOut[d4] = (byte)cr; rgbaOut[d4 + 1] = (byte)cg; rgbaOut[d4 + 2] = (byte)cb; rgbaOut[d4 + 3] = 255;
                        d4 += 4;
                    }
                }
            }
            double n = Math.Max(1.0, (double)w * h);
            r.meanLuma = (WR * sr + WG * sg + WB * sb) / (255.0 * n);
            r.meanAbsDiff = diff ? sd / (3.0 * n) : double.NaN;
            r.clippedFraction = clipped / n;
            r.magentaPixels = magenta;
            r.lumaP01 = Percentile(hist, n, 0.01);
            r.lumaP50 = Percentile(hist, n, 0.50);
            r.lumaP99 = Percentile(hist, n, 0.99);
        }

        static double Percentile(int[] hist, double n, double q)
        {
            double target = q * n, acc = 0;
            for (int i = 0; i < hist.Length; i++)
            {
                acc += hist[i];
                if (acc >= target) return i / 255.0;
            }
            return 1.0;
        }
    }
}
