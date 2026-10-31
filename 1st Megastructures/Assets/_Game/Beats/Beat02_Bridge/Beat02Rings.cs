using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Ring ribs and belts of the shaft S0 (14.5.2 a, 14.7.2): the level layout and the mitred hexagonal ring meshes, shared
    /// by <see cref="Beat02_Bridge"/> and the EditMode tests (Beat02Tests: counts, no coplanar overlapping faces, clearances).
    ///
    /// Levels: k = 0..73 at y = AbyssConst.RingY(k) (1170 - 60 k, lead arbitration), a belt where RingIsBelt(k) (k % 4 == 0,
    /// 19 belts), otherwise a rib (55 ribs). The Simplified tier keeps the even levels only (ribs every 120 m, all belts).
    ///
    /// Meshes (ring-local: origin on the shaft axis at the ring's centre height, never scaled):
    /// - Full ring: six mitred sides (instanced at every level without a gap).
    /// - Lane body + lane cut: levels whose band reaches the hoist travel (k 0..28) leave a vertical hoist lane open on the far
    ///   flat (x = Beat02_Bridge.HOIST_X +/- 8, the 10 m block and its guide rails run in it); the body holds the five other sides, the cut mesh
    ///   the two far-flat pieces with end caps. Both are instanced (the cut is identical at every lane level).
    /// - Bore levels (bands reaching the clear circle R 158 around the fan axis (-45, -317) on the near flat, k 23..27): one
    ///   separate, non-instanced mesh per level with the near-flat side gapped on the planes x = x0 / x1 (end caps), which put
    ///   the ring end 2 m radially outside Beat06's raised rim (collar R 146-156, z 58.8-62 = AbyssConst.CITY_FAN_EXCLUSION_R)
    ///   at the band edge nearest the fan centre, so every ring end lies on the circle R 158 concentric with the rim. A piece
    ///   shorter than 3 m at a corner is dropped and the neighbouring side ends on the corner bisector (capped) instead,
    ///   unless that mitre cap would stand inside the clear circle less than 2 m in front of the rim face (k 25: the gap
    ///   overruns the shaft corner): then the neighbouring side is cut on the plane x = x0 / x1 as well (clamped to the wall
    ///   corner), so all of its end stays outside R 158 (Beat02Tests: rim clearance).
    /// Profile (apothem a, height h) per side: top (wall to inner edge), 45 degree bevel, inner face, bevel, bottom; the top and
    /// bottom run 0.5 m into the wall (no hairline) and carry baked AO (vertex R) darkening toward the wall over 2.5 m; bevels
    /// carry edge wear (vertex G); the 45 degree top chamfer carries vertex B = 255 (every other face B = 0), a per-vertex
    /// marker of the chamfer (critique R2-06: a second, slower depth falloff of the shaft sun on the ring chamfers alone, so
    /// the rings below the deck step down into the abyss; AbyssLighting's AbRingChamferFactor currently selects the chamfers
    /// by normal and ring band, and the Surface shader ignores vertex B). Every corner vertex comes from one
    /// double-precision table, so pieces of different meshes meet bit-identically on the mitres.
    /// </summary>
    public static class Beat02Rings
    {
        public const int FAR_SIDE = 1;               // face normal 90 degrees: far flat z = 760
        public const int NEAR_SIDE = 4;              // face normal 270 degrees: near flat z = 60
        public const float EMBED = 0.5f;             // top/bottom run this far into the wall
        public const float AO_STRIP = 2.5f;          // AO ramp on top/bottom faces, measured from the embedded edge
        public const float AO_WALL = 0.55f;
        public const int SIDE_SEGMENTS = 4;
        public const float LANE_HALF = 8f;           // hoist lane half width on the far flat (block 5 + 3 m)
        public const float LANE_CLEARANCE = 3f;      // vertical clearance below the lowest block position
        public const float RIM_R = AbyssConst.CITY_FAN_EXCLUSION_R;   // outer radius of Beat06's raised rim (collar R 146-156)
        public const float RIM_FRONT_Z = AbyssConst.SHAFT_NEAR_Z + 2f; // the rim's front face (Beat06_Fan COLLAR_Z_FRONT 62)
        public const float BORE_MARGIN = 2f;         // ring ends stay this far (radially) outside the rim
        public const float CLEAR_R = RIM_R + BORE_MARGIN;              // 158: no ring end inside this circle around the fan axis
        public const float MIN_PIECE = 3f;           // shorter pieces (inner face) are dropped
        public const float MARKER_INSET = 0.6f;      // corner marker lights sit this far inside the inner corner

        public struct Level
        {
            public int K;
            public float Y;
            public bool Belt;
            public bool Lane;   // far flat gapped for the hoist lane
            public bool Bore;   // near flat gapped around the fan bore
        }

        /// <summary>One placed ring mesh: an instanced draw (Instanced) or a separate renderer.</summary>
        public struct Draw
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public bool Instanced;
            public int K;
            public bool Belt;
        }

        /// <summary>The meshes and placements of all ring levels of one tier.</summary>
        public sealed class Set
        {
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public readonly List<Draw> Draws = new List<Draw>();
            public readonly List<Level> Levels = new List<Level>();
            public int Ribs, Belts;
            public Mesh RibFull, BeltFull;
        }

        /// <summary>
        /// 45 degree chamfer along the inner TOP edge of every rib (rise = run = 2 m of its 6 m) and belt (4 m of 12 m), in
        /// place of the 3 / 5 cm top bevel (the bottom bevel is unchanged). Critic round 1 (R1-06): from the bridge the ribs and
        /// belts above the eye show only their inner face and underside, both turned away from the shaft sun (it travels toward
        /// -Z, steeply down), so the 60 m / 240 m rhythm read only where a belt top lay below the eye. The chamfer faces up and
        /// inward: it is visible from any point below the ring up to 45 degrees of elevation and takes the Key at N.L 0.47-0.85
        /// on every face, so each ring reads as a lit line over its dark inner face (about 3 px for a rib at 640 m, 6 px for a
        /// belt; at 300 m 6.6 / 13 px), and the f02 3 cm top bevel, whose sub-pixel highlight crawled in dashes (R1-26, B3
        /// free-look back), is gone. Height, protrusion and the inner apothem are unchanged (spec 14.5.2 a; the bevel value is
        /// the returned arbitration).
        /// </summary>
        public const float RIB_TOP_CHAMFER = 2f, BELT_TOP_CHAMFER = 4f;

        public static float Height(bool belt) => belt ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT;
        public static float InnerApothem(bool belt) => belt ? AbyssConst.BELT_INNER_APOTHEM : AbyssConst.RIB_INNER_APOTHEM;
        public static float Bevel(bool belt) => belt ? AbyssConst.BELT_BEVEL : AbyssConst.RIB_BEVEL;
        public static float TopChamfer(bool belt) => belt ? BELT_TOP_CHAMFER : RIB_TOP_CHAMFER;
        public static float LaneX => Beat02_Bridge.HOIST_X;

        /// <summary>Lowest point of the hoist lane gap: the block's lowest bottom (y(t) min - 5) minus the clearance.</summary>
        public static float LaneBottomY => (float)Beat02_Bridge.HoistYMin - 0.5f * AbyssConst.HOIST_SIZE - LANE_CLEARANCE;

        /// <summary>Ring centre (shaft axis) at height y.</summary>
        public static Vector3 Centre(float y) => new Vector3(AbyssConst.SHAFT_CENTRE.x, y, AbyssConst.SHAFT_CENTRE.y);

        /// <summary>Level list of a tier (everyOther: Simplified; hoistLane: false when the tier has no hoist).</summary>
        public static void Levels(bool everyOther, bool hoistLane, List<Level> result)
        {
            result.Clear();
            Vector3 fan = AbyssConst.FAN_CENTRE;
            float boreLo = fan.y - CLEAR_R, boreHi = fan.y + CLEAR_R;
            float laneBottom = LaneBottomY;
            for (int k = 0; k < AbyssConst.RIB_LEVELS; k++)
            {
                if (everyOther && (k & 1) == 1) continue;
                float y = AbyssConst.RingY(k);
                bool belt = AbyssConst.RingIsBelt(k);
                float hh = 0.5f * Height(belt);
                result.Add(new Level
                {
                    K = k, Y = y, Belt = belt,
                    Lane = hoistLane && y + hh > laneBottom,
                    Bore = y + hh > boreLo && y - hh < boreHi,
                });
            }
        }

        /// <summary>
        /// x range [x0, x1] of the near-flat gap of a ring band [y - hh, y + hh] around the fan bore: the widest chord of the
        /// clear circle (rim R 156 + BORE_MARGIN = R 158) inside the band, so the nearest corner of each ring end is exactly
        /// BORE_MARGIN outside the rim and every other point of it further.
        /// </summary>
        public static void BoreGap(float y, float hh, out float x0, out float x1)
        {
            Vector3 fan = AbyssConst.FAN_CENTRE;
            const float R = CLEAR_R;
            float lo = y - hh, hi = y + hh;
            float dy = fan.y < lo ? lo - fan.y : (fan.y > hi ? fan.y - hi : 0f);
            float half = dy < R ? Mathf.Sqrt(R * R - dy * dy) : 0f;
            x0 = fan.x - half;
            x1 = fan.x + half;
        }

        // ================================================================================================ build

        /// <summary>Ring mesh kinds: the whole ring, the ring without the far flat, the far flat cut by the lane, a bore level.</summary>
        public enum Part { Full = 0, LaneBody = 1, LaneCut = 2, Bore = 3 }

        /// <summary>The parts that make up one level (the far-flat lane cut is shared by every lane level, rib or belt).</summary>
        public static int Parts(in Level L, Part[] result)
        {
            if (L.Bore)
            {
                result[0] = Part.Bore;
                if (!L.Lane) return 1;
                result[1] = Part.LaneCut;
                return 2;
            }
            if (L.Lane) { result[0] = Part.LaneBody; result[1] = Part.LaneCut; return 2; }
            result[0] = Part.Full;
            return 1;
        }

        /// <summary>Append the ring-local geometry of one part of a level to the builder (no Mesh: usable outside Unity).</summary>
        public static void Geometry(MeshBuilder mb, Part part, in Level L)
        {
            bool belt = L.Belt;
            switch (part)
            {
                case Part.Full:
                    for (int k = 0; k < 6; k++) FullSide(mb, belt, k, false, false);
                    break;
                case Part.LaneBody:
                    for (int k = 0; k < 6; k++) if (k != FAR_SIDE) FullSide(mb, belt, k, false, false);
                    break;
                case Part.LaneCut:
                {
                    // far side: t = (-1, 0), so u = -x. Piece A from corner 1 (x > 0) to the lane's +x edge, piece B from its -x edge to corner 2.
                    float uA = -(LaneX + LANE_HALF), uB = -(LaneX - LANE_HALF), reach = InnerApothem(belt) * Tan30;
                    Run(mb, belt, FAR_SIDE, End.MitreAt(FAR_SIDE, false), End.Cut(uA), SegmentsFor(belt, uA + reach));
                    Run(mb, belt, FAR_SIDE, End.Cut(uB), End.MitreAt(FAR_SIDE + 1, false), SegmentsFor(belt, reach - uB));
                    break;
                }
                case Part.Bore:
                    BuildBoreLevel(mb, L);
                    break;
            }
        }

        static string PartName(Part part, in Level L)
        {
            string kind = L.Belt ? "Belt" : "Rib";
            return part == Part.Bore ? "B2_" + kind + "Bore_" + L.K : "B2_" + kind + part;
        }

        /// <summary>
        /// Build every ring mesh of a tier and its placements. Shared parts (Full, LaneBody, LaneCut per rib / belt) are built
        /// once and instanced; bore levels get their own mesh. The meshes are readable (MeshValidation, tests); the caller owns
        /// them (destroy them with the beat).
        /// </summary>
        public static Set Build(bool everyOther, bool hoistLane)
        {
            var set = new Set();
            Levels(everyOther, hoistLane, set.Levels);
            var mb = new MeshBuilder();
            var shared = new Mesh[8];   // [part * 2 + belt] for Full, LaneBody, LaneCut
            var parts = new Part[2];

            for (int i = 0; i < set.Levels.Count; i++)
            {
                Level L = set.Levels[i];
                if (L.Belt) set.Belts++; else set.Ribs++;
                Matrix4x4 m = Matrix4x4.Translate(Centre(L.Y));
                int n = Parts(L, parts);
                for (int p = 0; p < n; p++)
                {
                    Part part = parts[p];
                    Mesh mesh;
                    if (part == Part.Bore)
                    {
                        Geometry(mb, part, L);
                        mesh = Finish(mb, PartName(part, L), set);
                    }
                    else
                    {
                        int slot = (int)part * 2 + (L.Belt ? 1 : 0);
                        mesh = shared[slot];
                        if (mesh == null)
                        {
                            Geometry(mb, part, L);
                            mesh = shared[slot] = Finish(mb, PartName(part, L), set);
                        }
                    }
                    set.Draws.Add(new Draw { Mesh = mesh, Matrix = m, Instanced = part != Part.Bore, K = L.K, Belt = L.Belt });
                }
            }
            // the full meshes exist even when no level uses them (ReportScale measures rib / belt proportions from them)
            for (int b = 0; b < 2; b++)
            {
                if (shared[b] != null) continue;
                var L = new Level { Belt = b == 1 };
                Geometry(mb, Part.Full, L);
                shared[b] = Finish(mb, PartName(Part.Full, L), set);
            }
            set.RibFull = shared[0];
            set.BeltFull = shared[1];
            return set;
        }

        static void BuildBoreLevel(MeshBuilder mb, Level L)
        {
            bool belt = L.Belt;
            float hh = 0.5f * Height(belt);
            float reach = InnerApothem(belt) * Tan30;
            BoreGap(L.Y, hh, out float x0, out float x1);
            x0 -= AbyssConst.SHAFT_CENTRE.x;       // ring-local; near side: t = (1, 0), so u = x
            x1 -= AbyssConst.SHAFT_CENTRE.x;
            bool dropA = x0 + reach < MIN_PIECE;   // piece from corner 4 to the gap
            bool dropB = reach - x1 < MIN_PIECE;   // piece from the gap to corner 5
            FullSide(mb, belt, 0, false, false);
            if (!L.Lane) FullSide(mb, belt, FAR_SIDE, false, false);
            FullSide(mb, belt, 2, false, false);
            // the sides next to the near flat (3 ends at corner 4, 5 starts at corner 5): plain or capped mitre, or a plane cut
            End endA = AdjacentEnd(NEAR_SIDE, x0, dropA), startB = AdjacentEnd(NEAR_SIDE + 1, x1, dropB);
            Run(mb, belt, NEAR_SIDE - 1, End.MitreAt(NEAR_SIDE - 1, false), endA, SegmentsFor(belt, InnerU(endA, NEAR_SIDE - 1, belt) + reach));
            Run(mb, belt, NEAR_SIDE + 1, startB, End.MitreAt(NEAR_SIDE + 2, false), SegmentsFor(belt, reach - InnerU(startB, NEAR_SIDE + 1, belt)));
            if (!dropA) Run(mb, belt, NEAR_SIDE, End.MitreAt(NEAR_SIDE, false), End.Cut(x0), SegmentsFor(belt, x0 + reach));
            if (!dropB) Run(mb, belt, NEAR_SIDE, End.Cut(x1), End.MitreAt(NEAR_SIDE + 1, false), SegmentsFor(belt, reach - x1));
        }

        /// <summary>Ring-local |x| of the corners 4 / 5 at the embedded outer edge of the profile (the wall corner).</summary>
        static float OuterCornerX => (AbyssConst.SHAFT_APOTHEM + EMBED) * Tan30;

        /// <summary>
        /// End of the side next to the near flat at corner 4 (gap edge x0, ring-local) or corner 5 (x1) of a bore level: the
        /// plain mitre while the near-flat piece exists; the capped mitre where that piece is dropped and every point of the cap
        /// inside the clear circle stands at least BORE_MARGIN in front of the rim face (k 24: 5.8 m); otherwise a cut on the
        /// plane x = x0 / x1, clamped to the wall corner so it stays on that side, which keeps the whole end outside the clear
        /// circle (k 25, whose gap overruns the shaft corner).
        /// </summary>
        static End AdjacentEnd(int corner, float xGap, bool dropped)
        {
            if (!dropped) return End.MitreAt(corner, false);
            // the cap lies on the corner bisector, ring-local z = -sqrt(3) |x|, from the inner corner to the wall; its points with
            // |x| < |xGap| are inside the clear circle (at the band edge nearest the fan centre), the lowest of them at |x| = |xGap|
            float zInside = AbyssConst.SHAFT_CENTRE.y - Sqrt3 * Mathf.Abs(xGap);
            if (zInside >= RIM_FRONT_Z + BORE_MARGIN) return End.MitreAt(corner, true);
            return End.OnPlaneX(corner == NEAR_SIDE ? Mathf.Min(xGap, -OuterCornerX) : Mathf.Max(xGap, OuterCornerX));
        }

        /// <summary>Position u along side k of an end at the inner apothem (for the segment count).</summary>
        static float InnerU(in End e, int k, bool belt)
        {
            SideFrame(k, out Vector3 n, out Vector3 t);
            return Vector3.Dot(EndPoint(e, n, t, InnerApothem(belt), 0f), t);
        }

        static Mesh Finish(MeshBuilder mb, string name, Set set)
        {
            Mesh m = mb.ToMesh(name);
            mb.Clear();
            set.Meshes.Add(m);
            return m;
        }

        // ================================================================================================ geometry

        const float Cos30 = 0.8660254037844386f;
        const float Tan30 = 0.5773502691896258f;
        const float Sqrt3 = 1.7320508075688772f;
        static readonly double[] s_cornerCos = Table(true), s_cornerSin = Table(false);

        static double[] Table(bool cos)
        {
            var t = new double[6];
            for (int i = 0; i < 6; i++) t[i] = cos ? System.Math.Cos(i * System.Math.PI / 3.0) : System.Math.Sin(i * System.Math.PI / 3.0);
            return t;
        }

        /// <summary>Ring-local corner point i (any integer) at apothem a: on the corner bisector, from one table (bit-identical everywhere).</summary>
        public static Vector3 Corner(int i, float a, float h)
        {
            int k = ((i % 6) + 6) % 6;
            double r = a / (double)Cos30;
            return new Vector3((float)(r * s_cornerCos[k]), h, (float)(r * s_cornerSin[k]));
        }

        /// <summary>Outward normal (x, z) of side k and its tangent t (toward corner k + 1).</summary>
        static void SideFrame(int k, out Vector3 n, out Vector3 t)
        {
            Vector2 n2 = MeshKit.HexFaceNormal(k);
            n = new Vector3(n2.x, 0f, n2.y);
            t = new Vector3(-n2.y, 0f, n2.x);
        }

        struct End
        {
            public bool Mitre;   // on the corner bisector (corner index)
            public bool PlaneX;  // on the vertical plane x = X (ring-local; the side's tangent must have an x component)
            public int Corner;
            public float U;      // else a perpendicular cut at U
            public float X;
            public bool Cap;     // close the end with the profile polygon

            public static End MitreAt(int corner, bool cap) => new End { Mitre = true, Corner = corner, Cap = cap };
            public static End Cut(float u) => new End { Mitre = false, U = u, Cap = true };
            public static End OnPlaneX(float x) => new End { PlaneX = true, X = x, Cap = true };
        }

        static readonly Vector2[] s_prof = new Vector2[8];
        static readonly float[] s_ao = new float[8];

        /// <summary>Profile (apothem, height) from the embedded top edge around the inner face to the embedded bottom edge.</summary>
        static int Profile(bool belt)
        {
            float aIn = InnerApothem(belt), aOut = AbyssConst.SHAFT_APOTHEM + EMBED, aAo = aOut - AO_STRIP;
            float hh = 0.5f * Height(belt), b = Bevel(belt), c = TopChamfer(belt);
            int n = 0;
            s_prof[n] = new Vector2(aOut, hh); s_ao[n++] = AO_WALL;
            s_prof[n] = new Vector2(aAo, hh); s_ao[n++] = 1f;
            s_prof[n] = new Vector2(aIn + c, hh); s_ao[n++] = 1f;
            s_prof[n] = new Vector2(aIn, hh - c); s_ao[n++] = 1f;
            s_prof[n] = new Vector2(aIn, -hh + b); s_ao[n++] = 1f;
            s_prof[n] = new Vector2(aIn + b, -hh); s_ao[n++] = 1f;
            s_prof[n] = new Vector2(aAo, -hh); s_ao[n++] = 1f;
            s_prof[n] = new Vector2(aOut, -hh); s_ao[n++] = AO_WALL;
            return n;
        }

        static int SegmentsFor(bool belt, float innerLength)
        {
            float full = 2f * InnerApothem(belt) * Tan30;
            return Mathf.Max(1, Mathf.RoundToInt(SIDE_SEGMENTS * innerLength / full));
        }

        static void FullSide(MeshBuilder mb, bool belt, int k, bool capStart, bool capEnd)
        {
            Run(mb, belt, k, End.MitreAt(k, capStart), End.MitreAt(k + 1, capEnd), SIDE_SEGMENTS);
        }

        static Vector3 EndPoint(in End e, Vector3 n, Vector3 t, float a, float h)
        {
            if (e.Mitre) return Corner(e.Corner, a, h);
            float u = e.PlaneX ? (e.X - n.x * a) / t.x : e.U;
            Vector3 p = n * a + t * u;
            p.y = h;
            return p;
        }

        /// <summary>One straight ring run along side k between two ends, in ring-local coordinates.</summary>
        static void Run(MeshBuilder mb, bool belt, int k, End e0, End e1, int segs)
        {
            SideFrame(k, out Vector3 n, out Vector3 t);
            int pc = Profile(belt);
            int edges = pc - 1;
            Color32 saved = mb.Color;
            MeshKit.Reserve(mb, edges * 2 * (segs + 1) + 2 * (pc + 1), edges * segs * 6 + 2 * pc * 3);

            for (int e = 0; e < edges; e++)
            {
                Vector2 p = s_prof[e], q = s_prof[e + 1];
                Vector2 d = q - p;
                Vector2 n2 = new Vector2(d.y, -d.x).normalized;           // outward in (apothem, height)
                Vector3 nw = n * n2.x + Vector3.up * n2.y;
                // edge wear on the small bevels only (the 2-4 m top chamfer is a face of its own)
                bool bevel = Mathf.Abs(d.x) > 1e-6f && Mathf.Abs(d.y) > 1e-6f && d.magnitude < 0.5f;
                bool vertical = Mathf.Abs(n2.y) < 0.7f;
                Vector3 p0 = EndPoint(e0, n, t, p.x, p.y), p1 = EndPoint(e1, n, t, p.x, p.y);
                Vector3 q0 = EndPoint(e0, n, t, q.x, q.y), q1 = EndPoint(e1, n, t, q.x, q.y);
                byte aoP = (byte)Mathf.RoundToInt(255f * s_ao[e]), aoQ = (byte)Mathf.RoundToInt(255f * s_ao[e + 1]);
                byte wear = bevel ? (byte)255 : (byte)0;
                byte chamfer = Mathf.Abs(d.x) > 1f && Mathf.Abs(d.y) > 1f ? (byte)255 : (byte)0;   // the 2 / 4 m top chamfer
                int first = mb.VertexCount;
                for (int s = 0; s <= segs; s++)
                {
                    float f = (float)s / segs;
                    Vector3 a = s == 0 ? p0 : (s == segs ? p1 : Vector3.Lerp(p0, p1, f));
                    Vector3 b = s == 0 ? q0 : (s == segs ? q1 : Vector3.Lerp(q0, q1, f));
                    mb.Color = new Color32(aoP, wear, chamfer, 255);
                    mb.Vertex(a, nw, vertical ? new Vector2(Vector3.Dot(a, t), a.y) : new Vector2(a.x, a.z));
                    mb.Color = new Color32(aoQ, wear, chamfer, 255);
                    mb.Vertex(b, nw, vertical ? new Vector2(Vector3.Dot(b, t), b.y) : new Vector2(b.x, b.z));
                }
                for (int s = 0; s < segs; s++)
                {
                    int i0 = first + 2 * s;
                    BeatKit.Tri(mb, i0, i0 + 1, i0 + 3);
                    BeatKit.Tri(mb, i0, i0 + 3, i0 + 2);
                }
            }
            mb.Color = saved;

            if (e0.Cap) Cap(mb, e0, n, t, pc, EndPoint(e1, n, t, s_prof[2].x, 0f));
            if (e1.Cap) Cap(mb, e1, n, t, pc, EndPoint(e0, n, t, s_prof[2].x, 0f));
            mb.Color = saved;
        }

        /// <summary>Close a run end with its profile polygon (fan from the centroid), facing away from the run.</summary>
        static void Cap(MeshBuilder mb, in End e, Vector3 n, Vector3 t, int pc, Vector3 otherEnd)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < pc; i++) c += EndPoint(e, n, t, s_prof[i].x, s_prof[i].y);
            c /= pc;
            Vector3 a = EndPoint(e, n, t, s_prof[0].x, s_prof[0].y), b = EndPoint(e, n, t, s_prof[3].x, s_prof[3].y);
            Vector3 nc = Vector3.Cross(b - a, Vector3.up).normalized;
            if (Vector3.Dot(nc, c - otherEnd) < 0f) nc = -nc;
            mb.Color = new Color32(255, 0, 0, 255);
            int ci = mb.Vertex(c, nc, new Vector2(Vector3.Dot(c, n), c.y));
            int first = mb.VertexCount;
            for (int i = 0; i < pc; i++)
            {
                Vector3 p = EndPoint(e, n, t, s_prof[i].x, s_prof[i].y);
                mb.Vertex(p, nc, new Vector2(Vector3.Dot(p, n), p.y));
            }
            for (int i = 0; i < pc; i++) BeatKit.Tri(mb, ci, first + i, first + (i + 1) % pc);
        }

        // ================================================================================================ markers

        /// <summary>World position of the corner marker light of corner i at a level (on the bisector, inside the inner corner).</summary>
        public static Vector3 MarkerPosition(in Level L, int corner)
        {
            Vector3 p = Corner(corner, InnerApothem(L.Belt), 0f);
            float r = new Vector2(p.x, p.z).magnitude;
            float s = (r - MARKER_INSET) / r;
            return Centre(L.Y) + new Vector3(p.x * s, 0f, p.z * s);
        }
    }
}
