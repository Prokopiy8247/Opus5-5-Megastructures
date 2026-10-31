# R6: Banding mitigation and dithering (Thread, URP 17.6 / Unity 6000.6.0f1)

Researcher: R6 (read-only). Date: 2026-10-29. Ground truth is the installed package source:
`Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184` (URP below) and
`Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e` (Core below). Every number marked "(sim)" was computed in this session with numpy (`py -3.10 -I`) and was not taken from a paper.

## 0. Decisions in brief

1. **Use exactly one dither, in ThreadGrade, as the last operation.** Turn URP Camera Dithering off (`UniversalAdditionalCameraData.dithering = false`). URP's own dither is fixed at ±1 LSB TPDF, uses 16x16 textures and a `Time.frameCount` random offset, and runs in Uber or FinalPost. That conflicts with spec 14.7.1 (64x64 void-and-cluster tile, R2 offset, `-freeze-dither`, x1.5 when D > 0.5).
2. **HDR precision must be 64-bit.** Both project RP assets currently use `m_HDRColorBufferPrecision: 0` (= `_32Bits`, R11G11B10) at `Assets/Settings/PC_RPAsset.asset:27`. Set it to 1 (`_64Bits`, R16G16B16A16_SFloat).
   - In R11G11B10, one representable step in a bright sky is up to **2.6 sRGB codes in blue and 1.3 in red/green**. No ±1 LSB dither can hide that.
   - In FP16 the step is at most 0.08 LSB (section 2.3).
3. **URP post-processing stays on (for bloom), so the Uber pass always colour-grades.** UberPost.shader:271-274 says "Color grading is always enabled when post-processing/uber is active".
   - In LDR grading mode (project default, `m_ColorGradingMode: 0`), Uber clamps HDR to [0,1] (`ApplyTonemap` returns `saturate`, Common.hlsl:101-111). ThreadGrade at AfterRenderingPostProcessing would then receive clipped colour.
   - In HDR grading mode, the identity LogC LUT at size 32 adds a smooth scallop error of up to **1.29 LSB** (sim). At size 64 or 65 the error is 0.30 LSB.
   - So: either disable URP post and do bloom inside ThreadGrade (cleanest), or use HDR grading with `colorGradingLutSize = 64`.
4. **Dither math: add TPDF noise in sRGB-encoded space, then convert back to linear exactly.** The 8-bit quantiser is uniform in sRGB space. This is the same as URP's linear-space branch (Common.hlsl:202): `out = SRGBToLinear(LinearToSRGB(c) + n/255)` with the exact Core functions. Use the same noise value for R, G and B (monochrome), because luma is what survives 4:2:0 encoding.
5. **Noise source.** A 64x64 void-and-cluster rank map (sigma 1.5, toroidal), stored as `u = (rank+0.5)/4096` in an RFloat point/repeat texture.
   - Remap: `t = 2u-1; n = sign(t)*(1-sqrt(1-|t|))`. This is URP's own remap (Common.hlsl:190-192). It is monotonic, so the blue-noise ordering is kept.
   - Each frame, offset the tile by an integer R2 texel offset. `-freeze-dither` sets the frame index to 0.
6. **Amplitude: ±1.0 LSB, rising smoothly to ±1.5 LSB for D > 0.5.** Temporal mean frame difference (sim) is 0.47 codes at ±1.0, 0.70 at ±1.5 and 0.93 at ±2.0. So in temporal mode, ±2 LSB breaks the 0.8 budget of 14.10.3 where D ≤ 0.5. Above ±1.7 LSB, use static mode.
7. **Static vs temporal.** The shipped build animates (14.7.1 #4). For capture mode, implement both and let the CRF 23/30 encode test decide (11.12). Expectation, which the test must confirm: static or slow-temporal dither survives x264 better on slow shots, because inter prediction and mb-tree keep a predictable pattern.
8. **The spec banding metric is blind in places.** Taken literally, it cannot detect 1-code bands (step test) and fails correctly dithered slow gradients (run test). Keep it, but add the residual metric of section 6.2, which separates dithered from undithered by a factor of 5 or more (sim). Log the formula in DEVELOPMENT.md (Part 2 rule 2).

## 1. Verified facts in the URP 17.6 / Core source

### 1.1 `ApplyDithering`, URP Shaders/PostProcessing/Common.hlsl:188-206
- Line 191: `float noise = SAMPLE_TEXTURE2D(BlueNoiseTexture, BlueNoiseSampler, uv * scale + offset).a * 2.0 - 1.0;`
- Line 192: `noise = FastSign(noise) * (1.0 - sqrt(1.0 - abs(noise)));` with the comment "Symmetric triangular distribution on [-1,1] with maximal density at 0".
- Lines 194-195, gamma colour space: `input += noise / 255.0;`
- Lines 196-200, HDR_INPUT (HDR display output): divide by paper white, then `SRGBToLinear(LinearToSRGB(input) + noise / 255.0)`, then multiply back. The comment says the "fast" versions clamp.
- Line 202, linear colour space: `input = GetSRGBToLinear(GetLinearToSRGB(input) + noise /255.0);`. `GetLinearToSRGB` is exact unless `_USE_FAST_SRGB_LINEAR_CONVERSION` is set (Common.hlsl:51-82).
- Amplitude is hard-wired: TPDF on [-1,1] times 1 LSB, so ±1 LSB (2 LSB total width). It is not configurable.

### 1.2 Where URP applies it: only in the last post pass
- `UberPostProcessPass.cs:203-207`: film grain and dither are set up only when `m_IsFinalPass`. `PostProcess.cs:137`: "If hasFinalPass == true, Film Grain and Dithering are setup in the final pass".
- `PostProcess.cs:208` and `:239`: Uber gets `isFinalPass = !hasFinalPass`. `PostProcess.cs:387-390`: FinalPost gets the dither texture.
- UberPost.shader:296-302 applies it after the optional manual LinearToSRGB, then `color = max(color, 0)`. FinalPost.shader:115-118 applies it after FXAA, grain and `_LINEAR_TO_SRGB_CONVERSION`.
- `hasFinalPass` (= `applyFinalPostProcessing`) is true only when one of these is set: FXAA, FSR1 upscaling, or TAA with `contrastAdaptiveSharpening > 0` (`UniversalRendererRenderGraph.cs:1397-1400`).
- **FinalPost always runs after user passes at AfterRenderingPostProcessing** (comment at :1391-1394, call at :1481-1485). So any FXAA, FSR or TAA-CAS would filter ThreadGrade's dither. Keep all three off, or move the dither.
- If a user pass exists at AfterRenderingPostProcessing, Uber does not target the back buffer (`isTargetBackbuffer` at :1435 requires `!hasPassesAfterPostProcessing`). The camera colour stays floating point (64-bit when set) up to ThreadGrade.
- If nobody switched to the back buffer, `FinalBlitPass` copies cameraColor to it (:1490-1498). It uses the nearest or bilinear pass depending on the source filter mode (`FinalBlitPass.cs:117,226`), which is exact at 1:1 scale.
- The manual sRGB keyword is set from `cameraData.requireSrgbConversion` (`FinalBlitPass.cs:200`). That equals `Display.main.requiresSrgbBlitToBackbuffer` (`UniversalCameraData.cs:276-287`), normally false on Windows D3D11/12 with an sRGB back buffer. Log it at start-up.

### 1.3 URP's dither textures and how it animates them
- `PostProcessData.cs:237-238`: `[ResourceFormattedPaths("Textures/BlueNoise16/L/LDR_LLL1_{0}.png", 0, 32)] public Texture2D[] blueNoise16LTex;`. These are 32 textures of **16x16** px (checked from the PNG header). The naming matches C. Peters' free blue-noise set.
- `PostProcess.cs:112-127`: the texture index cycles each frame. `PostProcessUtils.cs:202-214`: the offset is `Random.InitState(Time.frameCount); Random.value` twice. It is static only when `LWRP_DEBUG_STATIC_POSTFX` is defined.
- `PostProcessUtils.cs:217-223`: tiling = `(pixelWidth/texW, pixelHeight/texH, offX, offY)`, giving 1 texel per pixel with `sampler_PointRepeat`.
- The camera flag comes from `UniversalAdditionalCameraData.dithering` (`UniversalAdditionalCameraData.cs:917-923`, get/set) and is copied at `UniversalRenderPipeline.cs:1745`. It is forced false for the Scene view (:1734).
- Also present: `Textures/BlueNoise64/L/LDR_LLL1_0.png` (64x64, used as `blueNoise64LTex` for LOD cross-fade, `UniversalRenderPipeline.cs:2447-2467`) and `Textures/BayerMatrix.png`. These are imported assets; Part 6 forbids reusing them, so generate our own.

### 1.4 HDR buffer precision
- `UniversalRenderPipelineAsset.cs:166-175`: `enum HDRColorBufferPrecision { _32Bits, _64Bits }`. The tooltip says R11G11B10 "can cause a subtle blue/yellow banding ... due to lower precision of the blue component".
- Default `_32Bits` at :452. Public get/set `hdrColorBufferPrecision` at :1096-1099.
- `UniversalRenderPipelineCore.cs:1477-1491`: with HDR on, the format is B10G11R11_UFloatPack32 unless alpha is needed or `_64Bits` is set. Otherwise it is R16G16B16A16_SFloat.
- Project state: `Assets/Settings/PC_RPAsset.asset:26-27` has `m_SupportsHDR: 1`, `m_HDRColorBufferPrecision: 0`. Line 79 has `m_ColorGradingMode: 0` (LowDynamicRange) and line 80 has `m_ColorGradingLutSize: 32`. Mobile_RPAsset is the same.
- LUT size is clamped to 16..65 (`UniversalRenderPipelineAsset.cs:637,642,1644-1647`).

### 1.5 Core helpers (Core ShaderLibrary)
- `Color.hlsl:76-120`: exact piecewise `SRGBToLinear` (≤ 0.04045 → c/12.92) and `LinearToSRGB` (≤ 0.0031308 → c*12.9232102, else 1.055*c^(1/2.4)-0.055). The two linear-segment slopes differ by 0.02 %, which is under 0.003 LSB and negligible.
- `Color.hlsl:123-162`: `FastLinearToSRGB` uses `saturate`, and the Fast functions are approximations. Do not use them for dither.
- `Common.hlsl:817,838`: `CopySign`, `FastSign` (±0 → +1).
- `API/D3D11.hlsl:147`: `#define LOAD_TEXTURE2D(textureName, unCoord2) textureName.Load(int3(unCoord2, 0))`.
- `Random.hlsl:91-99`: `InterleavedGradientNoise(float2 pixCoord, int frameCount)`, magic `float3(0.06711056, 0.00583715, 52.9829189)`, per-frame `pixCoord += frameCount * float2(2.083, 4.867)`, cited "[Jimenez 2014]".
- URP `ShaderLibrary/Core.hlsl` does **not** include `Color.hlsl`, so include it explicitly. URP's post-processing `Common.hlsl:4` does.
- Unity API, checked in `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml`: `Texture2D(int,int,TextureFormat,bool,bool)`, `Texture2D.SetPixelData<T>(T[],int,int)`, `TextureFormat.RFloat`, `Shader.SetGlobalTexture(int,Texture)`, `Display.requiresSrgbBlitToBackbuffer`.

## 2. Theory, with the numbers this game needs

### 2.1 Why triangular PDF of ±1 LSB, and why in display space
- Non-subtractive dither with TPDF of total width 2 LSB is the smallest dither for which both the mean and the variance of the total error are independent of the signal (Wannamaker, Lipshitz and Vanderkooy). With RPDF (uniform) noise of 1 LSB, bands disappear but the noise power still varies with the signal ("noise modulation"), which looks like faint bands of graininess. Gjøl's "Banding in Games" (Playdead, cited by Blender as slide 27) and Wronski's 2016 series show the same thing visually.
- The total error RMS is `sqrt(a²/6 + 1/12)` LSB: 0.500 at a = 1, 0.677 at a = 1.5, 0.866 at a = 2. The simulation matched to three decimals.
- The quantiser is uniform in the **encoded** (sRGB, gamma) domain, so the dither must be one encoded code wide. Adding 1/255 in linear space would be about ±12 codes near black (linear 0.001 is sRGB 0.0129, code 3.3) and far less than a code in highlights.
- When the target is sRGB-UNorm and the hardware encodes, write `SRGBToLinear(LinearToSRGB(c) + n/255)`. The hardware then reproduces `LinearToSRGB(c) + n/255` before rounding.
- When the target is a float intermediate that FinalBlit later copies to the sRGB back buffer, the same formula holds, provided the intermediate keeps enough precision (2.3) and the copy is 1:1.

### 2.2 Clipping near 0 and 255
- TPDF ±1 on a code-0 input gives a mean output of 0.125 codes. ±1.5 gives 0.223 codes (sim). Pure-black or pure-white regions are lifted or darkened.
- Fix: fade the noise inside 0.5 LSB of either end, `edge = saturate(min(s,1-s)*510)`. This keeps pure black and pure white exact, which helps the black-frame and white-frame checks, and does not touch the night sky (code 4 and up).

### 2.3 Storage precision in sRGB LSB (step size at the worst point, L just above 0.5)
Step in sRGB codes is about `(S+0.055)/2.4 × relative mantissa step × 255`:

| Format | Mantissa bits | Relative step | Worst step in sRGB LSB | Verdict |
|---|---|---|---|---|
| R11G11B10 blue | 5 | 2^-5 | ~2.6 | unusable for gradients |
| R11G11B10 red/green | 6 | 2^-6 | ~1.3 | bands visible |
| R16G16B16A16_SFloat | 10 | 2^-10 | ~0.08 | OK, and it keeps the dither through FinalBlit |
| R8G8B8A8_SRGB | n/a | 1 code | 1.0 | the final quantiser; dither goes right before it |

### 2.4 URP colour grading LUT error (relevant if URP post stays on)
- In HDR grading, Uber does `saturate(LinearToLogC(c))` and then a trilinear LUT (Common.hlsl:119-121).
- Even with an identity grade, linear interpolation of the LogC→linear curve between nodes gives a smooth periodic error (sim, 0.0005 < L ≤ 1):

| LUT size | Max error in sRGB LSB |
|---|---|
| 32 | 1.285 (at L ≈ 0.96, the day sky) |
| 64 | 0.310 |
| 65 | 0.298 |

- The error is C0-continuous, so it shows as slope kinks or gentle waves, not as steps. The residual metric (6.2) catches it.
- In LDR grading the LUT is linear (identity is exact), but HDR is clamped to [0,1] before ThreadGrade.

## 3. Techniques

### 3.1 Interleaved gradient noise (Jimenez 2014)
- Source: SIGGRAPH 2014 Advances course talk, "Next Generation Post Processing in Call of Duty: Advanced Warfare". It was used for 8-tap shadow filtering.
- Formula: `IGN(x,y) = frac(52.9829189 * frac(0.06711056*x + 0.00583715*y))`, with x and y in pixels. Every 3x3 block forms a low-discrepancy set, and there are no texture fetches.
- Animation: demofox suggests offsetting x and y by `5.588238*(frame % 64)`. Core uses `+frame*(2.083, 4.867)`.
- Downsides: visible diagonal structure and some aliasing (Wronski). It is a fallback only if the blue-noise texture cannot be bound. It also needs the same TPDF remap.

### 3.2 Playdead INSIDE (GDC 2016 "Low Complexity, High Fidelity", Gjøl and Svendsen; DTU 2014 "Banding in Games")
What the talks say about method:
- Dither in the final pass, right before 8-bit output.
- Use blue-noise-shaped noise with a triangular PDF of 2 LSB total width.
- Remap uniform to TPDF by inverting the CDF.

Two equivalent remaps (both give `P(|y|<t) = 1-(1-t)²`):
- **Monotonic** (URP Common.hlsl:191-192; the form this task attributes to Playdead): `t = 2u-1; y = sign(t)*(1-sqrt(1-|t|))`, range [-1,1]. Because it is monotonic, it keeps the rank order and therefore the blue-noise threshold structure. **Use this one.**
- **Shadertoy 4t2SDh form** (quoted in Blender commit 852cdd476b5): `o = 2u-1; y = o*rsqrt(|o|); y = max(-1,y); y = y - sign(o) + 0.5`. Its range is [-0.5,1.5], intended for `floor()` quantisation. It is discontinuous at u = 0.5, which scrambles blue-noise ordering, so avoid it with blue noise.
- Using TPDF from two uniform samples (`r1 - r2`) is fine for white noise but destroys the blue-noise spectrum.

Note: the 13.3 MB GDC 2016 PDF (Git LFS) exceeded the fetch limit. The attribution therefore rests on the Playdead publications index, the GDC Vault abstract ("properly using dithering to get rid of distracting artefacts"), the Blender commit citing Gjøl, and URP's identical code.

### 3.3 Void-and-cluster (Ulichney 1993, SPIE 1913 pp. 332-343)
Energy: `E(p) = Σ_q B(q)·exp(-d²(p,q)/(2σ²))` with toroidal distance `d` on a 64x64 grid and σ = 1.5 (2σ² = 4.5).
- Tightest cluster = the 1-pixel with the highest E. Largest void = the 0-pixel with the lowest E.

Steps:
0. **Initial pattern.** Set ⌈10 %⌉ of pixels to 1 at random (410 of 4096).
1. **Prototype loop.** Repeat until stable: remove the tightest cluster, then insert at the largest void. Stop when the void is the pixel just removed (put it back). Typically about 150 swaps (sim).
2. **Phase 1.** Starting from the prototype, repeatedly remove the tightest cluster. Its rank is the number of 1s remaining after removal (409 down to 0).
3. **Phase 2.** Restart from the prototype and repeatedly fill the largest void. Its rank is the number of 1s before insertion, until N/2.
4. **Phase 3.** Same as phase 2 with 0s as the minority: fill the tightest cluster of 0s, up to N-1.
   - **Identity:** with a linear energy and a toroidal kernel, `E0 = Σ_all K - E1`. So "max E0 among 0s" is the same as "min E1 among 0s", and phase 3 is the same code as phase 2.
   - This was verified independently with FFT convolutions: 300/300 identical picks (sim).
5. **Rank to uniform.** `u = (rank + 0.5)/N` lies in (0,1), has a flat histogram and mean 0.5. TPDF via the monotonic remap gives variance 0.1667 (= 1/6) and range ±0.984 (sim).

Cost:
- Precompute the toroidal kernel table K[dy*64+dx]. Each toggle adds or subtracts K into E: 4096 multiply-adds, no `exp`. Each pick is a 4096-element scan.
- Total is about 4.8k toggles and 4.8k scans, roughly 40M simple operations: well under 1 s in Mono or IL2CPP. The vectorised numpy version took 0.25 s.
- Truncating the kernel at radius 9 or more is lossless in float, since exp(-81/4.5) = 1.5e-8, but it is not needed. Demofox's quality loss came from truncating at 3σ.

Quality references for tests (sim; σ = 1.5, seed 1):
- Variance of 4x4 block means: 0.00091 (white noise 0.0052).
- Variance of 8x8 block means: 0.00011 (white 0.0013).
- Right-neighbour correlation: -0.27 (white about 0).
- Low-band power (|f| < 0.1) is 0.05 against white 346. The spectrum peaks at 0.3-0.75 cycles/px.
- Peters uses σ = 1.9 for more isotropy. The spec fixes 1.5, which looked fine at 64².

### 3.4 R2 sequence (Martin Roberts 2018)
- g = 1.32471795724474602596 (plastic number, g³ = g + 1), a1 = 1/g = 0.7548776662466927, a2 = 1/g² = 0.5698402909980532.
- Sequence: `x_n = frac(0.5 + a1·n)`, `y_n = frac(0.5 + a2·n)`.
- Integer tile offsets: `(floor(64·x_n), floor(64·y_n))` = (32,32), (16,4), (0,40), (48,13), (33,49), (17,22) for n = 0..5 (sim).
- Consecutive offsets jump by about 48 and 36 texels, so the tile never appears to slide (no "crawling") and every offset is used evenly over time. Compute on the CPU in double, because n·a1 loses precision in float after a few thousand frames.
- Demofox notes that adding a golden-ratio value offset to blue noise gives it low-frequency content. A **spatial** toroidal shift keeps the spectrum of every frame exactly blue, which is why the spec's R2 spatial offset is preferable.

### 3.5 Static vs temporal

| | Static tile (`-freeze-dither`, or capture variant) | Temporal R2 offset every frame |
|---|---|---|
| Perception at 60 fps | fixed screen-space pattern; can read as "dirty screen" on pans | the eye averages, so it looks smoother |
| Strict frame-difference tests | about 0 difference | 0.47 (±1), 0.70 (±1.5), 0.93 (±2) codes mean abs diff (sim) |
| Through H.264 / VP9 | predictable for inter prediction, so cheap and kept by mb-tree (hypothesis) | unpredictable: P/B frames lose it first, so bands "breathe" with the GOP (hypothesis) |

- An intermediate variant worth testing: advance the R2 index every k = 2 or 4 frames.

## 4. Code for builders

### 4.1 `ThreadBlueNoise.cs` (runtime, no Editor API; runs at load)
```csharp
using System;
using UnityEngine;

namespace Thread.Rendering
{
    /// Void-and-cluster blue noise (Ulichney 1993) on a toroidal n x n grid. n must be a power of two.
    public static class ThreadBlueNoise
    {
        public static int[] GenerateRanks(int n = 64, float sigma = 1.5f, int seed = 20261029, float initialDensity = 0.1f)
        {
            if (n < 4 || (n & (n - 1)) != 0) throw new ArgumentException("n must be a power of two >= 4", nameof(n));
            int N = n * n, mask = n - 1, shift = 0;
            while ((1 << shift) < n) shift++;

            // Toroidal Gaussian kernel: kernel[((y - py) & mask) * n + ((x - px) & mask)]
            var kernel = new float[N];
            double inv2s2 = 1.0 / (2.0 * sigma * sigma);
            for (int dy = 0; dy < n; dy++)
            {
                int wy = Math.Min(dy, n - dy);
                for (int dx = 0; dx < n; dx++)
                {
                    int wx = Math.Min(dx, n - dx);
                    kernel[dy * n + dx] = (float)Math.Exp(-(wx * wx + wy * wy) * inv2s2);
                }
            }

            var bits = new bool[N];
            var energy = new float[N];

            void Splat(int p, float sign)
            {
                int px = p & mask, py = p >> shift;
                for (int y = 0; y < n; y++)
                {
                    int kRow = ((y - py) & mask) << shift, eRow = y << shift;
                    for (int x = 0; x < n; x++)
                        energy[eRow + x] += sign * kernel[kRow + ((x - px) & mask)];
                }
            }
            int TightestCluster()
            {
                int best = -1; float bv = float.NegativeInfinity;
                for (int i = 0; i < N; i++) if (bits[i] && energy[i] > bv) { bv = energy[i]; best = i; }
                return best;
            }
            int LargestVoid()
            {
                int best = -1; float bv = float.PositiveInfinity;
                for (int i = 0; i < N; i++) if (!bits[i] && energy[i] < bv) { bv = energy[i]; best = i; }
                return best;
            }

            // 0. Random initial binary pattern (deterministic seed).
            var rng = new System.Random(seed);
            int ones = Math.Max(1, (int)Math.Round(N * initialDensity));
            var idx = new int[N];
            for (int i = 0; i < N; i++) idx[i] = i;
            for (int i = 0; i < ones; i++)
            {
                int j = rng.Next(i, N);
                (idx[i], idx[j]) = (idx[j], idx[i]);
                bits[idx[i]] = true; Splat(idx[i], +1f);
            }
            // 1. Prototype: move tightest cluster -> largest void until stable.
            for (int guard = 0; guard < 4 * N; guard++)
            {
                int c = TightestCluster(); bits[c] = false; Splat(c, -1f);
                int v = LargestVoid();     bits[v] = true;  Splat(v, +1f);
                if (v == c) break;
            }
            var protoBits = (bool[])bits.Clone();
            var protoEnergy = (float[])energy.Clone();

            var rank = new int[N];
            // Phase 1: remove tightest clusters, ranks ones-1 .. 0.
            for (int r = ones - 1; r >= 0; r--) { int c = TightestCluster(); rank[c] = r; bits[c] = false; Splat(c, -1f); }
            // Phases 2 + 3 (identical by E0 = sum(K) - E1): fill largest voids, ranks ones .. N-1.
            Array.Copy(protoBits, bits, N); Array.Copy(protoEnergy, energy, N);
            for (int r = ones; r < N; r++) { int v = LargestVoid(); rank[v] = r; bits[v] = true; Splat(v, +1f); }
            return rank;
        }

        /// RFloat, point, repeat, linear (non-sRGB); value = (rank + 0.5) / N, uniform in (0,1).
        public static Texture2D CreateTexture(int[] rank, int n, string name = "ThreadBlueNoise64")
        {
            var tex = new Texture2D(n, n, TextureFormat.RFloat, false, true)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0, hideFlags = HideFlags.DontSave
            };
            var data = new float[n * n];
            float invN = 1f / (n * n);
            for (int i = 0; i < data.Length; i++) data[i] = (rank[i] + 0.5f) * invN;
            tex.SetPixelData(data, 0);
            tex.Apply(false, true); // upload, then free the CPU copy
            return tex;
        }
    }
}
```
- Run `GenerateRanks` on a worker thread (`System.Threading.Tasks.Task.Run`) if a hitch matters. Call `CreateTexture` on the main thread.
- Row orientation (Unity row 0 = bottom) does not matter for a toroidal tile.
- Unit tests:
  - ranks are a permutation of 0..4095;
  - variance of 4x4 block means is under 0.0015;
  - right-neighbour correlation is under -0.15;
  - same seed gives identical ranks;
  - runtime is under 1 s in the built player (log it).

### 4.2 `ThreadDitherClock.cs` (R2 offset, `-freeze-dither`, globals)
```csharp
using System;
using UnityEngine;

namespace Thread.Rendering
{
    public static class ThreadDitherClock
    {
        const double G = 1.32471795724474602596;          // plastic number
        const double A1 = 1.0 / G, A2 = 1.0 / (G * G);    // 0.75487766624669276, 0.56984029099805327
        public static readonly int BlueNoiseId = Shader.PropertyToID("_ThreadBlueNoise");
        public static readonly int ParamsId    = Shader.PropertyToID("_ThreadDitherParams");
        public static bool Frozen = Array.IndexOf(Environment.GetCommandLineArgs(), "-freeze-dither") >= 0;

        static double Frac(double v) => v - Math.Floor(v);

        public static Vector2Int R2Offset(long frameIndex, int tile = 64)
        {
            if (Frozen) frameIndex = 0;
            int ox = (int)(Frac(0.5 + A1 * frameIndex) * tile) & (tile - 1);
            int oy = (int)(Frac(0.5 + A2 * frameIndex) * tile) & (tile - 1);
            return new Vector2Int(ox, oy);
        }

        /// amplitudeLSB: 1.0 by default; spec x1.5 for D > 0.5 -- smoothed to avoid a visible pop:
        public static float AmplitudeFor(float darknessD) => 1f + 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.55f, darknessD));

        /// Call once per rendered frame before the camera renders (e.g. from ThreadGrade's RecordRenderGraph or LateUpdate).
        /// frameIndex: the capture harness' virtual frame counter (deterministic), Time.frameCount only in free play.
        public static void Push(long frameIndex, float darknessD)
        {
            var o = R2Offset(frameIndex);
            Shader.SetGlobalVector(ParamsId, new Vector4(o.x, o.y, AmplitudeFor(darknessD), 0f));
        }
    }
}
```
At load: `Shader.SetGlobalTexture(ThreadDitherClock.BlueNoiseId, ThreadBlueNoise.CreateTexture(ThreadBlueNoise.GenerateRanks(), 64));`

If the material is owned by the pass, `material.SetVector` / `SetTexture` works just as well. Then set the camera flags: `cam.GetUniversalAdditionalCameraData().dithering = false;` and on the pipeline asset `asset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;` (set it in the asset file; runtime changes to assets persist in the Editor).

### 4.3 `ThreadDither.hlsl` (include from ThreadGrade's fragment shader only; no material shader dithers)
```hlsl
#ifndef THREAD_DITHER_INCLUDED
#define THREAD_DITHER_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"   // exact SRGBToLinear / LinearToSRGB

TEXTURE2D(_ThreadBlueNoise);   // 64x64 RFloat, (rank+0.5)/4096, read with Load (no sampler needed)
float4 _ThreadDitherParams;    // xy = integer R2 texel offset [0,63], z = amplitude in LSB (1.0 .. 1.5), w = unused

// Uniform [0,1) -> symmetric triangular PDF on [-1,1], monotonic (same formula as URP Common.hlsl:191-192).
float ThreadUniformToTPDF(float u)
{
    float t = u * 2.0 - 1.0;
    return FastSign(t) * (1.0 - sqrt(1.0 - abs(t)));
}

// positionSS = SV_Position.xy of the FINAL-resolution pass (pixel centres at .5).
float ThreadDitherTPDF(float2 positionSS)
{
    uint2 p = (uint2(positionSS) + uint2(_ThreadDitherParams.xy)) & 63u;
    return ThreadUniformToTPDF(LOAD_TEXTURE2D(_ThreadBlueNoise, p).r);
}

// displayLinear: tone-mapped, display-referred LINEAR colour in [0,1] (what an *_SRGB target expects).
// Returns LINEAR colour whose hardware sRGB encode equals LinearToSRGB(displayLinear) + n/255 (TPDF, +-amplitude LSB).
// Valid when writing (a) the sRGB 8-bit back buffer or (b) an R16G16B16A16_SFloat intermediate copied 1:1 by FinalBlit.
float3 ThreadApplyDither(float3 displayLinear, float2 positionSS)
{
    float  n    = ThreadDitherTPDF(positionSS) * _ThreadDitherParams.z * (1.0 / 255.0); // same n for R,G,B
    float3 s    = LinearToSRGB(saturate(displayLinear));
    float3 edge = saturate(min(s, 1.0 - s) * 510.0);   // fade within 0.5 LSB of 0/255: pure black/white stay exact
    s = saturate(s + n * edge);
#if UNITY_COLORSPACE_GAMMA
    return s;                                            // gamma project: the target already stores encoded values
#else
    return SRGBToLinear(s);
#endif
}

// Variant for a target that needs manual encoding (Display.main.requiresSrgbBlitToBackbuffer == true / UNorm target):
float3 ThreadApplyDitherEncoded(float3 displayLinear, float2 positionSS)
{
    float  n    = ThreadDitherTPDF(positionSS) * _ThreadDitherParams.z * (1.0 / 255.0);
    float3 s    = LinearToSRGB(saturate(displayLinear));
    float3 edge = saturate(min(s, 1.0 - s) * 510.0);
    return saturate(s + n * edge);                       // write directly to the UNorm (non-sRGB) target
}
#endif
```
Fragment usage (ThreadGrade, Blit.hlsl `Varyings` = `positionCS : SV_POSITION`, `texcoord`, Core `Runtime/Utilities/Blit.hlsl:33-38`):
```hlsl
float4 FragThreadGrade(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float3 hdr  = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0).rgb;
    float3 disp = ThreadToneAndGrade(hdr);            // exposure, Neutral curve, sat/contrast, vignette (float math)
    return float4(ThreadApplyDither(disp, input.positionCS.xy), 1.0);   // LAST operation
}
```
Optional decorrelated-channel variant for the encode ladder: use three loads at `p`, `p+uint2(21,43)` and `p+uint2(43,21)`. This lowers luma noise to about 0.64× after RGB→Y, so it is not the default.

### 4.4 Configuration checklist (verify in the built player log)
- `asset.supportsHDR == true`, `asset.hdrColorBufferPrecision == _64Bits`, and the camera colour descriptor format is R16G16B16A16_SFloat (log `renderGraph.GetTextureDesc(resourceData.cameraColor).format` once).
- Every camera has `dithering == false`, and ThreadGrade runs once per frame per output camera.
- URP Tonemapping is None, no Film Grain, no FXAA, no FSR, TAA CAS = 0. Render scale is 1.0 and dynamic resolution is off. Otherwise the noise is resampled, or FinalPost runs after the dither.
- If URP post is on (bloom): `colorGradingMode = HighDynamicRange` and `colorGradingLutSize = 64` (2.4). Better: URP post off and ThreadGrade does the bloom.
- HDR display output is off (`allowHDROutput = false`), so the back buffer is 8-bit sRGB. Log `Display.main.requiresSrgbBlitToBackbuffer` and pick `ThreadApplyDither` or `ThreadApplyDitherEncoded`.
- Known-ramp self-test in the built player:
  - Render a full-screen analytic linear ramp with amplitude 0 and check that every captured code equals `round(255*LinearToSRGB(L))` within ±1 (this proves the sRGB path is exact).
  - With amplitude 1, check that the 64x64-block means equal the analytic value within 0.05 code, and that the histogram of `code - analytic` is triangular within [-1.5, 1.5].

## 5. Video encoders: what happens and what to run

### 5.1 What encoding does to the dither
- **RGB→YUV, limited range (BT.709 TV range).** Y spans 16-235, so one RGB code is 0.859 Y codes. A ±1 RGB-LSB monochrome dither becomes ±0.86 Y-LSB, slightly below the TPDF optimum. Hence the ladder steps to ×1.25 or ×1.5 if bands appear.
- **Per-channel independent dither** keeps only 0.75 × 0.86 = 0.64 of its RMS in Y. Monochrome dither keeps all of it.
- **4:2:0** averages chroma over 2x2 and quantises it coarsely. Chroma bands (hue steps in the blue sky) can come back regardless of RGB dither. Low-contrast hue gradients are the risk.
- **DCT plus quantisation (CRF).** Blue noise sits at the highest frequencies, which are the first coefficients zeroed in flat blocks at CRF 30. x264 AQ helps flat areas. mb-tree lowers the quality of blocks that later frames do not reference, which is typical for frame-to-frame changing noise.
- On decode, the bands of the encoded Y plane (1 Y code = 1.164 RGB codes after expansion) reappear. Forum practice (doom9) is that ordered or structured dither survives lossy encoding better than random dither, and 10-bit encoding avoids the problem.
- YouTube re-encodes everything (VP9/AV1/H.264, BT.709, limited range). Its upload guidance is H.264 High, 4:2:0, BT.709, and 8 / 12 Mbps (1080p30/60) or 35-45 / 53-68 Mbps (2160p30/60) for SDR. Upload a high-quality master (x264 CRF ≤ 16, 2160p) so the only lossy step left is YouTube's.

### 5.2 Exact commands (ffmpeg 9.0.2 gyan full build is on PATH; libx264, libvpx-vp9 and libzimg are present; all checked in this session with `-f null`)
PNG sequence `cap\<shot>\frame_00000.png ...` at 60 fps. Create output folders first. Commands are one line each (PowerShell or bash).

H.264, CRF 23 and 30, preset medium, yuv420p, BT.709 tagged:
```
ffmpeg -hide_banner -y -framerate 60 -start_number 0 -i "cap/shot/frame_%05d.png" -vf "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p" -c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv -movflags +faststart "enc/shot_x264_crf23.mp4"
ffmpeg -hide_banner -y -framerate 60 -start_number 0 -i "cap/shot/frame_%05d.png" -vf "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p" -c:v libx264 -preset medium -crf 30 -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv -movflags +faststart "enc/shot_x264_crf30.mp4"
```
VP9 (constant quality needs `-b:v 0`). Use Google VOD values: CRF 31 at 1080p, CRF 15 at 2160p. Optional stress run at CRF 40:
```
ffmpeg -hide_banner -y -framerate 60 -start_number 0 -i "cap/shot/frame_%05d.png" -vf "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p" -c:v libvpx-vp9 -crf 31 -b:v 0 -deadline good -cpu-used 2 -row-mt 1 -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv "enc/shot_vp9_crf31.webm"
```
Decode back to full-range RGB PNG (input to BandScore):
```
ffmpeg -hide_banner -y -i "enc/shot_x264_crf23.mp4" -vf "scale=in_color_matrix=bt709:in_range=tv:out_range=pc:flags=accurate_rnd+full_chroma_int,format=rgb24" -start_number 0 "dec/shot_x264_crf23/frame_%05d.png"
```
Y plane only (diagnostic, gray8 PNG, limited-range codes, no colour conversion):
```
ffmpeg -hide_banner -y -i "enc/shot_x264_crf23.mp4" -vf "extractplanes=y" -start_number 0 "decY/shot_x264_crf23/frame_%05d.png"
```
Deterministic RGB→YUV without swscale (zimg, explicit no-dither; use `d=error_diffusion` to see the ceiling). The input transfer and primaries must be declared or zimg fails with "no path between colorspaces":
```
-vf "zscale=rin=full:min=gbr:tin=709:pin=709:r=limited:m=709:t=709:p=709:d=none,format=yuv420p"
```
Frame-count check: `ffprobe -v error -count_frames -select_streams v:0 -show_entries stream=nb_read_frames -of csv=p=0 "enc/shot_x264_crf23.mp4"`.

Pitfall: without `out_color_matrix=bt709`, swscale defaults to BT.601 (`out_color_matrix` default 2 = unspecified), which shifts hues on decode with BT.709. Always tag and convert explicitly.

## 6. Banding metric (Python, numpy and Pillow)
numpy and Pillow are **only installed for Python 3.10** on this machine (`py -3.10 -I script.py`; numpy 2.2.6, Pillow 12.3.0). The default `python` (3.12) has neither.

### 6.1 Literal spec metric (14.10) and its blind spots (sim, 64 x 256 synthetic strips)
- **Step test.** A 9-px box turns a 1-code step into a 9-px ramp of 0.11 code/px. The "> 0.6 code" test only fires on jumps above about 5.4 codes, so it reported 0 steps/1000 for **undithered** day and night skies.
- **Constant-run test.** For a gradient slope s < 0.05 code/px, even a perfect render has every neighbour difference below 0.05.
  - The night sky (6 codes over 700 px, s = 0.0086) dithered gave a run of 1071 px (fail).
  - Undithered day sky at 1080p (s = 0.129) gave a run of 0 (pass).
  - The criterion is only meaningful when |s| ≥ about 0.06 code/px. Apply it only there, and log this as a correction of an unreachable number, with formula `run_ok ⇔ |s| ≥ 0.06` (Part 2 rule 2; thresholds are not relaxed).
- **Recommended additional gate: residual vs a local quadratic trend.** Use a 3-px box along the strip, then a Savitzky-Golay quadratic with 129-px window:

| Gradient | Undithered RMS / p-p | TPDF ±1 RMS / p-p | TPDF ±1.5 RMS / p-p |
|---|---|---|---|
| day 1080p | 0.263 / 0.92 | 0.037 / 0.22 | 0.055 / 0.33 |
| day 4K | 0.273 / 0.95 | 0.037 / 0.26 | 0.051 / 0.40 |
| night 1080p | 0.287 / 0.95 | 0.037 / 0.25 | 0.053 / 0.32 |

  Gate: **RMS ≤ 0.10 code and p-p ≤ 0.60 code per strip and channel**. Real blue noise gives lower values than this white-TPDF simulation.

### 6.2 Code (`tools/band_score.py`; regions come from the 14.10 key-shot table, with the sun disc and objects excluded)
```python
import sys, json
import numpy as np
from PIL import Image

def load_codes(path):                      # H x W x 3 float64 in 8-bit code units
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)

def strips(region, direction="v", width=64):
    """direction 'v': gradient runs along y. Strips are `width` px wide perpendicular to the gradient.
    Returns profiles (L x 3): the mean over the strip width at each position along the gradient."""
    a = region if direction == "v" else np.transpose(region, (1, 0, 2))
    return [a[:, k*width:(k+1)*width, :].mean(axis=1) for k in range(a.shape[1] // width)]

def box(p, k):                              # moving average along the strip, valid part only
    ker = np.ones(k) / k
    return np.stack([np.convolve(p[:, c], ker, mode="valid") for c in range(p.shape[1])], axis=1)

def longest_run(mask):                      # longest run of True, in samples (= neighbour pairs)
    m = np.concatenate(([0], mask.astype(np.int8), [0])); d = np.diff(m)
    s, e = np.flatnonzero(d == 1), np.flatnonzero(d == -1)
    return int((e - s).max()) if s.size else 0

def sg2(m):                                 # Savitzky-Golay quadratic smoothing kernel, window 2m+1 (sums to 1)
    k = np.arange(-m, m + 1, dtype=np.float64)
    return (3*(3*m*m + 3*m - 1) - 15*k*k) / ((2*m - 1)*(2*m + 1)*(2*m + 3))

def strip_metrics(p, min_slope=0.06, half=64):
    L = p.shape[0]; x = np.arange(L)
    s9 = box(p, 9); d = np.abs(np.diff(s9, axis=0))
    steps = (d > 0.6).sum(axis=0) * 1000.0 / d.shape[0]                 # per 1000 px, per channel
    slope = np.array([np.polyfit(x, p[:, c], 1)[0] for c in range(p.shape[1])])
    runs = [longest_run(d[:, c] < 0.05) if abs(slope[c]) >= min_slope else -1 for c in range(p.shape[1])]
    rr = []
    if L > 2*half + 3:
        s3 = box(p, 3); ker = sg2(half)
        for c in range(p.shape[1]):
            r = s3[half:-half, c] - np.convolve(s3[:, c], ker, mode="valid")
            rr.append((float(np.sqrt(np.mean(r*r))), float(r.max() - r.min())))
    return {"steps_per_1000": float(steps.max()), "longest_run_px": int(max(runs)),
            "resid_rms": max((a for a, _ in rr), default=0.0), "resid_pp": max((b for _, b in rr), default=0.0),
            "slope_code_per_px": [float(v) for v in slope]}

def band_score(img, x0, y0, x1, y1, direction="v"):
    ms = [strip_metrics(p) for p in strips(img[y0:y1, x0:x1, :], direction)]
    worst = {k: max(m[k] for m in ms) for k in ("steps_per_1000", "longest_run_px", "resid_rms", "resid_pp")}
    worst["pass"] = (worst["steps_per_1000"] <= 2.0 and worst["longest_run_px"] <= 6
                     and worst["resid_rms"] <= 0.10 and worst["resid_pp"] <= 0.60)
    return worst

def self_test(seed=0):                      # must print fail for amp 0 and pass for amp 1 / 1.5
    rng = np.random.default_rng(seed)
    for H, top, bot in [(403, (243, 228, 208), (191, 217, 227)), (700, (10, 15, 38), (4, 5, 13))]:
        t = np.linspace(0, 1, H)[:, None, None]
        v = np.broadcast_to(np.array(top) * (1 - t) + np.array(bot) * t, (H, 256, 3))
        for amp in (0.0, 1.0, 1.5):
            n = (rng.random((H, 256, 1)) - rng.random((H, 256, 1))) * amp
            img = np.clip(np.floor(v + n + 0.5), 0, 255)
            print(H, amp, band_score(img, 0, 0, 256, H))

if __name__ == "__main__":
    if sys.argv[1] == "--self-test": self_test()
    else:
        x0, y0, x1, y1 = map(int, sys.argv[2:6]); d = sys.argv[6] if len(sys.argv) > 6 else "v"
        print(json.dumps(band_score(load_codes(sys.argv[1]), x0, y0, x1, y1, d)))
```
- For radial gradients (planet limb, Earth curvature), resample the region along the gradient normal first. Use nearest neighbour, not bilinear: bilinear interpolation smooths away the steps being measured.
- At 4K, keep 64-px strips. The constant-run limit stays at 6 px as specified, and the `min_slope` rule applies.
- Run on built-player PNGs and on decoded H.264/VP9 frames (5.2).

## 7. Pitfalls
1. **Double dither.** URP Camera Dithering plus ThreadGrade. URP's dither also changes every frame via `Time.frameCount`, so captures are not reproducible.
2. **R11G11B10 (current project setting).** It quantises bright sky blue in steps of up to 2.6 LSB before any dither runs.
3. **LDR grading mode clamps HDR in Uber** (Common.hlsl:110) before an AfterRenderingPostProcessing pass. HDR grading with a 32 LUT adds up to 1.29 LSB of scallops.
4. **FXAA, FSR1 or TAA-CAS force FinalPost after user passes**, which then filters the dither. Render scale ≠ 1 resamples it.
5. **Dither in linear space, or with `Fast*` sRGB functions** (approximate and saturating), gives the wrong amplitude.
6. **Non-monotonic remap (Shadertoy 4t2SDh form) or `r1 - r2`** applied to blue noise destroys the blue spectrum.
7. **Float precision for R2.** Computing `frac(n·a1)` in HLSL float loses precision. Use C# double and integer texel offsets.
8. **Capture path.** Capture what the player shows (back buffer, after the hardware sRGB encode). If you read a float RT instead, quantise it exactly as the hardware does (`round(255·LinearToSRGB)`), or the measured dither is wrong.
9. **swscale default BT.601**, an untagged colour range, or full-range output confuse the decode and YouTube. Use the explicit commands in 5.2.
10. **Pure black or white lifted by the dither** (0.125 or 0.223 codes mean). Fixed by the 0.5-LSB edge fade.
11. **Frame-difference budgets.** With temporal dither, keep amplitude ≤ 1.7 LSB where the budget is 0.8, and ≤ 2.5 LSB where it is 1.2 (D > 0.5). A jump in amplitude at D = 0.5 would itself pop, so smoothstep it over 0.45-0.55.
12. **Python environment.** numpy and Pillow are only available in `py -3.10`. Always use `-I`.

## 8. Sources (accessed 2026-10-29)
| URL | Used for |
|---|---|
| Local URP source `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184` (file:line in section 1) | ApplyDithering, Uber/FinalPost order, dither textures, HDR precision, grading clamp/LUT |
| Local Core source `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e` (Color.hlsl, Common.hlsl, Random.hlsl, API/D3D11.hlsl, Runtime/Utilities/Blit.hlsl) | exact sRGB functions, FastSign, IGN, LOAD_TEXTURE2D, Blit Varyings |
| `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml` | Texture2D ctor/SetPixelData, RFloat, Display.requiresSrgbBlitToBackbuffer |
| http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare | Jimenez 2014 talk, SIGGRAPH Advances, IGN used for shadow filtering |
| https://blog.demofox.org/2022/01/01/interleaved-gradient-noise-a-different-kind-of-low-discrepancy-sequence/ | IGN formula, 3x3 low-discrepancy property, 5.588238 px/frame offset |
| https://blog.playdead.com/articles/inside_presentations/inside_publications.html | Playdead talk list (GDC 2016 rendering talk, "Banding in Games" 2014) |
| https://github.com/playdeadgames/publications/tree/master/INSIDE | slide file names (PDFs are Git LFS, over the fetch limit) |
| https://gdcvault.com/play/1023002/Low-Complexity-High-Fidelity-INSIDE | talk abstract (dithering against banding), speakers |
| https://lists.blender.org/pipermail/bf-blender-cvs/2020-February/135857.html (via search snippet) | Blender commit citing Gjøl "Banding in Games" slide 27; Shadertoy 4t2SDh triangle remap |
| https://bartwronski.com/2016/10/30/dithering-part-three-real-world-2d-quantization-dithering/ | triangular vs uniform, blue noise vs white vs IGN, -1..1 remap note, clipping comment |
| https://blog.demofox.org/2019/06/25/generating-blue-noise-textures-with-void-and-cluster/ | void-and-cluster steps, energy LUT optimisation, sigma 1.5 vs 1.9, 3-sigma truncation warning |
| https://patents.google.com/patent/US5912745 (and the search result citations) | Ulichney 1993 citation, SPIE 1913 pp. 332-343; Gaussian sigma 1.0-1.5 |
| https://momentsingraphics.de/BlueNoise.html | Peters' blue-noise textures (sigma 1.9; random texture and offset per frame) |
| https://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/ (TLS failed; constants via https://scrapbox.io/0b5vr/R2) | R2 definition, g, a1, a2, 0.5 seed |
| https://blog.demofox.org/2017/11/03/animating-noise-for-integration-over-time-2-uniform-over-time/ | value-offset animation degrades the blue-noise spectrum |
| https://support.google.com/youtube/answer/1722171 | YouTube upload specs (H.264 High, 4:2:0, BT.709, bitrates, 8-bit conversion) |
| https://developers.google.com/media/vp9/settings/vod | VP9 CRF per resolution (31 @1080p, 15 @2160p), `-quality good` |
| https://forum.doom9.org/archive/index.php/t-167706.html | x264 banding: ordered dither survives better, 10-bit, aq/psy tweaks |
| Local `ffmpeg -h encoder=libx264 / libvpx-vp9`, `-h filter=scale / zscale` | option names, ranges, defaults (crf, deadline, cpu-used, row-mt, out_color_matrix) |
