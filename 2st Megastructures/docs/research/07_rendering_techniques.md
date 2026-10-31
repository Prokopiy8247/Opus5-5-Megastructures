# 07 Rendering techniques: analytic sky, filtered procedurals, hashes, dither, clipmaps, fog, star, water

Scope: Unity 6000.6.0f1, URP 17.6.0, everything procedural and handwritten HLSL. All code below is original
skeleton code written for this project from the formulas. No third-party code is pasted.
Tags: VERIFIED-SOURCE (path:line in Library/PackageCache), VERIFIED-DOCS (URL), UNVERIFIED (memory or reasoning),
DERIVED (my own math from first principles, checked by hand).

Path aliases used below:
- `URP` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`
- `CORE` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`

---------------------------------------------------------------------------------------------------

## 0. Surprises and pitfalls for this exact version (read first)

1. **URP fog does nothing in a custom shader unless the shader declares the fog keywords.** `MixFog` / `MixFogColor`
   only act inside `#if defined(FOG_*_KEYWORD_DECLARED)`. Declare them with
   `#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"`, as Lit does.
   That file uses `#pragma multi_compile_fog`, or `#pragma dynamic_branch _ FOG_LINEAR FOG_EXP FOG_EXP2` when
   `USE_DYNAMIC_BRANCH_FOG_KEYWORD` is set. It is `(0)` in this project.
   VERIFIED-SOURCE `URP/ShaderLibrary/Fog.hlsl:1-9`, `URP/ShaderLibrary/ShaderVariablesFunctions.hlsl:463-492`,
   `URP/Shaders/Lit.shader:179`,
   `Library/PackageCache/com.unity.render-pipelines.universal-config@561422b74439/Runtime/ShaderConfig.cs.hlsl:13`.
   **Recommendation:** do not use Unity fog at all. Its colour is one constant (`unity_FogColor`,
   `URP/ShaderLibrary/UnityInput.hlsl:225-226`), so it cannot match a gradient sky. Write one custom
   `ApplyAtmosphere()` (section 6) and include it in every shader.
2. **URP `FOG_EXP` mode is not exactly `exp(-density*z)`.** It computes `exp2(-unity_FogParams.x * z)`, and Unity
   documents `unity_FogParams.x = density/sqrt(ln 2)`. The result is `exp(-0.8326*density*z)`. EXP2 is exact:
   `exp2(-(x z)^2) = exp(-(density z)^2)`. VERIFIED-SOURCE `ShaderVariablesFunctions.hlsl:337-343,366-384`.
   VERIFIED-DOCS https://docs.unity3d.com/6000.2/Documentation/Manual/SL-UnityShaderVariables.html, which lists
   `(density/sqrt(ln2), density/ln2, -1/(end-start), end/(end-start))`.
3. **URP camera dithering is temporal.** Every frame it cycles `blueNoise16LTex[index]` and applies a random offset
   seeded by `Time.frameCount`. That is bad for YouTube compression (section 4). If you want static dither, turn
   `UniversalAdditionalCameraData.dithering` off (`URP/Runtime/UniversalAdditionalCameraData.cs:919`) and dither in
   your own final pass. VERIFIED-SOURCE `URP/Runtime/PostProcessUtils.cs:142-163,202-214`,
   `URP/Runtime/PostProcess.cs:112-127`. The noise shape URP uses is TPDF, ±1 LSB, applied in the sRGB-encoded domain:
   `URP/Shaders/PostProcessing/Common.hlsl:188-206`.
4. **URP Bloom has no Karis average.** Its HQ prefilter is the 13-tap pattern. The only anti-firefly control is
   `color = min(ClampMax, color)`, which is the `Bloom.clamp` volume parameter, default 65472. Lower it, for example
   to 20-100, and clamp the sun's radiance in its own shader. VERIFIED-SOURCE `URP/Shaders/PostProcessing/Bloom.shader:16-21,79-106`,
   `URP/Runtime/Overrides/Bloom.cs:195,208`.
5. **The default HDR buffer is R11G11B10** (`HDRColorBufferPrecision._32Bits`). Its mantissa is about 6 bits for R and G
   and 5 bits for B, so smooth dark sky gradients can band before tonemapping. Use `_64Bits` (RGBA16F) on the URP asset
   for a gradient-heavy sky. VERIFIED-SOURCE `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:166-174,452,1096`.
   The bit counts are UNVERIFIED (format knowledge).
6. **Useful ready-made Core functions** (include `CORE/ShaderLibrary/Common.hlsl` first):
   - `F_Schlick(f0, u)`: `CORE/ShaderLibrary/BSDF.hlsl:32-56`
   - `HenyeyGreensteinPhaseFunction(g, cosTheta)`, `CornetteShanksPhaseFunction(g, cosTheta)`, `RayleighPhaseFunction(cosTheta)`,
     `TransmittanceHomogeneousMedium(ext, len)`: `CORE/ShaderLibrary/VolumeRendering.hlsl:110,200-231,271-305`
   - `InterleavedGradientNoise(pixCoord, frameCount)`: `CORE/ShaderLibrary/Random.hlsl:93-99`
   - `NormalFiltering` and `GeometricNormalVariance` (specular AA): `CORE/ShaderLibrary/CommonMaterial.hlsl:183-227`
   - `SharpenAlpha` (`saturate((a-clip)/max(fwidth(a),1e-4)+0.5)`): `CORE/ShaderLibrary/Common.hlsl:1692-1695`
   - `ClampToFloat16Max`: `CORE/ShaderLibrary/Common.hlsl:1698`
   - Depth: `SampleSceneDepth(uv)` (`URP/ShaderLibrary/DeclareDepthTexture.hlsl:31-40`),
     `LinearEyeDepth(raw, _ZBufferParams)` (`CORE/ShaderLibrary/Common.hlsl:1206`),
     `ComputeWorldSpacePosition(ndc, deviceDepth, invVP)` (`CORE/ShaderLibrary/Common.hlsl:1388`)
   - Soft fade: `SoftParticles`, `CameraFade` (`URP/ShaderLibrary/Particles.hlsl:84-101,123-127`)
7. **Phase-function sign convention in Core:** the denominator is `1 + g^2 - 2 g cosTheta`. Pass
   `cosTheta = dot(viewRayDir, dirToSun)`; with g > 0 the lobe peaks when you look at the sun.
   VERIFIED-SOURCE `VolumeRendering.hlsl:218-225,285-291`.
8. **Ringworld fog:** a distance-based exp/exp² fog saturates completely on the far arch, which is millions of km
   away. Use optical depth through a thin atmosphere shell instead (section 6.3). DERIVED.

---------------------------------------------------------------------------------------------------

## 1. Analytic sky models: ideas for an art-directed sky

### 1.1 Preetham 1999 (Perez form)
- Perez luminance distribution: `F(theta, gamma) = (1 + A*exp(B/cos(theta))) * (1 + C*exp(D*gamma) + E*cos^2(gamma))`.
  `theta` is the view zenith angle and `gamma` the angle between the view and the sun.
  The sky value is `Y = Y_zenith * F(theta,gamma) / F(0, theta_sun)`, evaluated per xyY channel. The A..E coefficients
  are linear in turbidity T. VERIFIED-DOCS (structure): Zotti 2007 WSCG and Hosek-Wilkie preprint (search summaries).
  The exact coefficient tables are UNVERIFIED and not needed.
- Ideas worth reusing:
  - `1 + A e^{B/cos theta}` with A<0, B<0: darker zenith, brighter horizon.
  - `C e^{D gamma}`: circumsolar glow.
  - `E cos^2 gamma`: broad Rayleigh-like brightening opposite the sun.
- Known weakness: poor at sunset and high turbidity, and the zenith formula can go negative at the edges of its range.
  VERIFIED-DOCS (Zotti 2007, via search).

### 1.2 Hosek-Wilkie 2012
- Extended Perez form, used per channel:
  `F = (1 + A*exp(B/(cos(theta)+0.01))) * (C + D*exp(E*gamma) + F*cos^2(gamma) + G*chi(H,gamma) + I*sqrt(cos(theta)))`,
  with `chi(g, gamma) = (1 + cos^2 gamma) / (1 + g^2 - 2 g cos gamma)^{3/2}`, and `L = F * L_M`, where L_M is the
  expected zenith radiance. VERIFIED-DOCS: Blender `intern/sky/source/sky_model.cpp` v2.93. Note the code's coefficient
  order differs: `configuration[7]` is the sqrt(cos) weight and `[8]` is the Mie g.
- The `+0.01` keeps the horizon finite. The `chi` term is a Mie-like forward lobe with built-in Rayleigh symmetry.
  The `sqrt(cos theta)` term adds zenith brightening.
- **License:** 3-clause BSD, "Copyright (c) 2012 - 2013, Lukas Hosek and Alexander Wilkie", version 1.4a (Feb 22 2013).
  VERIFIED-DOCS (Blender copy, Debian/OSPRay copyright file, CRAN skymodelr COPYRIGHTS).
  The project page does not state the license itself; it links `HosekWilkie_SkylightModel_C_Source.1.4a.zip`.
  **Decision for The Ring:** do not port the code or its large coefficient data tables. That would be imported data
  (Part 6) and it is not art-directable. **Do** use the functional form above with **hand-picked RGB coefficients**.
  It is the best-shaped art-directable sky formula: 9 sliders per channel, smooth, horizon-safe.

### 1.3 Hillaire 2020 (EGSR, CGF 39(4) pp. 13-22, DOI 10.1111/cgf.14050)
- LUTs: Transmittance, Multiple-scattering, Sky-View and Aerial-Perspective (froxel volume). VERIFIED-DOCS (diglib TOC).
- Sky-view LUT is 192x108. Its mapping puts resolution near the horizon: for rays above the horizon
  `v = 0.5*(1 - sqrt(1 - theta/theta_horizon))`, for rays below `v = 0.5 + 0.5*sqrt((theta - theta_horizon)/beta)`.
  The azimuth axis uses `u = sqrt(0.5 - 0.5*cos(phi_light))`, and there is a half-texel remap `(u + 0.5/W) * W/(W+1)`.
  VERIFIED-DOCS `sebh/UnrealEngineSkyAtmosphere/Resources/RenderSkyCommon.hlsl`. That reference code is MIT.
- Multiple scattering: `Psi_ms = L_2nd * (1 + f_ms + f_ms^2 + ...) = L_2nd / (1 - f_ms)`, per RGB channel, valid for
  f_ms < 1. f_ms and L_2nd are sphere integrals with an isotropic phase, 64 samples.
  VERIFIED-DOCS `RenderSkyRayMarching.hlsl`.
- Energy-conserving step integration: `L += throughput * (S - S*exp(-sigma_t*dt)) / sigma_t; throughput *= exp(-sigma_t*dt)`.
  VERIFIED-DOCS (same file).
- Aerial perspective: 32 slices at 4 km per slice. VERIFIED-DOCS (same file).
- Phase functions: Rayleigh `3/(16 pi) (1+mu^2)`; Mie Cornette-Shanks `3/(8 pi) (1-g^2)/(2+g^2) (1+mu^2)/(1+g^2-2 g mu)^{1.5}`.
  The same functions exist in Unity Core (section 0.6).
- Earth defaults, UNVERIFIED (Bruneton-style values from memory):
  - Rayleigh sigma_s = (5.802, 13.558, 33.1)e-6 /m, H_R = 8 km
  - Mie sigma_s = 3.996e-6 /m, sigma_a = 4.40e-6 /m, H_M = 1.2 km, g = 0.8
  - Ozone sigma_a = (0.650, 1.881, 0.085)e-6 /m (tent layer 10-40 km)
  - Ground radius 6360 km, top 6460 km
- Ideas to steal for an art sky:
  - (a) The 1/(1-f) series is why the horizon gets brighter and less saturated. Imitate it by lerping the horizon
    colour toward a brighter, desaturated "haze" colour.
  - (b) Bake the art sky into a small 192x108 LUT with the horizon-dense v mapping (an RT updated only when the
    lighting state changes). Sample that one LUT for the skybox, fog in-scatter and water reflections. They then
    match exactly and stay cheap.

### 1.4 Recommended art-directed sky (original skeleton)
Inputs: `d` is the unit view ray; `up` is local up (toward the ring axis); `L` is the unit direction to the star.
```hlsl
// e = sin(elevation), mu = cos(scattering angle)
float3 SkyRadiance(float3 d, float3 up, float3 L, bool withDisc)
{
    float e  = dot(d, up);
    float mu = dot(d, L);
    // Hosek-Wilkie-shaped gradient (hand-tuned per channel; float3 coefficients A..I)
    float ct = max(e, 0.0);                                   // cos(view zenith)
    float g  = acos(clamp(mu, -1, 1));                         // gamma
    float3 term1 = 1.0 + _SkyA * exp(_SkyB / (ct + 0.01));
    float3 chi   = (1.0 + mu*mu) / pow(1.0 + _SkyH*_SkyH - 2.0*_SkyH*mu, 1.5);
    float3 term2 = _SkyC + _SkyD * exp(_SkyE * g) + _SkyF * mu*mu + _SkyG * chi + _SkyI * sqrt(ct);
    float3 c = term1 * term2 * _SkyZenithRadiance;
    c = lerp(c, _GroundHaze, saturate(-e * 6.0));              // below the local horizon
    c += _HorizonGlow * exp(-abs(e) / _HorizonGlowWidth);      // thin bright horizon band
    c *= _SunTint;                                             // sunset/eclipse tint (see below)
    if (withDisc) c += StarDisc(d, L);                         // section 7; never inside fog
    return c;
}
```
- Sunset colour: use transmittance `T_sun = exp(-beta_rgb * airmass)`, with `beta_rgb ∝ lambda^-4`. At (680, 550, 440) nm the
  ratios are (0.428, 1, 2.44) times beta_G. DERIVED (`(550/680)^4`, `(550/440)^4`).
  Kasten-Young air mass: `X = 1/(cos z + 0.50572*(96.07995 - z_deg)^-1.6364)`, about 38 at the horizon; plane-parallel
  is `sec z`. VERIFIED-DOCS https://en.wikipedia.org/wiki/Air_mass_(astronomy).
- **Ringworld note:** the star sits at the ring centre, which is always the local zenith, so physically there is no
  sunset path-length reddening. The evening comes from shadow squares. Motivate the warm "evening" tint by limb
  darkening instead (section 7): limb darkening is much stronger in blue, so the uncovered sliver of a partly
  eclipsed star is redder. Drive `_SunTint` from the visible-disc fraction. UNVERIFIED (art reasoning on top of
  VERIFIED-DOCS wavelength data).
- **Keep fog equal to the sky:** evaluate the same `SkyRadiance(d, up, L, false)` (or sample the same LUT) as the fog
  in-scatter colour for every pixel along that pixel's own view ray. Then `final = surf*T + Sky(d)*(1-T)`, and as
  T → 0 the geometry dissolves into exactly the sky behind it, including on the curved-up land. Never add the star disc
  to the in-scatter, or distant terrain will glow. DERIVED.

---------------------------------------------------------------------------------------------------

## 2. Filtering procedural patterns

General rule: compute derivatives of the **pattern domain coordinate** `p` before any `frac`/`floor`. Use
`ddx(p)`/`ddy(p)`, or `ddx_fine`/`ddy_fine` (SM5, `#pragma target 4.5+`). The default `ddx`/`ddy` may be coarse,
one value per 2x2 quad. UNVERIFIED (HLSL spec knowledge).

### 2.1 Box-filtered checker (Quilez "filterableprocedurals"/"checkerfiltering", after Apodaca & Gritz)
- Square wave of period 2: `s(x) = +1` on [0,1) and `-1` on [1,2). Its integral is the triangle wave
  `S(x) = 1 - 2*|frac(x/2) - 0.5|`. DERIVED, checked on both half-periods.
- Box filter of width w: `s_bar(x,w) = (S(x + w/2) - S(x - w/2)) / w`.
- 2D 0/1 checker: `c = 0.5 - 0.5 * s_bar(p.x, w.x) * s_bar(p.y, w.y)`. The product is exact because both the
  pattern and the box kernel are separable.
- `w = abs(ddx(p)) + abs(ddy(p)) + 1e-4`, per component.
  VERIFIED-DOCS (existence and method): https://iquilezles.org/articles/filterableprocedurals and
  https://iquilezles.org/articles/checkerfiltering/ (search snippets; the site returned 404 to the fetcher). Formula DERIVED.
- Quilez's follow-up "morecheckerfiltering": a triangle (tent) kernel flickers less in motion than the box filter,
  because a box lets a feature affect a pixel the instant it enters the footprint. VERIFIED-DOCS (search snippet).
  For a tent kernel, integrate twice: `(S2(x+w) - 2 S2(x) + S2(x-w)) / w^2`, where S2 is the integral of S. DERIVED.

### 2.2 Box-filtered grid lines
- Line fraction `f = 1/N` of the cell (line width relative to spacing). Indicator `g(x) = 1` if `frac(x) < f`.
- Integral: `G(x) = floor(x)*f + min(frac(x), f)`. Filtered: `g_bar = (G(x+w/2) - G(x-w/2)) / w`. DERIVED.
- Line coverage: `lines = 1 - (1 - g_bar(p.x))*(1 - g_bar(p.y))`. Use `w = max(abs(ddx(p)), abs(ddy(p))) + 1e-4`.
  Mean (fully filtered) value: `1 - (1-f)^2`.
- **Fade lines when spacing falls below N pixels** (box filtering still leaves moire near Nyquist):
  ```hlsl
  float fw  = max(fwidth(pWorld.x), fwidth(pWorld.y));   // world units per pixel
  float px  = cellSize / fw;                              // pixels per cell
  float k   = saturate((px - _NMin) / (_NMax - _NMin));   // e.g. NMin=3, NMax=6
  float val = lerp(meanValue, filteredLines, k);          // fade to the MEAN, not to background
  ```
  Fading to the mean keeps average brightness constant, so there is no pop or brightness shift. Fade every scale of a
  multi-scale panel/seam pattern independently. DERIVED.

### 2.3 Band-limited synthesis (Quilez "bandlimiting")
- Exact box-filtered cosine: average of cos over `[x - w/2, x + w/2]` is `cos(x) * sin(w/2)/(w/2)`, with
  `w = fwidth(x)` the phase change per pixel. DERIVED. The sinc goes negative for w > 2π, so clamp it to ≥ 0.
- Cheap version used by Quilez (`fcos`): `cos(x) * smoothstep(2π, 0, fwidth(x))`. VERIFIED-DOCS: LYGIA `math/fcos.hlsl`
  credits Quilez. Do not copy LYGIA code: its license is Prosperity/Patron, not MIT (UNVERIFIED).
- Octave fade for fbm and sums of waves: octave i with frequency `f_i` (cycles per unit) has footprint
  `u_i = f_i * fw` (cycles per pixel). Weight it with `a_i *= 1 - smoothstep(0.25, 0.5, u_i)` (Nyquist is 0.5 cycles
  per pixel). Skip the octave entirely when the weight is 0, which is cheaper too. For zero-mean noise, fading toward 0
  keeps the mean. For [0,1] noise, fade toward 0.5. DERIVED.

### 2.4 Anti-aliased SDF edges
- Coverage of the inside (d < 0): `cov = saturate(0.5 - d / fw)`.
  - `fw = length(float2(ddx(d), ddy(d)))` is exact for locally linear d.
  - `fwidth(d) = |ddx|+|ddy|` is cheaper but up to √2 too wide on diagonals.
  DERIVED. Core uses the same form for alpha: `SharpenAlpha`, `CORE/ShaderLibrary/Common.hlsl:1692-1695`.
- Sub-pixel thin lines (half-width h, signed position x across the line): use the true box overlap
  `cov = saturate((min(x + fw/2, h) - max(x - fw/2, -h)) / fw)`. Brightness then scales with width, so very thin
  lines get dimmer instead of fattening to 1 px and shimmering. DERIVED.
- If the SDF is analytic in world units (|∇d| = 1), `fw` can be the world size of a pixel at that depth:
  `fw ≈ depth * 2*tan(vfov/2) / screenHeight`. That is stable even where `d` has a derivative cusp. DERIVED.

---------------------------------------------------------------------------------------------------

## 3. Hashes without sin()

Paper: Jarzynski & Olano, "Hash Functions for GPU Rendering", JCGT 9(3), 2020, pp. 21-38.
VERIFIED-DOCS (paper text extracted from https://www.jcgt.org/published/0009/03/02/paper.pdf):
- Recommended spanning set, fastest to highest quality:
  - 1→1: `iqint1, pcg, xxhash32, pcg3d, pcg4d`
  - 2→1: `iqint3, xxhash32 (multibyte2), pcg3d, pcg4d`
  - 3→1: `pcg3d16, xxhash32 (multibyte3), pcg3d, pcg4d`
  - 4→1: `xxhash32 (multibyte4), pcg4d`
  - N→N: `pcg3d, pcg4d`
- Warning from the paper: the linear-combination trick `hash(aX + bY + cZ + d)` "greatly diminishes" quality and makes
  repeating patterns. Use a native multi-dimensional hash instead.
- **pcg3d** (paper §6.1 listing, described in my own words), on a uint3 `v`:
  1. `v = v * 1664525u + 1013904223u`
  2. `v.x += v.y*v.z; v.y += v.z*v.x; v.z += v.x*v.y;`
  3. `v ^= v >> 16u;`
  4. Repeat step 2.

  **pcg4d** is the same with `x += y*w; y += z*x; z += x*y; w += y*z`.
- **pcg (1D, RXS-M-XS 32-bit):** `state = v*747796405u + 2891336453u;`
  `word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u; return (word >> 22u) ^ word;`
  VERIFIED-DOCS https://reedbeta.com/blog/hash-functions-for-gpu-rendering/. Reed calls PCG "probably your default GPU
  hash", and found Wang hash far off the Pareto frontier.
- Unity Core already has `JenkinsHash(uint..uint4)`, a `Hash(uint)` and `XorShift`
  (VERIFIED-SOURCE `CORE/ShaderLibrary/Random.hlsl:4-40,102-108`). Prefer pcg3d for quality.

Skeleton (original):
```hlsl
uint3 Pcg3d(uint3 v) { v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;  v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;  return v; }
float  U01(uint h) { return (h >> 8) * (1.0 / 16777216.0); }   // exact 24-bit -> [0,1)
float3 Hash33(int3 c, uint seed) { uint3 h = Pcg3d(asuint(c) ^ uint3(0u, 0u, seed)); return float3(U01(h.x), U01(h.y), U01(h.z)); }
```
- Core's `ConstructFloat` maps the low 23 bits into [1,2) and subtracts 1 (`Random.hlsl:44-58`). That is fine for
  well-mixed hashes. The `>> 8` version above uses the high bits, which is safer for weaker LCG-like hashes. DERIVED.

Value noise and fbm from integer hashes (original):
```hlsl
float ValueNoise3(float3 p, uint seed)            // returns [0,1)
{
    int3   i = (int3)floor(p);  float3 f = p - floor(p);
    float3 u = f*f*f*(f*(f*6.0 - 15.0) + 10.0);   // quintic fade (C2); du = 30 f^2 (f-1)^2
    // 8 corners: h(i + c), c in {0,1}^3, trilinear lerp with u
    ...
}
float Fbm(float3 p, float fw, uint seed)           // fw = world size of one pixel
{
    float sum = 0, amp = 0.5, freq = 1;
    [loop] for (int o = 0; o < 6; o++) {
        float wgt = 1.0 - smoothstep(0.25, 0.5, freq * fw);   // octave fade by footprint (2.3)
        if (wgt <= 0) break;
        sum += amp * wgt * (ValueNoise3(p * freq, seed + o) - 0.5);
        freq *= 2.03; amp *= 0.5;                  // non-2.0 lacunarity + a rotation per octave hides lattice
        p = float3(0.8*p.x - 0.6*p.z, p.y, 0.6*p.x + 0.8*p.z); // 3-4-5 rotation (36.87 deg)
    }
    return sum;                                     // zero-mean
}
```
- **Precision pitfall:** `floor()` of a float loses integer resolution above 2^24 = 16.7 M units, and `int` overflows at
  2^31. On a ringworld, feed hashes **tile-local** coordinates plus an integer tile index from C#
  (`seed ^= tileIndex`). Do not pass absolute world coordinates. DERIVED.
- Gradient noise looks less blocky than value noise at the same cost and gives analytic derivatives for normals.
  UNVERIFIED (common knowledge).

---------------------------------------------------------------------------------------------------

## 4. Dither

- **Interleaved gradient noise (Jimenez 2014):** `IGN(x,y) = frac(52.9829189 * frac(0.06711056*x + 0.00583715*y))`, with
  x, y in pixels. Unity Core's version adds a per-frame offset: `pixCoord += frameCount * float2(2.083, 4.867)`.
  **Pass `frameCount = 0` for a static pattern.** VERIFIED-SOURCE `CORE/ShaderLibrary/Random.hlsl:91-99`.
  VERIFIED-DOCS: Wronski, "Dithering part three"; iryoku.com talk page (SIGGRAPH 2014 Advances course).
  Use `SV_Position.xy` (pixel centres at .5) as `pixCoord`.
- **TPDF** (triangular PDF on [-1, 1] LSB):
  - From two uniforms `r1, r2` in [0,1): `n = r1 + r2 - 1`. Variance = 2 × 1/12 = 1/6 LSB². DERIVED.
  - From one uniform `v` in [-1,1] (what URP does): `n = sign(v) * (1 - sqrt(1 - |v|))`.
    VERIFIED-SOURCE `URP/Shaders/PostProcessing/Common.hlsl:190-192`.
  - Amplitude: TPDF dither should span two quantization steps (-1..+1 LSB). It does not introduce noise modulation.
    VERIFIED-DOCS https://en.wikipedia.org/wiki/Dither. Uniform noise leaves bands near the step centres, and doubling
    it is too noisy; Gjøl's "Banding in Games" (Playdead) is the reference. VERIFIED-DOCS (Wronski post).
  - Apply it **in the display-encoded domain, right before 8-bit quantization**: `srgb = LinearToSRGB(c) + n/255`,
    as URP does: `GetSRGBToLinear(GetLinearToSRGB(input) + noise/255.0)` (`Common.hlsl:202`).
  - Use **one noise value for R, G and B** (monochrome; URP samples one `.a` channel). It lands in luma, which
    survives 4:2:0 chroma subsampling. Per-channel chroma noise is averaged away. UNVERIFIED (reasoning).
  - Clipping near black and white biases the mean, by about 20% of a step according to a reader comment on Wronski's post.
    Fade the dither amplitude to 0 as the encoded value nears 0 or 1, for example `amp = min(1, 4*min(s, 1-s)*255)`.
    VERIFIED-DOCS (comment) / DERIVED (fix).
- **Why static (screen-locked) dither survives video compression better:**
  - Inter-frame prediction copies a pattern that is identical between frames at near-zero residual cost.
  - Per-frame random noise is new information every frame, so the encoder spends bits on it or, at YouTube bitrates,
    quantizes it away. The bands come back and the residual blocks shimmer.
  - A forum post (VideoHelp) states that a fixed pattern needs less bitrate than random per-frame grain.
  VERIFIED-DOCS (forum claim, weak), UNVERIFIED (codec reasoning).
- Practical: static IGN or a pcg-hashed static TPDF at ±1 LSB in the final pass. Consider ±1.5 LSB for upload,
  because the RGB→YUV limited-range (16-235) conversion re-quantizes. Better still, record captures at 10-bit or from
  an RGBA16F target. UNVERIFIED.

---------------------------------------------------------------------------------------------------

## 5. Geometry clipmaps (Losasso & Hoppe 2004; GPU Gems 2 ch. 2, Asirvatham & Hoppe)

VERIFIED-DOCS https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry
and https://www.hhoppe.com/geomclipmap.pdf (text extracted):
- Grid per level: `n x n` vertices with n odd, `n = 2^k - 1` (GPU Gems uses 255), so the outer perimeter lies on the
  coarser grid. Block size `m = (n+1)/4`. The ring is 12 `m x m` blocks, plus four `m x 3` fix-up strips filling the
  2-quad gap mid-side (`(n-1) - 4(m-1) = 2`). One quad-wide **L-shaped interior trim** fills the gap because the finer
  level sits off-centre by one unit. The finest level fills its centre with 4 more blocks and one L.
  16-bit indices allow m ≤ 256.
- **"128 grid" version:** n = 127 vertices (126 quads), m = 32 vertices (31 quads): 4×31 + 2 = 126. DERIVED.
- Level l spacing is `s_l = s_0 * 2^l`. Each level is centred on the viewer and snapped so its vertices lie on the
  coarser grid. With origins snapped to multiples of `2*s_l` (the parent spacing), the child's footprint sits off-centre
  by 0 or ±1 parent quad. The L-trim's orientation is chosen from that offset. DERIVED.
- Simpler layout (Savage, mikejsavage.co.uk), VERIFIED-DOCS:
  - Each level is a 4×4 set of tiles with the middle 2×2 omitted for l > 0, plus 1-unit fillers, an L-trim and a seam.
  - `snapped = floor(cam/scale)*scale`, `scale = 2^l`.
  - Trim rotation index: `d = cam - nextSnapped; r = (d.x >= scale ? 0 : 2) | (d.y >= scale ? 0 : 1)`. Use hard-coded
    rotation matrices, since a generic rotation can crack.
  - Seam mesh: triangles whose coarse vertices match fine vertices exactly, which kills T-junctions. Side length `4T + 2`.
- **Geomorph (transition region)** (Losasso & Hoppe):
  - Transition width `w = n/10` grid units, with `w = min(n/10, ...)` if the finer level is too close.
  - α is 0 except in the transition band, where it ramps linearly to 1 at the outer perimeter.
  - Blend `z' = (1-α) z_fine + α z_coarse`, with `α = max(α_x, α_y)`.
  - GPU Gems shader form: `alpha = saturate((abs(worldPos - viewerPos) - AlphaOffset) * OneOverWidth)`.
  VERIFIED-DOCS.
  The exact per-axis expression, consistent with that description, is UNVERIFIED (reconstructed):
  `α_x = saturate((|x - v_x| - (halfExtent - w - 1)) / w)`, with halfExtent = (x_max - x_min)/2 in grid units of
  that level.
- **T-junctions:** render zero-area (degenerate) triangles along each level's outer boundary. VERIFIED-DOCS (both sources).
- **Procedural-height variant (no elevation texture):** morph the **XZ position** instead of the height, as in CDLOD.
  Slide each odd vertex toward its even neighbour by α (`xz' = xz - frac(gridIdx*0.5)*2*s_l*α`), then evaluate the
  height function at `xz'`. At α = 1 the fine mesh equals the coarse mesh exactly, with no seams. UNVERIFIED
  (Strugar CDLOD 2009, from memory). Evaluate the height in tile-local coordinates (section 3 pitfall).
- **Ringworld bend in the vertex shader:** the floor rises along the spin axis x:
  `y += R - sqrt(R^2 - x^2)`. Compute it as `x*x / (R + sqrt(R*R - x*x))` to avoid catastrophic cancellation in
  float when x << R. DERIVED.

---------------------------------------------------------------------------------------------------

## 6. Atmospheric perspective, exp² fog, height fog, light shafts

### 6.1 Distance fog
- Exp: `T = exp(-sigma*d)`. Exp²: `T = exp(-(sigma*d)^2)`. Linear: `T = saturate((end - d)/(end - start))`.
  URP equivalents are in `ShaderVariablesFunctions.hlsl:326-394` (see pitfall 0.2).
  The blend is `lerp(fogColor, color, T)` (`:518`).
- URP uses view depth minus the near plane (`max(viewZ - _ProjectionParams.y, 0)`) rather than the radial distance
  (`:422-425`). For a sky-matched fog use **radial** distance `length(posWS - camPosWS)`; otherwise the fog visibly
  rotates with the camera at the screen edges. DERIVED.

### 6.2 Exponential height fog (closed form), density `rho(h) = a*exp(-b*h)`
DERIVED; it matches the structure in Quilez's fog article (https://iquilezles.org/articles/fog/, via LYGIA snippet).
Camera at height h0, unit ray with vertical component `dy`, length t:
`tau(t) = a*exp(-b*h0) * t * E(b*t*dy)`, where `E(x) = (1 - exp(-x))/x`. E has no division-by-zero problem:
`E(0) = 1`; use `E(x) ≈ 1 - x/2 + x*x/6` when `|x| < 1e-3`. The naive `/rd.y` form blows up for level rays
(MonoGame thread). Then `T = exp(-tau)`.
- For `dy > 0`, `tau(∞) = a*exp(-b*h0)/(b*dy)`, which is finite. Rays looking up see a finite atmosphere column.
- Sun-aligned tint (Quilez): blend the fog colour toward a warm colour by `pow(saturate(dot(d, L)), 8)`, or better,
  use the sky function itself (1.4).

### 6.3 Ringworld optical depth (avoid fogging out the arch completely)
DERIVED, using the tangent-chord theorem:
- The floor at arc distance s along the spin direction appears at elevation `eps = s/(2R)` above the local tangent
  plane. It is at straight-line distance `2R*sin(s/(2R))` and height `R(1 - cos(s/R)) ≈ s²/(2R)`.
  The far side (s = πR) is at the zenith, 2R away.
- The chord meets the floor at the same angle eps at both ends. With a thin atmosphere, the column optical depth is
  `tau_col(h) = a*exp(-b*h)/b`. A ray to an arch point therefore crosses about one slanted column at each end:
  `tau_arch ≈ tau_col(h0)/sin(eps) + tau_col(h_target)/sin(eps)`.
- Use `tau = min(tau_heightfog(dist), tau_arch)`, with `sin(eps)` clamped to ≥ about 0.01. Near ground uses the
  height-fog integral; the far arch gets a constant, pleasant haze instead of an opaque wall.

### 6.4 Combined skeleton
```hlsl
float3 ApplyAtmosphere(float3 surf, float3 posWS)
{
    float3 v = posWS - _WorldSpaceCameraPos; float dist = length(v); float3 d = v / dist;
    float dy = dot(d, _LocalUp);
    float x  = _FogFalloff * dist * dy;
    float E  = abs(x) < 1e-3 ? 1 - 0.5*x + x*x/6.0 : (1 - exp(-x)) / x;
    float tau = _FogDensity * exp(-_FogFalloff * _CamHeight) * dist * E;   // 6.2
    tau += (_DistSigma * dist) * (_DistSigma * dist);                    // optional exp^2 term (art)
    tau  = min(tau, _ArchTau / max(dy, 0.01));                           // 6.3 cap (simplified)
    float T = exp(-tau);
    return surf * T + SkyRadiance(d, _LocalUp, _SunDir, false) * (1 - T);
}
```

### 6.5 Light shafts without volumetrics
- **Additive geometry with depth fade:** cones or quads, `Blend One One`, `ZWrite Off`, `ZTest LEqual`, transparent queue.
  - Soft-particle fade against scene depth: `fade = saturate(invFade * ((sceneEyeDepth - near) - fragEyeDepth))`,
    exactly URP's `SoftParticles` (`URP/ShaderLibrary/Particles.hlsl:84-101`). It needs the depth texture enabled on
    the URP asset or camera.
  - Near-camera fade: `saturate((eyeDepth - near) * invFar)` (`CameraFade`, `:123-127`).
  - Silhouette fade: `pow(saturate(abs(dot(N, V))), p)`, since a shaft is thickest in the middle.
  - Also multiply by the fog transmittance and by the star's visibility (eclipse fraction).
  DERIVED/VERIFIED-SOURCE.
- **Screen-space radial blur (GPU Gems 3 ch. 13, Mitchell):**
  - Per pixel: `step = (uv - lightUV) * Density / N`; `c = I(uv)`; `decay = 1`.
  - N times: `uv -= step; c += I(uv) * decay * Weight; decay *= Decay;`. Output `c * Exposure`.
  - The source image I has occluders drawn black, then the result is added to the scene.
  VERIFIED-DOCS https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process.
  - In URP, build I from depth: `I = (raw depth is far plane) ? skyBrightnessNearSun : 0`. With
    `UNITY_REVERSED_Z` the far plane is raw depth 0.
  - Typical starting values, UNVERIFIED: N = 48-64 at half or quarter resolution, Density ≈ 0.9-1.0,
    Weight ≈ 0.02-0.05, Decay ≈ 0.96-0.98.
  - Jitter the start by `IGN * step` (static) to trade banding for fine noise.
  - Fade by `saturate(dot(camForward, L))` and by the light's distance to the screen edge.

---------------------------------------------------------------------------------------------------

## 7. Star disc, limb darkening, corona, anti-firefly

- **Linear limb darkening:** `I(mu)/I(1) = 1 - u(1 - mu)`, `mu = sqrt(1 - r^2)`, r = angular distance / angular radius.
  - u ≈ 0.6 in the visible (0.607 at Hα; about 0.61 averaged).
  - u ≈ 0.56 at 600 nm and 0.95 at 320 nm, so the limb is much darker in blue and violet.
  - Quadratic solar fit at 550 nm: a0 = 0.3, a1 = 0.93, a2 = -0.23 (edge about 30%, mean about 80.5% of centre).
  VERIFIED-DOCS https://en.wikipedia.org/wiki/Limb_darkening and the search summaries (arXiv/journal list in Sources).
- Mean disc intensity for the linear law is `1 - u/3` (because ⟨mu⟩ over the disc area is 2/3), which is 0.8 for
  u = 0.6. To keep the total flux constant while tuning u, use `I0 = I_mean / (1 - u/3)`. DERIVED.
- Art choice: per-channel u, for example `u_rgb ≈ (0.50, 0.60, 0.75)`, gives a warmer limb. UNVERIFIED art values
  based on the wavelength data above.
- **Precision:** for a small disc, `1 - dot(d,L)` loses bits. Use `theta = 2*asin(0.5*length(d - L))`.
- **Edge AA:** `cov = saturate((theta_s - theta) / pixAngle + 0.5)`, with
  `pixAngle = 2*tan(vfov/2)/screenHeightPx` set from C#. That is more stable than `fwidth(theta)`. DERIVED.
- **Corona:** `G(theta) = Ic * (theta_s / max(theta, theta_s))^p * exp(-theta/theta_c)` with p ≈ 2-3, plus a broad
  Cornette-Shanks lobe in the sky (g ≈ 0.76-0.85). Leave the widest glow to bloom. UNVERIFIED (art).
- **Shadow-square eclipse:** a straight shadow edge at signed angular distance s from the disc centre. With
  `k = s/theta_s` in [-1, 1], the visible fraction of a uniform disc is `A(k) = (acos(k) - k*sqrt(1-k^2))/π`.
  Use it, or a few-sample numeric integral for the limb-darkened version, to scale the main light, `_SunTint` and the
  shafts. DERIVED.
- **Anti-firefly:**
  - Karis average: weight each sample by `1/(1 + luma)`, then divide by the sum of weights. That equals tonemap,
    average, inverse tonemap; the inverse is `c / (1 - luma/range)`.
    VERIFIED-DOCS https://graphicrants.blogspot.com/2013/12/tone-mapping.html.
  - Jimenez applies it on the mip0→mip1 downsample, over each 2x2 block, and renormalizes.
    VERIFIED-DOCS https://iryoku.com/?p=1707 (author comments).
  - URP Bloom lacks it (pitfall 0.4). Lower `Bloom.clamp`, keep the sun ≤ about 50-500 in HDR, and wrap emissive
    output in `ClampToFloat16Max` (`Common.hlsl:1698`) so no INF or NaN enters the RGBA16F or R11G11B10 buffers.

---------------------------------------------------------------------------------------------------

## 8. Cheap water

- **Schlick:** `F = F0 + (1 - F0)(1 - cos theta)^5`, `F0 = ((n-1)/(n+1))^2 = 0.0201` for n = 1.333.
  Use Core `F_Schlick(0.02, saturate(dot(N,V)))` (`CORE/ShaderLibrary/BSDF.hlsl:32-43`).
  VERIFIED-DOCS (Unity Fresnel Equation node doc: dielectric f0 typically 0.02-0.08) and VERIFIED-SOURCE.
- **Sky reflection:** `R = reflect(-V, N)`; clamp `R.up` to ≥ about 0.02 so ripples never sample below-horizon sky;
  `col = lerp(waterBody, SkyRadiance(R, up, L, false), F)`. Add the sun glint separately (GGX).
  If the sky LUT (1.3b) also contains the analytic ring arch, the reflection shows the arch for free. DERIVED.
- **Ripple normals:** a sum of K directional waves `h = Σ A_i sin(k_i·x - ω_i t + φ_i)` with deep-water dispersion
  `ω = sqrt(9.81*|k|)`. The analytic gradient is `∇h = Σ A_i cos(...) k_i`, and `N = normalize(float3(-∂h/∂x, 1, -∂h/∂z))`.
  UNVERIFIED (standard).
- **Footprint-based flattening (no sparkle):**
  - Per wave, the phase change per pixel is `w_i = |k_i| * fw`, with fw the world size of a pixel.
    Attenuate with `att_i = saturate(sin(w_i/2)/(w_i/2))` or `smoothstep(π, 0, w_i)` (2.3).
  - Move the lost slope variance into roughness. A sinusoid's slope variance is `A_i²|k_i|²/2`, so
    `var = Σ (1 - att_i²) * A_i²|k_i|²/2`.
  - Then `roughness² += 2*var`, which is exactly Core `NormalFiltering(perceptualSmoothness, variance, threshold)`
    (`CORE/ShaderLibrary/CommonMaterial.hlsl:183-190`, ref Pixar "Geometry into Shading").
  - Optionally add `GeometricNormalVariance(N, 0.25)` (`:209-215`, Kaplanyan/Tokuyoshi). Its `screenSpaceVariance`
    should be ≤ 0.25. VERIFIED-SOURCE.
  - The glint then widens into a stable highlight with distance instead of flickering. DERIVED.
- Depth tint: `body = lerp(shallow, deep, 1 - exp(-sigma_abs * (sceneEyeDepth - waterEyeDepth)))`, from
  `SampleSceneDepth` and `LinearEyeDepth`. That needs the depth texture. UNVERIFIED (standard).

---------------------------------------------------------------------------------------------------

## Sources (all accessed 2026-10-29)
- `URP/ShaderLibrary/ShaderVariablesFunctions.hlsl`: fog math, keyword guards, MixFogColor (lines 326-532).
- `URP/ShaderLibrary/Fog.hlsl`: fog keyword declaration (1-9). `URP/Shaders/Lit.shader:179`: include_with_pragmas usage.
- `Library/PackageCache/com.unity.render-pipelines.universal-config@561422b74439/Runtime/ShaderConfig.cs.hlsl:13`: USE_DYNAMIC_BRANCH_FOG_KEYWORD = 0.
- `URP/ShaderLibrary/UnityInput.hlsl:225-226`: unity_FogParams and unity_FogColor.
- `URP/Shaders/PostProcessing/Common.hlsl:188-206`: URP TPDF dither.
- `URP/Runtime/PostProcessUtils.cs:142-214`, `URP/Runtime/PostProcess.cs:112-127`: dither is temporal.
- `URP/Runtime/UniversalAdditionalCameraData.cs:919`: `dithering` property.
- `URP/Shaders/PostProcessing/Bloom.shader:16-118`, `URP/Runtime/Overrides/Bloom.cs:176-239`: bloom prefilter, clamp, no Karis.
- `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:166-174,452,1096`: HDR precision.
- `URP/ShaderLibrary/Particles.hlsl:84-127`: soft-particle and camera fade. `URP/ShaderLibrary/DeclareDepthTexture.hlsl:31-40`: SampleSceneDepth.
- `CORE/ShaderLibrary/Random.hlsl:1-111`: IGN, Jenkins hash, ConstructFloat.
- `CORE/ShaderLibrary/BSDF.hlsl:32-56`: F_Schlick. `CORE/ShaderLibrary/VolumeRendering.hlsl:110-305`: phase functions.
- `CORE/ShaderLibrary/CommonMaterial.hlsl:175-267`: normal/specular filtering. `CORE/ShaderLibrary/Common.hlsl:1206,1388,1692-1698`: depth helpers, SharpenAlpha, float16 clamp.
- https://docs.unity3d.com/6000.2/Documentation/Manual/SL-UnityShaderVariables.html: unity_FogParams definition.
- https://www.jcgt.org/published/0009/03/02/paper.pdf: Jarzynski & Olano 2020 (pcg3d/pcg4d listing, recommendations); text extracted locally.
- https://reedbeta.com/blog/hash-functions-for-gpu-rendering/: 1D pcg constants and recommendation.
- https://rene.ruhr/gfx/gpuhash: pcg3d constants cross-check (search snippet).
- https://iquilezles.org/articles/filterableprocedurals, https://iquilezles.org/articles/checkerfiltering/, https://iquilezles.org/articles/morecheckerfiltering, https://iquilezles.org/articles/bandlimiting/, https://iquilezles.org/articles/fog/: technique existence and structure (search snippets only; the site returned 404 to the fetcher).
- https://git.gay/kaneraZh/lygia/src/tag/1.4.1/math/fcos.hlsl: fcos = cos(x)*smoothstep(TWO_PI,0,fwidth(x)) (search snippet).
- https://community.monogame.net/t/solved-height-distance-based-fog-again/14371: rd.y = 0 division problem in height fog.
- https://raw.githubusercontent.com/blender/blender/v2.93.0/intern/sky/source/sky_model.cpp: Hosek-Wilkie radiance formula and BSD-3 header.
- https://cgg.mff.cuni.cz/projects/SkylightModelling/: HW reference code v1.4a (license not stated on the page).
- https://metadata.ftp-master.debian.org/changelogs/main/o/ospray/stable_copyright, https://ftp.fau.de/cran/web/packages/skymodelr/COPYRIGHTS: HW BSD-3 confirmation.
- https://www.cg.tuwien.ac.at/research/publications/2007/zotti-2007-wscg/zotti-2007-wscg-paper.pdf: Perez/Preetham structure and weaknesses (search summary).
- https://diglib.eg.org/handle/10.1111/cgf14050: Hillaire 2020 bibliographic data and LUT list.
- https://github.com/sebh/UnrealEngineSkyAtmosphere (MIT): RenderSkyCommon.hlsl (sky-view mapping, phase functions), RenderSkyRayMarching.hlsl (multiple scattering, step integration, LUT sizes).
- https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry: clipmap layout, shader steps, alpha form.
- https://www.hhoppe.com/geomclipmap.pdf: transition width n/10, alpha property, zero-area triangles (text extracted locally).
- https://mikejsavage.co.uk/geometry-clipmaps/: simplified tile/trim/seam layout and trim rotation rule.
- https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process: radial blur light shafts.
- https://graphicrants.blogspot.com/2013/12/tone-mapping.html: Karis 1/(1+luma) weighting.
- https://iryoku.com/?p=1707: Jimenez 2014 CoD:AW post-processing (IGN; 2x2 firefly weighting on first downsample).
- https://bartwronski.com/2016/10/30/dithering-part-three-real-world-2d-quantization-dithering/: TPDF vs uniform, IGN formula, black/white clipping comment.
- https://en.wikipedia.org/wiki/Dither: TPDF amplitude and noise modulation.
- https://forum.videohelp.com/threads/349125-BANDING-still and https://forum.doom9.org/archive/index.php/t-133546.html: dither vs encoder bitrate (weak, forum).
- https://en.wikipedia.org/wiki/Limb_darkening: limb laws and solar coefficients.
- https://jurnal.umsu.ac.id/index.php/alhisab/article/view/17148, https://www.icranet.org/scuola_lavoro/2023-2024/20042023/Sole_20230420_light.pdf: u ≈ 0.607 at Hα; u(600 nm) = 0.56, u(320 nm) = 0.95 (search summaries).
- https://en.wikipedia.org/wiki/Air_mass_(astronomy): Kasten-Young air mass.
- https://docs.unity3d.com/Packages/com.unity.shadergraph@16.0/manual/Fresnel-Equation-Node.html: dielectric f0 range.
