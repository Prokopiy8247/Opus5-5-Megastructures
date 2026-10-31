using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Lights of the full and simplified tiers (all children of the interior, so they switch with it; no shadows, explicit
    /// ranges). The 14.2 rig: 8 ceiling spot cans hung at y +22 on stems from the roof (70 degrees, range 60, SPOT_CAN) over
    /// the cylinders, and 6 amber hazard points (I 3.5, range 8): the four crank pits and the two openings (in the jamb, so
    /// the pool reaches both sides). Art direction (lead: local light pools, 4 depth layers, no flat darkness), all cold:
    /// a weak uplight on every can (halo on the ceiling that silhouettes the trusses), a cold uplight on every crank pit plate
    /// (the underside of each wheel, seen from the catwalk below its plane), a small spot every 10 m on the catwalk rail
    /// (pools on the grating aimed away from its own post, a receding line of points), wall washers on the side-wall pilasters every 20 m and four on
    /// the exit wall (scallops on the concrete behind the machines), two lintel washers that graze the exit wall above the
    /// opening (the backdrop the trusses converge on), and the daylight that spills in through both openings (the sunlit
    /// shaft of beats 3 and 5 outside). Placements keep the cones away from exterior geometry behind the walls: no washers
    /// on the portal wall (column C1 is 11 m behind it), exit washers at |v| >= 30 with ranges that stop short of the dock
    /// and the guide rails, lintel washers aimed up the wall (nothing above the dock within their range), spills placed in
    /// the mid plane of their reveal and aimed into the hall (the inner wall faces and everything outside lie behind them).
    ///
    /// Look-dev calibration (hall slot, KS4 against 14.10 beat 4: mean luma 0.08-0.18, p99 0.80, clipped 0.5 %): the 14.2
    /// intensities are starting values ("tune intensity first against the luma targets and leave ranges as specified");
    /// ranges and cone angles of the rig are unchanged. The beat-4 grade keeps contrast +14, which in URP's LogC toe maps
    /// v -> 1.14 v - 0.0085 after the +0.3 EV, i.e. every scene radiance below 0.006 is black: with the DOOR_LEAF machine
    /// paint (albedo 0.05) a surface needs about E 0.25 just to leave black, so the trilight ambient (x0.7 of the hall fog
    /// colours, E about 0.01) cannot carry anything and every readable surface is lit by a local light. Measured f01: 43 % of
    /// KS4 was pure black, mean 0.042.
    /// </summary>
    public sealed partial class Beat04_Hall
    {
        const float SPOT_S0 = 56f, SPOT_S1 = 68f;                       // can positions along the slider axis (cylinder centre 62 +/- 6)
        /// <summary>Full-tier spot can (14.2 starting value I 900): bright core on the cylinder, pool on the floor 43 m below.</summary>
        const float SPOT_I_FULL = 2000f;
        /// <summary>Inner cone of the cans (full angle): a hot core on the cylinder fading over the 70 degree cone into a pool.</summary>
        const float SPOT_INNER_ANGLE = 30f;
        const float CAN_UP_I = 420f, CAN_UP_RANGE = 22f, CAN_UP_ANGLE = 120f;
        const float CATWALK_LAMP_I = 4f, CATWALK_LAMP_RANGE = 4.5f, CATWALK_LAMP_SPACING = 10f;
        /// <summary>
        /// Catwalk lamp as a spot (critique R1-32: the point light 0.15 m from the post turned the post under it into a clipped
        /// bar and the top rail into a hot streak): source 0.28 m inboard of the rail, level with the fixture head, aimed down
        /// and tilted 35 degrees across the walkway; with a 100 degree cone the own post, top and mid rail lie 64-90 degrees off
        /// the axis (outside the cone) and the pool falls on the grating and the far kick plate.
        /// </summary>
        const float CATWALK_LAMP_INBOARD = 0.28f, CATWALK_LAMP_TILT = 35f, CATWALK_LAMP_ANGLE = 100f, CATWALK_LAMP_INNER = 50f;
        /// <summary>
        /// Extra catwalk lamp at the portal end (critique R2-10: in KS4 the grating 4-8 m ahead of the eye, u 2-6, was a uniform
        /// dark band because the first lamp stood at u 10): on the -Z top rail at u 3, between the u 2 and u 4 posts (the u 10
        /// lamp is on +Z); its 100 degree cone lights the grating at u 1.6-4.4, from the KS4 frame's lower edge (u 2) to 6 m
        /// from the KS4 eye, so the bars read against the shaded slots.
        /// </summary>
        const float CATWALK_LAMP_PORTAL_U = 3f;
        /// <summary>
        /// Lens emission of the hall's cold fixtures (critique R2-26: LampCold x 4 displayed as pure-white panels, 236-249
        /// levels, the hottest objects of B4_middle): x 0.85 keeps every channel at or below about 0.79 display after the beat-4
        /// EV +0.3 and the Neutral tonemapper (and below the bloom threshold). Amber lenses (hazard jambs, crank pits) follow
        /// the R2 lead decision D4 (small amber emitters at most 2.5 so the Neutral tonemapper keeps them amber).
        /// </summary>
        const float LENS_COLD_STRENGTH = 0.85f, LENS_AMBER_STRENGTH = 2.5f;
        /// <summary>Catwalk lamp end lenses: recessed in a 35 mm hood behind two 6 mm bars (ThinLine), R2-26.</summary>
        const float LAMP_HOOD = 0.035f, LAMP_BAR_R = 0.003f;

        Material ColdLensMat() => _mats.Variant(MatId.LampCold, "b4lens", m => m.SetFloat(ShaderIds.EmissionStrength, LENS_COLD_STRENGTH));
        Material AmberLensMat() => _mats.Variant(MatId.LampAmber, "b4amberlens", m => m.SetFloat(ShaderIds.EmissionStrength, LENS_AMBER_STRENGTH));

        /// <summary>Hall u of catwalk lamp i: the portal-end lamp (i 0) at u 3, then every 10 m.</summary>
        public static float CatwalkLampU(int i) => i == 0 ? CATWALK_LAMP_PORTAL_U : i * CATWALK_LAMP_SPACING;
        /// <summary>
        /// Wheel uplights (critique R1-20: let the light pick out the crank wheels; the walker sees each wheel from below, 4-8 m
        /// above the eye, where the ceiling cans cannot reach): one cold flood per bank on the pit plate 5.7 m inboard of the
        /// crankshaft, aimed straight up and tilted 10 degrees further inboard (the polished shaft base stays 64 degrees off the
        /// axis, outside the 90 degree cone), lighting the underside of the wheel half that faces the catwalk ~20 m above.
        /// </summary>
        const float WHEEL_UP_I = 800f, WHEEL_UP_RANGE = 34f, WHEEL_UP_ANGLE = 90f, WHEEL_UP_INNER = 40f, WHEEL_UP_R = 5.7f, WHEEL_UP_TILT = 10f;
        /// <summary>
        /// Cylinder flank uplights of the two inner banks (critique R1-20: the catwalk sees the inner cylinders' flank and
        /// underside 10 m above the eye, which the ceiling cans only graze; hall_r10 KS4 cylinder B p50 0.10): a cold flood on
        /// the floor under the cylinder centre, 9 m inboard of its axis (3 m outside the barrel, 4.5 m outside the floor pipe),
        /// aimed up and tilted 20 degrees away from the catwalk, straight at the cylinder axis 25 m above; the catwalk deck lies
        /// 51 degrees off its axis and the concrete piers (8.5 m to either side along the slider axis) 35-45 degrees, outside
        /// the 70 degree cone (hall_r11: a 90 degree cone lit the piers and pushed KS4 16:10 to a mean of 0.180).
        /// </summary>
        const float CYL_UP_I = 1200f, CYL_UP_RANGE = 36f, CYL_UP_ANGLE = 70f, CYL_UP_INNER = 30f, CYL_UP_V = 9f, CYL_UP_TILT = 20f;
        /// <summary>Slider-axis station of the cylinder centre (bank frame).</summary>
        const float CYL_CENTRE_S = CYL_S0 + 0.5f * CYL_LEN;
        /// <summary>Washers 6 m under the truss bottom chords: scallops from about y 20 down to the floor spill (the end wall fills ~55 % of KS4).</summary>
        const float WASH_I = 1000f, WASH_RANGE = 34f, WASH_ANGLE = 110f, WASH_Y = 22f, WASH_OUT = 4f, WASH_AIM_DROP = 18f;
        static readonly float[] SIDE_WASH_U = { 15f, 35f, 55f, 75f, 95f };
        static readonly float[] EXIT_WASH_V = { -50f, -30f, 30f, 50f };
        /// <summary>
        /// Lintel washer: one flood on a 6 m bracket 1 m above the centre of the exit opening, aimed at the wall 20 m higher
        /// with a soft edge (inner cone 30 of 110 degrees), so the wall over the cold exit rectangle glows in one fan that
        /// reaches the truss ends under the roof (KS4 centre backdrop; two side-by-side floods read as a hard double scallop).
        /// The nearest exterior parts (platform pylons at x &lt;= -152, about 22 m away) lie 50 degrees off its axis, where the
        /// soft edge has already faded to under 5 %.
        /// </summary>
        const float LINTEL_WASH_I = 260f, LINTEL_WASH_RANGE = 32f, LINTEL_WASH_ANGLE = 110f, LINTEL_WASH_INNER = 30f, LINTEL_WASH_OUT = 6f,
            LINTEL_WASH_RISE = 20f;
        /// <summary>
        /// Daylight spill through the openings (cold, COLD_LAMP): a spot in the mid plane of each reveal just under the lintel,
        /// pitched down into the hall. The portal spill lights the faces the walker sees (everything faces +X toward the portal),
        /// the exit spill backlights the machines (rims on rods, rails and pipes, a floor gradient toward the exit) and makes the
        /// exit read as the source of the cold light 82 m ahead. Range 105 ends before the opposite end wall.
        /// </summary>
        const float PORTAL_SPILL_I = 650f, EXIT_SPILL_I = 2400f, SPILL_RANGE = 105f, SPILL_ANGLE = 120f, SPILL_INNER_ANGLE = 40f,
            SPILL_Y = 3f, SPILL_PITCH = 8f;
        /// <summary>
        /// Haze shaft of the exit spill (BeamWedge mode 2, SHADERS 11): the 20 x 24 opening extruded 45 m into the hall along
        /// the spill direction (fading out over its last 45 %), COLD_LAMP, density per metre. Seen from the catwalk it is a
        /// soft cold glow around the exit (forward scatter, about +0.03 linear end-on: "far hall haze"); seen from the dock it
        /// is backscatter and nearly invisible. Near-camera fade 2-10 m (walking through it never floods the frame) and a
        /// saturation length for the end-on chord.
        /// </summary>
        const float EXIT_SHAFT_LENGTH = 45f, EXIT_SHAFT_DENSITY = 0.0009f, EXIT_SHAFT_NEAR0 = 2f, EXIT_SHAFT_NEAR1 = 10f, EXIT_SHAFT_SAT = 40f;
        static readonly int BeamParamsId = Shader.PropertyToID("_BeamParams");
        static readonly int BeamDirId = Shader.PropertyToID("_BeamDir");
        static readonly int BeamRectId = Shader.PropertyToID("_BeamRect");
        static readonly int BeamShapeId = Shader.PropertyToID("_BeamShape");
        MeshRenderer _exitShaft;

        void BuildFullLights(BeatContext ctx, Transform interior)
        {
            Transform lights = BeatKit.Group(interior, "Lights");
            var steel = new MeshBuilder();
            var thin = new MeshBuilder();
            var cold = new MeshBuilder();
            var amber = new MeshBuilder();
            float zc = AbyssConst.W[8].z;
            int spots = 0, hazards = 0, n = 0;

            // ---- spot cans over the cylinders (+ uplights), hazard points at the pits
            for (int k = 0; k < 4; k++)
            {
                if (!_bankBuilt[k]) continue;
                Matrix4x4 m = BankMatrix(k);
                for (int i = 0; i < 2; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3(i == 0 ? SPOT_S0 : SPOT_S1, 0f, 0f));
                    float x = p.x, z = p.z;
                    MeshKit.Cylinder(thin, new Vector3(x, SPOT_Y + 2.0f, z), new Vector3(x, CEILING_Y + 0.1f, z), 0.06f, 8, true, true);
                    s_prof.Clear();
                    P(0.80f, SPOT_Y + 0.75f); P(0.85f, SPOT_Y + 0.8f); P(0.85f, SPOT_Y + 1.0f); P(0.42f, SPOT_Y + 1.9f); P(0.42f, SPOT_Y + 2.1f);
                    P(0.36f, SPOT_Y + 2.16f);
                    LatheAt(steel, new Vector3(x, 0f, z), Vector3.up, 32, true);
                    MeshKit.Disc(cold, new Vector3(x, SPOT_Y + 0.74f, z), Vector3.down, 0.74f, 32);
                    Light can = BeatKit.Spot(lights, "SpotCan" + spots, new Vector3(x, SPOT_Y + 0.6f, z), Vector3.down, AbyssPalette.SPOT_CAN,
                        SPOT_I_FULL, SPOT_RANGE, SPOT_ANGLE);
                    can.innerSpotAngle = SPOT_INNER_ANGLE;
                    BeatKit.Spot(lights, "SpotCanUp" + spots, new Vector3(x, SPOT_Y + 2.4f, z), Vector3.up, AbyssPalette.SPOT_CAN, CAN_UP_I, CAN_UP_RANGE, CAN_UP_ANGLE);
                    spots++;
                }
                Vector3 h = m.MultiplyPoint3x4(PIT_LAMP + new Vector3(0f, 0.35f, 0f));
                BeatKit.Point(lights, "HazardPit" + k, h, AbyssPalette.AMBER_LIGHT, HAZARD_I, HAZARD_RANGE);
                hazards++;

                // wheel uplight on the pit plate, inboard of the crankshaft (world: toward the catwalk)
                Vector3 c = BankOrigin(k);
                float inward = -Mathf.Sign(AbyssConst.BANK_V[k]);
                float yPlate = AbyssConst.HALL_FLOOR_Y + 0.05f;
                var up0 = new Vector3(c.x, yPlate, c.z + inward * WHEEL_UP_R);
                MeshKit.ChamferBox(steel, up0 + new Vector3(0f, 0.14f, 0f), new Vector3(0.6f, 0.3f, 0.6f), 0.02f);
                MeshKit.Disc(cold, up0 + new Vector3(0f, 0.295f, 0f), Vector3.up, 0.22f, 24);
                float tilt = WHEEL_UP_TILT * Mathf.Deg2Rad;
                Light wl = BeatKit.Spot(lights, "WheelUplight" + k, up0 + new Vector3(0f, 0.45f, 0f),
                    new Vector3(0f, Mathf.Cos(tilt), inward * Mathf.Sin(tilt)), AbyssPalette.SPOT_CAN, WHEEL_UP_I, WHEEL_UP_RANGE, WHEEL_UP_ANGLE);
                wl.innerSpotAngle = WHEEL_UP_INNER;

                // cylinder flank uplight (inner banks only: the outer cylinders are never seen from below at close range)
                if (!InnerBank(k)) continue;
                var cu = new Vector3(c.x + AxisX(k) * CYL_CENTRE_S, AbyssConst.HALL_FLOOR_Y, c.z + inward * CYL_UP_V);
                MeshKit.ChamferBox(steel, cu + new Vector3(0f, 0.14f, 0f), new Vector3(0.7f, 0.3f, 0.7f), 0.02f);
                MeshKit.Disc(cold, cu + new Vector3(0f, 0.295f, 0f), Vector3.up, 0.26f, 24);
                float ct = CYL_UP_TILT * Mathf.Deg2Rad;
                Light cyl = BeatKit.Spot(lights, "CylinderUplight" + k, cu + new Vector3(0f, 0.45f, 0f),
                    new Vector3(0f, Mathf.Cos(ct), -inward * Mathf.Sin(ct)), AbyssPalette.SPOT_CAN, CYL_UP_I, CYL_UP_RANGE, CYL_UP_ANGLE);
                cyl.innerSpotAngle = CYL_UP_INNER;
            }

            // ---- hazard points in the +Z jamb of both openings (amber lens, housing 1 cm into the jamb)
            float jz = zc + AbyssConst.HALL_OPENING_W * 0.5f, hy = AbyssConst.CATWALK_Y + 2.8f;
            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? AbyssConst.HALL_X0 - HULL_WALL_T * 0.5f : AbyssConst.HALL_X1 + HULL_WALL_T * 0.5f;
                MeshKit.ChamferBox(steel, new Vector3(x, hy, jz - 0.12f), new Vector3(0.3f, 0.3f, 0.26f), 0.02f);
                MeshKit.ChamferBox(amber, new Vector3(x, hy, jz - 0.255f), new Vector3(0.22f, 0.12f, 0.03f), 0.005f);
                BeatKit.Point(lights, e == 0 ? "HazardPortal" : "HazardExit", new Vector3(x, hy, jz - 0.6f), AbyssPalette.AMBER_LIGHT, HAZARD_I, HAZARD_RANGE);
                hazards++;
            }

            // ---- catwalk lamps on the rail posts (u 4, then every 10 m), alternating sides: head on the top rail, hooded and
            //      barred lenses toward both walk directions
            float railTop = AbyssConst.CATWALK_Y + AbyssConst.RAIL_H;
            for (int i = 0; CatwalkLampU(i) < AbyssConst.HALL_X0 - AbyssConst.HALL_X1 - 1f; i++)
            {
                float x = X(CatwalkLampU(i)), s = (i & 1) == 1 ? 1f : -1f, z = zc + s * RAIL_V, yh = railTop + 0.05f;
                MeshKit.ChamferBox(steel, new Vector3(x, yh, z), new Vector3(0.22f, 0.12f, 0.16f), 0.01f);
                for (int d = -1; d <= 1; d += 2)
                {
                    // lens 2 mm into the head, 10 mm proud; a 35 mm hood around it; two bars across the hood mouth
                    MeshKit.ChamferBox(cold, new Vector3(x + d * 0.114f, yh, z), new Vector3(0.012f, 0.068f, 0.118f), 0.003f);
                    float hx = x + d * (0.11f + 0.5f * LAMP_HOOD - 0.002f), hl = LAMP_HOOD + 0.004f;
                    MeshKit.ChamferBox(steel, new Vector3(hx, yh + 0.04f, z), new Vector3(hl, 0.01f, 0.14f), 0.002f);
                    MeshKit.ChamferBox(steel, new Vector3(hx, yh - 0.04f, z), new Vector3(hl, 0.01f, 0.14f), 0.002f);
                    MeshKit.ChamferBox(steel, new Vector3(hx, yh, z + 0.065f), new Vector3(hl, 0.07f, 0.01f), 0.002f);
                    MeshKit.ChamferBox(steel, new Vector3(hx, yh, z - 0.065f), new Vector3(hl, 0.07f, 0.01f), 0.002f);
                    float bx = x + d * (0.11f + LAMP_HOOD - 0.006f);
                    for (int b = -1; b <= 1; b += 2)
                        MeshKit.Cylinder(thin, new Vector3(bx, yh + b * 0.012f, z - 0.0645f), new Vector3(bx, yh + b * 0.012f, z + 0.0645f), LAMP_BAR_R, 6, false, true);
                }
                // inboard lens (the side the spot shines from), hooded and barred like the end lenses
                float yi = railTop + 0.04f;
                MeshKit.ChamferBox(cold, new Vector3(x, yi, z - s * 0.085f), new Vector3(0.138f, 0.058f, 0.02f), 0.004f);
                float iz = z - s * (0.08f + 0.5f * LAMP_HOOD - 0.002f), il = LAMP_HOOD + 0.004f;
                MeshKit.ChamferBox(steel, new Vector3(x, yi + 0.035f, iz), new Vector3(0.16f, 0.01f, il), 0.002f);
                MeshKit.ChamferBox(steel, new Vector3(x, yi - 0.035f, iz), new Vector3(0.16f, 0.01f, il), 0.002f);
                MeshKit.ChamferBox(steel, new Vector3(x + 0.075f, yi, iz), new Vector3(0.01f, 0.06f, il), 0.002f);
                MeshKit.ChamferBox(steel, new Vector3(x - 0.075f, yi, iz), new Vector3(0.01f, 0.06f, il), 0.002f);
                float bz = z - s * (0.08f + LAMP_HOOD - 0.006f);
                for (int b = -1; b <= 1; b += 2)
                    MeshKit.Cylinder(thin, new Vector3(x - 0.0745f, yi + b * 0.01f, bz), new Vector3(x + 0.0745f, yi + b * 0.01f, bz), LAMP_BAR_R, 6, false, true);
                float tilt = CATWALK_LAMP_TILT * Mathf.Deg2Rad;
                Light cl = BeatKit.Spot(lights, "CatwalkLamp" + i, new Vector3(x, railTop + 0.05f, zc + s * (RAIL_V - CATWALK_LAMP_INBOARD)),
                    new Vector3(0f, -Mathf.Cos(tilt), -s * Mathf.Sin(tilt)), AbyssPalette.FIXTURE_LIGHT, CATWALK_LAMP_I, CATWALK_LAMP_RANGE,
                    CATWALK_LAMP_ANGLE);
                cl.innerSpotAngle = CATWALK_LAMP_INNER;
                n++;
            }

            // ---- wall washers: side-wall pilasters every 20 m, exit wall at |v| 30 and 50
            float t = HULL_WALL_T;
            for (int i = 0; i < SIDE_WASH_U.Length; i++)
            {
                float x = X(SIDE_WASH_U[i]);
                for (int s = 0; s < 2; s++)
                {
                    float face = s == 0 ? AbyssConst.HALL_Z0 + t + PILASTER_D : AbyssConst.HALL_Z1 - t - PILASTER_D;
                    var inward = new Vector3(0f, 0f, s == 0 ? 1f : -1f);
                    WallWasher(lights, steel, cold, new Vector3(x, WASH_Y, face), inward, "WashSide" + (s * 10 + i));
                }
            }
            for (int i = 0; i < EXIT_WASH_V.Length; i++)
                WallWasher(lights, steel, cold, new Vector3(AbyssConst.HALL_X1 + t, WASH_Y, zc + EXIT_WASH_V[i]), Vector3.right, "WashExit" + i);

            // ---- lintel washer over the exit opening (aimed up the wall)
            float lintelY = OPENING_Y0 + AbyssConst.HALL_OPENING_H + 1f;
            Light lintel = WallWasher(lights, steel, cold, new Vector3(AbyssConst.HALL_X1 + t, lintelY, zc), Vector3.right, "WashLintel",
                LINTEL_WASH_OUT, -LINTEL_WASH_RISE, LINTEL_WASH_I, LINTEL_WASH_RANGE, LINTEL_WASH_ANGLE);
            lintel.innerSpotAngle = LINTEL_WASH_INNER;

            // ---- daylight spill through the portal (into -X) and the exit (into +X)
            OpeningSpill(lights, "SpillPortal", AbyssConst.HALL_X0 - 0.5f * t, -1f, PORTAL_SPILL_I);
            OpeningSpill(lights, "SpillExit", AbyssConst.HALL_X1 + 0.5f * t, 1f, EXIT_SPILL_I);
            BuildExitShaft(interior);

            BeatKit.Emit(steel, interior, "LampHousings", Nokey(MatId.SteelPainted), _meshes, ShadowCastingMode.Off);
            BeatKit.Emit(thin, interior, "SpotStems", ThinMat(), _meshes, ShadowCastingMode.Off);
            BeatKit.Emit(cold, interior, "LampLensesCold", ColdLensMat(), _meshes, ShadowCastingMode.Off);
            BeatKit.Emit(amber, interior, "LampLensesAmber", AmberLensMat(), _meshes, ShadowCastingMode.Off);
            _spotCount = spots;
            _hazardCount = hazards;
        }

        void WallWasher(Transform lights, MeshBuilder steel, MeshBuilder cold, Vector3 wallPoint, Vector3 n, string name)
            => WallWasher(lights, steel, cold, wallPoint, n, name, WASH_OUT, WASH_AIM_DROP, WASH_I, WASH_RANGE, WASH_ANGLE);

        /// <summary>
        /// Flood on a bracket 'outDist' from the wall face (inward normal n), aimed back at the wall 'aimDrop' below the head
        /// (negative: above): scallop pool.
        /// </summary>
        Light WallWasher(Transform lights, MeshBuilder steel, MeshBuilder cold, Vector3 wallPoint, Vector3 n, string name,
            float outDist, float aimDrop, float intensity, float range, float angle)
        {
            Vector3 head = wallPoint + n * outDist;
            Vector3 aim = (-n * outDist + Vector3.down * aimDrop).normalized;
            MeshKit.ChamferMember(steel, wallPoint - n * 0.05f, head - n * 0.2f, 0.16f, 0.16f, 0.01f, Vector3.up);
            Quaternion rot = Quaternion.LookRotation(aim, Vector3.up);
            MeshKit.ChamferBox(steel, head, new Vector3(0.7f, 0.45f, 0.5f), 0.02f, rot);
            MeshKit.ChamferBox(cold, head + aim * 0.255f, new Vector3(0.56f, 0.33f, 0.03f), 0.005f, rot);
            return BeatKit.Spot(lights, name, head + aim * 0.4f, aim, AbyssPalette.FIXTURE_LIGHT, intensity, range, angle);
        }

        /// <summary>
        /// Daylight spill of one opening: a cold spot in the mid plane of the reveal (x), 2 m under the lintel on the walk axis,
        /// travelling along dirX into the hall and pitched down. The inner face of its own wall faces away from it, so only
        /// the hall interior in front of the opening receives it.
        /// </summary>
        static void OpeningSpill(Transform lights, string name, float x, float dirX, float intensity)
        {
            Light l = BeatKit.Spot(lights, name, new Vector3(x, SPILL_Y, AbyssConst.W[8].z), SpillDir(dirX), AbyssPalette.COLD_LAMP, intensity,
                SPILL_RANGE, SPILL_ANGLE);
            l.innerSpotAngle = SPILL_INNER_ANGLE;
        }

        /// <summary>World travel direction of an opening spill: along dirX (+1 into the hall from the exit), pitched down.</summary>
        static Vector3 SpillDir(float dirX)
        {
            float p = SPILL_PITCH * Mathf.Deg2Rad;
            return new Vector3(dirX * Mathf.Cos(p), -Mathf.Sin(p), 0f);
        }

        /// <summary>
        /// The exit haze shaft: a mode-2 BeamWedge prism with its object +Z along world +X (into the hall), the rectangle in the
        /// opening's mid plane. BeamWedge multiplies by the global _AbBeamFade, which stays 0 until the ride (the fan beams), so
        /// a per-renderer block pins it to 1 (set once here; the shaft switches with the interior). Part of the interior.
        /// </summary>
        void BuildExitShaft(Transform interior)
        {
            Vector3 dirWorld = SpillDir(1f);
            Quaternion rot = Quaternion.Euler(0f, 90f, 0f);
            Vector3 dirObject = Quaternion.Inverse(rot) * dirWorld;
            var half = new Vector2(AbyssConst.HALL_OPENING_W * 0.5f, AbyssConst.HALL_OPENING_H * 0.5f);
            Mesh prism = Beat01Geo.BeamPrism(new MeshBuilder(), "ExitSpillShaftPrism", _meshes, half, dirObject, EXIT_SHAFT_LENGTH);
            Material beam = _mats.Variant(MatId.BeamWedge, "b4exitShaft", m =>
            {
                m.SetFloat(ShaderIds.Recipe, 2f);
                m.SetVector(BeamParamsId, new Vector4(0f, 0f, 0f, EXIT_SHAFT_LENGTH));
                m.SetVector(BeamDirId, new Vector4(dirWorld.x, dirWorld.y, dirWorld.z, 0f));
                m.SetVector(BeamRectId, new Vector4(half.x, half.y, 0f, 0f));
                m.SetVector(BeamShapeId, new Vector4(0f, EXIT_SHAFT_NEAR0, EXIT_SHAFT_NEAR1, EXIT_SHAFT_SAT));
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, EXIT_SHAFT_DENSITY);
            });
            var origin = new Vector3(AbyssConst.HALL_X1 + 0.5f * HULL_WALL_T, OPENING_Y0 + half.y, AbyssConst.W[8].z);
            _exitShaft = BeatKit.Renderer(interior, "ExitSpillShaft", prism, beam, origin, rot, ShadowCastingMode.Off);
            _exitShaft.receiveShadows = false;
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat(ShaderIds.AbBeamFade, 1f);
            mpb.SetFloat(ShaderIds.AbFade, 1f);
            _exitShaft.SetPropertyBlock(mpb);
        }
    }
}
