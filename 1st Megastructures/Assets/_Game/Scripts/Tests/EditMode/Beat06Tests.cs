using System;
using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 6 acceptance checks of 14.5.6 / 14.10 on the Full tier (and the Simplified fallback), built in EditMode with the
    /// generated MaterialLibrary: housing O 300 +/- 0.5; 12 blades; tip R 138 +/- 0.2; free gap 18 +/- 0.5 degrees at R 48, 90
    /// and 130 with no two blades intersecting, measured on the blade mesh itself projected on the rotation plane (every
    /// triangle edge crossing the circle R contributes its crossing angle to its blade's interval); beam wedges 12.0 +/- 0.1
    /// degrees wide, R 70-130, 300 m, pitched 18 degrees and centred in the blade gaps; gantry 199.0 +/- 0.1 x 3.0 +/- 0.02 ending
    /// at z = 259 and z = 60; rotation clockwise seen from +Z with a 40 s period; every Full-tier mesh valid and the beat inside
    /// its 800 k triangle budget. Temporary objects and meshes are destroyed afterwards.
    /// </summary>
    public sealed class Beat06Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";
        static readonly float[] GapRadii = { 48f, 90f, 130f };
        const int Blades = 12;
        const double Pitch = 360.0 / Blades;

        MaterialLibrary _lib;
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Mesh> _meshes = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            _lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (_lib == null) Assert.Inconclusive("MaterialLibrary missing at " + LibraryPath + " (run MaterialBuilder.BuildAll)");
            if (_lib.materials == null || _lib.materials.Length < (int)MatId.Count) Assert.Inconclusive("MaterialLibrary incomplete");
            for (int i = 0; i < (int)MatId.Count; i++)
                if (_lib.materials[i] == null) Assert.Inconclusive("MaterialLibrary lacks base material " + (MatId)i);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) UnityEngine.Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
        }

        Beat06_Fan Build(BeatTier tier)
        {
            var root = new GameObject("Beat06Test_" + tier);
            _roots.Add(root);
            var beat = root.AddComponent<Beat06_Fan>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            var ctx = new BeatContext
            {
                Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = true,
            };
            beat.Build(ctx);
            var list = new List<Mesh>();
            beat.CollectMeshes(list);
            _meshes.AddRange(list);
            return beat;
        }

        static MeshFilter Filter(Component beat, string meshName)
        {
            foreach (MeshFilter mf in beat.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name == meshName) return mf;
            Assert.Fail("no mesh named " + meshName);
            return null;
        }

        static Renderer RendererNamed(Component beat, string objectName)
        {
            foreach (Renderer r in beat.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == objectName) return r;
            Assert.Fail("no renderer on an object named " + objectName);
            return null;
        }

        static Transform Child(Component beat, string objectName)
        {
            foreach (Transform t in beat.GetComponentsInChildren<Transform>(true))
                if (t.name == objectName) return t;
            Assert.Fail("no object named " + objectName);
            return null;
        }

        static double Wrap180(double a)
        {
            a -= 360.0 * Math.Round(a / 360.0);
            return a;
        }

        // ---------------------------------------------------------------- blade projection (rotor frame, XY plane)

        sealed class BladeMesh
        {
            public Vector3[] V;
            public int[] T;
            public int[] BladeOfTriangle;
        }

        static BladeMesh LoadBlades(Component beat)
        {
            Mesh m = Filter(beat, "FanBlades").sharedMesh;
            Assert.IsTrue(m.isReadable, "blade mesh must stay readable for the projection test");
            var b = new BladeMesh { V = m.vertices, T = m.triangles };
            b.BladeOfTriangle = new int[b.T.Length / 3];
            for (int i = 0; i < b.BladeOfTriangle.Length; i++)
            {
                Vector3 c = (b.V[b.T[3 * i]] + b.V[b.T[3 * i + 1]] + b.V[b.T[3 * i + 2]]) / 3f;
                double a = Math.Atan2(c.y, c.x) * 180.0 / Math.PI;
                int k = (int)Math.Round(a / Pitch);
                b.BladeOfTriangle[i] = ((k % Blades) + Blades) % Blades;
            }
            return b;
        }

        /// <summary>Per-blade angular interval at radius r, in degrees relative to the blade's nominal centre 30 k.</summary>
        static void Coverage(BladeMesh b, double r, double[] lo, double[] hi)
        {
            for (int k = 0; k < Blades; k++) { lo[k] = double.MaxValue; hi[k] = double.MinValue; }
            double r2 = r * r;
            for (int i = 0; i < b.BladeOfTriangle.Length; i++)
            {
                int k = b.BladeOfTriangle[i];
                for (int e = 0; e < 3; e++)
                {
                    Vector3 pa = b.V[b.T[3 * i + e]], pb = b.V[b.T[3 * i + (e + 1) % 3]];
                    double ax = pa.x, ay = pa.y, dx = pb.x - pa.x, dy = pb.y - pa.y;
                    double qa = dx * dx + dy * dy, qb = 2.0 * (ax * dx + ay * dy), qc = ax * ax + ay * ay - r2;
                    if (qa < 1e-18) continue;
                    double disc = qb * qb - 4.0 * qa * qc;
                    if (disc < 0.0) continue;
                    double sq = Math.Sqrt(disc);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        double t = (-qb + sgn * sq) / (2.0 * qa);
                        if (t < 0.0 || t > 1.0) continue;
                        double ang = Math.Atan2(ay + t * dy, ax + t * dx) * 180.0 / Math.PI;
                        double d = Wrap180(ang - Pitch * k);
                        if (d < lo[k]) lo[k] = d;
                        if (d > hi[k]) hi[k] = d;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- tests

        [Test]
        public void HousingDiameterIs300()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Bounds hb = Filter(b, "FanHousing").sharedMesh.bounds;   // lathe frame: x and z are radial, y is the fan axis
            Assert.That(hb.size.x, Is.EqualTo(2.0 * AbyssConst.FAN_R_OUT).Within(0.5), "housing outer diameter");
            Assert.That(hb.size.z, Is.EqualTo(2.0 * AbyssConst.FAN_R_OUT).Within(0.5), "housing outer diameter (z)");
            Bounds lb = Filter(b, "FanHousingLiner").sharedMesh.bounds;
            Assert.That(lb.size.x, Is.EqualTo(2.0 * AbyssConst.FAN_R_INNER).Within(0.1), "housing inner diameter (liner, R 138)");
        }

        /// <summary>
        /// The trim collar on the near flat (R 146 to R1, 2 m proud) shares its depth with the ribs and belts of Beat02Rings
        /// (6 / 20 m proud), so every ring band it reaches must be a bore level whose gap (bore chord + BORE_MARGIN at the band
        /// edge nearest the fan centre) clears the collar's widest chord in that band by at least 0.5 m. The collar also keeps
        /// the visible rim within 3 m of the 300 m housing.
        /// </summary>
        [Test]
        public void CollarStaysInsideTheRingBoreGaps()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            MeshFilter mf = Filter(b, "FanCollar");
            double r1 = 0.0;   // lathe frame: x and z are radial
            foreach (Vector3 v in mf.sharedMesh.vertices) r1 = Math.Max(r1, Math.Sqrt((double)v.x * v.x + (double)v.z * v.z));
            Assert.Greater(r1, AbyssConst.FAN_R_OUT, "the collar covers the wall / lining corner of the bore");
            Assert.LessOrEqual(r1, AbyssConst.FAN_R_OUT + 3.0, "collar within 3 m of the bore (300 m housing silhouette)");

            Bounds wb = RendererNamed(b, "FanCollar").bounds;
            Assert.Greater(wb.max.z, AbyssConst.SHAFT_NEAR_Z, "the collar stands proud of the near flat (it meets the ring depth)");

            Vector3 fan = AbyssConst.FAN_CENTRE;
            var levels = new List<Beat02Rings.Level>();
            Beat02Rings.Levels(false, true, levels);
            int reached = 0;
            foreach (Beat02Rings.Level L in levels)
            {
                float hh = 0.5f * Beat02Rings.Height(L.Belt);
                double lo = L.Y - hh, hi = L.Y + hh;
                double dy = fan.y < lo ? lo - fan.y : (fan.y > hi ? fan.y - hi : 0.0);
                if (dy >= r1) continue;   // the collar does not reach this band
                reached++;
                Assert.IsTrue(L.Bore, "ring level " + L.K + " reaches the collar but is not gapped around the bore");
                double chord = Math.Sqrt(r1 * r1 - dy * dy);
                Beat02Rings.BoreGap(L.Y, hh, out float x0, out float x1);
                double gapHalf = Math.Min(fan.x - x0, x1 - fan.x);
                Assert.GreaterOrEqual(gapHalf, chord + 0.5, "ring level " + L.K + " (y " + L.Y + "): band end clear of the collar by 0.5 m");
            }
            Assert.Greater(reached, 0, "the collar crosses the ring levels around the fan (k 23..27)");

            var report = new ScaleReport();
            b.ReportScale(report);
            Assert.That(report.Values["fan.rim.diameter"], Is.EqualTo(2.0 * r1).Within(0.05), "visible rim diameter");
            Assert.That(report.Values["fan.rim.clearance"], Is.GreaterThanOrEqualTo(17.0), "rim well above the walkway");
        }

        [Test]
        public void ConcreteIsBoardFormedAndRailsFadeToTheDeckTone()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            float board = _lib.materials[(int)MatId.ConcreteBoard].GetFloat("_Recipe");
            foreach (string n in new[] { "BoreLining", "FanHousingLiner", "FanHousing", "FanCollar" })
                Assert.AreEqual(board, RendererNamed(b, n).sharedMaterial.GetFloat("_Recipe"),
                    n + ": M_ConcreteBoard (world projection), not the vertical-column ConcreteCyl recipe");

            Material rail = RendererNamed(b, "GantryRails").sharedMaterial;
            Vector4 fr = rail.GetVector("_FarRange");
            Assert.Greater(fr.y, fr.x, "rails fade into the deck tone (14.7.9)");
            Assert.LessOrEqual(fr.y, 60f, "rails reach the deck tone by 60 m");
            Color tint = rail.GetColor("_FarTint");
            Color deck = RendererNamed(b, "GantryDeck").sharedMaterial.GetColor("_BaseColor");
            Assert.That(tint.r, Is.EqualTo(deck.r).Within(0.01f), "far tint = deck tone (r)");
            Assert.That(tint.g, Is.EqualTo(deck.g).Within(0.01f), "far tint = deck tone (g)");
            Assert.That(tint.b, Is.EqualTo(deck.b).Within(0.01f), "far tint = deck tone (b)");
            Assert.GreaterOrEqual(rail.GetFloat("_ThinLine"), AbyssConst.MIN_LINE_PX, "rails keep the ThinLine minimum width");
            Assert.AreEqual(ShadowCastingMode.Off, RendererNamed(b, "GantryRails").shadowCastingMode, "rails cast no shadows");
        }

        [Test]
        public void TwelveBladesWithTipRadius138()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            BladeMesh m = LoadBlades(b);
            var seen = new bool[Blades];
            foreach (int k in m.BladeOfTriangle) seen[k] = true;
            int count = 0;
            for (int k = 0; k < Blades; k++) if (seen[k]) count++;
            Assert.AreEqual(Blades, count, "distinct blades in the blade mesh");
            double rMax = 0.0;
            foreach (Vector3 v in m.V) rMax = Math.Max(rMax, Math.Sqrt((double)v.x * v.x + (double)v.y * v.y));
            Assert.That(rMax, Is.EqualTo(AbyssConst.FAN_R_TIP).Within(0.2), "blade tip radius");
            Assert.LessOrEqual(rMax, AbyssConst.FAN_R_INNER, "the tips stay inside the R 138 housing liner");
        }

        [Test]
        public void FreeGapIs18DegreesAtR48_90_130()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            BladeMesh m = LoadBlades(b);
            var lo = new double[Blades];
            var hi = new double[Blades];
            foreach (float r in GapRadii)
            {
                Coverage(m, r, lo, hi);
                for (int k = 0; k < Blades; k++)
                {
                    Assert.Less(lo[k], hi[k], "blade " + k + " does not cross R " + r);
                    Assert.That(hi[k] - lo[k], Is.EqualTo(12.0).Within(0.5), "blade " + k + " arc at R " + r);
                    int n = (k + 1) % Blades;
                    double gap = Pitch + lo[n] - hi[k];
                    Assert.That(gap, Is.EqualTo(18.0).Within(0.5), "free gap between blades " + k + " and " + n + " at R " + r);
                }
            }
        }

        [Test]
        public void NoTwoBladesIntersectInProjection()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            BladeMesh m = LoadBlades(b);
            var lo = new double[Blades];
            var hi = new double[Blades];
            for (double r = 39.5; r <= AbyssConst.FAN_R_TIP - 0.1; r += 0.5)
            {
                Coverage(m, r, lo, hi);
                for (int k = 0; k < Blades; k++)
                {
                    if (lo[k] > hi[k]) continue;   // radius inside the hub drum region without a crossing
                    int n = (k + 1) % Blades;
                    if (lo[n] > hi[n]) continue;
                    Assert.Greater(Pitch + lo[n] - hi[k], 0.0, "blades " + k + " and " + n + " overlap in projection at R " + r);
                }
            }
        }

        [Test]
        public void BeamWedgesAre12DegreesWideAndCentredInTheGaps()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Material bm = RendererNamed(b, "FanBeams").sharedMaterial;
            Vector4 p = bm.GetVector("_BeamParams");
            Vector4 d = bm.GetVector("_BeamDir");
            Assert.That(2.0 * p.z * 180.0 / Math.PI, Is.EqualTo(AbyssConst.BEAM_WIDTH_DEG).Within(0.1), "wedge width");
            Assert.That(p.x, Is.EqualTo(AbyssConst.BEAM_R0).Within(1e-3), "wedge inner radius");
            Assert.That(p.y, Is.EqualTo(AbyssConst.BEAM_R1).Within(1e-3), "wedge outer radius");
            Assert.That(p.w, Is.EqualTo(AbyssConst.BEAM_LENGTH).Within(1e-3), "wedge length");
            double pitch = -Math.Asin(d.y / ((Vector3)d).magnitude) * 180.0 / Math.PI;
            Assert.That(pitch, Is.EqualTo(AbyssConst.BEAM_PITCH_DEG).Within(0.1), "beams pitched 18 degrees down");
            Assert.Greater(d.z, 0f, "beams travel +Z, from the fan into the shaft");
            Assert.AreEqual(0f, d.x, 1e-6f, "beams have no sideways component (light yaw 0)");

            var report = new ScaleReport();
            b.ReportScale(report);
            Assert.AreEqual(AbyssConst.BEAM_COUNT, report.Values["fan.beam.count"], 1e-9, "12 wedges");
            double phase = report.Values["fan.beam.phase.deg"];

            // the wedges must sit in the measured gap centres (wedge narrower than the gap on both sides)
            BladeMesh m = LoadBlades(b);
            var lo = new double[Blades];
            var hi = new double[Blades];
            foreach (float r in GapRadii)
            {
                Coverage(m, r, lo, hi);
                for (int k = 0; k < Blades; k++)
                {
                    int n = (k + 1) % Blades;
                    double gapStart = Pitch * k + hi[k], gapEnd = Pitch * (k + 1) + lo[n];
                    double centre = 0.5 * (gapStart + gapEnd);
                    Assert.That(Wrap180(centre - (phase + Pitch * k)), Is.EqualTo(0.0).Within(0.1), "wedge " + k + " centred in its gap at R " + r);
                    Assert.Less(AbyssConst.BEAM_WIDTH_DEG, gapEnd - gapStart, "wedge narrower than the gap at R " + r);
                }
            }
            Assert.AreEqual(ShadowCastingMode.Off, RendererNamed(b, "FanBeams").shadowCastingMode, "beams never cast shadows");
            Assert.AreEqual(Child(b, "Rotor"), RendererNamed(b, "FanBeams").transform.parent, "beams rotate rigidly with the blades");
        }

        // ---------------------------------------------------------------- CPU mirror of BeamWedge mode 0 (14.7.4)

        /// <summary>Fan-beam look read from the FanBeams material (the shader's mode-0 inputs).</summary>
        struct BeamLook
        {
            public double R0, R1, Half, Len, FeatherMin, FeatherPx, ChordFeather, AxialIn, AxOut0, AxOut1, Ext, Near0, Near1, Sat, G, Density,
                ColourLuma;

            /// <summary>The beam look of the material at a ride second (density, near fade and fade-in follow the ride blend).</summary>
            public static BeamLook At(Material m, float rideSeconds)
            {
                BeamLook b = From(m, Beat06_Fan.BeamDensityFull * Beat06_Fan.BeamHaze(rideSeconds));
                Vector4 s = Beat06_Fan.BeamShapeAt(rideSeconds), f = Beat06_Fan.BeamFeatherAt(rideSeconds);
                b.Ext = s.x; b.Near0 = s.y; b.Near1 = s.z; b.Sat = s.w;
                b.FeatherMin = f.x; b.FeatherPx = f.y; b.ChordFeather = f.z; b.AxialIn = Math.Max(Math.Min(f.w, 0.25 * b.Len), 1e-3);
                return b;
            }

            public static BeamLook From(Material m, float density)
            {
                Vector4 p = m.GetVector("_BeamParams"), f = m.GetVector("_BeamFeather"), s = m.GetVector("_BeamShape");
                Vector4 a = m.GetVector("_BeamAxial");
                bool axial = a.y > a.x;
                Color c = m.GetColor("_EmissionColor");
                return new BeamLook
                {
                    R0 = p.x, R1 = p.y, Half = p.z, Len = p.w, FeatherMin = f.x, FeatherPx = f.y, ChordFeather = f.z,
                    AxialIn = Math.Max(Math.Min(f.w, 0.25 * p.w), 1e-3), AxOut0 = axial ? a.x : 0.55 * p.w, AxOut1 = axial ? a.y : p.w,
                    Ext = s.x, Near0 = s.y, Near1 = s.z, Sat = s.w,
                    G = m.GetFloat("_PhaseG"), Density = density, ColourLuma = 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b,
                };
            }
        }

        static double S5(double x)
        {
            x = x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x);
            return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
        }

        static void ClipHalf(double a, double b, ref double tIn, ref double tOut)
        {
            if (Math.Abs(b) < 1e-7) { if (a < 0.0) { tIn = 1.0; tOut = -1.0; } return; }
            double t = -a / b;
            if (b > 0.0) tIn = Math.Max(tIn, t); else tOut = Math.Min(tOut, t);
        }

        /// <summary>Gauss-Legendre nodes and weights on [-1, 1] (Newton iteration on P_n).</summary>
        static void GaussLegendre(int n, double[] x, double[] w)
        {
            for (int i = 0; i < n; i++)
            {
                double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5)), pp = 1.0;
                for (int it = 0; it < 100; it++)
                {
                    double p1 = 1.0, p2 = 0.0;
                    for (int j = 1; j <= n; j++) { double p3 = p2; p2 = p1; p1 = ((2.0 * j - 1.0) * z * p2 - (j - 1.0) * p3) / j; }
                    pp = n * (z * p1 - p2) / (z * z - 1.0);
                    double z1 = z;
                    z = z1 - p1 / pp;
                    if (Math.Abs(z - z1) < 1e-15) break;
                }
                x[i] = z;
                w[i] = 2.0 / ((1.0 - z * z) * pp * pp);
            }
        }

        /// <summary>BeamFrag's integral of one wedge (mode 0, no scene depth) for a ray o + t d in the rotor frame.</summary>
        static double WedgeIntegral(in BeamLook b, double[] o, double[] d, double[] L, double phi, double tNear, double fogK, double pixelAngle,
            double[] gx, double[] gw)
        {
            double kx = L[0] / L[2], ky = L[1] / L[2];
            double x0 = o[0] - kx * o[2], y0 = o[1] - ky * o[2], xd = d[0] - kx * d[2], yd = d[1] - ky * d[2];
            double s0 = o[2] / L[2], sd = d[2] / L[2];
            double n0x = Math.Sin(phi + b.Half), n0y = -Math.Cos(phi + b.Half);
            double n1x = -Math.Sin(phi - b.Half), n1y = Math.Cos(phi - b.Half);
            double n2x = Math.Cos(phi), n2y = Math.Sin(phi);
            double tIn = -1e7, tOut = 1e7;
            ClipHalf(n0x * x0 + n0y * y0, n0x * xd + n0y * yd, ref tIn, ref tOut);
            ClipHalf(n1x * x0 + n1y * y0, n1x * xd + n1y * yd, ref tIn, ref tOut);
            ClipHalf(n2x * x0 + n2y * y0 - b.R0, n2x * xd + n2y * yd, ref tIn, ref tOut);
            ClipHalf(b.R1 - (n2x * x0 + n2y * y0), -(n2x * xd + n2y * yd), ref tIn, ref tOut);
            ClipHalf(s0, sd, ref tIn, ref tOut);
            ClipHalf(b.Len - s0, -sd, ref tIn, ref tOut);
            if (tOut <= tIn) return 0.0;
            bool nearOn = b.Near1 > b.Near0;
            double ta = Math.Max(tIn, tNear);
            if (nearOn) ta = Math.Max(ta, b.Near0);
            double tb = tOut;
            if (tb <= ta) return 0.0;
            double half = 0.5 * (tb - ta), mid = 0.5 * (tb + ta), sum = 0.0;
            for (int i = 0; i < gx.Length; i++)
            {
                double t = mid + half * gx[i];
                double x = x0 + t * xd, y = y0 + t * yd, s = s0 + t * sd, r = n2x * x + n2y * y;
                double f = Math.Max(b.FeatherMin, b.FeatherPx * t * pixelAngle), fc = Math.Max(f, b.ChordFeather);
                double dens = S5((n0x * x + n0y * y) / f) * S5((n1x * x + n1y * y) / f) * S5((r - b.R0) / fc) * S5((b.R1 - r) / fc)
                              * S5(s / b.AxialIn) * (1.0 - S5((s - b.AxOut0) / (b.AxOut1 - b.AxOut0)));
                if (b.Ext > 0.0) dens *= Math.Exp(-Math.Max(s, 0.0) / b.Ext);
                if (nearOn) dens *= S5((t - b.Near0) / (b.Near1 - b.Near0));
                double fx = fogK * t;
                sum += gw[i] * dens * Math.Exp(-fx * fx);
            }
            double integral = sum * half;
            if (b.Sat > 0.0) integral = b.Sat * (1.0 - Math.Exp(-integral / b.Sat));
            return integral;
        }

        /// <summary>
        /// Linear beam radiance (luma) of every wedge over a cols x rows grid of a 16:9 key-shot frame at fan angle thetaDeg,
        /// with the fog of the given row (no scene depth: the fan pose sees sky or far walls behind the beams).
        /// </summary>
        static double[] BeamFrame(in BeamLook b, KeyShot ks, double thetaDeg, int cols, int rows, LookRow fog, int samples)
        {
            var gx = new double[samples];
            var gw = new double[samples];
            GaussLegendre(samples, gx, gw);
            var img = new double[cols * rows];
            Quaternion q = ks.Rotation;
            Vector3 fw = q * Vector3.forward, rt = q * Vector3.right, up = q * Vector3.up;
            double tanH = Math.Tan(0.5 * ks.Fov * Math.PI / 180.0), aspect = 16.0 / 9.0, pixelAngle = 2.0 * tanH / 1080.0;
            double th = thetaDeg * Math.PI / 180.0, c = Math.Cos(th), sn = Math.Sin(th);
            Vector3 rel = ks.Eye - AbyssConst.FAN_CENTRE;
            double[] o = { c * rel.x + sn * rel.y, -sn * rel.x + c * rel.y, rel.z };   // world -> rotor frame (rotate by -theta)
            Vector3 lw = AbyssConst.Dir(0f, -AbyssConst.BEAM_PITCH_DEG);
            double[] L = { c * lw.x + sn * lw.y, -sn * lw.x + c * lw.y, lw.z };
            double g2 = b.G * b.G;
            var d = new double[3];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    double u = ((i + 0.5) / cols * 2.0 - 1.0) * tanH * aspect, v = (1.0 - (j + 0.5) / rows * 2.0) * tanH;
                    double wx = fw.x + u * rt.x + v * up.x, wy = fw.y + u * rt.y + v * up.y, wz = fw.z + u * rt.z + v * up.z;
                    double inv = 1.0 / Math.Sqrt(wx * wx + wy * wy + wz * wz);
                    wx *= inv; wy *= inv; wz *= inv;
                    d[0] = c * wx + sn * wy; d[1] = -sn * wx + c * wy; d[2] = wz;
                    double tNear = 0.1 / Math.Max(wx * fw.x + wy * fw.y + wz * fw.z, 1e-3);
                    double fogK = 0.8326 / fog.D50 * (1.0 + fog.MDown * Math.Max(0.0, -wy));
                    double cosT = -(wx * lw.x + wy * lw.y + wz * lw.z);
                    double den = Math.Max(1.0 + g2 - 2.0 * b.G * cosT, 1e-4);
                    double phase = (1.0 - g2) / (den * Math.Sqrt(den));                      // 4 pi HG
                    double sum = 0.0;
                    for (int k = 0; k < AbyssConst.BEAM_COUNT; k++)
                    {
                        double phi = (Beat06_Fan.GAP_PHASE_DEG + 360.0 / AbyssConst.BEAM_COUNT * k) * Math.PI / 180.0;
                        sum += WedgeIntegral(b, o, d, L, phi, tNear, fogK, pixelAngle, gx, gw);
                    }
                    img[j * cols + i] = b.Density * b.ColourLuma * phase * sum;
                }
            }
            return img;
        }

        static double Display(double linear)
        {
            double x = Math.Min(1.0, Math.Max(0.0, linear));
            return 255.0 * (x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(x, 1.0 / 2.4) - 0.055);
        }

        [Test]
        public void BeamShapeIsSetOnTheFanBeamsOnly()
        {
            Material baseMat = _lib[MatId.BeamWedge];
            Assert.AreEqual(Vector4.zero, baseMat.GetVector("_BeamShape"), "the base material (gallery doorway spill) keeps the unshaped beam");
            Assert.AreEqual(Vector4.zero, baseMat.GetVector("_BeamAxial"), "the base material keeps the default axial window");
            Assert.AreEqual(0f, baseMat.GetFloat("_BeamBlendOp"), "the base material is additive");
            Beat06_Fan b = Build(BeatTier.Full);
            Material bm = RendererNamed(b, "FanBeams").sharedMaterial;
            Assert.AreEqual(0f, bm.GetFloat("_BeamBlendOp"), "the light wedges are additive");
            Assert.AreEqual(Beat06_Fan.BeamShapeAt(GameState.RideSeconds), bm.GetVector("_BeamShape"), "material shape = the ride-second look");
            Assert.AreEqual(Beat06_Fan.BeamFeatherAt(GameState.RideSeconds), bm.GetVector("_BeamFeather"), "material feathers = the ride-second look");
            Assert.AreEqual(Beat06_Fan.BeamAxial, bm.GetVector("_BeamAxial"), "material axial window");
            Assert.AreEqual(Beat06_Fan.BeamDensityFull * Beat06_Fan.BeamHaze(GameState.RideSeconds), bm.GetFloat("_EmissionStrength"), 1e-7f,
                "the material carries density x ride haze");

            foreach (float ride in new[] { 75f, 200f })   // the ride look (KS5b) and the chamber look (beat 6, KS6)
            {
                Vector4 s = Beat06_Fan.BeamShapeAt(ride);
                Assert.Greater(s.z, s.y, ride + ": near-camera fade on");
                Assert.GreaterOrEqual(s.y, 0f);
                Assert.Greater(s.w, 0f, ride + ": end-on chords saturate");
                Vector4 f = Beat06_Fan.BeamFeatherAt(ride);
                Assert.GreaterOrEqual(f.y, 12f, "side feathers of at least 12 px (14.5.6 beam silhouettes)");
                Assert.GreaterOrEqual(f.z, 12f, "soft radial window (R1-10: no flat card edge at R 70 / 130)");
                Assert.That(f.w, Is.InRange(1f, 0.25f * AbyssConst.BEAM_LENGTH), "fade-in from the fan plane");
            }
            Assert.GreaterOrEqual(Beat06_Fan.BeamFeatherAt(200f).w, 20f, "chamber: the fan's gap silhouettes stay crisp (fade-in)");
            // ride (R2-13): the stretch within 100 m of the eye (the end-on wash inside the annulus) stays dark, while the first
            // ~95 m beyond the gaps are drawn as rays (the eye is about 221 m down the light)
            Vector4 rideShape = Beat06_Fan.BeamShapeAt(75f);
            Assert.Greater(rideShape.y, 100f, "ride: no wash within 100 m of the eye");
            Assert.Less(rideShape.y, 150f, "ride: rays reach out of the gaps (R2-13)");

            // R1-10: the spokes keep their light out over the walls and fade out before the eyes that stand inside the annulus
            Vector4 a = Beat06_Fan.BeamAxial;
            Assert.Greater(a.y, a.x, "axial fade-out window on");
            Assert.GreaterOrEqual(a.x, 50f, "full density to at least 50 m along the light (spokes past the housing ring in KS6)");
            Vector3 l = AbyssConst.Dir(0f, -AbyssConst.BEAM_PITCH_DEG);
            foreach (string tag in new[] { "KS6_gantry_fan", "KS5b_lift_fan" })
            {
                KeyShot ks = KeyShots.All[KeyShots.IndexOf(tag)];
                float sEye = (ks.Eye.z - AbyssConst.FAN_CENTRE.z) / l.z;
                Assert.Less(a.y, sEye, tag + ": the wedges are dark around the eye (s " + sEye.ToString("F0") + " m)");
            }
            float g = bm.GetFloat("_PhaseG");
            Assert.That(g, Is.InRange(0f, 0.9f), "HG phase g");

            // the ride haze: a fraction of the chamber density in the lift shaft, at least half of it while the fan is the
            // subject of the ride (R2-13: from ride second 60, KS5b at 75), full from ride second 120 (the 5-to-6 window);
            // the shape blends with the chamber weight
            float rideHaze = Beat06_Fan.BeamRideHaze, fanHaze = Beat06_Fan.BeamRideHazeFan;
            Assert.That(rideHaze, Is.InRange(0.1f, 1f), "ride haze");
            Assert.That(fanHaze, Is.InRange(0.5f, 1f), "R2-13: at least 0.5 of the chamber density while the fan is in view");
            Assert.AreEqual(rideHaze, Beat06_Fan.BeamHaze(-1f), 1e-6f);
            Assert.AreEqual(rideHaze, Beat06_Fan.BeamHaze(45f), 1e-6f, "before the fan is in view");
            Assert.AreEqual(fanHaze, Beat06_Fan.BeamHaze(60f), 1e-6f, "from ride second 60");
            Assert.AreEqual(fanHaze, Beat06_Fan.BeamHaze(75f), 1e-6f, "KS5b");
            Assert.AreEqual(fanHaze, Beat06_Fan.BeamHaze(110f), 1e-6f);
            Assert.AreEqual(1f, Beat06_Fan.BeamHaze(120f), 1e-6f);
            Assert.AreEqual(1f, Beat06_Fan.BeamHaze(140f), 1e-6f, "after the ride (Arrived / Left)");
            float prev = Beat06_Fan.BeamHaze(0f), maxRate = 0f;
            float prevNear = Beat06_Fan.BeamShapeAt(0f).z, maxNearRate = 0f;
            for (int i = 1; i <= 1300; i++)
            {
                float h = Beat06_Fan.BeamHaze(0.1f * i);
                Assert.GreaterOrEqual(h, prev, "monotonic");
                maxRate = Mathf.Max(maxRate, (h - prev) / 0.1f);
                prev = h;
                float n = Beat06_Fan.BeamShapeAt(0.1f * i).z;
                maxNearRate = Mathf.Max(maxNearRate, Mathf.Abs(n - prevNear) / 0.1f);
                prevNear = n;
            }
            Assert.Less(maxRate, 0.15f, "no beam pop: at most 15 % of the chamber density per second");
            Assert.Less(maxNearRate, 40f, "the ride-to-chamber shape blend has no step (smootherstep over 10 s)");
        }

        /// <summary>
        /// R1-10 / R1-11: the key shadow (BeamWedge mode 3) carves the blade shadows out of the fog's key lobe. It must sit on the
        /// blade centres (the light wedges on the gap centres), fill the rest of the 30 degree pitch, span hub to tip, subtract
        /// (ReverseSubtract), never remove more than the lobe (strength 0..1), ride on the rotor and exist on the Full tier only.
        /// </summary>
        [Test]
        public void KeyShadowCarvesTheBladeShadowsOnTheFullTier()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Renderer sr = RendererNamed(b, "FanKeyShadow");
            Material sm = sr.sharedMaterial;
            Assert.AreEqual(3f, sm.GetFloat("_Recipe"), "mode 3 (key shadow)");
            Assert.AreEqual((float)BlendOp.ReverseSubtract, sm.GetFloat("_BeamBlendOp"), "subtracts from the frame");
            Assert.That(sm.GetFloat("_EmissionStrength"), Is.InRange(0.01f, 1f), "removes at most the lobe the fog added");
            Vector4 p = sm.GetVector("_BeamParams");
            Assert.LessOrEqual(p.x, AbyssConst.FAN_R_HUB, "from inside the hub (the hub shadows the key too)");
            Assert.GreaterOrEqual(p.y, AbyssConst.FAN_R_TIP, "at least to the blade tips");
            Assert.GreaterOrEqual(sm.GetVector("_BeamFeather").y, 12f, "side feathers of at least 12 px");
            Assert.GreaterOrEqual(sm.GetVector("_BeamFeather").z, 12f, "soft radial ends");
            double shadowDeg = 2.0 * p.z * 180.0 / Math.PI;
            Assert.That(shadowDeg + AbyssConst.BEAM_WIDTH_DEG, Is.EqualTo(Pitch).Within(1e-3), "light wedge + shadow wedge = one blade pitch");
            Vector4 d = sm.GetVector("_BeamDir"), dl = RendererNamed(b, "FanBeams").sharedMaterial.GetVector("_BeamDir");
            Assert.AreEqual((Vector3)dl, (Vector3)d, "the shadow follows the light");
            Assert.AreEqual(Child(b, "Rotor"), sr.transform.parent, "the shadows rotate rigidly with the blades");
            Assert.AreEqual(ShadowCastingMode.Off, sr.shadowCastingMode);
            Assert.AreEqual(sm.renderQueue, RendererNamed(b, "FanBeams").sharedMaterial.renderQueue, "same explicit transparent queue");

            // the prism centres (UV1.x of the mesh) are the blade centres: 30 k degrees, i.e. 15 degrees off the light wedges
            Mesh shadowMesh = sr.GetComponent<MeshFilter>().sharedMesh;
            var uv1 = new List<Vector4>();
            shadowMesh.GetUVs(1, uv1);
            var centres = new HashSet<int>();
            foreach (Vector4 u in uv1)
            {
                double deg = u.x * 180.0 / Math.PI;
                Assert.That(Wrap180(deg - Pitch * Math.Round(deg / Pitch)), Is.EqualTo(0.0).Within(1e-3), "shadow prism on a blade centre");
                centres.Add((int)Math.Round(deg / Pitch) % Blades);
            }
            Assert.AreEqual(Blades, centres.Count, "one shadow per blade");
            var report = new ScaleReport();
            b.ReportScale(report);
            Assert.That(Wrap180(report.Values["fan.beam.phase.deg"] - 0.5 * Pitch), Is.EqualTo(0.0).Within(1e-3), "light wedges on the gap centres");

            Beat06_Fan simplified = Build(BeatTier.Simplified);
            foreach (Renderer r in simplified.GetComponentsInChildren<Renderer>(true))
                Assert.AreNotEqual("FanKeyShadow", r.gameObject.name, "Simplified tier: 6 wedges and no key shadow");
        }

        static double Luma(Color c) => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;

        /// <summary>
        /// R2-13 (contre-jour from the lift): the bore card is a graded window, brightest behind the hub and toward the top and
        /// darkest at the lower rim (GradientCard mode 0 with the weight in UV0.y on a ring-subdivided disc that faces +Z and
        /// shares the lining's 128-gon at R 150). 14.6: x 0.35 until ride second 0, full over ride seconds 0-20; the fan chamber
        /// returns to a lower level (KS6 band), blended over ride seconds 98-108 while the ride gaze looks down (D1); no step.
        /// </summary>
        [Test]
        public void BoreCardIsAGradedWindowBrightestBehindTheHubAndTop()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Material m = RendererNamed(b, "BoreCard").sharedMaterial;
            Assert.AreEqual(0f, m.GetFloat("_CardMode"), "vertical-gradient mode (the weight is authored in UV0.y)");
            Assert.AreEqual(1f, m.GetFloat("_CardFog"), "fogged like every surface of the shaft");
            double core = Luma(m.GetColor("_CardTop").linear), rim = Luma(m.GetColor("_CardBottom").linear);
            Assert.Greater(core, 1.8 * rim, "a pale core and a dim rim (contre-jour gradient)");

            Mesh mesh = Filter(b, "BoreCard").sharedMesh;
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            var uv = new List<Vector2>();
            mesh.GetUVs(0, uv);
            Vector3 fc = AbyssConst.FAN_CENTRE;
            double rMax = 0.0;
            foreach (Vector3 p in v) rMax = Math.Max(rMax, Math.Sqrt((double)(p.x - fc.x) * (p.x - fc.x) + (double)(p.y - fc.y) * (p.y - fc.y)));
            Assert.That(rMax, Is.EqualTo(AbyssConst.FAN_R_OUT).Within(1e-3), "the card fills the R 150 bore");
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Assert.Greater(n.z, 0f, "every card triangle faces +Z (culled from outside)");
            }
            // weights: brighter inward at every angle, brighter upward at every radius
            Assert.Greater(Beat06_Fan.BoreCardWeight(45f, 0f), Beat06_Fan.BoreCardWeight(135f, 0f), "brighter behind the hub");
            Assert.Greater(Beat06_Fan.BoreCardWeight(90f, 80f), Beat06_Fan.BoreCardWeight(90f, -80f), "brighter toward the top");
            for (int i = 0; i < v.Length; i++)
            {
                float r = Mathf.Sqrt((v[i].x - fc.x) * (v[i].x - fc.x) + (v[i].y - fc.y) * (v[i].y - fc.y));
                Assert.That(uv[i].y, Is.EqualTo(Beat06_Fan.BoreCardWeight(r, v[i].y - fc.y)).Within(1e-4f), "UV0.y carries the weight");
                Assert.That(uv[i].y, Is.InRange(0f, 1f));
            }

            // strength over the ride
            float full = Beat06_Fan.BoreCardStrength(50f);
            Assert.AreEqual(0.35f * full, Beat06_Fan.BoreCardStrength(-1f), 1e-5f, "14.6: x 0.35 until ride second 0");
            Assert.AreEqual(full, Beat06_Fan.BoreCardStrength(20f), 1e-5f, "full from ride second 20");
            Assert.AreEqual(full, Beat06_Fan.BoreCardStrength(97.9f), 1e-5f, "KS5b and the fan hold at full strength");
            float chamber = Beat06_Fan.BoreCardStrength(130f);
            Assert.Less(chamber, 0.6f * full, "the fan chamber (KS6) keeps its mean band");
            Assert.AreEqual(chamber, Beat06_Fan.BoreCardStrength(108.1f), 1e-5f, "chamber level before the gaze returns level (110 s)");
            float prev = Beat06_Fan.BoreCardStrength(0f), maxRate = 0f;
            for (int i = 1; i <= 1400; i++)
            {
                float s = Beat06_Fan.BoreCardStrength(0.1f * i);
                maxRate = Mathf.Max(maxRate, Mathf.Abs(s - prev) / 0.1f);
                prev = s;
            }
            Assert.Less(maxRate, 0.2f * full, "no pop: under 20 % of full strength per second");
            Assert.AreEqual(Beat06_Fan.BoreCardStrength(GameState.RideSeconds, GameState.CameraPos), m.GetFloat("_EmissionStrength"), 1e-5f,
                "material follows the ride second and the eye");

            // R2-06 lead note (KS2b): looking steeply down through the bore (bridge) the card is nearly dark, as f03's
            // FogSky(view) card was (FOG_DOWN); level or from below (the lift at KS5a / KS5b, the gantry) it is at full strength
            KeyShot ks2b = KeyShots.All[KeyShots.IndexOf("KS2b_bridge_down")];
            Assert.LessOrEqual(Beat06_Fan.BoreCardViewFactor(ks2b.Eye), 0.1f, "KS2b: no lit card wedge through the upper gaps");
            Assert.LessOrEqual(Beat06_Fan.BoreCardViewFactor(new Vector3(0f, -1.3f, 216f)), 0.1f, "bridge far end");
            foreach (string tag in new[] { "KS5a_lift_city", "KS5b_lift_fan", "KS6_gantry_fan" })
            {
                KeyShot ks = KeyShots.All[KeyShots.IndexOf(tag)];
                Assert.AreEqual(1f, Beat06_Fan.BoreCardViewFactor(ks.Eye), 1e-4f, tag + ": full card");
            }
            float prevV = Beat06_Fan.BoreCardViewFactor(new Vector3(-103.5f, 0f, 269f));
            for (float y = -2f; y >= -500f; y -= 2f)
            {
                float vf = Beat06_Fan.BoreCardViewFactor(new Vector3(-103.5f, y, 269f));
                Assert.GreaterOrEqual(vf, prevV - 1e-6f, "monotonic over the lift descent");
                Assert.Less(vf - prevV, 0.05f, "no step over 2 m of descent");
                prevV = vf;
            }
        }

        /// <summary>
        /// R2-10 / R2-13 / R2-17: the rotor paint (hub, collars, blades, bolts) carries no rust or stain blobs, is a rough
        /// dielectric (no hot specular line on the sub-pixel blade nose), and is masked from the shaft suns until the tunnel
        /// (contre-jour from the lift, KS5b), while the exterior sun lights the blade backs in beat 7 (KS7d).
        /// </summary>
        [Test]
        public void RotorPaintIsMatteAndMaskedFromTheShaftSun()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            foreach (string n in new[] { "Blades", "Hub" })
            {
                Material m = RendererNamed(b, n).sharedMaterial;
                Assert.AreEqual(0f, m.GetFloat("_RustAmount"), n + ": no rust or stain blobs (R2-10)");
                Assert.GreaterOrEqual(m.GetFloat("_Roughness"), 0.75f, n + ": rough paint (R2-17)");
                Assert.LessOrEqual(m.GetFloat("_Metallic"), 0.1f, n + ": dielectric paint (R2-17)");
                Assert.LessOrEqual(m.GetFloat("_WearAmount"), 0.35f, n + ": no edge strip turns into a bare-steel mirror line (R2-17, KS7d)");
                Assert.AreEqual(Beat06_Fan.RotorWear, m.GetFloat("_WearAmount"), 1e-6f);
                Assert.AreEqual(Beat06_Fan.RotorKeyMask(GameState.E), m.GetFloat("_KeyLightMask"), 1e-6f, n + ": mask follows E");
            }
            Material stator = RendererNamed(b, "FanStator").sharedMaterial;
            Assert.GreaterOrEqual(stator.GetFloat("_Roughness"), 0.75f, "stator: rough paint (R2-17: the strut edge line in KS7d)");
            Assert.LessOrEqual(stator.GetFloat("_Metallic"), 0.1f, "stator: dielectric paint");
            // the shaft suns are dimmed on the rotor (contre-jour) but not removed: at 0 the blades were flat cut-outs (lead_b01)
            float shaft = Beat06_Fan.RotorKeyShaft;
            Assert.That(shaft, Is.InRange(0.25f, 0.6f), "dimmed, with the hub and blade modelling kept");
            Assert.AreEqual(shaft, Beat06_Fan.RotorKeyMask(0f), 1e-6f, "bridge and ledge");
            Assert.AreEqual(shaft, Beat06_Fan.RotorKeyMask(ProgressTables.ERide(75f)), 1e-6f, "KS5b: blades in contre-jour");
            Assert.AreEqual(shaft, Beat06_Fan.RotorKeyMask(ProgressTables.EPost(AbyssConst.BEAT_START_D[6])), 1e-6f, "to the tunnel");
            Assert.AreEqual(1f, Beat06_Fan.RotorKeyMask(ProgressTables.EPost(AbyssConst.BEAT_START_D[6] + 20f)), 1e-6f,
                "beat 7: the exterior sun lights the blade backs (KS7d)");
            float prev = Beat06_Fan.RotorKeyMask(0f);
            for (float e = 0f; e < 950f; e += 0.25f)
            {
                float k = Beat06_Fan.RotorKeyMask(e);
                Assert.GreaterOrEqual(k, prev, "monotonic");
                prev = k;
            }

            float saved = GameState.E;
            try
            {
                GameState.E = ProgressTables.EPost(760f);
                b.PrepareCapture(0.0);
                Assert.AreEqual(1f, RendererNamed(b, "Blades").sharedMaterial.GetFloat("_KeyLightMask"), 1e-6f, "the capture hook applies E");
                GameState.E = ProgressTables.ERide(75f);
                b.PrepareCapture(0.0);
                Assert.AreEqual(Beat06_Fan.RotorKeyMask(GameState.E), RendererNamed(b, "Blades").sharedMaterial.GetFloat("_KeyLightMask"), 1e-6f);
            }
            finally
            {
                GameState.E = saved;
                b.PrepareCapture(0.0);
            }

            // R2-17: no blade vertex carries the 45-degree chamfer mark (G >= 0.97) that makes the sub-pixel bevel resolve bend
            // the round nose's normal onto a sun-facing axis face (the hot 1-px edge line of KS7d)
            Color32[] col = Filter(b, "FanBlades").sharedMesh.colors32;
            Assert.Greater(col.Length, 0, "blade vertex colours");
            int maxG = 0;
            foreach (Color32 c in col) maxG = Math.Max(maxG, c.g);
            Assert.Less(maxG / 255f, 0.97f, "blade edge wear below the chamfer mark");
            Assert.Greater(maxG / 255f, 0.5f, "the erosion strip still exists");
        }

        /// <summary>
        /// 14.5.6 / R2-28: the 12 far-wall patches lift the fogged far wall by about +0.08 luma seen from the gantry look-back
        /// (linear radiance x the beat-6 fog transmittance at the look-back distance, before exposure, about 0.02-0.06); the
        /// lift shaft keeps a quarter of it (8x clearer air). R2-15: the shader widens the 6 m feather to at least 12 px, and
        /// the prism caps keep 12 px as well (BEAM_MIN_PX in BeamWedge.shader).
        /// </summary>
        [Test]
        public void WallPatchesReachTheSpecLiftWithSoftEdges()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Material pm = RendererNamed(b, "FarWallPatches").sharedMaterial;
            Assert.AreEqual(1f, pm.GetFloat("_Recipe"), "wall-patch mode");
            Assert.AreEqual(6f, pm.GetVector("_BeamPatch").x, 1e-4f, "feather 6 m (14.5.6), widened to 12 px by the shader");
            Assert.GreaterOrEqual(pm.GetVector("_BeamFeather").y, 12f, "12 px minimum");
            Assert.AreEqual(Beat06_Fan.PatchFeatherPx, pm.GetVector("_BeamFeather").y, 1e-6f);

            float full = Beat06_Fan.PatchStrength(200f);
            Assert.AreEqual(full, Beat06_Fan.PatchStrength(120f), 1e-6f);
            Assert.AreEqual(0.3f, Beat06_Fan.PatchStrength(75f), 1e-3f, "lift shaft: the f03 radiance (clearer air)");
            Assert.Less(Beat06_Fan.PatchStrength(75f), 0.5f * full);
            Assert.AreEqual(Beat06_Fan.PatchStrength(GameState.RideSeconds), pm.GetFloat("_EmissionStrength"), 1e-6f);

            Vector3 to = KeyShots.FarWallPatchCentre - KeyShots.Beat6LookBackEye;
            LookRow fog = ProgressTables.Rows[ProgressTables.ROW_B6];
            double down = Math.Max(0.0, -to.normalized.y);
            double k = 0.8326 / fog.D50 * to.magnitude * (1.0 + fog.MDown * down);
            double lift = full * Luma(pm.GetColor("_EmissionColor")) * Math.Exp(-k * k);
            Assert.That(lift, Is.InRange(0.02, 0.06), "linear lift over the far wall from the look-back (about +0.08 display)");

            string src = System.IO.File.ReadAllText("Assets/_Game/Shaders/BeamWedge.shader");
            Assert.IsTrue(src.Contains("#define BEAM_MIN_PX 12.0"), "12 px minimum boundary gradient");
            Assert.IsTrue(src.Contains("max(_BeamFeather.y, BEAM_MIN_PX) * footprint"), "patch feather widened to 12 px of the wall footprint");
            Assert.IsTrue(src.Contains("float aIn = max(axialIn, min(pxT, 0.25 * len));"), "axial caps keep 12 px");
        }

        /// <summary>Lead decision D4 (R2-21): the small amber beacons and their glow sprites emit at most 2.5 (they stay amber).</summary>
        [Test]
        public void SmallAmberEmittersStayAmber()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Assert.LessOrEqual(Beat06_Fan.AMBER_SMALL_EMISSIVE, 2.5f, "D4 cap");
            Material m = RendererNamed(b, "AmberLamps").sharedMaterial;
            Assert.LessOrEqual(m.GetFloat("_EmissionStrength"), 2.5f, "beacon boxes");
        }

        /// <summary>
        /// R1-11: ThinLine widens per vertex from the vertex's camera distance, so a long member must carry vertex rings along
        /// its length (a 199 m rail with rings only at its ends was widened beside the viewer for the ~100 m distance of its
        /// ends: 3x too thick at ~30 % coverage). Every ThinLine member of the rails, stays and service pipes has rings at most
        /// 4 m apart along its axis.
        /// </summary>
        [Test]
        public void LongThinLineMembersHaveRingsEveryFourMetres()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            foreach (string name in new[] { "GantryRails", "StayCables", "ServicePipes" })
            {
                Mesh m = Filter(b, name).sharedMesh;
                Vector3[] v = m.vertices;
                var uv1 = new List<Vector4>();
                var uv2 = new List<Vector4>();
                m.GetUVs(1, uv1);
                m.GetUVs(2, uv2);
                Assert.AreEqual(v.Length, uv1.Count, name + ": ThinLine UV1 on every vertex");
                Assert.AreEqual(v.Length, uv2.Count, name + ": ThinLine UV2 on every vertex");
                var members = new Dictionary<Vector3Int, List<double>>();
                var axes = new Dictionary<Vector3Int, Vector3>();
                for (int i = 0; i < v.Length; i++)
                {
                    if (uv1[i].w <= 0f || uv2[i].w < 0.5f) continue;
                    var key = new Vector3Int(Mathf.RoundToInt(uv2[i].x * 100f), Mathf.RoundToInt(uv2[i].y * 100f), Mathf.RoundToInt(uv2[i].z * 100f));
                    var axis = new Vector3(uv1[i].x, uv1[i].y, uv1[i].z);
                    if (!members.TryGetValue(key, out List<double> list)) { list = new List<double>(); members[key] = list; axes[key] = axis; }
                    list.Add(Vector3.Dot(v[i] - new Vector3(uv2[i].x, uv2[i].y, uv2[i].z), axes[key]));
                }
                int longMembers = 0;
                foreach (KeyValuePair<Vector3Int, List<double>> kv in members)
                {
                    List<double> t = kv.Value;
                    t.Sort();
                    double len = t[t.Count - 1] - t[0], maxGap = 0.0;
                    for (int i = 1; i < t.Count; i++) maxGap = Math.Max(maxGap, t[i] - t[i - 1]);
                    if (len > 10.0) longMembers++;
                    Assert.LessOrEqual(maxGap, 4.0 + 1e-3, name + ": ring spacing along a " + len.ToString("F1") + " m member");
                }
                Assert.Greater(longMembers, 1, name + ": long members found");
            }
        }

        /// <summary>
        /// 14.5.6: "12 samples versus 64 samples differ by at most 1.5/255 mean absolute error in KS6 and KS5b", on a CPU mirror
        /// of the mode-0 integral (shape, feathers, fog transmittance, phase) over a 64 x 36 grid of each frame at three fan
        /// angles, displayed over a dark 0.02 linear scene; and the spokes keep dark gaps: on an arc around the fan centre in
        /// KS6 the darkest point carries at most 10 % of the brightest spoke.
        /// </summary>
        [Test]
        public void TwelveSamplesMatchSixtyFourAndTheSpokesKeepDarkGaps()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            Material bm = RendererNamed(b, "FanBeams").sharedMaterial;
            const int cols = 64, rows = 36;
            foreach (string tag in new[] { "KS6_gantry_fan", "KS5b_lift_fan" })
            {
                KeyShot ks = KeyShots.All[KeyShots.IndexOf(tag)];
                bool ride = ks.OnLift;
                LookRow fog = ProgressTables.Rows[ride ? ProgressTables.ROW_B5 : ProgressTables.ROW_B6];
                BeamLook look = BeamLook.At(bm, ride ? ks.RideSeconds : 200f);
                double theta0 = Beat06_Fan.ThetaDeg(ks.Clock);
                for (int a = 0; a < 3; a++)
                {
                    double theta = theta0 + 10.0 * a;
                    double[] i12 = BeamFrame(look, ks, theta, cols, rows, fog, 12);
                    double[] i64 = BeamFrame(look, ks, theta, cols, rows, fog, 64);
                    double mae = 0.0, peak = 0.0;
                    for (int p = 0; p < i12.Length; p++)
                    {
                        mae += Math.Abs(Display(0.02 + i12[p]) - Display(0.02 + i64[p]));
                        peak = Math.Max(peak, i64[p]);
                    }
                    mae /= i12.Length;
                    Assert.Greater(peak, 1e-3, tag + ": the beams are visible");
                    Assert.LessOrEqual(mae, 1.5, $"{tag} theta {theta:F1}: 12 vs 64 samples MAE {mae:F3} levels");
                }
            }

            // dark gaps between the spokes (KS6): an arc 0.3 frame heights around the projected fan centre, upper half
            {
                KeyShot ks = KeyShots.All[KeyShots.IndexOf("KS6_gantry_fan")];
                BeamLook look = BeamLook.At(bm, 200f);
                const int c6 = 192, r6 = 108;
                double[] img = BeamFrame(look, ks, Beat06_Fan.ThetaDeg(ks.Clock), c6, r6, ProgressTables.Rows[ProgressTables.ROW_B6], 12);
                Quaternion q = ks.Rotation;
                Vector3 v = AbyssConst.FAN_CENTRE - ks.Eye;
                double tanH = Math.Tan(0.5 * ks.Fov * Math.PI / 180.0);
                double z = Vector3.Dot(v, q * Vector3.forward);
                double cx = (Vector3.Dot(v, q * Vector3.right) / z / (tanH * 16.0 / 9.0) + 1.0) * 0.5 * c6;
                double cy = (1.0 - Vector3.Dot(v, q * Vector3.up) / z / tanH) * 0.5 * r6;
                double lo = double.MaxValue, hi = 0.0;
                for (int k = 0; k <= 200; k++)
                {
                    double ang = (20.0 + 140.0 * k / 200.0) * Math.PI / 180.0;
                    int x = (int)(cx + 0.3 * r6 * Math.Cos(ang)), y = (int)(cy - 0.3 * r6 * Math.Sin(ang));
                    if (x < 0 || x >= c6 || y < 0 || y >= r6) continue;
                    lo = Math.Min(lo, img[y * c6 + x]);
                    hi = Math.Max(hi, img[y * c6 + x]);
                }
                Assert.Greater(hi, 1e-3, "spokes around the fan in KS6");
                Assert.LessOrEqual(lo, 0.1 * hi, "dark gaps between the spokes in KS6");
            }
        }

        [Test]
        public void GantryIs199By3()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            MeshFilter deck = Filter(b, "GantryDeck");
            Bounds lb = deck.sharedMesh.bounds;
            Assert.That(lb.size.z, Is.EqualTo(AbyssConst.GANTRY_L).Within(0.1), "gantry length");
            Assert.That(lb.size.x, Is.EqualTo(AbyssConst.GANTRY_W).Within(0.02), "gantry width");
            Bounds wb = deck.GetComponent<Renderer>().bounds;
            Assert.That(wb.max.z, Is.EqualTo(AbyssConst.GANTRY_Z0).Within(1e-3), "gantry starts at the gate, z = 259");
            Assert.That(wb.min.z, Is.EqualTo(AbyssConst.GANTRY_Z1).Within(1e-3), "gantry ends at the tunnel mouth, z = 60");
            Assert.That(wb.center.x, Is.EqualTo(AbyssConst.W[14].x).Within(1e-3), "gantry centred on x = -45");
            Assert.That(wb.max.y, Is.GreaterThan(AbyssConst.GANTRY_Y), "kerb toe plates stand above the walking plane");

            var report = new ScaleReport();
            b.ReportScale(report);
            Assert.AreEqual(8.0, report.Values["gantry.portal.count"], 1e-9, "portal frames");
            Assert.That(report.Values["gantry.portal.width"], Is.EqualTo(6.0).Within(0.03), "portal width (post centres)");
            Assert.That(report.Values["gantry.portal.height"], Is.EqualTo(8.0).Within(0.04), "portal height (beam top above the deck)");
            Assert.That(report.Values["gantry.truss.depth"], Is.EqualTo(4.0).Within(0.02), "truss depth (chord centres)");
            Assert.AreEqual(33.0, report.Values["gantry.truss.bays"], 1e-9, "truss bays");
            Assert.AreEqual(16.0, report.Values["gantry.cables"], 1e-9, "two stay cables per portal frame");
        }

        [Test]
        public void FanTurnsClockwiseSeenFromPlusZWithPeriod40()
        {
            Assert.That(Beat06_Fan.ThetaDeg(0.0), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(Beat06_Fan.ThetaDeg(10.0), Is.EqualTo(90.0).Within(1e-9));
            Assert.That(Beat06_Fan.ThetaDeg(40.0), Is.EqualTo(0.0).Within(1e-9), "period 40 s");
            Assert.That(Beat06_Fan.ThetaDeg(1800.0 + 5.0), Is.EqualTo(45.0).Within(1e-6), "no drift after 30 minutes");

            Beat06_Fan b = Build(BeatTier.Full);
            b.PrepareCapture(10.0);
            Transform rotor = Child(b, "Rotor");
            Vector3 x = rotor.localRotation * Vector3.right;
            Assert.That(x.y, Is.EqualTo(1f).Within(1e-4f), "after a quarter period +X has turned to +Y (clockwise seen from +Z)");
            var report = new ScaleReport();
            b.ReportScale(report);
            Assert.That(report.Values["fan.period"], Is.EqualTo(AbyssConst.FAN_PERIOD).Within(0.05), "period");
        }

        [Test]
        public void FullTierReportsEveryAcceptanceDimension()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            var report = new ScaleReport();
            b.ReportScale(report);
            void Check(string key, double value, double tol)
            {
                Assert.IsTrue(report.Values.TryGetValue(key, out double v), "missing " + key);
                Assert.That(v, Is.EqualTo(value).Within(tol), key);
            }
            Check("fan.housing.diameter", 300.0, 0.5);
            Check("fan.blades", 12.0, 1e-9);
            Check("fan.tip.radius", 138.0, 0.2);
            Check("fan.blade.arc.deg", 12.0, 0.1);
            Check("fan.gap.deg", 18.0, 0.5);
            Check("fan.gap.min.deg", 18.0, 0.5);
            Check("fan.gap.max.deg", 18.0, 0.5);
            Check("fan.hub.radius", AbyssConst.FAN_R_HUB, 0.2);
            Check("fan.period", 40.0, 0.05);
            Check("fan.beam.width.deg", 12.0, 0.1);
            Check("fan.beam.pitch.deg", 18.0, 0.1);
            Check("gantry.length", 199.0, 0.1);
            Check("gantry.width", 3.0, 0.02);
            Check("gantry.z0", AbyssConst.GANTRY_Z0, 1e-3);
        }

        [Test]
        public void FullTierMeshesAreValidAndInsideTheBudget()
        {
            Beat06_Fan b = Build(BeatTier.Full);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            Assert.Greater(list.Count, 10, "full tier meshes");
            long triangles = 0;
            foreach (Mesh m in list)
            {
                MeshReport r = MeshValidation.Validate(m);
                Assert.IsTrue(r.Ok, r.Describe());
                triangles += r.TriangleCount;
            }
            Assert.LessOrEqual(triangles, 800000, "beat 6 visible triangle budget (14.7.8)");
            Assert.AreEqual(ShadowCastingMode.On, RendererNamed(b, "Blades").shadowCastingMode, "blades cast the moving shadows on the gantry");
        }

        [Test]
        public void SimplifiedTierHasSixWedgesAndStaticLitBlades()
        {
            Beat06_Fan b = Build(BeatTier.Simplified);
            var report = new ScaleReport();
            b.ReportScale(report);
            Assert.AreEqual(6.0, report.Values["fan.beam.count"], 1e-9, "6 wedges");
            Assert.That(report.Values["fan.gap.deg"], Is.EqualTo(18.0).Within(0.5), "gap");
            Assert.AreEqual(ShadowCastingMode.Off, RendererNamed(b, "Blades").shadowCastingMode, "static-lit blades");
            foreach (Mesh m in _meshes)
            {
                MeshReport r = MeshValidation.Validate(m);
                Assert.IsTrue(r.Ok, r.Describe());
            }
        }

        [Test]
        public void EveryTierKeepsTheGantryCorridor()
        {
            foreach (BeatTier tier in new[] { BeatTier.Full, BeatTier.Simplified, BeatTier.Graybox })
            {
                Beat06_Fan b = Build(tier);
                BoxCollider floor = null;
                foreach (BoxCollider bc in b.GetComponentsInChildren<BoxCollider>(true))
                    if (bc.name == "GantryFloor") floor = bc;
                Assert.IsNotNull(floor, tier + ": no GantryFloor collider");
                Bounds w = new Bounds(floor.transform.TransformPoint(floor.center), Vector3.zero);
                Vector3 e = 0.5f * floor.size;
                for (int c = 0; c < 8; c++)
                    w.Encapsulate(floor.transform.TransformPoint(floor.center + new Vector3((c & 1) == 0 ? -e.x : e.x, (c & 2) == 0 ? -e.y : e.y, (c & 4) == 0 ? -e.z : e.z)));
                Assert.That(w.max.z, Is.EqualTo(AbyssConst.GANTRY_Z0).Within(1e-3f), tier + ": floor starts at z = 259");
                Assert.That(w.min.z, Is.EqualTo(AbyssConst.GANTRY_Z1).Within(1e-3f), tier + ": floor ends at z = 60");
                Assert.That(w.max.y, Is.EqualTo(AbyssConst.GANTRY_Y).Within(1e-3f), tier + ": floor top y = -487");
            }
        }
    }
}
