using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Megastructure-specific generators: hexagonal rings, walls and tubes (shaft and honeycomb, flats perpendicular to Z),
    /// the helical ledge (beat 3) and the Warren truss (bridge, gantry).
    /// </summary>
    public static partial class MeshKit
    {
        static readonly List<Vector2> s_rp = new List<Vector2>(8);
        // Corner direction tables (computed in double once, so shared corners are bit-identical everywhere).
        static readonly float[] s_hexCos = HexTable(true);
        static readonly float[] s_hexSin = HexTable(false);

        static float[] HexTable(bool cosine)
        {
            var t = new float[6];
            for (int i = 0; i < 6; i++)
            {
                double a = i * System.Math.PI / 3.0;
                t[i] = (float)(cosine ? System.Math.Cos(a) : System.Math.Sin(a));
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------ hexagon basics

        /// <summary>
        /// Corner i (any integer, taken mod 6) of a hexagon with the shaft orientation: flats perpendicular to Z, face normals at
        /// 30, 90, ..., 330 degrees from +X toward +Z (AbyssConst.SHAFT_FACE_NORMAL_DEG), corners at 0, 60, ..., 300 degrees and
        /// circumradius apothem / cos 30. Face k spans corners k and k + 1. Every generator takes corners from here, so faces that
        /// share a corner share it bit for bit.
        /// </summary>
        public static Vector2 HexCorner(Vector2 centreXZ, float apothem, int i)
        {
            int k = ((i % 6) + 6) % 6;
            float r = apothem / Cos30;
            return new Vector2(centreXZ.x + r * s_hexCos[k], centreXZ.y + r * s_hexSin[k]);
        }

        /// <summary>Outward unit normal (x, z) of hexagon face k (angle 30 + 60 k degrees from +X toward +Z).</summary>
        public static Vector2 HexFaceNormal(int faceIndex)
        {
            int k = ((faceIndex % 6) + 6) % 6;
            float a = (30f + 60f * k) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>"Hexagonal radius" of a point: max over the six face normals of dot(p - centre, n). Equals the apothem on the hexagon.</summary>
        public static float HexApothemOf(Vector2 centreXZ, Vector2 pXZ)
        {
            Vector2 d = pXZ - centreXZ;
            float m = float.NegativeInfinity;
            for (int k = 0; k < 6; k++)
            {
                float v = Vector2.Dot(d, HexFaceNormal(k));
                if (v > m) m = v;
            }
            return m;
        }

        /// <summary>
        /// One flat face (index 0..5) of a vertical hexagonal prism from y0 to y1, subdivided for large walls (shaft shell chunks,
        /// the Face, honeycomb tubes). inward = true faces the hexagon centre (seen from inside the shaft or hole).
        /// UV0 = (metres along the face, metres up from y0). For chunk-local vertices push a translation of -chunkOrigin on the
        /// builder first (the GameObject carries the origin, CONTRACTS 6).
        /// </summary>
        public static void HexWallPanel(MeshBuilder mb, Vector2 centreXZ, float apothem, int faceIndex, float y0, float y1,
            int divisionsAlong = 1, int divisionsUp = 1, bool inward = true)
        {
            Vector2 c0 = HexCorner(centreXZ, apothem, faceIndex), c1 = HexCorner(centreXZ, apothem, faceIndex + 1);
            Vector3 p0 = new Vector3(c0.x, y0, c0.y), p1 = new Vector3(c1.x, y0, c1.y);
            Vector3 origin = inward ? p1 : p0;
            Vector3 u = inward ? p0 - p1 : p1 - p0;
            float len = u.magnitude;
            if (len < 1e-6f || Mathf.Abs(y1 - y0) < 1e-6f) return;
            if (y1 < y0) { origin.y = y1; }
            GridQuad(mb, origin, u / len, Vector3.up, len, Mathf.Abs(y1 - y0), divisionsAlong, divisionsUp);
        }

        /// <summary>
        /// Flat horizontal hexagonal annulus between two apothems (web tops around honeycomb holes: inner = hole apothem, outer =
        /// half the lattice pitch, so neighbouring cells tile exactly). UV0 planar (x, z).
        /// </summary>
        public static void HexAnnulus(MeshBuilder mb, Vector2 centreXZ, float y, float innerApothem, float outerApothem, bool faceUp = true)
        {
            float a0 = Mathf.Min(innerApothem, outerApothem), a1 = Mathf.Max(innerApothem, outerApothem);
            if (a1 - a0 < 1e-6f) return;
            Vector3 n = faceUp ? Vector3.up : Vector3.down;
            Reserve(mb, 12, 36);
            int first = mb.VertexCount;
            for (int i = 0; i < 6; i++)
            {
                Vector2 ci = HexCorner(centreXZ, a0, i), co = HexCorner(centreXZ, a1, i);
                mb.Vertex(new Vector3(ci.x, y, ci.y), n, ci);
                mb.Vertex(new Vector3(co.x, y, co.y), n, co);
            }
            for (int i = 0; i < 6; i++)
            {
                int a = first + 2 * i, b = first + 2 * ((i + 1) % 6);
                Quad(mb, a, a + 1, b + 1, b);
            }
        }

        // ------------------------------------------------------------------------------------------ MitredRing

        /// <summary>
        /// One mitred hexagonal ring (shaft ribs and belts, 14.5.2; honeycomb tube ribs): the ring runs around the inside of a
        /// hexagonal wall, from <paramref name="innerApothem"/> out to innerApothem + <paramref name="protrusion"/> (the wall
        /// surface), vertical extent <paramref name="height"/> centred on <paramref name="y"/>. Orientation as the shaft (flats
        /// perpendicular to Z, face normals 30, 90, ..., 330 degrees), the six sides mitred exactly on the 120 degree corner
        /// bisectors. Faces: top (up), bottom (down), inner face (toward the axis, visible from inside the shaft) and 45 degree
        /// bevels of <paramref name="bevel"/> on the two inner edges (edge wear G = 255). All facets flat-shaded.
        /// The outer face against the wall is omitted by default (never visible); <paramref name="outerFace"/> closes the solid.
        /// <paramref name="embed"/> extends the ring further outward (hidden inside the wall) to avoid a hairline at the wall.
        /// Rib: innerApothem 344, protrusion 6, height 6, bevel 0.03. Belt: 330, 20, 12, 0.05.
        /// UV0: vertical faces (metres along the side, y), horizontal and bevel faces planar (x, z).
        /// </summary>
        public static void MitredRing(MeshBuilder mb, Vector2 centreXZ, float y, float innerApothem, float protrusion, float height,
            float bevel, bool outerFace = false, float embed = 0f, int segmentsPerSide = 1)
        {
            if (protrusion <= 1e-5f || height <= 1e-5f || innerApothem <= 0f) return;
            float aIn = innerApothem, aOut = innerApothem + protrusion + Mathf.Max(0f, embed);
            float hh = 0.5f * height;
            float b = Mathf.Clamp(bevel, 0f, 0.45f * Mathf.Min(protrusion, height));
            int segs = Mathf.Max(1, segmentsPerSide);

            // profile in (apothem coordinate s, height) - counter-clockwise, region on the left: outward normal (dy, -ds)
            s_rp.Clear();
            if (outerFace) s_rp.Add(new Vector2(aOut, -hh));
            s_rp.Add(new Vector2(aOut, hh));
            if (b > 0f)
            {
                s_rp.Add(new Vector2(aIn + b, hh));
                s_rp.Add(new Vector2(aIn, hh - b));
                s_rp.Add(new Vector2(aIn, -hh + b));
                s_rp.Add(new Vector2(aIn + b, -hh));
            }
            else
            {
                s_rp.Add(new Vector2(aIn, hh));
                s_rp.Add(new Vector2(aIn, -hh));
            }
            if (!outerFace) s_rp.Add(new Vector2(aOut, -hh));
            int pc = s_rp.Count;
            int edges = outerFace ? pc : pc - 1;

            Reserve(mb, 6 * edges * 2 * (segs + 1), 6 * edges * segs * 6);
            Color32 saved = mb.Color;
            Vector3 c3 = new Vector3(centreXZ.x, 0f, centreXZ.y);
            for (int k = 0; k < 6; k++)
            {
                Vector2 fn2 = HexFaceNormal(k);
                Vector3 fn = new Vector3(fn2.x, 0f, fn2.y);
                Vector3 d0 = new Vector3(s_hexCos[k], 0f, s_hexSin[k]) / Cos30;
                Vector3 d1 = new Vector3(s_hexCos[(k + 1) % 6], 0f, s_hexSin[(k + 1) % 6]) / Cos30;
                Vector3 along = (d1 - d0).normalized;
                for (int e = 0; e < edges; e++)
                {
                    Vector2 p = s_rp[e], q = s_rp[(e + 1) % pc];
                    Vector2 d = q - p;
                    Vector2 n2 = new Vector2(d.y, -d.x).normalized;
                    Vector3 n = fn * n2.x + Vector3.up * n2.y;
                    bool isBevel = Mathf.Abs(d.x) > 1e-6f && Mathf.Abs(d.y) > 1e-6f;
                    bool vertical = Mathf.Abs(n2.y) < 0.7f;
                    mb.Color = isBevel ? WithWear(saved, WearFull) : saved;
                    int first = mb.VertexCount;
                    for (int t = 0; t <= segs; t++)
                    {
                        float f = (float)t / segs;
                        Vector3 dir = Vector3.Lerp(d0, d1, f);
                        for (int w = 0; w < 2; w++)
                        {
                            Vector2 pr = w == 0 ? p : q;
                            Vector3 pos = c3 + dir * pr.x + new Vector3(0f, y + pr.y, 0f);
                            Vector2 uv = vertical ? new Vector2(Vector3.Dot(pos - c3, along), pos.y) : new Vector2(pos.x, pos.z);
                            mb.Vertex(pos, n, uv);
                        }
                    }
                    for (int t = 0; t < segs; t++)
                    {
                        int a = first + 2 * t;
                        Quad(mb, a, a + 1, a + 3, a + 2);
                    }
                }
            }
            mb.Color = saved;
        }

        // ------------------------------------------------------------------------------------------ HexTube

        /// <summary>
        /// Honeycomb hole (14.5.7 tier 0): six inward-facing walls of a hexagonal shaft of the given apothem from yTop down
        /// <paramref name="depth"/> metres (no cap: the bottom is lost in fog), subdivided into rows (rowsPerRib per rib spacing)
        /// so the HoneyTube depth darkening has vertices to work with, plus mitred ribs protruding inward. Ribs sit at depths
        /// ribOffset + k * ribSpacing (ribOffset &lt; 0 means half a spacing: 30, 90, ..., 570 for 600 m / 60 m = 10 rings) while
        /// the whole rib stays inside the tube. wallThickness &gt; 0 adds the outer faces and a flat top lip (a free-standing tube).
        /// Returns the number of ribs.
        /// </summary>
        public static int HexTube(MeshBuilder mb, Vector2 centreXZ, float apothem, float yTop, float depth, float wallThickness = 0f,
            float ribSpacing = 60f, float ribProtrusion = 1.5f, float ribHeight = 2f, float ribBevel = 0.05f, int rowsPerRib = 2,
            int divisionsAlong = 1, float ribOffset = -1f)
        {
            if (apothem <= 0f || depth <= 1e-4f) return 0;
            float yBot = yTop - depth;
            int rows = ribSpacing > 0f ? Mathf.Max(1, Mathf.CeilToInt(depth / ribSpacing * Mathf.Max(1, rowsPerRib))) : Mathf.Max(1, rowsPerRib);
            for (int k = 0; k < 6; k++) HexWallPanel(mb, centreXZ, apothem, k, yBot, yTop, divisionsAlong, rows, true);
            if (wallThickness > 1e-4f)
            {
                float ao = apothem + wallThickness;
                for (int k = 0; k < 6; k++) HexWallPanel(mb, centreXZ, ao, k, yBot, yTop, divisionsAlong, rows, false);
                HexAnnulus(mb, centreXZ, yTop, apothem, ao, true);
            }
            int ribs = 0;
            if (ribSpacing > 0f && ribProtrusion > 0f && ribHeight > 0f)
            {
                float off = ribOffset < 0f ? 0.5f * ribSpacing : ribOffset;
                for (float dRib = off; dRib + 0.5f * ribHeight <= depth + 1e-3f; dRib += ribSpacing)
                {
                    if (dRib - 0.5f * ribHeight < -1e-3f) continue;
                    MitredRing(mb, centreXZ, yTop - dRib, apothem - ribProtrusion, ribProtrusion, ribHeight, ribBevel);
                    ribs++;
                }
            }
            return ribs;
        }

        // ------------------------------------------------------------------------------------------ HelicalAnnulus

        /// <summary>
        /// Helical ramp slab (beat 3 ledge): ring sector between rIn and rOut around the vertical axis through centreXZ, from angle
        /// phi0 to phi1 (radians, from +X toward +Z), top surface height falling/rising linearly from y0 (at phi0) to y1 (at phi1),
        /// slab <paramref name="thickness"/> below the top. Surfaces: top (exact helicoid normal), bottom, inner wall (facing the
        /// axis), outer wall (facing out), flat end caps. The ledge alpha of 14.5.3 (alpha 0 at -Z from C1, pi/2 at +X) maps to
        /// phi = alpha - pi/2. UV0: top and bottom planar (x, z); walls (r * phi, y); caps (r, y).
        /// </summary>
        public static void HelicalAnnulus(MeshBuilder mb, Vector2 centreXZ, float rIn, float rOut, float phi0, float phi1, float y0,
            float y1, int segments, float thickness, bool capEnds = true, bool innerWall = true, bool outerWall = true,
            int radialDivisions = 1)
        {
            int seg = Mathf.Max(1, segments), rd = Mathf.Max(1, radialDivisions);
            float r0 = Mathf.Max(0f, Mathf.Min(rIn, rOut)), r1 = Mathf.Max(rIn, rOut);
            float dphi = phi1 - phi0;
            if (r1 - r0 < 1e-6f || Mathf.Abs(dphi) < 1e-6f) return;
            float th = Mathf.Max(0f, thickness);
            float slope = (y1 - y0) / dphi;
            Vector3 c3 = new Vector3(centreXZ.x, 0f, centreXZ.y);
            int row = rd + 1;
            Reserve(mb, 2 * (seg + 1) * row + 4 * (seg + 1) + 8, 2 * seg * rd * 6 + 4 * seg * 6 + 12);

            for (int surf = 0; surf < 2; surf++)
            {
                bool top = surf == 0;
                if (!top && th <= 1e-6f) break;
                int first = mb.VertexCount;
                for (int k = 0; k <= seg; k++)
                {
                    float f = (float)k / seg;
                    float ph = phi0 + dphi * f;
                    float cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
                    float yy = Mathf.Lerp(y0, y1, f) - (top ? 0f : th);
                    for (int j = 0; j <= rd; j++)
                    {
                        float r = Mathf.Lerp(r0, r1, (float)j / rd);
                        Vector3 n = SafeNormalize(new Vector3(slope * sp, r, -slope * cp), Vector3.up);
                        if (!top) n = -n;
                        Vector3 p = c3 + new Vector3(r * cp, yy, r * sp);
                        mb.Vertex(p, n, new Vector2(p.x, p.z));
                    }
                }
                for (int k = 0; k < seg; k++)
                for (int j = 0; j < rd; j++)
                {
                    int a = first + k * row + j;
                    Quad(mb, a, a + 1, a + row + 1, a + row);
                }
            }

            if (th > 1e-6f)
            {
                for (int wall = 0; wall < 2; wall++)
                {
                    bool inner = wall == 0;
                    if (inner ? !innerWall : !outerWall) continue;
                    if (inner && r0 <= 1e-6f) continue;
                    float r = inner ? r0 : r1, sg = inner ? -1f : 1f;
                    int first = mb.VertexCount;
                    for (int k = 0; k <= seg; k++)
                    {
                        float f = (float)k / seg;
                        float ph = phi0 + dphi * f;
                        float cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
                        float yt = Mathf.Lerp(y0, y1, f);
                        Vector3 n = new Vector3(cp * sg, 0f, sp * sg);
                        mb.Vertex(c3 + new Vector3(r * cp, yt, r * sp), n, new Vector2(r * ph, yt));
                        mb.Vertex(c3 + new Vector3(r * cp, yt - th, r * sp), n, new Vector2(r * ph, yt - th));
                    }
                    for (int k = 0; k < seg; k++)
                    {
                        int a = first + 2 * k;
                        Quad(mb, a, a + 1, a + 3, a + 2);
                    }
                }

                if (capEnds)
                {
                    float dirSign = dphi > 0f ? 1f : -1f;
                    for (int end = 0; end < 2; end++)
                    {
                        float ph = end == 0 ? phi0 : phi1;
                        float yt = end == 0 ? y0 : y1;
                        float cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
                        Vector3 n = new Vector3(-sp, 0f, cp) * (dirSign * (end == 0 ? -1f : 1f));
                        int a = mb.Vertex(c3 + new Vector3(r0 * cp, yt, r0 * sp), n, new Vector2(r0, yt));
                        int b = mb.Vertex(c3 + new Vector3(r1 * cp, yt, r1 * sp), n, new Vector2(r1, yt));
                        int c = mb.Vertex(c3 + new Vector3(r1 * cp, yt - th, r1 * sp), n, new Vector2(r1, yt - th));
                        int d = mb.Vertex(c3 + new Vector3(r0 * cp, yt - th, r0 * sp), n, new Vector2(r0, yt - th));
                        Quad(mb, a, b, c, d);
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------ WarrenTruss

        // Section proportions as fractions of memberSize (height = in the truss plane, width = flange width across the plane).
        // Chords carry the full section. The diagonals of even bays (family A) and odd bays (family B) meet at every node, so
        // the two families differ from each other and from the chords: lower (0.9), narrower flanges (0.8 / 0.7) and thinner
        // webs (0.04 / 0.03 against 0.08). No diagonal face is then coplanar with a chord face or with the other family where
        // the members overlap at a node (14.7.5 / CONTRACTS 2: no co-planar faces); the near-coplanar webs that remain inside
        // a node are buried in its gusset plate.
        const float TrussChordWeb = 0.08f, TrussChordFlangeT = 0.1f;
        const float TrussDiagHeight = 0.9f, TrussDiagFlangeT = 0.08f;
        const float TrussDiagWidthA = 0.8f, TrussDiagWidthB = 0.7f;
        const float TrussDiagWebA = 0.04f, TrussDiagWebB = 0.03f;

        /// <summary>
        /// Planar Warren truss hanging below the top chord start -> end (bridge 14.5.2: 6 m deep, 25 bays, 0.4 m I-section members;
        /// gantry 14.5.6: 4 m, 33 bays, 0.3 m). The truss plane contains the chord and <paramref name="up"/>. Members per bay: one
        /// top chord segment, one bottom chord segment and one diagonal zig-zagging (T0-B1, B1-T2, T2-B3, ...), i.e. 3 x bays members
        /// (75 for 25 bays), each authored at final length between node centres and closed; returns that member count.
        /// Chords use the full memberSize section; diagonals are inset (0.9 high, flanges 0.8 / 0.7 wide, thinner webs,
        /// alternating per bay) so overlapping members never share a face plane.
        /// <paramref name="gussets"/> adds one gusset plate per diagonal node (bays + 1 plates: T0, B1, T2, ...; 44 triangles each):
        /// a chamfered plate in the truss plane, a little thicker than the chord web, reaching from inside the chord's far flange
        /// past the chord toward the diagonals; it hides the node overlaps of the webs. All parts are 44-triangle closed solids.
        /// With <paramref name="thinLine"/> every part writes ThinLine UV1 / UV2 (gussets use their chord's axis and radius, so
        /// they widen together with the chord).
        /// </summary>
        public static int WarrenTruss(MeshBuilder mb, Vector3 start, Vector3 end, float depth, int bays, float memberSize, Vector3 up,
            TrussMemberShape shape = TrussMemberShape.IBeam, bool thinLine = false, bool gussets = true)
        {
            Vector3 axis = end - start;
            float len = axis.magnitude;
            int nb = Mathf.Max(1, bays);
            if (len < 1e-4f || depth <= 0f || memberSize <= 0f) return 0;
            float s = memberSize;
            Vector3 dir = axis / len;
            Vector3 down = -PerpendicularUp(dir, up);
            Vector3 planeNormal = Vector3.Cross(dir, down).normalized;
            Vector3 drop = down * depth;
            float diagH = TrussDiagHeight * s, diagT = TrussDiagFlangeT * s;
            int count = 0;
            for (int i = 0; i < nb; i++)
            {
                Vector3 t0 = start + axis * ((float)i / nb), t1 = start + axis * ((float)(i + 1) / nb);
                Vector3 b0 = t0 + drop, b1 = t1 + drop;
                TrussMember(mb, t0, t1, s, s, TrussChordWeb * s, TrussChordFlangeT * s, planeNormal, shape, thinLine);
                TrussMember(mb, b0, b1, s, s, TrussChordWeb * s, TrussChordFlangeT * s, planeNormal, shape, thinLine);
                if ((i & 1) == 0)
                    TrussMember(mb, t0, b1, diagH, TrussDiagWidthA * s, TrussDiagWebA * s, diagT, planeNormal, shape, thinLine);
                else
                    TrussMember(mb, b0, t1, diagH, TrussDiagWidthB * s, TrussDiagWebB * s, diagT, planeNormal, shape, thinLine);
                count += 3;
            }
            if (gussets) TrussGussets(mb, start, axis, len, nb, depth, s, dir, down, thinLine);
            return count;
        }

        static void TrussMember(MeshBuilder mb, Vector3 a, Vector3 b, float height, float width, float web, float flangeT,
            Vector3 planeNormal, TrussMemberShape shape, bool thinLine)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            Vector3 dir = d / len;
            Vector3 sectionUp = Vector3.Cross(planeNormal, dir); // in the truss plane, perpendicular to the member
            if (shape == TrussMemberShape.IBeam)
                IBeam(mb, a, b, height, width, web, flangeT, sectionUp, thinLine);
            else
                ChamferMember(mb, a, b, width, height, Mathf.Min(0.03f, 0.08f * Mathf.Min(width, height)), sectionUp, thinLine);
        }

        /// <summary>
        /// Gusset plates at the diagonal nodes (node k sits on the top chord for even k, on the bottom chord for odd k). Each plate
        /// lies in the truss plane, centred on it, aligned with the chord: thickness = chord web + 2 x max(6 mm, 0.02 s); across
        /// the chord from 0.45 s on the far side of the chord axis (buried in the far flange) to the reach toward the diagonals
        /// (at least 0.5 s past the chord face, and past the end of the diagonal-web overlap h_web / cos(theta)); along the chord
        /// far enough to contain both diagonals' sections at that reach, at most 0.8 bay, and kept 0.25 s inside the truss ends.
        /// Every plate face is offset from the chord and diagonal faces it is parallel to (>= 6 mm for the 0.3-0.4 m members).
        /// </summary>
        static void TrussGussets(MeshBuilder mb, Vector3 start, Vector3 axis, float len, int nb, float depth, float s, Vector3 dir,
            Vector3 down, bool thinLine)
        {
            float bay = len / nb;
            float hyp = Mathf.Sqrt(bay * bay + depth * depth);
            float cosT = Mathf.Max(bay / hyp, 0.05f), sinT = Mathf.Max(depth / hyp, 0.05f); // diagonal angle to the chord
            float chordHalf = 0.5f * s;
            float diagHalf = 0.5f * TrussDiagHeight * s;
            float diagWebHalf = diagHalf - TrussDiagFlangeT * s;
            float reach = Mathf.Max(chordHalf + 0.5f * s, diagWebHalf / cosT + 0.1f * s);
            reach = Mathf.Min(reach, 0.45f * depth);                   // plates of the two chords never meet
            if (reach < chordHalf + 0.05f * s) return;                 // truss too shallow for plates
            float back = chordHalf - 0.5f * TrussChordFlangeT * s;     // inside the far flange (0.05 s from both its faces)
            float half = Mathf.Min((reach * cosT + diagHalf) / sinT + 0.1f * s, 0.8f * bay);
            float endInset = 0.25f * s;
            float thick = TrussChordWeb * s + 2f * Mathf.Max(0.006f, 0.02f * s);
            float bevel = Mathf.Min(0.01f, 0.125f * thick);
            Vector3 drop = down * depth;
            for (int k = 0; k <= nb; k++)
            {
                bool top = (k & 1) == 0;
                Vector3 node = start + axis * ((float)k / nb);
                if (!top) node += drop;
                Vector3 inward = top ? down : -down;                   // toward the other chord
                float uMin = k > 0 ? -half : endInset, uMax = k < nb ? half : -endInset;
                if (uMax - uMin < 0.1f * s) continue;
                Vector3 centre = node + dir * (0.5f * (uMin + uMax)) + inward * (0.5f * (reach - back));
                var tl = BeginThinLine(mb, thinLine, dir, chordHalf, node);
                ChamferBox(mb, centre, new Vector3(thick, reach + back, uMax - uMin), bevel, Quaternion.LookRotation(dir, inward));
                EndThinLine(mb, tl);
            }
        }
    }
}
