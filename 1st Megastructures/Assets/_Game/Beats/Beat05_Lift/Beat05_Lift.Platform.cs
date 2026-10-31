using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>Beat 5 full tier: materials, the dock apron and the lift platform (14.5.5, 14.7.1).</summary>
    public sealed partial class Beat05_Lift
    {
        // ---------------------------------------------------------------- platform / dock detail constants
        const float DECK_BODY_TOP = -0.02f;     // steel body top; tread plates sit on it (embedded 1 cm), their tops at 0
        /// <summary>
        /// Tread plates butt against each other (R2-14): the joint is the V of the two 4 mm bevels, which the Surface shader's
        /// sub-pixel bevel resolve (_BevelWidth = TREAD_BEVEL) bends flat once it projects under 1-2.5 px. The r1 12 mm gap showed
        /// the steel body 2 cm below at every joint, a hard sub-pixel slot that MSAA broke into dashes at grazing angles (B5_start).
        /// The outermost plates keep TREAD_KERB_CLEAR from the kerb's inner face (never co-planar with it).
        /// </summary>
        const float TREAD_T = 0.03f, TREAD_GAP = 0f, TREAD_BEVEL = 0.004f, TREAD_KERB_CLEAR = 0.003f;
        const int TREADS = 9;
        /// <summary>
        /// Per-plate paint tone (linear albedo x, by hash of the plate index; R2-14 "per-plate tone"): +/-12 % so the plate grid
        /// still reads 1-4 levels apart in the dim deck (about 25 levels) and under the spot pools (about 80) once the joints
        /// have merged; the mean stays 1.
        /// </summary>
        static readonly float[] TREAD_TONES = { 0.88f, 0.96f, 1.04f, 1.12f };
        const float KERB_W = 0.35f, KERB_TOP = 0.012f, KERB_BOTTOM = -0.3f;
        const float RAIL_TOP_TUBE = 0.05f, RAIL_MID_TUBE = 0.04f, RAIL_POST = 0.06f, RAIL_POST_TOP = 1.095f;
        const float TOE_H = 0.12f, TOE_T = 0.012f;
        const float GIRDER_DEPTH = 0.8f;
        /// <summary>
        /// Painted keep-clear line around the deck, 0.6 m inside the kerb: 0.1 m wide, 5 mm proud of the treads (never
        /// co-planar). A human-scale cue on the 20 x 20 m deck. R2-26 / R2-14: in CONC_LIGHT (11 x the tread albedo) it lit up
        /// under the pylon spot pools as a near-white glowing trim (KS5a 0.90, B5_start, B5_middle) and as KS5c's pale bar
        /// (0.284 on a 0.10 deck); it is now worn grey paint, <see cref="STRIPE_ALBEDO"/> x the tread albedo, matte, so it stays a
        /// line a little lighter than the deck. lift_r31 at x 1.6: 0.55 on the 0.41 pool beside the KS5a pylon base (f03 0.90);
        /// x 1.35 keeps it about +0.08 above the pool.
        /// </summary>
        const float STRIPE_W = 0.1f, STRIPE_T = 0.006f, STRIPE_PROUD = 0.005f, STRIPE_FROM_KERB = 0.6f;
        const float STRIPE_ALBEDO = 1.35f;

        Material _steelDeck, _steelTread, _paintLine, _steelThin, _rodThin, _glowCold, _glowAmber, _glowFreight, _glowMarker;
        Material[] _treadTones;

        void MakeMaterials(MaterialLibrary mats)
        {
            // painted steel near the camera: rust kept subtle (lead: <= 12 %, desaturated)
            _steelDeck = mats.Variant(MatId.SteelPainted, "b05deck", m =>
            {
                m.SetFloat("_RustAmount", 0.025f);
                m.enableInstancing = true;
            });
            // tread plates: weathered paint (grime +/-11 % on a 3 m tile, roughness 0.62), so the 20 x 20 m deck that fills the
            // lower third of KS5a and two thirds of KS5c reads as a worn working surface instead of one smooth dark plane
            _steelTread = mats.Variant(MatId.SteelWeathered, "b05tread", m =>
            {
                m.SetFloat("_RustAmount", 0.015f);
                m.SetFloat("_BevelWidth", TREAD_BEVEL);   // SHADERS.md 6: the 4 mm tread bevels resolve by their own width
                m.enableInstancing = true;
            });
            _treadTones = new Material[TREAD_TONES.Length];
            for (int t = 0; t < TREAD_TONES.Length; t++)
            {
                float k = TREAD_TONES[t];
                _treadTones[t] = mats.Variant(MatId.SteelWeathered, "b05tread" + t, m =>
                {
                    m.SetColor(ShaderIds.BaseColor, ScaleLinear(m.GetColor(ShaderIds.BaseColor), k));
                    m.SetFloat("_RustAmount", 0.015f);
                    m.SetFloat("_BevelWidth", TREAD_BEVEL);
                    m.enableInstancing = true;
                });
            }
            _paintLine = mats.Variant(MatId.SteelPainted, "b05line2", m =>
            {
                m.SetColor(ShaderIds.BaseColor, ScaleLinear(AbyssPalette.STEEL_DARK, STRIPE_ALBEDO));
                m.SetFloat("_RustAmount", 0f);
                m.SetFloat(ShaderIds.Roughness, 0.85f);
                m.SetFloat(ShaderIds.Metallic, 0f);
            });
            // guide-rail marker lamps (R2-11): small cold points, no light source
            _glowMarker = mats.Variant(MatId.Glow, "b05marker", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, MARKER_STRENGTH);
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.enableInstancing = true;
            });
            _steelThin = mats.Variant(MatId.SteelPainted, "b05thin", m =>
            {
                m.SetFloat("_RustAmount", 0.025f);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
                m.enableInstancing = true;
            });
            _rodThin = mats.Variant(MatId.RodPolished, "b05thin", m =>
            {
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
                m.enableInstancing = true;
            });
            _glowCold = mats.Variant(MatId.Glow, "b05cold", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, 2.5f);
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.enableInstancing = true;
            });
            _glowAmber = mats.Variant(MatId.Glow, "b05amber", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.AMBER.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, AMBER_SMALL_EMISSIVE_MAX);   // D4: far amber sprites at most 2.5
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.enableInstancing = true;
            });
            _glowFreight = mats.Variant(MatId.Glow, "b05freight", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.WIN_COLD.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, 3f);
                m.SetFloat(ShaderIds.MinPixels, 4f);   // at least 4 px at 170 m (14.5.5)
                m.enableInstancing = true;
            });
        }

        // ================================================================================================ dock

        /// <summary>
        /// The 10 m apron at the hall exit: a concrete slab (x -142..-151.3, worn path along x), a steel nosing angle to x -151.6
        /// (the deck's 0.4 m bridging plate closes the rest to -152), rails on both sides ending at -151.3, three knee braces to the
        /// hull below the exit opening and a call pedestal. Every platform part slides +X while it descends past the apron level:
        /// the slab stops 13 cm, the nosing 6 cm and the rails 20 cm clear of the swept platform (EditMode test).
        /// </summary>
        void BuildDockFull(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform dock = BeatKit.Group(_geo, "Dock");
            Vector3 top = AbyssConst.LIFT_TOP;
            float y = top.y, zc = top.z, half = PathGuide.DOCK_APRON_W * 0.5f;
            float xa = AbyssConst.HALL_X1;                              // -142: hall exit
            float xEdge = Beat05Layout.DockNoseEdgeX;                   // -151.6: nosing edge
            float xSlab = Beat05Layout.DockSlabEdgeX;                   // -151.3: concrete edge

            MeshKit.ChamferBox(mb, new Vector3((xa + xSlab) * 0.5f, y - DOCK_T * 0.5f, zc), new Vector3(xa - xSlab, DOCK_T, 2f * half), 0.02f);
            BeatKit.FloorUVAlongX(mb, 0, zc);
            _dock = BeatKit.Emit(mb, dock, "DockApron", mats[MatId.ConcreteFloor], _meshes, ShadowCastingMode.On);

            // nosing: top plate 1 cm proud of the concrete, vertical leg down the slab edge
            float noseX1 = xSlab + 0.02f;
            MeshKit.ChamferBox(mb, new Vector3((xEdge + noseX1) * 0.5f, y - 0.02f, zc), new Vector3(noseX1 - xEdge, 0.06f, 2f * half - 0.02f), 0.01f);
            MeshKit.ChamferBox(mb, new Vector3(xSlab - 0.01f, y - 0.33f, zc), new Vector3(0.02f, 0.56f, 2f * half - 0.04f), 0.004f);
            // knee braces from the hull wall below the exit opening to the slab underside
            for (int i = -1; i <= 1; i++)
            {
                float z = zc + i * 7f;
                MeshKit.IBeam(mb, new Vector3(xa + 0.3f, y - 20f, z), new Vector3(xa - 7.6f, y - DOCK_T + 0.15f, z), 0.5f, 0.35f, 0.03f, 0.04f, Vector3.up);
            }
            // call pedestal by the +Z rail (scale cue; cold status strip on its face)
            MeshKit.ChamferBox(mb, new Vector3(xa - 5.5f, y + 0.6f, zc + half - 1.2f), new Vector3(0.45f, 1.2f, 0.35f), 0.02f);
            BeatKit.Emit(mb, dock, "DockSteel", _steelDeck, _meshes, ShadowCastingMode.On);
            MeshKit.ChamferBox(mb, new Vector3(xa - 5.5f - 0.23f, y + 1.02f, zc + half - 1.2f), new Vector3(0.02f, 0.05f, 0.24f), 0.005f);
            BeatKit.Emit(mb, dock, "DockLamp", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off);

            for (int s = -1; s <= 1; s += 2)
            {
                float z = zc + s * (half - RAIL_INSET);
                RailRun(mb, new Vector3(xa - 0.3f, y, z), new Vector3(xSlab, y, z), true, true);
            }
            BeatKit.Emit(mb, dock, "DockRails", _steelThin, _meshes, ShadowCastingMode.On);
        }

        // ================================================================================================ rails

        /// <summary>
        /// A straight 1.1 m hand rail along a -> b on a floor at a.y: posts (0.06) every &lt;= 2 m whose tops stay 5 mm under
        /// the top tube, top tube 0.05 (its top at exactly 1.1), mid tube 0.04, toe board 0.12. An end post is emitted only
        /// where this run owns it; a run that does not own its end stops its tubes at the neighbour's post face, so two runs
        /// meeting at a corner never overlap co-planar faces. Tubes and posts are ThinLine members.
        /// </summary>
        static void RailRun(MeshBuilder mb, Vector3 a, Vector3 b, bool postA, bool postB)
        {
            Vector3 up = Vector3.up;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            Vector3 dir = d / len;
            float hp = 0.5f * RAIL_POST;
            Vector3 ta = postA ? a : a + dir * hp, tb = postB ? b : b - dir * hp;
            float h = AbyssConst.RAIL_H;
            MeshKit.ChamferMember(mb, ta + up * (h - 0.5f * RAIL_TOP_TUBE), tb + up * (h - 0.5f * RAIL_TOP_TUBE), RAIL_TOP_TUBE, RAIL_TOP_TUBE, 0.008f, up, true);
            MeshKit.ChamferMember(mb, ta + up * 0.55f, tb + up * 0.55f, RAIL_MID_TUBE, RAIL_MID_TUBE, 0.006f, up, true);
            Vector3 side = Vector3.Cross(up, dir);
            MeshKit.ChamferBox(mb, (ta + tb) * 0.5f + up * (-0.01f + 0.5f * TOE_H), new Vector3(TOE_T, TOE_H, (tb - ta).magnitude), 0.003f,
                Quaternion.LookRotation(dir, up));
            int n = Mathf.Max(1, Mathf.RoundToInt(len / RAIL_POST_SPACING));
            for (int i = 0; i <= n; i++)
            {
                if (i == 0 && !postA) continue;
                if (i == n && !postB) continue;
                Vector3 p = Vector3.Lerp(a, b, (float)i / n);
                MeshKit.ChamferMember(mb, p - up * 0.01f, p + up * RAIL_POST_TOP, RAIL_POST, RAIL_POST, 0.008f, side, true);
            }
        }

        /// <summary>
        /// A 4 m sliding rail section (gate / boarding) along local x: end and mid posts, top, knee and bottom tubes, the same
        /// open rail as the fixed runs. No infill: the KS5c pose stands 2 m behind the gate looking 55 degrees down, and a
        /// grating panel filled the whole upper third of that frame instead of the shaft below.
        /// </summary>
        static void SlideFrame(MeshBuilder mb)
        {
            Vector3 up = Vector3.up;
            float hw = OPENING * 0.5f, h = AbyssConst.RAIL_H;
            Vector3 a = new Vector3(-hw, 0f, 0f), b = new Vector3(hw, 0f, 0f);
            MeshKit.ChamferMember(mb, a + up * (h - 0.5f * RAIL_TOP_TUBE), b + up * (h - 0.5f * RAIL_TOP_TUBE), RAIL_TOP_TUBE, RAIL_TOP_TUBE, 0.008f, up, true);
            MeshKit.ChamferMember(mb, a + up * 0.55f, b + up * 0.55f, RAIL_MID_TUBE, RAIL_MID_TUBE, 0.006f, up, true);
            MeshKit.ChamferMember(mb, a + up * 0.15f, b + up * 0.15f, RAIL_MID_TUBE, RAIL_MID_TUBE, 0.006f, up, true);
            for (int i = 0; i <= 2; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i * 0.5f);
                MeshKit.ChamferMember(mb, p + up * 0.02f, p + up * RAIL_POST_TOP, RAIL_POST, RAIL_POST, 0.008f, Vector3.forward, true);
            }
        }

        // ================================================================================================ platform

        void BuildPlatformFull(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            _platform = BeatKit.Group(_geo, "Platform", AbyssConst.LIFT_TOP, Quaternion.identity);
            float half = DeckHalf, ri = half - RAIL_INSET, open = OPENING * 0.5f;
            float S = AbyssConst.LIFT_SIZE, T = AbyssConst.LIFT_THICK;

            // ---- deck body (steel box): 20 x 20, y -1.2 .. -0.02
            MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * (-T + DECK_BODY_TOP), 0f), new Vector3(S, T + DECK_BODY_TOP, S), 0.03f);
            _deck = BeatKit.Emit(mb, _platform, "Deck", _steelDeck, _meshes, ShadowCastingMode.On);

            // ---- kerb (perimeter steel, 1.2 cm proud of the treads); +/-Z pieces butt between the +/-X pieces
            // outer faces 5 mm proud of the body faces (never co-planar with them)
            float kw = KERB_W + 0.005f, kc = half + 0.005f - 0.5f * kw, ky = 0.5f * (KERB_TOP + KERB_BOTTOM), kh = KERB_TOP - KERB_BOTTOM;
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit.ChamferBox(mb, new Vector3(s * kc, ky, 0f), new Vector3(kw, kh, S + 0.01f), 0.01f);
                MeshKit.ChamferBox(mb, new Vector3(0f, ky, s * kc), new Vector3(S - 2f * KERB_W, kh, kw), 0.01f);
            }
            // corner marker lenses go on the kerb ends (LampCold, below)
            // ---- underside girders (3 + 3, inset from the edges; top flanges buried 2 cm in the body)
            float gy = -T + 0.02f;
            for (int i = -1; i <= 1; i++)
            {
                MeshKit.IBeam(mb, new Vector3(-8.8f, gy - 0.5f * GIRDER_DEPTH, i * 6f), new Vector3(8.8f, gy - 0.5f * GIRDER_DEPTH, i * 6f),
                    GIRDER_DEPTH, 0.45f, 0.04f, 0.05f, Vector3.up);
                MeshKit.IBeam(mb, new Vector3(i * 6f, gy - 0.35f, -8.8f), new Vector3(i * 6f, gy - 0.35f, 8.8f), 0.7f, 0.4f, 0.035f, 0.045f, Vector3.up);
            }
            BeatKit.Emit(mb, _platform, "DeckFrame", _steelDeck, _meshes, ShadowCastingMode.On);

            // ---- tread plates 9 x 9 between the kerbs, tops at 0, butted (TREAD_GAP 0), one renderer per paint tone (R2-14)
            float span = S - 2f * KERB_W - 2f * TREAD_KERB_CLEAR, pitch = span / TREADS, size = pitch - TREAD_GAP;
            for (int t = 0; t < TREAD_TONES.Length; t++)
            {
                for (int i = 0; i < TREADS; i++)
                    for (int j = 0; j < TREADS; j++)
                    {
                        if (TreadTone(i, j) != t) continue;
                        var c = new Vector3(-0.5f * span + pitch * (i + 0.5f), -0.5f * TREAD_T, -0.5f * span + pitch * (j + 0.5f));
                        MeshKit.ChamferBox(mb, c, new Vector3(size, TREAD_T, size), TREAD_BEVEL);
                    }
                MeshRenderer r = BeatKit.Emit(mb, _platform, t == 0 ? "DeckTreads" : "DeckTreads_" + t, _treadTones[t], _meshes, ShadowCastingMode.On);
                if (t == 0) _deckTop = r;
            }

            // ---- keep-clear line: the +/-Z runs full length, the +/-X runs stop at their inner edges (no overlapping tops)
            float sc = half - KERB_W - STRIPE_FROM_KERB - 0.5f * STRIPE_W, sy = STRIPE_PROUD - 0.5f * STRIPE_T;
            float sLen = 2f * sc + STRIPE_W, sIn = 2f * (sc - 0.5f * STRIPE_W);
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit.ChamferBox(mb, new Vector3(0f, sy, s * sc), new Vector3(sLen, STRIPE_T, STRIPE_W), 0.002f);
                MeshKit.ChamferBox(mb, new Vector3(s * sc, sy, 0f), new Vector3(STRIPE_W, STRIPE_T, sIn), 0.002f);
            }
            BeatKit.Emit(mb, _platform, "DeckLine", _paintLine, _meshes, ShadowCastingMode.Off);

            // ---- fixed rails: -X and +Z full, -Z beside the 4 m gate, +X beside the 4 m boarding opening (owned corner posts)
            RailRun(mb, new Vector3(-ri, 0f, -ri), new Vector3(-ri, 0f, ri), true, true);
            RailRun(mb, new Vector3(-ri, 0f, ri), new Vector3(ri, 0f, ri), false, true);
            RailRun(mb, new Vector3(ri, 0f, ri), new Vector3(ri, 0f, open), false, true);
            RailRun(mb, new Vector3(ri, 0f, -open), new Vector3(ri, 0f, -ri), true, true);
            RailRun(mb, new Vector3(ri, 0f, -ri), new Vector3(open, 0f, -ri), false, true);
            RailRun(mb, new Vector3(-open, 0f, -ri), new Vector3(-ri, 0f, -ri), true, false);
            _rails = BeatKit.Emit(mb, _platform, "DeckRails", _steelThin, _meshes, ShadowCastingMode.On);

            // ---- sliding sections: the gate (-Z) slides toward -X when opening, the boarding section (+X) slides from +Z to close
            SlideFrame(mb);
            Mesh frame = BeatKit.Bake(mb, "SlideFrame", _meshes);
            _gate = BeatKit.Group(_platform, "Gate", new Vector3(0f, 0f, -ri + SLIDE_INSET), Quaternion.identity);
            _gateR = BeatKit.Renderer(_gate, "GateFrame", frame, _steelThin, Vector3.zero, Quaternion.identity, ShadowCastingMode.On);
            Quaternion alongZ = Quaternion.Euler(0f, 90f, 0f);
            _boarding = BeatKit.Group(_platform, "Boarding", new Vector3(ri - SLIDE_INSET, 0f, OPENING), Quaternion.identity);
            BeatKit.Renderer(_boarding, "BoardingFrame", frame, _steelThin, Vector3.zero, alongZ, ShadowCastingMode.On);

            // ---- bridging plates (top 8 mm below the deck top: hidden inside the deck when retracted, under the dock / gantry when out)
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(PLATE_DEPTH, PLATE_T, OPENING), 0.005f);
            Mesh plateX = BeatKit.Bake(mb, "PlateX", _meshes);
            _plateXR = BeatKit.Renderer(_platform, "PlateX", plateX, _steelDeck, new Vector3(PLATE_HOME + PLATE_TRAVEL, PLATE_TOP - PLATE_T * 0.5f, 0f),
                Quaternion.identity, ShadowCastingMode.Off);
            _plateX = _plateXR.transform;
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(OPENING, PLATE_T, PLATE_DEPTH), 0.005f);
            Mesh plateZ = BeatKit.Bake(mb, "PlateZ", _meshes);
            _plateZ = BeatKit.Renderer(_platform, "PlateZ", plateZ, _steelDeck, new Vector3(0f, PLATE_TOP - PLATE_T * 0.5f, -PLATE_HOME),
                Quaternion.identity, ShadowCastingMode.Off).transform;

            BuildPylons(ctx, mb);
            BuildGuideShoes(mb);
        }

        /// <summary>Paint-tone index (into TREAD_TONES) of tread plate (i, j): a fixed hash, so every build and tier agrees.</summary>
        public static int TreadTone(int i, int j)
        {
            uint h = Beat05Layout.Pcg((uint)(i * 31 + j) * 2654435761u + 0x7EADu);
            return (int)((h >> 8) % (uint)TREAD_TONES.Length);
        }

        void BuildPylons(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            float hp = 0.5f * PYLON_SIZE, inb = PYLON_OFFSET - LAMP_INBOARD;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                var p = new Vector3(sx * PYLON_OFFSET, 0f, sz * PYLON_OFFSET);
                MeshKit.ChamferBox(mb, p + new Vector3(0f, 0.5f * PYLON_H, 0f), new Vector3(PYLON_SIZE, PYLON_H, PYLON_SIZE), 0.03f);
                float plate = 2f * Beat05Layout.PYLON_FOOTPRINT_HALF;   // 1.3 m base plate = the walk keep-out footprint
                MeshKit.ChamferBox(mb, p + new Vector3(0f, 0.02f, 0f), new Vector3(plate, 0.06f, plate), 0.01f);
                for (int c = 1; c <= 2; c++)
                    MeshKit.ChamferBox(mb, p + new Vector3(0f, 4f * c, 0f), new Vector3(PYLON_SIZE + 0.1f, 0.08f, PYLON_SIZE + 0.1f), 0.01f);
                // base stiffeners on the four faces
                for (int k = 0; k < 4; k++)
                {
                    Vector3 n = k == 0 ? Vector3.right : k == 1 ? Vector3.left : k == 2 ? Vector3.forward : Vector3.back;
                    Vector3 sizeK = k < 2 ? new Vector3(0.24f, 0.42f, 0.03f) : new Vector3(0.03f, 0.42f, 0.24f);
                    MeshKit.ChamferBox(mb, p + n * (hp + 0.11f) + new Vector3(0f, 0.25f, 0f), sizeK, 0.005f);
                }
                for (int bx = -1; bx <= 1; bx += 2)
                    for (int bz = -1; bz <= 1; bz += 2)
                        MeshKit.HexBolt(mb, p + new Vector3(bx * 0.52f, 0.05f, bz * 0.52f), Vector3.up, 0.07f, 15f * (i + bx + 2 * bz));
                // cable lug on the outboard corner
                MeshKit.ChamferBox(mb, new Vector3(sx * (PYLON_OFFSET + hp + 0.02f), 11.0f, sz * (PYLON_OFFSET + hp + 0.02f)), new Vector3(0.12f, 0.3f, 0.12f), 0.01f);
            }
            _pylons = BeatKit.Emit(mb, _platform, "Pylons", _steelDeck, _meshes, ShadowCastingMode.On);
            // lamp heads, inboard on the column tops (they pass 6 cm under the dock nosing while the platform leaves)
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                MeshKit.ChamferBox(mb, new Vector3(sx * inb, LAMP_Y, sz * inb), new Vector3(0.5f, 0.3f, 0.5f), 0.02f);
            }
            BeatKit.Emit(mb, _platform, "PylonHeads", _steelDeck, _meshes, ShadowCastingMode.On);

            // lenses on the two outboard faces of each lamp head, corner markers on the kerb
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                var head = new Vector3(sx * inb, LAMP_Y, sz * inb);
                MeshKit.ChamferBox(mb, head + new Vector3(sx * 0.26f, -0.02f, 0f), new Vector3(0.025f, 0.18f, 0.36f), 0.005f);
                MeshKit.ChamferBox(mb, head + new Vector3(0f, -0.02f, sz * 0.26f), new Vector3(0.36f, 0.18f, 0.025f), 0.005f);
                // corner marker on the kerb, inboard of the corner rail post
                MeshKit.ChamferBox(mb, new Vector3(sx * 9.73f, KERB_TOP + 0.01f, sz * 9.73f), new Vector3(0.12f, 0.025f, 0.12f), 0.004f);
            }
            BeatKit.Emit(mb, _platform, "PylonLamps", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off);

            // cables (O 0.12) from the pylon lugs to anchors just inside the deck corners
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                var a = new Vector3(sx * (PYLON_OFFSET + hp + 0.02f), 10.9f, sz * (PYLON_OFFSET + hp + 0.02f));
                var b = new Vector3(sx * 9.45f, 0.08f, sz * 9.45f);
                MeshKit.Cylinder(mb, a, b, CABLE_R, 10, true, true);
            }
            _cables = BeatKit.Emit(mb, _platform, "Cables", mats[MatId.Cable], _meshes, ShadowCastingMode.Off);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                MeshKit.ChamferBox(mb, new Vector3(sx * 9.45f, 0.05f, sz * 9.45f), new Vector3(0.3f, 0.12f, 0.3f), 0.01f);
            }
            BeatKit.Emit(mb, _platform, "CableAnchors", _steelDeck, _meshes, ShadowCastingMode.On);

            // lamp glows (cold), riding with the platform
            Mesh glow = GlowQuad();
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                var pos = new Vector3(sx * (inb + 0.27f), LAMP_Y - 0.02f, sz * (inb + 0.27f));
                MeshRenderer r = BeatKit.Renderer(_platform, "LampGlow" + i, glow, _glowCold, pos, Quaternion.identity, ShadowCastingMode.Off);
                r.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                r.receiveShadows = false;
            }
        }

        /// <summary>
        /// Two guide shoes on the deck's +Z edge clamping the polished running rails of the first guide rail (z = 283): an arm
        /// from the deck to a back plate and two jaws straddling the running rail with 3 cm clearance on every side, all aligned
        /// with the rail (13.7 degrees from vertical).
        /// </summary>
        void BuildGuideShoes(MeshBuilder mb)
        {
            Quaternion rot = Beat05Layout.RailRotation;
            Vector3 d = Beat05Layout.LiftDir, p = Beat05Layout.LiftPerp;
            float zRail = AbyssConst.GUIDE_RAIL_Z0 - AbyssConst.LIFT_TOP.z;   // 14: rail axis in platform-local z
            const float yShoe = -0.6f;
            Vector3 RailToLocal(float xr, float yrr, float zrr) => rot * new Vector3(xr, yrr, zrr) + new Vector3(0f, 0f, zRail);
            for (int s = -1; s <= 1; s += 2)
            {
                float yr = s * RAIL_CHORD_Y;
                float zr = (yShoe - p.y * yr) / d.y;                         // along the rail so the shoe centre is at deck mid-height
                for (int j = -1; j <= 1; j += 2)
                    MeshKit.ChamferBox(mb, RailToLocal(SHOE_JAW_X0 + 0.5f * SHOE_JAW_DEPTH, yr + j * (RUN_HALF_W + SHOE_CLEAR + 0.105f), zr),
                        new Vector3(SHOE_JAW_DEPTH, 0.21f, 1.6f), 0.01f, rot);
                MeshKit.ChamferBox(mb, RailToLocal(SHOE_JAW_X0 + SHOE_JAW_DEPTH + 0.09f, yr, zr), new Vector3(0.22f, 0.62f, 1.7f), 0.015f, rot);
                Vector3 c = RailToLocal(0f, yr, zr);
                float zBack = AbyssConst.GUIDE_RAIL_Z0 - (SHOE_JAW_X0 + SHOE_JAW_DEPTH + 0.2f) - AbyssConst.LIFT_TOP.z + 0.05f;
                float z0 = DeckHalf - 0.15f;
                MeshKit.ChamferBox(mb, new Vector3(c.x, yShoe, 0.5f * (z0 + zBack)), new Vector3(0.5f, 0.7f, zBack - z0), 0.02f);
            }
            BeatKit.Emit(mb, _platform, "GuideShoes", _steelDeck, _meshes, ShadowCastingMode.On);
        }

        /// <summary>Platform-to-hull clearance over the whole ride from every platform vertex in the riding configuration (m).</summary>
        float HullClearance()
        {
            var pts = new System.Collections.Generic.List<Vector3>(40000);
            CollectRidingPlatformPoints(pts);
            if (pts.Count == 0) return 0f;
            Vector2[] hull = Beat05Layout.HullPolygon();
            float best = float.PositiveInfinity;
            for (int k = 0; k < pts.Count; k++)
            {
                float dist = Beat05Layout.HullDistance(pts[k], hull);
                if (dist < best) best = dist;
            }
            return best;
        }

        /// <summary>
        /// Every vertex of the platform's meshes in platform-local coordinates, in the riding configuration (gate shut, boarding
        /// section closed, bridging plates retracted). QA / EditMode tests: hull and dock clearance over all s (build time only).
        /// </summary>
        public void CollectRidingPlatformPoints(System.Collections.Generic.List<Vector3> points)
        {
            points.Clear();
            if (_platform == null) return;
            MeshFilter[] filters = _platform.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh m = filters[i].sharedMesh;
                if (m == null || !m.isReadable || m == _glowQuad) continue;
                Matrix4x4 toPlatform = RideLocalMatrix(filters[i].transform);
                Vector3[] v = m.vertices;
                for (int k = 0; k < v.Length; k++) points.Add(toPlatform.MultiplyPoint3x4(v[k]));
            }
        }

        /// <summary>Mesh-to-platform matrix of a platform child in the riding configuration (gate shut, boarding section closed, plates in).</summary>
        Matrix4x4 RideLocalMatrix(Transform t)
        {
            float ri = DeckHalf - RAIL_INSET;
            Matrix4x4 m = Matrix4x4.identity;
            for (Transform x = t; x != null && x != _platform; x = x.parent)
            {
                Vector3 lp = x.localPosition;
                if (x == _gate) lp = new Vector3(0f, 0f, -ri + SLIDE_INSET);
                else if (x == _boarding) lp = new Vector3(ri - SLIDE_INSET, 0f, 0f);
                else if (x == _plateX) lp = new Vector3(PLATE_HOME, lp.y, 0f);
                else if (x == _plateZ) lp = new Vector3(0f, lp.y, -PLATE_HOME);
                m = Matrix4x4.TRS(lp, x.localRotation, x.localScale) * m;
            }
            return m;
        }

        Mesh _glowQuad;

        /// <summary>The Glow contract quad (vertices +/-0.5, docs/SHADERS.md 10), readable for validation, bounds inflated like ShaderMeshes.GlowQuad.</summary>
        Mesh GlowQuad()
        {
            if (_glowQuad != null) return _glowQuad;
            var mb = new MeshBuilder();
            BeatKit.Quad(mb, new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), Vector3.back);
            _glowQuad = BeatKit.Bake(mb, "B05GlowQuad", _meshes);
            _glowQuad.bounds = new Bounds(Vector3.zero, new Vector3(16f, 16f, 16f));
            return _glowQuad;
        }
    }
}
