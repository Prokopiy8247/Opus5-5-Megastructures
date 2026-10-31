using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 7 graybox tier (Fallbacks: "hex-hole plane with fog, balcony and door"): the Phase-3 geometry, kept verbatim so
    /// the fallback looks exactly as it passed the autotest (only the tunnel slab's shaft end is recessed 1 cm behind
    /// Beat02's near flat, against the co-planar end face). The honeycomb is one static M_Lattice plane at y -1087.6 (a
    /// 28 km annulus around the tower with the tower footprint cut out, no distance fade).
    /// </summary>
    public sealed partial class Beat07_Reveal
    {
        const float GRAY_LATTICE_HOLE_APOTHEM = 380f;   // tower footprint cut out of the graybox plane
        const int GRAY_LATTICE_SEGMENTS = 96;
        static readonly float[] GRAY_LATTICE_RINGS = { 600f, 1000f, 1600f, 2500f, 4000f, 6000f, 9000f, 13000f, 19000f, 28000f };

        void BuildGraybox(BeatContext ctx, MeshBuilder mb)
        {
            GrayTunnel(ctx, mb);
            GrayFace(ctx, mb);
            GrayBalconyAndPier(ctx, mb);
            GrayDoor(ctx, mb);
            GrayHoneycomb(ctx, mb);
            BuildDarkCard(ctx, mb);
        }

        void GrayTunnel(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform t = BeatKit.Group(_geo, "Tunnel");
            float x = X, y = Yf, hw = AbyssConst.TUNNEL_W * 0.5f, h = AbyssConst.TUNNEL_H;
            float zM = AbyssConst.W[15].z, zF = AbyssConst.W[16].z;

            // open U section (walls + ceiling) facing inward; walking toward -Z the frame's right is world -X
            var sec = new CrossSection();
            sec.Clear(false);
            sec.Add(new Vector2(-hw, -LINING_SKIRT), true);
            sec.Add(new Vector2(-hw, h), true);
            sec.Add(new Vector2(hw, h), true);
            sec.Add(new Vector2(hw, -LINING_SKIRT), true);
            Quaternion rot = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var frames = new List<LoftFrame>
            {
                new LoftFrame(new Vector3(x, y, zM + LINING_OVERLAP), rot),
                new LoftFrame(new Vector3(x, y, zF - LINING_OVERLAP), rot),
            };
            MeshKit.Loft(mb, sec, frames);
            _liningB = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, t, "TunnelLining", TunnelVariant(mats, MatId.ConcreteBoard), _meshes, ShadowCastingMode.TwoSided);

            // the shaft end stops SHAFT_RECESS behind Beat02's near flat (z = 60, same facing): no co-planar end face
            float zSlab = zM - SHAFT_RECESS;
            MeshKit.ChamferBox(mb, new Vector3(x, y - FLOOR_T * 0.5f, (zSlab + zF) * 0.5f), new Vector3(AbyssConst.TUNNEL_W + 0.1f, FLOOR_T, zSlab - zF), 0.01f);
            BeatKit.FloorUVAlongZ(mb, 0, x);
            _tunnelFloorB = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, t, "TunnelFloor", TunnelVariant(mats, MatId.ConcreteFloor), _meshes, ShadowCastingMode.On);

            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(0.3f, 0.05f, 2f), 0.01f);
            Mesh stripMesh = BeatKit.Bake(mb, "TunnelStrip", _meshes);
            for (int i = 0; i < STRIPS; i++)
                BeatKit.Renderer(t, "Strip" + i, stripMesh, StripMaterial(mats, i), new Vector3(x, y + h - 0.03f, STRIP_Z0 - STRIP_SPACING * i),
                    Quaternion.identity, ShadowCastingMode.Off);
        }

        void GrayFace(BeatContext ctx, MeshBuilder mb)
        {
            Material mat = ctx.Mats.Get(MatId.ConcreteBoard, FogDomain.Exterior);
            Transform face = BeatKit.Group(_geo, "Face");
            float zF = AbyssConst.W[16].z, hw = AbyssConst.FACE_WIDTH * 0.5f;
            Vector3 fan = AbyssConst.FAN_CENTRE;
            var holes = new List<BeatKit.Rect2>
            {
                new BeatKit.Rect2(fan.x - BeatKit.BORE_SQUARE_HALF, fan.y - BeatKit.BORE_SQUARE_HALF, fan.x + BeatKit.BORE_SQUARE_HALF, fan.y + BeatKit.BORE_SQUARE_HALF),
                new BeatKit.Rect2(X - AbyssConst.TUNNEL_W * 0.5f, Yf, X + AbyssConst.TUNNEL_W * 0.5f, Yf + AbyssConst.TUNNEL_H),
                BeatKit.GALLERY_FACE_HOLE,
            };
            for (float y1 = AbyssConst.SHAFT_TOP_Y; y1 > AbyssConst.TERRACE_Y + 1e-3f; y1 -= CHUNK)
            {
                float y0 = Mathf.Max(AbyssConst.TERRACE_Y, y1 - CHUNK);
                var origin = new Vector3(0f, y0, zF);
                mb.Push(Matrix4x4.Translate(-origin));
                BeatKit.ZPlaneWithHoles(mb, zF, false, new BeatKit.Rect2(-hw, y0, hw, y1), holes, WALL_CELL);
                mb.Pop();
                if (mb.VertexCount > 0) AddFaceBounds(Offset(BeatKit.VertexBounds(mb, 0), origin));
                // the Face casts: in beat 6 the key light enters only through the bore (14.2 beat-6 reasoning)
                BeatKit.Emit(mb, face, "Face_" + Mathf.RoundToInt(y0), mat, _meshes, ShadowCastingMode.On, origin, true);
            }
            var boreOrigin = new Vector3(fan.x, fan.y, zF);
            mb.Push(Matrix4x4.Translate(-boreOrigin));
            BeatKit.ZSquareMinusCircle(mb, zF, false, new Vector2(fan.x, fan.y), BeatKit.BORE_SQUARE_HALF, AbyssConst.FAN_R_OUT, BeatKit.BORE_SEGMENTS);
            mb.Pop();
            BeatKit.Emit(mb, face, "FaceBoreRing", mat, _meshes, ShadowCastingMode.On, boreOrigin, true);
        }

        void GrayBalconyAndPier(BeatContext ctx, MeshBuilder mb)
        {
            Material mat = ctx.Mats.Get(MatId.ConcreteBoard, FogDomain.Exterior);
            Transform g = BeatKit.Group(_geo, "Balcony");
            float x = X, y = Yf, zF = AbyssConst.W[16].z, zB = AbyssConst.W[17].z, zD = AbyssConst.W[18].z;
            float bw = AbyssConst.BALCONY_W, bd = zF - zB, pw = AbyssConst.PIER_W * 0.5f;

            MeshKit.ChamferBox(mb, new Vector3(x, y - AbyssConst.BALCONY_THICK * 0.5f, (zF + zB) * 0.5f), new Vector3(bw, AbyssConst.BALCONY_THICK, bd), 0.01f);
            _balconyB = BeatKit.VertexBounds(mb, 0);
            _walkTopY = _balconyB.max.y;
            BeatKit.Emit(mb, g, "BalconySlab", mat, _meshes, ShadowCastingMode.On);

            // kerbs 0.15 on the two sides and the front (open where the pier starts)
            float kh = KERB_H, kw = KERB_W, kz = (zF + zB) * 0.5f;
            MeshKit.ChamferBox(mb, new Vector3(x - bw * 0.5f + kw * 0.5f, y + kh * 0.5f, kz), new Vector3(kw, kh, bd), 0.01f);
            MeshKit.ChamferBox(mb, new Vector3(x + bw * 0.5f - kw * 0.5f, y + kh * 0.5f, kz), new Vector3(kw, kh, bd), 0.01f);
            float fl = bw * 0.5f - pw - kw;
            MeshKit.ChamferBox(mb, new Vector3(x - pw - fl * 0.5f, y + kh * 0.5f, zB + kw * 0.5f), new Vector3(fl, kh, kw), 0.01f);
            MeshKit.ChamferBox(mb, new Vector3(x + pw + fl * 0.5f, y + kh * 0.5f, zB + kw * 0.5f), new Vector3(fl, kh, kw), 0.01f);
            BeatKit.Emit(mb, g, "BalconyKerbs", mat, _meshes, ShadowCastingMode.On);

            // pier: tapered corbel slab (4 m deep at the root, 0.6 m at the tip); extruded along +X, section x = world -z
            var sec = new CrossSection();
            sec.Clear(true);
            sec.Add(new Vector2(-zB, y), true);
            sec.Add(new Vector2(-zD, y), true);
            sec.Add(new Vector2(-zD, y - PIER_TIP_DEPTH), true);
            sec.Add(new Vector2(-zB, y - PIER_ROOT_DEPTH), true);
            MeshKit.Extrude(mb, sec, new Vector3(x - pw, 0f, 0f), new Vector3(x + pw, 0f, 0f), Vector3.up, true);
            _pierB = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, g, "Pier", mat, _meshes, ShadowCastingMode.On);
        }

        void GrayDoor(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Material concrete = mats.Get(MatId.ConcreteBoard, FogDomain.Exterior);
            Transform g = BeatKit.Group(_geo, "Door");
            float x = X, y = Yf, zD = AbyssConst.W[18].z;
            float dw = AbyssConst.GOAL_DOOR_W * 0.5f, dh = AbyssConst.GOAL_DOOR_H;
            float fw = FRAME_W * 0.5f, jw = fw - dw;
            float zFront = zD + FRAME_DEPTH, zMid = zD + FRAME_DEPTH * 0.5f;

            // jambs and lintel (the lintel sits between the jambs: no overlapping coplanar faces)
            MeshKit.ChamferBox(mb, new Vector3(x - dw - jw * 0.5f, y + FRAME_H * 0.5f, zMid), new Vector3(jw, FRAME_H, FRAME_DEPTH), 0.02f);
            MeshKit.ChamferBox(mb, new Vector3(x + dw + jw * 0.5f, y + FRAME_H * 0.5f, zMid), new Vector3(jw, FRAME_H, FRAME_DEPTH), 0.02f);
            MeshKit.ChamferBox(mb, new Vector3(x, y + (dh + FRAME_H) * 0.5f, zMid), new Vector3(2f * dw, FRAME_H - dh, FRAME_DEPTH), 0.02f);
            _frameB = BeatKit.VertexBounds(mb, 0);
            _frameBaseY = y;
            BeatKit.Emit(mb, g, "Frame", concrete, _meshes, ShadowCastingMode.On);
            // closed back panel right behind the frame (closes the recess)
            MeshKit.ChamferBox(mb, new Vector3(x, y + FRAME_H * 0.5f, zD - BACK_PANEL_T * 0.5f), new Vector3(FRAME_W, FRAME_H, BACK_PANEL_T), 0.02f);
            BeatKit.Emit(mb, g, "BackPanel", concrete, _meshes, ShadowCastingMode.On);

            // leaf 1.0 x 2.0 in the middle of the recess, hinged on its -X edge; it swings toward the pier (+Z) when it opens
            float zLeaf = zMid - LEAF_T * 0.5f;
            _leafPivot = BeatKit.Group(g, "LeafHinge", new Vector3(x - dw, y, zLeaf), Quaternion.identity);
            MeshKit.ChamferBox(mb, new Vector3(dw, dh * 0.5f, 0f), new Vector3(2f * dw, dh, LEAF_T), 0.01f);
            _leafB = BeatKit.VertexBounds(mb, 0);
            MeshKit.ChamferBox(mb, new Vector3(2f * dw - 0.12f, dh * 0.48f, LEAF_T * 0.5f + 0.02f), new Vector3(0.04f, 0.18f, 0.04f), 0.008f);   // handle
            BeatKit.Emit(mb, _leafPivot, "Leaf", mats.Get(MatId.DoorLeaf, FogDomain.Exterior), _meshes, ShadowCastingMode.On);

            // amber backing 0.15 m behind the leaf (AMBER x 3.0)
            _backingMat = BackingMaterial(mats);
            BeatKit.ZCard(mb, x - dw, x + dw, y, y + dh, zLeaf - LEAF_T * 0.5f - BACKING_GAP, true);
            BeatKit.Emit(mb, g, "AmberBacking", _backingMat, _meshes, ShadowCastingMode.Off);

            // lamp above the door (its emission follows the door light, as the full tier) and two tension rods to the Face
            _lensMat = mats.Variant(MatId.LampAmber, FogDomain.Exterior, "b7doorlens", null);
            _lensBase = mats[MatId.LampAmber].GetFloat(ShaderIds.EmissionStrength);
            MeshKit.ChamferBox(mb, new Vector3(x, y + FRAME_H + 0.1f, zFront - 0.15f), new Vector3(0.3f, 0.2f, 0.2f), 0.02f);
            BeatKit.Emit(mb, g, "DoorLamp", _lensMat, _meshes, ShadowCastingMode.Off);
            for (int s = -1; s <= 1; s += 2)
            {
                var a = new Vector3(x + s * (fw - 0.15f), y + FRAME_H - 0.05f, zMid);
                var b = new Vector3(x + s * ROD_ANCHOR_DX, y + ROD_ANCHOR_UP, AbyssConst.W[16].z + 0.05f);
                MeshKit.Cylinder(mb, a, b, ROD_R, 12);
            }
            BeatKit.Emit(mb, g, "TensionRods", mats.Get(MatId.SteelPainted, FogDomain.Exterior), _meshes, ShadowCastingMode.On);
        }

        void GrayHoneycomb(BeatContext ctx, MeshBuilder mb)
        {
            _latticeMat = ctx.Mats.Variant(MatId.Lattice, "graybox", m => m.SetVector(BeatKit.DistFadeId, Vector4.zero));
            _latticeFade = Vector4.zero;
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            float y = AbyssConst.TERRACE_Y - LATTICE_DROP;
            int n = GRAY_LATTICE_SEGMENTS;
            var ring0 = new Vector3[n];
            var ring1 = new Vector3[n];
            // inner boundary: the hexagon (corners at 0, 60, ... degrees, the shaft orientation) by radial projection
            for (int i = 0; i < n; i++)
            {
                Vector2 d = BeatKit.Dir(i, n);   // from +X toward +Y of the helper = toward +Z here
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float rel = Mathf.Repeat(ang, 60f) - 30f;   // angle to the nearest face normal (30 + 60 k degrees)
                float r = GRAY_LATTICE_HOLE_APOTHEM / Mathf.Cos(rel * Mathf.Deg2Rad);
                ring0[i] = new Vector3(c.x + d.x * r, 0f, c.y + d.y * r);
            }
            for (int j = 0; j < GRAY_LATTICE_RINGS.Length; j++)
            {
                float R = GRAY_LATTICE_RINGS[j];
                for (int i = 0; i < n; i++)
                {
                    Vector2 d = BeatKit.Dir(i, n);
                    ring1[i] = new Vector3(c.x + d.x * R, 0f, c.y + d.y * R);
                }
                for (int i = 0; i < n; i++)
                {
                    int i1 = (i + 1) % n;
                    BeatKit.Quad(mb, ring0[i], ring0[i1], ring1[i1], ring1[i], Vector3.up);
                }
                var tmp = ring0; ring0 = ring1; ring1 = tmp;
            }
            _latticeRadius = BeatKit.VertexBounds(mb, 0).extents.x;
            MeshRenderer r0 = BeatKit.Emit(mb, _geo, "HoneycombLattice", _latticeMat, _meshes, ShadowCastingMode.Off, new Vector3(0f, y, 0f), true);
            if (r0 != null) { r0.receiveShadows = false; _latticeGo = r0.gameObject; }
        }

        // ---------------------------------------------------------------- materials shared by both tiers

        static Material StripMaterial(MaterialLibrary mats, int i)
        {
            float level = StripLevel(i);
            return mats.Variant(MatId.LampCold, FogDomain.Tunnel, "b7strip" + i, m => m.SetFloat(ShaderIds.EmissionStrength, STRIP_EMISSION * level));
        }

        static Material BackingMaterial(MaterialLibrary mats)
        {
            return mats.Variant(MatId.LampAmber, FogDomain.Exterior, "doorbacking", m => m.SetFloat(ShaderIds.EmissionStrength, BACKING_EMISSION));
        }
    }
}
