// EditMode tests of the beat 5-7 geometry (MASTER_PROMPT 14.10.1 T-SCALE-4, T-SCALE-5, T-NORMALS; 14.5.5
// acceptance 1 and 5; 14.5.6 acceptance 1; 14.5.7 acceptance 3; the arm rule of 14.5.6; D-035: the bead column
// above the pier; critique run06 B1 / A9 / C-13 / E9: smooth bead silhouettes; critique run07 A-r2-1 / A5 / A11 / B3 /
// B-r2-5: the walkway / cable material terms). Every check reads the generated meshes or the same layout functions the
// builders use; the bead layout is read from ThreadConstants; shader-only constants are read from their #defines.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Megastructure.Beats;
using Megastructure.EditorTools;

namespace Megastructure.Tests
{
    public class Beat05_07Tests
    {
        readonly List<UnityEngine.Object> m_trash = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_trash.Count; i++) if (m_trash[i] != null) UnityEngine.Object.DestroyImmediate(m_trash[i]);
            m_trash.Clear();
        }

        Mesh Keep(Mesh m) { m_trash.Add(m); return m; }

        Mesh DeckMesh() { var mb = new MeshBuilder(8192); WalkwayLayout.BuildDeck(mb); return Keep(mb.ToMesh("Walkway_Deck")); }
        Mesh RailMesh() { var mb = new MeshBuilder(8192); WalkwayLayout.BuildRail(mb); return Keep(mb.ToMesh("Walkway_Rail")); }
        Mesh PierMesh() { var mb = new MeshBuilder(4096); Beat07_Top.BuildPierPad(mb); return Keep(mb.ToMesh("Top_PierPad")); }
        Mesh LensMesh(int k, bool porch)
        {
            var mb = new MeshBuilder((LensBead.SegmentsAzimuth + 1) * (LensBead.SegmentsProfile + 1) + 256);
            LensBead.BuildLens(mb, k);
            if (porch && LensBead.HasDoor(k)) LensBead.BuildDoorPorch(mb, LensBead.Door(k));
            return Keep(mb.ToMesh("Bead_S" + (k + 1)));
        }

        const double Cx = ThreadConstants.CableAxisX, Cz = ThreadConstants.CableAxisZ;

        static double RadiusXZ(Vector3 v) { double dx = v.x - Cx, dz = v.z - Cz; return Math.Sqrt(dx * dx + dz * dz); }

        /// <summary>Deck vertices lying in the vertical cross-section plane of station s.</summary>
        static List<Vector3> Section(Vector3[] verts, double s, out D3 p, out double rx, out double rz)
        {
            WalkwayLayout.DeckFrame(s, out p, out rx, out rz);
            ThreadMath.Tangent(s, out double tx, out double tz);
            if (s >= ThreadConstants.HelixEndS - 1e-9) { tx = 0; tz = 1; }
            var list = new List<Vector3>();
            for (int i = 0; i < verts.Length; i++)
            {
                double d = (verts[i].x - p.x) * tx + (verts[i].z - p.z) * tz;
                // the pier (y 146) lies 110 m above the helix start in plan: keep only this section's height band
                if (Math.Abs(d) < 2e-3 && Math.Abs(verts[i].y - p.y) < 1.0) list.Add(verts[i]);
            }
            return list;
        }

        // ------------------------------------------------------------------ T-SCALE-4
        [Test]
        public void TScale4_DeckWidthRadiusAndGrade()
        {
            var verts = DeckMesh().vertices;
            double[] stations = { 404, 420, 500, 520, 600, 648, 700, 776, 838, 840 };
            double prevS = double.NaN, prevY = double.NaN;
            foreach (double s in stations)
            {
                var sec = Section(verts, s, out D3 p, out double rx, out double rz);
                Assert.Greater(sec.Count, 8, $"cross-section at s = {s}");
                double lo = double.MaxValue, hi = double.MinValue, top = double.MinValue;
                Vector3 inner = default, outer = default;
                foreach (var v in sec)
                {
                    double lat = (v.x - p.x) * rx + (v.z - p.z) * rz;
                    if (lat < lo) { lo = lat; }
                    if (lat > hi) { hi = lat; }
                    top = Math.Max(top, v.y);
                    if (Math.Abs(v.y - p.y) < 1e-3 && lat > 1.4) inner = v;
                    if (Math.Abs(v.y - p.y) < 1e-3 && lat < -1.4) outer = v;
                }
                Assert.AreEqual(ThreadConstants.DeckWidth, hi - lo, 0.01, $"deck width at s = {s}");
                Assert.AreEqual(ThreadMath.PathY(s), top, 1e-3, $"deck top = path y at s = {s}");
                if (s < ThreadConstants.HelixEndS)
                {
                    var mid = (inner + outer) * 0.5f;
                    Assert.AreEqual(ThreadConstants.HelixRadius, RadiusXZ(mid), 0.02, $"centre-line radius at s = {s}");
                }
                if (!double.IsNaN(prevS) && s - prevS <= 2.0 + 1e-9 && s <= ThreadConstants.HelixEndS)
                    Assert.AreEqual(ThreadConstants.HelixGrade, (top - prevY) / (s - prevS), 0.001, $"grade at s = {s}");
                prevS = s; prevY = top;
            }
            // full grade check along every 2 m station from s = 404
            var st = WalkwayLayout.DeckStations();
            for (int i = 1; i < st.Count; i++)
            {
                if (st[i - 1] < ThreadConstants.GradeBlendEnd - 1e-9) continue;
                WalkwayLayout.DeckFrame(st[i - 1], out D3 a, out _, out _);
                WalkwayLayout.DeckFrame(st[i], out D3 b, out _, out _);
                Assert.AreEqual(ThreadConstants.HelixGrade, (b.y - a.y) / (st[i] - st[i - 1]), 0.001, $"grade {st[i - 1]}..{st[i]}");
            }
        }

        [Test]
        public void TScale4_PlanLengthEndHeightAndCheckPoints()
        {
            var st = WalkwayLayout.DeckStations();
            double plan = 0;
            for (int i = 1; i < st.Count; i++)
            {
                if (st[i - 1] < ThreadConstants.HelixStartS - 1e-9) continue;
                WalkwayLayout.DeckFrame(st[i - 1], out D3 a, out _, out _);
                WalkwayLayout.DeckFrame(st[i], out D3 b, out _, out _);
                plan += Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
            }
            Assert.AreEqual(ThreadConstants.HelixEndS - ThreadConstants.HelixStartS, plan, 0.1, "helix plan length");
            float maxY = float.MinValue;
            foreach (var v in DeckMesh().vertices) maxY = Mathf.Max(maxY, v.y);
            Assert.AreEqual(ThreadConstants.TopY, maxY, 0.05, "deck end y");
            // the deck emerges from the terrace without co-planar faces
            WalkwayLayout.DeckFrame(WalkwayLayout.DeckStartS, out D3 p0, out _, out _);
            Assert.Greater(p0.y - ThreadConstants.TerraceY, 0.003, "deck start above the terrace top");
            // check points of 14.4.2 (printed to 0.1 m; the exact helix differs from the print by up to 0.06 m at s = 648)
            double[,] cp = { { 490, 50.3, 58.5, 467.2 }, { 520, 80.0, 66.0, 469.3 }, { 580, 128.9, 81.0, 437.9 }, { 648, 134.5, 98.0, 372.8 }, { 750, 50.3, 123.5, 332.8 }, { 776, 27.3, 130.0, 344.5 } };
            for (int i = 0; i < cp.GetLength(0); i++)
            {
                WalkwayLayout.DeckFrame(cp[i, 0], out D3 p, out _, out _);
                Assert.AreEqual(cp[i, 1], p.x, 0.08); Assert.AreEqual(cp[i, 2], p.y, 0.08); Assert.AreEqual(cp[i, 3], p.z, 0.08);
            }
        }

        [Test]
        public void TScale4_RailHeight()
        {
            var verts = RailMesh().vertices;
            double[] stations = { 400, 410, 520, 700, 838 };
            foreach (double s in stations)
                for (int side = -1; side <= 1; side += 2)
                {
                    D3 c = WalkwayLayout.RailPoint(s, side);
                    WalkwayLayout.DeckFrame(s, out D3 p, out _, out _);
                    double top = double.MinValue;
                    foreach (var v in verts)
                    {
                        double dx = v.x - c.x, dy = v.y - c.y, dz = v.z - c.z;
                        if (dx * dx + dy * dy + dz * dz < 0.04 * 0.04) top = Math.Max(top, v.y);
                    }
                    Assert.AreEqual(ThreadConstants.RailHeight, top - p.y, 0.01, $"rail height at s = {s}, side {side}");
                }
            Assert.AreEqual(ThreadConstants.RailHeight, WalkwayLayout.RailCentreHeight + 0.5 * ThreadConstants.HandrailDiameter, 1e-9);
        }

        [Test]
        public void TScale4_ArmsCountAndStations()
        {
            var arms = WalkwayLayout.Arms();
            Assert.AreEqual(43, arms.Length);
            Assert.AreEqual(ThreadConstants.ArmCountHelix + ThreadConstants.ArmCountPier, arms.Length);
            for (int i = 0; i < arms.Length; i++)
            {
                Assert.AreEqual(ThreadConstants.HelixStartS + ThreadConstants.ArmSpacing * i, arms[i].s, 1e-9);
                if (i > 0) Assert.AreEqual(ThreadConstants.ArmSpacing, arms[i].s - arms[i - 1].s, 1e-9);
            }
            double[] pierZ = { 400, 411, 422 };
            for (int j = 0; j < 3; j++)
            {
                var a = arms[ThreadConstants.ArmCountHelix + j];
                Assert.AreEqual(pierZ[j], ThreadConstants.PierStartZ + (a.s - ThreadConstants.HelixEndS), 1e-9);
                Assert.AreEqual(-1, a.bead, "pier arms end in the cable");
                // visible length from the slab edge to the ridge crest (14.5.7: 17.3 to 20.8 m)
                double rEdge = a.rTip;
                for (double r = a.rRoot; r < a.rTip; r += 0.005)
                {
                    D3 q = a.Point(r, 0, 0);
                    bool onSlab = (Math.Abs(q.x) <= PierPadOutline.W && q.z >= ThreadConstants.PierStartZ - 0.5 && q.z <= PierPadOutline.FilletZ)
                                  || PierPadOutline.Contains(q.x, q.z);
                    if (onSlab) { rEdge = r; break; }
                }
                double vis = rEdge - ThreadConstants.CableRidgeCrest;
                Assert.That(vis, Is.InRange(17.2, 20.9), $"pier arm {j} visible length");
            }
        }

        // ------------------------------------------------------------------ T-SCALE-5
        [Test]
        public void TScale5_Beads()
        {
            Assert.AreEqual(9, ThreadConstants.BeadCount);
            double a = ThreadConstants.BeadRadius, b = ThreadConstants.BeadHalfHeight;
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                Assert.AreEqual(ThreadConstants.BeadY0 + ThreadConstants.BeadYStep * k, LensBead.BeadY(k), 1e-9);
                var lens = LensMesh(k, false);
                Assert.AreEqual((LensBead.SegmentsAzimuth + 1) * (LensBead.SegmentsProfile + 1), lens.vertexCount, $"bead {k + 1} lathe vertices (seam column duplicated)");
                Assert.AreEqual(2L * LensBead.SegmentsAzimuth * LensBead.SegmentsProfile, MeshKit.TriangleCount(lens), $"bead {k + 1} lathe triangles");
                var verts = lens.vertices;
                double rMax = 0, yAtMax = 0, worst = 0;
                foreach (var v in verts)
                {
                    double r = RadiusXZ(v), yr = v.y - LensBead.BeadY(k);
                    if (r > rMax) { rMax = r; yAtMax = v.y; }
                    worst = Math.Max(worst, Math.Abs((r / a) * (r / a) + (yr / b) * (yr / b) - 1.0));
                }
                Assert.AreEqual(ThreadConstants.BeadRadius, rMax, 0.05, $"bead {k + 1} radius");
                Assert.AreEqual(LensBead.BeadY(k), yAtMax, 0.01, $"bead {k + 1} equator height");
                Assert.Less(worst, 2e-4, $"bead {k + 1} vertices on the ellipsoid r 62 / half-height 12");
            }
            Assert.AreEqual(0.0, LensBead.RingY(LensBead.SegmentsProfile / 2), 1e-12);
            Assert.AreEqual(ThreadConstants.BeadRadius, LensBead.RingR(LensBead.SegmentsProfile / 2), 1e-12);
            Assert.Less(LensBead.CutRadius, ThreadConstants.CableRadius, "lens cut inside the cable core");
            // critique run06 B1 / A9 / C-13 / E9: at least 192 x 48 segments, and a facet count that puts every window
            // of the eye row and of the half-step staggered rows on a facet centre
            Assert.GreaterOrEqual(LensBead.SegmentsAzimuth, 192, "lens azimuth segments");
            Assert.GreaterOrEqual(LensBead.SegmentsProfile, 48, "lens profile segments");
            Assert.AreEqual(0, LensBead.SegmentsAzimuth % (2 * ThreadConstants.WindowsPerRing), "facets per window step even");
            Assert.AreEqual(LensBead.FacetsPerWindowStep * ThreadConstants.WindowsPerRing, LensBead.SegmentsAzimuth);
        }

        /// <summary>Largest distance between the faceted lens and the true ellipsoid: azimuthal chord sagitta at the rim plus the largest profile chord sagitta.</summary>
        static double LensDeviation(out double azimuthal, out double profile)
        {
            double a = ThreadConstants.BeadRadius, b = ThreadConstants.BeadHalfHeight;
            azimuthal = a * (1.0 - Math.Cos(0.5 * LensBead.Delta));
            profile = 0.0;
            for (int i = 0; i < LensBead.SegmentsProfile; i++)
            {
                double r0 = LensBead.RingR(i), y0 = LensBead.RingY(i), r1 = LensBead.RingR(i + 1), y1 = LensBead.RingY(i + 1);
                double er = r1 - r0, ey = y1 - y0, l = Math.Sqrt(er * er + ey * ey);
                double t0 = LensBead.RingTheta(i), t1 = LensBead.RingTheta(i + 1);
                for (int q = 1; q < 32; q++)
                {
                    double t = t0 + (t1 - t0) * q / 32.0, r = a * Math.Cos(t), y = b * Math.Sin(t);
                    profile = Math.Max(profile, Math.Abs((r - r0) * ey - (y - y0) * er) / l);
                }
            }
            return azimuthal + profile;
        }

        /// <summary>Distance from a point to the nearest true bead lens (meridian-plane distance to the ellipse inside the r = 48 cut).</summary>
        static double DistanceToBeads(D3 p)
        {
            double a = ThreadConstants.BeadRadius, b = ThreadConstants.BeadHalfHeight;
            double rp = Math.Sqrt((p.x - Cx) * (p.x - Cx) + (p.z - Cz) * (p.z - Cz));
            double thMax = Math.Acos(LensBead.CutRadius / a), best = double.MaxValue;
            const int n = 2000;
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
                for (int i = 0; i <= n; i++)
                {
                    double t = -thMax + 2.0 * thMax * i / n;
                    double dr = rp - a * Math.Cos(t), dy = p.y - (LensBead.BeadY(k) + b * Math.Sin(t));
                    best = Math.Min(best, Math.Sqrt(dr * dr + dy * dy));
                }
            return best;
        }

        // critique run06 B1: "at the key-shot poses the maximum deviation between the true ellipse and the mesh outline
        // should be under 0.5 px at 4K". Pixel size at 4K (2160 rows) along the view axis: d 2 tan(fov / 2) / 2160;
        // in the 16:9 frame corner a radial deviation covers 1 + tan^2(fov / 2) (1 + (16/9)^2) times more pixels.
        const double Rows4K = 2160.0, Aspect169 = 16.0 / 9.0;
        const double ChordPxCentreMax = 0.25, ChordPxCornerMax = 0.5;

        [Test]
        public void Beads_SilhouetteChordBelowHalfPixelAt4K()
        {
            double dev = LensDeviation(out double az, out double prof);
            Assert.Less(az, 0.005, "azimuthal chord sagitta at the rim");
            Assert.Less(prof, 0.005, "profile chord sagitta");
            double fovMin = double.MaxValue;
            foreach (var shot in ThreadConstants.KeyShots)
            {
                fovMin = Math.Min(fovMin, shot.fov);
                double d = DistanceToBeads(new D3(shot.x, shot.y, shot.z));
                double th = Math.Tan(0.5 * shot.fov * ThreadMath.Deg);
                double px = d * 2.0 * th / Rows4K;
                double corner = 1.0 + th * th * (1.0 + Aspect169 * Aspect169);
                Assert.Less(dev / px, ChordPxCentreMax, $"{shot.tag}: bead outline deviation at the image centre (4K px), nearest bead {d:F1} m");
                Assert.Less(dev / px * corner, ChordPxCornerMax, $"{shot.tag}: bead outline deviation in the 16:9 corner (4K px), nearest bead {d:F1} m");
            }
            // the closest any walker eye gets to a bead: the helix edges and the pier / pad (eye height 1.7 m)
            double dMin = double.MaxValue;
            for (double s = ThreadConstants.HelixStartS; s <= ThreadConstants.HelixEndS; s += 4.0)
                for (int side = -1; side <= 1; side += 2)
                    dMin = Math.Min(dMin, DistanceToBeads(WalkwayLayout.DeckPoint(s, side * WalkwayLayout.HalfWidth, ThreadConstants.EyeHeight)));
            for (double z = ThreadConstants.PierStartZ; z <= ThreadConstants.PadCentreZ + ThreadConstants.PadRadius; z += 2.0)
                dMin = Math.Min(dMin, DistanceToBeads(new D3(0.0, ThreadConstants.TopY + ThreadConstants.EyeHeight, z)));
            double thMin = Math.Tan(0.5 * fovMin * ThreadMath.Deg);
            Assert.Less(dev / (dMin * 2.0 * thMin / Rows4K), ChordPxCentreMax, $"closest walker eye {dMin:F1} m: bead outline deviation at the image centre (4K px)");
        }

        [Test]
        public void TScale5_PierPadAndBeacon()
        {
            var verts = PierMesh().vertices;
            double maxX = double.MinValue, minX = double.MaxValue, padR = 0, minYPier = double.MaxValue, minYPad = double.MaxValue;
            foreach (var v in verts)
            {
                Assert.LessOrEqual(v.y, ThreadConstants.TopY + 1e-4, "single top surface at 146.0");
                if (v.z > 401 && v.z < 420)
                {
                    if (v.y > ThreadConstants.TopY - 0.03) { maxX = Math.Max(maxX, v.x); minX = Math.Min(minX, v.x); }
                    minYPier = Math.Min(minYPier, v.y);
                }
                if (v.z > ThreadConstants.PadCentreZ)
                {
                    double dx = v.x, dz = v.z - ThreadConstants.PadCentreZ;
                    padR = Math.Max(padR, Math.Sqrt(dx * dx + dz * dz));
                }
                if (Math.Abs(v.x) < 0.5 && Math.Abs(v.z - ThreadConstants.PadCentreZ) < 0.5) minYPad = Math.Min(minYPad, v.y);
            }
            Assert.AreEqual(ThreadConstants.DeckWidth, maxX - minX, 0.01, "pier width at y 146");
            Assert.AreEqual(0.0, maxX + minX, 1e-4, "pier centred on x = 0");
            Assert.AreEqual(ThreadConstants.PadRadius, padR, 0.05, "pad radius");
            Assert.AreEqual(ThreadConstants.TopY - ThreadConstants.DeckThickness, minYPier, 1e-3, "pier thickness 0.4");
            Assert.AreEqual(ThreadConstants.TopY - ThreadConstants.PadThickness, minYPad, 1e-3, "pad thickness 0.6");
            // the top vertices that are on the top surface are exactly at 146.0
            int topCount = 0;
            foreach (var v in verts) if (v.y > ThreadConstants.TopY - 1e-3) { Assert.AreEqual((float)ThreadConstants.TopY, v.y); topCount++; }
            Assert.Greater(topCount, 100);

            D3 bb = Beat07_Top.BeaconBase;
            Assert.AreEqual(0.0, bb.x, 0.01); Assert.AreEqual(ThreadConstants.TopY, bb.y, 0.01); Assert.AreEqual(ThreadConstants.PadCentreZ, bb.z, 0.01);
            var pmb = new MeshBuilder(256); Beat07_Top.BuildPillar(pmb);
            var pillar = Keep(pmb.ToMesh("Beacon_Pillar"));
            Assert.AreEqual(0.0, pillar.bounds.center.x, 0.01);
            Assert.AreEqual(ThreadConstants.PadCentreZ, pillar.bounds.center.z, 0.01);
            Assert.AreEqual(ThreadConstants.TopY + ThreadConstants.BeaconPillarHeight, pillar.bounds.max.y, 1e-3, "pillar 1.6 m (D-051)");
            Assert.AreEqual(2.0 * ThreadConstants.BeaconPillarR0, pillar.bounds.size.x, 0.01);
            var cmb = new MeshBuilder(256); Beat07_Top.BuildCrystal(cmb);
            var crystal = Keep(cmb.ToMesh("Beacon_Crystal"));
            Assert.AreEqual(ThreadConstants.BeaconCrystalHeight, crystal.bounds.size.y, 0.005, "crystal 0.5 m tall");
            Assert.AreEqual(0.0, crystal.bounds.center.x, 0.01);
            Assert.AreEqual(ThreadConstants.PadCentreZ, crystal.bounds.center.z, 0.01);
            Assert.Less(crystal.bounds.min.y, pillar.bounds.max.y, "crystal seated in the pillar top");
            for (int h = 0; h < 2; h++)
            {
                double r = h == 0 ? ThreadConstants.BeaconHaloR0 : ThreadConstants.BeaconHaloR1;
                var halo = Keep(Beat07_Top.BuildHaloMesh(r, "halo"));
                Assert.AreEqual(2.0 * r, halo.bounds.size.z, 1e-4, "halo bounds hold the full radius in every axis");
                Assert.AreEqual(2.0 * r, halo.bounds.size.x, 1e-4);
            }
        }

        [Test]
        public void BeadColumn_AboveThePierNoEquatorPassed()
        {
            // D-035 (supersedes D-023): the whole column sits above the pier, so no deck passes a bead equator
            float deckTop = float.MinValue;
            foreach (var v in DeckMesh().vertices) deckTop = Mathf.Max(deckTop, v.y);
            foreach (var v in PierMesh().vertices) deckTop = Mathf.Max(deckTop, v.y);
            double eyeTop = ThreadConstants.TopY + ThreadConstants.EyeHeight;
            Assert.AreEqual(ThreadConstants.TopY, deckTop, 1e-3, "highest walkway surface is the pier / pad top");
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                double lowest = LensBead.BeadY(k) - ThreadConstants.BeadHalfHeight;
                Assert.Greater(lowest, eyeTop, $"bead S{k + 1}: lowest lens point above the walker's eye on the pier");
                Assert.Greater(LensBead.EquatorS(k), ThreadConstants.HelixEndS, $"bead S{k + 1}: equator beyond the helix");
                Assert.IsFalse(LensBead.HasDoor(k), $"bead S{k + 1}: no door (D-035)");
                if (k > 0) Assert.AreEqual(ThreadConstants.BeadYStep, LensBead.BeadY(k) - LensBead.BeadY(k - 1), 1e-9, "bead spacing");
                // the lens mesh lies entirely above the walkway
                float minY = float.MaxValue;
                foreach (var v in LensMesh(k, true).vertices) minY = Mathf.Min(minY, v.y);
                Assert.Greater(minY, eyeTop, $"bead S{k + 1} mesh above the walkway");
            }
            Assert.AreEqual(0, LensBead.DoorBeadCount, "D-035: no doors are built");
            Assert.AreEqual(ThreadConstants.BeadY0, LensBead.BeadY(0), 1e-12);
        }

        // ------------------------------------------------------------------ arm rule (14.5.6)
        [Test]
        public void Arms_EmbeddedInCableOrLensAndInDeck()
        {
            var arms = WalkwayLayout.Arms();
            int inLens = 0;
            foreach (var a in arms)
            {
                // root face: every point at least 0.3 m inside max(cable core, lens)
                for (int yi = 0; yi <= 8; yi++)
                    for (int li = -2; li <= 2; li++)
                    {
                        double y = a.yTop - a.depthRoot * yi / 8.0, lat = 0.25 * li * a.widthRoot;
                        D3 q = a.Point(a.rRoot, lat, y);
                        double r = Math.Sqrt((q.x - Cx) * (q.x - Cx) + (q.z - Cz) * (q.z - Cz));
                        double lens = LensBead.SurfaceRadius(y, BeatMeshUtil.Azimuth(q.x - Cx, q.z - Cz), out _);
                        double surf = Math.Max(ThreadConstants.CableRadius, lens);
                        Assert.LessOrEqual(r, surf - WalkwayLayout.ArmEmbed + 2e-3, $"arm {a.index} root not embedded (r {r:F3}, surface {surf:F3})");
                    }
                if (a.bead >= 0) inLens++;
                // the arm rule: a root in a lens only where the lens is wider than the ridge crest
                if (a.bead >= 0) Assert.Greater(a.rRoot + WalkwayLayout.ArmEmbed, ThreadConstants.CableRadius, $"arm {a.index}");
                else Assert.AreEqual(ThreadConstants.CableRadius - WalkwayLayout.ArmEmbed, a.rRoot, 0.01, $"arm {a.index} ends in the cable core");
                // top inside the deck body, everywhere across the arm width
                Assert.Less(a.yTop, a.deckTopY - 0.05, $"arm {a.index} top below the deck top");
                Assert.Greater(a.yTop, a.deckTopY - ThreadConstants.DeckThickness + 0.25, $"arm {a.index} top inside the deck");
                if (a.index < ThreadConstants.ArmCountHelix)
                {
                    double inner = ThreadConstants.HelixRadius - WalkwayLayout.HalfWidth, outer = ThreadConstants.HelixRadius + WalkwayLayout.HalfWidth;
                    Assert.Greater(a.rTip, inner + WalkwayLayout.ArmEmbed, $"arm {a.index} tip under the deck");
                    Assert.Less(a.rTip, outer - WalkwayLayout.ArmEmbed + 0.03, $"arm {a.index} tip under the deck");
                    // with the 0.25 grade the deck top over the arm's width stays above the arm top
                    double ds = 0.5 * a.widthRoot * ThreadConstants.HelixRadius / (ThreadConstants.HelixRadius - WalkwayLayout.HalfWidth);
                    Assert.Less(a.yTop, ThreadMath.PathY(a.s - ds) - 0.03, $"arm {a.index} grade margin");
                    Assert.Greater(a.yTop, ThreadMath.PathY(a.s + ds) - ThreadConstants.DeckThickness, $"arm {a.index} grade margin");
                }
                else
                {
                    D3 tip = a.Point(a.rTip, 0, a.yTop);
                    bool inside = PierPadOutline.Contains(tip.x, tip.z) || tip.z < ThreadConstants.PierStartZ;
                    Assert.IsTrue(inside, $"pier arm {a.index} tip inside the slab");
                    Assert.Greater(PierPadOutline.DistanceToEdge(tip.x, tip.z), 0.3 - 1e-3, $"pier arm {a.index} tip 0.3 m inside the edge");
                    for (int li = -1; li <= 1; li += 2)
                    {
                        D3 c = a.Point(a.rTip, li * 0.5 * a.widthTip, a.yTop);
                        double dz = c.z - ThreadConstants.PadCentreZ;
                        Assert.Greater(Math.Sqrt(c.x * c.x + dz * dz), PierPadOutline.BossRadius, $"pier arm {a.index} clears the pad boss");
                    }
                }
            }
            // D-035: no lens reaches deck height, so every arm (helix and pier) ends in the cable core at r = 49.7
            Assert.AreEqual(0, inLens, "D-035: no arm ends in a bead lens");
            foreach (var a in arms)
            {
                Assert.AreEqual(-1, a.bead, $"arm {a.index} ends in the cable");
                for (int li = -1; li <= 1; li += 2)
                {
                    D3 c = a.Point(a.rRoot, li * 0.5 * a.widthRoot, a.yTop);
                    double rc = Math.Sqrt((c.x - Cx) * (c.x - Cx) + (c.z - Cz) * (c.z - Cz));
                    Assert.AreEqual(ThreadConstants.CableRadius - WalkwayLayout.ArmEmbed, rc, 1e-6, $"arm {a.index} root corner on r = 49.7");
                }
            }
        }

        // ------------------------------------------------------------------ T-NORMALS
        [Test]
        public void Meshes_AreValid()
        {
            var meshes = new List<Mesh> { DeckMesh(), RailMesh(), PierMesh() };
            var mb = new MeshBuilder(64); WalkwayLayout.BuildPost(mb); meshes.Add(Keep(mb.ToMesh("RailPost")));
            mb = new MeshBuilder(4096); WalkwayLayout.BuildArms(mb, 0, ThreadConstants.ArmCountHelix); meshes.Add(Keep(mb.ToMesh("Walkway_Arms")));
            mb = new MeshBuilder(256); WalkwayLayout.BuildArms(mb, ThreadConstants.ArmCountHelix, WalkwayLayout.ArmCount); meshes.Add(Keep(mb.ToMesh("Top_PierArms")));
            for (int k = 0; k < ThreadConstants.BeadCount; k++) meshes.Add(LensMesh(k, true));
            if (LensBead.DoorBeadCount > 0)
            {
                mb = new MeshBuilder(256);
                for (int k = 0; k < LensBead.DoorCount; k++) if (LensBead.HasDoor(k)) LensBead.BuildDoorLeaf(mb, LensBead.Door(k));
                meshes.Add(Keep(mb.ToMesh("Bead_Doors")));
            }
            for (int r = 0; r < ThreadConstants.WindowRingOffsets.Length; r++) { mb = new MeshBuilder(64); LensBead.BuildWindowRow(mb, r, out _, out _); meshes.Add(Keep(mb.ToMesh("WindowRow_" + r))); }
            mb = new MeshBuilder(256); Beat07_Top.BuildPillar(mb); meshes.Add(Keep(mb.ToMesh("Beacon_Pillar")));
            mb = new MeshBuilder(256); Beat07_Top.BuildCrystal(mb); meshes.Add(Keep(mb.ToMesh("Beacon_Crystal")));
            meshes.Add(Keep(Beat07_Top.BuildHaloMesh(ThreadConstants.BeaconHaloR0, "halo0")));
            meshes.Add(Keep(Beat07_Top.BuildHaloMesh(ThreadConstants.BeaconHaloR1, "halo1")));
            foreach (var m in meshes)
            {
                Assert.IsTrue(MeshKit.Validate(m, out string report), report);
                Assert.Greater(MeshKit.TriangleCount(m), 0, m.name);
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++) Assert.IsFalse(float.IsNaN(v[i].x) || float.IsNaN(v[i].y) || float.IsNaN(v[i].z), m.name);
                Assert.IsTrue(m.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 || m.vertexCount <= 65535, m.name);
            }
        }

        [Test]
        public void DeckToPier_JoinIsBitIdentical()
        {
            var deckEnd = new HashSet<Vector3>();
            foreach (var v in DeckMesh().vertices) if (v.z == (float)ThreadConstants.PierStartZ && v.y > ThreadConstants.TopY - 1.0) deckEnd.Add(v);
            var pierStart = new HashSet<Vector3>();
            foreach (var v in PierMesh().vertices) if (v.z == (float)ThreadConstants.PierStartZ) pierStart.Add(v);
            Assert.AreEqual(WalkwayLayout.DeckProfile.Length, deckEnd.Count, "deck end ring");
            Assert.IsTrue(deckEnd.SetEquals(pierStart), "the pier/pad slab starts with exactly the deck's end ring");
        }

        [Test]
        public void Rail_IsOneContinuousTubeThroughEveryPost()
        {
            var line = WalkwayLayout.RailCentreLine();
            for (int i = 1; i < line.Count; i++)
            {
                double l = (line[i] - line[i - 1]).Length;
                Assert.Greater(l, 1e-4, "no duplicate rail points");
                Assert.Less(l, 2.2, "rail segments at most one deck segment (2.04 m plan on the outer edge, 0.5 m rise)");
            }
            var posts = WalkwayLayout.Posts();
            Assert.AreEqual(440, 2 * WalkwayLayout.HelixPostStations, "440 posts on the helix");
            Assert.That(posts.Count - 440, Is.InRange(26, 34), "about 30 posts on the pier and pad");
            foreach (var p in posts)
            {
                // every post top is inside the handrail tube
                D3 top = p.basePos + new D3(0, WalkwayLayout.RailCentreHeight, 0);
                double best = double.MaxValue;
                for (int i = 1; i < line.Count; i++)
                {
                    D3 a = line[i - 1], ab = line[i] - a;
                    double t = Math.Max(0, Math.Min(1, D3.Dot(top - a, ab) / Math.Max(1e-12, D3.Dot(ab, ab))));
                    best = Math.Min(best, (a + ab * t - top).Length);
                }
                Assert.Less(best, 0.01, $"post at {p.basePos} under the rail");
                if (p.basePos.z >= ThreadConstants.PierStartZ && Math.Abs(p.basePos.y - ThreadConstants.TopY) < 1e-9)
                    Assert.IsTrue(PierPadOutline.Contains(p.basePos.x, p.basePos.z + 1e-6) || p.basePos.z == ThreadConstants.PierStartZ, "pier post on the slab");
            }
        }

        // ------------------------------------------------------------------ windows and doors
        [Test]
        public void Windows_OnTheFacetsInsideOneColumn()
        {
            int total = 0;
            double halfCol = Math.Tan(0.5 * LensBead.Delta);
            for (int r = 0; r < ThreadConstants.WindowRingOffsets.Length; r++)
            {
                var mb = new MeshBuilder(64);
                LensBead.BuildWindowRow(mb, r, out double y0, out double y1);
                // every strip vertex is exactly 0.02 m outside the faceted profile of its column
                foreach (var v in mb.Positions)
                {
                    double best = double.MaxValue;
                    for (int i = 0; i < LensBead.SegmentsProfile; i++)
                    {
                        double c = Math.Cos(0.5 * LensBead.Delta);
                        double ar = LensBead.RingR(i) * c, ay = LensBead.RingY(i), br = LensBead.RingR(i + 1) * c, by = LensBead.RingY(i + 1);
                        double er = br - ar, ey = by - ay, l2 = er * er + ey * ey;
                        double t = Math.Max(0, Math.Min(1, ((v.z - ar) * er + (v.y - ay) * ey) / l2));
                        double dr = ar + er * t - v.z, dy = ay + ey * t - v.y;
                        best = Math.Min(best, Math.Sqrt(dr * dr + dy * dy));
                    }
                    Assert.AreEqual(LensBead.WindowOffset, best, 2e-4, $"row {r} window offset");
                }
                for (int k = 0; k < ThreadConstants.BeadCount; k++)
                {
                    var wins = LensBead.Windows(k, r, y0, y1);
                    total += wins.Count;
                    double rho = LensBead.ColumnRho(ThreadConstants.WindowRingOffsets[r]);
                    foreach (var w in wins)
                    {
                        Assert.Less(Math.Abs(w.t) + 0.5 * ThreadConstants.WindowW, rho * halfCol - 0.1, "window inside one facet column");
                        Assert.Less(Math.Abs(w.t), 1e-6, "window centred on its facet column");
                    }
                    if (LensBead.DoorBeadCount == 0)
                        Assert.AreEqual(ThreadConstants.WindowsPerRing, wins.Count, $"bead {k + 1} row {r}: no window omitted (D-035: no door, no arm at bead height)");
                    if (r == LensBead.EyeRow && LensBead.HasDoor(k))
                        foreach (var w in wins)
                            Assert.Greater(Math.Abs(BeatMeshUtil.AngDiff(w.azimuth, LensBead.Door(k).azimuth)), 1e-3, "the door replaces a window");
                }
            }
            Assert.LessOrEqual(total, ThreadConstants.BeadCount * ThreadConstants.WindowRingOffsets.Length * ThreadConstants.WindowsPerRing);
            Assert.Greater(total, 1200, "window dots");
            // the window rows of one bead are aligned: the eye row on the reference azimuth, the others half a step on
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                LensBead.BuildWindowRow(new MeshBuilder(64), LensBead.EyeRow, out double e0, out double e1);
                var eye = LensBead.Windows(k, LensBead.EyeRow, e0, e1);
                if (eye.Count == ThreadConstants.WindowsPerRing)
                    Assert.AreEqual(0.0, BeatMeshUtil.AngDiff(eye[0].azimuth, LensBead.ReferenceAzimuth(k)), 1e-9, $"bead {k + 1}: eye row on the reference azimuth");
            }
        }

        [Test]
        public void Doors_OnlyWhereTheDeckPassesAnEquator()
        {
            // porch constants (14.5.6): recessed 0.4 m, door plane outside the rim, door centre 1.0 m above the sill
            Assert.AreEqual(ThreadConstants.BeadDoorRecess, LensBead.PorchFront - LensBead.DoorLeafFront, 1e-9, "door recessed 0.4 m");
            Assert.Greater(LensBead.DoorLeafFront, ThreadConstants.BeadRadius, "door plane outside the lens rim");
            Assert.AreEqual(ThreadConstants.DoorHeight * 0.5, 1.0, 1e-9, "door centre 1.0 m above the sill");
            var arms = WalkwayLayout.Arms();
            int doors = 0;
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                bool passes = LensBead.EquatorS(k) >= ThreadConstants.HelixStartS && LensBead.EquatorS(k) <= ThreadConstants.HelixEndS;
                Assert.AreEqual(passes && k < LensBead.DoorCount, LensBead.HasDoor(k), $"bead {k + 1}: a door exactly where the deck passes the equator");
                if (!LensBead.HasDoor(k)) continue;
                doors++;
                var d = LensBead.Door(k);
                Assert.AreEqual(ThreadMath.PathY(d.s), d.sillY, 1e-9, "sill flush with the deck");
                Assert.LessOrEqual(Math.Abs(d.s - LensBead.EquatorS(k)), LensBead.DoorShiftMax, "door near the equator crossing");
                foreach (var a in arms)
                {
                    double gap = Math.Abs(d.s - a.s) * ThreadConstants.BeadRadius / ThreadConstants.HelixRadius - LensBead.PorchHalfWidth - 0.5 * a.widthRoot;
                    Assert.Greater(gap, LensBead.DoorArmClearance - 1e-9, $"door {k + 1} clear of arm {a.index}");
                }
            }
            Assert.AreEqual(doors, LensBead.DoorBeadCount);
            // D-035: the column starts above the pier, no deck passes an equator, so no door is built
            Assert.AreEqual(0, LensBead.DoorBeadCount, "D-035: no doors");
        }

        // ------------------------------------------------------------------ builders
        [Test]
        public void Builders_RunWithoutMaterials()
        {
            var lib = ScriptableObject.CreateInstance<MaterialLibrary>();
            m_trash.Add(lib);
            var near = new GameObject("NearRoot_Test");
            m_trash.Add(near);
            var ctx = new WorldContext { Materials = lib, NearRoot = near.transform, Seed = 1 };
            IBeatBuilder[] builders = { new Beat05_Spiral(), new Beat06_Clouds(), new Beat07_Top() };
            string[] names = { "Beat05_Spiral", "Beat06_Clouds", "Beat07_Top" };
            for (int i = 0; i < builders.Length; i++)
            {
                Assert.AreEqual(names[i], builders[i].Name);
                Assert.DoesNotThrow(() => builders[i].Build(ctx), names[i]);
            }
            Assert.AreEqual(3, near.transform.childCount);
            Assert.Greater(ctx.Triangles, 30000);
            foreach (var mf in near.GetComponentsInChildren<MeshFilter>()) m_trash.Add(mf.sharedMesh);
        }

        // ------------------------------------------------------------------ material terms (critique run07)
        /// <summary>A number #defined in a shader include (the single source of the shader-only constants).</summary>
        static double HlslDefine(string include, string name)
        {
            string path = Path.Combine(Application.dataPath, "_Game/Shaders/Include", include);
            Assert.IsTrue(File.Exists(path), path);
            var rx = new Regex(@"^\s*#define\s+" + name + @"\s+([-+0-9.eE]+)");
            foreach (var line in File.ReadAllLines(path))
            {
                var m = rx.Match(line);
                if (m.Success) return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.Fail($"{name} is not #defined in {include}");
            return 0;
        }

        static MatSpec Row(List<MatSpec> specs, string field)
        {
            foreach (var s in specs) if (s.Field == field) return s;
            Assert.Fail("no material contract row " + field);
            return null;
        }

        static float Flag(MatSpec s, string prop) => s.Floats.TryGetValue(prop, out float v) ? v : 0f;

        /// <summary>
        /// A-r2-1 / A5 / B3 / A11: deck, arms, posts and handrails fade far below the walker (and by A2C); the handrails
        /// fade with the posts; the cable, its collars and its ribbons haze far below; the cable draws CREAM plates;
        /// nothing else (beads, beacon, windows, blended or additive materials) takes the walkway fade.
        /// </summary>
        [Test]
        public void WalkwayMaterials_FadeAndHazeFlags()
        {
            var specs = MaterialFactory.Specs();
            var fading = new HashSet<string> { "Deck", "Arm", "RailPost", "Handrail" };
            var hazed = new HashSet<string> { "Cable", "Collar", "ThreadRibbon" };
            foreach (var s in specs)
            {
                bool fade = Flag(s, "_BelowFade") > 0.5f, haze = Flag(s, "_BelowHaze") > 0.5f;
                Assert.AreEqual(fading.Contains(s.Field), fade, s.Field + ": _BelowFade");
                Assert.AreEqual(hazed.Contains(s.Field), haze, s.Field + ": _BelowHaze");
                if (fade)
                {
                    Assert.IsTrue(MaterialFactory.UsesAlphaToMask(s), s.Field + ": the fade needs alpha-to-coverage");
                    Assert.IsTrue(s.Src == UnityEngine.Rendering.BlendMode.One && s.Dst == UnityEngine.Rendering.BlendMode.Zero && s.ZWrite,
                                  s.Field + ": only opaque materials fade by coverage");
                }
            }
            var rail = Row(specs, "Handrail");
            Assert.AreEqual(MaterialConstants.PostFadeNear, Flag(rail, "_FadeNear"), 1e-6, "handrail fade starts with the posts");
            Assert.AreEqual(MaterialConstants.PostFadeFar, Flag(rail, "_FadeFar"), 1e-6, "handrail fade ends with the posts");
            Assert.AreEqual(Pal.CREAM, Row(specs, "Cable").Third, "cable marker plates are CREAM");
        }

        /// <summary>
        /// A-r2-1: ThBelowFadeKeep keeps the deck under the walker and at least 100 m of path behind; its altitude gate
        /// opens only while the camera is inside the deck core, where every point the fade touches (30 m or more below)
        /// lies behind a deck optical depth above 2.5 (transmittance under 8 percent), so the gate is never seen.
        /// </summary>
        [Test]
        public void WalkwayBelowFade_GateHiddenInsideTheDeck()
        {
            double drop0 = HlslDefine("ThreadPatterns.hlsl", "TP_BELOW_DROP0"), drop1 = HlslDefine("ThreadPatterns.hlsl", "TP_BELOW_DROP1");
            double a0 = HlslDefine("ThreadPatterns.hlsl", "TP_BELOW_A0"), a1 = HlslDefine("ThreadPatterns.hlsl", "TP_BELOW_A1");
            Assert.Less(drop0, drop1);
            Assert.Less(a0, a1);
            Assert.GreaterOrEqual((drop0 - ThreadConstants.EyeHeight) / ThreadConstants.HelixGrade, 100.0, "path kept behind the walker (m)");
            Assert.GreaterOrEqual(a0, ThreadConstants.CloudBottom + ThreadConstants.DeckBottomSkin, "gate opens inside the deck core");
            Assert.LessOrEqual(a1, ThreadConstants.CloudTop, "gate complete before the camera leaves the deck top");
            for (int i = 0; i <= 8; i++)
            {
                double a = a0 + (a1 - a0) * i / 8.0, camY = 90.0;
                FogMath.DeckSlab(camY, a, out double top, out double bot);
                double tau = FogMath.TauDeck(new D3(0, camY, 0), new D3(0, -1, 0), drop0, FogMath.DeckGate(a), top, bot);
                Assert.Greater(tau, 2.5, $"deck optical depth to {drop0} m below at A = {a:F0} m");
            }
        }

        /// <summary>
        /// B-r2-5: the shader's deck edge trim (ThDeckStripe: |uv1.x| >= stripe start and uv1.y above the lower-bevel
        /// crease) covers the top chamfers and outer side faces of the helix deck and of the pier / pad slab, and never
        /// a bevel or bottom face; its bevel depth mirrors WalkwayLayout.DeckBevel.
        /// </summary>
        [Test]
        public void DeckEdgeTrim_ProfileCoordinatesMatchTheShader()
        {
            Assert.AreEqual(WalkwayLayout.DeckBevel, HlslDefine("ThreadPatterns.hlsl", "TP_DECK_BEVEL"), 1e-9, "TP_DECK_BEVEL");
            double lo = 0.5 * ThreadConstants.DeckWidth - ThreadConstants.DeckChamfer - ThreadConstants.DeckEdgeStripe;
            double crease = -(ThreadConstants.DeckThickness - WalkwayLayout.DeckBevel);
            foreach (var mesh in new[] { DeckMesh(), PierMesh() })
            {
                var uv1 = new List<Vector4>();
                mesh.GetUVs(1, uv1);
                var n = mesh.normals;
                int side = 0;
                for (int i = 0; i < uv1.Count; i++)
                {
                    double x = Math.Abs(uv1[i].x), h = uv1[i].y;
                    bool inTrim = x >= lo - 1e-5 && h >= crease - 1e-5;
                    if (n[i].y < -0.5f) Assert.IsFalse(x > lo + 1e-4 && h > crease + 1e-4, $"{mesh.name}: bevel / bottom vertex {i} inside the trim");
                    else if (Math.Abs(n[i].y) < 0.9f && x > 0.5 * ThreadConstants.DeckWidth - ThreadConstants.DeckChamfer - 1e-4)
                    {
                        Assert.IsTrue(inTrim, $"{mesh.name}: chamfer / side vertex {i} (x {x:F3}, h {h:F3}) outside the trim");
                        side++;
                    }
                }
                Assert.Greater(side, 0, mesh.name + ": no side-face vertices found");
            }
        }
    }
}
