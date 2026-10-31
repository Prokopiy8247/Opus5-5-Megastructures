# Abyss — shaders, materials and procedural textures

Owner: shader/material builder. Spec: MASTER_PROMPT Part 8, 10, 14.2, 14.7.3/4/7/9. Contracts: `docs/CONTRACTS.md` §7-8.
All shaders are handwritten URP 17.6 HLSL in `Assets/_Game/Shaders/`, named `Megastructure/*`. No Unity fog keywords, no
`_FORWARD_PLUS`, no SH, no reflection probes, no material keywords (every variant is an explicit `multi_compile`).

## 1. Shader list

| Shader | File | MatIds | Passes (LightMode) | Queue |
|---|---|---|---|---|
| `Megastructure/Surface` | `Surface.shader` | ConcreteBoard, ConcreteFloor, ConcreteCyl, SteelPainted, SteelWeathered, RodPolished, Cable, DoorLeaf, HoneyTube | UniversalForward, ShadowCaster, DepthOnly, DepthNormals | Geometry |
| `Megastructure/Grating` | `Grating.shader` | Grating | same 4 | AlphaTest |
| `Megastructure/Facade` | `Facade.shader` | Facade (`_Recipe` 0), FacadeModule (`_Recipe` 1) | same 4 | Geometry |
| `Megastructure/Emissive` | `Emissive.shader` | LampAmber, LampCold, ProxyCard, GradientCard | same 4 | Geometry |
| `Megastructure/Glow` | `Glow.shader` | Glow | UniversalForward (additive) | 3000 |
| `Megastructure/BeamWedge` | `BeamWedge.shader` | BeamWedge | UniversalForward (additive) | 2950 (explicit) |
| `Megastructure/Lattice` | `Lattice.shader` | Lattice | UniversalForward, DepthOnly, DepthNormals | Geometry+10 |
| `Megastructure/Sky` | `Sky.shader` | `M_Sky` (RenderSettings.skybox) | one unnamed pass | Background |
| `Megastructure/FinalDither` | `FinalDither.shader` | `M_FinalDither` | one pass (FullScreenPassRendererFeature) | — |
| `Megastructure/ShadowOnly` | `ShadowOnly.shader` | ShadowCaster | ShadowCaster only | Geometry |

Includes (`Include/`): `AbyssCommon.hlsl` (globals, PCG hashes, IGN, `_AbFade`, PatternPos, filtered primitives,
NaN-safe helpers, RUST mask and colour, ThinLine, lamp ignite), `AbyssFog.hlsl` (FogSky, fog domains, `AbyssApplyFog`, `AbyssFogAdditive`, `AbyssSkyColor`),
`AbyssLighting.hlsl` (BRDF, main light + cascades, Forward+ cluster loop, trilight ambient, FogSky environment, specular
AA), `AbyssPasses.hlsl` (one vertex function and the ShadowCaster / DepthOnly / DepthNormals fragments for every opaque
shader), `AbyssSurfaceRecipes.hlsl` (the Surface recipes). Include paths are project-absolute (`Assets/_Game/...`).

Lit forward pass keywords (exactly Lit's names, all `multi_compile_fragment`): `_MAIN_LIGHT_SHADOWS
_MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN`, `_ADDITIONAL_LIGHTS`, `_SHADOWS_SOFT _SHADOWS_SOFT_LOW
_SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`, `_CLUSTER_LIGHT_LOOP`; plus `multi_compile_instancing`. ShadowCaster:
`multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW`. DepthNormals: `_GBUFFER_NORMALS_OCT`. Not used (by design): light
layers, cookies, SSAO, additional-light shadows (local lights cast none), LOD cross-fade (we use `_AbFade`), DOTS
instancing (the GPU Resident Drawer therefore skips our renderers: wanted for the moving lift, 14.7.1).

SRP Batcher: each shader declares ONE `UnityPerMaterial` CBUFFER in its SubShader-level include block, so every pass
has the identical layout. Render-state-only properties (`_Cull`, `_ZTest`, `_AlphaToMask`) are not in the CBUFFER.
GPU instancing: `multi_compile_instancing`, no `UNITY_INSTANCING_BUFFER` material properties; per-batch values (the
batch `_AbFade`) go through the core's MaterialPropertyBlock. Audited (g01 round) and guarded by EditMode tests:
`EveryShaderHasOneMaterialCBufferCoveringItsProperties` (one CBUFFER, before the first Pass, every non-render-state
property in it and every member a property, no include declares one, no `UNITY_INSTANCING_BUFFER_START`) and
`EveryPassAppliesFadeAndFog` (opaque forward passes clip by `_AbFade` and `_DistFade` and end in `AbyssApplyFog`; every
ShadowCaster / DepthOnly / DepthNormals pass uses `AbVert` (ThinLine) and the `AbDepthClip` fragments; additive shaders
use `|_AbFade|` and `AbyssFogAdditive`). Sky and FinalDither have no material properties (no CBUFFER).

**Key light mask `_KeyLightMask`** (Surface, Grating, Facade, Lattice; `Range(0, 1)`, default 1, in the CBUFFER of all
passes): scales ONLY the main-light (Key) term, diffuse and specular after its shadow (`AbyssShadeLit(..., keyLightMask)`;
the Lattice multiplies `sun.color`). Additional lights, the trilight ambient, the FogSky environment, emission and fog are
unaffected. Use it on interior materials the directional Key must not reach (beyond the shadow cascades it would light
the inside of the gallery / hall / tunnel): `ctx.Mats.Variant(MatId.ConcreteBoard, "interior", m => m.SetFloat("_KeyLightMask", 0f))`.
MaterialBuilder writes 1 on every base material that has it. (Core: a `ShaderIds.KeyLightMask` id is not defined yet.)

NaN guards: normals go through `AbSafeNormalize(v, fallback)` (degenerate normals, cancelled perturbations), view rays
through `AbViewRay` (camera at the fragment), `AbSmoothstepSafe` replaces `smoothstep(a, b, x)` where a = b is possible
(`_DistFade` start = end), the FogSky reflection vector is safe-normalised, every divisor is clamped (HG denominator,
Jacobian determinant, footprints, `_DepthFalloff`, `_CylRadius`), and FinalDither's `saturate` maps any NaN reaching the
last pass to 0.

## 2. Globals (set from C#, LINEAR colours via SetGlobalVector)
Fog/sky: `_AbFogSigma, _AbFogDownMul, _AbFogDown, _AbFogHoriz, _AbFogUp, _AbScatterAmt, _AbScatterColor, _AbSunDir`
(toward the key light), `_AbExtSunDir` (toward the beat-7 sun), `_AbSkyBlend` (already eased). Ambient: `_AbAmbUp,
_AbAmbMid, _AbAmbDown` (domain 0 only, see §3). Camera/time: `_AbPixelAngle` (= 2 tan(vfov/2) / height px; 0.0012 fallback while unset),
`_AbTime` (x = SceneClock mod 3600). Beat: `_AbLampIgnite` (start mod 3600 or -1, origin x, origin z, m/s), `_AbBeamFade`,
`_AbFanTheta`. City: `_AbDownView` (R1-01, x = weight 0..1, y = deck height m, z = darkening depth scale m; an unset
global is 0 = off; owner lift / ProgressDirector, see section 6). Final pass: `_AbDitherParams` (amplitude LSB, temporal 0/1, frame mod 64, enabled 0/1), `_AbPictureFade`
(id `FadeController.AbPictureFade`; an unset global is 0 = black, so MaterialBuilder sets 1 in edit mode only). Textures:
`_AbNoiseTex`, `_AbStreakTex` (`ProceduralTextures.NoiseTexId / StreakTexId`).

## 3. Fog domains (`_AbDomain`: 0 Shaft, 1 Tunnel, 2 Exterior)
`FogSky(v, domain)`: `c = lerp(FOG_DOWN, FOG_HORIZ, smoothstep(-0.55, 0, v.y)); c = lerp(c, FOG_UP, smoothstep(0, 0.8,
v.y)); c += SCATTER * lobe * scatterAmt` with `lobe = HG(dot(v, sunDir), 0.55)` (normalised HG). Exterior: `lobe =
min(HG * smoothstep(0.05, 0.35, v.y), 0.15)`, SCATTER = sun #D6E2EC, amount 0.6, so the horizon row is exactly
OUT_HORIZ. Fog factor `F = 1 - exp(-(sigma d_eff)^2)`, `d_eff = d (1 + m_down saturate(-v.y))`. Opaque:
`AbyssApplyFog(rgb, positionWS, domain)` = desaturate `lerp(rgb, luma, 0.5 F)` then `lerp(rgb, FogSky(v), F)`.
Additive (Glow, beams): `AbyssFogAdditive` = same desaturation, then `* (1 - F)`.
Domain 1: D50 90, m_down 0, all three colours #0A0D10. Domain 2: D50 7000, m_down 0.4, #0A1015 / OUT_HORIZ / OUT_ZENITH.
Domain 0: the globals. **Ambient follows the domain like the fog** (lead decision): `AbTrilight(n, domain)` uses the
`_AbAmb*` globals in domain 0 only; domain 1 is TUNNEL_FOG x 1.2 for all three terms, domain 2 is #0A1015 / OUT_HORIZ /
OUT_ZENITH x 1.2 as ground / equator / sky (`AB_AMB_DOMAIN_SCALE` = the "x1.2" of the beat-7 rows, checked against
`ProgressTables.Rows` by ShaderTests). Every lit shader (Surface, Grating, Facade, Lattice) passes its `_AbDomain`. The skybox draws `lerp(FogSky(v,0), FogSky(v,2), _AbSkyBlend)`. A material in another domain:
`ctx.Mats.Get(MatId.X, FogDomain.Exterior)` (sets `_AbDomain`). Tunnel materials 1; Face, balcony, pier, honeycomb 2.

**Horizon agreement by construction (14.7.7, 14.10).** Sky (`AbyssSkyColor` = Exterior `FogSky` once `_AbSkyBlend`
reaches 1), opaque fog (`lerp(rgb, FogSky(v), F)`), the Lattice (fog is its last colour operation, the lamps are added
before it), the Face/balcony (domain 2) and the bore card (`FogSky(v, 2)`) all evaluate the same function. At v.y = 0
`FogSky` is exactly OUT_HORIZ for any sun direction (both smoothsteps have zero slope there, the lobe weight is 0 below
v.y = 0.05); within +/-1.7 degrees it stays within 1 level of #8496A5, within +/-2.6 degrees within 2 levels; the 28 km
lattice rim has F > 0.9999 (no seam). Test `ExteriorHorizonAgreesByConstruction` mirrors this on the CPU.
g01 investigation of the KS7c `horizon_agreement` failure (5.7-7.5 levels): per column, the sky row just above the
horizon and the fogged lattice just below agree within 0.1 level at every x (shader side correct). The failure is in the
measurement: (1) the `horizon_sky` box [0.35-0.65] x [0.437-0.457] contains the goal door and its amber lamp (columns
0.477-0.523, about 12 % of the box: R 128 vs 112), and (2) the post vignette makes the centre box about 2 levels brighter
than the side boxes (`horizon_fog` [0.02-0.25], `horizon_backdrop` [0.75-0.98]); without the door columns the sky box is
(111.8, 127.3, 141.9) vs fog (109.8, 125.3, 139.7). KS7b passes only because nothing stands in its sky box. The
`-nopost` `horizon_row` box [0.30-0.70] x [0.467-0.477] of KS7c also contains the door. Fix in the capture harness: sample
the sky at the same x ranges as fog/backdrop (or exclude the projected subject bounds) and keep the boxes vignette-
symmetric.

## 4. Tier fades
`_AbFade` (every shader, colour AND depth passes): f >= 0 keeps the pixel where noise < f, f < 0 where noise > 1 + f;
noise = static IGN of the integer pixel quantised to (k + 0.5) / 256, so f = 1 and f = -1 keep everything, f = 0 nothing,
near = c / far = -(1 - c) are exact complements. Additive shaders (Glow, BeamWedge) use `|f|` as a brightness factor.
`_DistFade` = (start m, end m, mode, 0), per-pixel by horizontal camera distance: mode +1 = far tier shown beyond
(f = -s), mode -1 = near tier shown before (f = 1 - s), s = smoothstep(start, end, r). Lattice default (5400, 6600, +1);
give the honeycomb tier-1 tubes a HoneyTube variant with `_DistFade = (5400, 6600, -1, 0)` for the exact complement.

## 5. Pattern coordinates (precision ADR 4)
`PatternPos = mul((float3x3)UNITY_MATRIX_M, posOS) + (object origin mod 1332)` = world − 1332 k. Every period (0.2, 1.0,
1.2, 2.4, 3, 9, 12, 36, 37 m; noise tiles 0.75 / 1.5 / 3 m) divides 1332 and every hash input is an integer cell index
modulo its count (`AbWrap`), so chunk origins may differ by any multiple of 1332 without a seam (test 14.10 a).
Wall projection: u = dot(P.xz, t), t = horizontal tangent from the normal, v = P.y; floors/soffits use (x, z); blend
over |N.y| 0.6-0.8. For axis-aligned walls this is exact under the wrap. **Non-axis-aligned planar faces (the shaft's
30/150/210/330-degree faces): keep the xz of all chunk origins of one face identical (chunk only vertically), otherwise
u shifts by 1332 (k·t), which is not a multiple of the periods.** Derivatives come from the rotated local position
(small numbers), never from world coordinates.

## 6. Surface (`Megastructure/Surface`) recipes and properties
Properties: `_Recipe, _BaseColor, _Roughness, _Metallic, _AbDomain, _AbFade, _ThinLine, _EmissionColor,
_EmissionStrength, _MacroAmp (1 = +/-12 %), _WearAmount (vertex G), _RustAmount (exact coverage, <= 0.12), _WearWidth,
_PanelCount, _CylRadius, _CylJoints, _TopY, _DepthFalloff, _FarTint, _FarRange (start, end m: albedo -> _FarTint, e.g.
bridge edge lines fading beyond 60 m: (40, 60)), _DistFade, _KeyLightMask (main light only, default 1), _BevelWidth
(nominal chamfer width m, default 0.01, R1-26), _DownViewAtten (opt-in to `_AbDownView`, default 0, R1-01), _Cull,
_AlphaToMask`.

**Sub-pixel bevels (R1-26, `AbBevelResolve` in AbyssCommon.hlsl).** MeshKit.ChamferBox gives its chamfer and corner facets
edge wear G = 255. A chamfer narrower than about 1.5 px is caught by MSAA as dashes and its specular / bare-steel highlight
crawls (KS5b deck joints, B3 rib bevels). Projected width `px = _BevelWidth * sqrt2 * max(|N.V|, 0.25) / (d *
_AbPixelAngle)`, `k = smoothstep(1, 2.5, px)` (1 cm: resolved at 2.5 m, merged at 10 m). On G >= 0.97 facets whose
object-space normal has two components of about 0.707 (a 45-degree chamfer of an object-axis-aligned box, instanced or
not), the normal is bent onto the adjoining face that looks most toward the camera as k falls, and the steel roughness
rises to 0.7 (no highlight). Edge wear (vertex G) of every recipe is multiplied by k (a sub-pixel worn edge has no
visible contribution). Variants with a known bevel (4 mm tread plates) may set their own `_BevelWidth`.

**Downward-view attenuation (R1-01, `AbDownViewEmission`).** Opt-in per material (`_DownViewAtten`: Facade and
FacadeModule 1, all others 0; Surface and the Emissive lamp modes support it for city variants) times the global weight
`_AbDownView.x`. Emission x `lerp(1, 0.0025, smoothstep(sin 20 deg, 0.62, down))` with `down = sin(angle below the
horizon)`: full from the horizon to 20 degrees down, off (a 0.0025 far-tier glint at most) from 38 degrees down, so no
window within 50 degrees of the nadir exceeds about 0.012 linear (0.10-0.12 luma) while the far city toward the horizon
keeps its lights. Albedo x `lerp(1, exp(-max(_AbDownView.y - y, 0) / _AbDownView.z), w)` (concrete darkens toward
FOG_DOWN with depth below the deck). The DRIVER is not a shader: the lift owner / ProgressDirector sets
`Shader.SetGlobalVector("_AbDownView", new Vector4(w, -3, 60, 0))` with w = 1 while the camera is on the bridge (d < 216,
eased out over d 216-240) and 0 from the ledge on, so the ride shows the whole city. Test
`DownViewAttenuationDarkensTheNadirOnlyWhenDriven`.

**RUST** (`AbRustMask` / `AbRustColor` / `AbStainColor` in AbyssCommon.hlsl, shared by the steel recipes and the
Grating; 14.2 "at most 12 % of a surface set"; g01: the lift deck showed saturated orange blotches; R1-27 / R1-33: rust
read as hue-less stamped blots repeating every 3 m, and walking decks carried identical reddish "paw prints"). Mask =
soft threshold of a uniform patch value with the band [t - a, t + a], t = 1 - a (the widest symmetric band inside [0, 1]:
soft gradients, no decal edges); the area with mask > 0.5 is exactly `_RustAmount` (a) and E[mask] = a; a second channel
only modulates the density inside a patch (x 0.55-1.0); patches blend to the mean 0.775 a when the footprint grows from 5
to 40 cm/px. The patch value is `AbUniformMax2(g(P / 14.8 m), g(P / 9.25 m))` = max(a, b)^2 of two equalised NoiseTex
lookups (uniform again, common period 74 m: no visible repetition; patches 0.3-3.7 m, the 7.4 / 4.5 m pair of the first
R1 build left 0.2 m "leopard" spots on the crank wheel; the Grating keeps 7.4 / 4.5 m), biased by `AB_RUST_EDGE` 0.08 x vertex G so rust
gathers on worn edges (a 0.04 surface rusts about 12 % along its edges, about 4 % on faces), plus a 30 % halo over the
values [1 - 4a, 1 - a] around each patch. Colour on structure: RUST #5B3A27 at `AB_RUST_CHROMA` 0.62 of its chroma at
`AB_RUST_LUMA` 0.95 x the paint luma, +/-15 % variation (HSV saturation about 0.40 on STEEL_DARK, raw RUST 0.57), metallic
0, roughness 0.88. On walking surfaces (N.y > 0.7, and the whole Grating) the same mask is a dark-neutral stain
`AbStainColor` = paint x 0.66-0.82 (no hue, never red), matte (roughness >= 0.7: a glossy stain under the low beat-6
Key read as white puddles on the gantry in the first R1 build). Base values: SteelPainted 0.01, SteelWeathered 0.04, Grating 0.03
(`MaterialBuilder.Rust*`); test `RustMaskIsCoverageExactAndSubtle` mirrors the mask on the real NoiseTex. Vertex colour R = AO (ambient + environment only),
G = edge wear. Roughness is widened by the screen-space variance of the geometric normal (specular AA), GGX term clamped
at 40, global floor 0.12.

| `_Recipe` | Id | Recipe |
|---|---|---|
| 0 | ConcreteBoard | boards 0.20 m x 2.4 m, butt joints staggered by hash per row; 3 mm grooves (normal + 35 % darkening of the filtered coverage); per-board tone +/-5.5 % and roughness +/-0.05 by integer board index, a wood-grain imprint (StreakTex.g along the board, +/-2.5 %, offset per board), all board-scale tone fading between 2.5 and 5 px rows (R1-08/09: at 1.5-3 px the random row tone read as "brushed metal" hatching); tie holes 25 mm on 1.2 x 1.0 m (cones, centred in a board row); cold joint every 3.0 m (8 mm band, 4 mm step: the lift above stands proud) inside a 25 mm rustication recess (-42 %) with a darker 12 mm lower lip (-28 %), a 6 cm lighter band (+7 %) over it (the 4 mm proud bottom edge of the lift above
catches the light, so every joint reads as a light / dark pair) and a weathering band under each joint (-20 % x exp(-t / 9 cm), exact box filter `AbExpStainBox`, drawn at least 0.6 px deep so it survives as a soft line out to 500 m); expansion joint 25 mm every 12 m with a 0.5 m rain streak along it (-11 %, strongest under each cold joint, hashed per joint and lift; `AbLineMinWidth` >= 1.5 px with contrast x sqrt(true / drawn): the Face reads as graph paper at 70-500 m); per-lift tone +/-3.5 % plus per-pour (12 m bay x 3 m lift) tone +/-5.5 % (box-filtered across lift and bay boundaries, fading only under 1.5-3 px lifts); macro value noise 9 m + 37 m (+/-12 %); StreakTex streaks (12 m x 36 m, +8 % darker / -3 % lighter, +0.10 roughness in the runs, concentrated in the 1.6 m below each cold joint); mottling (NoiseTex, 1.5 m tile); edge wear faded by its projected width; roughness 0.88 |
| 1 | ConcreteFloor | (R1-28) trowelled 6 m slabs: 6 mm saw cuts every 6 m and 18 mm sealed expansion joints every 12 m (filtered grooves + darkening) with a 6 cm dirt band held at >= 1.5 px (`AbLineMinWidth`, up to -16 %: the 6 m slab rhythm survives at grazing angles),
per-slab tone +/-4 % and roughness +/-0.03; aggregate micro-variation +/-3 % (NoiseTex.a at 0.75 m + .b at 0.9 m, the mips fade it); mottling +/-5 % (3.7 m tile); macro; damp patches (NoiseTex.g at 7.4 m, soft threshold 0.66-0.92, biased toward vertex-G edges) up to -10 % and +0.05
roughness; kerb edge wear from vertex G (+18 %, chipped by noise, faded by its projected width); worn centre path from **UV0.x = signed lateral offset from the walking centreline (m)**: Gaussian of half-width `_WearWidth` (0 = off), a lighter (+6 %), smoother (-0.14) band with exposed aggregate |
| 2 | ConcreteCyl | **UV0.x = arc length (m)**, v = pattern height; full-height panel joints every 2πR/N (`_PanelCount` N, `_CylRadius` R; C1: R 22, N 115), boards and lifts as recipe 0, `_CylJoints` expansion joints, streaks with an integer number of repeats per turn (no seam at u = 0); the u tangent of the normal detail is oriented by the screen derivatives of UV0.x (`sign(Σ dot(dP, Tu) du)`), so joints and tie holes read as grooves/cones on outward columns (where AbWallTangent = −dP/du) and inward bores alike |
| 3 | SteelPainted | STEEL_DARK, roughness 0.55 (floor 0.25), paint +/-2 % on a 3 m tile (no 0.75 m "granite" speckle, R1-27), edge wear from G → bare steel (metallic 0.85, rough 0.38) faded by its projected width (R1-26), rust by `AbRustMask` on the two-lookup patch value (exactly `_RustAmount` 0.01 of the surface, ≤ 12 %, edge-biased, RUST hue on structure, dark-neutral stains on walking surfaces) |
| 4 | SteelWeathered | as 3 with grime (vertical runs: value noise on a 1.5 x 6 m lattice, -10 %; broad 4.5 m patches, -8 %; mean 1, replacing the 3 m "camouflage" mottling; 60 % of this grime also on every walking surface of recipe 3, with deck scuffs: StreakTex.g along both plan axes at 1.2 / 2.4 m, +/-4 %, and anti-slip grit +/-2 %, R1-28), rust 0.04, wear 1.5, roughness 0.62 |
| 5 | RodPolished | #8A9096, metallic 1, roughness ≥ 0.30, specular AA |
| 6 | Cable | #15181C, roughness 0.7, `_ThinLine` 1.5 |
| 7 | DoorLeaf | #3B4148, roughness 0.5, light edge wear |
| 8 | HoneyTube | recipe 0 × exp(−max(0, `_TopY` − y) / `_DepthFalloff`) (−1087, 140), domain 2 |

Per-column C2-C4: `ctx.Mats.Variant(MatId.ConcreteCyl, "C2", m => { m.SetFloat("_CylRadius", 36); m.SetFloat("_PanelCount", Mathf.Round(2*Mathf.PI*36/1.2f)); })`.

### ThinLine (14.7.9) mesh contract
Set `_ThinLine` (px, e.g. 1.5) on a Surface variant. **UV1 = (member axis direction xyz in object space, member radius
m)** (MeshBuilder `WriteUV1` / `CurrentUV1`). Optional **UV2 = (a point on the axis in object space, 1)**: makes the
expansion crack-free for split-normal sections (chamfered boxes, I-sections); without UV2 the push is the normal minus its
axial component (crack-free only for smooth-normal lathes). Every pass expands identically (shadow and depth match);
under MSAA the colour pass exports coverage r / (r + e) via alpha-to-coverage (`_AlphaToMask` 1, automatic only when
URP's `_AlphaToMaskAvailable`), without MSAA the widened member stays opaque.

## 7. Grating
Pitch `_Pitch` 0.04, bar `_Bar` 0.008, depth `_BarDepth` 0.03 (closes up at grazing angles), plane from the dominant
normal axis (x/z for floors) on PatternPos. R1-27 ("the catwalk renders as a solid slab"): each bar family is
box-filtered over ITS OWN footprint and blends to its own mean between 8 and 4 px of pitch (the old rule faded both at the
larger footprint, so 3.5 m ahead of the eye nothing was left); walking along a catwalk the bars running with the view stay
resolved about 6-8 m ahead while the cross bars close up. Visible bar extent = top + the side face exposed by the depth
(`bar + depth tan`), tops lit paint, exposed sides x `_SideShade` 0.42 (grazing views read as bright bar tops over dark
slots). Far mean per family `lerp(m0, 1, saturate(depth tan / open))`, m0 = 1 - sqrt(1 - `_FarOpacity`) (25 % for both
families at normal incidence). Panels `_PanelSize` 1 m: a 30 mm solid banding bar at every panel edge with a 6 mm dark
seam and +/-4 % per-panel tone (the mid-scale rhythm that survives to 15-20 m). MSAA: AlphaToMask; no MSAA: static
dithered clip. Depth-type passes clip by the same coverage (dithered). Cull Off. Real 0.6 m support bars are geometry.
Painted steel with dark-neutral stains `AbRustMask(_RustAmount (0.03), AbUniformMax2(na.g, nb.g), nb.a)` /
`AbStainColor` on the 7.4 m and 4.5 m xz noise tiles (no 3 m repetition) at 60 % strength, metallic x 0.6 and roughness
+0.2 inside a stain (glossy stains read as white puddles under the beat-6 Key, fully matte ones as black decals);
`_KeyLightMask` as Surface.

## 8. Facade — window identity contract (beat 5 builder: read this)
Both tiers compute the window from the **world position of the fragment**; no per-mesh ids, so the instanced module mesh
needs no per-instance data and the lit windows of both tiers coincide exactly (test 14.10 b).
* Face: `k = round((atan2(z − 410, x − 0) − 30°) / 60°) mod 6` (shaft centre `_GridOrigin.xy` = (0, 410)); face angle
  `a = 30° + 60° k` (from +X toward +Z; the city faces are a = 150, 210, 270).
* `u = dot((x, z) − (0, 410), (−sin a, cos a))` = signed distance along the face from its centre; `v = y − (−60)`.
* Cell `col = floor(u / 3.0)`, `row = floor(v / 3.5)` (rows −1 … −144 for y −60 … −564).
* Window rectangle in the cell: `u ∈ [3 col + 0.9, 3 col + 2.1]`, `v ∈ [3.5 row + 0.95, 3.5 row + 2.55]` (1.2 x 1.6,
  centred). Modules (24 x 21 m = 8 x 6 cells) start at u = −204 + 24 i, so their windows land on this grid exactly.
* State: `pcg3d(face, col + 32768, row + 32768)`: lit if U(h.x) < 0.06, amber if U(h.y) < 0.15, strength
  2.5 + 2.5 U(h.z), tint ±12 % from U(h.x ^ h.z). Static in time.
* **FacadeModule meshes: vertex colour B = 255 on glass faces, B = 0 on concrete (frame, reveals).** Glass gets the
  window state of the cell it lies in; reveals use real geometry. R = AO as usual.
* Shader tier (`_Recipe` 0): one quad per wall chunk on the face plane; box-filtered window mask
  `saturate((halfOpen − |frac − 0.5| cell) / fp + 0.5)` per axis, lintel darkening 0.35 at the window top → 0 at the
  bottom (also applied to module glass so the tiers match), mean blend between 7 and 4 px cells.
* Near/far switch 130-170 m: DetailTier with `_AbFade` (dithered, never alpha).
* `_DownViewAtten` (1 on both base materials) x the global `_AbDownView.x` (unset = off): downward-view attenuation of
  the window emission and darkening of the concrete with depth below the deck (R1-01, section 6).
* Use the Facade shader only on surfaces parallel to a shaft face (the grid is the face's own u, v): side faces of the
  protruding blocks would show column stripes; give them ConcreteBoard (or a separate window layout).
* Points past a face corner (the 1.9 m module overhang inside the neighbouring wall solid) resolve to the neighbouring
  face; they are hidden by design.

## 9. Emissive modes (`_Recipe`)
0 LampAmber (`_EmissionColor` AMBER x `_EmissionStrength` 8), 1 LampCold (COLD_LAMP x 4), 2 ProxyCard (flat `_BaseColor`,
fogged), 3 GradientCard: `_CardMode` 0 = `lerp(_CardBottom, _CardTop, UV0.y)` (**UV0.y 0 at the bottom, 1 at the top**),
`_CardMode` 1 = Exterior sky `FogSky(view dir, 2)` (the bore card: a window onto the outside), both x `_EmissionStrength`,
fogged by the material domain when `_CardFog` = 1. Lamp modes 0/1 support `_DownViewAtten` (default 0) for city lamp
variants (R1-01). Cards are single-sided (`_Cull` 2): author the bore card facing +Z,
the dark card facing −Z. Bore card strength: `_EmissionStrength` 0.35 → 1.0 (14.6), via a variant / property block.

## 10. Glow (light points)
Mesh: `ShaderMeshes.GlowQuad()` (vertices ±0.5, bounds inflated). Object/instance matrix: translation = light centre,
uniform scale = true diameter (m). Drawn diameter = max(true, `_MinPixels` (≥ 2.5) px at that distance), soft
Gaussian disc on a quad of twice that size, pulled toward the camera by half a diameter, soft depth fade, additive,
fogged additively from the centre. `_EmissionColor` x `_EmissionStrength`; `_EnergyConserve` 0..1 dims enlarged points by
(true/drawn)^2. `_Ignite` 1: per-instance ignite from `_AbLampIgnite` at the instance position (smoothstep over 1 s
after `distance / speed`). One instanced batch per colour (`ctx.Mats.Variant(MatId.Glow, "cold", ...)`).

## 11. BeamWedge (14.7.4)
Mode 0 (fan): mesh = `ShaderMeshes.BeamWedgeMesh(12, phase0, 130, 300)`; **UV1 = (wedge centre angle φ_k in the object
XY plane from +X toward +Y, corner 0..3, cap 0/1, 0)**, positions ignored (the vertex shader builds the prism).
Object frame = fan frame: origin at the fan centre (−45, −317, 50), +Z = fan axis, rotation about +Z = the fan rotation,
**scale 1**. φ_k must be the blade-gap centres. Extrusion along the WORLD light direction `_BeamDir` (default
Dir(0, −18) = (0, −0.309, 0.951)), 300 m; R 70-130, half width 6°. 6-plane analytic interval clamped to [near,
scene depth], density with smootherstep feathers max(2 m, 12 px) (sides), 8 m (chords), axial window, fog
transmittance, 12 Gauss-Legendre samples, no jitter. Winding-independent (Cull Off, far layer kept), works from inside.
Intensity `_EmissionColor x _EmissionStrength (per metre) x ∫ x 4π HG(g = _PhaseG) x _AbBeamFade x |_AbFade|`.
Mode 1 (far-wall patches): variant with `_Recipe` 1, `_Cull` 2, `_ZTest` 4 (LEqual) on a quad ≥ 0.5 m in front of the wall
z = 760; wedges at `_BeamPatch.z + k 2π/12 + _BeamPatch.w * _AbFanTheta` around `_BeamCentre`, feather 6 m, stretch 1.05.
Beat06 must keep `_BeamPatch.z / .w` consistent with its transform rotation (θ = 2πt/40, clockwise seen from +Z = angle
increasing from +X toward +Y). Mode 2 (doorway spill): rectangle `_BeamRect.xy` half sizes in the object XY plane
(base material: the exit door, 0.6 x 1.0 m), length `_BeamParams.w`, extruded along `_BeamDir` (object +Z must point
into the spill: the object-space light direction keeps z ≥ 0.05), mesh from `BeamWedgeMesh(1, 0, ...)`. In mode 2 the
side feathers are `min(max(2 m, 12 px), 0.5 _BeamRect.x)` and `min(max(2 m, 12 px), 0.5 _BeamRect.y)` (no 8 m chord
feather), so the middle half of the rectangle has full density at every distance; for all modes the axial fade-in is
`min(_BeamFeather.w, 0.25 length)` (unchanged for the 300 m fan). No `_BeamFeather` override is needed. Example variant:
`ctx.Mats.Variant(MatId.BeamWedge, "door", m => { m.SetFloat("_Recipe", 2); m.SetVector("_BeamParams", new Vector4(0, 0, 0, 14)); m.SetVector("_BeamDir", dirWorld); m.SetColor("_EmissionColor", cold); m.SetFloat("_EmissionStrength", k); })`.
`_UseDepth` 0 disables the scene-depth clamp.

## 12. Lattice (14.7.7)
Disc mesh at y = −1087.6 (radius 28 km, moved on a 100 m grid, rotation locked); the shader uses absolute world xz.
`_LatticeOrigin` (0, 410), `_LatticeParams` (size 760/√3 = 438.80, hole apothem 350, rim 6, lamp offset 269). Flat-top
axial + cube rounding; hex distance over normals 30/90/150°. Holes: analytic ray-hexagon exit gives the visible far-wall
depth; wall lit by the main light + trilight, albedo x exp(−depth/140), blended to FOG_DOWN (domain 2). Webs CONC_MID lit,
rim x 1.35. Lamps: soft dots ≥ `_MinPixels` in true screen pixels (inverse Jacobian), cells k and k ± b1, ignite from
`_AbLampIgnite`. Footprint rules: rim fades under 1.5 px, webs held at 1.5 px with contrast scaled (mean kept), whole
cell → cell average under 3-6 px. Domain 2, `_DistFade` (5400, 6600, +1). `_KeyLightMask` scales `sun.color` (holes
and webs). Fog (`AbyssApplyFog`, domain 2) is the last colour operation, so the field meets the sky at the horizon by
construction (section 3).

## 13. Sky and FinalDither
`M_Sky` → `RenderSettings.skybox`. `M_FinalDither` → a `FullScreenPassRendererFeature` (AfterRenderingPostProcessing,
fetchColorBuffer on, pass 0). Requirements for exactly one dither: camera `dithering` OFF, FXAA off, 64-bit HDR
(R16G16B16A16) colour so nothing quantises before the final write. The lead's RenderSetup / SceneBuilder apply exactly
this: MSAA 4x (so ThinLine and Grating use alpha-to-coverage), 64-bit HDR, camera dithering off, no FXAA, the
FullScreenPassRendererFeature "AbyssFinalDither" (AfterRenderingPostProcessing, fetchColorBuffer, pass 0) with
`MaterialLibrary.finalDither`, depth texture on (BeamWedge, Glow soft depth), `RenderSettings.skybox =
MaterialLibrary.skybox`, Unity fog off; GameBootstrap / FadeController own `_AbPictureFade`. Formula: sRGB(c) +
TPDF(frac(IGN(p) + `AB_DITHER_ROW_SHIFT` p.y)) x amp/255, p = pixel (+ 5.588238 x frame when temporal), x
`_AbPictureFade`, back to linear. `AB_DITHER_ROW_SHIFT` = -0.1192692 moves IGN's vertical step from 0.3092692 to 0.19.
Reason (g01 KS7d `sky_haze_top40` 13 / 14 px at 16:10 / 21:9): IGN nearly repeats every 13 rows (13 x 0.3093 = 4.02);
on slow vertical gradients (about 1 level per 20-30 px, here the fogged fan housing and Face, not the sky) that leaves
runs of 13-15 identical 8-bit values although the dither is present. With step 0.19 any 6 consecutive rows hit every arc
of 0.25, so the worst run of the dither on any gradient is 6 px (CPU mirror: `FinalDitherBreaksVerticalRuns`, plain IGN
15). Amplitude, TPDF shape, temporal offset and 2x2 / 4x4 noise energy are unchanged or better; re-run the encode test
(11.12) once with the new pattern. The same resonance is the likely cause of the KS7a `mouth_glow` 12 / 11.7 px.

## 14. C# API
* `ProceduralTextures.Generate(MaterialLibrary lib)` — builds NoiseTex (256², RGBA32 linear, mips, aniso 16, repeat; RGBA =
  four histogram-equalised fBm channels) and StreakTex (512 x 128, R = streaks mean 0.5, G = fine grain), binds
  `_AbNoiseTex` / `_AbStreakTex`, sets `lib.noiseTex / streakTex`. Cached per domain reload; `Release()` frees them.
  `BuildNoisePixels()` / `BuildStreakPixels()` are deterministic (tests).
* `ShaderMeshes.GlowQuad()`, `ShaderMeshes.BeamWedgeMesh(count, phase0Rad, maxRadius, length)`.
* `Megastructure.EditorTools.MaterialBuilder.BuildAll()` — creates/updates `Assets/_Game/Materials/M_<MatId>.mat`,
  `M_Sky.mat`, `M_FinalDither.mat`, `Assets/_Game/Settings/MaterialLibrary.asset` in place (GUIDs stable).
  Instancing is enabled on ConcreteBoard, SteelPainted, SteelWeathered, Cable, HoneyTube, Grating, FacadeModule,
  LampAmber, LampCold, Glow. `MaterialBuilder.Shaders` lists every shader; `FindShader(name)` loads by path.
  Idempotent: every value of a base material (including `_KeyLightMask` = `KeyLightMaskDefault` 1, the rust amounts
  `RustSteelPainted` 0.01, `RustSteelWeathered` 0.04, `RustGrating` 0.03, cap `RustMax` 0.12, `_BevelWidth` =
  `BevelWidthDefault` 0.01 on every Surface material, `_DownViewAtten` = `DownViewFacade` 1 on Facade / FacadeModule and 0
  elsewhere, and the grating's `GratingPanelSize` 1 / `GratingSideShade` 0.42) is written on every run; assets are loaded
  and updated in place, never recreated.

## 15. Verification done by the builder
Every pass of every shader was compiled offline (no Unity) with DXC (SM 6.0, HLSL 2018) and with FXC (SM 5.0, the
D3D11 compiler, on the DXC-preprocessed source) against the URP 17.6 / Core package includes, for the keyword set none,
each keyword alone, and main-light cascades + soft shadows + additional lights + cluster loop + instancing: zero errors,
zero warnings. EditMode `ShaderTests` re-check compile state, passes, the library, constants and textures inside Unity.
g01 round (key mask, rust, dither row shift, NaN guards): the same offline check over all 10 shaders, 190 stage variants
(DXC vs/ps_6_0 HLSL 2018, FXC vs/ps_5_0 `/Gec` on the DXC-preprocessed source; instanced variants map the
`UNITY_BUILTINS_WITH_WORLDTOOBJECTARRAY##Array` paste that clang leaves unexpanded): 0 errors, 0 warnings. New EditMode
tests: `EveryShaderHasOneMaterialCBufferCoveringItsProperties`, `EveryPassAppliesFadeAndFog`,
`LitShadersMaskOnlyTheKeyLight`, `RustMaskIsCoverageExactAndSubtle`, `FinalDitherBreaksVerticalRuns`,
`ExteriorHorizonAgreesByConstruction` (not yet run inside Unity).
R1 round (concrete legibility R1-08/09, floors R1-28, steel / grating R1-27/33, bevels R1-26, down-view term R1-01): the
same DXC + FXC offline check over Surface, Grating, Facade and Emissive (156 stage variants): 0 errors, 0 warnings. New /
updated EditMode tests: `BoardFormedConcreteReadsAtWalkingAndFaceDistances` (stain box filter vs numeric integration,
1.5 px joint lines at 300 m), `GratingFamiliesFadeWithTheirOwnFootprint`, `DownViewAttenuationDarkensTheNadirOnlyWhenDriven`,
`SubPixelBevelsMergeIntoTheirFace` (on a real ChamferBox), `RustMaskIsCoverageExactAndSubtle` (wide band, two-lookup
coverage, RUST hue 0.30-0.45), `PatternPeriodsDivideTheNoiseWrap` (new tiles 7.4, 4.5, 3.7, 0.9, 0.6, 7.2, 6).
R2 round (shaders owner; rows R2-02, R2-10, R2-14, R2-17, R2-21, R2-25, R2-32, support R2-04 / R2-06 / R2-19):
- Board concrete (R2-02): per-board tone +/-7.5 %, per-lift +/-5.5 %, per-pour +/-3.5 %, per pour campaign (12 m x 4 lifts)
  +/-4.5 %, 3 mm board joints held at 1.5 px while rows are over 12 px apart, a tapered grout-bleed band (14 mm, -24 %)
  along every row joint (fades to its mean with the held line), cold joints = dark recess + skylit lip + weathering band
  (-30 % near, -18 % far) with joint darkness +/-40 % per (bay, lift). `_MacroAmp` 2 on the ConcreteBoard / ConcreteCyl /
  HoneyTube bases (MaterialBuilder.MacroConcrete).
- Steel (R2-10 / R2-14): structure rust from two domain-warped, 3:1 vertically stretched lookups (no round discs); walking
  surfaces use stochastic per-6 m-cell stains (`AbBombG`: random offset / rotation / mirror per cell, Gaussianised
  histogram-preserving blend, coverage exact) that are 8-14 % darker and keep the paint's gloss and most of its metallic
  (a matte stain under the low beat-6 Key read 50 % darker); deck plates 1.2 x 2.4 m (+/-8 %), 8 mm weld seams held at
  1.5 px with a grime band. Grating: per-family fade 6 -> 3 px of pitch (KS4 catwalk bars read within 6 m).
- Specular AA (R2-17): `AbGeometricSpecularAA` (roughness widened by the screen variance of the geometric normal) in
  Surface, Grating and Facade; `AbThinLineShading` bends widened sub-pixel ThinLine members toward the viewer and makes
  them matte (coverage < 1).
- Amber emitters (R2-21, lead D4): `AbHueSafe` / `AbWarmEmission` pre-compensate URP's Neutral tonemapper per channel
  (exact inverse), so a warm emitter shows its own hue at a display peak of at most 0.92 (`AbWarmth`: 1 for AMBER, 0 for
  cold and neutral colours). Enlarged warm Glow points (strength > 1, not energy-conserved) and resolved Lattice dots are
  small amber discs over the fogged background (coverage saturate(gain x profile), the lamp dimmed by half of the fog);
  they hand over to the fogged additive dot as the fog transmittance falls from 0.4 to 0.1 (8-12 km), so the horizon
  backdrop stays haze. Glow is premultiplied (`Blend One OneMinusSrcAlpha`, alpha 0 = additive). Emissive lamp heads
  keep the white-hot value only where N.V > 0.9-0.985.
- Glow points (R2-25): drawn under 6 px they are integrated over each pixel (erf), energy within 3 % at any sub-pixel
  offset. Facade windows narrower than 1.5 px on screen fade out.
- Honeycomb (R2-04 / R2-19 support): `AbyssHoneycomb.hlsl` shares the hex cell (size 760 / sqrt 3), a per-cell +/-6 %
  tone and the air (`_HoneyAir`: aerial veil toward OUT_HORIZ and tube air with depth) between the tier-0/1 tubes and the
  tier-2 Lattice; the pier, balcony and Face have no `_HoneyAir`. Rim chamfers (vertex G) are grimed, not lightened.
