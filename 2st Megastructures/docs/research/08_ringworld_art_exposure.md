# 08 - Ringworld facts, art references, exposure adaptation, bloom, video banding

Research notes for "The Ring" (Unity 6000.6.0f1, URP 17.6.0). Accessed 2026-10-29.
Tags: VERIFIED-SOURCE (path:line in the local package / editor files), VERIFIED-DOCS (URL),
UNVERIFIED (memory or common knowledge, not confirmed), DERIVED (my own arithmetic from verified inputs).

Abbreviations: URP = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`,
CORE = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`,
CGINC = `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Resources/CGIncludes`.

---

## 1. Larry Niven's Ringworld numbers (Ringworld, 1970)

| Quantity | Value | Status |
|---|---|---|
| Radius | ~0.95 x 10^8 mi (~1.53 x 10^8 km), about 1 AU. Niven's cover text is quoted as "ninety three million miles"; reference tables give 9.5 x 10^7 mi | VERIFIED-DOCS (academickids mirror of old Wikipedia table; fandom search snippet). Sources differ: 93 vs 95 million mi |
| Width | 997,000 mi (~1.6 million km) | VERIFIED-DOCS (academickids; Wikipedia "about one million miles (1.6 million km) wide"; dennisantinori RPG page) |
| Surface gravity | 0.992 g (~9.69 m/s^2); Wikipedia says "99% as strong as Earth's" | VERIFIED-DOCS (academickids; Wikipedia) |
| Rim walls | 1,000 mi (~1,600 km) high | VERIFIED-DOCS (academickids; dennisantinori; zealotscript) |
| Spin speed | 770 mi/s (~1,240 km/s; RPG page gives 1,239 km/s) | VERIFIED-DOCS (academickids; dennisantinori) |
| Habitable area | ~3 million Earths | VERIFIED-DOCS (Wikipedia) |
| Day length | 30 h (shadow-square cycle) | VERIFIED-DOCS (academickids; dennisantinori); not needed by the game |
| Shadow squares (count, size, orbit radius) | Sources conflict: "20 squares at the orbit of Mercury" (RPG page), "10 squares, ring radius 35,980,000 mi" (Traveller forum), "1x10^6 by 2.5x10^6 mi, ~12x10^6 mi from the primary" (POV-Ray forum quoting Niven) | **NOT VERIFIED, DO NOT QUOTE. UNUSED.** No primary source found |

Physics check (DERIVED, from r = 0.95e8 mi and v = 770 mi/s):
- g = v^2 / r = 10.04 m/s^2 = **1.024 g**, not 0.992 g. To get 0.992 g at that radius, v would have to be ~758 mi/s. A Traveller "Ringworld 2.0" table uses 764.9 mi/s. **Pitfall:** never print "0.992 g" and "770 mi/s" together as if exact. Say "about one Earth gravity" and "about 770 miles per second".
- Rotation period = 2*pi*r / v = 215 h = **8.97 Earth days** (the RPG page says "9 UNS days"; consistent).
- Angular width of the far side ("the Arch") seen at the zenith: atan(997,000 mi / (2r)) = **0.30 deg**, about 0.6 full-moon diameters. At Niven's real scale the Arch overhead is a thin ribbon, and the local up-curve of the land is invisible (R = 1.5e8 km).
- A 1,000 mi rim wall seen from the centre line (498,500 mi away) subtends **0.11 deg**, so it is effectively invisible.
- **Art-direction consequence:** "land curving up into the sky" can only be seen at a much smaller radius (Halo-like), with heavy aerial perspective, or with artistic compression. Treat Niven's figures as narration flavour, not as the render scale.

Halo comparison (inspiration only): Halopedia lists Installation 04 with a diameter of 10,000 km and a surface width of 318 km. VERIFIED-DOCS (fan wiki, unsourced; its own talk page questions the origin of the 10,000 km figure). DERIVED: at R = 5,000 km the land rises about d^2/(2R), so roughly 1 km of rise at 100 km and 25 km of rise at 500 km. The up-curve becomes readable through haze at tens to hundreds of km.

## 2. Dyson swarm (one paragraph)

Freeman J. Dyson, "Search for Artificial Stellar Sources of Infrared Radiation", *Science* 131(3414):1667-1668 (1960), doi:10.1126/science.131.3414.1667 (VERIFIED-DOCS, Wikipedia "Dyson sphere"). Dyson argued that an energy-hungry civilization would intercept most of its star's light with orbiting structures. Those structures would re-radiate the energy as waste heat in the infrared, so infrared excess around a dimmed star is a detectable technosignature. The paper speaks of a "shell" or "biosphere" but gives no construction. Dyson later clarified that "A solid shell or ring surrounding a star is mechanically impossible." What he meant was "a loose collection or swarm of objects traveling on independent orbits around the star", now called a **Dyson swarm** (VERIFIED-DOCS, Wikipedia). Fiction turned it into a solid shell. A swarm can be built incrementally, and each element follows its own orbit, so there is no shell-stability problem. The idea goes back to Olaf Stapledon's *Star Maker* (1937). Wikipedia notes that Dyson suggested "Stapledon sphere" as the fairer name. Visual implication for the final platform: the star should show through gaps between thousands of separate collectors on inclined orbits, as dark specks or glinting panels in many orbital planes. It should not sit behind a continuous surface.

## 3. Art references (inspiration only; nothing is copied or imported)

What makes huge things read as huge:
1. **Aerial perspective** (VERIFIED-DOCS, Britannica and art-education summaries via search). With distance, contrast falls, saturation falls, edges soften, and colour shifts toward the sky colour (blue by day, warm near sunset). Leonardo described this in his Treatise on Painting, including the "perspective of disappearance" (loss of detail with distance). Rayleigh explained the blue in 1871. Shader rule: distant ring land must converge toward the in-scatter colour. Never draw the far side at full contrast.
2. **Known-size scale figures** (VERIFIED-DOCS, concept-art guides via search). A familiar object such as a tree, a person-sized railing, a bridge truss or a lamp post gives the eye a yardstick. Put repeated objects of the same size at many distances (diminution).
3. **Draw distance plus emptiness** (VERIFIED-DOCS, Shadow of the Colossus developer interview, siliconera/glitterberri). Fog and visible far scenery carried the mood, and the openness and emptiness accentuated the sense of scale.
4. **Slow camera and slow motion** (UNVERIFIED, film-craft common knowledge). Big things move slowly in angular terms, so slow walking speed, slow cloud and shadow drift and long takes all make objects read as large. Fast motion reads as small. Keep head-bob tiny and turn rates gentle.
5. **Film notes.** Blade Runner 2049: Deakins and Villeneuve insisted on shooting exteriors in grey, smoggy, foggy light, and the monolithic interiors got moving caustic "sunlight" patterns (VERIFIED-DOCS, shootonline and ASC via search). Arrival: DP Bradford Young described "soft, atmospheric light" and "spectacular scale with these deep rich black walls and a beautiful soft light source" (VERIFIED-DOCS, ASC article via search). Interstellar: UNVERIFIED (not researched); the common reading is a tiny craft against vast phenomena, with long, quiet holds.
6. **Shadow as a scale cue** (DERIVED). The Sun's angular diameter is ~0.533 deg (standard value, UNVERIFIED-not-fetched), so a penumbra is ~9.3 m wide per km between occluder and ground. An ark ship 10 km up casts a shadow edge ~93 m soft, and a ship 50 km up casts one ~465 m soft. A soft, slowly moving shadow edge sweeping over the land sells altitude and size. A razor-sharp edge reads as small and close.

Golden-hour valley photography principles (VERIFIED-DOCS, naturettl, Adobe, 500px via search):
- A low sun means long shadows and warm tones. The longer air path scatters out blue, so direct light shifts to yellow, orange and red (Adobe).
- **Side light** reveals texture and depth. Front light is flat. Backlight is dramatic but needs exposure care and strong graphic shapes (naturettl).
- Warm haze glows in the early part of the window.
- Shadows are lit only by skylight, so they read cool or blue against the warm key. This is the classic warm-key / cool-fill split. (Partially VERIFIED: the snow-shadow-is-blue statement came from search results; the golden-hour generalisation is physics inference.)

Glazed white ceramic architecture (Sydney Opera House as the reference):
- 1,056,006 ceramic tiles by Hoganas AB (Sweden), self-cleaning by rain (VERIFIED-DOCS, sydneyoperahouse.com via search snippet; the page 404'd on direct fetch).
- A chevron pattern of **glossy white and matte cream** tiles. From a distance the shells read as uniform white, and pure white glossy tiles were avoided because of glare (VERIFIED-DOCS, secondary via search). The tile surface was described as "glossy non-reflective" and "like hammered silver - uneven and vibrant" (substack via search).
- Shader cues (UNVERIFIED, PBR common practice): glaze is a dielectric with F0 ~0.04 (IOR ~1.5). Use albedo ~0.75-0.85 (never pure 1.0) with a slightly warm cream tint. Mix two roughness populations, glossy ~0.05-0.15 and matte ~0.35-0.5, in a tile pattern. Add low-frequency normal waviness ("orange peel" / hammered) so the sun reflection breaks into a lively streak instead of a mirror blob. That also tames sun fireflies (see section 5).

## 4. Eye adaptation: time constants, EV ramps, avoiding pumping

Human vision (VERIFIED-DOCS, Wikipedia "Adaptation (eye)"):
- Light adaptation (dark to bright) is fast, "within seconds" (the article also says "five minutes"; it is inconsistent).
- Dark adaptation is slow. Cones reach maximum sensitivity in 9-10 min. Rods take over after 5+ min. Full adaptation from sunlight to darkness takes ~20-30 min, and rods need 30-45 min or longer.
- Instantaneous contrast is ~1,000:1. The total range covers ~9 orders of magnitude.
- The asymmetry (bright adaptation fast, dark adaptation slow) is the key fact. Pattanaik et al., SIGGRAPH 2000, "Time-Dependent Visual Adaptation for Realistic Image Display", is built on it (VERIFIED-DOCS, MPI page / SIGGRAPH history).

Road tunnels (VERIFIED-DOCS, CIE 88:2004 summaries via search):
- The zones are access, threshold, transition, interior and exit.
- The "black hole effect" happens at the entrance and is countered by a bright threshold zone. The transition zone steps luminance down with ratios no greater than 1:3.
- At the exit the eye adapts dark to light quickly.
- Design lesson for the game tunnel: never make the interior pitch-black. Light the first stretch brighter and fall off in steps so the exposure swing stays at about 5-7 stops.

Engine conventions and constants:
- **SRP Core helper** (VERIFIED-SOURCE `CORE/ShaderLibrary/PhysicalCamera.hlsl:66-73`):
  `float ComputeLuminanceAdaptation(float previousLuminance, float currentLuminance, float speedDarkToLight, float speedLightToDark, float deltaTime)` = `prev + delta * (1.0 - exp2(-deltaTime * speed))`. This is frame-rate independent. **Time constant tau = 1 / (speed * ln 2)**: speed 1 gives 1.44 s, speed 2 gives 0.72 s, speed 3 gives 0.48 s (DERIVED).
- The same file has `ComputeEV100(aperture, shutter, ISO)` (:12), `ComputeEV100FromAvgLuminance(avgLum)` with K = 12.5 (:30-40), and `ConvertEV100ToExposure(EV100)` = 1 / (1.2 * 2^EV100) (:42-58).
- **Unity PPv2 AutoExposure defaults** (VERIFIED-DOCS, GitHub PostProcessing v2 AutoExposure.cs/.compute): `filtering` = (50, 95) %, `speedUp` = 2 (dark to light), `speedDown` = 1 (light to dark), `keyValue` = 1, `Progressive`. The compute shader runs `exposure = old + delta * (1 - exp2(-dt * speed))`, with the speed chosen by the sign of the delta, and `exposure = keyValue / avgLuminance`.
- **Unreal**: Speed Up and Speed Down are in f-stops per second, with Python API defaults speed_up = 3.0 and speed_down = 1.0 (VERIFIED-DOCS, Unreal Python API via search). The adaptation moves linearly when far from the target and exponentially within 1.5 stops (`r.EyeAdaptation.ExponentialTransitionDistance`). Histogram Low/High percent starting ranges are 70-80 and 80-95. A metering mask weighting the screen centre "helps stabilize auto exposure" (VERIFIED-DOCS, Epic docs).
- HDRP defaults (Dark to Light 3, Light to Dark 1): UNVERIFIED (docs page did not list defaults).
- Histogram auto exposure write-up (VERIFIED-DOCS, alextardif.com): `adapted = last + (avg - last) * (1 - exp(-dt * tau))`, with tau ~1.1 as the starting point, 256 log2 bins, range [-10, +2], and near-black bin 0 excluded.

EV reference values at ISO 100 (VERIFIED-DOCS, Wikipedia "Exposure value"; L = 2^(EV-3) cd/m^2):
| Condition | EV100 |
|---|---|
| full sun | 15 (about 4096 cd/m^2) |
| hazy sun | 14 |
| heavy overcast | 12 |
| at sunset | 12 |
| just before sunset | 12-14 |
| just after sunset | 9-11 |
| home interiors | 5-7 |
| offices | 7-8 |

Tunnel exit (outside / inside) is therefore ~7-9 stops raw, and sun to post-sunset is ~4-6 stops.

**URP 17.6 has NO auto-exposure component** (VERIFIED-SOURCE: no Exposure override in the list of `URP/Runtime/Overrides/*.cs`). The only volume exposure is `ColorAdjustments.postExposure` (EV, `FloatParameter(0f)`, `URP/Runtime/Overrides/ColorAdjustments.cs:114`). It is applied as `Mathf.Pow(2f, postExposure)` (`URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs:353-354`).
**Pitfall:** postExposure is multiplied in **after bloom**. `URP/Shaders/PostProcessing/Common.hlsl:115-116` reads `// Artist request to fine tune exposure in post without affecting bloom, dof etc` / `input *= postExposure;`, and bloom is added earlier at `UberPost.shader:233-234`. So the bloom threshold and clamp act on **un-exposed scene radiance**.

Recommended approach for a linear showcase (synthesis; UNVERIFIED as best practice, consistent with the sources above):
1. **Authored exposure track keyed by path progress**, plus a slow metered correction (at most +/-0.5 EV, tau 3-5 s). The result is deterministic for captures, and reactive pumping is impossible.
2. Anti-pumping if any metering is used: compute a log-luminance histogram on a small target and trim percentiles (for example 50-90 %). Exclude the sun disc and sky (mask, or clamp luminance into the meter at about 4x the target). Weight the centre. Add a dead-band (ignore errors under ~0.3 EV). Use asymmetric speeds and clamp to a min/max EV per zone.
3. **Tunnel entry** (bright to dark): let the image go dark first, then open up 3-5 stops with tau ~1.5-2.5 s, so it settles in about 4-6 s. Never fully compensate; the tunnel should still feel dim (leave the interior ~1-2 stops darker than mid-grey).
4. **Tunnel exit** (dark to bright): a reactive meter would start closing down as the portal fills the frame. Script that by starting the ramp ~1-2 s before the portal. Allow a deliberate bloom/white-out of about +1.5 to +2.5 EV over target at the threshold, then settle with tau ~0.5-0.8 s (about 2-3 s total). Fast bright adaptation matches physiology.
5. **Shadow-square evening**: compensate only about 50-70 % of the light drop. Photographic practice goes EV 15 to 12 at sunset to 9-11 after, so the scene should get visibly darker and warmer. Ease with smoothstep over the whole evening segment. No per-frame metering.
6. Because bloom ignores postExposure, either (a) pre-expose: multiply the sun, sky, emissive and light intensities by a global `_RingExposure` in our own HLSL and C# light setup so the HDR buffer is already exposed (best; it also keeps values inside the R11G11B10 / LogC ranges in section 5), or (b) compensate the bloom threshold every frame (skeleton below).

```csharp
// Exposure driver skeleton (URP 17.6). Names verified: ColorAdjustments.postExposure, Bloom.threshold,
// VolumeProfile.TryGet<T>, VolumeParameter.overrideState/value.
using UnityEngine; using UnityEngine.Rendering; using UnityEngine.Rendering.Universal;
public sealed class ExposureDirector : MonoBehaviour {
    public VolumeProfile profile; public AnimationCurve evByProgress; // authored EV vs path t
    public float displayBloomThreshold = 1.0f; // linear, in *exposed* units
    ColorAdjustments ca; Bloom bloom; float ev;
    void Awake(){ profile.TryGet(out ca); profile.TryGet(out bloom);
        ca.postExposure.overrideState = true; bloom.threshold.overrideState = true; }
    public void Tick(float pathT, float dt, float speedDarkToLight = 2f, float speedLightToDark = 0.6f){
        float target = evByProgress.Evaluate(pathT);
        // postExposure rising (d > 0) = brightening the image = scene got darker = light-to-dark adaptation (slow)
        float d = target - ev; float speed = d > 0 ? speedLightToDark : speedDarkToLight;
        ev += d * (1f - Mathf.Pow(2f, -dt * speed));               // same law as PhysicalCamera.hlsl:72
        ca.postExposure.value = ev;
        float lin = displayBloomThreshold * Mathf.Pow(2f, -ev);   // threshold in scene units
        bloom.threshold.value = lin < 1f ? Mathf.LinearToGammaSpace(lin) : Mathf.Pow(lin, 1f/2.2f); // see 5.2
    }
}
```
(Option (a), pre-exposure, makes the threshold compensation unnecessary. Only one of the two should be used.)

## 5. URP 17.6 Bloom with very bright HDR suns (radiance ~90-200)

### 5.1 Parameters (VERIFIED-SOURCE `URP/Runtime/Overrides/Bloom.cs`)
- `threshold` MinFloatParameter(0.9, min 0), "gamma-space" (:176)
- `intensity` MinFloatParameter(0, min 0) (:182). The docs say "0 to 1", but the source has **no max**. `IsActive()` returns intensity > 0 (:245).
- `scatter` ClampedFloatParameter(0.7, 0..1) (:188)
- `clamp` MinFloatParameter(65472) (:195)
- `tint` ColorParameter(white, HDR) (:201)
- `highQualityFiltering` BoolParameter(false) (:208)
- `filter` BloomFilterModeParameter(Gaussian), new in 6000.x, with Gaussian / Dual / Kawase (:24-43, :214). The docs say the parameters must be retuned per method.
- `downscale` DownscaleParameter(Half) (:220)
- `maxIterations` ClampedIntParameter(6, 2..8) (:226)
- `dirtTexture`, `dirtIntensity` (:233, :239)

### 5.2 Exact math (VERIFIED-SOURCE)
- C# setup (`URP/Runtime/Passes/PostProcess/BloomPostProcessPass.cs:95-106`):
  - `threshold = Mathf.GammaToLinearSpace(bloom.threshold.value)`, with the knee hardcoded to `threshold * 0.5f`. `clamp` is used as-is (linear).
  - Gaussian scatter: `Mathf.Lerp(0.05f, 0.95f, scatter)`. Kawase: `Clamp01(scatter)`. Dual: `Lerp(0.3f, 1.3f, scatter)`.
  - All of these go into `_Params = (scatter, clamp, threshold, knee)` and `_Params2`.
- Gamma-to-linear for values above 1: Unity's exact helper uses `pow(value, 2.2)` (`CGINC/UnityCG.cginc:95-103`). Mathf.GammaToLinearSpace is native; assume the same (UNVERIFIED). DERIVED mapping of threshold parameter to linear: 0.9 -> 0.787, 1.0 -> 1.0, 1.5 -> 2.44, 2 -> 4.59, 3 -> 11.2, 5 -> 34.5. The inverse for values above 1 is `pow(x, 1/2.2)` (`UnityCG.cginc:114-124`).
- Prefilter (`URP/Shaders/PostProcessing/Bloom.shader:67-118`):
  1. Without HQ, a single bilinear tap. With HQ (`_BLOOM_HQ`), a 13-tap box (CoD:AW style) with **equal weights and no Karis/luma weighting** (:79-100).
  2. Then `color = min(ClampMax, color)`, a **per-channel** clamp (:106).
  3. Then `brightness = Max3(r,g,b)` (the max channel, not luminance) with a quadratic soft knee: `multiplier = max(brightness - T, soft) / max(brightness, 1e-4)` (:109-113).
  4. Then `max(color, 0)` to kill NaN/negative values (:116).
- Upsample: `lerp(highMip, lowMip, Scatter)` (:174). HQ uses a bicubic low-mip sample (:168-169), and the Uber composite also samples bicubically with HQ (`UberPost.shader:223-224`).
- Composite (`UberPost.shader:233-234`): `bloom *= BloomIntensity; color += bloom * BloomTint;`. This is purely additive, with no energy conservation. The tint is normalised to luminance 1 (`UberPostProcessPass.cs:412-415`). Order in Uber: bloom (:213), vignette (:259), color grading with postExposure (:273), grain (:276), sRGB (:283-294), dithering (:296).
- Mip pyramid (`BloomPostProcessPass.cs:161-193`): Half gives width >> 1, Quarter gives width >> 2. The mip count is `clamp(floor(log2(maxSide) - 1), 1, maxIterations)`. The format is the camera colour format (:458), i.e. HDR. Kawase uses a single fixed-size texture (:463).

### 5.3 Pitfalls for a 90-200 radiance sun
1. **Per-channel clamp shifts hue.** A warm sun (200, 140, 70) with clamp 50 becomes (50, 50, 50), a white halo. Either keep clamp above the sun's channel values and lower the intensity, or clamp by luminance in our own sky shader (scale the RGB so that max <= C before writing the sun), or retint with the bloom `tint`. DERIVED from :106.
2. **Bloom is threshold-relative to un-exposed radiance** (see section 4). With postExposure -3 EV and threshold 1, everything above 1 in scene units blooms, which can be most of a sunlit 3-8 radiance landscape. Use pre-exposure or the threshold compensation.
3. **Fireflies** come from sub-pixel sun glints on glossy ceramic, water and metal, because the sun is a near-delta light of radiance ~150. URP has **no Karis average**. Mitigations:
   - HQ filtering on (13-tap plus bicubic reduces flicker; the docs say it "reduces flickering").
   - `clamp` around 20-60 in exposed units.
   - Clamp the minimum roughness for sun specular (perceptual roughness >= ~0.08-0.12).
   - Geometric specular AA (Toksvig / Kaplanyan-style roughness widening from normal variance).
   - An analytically anti-aliased sun disc edge in the sky shader. A disc that pops between 3 and 4 px makes the halo flicker.
   - TAA/STP if used. Note that Jimenez's CoD:AW bloom weights the mip0 to mip1 downsample by 1/(1+luma) per 2x2 block to suppress fireflies (VERIFIED-DOCS, iryoku.com). URP does not do this, so a custom prefilter would be needed. We do not modify package shaders, so the practical route is to keep the scene's peak values controlled at the source.
4. **Halo size depends on resolution** (DERIVED from :161-193). At 1080p/Half the bloom base is 960 px, giving 8 possible mips capped at 6, with the lowest mip 30 px wide. At 2160p/Half the base is 1920 px, again capped at 6, with the lowest mip **60 px**. The bloom therefore looks about **2x tighter in 4K captures** than in 1080p tests. Fix: use `downscale = Quarter` when rendering 4K (the pyramid is then identical to 1080p/Half), or compare captures at the final recording resolution only. `maxIterations = 8` also widens the glow.
5. **HDR buffer precision.** The default `HDRColorBufferPrecision._32Bits` selects `B10G11R11_UFloatPack32` (`URP/Runtime/UniversalRenderPipelineCore.cs:1484-1485`; the default is set at `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:452`). That format has 6/6/5-bit mantissas, so blue steps by ~3 % relative. This can band dusk skies **before** any dithering. For the showcase use `UniversalRenderPipelineAsset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits` (property at :1096), which gives R16G16B16A16_SFloat.
6. **HDR grading LUT range**: `saturate(LinearToLogC(input))` (`Common.hlsl:123`, LogC constants at `CORE/ShaderLibrary/Color.hlsl:349-358`) clips at **~58.9 linear after postExposure** (DERIVED). Per-channel clipping above that turns a bright warm sky near the sun toward white or yellow. Keep exposed sky values under ~50 except the sun disc.
7. Starting values to test (UNVERIFIED, tune with captures), in exposed units: threshold 1.0-1.3 (gamma parameter), intensity 0.15-0.4, scatter 0.6-0.75, clamp 30-60, HQ on, Gaussian, maxIterations 7-8. The sun disc should be ~100-200x mid-grey before tonemapping so ACES/Neutral rolls it to white with a soft glow rather than a hard white blob.

## 6. Video encoding and banding (YouTube / H.264)

- **Why gradients crush.** Banding comes from too few bits and from too little bitrate to keep neighbouring values distinct (forasoft). YouTube always re-encodes. Flat, slow gradients are where the encoder spends the fewest bits, because large DCT blocks get coarsely quantized and the stair-steps reappear. Banding often appears only after the upload is recompressed (VERIFIED-DOCS, forasoft / ProVideo Coalition / VideoHelp via search). Motion masks artifacts, so CRF raises QP on high-motion frames (slhck). A slow, quiet walk is therefore the worst case for banding.
- **YouTube upload recommendations** (VERIFIED-DOCS, support.google.com/youtube/answer/1722171):
  - H.264, progressive, High Profile, 2 consecutive B-frames, closed GOP of half the frame rate, CABAC, variable bitrate, no bitrate limit required.
  - 4:2:0 chroma. SDR BT.709 primaries, transfer and matrix (H.273 value 1). MP4 with moov at the front. AAC-LC 48 kHz.
  - SDR bitrates: 1080p 8 Mbps (24-30 fps) / 12 Mbps (48-60 fps); 1440p 16 / 24 Mbps; 2160p 35-45 / 53-68 Mbps.
- **Upload at 1440p or 4K** even when the master is 1080p, so YouTube serves its higher-bitrate VP9/AV1 ladder. Community observation is that 1080p uploads often stay AVC (VERIFIED-DOCS, community blogs; not official, treat as likely).
- **CRF** (VERIFIED-DOCS, slhck CRF guide + shotstack):
  - Constant-quality rate control for x264/x265. The range is 0-51 with 0 lossless; x264 defaults to 23 and x265 to 28. 18-28 is the "sane" range, and ~18 is near-transparent.
  - "A change of +/-6 should result in about half/double the file size".
  - VP9 CRF ranges 0-63 and needs `-b:v 0`.
  - For the master, use x264 CRF 12-16 with `-preset slow -tune film` (or `grain` if film grain is added) and `-pix_fmt yuv420p`, BT.709 tagged. The tune advice is UNVERIFIED (the ffmpeg wiki was blocked).
- **Dither strategy.**
  - URP's built-in dithering (`UniversalAdditionalCameraData.dithering`, `URP/Runtime/UniversalAdditionalCameraData.cs:919`) is **temporal blue noise** (VERIFIED-SOURCE). The texture index cycles every frame (`URP/Runtime/PostProcess.cs:112-120`) and there is a random UV offset seeded by `Time.frameCount` (`URP/Runtime/PostProcessUtils.cs:202-213`), so it is deterministic per frame index. The amplitude is TPDF +/-1 LSB in sRGB space (`URP/Shaders/PostProcessing/Common.hlsl:188-206`).
  - Triangular-PDF noise (Gjol, Playdead, "Banding in Games: A Noisy Rant") removes bands without over-noising. Blue noise avoids clumping (VERIFIED-DOCS, Bart Wronski).
  - **Static vs temporal for recording** (synthesis; partly UNVERIFIED):
    - Temporal noise changes every frame, so motion prediction fails and the encoder either spends bits on it or smears it away (VERIFIED-DOCS, AV1 film-grain write-ups). At YouTube bitrates a +/-1 LSB temporal dither is largely removed, and the banding returns.
    - A static (screen-locked) pattern is cheap to encode but becomes a visible "screen-door" during camera motion. A forum consensus favours static/ordered over noise-like dither for encodes (VERIFIED-DOCS, VideoHelp; anecdotal).
    - Recommended for this project: (1) render with the 64-bit HDR buffer and keep URP temporal dithering on for live play. (2) For capture, add a subtle **luminance film grain of ~2-3 LSB amplitude with a ~1.5-2 px correlation size**, applied in sRGB space after tonemapping. Coarser and stronger than a 1-LSB dither, it survives YouTube better. Animate it at most once per 2 frames, or keep it world/sky-anchored so it is not pure per-pixel temporal noise. (3) Avoid large, perfectly smooth, dark gradients in the art: break skies with faint cloud and noise structure and keep dusk skies above ~10-15 % sRGB. (4) Master at high quality (CRF 12-16 or an intermediate codec) and upload at 4K.
  - AV1 film-grain synthesis (strip grain, send parameters) exists, but denoising before compression "can make banding more pronounced" (VERIFIED-DOCS, Norkin DCC 2026 paper abstract via search). YouTube's own pipeline is not under our control.

---

## Sources (all accessed 2026-10-29)
- https://en.wikipedia.org/wiki/Ringworld - width 1.6 million km, 99 % g, 3 million Earths, shadow squares exist (no numbers).
- https://academickids.com/encyclopedia/index.php/Ringworld - table: radius 0.95e8 mi, width 997,000 mi, rim 1,000 mi, 770 mi/s, 0.992 g, 30 h day.
- https://larryniven.fandom.com/wiki/Ringworld (search snippets only; fetch returned 402) - corroborates width, rim walls, 770 mi/s, 0.992 g, 9.5e7 mi.
- https://www.dennisantinori.com/ringworldrpg/14109.html - RPG figures (1,239 km/s, 1,000 mi walls); conflicting shadow-square data (not used).
- https://zealotscript.co.uk/2017/10/08/superstructure-of-the-week-ringworld/ - 1,000 mi walls, 99.2 % g.
- https://travellerrpg.com/threads/ideas-for-polities-worlds-and-organisations.12869/page-2 and https://news.povray.org/4360830a%40news.povray.org (search snippets) - conflicting shadow-square numbers, proof that they are unverified.
- https://www.halopedia.org/Installation_04 (search snippet) - Halo ring 10,000 km diameter, 318 km width.
- https://en.wikipedia.org/wiki/Dyson_sphere - Dyson 1960 citation, swarm vs shell, Stapledon.
- https://www.scientificamerican.com/blog/life-unbounded/revisiting-the-dyson-sphere (search snippet) - Dyson's 2003 "swarm" remark.
- https://en.wikipedia.org/wiki/Adaptation_(eye) - human light and dark adaptation time courses.
- https://resources.mpi-inf.mpg.de/tmo/pattanaik00/index.html, https://history.siggraph.org/?p=118253 (search snippets) - Pattanaik 2000 asymmetric adaptation model.
- https://www.mankier.com/1/pfstmo_pattanaik00 - pfstmo implementation options.
- https://dev.epicgames.com/documentation/en-us/unreal-engine/auto-exposure-in-unreal-engine - f-stops/s, linear-then-exponential transition at 1.5 stops, percentiles, metering mask.
- https://docs.unrealengine.com/5.0/en-US/PythonAPI/class/CameraExposureSettings.html (search snippet) - speed_up 3.0 / speed_down 1.0 defaults.
- https://raw.githubusercontent.com/Unity-Technologies/PostProcessing/v2/PostProcessing/Runtime/Effects/AutoExposure.cs and .../Shaders/Builtins/AutoExposure.compute - PPv2 defaults and exp2 adaptation law.
- https://docs.unity3d.com/Packages/com.unity.postprocessing@3.4/manual/Auto-Exposure.html - property semantics.
- https://www.alextardif.com/HistogramLuminance.html - histogram auto exposure, tau 1.1, log range [-10, 2].
- https://bruop.github.io/exposure/ - histogram exposure, temporal blending.
- https://en.wikipedia.org/wiki/Exposure_value - EV100 table, L = 2^(EV-3).
- CIE 88:2004 summaries via search (scribd "Tunnel-Int", inrim x046-PP28.pdf, smartcitiesdive) - tunnel zones, 1:3 steps, black-hole effect.
- https://docs.unity.com/en-us/engine/6000.7/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom.md - URP Bloom property docs (Filter, HQ, Downscale, Max Iterations).
- https://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare/ - CoD:AW bloom, 1/(1+luma) anti-firefly weighting.
- https://support.google.com/youtube/answer/1722171 - YouTube upload encoding settings and bitrates.
- https://slhck.info/video/2017/02/24/crf-guide.html - CRF meaning, ranges, +/-6 rule.
- https://shotstack.io/learn/ffmpegcrf/ - CRF ranges for H.264/H.265/VP9.
- https://bartwronski.com/2016/10/30/dithering-part-three-real-world-2d-quantization-dithering/ - triangular vs blue-noise dithering.
- https://www.forasoft.com/learn/video-encoding/glossary/terms/banding, https://provideocoalition.com/?p=6172, https://forum.videohelp.com/showthread.php?p=2235440 (search snippets) - banding causes, grain workaround, static vs noise dither (anecdotal).
- https://dev.to/masonwritescode/turn-on-av1-film-grain-synthesis-and-measure-what-it-saves-on-your-own-footage-37bb, https://norkin.org/pdf/DCC_2026_FGS_with_debanding.pdf (search snippets) - grain bitrate cost, FGS and banding.
- https://filmdaft.com/how-to-get-better-image-quality-on-your-youtube-videos/, https://clip.dor.gg/en/blog/upload-game-video-without-quality-loss (search snippets) - upload-at-higher-resolution tip (community).
- https://www.siliconera.com/graphic-designers-discuss-creating-shadow-of-the-colossus-majestic-world/, https://glitterberri.com/extended-developer-interview/the-graphic-designers-tell-it-straight-2/ (search snippets) - fog and draw distance for scale.
- https://shootonline.com/?p=47151, https://theasc.com/articles/arrival-cinematography-bradford-young (search snippets) - Blade Runner 2049 and Arrival light/atmosphere notes.
- https://www.naturettl.com/?p=19594, https://www.adobe.com/creativecloud/photography/discover/golden-hour (search snippets) - golden-hour side, front and back light, warm tones.
- https://www.sydneyoperahouse.com/uncovered/uncovered-milly-dent (search snippet; direct fetch 404) and https://www.yourlifechoices.com.au/travel/fun-facts-about-the-sydney-opera-house/ - 1,056,006 Hoganas tiles, glossy and matte, glare avoidance.
- https://www.britannica.com/art/aerial-perspective (search snippet; fetch 403) - aerial perspective definition, Leonardo.
- Local package source: URP/Runtime/Overrides/Bloom.cs; URP/Runtime/Passes/PostProcess/BloomPostProcessPass.cs; URP/Shaders/PostProcessing/Bloom.shader; URP/Shaders/PostProcessing/UberPost.shader; URP/Shaders/PostProcessing/Common.hlsl; URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs; URP/Runtime/PostProcessUtils.cs; URP/Runtime/PostProcess.cs; URP/Runtime/UniversalRenderPipelineCore.cs; URP/Runtime/Data/UniversalRenderPipelineAsset.cs; URP/Runtime/UniversalAdditionalCameraData.cs; URP/Runtime/Overrides/ColorAdjustments.cs; CORE/ShaderLibrary/PhysicalCamera.hlsl; CORE/ShaderLibrary/Color.hlsl; CGINC/UnityCG.cginc - all bloom, exposure, dither and format facts tagged VERIFIED-SOURCE.
