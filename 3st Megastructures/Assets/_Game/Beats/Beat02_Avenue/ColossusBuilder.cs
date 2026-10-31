// ColossusBuilder: turns a ColossusShape into meshes (MASTER_PROMPT 14.5.2 a-f, 8.7).
// Renderers per colossus (see Beat02_Avenue):
//   Body   : robe + hood with its brim + sleeves with closed cloth cuffs (robe colour), the hood recess walls (INK, the
//            only INK on the figure), blank face plate (SAND_SHADE). Casts no shadow maps (a giant, Part 8.5); bounds
//            padded for the 5 cm hem sway.
//   Hem    : the 6 m SAND hem band with the real doorway hole. Casts no shadow maps either; a hidden
//            HemShadowProxy (the band inset by 0.15 m, shadows only) shades the door ledge and bollards on the
//            shade side without any acne on the visible band.
//   Foot   : 3-step ledge (Stone), bollard pair (Bollard), boxed INK alcove behind the doorway, light raised door frame
//            (DoorFrame). Casts shadows.
//   Shadow : the flat ground shadow (SaltShadow material, alpha = vertex A), 0.03 m above the salt.
// Vertex colour: R = AO 0.55..1, G = height shade (1 at the base -> 0 at the top), B = sway weight, A = 1.
// All positions are relative to the colossus root at (Cx, 0, Cz); the yaw is baked into the vertices.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    public sealed class ColossusGeometry
    {
        public ColossusShape Shape;
        public ColossusShadowShape ShadowShape;
        public string Name;
        public Vector3 RootPosition;

        public readonly MeshBuilder Robe = new MeshBuilder(6000), Ink = new MeshBuilder(400), Face = new MeshBuilder(300);
        public readonly MeshBuilder Hem = new MeshBuilder(400), HemProxy = new MeshBuilder(400);
        public readonly MeshBuilder Stone = new MeshBuilder(300), Bollard = new MeshBuilder(400), AlcoveInk = new MeshBuilder(32);
        /// <summary>Light raised frame around the doorway (its own submesh of the Foot mesh).</summary>
        public readonly MeshBuilder DoorFrame = new MeshBuilder(40);
        public readonly MeshBuilder Shadow = new MeshBuilder(800);

        /// <summary>Lathe seam pairs (column 0, duplicated copy) per builder, for T-NORMALS.</summary>
        public readonly List<Vector2Int> RobeSeams = new List<Vector2Int>(), HemSeams = new List<Vector2Int>(), BollardSeams = new List<Vector2Int>();
        /// <summary>Open edges expected on a position-welded graph (sunk hem bottom, crease rings, the doorway).</summary>
        public int BodyOpenEdges, HemOpenEdges;
        /// <summary>Highest vertex of the robe + hood (world y).</summary>
        public float TopY;
        /// <summary>Step boxes: (bottom y, top y) per step, world.</summary>
        public readonly List<Vector2> StepHeights = new List<Vector2>();

        public const float SwayPadding = 0.1f;

        public int ColossusTriangles => Robe.TriangleCount + Ink.TriangleCount + Face.TriangleCount + Hem.TriangleCount
                                        + Stone.TriangleCount + Bollard.TriangleCount + AlcoveInk.TriangleCount + DoorFrame.TriangleCount;

        public Mesh CreateBodyMesh() => AvenueMesh.ToMesh(Name + "_Body", SwayPadding, Robe, Ink, Face);
        public Mesh CreateHemMesh() => AvenueMesh.ToMesh(Name + "_Hem", SwayPadding, Hem);
        public Mesh CreateHemProxyMesh() => AvenueMesh.ToMesh(Name + "_HemShadowProxy", SwayPadding, HemProxy);
        public Mesh CreateFootMesh() => AvenueMesh.ToMesh(Name + "_Foot", 0f, Stone, Bollard, AlcoveInk, DoorFrame);
        public Mesh CreateShadowMesh() => AvenueMesh.ToMesh(Name + "_GroundShadow", 0f, Shadow);
    }

    public static class ColossusBuilder
    {
        // shaping constants of the baked vertex AO (design values; no Part 14 numbers)
        const double AoGround = 0.62, AoGroundHeight = 4.0, AoFlute = 0.30, AoSleeve = 0.28, AoNeck = 0.80, AoHoodNeck = 0.72;
        const double AoMin = 0.55;
        const double ProxyInset = 0.15;
        const int BollardSegments = 16;

        static readonly Vector3 Up = new Vector3(0, 1, 0);

        public static ColossusGeometry Generate(int pair, int side) => Generate(new ColossusShape(pair, side));

        /// <summary>The spec's ColossusBuilder(seed, side) at a given avenue pair.</summary>
        public static ColossusGeometry Generate(uint seed, int side, int pair) => Generate(new ColossusShape(pair, side, seed));

        public static ColossusGeometry Generate(ColossusShape s)
        {
            var g = new ColossusGeometry
            {
                Shape = s,
                Name = $"Colossus_{(s.Side == 0 ? "L" : "R")}{s.Pair + 1}",
                RootPosition = AvenueMesh.V(s.RootPosition),
            };
            int cols = ColossusSpec.Segments;

            // ------------------------------------------------------------ hem band (run 0) with the doorway hole
            double sill = s.Door.Sill, lintel = s.Door.Lintel;
            double[] hemEta = { -ThreadConstants.HemSink, 0.0, sill, lintel, ThreadConstants.HemBand };
            var hem = new Grid(hemEta.Length, cols);
            for (int i = 0; i < hem.Rings; i++)
            {
                double y = s.Y(hemEta[i]);
                hem.Axis[i] = new Vector3(0, (float)y, 0);
                hem.V[i] = (float)y;
                for (int j = 0; j < cols; j++)
                {
                    double th = s.ColumnTheta(j);
                    hem.P[hem.Id(i, j)] = AvenueMesh.V(s.LocalToRoot(s.RobeLocal(th, hemEta[i], false, 0)));
                    hem.C[hem.Id(i, j)] = RobeColour(s, th, y);
                }
            }
            hem.ComputeNormals();
            int jd = ColossusSpec.DoorColumn;
            Func<int, int, bool> doorSkip = (i, j) => j == jd && i >= 1 && i <= 3;
            int[] hemIdx = hem.Emit(g.Hem, doorSkip, g.HemSeams);
            AddDoorCells(g, s, hem, hemIdx);
            g.HemOpenEdges = 2 * cols + 4;

            // shadow-only proxy: the band inset by 0.15 m, no hole, no sway
            var proxy = new Grid(hem.Rings, cols);
            for (int k = 0; k < hem.P.Length; k++)
            {
                Vector3 n = hem.N[k];
                var nh = new Vector3(n.x, 0, n.z);
                proxy.P[k] = hem.P[k] - nh.normalized * (float)ProxyInset;
                proxy.N[k] = n;
                proxy.C[k] = new Color(1f, 1f, 0f, 1f);   // B = 0: the proxy never sways (no shadow shimmer on the ledge)
            }
            for (int i = 0; i < hem.Rings; i++) { proxy.Axis[i] = hem.Axis[i]; proxy.V[i] = hem.V[i]; }
            proxy.Emit(g.HemProxy);

            // ------------------------------------------------------------ robe body (run 0) + shoulder slope (run 1)
            int nBody = 30, nShoulder = 6;
            double e0 = ThreadConstants.HemBand, e1 = ColossusSpec.ShoulderEta[0], e2 = ColossusSpec.ShoulderEta[2];
            int bodyRings = (nBody + 1) + (nShoulder + 1);
            var body = new Grid(bodyRings, cols);
            var ringEta = new double[bodyRings];
            var ringRun = new int[bodyRings];
            for (int k = 0; k <= nBody; k++) { ringEta[k] = e0 + (e1 - e0) * k / nBody; ringRun[k] = 1; }
            for (int k = 0; k <= nShoulder; k++) { ringEta[nBody + 1 + k] = e1 + (e2 - e1) * k / nShoulder; ringRun[nBody + 1 + k] = 2; }
            for (int i = 0; i < bodyRings; i++)
            {
                double y = s.Y(ringEta[i]);
                body.Run[i] = ringRun[i];
                body.Axis[i] = new Vector3(0, (float)y, 0);
                body.V[i] = (float)y;
                for (int j = 0; j < cols; j++)
                {
                    double th = s.ColumnTheta(j);
                    int id = body.Id(i, j);
                    // the first ring is the hem's top ring (bit-identical copy), the shoulder crease ring is shared too
                    body.P[id] = i == 0 ? hem.P[hem.Id(hem.Rings - 1, j)]
                               : (i == nBody + 1 ? body.P[body.Id(nBody, j)] : AvenueMesh.V(s.LocalToRoot(s.RobeLocal(th, ringEta[i], false, ringRun[i]))));
                    body.C[id] = RobeColour(s, th, y);
                }
            }
            body.ComputeNormals();
            body.Emit(g.Robe, null, g.RobeSeams);
            g.BodyOpenEdges = cols;   // the bottom ring at the hem crease (the hem is a separate mesh)

            // ------------------------------------------------------------ hood (welded on the neck ring) with the face recess
            BuildHood(g, s, body, bodyRings - 1);

            // ------------------------------------------------------------ sleeves
            for (int i = 0; i < s.Sleeves.Length; i++) BuildSleeve(g, s, s.Sleeves[i]);

            // ------------------------------------------------------------ foot: ledge, bollards, alcove
            BuildFoot(g, s);

            // ------------------------------------------------------------ ground shadow
            g.ShadowShape = ColossusShadowShape.Build(s);
            BuildShadowMesh(g, s, g.ShadowShape);

            float top = float.MinValue;
            for (int k = 0; k < g.Robe.VertexCount; k++) top = Mathf.Max(top, g.Robe.Positions[k].y);
            g.TopY = top;
            return g;
        }

        // ================================================================ colours
        /// <summary>Robe vertex colour: AO in the flute valleys, at the ground, under the sleeves and at the neck; height shade; sway weight.</summary>
        public static Color RobeColour(ColossusShape s, double theta, double y)
        {
            double ao = ThreadMath.Lerp(AoGround, 1.0, ThreadMath.Smoothstep(-ThreadConstants.HemSink, AoGroundHeight, y));
            double amp = s.FluteAmplitude(y) / ColossusSpec.FluteAmp, win = s.FluteWindow(theta);
            double valley = 0.5 * (1.0 - s.FluteWave(theta));
            ao *= 1.0 - AoFlute * valley * amp * win;
            // under the sleeves: the lateral faces between the cuffs and the shoulders
            double hx = Math.Sin(theta) * s.Sx, hz = Math.Cos(theta) * s.Sz;
            double lat = Math.Abs(hx) / Math.Max(1e-9, Math.Sqrt(hx * hx + hz * hz));
            double cuff = ColossusSpec.ShoulderEta[0] - ColossusSpec.SleeveLength;
            double hw = ThreadMath.Smoothstep(s.Y(cuff - 10.0), s.Y(cuff + 20.0), y)
                      * (1.0 - ThreadMath.Smoothstep(s.Y(ColossusSpec.SleeveBlendEta), s.Y(ColossusSpec.ShoulderEta[0] - 1.0), y));
            ao *= 1.0 - AoSleeve * ThreadMath.Smoothstep(0.72, 0.97, lat) * hw;
            ao *= ThreadMath.Lerp(1.0, AoNeck, ThreadMath.Smoothstep(s.Y(ColossusSpec.ShoulderEta[1] + 2.0), s.Y(ColossusSpec.ShoulderEta[2]), y));
            return Col(ao, HeightShade(s, y), amp * win);
        }

        static double HeightShade(ColossusShape s, double y) => 1.0 - ColossusShape.Clamp(y / s.Height, 0.0, 1.0);

        static Color Col(double ao, double g, double b) =>
            new Color((float)ColossusShape.Clamp(ao, AoMin, 1.0), (float)ColossusShape.Clamp(g, 0.0, 1.0), (float)ColossusShape.Clamp(b, 0.0, 1.0), 1f);

        static Color HoodColour(ColossusShape s, double yUnbent, double ovalDist)
        {
            double ao = ThreadMath.Lerp(AoHoodNeck, 1.0, ThreadMath.Smoothstep(s.Y(ColossusSpec.HoodEta[0]), s.Y(ColossusSpec.HoodEta[1] + 4.0), yUnbent));
            ao *= ThreadMath.Lerp(0.82, 1.0, ThreadMath.Smoothstep(1.0, 1.6, ovalDist));
            return Col(ao, HeightShade(s, yUnbent), 0.0);
        }

        // ================================================================ doorway cells of the hem
        static void AddDoorCells(ColossusGeometry g, ColossusShape s, Grid hem, int[] idx)
        {
            int jd = ColossusSpec.DoorColumn;
            var mb = g.Hem;
            int A(int i) => hem.VertexIndex(idx, i, jd);
            int B(int i) => hem.VertexIndex(idx, i, jd + 1);
            Vector3 root = g.RootPosition;
            int Corner(D3 world, int ring)
            {
                Vector3 p = AvenueMesh.V(world) - root;
                Vector3 pa = mb.Positions[A(ring)], pb = mb.Positions[B(ring)];
                Vector3 ab = pb - pa;
                float t = Mathf.Clamp01(Vector3.Dot(p - pa, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
                Vector3 n = Vector3.Lerp(mb.Normals[A(ring)], mb.Normals[B(ring)], t).normalized;
                Color c = Color.Lerp(mb.Colors[A(ring)], mb.Colors[B(ring)], t);
                c.b = 0f;   // the doorway panel never sways (B = 0 in the flute window anyway)
                return mb.Add(p, n, new Vector2((float)jd / ColossusSpec.Segments, p.y), c);
            }
            var d = s.Door;
            int dl0 = Corner(d.DL0, 2), dr0 = Corner(d.DR0, 2), dl1 = Corner(d.DL1, 3), dr1 = Corner(d.DR1, 3);
            // ring 1 -> 2 (ground to sill): convex polygon A1 A2 DL0 DR0 B2 B1, fan from A1
            AvenueMesh.Tri(mb, A(1), A(2), dl0); AvenueMesh.Tri(mb, A(1), dl0, dr0);
            AvenueMesh.Tri(mb, A(1), dr0, B(2)); AvenueMesh.Tri(mb, A(1), B(2), B(1));
            // ring 2 -> 3 (sill to lintel): the two jamb strips beside the opening
            AvenueMesh.Quad(mb, A(2), A(3), dl1, dl0);
            AvenueMesh.Quad(mb, dr0, dr1, B(3), B(2));
            // ring 3 -> 4 (lintel to hem top): convex polygon A3 A4 B4 B3 DR1 DL1, fan from A4
            AvenueMesh.Tri(mb, A(4), B(4), B(3)); AvenueMesh.Tri(mb, A(4), B(3), dr1);
            AvenueMesh.Tri(mb, A(4), dr1, dl1); AvenueMesh.Tri(mb, A(4), dl1, A(3));
        }

        // ================================================================ hood
        static void BuildHood(ColossusGeometry g, ColossusShape s, Grid body, int neckRing)
        {
            int cols = ColossusSpec.Segments;
            int[] segSamples = { 2, 3, 5, 4, 3 };
            var ts = new List<double> { 0.0 };
            for (int i = 0; i < segSamples.Length; i++)
                for (int k = 1; k <= segSamples[i]; k++) ts.Add(i + (double)k / segSamples[i]);
            int rings = ts.Count;
            var hood = new Grid(rings, cols);
            var etaOf = new double[rings];
            var yUnbent = new double[rings];
            double halfTh = ColossusSpec.FaceHalfAngleDeg * ThreadMath.Deg;
            for (int i = 0; i < rings; i++)
            {
                s.HoodCurve(ts[i], out double eta, out double r);
                etaOf[i] = eta;
                yUnbent[i] = s.Y(eta);
                hood.V[i] = (float)yUnbent[i];
                hood.Axis[i] = AvenueMesh.V(s.LocalToRoot(s.Bend(new D3(0, yUnbent[i], 0))));
                for (int j = 0; j < cols; j++)
                {
                    double th = s.ColumnTheta(j);
                    int id = hood.Id(i, j);
                    hood.P[id] = i == 0 ? body.P[body.Id(neckRing, j)] : AvenueMesh.V(s.LocalToRoot(s.Bend(s.HoodLocalUnbent(th, ts[i]))));
                    hood.C[id] = HoodColour(s, yUnbent[i], OvalDist(ColossusShape.AngDiff(th, s.FaceTheta), eta, halfTh));
                }
            }
            int apex = rings - 1;
            hood.Pole[apex] = true;
            hood.PoleNormal[apex] = AvenueMesh.V(s.LocalToRoot(s.BendVector(new D3(0, 1, 0), yUnbent[apex])));
            for (int j = 0; j < cols; j++) hood.P[hood.Id(apex, j)] = AvenueMesh.V(s.LocalToRoot(s.Bend(new D3(0, yUnbent[apex], 0))));
            hood.ComputeNormals();

            // face window: the cells around the oval (centred on FaceTheta, turned toward the path); the brim rises over the margin band
            double marginTh = ColossusSpec.FaceMarginDeg * ThreadMath.Deg, marginEta = ColossusSpec.FaceMarginEta;
            int jLo = -1, jHi = -1;
            for (int j = 0; j < cols; j++)
            {
                if (Math.Abs(ColossusShape.AngDiff(s.ColumnTheta(j), s.FaceTheta)) <= halfTh + marginTh)
                {
                    if (jLo < 0) jLo = j;
                    jHi = j;
                }
            }
            int ja = jLo - 1, jb = jHi + 1;
            int ia = -1, ib = -1;
            double faceLo = ColossusSpec.FaceEta - ColossusSpec.FaceHalfEta - marginEta, faceHi = ColossusSpec.FaceEta + ColossusSpec.FaceHalfEta + marginEta;
            for (int i = 0; i < rings; i++)
            {
                if (etaOf[i] <= faceLo) ia = i;
                if (ib < 0 && etaOf[i] >= faceHi) ib = i;
            }
            bool face = ja >= 0 && jb < cols && ia >= 1 && ib > ia && ib < apex && jHi - jLo < cols / 2;
            Func<int, int, bool> skip = (i, j) => face && i >= ia && i < ib && j >= ja && j < jb;
            int[] idx = hood.Emit(g.Robe, skip, g.RobeSeams);
            if (face) BuildFace(g, s, hood, idx, ts, etaOf, ja, jb, ia, ib);
            else Debug.LogError($"[THREAD-FAIL] {g.Name}: face window does not fit the hood grid");
        }

        static double OvalDist(double thetaRel, double eta, double halfTh)
        {
            double u = thetaRel / halfTh, v = (eta - ColossusSpec.FaceEta) / ColossusSpec.FaceHalfEta;
            return Math.Sqrt(u * u + v * v);
        }

        static void BuildFace(ColossusGeometry g, ColossusShape s, Grid hood, int[] idx, List<double> ts, double[] etaOf, int ja, int jb, int ia, int ib)
        {
            double halfTh = ColossusSpec.FaceHalfAngleDeg * ThreadMath.Deg;
            double fEta = ColossusSpec.FaceEta, hEta = ColossusSpec.FaceHalfEta;
            // outer loop: grid vertices around the window (counter-clockwise in (theta, eta))
            var loopI = new List<int>(); var loopJ = new List<int>();
            for (int j = ja; j <= jb; j++) { loopI.Add(ia); loopJ.Add(j); }
            for (int i = ia + 1; i <= ib; i++) { loopI.Add(i); loopJ.Add(jb); }
            for (int j = jb - 1; j >= ja; j--) { loopI.Add(ib); loopJ.Add(j); }
            for (int i = ib - 1; i > ia; i--) { loopI.Add(i); loopJ.Add(ja); }
            int n = loopI.Count;

            Vector3 Surf(double th, double t) => AvenueMesh.V(s.LocalToRoot(s.Bend(s.HoodLocalUnbent(th, t))));
            Vector3 SurfN(double th, double t)
            {
                s.HoodCurve(t, out double eta, out _);
                return AvenueMesh.V(s.LocalToRoot(s.BendVector(s.HoodNormalUnbent(th, t), s.Y(eta)))).normalized;
            }

            var omega = new double[n];
            var outer = new int[n];
            var thB = new double[n]; var tB = new double[n];
            for (int m = 0; m < n; m++)
            {
                int i = loopI[m], j = loopJ[m];
                outer[m] = hood.VertexIndex(idx, i, j);
                thB[m] = ColossusShape.AngDiff(s.ColumnTheta(j), s.FaceTheta);   // relative to the face centre
                tB[m] = ts[i];
                omega[m] = Math.Atan2((etaOf[i] - fEta) / hEta, thB[m] / halfTh);
            }
            double tCentre = s.HoodT(fEta);
            // the face direction after the head bend (turned toward the path, bowed with the head)
            Vector3 fwd = AvenueMesh.V(s.LocalToRoot(s.HeadForward)).normalized;
            Vector3 recess = fwd * (float)ColossusSpec.FaceRecess;
            Vector3 centre = Surf(s.FaceTheta, tCentre);
            Vector3 headRef = AvenueMesh.V(s.LocalToRoot(s.Bend(new D3(0, s.Y(fEta), 0))));   // on the bent hood axis

            // Brim (critique run06 E6 / PAC-05 / C-04): grid boundary -> crest -> lip (the opening's edge, standing
            // forward of the hood surface by Brim(w)) -> lip-inner (rolled back into the opening), all robe material.
            // Then INK walls back to the recessed SAND_SHADE plate, which is smaller than the opening, so a dark ring
            // frames the lighter blank plate.
            var crestP = new Vector3[n]; var lipP = new Vector3[n]; var innerP = new Vector3[n]; var plateP = new Vector3[n];
            var outerP = new Vector3[n];
            var crestY = new double[n]; var lipY = new double[n]; var innerY = new double[n];
            for (int m = 0; m < n; m++)
            {
                double w = omega[m], sw = Math.Sin(w);
                double brim = ColossusSpec.BrimBase + ColossusSpec.BrimSin * sw + ColossusSpec.BrimSin2 * sw * sw;
                OvalAt(s, 1.0, w, halfTh, fEta, hEta, out double thO, out double tO);
                OvalAt(s, ColossusSpec.BrimLipScale, w, halfTh, fEta, hEta, out double thI, out double tI);
                OvalAt(s, ColossusSpec.BrimPlateScale, w, halfTh, fEta, hEta, out double thP, out double tP);
                double thM = s.FaceTheta + 0.5 * (thB[m] + (thO - s.FaceTheta)), tM = 0.5 * (tB[m] + tO);
                outerP[m] = g.Robe.Positions[outer[m]];
                crestP[m] = Surf(thM, tM) + fwd * (float)(ColossusSpec.BrimCrestFrac * brim);
                lipP[m] = Surf(thO, tO) + fwd * (float)brim;
                innerP[m] = Surf(thI, tI) + fwd * (float)(brim - ColossusSpec.BrimLipRoll);
                plateP[m] = Surf(thP, tP) - recess;
                crestY[m] = UnbentY(s, tM); lipY[m] = UnbentY(s, tO); innerY[m] = UnbentY(s, tI);
            }
            var crest = new int[n]; var lip = new int[n]; var inner = new int[n];
            for (int m = 0; m < n; m++)
            {
                Vector3 nCrest = LoopNormal(crestP, m, lipP[m] - outerP[m], crestP[m] - headRef);
                Vector3 nLip = LoopNormal(lipP, m, innerP[m] - crestP[m], lipP[m] - headRef);
                Vector3 nInner = LoopNormal(innerP, m, innerP[m] - lipP[m], fwd);
                crest[m] = g.Robe.Add(crestP[m], nCrest, new Vector2(0, (float)crestY[m]), BrimColour(s, crestY[m], BrimCrestAo));
                lip[m] = g.Robe.Add(lipP[m], nLip, new Vector2(0, (float)lipY[m]), BrimColour(s, lipY[m], 1.0));
                inner[m] = g.Robe.Add(innerP[m], nInner, new Vector2(0, (float)innerY[m]), BrimColour(s, innerY[m], BrimInnerAo));
            }
            for (int m = 0; m < n; m++)
            {
                int k = (m + 1) % n;
                AvenueMesh.Quad(g.Robe, outer[m], outer[k], crest[k], crest[m]);
                AvenueMesh.Quad(g.Robe, crest[m], crest[k], lip[k], lip[m]);
                AvenueMesh.Quad(g.Robe, lip[m], lip[k], inner[k], inner[m]);
            }

            // recess walls (INK): from the rolled lip back to the plate rim, normals toward the opening axis
            var wallTop = new int[n]; var wallBot = new int[n];
            Color inkC = Col(0.7, 1.0 - ColossusShape.Clamp(centre.y / s.Height, 0, 1), 0.0);
            for (int m = 0; m < n; m++)
            {
                int k = (m + 1) % n, h = (m + n - 1) % n;
                Vector3 along = (innerP[k] - innerP[h]) + (plateP[k] - plateP[h]);
                Vector3 toAxis = centre + fwd * Vector3.Dot(innerP[m] - centre, fwd) - innerP[m];
                Vector3 nn = Vector3.Cross(along, plateP[m] - innerP[m]);
                nn = nn.sqrMagnitude > 1e-12f ? nn.normalized : toAxis.normalized;
                if (Vector3.Dot(nn, toAxis) < 0f) nn = -nn;
                wallTop[m] = g.Ink.Add(innerP[m], nn, new Vector2((float)m / n, 0), inkC);
                wallBot[m] = g.Ink.Add(plateP[m], nn, new Vector2((float)m / n, 1), inkC);
            }
            for (int m = 0; m < n; m++)
            {
                int k = (m + 1) % n;
                AvenueMesh.Quad(g.Ink, wallTop[m], wallTop[k], wallBot[k], wallBot[m]);
            }

            // blank face plate (SAND_SHADE): the hood surface inside the plate oval, recessed by 1.2 m (660 b)
            float[] rhos = { 1f, 2f / 3f, 1f / 3f };
            var plate = new int[rhos.Length, n];
            for (int r = 0; r < rhos.Length; r++)
            {
                for (int m = 0; m < n; m++)
                {
                    OvalAt(s, rhos[r] * ColossusSpec.BrimPlateScale, omega[m], halfTh, fEta, hEta, out double th, out double t);
                    Vector3 p = r == 0 ? plateP[m] : Surf(th, t) - recess;
                    plate[r, m] = g.Face.Add(p, SurfN(th, t), new Vector2((float)m / n, rhos[r]), Col(0.9, 1.0 - ColossusShape.Clamp(p.y / s.Height, 0, 1), 0.0));
                }
            }
            int pc = g.Face.Add(centre - recess, SurfN(s.FaceTheta, tCentre), new Vector2(0.5f, 0f), Col(0.95, 1.0 - ColossusShape.Clamp(centre.y / s.Height, 0, 1), 0.0));
            for (int r = 0; r + 1 < rhos.Length; r++)
                for (int m = 0; m < n; m++)
                {
                    int k = (m + 1) % n;
                    AvenueMesh.Quad(g.Face, plate[r, m], plate[r, k], plate[r + 1, k], plate[r + 1, m]);
                }
            for (int m = 0; m < n; m++) AvenueMesh.Tri(g.Face, plate[rhos.Length - 1, m], plate[rhos.Length - 1, (m + 1) % n], pc);
        }

        /// <summary>Hood-curve coordinates (theta, t) of the oval at scale rho and angle w around it (+90 deg = top).</summary>
        static void OvalAt(ColossusShape s, double rho, double w, double halfTh, double fEta, double hEta, out double th, out double t)
        {
            th = s.FaceTheta + rho * halfTh * Math.Cos(w);
            t = s.HoodT(fEta + rho * hEta * Math.Sin(w));
        }

        static double UnbentY(ColossusShape s, double t)
        {
            s.HoodCurve(t, out double eta, out _);
            return s.Y(eta);
        }

        /// <summary>Unit normal of a closed vertex loop at m: across the loop (given) x along it, oriented toward refDir.</summary>
        static Vector3 LoopNormal(Vector3[] loop, int m, Vector3 across, Vector3 refDir)
        {
            int n = loop.Length;
            Vector3 along = loop[(m + 1) % n] - loop[(m + n - 1) % n];
            Vector3 nn = Vector3.Cross(along, across);
            if (nn.sqrMagnitude < 1e-12f) return refDir.normalized;
            nn.Normalize();
            return Vector3.Dot(nn, refDir) < 0f ? -nn : nn;
        }

        // baked AO of the brim: the crest a touch darker than the lit lip, the rolled inner face darker (it looks into the opening)
        const double BrimCrestAo = 0.97, BrimInnerAo = 0.82;

        static Color BrimColour(ColossusShape s, double yUnbent, double aoMul)
        {
            Color c = HoodColour(s, yUnbent, 2.0);   // 2.0: outside the oval-edge AO band
            c.r = (float)ColossusShape.Clamp(c.r * aoMul, AoMin, 1.0);
            return c;
        }

        // ================================================================ sleeves
        static void BuildSleeve(ColossusGeometry g, ColossusShape s, SleeveShape sl)
        {
            int sides = ColossusSpec.SleeveSides;
            int m = sl.Count;
            // dome rings (ring 0 = apex), the 12 sections and the closed cuff (last ring = its pole): one smooth loft in
            // the robe material, the same rings the ground shadow projects. No INK anywhere on a sleeve (C-10, E7).
            int rings = sl.RingCount, cuff0 = sl.CuffStart, last = rings - 1;
            var grid = new Grid(rings, sides);
            D3 t0 = sl.T[0], tc = sl.T[m - 1];
            for (int i = 0; i < rings; i++)
            {
                sl.Ring(i, out D3 centre, out _, out _, out _);
                // cuff rings orient their normals away from the last section centre (behind the whole cuff), so the
                // pleat flanks on the nearly flat dome never flip
                grid.Axis[i] = AvenueMesh.V(s.LocalToRoot(i >= cuff0 ? sl.C[m - 1] : centre));
                grid.V[i] = (float)centre.y;
                for (int j = 0; j < sides; j++)
                    grid.P[grid.Id(i, j)] = AvenueMesh.V(s.LocalToRoot(sl.RingPoint(i, 2.0 * Math.PI * j / sides)));
            }
            grid.Pole[0] = true;
            grid.PoleNormal[0] = AvenueMesh.V(s.LocalToRoot(t0 * -1.0));
            grid.Pole[last] = true;
            grid.PoleNormal[last] = AvenueMesh.V(s.LocalToRoot(tc));
            grid.ComputeNormals();
            // AO: darker where the sleeve faces the robe, where it enters the shoulder, in the groove where the hem rolls
            // under (CuffAo) and in the pleat valleys of the gathered dome. The facing term uses the horizontal part of the
            // unit normal, so it fades out smoothly on the dome where the normal turns vertical.
            double foldMax = 0.0;
            foreach (double f in ColossusSpec.CuffFold) foldMax = Math.Max(foldMax, f);
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sides; j++)
                {
                    int id = grid.Id(i, j);
                    Vector3 p = grid.P[id], n = grid.N[id];
                    var toAxis = new Vector3(-p.x, 0, -p.z);
                    var nh = new Vector3(n.x, 0, n.z);
                    float facing = toAxis.sqrMagnitude > 1e-6f ? Vector3.Dot(toAxis.normalized, nh) : 0f;
                    double ao = 1.0 - 0.30 * ThreadMath.Smoothstep(0.0, 0.9, facing);
                    ao *= ThreadMath.Lerp(1.0, 0.75, ThreadMath.Smoothstep(s.Y(ColossusSpec.SleeveBlendEta - 2.0), s.Y(ColossusSpec.ShoulderEta[0] - 5.0), p.y));
                    if (i >= cuff0)
                    {
                        int q = i - cuff0;
                        double valley = 0.5 * (1.0 - SleeveShape.FoldWave(2.0 * Math.PI * j / sides));
                        ao *= ColossusSpec.CuffAo[q] * (1.0 - ColossusSpec.CuffFoldAo * valley * ColossusSpec.CuffFold[q] / foldMax);
                    }
                    grid.C[id] = Col(ao, HeightShade(s, p.y), 0.0);
                }
            grid.Emit(g.Robe, null, g.RobeSeams);
        }

        // ================================================================ foot: ledge, bollards, alcove
        static void BuildFoot(ColossusGeometry g, ColossusShape s)
        {
            var d = s.Door;
            Vector3 root = g.RootPosition;
            Vector3 W(D3 p) => AvenueMesh.V(p) - root;
            Vector3 nh = AvenueMesh.V(d.Nh).normalized, h = AvenueMesh.V(d.H).normalized;
            Vector3 o = W(d.O);
            Quaternion rot = AvenueMesh.LookRotation(nh, Up);
            double riser = ThreadConstants.LedgeRiser, tread = ThreadConstants.LedgeTread;
            int steps = ThreadConstants.LedgeSteps;
            const float chamfer = 0.02f, inset = 0.02f, sinkBottom = 0.05f, backIn = 0.35f;
            Color stoneC = Col(1.0, 1.0, 0.0);
            // the top step reaches through the doorway to just behind the alcove's back wall (it is the threshold floor)
            float alcoveBack = (float)(d.WallOffset(d.Sill) - ThreadConstants.DoorRecess) - 0.05f;
            for (int k = 1; k <= steps; k++)
            {
                float bottom = k == 1 ? -sinkBottom : (float)((k - 1) * riser);
                float topY = (float)(k * riser);
                float front = (float)((steps - k + 1) * tread);
                float back = k < steps ? -backIn : alcoveBack;
                float width = (float)ThreadConstants.LedgeWidth - 2f * inset * (k - 1);
                Vector3 centre = o + nh * (0.5f * (front + back)) + Up * (0.5f * (topY + bottom));
                MeshKit.ChamferedBox(g.Stone, centre, new Vector3(width, topY - bottom, front - back), chamfer, rot, stoneC);
                g.StepHeights.Add(new Vector2(bottom, topY));
            }

            // bollard pair (lathe, pole at the top), 1.0 m, never scaled
            double br = 0.5 * ThreadConstants.BollardDiameter, bh = ThreadConstants.BollardHeight;
            double[] by = { -sinkBottom, 0.78 * bh, 0.86 * bh, 0.92 * bh, 0.965 * bh, 0.99 * bh, bh };
            double[] brr = { br, br, 0.96 * br, 0.85 * br, 0.64 * br, 0.36 * br, 0.0 };
            float lateral = (float)(0.5 * ThreadConstants.LedgeWidth + 0.45), outward = 0.9f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 basePt = o + h * (side * lateral) + nh * outward;
                var grid = new Grid(by.Length, BollardSegments);
                for (int i = 0; i < by.Length; i++)
                {
                    grid.Axis[i] = basePt + Up * (float)by[i];
                    grid.V[i] = (float)by[i];
                    for (int j = 0; j < BollardSegments; j++)
                    {
                        double a = 2.0 * Math.PI * j / BollardSegments;
                        grid.P[grid.Id(i, j)] = basePt + new Vector3((float)(Math.Sin(a) * brr[i]), (float)by[i], (float)(Math.Cos(a) * brr[i]));
                        grid.C[grid.Id(i, j)] = Col(ThreadMath.Lerp(0.75, 1.0, ThreadMath.Smoothstep(0.0, 0.3, by[i])), 1.0, 0.0);
                    }
                }
                grid.Pole[by.Length - 1] = true;
                grid.PoleNormal[by.Length - 1] = Up;
                grid.ComputeNormals();
                grid.Emit(g.Bollard, null, g.BollardSeams);
            }

            // boxed alcove behind the opening (0.8 m deep, INK); its floor is the top step
            Vector3 back3 = -nh * (float)ThreadConstants.DoorRecess;
            Vector3 dl0 = W(d.DL0), dr0 = W(d.DR0), dl1 = W(d.DL1), dr1 = W(d.DR1);
            Vector3 u = AvenueMesh.V(d.U).normalized, nFacet = AvenueMesh.V(d.N).normalized;
            Vector3 nLeft = Vector3.Cross(u, nh).normalized;
            if (Vector3.Dot(nLeft, h) < 0f) nLeft = -nLeft;    // left jamb faces +H (toward the opening centre)
            Color inkC = Col(0.75, 1.0, 0.0);
            AvenueMesh.FlatQuad(g.AlcoveInk, dl0, dl1, dl1 + back3, dl0 + back3, nLeft, inkC);
            AvenueMesh.FlatQuad(g.AlcoveInk, dr0, dr0 + back3, dr1 + back3, dr1, -nLeft, inkC);
            AvenueMesh.FlatQuad(g.AlcoveInk, dl1, dr1, dr1 + back3, dl1 + back3, -Up, inkC);
            AvenueMesh.FlatQuad(g.AlcoveInk, dl0 + back3, dl1 + back3, dr1 + back3, dr0 + back3, nFacet, inkC);

            // Raised light frame (critique run07 A15: seen from the path at 52 m and ~57 deg off the door normal, the INK
            // opening alone read as a dark post). Jambs and head DoorFrameWidth wide, standing DoorFrameDepth proud of the
            // facet, on the threshold step: the front band, the outer sides, the head top and the reveal that leads into
            // the INK alcove. The head is split over the jambs so no edge has a T-junction.
            double hw0 = 0.5 * ThreadConstants.DoorWidth, fw = ColossusSpec.DoorFrameWidth;
            Vector3 fd = nFacet * (float)ColossusSpec.DoorFrameDepth;
            Vector3 xl0 = W(d.At(-hw0 - fw, d.Sill)), xr0 = W(d.At(hw0 + fw, d.Sill));
            Vector3 hl = W(d.At(-hw0 - fw, d.Lintel)), hr = W(d.At(hw0 + fw, d.Lintel));
            Vector3 xl1 = W(d.At(-hw0 - fw, d.Lintel + fw)), xr1 = W(d.At(hw0 + fw, d.Lintel + fw));
            Vector3 tl = W(d.At(-hw0, d.Lintel + fw)), tr = W(d.At(hw0, d.Lintel + fw));
            Color frontC = Col(1.0, 1.0, 0.0), sideC = Col(FrameSideAo, 1.0, 0.0), revealC = Col(FrameRevealAo, 1.0, 0.0);
            var fm = g.DoorFrame;
            AvenueMesh.FlatQuad(fm, xl0 + fd, dl0 + fd, dl1 + fd, hl + fd, nFacet, frontC);   // left jamb
            AvenueMesh.FlatQuad(fm, dr0 + fd, xr0 + fd, hr + fd, dr1 + fd, nFacet, frontC);   // right jamb
            AvenueMesh.FlatQuad(fm, hl + fd, dl1 + fd, tl + fd, xl1 + fd, nFacet, frontC);    // head over the left jamb
            AvenueMesh.FlatQuad(fm, dl1 + fd, dr1 + fd, tr + fd, tl + fd, nFacet, frontC);    // head over the opening
            AvenueMesh.FlatQuad(fm, dr1 + fd, hr + fd, xr1 + fd, tr + fd, nFacet, frontC);    // head over the right jamb
            AvenueMesh.FlatQuad(fm, xl0, xl1, xl1 + fd, xl0 + fd, -h, sideC);                  // outer sides and head top
            AvenueMesh.FlatQuad(fm, xr0, xr0 + fd, xr1 + fd, xr1, h, sideC);
            AvenueMesh.FlatQuad(fm, xl1, xr1, xr1 + fd, xl1 + fd, u, frontC);
            AvenueMesh.FlatQuad(fm, dl0, dl0 + fd, dl1 + fd, dl1, h, revealC);                  // reveal into the alcove
            AvenueMesh.FlatQuad(fm, dr0, dr1, dr1 + fd, dr0 + fd, -h, revealC);
            AvenueMesh.FlatQuad(fm, dl1, dl1 + fd, dr1 + fd, dr1, -u, revealC);
        }

        // baked AO of the door frame: the outer sides a touch darker than the front, the reveal (it looks into the alcove) darker
        const double FrameSideAo = 0.92, FrameRevealAo = 0.8;

        // ================================================================ ground shadow
        static void BuildShadowMesh(ColossusGeometry g, ColossusShape s, ColossusShadowShape sh)
        {
            float y = (float)ThreadConstants.ColossusShadowY;
            for (int i = 0; i < sh.Vertices.Count; i++)
            {
                V2 v = sh.Vertices[i];
                var p = new Vector3((float)(v.x - s.Cx), y, (float)(v.z - s.Cz));
                g.Shadow.Add(p, Up, new Vector2((float)v.x, (float)v.z), new Color(1f, 1f, 0f, (float)sh.Alpha[i]));
            }
            for (int k = 0; k + 2 < sh.Triangles.Count; k += 3) g.Shadow.Tri(sh.Triangles[k], sh.Triangles[k + 1], sh.Triangles[k + 2]);
        }
    }
}
