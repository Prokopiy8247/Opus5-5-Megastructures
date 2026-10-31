using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 3 - column ledge (14.5.3, d 216-297.5). The C1 collar as a helical ramp following the path: a ring loft r 22-28
    /// (inner edge 5 cm inside the column), 2.5 m fascia, a 0.5 m soffit and a corbel cone tapering to the column over 8 m,
    /// a 0.15 x 0.15 kerb, its walking surface falling linearly from y -3 at alpha 0 (W6) to -7 at alpha pi (W8). Left of the
    /// bridge landing a flat pad ends on the plane x = -3 (flush with the corridor edge of W5-W6). The bridge lands on a steel
    /// landing plate (the kerb is open there) above a concrete bearing nib under the deck end. The 32 x 14 apron runs from
    /// alpha = pi to the hall wall at x = -32. Near detail (DetailTier by camera distance, 150 m, 20 m hysteresis, dithered):
    /// four amber lamps (O 0.5 globes 2.6 m above the floor on bracket poles inboard at r 22.25 with braced arms out to
    /// r 23.7, a downward amber spot at the globe centre), the closed steel hatch 0.9 x 2.0 at alpha 1.85 in a
    /// steel frame on the column face (2 hinges, lever handle, cold status lamp, level sill), expansion-joint cover plates at
    /// alpha pi/2 and pi (aligned with the column's and the fascia's shader joints at alpha 0, pi/2, pi), a low conduit along
    /// the column foot feeding the lamps, the KS3 cold spot can on a yoke off the fascia, scuppers and drains. (The second
    /// cold spot, which grades the face seen from the bridge, is Beat02's floodlight on the bridge.)
    ///
    /// Persistent (all distances): collar, apron, bearing nib, lamp globes (camera-facing, white-hot core to amber limb) and
    /// their halos. C1 itself belongs to Beat02.
    ///
    /// Colliders: the walking surface is ONE closed MeshCollider slab that is exactly the visible helicoid (same frames, same
    /// radii, same function <see cref="TopY"/>), extended to r 21.5-28.6 and from alpha -0.25 to pi + 0.06 (flat outside
    /// [0, pi]) so the bridge end and the apron meet it flush; the column wall at r 22.4, the outer kerb wall at r 28 with
    /// the bridge opening (|x| &lt;= 0.6), the pad end wall on x = -3, the apron floor and edge walls, safety volumes.
    /// </summary>
    public sealed partial class Beat03_Ledge : BeatBase
    {
        public override int Index => 3;

        // ---------------------------------------------------------------- beat-specific constants (public: Beat03Tests, QA)
        /// <summary>Collar loft frames over alpha 0..pi (0.70 degrees, 0.31 m at the centreline).</summary>
        public const int ARC_SEGMENTS = 256;
        /// <summary>The pad left of the bridge landing ends on the plane x = -3 (-LEDGE_W / 2, the W5-W6 corridor edge).</summary>
        public const float PAD_END_X = -0.5f * AbyssConst.LEDGE_W;
        /// <summary>Ledge-to-column overlap (14.5.3 pitfalls): the collar's inner edge lies 5 cm inside the column face.</summary>
        public const float COLUMN_OVERLAP = 0.05f;
        public const float KERB_W = AbyssConst.LEDGE_KERB, KERB_H = AbyssConst.LEDGE_KERB;
        /// <summary>Flat soffit strip under the fascia before the corbel cone starts.</summary>
        public const float SOFFIT_W = 0.5f;
        public const float EDGE_BEVEL = 0.02f, FASCIA_BEVEL = 0.03f;
        /// <summary>The kerb is open over |x| &lt;= 0.75 at the bridge landing (the landing plate spans the opening).</summary>
        public const float KERB_OPEN_HALF = 0.75f;
        /// <summary>The kerb stops 0.3 m before the pad's end cut (x = -2.7), leaving a chamfered bare edge at the cut.</summary>
        public const float KERB_PAD_END_X = -2.7f;
        public const float COLLIDER_R_IN = 21.5f, COLLIDER_R_OUT = 28.6f, COLLIDER_THICK = 1f;
        public const float COLLIDER_ALPHA_BEFORE = -0.25f, COLLIDER_ALPHA_PAST = 0.06f;

        /// <summary>Radii of the collar: inner edge (in the column), walking surface edge at the kerb, outer face.</summary>
        public const float R_COLLAR_IN = AbyssConst.LEDGE_R_IN - COLUMN_OVERLAP;
        public const float R_WALK = AbyssConst.LEDGE_R_OUT - KERB_W;
        public const float R_EDGE = AbyssConst.LEDGE_R_OUT - EDGE_BEVEL;
        const float R_OUT = AbyssConst.LEDGE_R_OUT;
        const float R_FACE = AbyssConst.LEDGE_R_IN;          // the column face (C1 radius)
        const float CUT_TOP = PAD_END_X + EDGE_BEVEL;          // the walking surface stops 2 cm short of the cut (chamfer)

        const int COLUMN_WALL_SEGMENTS = 128;
        const int OUTER_WALL_SEGMENTS = 64;
        const float WALL_BASE = -9f, WALL_HEIGHT = 10f;       // covers every ledge height (-7..-3) plus head room
        const float APRON_BEVEL = 0.03f;

        // lamps. Lead decision: poles inboard at r 22.25, against the column and out of the walking line. The heads sit on
        // 1.5 m braced arms at r 23.7 with the light at the globe centre (review S2: at r 22.6 with the light at 22.95 the
        // face got E = 5 / 0.95^2 = 5.5 and the y = 0 band above lamp alpha 0.6 about 11, clipped amber stripes). In KS3,
        // lamp 2.2 stands 15.1 m away, 9.3 degrees right of centre and 5.6 degrees clear of the C1 silhouette. The spec's
        // numbers (15.5 m, 14 degrees) assume r 25 on the walking line.
        public const float LAMP_POLE_R = 22.25f, LAMP_HEAD_R = 23.7f, LAMP_HEAD_ABOVE_FLOOR = 2.6f, LAMP_GLOBE_R = 0.25f;
        // per lamp (alpha 0.6, 1.4, 2.2, 2.9). Lamp 2.2 (KS3) is tuned up so its warm pool reads on the sunlit floor.
        static readonly float[] LAMP_I = { 5f, 5f, 8f, 5f };
        const float LAMP_RANGE = 10f;
        /// <summary>
        /// Critic round 1 (R1-18): as omnidirectional points the four amber lamps were the brightest source on the neutral C1
        /// face (E 1.7-2.8 at 1.7 m, still 0.2 at 5 m against a near-black cold ambient) and washed some 40 % of the beat-3
        /// free-look frames a muddy orange-brown. Each lamp is now a downward spot from the globe centre, its axis tilted
        /// <see cref="LAMP_SPOT_TILT"/> degrees outward (away from the column), full cone <see cref="LAMP_SPOT_ANGLE"/> with a
        /// <see cref="LAMP_SPOT_INNER"/> core: the floor directly below gets the same E as before (I / 2.6^2, 0.74 / 1.18 for
        /// lamp 2.2), the warm pool spans about r 22.5-26.7 and +/- 2 m along the arc, and the column face lies outside the cone
        /// except for its foot, which the penumbra reaches at E below 0.005 (Beat03Tests). Spec 14.2 says "amber point";
        /// returned as an arbitration (I and range unchanged).
        /// </summary>
        public const float LAMP_SPOT_ANGLE = 100f, LAMP_SPOT_INNER = 50f, LAMP_SPOT_TILT = 15f;
        /// <summary>
        /// Globe (R1-17, R2-21, lead decision D4): a camera-facing sphere (64 x 64 segments, pole turned toward the camera every
        /// frame in Tick) with the GradientCard emitter, UV0.y = <see cref="GlobeCoreWeight"/>. D4: the near lamp heads keep the
        /// spec peak AMBER x 8.0 only in a small core with a saturated amber falloff. With the f03 weight cos^3 (still 0.65 at
        /// half the radius) most of the disc sat near the peak, which the Neutral tonemapper clips to cream (KS3 / B3 globes
        /// (237-242, 212-219, 175-183), HSV S 0.25). Now the white-hot core (weight 1) ends at 8 % of the projected radius and
        /// falls to <see cref="GLOBE_MID_WEIGHT"/> by 25 %; the Emissive shader's hue-safe warm emission (outside its N.V core)
        /// then shows a saturated amber body that holds to <see cref="GLOBE_BAND_RHO"/> and deepens to the limb
        /// (GLOBE_LIMB_SRGB x GLOBE_LIMB_GAIN). Look-dev: with a mid weight of 0.14-0.35 that fell from 30 % of the radius and
        /// the red-orange limb (1, 0.55, 0.2), the body read salmon (lead_b01 KS3 (229, 173, 134); ledge_r31 B3_freelook_back
        /// rho 0.3-0.9 hue 20-25, S 0.39-0.49), because the limb colour dominated it and the core's bloom lifts blue; an
        /// AMBER-led body (0.42) held to 55 % of the radius and an amber limb (1, 0.62, 0.26; hue 29) keep it at hue 28-38.
        /// 64 latitude rings keep the core gradient smooth (about 5 px per ring at 2.75 m).
        /// </summary>
        public const int GLOBE_LON = 64, GLOBE_LAT = 64;
        /// <summary>Weight of the amber body (from GLOBE_MID_RHO to GLOBE_BAND_RHO of the projected radius).</summary>
        public const float GLOBE_MID_WEIGHT = 0.42f;
        /// <summary>Projected radius fractions: the white-hot core, the end of the core falloff, the start of the limb falloff.</summary>
        public const float GLOBE_CORE_RHO = 0.08f, GLOBE_MID_RHO = 0.25f, GLOBE_BAND_RHO = 0.55f;

        /// <summary>
        /// GradientCard weight of the globe at a point whose direction from the centre makes cos = <paramref name="cosFromPole"/>
        /// with the pole (the camera axis): rho = sin of that angle is the projected radius fraction. 1 in the core (rho &lt;=
        /// 0.08), smoothstep down to GLOBE_MID_WEIGHT at rho 0.25, held to rho 0.55, then smoothstep to 0 at the limb (rho 1);
        /// 0 behind the limb. Monotonic in the angle from the pole.
        /// </summary>
        public static float GlobeCoreWeight(float cosFromPole)
        {
            if (cosFromPole <= 0f) return 0f;
            float rho = Mathf.Sqrt(Mathf.Max(0f, 1f - cosFromPole * cosFromPole));
            float core = 1f - BeatKit.Smoothstep((rho - GLOBE_CORE_RHO) / (GLOBE_MID_RHO - GLOBE_CORE_RHO));
            float band = 1f - BeatKit.Smoothstep((rho - GLOBE_BAND_RHO) / (1f - GLOBE_BAND_RHO));
            return GLOBE_MID_WEIGHT * band + (1f - GLOBE_MID_WEIGHT) * core;
        }
        const float GLOBE_CORE_STRENGTH = AbyssPalette.AMBER_EMISSIVE, GLOBE_LIMB_GAIN = 1.3f;
        static readonly Color GLOBE_LIMB_SRGB = new Color(1f, 0.62f, 0.26f, 1f);
        /// <summary>
        /// Soft amber haze halo around each globe (Glow sprite, true size, energy-conserving), as on the bridge lamps.
        /// Amber cap (14.2 "amber covers at most 0.3 % of any frame", critique R2-24; returned as a 1.6.2 arbitration): a 0.5 m
        /// globe covers 0.3 % of a 1080p frame (a disc of 44.5 px radius) at d = 0.5 / (2 tan(44.5 px x 66 / 1080 deg)) = 5.3 m,
        /// and the walking line passes each globe at about 2.75 m (1.2 % of the frame), so the cap is geometrically
        /// unreachable for the lamp head and its pool within about 5.5 m of the camera and is not applied there.
        /// </summary>
        const float GLOBE_HALO_D = 0.75f, GLOBE_HALO_STRENGTH = 0.15f;
        static readonly int[] SIMPLIFIED_LAMPS = { 0, 2 };   // alpha 0.6 and 2.2 (KS3 needs 2.2)
        /// <summary>Lamp arm, knee brace, stem and cup: matte near-black paint like the bridge lamp arms (Beat02 "b2arm").</summary>
        static readonly Color LAMP_ARM_SRGB = new Color(0.038f, 0.043f, 0.05f, 1f);

        // Cold spot (14.2 rig: range 90) on the KS3 stretch of the face. The bridge-side spot of f02 (alpha 0.14, 6 m from the
        // face, aimed 64 m up with a 6 / 38 degree cone) drew a narrow hard teardrop about 10 m wide on the 44 m column in
        // KS2a (R1-30); the column face seen from the bridge is now graded by Beat02's floodlight on the bridge (wide cone, 46
        // m from the face). This one, alpha 1.62 (3.4 m right of the KS3 eye, out of frame), aims along the face at alpha 1.8,
        // 12 m above the floor, with a wide soft penumbra (70 / 25 degrees, R1-30 asks for 55-70): the hatch and the face
        // below the y = 0 band stay dark, and the face brightens continuously up through the band to a broad highlight about
        // 8 m above the floor (E 1.5 at I 300), fading above the KS3 frame (Beat03Tests). From the low mount the light rakes
        // the face, so the board grooves, tie holes and 3 m cold joints read in KS3.
        static readonly float[] SPOT_ALPHA = { 1.62f };
        static readonly float[] SPOT_AIM_ALPHA = { 1.8f };
        static readonly float[] SPOT_AIM_UP = { 12f };
        static readonly float[] SPOT_INNER = { 25f };
        static readonly float[] SPOT_I = { 300f };
        /// <summary>Pivot height above the floor: the shallow-aimed can sits on a taller yoke so its bezel clears the kerb.</summary>
        static readonly float[] SPOT_PIVOT_UP = { 0.4f };
        const float SPOT_R = 28.25f, SPOT_RANGE = 90f, SPOT_ANGLE = 70f;
        /// <summary>The spot light sits at the lens, this far ahead of the can's pivot along the beam.</summary>
        const float SPOT_LENS = 0.21f;

        const int SAFETY_SEGMENTS = 8;
        const float DETAIL_NEAR = AbyssConst.NEAR_TIER, DETAIL_HYSTERESIS = 20f, DETAIL_FADE = 20f;

        // ---------------------------------------------------------------- state
        readonly List<Mesh> _meshes = new List<Mesh>();
        Transform _geo;
        MeshRenderer _padTop, _helixTop, _body, _hatchFrame, _hatchLeaf, _apron;
        Mesh _walkCollider;
        readonly Vector3[] _lampHead = new Vector3[4];
        readonly bool[] _lampBuilt = new bool[4];
        readonly Transform[] _globe = new Transform[4];   // camera-facing globes (null in Graybox)
        int _lampCount;
        DetailTier _detail;

        /// <summary>The walking-surface collider mesh (QA, tests).</summary>
        public Mesh WalkColliderMesh => _walkCollider;
        /// <summary>The visible walking surface: the flat pad (alpha &lt;= 0) and the helix (alpha 0..pi).</summary>
        public Mesh PadTopMesh => _padTop != null ? _padTop.GetComponent<MeshFilter>().sharedMesh : null;
        public Mesh HelixTopMesh => _helixTop != null ? _helixTop.GetComponent<MeshFilter>().sharedMesh : null;
        public int LampCount => _lampCount;

        /// <summary>Centre of lamp i's globe (index into AbyssConst.LEDGE_LAMP_ALPHA); false when this tier has no lamp i.</summary>
        public bool TryGetLampHead(int i, out Vector3 head)
        {
            bool built = i >= 0 && i < _lampBuilt.Length && _lampBuilt[i];
            head = built ? _lampHead[i] : Vector3.zero;
            return built;
        }

        public bool HasHatch => _hatchLeaf != null;
        public DetailTier Detail => _detail;

        /// <summary>Camera-facing globe renderer of lamp i (Full and Simplified tiers), or null.</summary>
        public Transform GlobeTransform(int i) => i >= 0 && i < _globe.Length ? _globe[i] : null;

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat03");
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            BuildFrames();
            BuildColliders(ctx);
            if (ctx.Tier == BeatTier.Graybox) BuildGraybox(ctx);
            else BuildFull(ctx, ctx.Tier == BeatTier.Simplified);
            BuildLights(ctx);
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            Vector2 c1 = AbyssConst.ARC_CENTRE;
            float rOut = AbyssConst.LEDGE_R_OUT, rWall = AbyssConst.COLLIDER_INNER_R;
            float y8 = AbyssConst.W[8].y;

            // walking surface: one closed slab, exactly the visible helicoid (pad, helix, short flat run past alpha = pi)
            _walkCollider = BuildWalkColliderMesh();
            CorridorKit.MeshSurface(c, "LedgeWalkSurface", _walkCollider);

            // column wall at r = 22.4: a full ring of chords, bodies toward the axis
            for (int i = 0; i < COLUMN_WALL_SEGMENTS; i++)
            {
                float a0 = 2f * Mathf.PI * i / COLUMN_WALL_SEGMENTS, a1 = 2f * Mathf.PI * (i + 1) / COLUMN_WALL_SEGMENTS;
                Vector3 p0 = Ring(a0, rWall, 0f), p1 = Ring(a1, rWall, 0f);
                Vector3 mid = Ring(0.5 * (a0 + a1), 1f, 0f);
                var inward = new Vector2(c1.x - mid.x, c1.y - mid.z);
                BeatKit.Wall(c, "ColumnWall" + i, new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z), WALL_BASE, WALL_HEIGHT, inward);
            }

            // outer kerb wall at r = 28 from the bridge opening edge (x = 0.6) to alpha = pi, and left of the bridge to the pad end
            float aOpen = Mathf.Asin(AbyssConst.BRIDGE_W * 0.5f / rOut);
            float aPad = (float)CutAlpha(rOut, PAD_END_X);
            OuterWall(c, "OuterWall", aOpen, Mathf.PI, OUTER_WALL_SEGMENTS);
            OuterWall(c, "OuterWallPad", aPad, -aOpen, 4);

            // pad end wall on the plane x = -3 (flush with the visible end cut), body toward -X, into the column wall
            float zOuter = AbyssConst.ARC_CENTRE.y - Mathf.Sqrt(COLLIDER_R_OUT * COLLIDER_R_OUT - PAD_END_X * PAD_END_X) - 1f;
            float zInner = AbyssConst.ARC_CENTRE.y - Mathf.Sqrt(rWall * rWall - PAD_END_X * PAD_END_X) + 1f;
            BeatKit.Wall(c, "PadEndWall", new Vector2(PAD_END_X, zOuter), new Vector2(PAD_END_X, zInner), WALL_BASE, WALL_HEIGHT, new Vector2(-1f, 0f));

            // apron 14 wide (z 262-276) from x = 0 to the hall wall at x = -32, floor at y -7
            float ax0 = AbyssConst.HALL_X0, az0 = AbyssConst.APRON_Z0, az1 = AbyssConst.APRON_Z1;
            CorridorKit.Floor(c, "ApronFloor", new Vector3(ax0 * 0.5f, y8, (az0 + az1) * 0.5f), new Vector2(-ax0, az1 - az0));
            float colX = -Mathf.Sqrt(rWall * rWall - (az0 - c1.y) * (az0 - c1.y)) + 0.3f;   // the -Z edge wall runs into the column wall
            BeatKit.Wall(c, "ApronEdgeS", new Vector2(ax0 - 2f, az0), new Vector2(colX, az0), y8 - 1f, 4f, new Vector2(0f, -1f));
            BeatKit.Wall(c, "ApronEdgeN", new Vector2(ax0 - 2f, az1), new Vector2(0f, az1), y8 - 1f, 4f, new Vector2(0f, 1f));
            BeatKit.Wall(c, "ApronEdgeE", new Vector2(0f, c1.y + rOut), new Vector2(0f, az1), y8 - 1f, 4f, new Vector2(1f, 0f));

            // safety volumes along the arc (each 3.5 m below the lower end of its segment) and under the apron
            float a = aPad, step = (Mathf.PI - aPad) / SAFETY_SEGMENTS;
            for (int i = 0; i < SAFETY_SEGMENTS; i++, a += step)
            {
                float am = a + 0.5f * step;
                float yLow = TopY(a + step);
                Vector3 centre = Ring(am, AbyssConst.LEDGE_R_CENTRE, yLow - 3.5f);
                float chord = 2f * rOut * Mathf.Sin(0.5f * step);
                float yaw = Mathf.Atan2(Mathf.Cos(am), Mathf.Sin(am)) * Mathf.Rad2Deg;   // tangent (cos a, sin a) in (x, z)
                CorridorKit.SafetyVolume(c, "SafetyLedge" + i, centre, new Vector3(AbyssConst.LEDGE_W + 8f, 1f, chord + 8f), Quaternion.Euler(0f, yaw, 0f));
            }
            CorridorKit.SafetyBelow(c, "SafetyApron", new Vector3(ax0 * 0.5f, y8, (az0 + az1) * 0.5f), new Vector2(-ax0, az1 - az0));
        }

        static void OuterWall(Transform c, string name, float a0, float a1, int segments)
        {
            float r = AbyssConst.LEDGE_R_OUT;
            Vector2 c1 = AbyssConst.ARC_CENTRE;
            for (int i = 0; i < segments; i++)
            {
                float s0 = Mathf.Lerp(a0, a1, (float)i / segments), s1 = Mathf.Lerp(a0, a1, (float)(i + 1) / segments);
                Vector3 p0 = Ring(s0, r, 0f), p1 = Ring(s1, r, 0f);
                Vector3 mid = Ring(0.5 * (s0 + s1), 1f, 0f);
                var outward = new Vector2(mid.x - c1.x, mid.z - c1.y);
                // chords of a convex boundary meet exactly at their vertices on the walkable side: no end extension needed
                BeatKit.Wall(c, name + i, new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z), WALL_BASE, WALL_HEIGHT, outward);
            }
        }

        // ================================================================================================ tiers

        void BuildFull(BeatContext ctx, bool simplified)
        {
            MaterialLibrary mats = ctx.Mats;
            Material top = FloorMaterial(mats);
            Material body = CollarMaterial(mats);
            var mb = new MeshBuilder();

            // ---- persistent: collar, bearing nib, apron, lamp globes
            BuildCollarTop(mb, TOP, out Mesh padTop, out Mesh helixTop);
            _padTop = BeatKit.Renderer(_geo, "CollarPadTop", padTop, top, Vector3.zero, Quaternion.identity, ShadowCastingMode.On);
            _helixTop = BeatKit.Renderer(_geo, "CollarTop", helixTop, top, Vector3.zero, Quaternion.identity, ShadowCastingMode.On);
            BuildCollarBody(mb);
            _body = BeatKit.Emit(mb, _geo, "CollarBody", body, _meshes, ShadowCastingMode.On);
            BuildBridgeBearing(mb);
            BeatKit.Emit(mb, _geo, "BridgeBearing", body, _meshes, ShadowCastingMode.On);
            BuildApron(mb, mats);

            Transform near = BeatKit.Group(_geo, "NearDetail");
            var detail = new DetailTier("Beat03 ledge detail", DetailBounds(), DETAIL_NEAR, DETAIL_HYSTERESIS, DETAIL_FADE);
            BuildLamps(mats, mb, near, detail, simplified);
            BuildSpots(mats, mb, near, detail);
            BuildLandingPlate(mats, mb, near, detail);
            if (!simplified)
            {
                BuildHatch(mats, mb, near, detail);
                BuildJointPlates(mats, mb, near, detail);
                BuildConduit(mats, mb, near, detail);
                BuildDrains(mats, mb, near, detail);
                BuildAnchors(mats, mb, near, detail);
            }
            _detail = detail;
        }

        void BuildGraybox(BeatContext ctx)
        {
            MaterialLibrary mats = ctx.Mats;
            Material cyl = mats[MatId.ConcreteCyl];
            var mb = new MeshBuilder();
            float y8 = AbyssConst.W[8].y;

            BuildCollarTop(mb, GB_TOP, out Mesh padTop, out Mesh helixTop);
            _padTop = BeatKit.Renderer(_geo, "LedgePad", padTop, cyl, Vector3.zero, Quaternion.identity, ShadowCastingMode.On);
            _helixTop = BeatKit.Renderer(_geo, "LedgeHelix", helixTop, cyl, Vector3.zero, Quaternion.identity, ShadowCastingMode.On);
            BuildGrayboxBody(mb);
            _body = BeatKit.Emit(mb, _geo, "LedgeBody", cyl, _meshes, ShadowCastingMode.On);

            // apron slab 32 x 14 (its top meets the helix end at alpha = pi, y -7; it runs into C1 on its inner side)
            float ax0 = AbyssConst.HALL_X0, az0 = AbyssConst.APRON_Z0, az1 = AbyssConst.APRON_Z1;
            MeshKit.ChamferBox(mb, new Vector3(ax0 * 0.5f, y8 - AbyssConst.LEDGE_COLLAR_THICK * 0.5f, (az0 + az1) * 0.5f),
                new Vector3(-ax0, AbyssConst.LEDGE_COLLAR_THICK, az1 - az0), APRON_BEVEL);
            BeatKit.FloorUVAlongX(mb, 0, AbyssConst.W[8].z);
            _apron = BeatKit.Emit(mb, _geo, "Apron", mats[MatId.ConcreteBoard], _meshes, ShadowCastingMode.On);

            // closed hatch 0.9 x 2.0 on the column face at alpha 1.85 (bottom at the ledge floor)
            const float hatchT = 0.06f;
            float hy = TopY(AbyssConst.HATCH_ALPHA);
            Frame(AbyssConst.HATCH_ALPHA, out Vector3 radial, out _);
            Vector3 hc = Ring(AbyssConst.HATCH_ALPHA, R_FACE + hatchT * 0.5f - 0.01f, hy + AbyssConst.HATCH_H * 0.5f);
            MeshKit.ChamferBox(mb, hc, new Vector3(AbyssConst.HATCH_W, AbyssConst.HATCH_H, hatchT), 0.015f, Quaternion.LookRotation(radial, Vector3.up));
            _hatchLeaf = BeatKit.Emit(mb, _geo, "Hatch", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
            _hatchFrame = _hatchLeaf;

            // four amber lamps on 2.6 m poles beside the column (inside the column collider gap, out of the walking line)
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                float a = AbyssConst.LEDGE_LAMP_ALPHA[i];
                float fy = TopY(a);
                Vector3 foot = Ring(a, LAMP_POLE_R, fy);
                Vector3 head = Ring(a, LAMP_HEAD_R, fy + LAMP_HEAD_ABOVE_FLOOR);
                MeshKit.Cylinder(mb, foot - new Vector3(0f, 0.05f, 0f), new Vector3(foot.x, head.y, foot.z), 0.05f, 12);
                MeshKit.ChamferMember(mb, new Vector3(foot.x, head.y, foot.z), head, 0.06f, 0.06f, 0.01f, Vector3.up);
            }
            BeatKit.Emit(mb, _geo, "LampPoles", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                float a = AbyssConst.LEDGE_LAMP_ALPHA[i];
                _lampHead[i] = Ring(a, LAMP_HEAD_R, TopY(a) + LAMP_HEAD_ABOVE_FLOOR);
                _lampBuilt[i] = true;
                MeshKit.Sphere(mb, _lampHead[i], LAMP_GLOBE_R, 16, 8);
            }
            _lampCount = AbyssConst.LEDGE_LAMP_ALPHA.Length;
            BeatKit.Emit(mb, _geo, "LampHeads", mats[MatId.LampAmber], _meshes, ShadowCastingMode.Off);

            // cold spot housings hanging off the outer edge
            for (int i = 0; i < SPOT_ALPHA.Length; i++)
            {
                float a = SPOT_ALPHA[i];
                MeshKit.ChamferBox(mb, Ring(a, SPOT_R, TopY(a) - 0.25f), new Vector3(0.5f, 0.4f, 0.5f), 0.02f);
            }
            BeatKit.Emit(mb, _geo, "SpotHousings", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
        }

        void BuildLights(BeatContext ctx)
        {
            Transform lights = BeatKit.Group(ctx.Root, "Lights");
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                if (!_lampBuilt[i]) continue;
                // at the globe centre: the visible source and its light pool agree (the globe's own faces face away from it)
                Light l = BeatKit.Spot(lights, "LedgeAmber" + i, _lampHead[i], LampSpotDirection(AbyssConst.LEDGE_LAMP_ALPHA[i]),
                    AbyssPalette.AMBER_LIGHT, LAMP_I[i], LAMP_RANGE, LAMP_SPOT_ANGLE);
                l.innerSpotAngle = LAMP_SPOT_INNER;
            }
            for (int i = 0; i < SPOT_ALPHA.Length; i++)
            {
                SpotPose(i, out Vector3 pos, out Vector3 dir);
                Light spot = BeatKit.Spot(lights, "ColumnSpot" + i, pos + dir * SPOT_LENS, dir, AbyssPalette.COLD_LAMP, SPOT_I[i], SPOT_RANGE,
                    SPOT_ANGLE);
                spot.innerSpotAngle = SPOT_INNER[i];
            }
        }

        /// <summary>
        /// Cold spot i: the can's pivot just outside the kerb (SPOT_PIVOT_UP above the floor), aimed at the column face at
        /// SPOT_AIM_ALPHA, SPOT_AIM_UP above the floor there.
        /// </summary>
        static void SpotPose(int i, out Vector3 pos, out Vector3 dir)
        {
            float a = SPOT_ALPHA[i], at = SPOT_AIM_ALPHA[i];
            pos = Ring(a, SPOT_R, TopY(a) + SPOT_PIVOT_UP[i]);
            Vector3 target = Ring(at, R_FACE, TopY(at) + SPOT_AIM_UP[i]);
            dir = (target - pos).normalized;
        }

        /// <summary>Axis of lamp spot at alpha: straight down, tilted LAMP_SPOT_TILT degrees outward (away from the column).</summary>
        public static Vector3 LampSpotDirection(double alpha)
        {
            Frame(alpha, out Vector3 radial, out _);
            float t = LAMP_SPOT_TILT * Mathf.Deg2Rad;
            return (Vector3.down * Mathf.Cos(t) + radial * Mathf.Sin(t)).normalized;
        }

        /// <summary>
        /// Globe emitter (GradientCard, CardMode 0): UV0.y 1 = core (AMBER x 8.0, M_Lamp's peak), 0 = limb (deep amber x 1.3).
        /// _CardTop / _CardBottom are non-HDR colours (Unity converts them from sRGB), x _EmissionStrength 8.
        /// </summary>
        static Material GlobeMaterial(MaterialLibrary mats) =>
            mats.Variant(MatId.GradientCard, "b3globe", m =>
            {
                m.SetColor(ShaderIds.CardTop, AbyssPalette.AMBER);
                Color limb = GLOBE_LIMB_SRGB.linear * (GLOBE_LIMB_GAIN / GLOBE_CORE_STRENGTH);
                limb = limb.gamma;
                limb.a = 1f;
                m.SetColor(ShaderIds.CardBottom, limb);
                m.SetFloat(BeatKit.CardModeId, 0f);
                m.SetFloat("_CardFog", 1f);
                m.SetFloat(ShaderIds.EmissionStrength, GLOBE_CORE_STRENGTH);
                m.SetFloat("_Cull", (float)CullMode.Back);
            });

        static Material HaloMaterial(MaterialLibrary mats) =>
            mats.Variant(MatId.Glow, "b3halo", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.AMBER.linear);   // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, GLOBE_HALO_STRENGTH);
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.SetFloat("_EnergyConserve", 1f);
            });

        static Material LampArmMaterial(MaterialLibrary mats) =>
            mats.Variant(MatId.SteelPainted, "b3lamparm", m =>
            {
                // F0 = lerp(0.04, albedo, metallic) ~ 0.009: the lamp's own spot reaches the arm top 0.45 m below the globe
                // centre at E ~ 20-40, where STEEL_DARK paint with wear (f02) rendered a clipped hot point beside the globe
                m.SetColor(ShaderIds.BaseColor, LAMP_ARM_SRGB);
                m.SetFloat(ShaderIds.Roughness, 0.92f);
                m.SetFloat(ShaderIds.Metallic, 0.85f);
                m.SetFloat("_RustAmount", 0f);
                m.SetFloat("_WearAmount", 0f);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            });

        static Bounds DetailBounds()
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(AbyssConst.HALL_X0 - 1f, AbyssConst.W[8].y - 14f, AbyssConst.ARC_CENTRE.y - R_OUT - 2f),
                new Vector3(R_OUT + 1f, AbyssConst.W[6].y + 3f, AbyssConst.APRON_Z1 + 1f));
            return b;
        }

        // ================================================================================================ per frame

        public override void Tick(in BeatFrame f)
        {
            if (_detail != null) _detail.Update(f.CameraPos);
            FaceGlobes(f.CameraPos);
        }

        /// <summary>Turn every globe's pole (local +Z, the hot core of its gradient) toward the camera. No allocation.</summary>
        public void FaceGlobes(Vector3 cameraPos)
        {
            for (int i = 0; i < _globe.Length; i++)
            {
                Transform g = _globe[i];
                if (g == null) continue;
                Vector3 d = cameraPos - _lampHead[i];
                if (d.sqrMagnitude < 1e-8f) continue;
                d.Normalize();
                g.rotation = Quaternion.LookRotation(d, Mathf.Abs(d.y) > 0.99f ? Vector3.forward : Vector3.up);
            }
        }

        // ================================================================================================ QA hooks

        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            if (keyShotTag != "KS3_column_up") return;
            if (_hatchLeaf != null)
                subjects.Add(new KeyShotSubject("hatch", BeatKit.Union(_hatchFrame, _hatchLeaf), 0.1f));
            if (_lampBuilt[2])
                subjects.Add(new KeyShotSubject("amber lamp alpha 2.2", new Bounds(_lampHead[2], Vector3.one * (2f * LAMP_GLOBE_R))));
            float R = AbyssConst.COLUMNS[0].z;
            // the visible stretch of the y = 0 band (alpha 1.7-2.6) and of the face with its pour lifts: from the KS3 eye (alpha
            // pi/2, r 25) the face is seen from alpha ~1.6 to the tangent at 2.07, up to y ~ 8 near the silhouette
            var band = new Bounds(Ring(1.7, R, 0f), Vector3.zero);
            var face = new Bounds(Ring(1.7, R, -4f), Vector3.zero);
            for (int k = 0; k <= 9; k++)
            {
                double a = 1.7 + 0.1 * k;
                band.Encapsulate(Ring(a, R + AbyssConst.COLUMN_BAND_PROTRUSION, -AbyssConst.COLUMN_BAND_H * 0.5f));
                band.Encapsulate(Ring(a, R + AbyssConst.COLUMN_BAND_PROTRUSION, AbyssConst.COLUMN_BAND_H * 0.5f));
            }
            for (int k = 0; k <= 6; k++)
            {
                double a = 1.7 + 0.05 * k;
                face.Encapsulate(Ring(a, R, -4f));
                face.Encapsulate(Ring(a, R, 6f));
            }
            subjects.Add(new KeyShotSubject("y = 0 collar band", band));
            subjects.Add(new KeyShotSubject("column face pour lifts", face, 0.3f));
        }

        public override void ReportScale(ScaleReport report)
        {
            Mesh helix = HelixTopMesh, pad = PadTopMesh;
            Mesh body = _body != null ? _body.GetComponent<MeshFilter>().sharedMesh : null;
            Vector2 c1 = AbyssConst.ARC_CENTRE;
            if (helix != null && body != null)
            {
                // width: the column face (or the collar's own inner edge, whichever is outer) to the outer face of the collar
                BeatKit.RadialRange(body, Vector3.zero, c1, 0f, out _, out float rMax);
                float rMin = float.MaxValue;
                Vector3[] hv = helix.vertices;
                for (int i = 0; i < hv.Length; i++) rMin = Mathf.Min(rMin, Radius(hv[i]));
                report.Add("ledge.width", rMax - Mathf.Max(rMin, R_FACE));
                report.Add("ledge.column.overlap", R_FACE - rMin);

                // arc (alpha span of the helix at the centreline radius) and drop of its top surface
                Vector3[] n = helix.normals;
                double aMin = double.MaxValue, aMax = double.MinValue;
                float yTopMax = float.MinValue, yTopMin = float.MaxValue;
                for (int i = 0; i < hv.Length; i++)
                {
                    double alpha = AlphaOf(hv[i].x, hv[i].z);
                    aMin = Math.Min(aMin, alpha);
                    aMax = Math.Max(aMax, alpha);
                    if (n[i].y > 0.9f) { yTopMax = Mathf.Max(yTopMax, hv[i].y); yTopMin = Mathf.Min(yTopMin, hv[i].y); }
                }
                report.Add("ledge.arc", (aMax - aMin) * AbyssConst.LEDGE_R_CENTRE);
                report.Add("ledge.drop", yTopMax - yTopMin);

                // collar thickness at the outer face, corbel depth below it, kerb height and width
                Vector3[] bv = body.vertices;
                float thick = 0f, depth = 0f, kerbTop = float.MinValue, kerbIn = float.MaxValue;
                for (int i = 0; i < bv.Length; i++)
                {
                    Vector3 p = bv[i];
                    float r = Radius(p);
                    float below = TopY(AlphaOf(p.x, p.z)) - p.y;
                    if (r >= R_OUT - 0.1f) thick = Mathf.Max(thick, below);
                    depth = Mathf.Max(depth, below);
                    if (-below > 1e-3f) { kerbTop = Mathf.Max(kerbTop, -below); kerbIn = Mathf.Min(kerbIn, r); }
                }
                report.Add("ledge.thickness", thick);
                report.Add("ledge.corbel.drop", depth - thick);
                if (kerbTop > 0f)
                {
                    report.Add("ledge.kerb.height", kerbTop);
                    report.Add("ledge.kerb.width", rMax - kerbIn);
                }
                // the pad: flat at the bridge deck height, its end face on the plane x = -3 (the corridor edge)
                if (pad != null) report.Add("ledge.pad.y", pad.bounds.max.y);
                report.Add("ledge.pad.end.x", body.bounds.min.x);
            }
            if (_hatchLeaf != null)
            {
                Mesh leaf = _hatchLeaf.GetComponent<MeshFilter>().sharedMesh;
                report.Add("hatch.height", leaf.bounds.size.y);
                Frame(AbyssConst.HATCH_ALPHA, out _, out Vector3 tan);
                Vector3[] lv = leaf.vertices;
                float u0 = float.MaxValue, u1 = float.MinValue;
                for (int i = 0; i < lv.Length; i++) { float u = Vector3.Dot(lv[i], tan); u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u); }
                report.Add("hatch.width", u1 - u0);
            }
            if (_apron != null)
            {
                Bounds b = _apron.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("apron.length", b.size.x);
                report.Add("apron.width", b.size.z);
            }
            report.Add("lamp.count", _lampCount);
            for (int i = 0; i < _lampBuilt.Length; i++)
            {
                if (!_lampBuilt[i]) continue;
                Vector3 h = _lampHead[i];
                report.Add("lamp.head.above.floor", h.y - TopY(AlphaOf(h.x, h.z)));
                break;
            }
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);

        // ================================================================================================ ledge geometry (public: QA, tests)

        /// <summary>
        /// Walking-surface height at alpha (14.4): y falls linearly from W6 (-3) at alpha 0 to W8 (-7) at alpha pi; flat before
        /// alpha 0 (the pad) and after alpha pi (the apron). The visible collar top and the collider both use this function.
        /// </summary>
        public static float TopY(double alpha)
        {
            double y0 = AbyssConst.W[6].y, y1 = AbyssConst.W[8].y;
            if (alpha <= 0.0) return (float)y0;
            if (alpha >= Math.PI) return (float)y1;
            return (float)(y0 + (y1 - y0) * (alpha / Math.PI));
        }

        /// <summary>dTopY / dalpha (the mean of both sides at the kinks alpha 0 and pi, for smooth vertex normals).</summary>
        static double TopSlope(double alpha)
        {
            double s = (AbyssConst.W[8].y - AbyssConst.W[6].y) / Math.PI;
            if (alpha < 0.0 || alpha > Math.PI) return 0.0;
            if (alpha == 0.0 || alpha == Math.PI) return 0.5 * s;
            return s;
        }

        /// <summary>Alpha of a world position around C1 (0 at -Z from C1, pi/2 at +X, pi at +Z), in [-pi/2, 3 pi/2).</summary>
        public static double AlphaOf(float x, float z)
        {
            Vector2 c = AbyssConst.ARC_CENTRE;
            double a = Math.Atan2((double)x - c.x, (double)c.y - z);
            if (a < -0.5 * Math.PI) a += 2.0 * Math.PI;
            return a;
        }

        /// <summary>Height of the walking surface (collar, pad or apron) under a world position on the ledge (QA reference).</summary>
        public static float SurfaceY(float x, float z) => TopY(AlphaOf(x, z));

        /// <summary>Point at angle alpha (14.4 convention) and radius r around C1, at height y. Exact at alpha 0, pi/2 and pi.</summary>
        public static Vector3 Ring(double alpha, float r, float y)
        {
            SinCos(alpha, out double s, out double c);
            Vector2 o = AbyssConst.ARC_CENTRE;
            return new Vector3((float)(o.x + r * s), y, (float)(o.y - r * c));
        }

        static void SinCos(double a, out double s, out double c)
        {
            if (a == 0.0) { s = 0.0; c = 1.0; return; }
            if (a == 0.5 * Math.PI) { s = 1.0; c = 0.0; return; }
            if (a == Math.PI) { s = 0.0; c = -1.0; return; }
            s = Math.Sin(a); c = Math.Cos(a);
        }

        /// <summary>Outward radial and tangent (direction of increasing alpha) at alpha, horizontal unit vectors.</summary>
        static void Frame(double a, out Vector3 radial, out Vector3 tangent)
        {
            SinCos(a, out double s, out double c);
            radial = new Vector3((float)s, 0f, (float)-c);
            tangent = new Vector3((float)c, 0f, (float)s);
        }

        /// <summary>Rotation of a part lying on the ramp at radius r: local x = outward radial, y = surface up, z = rising tangent.</summary>
        static Quaternion FloorRot(double a, float r)
        {
            Frame(a, out _, out Vector3 tan);
            float slope = (float)(TopSlope(a) / r);
            return Quaternion.LookRotation((tan + Vector3.up * slope).normalized, Vector3.up);
        }

        /// <summary>Alpha at which radius r meets the plane x = cutX (cutX &lt; 0); -infinity for cutX &gt;= 0 (no cut).</summary>
        static double CutAlpha(float r, float cutX)
        {
            if (cutX >= 0f) return double.NegativeInfinity;
            return -Math.Asin(Math.Min(1.0, -(double)cutX / r));
        }

        static float Radius(Vector3 p)
        {
            Vector2 c = AbyssConst.ARC_CENTRE;
            float dx = p.x - c.x, dz = p.z - c.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Walking surface (collar top and pad): the worn-floor recipe with CONC_MID albedo x LEDGE_FLOOR_ALBEDO. The Key lights
        /// the ledge floor almost head-on (pitch -72), so at CONC_MID it was the brightest large plane of every beat-3 frame
        /// (luma 0.19-0.20 over 40 % of B3_middle, which then sat above the 0.15 beat-3 cap with the cold pool on the face).
        /// </summary>
        static Material FloorMaterial(MaterialLibrary mats) =>
            mats.Variant(MatId.ConcreteFloor, "b3floor", m =>
            {
                Color c = AbyssPalette.CONC_MID.linear * LEDGE_FLOOR_ALBEDO;
                c = c.gamma;
                c.a = 1f;
                m.SetColor(ShaderIds.BaseColor, c);
            });

        const float LEDGE_FLOOR_ALBEDO = 0.85f;

        static Material CollarMaterial(MaterialLibrary mats)
        {
            // board-formed cylinder recipe at the collar's outer radius: 147 panels and 4 expansion joints, so the fascia's
            // joints (alpha 0, pi/2, pi) line up radially with the column's (_CylJoints 4 at R 22)
            return mats.Variant(MatId.ConcreteCyl, "b3collar", m =>
            {
                m.SetFloat("_CylRadius", AbyssConst.LEDGE_R_OUT);
                m.SetFloat("_PanelCount", Mathf.Round(2f * Mathf.PI * AbyssConst.LEDGE_R_OUT / 1.2f));
                m.SetFloat("_CylJoints", 4f);
            });
        }
    }
}
