using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 5 full tier: the vertical city (14.5.5, 14.7.3).
    /// - Shader tier (persistent, also the faint far city seen from the bridge): M_Facade quads per face, 200 m chunk (module
    ///   rows 0-6, 7-15, 16-23) and module bucket (tone / lit fraction, Beat05Layout.ModuleBucket), 5 cm in front of the wall,
    ///   a quad for every generated module and none for the excluded ones (fan zone, mouth window, the bare near flat under
    ///   the bridge above y -333), so both tiers cover exactly the same rectangles with the same materials.
    /// - Bridge void (R1-01): until the hall portal the city windows, beacons and freight cars are dimmed per face and the
    ///   shader's downward-view attenuation (_AbDownView) is on; see <see cref="UpdateBridgeVoid"/>.
    /// - Near tier: one module mesh (24 x 21 m, 48 windows: proud surround, 45-degree chamfer, 0.32 m reveal, recessed glass
    ///   with vertex B = 255; V joints and skirts at the module edge; about 2.2 k triangles) instanced per tile of 2 x 3
    ///   modules, each tile a DetailTier by the exact camera distance to its modules (near 170 m, dithered fade over 130-170 m,
    ///   no hysteresis: see <see cref="MODULE_HYSTERESIS"/>). The tier box is kept in the face frame (u, y, depth; an isometry
    ///   of world space) and the camera is moved into the same frame each Tick, because a world AABB of a tile on the slanted
    ///   faces 210 / 150 reaches up to 21 m in front of the wall and would switch tiles by a phantom distance. The
    ///   module front sits 0.45 m and its glass 0.17 m in front of the wall, so a module fully covers the shader quad behind it:
    ///   the dithered near tier fades over the always-present quad (exact complement not needed, never alpha).
    ///   On the ride the nearest module is 149 m away at the top dock (the city starts 55 m below the eye; the spec's 139 m is
    ///   the horizontal distance to the wall plane), 147 m at s 25 and beyond 170 m after s 145: near-tier coverage is about
    ///   0.53 at the dock, 0.61 at s 25 and 0 from ride second 40 on, as 14.5.5 asks.
    ///   Lit windows match across tiers because the facade shader derives every window from the world position (SHADERS.md 8).
    /// - 200 protruding blocks (ConcreteBoard bodies in four variants and four tones, M_Facade window panels with their own
    ///   hash rows, girder frames for the stand-offs, cold / rare amber beacons), instanced; none in the bridge's look down.
    /// - Eight freight cars of three WIN_COLD glows on face 210 with vertical tracks between the ring levels.
    /// </summary>
    public sealed partial class Beat05_Lift
    {
        static readonly int[] CHUNK_ROWS = { 0, 7, 16, 24 };
        const float MOD_D = 0.45f, MOD_JOINT = 0.15f, MOD_EMBED = 0.1f;
        const float WIN_F = 0.10f, WIN_FW = 0.18f, WIN_C = 0.06f, WIN_R = 0.32f;
        const int TILE_COLS = 2, TILE_ROWS = 3;
        const float MODULE_NEAR = 170f, MODULE_FADE = 40f, MODULE_NEAR_SIMPLIFIED = 100f, MODULE_FADE_SIMPLIFIED = 30f;
        /// <summary>
        /// City tiles use no hysteresis (14.6's generic 20 m does not fit here). The camera never comes closer than 147 m to a
        /// module, so with 20 m the approach curve (110-150 m) would hold the near tier at 1-2 % coverage for the whole ride.
        /// The dithered fade is a continuous function of distance, so it cannot pop or flicker, and coverage is then a pure
        /// function of the camera position: teleported key shots and walked-in frames match.
        /// </summary>
        const float MODULE_HYSTERESIS = 0f;
        const int SIMPLIFIED_MODULES = 400;
        const float TRACK_INWARD = 1.1f, TRACK_HALF = 4.6f, TRACK_SECTION = 0.3f;
        const float FREIGHT_DIAMETER = 1.4f;

        readonly List<Beat05Layout.Freight> _freight = new List<Beat05Layout.Freight>(Beat05Layout.FREIGHT_GROUPS);
        readonly List<Transform> _freightLights = new List<Transform>(Beat05Layout.FREIGHT_GROUPS * Beat05Layout.FREIGHT_PER_GROUP);
        readonly List<MeshRenderer> _freightRenderers = new List<MeshRenderer>(Beat05Layout.FREIGHT_GROUPS * Beat05Layout.FREIGHT_PER_GROUP);
        readonly List<Beat05Layout.Block> _blocks = new List<Beat05Layout.Block>(Beat05Layout.BLOCK_COUNT);
        /// <summary>Index into <see cref="Beat05Layout.CityFaces"/> of each city tier (parallel to _tiers).</summary>
        readonly List<int> _tierFace = new List<int>();
        /// <summary>The camera in each city face's frame (Tick scratch, no allocation).</summary>
        readonly Vector3[] _camFace = new Vector3[3];
        MaterialPropertyBlock _freightMpb;

        /// <summary>Facade materials [face index, module bucket] of the shader tier and the module meshes (null in Graybox).</summary>
        Material[,] _facadeMats, _moduleMats;
        /// <summary>Block window panels per face index, block body concrete per tone.</summary>
        Material[] _blockFronts, _blockConcrete;
        float _facadeEmission0 = 1f, _moduleEmission0 = 1f;
        InstancedBatch _beaconCold, _beaconAmber;

        /// <summary>Lit fraction of the block window panels (twice the wall's: the blocks are the lived-in front of the city).</summary>
        const float BLOCK_LIT_FRACTION = 0.12f;

        // ---------------------------------------------------------------- bridge void (R1-01)
        /// <summary>
        /// While the walk is on the bridge and the ledge (d up to the hall portal) the vertical city is not yet revealed: its
        /// window emission per face is scaled by these factors (face index order of Beat05Layout.CityFaces: 210, 150, 270), so
        /// the walls below read as a few faint far lights (14.5.2 e) and the strips beside the deck in the look straight down
        /// stay void (14.5.2 KS2b: no pixel above 0.12, worst lit window at the 21:9 frame edge, 58 % transmittance, about 0.10).
        /// Face 150 (KS2a's lower left, 450-700 m away) keeps a little more. The beacons and freight cars dim by GLOW_DIM.
        /// The weight ramps back to 0 over the first 22 m of the hall, where the hull hides the shaft (unseen), and stays 0
        /// for the ride. The same weight drives the shader's downward-view attenuation (_AbDownView, AbyssCommon.hlsl).
        /// </summary>
        static readonly float[] BRIDGE_WINDOW_DIM = { 0.012f, 0.03f, 0.012f };
        /// <summary>
        /// Window emission per face once the bridge void has ramped out (hall, ride; same face order). R2-12 (2): face 150 is the
        /// far wall of the whole ride, 280-380 m from the lift line (face 210: 139-240 m, face 270: 209 m), seen at 330-450 m in
        /// KS5a; with the shader's distance falloff alone (x 0.52 at 400 m) its windows still displayed 0.52 against 0.71 on
        /// face 210 and the far wall read as near (lift_r31: face 150 window median 0.515 on a 0.132 facade). At x 0.35 (lift_r32)
        /// they sit at 0.37, 0.24 above their facade against 0.59 on face 210: the 'opposite wall as faint points' of 14.5.5
        /// KS5a; the rest of the far-layer contrast belongs to the Facade shader's distance falloff (FACADE_FALL_L). Both tiers and
        /// the block panels of the face share it (14.10 b crossover).
        /// </summary>
        public static readonly float[] RIDE_WINDOW_SCALE = { 1f, 0.35f, 1f };
        const float BRIDGE_GLOW_DIM = 0.01f, BRIDGE_VOID_RAMP = 22f, DOWNVIEW_DEPTH_SCALE = 150f;
        static readonly int AbDownViewId = Shader.PropertyToID("_AbDownView");
        float _bridgeWeight = -1f, _glowDim = 1f;

        /// <summary>Bridge-void weight at path distance d: 1 up to the hall portal, 0 from 22 m into the hall (and on the lift).</summary>
        public static float BridgeVoidWeight(float d)
        {
            float d0 = AbyssConst.BEAT_START_D[3];
            return 1f - BeatKit.Smoothstep((d - d0) / BRIDGE_VOID_RAMP);
        }

        /// <summary>Set the downward-view global and, when the weight changed, the facade emission and glow dims. Allocation-free.</summary>
        void UpdateBridgeVoid(float d)
        {
            float w = BridgeVoidWeight(d);
            Shader.SetGlobalVector(AbDownViewId, new Vector4(w, AbyssConst.BRIDGE_Y, DOWNVIEW_DEPTH_SCALE, 0f));
            if (Mathf.Abs(w - _bridgeWeight) < 1e-5f) return;
            _bridgeWeight = w;
            _glowDim = Mathf.Lerp(1f, BRIDGE_GLOW_DIM, w);
            if (_facadeMats != null)
            {
                int nf = _facadeMats.GetLength(0), nb = _facadeMats.GetLength(1);
                for (int fi = 0; fi < nf; fi++)
                {
                    float k = Mathf.Lerp(RIDE_WINDOW_SCALE[fi], BRIDGE_WINDOW_DIM[fi], w);
                    for (int b = 0; b < nb; b++)
                    {
                        _facadeMats[fi, b].SetFloat(ShaderIds.EmissionStrength, _facadeEmission0 * k);
                        _moduleMats[fi, b].SetFloat(ShaderIds.EmissionStrength, _moduleEmission0 * k);
                    }
                    if (_blockFronts != null) _blockFronts[fi].SetFloat(ShaderIds.EmissionStrength, _facadeEmission0 * k);
                }
            }
            if (_beaconCold != null) { _beaconCold.Fade = _glowDim; _beaconCold.Visible = _glowDim > 0.002f; }
            if (_beaconAmber != null) { _beaconAmber.Fade = _glowDim; _beaconAmber.Visible = _glowDim > 0.002f; }
            if (_markerGlow != null) { _markerGlow.Fade = _glowDim; _markerGlow.Visible = _glowDim > 0.002f; }
        }

        /// <summary>QA / tests: the current brightness factor of the city beacons, freight cars and guide-rail markers.</summary>
        public float GlowDim => _glowDim;

        /// <summary>QA / tests: current bridge-void weight and window factor of a face index.</summary>
        public float BridgeWeight => _bridgeWeight;

        // ================================================================================================ shader tier

        /// <summary>
        /// Facade materials of both tiers: one variant per city face (its own emission strength, dimmed while the bridge looks
        /// at the void, see <see cref="UpdateBridgeVoid"/>) and per module bucket (tone and lit fraction, Beat05Layout.ModuleBucket).
        /// </summary>
        void MakeCityMaterials(MaterialLibrary mats)
        {
            int nf = Beat05Layout.CityFaces.Length, nb = Beat05Layout.MODULE_BUCKETS;
            _facadeMats = new Material[nf, nb];
            _moduleMats = new Material[nf, nb];
            uint seed = _seed;
            for (int fi = 0; fi < nf; fi++)
            {
                float corr = Beat05Layout.FaceLitCorrection(seed, Beat05Layout.CityFaces[fi]);
                float key = FACADE_KEY_MASK[fi];
                for (int b = 0; b < nb; b++)
                {
                    float tone = Beat05Layout.ModuleToneScale[b], lit = Beat05Layout.ModuleLitScale[b] * corr;
                    // keys carry the seed: the district layout (and so the lit correction) is seeded
                    string k = fi + "_" + b + "_s" + seed;
                    _facadeMats[fi, b] = mats.Variant(MatId.Facade, "b05f" + k, m => ToneFacade(m, tone, lit, key, false));
                    _moduleMats[fi, b] = mats.Variant(MatId.FacadeModule, "b05m" + k, m => ToneFacade(m, tone, lit, key, true));
                }
            }
            _facadeEmission0 = mats[MatId.Facade].GetFloat(ShaderIds.EmissionStrength);
            _moduleEmission0 = mats[MatId.FacadeModule].GetFloat(ShaderIds.EmissionStrength);
            // block window panels (one per face for the bridge dim): same window function, rows shifted by 4000 cells so the
            // hash differs from the wall behind
            _blockFronts = new Material[nf];
            for (int fi = 0; fi < nf; fi++)
                _blockFronts[fi] = mats.Variant(MatId.Facade, "b05block" + fi, m =>
                {
                    m.SetVector("_GridOrigin", new Vector4(AbyssConst.SHAFT_CENTRE.x, AbyssConst.SHAFT_CENTRE.y,
                        AbyssConst.CITY_TOP_Y - 4000f * AbyssConst.CITY_CELL_H, 0f));
                    m.SetFloat(LitFractionId, BLOCK_LIT_FRACTION);
                    CapWindowAmber(m);
                    m.enableInstancing = true;
                });
            _blockConcrete = new Material[Beat05Layout.BlockToneScale.Length];
            for (int t = 0; t < _blockConcrete.Length; t++)
            {
                float tone = Beat05Layout.BlockToneScale[t];
                _blockConcrete[t] = mats.Variant(MatId.ConcreteBoard, "b05blockT" + t, m =>
                {
                    m.SetColor(ShaderIds.BaseColor, ScaleLinear(m.GetColor(ShaderIds.BaseColor), tone));
                    m.enableInstancing = true;
                });
            }
        }

        static readonly int LitFractionId = Shader.PropertyToID("_LitFraction");
        static readonly int KeyLightMaskId = Shader.PropertyToID("_KeyLightMask");
        static readonly int WinAmberId = Shader.PropertyToID("_WinAmber");
        static readonly int EmissiveRangeId = Shader.PropertyToID("_EmissiveRange");

        /// <summary>
        /// Key-light mask of the city facades per face (order of Beat05Layout.CityFaces: 210, 150, 270). R2-12 (1): face 210, the
        /// near wall of the ride (139-240 m), faces the beat-5 Key (N.L 0.25) and its haze skylight, which lifted it to 0.153,
        /// almost the fog value (0.162) and above the far face 150 (0.129, N.L below 0), so the depth read inverted. At 0.35 the
        /// near facade sits at about 0.11-0.12, below the fog, and the farther faces carry the fog value. Physically it stands
        /// for the shadow band under each 6 m rib and 20 m belt (Key pitch -72: 18-62 m shadow below every ring, 60 m apart),
        /// which the 200 m Key shadow distance does not reach at 190 m. Both tiers share it (14.10 b crossover).
        /// </summary>
        static readonly float[] FACADE_KEY_MASK = { 0.35f, 1f, 1f };

        /// <summary>
        /// Lead decision D4 (R2-21): small and distant amber emitters stay at an emissive of at most 2.5 so Neutral tonemapping
        /// keeps them amber (at the 14.2 window peak 5.0 x 1.12 tint the cores clip to cream-white).
        /// </summary>
        public const float AMBER_SMALL_EMISSIVE_MAX = 2.5f;
        /// <summary>
        /// The amber window peak used: D4's 2.5. lift_r21 (2.5, before the Facade shader's hue-safe warm emission) measured the
        /// KS5a amber cores at H 31 / S 0.29 (f03 0.23); lift_r22 (1.6) at H 23 / S 0.38, where the dimmer core lets the cold fog
        /// and aerial blend shift it toward red. With the shader's AbWarmEmission (R2-21, shaders owner) mapping warm emitters
        /// through the inverse Neutral curve, the brighter 2.5 core keeps the AMBER hue best.
        /// </summary>
        public const float AMBER_WINDOW_PEAK = AMBER_SMALL_EMISSIVE_MAX;
        const float WINDOW_TINT = 0.12f;   // Facade shader room tint +/-12 % (14.7.3)

        static void ToneFacade(Material m, float tone, float litScale, float keyMask, bool instanced)
        {
            m.SetColor(ShaderIds.BaseColor, ScaleLinear(m.GetColor(ShaderIds.BaseColor), tone));
            m.SetFloat(LitFractionId, m.GetFloat(LitFractionId) * litScale);
            if (m.HasProperty(KeyLightMaskId)) m.SetFloat(KeyLightMaskId, m.GetFloat(KeyLightMaskId) * keyMask);
            CapWindowAmber(m);
            if (instanced) m.enableInstancing = true;
        }

        /// <summary>
        /// D4: scale the amber window colour (in linear, hue kept) so its peak emission, strength max x (1 + tint) x the brightest
        /// linear channel x the material multiplier, is at most <see cref="AMBER_WINDOW_PEAK"/>. A clamp on the material's current values: a base
        /// material that already meets it is left alone. _WinAmber is a non-HDR colour (gamma, converted to linear for the shader).
        /// </summary>
        public static void CapWindowAmber(Material m)
        {
            if (!m.HasProperty(WinAmberId) || !m.HasProperty(EmissiveRangeId)) return;
            Color g = m.GetColor(WinAmberId);
            Color lin = g.linear;
            float peak = Mathf.Max(lin.r, Mathf.Max(lin.g, lin.b)) * m.GetVector(EmissiveRangeId).y * (1f + WINDOW_TINT)
                * Mathf.Max(m.GetFloat(ShaderIds.EmissionStrength), 0f);   // the ride's multiplier (the bridge only dims it)
            if (peak <= AMBER_WINDOW_PEAK || peak <= 0f) return;
            float k = AMBER_WINDOW_PEAK / peak;
            m.SetColor(WinAmberId, new Color(lin.r * k, lin.g * k, lin.b * k, g.a).gamma);
        }

        /// <summary>Peak amber window emission of a facade material (QA / tests; same formula as <see cref="CapWindowAmber"/>).</summary>
        public static float WindowAmberPeak(Material m)
        {
            if (!m.HasProperty(WinAmberId) || !m.HasProperty(EmissiveRangeId)) return 0f;
            Color lin = m.GetColor(WinAmberId).linear;
            return Mathf.Max(lin.r, Mathf.Max(lin.g, lin.b)) * m.GetVector(EmissiveRangeId).y * (1f + WINDOW_TINT) * m.GetFloat(ShaderIds.EmissionStrength);
        }

        /// <summary>QA / tests: the shader-tier and module materials of a face index and bucket.</summary>
        public Material FacadeMaterial(int faceIndex, int bucket, bool module) => module ? _moduleMats[faceIndex, bucket] : _facadeMats[faceIndex, bucket];

        /// <summary>Scale a gamma-space material colour by a linear albedo factor.</summary>
        static Color ScaleLinear(Color c, float k)
        {
            Color l = c.linear;
            return new Color(l.r * k, l.g * k, l.b * k, c.a).gamma;
        }

        /// <summary>
        /// Shader tier: per face and 200 m chunk (module rows 0-6, 7-15, 16-23) one renderer per module bucket, a quad for every
        /// generated module of that bucket 5 cm in front of the wall (the quads of all buckets tile exactly the module
        /// rectangles; their edge vertices are computed identically, so the tiling is watertight), none for the excluded ones.
        /// </summary>
        void BuildCityFar(BeatContext ctx, MeshBuilder mb)
        {
            Transform city = BeatKit.Group(_geo, "City");
            int cols = AbyssConst.CITY_COLUMNS;
            mb.Clear();
            mb.Color = new Color32(255, 0, 0, 255);   // AO 1, no wear, concrete
            for (int fi = 0; fi < Beat05Layout.CityFaces.Length; fi++)
            {
                int face = Beat05Layout.CityFaces[fi];
                Vector3 nIn = -Beat05Layout.Normal(face);
                for (int ch = 0; ch + 1 < CHUNK_ROWS.Length; ch++)
                {
                    int r0 = CHUNK_ROWS[ch], r1 = CHUNK_ROWS[ch + 1];
                    // chunk origin: one xz per face (SHADERS.md 5), y = the chunk's bottom
                    Vector3 origin = Beat05Layout.WallPoint(face, 0f, Beat05Layout.ModuleTopY(r1), Beat05Layout.FACADE_INSET);
                    for (int bucket = 0; bucket < Beat05Layout.MODULE_BUCKETS; bucket++)
                    {
                        for (int j = r0; j < r1; j++)
                            for (int i = 0; i < cols; i++)
                            {
                                if (!Beat05Layout.ModuleIncluded(face, i, j) || Beat05Layout.ModuleBucket(_seed, face, i, j) != bucket) continue;
                                float u0 = Beat05Layout.ModuleU(i), u1 = Beat05Layout.ModuleU(i + 1);
                                float yt = Beat05Layout.ModuleTopY(j), yb = Beat05Layout.ModuleTopY(j + 1);
                                int a = FarVertex(mb, face, u0, yt, origin, nIn), b = FarVertex(mb, face, u1, yt, origin, nIn);
                                int c = FarVertex(mb, face, u1, yb, origin, nIn), d = FarVertex(mb, face, u0, yb, origin, nIn);
                                BeatKit.Tri(mb, a, b, c);
                                BeatKit.Tri(mb, a, c, d);
                            }
                        MeshRenderer r = BeatKit.Emit(mb, city, "Facade" + face + "_" + ch + "_" + bucket, _facadeMats[fi, bucket], _meshes,
                            ShadowCastingMode.Off, origin, true);
                        if (r != null) _facades.Add(r);
                    }
                }
            }
        }

        static int FarVertex(MeshBuilder mb, int face, float u, float y, Vector3 origin, Vector3 n)
            => mb.Vertex(Beat05Layout.WallPoint(face, u, y, Beat05Layout.FACADE_INSET) - origin, n, new Vector2(u, y));

        // ================================================================================================ near tier

        void BuildCityModules(BeatContext ctx, MeshBuilder mb, bool full)
        {
            mb.Clear();
            BuildModuleMesh(mb);
            _moduleMesh = BeatKit.Bake(mb, "CityModule", _meshes);
            int nf = Beat05Layout.CityFaces.Length, cols = AbyssConst.CITY_COLUMNS, rows = AbyssConst.CITY_ROWS;

            var use = new bool[nf, cols, rows];
            if (full)
            {
                for (int fi = 0; fi < nf; fi++)
                    for (int i = 0; i < cols; i++)
                        for (int j = 0; j < rows; j++)
                            use[fi, i, j] = Beat05Layout.ModuleIncluded(Beat05Layout.CityFaces[fi], i, j);
            }
            else
            {
                // Simplified (14.5.5 Fallbacks): the 400 modules nearest the walked line (lift line and gantry)
                var keys = new List<int>(Beat05Layout.CITY_MODULES);
                var dist = new List<float>(Beat05Layout.CITY_MODULES);
                for (int fi = 0; fi < nf; fi++)
                    for (int i = 0; i < cols; i++)
                        for (int j = 0; j < rows; j++)
                        {
                            int face = Beat05Layout.CityFaces[fi];
                            if (!Beat05Layout.ModuleIncluded(face, i, j)) continue;
                            keys.Add((fi * cols + i) * rows + j);
                            dist.Add(Beat05Layout.PathDistance(Beat05Layout.WallPoint(face, Beat05Layout.ModuleCentreU(i), Beat05Layout.ModuleCentreY(j), 0f)));
                        }
                int[] order = new int[keys.Count];
                float[] dk = dist.ToArray();
                for (int k = 0; k < order.Length; k++) order[k] = k;
                System.Array.Sort(dk, order);
                for (int k = 0; k < Mathf.Min(SIMPLIFIED_MODULES, order.Length); k++)
                {
                    int key = keys[order[k]];
                    use[key / (cols * rows), (key / rows) % cols, key % rows] = true;
                }
            }

            float near = full ? MODULE_NEAR : MODULE_NEAR_SIMPLIFIED, fade = full ? MODULE_FADE : MODULE_FADE_SIMPLIFIED;
            Bounds local = _moduleMesh.bounds;
            var buckets = new InstancedBatch[Beat05Layout.MODULE_BUCKETS];
            for (int fi = 0; fi < nf; fi++)
            {
                int face = Beat05Layout.CityFaces[fi];
                for (int tc = 0; tc * TILE_COLS < cols; tc++)
                    for (int tr = 0; tr * TILE_ROWS < rows; tr++)
                    {
                        // one batch per module bucket (tone / lit fraction material), all switched by the tile's tier
                        for (int k = 0; k < buckets.Length; k++) buckets[k] = null;
                        bool any = false;
                        Bounds fb = default;   // tile box in the face frame (u, y, depth): exact distances, see the class summary
                        for (int i = tc * TILE_COLS; i < Mathf.Min(cols, (tc + 1) * TILE_COLS); i++)
                            for (int j = tr * TILE_ROWS; j < Mathf.Min(rows, (tr + 1) * TILE_ROWS); j++)
                            {
                                if (!use[fi, i, j]) continue;
                                Matrix4x4 m = Beat05Layout.ModuleMatrix(face, i, j);
                                // the module frame is the face frame shifted to the module centre (x = u, y = y, z = depth)
                                var mb3 = new Bounds(local.center + new Vector3(Beat05Layout.ModuleCentreU(i), Beat05Layout.ModuleCentreY(j), 0f), local.size);
                                if (!any) { fb = mb3; any = true; }
                                else fb.Encapsulate(mb3);
                                int bucket = Beat05Layout.ModuleBucket(_seed, face, i, j);
                                if (buckets[bucket] == null)
                                    buckets[bucket] = new InstancedBatch("B05Modules_" + face + "_" + tc + "_" + tr + "_" + bucket, _moduleMesh,
                                        _moduleMats[fi, bucket], ShadowCastingMode.Off, true, 0, 200f);
                                buckets[bucket].Add(m);
                                _modulesBuilt++;
                            }
                        if (!any) continue;
                        var tier = new DetailTier("B05City_" + face + "_" + tc + "_" + tr, fb, near, MODULE_HYSTERESIS, fade);
                        for (int k = 0; k < buckets.Length; k++)
                        {
                            if (buckets[k] == null) continue;
                            Register(buckets[k]);
                            buckets[k].Visible = false;   // the tier switches it on by camera distance
                            tier.AddNear(buckets[k]);
                        }
                        _tiers.Add(tier);
                        _tierFace.Add(fi);
                    }
            }
        }

        /// <summary>
        /// Update the city tiers for a world camera position: the camera goes into each face's frame once (the tier boxes live
        /// there, so DetailTier measures the true distance to the tile's modules). Allocation-free.
        /// </summary>
        void UpdateCityTiers(Vector3 cam)
        {
            if (_tiers.Count == 0) return;   // Graybox: shader quads only
            int nf = Mathf.Min(_camFace.Length, Beat05Layout.CityFaces.Length);
            for (int fi = 0; fi < nf; fi++) _camFace[fi] = Beat05Layout.FaceFrame(Beat05Layout.CityFaces[fi], cam);
            for (int i = 0; i < _tiers.Count; i++) _tiers[i].Update(_camFace[_tierFace[i]]);
        }

        /// <summary>QA / tests: number of city near-tier tiles.</summary>
        public int CityTileCount => _tiers.Count;

        /// <summary>QA / tests: near-tier coverage (0 = shader tier only, 1 = modules only) of city tile i at the last Tick.</summary>
        public float CityTileCoverage(int i) => _tiers[i].Coverage;

        /// <summary>QA / tests: camera distance to the modules of city tile i at the last Tick (m).</summary>
        public float CityTileDistance(int i) => _tiers[i].LastDistance;

        /// <summary>QA / tests: face index (hexagon) of city tile i.</summary>
        public int CityTileFace(int i) => Beat05Layout.CityFaces[_tierFace[i]];

        /// <summary>Number of glass quads in the module mesh (vertex B = 255), measured from the generated mesh.</summary>
        int ModuleWindowCount()
        {
            if (_moduleMesh == null || !_moduleMesh.isReadable) return 0;
            Color32[] c = _moduleMesh.colors32;
            int n = 0;
            for (int i = 0; i < c.Length; i++) if (c[i].b == 255) n++;
            return n / 4;
        }

        static void Q(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n) => BeatKit.Quad(mb, a, b, c, d, n);

        /// <summary>Quad with per-vertex AO (vertex colour R) for the window reveals.</summary>
        static void QAo(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, byte aoA, byte aoB, byte aoC, byte aoD)
        {
            Color32 saved = mb.Color;
            int i0 = Ao(mb, saved, aoA, a, n), i1 = Ao(mb, saved, aoB, b, n), i2 = Ao(mb, saved, aoC, c, n), i3 = Ao(mb, saved, aoD, d, n);
            BeatKit.Tri(mb, i0, i1, i2);
            BeatKit.Tri(mb, i0, i2, i3);
            mb.Color = saved;
        }

        static int Ao(MeshBuilder mb, Color32 baseColor, byte ao, Vector3 p, Vector3 n)
        {
            baseColor.r = ao;
            mb.Color = baseColor;
            return mb.Vertex(p, n, new Vector2(p.x, p.y));
        }

        /// <summary>
        /// One 24 x 21 m facade module in its local frame (x = along the face, y = up, z = into the shaft; origin = module centre
        /// on the wall plane). Front plane at z 0.45 with 48 holes; per window a surround 0.10 proud and 0.18 wide, a 6 cm
        /// 45-degree chamfer down to the 1.2 x 1.6 opening, a 0.32 m reveal and the glass at z 0.17 (vertex B = 255; the cell's
        /// world position gives its lit state). V joints (0.15) and skirts (into the wall) close the module edge. All edges are
        /// shared on one breakpoint lattice, so the mesh has no T-junctions.
        /// </summary>
        static void BuildModuleMesh(MeshBuilder mb)
        {
            float hw = 0.5f * AbyssConst.CITY_MODULE_W, hh = 0.5f * AbyssConst.CITY_MODULE_H;
            float jx = hw - MOD_JOINT, jy = hh - MOD_JOINT;
            int nc = Beat05Layout.CellsPerModuleU, nr = Beat05Layout.CellsPerModuleV;
            float cw = AbyssConst.CITY_CELL_W, chh = AbyssConst.CITY_CELL_H, ww = AbyssConst.CITY_WINDOW_W, wh = AbyssConst.CITY_WINDOW_H;
            float ox = 0.5f * (cw - ww), oy = 0.5f * (chh - wh), e = WIN_C + WIN_FW;
            float zf = MOD_D, zr = MOD_D + WIN_F, zc = zr - WIN_C, zg = zc - WIN_R, zj = MOD_D - MOD_JOINT, zb = -MOD_EMBED;

            var bx = new float[2 * nc + 2];
            var by = new float[2 * nr + 2];
            bx[0] = -jx; bx[bx.Length - 1] = jx;
            by[0] = -jy; by[by.Length - 1] = jy;
            for (int c = 0; c < nc; c++)
            {
                float g0 = -hw + cw * c + ox;
                bx[1 + 2 * c] = g0 - e;
                bx[2 + 2 * c] = g0 + ww + e;
            }
            for (int r = 0; r < nr; r++)
            {
                float g0 = -hh + chh * r + oy;
                by[1 + 2 * r] = g0 - e;
                by[2 + 2 * r] = g0 + wh + e;
            }
            // outer-edge breakpoints: the corners move out to the module edge (mitred joints), the rest stay
            var bxo = (float[])bx.Clone();
            var byo = (float[])by.Clone();
            bxo[0] = -hw; bxo[bxo.Length - 1] = hw;
            byo[0] = -hh; byo[byo.Length - 1] = hh;

            Vector3 fwd = Vector3.forward;
            var concrete = new Color32(255, 0, 0, 255);

            // ---- front plane (every lattice cell except the 48 window-surround holes)
            mb.Color = concrete;
            for (int j = 0; j + 1 < by.Length; j++)
                for (int i = 0; i + 1 < bx.Length; i++)
                {
                    if ((i & 1) == 1 && (j & 1) == 1) continue;
                    Q(mb, new Vector3(bx[i], by[j], zf), new Vector3(bx[i], by[j + 1], zf), new Vector3(bx[i + 1], by[j + 1], zf), new Vector3(bx[i + 1], by[j], zf), fwd);
                }

            // ---- windows
            float s2 = 0.70710678f;
            for (int c = 0; c < nc; c++)
                for (int r = 0; r < nr; r++)
                {
                    float gx0 = -hw + cw * c + ox, gx1 = gx0 + ww, gy0 = -hh + chh * r + oy, gy1 = gy0 + wh;
                    float kx0 = gx0 - WIN_C, kx1 = gx1 + WIN_C, ky0 = gy0 - WIN_C, ky1 = gy1 + WIN_C;
                    float ox0 = gx0 - e, ox1 = gx1 + e, oy0 = gy0 - e, oy1 = gy1 + e;

                    // surround outer sides (front plane -> proud face)
                    mb.Color = new Color32(232, 0, 0, 255);
                    Q(mb, new Vector3(ox0, oy0, zf), new Vector3(ox0, oy1, zf), new Vector3(ox0, oy1, zr), new Vector3(ox0, oy0, zr), Vector3.left);
                    Q(mb, new Vector3(ox1, oy0, zf), new Vector3(ox1, oy0, zr), new Vector3(ox1, oy1, zr), new Vector3(ox1, oy1, zf), Vector3.right);
                    Q(mb, new Vector3(ox0, oy0, zf), new Vector3(ox0, oy0, zr), new Vector3(ox1, oy0, zr), new Vector3(ox1, oy0, zf), Vector3.down);
                    Q(mb, new Vector3(ox0, oy1, zf), new Vector3(ox1, oy1, zf), new Vector3(ox1, oy1, zr), new Vector3(ox0, oy1, zr), Vector3.up);
                    // surround face (mitred ring)
                    mb.Color = concrete;
                    Q(mb, new Vector3(ox0, oy0, zr), new Vector3(ox0, oy1, zr), new Vector3(kx0, ky1, zr), new Vector3(kx0, ky0, zr), fwd);
                    Q(mb, new Vector3(kx1, ky0, zr), new Vector3(kx1, ky1, zr), new Vector3(ox1, oy1, zr), new Vector3(ox1, oy0, zr), fwd);
                    Q(mb, new Vector3(ox0, oy0, zr), new Vector3(kx0, ky0, zr), new Vector3(kx1, ky0, zr), new Vector3(ox1, oy0, zr), fwd);
                    Q(mb, new Vector3(kx0, ky1, zr), new Vector3(ox0, oy1, zr), new Vector3(ox1, oy1, zr), new Vector3(kx1, ky1, zr), fwd);
                    // 45-degree chamfer to the opening
                    mb.Color = new Color32(226, 0, 0, 255);
                    Q(mb, new Vector3(kx0, ky0, zr), new Vector3(kx0, ky1, zr), new Vector3(gx0, gy1, zc), new Vector3(gx0, gy0, zc), new Vector3(s2, 0f, s2));
                    Q(mb, new Vector3(kx1, ky0, zr), new Vector3(gx1, gy0, zc), new Vector3(gx1, gy1, zc), new Vector3(kx1, ky1, zr), new Vector3(-s2, 0f, s2));
                    Q(mb, new Vector3(kx0, ky0, zr), new Vector3(gx0, gy0, zc), new Vector3(gx1, gy0, zc), new Vector3(kx1, ky0, zr), new Vector3(0f, s2, s2));
                    Q(mb, new Vector3(kx0, ky1, zr), new Vector3(kx1, ky1, zr), new Vector3(gx1, gy1, zc), new Vector3(gx0, gy1, zc), new Vector3(0f, -s2, s2));
                    // reveal (AO darkens toward the glass)
                    mb.Color = concrete;
                    const byte aoF = 205, aoB = 150;
                    QAo(mb, new Vector3(gx0, gy0, zc), new Vector3(gx0, gy1, zc), new Vector3(gx0, gy1, zg), new Vector3(gx0, gy0, zg), Vector3.right, aoF, aoF, aoB, aoB);
                    QAo(mb, new Vector3(gx1, gy0, zc), new Vector3(gx1, gy0, zg), new Vector3(gx1, gy1, zg), new Vector3(gx1, gy1, zc), Vector3.left, aoF, aoB, aoB, aoF);
                    QAo(mb, new Vector3(gx0, gy0, zc), new Vector3(gx0, gy0, zg), new Vector3(gx1, gy0, zg), new Vector3(gx1, gy0, zc), Vector3.up, aoF, aoB, aoB, aoF);
                    QAo(mb, new Vector3(gx0, gy1, zc), new Vector3(gx1, gy1, zc), new Vector3(gx1, gy1, zg), new Vector3(gx0, gy1, zg), Vector3.down, aoF, aoF, aoB, aoB);
                    // glass (vertex B = 255: the shader gives it the window state of its cell)
                    mb.Color = new Color32(176, 0, 255, 255);
                    Q(mb, new Vector3(gx0, gy0, zg), new Vector3(gx0, gy1, zg), new Vector3(gx1, gy1, zg), new Vector3(gx1, gy0, zg), fwd);
                }

            // ---- V joints at the module edge (front plane -> edge, 45 degrees) and skirts into the wall
            mb.Color = new Color32(214, 0, 0, 255);
            for (int k = 0; k + 1 < by.Length; k++)
            {
                Q(mb, new Vector3(-jx, by[k], zf), new Vector3(-jx, by[k + 1], zf), new Vector3(-hw, byo[k + 1], zj), new Vector3(-hw, byo[k], zj), new Vector3(-s2, 0f, s2));
                Q(mb, new Vector3(jx, by[k], zf), new Vector3(hw, byo[k], zj), new Vector3(hw, byo[k + 1], zj), new Vector3(jx, by[k + 1], zf), new Vector3(s2, 0f, s2));
            }
            for (int k = 0; k + 1 < bx.Length; k++)
            {
                Q(mb, new Vector3(bx[k], -jy, zf), new Vector3(bxo[k], -hh, zj), new Vector3(bxo[k + 1], -hh, zj), new Vector3(bx[k + 1], -jy, zf), new Vector3(0f, -s2, s2));
                Q(mb, new Vector3(bx[k], jy, zf), new Vector3(bx[k + 1], jy, zf), new Vector3(bxo[k + 1], hh, zj), new Vector3(bxo[k], hh, zj), new Vector3(0f, s2, s2));
            }
            mb.Color = new Color32(170, 0, 0, 255);
            for (int k = 0; k + 1 < byo.Length; k++)
            {
                Q(mb, new Vector3(-hw, byo[k], zj), new Vector3(-hw, byo[k + 1], zj), new Vector3(-hw, byo[k + 1], zb), new Vector3(-hw, byo[k], zb), Vector3.left);
                Q(mb, new Vector3(hw, byo[k], zj), new Vector3(hw, byo[k], zb), new Vector3(hw, byo[k + 1], zb), new Vector3(hw, byo[k + 1], zj), Vector3.right);
            }
            for (int k = 0; k + 1 < bxo.Length; k++)
            {
                Q(mb, new Vector3(bxo[k], -hh, zj), new Vector3(bxo[k], -hh, zb), new Vector3(bxo[k + 1], -hh, zb), new Vector3(bxo[k + 1], -hh, zj), Vector3.down);
                Q(mb, new Vector3(bxo[k], hh, zj), new Vector3(bxo[k + 1], hh, zj), new Vector3(bxo[k + 1], hh, zb), new Vector3(bxo[k], hh, zb), Vector3.up);
            }
            mb.Color = concrete;
        }

        // ================================================================================================ protruding blocks

        void BuildBlocks(BeatContext ctx, MeshBuilder mb)
        {
            Beat05Layout.PlaceBlocks(_seed, _blocks, _freight);
            int nv = Beat05Layout.BLOCK_VARIANTS, nt = _blockConcrete.Length, nf = Beat05Layout.CityFaces.Length;
            var bodies = new Mesh[nv];
            var panels = new Mesh[nv];
            mb.Clear();
            for (int v = 0; v < nv; v++)
            {
                BlockBody(mb, v);
                bodies[v] = BeatKit.Bake(mb, "BlockBody" + VariantLetter(v), _meshes);
                BlockPanel(mb, v);
                panels[v] = BeatKit.Bake(mb, "BlockPanel" + VariantLetter(v), _meshes);
            }
            var frames = new Mesh[Beat05Layout.BLOCK_STANDOFF_STEPS];
            for (int g = 1; g < frames.Length; g++)
            {
                BlockFrame(mb, g * Beat05Layout.BLOCK_STANDOFF_STEP);
                frames[g] = BeatKit.Bake(mb, "BlockFrame" + g, _meshes);
            }
            Mesh glow = GlowQuad();

            // bodies per (variant, tone), window panels per (variant, face: the bridge dim is per face), frames per stand-off
            var bBody = new InstancedBatch[nv, nt];
            var bPanel = new InstancedBatch[nv, nf];
            for (int v = 0; v < nv; v++)
            {
                for (int t = 0; t < nt; t++)
                    bBody[v, t] = new InstancedBatch("B05Blocks" + VariantLetter(v) + t, bodies[v], _blockConcrete[t], ShadowCastingMode.Off, true, 0, 200f);
                for (int fi = 0; fi < nf; fi++)
                    bPanel[v, fi] = new InstancedBatch("B05BlockPanels" + VariantLetter(v) + fi, panels[v], _blockFronts[fi], ShadowCastingMode.Off, true, 0, 200f);
            }
            // cantilevers in the block's own concrete tone (R2-27: one batch per stand-off and tone)
            var bF = new InstancedBatch[frames.Length, nt];
            for (int g = 1; g < frames.Length; g++)
                for (int t = 0; t < nt; t++)
                    bF[g, t] = new InstancedBatch("B05BlockFrames" + g + "_" + t, frames[g], _blockConcrete[t], ShadowCastingMode.Off, true, 0, 200f);
            _beaconCold = new InstancedBatch("B05BlockBeacons", glow, _glowCold, ShadowCastingMode.Off, false, 0, 200f);
            _beaconAmber = new InstancedBatch("B05BlockBeaconsAmber", glow, _glowAmber, ShadowCastingMode.Off, false, 0, 200f);
            var beaconScale = new Vector3(0.6f, 0.6f, 0.6f);
            for (int i = 0; i < _blocks.Count; i++)
            {
                Beat05Layout.Block b = _blocks[i];
                Matrix4x4 m = Beat05Layout.BlockMatrix(b);
                int fi = System.Array.IndexOf(Beat05Layout.CityFaces, b.Face);
                bBody[b.Variant, b.Tone].Add(m);
                bPanel[b.Variant, fi].Add(m);
                if (b.Standoff > 0) bF[b.Standoff, b.Tone].Add(Beat05Layout.BlockFrameMatrix(b));
                if (b.Beacon)
                    (b.Amber ? _beaconAmber : _beaconCold).Add(Matrix4x4.TRS(m.MultiplyPoint3x4(Beat05Layout.BeaconLocal(b.Variant)), Quaternion.identity, beaconScale));
            }
            for (int v = 0; v < nv; v++)
            {
                for (int t = 0; t < nt; t++) RegisterIfAny(bBody[v, t]);
                for (int fi = 0; fi < nf; fi++) RegisterIfAny(bPanel[v, fi]);
            }
            for (int g = 1; g < bF.GetLength(0); g++)
                for (int t = 0; t < nt; t++) RegisterIfAny(bF[g, t]);
            RegisterIfAny(_beaconCold); RegisterIfAny(_beaconAmber);
            _blocksBuilt = _blocks.Count;
        }

        static string VariantLetter(int v) => v == 0 ? "A" : v == 1 ? "B" : v == 2 ? "C" : "D";

        void RegisterIfAny(InstancedBatch b)
        {
            if (b.Count > 0) Register(b);
        }

        /// <summary>
        /// Block body in its frame (x along the face, y up from the base, z into the shaft; back plane z = 0 - 0.3 embed). Four
        /// variants (R1-01: no single repeated silhouette): 0 banded with roof plant and mast, 1 finned with a crown slab, 2 set
        /// back above the 7th panel row (upper front 6 m behind, sides 1 m in, a plant box on the terrace), 3 stepped (full block
        /// to the 6th panel row, a 7.5 m tower on the -x side to the top, its front 0.4 m in, a plant box on the lower roof).
        /// Stacked parts overlap 5 cm and never share a face plane.
        /// </summary>
        static void BlockBody(MeshBuilder mb, int variant)
        {
            float w = Beat05Layout.BLOCK_W, h = Beat05Layout.BLOCK_H, d = Beat05Layout.BLOCK_D, em = Beat05Layout.BLOCK_EMBED;
            float rowY = Beat05Layout.BLOCK_PANEL_Y0, cell = AbyssConst.CITY_CELL_H;
            mb.Color = new Color32(255, 0, 0, 255);
            if (variant == 2)
            {
                float hs = rowY + Beat05Layout.SETBACK_ROWS * cell, ws = w - 2f * Beat05Layout.SETBACK_SIDE, ds = d - Beat05Layout.SETBACK_FRONT;
                MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * hs, 0.5f * (d - em)), new Vector3(w, hs, d + em), 0.08f);
                float y0 = hs - 0.05f, z0 = -em + 0.02f;
                MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * (y0 + h), 0.5f * (z0 + ds)), new Vector3(ws, h - y0, ds - z0), 0.08f);
                MeshKit.ChamferBox(mb, new Vector3(-2f, hs + 1.15f, d - 3.2f), new Vector3(3.5f, 2.4f, 3.2f), 0.05f);
                return;
            }
            if (variant == 3)
            {
                float hs = rowY + Beat05Layout.STEP_ROWS * cell;
                MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * hs, 0.5f * (d - em)), new Vector3(w, hs, d + em), 0.08f);
                float x0 = Beat05Layout.STEP_TOWER_X0, x1 = Beat05Layout.STEP_TOWER_X1, y0 = hs - 0.05f, z0 = -em + 0.02f;
                float z1 = d - Beat05Layout.STEP_TOWER_RECESS;
                MeshKit.ChamferBox(mb, new Vector3(0.5f * (x0 + x1), 0.5f * (y0 + h), 0.5f * (z0 + z1)), new Vector3(x1 - x0, h - y0, z1 - z0), 0.08f);
                MeshKit.ChamferBox(mb, new Vector3(3.9f, hs + 1.15f, 0.55f * d), new Vector3(3f, 2.4f, 5f), 0.05f);
                return;
            }
            MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * h, 0.5f * (d - em)), new Vector3(w, h, d + em), 0.08f);
            if (variant == 0)
            {
                // two floor bands at cell boundaries (clear of the windows), roof plant and a mast
                for (int k = 1; k <= 2; k++)
                {
                    float y = rowY + 4f * k * cell;
                    MeshKit.ChamferBox(mb, new Vector3(0f, y, 0.5f * (0.4f + d + 0.5f)), new Vector3(w + 1f, 0.5f, d + 0.1f), 0.04f);
                }
                MeshKit.ChamferBox(mb, new Vector3(-1f, h + 1.2f, 7f), new Vector3(4f, 2.5f, 6f), 0.05f);
                MeshKit.ChamferBox(mb, new Vector3(3.5f, h + 0.85f, 13f), new Vector3(3f, 1.8f, 4f), 0.05f);
                MeshKit.Cylinder(mb, new Vector3(3f, h - 0.1f, 3f), new Vector3(3f, h + 6f, 3f), 0.15f, 8, true, false);
            }
            else
            {
                // vertical fins on the cell boundaries beside the middle window column, and a crown slab
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferBox(mb, new Vector3(s * 0.5f * AbyssConst.CITY_CELL_W, 0.5f * h, d + 0.35f), new Vector3(0.4f, h - 0.6f, 0.9f), 0.03f);
                MeshKit.ChamferBox(mb, new Vector3(0f, h + 0.5f, 0.5f * (d + 0.4f - 0.4f)), new Vector3(w + 0.8f, 1.2f, d + 0.8f), 0.05f);
            }
        }

        /// <summary>
        /// Window panels on the block front, cell aligned, 6 cm proud of the face they sit on: 3 x 11 cells (9 x 38.5 m) on the
        /// front of variants 0 and 1; variant 2: rows 0-6 on the front and rows 7-10 on the set-back front; variant 3: rows 0-5
        /// on the front and rows 6-10, the two -x columns, on the tower front.
        /// </summary>
        static void BlockPanel(MeshBuilder mb, int variant)
        {
            float cw = AbyssConst.CITY_CELL_W, ch = AbyssConst.CITY_CELL_H, y0 = Beat05Layout.BLOCK_PANEL_Y0;
            float hx = 0.5f * Beat05Layout.BLOCK_PANEL_COLS * cw, d = Beat05Layout.BLOCK_D;
            int rows = Beat05Layout.BLOCK_PANEL_ROWS;
            mb.Color = new Color32(255, 0, 0, 255);
            if (variant == 2)
            {
                int k = Beat05Layout.SETBACK_ROWS;
                PanelQuad(mb, -hx, hx, y0, y0 + k * ch, d + 0.06f);
                PanelQuad(mb, -hx, hx, y0 + k * ch, y0 + rows * ch, d - Beat05Layout.SETBACK_FRONT + 0.06f);
            }
            else if (variant == 3)
            {
                int k = Beat05Layout.STEP_ROWS;
                PanelQuad(mb, -hx, hx, y0, y0 + k * ch, d + 0.06f);
                PanelQuad(mb, -hx, -hx + 2f * cw, y0 + k * ch, y0 + rows * ch, d - Beat05Layout.STEP_TOWER_RECESS + 0.06f);
            }
            else PanelQuad(mb, -hx, hx, y0, y0 + rows * ch, d + 0.06f);
        }

        static void PanelQuad(MeshBuilder mb, float x0, float x1, float y0, float y1, float z)
            => BeatKit.Quad(mb, new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), Vector3.forward);

        /// <summary>Cantilever beams of a stand-off (R2-27), block-frame local: x across, y up from the block base, z out of the wall.</summary>
        const float CANT_X = 3.4f, CANT_W = 2.0f, CANT_Y0 = -0.25f, CANT_Y1 = 6.0f;

        /// <summary>
        /// Stand-off structure of a block (R2-27): one pair of deep concrete cantilevers (2.0 m wide, 6.25 m deep, 6.8 m apart)
        /// under the block's back, their soffits 0.25 m below the block base, 0.6 m into the wall and 0.9 m into the body, in the
        /// block's own concrete tone (lift_r21 also had a lighter upper pair at 16.5-19 m: four bars per block still read as
        /// sticks from below, one deep pair reads as two corbels). The r1 frames (four 1.4 m steel I-beams per block with splice plates and knee braces) read
        /// from below as bundles of thin black sticks, "porcupine quills", and made the identical boxes look suspended.
        /// Everything stays inside the bridge-view sample envelope of <see cref="Beat05Layout.BlockBridgeTransmittance"/>.
        /// </summary>
        static void BlockFrame(MeshBuilder mb, float standoff)
        {
            float z0 = -0.6f, z1 = standoff + 0.6f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * CANT_X;
                MeshKit.ChamferBox(mb, new Vector3(x, 0.5f * (CANT_Y0 + CANT_Y1), 0.5f * (z0 + z1)), new Vector3(CANT_W, CANT_Y1 - CANT_Y0, z1 - z0), 0.06f);
            }
        }

        /// <summary>Union of the face-210 blocks within the KS5a view cone (+/-34 degrees across, +/-24 up and down), if any.</summary>
        bool TryBlocksInView(Vector3 eye, float yaw, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Vector3 f = AbyssConst.Dir(yaw, 0f);
            for (int i = 0; i < _blocks.Count; i++)
            {
                Beat05Layout.Block b = _blocks[i];
                if (b.Face != Beat05Layout.FACE_210) continue;
                Vector3 c = Beat05Layout.BlockMatrix(b).MultiplyPoint3x4(new Vector3(0f, 0.5f * Beat05Layout.BLOCK_H, 0.5f * Beat05Layout.BLOCK_D));
                Vector3 v = c - eye;
                float fwd = Vector3.Dot(v, f);
                if (fwd < 50f) continue;
                Vector3 hz = v - Vector3.up * v.y;
                float across = Mathf.Abs(Vector3.SignedAngle(new Vector3(f.x, 0f, f.z), hz, Vector3.up));
                float elev = Mathf.Atan2(v.y, hz.magnitude) * Mathf.Rad2Deg - 3f;
                if (across > 34f || Mathf.Abs(elev) > 24f) continue;
                var bb = new Bounds(c, new Vector3(Beat05Layout.BLOCK_W, Beat05Layout.BLOCK_H, Beat05Layout.BLOCK_D));
                if (!any) { bounds = bb; any = true; } else bounds.Encapsulate(bb);
            }
            return any;
        }

        // ================================================================================================ freight lights

        void BuildFreight(BeatContext ctx, MeshBuilder mb)
        {
            if (_freight.Count == 0) Beat05Layout.PlaceFreight(_seed, _freight);
            Transform root = BeatKit.Group(_geo, "Freight");
            int face = Beat05Layout.FACE_210;

            // tracks: two vertical members per car, interrupted by the ring levels (the cars pass through slots in the ribs)
            Vector3 origin = Beat05Layout.WallPoint(face, 0f, 0.5f * (AbyssConst.CITY_TOP_Y + AbyssConst.CITY_BOTTOM_Y), 0f);
            Vector3 n = Beat05Layout.Normal(face);
            mb.Clear();
            for (int g = 0; g < _freight.Count; g++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float u = _freight[g].U + s * TRACK_HALF;
                    // the dock group's track runs on up its longer loop (R2-11), the others stop at the city top
                    float top = Mathf.Max(AbyssConst.CITY_TOP_Y, Beat05Layout.FreightTopY(_freight[g]));
                    for (int k = 0; k + 1 < AbyssConst.RIB_LEVELS; k++)
                    {
                        float hiRing = AbyssConst.RingY(k), loRing = AbyssConst.RingY(k + 1);
                        float hi = Mathf.Min(top, hiRing - 0.5f * (AbyssConst.RingIsBelt(k) ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT) + 0.2f);
                        float lo = Mathf.Max(AbyssConst.CITY_BOTTOM_Y, loRing + 0.5f * (AbyssConst.RingIsBelt(k + 1) ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT) - 0.2f);
                        if (hi - lo < 1f) continue;
                        Vector3 a = Beat05Layout.WallPoint(face, u, lo, TRACK_INWARD) - origin, b = Beat05Layout.WallPoint(face, u, hi, TRACK_INWARD) - origin;
                        MeshKit.ChamferMember(mb, a, b, TRACK_SECTION, TRACK_SECTION, 0.01f, n, true);
                    }
                }
            BeatKit.Emit(mb, root, "FreightTracks", _steelThin, _meshes, ShadowCastingMode.Off, origin, true);

            // the cars: three WIN_COLD glows each (moved in Tick from the scene clock; faded at the loop ends)
            Mesh glow = GlowQuad();
            _freightMpb = new MaterialPropertyBlock();
            var scale = new Vector3(FREIGHT_DIAMETER, FREIGHT_DIAMETER, FREIGHT_DIAMETER);
            for (int g = 0; g < _freight.Count; g++)
                for (int k = 0; k < Beat05Layout.FREIGHT_PER_GROUP; k++)
                {
                    MeshRenderer r = BeatKit.Renderer(root, "Freight" + g + "_" + k, glow, _glowFreight, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                    r.receiveShadows = false;
                    r.transform.localScale = scale;
                    _freightLights.Add(r.transform);
                    _freightRenderers.Add(r);
                }
        }

        /// <summary>Freight car positions from the scene clock (double, wrapped every loop: 480 m, the dock group 644 m); zero allocation.</summary>
        void UpdateFreight(double t)
        {
            int n = _freight.Count, per = Beat05Layout.FREIGHT_PER_GROUP;
            if (n == 0 || _freightMpb == null || _freightLights.Count < n * per) return;
            float fadeLen = Beat05Layout.FREIGHT_FADE;
            for (int g = 0; g < n; g++)
            {
                Beat05Layout.Freight f = _freight[g];
                float loop = f.Loop;
                double off = Beat05Layout.FreightOffset(f, t);
                float y = AbyssConst.CITY_BOTTOM_Y + (float)off;
                float fade = BeatKit.Smoothstep((float)off / fadeLen) * BeatKit.Smoothstep((float)(loop - off) / fadeLen) * _glowDim;
                bool vis = fade > 0.002f;
                _freightMpb.SetFloat(ShaderIds.AbFade, fade);
                for (int k = 0; k < per; k++)
                {
                    int idx = g * per + k;
                    _freightLights[idx].localPosition = Beat05Layout.WallPoint(Beat05Layout.FACE_210, f.U + (k - 1) * Beat05Layout.FREIGHT_SPACING, y,
                        Beat05Layout.FREIGHT_INWARD);
                    MeshRenderer r = _freightRenderers[idx];
                    if (r.enabled != vis) r.enabled = vis;
                    r.SetPropertyBlock(_freightMpb);
                }
            }
        }
    }
}
