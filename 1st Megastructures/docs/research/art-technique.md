## Art direction and rendering-technique references (web research, accessed 2026-10-29)

Tags: **VS** = VERIFIED-SOURCE (package source, original paper/slides/code, or checked numerically by me). **VD** = VERIFIED-DOCS (an authoritative web page). **UV** = UNVERIFIED (industry rule of thumb or my own recommendation). Every web page was treated as untrusted data. I downloaded no assets.

### 1. Brutalist concrete (scale, surface, joints)
- **Barbican:** poured in-situ concrete, most exposed surfaces deep pick-hammered to show granite aggregate, with bush or brush hammering in interiors. Formwork was 3/4 in (19 mm) plywood boxes, and sections waited about 28 days. **VD** (barbicanliving, City of London guide)
- **National Theatre (Lasdun):** cast against rough-sawn boards. The grain of each plank and a horizontal ridge at each board joint are printed into the surface. **VD** (studiomatrx/RIBA)
- **Boston City Hall:** about half precast (about 22,000 components) and half cast-in-place, with heavy board-marked piers under a precast office block. **VD/UV** (Architectuul is a wiki)
- **Habitat 67:** 354 precast boxes of 17'6" x 38'6" x 10' (5.3 x 11.7 x 3.0 m), 70-90 tons when lifted, 5000 psi concrete, steam cured. **VD** (PCI Journal 1967)
- **Ties:** put tie holes on rustication or reveal lines. One spec caps holes at 7/8 in (22 mm). Breakback ties need at least 1 in (25 mm) of cover. **VD** (SpecFormliners guide, Lee County spec)
- **Fresh-concrete pressure:** about 150-160 psf per ft of depth (about 23.6-25 kPa/m), so ties sit closer near the bottom of tall pours. **VD** (patent text)
- **Shader numbers, all UV (I found no authoritative source):**
  - Boards 90-150 mm wide (nominal 1x4/1x6 = 89/140 mm actual), lengths 2.4-4.8 m with staggered butt joints.
  - Joint ridges 1-3 mm proud of the surface. Each board gets its own depth offset (±1-2 mm) and albedo jitter (±3-6%). Grain is stretched about 1:20 along the board.
  - Tie-cone holes 22-32 mm across and 25-40 mm deep, on a grid of about 600 mm horizontal by 600-900 mm vertical, aligned to board joints.
  - Pour lifts 1.2-3.6 m (often one storey). Each lift line shows a thin horizontal seam with a small tone shift and grout-bleed streaks.
  - Control or expansion joints every 6-9 m along walls.

### 2. BLAME! / Control: composition principles (no frames copied)
- **BLAME!:** the City is a layered megastructure, and the underside of each layer lights the layer below. The hero is a silent loner. **VD** (Wikipedia). Figures read as tiny next to the structures ("like ants"). Blocky, repeating man-made lines are set against chaotic organic forms. Nihei trained in architecture. **VD** (Carroll News)
- **Control:** sharp brutalist architecture against the fluid Hiss, Escher-like spaces, tall pipes that read like a forest, and an intense red reserved for the Hiss. **VD** (Game Informer). Brutalism is at the core of the look, and the team deliberately avoided other games as references. **VD** (alanwake.info, quoting Remedy)
- **Turned into rules (UV):**
  - Repeat verticals at a regular rhythm so perspective shrinkage shows the distance.
  - One small figure or human-scale object per vista.
  - One hard key light against near-black.
  - Fog layers that separate depth planes.
  - Mostly cool, desaturated concrete with a single warm sodium or red accent. Concrete linear albedo 0.25-0.40.

### 3. Crank-slider (pistons) — VS (Wikipedia "Piston motion equations", checked by finite difference)
- Position: `x(θ) = r cosθ + sqrt(l² − r² sin²θ)`, with r = half stroke, l = rod length, θ measured from top dead centre (TDC).
- Velocity: `dx/dθ = −r sinθ − r² sinθ cosθ / sqrt(l² − r² sin²θ)`. Multiply by ω in the time domain.
- Acceleration: `−r cosθ − r²(cos²θ − sin²θ)/sqrt(l²−r²sin²θ) − r⁴ sin²θ cos²θ/(l²−r²sin²θ)^{3/2}`.
- Rod angle: `φ = asin((r/l) sinθ)`, so φ_max = asin(r/l), about 19.5° at l/r = 3. Peak velocity is not at 90°: for l = 6 in, r = 2 in it is at about ±73.2°.
- Typical rod length is about 1.4-2.2 times the stroke, so l/r ≈ 2.8-4.4. **VD-weak.** Marine crosshead engines use short rods. **VD**
- Real reference: RTA96-C mean piston speed 8.5 m/s at 102 rpm, which matches 2·S·n/60. **VS**

### 4. Fog, light shafts, phase functions, quadrature
- **Wronski, SIGGRAPH 2014 (AC4).** **VS** (slide text extracted from the PDF)
  - Frustum-aligned volume of 160x90x64 or 160x90x128 with an exponential depth distribution packed near the camera. Range 50-128 m.
  - Pass order: light the froxels, then a 2D compute march through the depth slices, then apply with one 3D-texture lookup and an FMA (works for transparents too).
  - Shadows: ESM downsampled to R32F 1024x256 plus a separable box blur. ESM leaking was not visible in fog.
  - Density: one octave of wind-animated Perlin noise plus exponential height falloff. A 2-colour art-driven phase in AC4; HG and SH extensions came later.
  - Cost about 1.1 ms. A 1-sample temporal jitter plus reprojection removed almost all edge artefacts.
- **Hillaire, SIGGRAPH 2015 (Frostbite).** **VD** (slide text on SlideShare)
  - Default froxels: 8x8-pixel tiles, 64 depth slices.
  - Artists author {absorption, scattering} or {albedo, extinction}. σt = σs + σa and albedo = σs/σt.
  - Temporal: blend 5% current into history (exponential moving average) with Halton jitter, using the same offset along each view ray.
  - Uses a single average phase g ("no multi lobe"). PS4 900p total cost: 2.95 ms at 8x8 tiles, 0.83 ms at 16x16.
  - Slide 28 says to "integrate lighting w.r.t. transmittance over froxel depth D". The closed form is `S_int = (S − S·exp(−σt·D)) / σt`, then `L += T·S_int; T *= exp(−σt·D)`, where S = σs·phase·L_light·shadow. This is **VS-math** (∫₀ᴰ S e^{−σt x} dx); the slide itself is garbled in the text extraction.
- **Schneider, SIGGRAPH 2015 (Horizon Zero Dawn clouds).** **VS** (PDF text)
  - Lighting model: Beer's law, Henyey-Greenstein, and a "powder" darkening term that depends on view direction.
  - 64-128 potential march samples plus 6 cone light samples. Cheap large steps until density is found, then one step back.
  - Quarter-res buffer updates 1 of 16 pixels per 4x4 block each frame, with reprojection. About 20 ms naive, about 2 ms target.
  - The powder formula `2·e^{−d}(1−e^{−2d})` is **UV** (it is drawn as an image in the slides).
- **Henyey-Greenstein:** `p(θ) = (1−g²) / (4π (1 + g² − 2g cosθ)^{3/2})`, with cosθ = dot(rayDir, dirToLight) and g > 0 meaning forward scattering. It integrates to 1 over the sphere (numerically 0.99999999977 at g = 0.6). **VS**
  - pbrt writes `+2g cosθ` because both its directions point away from the scattering point. This sign flip is a common bug. **VD**
  - Forward/back ratio is (1+g)³/(1−g)³: about 730:1 at g = 0.8. **VS-math**
- **Schlick approximation:** `(1−k²)/(4π(1 − k cosθ)²)` with `k ≈ 1.55g − 0.55g³`. I confirmed the normalisation analytically; the k fit is **UV**.
- **Haines 1991**, Graphics Gems II §V.1, pp. 247-250. **VS** (erich666/GraphicsGems `gemsii/RayCPhdron.c`). Planes are `n·x + w = 0` with outward normals.
  ```
  tnear=-INF; tfar=tmax;
  foreach plane: vd=dot(n,dir); vn=dot(n,org)+w;
    if(vd==0){ if(vn>0) MISS; }
    else { t=-vn/vd;
      if(vd<0){ if(t>tfar) MISS; if(t>tnear) tnear=t; }   // front face
      else    { if(t<tnear) MISS; if(t<tfar)  tfar=t;  } } // back face
  hit: tnear>=0 → entering at tnear; else inside, exit at tfar (if < tmax)
  ```
  For fog, use the segment `[max(tnear,0), min(tfar, sceneDepth)]`.
- **Gauss-Legendre, 12 points on [−1,1].** Computed by Newton iteration at 40 digits; matches the pomax table to 16 digits and the weights sum to 2. **VS**

| ±x_i | w_i |
|---|---|
| 0.12523340851146891328 | 0.24914704581340277323 |
| 0.36783149899818018413 | 0.23349253653835480571 |
| 0.58731795428661748293 | 0.20316742672306592477 |
| 0.76990267419430469253 | 0.16007832854334622108 |
| 0.90411725637047490878 | 0.10693932599531842664 |
| 0.98156063424671924356 | 0.04717533638651182776 |

  To map onto [a, b]: `x = (b−a)/2·t + (a+b)/2`, and multiply each weight by (b−a)/2.

### 5. Procedural materials
- **Quilez, box-filtered checker** (shadertoy XlcSz2). **VS**: I checked it numerically; it matches the naive checker within 2e-12 as w → 0 and returns 0.5 for large w.
  ```
  w = max(abs(dpdx),abs(dpdy));
  i = 2*(abs(frac((p-0.5*w)*0.5)-0.5) - abs(frac((p+0.5*w)*0.5)-0.5))/w;
  return 0.5 - 0.5*i.x*i.y;
  ```
  Pitfall: the `tri()`-based snippet printed in the checkerfiltering article returns values outside [0, 1]. Use the version from the filterableprocedurals article above.
- **Box-filtered grid** (XtBfzz), where 1/N is the line width. **VS**: the result is 0 on lines, 1 inside cells, and (1−1/N)² as the footprint gets large.
  ```
  a=p+0.5*w; b=p-0.5*w;
  i=(floor(a)+min(frac(a)*N,1)-floor(b)-min(frac(b)*N,1))/(N*w);
  return (1-i.x)*(1-i.y);
  ```
  Add a small epsilon to w, because w = 0 gives NaN.
- **Value noise** (Quilez "morenoise"): quintic fade `6t⁵−15t⁴+10t³`, with derivative `30t²(t−1)²`. The cubic fade causes discontinuity artefacts. Analytic derivatives are about 5x cheaper than central differences. **VS**
- **fBm** (Quilez "fbm"): gain `G = 2^{−H}`, and G = 0.5 (H = 1) is the natural choice. Detune lacunarity to 1.98-2.01 and rotate the domain each octave (`mat2(0.8,-0.6,0.6,0.8)`). **VS**
- **PCG hash.** **VS** (reedbeta; JCGT paper text)
  ```
  uint pcg(uint v){ uint s=v*747796405u+2891336453u;
    uint w=((s>>((s>>28u)+4u))^s)*277803737u; return (w>>22u)^w; }
  uint3 pcg3d(uint3 v){ v=v*1664525u+1013904223u;
    v.x+=v.y*v.z; v.y+=v.z*v.x; v.z+=v.x*v.y; v^=v>>16u;
    v.x+=v.y*v.z; v.y+=v.z*v.x; v.z+=v.x*v.y; return v; }
  ```
  Jarzynski and Olano recommend pcg3d/pcg4d for N→N hashes and xxhash32 for 3→1.
- **"Hash without Sine"** (David Hoskins, MIT licence, 4djSRW; not by Quilez): `p3=frac(p.xyx*.1031); p3+=dot(p3,p3.yzx+33.33); return frac((p3.x+p3.y)*p3.z);`. **VD** (third-party copy). It is documented to fail with small changes in p.
- **Pitfalls (VS-math):**
  - At world coordinates around 4 km, the float32 step is about 0.5 mm, so mm-scale noise and float hashes break down. Hash integer cell coordinates with pcg3d, or evaluate noise in local, rebased space.
  - HLSL `fmod` is not GLSL `mod` for negative inputs; use `x - y*floor(x/y)`.
  - GLSL `mat2(...)` is column-major and HLSL `float2x2(...)` is row-major, so a ported matrix comes out transposed.

### 6. Hex grids — VS (Red Blob implementation guide)
- Flat-top matrices: `f = (3/2, 0, √3/2, √3)`, `b = (2/3, 0, −1/3, √3/3)`, start_angle 0.
- So `q = (2/3·x)/s` and `r = (−1/3·x + √3/3·z)/s` are confirmed. The inverse is `x = s·3/2·q`, `z = s·(√3/2·q + √3·r)`.
- Flat-top hex width = 2s, height = √3·s. Pointy-top: `f = (√3, √3/2, 0, 3/2)`, `b = (√3/3, −1/3, 0, 2/3)`, start_angle 0.5.
- Rounding:
  ```
  round q,r,s; if(dq>dr && dq>ds) q=-r-s; else if(dr>ds) r=-q-s; else s=-q-r;
  ```
- Distance `(|dq|+|dr|+|ds|)/2`. Directions: (1,0,−1), (1,−1,0), (0,−1,1), (−1,0,1), (−1,1,0), (0,1,−1).
- Pitfall (UV): the guide assumes y-down screen space. When mapped to Unity XZ, corner order flips handedness, so check triangle winding for upward-facing caps (Unity front faces are clockwise).

### 7. Banding, dithering, YouTube
- **Gjoel, INSIDE (GDC 2016).** **VS** (slide text extracted)
  - Use the highest precision available, use sRGB or another dark-biased encoding, and dither when tonemapping to 8 bit: "almost always dither any 8bit write".
  - Uniform 1-LSB noise causes noise modulation. Triangular (TPDF) noise makes the error independent of the signal, but it must be 2 LSB wide.
  - Two ways to make TPDF: add two uniform randoms, or reshape one uniform (shadertoy 4t2SDh; reference ltBSRG).
  - INSIDE dithered after every pass and also dithered its 8-bit G-buffer normals.
- **URP 17.6 source:** `UniversalAdditionalCameraData.dithering` (bool, `UniversalAdditionalCameraData.cs:919`), keyword `_DITHERING` (`UniversalRenderPipelineCore.cs:1217`). **VS**
  - `ApplyDithering` (`Shaders/PostProcessing/Common.hlsl:188-206`) applies blue-noise-driven TPDF noise on [−1,1]/255 in sRGB space, in UberPost or FinalPost, cycling the `blueNoise16LTex` textures each frame.
  - It is forced off in the Scene view (`UniversalRenderPipeline.cs:1734`), so take captures from the Game view or the player.
  - It only runs when post-processing is on (`UniversalRendererRenderGraph.cs:648,1384`). Passes after post-processing are not dithered.
- **Codec effects.** **VD** (forasoft)
  - Quantization erases low-amplitude gradients. 10-bit coding narrows bands about 4x.
  - Dither added just before a lossy encode is often quantized away, so prefer higher bit depth and grain added near delivery.
- **YouTube upload settings:** MP4, H.264 High Profile, progressive, 2 B-frames, closed GOP of half the frame rate, CABAC, 4:2:0, BT.709. YouTube converts full range to limited range. **VD**
  - SDR bitrates (24-30 / 48-60 fps): 1080p 8/12 Mbps, 1440p 16/24, 2160p 35-45/53-68.
  - Uploading at 2160p to get VP9/AV1 is anecdotal. **UV**

### 8. Real scales
| Object | Figure | Tag |
|---|---|---|
| Bingham Canyon | 1.2 km deep, ~4 km across (Guinness); one infographic says 0.97 km | VD |
| Mponeng | ~4 km deep (Guinness); ~3.8 km per Wikipedia, planned 4.27 km | VD |
| Burj Khalifa | 828 m architectural, 829.8 m tip, 154+9 floors | VD |
| RTA96-C | bore 960 mm, stroke 2500 mm, 6-14 cylinders inline; 14-cyl 13.5 m high, 26.59 m long, >2300 t; crankshaft 300 t; 15-102 rpm; 80,080 kW | VD |
| NASA Ames 40x80 | six 40 ft (12.2 m), 15-blade, variable-pitch fans; 18,000 hp each | VD |
| Mine fans | Broken Hill 1954: 5.3 m single-stage axial; TLT range 1.8-5.3 m, up to 13.5 MW; Zitrón 3.8 m; Shanghai Electric 4.22 m | VD (vendor) |
| Habitat 67 module | 5.3 x 11.7 x 3.0 m | VD |

**Fan maths (VS-math):** tip speed is `v = π·D·n/60`; an 80 m fan at 1 rpm has a 4.2 m/s tip. To avoid wagon-wheel strobing, keep the per-frame rotation `ω/F` well below `(360°/B)/2` for B blades. Check this at 30 fps, not only at 60.

### 9. Megalophobia staging
- "The trick is people": viewers cannot judge scale without a reference, so keep a human-scale element in frame (Greig Fraser). **VD** (Inverse)
- **Aerial perspective:** contrast and saturation drop and fine detail is lost with distance as colour shifts toward the haze colour. **VD**
- **Koschmieder visual range:** `V = 3.912/β_ext` at 2% contrast. **VD.** So β = 3.912/V: a shaft wall that should fade out at 400 m needs β ≈ 0.0098 m⁻¹, and one that fades at 1.5 km needs β ≈ 0.0026 m⁻¹.
- **UV recommendations:**
  - Slow walk (1.0-1.3 m/s) with minimal head bob; never cut on a reveal.
  - Frame reveals through doorways or occluders, then open to the void.
  - Repeat known sizes (2.1 m doors, 1.1 m rails, regular lamps) receding into haze.
  - Stack discrete haze layers by height: `σ(h) = σ0·exp(−(h−h0)/H)`.
  - A narrower FOV (50-60° vertical) on vistas compresses depth and enlarges distant mass.
  - Slow pistons with 10-30 s periods; real engines run about 100 rpm.

### Sources (all accessed 2026-10-29)
- https://advances.realtimerendering.com/s2014/wronski/bwronski_volumetric_fog_siggraph2014.pdf — froxel fog numbers
- https://www.slideshare.net/DICEStudio/physically-based-and-unified-volumetric-rendering-in-frostbite — Hillaire froxels, temporal blend, costs
- https://advances.realtimerendering.com/s2015/index.html and its "The Real-time Volumetric Cloudscapes of Horizon - Zero Dawn - ARTR.pdf" — HZD clouds
- https://pbr-book.org/3ed-2018/Volume_Scattering/Phase_Functions — HG formula and sign convention
- https://raw.githubusercontent.com/erich666/GraphicsGems/master/gemsii/RayCPhdron.c ; https://www.gameenginegems.com/gemsdb/article.php?id=819 — Haines code and citation
- https://pomax.github.io/bezierinfo/legendre-gauss.html — Gauss-Legendre cross-check
- http://iquilezles.org/articles/checkerfiltering/ , /filterableprocedurals/ , /fbm/ , /morenoise/ — filtering, fBm, value noise
- https://reedbeta.com/blog/hash-functions-for-gpu-rendering/ ; https://www.jcgt.org/published/0009/03/02/paper.pdf — PCG and pcg3d
- https://pyssv.readthedocs.io/en/latest/_modules/random.glsl.html — Hoskins hash copy (MIT)
- https://www.redblobgames.com/grids/hexagons/implementation.html — hex formulas
- https://blog.playdead.com/articles/inside_presentations/inside_publications.html ; https://media.githubusercontent.com/media/playdeadgames/publications/master/INSIDE/rendering_inside_gdc2016.pdf — INSIDE dithering
- https://www.forasoft.com/learn/video-quality/articles-vqm/banding-gradient-steps — banding after encoding
- https://support.google.com/youtube/answer/1722171 — YouTube upload settings
- https://en.wikipedia.org/wiki/Piston_motion_equations — crank-slider
- https://en.wikipedia.org/wiki/Connecting_rod — rod ratios
- https://en.wikipedia.org/wiki/W%C3%A4rtsil%C3%A4-Sulzer_RTA96-C ; https://en.wikipedia.org/wiki/Burj_Khalifa ; https://www.guinnessworldrecords.com/world-records/383096-deepest-open-pit-mine ; https://www.mining-technology.com/features/feature-top-ten-deepest-mines-world-south-africa/ ; https://www.nasa.gov/image-detail/amf-aal-4479 ; https://im-mining.com/?p=83589 ; https://www.ausimm.com/publications/conference-proceedings/the-ausimm-proceedings-1956/the-zinc-corporation-limited---broken-hill/ — real scales
- https://www.barbicanliving.co.uk/barbican-story/construction/hammered-and-brushed-finish/ ; https://www.studiomatrx.org/architecture-canon/national-theatre ; https://architectuul.com/architecture/boston-city-hall ; https://www.pci.org/PCI_Docs/Publications/PCI%20Journal/1967/February-1967/Habitat%2067%20-%20Towards%20the%20Development%20of%20a%20Building%20System.pdf ; https://www.specformliners.com/wp-content/uploads/pdfs/guides/elastospec-guide.pdf — concrete references
- https://en.wikipedia.org/wiki/Blame! ; https://carrollnews.org/200840/arts-and-life/niheis-style-is-special/ ; https://gameinformer.com/2019/03/27/the-mesmerizing-art-behind-control ; https://www.alanwake.info/2026/05/1st-may-2026-third-control-resonant.html — composition
- https://www.inverse.com/entertainment/dune-2-greig-fraser-interview ; https://en.wikipedia.org/wiki/Aerial_perspective ; https://en.wikipedia.org/wiki/Visibility — scale and haze
