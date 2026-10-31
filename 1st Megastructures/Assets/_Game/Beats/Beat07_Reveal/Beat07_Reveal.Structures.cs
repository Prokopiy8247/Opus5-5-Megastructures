using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 7 full tier: tunnel, the Face, balcony, pier and the door assembly (14.5.7 Geometry, 14.11 Ending).
    /// All near hard edges are chamfered 1-3 cm (Face-scale edges 5 cm), no two visible faces are coplanar (abutting solids
    /// meet along chamfer grooves or are embedded a few centimetres into each other), Face-scale meshes are chunked every
    /// 200 m with chunk-local vertices and the same xz origin.
    /// </summary>
    public sealed partial class Beat07_Reveal
    {
        // ---------------------------------------------------------------- tunnel
        const float LINING_CHAMFER = 0.06f;                       // inner wall/ceiling corner chamfer
        const float JOINT_SPACING = 12f, JOINT_W = 0.06f, JOINT_DEPTH = 0.03f;   // recessed segment joint every 12 m (45 degree sides)
        const float TRAY_Y = 2.62f, TRAY_W = 0.3f, TRAY_LIP = 0.08f, TRAY_T = 0.012f, CABLE_R = 0.022f;
        const int TRAY_CABLES = 3;
        const float BRACKET_SPACING = 3f, DRAIN_W = 0.3f, DRAIN_T = 0.015f, DRAIN_PLATE = 2.9f;
        const float PROPS_NEAR = 150f;

        // ---------------------------------------------------------------- Face
        const float PORTAL_OVERLAP = 0.05f, PORTAL_JAMB = 0.9f, PORTAL_LINTEL = 0.9f, PORTAL_DEPTH = 0.4f, PORTAL_EMBED = 0.05f;
        const float EDGE_BEVEL = 0.05f;                           // bore and mouth edges (14.5.7)
        /// <summary>
        /// Macro tone amplitude of the Face concrete (1 = the recipe's +/-12 % of the 9 m / 37 m value noise). The Face is
        /// 473 m of sunlit board-formed concrete seen from 70-300 m, where boards, ties and joints are all under a pixel: with
        /// the default amplitude it read as a smooth CG plane whose near-flat tone left 13-14 px runs of one 8-bit value in the
        /// KS7d "sky and haze" band (f01; at 1.7 the 21:9 frame still had 13-15 px runs on the upper Face corners, r01/r04).
        /// Pour-to-pour tone variation of +/-25 % is what a weathered wall of that size shows from a few hundred metres.
        /// </summary>
        const float FACE_MACRO_AMP = 2.2f;
        /// <summary>Walking toppings stay matte (14.2: concrete roughness 0.88; the floor recipe takes 0.12 off along the worn path).</summary>
        const float WALK_ROUGHNESS = 0.8f;
        /// <summary>
        /// R2-04 (2): the balcony and pier toppings are the highest-contrast near layer. At CONC_MID (linear 0.11) the sunlit
        /// pier (0.36-0.41) sat under the near web tops (CONC_LIGHT x 1.35 = 0.35 linear, 0.48-0.50), so the honeycomb read as a
        /// floor in front of the pier. The toppings are CONC_LIGHT x this (linear 0.36, sun-bleached exterior concrete), above
        /// the web tops (CONC_LIGHT x WEB_TOP_GAIN = 0.29) and unfogged; the kerbs and slab edges keep CONC_MID, so dark
        /// edge lines frame the light walk. reveal r21 at 1.38: pier 0.55 against web tops 0.47 p95, but B7_middle (34 % balcony
        /// floor at a level gaze) rose to 0.461 over the 0.45 band; 1.12 with the near web gain at 1.0 keeps the 0.05 margin.
        /// </summary>
        public const float WALK_ALBEDO_GAIN = 1.12f;
        /// <summary>
        /// R2-14: macro tone of the toppings (x +/-12 %): pours and wear read at 1x on the pier (f03: 1.1 levels high-pass,
        /// lead_b01 at 1.7: 1.86 levels against the 2-level target; 2.2 scales it to about 2.4).
        /// </summary>
        const float WALK_MACRO_AMP = 2.2f;
        /// <summary>
        /// R2-31: the bore collar's front outer edge is a 0.3 m chamfer (was 0.05): seen from KS7d the collar's shaded outer side
        /// was a 1 px dark "crack" around the bore; the chamfer faces the sun and the camera and reads as a lit bevel. The bore
        /// edge keeps the 0.05 of 14.5.7.
        /// </summary>
        const float COLLAR_OUTER_BEVEL = 0.3f;
        const float COLLAR_W = 3f, COLLAR_DEPTH = 1f, COLLAR_INSET = 0.1f, COLLAR_EMBED = 0.05f;
        const int COLLAR_SEGMENTS = 512;                          // smooth at 70 m; the 128-gon lining stays >= 5 cm outside it
        const float RIB_BAND_PROUD = 0.8f, RIB_BAND_H = 1.5f, BELT_BAND_PROUD = 2f, BELT_BAND_H = 4f, BAND_EMBED = 0.05f, BAND_BORE_MARGIN = 3f;
        /// <summary>
        /// R1-34: a band that crosses the bore runs into the collar and ends BAND_COLLAR_BURY inside its outer face (the end
        /// face is hidden in the ring), instead of stopping 3 m short of it with a free chamfered stub (KS7d, all aspects).
        /// </summary>
        const float BAND_COLLAR_BURY = 0.05f;
        const float ANCHOR_PLATE = 0.9f, ANCHOR_T = 0.17f, ANCHOR_EMBED = 0.02f;

        // ---------------------------------------------------------------- balcony and pier
        const float TOPPING_T = 0.05f, EDGE_INSET = 0.02f, SLAB_BEVEL = 0.03f;
        const float CORBEL_DROP = 8f, CORBEL_SIDE_INSET = 0.4f, CORBEL_FRONT_INSET = 0.6f;
        const float PIER_KERB_H = 0.10f, PIER_KERB_W = 0.15f, PIER_TOP_DROP = 0.005f;
        const float SUNK = 0.03f;                                 // kerbs, toppings and the door frame sit 3 cm into the slab (no slit, no shared face)

        // ---------------------------------------------------------------- door
        const float DOOR_CLEAR = 0.005f, LEAF_LIFT = 0.008f, SILL_T = 0.006f, ROD_FRAME_DX = 1.3f;
        /// <summary>
        /// R1-21: the frame reads as a heavy concrete portal rather than a cabin: 4 cm arrises on the blocks, and the leaf set
        /// back in a 0.4 m reveal (it stood 0.3 m in), with the amber backing 5 mm in front of the closed back panel and still
        /// 0.15 m behind the leaf (14.5.7). Leaf centre = back of the frame + this.
        /// </summary>
        const float FRAME_BEVEL = 0.04f, BACKING_CLEAR = 0.005f;
        const float LEAF_CENTRE_FROM_BACK = BACKING_CLEAR + BACKING_GAP + LEAF_T * 0.5f;
        /// <summary>Height of the door-lamp housing centre above the pier top.</summary>
        const float DOOR_LAMP_Y = 2.3f;
        /// <summary>Tension-rod ends (R1-21: clevis forks on thin lug plates instead of box clamps).</summary>
        const float LUG_T = 0.05f, LUG_W = 0.26f, CLEVIS_PLATE_T = 0.025f, CLEVIS_PLATE_H = 0.16f, CLEVIS_SLEEVE_R = 0.1f, CLEVIS_PIN_R = 0.022f;
        const float ROD_PIN_ABOVE_FRAME = 0.16f, ROD_PIN_OFF_ANCHOR = 0.2f;

        /// <summary>World z of the leaf's centre plane (closed) for the tier.</summary>
        static float LeafZ(BeatTier tier)
        {
            float zD = AbyssConst.W[18].z;
            return tier == BeatTier.Graybox ? zD + FRAME_DEPTH * 0.5f - LEAF_T * 0.5f : zD + LEAF_CENTRE_FROM_BACK;
        }

        static readonly CrossSection s_sec = new CrossSection();
        static readonly List<LoftFrame> s_frames = new List<LoftFrame>(8);
        static readonly List<Vector2> s_profile = new List<Vector2>(16);
        static readonly List<BeatKit.Rect2> s_holes = new List<BeatKit.Rect2>(4);
        static readonly float[] s_pierDepth = new float[3];

        static float BaseY => Yf - TOPPING_T;   // structural top of balcony and pier under the 5 cm topping (-487.05)

        // ================================================================================================ tunnel

        void BuildTunnelFull(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform t = BeatKit.Group(_geo, "Tunnel");
            float x = X, y = Yf, hw = AbyssConst.TUNNEL_W * 0.5f, h = AbyssConst.TUNNEL_H;
            float zM = AbyssConst.W[15].z, zF = AbyssConst.W[16].z;

            // ---- lining: open section (walls + chamfered ceiling corners) facing inward, frame forward -Z (frame right = world -X)
            s_sec.Clear(false);
            s_sec.Add(new Vector2(-hw, -LINING_SKIRT), true);
            s_sec.Add(new Vector2(-hw, h - LINING_CHAMFER), true);
            s_sec.Add(new Vector2(-hw + LINING_CHAMFER, h), true);
            s_sec.Add(new Vector2(hw - LINING_CHAMFER, h), true);
            s_sec.Add(new Vector2(hw, h - LINING_CHAMFER), true);
            s_sec.Add(new Vector2(hw, -LINING_SKIRT), true);
            Quaternion rot = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var recess = new Vector2((hw + JOINT_DEPTH) / hw, (h + JOINT_DEPTH) / h);
            float jh = JOINT_W * 0.5f;
            int joints = Mathf.RoundToInt((zM - zF) / JOINT_SPACING) - 1;   // 4 joints at z = 48, 36, 24, 12
            float segStart = zM + LINING_OVERLAP;
            for (int j = joints; j >= 0; j--)
            {
                float zj = zF + JOINT_SPACING * j;
                float segEnd = j > 0 ? zj + jh + JOINT_DEPTH : zF - LINING_OVERLAP;
                int first = mb.VertexCount;
                s_frames.Clear();
                s_frames.Add(new LoftFrame(new Vector3(x, y, segStart), rot));
                s_frames.Add(new LoftFrame(new Vector3(x, y, segEnd), rot));
                MeshKit.Loft(mb, s_sec, s_frames);
                if (j == joints) _liningB = BeatKit.VertexBounds(mb, first);
                if (j == 0) break;
                // recessed joint: 45 degree sides, 6 cm wide and 3 cm deep at the bottom
                s_frames.Clear();
                s_frames.Add(new LoftFrame(new Vector3(x, y, zj + jh + JOINT_DEPTH), rot));
                s_frames.Add(new LoftFrame(new Vector3(x, y, zj + jh), rot, recess, true));
                s_frames.Add(new LoftFrame(new Vector3(x, y, zj - jh), rot, recess, true));
                s_frames.Add(new LoftFrame(new Vector3(x, y, zj - jh - JOINT_DEPTH), rot));
                MeshKit.Loft(mb, s_sec, s_frames);
                segStart = zj - jh - JOINT_DEPTH;
            }
            // the tunnel is never lit by the Key, but it must still block it: a Key travelling +Z (beat 6, beat 7) enters the
            // Face's mouth hole and would otherwise shine through the web into the shaft (the open lining casts two-sided)
            BeatKit.Emit(mb, t, "TunnelLining", TunnelVariant(mats, MatId.ConcreteBoard), _meshes, ShadowCastingMode.TwoSided);

            // ---- floor slab (worn centre path), 5 cm into the walls; the shaft end stops SHAFT_RECESS behind Beat02's near flat
            float zSlab = zM - SHAFT_RECESS;
            MeshKit.ChamferBox(mb, new Vector3(x, y - FLOOR_T * 0.5f, (zSlab + zF) * 0.5f), new Vector3(AbyssConst.TUNNEL_W + 0.1f, FLOOR_T, zSlab - zF), 0.01f);
            BeatKit.FloorUVAlongZ(mb, 0, x);
            _tunnelFloorB = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, t, "TunnelFloor", TunnelVariant(mats, MatId.ConcreteFloor), _meshes, ShadowCastingMode.On);

            // ---- six ceiling fixtures: steel housing 5 mm under the ceiling and an emissive diffuser, dimmer toward the mouth
            Material steel = TunnelVariant(mats, MatId.SteelPainted);
            for (int i = 0; i < STRIPS; i++)
            {
                float zs = STRIP_Z0 - STRIP_SPACING * i;
                MeshKit.ChamferBox(mb, new Vector3(x, y + h - 0.005f - 0.04f, zs), new Vector3(0.36f, 0.08f, 2.1f), 0.01f);
            }
            BeatKit.Emit(mb, t, "FixtureHousings", steel, _meshes, ShadowCastingMode.Off);
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(0.28f, 0.02f, 1.96f), 0.004f);
            Mesh diffuser = BeatKit.Bake(mb, "FixtureDiffuser", _meshes);
            for (int i = 0; i < STRIPS; i++)
                BeatKit.Renderer(t, "Strip" + i, diffuser, StripMaterial(mats, i), new Vector3(x, y + h - 0.085f, STRIP_Z0 - STRIP_SPACING * i),
                    Quaternion.identity, ShadowCastingMode.Off);

            // ---- props (near detail): cable tray on the left (+X) wall, drain cover plates along the right (-X) wall base
            var props = new List<Renderer>(4);
            float wallE = x + hw - 0.01f, tz0 = zF + 0.6f, tz1 = zM - 0.6f, tzc = (tz0 + tz1) * 0.5f, tl = tz1 - tz0;
            float trayY = y + TRAY_Y;
            MeshKit.ChamferBox(mb, new Vector3(wallE - TRAY_W * 0.5f, trayY, tzc), new Vector3(TRAY_W, TRAY_T, tl), 0.003f);
            MeshKit.ChamferBox(mb, new Vector3(wallE - TRAY_W + TRAY_T * 0.5f, trayY + TRAY_LIP * 0.5f, tzc), new Vector3(TRAY_T, TRAY_LIP, tl), 0.003f);
            for (float bz = tz0 + 0.9f; bz < tz1; bz += BRACKET_SPACING)
            {
                MeshKit.ChamferBox(mb, new Vector3(wallE - TRAY_W * 0.5f, trayY - TRAY_T * 0.5f - 0.02f, bz), new Vector3(TRAY_W, 0.04f, 0.05f), 0.006f);
                MeshKit.ChamferBox(mb, new Vector3(x + hw - 0.003f, trayY - 0.1f, bz), new Vector3(0.03f, 0.22f, 0.09f), 0.004f);   // 12 mm into the lining
            }
            for (int i = 0; i < 20; i++)
            {
                float pz = zF + 1.5f + BRACKET_SPACING * i;
                MeshKit.ChamferBox(mb, new Vector3(x - hw + 0.01f + DRAIN_W * 0.5f, y + DRAIN_T * 0.5f, pz), new Vector3(DRAIN_W, DRAIN_T, DRAIN_PLATE), 0.004f);
            }
            props.Add(BeatKit.Emit(mb, t, "TrayAndDrain", steel, _meshes, ShadowCastingMode.Off));
            for (int i = 0; i < TRAY_CABLES; i++)
            {
                float cx = wallE - 0.06f - 0.075f * i, cy = trayY + TRAY_T * 0.5f + CABLE_R + 0.001f;
                MeshKit.Cylinder(mb, new Vector3(cx, cy, tz0 + 0.1f), new Vector3(cx, cy, tz1 - 0.1f), CABLE_R, 8, true, true);
            }
            props.Add(BeatKit.Emit(mb, t, "TrayCables", TunnelVariant(mats, MatId.Cable), _meshes, ShadowCastingMode.Off));
            var tb = new Bounds(new Vector3(x, y + h * 0.5f, (zM + zF) * 0.5f), new Vector3(AbyssConst.TUNNEL_W, h, zM - zF));
            _tunnelProps = new DetailTier("Beat07.TunnelProps", tb, PROPS_NEAR, 20f, 20f);
            for (int i = 0; i < props.Count; i++) _tunnelProps.AddNear(props[i]);

            // ---- steel threshold across the mouth (covers the Tunnel/Exterior floor seam at z = 0)
            MeshKit.ChamferBox(mb, new Vector3(x, y + 0.006f, zF - 0.05f), new Vector3(2f * (hw - PORTAL_OVERLAP) - 0.02f, 0.012f, 0.5f), 0.004f);
            BeatKit.Emit(mb, t, "MouthThreshold", mats.Get(MatId.SteelPainted, FogDomain.Exterior), _meshes, ShadowCastingMode.On);
        }

        // ================================================================================================ the Face

        void BuildFaceFull(BeatContext ctx, MeshBuilder mb)
        {
            Material mat = ctx.Mats.Variant(MatId.ConcreteBoard, FogDomain.Exterior, "b7face", m => m.SetFloat(MacroAmpId, FACE_MACRO_AMP));
            Transform face = BeatKit.Group(_geo, "Face");
            float zF = AbyssConst.W[16].z, hw = AbyssConst.FACE_WIDTH * 0.5f;
            Vector3 fan = AbyssConst.FAN_CENTRE;
            float collarOut = AbyssConst.FAN_R_OUT + COLLAR_W;

            // ---- the wall plane, 200 m chunks, holes for the bore (square piece below), the mouth and the gallery
            s_holes.Clear();
            s_holes.Add(new BeatKit.Rect2(fan.x - BeatKit.BORE_SQUARE_HALF, fan.y - BeatKit.BORE_SQUARE_HALF, fan.x + BeatKit.BORE_SQUARE_HALF, fan.y + BeatKit.BORE_SQUARE_HALF));
            s_holes.Add(new BeatKit.Rect2(X - AbyssConst.TUNNEL_W * 0.5f, Yf, X + AbyssConst.TUNNEL_W * 0.5f, Yf + AbyssConst.TUNNEL_H));
            s_holes.Add(BeatKit.GALLERY_FACE_HOLE);
            for (float y1 = AbyssConst.SHAFT_TOP_Y; y1 > AbyssConst.TERRACE_Y + 1e-3f; y1 -= CHUNK)
            {
                float y0 = Mathf.Max(AbyssConst.TERRACE_Y, y1 - CHUNK);
                var origin = new Vector3(0f, y0, zF);
                mb.Push(Matrix4x4.Translate(-origin));
                BeatKit.ZPlaneWithHoles(mb, zF, false, new BeatKit.Rect2(-hw, y0, hw, y1), s_holes, WALL_CELL);
                mb.Pop();
                if (mb.VertexCount > 0) AddFaceBounds(Offset(BeatKit.VertexBounds(mb, 0), origin));
                // the Face casts: in beat 6 the key light enters only through the bore (14.2 beat-6 reasoning)
                BeatKit.Emit(mb, face, "Face_" + Mathf.RoundToInt(y0), mat, _meshes, ShadowCastingMode.On, origin, true);

                // horizontal bands at the S0 ring levels in this chunk (ribs 60 m, belts 240 m), gapped around the bore collar
                mb.Push(Matrix4x4.Translate(-origin));
                for (int k = 0; k < AbyssConst.RIB_LEVELS; k++)
                {
                    float yk = AbyssConst.RingY(k);
                    if (yk < y0 || yk >= y1) continue;
                    bool belt = AbyssConst.RingIsBelt(k);
                    float bh = belt ? BELT_BAND_H : RIB_BAND_H, proud = belt ? BELT_BAND_PROUD : RIB_BAND_PROUD;
                    if (yk - bh < AbyssConst.TERRACE_Y || yk + bh > AbyssConst.SHAFT_TOP_Y) continue;
                    float zc = zF + (BAND_EMBED - proud) * 0.5f, zs = proud + BAND_EMBED;
                    float gapHalf = BandGapHalf(yk, bh, fan, AbyssConst.FAN_R_OUT - COLLAR_INSET, collarOut);
                    if (gapHalf <= 0f)
                    {
                        MeshKit.ChamferBox(mb, new Vector3(0f, yk, zc), new Vector3(2f * hw, bh, zs), EDGE_BEVEL);
                    }
                    else
                    {
                        float xa = fan.x - gapHalf, xb = fan.x + gapHalf;
                        if (xa > -hw + 1f) MeshKit.ChamferBox(mb, new Vector3((-hw + xa) * 0.5f, yk, zc), new Vector3(xa + hw, bh, zs), EDGE_BEVEL);
                        if (xb < hw - 1f) MeshKit.ChamferBox(mb, new Vector3((xb + hw) * 0.5f, yk, zc), new Vector3(hw - xb, bh, zs), EDGE_BEVEL);
                    }
                }
                mb.Pop();
                BeatKit.Emit(mb, face, "FaceBands_" + Mathf.RoundToInt(y0), mat, _meshes, ShadowCastingMode.On, origin, true);
            }

            // ---- bore piece: the square around the bore minus the collar's footprint (10 cm under the collar)
            var boreOrigin = new Vector3(fan.x, fan.y, zF);
            mb.Push(Matrix4x4.Translate(-boreOrigin));
            BeatKit.ZSquareMinusCircle(mb, zF, false, new Vector2(fan.x, fan.y), BeatKit.BORE_SQUARE_HALF, collarOut - 0.1f, BeatKit.BORE_SEGMENTS);
            mb.Pop();
            BeatKit.Emit(mb, face, "FaceBorePiece", mat, _meshes, ShadowCastingMode.On, boreOrigin, true);

            // ---- bore collar: a ring R 150 -> 153 standing 1 m proud of the Face, edges bevelled 0.05, 10 cm inside the lining
            // radius (never coplanar with Beat06's 128-gon lining, which runs 5 cm out of the Face) and 5 cm into the web (closed
            // profile: no open edge, no leak). Lathe axis (local +Y) = world -Z.
            float rIn = AbyssConst.FAN_R_OUT - COLLAR_INSET, b = EDGE_BEVEL;
            s_profile.Clear();
            s_profile.Add(new Vector2(rIn, -COLLAR_EMBED));
            s_profile.Add(new Vector2(rIn, COLLAR_DEPTH - b));
            s_profile.Add(new Vector2(rIn + b, COLLAR_DEPTH));
            s_profile.Add(new Vector2(collarOut - COLLAR_OUTER_BEVEL, COLLAR_DEPTH));
            s_profile.Add(new Vector2(collarOut, COLLAR_DEPTH - COLLAR_OUTER_BEVEL));
            s_profile.Add(new Vector2(collarOut, -COLLAR_EMBED));
            s_profile.Add(new Vector2(rIn, -COLLAR_EMBED));
            mb.Push(Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)));
            MeshKit.Lathe(mb, s_profile, COLLAR_SEGMENTS);
            mb.Pop();
            BeatKit.Emit(mb, face, "BoreCollar", mat, _meshes, ShadowCastingMode.On, boreOrigin, true);

            // ---- mouth portal: jambs and lintel 0.4 m proud, overlapping the lining by 5 cm (no light leak), bevel 0.05
            float x = X, y = Yf, inner = AbyssConst.TUNNEL_W * 0.5f - PORTAL_OVERLAP, outer = inner + PORTAL_JAMB;
            float pz0 = zF + PORTAL_EMBED, pz1 = zF - PORTAL_DEPTH, pzc = (pz0 + pz1) * 0.5f, pd = pz0 - pz1;
            float yBot = BaseY - 0.25f, lintelBot = y + AbyssConst.TUNNEL_H - PORTAL_OVERLAP, lintelTop = lintelBot + PORTAL_LINTEL;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(x + s * (inner + outer) * 0.5f, (yBot + lintelBot) * 0.5f, pzc), new Vector3(PORTAL_JAMB, lintelBot - yBot, pd), EDGE_BEVEL);
            MeshKit.ChamferBox(mb, new Vector3(x, (lintelBot + lintelTop) * 0.5f, pzc), new Vector3(2f * outer, PORTAL_LINTEL, pd), EDGE_BEVEL);

            // ---- rod anchor plates 14 m above the deck with four bolts each
            float ay = y + ROD_ANCHOR_UP, az = zF + ANCHOR_EMBED - ANCHOR_T * 0.5f, afront = zF + ANCHOR_EMBED - ANCHOR_T;
            for (int s = -1; s <= 1; s += 2)
            {
                float axp = x + s * ROD_ANCHOR_DX;
                MeshKit.ChamferBox(mb, new Vector3(axp, ay, az), new Vector3(ANCHOR_PLATE, ANCHOR_PLATE, ANCHOR_T), 0.015f);
                for (int bi = 0; bi < 4; bi++)
                {
                    float bx = (bi & 1) == 0 ? -0.32f : 0.32f, by = (bi & 2) == 0 ? -0.32f : 0.32f;
                    MeshKit.HexBolt(mb, new Vector3(axp + bx, ay + by, afront), Vector3.back, 0.07f, 15f * bi);
                }
            }
            BeatKit.Emit(mb, face, "MouthPortalAndAnchors", mat, _meshes, ShadowCastingMode.On);
        }

        /// <summary>Half width of the gap a horizontal band (centre yk, height bh) leaves around the bore (0 = no gap).</summary>
        static float BoreGapHalf(float yk, float bh, Vector3 fan, float radius)
        {
            float dy = Mathf.Max(0f, Mathf.Abs(yk - fan.y) - 0.5f * bh);
            if (dy >= radius) return 0f;
            return Mathf.Sqrt(radius * radius - dy * dy);
        }

        /// <summary>
        /// Half width of the gap of a band (centre yk, height bh) that crosses the bore collar (ring rIn..rOut around the fan
        /// axis): the band's end face lies inside the ring at every height of the band, its outer corners BAND_COLLAR_BURY
        /// inside the collar's outer face and its inner corners still outside the bore (rIn). 0 = the band passes the collar.
        /// Where a band crosses the collar's top or bottom (no room in the ring) it keeps the old free end BAND_BORE_MARGIN off.
        /// </summary>
        public static float BandGapHalf(float yk, float bh, Vector3 fan, float rIn, float rOut)
        {
            float dc = Mathf.Abs(yk - fan.y), dyNear = Mathf.Max(0f, dc - 0.5f * bh), dyFar = dc + 0.5f * bh;
            if (dyNear >= rOut) return 0f;
            float rb = rOut - BAND_COLLAR_BURY;
            if (dyFar < rb)
            {
                float xe = Mathf.Sqrt(rb * rb - dyFar * dyFar);
                if (xe * xe + dyNear * dyNear > (rIn + BAND_COLLAR_BURY) * (rIn + BAND_COLLAR_BURY)) return xe;
            }
            return BoreGapHalf(yk, bh, fan, rOut + BAND_BORE_MARGIN);
        }

        // ================================================================================================ balcony and pier

        void BuildBalconyAndPierFull(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Material conc = mats.Get(MatId.ConcreteBoard, FogDomain.Exterior);
            // matte: the default floor roughness (0.45, 0.33 on the worn path) gave the pier a wet-looking sky sheen in KS7b/KS7c
            Material walk = mats.Variant(MatId.ConcreteFloor, FogDomain.Exterior, "b7walk", m =>
            {
                m.SetFloat(BeatKit.WearWidthId, 0.45f);
                m.SetFloat(ShaderIds.Roughness, WALK_ROUGHNESS);
                Color lin = AbyssPalette.CONC_LIGHT.linear * WALK_ALBEDO_GAIN;
                lin.a = 1f;
                m.SetColor(ShaderIds.BaseColor, lin.gamma);   // colour property: converted to linear by Unity
                m.SetFloat(MacroAmpId, WALK_MACRO_AMP);
            });
            Transform g = BeatKit.Group(_geo, "Balcony");
            float x = X, y = Yf, top = BaseY, zF = AbyssConst.W[16].z, zB = AbyssConst.W[17].z, zD = AbyssConst.W[18].z;
            float bw = AbyssConst.BALCONY_W, bd = zF - zB, bottom = y - AbyssConst.BALCONY_THICK, pw = AbyssConst.PIER_W * 0.5f;

            // ---- balcony slab (2.45 m under a 5 cm topping = 2.5 m) and its corbel: a wedge under the slab, 8 m deep at the Face
            MeshKit.ChamferBox(mb, new Vector3(x, (top + bottom) * 0.5f, (zF + zB) * 0.5f), new Vector3(bw, top - bottom, bd), SLAB_BEVEL);
            _balconyB = BeatKit.VertexBounds(mb, 0);
            // corbel: section x = world -z, y = world y (Extrude along +X); top 5 cm inside the slab, back 2 cm inside the web
            s_sec.Clear(true);
            s_sec.Add(new Vector2(zF - 0.02f, bottom + 0.05f), true);
            s_sec.Add(new Vector2(-(zB + CORBEL_FRONT_INSET), bottom + 0.05f), true);
            s_sec.Add(new Vector2(zF - 0.02f, bottom - CORBEL_DROP), true);
            float cx = bw * 0.5f - CORBEL_SIDE_INSET;
            MeshKit.Extrude(mb, s_sec, new Vector3(x - cx, 0f, zF), new Vector3(x + cx, 0f, zF), Vector3.up, true);

            // ---- pier: tapered corbel slab, 4 m deep at the root (balcony front) to 0.6 m at the tip; its root runs 4 m back
            // into the balcony body so no end face shows under the balcony; chamfered long edges
            int pierFirst = mb.VertexCount;
            s_pierDepth[0] = PIER_ROOT_DEPTH; s_pierDepth[1] = PIER_ROOT_DEPTH; s_pierDepth[2] = PIER_TIP_DEPTH;
            Quaternion rot = Quaternion.LookRotation(Vector3.back, Vector3.up);
            s_frames.Clear();
            s_frames.Add(new LoftFrame(new Vector3(x, 0f, zB + PIER_ROOT_DEPTH), rot));
            s_frames.Add(new LoftFrame(new Vector3(x, 0f, zB), rot, Vector2.one, true));
            s_frames.Add(new LoftFrame(new Vector3(x, 0f, zD), rot));
            // the pier top sits 5 mm under the balcony top so the 4 m root inside the balcony never shares a face with it
            MeshKit.LoftVarying(mb, k => PierSection(pw, top - PIER_TOP_DROP, s_pierDepth[k]), s_frames, true);
            _pierB = BeatKit.VertexBounds(mb, pierFirst);
            BeatKit.Emit(mb, g, "BalconyAndPier", conc, _meshes, ShadowCastingMode.On);

            // ---- kerbs: balcony 0.15 (sides and the front with the pier opening), pier 0.10; 2 cm inside the slab edges
            float sunk = top - SUNK, kTop = y + KERB_H, kc = (sunk + kTop) * 0.5f, kh = kTop - sunk;
            float kerbFrontIn = zB + EDGE_INSET + KERB_W;               // -13.78: inner face of the front kerb
            float sideX = bw * 0.5f - EDGE_INSET - KERB_W * 0.5f;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(x + s * sideX, kc, (kerbFrontIn + zF) * 0.5f), new Vector3(KERB_W, kh, zF - kerbFrontIn), 0.015f);
            float fl0 = pw, fl1 = bw * 0.5f - EDGE_INSET;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(x + s * (fl0 + fl1) * 0.5f, kc, zB + EDGE_INSET + KERB_W * 0.5f), new Vector3(fl1 - fl0, kh, KERB_W), 0.015f);
            float pkTop = y + PIER_KERB_H, pkc = (sunk + pkTop) * 0.5f, pkx = pw - EDGE_INSET - PIER_KERB_W * 0.5f;
            float pz0 = zD + FRAME_DEPTH, pz1 = kerbFrontIn;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(x + s * pkx, pkc, (pz0 + pz1) * 0.5f), new Vector3(PIER_KERB_W, pkTop - sunk, pz1 - pz0), 0.012f);
            BeatKit.Emit(mb, g, "Kerbs", conc, _meshes, ShadowCastingMode.On);

            // ---- walking toppings (worn path along x = -45): balcony inside the kerbs, pier between its kerbs; chamfer joint at -13.78
            float tbx = bw * 0.5f - EDGE_INSET - KERB_W;
            MeshKit.ChamferBox(mb, new Vector3(x, (sunk + y) * 0.5f, (kerbFrontIn + zF) * 0.5f), new Vector3(2f * tbx, y - sunk, zF - kerbFrontIn), 0.01f);
            float tpx = pw - EDGE_INSET - PIER_KERB_W, tz1 = zD + EDGE_INSET;
            MeshKit.ChamferBox(mb, new Vector3(x, (sunk + y) * 0.5f, (kerbFrontIn + tz1) * 0.5f), new Vector3(2f * tpx, y - sunk, kerbFrontIn - tz1), 0.01f);
            BeatKit.FloorUVAlongZ(mb, 0, x);
            _walkTopY = BeatKit.VertexBounds(mb, 0).max.y;
            BeatKit.Emit(mb, g, "WalkTopping", walk, _meshes, ShadowCastingMode.On);
        }

        /// <summary>Pier cross-section (counter-clockwise, chamfered corners) of the given depth below the structural top.</summary>
        static CrossSection PierSection(float halfW, float top, float depth)
        {
            float c = SLAB_BEVEL, b = top - depth;
            s_sec.Clear(true);
            s_sec.Add(new Vector2(-halfW + c, b), true);
            s_sec.Add(new Vector2(halfW - c, b), true);
            s_sec.Add(new Vector2(halfW, b + c), true);
            s_sec.Add(new Vector2(halfW, top - c), true);
            s_sec.Add(new Vector2(halfW - c, top), true);
            s_sec.Add(new Vector2(-halfW + c, top), true);
            s_sec.Add(new Vector2(-halfW, top - c), true);
            s_sec.Add(new Vector2(-halfW, b + c), true);
            return s_sec;
        }

        // ================================================================================================ door assembly

        void BuildDoorFull(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Material conc = mats.Get(MatId.ConcreteBoard, FogDomain.Exterior);
            Material steel = mats.Get(MatId.SteelPainted, FogDomain.Exterior);
            Material polished = mats.Get(MatId.RodPolished, FogDomain.Exterior);
            Transform g = BeatKit.Group(_geo, "Door");
            float x = X, y = Yf, zD = AbyssConst.W[18].z, top = BaseY;
            float dw = AbyssConst.GOAL_DOOR_W * 0.5f, dh = AbyssConst.GOAL_DOOR_H;
            float fw = FRAME_W * 0.5f, open = dw + DOOR_CLEAR;
            float zFront = zD + FRAME_DEPTH, zMid = zD + FRAME_DEPTH * 0.5f;
            float frameTop = top + FRAME_H, openTop = y + LEAF_LIFT + dh + 0.004f;

            // ---- frame 3.0 x 2.6 x 0.6 standing on the pier tip: two jambs and a lintel between them (chamfer joints, 4 cm arrises)
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(x + s * (open + fw) * 0.5f, (top - SUNK + frameTop) * 0.5f, zMid), new Vector3(fw - open, FRAME_H + SUNK, FRAME_DEPTH), FRAME_BEVEL);
            MeshKit.ChamferBox(mb, new Vector3(x, (openTop + frameTop) * 0.5f, zMid), new Vector3(2f * open, frameTop - openTop, FRAME_DEPTH), FRAME_BEVEL);
            _frameB = BeatKit.VertexBounds(mb, 0);
            _frameBaseY = top;
            // closed back panel behind the recess, 5 cm wider and taller than the frame, down over the pier's tip face
            float bpBot = top - PIER_TIP_DEPTH - 0.05f, bpTop = frameTop + 0.05f;
            MeshKit.ChamferBox(mb, new Vector3(x, (bpBot + bpTop) * 0.5f, zD - BACK_PANEL_T * 0.5f), new Vector3(FRAME_W + 0.1f, bpTop - bpBot, BACK_PANEL_T), FRAME_BEVEL);
            BeatKit.Emit(mb, g, "DoorFrame", conc, _meshes, ShadowCastingMode.On);

            // ---- steel sill in the recess, lamp housing on the lintel face
            MeshKit.ChamferBox(mb, new Vector3(x, y + SILL_T * 0.5f, (zD + 0.05f + zFront - 0.02f) * 0.5f), new Vector3(2f * dw, SILL_T, zFront - 0.02f - zD - 0.05f), 0.002f);
            float lampY = y + DOOR_LAMP_Y;
            MeshKit.ChamferBox(mb, new Vector3(x, lampY, zFront + 0.1f), new Vector3(0.38f, 0.14f, 0.22f), 0.012f);
            BeatKit.Emit(mb, g, "DoorSteel", steel, _meshes, ShadowCastingMode.On);
            // the lens has its own variant: its emission follows the door light (dark until the ignition sweep has run).
            // R2-21 / D4: a flat elliptical GradientCard lens 3 mm under the housing: AMBER x 8 (the 14.2 peak, white-hot after
            // the tonemapper) only in the inner 30 % of the radius, falling to a saturated amber limb (x LENS_LIMB) at the rim.
            AddLens(mb, new Vector3(x, lampY - 0.07f - 0.003f, zFront + 0.1f), 0.15f, 0.08f);
            _lensMat = LensMaterial(mats);
            _lensBase = AbyssPalette.AMBER_EMISSIVE;
            BeatKit.Emit(mb, g, "DoorLampLens", _lensMat, _meshes, ShadowCastingMode.Off);

            // ---- leaf 1.0 x 2.0 set back in the recess, hinged on its -X edge; swings toward the pier (+Z) when it opens.
            // R2-33: a heavy steel leaf (M_DoorLeaf in STEEL_DARK with worn edges): a flat-bar border and a mid rail 8 mm proud,
            // the kick plate and a vertical pull bar on two stand-offs (no lever handle).
            float zLeaf = LeafZ(_tier);
            _leafPivot = BeatKit.Group(g, "LeafHinge", new Vector3(x - dw, y + LEAF_LIFT, zLeaf), Quaternion.identity);
            MeshKit.ChamferBox(mb, new Vector3(dw, dh * 0.5f, 0f), new Vector3(2f * dw, dh, LEAF_T), 0.006f);
            _leafB = BeatKit.VertexBounds(mb, 0);
            Material leafMat = mats.Variant(MatId.DoorLeaf, FogDomain.Exterior, "b7leafsteel", m =>
            {
                Color c = AbyssPalette.STEEL_DARK;
                m.SetColor(ShaderIds.BaseColor, c);
                m.SetFloat(WearAmountId, 1.0f);
                m.SetFloat(ShaderIds.Roughness, 0.55f);
            });
            float face = LEAF_T * 0.5f;
            const float BAR = 0.05f, BAR_T = 0.008f;
            MeshKit.ChamferBox(mb, new Vector3(BAR * 0.5f + 0.004f, dh * 0.5f, face + BAR_T * 0.5f - 0.002f), new Vector3(BAR, dh - 0.008f, BAR_T), 0.002f);
            MeshKit.ChamferBox(mb, new Vector3(2f * dw - BAR * 0.5f - 0.004f, dh * 0.5f, face + BAR_T * 0.5f - 0.002f), new Vector3(BAR, dh - 0.008f, BAR_T), 0.002f);
            for (int i = 0; i < 3; i++)
            {
                float ry = i == 0 ? 0.30f : (i == 1 ? 1.05f : dh - BAR * 0.5f - 0.004f);
                MeshKit.ChamferBox(mb, new Vector3(dw, ry, face + BAR_T * 0.5f - 0.003f), new Vector3(2f * dw - 2f * BAR - 0.012f, BAR, BAR_T - 0.002f), 0.002f);
            }
            BeatKit.Emit(mb, _leafPivot, "Leaf", leafMat, _meshes, ShadowCastingMode.On);
            MeshKit.ChamferBox(mb, new Vector3(dw, 0.14f, face), new Vector3(2f * dw - 2f * BAR - 0.02f, 0.24f, 0.01f), 0.002f);     // kick plate, 5 mm proud
            BeatKit.Emit(mb, _leafPivot, "LeafKickPlate", steel, _meshes, ShadowCastingMode.On);
            float hx = 2f * dw - 0.13f, hy0 = 0.78f, hy1 = 1.38f, hz = face + 0.065f;
            MeshKit.Cylinder(mb, new Vector3(hx, hy0 - 0.03f, hz), new Vector3(hx, hy1 + 0.03f, hz), 0.016f, 12, true, true);   // pull bar
            for (int s = 0; s < 2; s++)
            {
                float sy = s == 0 ? hy0 : hy1;
                MeshKit.Cylinder(mb, new Vector3(hx, sy, face - 0.002f), new Vector3(hx, sy, hz), 0.012f, 10);                 // stand-off
                MeshKit.Cylinder(mb, new Vector3(hx, sy, face - 0.002f), new Vector3(hx, sy, face + 0.01f), 0.028f, 14);       // rose
            }
            for (int i = 0; i < 3; i++)
            {
                float hgy = 0.25f + 0.75f * i;
                MeshKit.Cylinder(mb, new Vector3(-0.004f, hgy - 0.06f, face + 0.012f), new Vector3(-0.004f, hgy + 0.06f, face + 0.012f), 0.016f, 10);
            }
            BeatKit.Emit(mb, _leafPivot, "LeafHardware", polished, _meshes, ShadowCastingMode.On);

            // ---- amber backing 0.15 m behind the leaf (AMBER x 3.0), 5 mm in front of the back panel
            BeatKit.ZCard(mb, x - dw, x + dw, y + LEAF_LIFT, y + LEAF_LIFT + dh, zLeaf - LEAF_T * 0.5f - BACKING_GAP, true);
            _backingMat = BackingMaterial(mats);
            BeatKit.Emit(mb, g, "AmberBacking", _backingMat, _meshes, ShadowCastingMode.Off);

            // ---- two 0.15 m tension rods (painted STEEL_DARK, ThinLine) from lug plates on the frame top to lug plates on the
            // Face anchors, each end a clevis fork with a pin, a turnbuckle a third of the way up (R1-21: no box clamps)
            // R2-33: a smoother paint (0.36) so the rods' upper sides pick up the sky reflection instead of reading as black lines
            Material rod = mats.Variant(MatId.SteelPainted, FogDomain.Exterior, "b7rod", m =>
            {
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
                m.SetFloat(ShaderIds.Roughness, 0.36f);
            });
            float afront = AbyssConst.W[16].z + ANCHOR_EMBED - ANCHOR_T;   // front face of the anchor plates
            for (int s = -1; s <= 1; s += 2)
            {
                var pa = new Vector3(x + s * ROD_FRAME_DX, frameTop + ROD_PIN_ABOVE_FRAME, zMid);
                var pe = new Vector3(x + s * ROD_ANCHOR_DX, y + ROD_ANCHOR_UP, afront - ROD_PIN_OFF_ANCHOR);
                Vector3 dir = (pe - pa).normalized;
                // lug plates in the rod's vertical plane: on the frame top (3 cm into it) and out of the anchor plate (2 cm in)
                float lugTop = pa.y + 0.1f, lugBot = frameTop - SUNK;
                MeshKit.ChamferBox(mb, new Vector3(pa.x, 0.5f * (lugTop + lugBot), zMid), new Vector3(LUG_T, lugTop - lugBot, LUG_W), 0.008f);
                float le0 = afront + 0.02f, le1 = pe.z - 0.1f;
                MeshKit.ChamferBox(mb, new Vector3(pe.x, pe.y, 0.5f * (le0 + le1)), new Vector3(LUG_T, LUG_W, le0 - le1), 0.008f);
                Clevis(mb, pa, dir);
                Clevis(mb, pe, -dir);
                MeshKit.Cylinder(mb, pa + dir * 0.38f, pe - dir * 0.38f, ROD_R, 16, true, true);
                Vector3 tb = Vector3.Lerp(pa, pe, 0.35f);
                MeshKit.TaperedCylinder(mb, tb - dir * 0.45f, tb, 0.085f, 0.11f, 16);
                MeshKit.TaperedCylinder(mb, tb, tb + dir * 0.45f, 0.11f, 0.085f, 16);
            }
            BeatKit.Emit(mb, g, "TensionRods", rod, _meshes, ShadowCastingMode.On);
        }

        /// <summary>
        /// Clevis at a rod end: two fork plates straddling the lug plate (4 mm clear of it), a pin across them and a sleeve the
        /// rod enters. pin = pin centre, o = unit direction from the pin into the rod.
        /// </summary>
        static void Clevis(MeshBuilder mb, Vector3 pin, Vector3 o)
        {
            Vector3 side = Vector3.Cross(Vector3.up, o);
            side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(o, side).normalized;
            float off = 0.5f * LUG_T + 0.004f + 0.5f * CLEVIS_PLATE_T;
            for (int t = -1; t <= 1; t += 2)
                MeshKit.ChamferMember(mb, pin - o * 0.07f + side * (t * off), pin + o * 0.22f + side * (t * off), CLEVIS_PLATE_T, CLEVIS_PLATE_H, 0.006f, up);
            MeshKit.Cylinder(mb, pin + o * 0.2f, pin + o * 0.4f, CLEVIS_SLEEVE_R, 16);
            MeshKit.Cylinder(mb, pin - side * 0.07f, pin + side * 0.07f, CLEVIS_PIN_R, 10);
        }

        static readonly int WearAmountId = Shader.PropertyToID("_WearAmount");
        static readonly int CardFogId = Shader.PropertyToID("_CardFog");
        /// <summary>Door lens limb (rim) emission as a fraction of the AMBER x 8 core (AMBER x 1.6 at the rim: saturation 0.5, D4).</summary>
        public const float LENS_LIMB = 0.2f, LENS_CORE = 0.3f;
        const int LENS_SEGMENTS = 24;

        /// <summary>
        /// Elliptical lens facing down (half axes rx along x, rz along z) as a GradientCard fan: UV0.y = 1 inside LENS_CORE of
        /// the radius (the white-hot core), falling linearly to 0 at the rim (the amber limb).
        /// </summary>
        static void AddLens(MeshBuilder mb, Vector3 c, float rx, float rz)
        {
            int centre = mb.Vertex(c, Vector3.down, new Vector2(0.5f, 1f));
            int first = mb.VertexCount;
            for (int k = 0; k < LENS_SEGMENTS; k++)
            {
                float a = k * 2f * Mathf.PI / LENS_SEGMENTS, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                mb.Vertex(c + new Vector3(ca * rx * LENS_CORE, 0f, sa * rz * LENS_CORE), Vector3.down, new Vector2(0.5f, 1f));
                mb.Vertex(c + new Vector3(ca * rx, 0f, sa * rz), Vector3.down, new Vector2(0.5f, 0f));
            }
            for (int k = 0; k < LENS_SEGMENTS; k++)
            {
                int i0 = first + 2 * k, i1 = first + 2 * ((k + 1) % LENS_SEGMENTS);
                BeatKit.Tri(mb, centre, i0, i1);
                BeatKit.Tri(mb, i0, i0 + 1, i1 + 1);
                BeatKit.Tri(mb, i0, i1 + 1, i1);
            }
        }

        /// <summary>The door lens: GradientCard (core AMBER, limb AMBER x LENS_LIMB) x _EmissionStrength (AMBER_EMISSIVE x door light).</summary>
        static Material LensMaterial(MaterialLibrary mats)
        {
            return mats.Variant(MatId.GradientCard, FogDomain.Exterior, "b7doorlensgrad", m =>
            {
                m.SetFloat(BeatKit.CardModeId, 0f);
                m.SetFloat(CardFogId, 1f);
                Color lin = AbyssPalette.AMBER.linear;
                Color limb = new Color(lin.r * LENS_LIMB, lin.g * LENS_LIMB, lin.b * LENS_LIMB, 1f);
                m.SetColor(ShaderIds.CardTop, AbyssPalette.AMBER);   // colour properties are converted to linear by Unity
                m.SetColor(ShaderIds.CardBottom, limb.gamma);
                m.SetFloat(ShaderIds.EmissionStrength, AbyssPalette.AMBER_EMISSIVE);
            });
        }
    }
}
