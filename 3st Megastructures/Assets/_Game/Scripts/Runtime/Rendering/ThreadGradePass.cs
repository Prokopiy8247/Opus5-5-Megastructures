// ThreadGradePass: the Render Graph pass behind ThreadGradeFeature (MASTER_PROMPT 8.2, 14.2.5, 14.7.1;
// research R1 option B, write path B1; R6 dither). Records, per final camera:
//   prefilter (half res, Karis + exposure + soft-knee threshold) -> down chain -> scatter up chain
//   (all R16G16B16A16_SFloat, never 8-bit) -> ThreadGrade into a fresh R8G8B8A8_SRGB texture,
// then sets resourceData.cameraColor to that texture so URP's FinalBlit copies it 1:1 to the screen.
//
// Bloom pyramid (resolution-normalised): level i is (W >> (i+1)) x (H >> (i+1)). At 4K the half-res level
// is an intermediate only (ThreadGradeMath.BloomSkipLevels), so the bloom levels are 960x540 .. 30x17 at
// both 1080p and 2160p and the glow has the same screen-relative shape in every capture.
//
// CAPTURE CONTRACT (RenderPipeline.SubmitRenderRequest into a RenderTexture; verified in URP 17.6 source):
//  * The camera target must be R16G16B16A16_SFloat (MSAA allowed). URP's intermediate camera colour copies the
//    target's format (UniversalRenderPipelineCore.cs MakeRenderTextureGraphicsFormat / intermediate descriptor),
//    so a B10G11R11 or 8-bit target would render the whole scene at that precision.
//  * FinalBlit (FinalBlitPass.Render / ExecutePass) samples our R8G8B8A8_SRGB output (the hardware decodes each
//    8-bit code to its exact linear value) and writes it unmodified into the float target: no tone mapping, no
//    dither, and no _LINEAR_TO_SRGB_CONVERSION because requireSrgbConversion = targetTexture == null && ...
//    (UniversalCameraData.requireSrgbConversion). The nearest-sampling blit pass is used (our texture is Point).
//  * RGBA16F keeps 10 mantissa bits: the decoded codes are stored within 0.03 LSB of their exact value, so the
//    harness' Graphics.Blit into an R8G8B8A8_SRGB RT (GPU encode + round) reproduces the shown codes exactly.
//    An MSAA target receives the same value in every sample (full-screen triangle), so its resolve is exact too.
//  * On screen (no target texture) FinalBlit writes to the sRGB swapchain (GPU encode) or, when
//    Display.main.requiresSrgbBlitToBackbuffer, encodes in its shader: both reproduce the codes exactly.
// Zero GC allocations per frame: pooled RG textures (stable descriptors, cached names), pooled PassData, one
// static MaterialPropertyBlock (copied by the command buffer at record time), a cached static render func.
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Megastructure.Rendering
{
    public sealed class ThreadGradePass : ScriptableRenderPass
    {
        const int kMaxLevels = 10;

        // shader pass names (ThreadGrade.shader)
        const string kPassPrefilter = "ThreadBloomPrefilter";
        const string kPassDown = "ThreadBloomDown";
        const string kPassUp = "ThreadBloomUp";
        const string kPassGrade = "ThreadGrade";

        // property ids
        static readonly int s_BlitTexture = Shader.PropertyToID("_BlitTexture");
        static readonly int s_BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        static readonly int s_Low = Shader.PropertyToID("_ThGradeLow");
        static readonly int s_Noise = Shader.PropertyToID("_ThGradeNoise");
        static readonly int s_SrcSize = Shader.PropertyToID("_ThGradeSrcSize");
        static readonly int s_LowSize = Shader.PropertyToID("_ThGradeLowSize");
        static readonly int s_P0 = Shader.PropertyToID("_ThGradeP0");
        static readonly int s_P1 = Shader.PropertyToID("_ThGradeP1");
        static readonly int s_P2 = Shader.PropertyToID("_ThGradeP2");

        static readonly ProfilingSampler s_PrefilterSampler = new ProfilingSampler("ThreadBloom Prefilter");
        static readonly ProfilingSampler s_DownSampler = new ProfilingSampler("ThreadBloom Down");
        static readonly ProfilingSampler s_UpSampler = new ProfilingSampler("ThreadBloom Up");
        static readonly ProfilingSampler s_GradeSampler = new ProfilingSampler("ThreadGrade");

        static readonly string[] s_DownNames = new string[kMaxLevels];
        static readonly string[] s_UpNames = new string[kMaxLevels];
        const string kOutName = "_ThreadGradeOut";

        static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();
        static readonly BaseRenderFunc<PassData, RasterGraphContext> s_Execute = Execute;

        static bool s_WarnedPost, s_WarnedHdrOutput;

        static ThreadGradePass()
        {
            for (int i = 0; i < kMaxLevels; i++)
            {
                s_DownNames[i] = "_ThreadBloomDown" + i;
                s_UpNames[i] = "_ThreadBloomUp" + i;
            }
        }

        // ------------------------------------------------------------------------------------ state
        Material m_Material;
        Shader m_Shader;
        int m_PassPrefilter = -1, m_PassDown = -1, m_PassUp = -1, m_PassGrade = -1;
        bool m_Valid;
        static Texture2D s_Black;

        // per-record scratch (cameras are recorded one after another on the main thread)
        readonly TextureHandle[] m_Down = new TextureHandle[kMaxLevels];
        readonly TextureHandle[] m_Up = new TextureHandle[kMaxLevels];
        readonly Vector4[] m_Size = new Vector4[kMaxLevels];

        public ThreadGradePass()
        {
            profilingSampler = s_GradeSampler;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;   // camera colour is always an HDR intermediate we can sample
        }

        public bool IsValid => m_Valid;

        /// <summary>Bind the material and resolve its pass indices (cheap when nothing changed).</summary>
        public bool Bind(Material material)
        {
            Shader shader = material != null ? material.shader : null;
            if (material == m_Material && shader == m_Shader) return m_Valid;
            m_Material = material;
            m_Shader = shader;
            m_Valid = false;
            if (material == null || shader == null || !shader.isSupported) return false;
            m_PassPrefilter = material.FindPass(kPassPrefilter);
            m_PassDown = material.FindPass(kPassDown);
            m_PassUp = material.FindPass(kPassUp);
            m_PassGrade = material.FindPass(kPassGrade);
            m_Valid = m_PassPrefilter >= 0 && m_PassDown >= 0 && m_PassUp >= 0 && m_PassGrade >= 0;
            if (!m_Valid) Debug.LogWarning("[THREAD] ThreadGrade: material shader lacks the ThreadGrade passes; grade disabled");
            return m_Valid;
        }

        // ------------------------------------------------------------------------------------ pass data
        sealed class PassData
        {
            public TextureHandle src, src2, dst;
            public bool hasSrc2;
            public Texture fallback2;
            public Texture noise;
            public Material material;
            public int shaderPass;
            public Vector4 srcSize, src2Size, p0, p1, p2;
        }

        static void Execute(PassData d, RasterGraphContext ctx)
        {
            // Same maths as URP's internal RenderingUtils.GetFinalBlitScaleBias.
            RTHandle srcRt = d.src;
            Vector2 scale = srcRt != null && srcRt.useScaling
                ? new Vector2(srcRt.rtHandleProperties.rtHandleScale.x, srcRt.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;
            bool yflip = ctx.GetTextureUVOrigin(in d.src) != ctx.GetTextureUVOrigin(in d.dst);
            Vector4 scaleBias = yflip ? new Vector4(scale.x, -scale.y, 0f, scale.y) : new Vector4(scale.x, scale.y, 0f, 0f);

            MaterialPropertyBlock mpb = s_Mpb;
            mpb.Clear();
            Texture src = d.src;
            mpb.SetTexture(s_BlitTexture, src);
            mpb.SetVector(s_BlitScaleBias, scaleBias);
            Texture low = d.hasSrc2 ? (Texture)d.src2 : d.fallback2;
            if (low != null) mpb.SetTexture(s_Low, low);
            if (d.noise != null) mpb.SetTexture(s_Noise, d.noise);
            mpb.SetVector(s_SrcSize, d.srcSize);
            mpb.SetVector(s_LowSize, d.src2Size);
            mpb.SetVector(s_P0, d.p0);
            mpb.SetVector(s_P1, d.p1);
            mpb.SetVector(s_P2, d.p2);
            ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.shaderPass, MeshTopology.Triangles, 3, 1, mpb);
        }

        void AddPass(RenderGraph rg, string name, ProfilingSampler sampler, int shaderPass,
                     TextureHandle src, Vector4 srcSize, TextureHandle src2, Vector4 src2Size, TextureHandle dst,
                     Vector4 p0, Vector4 p1, Vector4 p2, Texture fallback2, Texture noise)
        {
            using (var builder = rg.AddRasterRenderPass<PassData>(name, out var d, sampler))
            {
                d.src = src;
                d.src2 = src2;
                d.hasSrc2 = src2.IsValid();
                d.dst = dst;
                d.fallback2 = fallback2;
                d.noise = noise;
                d.material = m_Material;
                d.shaderPass = shaderPass;
                d.srcSize = srcSize;
                d.src2Size = src2Size;
                d.p0 = p0; d.p1 = p1; d.p2 = p2;
                builder.UseTexture(src, AccessFlags.Read);                 // an MSAA camera colour is resolved here
                if (d.hasSrc2) builder.UseTexture(src2, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll); // full-screen write: DontCare load
                builder.SetRenderFunc(s_Execute);
            }
        }

        static Vector4 SizeVector(int w, int h) => new Vector4(w, h, 1f / w, 1f / h);

        // ------------------------------------------------------------------------------------ record
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!m_Valid || m_Material == null || !GradeState.Enabled) return;
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer) return;   // nothing to sample (requiresIntermediateTexture prevents it)
            if (!cameraData.resolveFinalTarget) return;          // grade exactly once per output: the last camera of a stack
            bool sceneView = cameraData.isSceneViewCamera;
            if (cameraData.postProcessEnabled)
            {
                // Contract (R1 option B): URP post-processing is OFF on the camera; otherwise URP would grade,
                // clamp and dither first. The Scene view follows its own toggle, so it is skipped silently.
                if (!sceneView && !s_WarnedPost)
                {
                    s_WarnedPost = true;
                    Debug.LogWarning("[THREAD] ThreadGrade skipped: URP post-processing is enabled on the camera (contract: renderPostProcessing = false)");
                }
                return;
            }
            if (cameraData.isHDROutputActive)
            {
                if (!s_WarnedHdrOutput)
                {
                    s_WarnedHdrOutput = true;
                    Debug.LogWarning("[THREAD] ThreadGrade skipped: HDR display output is active (the grade is SDR only)");
                }
                return;
            }

            TextureHandle src = resourceData.cameraColor;
            if (!src.IsValid()) return;
            TextureDesc srcDesc = renderGraph.GetTextureDesc(src);
            int W = srcDesc.width, H = srcDesc.height;
            if (srcDesc.sizeMode != TextureSizeMode.Explicit || W < 1 || H < 1)
            {
                W = cameraData.cameraTargetDescriptor.width;
                H = cameraData.cameraTargetDescriptor.height;
            }
            if (W < 1 || H < 1) return;
            Vector4 srcSize = SizeVector(W, H);

            // ---------------------------------------------------------------- parameters (GradeState, ThreadClock)
            float exposure = Mathf.Pow(2f, GradeState.ExposureEV);
            float threshold = Mathf.Max(0f, GradeState.BloomThreshold);
            float knee = Mathf.Max(1e-4f, threshold * (float)ThreadGradeMath.BloomKneeFraction);
            float intensity = Mathf.Max(0f, GradeState.BloomIntensity);
            float scatter = (float)ThreadGradeMath.ScatterLerp(GradeState.BloomScatter);
            float satFactor = (float)ThreadGradeMath.SaturationFactor(GradeState.Saturation);
            float contrastK = (float)ThreadGradeMath.ContrastK(GradeState.Contrast);
            float vignette = Mathf.Clamp01(GradeState.Vignette);
            float fade = sceneView ? 1f : 1f - Mathf.Clamp01(GradeState.FadeBlack);
            float amplitude = Mathf.Max(0f, (float)ThreadGradeMath.DitherAmplitude(GradeState.DitherLsb, GradeState.Darkness));
            R2Sequence.TileOffset(R2Sequence.DitherFrame(ThreadClock.Frame, ThreadClock.FreezeDither), BlueNoise.Size, out int ox, out int oy);
            Texture noise = BlueNoise.Get();
            if (s_Black == null) s_Black = Texture2D.blackTexture;
            Texture black = s_Black;

            // ---------------------------------------------------------------- bloom pyramid
            TextureHandle none = TextureHandle.nullHandle;
            TextureHandle bloom = none;
            Vector4 bloomSize = new Vector4(1f, 1f, 1f, 1f);
            if (intensity > 0f)
            {
                int total = ThreadGradeMath.BloomTotalLevels(W, H, kMaxLevels);
                int skip = Mathf.Min(ThreadGradeMath.BloomSkipLevels(H), total - 1);
                var desc = new TextureDesc(1, 1)
                {
                    format = GraphicsFormat.R16G16B16A16_SFloat,
                    filterMode = FilterMode.Bilinear,          // the ctor default would be Point
                    wrapMode = TextureWrapMode.Clamp,          // the ctor default would be Repeat
                    msaaSamples = MSAASamples.None,
                    clearBuffer = false,
                };
                int w = W, h = H;
                for (int i = 0; i < total; i++)
                {
                    w = Mathf.Max(1, w >> 1);
                    h = Mathf.Max(1, h >> 1);
                    m_Size[i] = SizeVector(w, h);
                    desc.width = w;
                    desc.height = h;
                    desc.name = s_DownNames[i];
                    m_Down[i] = renderGraph.CreateTexture(desc);
                    if (i >= skip && i < total - 1)
                    {
                        desc.name = s_UpNames[i];
                        m_Up[i] = renderGraph.CreateTexture(desc);
                    }
                }

                Vector4 pf = new Vector4(threshold, knee, (float)ThreadGradeMath.BloomClamp, exposure);
                AddPass(renderGraph, kPassPrefilter, s_PrefilterSampler, m_PassPrefilter, src, srcSize, none, srcSize, m_Down[0],
                        pf, Vector4.zero, Vector4.zero, black, null);
                for (int i = 1; i < total; i++)
                    AddPass(renderGraph, kPassDown, s_DownSampler, m_PassDown, m_Down[i - 1], m_Size[i - 1], none, m_Size[i - 1], m_Down[i],
                            Vector4.zero, Vector4.zero, Vector4.zero, black, null);

                TextureHandle low = m_Down[total - 1];
                Vector4 lowSize = m_Size[total - 1];
                Vector4 up = new Vector4(scatter, 0f, 0f, 0f);
                for (int i = total - 2; i >= skip; i--)
                {
                    AddPass(renderGraph, kPassUp, s_UpSampler, m_PassUp, m_Down[i], m_Size[i], low, lowSize, m_Up[i],
                            up, Vector4.zero, Vector4.zero, black, null);
                    low = m_Up[i];
                    lowSize = m_Size[i];
                }
                bloom = low;
                bloomSize = lowSize;
            }

            // ---------------------------------------------------------------- grade + 8-bit output
            var od = new TextureDesc(W, H)
            {
                format = GraphicsFormat.R8G8B8A8_SRGB,    // the one and only 8-bit quantisation happens here
                filterMode = FilterMode.Point,           // FinalBlit then picks its nearest-sampling pass
                wrapMode = TextureWrapMode.Clamp,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                name = kOutName,
            };
            TextureHandle dst = renderGraph.CreateTexture(od);
            Vector4 g0 = new Vector4(exposure, intensity, satFactor, contrastK);
            Vector4 g1 = new Vector4(vignette, W / (float)H, fade, amplitude);
            Vector4 g2 = new Vector4(ox, oy, (float)ThreadGradeMath.NeutralStart, (float)ThreadGradeMath.NeutralDesaturation);
            AddPass(renderGraph, kPassGrade, s_GradeSampler, m_PassGrade, src, srcSize, bloom, bloomSize, dst,
                    g0, g1, g2, black, noise);

            resourceData.cameraColor = dst;   // URP FinalBlit copies it 1:1 (research R1 B1)
        }
    }
}
