using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 6 - cyclopean fan and gantry (14.5.6, d 469.5-668.5).
    ///
    /// Full tier (Beat06_Fan.Rotor.cs, Beat06_Fan.Gantry.cs, FanBlade.cs): the 60 m bore lining (R 150, M_ConcreteBoard),
    /// the concrete housing ring R 150 / 138 (z 24-59) with a trim collar R 146-151.5 standing 2 m proud of the near flat
    /// (inside every Beat02Rings bore gap), the static stator
    /// (tail nacelle, 12 streamlined struts behind the rotor, the two stay anchors on the housing lip), the rotor (hub drum
    /// R 40 with a spinner nose, 12 lofted blades: chord c(R) = 14 + 16 (R - 40) / 98, pitch p(R) = acos(0.2094 R / c(R)),
    /// thickness 3 m, every section wrapped on its own cylinder so each blade projects exactly 12 degrees of arc and leaves an
    /// 18 degree gap; root collars), the 12 analytic beam wedges riding on the rotor with the 12 key-shadow prisms between them
    /// (BeamWedge mode 3: the blade shadows carved out of the fog's key lobe, R1-10), the bore card, the 12 lighthouse patches
    /// on the far wall, and the 3 x 199 gantry (kerb stringers, grating inserts between solid landing plates, two Warren
    /// trusses 4 m deep of 33 bays with bottom bracing, 8 portal frames 6 x 8 m with outriggers, knee braces, lamp housings,
    /// two stay cables each to the anchors at (x = -45 +/- 6, y = -455), pale-painted rails fading into the deck tone over
    /// 30-60 m (14.7.9), two O 0.4 service pipes, lamp points).
    /// Simplified tier (Fallbacks): the same structure with static-lit blades (no blade shadows), 6 beam wedges, no wall
    /// patches and no near-detail tiers. Graybox tier: flat spoked disc, no beams (unchanged).
    ///
    /// Persistent skeleton (14.6): housing, hub, blades (turning on the global clock), bore card, gantry deck, portal frames,
    /// trusses, rails, cables and lamp points stay active for the whole walk; only near detail (bolts, floor beams, grating
    /// support bars, pipe hangers, base plates) switches by camera distance through DetailTier (150 m, 20 m hysteresis,
    /// dithered cross-fade). The rotor turns clockwise seen from +Z by theta = 2 pi t / 40 from SceneClock (no start event)
    /// and publishes _AbFanTheta (owner Beat06).
    ///
    /// Colliders are final: the gantry floor from exactly z = 259 (the deck's -Z edge at the bottom dock: the player leaves the
    /// kinematic deck at local z -10.25 inside the 1.15 m gate corridor, i.e. onto this floor) to z = 60, edge walls at
    /// x = -45 +/- 1.5 (walkable 3.0) and a safety volume. No collider reaches past z = 259 into the gate corridor.
    /// </summary>
    public sealed partial class Beat06_Fan : BeatBase
    {
        public override int Index => 6;

        // ---------------------------------------------------------------- beat-specific constants (graybox and shared)
        const float HOUSING_DEPTH = 10f, HUB_DEPTH = 10f, BLADE_T = 3f;
        const float BLADE_ARC_DEG = 12f;                      // 12 blades x 12 degrees + 12 gaps x 18 degrees
        const float BLADE_ROOT_EMBED = 0.5f;                  // blade roots start inside the hub (no seam)
        const float BORE_OVERLAP = 0.05f;                      // lining runs 5 cm past both wall planes (no light leak)
        const float BORE_CARD_Z = 1f, BORE_CARD_DIM = 0.35f, BORE_CARD_RAMP = 20f;

        // ---------------------------------------------------------------- bore card (14.2 / 14.6; R2-13 contre-jour)
        // The card is the outside light seen through the blade gaps. f03 drew FogSky(view, Exterior) at strength 1: a flat
        // card (std 0.01) at 0.38-0.47 in KS5b, below the 14.2 'luma 0.55-0.75 at full strength', so the fan read as a cut-out
        // against paper. Now: the GradientCard's vertical-gradient mode on a ring-subdivided disc whose UV0.y carries a radial
        // and vertical weight (BoreCardWeight: brightest behind the hub and toward the top, darkest at the lower rim) between a
        // dim OUT_HORIZ rim and a pale cold-white core, so light seems to pour through the gaps. 14.6: x 0.35 until ride
        // second 0, full over ride seconds 0-20 (BORE_CARD_FULL: KS5b gaps about 0.55-0.70 through 22 % of beat-5 haze); in the
        // fan chamber (KS6, mean band 0.09-0.22 at 0.211 in f03) the card returns to about its f03 level (BORE_CARD_CHAMBER of
        // full), blended over ride seconds 98-108 while the ride gaze looks down (D1: pitch -38 over 90-110 s) and the card is
        // out of frame.
        // lead_b01 at 2.3: KS5b gaps 0.62-0.74 (rim to hub), mean 0.200; 2.1 leaves mean room for the ride rays (R2-13, D8
        // band 0.09-0.22) and keeps the gaps inside 0.55-0.75. The chamber level stays at the f03 radiance (2.1 x 0.35).
        const float BORE_CARD_FULL = 2.1f, BORE_CARD_CHAMBER = 0.35f, BORE_CARD_CH0 = 98f, BORE_CARD_CH1 = 108f;
        const float BORE_CARD_W_RADIAL = 0.6f, BORE_CARD_W_VERTICAL = 0.4f, BORE_CARD_RIM_SCALE = 0.55f;
        static readonly float[] BoreCardRings = { 20f, 40f, 55f, 70f, 85f, 100f, 115f, 130f, 140f, 150f };
        /// <summary>Linear core colour of the bore card (UV0.y = 1): a pale cold white, luma about 0.55.</summary>
        public static readonly Color BoreCardCore = new Color(0.50f, 0.555f, 0.61f, 1f);
        /// <summary>Linear rim colour of the bore card (UV0.y = 0): OUT_HORIZ x 0.55.</summary>
        public static Color BoreCardRim
        {
            get
            {
                Color c = AbyssPalette.OUT_HORIZ.linear * BORE_CARD_RIM_SCALE;
                c.a = 1f;
                return c;
            }
        }

        /// <summary>Gradient weight of the bore card at radius r and height y above the fan axis (0 = rim colour, 1 = core).</summary>
        public static float BoreCardWeight(float r, float y)
        {
            float R = AbyssConst.FAN_R_OUT;
            float q = Mathf.Clamp01(r / R);
            float wv = 0.5f + 0.5f * Mathf.Clamp(y / R, -1f, 1f);
            return Mathf.Clamp01(BORE_CARD_W_RADIAL * (1f - q * q) + BORE_CARD_W_VERTICAL * wv);
        }

        /// <summary>Bore-card _EmissionStrength for the ride second (14.6 ramp, then the chamber level from ride second 108).</summary>
        public static float BoreCardStrength(float rideSeconds)
        {
            if (rideSeconds < 0f) return BORE_CARD_FULL * BORE_CARD_DIM;
            float s = Mathf.Lerp(BORE_CARD_DIM, 1f, BeatKit.Smootherstep(rideSeconds / BORE_CARD_RAMP));
            float ch = rideSeconds <= BORE_CARD_CH0 ? 0f : rideSeconds >= BORE_CARD_CH1 ? 1f
                : BeatKit.Smootherstep((rideSeconds - BORE_CARD_CH0) / (BORE_CARD_CH1 - BORE_CARD_CH0));
            return BORE_CARD_FULL * s * Mathf.Lerp(1f, BORE_CARD_CHAMBER, ch);
        }

        // View factor (R2-06 lead note, KS2b): f03's card drew FogSky(view, Exterior), so a view looking DOWN through the bore
        // (bridge, ledge, hall, top of the ride) saw the dark exterior below the horizon (FOG_DOWN #0A1015) and the card was
        // invisible from above. The graded card is view-independent: from the bridge (sin elevation to the card about -0.9)
        // its bright top showed through the upper blade gaps as a light-grey wedge (global_r31 KS2b 16:10 px 480-503 x
        // 882-892, 0.16-0.23 on 0.03; the side strips allow 0.12 and 0.03 edges). The card keeps BORE_CARD_VIEW_FLOOR of its
        // strength while the eye looks down at it steeply (sin elevation <= -0.7) and reaches full strength at -0.35, which
        // the lift passes on its descent (KS5a at ride 60 s: -0.25; KS5b: 0; the gantry looks up at it), so the reveal from
        // the lift follows the descent smoothly (about 0.016 / s in sin elevation).
        const float BORE_CARD_VIEW_S0 = -0.7f, BORE_CARD_VIEW_S1 = -0.35f, BORE_CARD_VIEW_FLOOR = 0.05f;

        /// <summary>Bore-card view factor for an eye position: BORE_CARD_VIEW_FLOOR looking steeply down at the card, 1 from level.</summary>
        public static float BoreCardViewFactor(Vector3 eye)
        {
            Vector3 fc = AbyssConst.FAN_CENTRE;
            Vector3 d = new Vector3(fc.x, fc.y, BORE_CARD_Z) - eye;
            float len = d.magnitude;
            if (len < 1e-3f) return 1f;
            float sinElev = d.y / len;
            float t = BeatKit.Smootherstep(Mathf.Clamp01((sinElev - BORE_CARD_VIEW_S0) / (BORE_CARD_VIEW_S1 - BORE_CARD_VIEW_S0)));
            return Mathf.Lerp(BORE_CARD_VIEW_FLOOR, 1f, t);
        }

        /// <summary>Bore-card _EmissionStrength for the ride second and the eye position (ride ramp x view factor).</summary>
        public static float BoreCardStrength(float rideSeconds, Vector3 eye) => BoreCardStrength(rideSeconds) * BoreCardViewFactor(eye);
        const float DECK_T = 0.3f, RAIL_TUBE = 0.06f, RAIL_POST_SPACING = 2.5f;
        const float TRUSS_DEPTH = 4f, TRUSS_MEMBER = 0.3f;
        const int TRUSS_BAYS = 33;
        const int PORTAL_COUNT = 8;
        const float PORTAL_Z0 = 247f, PORTAL_SPACING = 25f, PORTAL_W = 6f, PORTAL_H = 8f, PORTAL_SECTION = 0.4f;
        const float COLD_I = 25f, COLD_RANGE = 14f, AMBER_I = 5f, AMBER_RANGE = 9f;

        /// <summary>Blade-gap centre of gap 0 in the rotor frame (blade k is centred at 30 k degrees, gap k at 15 + 30 k).</summary>
        public const float GAP_PHASE_DEG = 180f / AbyssConst.FAN_BLADES;

        readonly List<Mesh> _meshes = new List<Mesh>();
        Transform _geo, _rotor;
        MeshRenderer _housing, _collar, _hub, _deck, _boreCard, _blade, _beamR;
        Material _boreMat, _beamMat, _patchMat, _rotorMat;
        float _boreStrength = -1f, _beamHaze = -1f, _beamDensity = -1f, _patchStrength = -1f, _rotorKey = -1f;
        bool _editMode;
        BeatTier _tier = BeatTier.Full;
        float _bladeArcDeg, _tipR, _gapMinDeg = float.NaN, _gapMaxDeg = float.NaN;
        readonly float[] _gapAtDeg = { float.NaN, float.NaN, float.NaN };   // measured free gap at R 48, 90, 130
        int _blades, _beamCount;
        readonly Bounds[] _portalBounds = new Bounds[PORTAL_COUNT];
        DetailTier[] _tiers = System.Array.Empty<DetailTier>();

        // measured portal geometry (full / simplified tiers)
        float _portalPostSpan, _portalHeight;

        /// <summary>Fan rotation in degrees (clockwise seen from +Z = increasing from +X toward +Y) at scene time t.</summary>
        public static double ThetaDeg(double t)
        {
            double tw = t - AbyssConst.FAN_PERIOD * System.Math.Floor(t / AbyssConst.FAN_PERIOD);
            return 360.0 * tw / AbyssConst.FAN_PERIOD;
        }

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat06");
            _editMode = ctx.EditMode;
            _tier = ctx.Tier;
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            _boreMat = _beamMat = _patchMat = _rotorMat = null;
            _boreStrength = _beamHaze = _beamDensity = _patchStrength = _rotorKey = -1f;
            BuildColliders(ctx);
            if (ctx.Tier == BeatTier.Graybox) BuildGraybox(ctx);
            else BuildFull(ctx);
            BuildLights(ctx);
            ApplyFan(SceneClock.Time);
            ApplyBoreCard(GameState.RideSeconds, GameState.CameraPos);
            ApplyBeamHaze(GameState.RideSeconds);
            ApplyRotorKey(GameState.E);
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            float x = AbyssConst.W[14].x, y = AbyssConst.GANTRY_Y, hw = AbyssConst.GANTRY_W * 0.5f;
            float z0 = AbyssConst.GANTRY_Z0, z1 = AbyssConst.GANTRY_Z1;
            CorridorKit.Floor(c, "GantryFloor", new Vector3(x, y, (z0 + z1) * 0.5f), new Vector2(AbyssConst.GANTRY_W + 1f, z0 - z1));
            BeatKit.Wall(c, "GantryEdgeW", new Vector2(x - hw, z0), new Vector2(x - hw, z1), y - 1f, 4f, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "GantryEdgeE", new Vector2(x + hw, z0), new Vector2(x + hw, z1), y - 1f, 4f, new Vector2(1f, 0f));
            CorridorKit.SafetyBelow(c, "SafetyGantry", new Vector3(x, y, (z0 + z1) * 0.5f), new Vector2(AbyssConst.GANTRY_W, z0 - z1));
        }

        /// <summary>Full and Simplified tiers (Fallbacks: Simplified = static-lit blades, 6 wedges, no near detail).</summary>
        void BuildFull(BeatContext ctx)
        {
            bool full = ctx.Tier == BeatTier.Full;
            var mb = new MeshBuilder();
            var tiers = new List<DetailTier>(8);
            BuildFanFull(ctx, mb, full, tiers);
            BuildGantryFull(ctx, mb, full, tiers);
            _tiers = tiers.ToArray();
        }

        void BuildGraybox(BeatContext ctx)
        {
            var mb = new MeshBuilder();
            BuildFan(ctx, mb);
            BuildGantry(ctx, mb);
        }

        // ---------------------------------------------------------------- graybox fan (flat spoked disc, no beams)

        void BuildFan(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Vector3 fc = AbyssConst.FAN_CENTRE;
            Transform fan = BeatKit.Group(_geo, "Fan");
            Quaternion yToZ = Quaternion.Euler(90f, 0f, 0f);   // lathe axis (local +Y) -> world +Z
            int seg = BeatKit.BORE_SEGMENTS;
            float rOut = AbyssConst.FAN_R_OUT, rIn = AbyssConst.FAN_R_INNER;

            // bore lining through the web, seen from inside
            var profile = new List<Vector2> { new Vector2(rOut, -BORE_OVERLAP), new Vector2(rOut, AbyssConst.WEB + BORE_OVERLAP) };
            MeshKit.Lathe(mb, profile, seg, MeshKit.HardEdgeAngleDeg, 0f, 360f, false, true);
            Mesh bore = BeatKit.Bake(mb, "BoreLining", _meshes);
            BeatKit.Renderer(fan, "BoreLining", bore, mats[MatId.ConcreteBoard], new Vector3(fc.x, fc.y, AbyssConst.W[16].z), yToZ, ShadowCastingMode.On, true);

            // housing ring R 150 / 138 (its outer face lies on the lining and faces away from every viewer)
            profile.Clear();
            float hd = HOUSING_DEPTH * 0.5f;
            profile.Add(new Vector2(rIn, -hd)); profile.Add(new Vector2(rOut, -hd)); profile.Add(new Vector2(rOut, hd));
            profile.Add(new Vector2(rIn, hd)); profile.Add(new Vector2(rIn, -hd));
            MeshKit.Lathe(mb, profile, seg);
            Mesh housing = BeatKit.Bake(mb, "FanHousing", _meshes);
            _housing = BeatKit.Renderer(fan, "FanHousing", housing, mats[MatId.ConcreteBoard], fc, yToZ, ShadowCastingMode.On, true);

            // rotor: hub drum + 12 flat blade sectors, authored with the fan axis on local +Z
            _rotor = BeatKit.Group(fan, "Rotor", fc, Quaternion.identity);
            mb.Push(Matrix4x4.Rotate(yToZ));
            profile.Clear();
            float hh = HUB_DEPTH * 0.5f, hr = AbyssConst.FAN_R_HUB;
            profile.Add(new Vector2(0f, -hh)); profile.Add(new Vector2(hr, -hh)); profile.Add(new Vector2(hr, hh)); profile.Add(new Vector2(0f, hh));
            MeshKit.Lathe(mb, profile, 64);
            mb.Pop();
            Mesh hub = BeatKit.Bake(mb, "FanHub", _meshes);
            _hub = BeatKit.Renderer(_rotor, "Hub", hub, mats[MatId.SteelWeathered], Vector3.zero, Quaternion.identity, ShadowCastingMode.On, true);

            mb.Push(Matrix4x4.Rotate(yToZ));
            float half = 0.5f * BLADE_ARC_DEG * Mathf.Deg2Rad;
            _blades = 0;
            for (int k = 0; k < AbyssConst.FAN_BLADES; k++)
            {
                // builder angle phi runs from local +X toward local +Z = rotor -Y, so a rotor angle theta is phi = -theta
                float theta = k * 2f * Mathf.PI / AbyssConst.FAN_BLADES;
                int first = mb.VertexCount;
                MeshKit.HelicalAnnulus(mb, Vector2.zero, hr - BLADE_ROOT_EMBED, AbyssConst.FAN_R_TIP, -theta - half, -theta + half,
                    BLADE_T * 0.5f, BLADE_T * 0.5f, 6, BLADE_T, true, false, true, 4);
                if (k == 0) MeasureBlade(mb, first);
                _blades++;
            }
            mb.Pop();
            BeatKit.Emit(mb, _rotor, "Blades", mats[MatId.SteelWeathered], _meshes, ShadowCastingMode.On, Vector3.zero, true);

            // bore card: a window onto the Exterior sky at z = 1, facing the shaft (culled from outside)
            BuildBoreCard(ctx, mb, fan);
        }

        /// <summary>
        /// The bore card: a disc R 150 at z = 1 facing +Z (culled from outside), centre vertex plus rings at BoreCardRings with
        /// the bore lining's 128 segments (the same polygon at R 150), UV0 = (x in metres, BoreCardWeight). GradientCard mode 0
        /// lerp(_CardBottom = rim, _CardTop = core, UV0.y) x _EmissionStrength, fogged in domain 0.
        /// </summary>
        void BuildBoreCard(BeatContext ctx, MeshBuilder mb, Transform fan)
        {
            Vector3 fc = AbyssConst.FAN_CENTRE;
            _boreMat = ctx.Mats.Variant(MatId.GradientCard, "borecard", m =>
            {
                m.SetFloat(FanIds.CardMode, 0f);
                m.SetColor(ShaderIds.CardBottom, BoreCardRim.gamma);   // non-HDR colours: Unity converts them from sRGB
                m.SetColor(ShaderIds.CardTop, BoreCardCore.gamma);
                m.SetFloat(FanIds.CardFog, 1f);
                m.SetFloat(ShaderIds.EmissionStrength, BoreCardStrength(GameState.RideSeconds, GameState.CameraPos));
            });
            int seg = BeatKit.BORE_SEGMENTS, rings = BoreCardRings.Length;
            float z = BORE_CARD_Z;
            int centre = mb.Vertex(new Vector3(fc.x, fc.y, z), Vector3.forward, new Vector2(0f, BoreCardWeight(0f, 0f)));
            int first = mb.VertexCount;
            for (int i = 0; i < rings; i++)
            {
                float r = BoreCardRings[i];
                for (int k = 0; k < seg; k++)
                {
                    float a = k * 2f * Mathf.PI / seg;
                    float x = r * Mathf.Cos(a), y = r * Mathf.Sin(a);
                    mb.Vertex(new Vector3(fc.x + x, fc.y + y, z), Vector3.forward, new Vector2(x, BoreCardWeight(r, y)));
                }
            }
            for (int k = 0; k < seg; k++)
                FrontTri(mb, centre, first + k, first + (k + 1) % seg);
            for (int i = 0; i + 1 < rings; i++)
            {
                int a0 = first + i * seg, b0 = a0 + seg;
                for (int k = 0; k < seg; k++)
                {
                    int k1 = (k + 1) % seg;
                    FrontTri(mb, a0 + k, b0 + k, b0 + k1);
                    FrontTri(mb, a0 + k, b0 + k1, a0 + k1);
                }
            }
            _boreCard = BeatKit.Emit(mb, fan, "BoreCard", _boreMat, _meshes, ShadowCastingMode.Off, Vector3.zero, true);
        }

        /// <summary>A triangle of the bore card wound to face +Z (normal = cross(b - a, c - a), MeshBuilder convention).</summary>
        static void FrontTri(MeshBuilder mb, int a, int b, int c)
        {
            Vector3 pa = mb.Positions[a], pb = mb.Positions[b], pc = mb.Positions[c];
            if (Vector3.Cross(pb - pa, pc - pa).z >= 0f) BeatKit.Tri(mb, a, b, c);
            else BeatKit.Tri(mb, a, c, b);
        }

        void MeasureBlade(MeshBuilder mb, int first)
        {
            float aMin = float.MaxValue, aMax = float.MinValue, rMax = 0f;
            for (int i = first; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i];
                float r = Mathf.Sqrt(p.x * p.x + p.y * p.y);
                if (r > rMax) rMax = r;
                if (r < 1f) continue;
                float a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                aMin = Mathf.Min(aMin, a);
                aMax = Mathf.Max(aMax, a);
            }
            _bladeArcDeg = aMax - aMin;
            _tipR = rMax;
        }

        // ---------------------------------------------------------------- graybox gantry

        void BuildGantry(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform g = BeatKit.Group(_geo, "Gantry");
            float x = AbyssConst.W[14].x, y = AbyssConst.GANTRY_Y, hw = AbyssConst.GANTRY_W * 0.5f;
            float z0 = AbyssConst.GANTRY_Z0, z1 = AbyssConst.GANTRY_Z1;

            MeshKit.ChamferBox(mb, new Vector3(x, y - DECK_T * 0.5f, (z0 + z1) * 0.5f), new Vector3(AbyssConst.GANTRY_W, DECK_T, z0 - z1), 0.02f);
            _deck = BeatKit.Emit(mb, g, "GantryDeck", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);

            for (int s = -1; s <= 1; s += 2)
            {
                float rx = x + s * (hw - 0.05f);
                BeatKit.Rail(mb, new Vector3(rx, y, z0 - 0.05f), new Vector3(rx, y, z1 + 0.05f), AbyssConst.RAIL_H, RAIL_TUBE, RAIL_POST_SPACING);
            }
            MeshKit.WarrenTruss(mb, new Vector3(x, y - DECK_T, z0), new Vector3(x, y - DECK_T, z1), TRUSS_DEPTH, TRUSS_BAYS, TRUSS_MEMBER, Vector3.up);
            BeatKit.Emit(mb, g, "GantrySteel", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);

            // portal frames 6 x 8 m: posts on outriggers beside the deck, a top beam, a cold lamp under the beam
            float ps = PORTAL_SECTION, pw = PORTAL_W * 0.5f;
            for (int i = 0; i < PORTAL_COUNT; i++)
            {
                float z = PORTAL_Z0 - PORTAL_SPACING * i;
                int first = mb.VertexCount;
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferBox(mb, new Vector3(x + s * pw, y - DECK_T - 0.2f + (PORTAL_H + DECK_T + 0.2f) * 0.5f, z),
                        new Vector3(ps, PORTAL_H + DECK_T + 0.2f, ps), 0.02f);
                MeshKit.ChamferBox(mb, new Vector3(x, y + PORTAL_H - ps * 0.5f, z), new Vector3(PORTAL_W + ps, ps, ps), 0.02f);
                MeshKit.ChamferBox(mb, new Vector3(x, y - DECK_T - 0.15f, z), new Vector3(PORTAL_W + ps, 0.3f, 0.3f), 0.02f);
                _portalBounds[i] = BeatKit.VertexBounds(mb, first);
            }
            BeatKit.Emit(mb, g, "PortalFrames", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
            for (int i = 0; i < PORTAL_COUNT; i++)
                MeshKit.ChamferBox(mb, new Vector3(x, y + PORTAL_H - ps - 0.075f, PORTAL_Z0 - PORTAL_SPACING * i), new Vector3(1.2f, 0.15f, 0.3f), 0.02f);
            BeatKit.Emit(mb, g, "PortalLamps", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off);
            for (int i = 1; i < PORTAL_COUNT; i += 2)
                MeshKit.ChamferBox(mb, new Vector3(x + pw - ps * 0.5f - 0.1f, y + 2.2f, PORTAL_Z0 - PORTAL_SPACING * i), new Vector3(0.2f, 0.25f, 0.2f), 0.02f);
            BeatKit.Emit(mb, g, "AmberLamps", mats[MatId.LampAmber], _meshes, ShadowCastingMode.Off);
            _portalPostSpan = PORTAL_W;
            _portalHeight = PORTAL_H;
        }

        // ---------------------------------------------------------------- lights

        void BuildLights(BeatContext ctx)
        {
            Transform lights = BeatKit.Group(ctx.Root, "Lights");
            float x = AbyssConst.W[14].x, y = AbyssConst.GANTRY_Y;
            for (int i = 0; i < PORTAL_COUNT; i++)
            {
                float z = PORTAL_Z0 - PORTAL_SPACING * i;
                BeatKit.Point(lights, "GantryCold" + i, new Vector3(x, y + PORTAL_H - PORTAL_SECTION - 0.5f, z), AbyssPalette.COLD_LAMP, COLD_I, COLD_RANGE);
                if ((i & 1) == 1)
                    BeatKit.Point(lights, "GantryAmber" + i, new Vector3(x + PORTAL_W * 0.5f - PORTAL_SECTION - 0.3f, y + 2.2f, z), AbyssPalette.AMBER_LIGHT,
                        AMBER_I, AMBER_RANGE);
            }
        }

        // ================================================================================================ per frame

        public override void Tick(in BeatFrame f)
        {
            ApplyFan(f.Time);
            ApplyBoreCard(f.RideSeconds, f.CameraPos);
            ApplyBeamHaze(f.RideSeconds);
            ApplyRotorKey(f.E);
            for (int i = 0; i < _tiers.Length; i++) _tiers[i].Update(f.CameraPos);
        }

        public override void PrepareCapture(double sceneTime)
        {
            ApplyFan(sceneTime);
            Vector3 cam = GameState.CameraPos;
            ApplyBoreCard(GameState.RideSeconds, cam);
            ApplyBeamHaze(GameState.RideSeconds);
            ApplyRotorKey(GameState.E);
            for (int i = 0; i < _tiers.Length; i++) _tiers[i].Update(cam);
        }

        void ApplyFan(double t)
        {
            if (_rotor == null) return;
            double deg = ThetaDeg(t);
            _rotor.localRotation = Quaternion.AngleAxis((float)deg, Vector3.forward);
            if (!_editMode) Shader.SetGlobalFloat(ShaderIds.AbFanTheta, (float)(deg * System.Math.PI / 180.0));
        }

        /// <summary>Bore-card strength for the ride second and eye (written only when it changes by more than 0.1 %).</summary>
        void ApplyBoreCard(float rideSeconds, Vector3 eye)
        {
            if (_boreMat == null) return;
            float s = BoreCardStrength(rideSeconds, eye);
            if (Mathf.Abs(s - _boreStrength) <= 1e-3f * Mathf.Max(s, 1e-3f)) return;
            _boreStrength = s;
            _boreMat.SetFloat(ShaderIds.EmissionStrength, s);
        }

        /// <summary>
        /// Beam look for the ride second: density BEAM_DENSITY x the ride haze scale (0.25 / 0.5 in the lift shaft, 1 in the fan
        /// chamber), the ride / chamber shape blend (near fade, fade-in) and the far-wall patch radiance. Material writes only
        /// while a value changes (ride seconds 50-60 and 110-120), so the steady state costs nothing per frame.
        /// </summary>
        void ApplyBeamHaze(float rideSeconds)
        {
            if (_patchMat != null)
            {
                float p = PatchStrength(rideSeconds);
                if (p != _patchStrength)
                {
                    _patchStrength = p;
                    _patchMat.SetFloat(ShaderIds.EmissionStrength, p);
                }
            }
            if (_beamMat == null) return;
            float w = BeamChamber(rideSeconds), h = BeamHaze(rideSeconds);
            if (w == _beamHaze && h == _beamDensity) return;
            _beamHaze = w;
            _beamDensity = h;
            _beamMat.SetFloat(ShaderIds.EmissionStrength, BEAM_DENSITY * h);
            _beamMat.SetVector(FanIds.BeamShape, BeamShapeAt(rideSeconds));
            _beamMat.SetVector(FanIds.BeamFeather, BeamFeatherAt(rideSeconds));
        }

        /// <summary>Key-light mask of the rotor paint for extended progress E (written only when it changes).</summary>
        void ApplyRotorKey(float e)
        {
            if (_rotorMat == null) return;
            float k = RotorKeyMask(e);
            if (k == _rotorKey) return;
            _rotorKey = k;
            _rotorMat.SetFloat(FanIds.KeyLightMask, k);
        }

        // ================================================================================================ QA hooks

        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            if (keyShotTag != "KS6_gantry_fan") return;
            Bounds frames = _portalBounds[0];
            frames.Encapsulate(_portalBounds[1]);
            subjects.Add(new KeyShotSubject("portal frames", frames, 0.15f));
            subjects.Add(new KeyShotSubject("fan ring", BeatKit.BoundsOf(_housing), 0.5f));
            subjects.Add(new KeyShotSubject("hub", BeatKit.BoundsOf(_hub)));
        }

        public override void ReportScale(ScaleReport report)
        {
            if (_housing != null)
            {
                Bounds b = _housing.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("fan.housing.diameter", b.size.x);
                report.Add("fan.housing.depth", b.size.y);
            }
            // visible outer silhouette on the near flat (the trim collar in Full / Simplified, the housing in Graybox)
            MeshRenderer rim = _collar != null ? _collar : _housing;
            if (rim != null)
            {
                float rimD = rim.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                report.Add("fan.rim.diameter", rimD);
                report.Add("fan.rim.clearance", AbyssConst.FAN_CENTRE.y - 0.5f * rimD - AbyssConst.GANTRY_Y);
            }
            if (_hub != null) report.Add("fan.hub.radius", _hub.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x);
            report.Add("fan.blades", _blades);
            report.Add("fan.tip.radius", _tipR);
            report.Add("fan.blade.arc.deg", _bladeArcDeg);
            report.Add("fan.period", 360.0 / (ThetaDeg(1.0) - ThetaDeg(0.0)));
            if (_tier == BeatTier.Graybox)
            {
                report.Add("fan.gap.deg", 360f / Mathf.Max(1, _blades) - _bladeArcDeg);
            }
            else
            {
                // measured on the blade mesh at R 48, 90 and 130 (worst case of the three)
                report.Add("fan.gap.deg", Mathf.Abs(_gapMinDeg - 18f) > Mathf.Abs(_gapMaxDeg - 18f) ? _gapMinDeg : _gapMaxDeg);
                report.Add("fan.gap.min.deg", _gapMinDeg);
                report.Add("fan.gap.max.deg", _gapMaxDeg);
                report.Add("fan.gap.r48.deg", _gapAtDeg[0]);
                report.Add("fan.gap.r90.deg", _gapAtDeg[1]);
                report.Add("fan.gap.r130.deg", _gapAtDeg[2]);
                report.Add("fan.beam.count", _beamCount);
                report.Add("fan.beam.phase.deg", GAP_PHASE_DEG);
                if (_beamR != null)
                {
                    Material bm = _beamR.sharedMaterial;
                    Vector4 bp = bm.GetVector(FanIds.BeamParams);
                    Vector4 bd = bm.GetVector(FanIds.BeamDir);
                    report.Add("fan.beam.width.deg", 2.0 * bp.z * Mathf.Rad2Deg);
                    report.Add("fan.beam.r0", bp.x);
                    report.Add("fan.beam.r1", bp.y);
                    report.Add("fan.beam.length", bp.w);
                    report.Add("fan.beam.pitch.deg", -Mathf.Asin(Mathf.Clamp(bd.y / Mathf.Max(1e-6f, ((Vector3)bd).magnitude), -1f, 1f)) * Mathf.Rad2Deg);
                }
            }
            if (_deck != null)
            {
                Bounds b = _deck.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("gantry.length", b.size.z);
                report.Add("gantry.width", b.size.x);
                report.Add("gantry.z0", _tier == BeatTier.Graybox ? b.max.z : _deck.transform.position.z + b.max.z);
            }
            report.Add("gantry.portal.count", PORTAL_COUNT);
            report.Add("gantry.portal.width", _tier == BeatTier.Graybox ? _portalBounds[0].size.x - PORTAL_SECTION : _portalPostSpan);
            report.Add("gantry.portal.height", _portalHeight);
            report.Add("gantry.portal.spacing", PORTAL_SPACING);
            if (_tier != BeatTier.Graybox)
            {
                report.Add("gantry.truss.depth", _trussDepth);
                report.Add("gantry.truss.bays", _trussBays);
                report.Add("gantry.cables", _cableCount);
            }
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);
    }
}
