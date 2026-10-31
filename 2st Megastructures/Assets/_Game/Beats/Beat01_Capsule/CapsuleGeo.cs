using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>Every mesh builder of beat 1 (filled by <see cref="CapsuleGeo.Build"/>), grouped by material.</summary>
    internal sealed class CapsuleMeshes
    {
        public readonly MeshBuilder Interior = new MeshBuilder();   // B1_Interior: walls, ceiling, rear wall, front wall
        public readonly MeshBuilder Floor = new MeshBuilder();      // B1_Floor: 0.6 m tiles
        public readonly MeshBuilder Rib = new MeshBuilder();        // B1_Rib: ribs, hatch reveal and slot, seat pads
        public readonly MeshBuilder Bay = new MeshBuilder();        // B1_Bay: cabin-side porthole bay walls and collars (one-sided)
        public readonly MeshBuilder Metal = new MeshBuilder();      // Metal: seats, handrails
        public readonly MeshBuilder PortFrame = new MeshBuilder();  // Metal: porthole panel, frame, bore and rims
        public readonly MeshBuilder Strip = new MeshBuilder();      // B1_Strip: the two emissive strips
        public readonly MeshBuilder Gold = new MeshBuilder();       // PaleGold: porthole ring
        public readonly MeshBuilder Exterior = new MeshBuilder();   // Ceramic: outer skin, fairing, hatch collar
        public readonly MeshBuilder Cradle = new MeshBuilder();     // Metal: 4-leg cradle
        public readonly MeshBuilder LeafLeft = new MeshBuilder();   // Metal, local to the leaf pivot
        public readonly MeshBuilder LeafRight = new MeshBuilder();
        public readonly MeshBuilder RampDeck = new MeshBuilder();   // B1_Floor: gangway deck
        public readonly MeshBuilder RampFrame = new MeshBuilder();  // Metal: stringers, legs
        public readonly MeshBuilder Rails = new MeshBuilder();      // Metal: posts and rails merged
        public readonly MeshBuilder RampCollider = new MeshBuilder();
        public readonly MeshBuilder Pad = new MeshBuilder();        // Deck: landing pad
        public readonly MeshBuilder Motes = new MeshBuilder();      // Mote
        public PortholeAssembly Porthole;
    }

    /// <summary>
    /// Geometry of beat 1 (MASTER_PROMPT 14.5.1): the docking capsule (clear interior 2.6 x 2.5 x 11.0 m, walls 0.3 m,
    /// corner r 0.45, floor 0.9, ceiling 3.4, rear wall z -1.8..-1.5, hatch ring z 9.5..10.0), the porthole, 11 ribs,
    /// 2 folding seats, handrails, strips, the hatch collar with its slot and the two leaves, the exterior skin with the
    /// rear fairing and the 4-leg cradle, the gangway ramp (z 10..24, y 0.90 -> 0.03) with rails, the landing pad
    /// (6 x 6 m, z 24..30) and 60 motes. Pure maths (no scene objects), also run by the headless geometry self-test.
    /// </summary>
    internal static class CapsuleGeo
    {
        // ---- interior section (14.5.1 / 14.12)
        const float Wall = RingConstants.CapsuleWall;                                  // 0.3
        public static readonly RRect Inner = new RRect(0.5f * RingConstants.CapsuleInnerW, RingConstants.CapsuleFloorY,
            RingConstants.CapsuleCeilY, RingConstants.CapsuleCornerR);
        public static readonly RRect Outer = Inner.Offset(Wall);                       // 3.2 x 3.1, r 0.75
        const float Z0 = RingConstants.CapsuleZ0, Z1 = RingConstants.CapsuleZ1;       // -1.5 .. 9.5
        const float RearOuterZ = -1.8f;                                                // rear wall z -1.8..-1.5
        const float OuterFrontZ = 9.6f;                                                // outer skin ends 0.1 m inside the collar
        const float CapOverlap = 0.05f;                                                // end faces reach 5 cm into the wall

        // ---- rear fairing (exterior)
        const float FairLen = 0.45f, FairDepth = 0.45f; const int FairSteps = 6;

        // ---- ribs: 11, 1.0 m pitch, 0.12 m proud (14.5.1)
        const int RibCount = 11; const float RibZ0 = -1.0f, RibPitch = 1.0f, RibProud = 0.12f, RibHalfW = 0.05f, RibEmbed = 0.02f, RibCh = 0.015f;

        // ---- vertex AO (colour R) of the enclosed cabin. The shared lighting scales its sky terms (Trilight ambient and
        // the RingSkyColor reflection) by the occlusion; inside a sealed shell whose only aperture is the 0.55 m porthole
        // those terms are mostly occluded, otherwise the glazed floor and walls mirror the bright day sky (a cool, neutral
        // floor under the 2700 K strips). Walls and ceiling 0.85, floor 0.45 (rising up the floor fillets, so the floor /
        // fillet seam is continuous), contact shadows 0.5 on the wall within 0.18 m of a rib and at the rib roots.
        const float AoWall = 0.85f, AoFloor = 0.45f, AoRib = 0.5f, AoRibReach = 0.18f;
        // extra skin rows beside every rib (just outside its faces and at the end of the contact shadow)
        static readonly float[] RibAoRows = { RibHalfW + 0.015f, RibHalfW + AoRibReach };

        // ---- hatch collar / slot / leaves (14.5.1: opening 2.0 x 1.9, slots 0.1, clearance 0.02)
        const float HZ0 = RingConstants.HatchZ0, HZ1 = RingConstants.HatchZ1;           // 9.5 .. 10.0
        const float CollarX = 2.25f, CollarY0 = 0.45f, CollarY1 = 3.85f, CollarCh = 0.04f;
        public const float OpenX = 0.5f * RingConstants.HatchW;                         // 1.0
        public const float OpenY0 = RingConstants.CapsuleFloorY, OpenY1 = RingConstants.CapsuleFloorY + RingConstants.HatchH; // 0.9 .. 2.8
        const float OpenCh = 0.02f;
        public const float SlotZ0 = 9.70f, SlotZ1 = 9.80f;                              // 0.1 m slot
        const float PocketX = 2.15f, TrackY0 = 0.82f, TrackY1 = 2.88f;
        public const float Clear = 0.02f;
        public const float LeafT = (SlotZ1 - SlotZ0) - 2f * Clear;                      // 0.06
        public const float LeafW = 0.5f * RingConstants.HatchW + CapsuleHatch.LeafOverlap; // 1.10
        public const float LeafY0 = TrackY0 + Clear, LeafY1 = TrackY1 - Clear;          // 0.84 .. 2.86
        public static float LeafZ => 0.5f * (SlotZ0 + SlotZ1);                          // 9.75

        // ---- seats, handrail, strips
        const float SeatH = 0.45f;                                                      // 14.5.1 scale cue
        const float RailY = RingConstants.CapsuleFloorY + 1.0f, RailR = 0.025f, RailX = 1.3f - 0.16f;
        const float StripX = 0.55f;

        // ---- gangway / pad (14.5.1, 14.12)
        public const float RampHalf = 0.5f * RingConstants.RampW;                       // 1.2
        const float RampThick = 0.12f, PadEnd = 0.12f;                                  // the ramp runs over the pad's 0.12 chamfer
        const float GangRailX = 1.15f;
        const int GangPosts = 8;

        public static float RampTop(float z)
        {
            float t = Mathf.Clamp01((z - RingConstants.RampZ0) / (RingConstants.RampZ1 - RingConstants.RampZ0));
            return RingConstants.RampY0 + (RingConstants.RampY1 - RingConstants.RampY0) * t;
        }

        static bool FloorSeg(int k) => k == 0 || k == 8;

        public static CapsuleMeshes Build()
        {
            var m = new CapsuleMeshes();
            var port = new PortholeAssembly(Inner, Outer);
            m.Porthole = port;
            var inLoop = port.Loop(true);
            var outLoop = port.Loop(false);

            // ---------------- skins
            var usIn = SkinSamples(Inner, inLoop, 3, 10, 4, 6);
            var zsIn = ZSamples(Z0, Z1, inLoop, true);
            SkinGrid.Build(m.Interior, m.Floor, Inner, usIn, zsIn, true, inLoop, FloorSeg);
            var usOut = SkinSamples(Outer, outLoop, 3, 12, 4, 6);
            var zsOut = ZSamples(RearOuterZ, OuterFrontZ, outLoop, false);
            SkinGrid.Build(m.Exterior, null, Outer, usOut, zsOut, false, outLoop, null);

            // ---------------- porthole (tube, pod, frame, ring)
            port.EmitTube(m.Bay, m.Exterior, m.PortFrame);
            port.EmitPod(m.PortFrame, m.Exterior, m.Gold);

            // ---------------- ends
            RearWall(m.Interior);
            RearFairing(m.Exterior, usOut);
            Collar(m);

            // ---------------- interior details
            int ribV0 = m.Rib.VertexCount;
            Ribs(m.Rib, port);
            RibRootAo(m.Rib, ribV0);
            CabinAo(m.Interior, port);
            CabinAo(m.Floor, port);
            Seats(m);
            Handrails(m.Metal);
            Strips(m.Strip);

            // ---------------- exterior details
            Cradle(m.Cradle);
            Leaves(m.LeafLeft, m.LeafRight);

            // ---------------- gangway and pad
            Gangway(m);
            B12Geo.ChamferSlab(m.Pad, -RingConstants.PadHalfX, RingConstants.PadHalfX, RingConstants.PadZ0, RingConstants.PadZ1,
                RingConstants.PadTopY, PadSkirtY, PadChamfer);

            Motes(m.Motes, port);
            return m;
        }

        public const float PadChamfer = 0.12f, PadSkirtY = -0.2f;

        // ================================================================ skins
        static float[] SkinSamples(RRect rr, SkinLoop hole, int bottom, int fillet, int wall, int top)
        {
            var l = rr.Samples(bottom, fillet, wall, top);
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var q in hole.UV) { mn = Mathf.Min(mn, q.x); mx = Mathf.Max(mx, q.x); }
            foreach (var u in B12Geo.Range(mn - 0.12f, mx + 0.12f, 0.05f)) l.Add(u);
            return B12Geo.Merge(l, 0.01f);
        }

        static float[] ZSamples(float z0, float z1, SkinLoop hole, bool ribRows)
        {
            var l = new List<float>(B12Geo.Range(z0, z1, 0.5f));
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var q in hole.UV) { mn = Mathf.Min(mn, q.y); mx = Mathf.Max(mx, q.y); }
            foreach (var z in B12Geo.Range(mn - 0.12f, mx + 0.12f, 0.06f)) if (z > z0 + 0.02f && z < z1 - 0.02f) l.Add(z);
            if (ribRows)
                for (int r = 0; r < RibCount; r++)
                    foreach (float d in RibAoRows)
                        foreach (float z in new[] { RibZ0 + RibPitch * r - d, RibZ0 + RibPitch * r + d })
                            if (z > z0 + 0.02f && z < z1 - 0.02f) l.Add(z);
            return B12Geo.Merge(l, 0.01f);
        }

        // ================================================================ cabin vertex AO
        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>Base occlusion of the cabin skin at height y: the floor value on the floor, rising up the floor fillets
        /// (y 0.9 .. 1.35) to the wall value, which also holds on the ceiling.</summary>
        static float CabinBaseAo(float y) => Mathf.Lerp(AoFloor, AoWall, Smooth01((y - Inner.Y0) / Inner.R));

        /// <summary>True when rib zr runs over the skin point xy (ribs cover the walls and the ceiling, not the flat floor,
        /// and are cut around the porthole bay).</summary>
        static bool RibCovers(PortholeAssembly port, Vector2 xy, float zr)
        {
            float u = Inner.Param(xy);
            if (u < Inner.SegStart(1) - 1e-3f || u > Inner.SegStart(8) + 1e-3f) return false;
            return !RibHitsPorthole(port, u, zr);
        }

        /// <summary>Writes the cabin AO into colour R of every vertex of a skin builder (walls, ceiling, end walls, floor).</summary>
        static void CabinAo(MeshBuilder mb, PortholeAssembly port)
        {
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.V[i];
                float ao = CabinBaseAo(p.y);
                int r = Mathf.Clamp(Mathf.RoundToInt((p.z - RibZ0) / RibPitch), 0, RibCount - 1);
                float zr = RibZ0 + RibPitch * r;
                float dz = Mathf.Abs(p.z - zr) - RibHalfW;
                if (dz < AoRibReach && RibCovers(port, new Vector2(p.x, p.y), zr))
                    ao *= Mathf.Lerp(AoRib, 1f, Smooth01(dz / AoRibReach));
                var c = mb.C[i]; c.r = ao; mb.C[i] = c;
            }
        }

        /// <summary>Rib roots: AO from the contact value at the wall to the cabin value on the rib's inner face.</summary>
        static void RibRootAo(MeshBuilder mb, int from)
        {
            for (int i = from; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.V[i];
                float h = -Inner.Sdf(new Vector2(p.x, p.y));          // height above the wall surface (m)
                float ao = CabinBaseAo(p.y) * Mathf.Lerp(AoRib, 1f, Smooth01(h / (RibProud - RibCh)));
                var c = mb.C[i]; c.r = ao; mb.C[i] = c;
            }
        }

        /// <summary>Points of a section outline (n per fillet, straight segments as 1 piece) at z.</summary>
        static List<Vector3> Outline(RRect rr, float z, int perFillet)
        {
            var pts = new List<Vector3>();
            var us = rr.Samples(1, perFillet, 1, 2);
            for (int i = 0; i < us.Count - 1; i++)
            {
                rr.Point(us[i], out Vector2 p, out _);
                pts.Add(new Vector3(p.x, p.y, z));
            }
            return pts;
        }

        static void RearWall(MeshBuilder interior)
        {
            // inner face z -1.5 facing the cabin, 5 cm larger than the clear section (hidden overlap joint)
            var e = Inner.Offset(CapOverlap);
            var pts = Outline(e, Z0, 10);
            B12Geo.Polygon(interior, pts, Vector3.forward);
        }

        static void RearFairing(MeshBuilder ext, float[] us)
        {
            // quarter-ellipse taper from the outer section at z -1.8 to a flat cap (smooth normals, shared first row)
            int nu = us.Length;
            var prevP = new Vector3[nu]; var prevN = new Vector3[nu];
            for (int i = 0; i < nu; i++) SkinGrid.S(Outer, us[i], RearOuterZ, 1f, out prevP[i], out prevN[i]);
            var curP = new Vector3[nu]; var curN = new Vector3[nu];
            for (int k = 1; k <= FairSteps; k++)
            {
                float th = 0.5f * Mathf.PI * k / FairSteps;
                float d = FairDepth * (1f - Mathf.Cos(th));
                float z = RearOuterZ - FairLen * Mathf.Sin(th);
                var rr = Outer.Offset(-d);
                for (int i = 0; i < nu; i++)
                {
                    rr.PointLike(Outer, us[i], out Vector2 p, out Vector2 n2);
                    curP[i] = new Vector3(p.x, p.y, z);
                    curN[i] = (new Vector3(n2.x, n2.y, 0f) * (FairLen * Mathf.Cos(th)) + Vector3.back * (FairDepth * Mathf.Sin(th))).normalized;
                }
                for (int i = 0; i < nu - 1; i++)
                    B12Geo.Quad(ext, prevP[i], prevP[i + 1], curP[i + 1], curP[i], prevN[i], prevN[i + 1], curN[i + 1], curN[i],
                        new Vector2(us[i], 0f), new Vector2(us[i + 1], 0f), new Vector2(us[i + 1], 1f), new Vector2(us[i], 1f));
                var t = prevP; prevP = curP; curP = t; var tn = prevN; prevN = curN; curN = tn;
            }
            var cap = new List<Vector3>(nu - 1);
            for (int i = 0; i < nu - 1; i++) cap.Add(prevP[i]);
            B12Geo.Polygon(ext, cap, Vector3.back);
        }

        // ================================================================ hatch collar
        static void Collar(CapsuleMeshes m)
        {
            var ext = m.Exterior;
            float cx = CollarX - CollarCh, y0 = CollarY0 + CollarCh, y1 = CollarY1 - CollarCh;
            // outer chamfered box without its two z faces
            var box = new MeshBuilder();
            box.ChamferedBox(new Vector3(0f, 0.5f * (CollarY0 + CollarY1), 0.5f * (HZ0 + HZ1)),
                new Vector3(2f * CollarX, CollarY1 - CollarY0, HZ1 - HZ0), CollarCh);
            AppendExceptPlanes(ext, box, HZ0, HZ1);

            var rect = new List<Vector3> { new Vector3(-cx, y0, 0f), new Vector3(cx, y0, 0f), new Vector3(cx, y1, 0f), new Vector3(-cx, y1, 0f) };
            // front face z 10.0 (exterior) with the chamfered opening
            float fx = OpenX + OpenCh, fy = OpenY1 + OpenCh;
            B12Geo.PolygonWithHole(ext, AtZ(rect, HZ1), new List<Vector3>
            {
                new Vector3(-fx, OpenY0, HZ1), new Vector3(fx, OpenY0, HZ1), new Vector3(fx, fy, HZ1), new Vector3(-fx, fy, HZ1)
            }, Vector3.forward);
            // back face z 9.5: exterior part (outside the 5 cm grown clear section) and the cabin's front wall
            var e = Inner.Offset(CapOverlap);
            var eLoop = Outline(e, HZ0, 10);
            B12Geo.PolygonWithHole(ext, AtZ(rect, HZ0), eLoop, Vector3.back);
            B12Geo.PolygonWithHole(m.Interior, eLoop, new List<Vector3>
            {
                new Vector3(-OpenX, OpenY0, HZ0), new Vector3(OpenX, OpenY0, HZ0), new Vector3(OpenX, OpenY1, HZ0), new Vector3(-OpenX, OpenY1, HZ0)
            }, Vector3.back);
            // the slivers between the floor fillets and the flat sill inside the opening (seen from the passage)
            for (int s = -1; s <= 1; s += 2)
            {
                var sl = new List<Vector3>();
                float xs = Inner.A - Inner.R;   // 0.85: end of the flat floor
                for (int i = 0; i <= 6; i++)
                {
                    float x = Mathf.Lerp(xs, OpenX, i / 6f);
                    float dx = x - xs;
                    float y = Inner.Y0 + Inner.R - Mathf.Sqrt(Mathf.Max(0f, Inner.R * Inner.R - dx * dx));
                    sl.Add(new Vector3(s * x, y, HZ0));
                }
                sl.Add(new Vector3(s * OpenX, OpenY0, HZ0));
                B12Geo.Polygon(m.Rib, sl, Vector3.forward);
            }

            var rib = m.Rib;
            // passage: jambs, head, sill (z 9.5..9.7 and 9.8..9.98 / 10.0), front chamfer strips
            float zc = HZ1 - OpenCh;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * OpenX;
                Vector3 n = new Vector3(-s, 0f, 0f);
                B12Geo.QuadN(rib, new Vector3(x, OpenY0, HZ0), new Vector3(x, OpenY1, HZ0), new Vector3(x, OpenY1, SlotZ0), new Vector3(x, OpenY0, SlotZ0), n);
                B12Geo.QuadN(rib, new Vector3(x, OpenY0, SlotZ1), new Vector3(x, OpenY1, SlotZ1), new Vector3(x, OpenY1, zc), new Vector3(x, OpenY0, zc), n);
                // front chamfer of the jamb
                B12Geo.QuadN(rib, new Vector3(x, OpenY0, zc), new Vector3(x, OpenY1, zc), new Vector3(s * fx, fy, HZ1), new Vector3(s * fx, OpenY0, HZ1),
                    new Vector3(-s, 0f, 1f).normalized);
                // sill corner under the chamfer
                B12Geo.TriN(rib, new Vector3(x, OpenY0, zc), new Vector3(s * fx, OpenY0, HZ1), new Vector3(x, OpenY0, HZ1), Vector3.up);
            }
            B12Geo.QuadN(rib, new Vector3(-OpenX, OpenY1, HZ0), new Vector3(OpenX, OpenY1, HZ0), new Vector3(OpenX, OpenY1, SlotZ0), new Vector3(-OpenX, OpenY1, SlotZ0), Vector3.down);
            B12Geo.QuadN(rib, new Vector3(-OpenX, OpenY1, SlotZ1), new Vector3(OpenX, OpenY1, SlotZ1), new Vector3(OpenX, OpenY1, zc), new Vector3(-OpenX, OpenY1, zc), Vector3.down);
            B12Geo.QuadN(rib, new Vector3(-OpenX, OpenY1, zc), new Vector3(OpenX, OpenY1, zc), new Vector3(fx, fy, HZ1), new Vector3(-fx, fy, HZ1), new Vector3(0f, -1f, 1f).normalized);
            B12Geo.QuadN(rib, new Vector3(-OpenX, OpenY0, HZ0), new Vector3(OpenX, OpenY0, HZ0), new Vector3(OpenX, OpenY0, SlotZ0), new Vector3(-OpenX, OpenY0, SlotZ0), Vector3.up);
            B12Geo.QuadN(rib, new Vector3(-OpenX, OpenY0, SlotZ1), new Vector3(OpenX, OpenY0, SlotZ1), new Vector3(OpenX, OpenY0, HZ1), new Vector3(-OpenX, OpenY0, HZ1), Vector3.up);

            // slot cavity (z 9.7..9.8): front and back walls around the opening, pocket and track walls
            var cav = new List<Vector3> { new Vector3(-PocketX, TrackY0, 0f), new Vector3(PocketX, TrackY0, 0f), new Vector3(PocketX, TrackY1, 0f), new Vector3(-PocketX, TrackY1, 0f) };
            var op = new List<Vector3> { new Vector3(-OpenX, OpenY0, 0f), new Vector3(OpenX, OpenY0, 0f), new Vector3(OpenX, OpenY1, 0f), new Vector3(-OpenX, OpenY1, 0f) };
            B12Geo.PolygonWithHole(rib, AtZ(cav, SlotZ1), AtZ(op, SlotZ1), Vector3.back);
            B12Geo.PolygonWithHole(rib, AtZ(cav, SlotZ0), AtZ(op, SlotZ0), Vector3.forward);
            for (int s = -1; s <= 1; s += 2)
                B12Geo.QuadN(rib, new Vector3(s * PocketX, TrackY0, SlotZ0), new Vector3(s * PocketX, TrackY1, SlotZ0), new Vector3(s * PocketX, TrackY1, SlotZ1), new Vector3(s * PocketX, TrackY0, SlotZ1), new Vector3(-s, 0f, 0f));
            B12Geo.QuadN(rib, new Vector3(-PocketX, TrackY0, SlotZ0), new Vector3(PocketX, TrackY0, SlotZ0), new Vector3(PocketX, TrackY0, SlotZ1), new Vector3(-PocketX, TrackY0, SlotZ1), Vector3.up);
            B12Geo.QuadN(rib, new Vector3(-PocketX, TrackY1, SlotZ0), new Vector3(PocketX, TrackY1, SlotZ0), new Vector3(PocketX, TrackY1, SlotZ1), new Vector3(-PocketX, TrackY1, SlotZ1), Vector3.down);
        }

        static List<Vector3> AtZ(List<Vector3> pts, float z)
        {
            var l = new List<Vector3>(pts.Count);
            foreach (var p in pts) l.Add(new Vector3(p.x, p.y, z));
            return l;
        }

        /// <summary>Appends src to dst except the triangles lying entirely in the plane z = za or z = zb.</summary>
        static void AppendExceptPlanes(MeshBuilder dst, MeshBuilder src, float za, float zb)
        {
            for (int t = 0; t + 2 < src.I.Count; t += 3)
            {
                int a = src.I[t], b = src.I[t + 1], c = src.I[t + 2];
                Vector3 pa = src.V[a], pb = src.V[b], pc = src.V[c];
                bool onA = Mathf.Abs(pa.z - za) < 1e-4f && Mathf.Abs(pb.z - za) < 1e-4f && Mathf.Abs(pc.z - za) < 1e-4f;
                bool onB = Mathf.Abs(pa.z - zb) < 1e-4f && Mathf.Abs(pb.z - zb) < 1e-4f && Mathf.Abs(pc.z - zb) < 1e-4f;
                if (onA || onB) continue;
                int i0 = dst.Vtx(pa, src.N[a], src.UV[a], src.C[a]);
                int i1 = dst.Vtx(pb, src.N[b], src.UV[b], src.C[b]);
                int i2 = dst.Vtx(pc, src.N[c], src.UV[c], src.C[c]);
                dst.Tri(i0, i1, i2);
            }
        }

        // ================================================================ ribs
        static void Ribs(MeshBuilder rib, PortholeAssembly port)
        {
            var us = new List<float>(Inner.Samples(1, 10, 1, 1));
            // dense samples where the left wall and the TL fillet meet the porthole
            for (float u = Inner.SegStart(5) - 0.4f; u <= Inner.SegStart(7) + 0.05f; u += 0.03f) us.Add(u);
            float ua = Inner.SegStart(1), ub = Inner.SegStart(8);
            var su = new List<float>();
            foreach (var u in B12Geo.Merge(us, 0.004f)) if (u >= ua - 1e-4f && u <= ub + 1e-4f) su.Add(u);
            var prof = new[]
            {
                new Vector2(-RibHalfW, -RibEmbed), new Vector2(RibHalfW, -RibEmbed), new Vector2(RibHalfW, RibProud - RibCh),
                new Vector2(RibHalfW - RibCh, RibProud), new Vector2(-RibHalfW + RibCh, RibProud), new Vector2(-RibHalfW, RibProud - RibCh)
            };
            for (int r = 0; r < RibCount; r++)
            {
                float z = RibZ0 + RibPitch * r;
                var blocked = new bool[su.Count];
                for (int i = 0; i < su.Count; i++) blocked[i] = RibHitsPorthole(port, su[i], z);
                // grow the blocked runs by one sample (the cut ends stay clear of the bay)
                var grown = (bool[])blocked.Clone();
                for (int i = 0; i < su.Count; i++)
                    if (blocked[i]) { if (i > 0) grown[i - 1] = true; if (i + 1 < su.Count) grown[i + 1] = true; }
                int run0 = -1;
                for (int i = 0; i <= su.Count; i++)
                {
                    bool ok = i < su.Count && !grown[i];
                    if (ok && run0 < 0) run0 = i;
                    if (!ok && run0 >= 0)
                    {
                        if (i - 1 > run0) RibRun(rib, su, run0, i - 1, z, prof);
                        run0 = -1;
                    }
                }
            }
        }

        static bool RibHitsPorthole(PortholeAssembly port, float u, float z)
        {
            Inner.Point(u, out Vector2 p, out Vector2 n);
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                {
                    float h = a == 0 ? 0f : RibProud;
                    float w = b == 0 ? -RibHalfW : RibHalfW;
                    Vector3 q = new Vector3(p.x - n.x * h, p.y - n.y * h, z + w) - port.C;
                    float ax = Vector3.Dot(q, port.N);
                    float rad = (q - port.N * ax).magnitude;
                    if (ax > -0.7f && ax < 0.9f && rad < PortholeAssembly.R + 0.05f) return true;
                }
            return false;
        }

        static void RibRun(MeshBuilder mb, List<float> su, int i0, int i1, float z, Vector2[] prof)
        {
            int np = prof.Length;
            int ns = i1 - i0 + 1;
            var pos = new Vector3[ns, np];
            var nin = new Vector3[ns];
            var tan = new Vector3[ns];
            for (int s = 0; s < ns; s++)
            {
                Inner.Point(su[i0 + s], out Vector2 p, out Vector2 n);
                Vector3 ni = new Vector3(-n.x, -n.y, 0f);
                nin[s] = ni;
                tan[s] = new Vector3(-n.y, n.x, 0f);
                for (int k = 0; k < np; k++) pos[s, k] = new Vector3(p.x, p.y, z) + ni * prof[k].y + Vector3.forward * prof[k].x;
            }
            // faces: profile edges 1..np-1 (edge 0 is the embedded base)
            for (int e = 1; e < np; e++)
            {
                int k0 = e, k1 = (e + 1) % np;
                Vector2 ed = prof[k1] - prof[k0];
                Vector2 en = new Vector2(ed.y, -ed.x).normalized;   // outward (counter-clockwise profile in (w, h))
                for (int s = 0; s < ns - 1; s++)
                {
                    Vector3 na = (Vector3.forward * en.x + nin[s] * en.y).normalized;
                    Vector3 nb = (Vector3.forward * en.x + nin[s + 1] * en.y).normalized;
                    B12Geo.Quad(mb, pos[s, k0], pos[s, k1], pos[s + 1, k1], pos[s + 1, k0], na, na, nb, nb,
                        new Vector2(su[i0 + s], 0f), new Vector2(su[i0 + s], 1f), new Vector2(su[i0 + s + 1], 1f), new Vector2(su[i0 + s + 1], 0f));
                }
            }
            var cap = new List<Vector3>(np);
            for (int end = 0; end < 2; end++)
            {
                int s = end == 0 ? 0 : ns - 1;
                cap.Clear();
                for (int k = 0; k < np; k++) cap.Add(pos[s, k]);
                B12Geo.Polygon(mb, cap, end == 0 ? -tan[s] : tan[s]);
            }
        }

        // ================================================================ seats, handrails, strips
        static void Seats(CapsuleMeshes m)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float zc = side < 0 ? 1.5f : 7.5f;            // between ribs (left near the rear, right near the hatch)
                float sx = side;                               // wall at x = sx * 1.3
                float wall = 0.5f * RingConstants.CapsuleInnerW;
                float top = RingConstants.CapsuleFloorY + SeatH;
                // back pad on the wall
                BoxX(m.Rib, sx, wall + 0.01f, wall - 0.04f, 1.50f, 2.05f, zc - 0.23f, zc + 0.23f, 0.012f);
                // seat pan (folded down, top 0.45 m above the floor)
                BoxX(m.Metal, sx, wall + 0.01f, wall - 0.44f, top - 0.04f, top, zc - 0.23f, zc + 0.23f, 0.012f);
                // hinge blocks and struts
                for (int k = -1; k <= 1; k += 2)
                {
                    float z = zc + k * 0.19f;
                    BoxX(m.Metal, sx, wall + 0.01f, wall - 0.09f, top - 0.11f, top - 0.035f, z - 0.018f, z + 0.018f, 0.006f);
                    Vector3 a = new Vector3(sx * (wall + 0.005f), RingConstants.CapsuleFloorY + 0.16f, z);
                    Vector3 b = new Vector3(sx * (wall - 0.40f), top - 0.045f, z);
                    B12Geo.BoxSpan(m.Metal, a, b, 0.028f, 0.028f, 0.006f, Vector3.forward);
                }
            }
        }

        /// <summary>Axis-aligned chamfered box given wall-relative x extents (|x| from xa to xb on side sx).</summary>
        static void BoxX(MeshBuilder mb, float sx, float xa, float xb, float y0, float y1, float z0, float z1, float ch)
        {
            float x0 = sx * xa, x1 = sx * xb;
            B12Geo.BoxMinMax(mb, new Vector3(Mathf.Min(x0, x1), y0, z0), new Vector3(Mathf.Max(x0, x1), y1, z1), ch);
        }

        static void Handrails(MeshBuilder metal)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * RailX;
                B12Geo.Tube(metal, new[] { new Vector3(x, RailY, Z0 + 0.3f), new Vector3(x, RailY, Z1 - 0.3f) }, RailR, 12, true);
                // brackets from the rib faces (0.12 proud) to the rod
                float face = 0.5f * RingConstants.CapsuleInnerW - RibProud;
                for (int r = 1; r < RibCount; r++)
                {
                    float z = RibZ0 + RibPitch * r;
                    BoxX(metal, s, face + 0.01f, RailX - 0.005f, RailY - 0.015f, RailY + 0.015f, z - 0.015f, z + 0.015f, 0.004f);
                }
            }
        }

        static void Strips(MeshBuilder strip)
        {
            float ceil = RingConstants.CapsuleCeilY;
            for (int s = -1; s <= 1; s += 2)
                B12Geo.BoxMinMax(strip, new Vector3(s * StripX - 0.025f, ceil - 0.012f, Z0 + 0.3f), new Vector3(s * StripX + 0.025f, ceil + 0.004f, Z1 - 0.3f), 0.004f);
        }

        // ================================================================ exterior
        static void Cradle(MeshBuilder mb)
        {
            foreach (float z in new[] { 0.6f, 7.4f })
            {
                // saddle following the hull bottom (embedded 3 cm), extruded +-0.12 m in z
                var prof = new List<Vector2>();
                const float half = 1.25f, bottom = 0.40f;
                prof.Add(new Vector2(-half, bottom)); prof.Add(new Vector2(half, bottom));
                for (int i = 0; i <= 16; i++)
                {
                    float x = Mathf.Lerp(half, -half, i / 16f);
                    prof.Add(new Vector2(x, HullBottom(x) + 0.03f));
                }
                var path = new[] { new Vector3(0f, 0f, z - 0.12f), new Vector3(0f, 0f, z + 0.12f) };
                var side = new[] { Vector3.right, Vector3.right };
                B12Geo.Sweep(mb, path, side, prof, true, true, true);
                for (int s = -1; s <= 1; s += 2)
                {
                    B12Geo.BoxMinMax(mb, new Vector3(s * 1.0f - 0.09f, -0.05f, z - 0.09f), new Vector3(s * 1.0f + 0.09f, bottom + 0.02f, z + 0.09f), 0.015f);
                    B12Geo.BoxMinMax(mb, new Vector3(s * 1.0f - 0.18f, -0.03f, z - 0.18f), new Vector3(s * 1.0f + 0.18f, 0.03f, z + 0.18f), 0.01f);
                }
            }
        }

        /// <summary>y of the outer hull bottom at x (|x| &lt;= 1.6).</summary>
        static float HullBottom(float x)
        {
            float ax = Mathf.Abs(x), flat = Outer.A - Outer.R;
            if (ax <= flat) return Outer.Y0;
            float dx = Mathf.Min(ax - flat, Outer.R);
            return Outer.Y0 + Outer.R - Mathf.Sqrt(Mathf.Max(0f, Outer.R * Outer.R - dx * dx));
        }

        static void Leaves(MeshBuilder left, MeshBuilder right)
        {
            var size = new Vector3(LeafW, LeafY1 - LeafY0, LeafT);
            left.ChamferedBox(Vector3.zero, size, 0.008f);
            right.ChamferedBox(Vector3.zero, size, 0.008f);
            // astragal lip on the right leaf's front face, overlapping the left leaf by 3 cm (covers the meeting seam)
            float zf = 0.5f * LeafT;
            B12Geo.BoxMinMax(right, new Vector3(-0.5f * LeafW - 0.03f, -0.5f * size.y + 0.01f, zf - 0.005f),
                new Vector3(-0.5f * LeafW + 0.02f, 0.5f * size.y - 0.01f, zf + Clear - 0.002f), 0.003f);
        }

        // ================================================================ gangway
        static float RampDeckTop(float z) => z <= RingConstants.RampZ1 ? RampTop(z) : RingConstants.RampY1;

        static void Gangway(CapsuleMeshes m)
        {
            float z0 = RingConstants.RampZ0, z1 = RingConstants.RampZ1 + PadEnd;
            var zs = new List<float>(B12Geo.Range(z0, RingConstants.RampZ1, 1.0f)) { z1 };
            B12Geo.TopFollowPrism(m.RampDeck, 0f, RampHalf, zs, RampDeckTop, z => RampDeckTop(z) - RampThick, 0.015f, true, true);
            // collider: the visible deck surface (14.4 colliders (2))
            B12Geo.TopFollowPrism(m.RampCollider, 0f, RampHalf, zs, RampDeckTop, z => RampDeckTop(z) - RampThick, 0f, true, true);
            // stringers and legs
            for (int s = -1; s <= 1; s += 2)
            {
                B12Geo.TopFollowPrism(m.RampFrame, s * 0.85f, 0.06f, 10.05f, 22.5f, z => RampTop(z) - RampThick + 0.01f,
                    z => RampTop(z) - RampThick - 0.2f, 0.01f, 1.0f);
                foreach (float z in new[] { 12.5f, 16.5f })
                {
                    float topY = RampTop(z) - RampThick - 0.18f;
                    B12Geo.BoxMinMax(m.RampFrame, new Vector3(s * 0.85f - 0.05f, -0.08f, z - 0.05f), new Vector3(s * 0.85f + 0.05f, topY, z + 0.05f), 0.01f);
                    B12Geo.BoxMinMax(m.RampFrame, new Vector3(s * 0.85f - 0.12f, -0.02f, z - 0.12f), new Vector3(s * 0.85f + 0.12f, 0.025f, z + 0.12f), 0.008f);
                }
            }
            // rails 1.0 m: posts every ~2 m and two round rails per side, merged into one mesh
            float pz0 = z0 + 0.15f, pz1 = RingConstants.RampZ1 - 0.15f;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * GangRailX;
                for (int i = 0; i < GangPosts; i++)
                {
                    float z = Mathf.Lerp(pz0, pz1, i / (float)(GangPosts - 1));
                    float y0 = RampTop(z) - 0.02f, y1 = RampTop(z) + RingConstants.RailH - 0.025f;
                    B12Geo.BoxMinMax(m.Rails, new Vector3(x - 0.025f, y0, z - 0.025f), new Vector3(x + 0.025f, y1, z + 0.025f), 0.006f);
                }
                B12Geo.Tube(m.Rails, new[] { new Vector3(x, RampTop(pz0) + RingConstants.RailH - 0.025f, pz0 - 0.05f), new Vector3(x, RampTop(pz1) + RingConstants.RailH - 0.025f, pz1 + 0.05f) }, 0.025f, 12, true);
                B12Geo.Tube(m.Rails, new[] { new Vector3(x, RampTop(pz0) + 0.5f, pz0), new Vector3(x, RampTop(pz1) + 0.5f, pz1) }, 0.018f, 10, true);
            }
        }

        // ================================================================ motes
        static void Motes(MeshBuilder mb, PortholeAssembly port)
        {
            // 14.5.1: 60 additive motes: 40 in the porthole sunbeam (along -L from the opening), 20 through the cabin
            Vector3 beam = -RingConstants.StarL.normalized;
            Vector3 a = Vector3.Cross(beam, Vector3.up).normalized, c = Vector3.Cross(beam, a);
            Color tint = RingPalette.Lin("CeramicWhite");
            uint h = 0x51ED27u;
            int made = 0, guard = 0;
            while (made < 40 && guard++ < 4000)
            {
                h = RingMath.Pcg(h + 0x9E37u);
                float r1 = RingMath.U01(h); h = RingMath.Pcg(h);
                float r2 = RingMath.U01(h); h = RingMath.Pcg(h);
                float r3 = RingMath.U01(h); h = RingMath.Pcg(h);
                float rs = RingMath.U01(h); h = RingMath.Pcg(h);
                float rb = RingMath.U01(h);
                float along = Mathf.Lerp(0.55f, 3.1f, r1);
                float rad = PortholeAssembly.Ro * 1.3f * Mathf.Sqrt(r2);
                float ang = 2f * Mathf.PI * r3;
                Vector3 p = port.C + beam * along + (a * Mathf.Cos(ang) + c * Mathf.Sin(ang)) * rad;
                if (!InCabin(p, 0.12f)) continue;
                B12Additive.Mote(mb, p, rs, tint * (1f - 0.4f * rb), 1f);
                made++;
            }
            guard = 0;
            while (made < 60 && guard++ < 4000)
            {
                h = RingMath.Pcg(h + 0x7F4Au);
                float rx = RingMath.U01(h); h = RingMath.Pcg(h);
                float ry = RingMath.U01(h); h = RingMath.Pcg(h);
                float rz = RingMath.U01(h); h = RingMath.Pcg(h);
                float rs = RingMath.U01(h);
                var p = new Vector3(Mathf.Lerp(-1.1f, 1.1f, rx), Mathf.Lerp(1.1f, 3.25f, ry), Mathf.Lerp(Z0 + 0.4f, Z1 - 0.4f, rz));
                if (!InCabin(p, 0.15f)) continue;
                B12Additive.Mote(mb, p, rs, tint * 0.8f, 0.6f);
                made++;
            }
        }

        static bool InCabin(Vector3 p, float clearance) => Inner.Sdf(new Vector2(p.x, p.y)) < -clearance && p.z > Z0 + clearance && p.z < Z1 - clearance;
    }
}
