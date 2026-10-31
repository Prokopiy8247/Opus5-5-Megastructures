using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Megastructure
{
    /// <summary>
    /// QA-only render pass (enqueued from RenderPipelineManager.beginCameraRendering while QaRenderer captures with depth):
    /// copies URP's resolved camera depth texture into an R32 float target after the transparents, for the composition
    /// lint's occlusion test (critique R1-29). Raw device depth; QaRenderer linearises it.
    /// </summary>
    sealed class QaDepthGrabPass : ScriptableRenderPass
    {
        public RTHandle Target;
        public bool Recorded;

        public QaDepthGrabPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (Target == null) return;
            UniversalResourceData res = frameData.Get<UniversalResourceData>();
            TextureHandle src = res.cameraDepthTexture;
            if (!src.IsValid()) return;
            TextureHandle dst = renderGraph.ImportTexture(Target);
            renderGraph.AddBlitPass(src, dst, Vector2.one, Vector2.zero, filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                                    passName: "QA depth grab");
            Recorded = true;
        }
    }

    /// <summary>
    /// Renders the main camera into an offscreen RenderTexture of any size and reads it back as 8-bit sRGB
    /// (docs/research/capture-player.md section 1 and recommendation 3; Part 11.2 "capture through a RenderTexture").
    ///
    /// - Target: R16G16B16A16_SFloat (URP's intermediate inherits the target format, so it matches the 64-bit HDR on-screen
    ///   intermediate the final dither needs), MSAA = the URP asset's sample count (URP takes the MSAA count of a camera
    ///   target from the target), depth D32_SFloat_S8_UInt (URP's render graph wants a depth buffer on the output).
    /// - Rendered with RenderPipeline.SubmitRenderRequest(StandardRequest) from LateUpdate after the instanced draws (+200),
    ///   so post-processing, renderer features and Graphics.RenderMeshInstanced draws of this frame are included; overlay UI is
    ///   never drawn into a RenderTexture. The camera's aspect and the _AbPixelAngle global follow the capture size during
    ///   the render and are restored afterwards.
    /// - MSAA resolve into a single-sample twin, AsyncGPUReadback (+WaitForCompletion) of the half floats, ReadPixels as the
    ///   fallback. Linear to sRGB on the CPU through a 65 536-entry half-to-byte table (exact piecewise sRGB, rounded;
    ///   NaN / +Inf to 255 so they show as specks, negatives to 0). Rec. 709 mean luma of the sRGB frame on the way.
    /// - Pixel rows are kept in Unity texture order (row 0 = bottom). <see cref="Flip"/> (-capture-flip on) reverses the rows
    ///   if a platform reads back top-down; the parity test reports which orientation matches the window.
    /// QA-only: allocates its buffers once per size.
    /// </summary>
    public sealed class QaRenderer : IDisposable
    {
        RenderTexture _rt, _resolve;
        int _w, _h, _msaa;
        byte[] _rgb;
        Texture2D _encodeTex, _readTex;
        static byte[] s_lut;

        // depth grab for the lint's occlusion test (R1-29)
        RenderTexture _depthRT;
        RTHandle _depthHandle;
        float[] _depth;
        QaDepthGrabPass _depthPass;
        Camera _depthCam;
        bool _subscribed;

        /// <summary>Reverse the pixel rows after the readback (platform orientation override).</summary>
        public bool Flip;

        /// <summary>Also grab the camera depth texture during <see cref="Capture"/> (linear eye depth in <see cref="Depth"/>).</summary>
        public bool WantDepth;

        /// <summary>Linear eye depth (metres) of the last capture, texture order (row 0 = bottom), or null.</summary>
        public float[] Depth => DepthValid ? _depth : null;
        public bool DepthValid { get; private set; }
        /// <summary>"ok", "not recorded (no camera depth texture)", "readback failed" or "off".</summary>
        public string DepthStatus { get; private set; } = "off";
        /// <summary>Median eye depth of the top and bottom tenth of the rows of the last depth grab (orientation check).</summary>
        public float DepthTopMedian { get; private set; }
        public float DepthBottomMedian { get; private set; }

        public int Width => _w;
        public int Height => _h;
        public int Msaa => _msaa;

        /// <summary>RGB24 pixels of the last capture, Unity texture order (row 0 = bottom).</summary>
        public byte[] Rgb => _rgb;

        /// <summary>Mean Rec. 709 luma (0-1) of the sRGB values of the last capture.</summary>
        public double MeanLuma { get; private set; }

        /// <summary>"async" or "readpixels".</summary>
        public string LastReadback { get; private set; } = "none";

        /// <summary>Milliseconds of the last render + readback + conversion.</summary>
        public float LastMs { get; private set; }

        /// <summary>The URP asset's MSAA sample count (the on-screen path), 4 when no URP asset is active.</summary>
        public static int AssetMsaa
        {
            get
            {
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                int m = urp != null ? urp.msaaSampleCount : 4;
                return m < 1 ? 1 : m;
            }
        }

        static void BuildLut()
        {
            if (s_lut != null) return;
            var lut = new byte[65536];
            for (int i = 0; i < 65536; i++)
            {
                float f = Mathf.HalfToFloat((ushort)i);
                if (float.IsNaN(f) || float.IsPositiveInfinity(f)) { lut[i] = 255; continue; }
                if (!(f > 0f)) { lut[i] = 0; continue; }
                double l = f >= 1f ? 1.0 : f;
                double s = l <= 0.0031308 ? 12.92 * l : 1.055 * Math.Pow(l, 1.0 / 2.4) - 0.055;
                int b = (int)Math.Floor(s * 255.0 + 0.5);
                lut[i] = (byte)(b < 0 ? 0 : (b > 255 ? 255 : b));
            }
            s_lut = lut;
        }

        void Ensure(int w, int h)
        {
            int msaa = AssetMsaa;
            if (_rt != null && _w == w && _h == h && _msaa == msaa) return;
            ReleaseTargets();
            _w = w; _h = h; _msaa = msaa;
            var desc = new RenderTextureDescriptor(w, h, GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.D32_SFloat_S8_UInt)
            {
                msaaSamples = msaa, useMipMap = false, autoGenerateMips = false,
            };
            _rt = new RenderTexture(desc) { name = "QA_Capture_" + w + "x" + h };
            _rt.Create();
            if (msaa > 1)
            {
                var rd = new RenderTextureDescriptor(w, h, GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.None)
                {
                    msaaSamples = 1, useMipMap = false, autoGenerateMips = false,
                };
                _resolve = new RenderTexture(rd) { name = "QA_CaptureResolve_" + w + "x" + h };
                _resolve.Create();
            }
            _rgb = new byte[w * h * 3];
        }

        /// <summary>
        /// Render cam into the w x h target and read it back into <see cref="Rgb"/>. Call from LateUpdate (after the
        /// instanced draws). Returns false with an error message on failure.
        /// </summary>
        public bool Capture(Camera cam, int w, int h, out string error)
        {
            error = null;
            if (cam == null) { error = "no camera"; return false; }
            BuildLut();
            Ensure(w, h);
            float t0 = Time.realtimeSinceStartup;

            float fov = cam.fieldOfView;
            Shader.SetGlobalFloat(ShaderIds.AbPixelAngle, 2f * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) / h);
            cam.aspect = (float)w / h;
            DepthValid = false;
            DepthStatus = "off";
            if (WantDepth)
            {
                EnsureDepth(w, h);
                _depthPass.Recorded = false;
                _depthCam = cam;
            }
            QaHooks.Capturing = true;
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = _rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req))
                {
                    RenderPipeline.SubmitRenderRequest(cam, req);
                }
                else
                {
                    RenderTexture prev = cam.targetTexture;
                    cam.targetTexture = _rt;
                    cam.Render();
                    cam.targetTexture = prev;
                }
            }
            catch (Exception ex)
            {
                error = "render failed: " + ex.Message;
            }
            finally
            {
                QaHooks.Capturing = false;
                _depthCam = null;
                cam.ResetAspect();
                RenderTexture tt = cam.targetTexture;
                int sh = tt != null ? tt.height : Mathf.Max(1, cam.pixelHeight);
                Shader.SetGlobalFloat(ShaderIds.AbPixelAngle, 2f * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) / sh);
            }
            if (error != null) return false;

            RenderTexture src = _rt;
            if (_msaa > 1 && _resolve != null)
            {
                _rt.ResolveAntiAliasedSurface(_resolve);
                src = _resolve;
            }

            bool ok = false;
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadbackRequest r = AsyncGPUReadback.Request(src, 0, GraphicsFormat.R16G16B16A16_SFloat);
                r.WaitForCompletion();
                if (!r.hasError && r.done)
                {
                    NativeArray<ushort> data = r.GetData<ushort>();
                    if (data.Length >= w * h * 4)
                    {
                        Convert(data);
                        LastReadback = "async";
                        ok = true;
                    }
                }
            }
            if (!ok)
            {
                if (_readTex == null || _readTex.width != w || _readTex.height != h)
                {
                    if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
                    _readTex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true) { name = "QA_ReadPixels" };
                }
                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = src;
                _readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                _readTex.Apply(false, false);
                RenderTexture.active = prevActive;
                NativeArray<ushort> data = _readTex.GetPixelData<ushort>(0);
                if (data.Length >= w * h * 4)
                {
                    Convert(data);
                    LastReadback = "readpixels";
                    ok = true;
                }
                else error = "readback returned " + data.Length + " values for " + w + "x" + h;
            }
            if (WantDepth) ReadDepth(cam, w, h);
            LastMs = (Time.realtimeSinceStartup - t0) * 1000f;
            return ok;
        }

        // ---------------------------------------------------------------- depth grab (R1-29)

        void EnsureDepth(int w, int h)
        {
            if (!_subscribed)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;
                _subscribed = true;
            }
            if (_depthPass == null) _depthPass = new QaDepthGrabPass();
            if (_depthRT != null && _depthRT.width == w && _depthRT.height == h) return;
            ReleaseDepth();
            var desc = new RenderTextureDescriptor(w, h, GraphicsFormat.R32_SFloat, GraphicsFormat.None)
            {
                msaaSamples = 1, useMipMap = false, autoGenerateMips = false,
            };
            _depthRT = new RenderTexture(desc) { name = "QA_Depth_" + w + "x" + h, filterMode = FilterMode.Point };
            _depthRT.Create();
            _depthHandle = RTHandles.Alloc(_depthRT);
            _depthPass.Target = _depthHandle;
            _depth = new float[w * h];
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_depthCam == null || cam != _depthCam || _depthPass == null) return;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null && data.scriptableRenderer != null) data.scriptableRenderer.EnqueuePass(_depthPass);
        }

        /// <summary>Read the grabbed raw depth back and convert it to linear eye depth (reversed-Z aware), texture order.</summary>
        void ReadDepth(Camera cam, int w, int h)
        {
            if (_depthPass == null || !_depthPass.Recorded) { DepthStatus = "not recorded (no camera depth texture)"; return; }
            if (!SystemInfo.supportsAsyncGPUReadback) { DepthStatus = "no async readback"; return; }
            AsyncGPUReadbackRequest r = AsyncGPUReadback.Request(_depthRT, 0, GraphicsFormat.R32_SFloat);
            r.WaitForCompletion();
            if (r.hasError || !r.done) { DepthStatus = "readback failed"; return; }
            NativeArray<float> raw = r.GetData<float>();
            if (raw.Length < w * h) { DepthStatus = "readback size " + raw.Length; return; }
            float n = cam.nearClipPlane, f = cam.farClipPlane;
            bool rev = SystemInfo.usesReversedZBuffer;
            float[] dst = _depth;
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w, dstRow = (Flip ? (h - 1 - y) : y) * w;
                for (int x = 0; x < w; x++)
                {
                    float d = raw[srcRow + x];
                    // reversed Z: eye = n f / (n + d (f - n)); conventional: eye = n f / (f - d (f - n))
                    float den = rev ? n + d * (f - n) : f - d * (f - n);
                    dst[dstRow + x] = den > 1e-9f ? n * f / den : f;
                }
            }
            DepthTopMedian = RowBandMedian(dst, w, h, h - 1 - h / 10, h - 1);
            DepthBottomMedian = RowBandMedian(dst, w, h, 0, h / 10);
            DepthValid = true;
            DepthStatus = "ok";
        }

        static float RowBandMedian(float[] d, int w, int h, int r0, int r1)
        {
            int n = 0;
            var tmp = new float[64];
            for (int k = 0; k < 64; k++)
            {
                int row = Mathf.Clamp(r0 + (r1 - r0) * (k / 8) / 8, 0, h - 1);
                int col = Mathf.Clamp(w * (2 * (k % 8) + 1) / 16, 0, w - 1);
                tmp[n++] = d[row * w + col];
            }
            Array.Sort(tmp, 0, n);
            return tmp[n / 2];
        }

        void ReleaseDepth()
        {
            if (_depthHandle != null) { RTHandles.Release(_depthHandle); _depthHandle = null; }
            if (_depthRT != null) { _depthRT.Release(); UnityEngine.Object.Destroy(_depthRT); _depthRT = null; }
            if (_depthPass != null) _depthPass.Target = null;
        }

        void Convert(NativeArray<ushort> data)
        {
            byte[] lut = s_lut;
            byte[] dst = _rgb;
            int w = _w, h = _h;
            double sum = 0.0;
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w * 4;
                int dstRow = (Flip ? (h - 1 - y) : y) * w * 3;
                long rowSum = 0;
                for (int x = 0; x < w; x++)
                {
                    int si = srcRow + 4 * x, di = dstRow + 3 * x;
                    byte r = lut[data[si]], g = lut[data[si + 1]], b = lut[data[si + 2]];
                    dst[di] = r; dst[di + 1] = g; dst[di + 2] = b;
                    rowSum += 2126 * r + 7152 * g + 722 * b;
                }
                sum += rowSum;
            }
            MeanLuma = sum / (10000.0 * 255.0 * w * h);
        }

        /// <summary>Encode the last capture as PNG (RGB24).</summary>
        public byte[] EncodePng()
        {
            if (_rgb == null) return null;
            if (_encodeTex == null || _encodeTex.width != _w || _encodeTex.height != _h)
            {
                if (_encodeTex != null) UnityEngine.Object.Destroy(_encodeTex);
                _encodeTex = new Texture2D(_w, _h, TextureFormat.RGB24, false, true) { name = "QA_Encode" };
            }
            _encodeTex.SetPixelData(_rgb, 0);
            _encodeTex.Apply(false, false);
            return _encodeTex.EncodeToPNG();
        }

        /// <summary>Write the last capture as a PNG file (creates the folder).</summary>
        public long WritePng(string path)
        {
            byte[] png = EncodePng();
            if (png == null) return 0;
            QaPaths.EnsureParent(path);
            File.WriteAllBytes(path, png);
            return png.LongLength;
        }

        /// <summary>Copy of the last capture's pixels (for pair analyses).</summary>
        public byte[] CopyRgb() => _rgb != null ? (byte[])_rgb.Clone() : null;

        // ---------------------------------------------------------------- pixel helpers (texture order: row 0 = bottom)

        /// <summary>Rec. 709 luma 0-1 of the pixel at x and yTop (row counted from the top).</summary>
        public static float Luma(byte[] rgb, int w, int h, int x, int yTop)
        {
            int i = ((h - 1 - yTop) * w + x) * 3;
            return (0.2126f * rgb[i] + 0.7152f * rgb[i + 1] + 0.0722f * rgb[i + 2]) / 255f;
        }

        /// <summary>Mean luma of a fractional rectangle [x0, y0, x1, y1] (top-left origin).</summary>
        public static double MeanLumaRect(byte[] rgb, int w, int h, float fx0, float fy0, float fx1, float fy1)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(fx0 * w), 0, w), x1 = Mathf.Clamp(Mathf.RoundToInt(fx1 * w), 0, w);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(fy0 * h), 0, h), y1 = Mathf.Clamp(Mathf.RoundToInt(fy1 * h), 0, h);
            double s = 0; long n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) { s += Luma(rgb, w, h, x, y); n++; }
            return n > 0 ? s / n : double.NaN;
        }

        /// <summary>Mean absolute per-channel difference (8-bit levels) of b against a shifted by dx pixels (b[x] vs a[x + dx]).</summary>
        public static double MeanAbsDiffRect(byte[] a, byte[] b, int w, int h, float fx0, float fy0, float fx1, float fy1, int dx = 0)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(fx0 * w), 0, w), x1 = Mathf.Clamp(Mathf.RoundToInt(fx1 * w), 0, w);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(fy0 * h), 0, h), y1 = Mathf.Clamp(Mathf.RoundToInt(fy1 * h), 0, h);
            double s = 0; long n = 0;
            for (int y = y0; y < y1; y++)
            {
                int row = (h - 1 - y) * w;
                for (int x = x0; x < x1; x++)
                {
                    int xa = x + dx;
                    if (xa < 0 || xa >= w) continue;
                    int ib = (row + x) * 3, ia = (row + xa) * 3;
                    s += Math.Abs(a[ia] - b[ib]) + Math.Abs(a[ia + 1] - b[ib + 1]) + Math.Abs(a[ia + 2] - b[ib + 2]);
                    n += 3;
                }
            }
            return n > 0 ? s / n : double.NaN;
        }

        /// <summary>
        /// Mean absolute difference (levels) between the last capture and a window capture of the same size, with the capture
        /// rows as stored and with the rows reversed (orientation check).
        /// </summary>
        public void CompareWithWindow(Color32[] win, out double direct, out double flipped)
        {
            direct = flipped = double.NaN;
            if (_rgb == null || win == null || win.Length != _w * _h) return;
            double sd = 0, sf = 0;
            int w = _w, h = _h;
            for (int y = 0; y < h; y++)
            {
                int rowW = y * w;
                int rowD = y * w * 3, rowF = (h - 1 - y) * w * 3;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = win[rowW + x];
                    int d = rowD + 3 * x, f = rowF + 3 * x;
                    sd += Math.Abs(c.r - _rgb[d]) + Math.Abs(c.g - _rgb[d + 1]) + Math.Abs(c.b - _rgb[d + 2]);
                    sf += Math.Abs(c.r - _rgb[f]) + Math.Abs(c.g - _rgb[f + 1]) + Math.Abs(c.b - _rgb[f + 2]);
                }
            }
            double n = 3.0 * w * h;
            direct = sd / n;
            flipped = sf / n;
        }

        void ReleaseTargets()
        {
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); _rt = null; }
            if (_resolve != null) { _resolve.Release(); UnityEngine.Object.Destroy(_resolve); _resolve = null; }
        }

        public void Dispose()
        {
            if (_subscribed) { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; _subscribed = false; }
            ReleaseDepth();
            ReleaseTargets();
            if (_encodeTex != null) UnityEngine.Object.Destroy(_encodeTex);
            if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            _encodeTex = null; _readTex = null; _rgb = null;
        }
    }
}
