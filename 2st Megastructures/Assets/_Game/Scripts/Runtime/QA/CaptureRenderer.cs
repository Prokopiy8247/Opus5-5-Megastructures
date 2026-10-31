using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Megastructure
{
    /// <summary>One written image and its in-player sanity statistics (report.json row).</summary>
    public sealed class CaptureRecord
    {
        public string File, Kind, Shot, Tag, Aspect, Flags, CompositionJson;
        /// <summary>Debug variants: what was overridden, the base key shot compared with, the mean absolute RGB
        /// difference from it (levels) and the verdict ("changed", "identical", "none", "not verified ...").</summary>
        public string Override, BaseFile, OverrideEffect;
        public double DiffFromBase = double.NaN;
        public int Beat, W, H, SetOverride = -1, Frame = -1;
        public float S, Yaw, Pitch, Vfov, Vfov169;
        public double T = double.NaN;
        public Vector3 Eye;

        // filled by the writer thread
        public long Bytes;
        public bool Written, SizeOk, Black, White, Magenta, Ok;
        public string Error;
        public double MeanLuma, MeanR, MeanG, MeanB, ClippedPct;
        public int P001, P50, P99, P999, MaxLuma;   // luma percentiles 0.1 %, 50 %, 99 %, 99.9 %
        public long MagentaPx, ClippedPx;

        public void WriteJson(QaJson j)
        {
            j.Obj().KV("file", File).KV("kind", Kind).KV("shot", Shot).KV("tag", Tag).KV("beat", Beat).KV("w", W).KV("h", H)
             .KV("aspect", Aspect).KV("s", S).KV("t", T).KV("eye", Eye).KV("yaw", Yaw).KV("pitch", Pitch)
             .KV("vfov", Vfov).KV("vfov169", Vfov169).KV("set_override", SetOverride).KV("flags", Flags ?? "");
            if (Frame >= 0) j.KV("frame", Frame);
            if (Override != null || OverrideEffect != null)
                j.KV("override", Override).KV("base_file", BaseFile).KV("diff_from_base", DiffFromBase).KV("override_effect", OverrideEffect);
            j.KV("bytes", Bytes).KV("written", Written).KV("size_ok", SizeOk).KV("mean_luma", MeanLuma)
             .KV("mean_rgb_r", MeanR).KV("mean_rgb_g", MeanG).KV("mean_rgb_b", MeanB)
             .KV("p001", P001).KV("p50", P50).KV("p99", P99).KV("p999", P999).KV("max_luma", MaxLuma)
             .KV("clipped_px", ClippedPx).KV("clipped_pct", ClippedPct).KV("magenta_px", MagentaPx)
             .KV("black", Black).KV("white", White).KV("magenta", Magenta).KV("ok", Ok).KV("error", Error);
            if (CompositionJson != null) j.Key("composition").Raw(CompositionJson);
            j.EndObj();
        }
    }

    /// <summary>
    /// Renders a dedicated capture camera into a RenderTexture of any size (research 04): HDR colour
    /// R16G16B16A16_SFloat with MSAA 4 and a D32_SFloat_S8_UInt depth buffer (the RT format becomes URP's internal
    /// HDR format), RenderPipeline.StandardRequest (full camera stack, volumes and the FullScreenPass dither; never
    /// SingleCameraRequest or isPreview), then a GL.sRGBWrite blit into an R8G8B8A8_SRGB target, AsyncGPUReadback
    /// (waited), and PNG encoding plus sanity statistics on worker threads (ImageConversion.EncodeArrayToPNG is
    /// thread safe). Not for the game: only created in capture mode.
    /// </summary>
    public sealed class CaptureRenderer : IDisposable
    {
        public readonly Camera Cam;
        readonly PngWriter _writer;
        RenderTexture _hdr, _ldr;
        Texture2D _readTex;
        int _w, _h;
        public int MsaaUsed { get; private set; } = 4;
        public GraphicsFormat HdrFormat { get; private set; } = GraphicsFormat.R16G16B16A16_SFloat;
        public GraphicsFormat DepthFormat { get; private set; } = GraphicsFormat.None;
        /// <summary>Flip rows before encoding (set by the orientation self-test if the readback is top-down).</summary>
        public bool Flip;

        public CaptureRenderer(Transform parent, int workers)
        {
            var go = new GameObject("[CaptureCamera]");
            go.transform.SetParent(parent, false);
            Cam = go.AddComponent<Camera>();
            Cam.enabled = false;
            _writer = new PngWriter(Mathf.Clamp(workers, 1, 8));
        }

        /// <summary>Copies the gameplay camera's settings (and its URP additional data) onto the capture camera.</summary>
        public void SyncFrom(Camera main)
        {
            if (main != null) Cam.CopyFrom(main);
            Cam.enabled = false;
            Cam.targetTexture = null;
            Cam.allowHDR = true;
            Cam.allowMSAA = true;
            Cam.allowDynamicResolution = false;
            var dst = Cam.GetUniversalAdditionalCameraData();
            if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData src))
            {
                dst.renderPostProcessing = src.renderPostProcessing;
                dst.antialiasing = src.antialiasing;
                dst.antialiasingQuality = src.antialiasingQuality;
                dst.dithering = src.dithering;
                dst.stopNaN = src.stopNaN;
                dst.volumeLayerMask = src.volumeLayerMask;
                dst.renderShadows = src.renderShadows;
                dst.requiresColorTexture = src.requiresColorTexture;
                dst.requiresDepthTexture = src.requiresDepthTexture;
            }
            else
            {
                dst.renderPostProcessing = true;
                dst.dithering = false;
            }
            dst.renderType = CameraRenderType.Base;
            dst.volumeTrigger = null;
            dst.allowHDROutput = false;
        }

        public void Pose(Vector3 eye, float yaw, float pitch, float vfov, float aspect, float near, float far)
        {
            Cam.transform.SetPositionAndRotation(eye, Quaternion.Euler(-pitch, yaw, 0f));
            Cam.fieldOfView = vfov;
            Cam.aspect = aspect;
            Cam.nearClipPlane = near;
            Cam.farClipPlane = far;
        }

        void EnsureTargets(int w, int h)
        {
            if (_hdr != null && _w == w && _h == h && _hdr.IsCreated() && _ldr.IsCreated()) return;
            ReleaseTargets();
            DepthFormat = CoreUtils.GetDefaultDepthStencilFormat();
            var d = new RenderTextureDescriptor(w, h)
            {
                graphicsFormat = HdrFormat,
                depthStencilFormat = DepthFormat,
                msaaSamples = 4,
                useMipMap = false,
                autoGenerateMips = false,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1
            };
            int sup = SystemInfo.GetRenderTextureSupportedMSAASampleCount(d);
            d.msaaSamples = sup >= 4 ? 4 : Mathf.Max(1, sup);
            MsaaUsed = d.msaaSamples;
            _hdr = new RenderTexture(d) { name = "RingCaptureHDR" };
            _hdr.Create();
            var l = new RenderTextureDescriptor(w, h)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1
            };
            _ldr = new RenderTexture(l) { name = "RingCaptureLDR" };
            _ldr.Create();
            _w = w; _h = h;
        }

        void ReleaseTargets()
        {
            if (_hdr != null) { _hdr.Release(); UnityEngine.Object.Destroy(_hdr); _hdr = null; }
            if (_ldr != null) { _ldr.Release(); UnityEngine.Object.Destroy(_ldr); _ldr = null; }
            if (_readTex != null) { UnityEngine.Object.Destroy(_readTex); _readTex = null; }
            _w = _h = 0;
        }

        /// <summary>Renders the capture camera (warm-up renders first) into the HDR target and converts it to sRGB 8-bit.</summary>
        public bool Render(int w, int h, int warmups, out string error)
        {
            error = null;
            EnsureTargets(w, h);
            var req = new RenderPipeline.StandardRequest { destination = _hdr, mipLevel = 0, slice = 0, isPreview = false };
            if (!RenderPipeline.SupportsRenderRequest(Cam, req)) { error = "RenderPipeline.StandardRequest not supported"; return false; }
            try
            {
                for (int i = 0; i <= warmups; i++)
                {
                    RingCaptureHooks.InvokeBefore(Cam);
                    Cam.enabled = true;
                    RenderPipeline.SubmitRenderRequest(Cam, req);
                    Cam.enabled = false;
                    RingCaptureHooks.InvokeAfter(Cam);
                }
            }
            catch (Exception e) { Cam.enabled = false; error = "render: " + e.Message; return false; }
            bool prev = GL.sRGBWrite;
            GL.sRGBWrite = true;
            Graphics.Blit(_hdr, _ldr);
            GL.sRGBWrite = prev;
            return true;
        }

        /// <summary>Reads the sRGB target back into a pooled buffer (w * h * 4 bytes, rows bottom-up as in Unity textures).</summary>
        public byte[] Readback(out string error)
        {
            error = null;
            int size = _w * _h * 4;
            var buf = _writer.Rent(size);
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                var r = AsyncGPUReadback.Request(_ldr, 0);
                r.WaitForCompletion();
                if (!r.hasError)
                {
                    var data = r.GetData<byte>();
                    if (data.Length == size) { data.CopyTo(buf); return buf; }
                    error = "readback size " + data.Length + " != " + size;
                }
                else error = "AsyncGPUReadback error";
            }
            // fallback: ReadPixels into an sRGB RGBA32 texture (same format as the target)
            try
            {
                if (_readTex == null || _readTex.width != _w || _readTex.height != _h)
                {
                    if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
                    _readTex = new Texture2D(_w, _h, TextureFormat.RGBA32, false, false);
                }
                var prev = RenderTexture.active;
                RenderTexture.active = _ldr;
                _readTex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0, false);
                RenderTexture.active = prev;
                var raw = _readTex.GetRawTextureData<byte>();
                if (raw.Length != size) { error = "ReadPixels size mismatch"; _writer.Return(buf); return null; }
                raw.CopyTo(buf);
                error = null;
                return buf;
            }
            catch (Exception e) { error = (error ?? "") + " / ReadPixels: " + e.Message; _writer.Return(buf); return null; }
        }

        public byte[] Rent(int size) => _writer.Rent(size);
        public void Return(byte[] b) => _writer.Return(b);

        /// <summary>Queues a buffer (w * h * 4 bytes) for statistics, PNG encoding and writing. The buffer returns to the pool.</summary>
        public void Write(byte[] buf, int w, int h, string path, CaptureRecord rec, bool flip)
        {
            _writer.Enqueue(new PngWriter.Job { Buf = buf, W = w, H = h, Path = path, Rec = rec, Flip = flip });
        }

        public int Pending => _writer.Pending;

        public void Flush() => _writer.Flush();

        /// <summary>
        /// Orientation self-test: blits a 4 x 4 texture with a red texel in its top-left corner (texture row 3) through
        /// the sRGB target and the readback, and writes it through the PNG encoder as _orientation_selftest.png
        /// (Tools/qa_metrics.py checks that the PNG's top-left pixel is red). Returns the readback row of the marker
        /// (3 = Unity bottom-up convention, no flip needed).
        /// </summary>
        public int OrientationSelfTest(string folder, out string detail)
        {
            detail = "";
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(0, 0, 0, 255);
            px[3 * 4 + 0] = new Color32(255, 0, 0, 255);   // x 0, y 3 (top-left in texture convention)
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var rt = new RenderTexture(new RenderTextureDescriptor(4, 4) { graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB, depthStencilFormat = GraphicsFormat.None, msaaSamples = 1 });
            rt.Create();
            bool prev = GL.sRGBWrite;
            GL.sRGBWrite = true;
            Graphics.Blit(tex, rt);
            GL.sRGBWrite = prev;
            int row = -1;
            byte[] data = new byte[64];
            try
            {
                var r = AsyncGPUReadback.Request(rt, 0);
                r.WaitForCompletion();
                if (!r.hasError)
                {
                    r.GetData<byte>().CopyTo(data);
                    for (int y = 0; y < 4; y++)
                        if (data[(y * 4) * 4] > 200 && data[(y * 4) * 4 + 1] < 60) { row = y; break; }
                }
                else detail = "readback error";
            }
            catch (Exception e) { detail = e.Message; }
            try
            {
                var png = ImageConversion.EncodeArrayToPNG(data, GraphicsFormat.R8G8B8A8_UNorm, 4, 4);
                QaUtil.EnsureDir(folder);
                System.IO.File.WriteAllBytes(Path.Combine(folder, "_orientation_selftest.png"), png);
            }
            catch (Exception e) { detail += " png: " + e.Message; }
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            if (detail.Length == 0) detail = "marker at readback row " + row + (row == 3 ? " (bottom-up, no flip)" : row == 0 ? " (top-down, flip)" : " (unexpected)");
            return row;
        }

        /// <summary>Captures the window (ScreenCapture.CaptureScreenshotIntoRenderTexture; call after WaitForEndOfFrame)
        /// into an RT of the given format and reads it back (w * h * 4 bytes as stored, possibly flipped).</summary>
        public byte[] CaptureWindow(GraphicsFormat fmt, out int w, out int h, out string error)
        {
            error = null;
            w = Screen.width; h = Screen.height;
            var rt = new RenderTexture(new RenderTextureDescriptor(w, h) { graphicsFormat = fmt, depthStencilFormat = GraphicsFormat.None, msaaSamples = 1 });
            rt.Create();
            byte[] buf = null;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
                var r = AsyncGPUReadback.Request(rt, 0);
                r.WaitForCompletion();
                if (r.hasError) error = "window readback error";
                else
                {
                    var data = r.GetData<byte>();
                    if (data.Length != w * h * 4) error = "window readback size " + data.Length;
                    else { buf = new byte[data.Length]; data.CopyTo(buf); }
                }
            }
            catch (Exception e) { error = e.Message; }
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            return buf;
        }

        public void Dispose()
        {
            try { _writer.Flush(); } catch (Exception) { }
            _writer.Stop();
            ReleaseTargets();
            if (Cam != null) UnityEngine.Object.Destroy(Cam.gameObject);
        }

        /// <summary>Mean absolute difference (levels, RGB) between two RGBA8 buffers of equal size; optionally with b flipped vertically.</summary>
        public static double MeanAbsDiff(byte[] a, byte[] b, int w, int h, bool flipB)
        {
            if (a == null || b == null || a.Length != b.Length) return double.NaN;
            long sum = 0;
            int row = w * 4;
            for (int y = 0; y < h; y++)
            {
                int ia = y * row, ib = (flipB ? (h - 1 - y) : y) * row;
                for (int x = 0; x < row; x += 4)
                {
                    sum += Math.Abs(a[ia + x] - b[ib + x]) + Math.Abs(a[ia + x + 1] - b[ib + x + 1]) + Math.Abs(a[ia + x + 2] - b[ib + x + 2]);
                }
            }
            return sum / (3.0 * w * h);
        }

        public static void FlipRows(byte[] buf, int w, int h)
        {
            int row = w * 4;
            var tmp = new byte[row];
            for (int y = 0; y < h / 2; y++)
            {
                int a = y * row, b = (h - 1 - y) * row;
                Buffer.BlockCopy(buf, a, tmp, 0, row);
                Buffer.BlockCopy(buf, b, buf, a, row);
                Buffer.BlockCopy(tmp, 0, buf, b, row);
            }
        }
    }

    /// <summary>Bounded worker pool: per-image statistics, alpha fix, optional flip, PNG encode, write, header check.</summary>
    sealed class PngWriter
    {
        public struct Job { public byte[] Buf; public int W, H; public string Path; public CaptureRecord Rec; public bool Flip; }

        readonly Queue<Job> _q = new Queue<Job>();
        readonly object _lock = new object();
        readonly Thread[] _threads;
        readonly Dictionary<int, Stack<byte[]>> _pool = new Dictionary<int, Stack<byte[]>>();
        bool _stop;
        int _busy;
        const int MaxQueued = 6;
        const int MaxPooledPerSize = 8;

        public PngWriter(int n)
        {
            _threads = new Thread[n];
            for (int i = 0; i < n; i++)
            {
                _threads[i] = new Thread(Loop) { IsBackground = true, Name = "RingPngWriter" + i };
                _threads[i].Start();
            }
        }

        public int Pending { get { lock (_lock) return _q.Count + _busy; } }

        public byte[] Rent(int size)
        {
            lock (_lock)
            {
                if (_pool.TryGetValue(size, out var st) && st.Count > 0) return st.Pop();
            }
            return new byte[size];
        }

        public void Return(byte[] b)
        {
            if (b == null) return;
            lock (_lock)
            {
                if (!_pool.TryGetValue(b.Length, out var st)) { st = new Stack<byte[]>(); _pool[b.Length] = st; }
                if (st.Count < MaxPooledPerSize) st.Push(b);
            }
        }

        public void Enqueue(Job j)
        {
            lock (_lock)
            {
                while (_q.Count >= MaxQueued) Monitor.Wait(_lock);
                _q.Enqueue(j);
                Monitor.PulseAll(_lock);
            }
        }

        public void Flush()
        {
            lock (_lock) { while (_q.Count > 0 || _busy > 0) Monitor.Wait(_lock); }
        }

        public void Stop()
        {
            lock (_lock) { _stop = true; Monitor.PulseAll(_lock); }
            foreach (var t in _threads) { try { t.Join(5000); } catch (Exception) { } }
        }

        void Loop()
        {
            while (true)
            {
                Job j;
                lock (_lock)
                {
                    while (_q.Count == 0 && !_stop) Monitor.Wait(_lock);
                    if (_q.Count == 0) return;
                    j = _q.Dequeue();
                    _busy++;
                    Monitor.PulseAll(_lock);
                }
                try { Process(j); }
                catch (Exception e) { j.Rec.Error = "writer: " + e.Message; j.Rec.Ok = false; }
                finally
                {
                    Return(j.Buf);
                    lock (_lock) { _busy--; Monitor.PulseAll(_lock); }
                }
            }
        }

        static void Process(Job j)
        {
            var rec = j.Rec;
            var b = j.Buf;
            int n = j.W * j.H;
            var hist = new long[256];
            long sr = 0, sg = 0, sb = 0, mag = 0, clip = 0;
            for (int i = 0, o = 0; i < n; i++, o += 4)
            {
                byte r = b[o], g = b[o + 1], bl = b[o + 2];
                b[o + 3] = 255;
                sr += r; sg += g; sb += bl;
                int l = (2126 * r + 7152 * g + 722 * bl + 5000) / 10000;
                hist[l]++;
                if (r >= 200 && bl >= 200 && g <= 60) mag++;
                if (r >= 254 || g >= 254 || bl >= 254) clip++;
            }
            double meanL = 0;
            for (int i = 0; i < 256; i++) meanL += i * (double)hist[i];
            meanL /= Math.Max(1, n);
            rec.MeanLuma = meanL;
            rec.MeanR = sr / (double)Math.Max(1, n); rec.MeanG = sg / (double)Math.Max(1, n); rec.MeanB = sb / (double)Math.Max(1, n);
            rec.P001 = Pct(hist, n, 0.001); rec.P50 = Pct(hist, n, 0.5); rec.P99 = Pct(hist, n, 0.99); rec.P999 = Pct(hist, n, 0.999);
            rec.MaxLuma = Pct(hist, n, 1.0);
            rec.MagentaPx = mag;
            rec.ClippedPx = clip;
            rec.ClippedPct = 100.0 * clip / Math.Max(1, n);
            // black: 99.9 % of the pixels at luma <= 5; white: 99.9 % at luma >= 250; magenta: any strong magenta pixel
            rec.Black = rec.P999 <= 5;
            rec.White = rec.P001 >= 250;
            rec.Magenta = mag > 0;

            if (j.Flip) CaptureRenderer.FlipRows(b, j.W, j.H);
            var png = ImageConversion.EncodeArrayToPNG(b, GraphicsFormat.R8G8B8A8_UNorm, (uint)j.W, (uint)j.H);
            var dir = System.IO.Path.GetDirectoryName(j.Path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(j.Path, png);
            rec.Written = true;
            var fi = new FileInfo(j.Path);
            rec.Bytes = fi.Exists ? fi.Length : 0;
            // verify the IHDR width and height of the written file
            var hdr = new byte[24];
            using (var fs = File.OpenRead(j.Path)) { int got = fs.Read(hdr, 0, 24); if (got < 24) hdr = null; }
            if (hdr != null && hdr[1] == (byte)'P' && hdr[2] == (byte)'N' && hdr[3] == (byte)'G')
            {
                int w = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
                int h = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
                rec.SizeOk = w == j.W && h == j.H && rec.Bytes > 0;
            }
            rec.Ok = rec.Written && rec.SizeOk && !rec.Black && !rec.White && !rec.Magenta && rec.Error == null;
        }

        static int Pct(long[] hist, int n, double q)
        {
            long target = (long)Math.Ceiling(q * n);
            if (target < 1) target = 1;
            long acc = 0;
            for (int i = 0; i < 256; i++) { acc += hist[i]; if (acc >= target) return i; }
            return 255;
        }
    }
}
