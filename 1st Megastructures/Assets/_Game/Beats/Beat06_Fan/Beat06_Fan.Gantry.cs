using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 6 full tier, part 2: the 3 x 199 gantry (14.5.6). Local frame: X across from the centreline x = -45, Y up from the
    /// deck top y = -487, Z = world z minus the current origin (<see cref="_gz"/>), so every mesh holds small chunk-local
    /// numbers (the gantry is one 199 m chunk; near-detail sections are 50 m chunks).
    ///
    /// Cross-section: two kerb stringers (|X| 1.35-1.5, Y -0.33..+0.10, the top 0.10 a toe plate) carry the walkway; grating
    /// inserts (Y 0) span between solid 2.5 cm landing plates at the two ends and at every portal; transverse floor beams every
    /// 1.2 m and support bars every 0.6 m under the grating (near tier). Two planar Warren trusses (top chords at X +/-1.30,
    /// 4 m deep, 33 bays, 0.3 m I-sections, gussets) hang under the stringers, tied by bottom struts at every diagonal node and
    /// a zig-zag of bottom lateral braces. Portal frames 6 x 8 m every 25 m from z = 247 stand on outriggers (cantilevered
    /// cross beams with knee braces down to the bottom chords), with corner haunches, a lamp housing and two cable lugs; each
    /// lug carries a stay cable to its anchor at (x = -45 +/- 6, y = -455) on the housing lip, so the 16 stays fan out of the
    /// fan's lower rim (the structural story: a cable-stayed cantilever from the wall). Rails 1.1 m (posts every 2 m), two
    /// O 0.4 service pipes on hangers outside the deck that disappear into the wall through sleeves.
    /// The deck ends flush with the tunnel floor at z = 60; trusses and pipes are buried 0.3-0.4 m in the near flat.
    /// </summary>
    public sealed partial class Beat06_Fan
    {
        const float G_KERB_IN = 1.35f, G_KERB_BOTTOM = -0.33f, G_KERB_TOP = 0.10f;
        const float G_PLATE_T = 0.025f, G_PLATE_HALF = 0.8f, G_END_PLATE = 1.4f;
        const float G_TRUSS_X = 1.30f, G_TRUSS_Y = -0.46f, G_TRUSS_Z_WALL = 59.7f, G_TRUSS_Z_GATE = 258.9f;
        const float G_FLOOR_BEAM_PITCH = 1.2f, G_SUPPORT_BAR_T = 0.012f;
        const float G_RAIL_X = 1.44f, G_RAIL_R = 0.025f, G_MIDRAIL_R = 0.02f, G_MIDRAIL_Y = 0.55f, G_RAIL_POST_PITCH = 2f;
        const float G_RAIL_Z_WALL = 60.25f, G_RAIL_Z_GATE = 258.75f;
        // 14.7.9: rails fade into the deck tone (STEEL_DARK, the kerbs, plates and grating) beyond 60 m. Near the player they
        // carry a pale safety-grey paint so the 1.1 m scale cue reads against the dark deck; the 1.5 px strokes converging on
        // the mouth (the beat-6 aliasing region) are deck-toned, so they never crawl as bright lines.
        const float G_RAIL_FADE0 = 30f, G_RAIL_FADE1 = 60f, G_RAIL_ROUGHNESS = 0.45f;
        static readonly Color G_RAIL_SRGB = new Color(0.44f, 0.46f, 0.48f, 1f);   // desaturated pale grey, never white
        const float G_OUTRIGGER_HALF = 3.45f, G_OUTRIGGER_TOP = -0.32f, G_OUTRIGGER_H = 0.45f, G_OUTRIGGER_D = 0.45f;
        const float G_TOPBEAM_H = 0.5f, G_TOPBEAM_D = 0.5f, G_TOPBEAM_HALF = 3.25f;
        const float G_LAMP_Y0 = 7.32f, G_LUG_TOP = 8.26f;
        const float G_PIPE_X = 2.2f, G_PIPE_Y = -1.1f, G_PIPE_R = 0.2f, G_PIPE_Z_WALL = 59.6f, G_PIPE_Z_GATE = 258.4f;
        const float G_PIPE_FLANGE_Z0 = 69f, G_PIPE_FLANGE_PITCH = 12f;
        const float G_CABLE_R = 0.05f;
        // ThinLine (14.7.9) widens a member per VERTEX from that vertex's camera distance. The rails, pipes and stays are
        // single members up to 199 m long; with rings only at their ends every point of a rail was widened for the ~100 m
        // distance of its ends, so beside the viewer the 5 cm rails rendered as 16 cm tubes at about 30 % coverage
        // (alpha-to-coverage): the translucent "glowing smears" of R1-11 (B6_freelook_back). One ring every 4 m keeps the
        // interpolated distance within 2 m of the true one where it matters (the widening starts beyond 27 m at 1080p).
        const float G_THIN_RING = 4f;
        const float G_SECTION = 50f;
        const float G_AMBER_X = 2.75f, G_AMBER_Y = 2.2f;
        const float G_KERB_DOT_PITCH = 12f;
        const float GLOW_LAMP_D = 0.35f, GLOW_AMBER_D = 0.12f, GLOW_KERB_D = 0.05f, GLOW_ANCHOR_D = 0.2f;
        const float GLOW_LAMP_STRENGTH = 1.5f, GLOW_KERB_STRENGTH = 0.8f;
        // Lead decision D4 (R2-21): small and distant amber emitters use an emissive of at most 2.5, so Neutral tonemapping
        // keeps them amber (AMBER x 8 displays cream-white). The 0.18 m beacon boxes on the odd portals and their 0.12 m glow
        // sprites are such emitters at every distance the walk sees them from (2.75 m beside the walkway and beyond); the
        // amber point lights (I 5, range 9) are unchanged.
        public const float AMBER_SMALL_EMISSIVE = 2.5f;

        float _gz;   // world z of the current local origin
        float _trussDepth, _trussBays;
        int _cableCount;

        Vector3 L(float x, float y, float z) => new Vector3(x, y, z - _gz);

        static float PortalZ(int i) => PORTAL_Z0 - PORTAL_SPACING * i;

        void BuildGantryFull(BeatContext ctx, MeshBuilder mb, bool full, List<DetailTier> tiers)
        {
            MaterialLibrary mats = ctx.Mats;
            float gx = AbyssConst.W[14].x, gy = AbyssConst.GANTRY_Y;
            float zW = AbyssConst.GANTRY_Z1, zG = AbyssConst.GANTRY_Z0, zMid = 0.5f * (zW + zG);
            float hw = AbyssConst.GANTRY_W * 0.5f;
            var origin = new Vector3(gx, gy, zMid);
            Transform g = BeatKit.Group(_geo, "Gantry", origin, Quaternion.identity);
            _gz = zMid;

            Material steel = mats[MatId.SteelPainted];
            Material thin = mats.Variant(MatId.SteelPainted, "b6thin", m => m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX));
            Material pipeMat = mats.Variant(MatId.SteelPainted, "b6pipe", m =>
            {
                m.SetColor(ShaderIds.BaseColor, AbyssPalette.DOOR_LEAF);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
                m.SetFloat(FanIds.Rust, 0.04f);
            });
            Material railMat = mats.Variant(MatId.SteelPainted, "b6rail", m =>
            {
                m.SetColor(ShaderIds.BaseColor, G_RAIL_SRGB);
                m.SetFloat(ShaderIds.Roughness, G_RAIL_ROUGHNESS);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
                m.SetFloat(FanIds.Rust, 0f);
                m.SetColor(FanIds.FarTint, AbyssPalette.STEEL_DARK);
                m.SetVector(FanIds.FarRange, new Vector4(G_RAIL_FADE0, G_RAIL_FADE1, 0f, 0f));
            });

            // ---- solid plate intervals (ends and every portal) and the grating spans between them
            var plates = new List<Vector2>(PORTAL_COUNT + 2) { new Vector2(zW, zW + G_END_PLATE) };
            for (int i = PORTAL_COUNT - 1; i >= 0; i--) plates.Add(new Vector2(PortalZ(i) - G_PLATE_HALF, PortalZ(i) + G_PLATE_HALF));
            plates.Add(new Vector2(zG - G_END_PLATE, zG));

            // ---- 1 deck (persistent): kerb stringers with toe plates, solid landing plates
            float kerbW = hw - G_KERB_IN, kerbH = G_KERB_TOP - G_KERB_BOTTOM;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, L(s * (G_KERB_IN + 0.5f * kerbW), 0.5f * (G_KERB_TOP + G_KERB_BOTTOM), zMid), new Vector3(kerbW, kerbH, zG - zW), 0.02f);
            for (int i = 0; i < plates.Count; i++)
            {
                Vector2 p = plates[i];
                MeshKit.ChamferBox(mb, L(0f, -0.5f * G_PLATE_T, 0.5f * (p.x + p.y)), new Vector3(2f * G_KERB_IN + 0.02f, G_PLATE_T, p.y - p.x), 0.008f);
            }
            _deck = BeatKit.Emit(mb, g, "GantryDeck", steel, _meshes, ShadowCastingMode.On, Vector3.zero, true);

            // ---- 2 grating inserts (Y = 0, between the kerbs and the plates; the shader draws the bars)
            for (int i = 0; i + 1 < plates.Count; i++)
            {
                float za = plates[i].y, zb = plates[i + 1].x;
                if (zb - za < 0.05f) continue;
                int v0 = mb.Vertex(L(-G_KERB_IN, 0f, za), Vector3.up, new Vector2(gx - G_KERB_IN, za));
                int v1 = mb.Vertex(L(-G_KERB_IN, 0f, zb), Vector3.up, new Vector2(gx - G_KERB_IN, zb));
                int v2 = mb.Vertex(L(G_KERB_IN, 0f, zb), Vector3.up, new Vector2(gx + G_KERB_IN, zb));
                int v3 = mb.Vertex(L(G_KERB_IN, 0f, za), Vector3.up, new Vector2(gx + G_KERB_IN, za));
                BeatKit.Tri(mb, v0, v1, v2);
                BeatKit.Tri(mb, v0, v2, v3);
            }
            BeatKit.Emit(mb, g, "GantryGrating", mats[MatId.Grating], _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- 3 trusses (persistent, ThinLine): two planar Warren trusses, bottom struts and lateral braces
            Color32 saved = mb.Color;
            mb.Color = new Color32(205, 0, 0, 255);   // under the deck: slightly occluded
            for (int s = -1; s <= 1; s += 2)
                MeshKit.WarrenTruss(mb, L(s * G_TRUSS_X, G_TRUSS_Y, G_TRUSS_Z_GATE), L(s * G_TRUSS_X, G_TRUSS_Y, G_TRUSS_Z_WALL), TRUSS_DEPTH,
                    TRUSS_BAYS, TRUSS_MEMBER, Vector3.up, TrussMemberShape.IBeam, true, true);
            float bay = (G_TRUSS_Z_GATE - G_TRUSS_Z_WALL) / TRUSS_BAYS, yb = G_TRUSS_Y - TRUSS_DEPTH;
            for (int k = 1; k <= TRUSS_BAYS; k += 2)
            {
                float z = G_TRUSS_Z_GATE - bay * k;
                MeshKit.ChamferMember(mb, L(-G_TRUSS_X, yb, z), L(G_TRUSS_X, yb, z), 0.2f, 0.2f, 0.015f, Vector3.up, true);
            }
            for (int k = 1; k + 2 <= TRUSS_BAYS; k += 2)
            {
                float za = G_TRUSS_Z_GATE - bay * k, zc = za - 2f * bay;
                float sg = ((k >> 1) & 1) == 0 ? 1f : -1f;
                MeshKit.ChamferMember(mb, L(-sg * G_TRUSS_X, yb, za), L(sg * G_TRUSS_X, yb, zc), 0.14f, 0.14f, 0.01f, Vector3.up, true);
            }
            mb.Color = saved;
            MeshRenderer truss = BeatKit.Emit(mb, g, "GantryTruss", thin, _meshes, ShadowCastingMode.On, Vector3.zero, true);
            Bounds tb = truss.GetComponent<MeshFilter>().sharedMesh.bounds;
            _trussDepth = tb.size.y - TRUSS_MEMBER;
            _trussBays = TRUSS_BAYS;

            // ---- 4 portal frames (persistent, ThinLine members): posts, top beam, haunches, outrigger, knee braces, lamp housing, lugs
            float px = PORTAL_W * 0.5f, oy = G_OUTRIGGER_TOP - 0.5f * G_OUTRIGGER_H;
            float postW = float.MaxValue, postE = float.MinValue, topY = float.MinValue;
            for (int i = 0; i < PORTAL_COUNT; i++)
            {
                float z = PortalZ(i);
                int first = mb.VertexCount;
                for (int s = -1; s <= 1; s += 2)
                {
                    int pf = mb.VertexCount;
                    MeshKit.ChamferMember(mb, L(s * px, G_OUTRIGGER_TOP - 0.01f, z), L(s * px, PORTAL_H - 0.1f, z), PORTAL_SECTION, PORTAL_SECTION, 0.02f,
                        Vector3.forward, true);
                    if (i == 0)
                    {
                        Bounds pb = BeatKit.VertexBounds(mb, pf);
                        if (s < 0) postW = pb.center.x; else postE = pb.center.x;
                    }
                    // corner haunch from the post's inner face into the beam's soffit
                    MeshKit.ChamferMember(mb, L(s * (px - 0.02f), PORTAL_H - 1.7f, z), L(s * (px - 1.3f), PORTAL_H - 0.3f, z), 0.22f, 0.22f, 0.015f,
                        Vector3.forward, true);
                    // cable lug on the beam top above the post
                    MeshKit.ChamferBox(mb, L(s * px, G_LUG_TOP - 0.14f, z), new Vector3(0.14f, 0.28f, 0.36f), 0.01f);
                }
                int bf = mb.VertexCount;
                MeshKit.ChamferMember(mb, L(-G_TOPBEAM_HALF, PORTAL_H - 0.5f * G_TOPBEAM_H, z), L(G_TOPBEAM_HALF, PORTAL_H - 0.5f * G_TOPBEAM_H, z),
                    G_TOPBEAM_D, G_TOPBEAM_H, 0.02f, Vector3.up, true);
                if (i == 0) topY = BeatKit.VertexBounds(mb, bf).max.y;   // top beam's top face above the deck top (lugs excluded)
                MeshKit.ChamferBox(mb, L(0f, G_LAMP_Y0 + 0.095f, z), new Vector3(1.3f, 0.19f, 0.36f), 0.02f);
                // key-shot subject: the frame above the deck (posts, haunches, lugs, beam, lamp housing), as the graybox reported it
                Bounds b = BeatKit.VertexBounds(mb, first);
                b.center += origin;
                _portalBounds[i] = b;
                // below the deck: the outrigger and the knee braces down to the bottom chords
                MeshKit.ChamferMember(mb, L(-G_OUTRIGGER_HALF, oy, z), L(G_OUTRIGGER_HALF, oy, z), G_OUTRIGGER_D, G_OUTRIGGER_H, 0.02f, Vector3.up, true);
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferMember(mb, L(s * (px - 0.15f), G_OUTRIGGER_TOP - G_OUTRIGGER_H + 0.07f, z), L(s * (G_TRUSS_X + 0.02f), yb - 0.07f, z),
                        0.22f, 0.22f, 0.015f, Vector3.forward, true);
            }
            BeatKit.Emit(mb, g, "PortalFrames", thin, _meshes, ShadowCastingMode.On, Vector3.zero, true);
            _portalPostSpan = postE - postW;
            _portalHeight = topY;

            // ---- 5 lamps: cold lenses under every top beam, amber beacons every 50 m (odd portals)
            for (int i = 0; i < PORTAL_COUNT; i++)
                MeshKit.ChamferBox(mb, L(0f, G_LAMP_Y0 - 0.0075f, PortalZ(i)), new Vector3(1.1f, 0.025f, 0.24f), 0.006f);
            BeatKit.Emit(mb, g, "PortalLamps", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off, Vector3.zero, true);
            for (int i = 1; i < PORTAL_COUNT; i += 2)
                MeshKit.ChamferBox(mb, L(G_AMBER_X, G_AMBER_Y, PortalZ(i)), new Vector3(0.18f, 0.25f, 0.18f), 0.02f);
            Material beacon = mats.Variant(MatId.LampAmber, "b6beacon", m => m.SetFloat(ShaderIds.EmissionStrength, AMBER_SMALL_EMISSIVE));
            BeatKit.Emit(mb, g, "AmberLamps", beacon, _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- 6 rails 1.1 m (persistent, ThinLine, pale paint fading to the deck tone over 30-60 m, no shadows: 5 cm tubes
            //      would only alias in the shadow map)
            var ring = new List<Vector2>(64);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * G_RAIL_X;
                LongTube(mb, L(x, AbyssConst.RAIL_H - G_RAIL_R, G_RAIL_Z_WALL), L(x, AbyssConst.RAIL_H - G_RAIL_R, G_RAIL_Z_GATE), G_RAIL_R, 8, ring);
                LongTube(mb, L(x, G_MIDRAIL_Y, G_RAIL_Z_WALL), L(x, G_MIDRAIL_Y, G_RAIL_Z_GATE), G_MIDRAIL_R, 8, ring);
                int posts = Mathf.Max(1, Mathf.RoundToInt((G_RAIL_Z_GATE - G_RAIL_Z_WALL) / G_RAIL_POST_PITCH));
                for (int p = 0; p <= posts; p++)
                {
                    float z = Mathf.Lerp(G_RAIL_Z_WALL, G_RAIL_Z_GATE, (float)p / posts);
                    MeshKit.Cylinder(mb, L(x, G_KERB_TOP - 0.01f, z), L(x, AbyssConst.RAIL_H - 0.015f, z), G_RAIL_R, 8, true, true);
                }
            }
            BeatKit.Emit(mb, g, "GantryRails", railMat, _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- 7 stay cables: lug top -> anchor row on the housing lip (one attachment per portal, nearest lowest)
            _cableCount = 0;
            for (int i = 0; i < PORTAL_COUNT; i++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 aw = AnchorAttach(i, s);
                    Vector3 a = L(aw.x - gx, aw.y - gy, aw.z);
                    LongTube(mb, L(s * px, G_LUG_TOP - 0.06f, PortalZ(i)), a, G_CABLE_R, 6, ring);
                    _cableCount++;
                }
            }
            BeatKit.Emit(mb, g, "StayCables", mats[MatId.Cable], _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- 8 service pipes O 0.4 with flanges, wall sleeves and blind flanges at the gate end
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * G_PIPE_X;
                LongTube(mb, L(x, G_PIPE_Y, G_PIPE_Z_WALL), L(x, G_PIPE_Y, G_PIPE_Z_GATE), G_PIPE_R, 16, ring);
                for (float z = G_PIPE_FLANGE_Z0; z < G_PIPE_Z_GATE - 1f; z += G_PIPE_FLANGE_PITCH)
                    MeshKit.Cylinder(mb, L(x, G_PIPE_Y, z - 0.06f), L(x, G_PIPE_Y, z + 0.06f), G_PIPE_R + 0.07f, 16, true, false);
                MeshKit.Cylinder(mb, L(x, G_PIPE_Y, zW - 0.15f), L(x, G_PIPE_Y, zW + 0.35f), G_PIPE_R + 0.1f, 16, true, false);
                MeshKit.Cylinder(mb, L(x, G_PIPE_Y, G_PIPE_Z_GATE - 0.08f), L(x, G_PIPE_Y, G_PIPE_Z_GATE + 0.06f), G_PIPE_R + 0.07f, 16, true, false);
            }
            BeatKit.Emit(mb, g, "ServicePipes", pipeMat, _meshes, ShadowCastingMode.On, Vector3.zero, true);

            // ---- 9 lamp points (persistent glow sprites, >= 2.5 px at any distance)
            BuildGantryGlows(ctx, gx, gy, zW, zG);

            // ---- 10 near detail in 50 m sections (Full only): floor beams, support bars, base plates, splice plates, bolts, pipe hangers
            if (!full) return;
            for (float z0 = zW; z0 < zG - 1e-3f; z0 += G_SECTION)
            {
                float z1 = Mathf.Min(zG, z0 + G_SECTION), zc = 0.5f * (z0 + z1);
                _gz = zc;
                BuildGantryDetail(mb, plates, z0, z1, z1 >= zG - 1e-3f, bay);
                MeshRenderer r = BeatKit.Emit(mb, g, "GantryDetail_" + Mathf.RoundToInt(z0), steel, _meshes, ShadowCastingMode.Off,
                    new Vector3(0f, 0f, zc - zMid), true);
                if (r == null) continue;
                var tier = new DetailTier("Beat06Gantry" + Mathf.RoundToInt(z0), new Bounds(new Vector3(gx, gy - 2f, zc), new Vector3(9f, 14f, z1 - z0 + 2f)));
                tier.AddNear(r);
                tiers.Add(tier);
            }
            _gz = zMid;
        }

        /// <summary>
        /// A long ThinLine tube (rail, stay, pipe) as ONE smooth lathe with a vertex ring at least every <see cref="G_THIN_RING"/>
        /// metres and flat caps at its two ends only (no internal caps that a widened, partly covered tube could show). Writes the
        /// MeshKit ThinLine data: UV1 = (axis direction in mesh space, radius), UV2 = (axis midpoint in mesh space, 1).
        /// </summary>
        static void LongTube(MeshBuilder mb, Vector3 a, Vector3 b, float radius, int sides, List<Vector2> profile)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector3 dir = d / len;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / G_THIN_RING - 1e-4f));
            profile.Clear();
            for (int i = 0; i <= n; i++) profile.Add(new Vector2(radius, len * i / n));

            bool w1 = mb.WriteUV1, w2 = mb.WriteUV2;
            Vector4 c1 = mb.CurrentUV1, c2 = mb.CurrentUV2;
            while (mb.UV1.Count < mb.Positions.Count) mb.UV1.Add(Vector4.zero);
            while (mb.UV2.Count < mb.Positions.Count) mb.UV2.Add(Vector4.zero);
            Vector3 ax = mb.Matrix.MultiplyVector(dir);
            float scale = ax.magnitude;
            ax = scale > 1e-12f ? ax / scale : Vector3.up;
            Vector3 mid = mb.Matrix.MultiplyPoint3x4((a + b) * 0.5f);
            mb.WriteUV1 = true;
            mb.CurrentUV1 = new Vector4(ax.x, ax.y, ax.z, radius * scale);
            mb.WriteUV2 = true;
            mb.CurrentUV2 = new Vector4(mid.x, mid.y, mid.z, 1f);

            mb.Push(a, Quaternion.FromToRotation(Vector3.up, dir));
            MeshKit.Lathe(mb, profile, sides, MeshKit.HardEdgeAngleDeg, 0f, 360f, true);
            mb.Pop();

            mb.WriteUV1 = w1; mb.CurrentUV1 = c1;
            mb.WriteUV2 = w2; mb.CurrentUV2 = c2;
        }

        static bool InPlate(List<Vector2> plates, float z, float margin)
        {
            for (int i = 0; i < plates.Count; i++)
                if (z > plates[i].x - margin && z < plates[i].y + margin) return true;
            return false;
        }

        void BuildGantryDetail(MeshBuilder mb, List<Vector2> plates, float z0, float z1, bool last, float bay)
        {
            float zW = AbyssConst.GANTRY_Z1, zG = AbyssConst.GANTRY_Z0, hw = AbyssConst.GANTRY_W * 0.5f;
            bool InSection(float z) => z >= z0 && (z < z1 || (last && z <= z1));

            // floor beams every 1.2 m (top 5 mm under the walking plane) and 12 mm support bars midway
            for (float z = zW + 0.6f; z < zG - 0.3f; z += G_FLOOR_BEAM_PITCH)
            {
                if (InSection(z))
                    MeshKit.ChamferMember(mb, L(-G_KERB_IN - 0.01f, -0.065f, z), L(G_KERB_IN + 0.01f, -0.065f, z), 0.08f, 0.12f, 0.008f, Vector3.up);
                float zs = z + 0.5f * G_FLOOR_BEAM_PITCH;
                if (InSection(zs) && zs < zG - 0.3f && !InPlate(plates, zs, 0.05f))
                    MeshKit.ChamferBox(mb, L(0f, -0.03f, zs), new Vector3(2f * G_KERB_IN + 0.02f, 0.05f, G_SUPPORT_BAR_T), 0.004f);
            }

            // rail post base plates (5 mm into the kerb top)
            int posts = Mathf.Max(1, Mathf.RoundToInt((G_RAIL_Z_GATE - G_RAIL_Z_WALL) / G_RAIL_POST_PITCH));
            for (int p = 0; p <= posts; p++)
            {
                float z = Mathf.Lerp(G_RAIL_Z_WALL, G_RAIL_Z_GATE, (float)p / posts);
                if (!InSection(z)) continue;
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferBox(mb, L(s * G_RAIL_X, G_KERB_TOP + 0.001f, z), new Vector3(0.12f, 0.012f, 0.12f), 0.003f);
            }

            // kerb splice plates on the outer faces every 12 m, six bolts each
            for (float z = zW + 6f; z < zG - 3f; z += 12f)
            {
                if (!InSection(z)) continue;
                for (int s = -1; s <= 1; s += 2)
                {
                    MeshKit.ChamferBox(mb, L(s * (hw + 0.005f), -0.115f, z), new Vector3(0.02f, 0.34f, 0.5f), 0.004f);
                    for (int bz = -1; bz <= 1; bz++)
                    for (int by = 0; by < 2; by++)
                        MeshKit.HexBolt(mb, L(s * (hw + 0.015f), -0.19f + 0.15f * by, z + 0.17f * bz), new Vector3(s, 0f, 0f), 0.04f);
                }
            }

            // portal post base plates with four bolts each
            for (int i = 0; i < PORTAL_COUNT; i++)
            {
                float z = PortalZ(i);
                if (!InSection(z)) continue;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * PORTAL_W * 0.5f;
                    MeshKit.ChamferBox(mb, L(x, G_OUTRIGGER_TOP + 0.015f, z), new Vector3(0.7f, 0.04f, 0.7f), 0.006f);
                    for (int c = 0; c < 4; c++)
                        MeshKit.HexBolt(mb, L(x + ((c & 1) == 0 ? -0.26f : 0.26f), G_OUTRIGGER_TOP + 0.035f, z + ((c & 2) == 0 ? -0.26f : 0.26f)),
                            Vector3.up, 0.06f, 15f * c);
                }
            }

            // pipe hangers at the top-chord nodes: arm from the chord, drop rod, clamp ring; at portals the rod hangs from the outrigger
            for (int k = 0; k <= TRUSS_BAYS; k += 2)
            {
                float z = G_TRUSS_Z_GATE - bay * k;
                if (!InSection(z) || z < G_PIPE_Z_WALL + 1f || z > G_PIPE_Z_GATE - 1f) continue;
                if (NearPortal(z, 1.2f) || NearFlange(z, 0.6f)) continue;
                for (int s = -1; s <= 1; s += 2)
                {
                    float xp = s * G_PIPE_X;
                    MeshKit.ChamferMember(mb, L(s * (G_TRUSS_X + 0.08f), G_TRUSS_Y, z), L(s * (G_PIPE_X + 0.25f), G_TRUSS_Y, z), 0.1f, 0.12f, 0.008f, Vector3.up);
                    PipeClamp(mb, xp, G_TRUSS_Y - 0.06f, z);
                }
            }
            for (int i = 0; i < PORTAL_COUNT; i++)
            {
                float z = PortalZ(i);
                if (!InSection(z)) continue;
                for (int s = -1; s <= 1; s += 2) PipeClamp(mb, s * G_PIPE_X, G_OUTRIGGER_TOP - G_OUTRIGGER_H + 0.02f, z);
            }
        }

        static bool NearPortal(float z, float d)
        {
            for (int i = 0; i < PORTAL_COUNT; i++) if (Mathf.Abs(z - PortalZ(i)) < d) return true;
            return false;
        }

        static bool NearFlange(float z, float d)
        {
            for (float f = G_PIPE_FLANGE_Z0; f < G_PIPE_Z_GATE - 1f; f += G_PIPE_FLANGE_PITCH) if (Mathf.Abs(z - f) < d) return true;
            return false;
        }

        /// <summary>Drop rod from yTop down into a clamp ring around the pipe at (x, G_PIPE_Y, z).</summary>
        void PipeClamp(MeshBuilder mb, float x, float yTop, float z)
        {
            float ringR = G_PIPE_R + 0.015f;
            MeshKit.Cylinder(mb, L(x, yTop, z), L(x, G_PIPE_Y + ringR, z), 0.018f, 8, true);
            MeshKit.Torus(mb, L(x, G_PIPE_Y, z), Vector3.forward, ringR, 0.02f, 16, 6);
        }

        void BuildGantryGlows(BeatContext ctx, float gx, float gy, float zW, float zG)
        {
            MaterialLibrary mats = ctx.Mats;
            Mesh quad = FanMeshes.Glow();
            Material coldLamp = mats.Variant(MatId.Glow, "b6lamp", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, GLOW_LAMP_STRENGTH);
            });
            Material coldKerb = mats.Variant(MatId.Glow, "b6kerb", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, GLOW_KERB_STRENGTH);
                m.SetFloat(FanIds.EnergyConserve, 0.5f);
            });

            var lamps = new InstancedBatch("Beat06LampPoints", quad, coldLamp);
            for (int i = 0; i < PORTAL_COUNT; i++)
                lamps.Add(Matrix4x4.TRS(new Vector3(gx, gy + G_LAMP_Y0 - 0.03f, PortalZ(i)), Quaternion.identity, Vector3.one * GLOW_LAMP_D));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = AnchorAttach(0, s);
                lamps.Add(Matrix4x4.TRS(new Vector3(a.x, ANCHOR_Y + ANCHOR_H * 0.5f - 0.15f, a.z + 0.12f), Quaternion.identity, Vector3.one * GLOW_ANCHOR_D));
            }
            Seal(lamps, ctx);

            Material amberGlow = mats.Variant(MatId.Glow, "b6amber", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.AMBER.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, AMBER_SMALL_EMISSIVE);
            });
            var amber = new InstancedBatch("Beat06AmberPoints", quad, amberGlow);
            for (int i = 1; i < PORTAL_COUNT; i += 2)
                amber.Add(Matrix4x4.TRS(new Vector3(gx + G_AMBER_X - 0.11f, gy + G_AMBER_Y, PortalZ(i)), Quaternion.identity, Vector3.one * GLOW_AMBER_D));
            Seal(amber, ctx);

            // kerb dots: a dotted perspective line on both kerbs every 12 m, converging on the tunnel mouth
            var kerb = new InstancedBatch("Beat06KerbDots", quad, coldKerb);
            for (float z = zW + 6f; z < zG - 1f; z += G_KERB_DOT_PITCH)
                for (int s = -1; s <= 1; s += 2)
                    kerb.Add(Matrix4x4.TRS(new Vector3(gx + s * (G_KERB_IN - 0.006f), gy + 0.06f, z), Quaternion.identity, Vector3.one * GLOW_KERB_D));
            Seal(kerb, ctx);
        }

        static void Seal(InstancedBatch b, BeatContext ctx)
        {
            b.Seal();
            if (!ctx.EditMode) InstancedRegistry.Register(b);   // the registry is the play-mode draw list (no driver in EditMode)
        }
    }
}
