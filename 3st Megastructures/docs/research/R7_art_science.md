# R7 - Art direction and physical references

Researcher: R7 (read-only). Date: 2026-10-29. Target: Unity 6000.6.0f1, URP 17.6.0 (`Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`, `package.json` says `"version": "17.6.0"`), SRP Core `@2d66c71e606e`.
Scope: MASTER_PROMPT 14.9 items 1, 2, 3, 6, 7 and 8. Numbers marked **[computed]** come from Python 3.12 scripts run in the R7 scratchpad (formulas are given, so anyone can re-run them). Numbers marked **[web]** come from the Sources list (section 8).

---

## 0. Decisions in brief

1. **Every horizon-dip value in the spec is correct** (14.5.7 text, 14.7.5 table, KF6a, KF6a2). Each one is within 0.012 degrees of `acos(R/(R+A))` with R = 6371 km, which is inside the T-CURV tolerance of 0.05 degrees.
2. **`eps0 = 25 km / sqrt(A(2R+A))` is correct: 0.506 degrees at 600 km.** `sqrt(A(2R+A))` is the tangent (horizon) distance, so eps0 is the angle a 25 km layer subtends at the limb. The curvature-exact value for a 25 km layer is 0.511 degrees. eps0 grows to 31 degrees at s = 500, so the glow must be gated at low altitude (section 2.4).
3. **The cloud shell has a horizon-cut defect at s of about 600 to 640.** The hit test uses the R-based limb, but the shell has radius R + 3 km. At KF6a the sea is cut at a dip of 2.77 degrees, while its own limb is at 2.14 degrees. At that cut, about 24 percent of the surface contrast is still unhazed. Fix: fade to full haze before the cut (section 2.3).
4. **Ray/sphere math:** use the RTG ch. 7 "c/q" form in the angular (mu) parametrisation, with `c/rho` and `mu_h = cos psi_limb` precomputed in double. Take the central angle from `atan2` (not `asin`), and take `sin psi` from `length(d.xz)`, not from `sqrt(1 - mu^2)`. The SRP Core `IntersectRaySphere` is the naive quadratic, so do not use it here (section 5).
5. **Cloud octaves need a variance-preserving combination.** A plain weighted sum of three F1 octaves almost removes the shade tone (0.0 percent of pixels below 0.18). Normalising by the active-weight RMS keeps the tone fractions at about 32 / 46 / 22 percent, independent of how many octaves have faded (section 4.4).
6. **Hash:** use `pcg3d` (JCGT 2020, verified from the paper text) on integer cell ids. Hoskins' "hash without sine" is fine for small coordinates. Compute in cell space (IQ's precision trick), with the integer camera-cell offset split on the CPU in double.
7. **Shading:** the URP calls are verified (`GetMainLight(shadowCoord)`, `TransformWorldToShadowCoord`, the keywords below). Use `centroid` normals under MSAA so the rim does not spike on silhouettes. `rim * (1 - fog)` followed by fog gives a (1 - f)^2 fade; that is intended, so document it. Non-colour `float3` literals need `// pal:ok` or a named `static const`, because T-PAL scans every `float3`/`float4` literal.

---

## 1. Space elevator: facts for the README (and believable detail)

### 1.1 Facts (each is safe to state in the README)

| # | Fact | Source |
|---|---|---|
| 1 | The geostationary radius is 42,164 km from Earth's centre, which is **35,786 km above the equator**. Derived from GM = 398,600.4418 km^3/s^2 and the sidereal day of 86,164.09 s. | **[computed]**, matches [S1] |
| 2 | History: Tsiolkovsky's 1895 "sky ladder" (a compression tower), Artsutanov's 1960 "Cosmic Railway" (a tether from orbit), and Pearson's 1975 "Orbital Tower", which proposed extending the counterweight to **144,000 km**. | [S1] |
| 3 | Bradley Edwards' NIAC studies (2000 to 2003) proposed a **carbon-nanotube composite ribbon about 1 m wide and thinner than paper, about 100,000 km long**, with a counterweight at the far end, anchored to a **mobile ocean platform in the equatorial Pacific**. | [S1] [S2] [S4] |
| 4 | Climbers grip the ribbon with rollers or treads and are powered by **ground-based lasers** (megawatt class, about 10 m adaptive mirrors). Edwards-era numbers give speeds **up to about 190 to 200 km/h**. At 200 km/h the trip to GEO takes **179 h, about 7.5 days**. Obayashi (2012) quotes 8 days. | [S1] [S3] [S2], **[computed]** |
| 5 | Edwards' construction plan: a **19,800 kg seed ribbon** is launched on rockets, then the first **207 climbers** thicken it into a 750-ton ribbon that can lift **20-ton climbers**. | [S1] |
| 6 | **Taper ratio** (cross-section at GEO divided by cross-section at the ground) for a constant-stress tether is `exp(dPhi / s)`. Here dPhi = 4.84e7 J/kg is the rotating-frame potential difference between the ground and GEO, and s is the specific strength in J/kg (1 MYuri = 1 MPa/(kg/m^3)). Results: steel about 1.6e33, Kevlar about 2.5e8, single-wall CNT at 100 MYuri **about 1.6**. The formula **[computed]** reproduces the [S1] table (2.5 MYuri gives 2.6e8, 100 MYuri gives 1.62). | [S1], **[computed]** |
| 7 | An untapered cable would have to hold **4,960 km** of its own weight at 1 g. A working Earth elevator needs about **30 to 80 MYuri**. Edwards assumed at least 100 GPa at 1,300 kg/m^3 (about 77 MYuri). | [S1] |
| 8 | ISEC (International Space Elevator Consortium) baseline: a tether **100,000 km long and 1 m wide**, as thin as plastic wrap. Candidate materials are **single-crystal graphene (favoured)**, hexagonal boron nitride and CNTs. The 2023 ISEC climber/tether study favours a **friction-drive climber with opposing wheels**. | [S5] [S6] |
| 9 | Release a payload at **23,400 km** and it enters an eccentric orbit. Release it above **53,100 km** and it reaches escape velocity. The tip of a 144,000 km cable moves at 10.93 km/s. | [S1] |
| 10 | Hazards: debris and meteoroids (Edwards: move the sea anchor to dodge tracked debris), atomic oxygen, the Van Allen belts, and pendulum and Coriolis oscillations caused by climber traffic. Recent work (Popescu and Sun 2018) argues for **higher stress ratios plus continuous autonomous repair**, modelled on tendons and bone. | [S1] [S7] |

The 2.6 taper for CNT at 130 GPa with a safety factor of 2 (Popescu and Sun [S7]) and defect-corrected estimates of 29 to 50 are useful if a viewer asks how fat the cable really is. The [S7] figure comes from a search summary, so cross-check it before quoting.

### 1.2 Believable details compatible with the fixed dimensions (do not change the spec)

- Real proposals are **ribbons, not cables**. Thread's 50 m radius, 36-strand cable is a deliberate fiction. Suggested README line: "Real proposals use a ribbon about a metre wide; Thread imagines the mature version, grown strand by strand by thousands of climbers." This is consistent with fact 5, where climbers thicken the seed ribbon.
- **Climber marks:** both Edwards (two treads, [S3]) and ISEC (opposing wheels, [S6]) imply **paired, parallel wear tracks** along the strand direction. If any surface detail is added to M05, two faint parallel lines on a strand crest read as "climber rails". This would be an optional extra pattern term, filtered like the lay lines.
- The **CORAL bands every 50 m** on the cable read naturally as collars or maintenance rings. The **beads** match the "Galactic Harbour" idea of stations along the route ([S6]).
- Altitudes for the README: ISS about 400 to 420 km; final view at 600 km (horizon 23.95 degrees below level, horizon distance 2,829 km **[computed]**).

---

## 2. What Earth looks like from 100 to 600 km

### 2.1 Horizon dip and limb glow scale **[computed]** (R = 6371 km)

`delta = acos(R/(R+A))`, `L = sqrt(A(2R+A))` (horizon distance), `eps0 = 25 km / L`. Pixels per degree at the image centre for a vertical FOV of 66 degrees at 1080p: `540/tan(33 deg) * pi/180 = 14.5 px/deg`.

| s | A | dip (spec) | dip (computed) | horizon distance | eps0 | eps0 at 1080p |
|---|---|---|---|---|---|---|
| 500 | 168 m | 0.42 | 0.416 | 46 km | 31.0 deg | (gate it) |
| 580 | 1.906 km | 1.40 | 1.401 | 156 km | 9.19 deg | (gate it) |
| 624 (KF6a) | 7.463 km | 2.77 | 2.772 | 308 km | 4.64 deg | - |
| 664 (KF6a2) | 25.18 km | 5.09 | 5.086 | 567 km | 2.53 deg | 37 px |
| 700 | 69.57 km | 8.43 | 8.429 | 944 km | 1.52 deg | 22 px |
| 750 | 226.1 km | 15.0 / 15.04 | 15.045 | 1,712 km | 0.836 deg | 12 px |
| (ISS) | 400 km | 19.8 | 19.793 | 2,293 km | 0.625 deg | 9 px |
| 800 (KF7a) | 486.5 km | 21.71 | 21.712 | 2,538 km | 0.565 deg | 8 px |
| 840 | 600 km | 23.95 | 23.946 | 2,829 km | **0.506 deg** | 7.3 px (15 px at 4K) |

Small-angle check: `sqrt(2A/R)` overestimates the dip by 0.9 degrees at 600 km (24.87 instead of 23.95). Always use `acos`, computed in double on the CPU.

**Angular thickness of an atmospheric layer of height H above the limb** (curvature-exact: `asin((R+H)/(R+A)) - asin(R/(R+A))`) **[computed]**:

| A | H = 8.5 km (scale height) | 25 km | 50 km (troposphere + stratosphere) | 100 km (airglow height) |
|---|---|---|---|---|
| 226 km | 0.29 deg | 0.86 | 1.78 | 3.83 |
| 400 km | 0.21 | 0.64 | 1.29 | 2.67 |
| 600 km | 0.17 | 0.51 | 1.03 | 2.11 |

The spec's "ATMO_RIM 0.8 degrees thick" at 600 km corresponds to a layer about 39 km tall. That is physically plausible: the bright "thin blue line" in ISS photos is the lower roughly 50 km. It is wide enough to read at 1080p (about 12 px) without looking fat.

### 2.2 Appearance facts (reference only; the palette stays as specified)

- **The thin blue line** is the atmosphere seen edge-on and back-lit, coloured by Rayleigh scattering. Near the terminator, the troposphere (0 to about 6 to 20 km) shows **orange and yellow bands** from aerosols and clouds, with pink-white stratosphere above and blue above that [S9]. The spec's lit-side ATMO_RIM glow is the right simplification. An optional palette-legal refinement is to lerp the innermost 0.1 degrees toward `lerp(ATMO_RIM, CREAM, 0.5)` for a pale core. Two-token blends are allowed by T-PAL.
- **Airglow** is a night-side phenomenon, too faint to see in daylight. The green 557.7 nm atomic-oxygen layer sits at **about 90 to 130 km, roughly 30 km thick**, and is visible only edge-on; the diffuse red 630 nm emission sits at **about 150 to 300 km** [S8] [S10] [S11]. At 600 km a 100 km layer would sit about 2.1 degrees above the limb. The palette has no green, so **do not add airglow**. If a night-limb accent is ever wanted, the only legal option is a 10 percent ATMO_RIM hairline 2.1 degrees above the dark limb.
- **Cloud fraction:** MODIS (King et al. 2013) gives about **67 percent globally, about 72 percent over ocean and about 55 percent over land**. Fewer than 10 percent of ocean skies are completely clear [S12] [S13]. The spec's high-altitude coverage `c = 0.45` shows more ocean than reality. That is a readable stylisation; keep it, but do not call it realistic in the README.
- Seen from orbit, the ocean is the darkest large tone, clouds are the brightest, and land is in between. EARTH_OCEAN (luma about 0.14) < EARTH_LAND (about 0.51) < CLOUD_TOP (about 0.94) matches that ordering.

### 2.3 Defect: cloud-shell horizon cut at s of about 600 to 640

The spec (14.7.7) tests `hit = cos(psi) > cos psi_limb` with `cos psi_limb = sqrt(1 - (R/(R+A))^2)` (R-based). The surface it shades is the shell `R_c = R + 3 km`. Rays with a dip between the shell limb and the R limb do hit the shell but are classified as misses. Results **[computed]**:

| s | A | shell limb dip | R-limb dip (cut) | slant distance to the shell at the cut | surface contrast left after `exp(-t/80 km)` |
|---|---|---|---|---|---|
| 624 | 7.46 km | 2.14 deg | 2.77 deg | 113 km | **24 percent** (visible step plus a 0.63 degree flat band) |
| 664 | 25.2 km | 4.78 | 5.09 | 372 km | 1 percent |
| 750 | 226 km | 14.94 | 15.04 | 1,517 km | 0 |

Keep the R-based test, because the acceptance values 2.77 and 5.09 depend on it. Then force full haze over the last 0.35 degrees before the cut:

```hlsl
// muH = cos(psi_limb) (R-based, CPU double), m = -d.y. Limb margin in radians (small-angle: delta mu ~ delta elevation).
float limbMargin = (m - _EarthMuH);                      // > 0 on the surface
float hazeLimb   = 1.0 - smoothstep(0.0, 0.0061, limbMargin); // 0.35 deg = 0.0061 rad
float haze       = max(1.0 - exp(-t / 80000.0), hazeLimb); // t in metres
```

### 2.4 Pitfall: an ungated limb glow paints the low-altitude sky

`eps0` is 9 to 31 degrees below 2 km. Multiply the glow by a gate such as `smoothstep(0.55, 0.9, D)` (D = 0.9 is at about s = 664) or by `1 - rho`, so the glow first appears as a band once the limb is a distinct edge. The spec does not state this gate; ask the owner of 14.7.7 to record it.

---

## 3. Journey and Manifold Garden: lessons and how Thread applies them

### 3.1 Verified facts

- **Journey** (thatgamecompany, art director Matt Nava). The design was "like a Japanese garden": everything that did not serve the intended emotion was removed. The structure follows Campbell's monomyth, and the goal is to make the player feel small and in awe. The score (Wintory) is one theme whose solo cello joins the orchestra as the player climbs, and at the end the orchestra falls away [S14]. Nava's GDC 2013 talk "The Art of Journey" covered the character, creature, landscape and architecture design [S15].
- **Journey sand shading** (John Edwards, GDC 2013 [S16], reconstructed by Zucconi [S17]):
  - Diffuse contrast: `NdotL = saturate(4 * dot(N * float3(1, 0.3, 1), L))`, then `lerp(shadowColor, terrainColor, NdotL)`. Lambert is deliberately over-driven so that about 25 percent lighting already reads as fully lit, giving a short, crisp terminator.
  - Fresnel rim: `pow(1 - N.V, power) * strength`.
  - "Ocean" specular: Blinn-Phong.
  - Rim and specular are combined with **max**, not a sum, so overlapping highlights do not blow out. The result is added to the diffuse and multiplied by albedo.
- **Manifold Garden** (William Chyr; rendering retrospective by Brussee, Saraev and Chyr, SIGGRAPH 2020 [S18] [S19]):
  - Outlines are "post-processed, anti-aliased" edges, and the problem became easy once it was treated as a **signed distance field**. The keywords "edge shading" and "toroidal geometry" and the references to Decaudin's cartoon rendering and Green 2007 (SDF alpha-tested magnification) point the same way.
  - Advice: do not rely on the standard MSAA resolve for such features.
  - The world is repeated in a grid out to the view distance, with GPU-placed instances. Shadow-map reuse was tried and dropped.
  - The clean, "sterile" look leaves "nowhere to hide", so every artefact is visible.
  - **Colour encodes structure:** each of the six gravity directions has one colour, applied to every aligned surface and object [S20].
  - No curved surfaces, because they do not fit the six gravity planes. The original inspiration was Tadao Ando [S21].
  - The sources do not confirm anything about Manifold Garden's fog, so make no claim about it.

### 3.2 Rules for Thread (original, not copied)

1. **Subtract, Japanese-garden style:** one key light, one rim colour, three tones per surface (shade, lit, rim). Each beat owns a single dominant sky family (day, then dusk indigo, then space). The emotional arc runs through the sky ramp `D` the way Journey's runs through its levels. This is already in the spec, so keep it strictly.
2. **Colour encodes structure:** cool TEAL on the left and warm CORAL on the right, CORAL bands as a 50 m scale ruler, and bead colours cycling MINT, PEACH, LILAC as a vertical rhythm. Never use a token outside its role.
3. **A short soft terminator:** the spec's `smoothstep(-0.05, 0.35, nl)` is a softer relative of Journey's over-driven Lambert. Its width is 23.4 degrees **[computed]**: `acos(-0.05) - acos(0.35)`.
4. **Edges as distance fields:** do not post-process outlines (that would echo Manifold Garden and cost originality). Instead give every thin feature analytic coverage `saturate(d / fwidth(d) + 0.5)`, plus geometric chamfers that catch the rim (M06). Apply the "do not trust the MSAA resolve" lesson to analytic features.
5. **Combine highlights with max, not sum,** if the beacon halo, rim and window dots ever overlap: `max(rimTerm, emissiveTerm)` caps local clipping (clipping budget in 14.10).
6. **Originality guard:** no sand glitter, no flowing cloth, no infinite-repeat world, no gravity colour coding. Thread's signatures are the line-to-cable reveal, analytic planetary curvature and the cable shadow on a pastel sea.

---

## 4. Stylised cloud sea: noise, filtering, precision (HLSL)

### 4.1 Hashes

`pcg3d`, verbatim logic from Jarzynski and Olano, JCGT 9(3) 2020, section 6.1 [S22]. The paper's conclusion lists `pcg3d` among the recommended hashes for 2-to-1 and 3-to-1 inputs. Hoskins' hash is MIT-licensed [S23].

```hlsl
// ThreadNoise.hlsl
#ifndef THREAD_NOISE_INCLUDED
#define THREAD_NOISE_INCLUDED

// Jarzynski & Olano 2020, "Hash Functions for GPU Rendering", JCGT 9(3), sec. 6.1
uint3 Pcg3d(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return v;
}

// [0,1)^2 per integer cell; 24-bit mantissa-exact conversion
float2 CellHash2(int2 cell, uint seed)
{
    uint3 h = Pcg3d(uint3(asuint(cell), seed));
    return float2(h.xy >> 8) * (1.0 / 16777216.0);
}
float CellHash1(int2 cell, uint seed)
{
    uint3 h = Pcg3d(uint3(asuint(cell), seed));
    return float(h.x >> 8) * (1.0 / 16777216.0);
}

// "Hash without Sine" (c) 2014 David Hoskins, MIT licence, https://www.shadertoy.com/view/4djSRW
// Good for |p| < ~1e4; for big world coordinates use CellHash2 on integer cells.
float2 HoskinsHash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973)); // pal:ok (hash constant, not a colour)
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}
```

### 4.2 Cellular F1 with a 3 x 3 search, in cell space

IQ's precision trick [S24]: take `floor` and `frac` first and do all distance maths relative to the current cell. The large integer part is then never subtracted in float, which is what makes planet-sized domains work.

```hlsl
// x: coordinate in cell units RELATIVE to an integer cell offset supplied by the CPU (|x| stays moderate).
// Returns F1 in cell units. Full jitter: feature point uniform in its cell.
// Measured for this construction [computed, 120k samples]: mean 0.431, median 0.426, p99 0.868, max 1.115.
float CellularF1(float2 x, int2 cellOffset, uint seed)
{
    float2 ip = floor(x);
    float2 fp = x - ip;
    int2   ic = int2(ip) + cellOffset;
    float  best = 8.0;
    [unroll] for (int j = -1; j <= 1; j++)
    [unroll] for (int i = -1; i <= 1; i++)
    {
        int2   b = int2(i, j);
        float2 r = float2(b) + CellHash2(ic + b, seed) - fp;
        best = min(best, dot(r, r));
    }
    return sqrt(best);
}

// Smooth value noise for the domain warp (C2 quintic fade; the warp must be smooth, F1 is only C0)
float ValueNoise(float2 x, int2 cellOffset, uint seed)
{
    float2 ip = floor(x);
    float2 f  = x - ip;
    int2   c  = int2(ip) + cellOffset;
    float a = CellHash1(c,             seed);
    float b = CellHash1(c + int2(1,0), seed);
    float e = CellHash1(c + int2(0,1), seed);
    float g = CellHash1(c + int2(1,1), seed);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(lerp(a, b, u.x), lerp(e, g, u.x), u.y);
}

// One warped cellular octave: n = 1 - F1, warp amplitude 0.6 cell (14.7.7)
float CloudOctave(float2 x, int2 cellOffset, uint seed)
{
    float2 w = float2(ValueNoise(x, cellOffset, seed + 101u),
                      ValueNoise(x, cellOffset, seed + 202u)) * 2.0 - 1.0;
    return 1.0 - CellularF1(x + 0.6 * w, cellOffset, seed);
}
#endif
```

**CPU side (double).** For octave k with cell size `S_k` (6000, 1500, 400 m), let `o_k = (surfaceOriginXZ + scroll) / S_k`. Here surfaceOriginXZ is the camera's ground point plus the 0.4 m/s drift, accumulated in double.

```csharp
// ProgressDirector (double precision), per octave k
double ox = (originX + scrollX) / cellSize[k], oz = (originZ + scrollZ) / cellSize[k];
double fx = Math.Floor(ox), fz = Math.Floor(oz);
cloudCellOffset[k] = new Vector4((float)fx, (float)fz, 0, 0);          // exact while |fx| < 2^24
cloudCellFrac[k]   = new Vector4((float)(ox - fx), (float)(oz - fz), 0, 0);
// shader: x_k = _CloudCellFrac[k].xy + pLocalMetres / S_k ; cellOffset = int2(_CloudCellOffset[k].xy)
```

`pLocalMetres` (camera-relative, up to about 2.8e6 m at the limb) divided by 400 m gives at most about 7,000 cells, with a float resolution of about 5e-4 cell. That is invisible.

### 4.3 Footprint fading: band-limiting

IQ [S25]: replace each octave by a self-limiting one, `noise(x) * smoothstep(1.0, 0.5, w)`, where `w` is the per-octave filter width (`fwidth`), propagated with the frequency. IQ fades toward zero. **For cellular `n`, fade toward the octave mean (0.569) instead**, otherwise the sea brightens or darkens as octaves drop out during the climb.

```hlsl
// pLocal: camera-relative surface coords (m), computed for EVERY pixel before any branch (derivatives!)
float  fwM   = max(length(ddx(pLocal)), length(ddy(pLocal)));   // metres per pixel (anisotropic max)
float  cellPx = cellSizeM / max(fwM, 1e-3);                      // cell size on screen in px
float  fade  = smoothstep(3.0, 6.0, cellPx);                     // spec: zero below 3 px
```

### 4.4 Variance-preserving octave combination (new, measured)

**[computed]** With weights 0.5 / 0.3 / 0.2, the plain sum `n = sum w_k n_k` has a 5 to 95 percent range of only 0.37 to 0.76. Then 0.0 percent of pixels fall below 0.18 and 2.7 percent below 0.34, so the shade tone disappears and acceptance item 3 ("at least 3 distinct tones") is at risk.

Normalising by the RMS of the active weights keeps the distribution fixed whatever has faded:

```hlsl
static const float CELL_N_MEAN  = 0.569; // single-octave mean of 1-F1 (measured; re-measure in EditMode)
static const float CELL_N_SIGMA = 0.189; // single-octave std
static const float CLOUD_SPREAD = 0.20;  // tone spread after normalisation

float CloudField(float2 x0, float2 x1, float2 x2, int2 c0, int2 c1, int2 c2, float3 fade)
{
    const float3 W = float3(0.5, 0.3, 0.2); // pal:ok (octave weights)
    float3 wk  = W * fade;
    float  num = wk.x * (CloudOctave(x0, c0, 11u) - CELL_N_MEAN)
               + wk.y * (CloudOctave(x1, c1, 23u) - CELL_N_MEAN)
               + wk.z * (CloudOctave(x2, c2, 37u) - CELL_N_MEAN);
    float  z   = num * rsqrt(max(dot(wk, wk), 1e-8)) / CELL_N_SIGMA;
    return saturate(0.5 + CLOUD_SPREAD * z);   // -> 0.5 (mid tone) when everything has faded
}
```

Measured tone fractions with `CLOUD_SPREAD = 0.2`: **n > 0.60: 32 to 33 percent, n > 0.46: 58 percent, n < 0.34: 22 percent, n < 0.18: 6 percent**. These hold for all three octaves, for octave 3 faded, for octave 2 at half weight and for octave 1 alone. With the spec thresholds this gives roughly 33 percent top, 45 percent mid and 22 percent shade.

Skip `CloudOctave` when `fade == 0`, using a uniform branch on the CPU-known altitude where possible, so faded octaves cost nothing. Do not branch per pixel on derivative-dependent values.

### 4.5 Tone quantisation without new aliasing

A smoothstep applied after the noise reintroduces high frequencies (IQ, and the GameDev.net discussion of [S25]). Widen the bands by the pixel footprint of `n`:

```hlsl
float fwN   = fwidth(n);
float top   = smoothstep(0.46 - fwN, 0.60 + fwN, n);
float shade = 1.0 - smoothstep(0.18 - fwN, 0.34 + fwN, n);
float3 mix50 = 0.5 * (CLOUD_TOP + CLOUD_SHADE);
float3 C = lerp(lerp(CLOUD_SHADE, mix50, 1.0 - shade), CLOUD_TOP, top);
```

**Domain warping** (IQ [S26]): `f(p + h(p))`, with the two warp components taken from offset evaluations of one noise function. A single warp level is enough here; IQ's double warp `fbm(p + 4 fbm(p + 4 fbm(p)))` is too organic for flat-tone puffs.

### 4.6 C# mirror for EditMode tests

```csharp
static uint3 Pcg3d(uint x, uint y, uint z) // unchecked uint arithmetic wraps like HLSL
{
    unchecked {
        x = x * 1664525u + 1013904223u; y = y * 1664525u + 1013904223u; z = z * 1664525u + 1013904223u;
        x += y * z; y += z * x; z += x * y;
        x ^= x >> 16; y ^= y >> 16; z ^= z >> 16;
        x += y * z; y += z * x; z += x * y;
        return new uint3(x, y, z);              // Unity.Mathematics.uint3, or a small struct
    }
}
```

Use it to histogram `CloudField` over 100k random points (expect the fractions in 4.4) and to assert the "3 distinct tones" acceptance offline.

---

## 5. Ray/sphere at planetary radii: precision and the angular formulation

### 5.1 Verified sources

- **RTG chapter 7** (Haines, Günther, Akenine-Möller 2019 [S27] [S28]). The official sample `sphereIntersect.hlsli` [S29] implements seven methods. **Method 6** (unit direction) is the recommended one:
  ```hlsl
  float3 f = orig - center; float b2 = dot(f, dir); float r2 = radius * radius;
  float3 fd = f - b2 * dir;                       // Hearn & Baker: avoid b^2 - c cancellation
  float discriminant = r2 - dot(fd, fd);
  if (discriminant >= 0.0f) {
      float c = dot(f, f) - r2;
      float sqrtVal = sqrt(discriminant);
      float q = (b2 >= 0) ? -sqrtVal - b2 : sqrtVal - b2;   // Press et al.: no cancellation in the root
      // near hit t0 = c / q ; far hit t1 = q
  }
  ```
  (file lines 162 to 189). Its comments credit Hearn and Baker eq. 10-72 for the case "radius^2 << distance" and Numerical Recipes for the `q` form.
- **SRP Core has only the naive form.** `GeometricTools.hlsl:95-102`, `IntersectRaySphere(start, dir, radius, out float2)`, computes `c = dot(start,start) - radius*radius` and calls `SolveQuadraticEquation(a, b, c, ...)`. `IntersectRaySphereSimple` (lines 107 to 114) uses `b*b - 4*c`. **Do not use either for the Earth or the cloud shell.**
- Remaining weakness of method 6 at our scale: `c = dot(f,f) - r2` subtracts two numbers of about 4e13 m^2 when the camera is close to the surface. Fix: compute `c = h (2R + h)` exactly on the CPU.

### 5.2 Float32 emulation at our radii **[computed]**

R_c = 6,374 km, 2,001 rays from nadir to the limb:

| h above shell | naive quadratic (SRP Core style): worst relative t error | false misses | stable form below: worst relative t error |
|---|---|---|---|
| 4.46 km (KF6a) | 1.9e-4 (about 21 m at the 113 km cut) | 1 of 2001 | 2.6e-6 |
| 25.2 km | 5.0e-5 | 1 of 2001 | 6.0e-6 |
| 226 km | 1.2e-5 | 0 | 2.4e-5 (limb ill-conditioning, inherent) |
| 600 km | 8.5e-5 | 0 | 3.2e-5 (inherent, about 1e-6 rad of ray direction) |

The naive error is per-pixel **noise**: it does not vary smoothly, so `fwidth` of the derived surface coordinates jitters and the octave fades shimmer. The stable form is smooth.

### 5.3 Angular (mu) formulation: drop-in HLSL

Camera at distance `rho = R_c + h` from the centre, `up` = +Y (camera-relative world), unit ray `d`, `m = -d.y` (cosine of the angle to nadir). For the near root:

`t = c / (rho m + sqrt(rho^2 m^2 - c))` with `c = rho^2 - R_c^2 = h (2R_c + h)` and `rho^2 m^2 - c = rho^2 (m - mu_h)(m + mu_h)`, where `mu_h = sqrt(c)/rho = cos psi_limb`. Every term is positive, so nothing cancels.

```hlsl
// CPU (double) -> float uniforms, per frame (ProgressDirector):
//   _ShellRho = Rc + h ; _ShellRc = Rc ; _ShellCOverRho = h*(2*Rc + h)/(Rc + h) ; _ShellMuH = sqrt(h*(2*Rc + h))/(Rc + h)
//   _EarthMuH = sqrt(1 - (R/(R + A))^2)   (R-based limb used by the hit test and the dip acceptance)
float  _ShellRho, _ShellRc, _ShellCOverRho, _ShellMuH, _EarthMuH;

// Evaluate for EVERY sky pixel (clamped), so ddx/ddy of the outputs are valid everywhere.
void ShellIntersect(float3 d, out float t, out float2 pLocal, out float coverage)
{
    float m  = -d.y;
    float s  = length(d.xz);                 // sin(psi) without the 1 - m*m cancellation near nadir
    float mc = max(m, _ShellMuH);            // clamp misses onto the limb (continuous at the limb)
    float root = sqrt(max((mc - _ShellMuH) * (mc + _ShellMuH), 0.0));
    t = _ShellCOverRho / (mc + root);        // RTG ch.7 "c/q" in angular form, metres
    // Central angle: atan2 is well conditioned up to the limb (asin(k sin psi) - psi is not: the
    // asin argument -> 1 there, error ~ sqrt(2 * 6e-8) = 3.5e-4 rad = 2.2 km of arc).
    float gamma = atan2(t * s, _ShellRho - t * mc);
    // Arc length along the ray azimuth; finite at the nadir (gamma/s -> t/(rho - t m) as s -> 0)
    float k = (s > 1e-4) ? gamma / s : t / (_ShellRho - t * mc);
    pLocal = d.xz * (k * _ShellRc);          // camera-relative surface coordinates (m)
    float edge = m - _EarthMuH;              // > 0 inside the (R-based) limb
    coverage = saturate(edge / max(fwidth(edge), 1e-7) + 0.5);
}
```

Notes:

- Clamp `h >= 1 m` on the CPU before the white-out emergence (the spec only guarantees h > 0 after it), so `_ShellMuH` and `_ShellCOverRho` stay finite and positive.
- **Nadir (free-look straight down, acceptance item 7):** `atan2(0, x) = 0` and `k` stays finite, so there is no NaN and no azimuth singularity. `d.xz * k` is used instead of `(cos phi, sin phi)`, so there is no `atan2(d.z, d.x)` seam.
- **Limb glow:** `elev = asin(d.y)`, `eps = elev + delta` (delta = `asin(_EarthMuH)` = dip), `glow = ATMO_RIM * 1.2 * exp(-max(eps, 0) / eps0) * sunSide * gate` (gate from 2.4). Keep `delta`, `_EarthMuH` and `eps0` from **one** CPU computation so the edge and the glow cannot drift apart.

### 5.4 Planet disc (angular, exact normal)

For a sphere of angular radius alpha in unit direction `c` (unit distance, `Rp = sin alpha`): `sin u = |d x c|` is well conditioned for small u, unlike `acos(dot)`, which has an error of about 3.5e-4 rad near u = 0. The exact hit is `t = cos u - sqrt((sa - su)(sa + su))` and **`n = (t d - c) / sa`**.

```hlsl
float3 _PlanetDir, _PlanetRight, _PlanetUp; float _PlanetSinA; // CPU, orthonormal frame; sa = sin(14 deg)
float  PlanetDisc(float3 d, out float3 nWS)
{
    float cu = dot(d, _PlanetDir);
    float su = length(cross(d, _PlanetDir));
    float sa = _PlanetSinA;
    float inside = sa - su;                                   // > 0 on the disc
    float t  = cu - sqrt(max((sa - su) * (sa + su), 0.0));
    nWS = (t * d - _PlanetDir) / sa;                          // exact sphere normal (world)
    float cov = saturate(inside / max(fwidth(su), 1e-7) + 0.5) * step(0.0, cu);
    return cov;
}
// Key light given in the disc frame (x right, y up, z toward viewer = -_PlanetDir):
// Lp = normalize(0.55*_PlanetRight + 0.40*_PlanetUp - 0.73*_PlanetDir); lit = smoothstep(-0.05, 0.35, dot(nWS, Lp))
```

The spec's disc-frame normal `(x/r, y/r, sqrt(1 - rho^2))` differs from the exact one by a rotation of up to u (14 degrees at the rim). Either is acceptable for stylised shading. The exact form keeps the terminator geometrically consistent with `L_p`.

---

## 6. Flat shading with a smooth terminator and a fresnel rim under fog (URP 17.6)

### 6.1 Verified API (package source)

| API / keyword | Where | Note |
|---|---|---|
| `Light GetMainLight()` | `ShaderLibrary/RealtimeLights.hlsl:100-119` | `direction = _MainLightPosition.xyz` (toward the light), `color = _MainLightColor.rgb`, `shadowAttenuation = 1` |
| `Light GetMainLight(float4 shadowCoord)` | `RealtimeLights.hlsl:121-126` | sets `shadowAttenuation = MainLightRealtimeShadow(shadowCoord)` |
| `float4 TransformWorldToShadowCoord(float3 positionWS)` | `ShaderLibrary/Shadows.hlsl:360-374` | picks the cascade per pixel when `_MAIN_LIGHT_SHADOWS_CASCADE` is defined |
| `MainLightRealtimeShadow` | `Shadows.hlsl:376-401` | returns 1 unless `MAIN_LIGHT_CALCULATE_SHADOWS` is defined |
| `MAIN_LIGHT_CALCULATE_SHADOWS` / `REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR` | `Shadows.hlsl:13-18` | defined from `_MAIN_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS_CASCADE` or `_MAIN_LIGHT_SHADOWS_SCREEN` (unless `_RECEIVE_SHADOWS_OFF`); the vertex interpolator is used only for non-cascade or screen shadows, so with 4 cascades compute the coordinate per pixel |
| Forward keywords | `Shaders/SimpleLit.shader:108-121` | `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN`, `#pragma multi_compile_fragment _ _SHADOWS_SOFT`, `#pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`; line 114 shows **`_CLUSTER_LIGHT_LOOP`** (the 17.x name; no `_FORWARD_PLUS`) |
| `half3 GetWorldSpaceNormalizeViewDir(float3)` | `ShaderLibrary/ShaderVariablesFunctions.hlsl:137-149` | perspective: `normalize(camPos - posWS)` |
| `_MainLightColor` value | `Runtime/UniversalRenderPipelineCore.cs:1904-1905` | `lightColor = lightData.finalColor` (colour x intensity, times the colour-temperature colour if `Light.useColorTemperature`) |
| ShadowCaster normal bias | `Shaders/ShadowCasterPass.hlsl:11-44` | the custom ShadowCaster must declare `float3 _LightDirection;` and use `TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection))` |

### 6.2 Lighting function (spec 14.2.2, compile-ready)

```hlsl
// ThreadFlatLighting.hlsl  (include after Core.hlsl and Lighting.hlsl)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

static const float3 THREAD_SHADE_TINT = float3(0.52, 0.58, 0.78); // pal:ok (named shade tint, 14.2.2 step 2)

// fogAmount = 1 - exp(-tau) from the SAME ThreadFog evaluation used by ThreadFogApply (compute tau once)
float3 ThreadFlatLighting(float3 albedo, float3 normalWS, float3 positionWS, float ao,
                          float3 ambient, float cableShadow, float fogAmount, float3 rimTint)
{
    float3 N  = normalize(normalWS);                         // re-normalise: interpolation shortens N
    Light  ml = GetMainLight(TransformWorldToShadowCoord(positionWS)); // per pixel: 4 cascades
    float3 V  = GetWorldSpaceNormalizeViewDir(positionWS);
    float  nl = dot(N, ml.direction);
    float  lit = smoothstep(-0.05, 0.35, nl);                // 23.4 deg wide soft terminator
    float  litTotal = lit * ml.shadowAttenuation * cableShadow;
    float3 base = lerp(albedo * THREAD_SHADE_TINT, albedo, litTotal) * lerp(0.78, 1.0, ao);
    float3 radiance = base * (0.40 * ambient + 0.70 * ml.color);   // ml.color = colour x intensity
    float  rim = pow(1.0 - saturate(dot(N, V)), 3.0) * 0.16 * (0.35 + 0.65 * lit);
    rim *= 1.0 - smoothstep(0.85, 0.95, abs(N.y));           // no rim on big horizontal planes
    rim *= 1.0 - fogAmount;                                  // then ThreadFogApply fades it again: (1-f)^2
    return radiance + rimTint * rim;
}
```

Fragment input: declare the normal as **`centroid float3 normalWS : TEXCOORD1;`**. With MSAA 4x, a pixel whose centre lies outside the triangle otherwise gets an extrapolated normal, and `pow(1 - N.V, 3)` spikes into a crawling bright edge on silhouettes. Rails are exempt because they use ThreadLine.

### 6.3 Pitfalls specific to this recipe

1. **Double fog on the rim:** `(1 - fog)` followed by `ThreadFogApply` gives a (1 - f)^2 fade. The spec intends this ("rim never survives in distant haze"); note it in a comment so critics do not file it as a bug. Use the identical `tau`; computing a second fog estimate makes rim and body fade at different distances.
2. **Shadow acne in the terminator band** (0 < nl < 0.15), where `lit` is already 0.3 to 0.6. Rely on the ShadowCaster `ApplyShadowBias` path (6.1) with normal bias 1.2 and depth bias 1.0. Do not darken the shade side further: `litTotal` already makes cast shadow equal to the terminator tone.
3. **Light colour double-counting:** if `Light.useColorTemperature` is on, `finalColor` multiplies in the temperature colour. Either disable it and write the palette hex, or set the colour to white and use the temperature. Never do both.
4. **T-PAL scanner:** every `float3`/`float4` literal must be a palette macro, a named `static const` in `ThreadCommon.hlsl`, or carry `// pal:ok`. This includes hash constants (`float3(0.1031, ...)`) and octave weights.
5. **`fwidth` inside divergent branches** is undefined. In the sky shader, compute `pLocal`, `n` and every footprint for all pixels (clamped, as in 5.3), and only then select or blend.
6. **Half precision:** `Light.direction` and `color` are `half3` (`RealtimeLights.hlsl:14-15`). That is fine for lighting. Keep `positionWS`, `t`, `pLocal` and every planetary quantity in `float`.
7. **Rim and flat facets:** on large flat facets `N.V` changes only through `V`. The 36 cable ridges therefore get one rim stripe per ridge on the shade-side silhouette (intended), but big vertical panels can show a full-face glow at grazing view. Cap it with the `(0.35 + 0.65 * lit)` factor already in the spec, and check it in KF6b (cable wall with the cream rim).

---

## 7. Consolidated pitfalls checklist (for critics)

- [ ] Every dip uses `acos` in double; no `sqrt(2A/R)` (0.9 degrees wrong at 600 km).
- [ ] The cloud-shell cut is hazed out (2.3); there is no flat band under the horizon in KF6a.
- [ ] The limb glow is gated at low altitude (2.4).
- [ ] No SRP Core `IntersectRaySphere` for planetary spheres; `c` comes from the CPU as `h(2R+h)`.
- [ ] `sin psi` comes from `length(d.xz)`, the central angle from `atan2`, and the nadir is guarded.
- [ ] Octave fades go toward the mean, and the variance-preserving combination is used (4.4); 3 tones are present at every altitude.
- [ ] Tone smoothsteps are widened by `fwidth(n)`.
- [ ] Hashes take integer cells (pcg3d); the camera-cell offset is split in double.
- [ ] Centroid normals; per-pixel shadow coordinates (cascades); the ShadowCaster uses `ApplyShadowBias`.
- [ ] The README states that the ribbon-versus-cable difference is stylised and uses the facts in 1.1 only.

---

## 8. Sources (all accessed 2026-10-29)

| ID | URL | Used for |
|---|---|---|
| S1 | https://en.wikipedia.org/wiki/Space_elevator | history, GEO, taper table, 4,960 km, 30 to 80 MYuri, Edwards 19,800 kg / 207 climbers, release altitudes, hazards |
| S2 | https://www.sciencenews.org/article/ribbon-stars | Edwards ribbon about 1 m wide, thinner than paper, 100,000 km, Pacific anchor, about 190 km/h (via search summary) |
| S3 | https://NSS.org/wp-content/uploads/2017/07/Ad-Astra-Magazine-v15n1f1.pdf | climbers up to 200 km/h, laser power beaming (via search summary) |
| S4 | https://NSS.org/wp-content/uploads/2017/07/2003-Space-Elevator-NIAC-phase2.pdf | Edwards NIAC Phase II Final Report (2003), existence and scope; PDF text not machine-readable here |
| S5 | https://www.isec.org/space-elevator-tether-materials | ISEC tether 100,000 km x 1 m, graphene / hBN / CNT |
| S6 | https://www.isec.org/news/climbertether-interface-of-the-space-elevator-study-now-available- and https://isec.org/space-elevator-newsletter-2024-september-special-edition | opposing-wheel friction climber (2023), Galactic Harbour and Apex Anchor (via search summary) |
| S7 | https://arxiv.org/abs/1804.06453 ; https://arxiv.org/abs/2412.17198 | Popescu and Sun 2018 (high stress ratio plus repair); Gassend exponential tethers (2004/2024) |
| S8 | https://earthobservatory.nasa.gov/images/1094/aurora-from-the-space-shuttle | airglow layer about 100 km, about 30 km thick, 557.7 nm, edge-on only (via search summary) |
| S9 | https://www.nasa.gov/image-detail/gmt064_ehdc1 and the ISS sunset image description (search results) | thin blue line (Rayleigh), orange troposphere band at the terminator |
| S10 | https://earthobservatory.nasa.gov/images/154648/nighttime-over-the-eastern-pacific | green airglow about 130 km, red about 240 km (via search summary) |
| S11 | https://earthref.org/ERR/33081 (Mende et al. 1993, STS-37 limb spectra) | 557.7 / 630 nm emissions above 110 km, Na peak about 92 km (via search summary) |
| S12 | https://www.nasa.gov/image-article/cloudy-earth | 67 percent cloud cover, under 10 percent of ocean skies fully clear |
| S13 | https://modis.gsfc.nasa.gov/sci_team/pubs/abstract.php?id=03751 | King et al. 2013: 67 percent global, 55 percent land, 72 percent ocean |
| S14 | https://en.wikipedia.org/wiki/Journey_(2012_video_game) | "Japanese garden" subtraction, monomyth, awe, music arc |
| S15 | https://www.gdcvault.com/play/1017799/The-Art-of | Nava "The Art of Journey" (GDC 2013) scope |
| S16 | https://gdcvault.com/play/1017742/Sand-Rendering-in | Edwards "Sand Rendering in Journey" (GDC 2013) |
| S17 | https://www.alanzucconi.com/2019/10/08/journey-sand-shader-1/ , -2/ , -4/ | diffuse contrast formula, rim and ocean specular, max combination |
| S18 | https://blog.siggraph.org/?p=11525 | Manifold Garden: SDF AA edges, MSAA advice, world wrapping, "nowhere to hide" |
| S19 | https://history.siggraph.org/?p=77384 | retrospective abstract, keywords, references (Decaudin, Green 2007) |
| S20 | https://www.pcgamer.com/uk/manifold-garden-review | colour per gravity direction (via search summary) |
| S21 | https://en.wikipedia.org/wiki/Manifold_Garden | Tadao Ando, no curved surfaces (via search summary) |
| S22 | https://www.jcgt.org/published/0009/03/02/paper.pdf | pcg3d listing (sec. 6.1) and recommendations (sec. 7), text extracted locally with zlib |
| S23 | https://www.shadertoy.com/view/4djSRW (code copy: https://pyssv.readthedocs.io/en/latest/_modules/random.glsl.html) | Hoskins "Hash without Sine", MIT licence |
| S24 | https://iquilezles.org/articles/smoothvoronoi/ | 3x3 voronoi, cell-space precision trick, smooth voronoi variants |
| S25 | https://iquilezles.org/articles/bandlimiting/ (and https://iquilezles.org/articles/filtering/) | `fcos`, band-limited fBM `smoothstep(1.0, 0.5, w)`, footprint propagation |
| S26 | https://iquilezles.org/articles/warp/ | domain warping `f(p + h(p))` |
| S27 | https://research.nvidia.com/publication/2019-03_precision-improvements-raysphere-intersection | RTG ch. 7 abstract and authors |
| S28 | https://www.realtimerendering.com/raytracinggems/rtg/ | book access (CC BY-NC-ND), errata link |
| S29 | https://github.com/Apress/ray-tracing-gems/blob/master/Ch_07_Precision_Improvements_for_Ray-Sphere_Intersection/sphereIntersect.hlsli | methods 0 to 6, method 6 code quoted above |
| S30 | https://reedbeta.com/blog/hash-functions-for-gpu-rendering | 32-bit `pcg_hash` constants (do not mix them with the pcg3d constants) |

Local ground truth (package source): see the table in 6.1, plus `com.unity.render-pipelines.core@2d66c71e606e/ShaderLibrary/GeometricTools.hlsl:95-114`.
