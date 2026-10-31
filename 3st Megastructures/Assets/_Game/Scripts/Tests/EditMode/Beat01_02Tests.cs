// EditMode tests of the beat 1 and beat 2 geometry (MASTER_PROMPT 14.10.1 T-SCALE-2, T-SHADE second part,
// T-NORMALS; 14.5.1 a; 14.5.2 a-f and acceptance 1, 2, 6, 7; 14.7.9 budgets; 8.7 seams / co-planarity).
// The meshes are produced by the builders' pure generators (ColossusBuilder.Generate, SaltGround.BuildGroup)
// without a scene. The expected values below are the specification's numbers on purpose: the tests check that
// the constants and the generators reproduce them.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Megastructure.Beats;

namespace Megastructure.Tests
{
    public class Beat01_02Tests
    {
        const double Deg = Math.PI / 180.0;

        static List<ColossusGeometry> s_all;

        /// <summary>The eight colossi in build order (pair 0 left, pair 0 right, pair 1 left, ...), generated once.</summary>
        static List<ColossusGeometry> All
        {
            get
            {
                if (s_all == null)
                {
                    var list = new List<ColossusGeometry>(8);
                    for (int pair = 0; pair < ThreadConstants.ColossusPairZ.Length; pair++)
                        for (int side = 0; side < 2; side++)
                            list.Add(ColossusBuilder.Generate(pair, side));
                    s_all = list;
                }
                return s_all;
            }
        }

        readonly List<UnityEngine.Object> m_temp = new List<UnityEngine.Object>();

        T Temp<T>(T o) where T : UnityEngine.Object { m_temp.Add(o); return o; }

        [TearDown]
        public void DestroyTemp()
        {
            for (int i = 0; i < m_temp.Count; i++) if (m_temp[i] != null) UnityEngine.Object.DestroyImmediate(m_temp[i]);
            m_temp.Clear();
        }

        [OneTimeTearDown]
        public void ReleaseGeometry() { s_all = null; }

        static Vector3 World(ColossusGeometry g, Vector3 rootRelative) => rootRelative + g.RootPosition;

        static D3 D(Vector3 v) => new D3(v.x, v.y, v.z);

        // ================================================================== builders
        [Test]
        public void Builders_HaveContractNames()
        {
            Assert.AreEqual("Beat01_SaltFlat", new Beat01_SaltFlat().Name);
            Assert.AreEqual("Beat02_Avenue", new Beat02_Avenue().Name);
        }

        // ================================================================== T-SCALE-2
        [Test]
        public void TScale2_EightRobesWithSeededHeights()
        {
            var all = All;
            Assert.AreEqual(8, all.Count, "8 robes");
            var seeds = new HashSet<uint>();
            var heights = new List<double>();
            for (int i = 0; i < all.Count; i++)
            {
                var g = all[i];
                var s = g.Shape;
                Assert.IsTrue(seeds.Add(s.Seed), $"{g.Name}: no two robes share a seed (14.5.2 d)");
                // the highest generated robe / hood vertex is the robe height (acceptance 1: bounds 282 to 318 m); the
                // seeded height is the unbent apex, and the bowed head (critique run07) lowers the crown by a few metres
                Assert.That(g.TopY, Is.InRange(282f, 318f), $"{g.Name} height");
                double crown = s.CrownY();
                Assert.AreEqual(crown, g.TopY, 0.3, $"{g.Name}: mesh top vs the crown of the bowed hood");
                Assert.That(s.Height - crown, Is.InRange(0.0, ColossusSpec.BowCrownDrop), $"{g.Name}: crown drop of the bow");
                foreach (double h in heights) Assert.Greater(Math.Abs(h - s.Height), 0.5, $"{g.Name}: heights differ");
                heights.Add(s.Height);
                // pair placement: centre x = +-50 at z = ColossusPairZ (left row TEAL at -x, right row CORAL at +x)
                Assert.AreEqual(new[] { 144.0, 192.0, 240.0, 288.0 }[s.Pair], s.Cz, 1e-9, $"{g.Name} pair z");
                Assert.AreEqual(s.Side == 0 ? -50.0 : 50.0, s.Cx, 0.25, $"{g.Name} centre x");
            }
        }

        [Test]
        public void TScale2_InnerFootEdgeAt28()
        {
            foreach (var g in All)
            {
                bool left = g.Shape.Side == 0;
                float edge = left ? float.MinValue : float.MaxValue;
                int n = 0;
                for (int i = 0; i < g.Hem.VertexCount; i++)
                {
                    Vector3 p = World(g, g.Hem.Positions[i]);
                    if (Mathf.Abs(p.y) > 1e-3f) continue;   // the ground-level ring of the hem band
                    edge = left ? Mathf.Max(edge, p.x) : Mathf.Min(edge, p.x);
                    n++;
                }
                Assert.Greater(n, 0, $"{g.Name}: hem ring at ground level");
                Assert.AreEqual(left ? -28.0 : 28.0, edge, 0.1, $"{g.Name}: inner foot edge");
                // the hem is sunk 0.3 m into the salt and the band is 6.0 m tall (never scaled)
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < g.Hem.VertexCount; i++) { minY = Mathf.Min(minY, g.Hem.Positions[i].y); maxY = Mathf.Max(maxY, g.Hem.Positions[i].y); }
                Assert.AreEqual(-0.3, minY, 1e-4, $"{g.Name}: hem sunk 0.3 m");
                Assert.AreEqual(6.0, maxY, 1e-4, $"{g.Name}: hem band 6 m");
            }
        }

        [Test]
        public void TScale2_DoorwayIsARealBoxedAlcove()
        {
            foreach (var g in All)
            {
                var s = g.Shape;
                var d = s.Door;
                Vector3 h = AvenueMesh.V(d.H).normalized, nh = AvenueMesh.V(d.Nh).normalized;
                Vector3 o = AvenueMesh.V(d.O) - g.RootPosition;
                // opening size from the INK alcove geometry (jamb walls, lintel, back wall)
                float minY = float.MaxValue, maxY = float.MinValue, minH = float.MaxValue, maxH = float.MinValue, minN = float.MaxValue;
                for (int i = 0; i < g.AlcoveInk.VertexCount; i++)
                {
                    Vector3 p = g.AlcoveInk.Positions[i];
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    float a = Vector3.Dot(p - o, h);
                    minH = Mathf.Min(minH, a); maxH = Mathf.Max(maxH, a);
                }
                Assert.AreEqual(2.0, maxY - minY, 0.02, $"{g.Name}: door height");
                Assert.AreEqual(1.1, maxH - minH, 0.02, $"{g.Name}: door width");
                Assert.AreEqual(3 * 0.18, minY, 1e-3, $"{g.Name}: threshold on the third ledge step");
                // the back wall stands 0.8 m behind the (leaning) robe wall: horizontal depth of every alcove vertex
                // behind the wall plane at its own height
                for (int i = 0; i < g.AlcoveInk.VertexCount; i++)
                {
                    Vector3 p = g.AlcoveInk.Positions[i];
                    minN = Mathf.Min(minN, Vector3.Dot(p - o, nh) - (float)d.WallOffset(p.y));
                }
                Assert.AreEqual(-0.8, minN, 0.01, $"{g.Name}: recess 0.8 m");
                // a real hole: no hem triangle covers the centre of the opening
                Vector3 centre = AvenueMesh.V(d.At(0.0, 0.5 * (d.Sill + d.Lintel))) - g.RootPosition;
                Assert.IsFalse(CoversPoint(g.Hem, centre, AvenueMesh.V(d.N)), $"{g.Name}: the robe wall has a hole at the doorway");
                // the doorway faces the path and sits at the foot of its pair
                Assert.Greater(s.InnerSign * d.Nh.x, 0.99, $"{g.Name}: door faces the path");
                Assert.AreEqual(s.Cz, d.O.z, 2.0, $"{g.Name}: door near z of the pair");
            }
        }

        [Test]
        public void TScale2_DoorFrameOutlinesTheOpening()
        {
            // critique run07 A15: a light raised frame (real geometry) around the 2.0 x 1.1 m opening, standing on the
            // threshold step, that never narrows the opening
            foreach (var g in All)
            {
                var d = g.Shape.Door;
                Vector3 h = AvenueMesh.V(d.H).normalized, nf = AvenueMesh.V(d.N).normalized;
                Vector3 o = AvenueMesh.V(d.O) - g.RootPosition;
                Assert.Greater(g.DoorFrame.TriangleCount, 0, $"{g.Name}: door frame");
                float minY = float.MaxValue, maxY = float.MinValue, minH = float.MaxValue, maxH = float.MinValue, maxN = float.MinValue;
                for (int i = 0; i < g.DoorFrame.VertexCount; i++)
                {
                    Vector3 p = g.DoorFrame.Positions[i];
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    float a = Vector3.Dot(p - o, h);
                    minH = Mathf.Min(minH, a); maxH = Mathf.Max(maxH, a);
                    // height above the (leaning) facet plane through the sill line
                    maxN = Mathf.Max(maxN, Vector3.Dot(p - (AvenueMesh.V(d.At(0.0, p.y)) - g.RootPosition), nf));
                }
                double fw = ColossusSpec.DoorFrameWidth;
                Assert.AreEqual(d.Sill, minY, 1e-3, $"{g.Name}: frame stands on the threshold step");
                Assert.AreEqual(d.Lintel + fw, maxY, 0.02, $"{g.Name}: frame head");
                Assert.AreEqual(ThreadConstants.DoorWidth + 2.0 * fw, maxH - minH, 0.01, $"{g.Name}: frame outer width");
                Assert.AreEqual(ColossusSpec.DoorFrameDepth, maxN, 0.01, $"{g.Name}: frame stands proud of the wall");
                Assert.That(fw, Is.InRange(0.1, 0.15), "frame width 0.10-0.15 m");
                // the opening stays clear: no frame triangle covers its centre, and the reveal faces start at the opening edge
                Vector3 centre = AvenueMesh.V(d.At(0.0, 0.5 * (d.Sill + d.Lintel))) - g.RootPosition;
                Assert.IsFalse(CoversPoint(g.DoorFrame, centre + nf * (float)ColossusSpec.DoorFrameDepth, nf), $"{g.Name}: the frame leaves the opening clear");
                for (int i = 0; i < g.DoorFrame.VertexCount; i++)
                {
                    Vector3 p = g.DoorFrame.Positions[i];
                    float a = Mathf.Abs(Vector3.Dot(p - o, h));
                    if (p.y > d.Sill + 1e-3 && p.y < d.Lintel - 1e-3) Assert.GreaterOrEqual(a, 0.5f * (float)ThreadConstants.DoorWidth - 1e-3f, $"{g.Name}: frame vertex inside the opening");
                }
            }
        }

        [Test]
        public void TScale2_LedgeAndBollardsAreHumanScale()
        {
            foreach (var g in All)
            {
                var d = g.Shape.Door;
                Vector3 h = AvenueMesh.V(d.H).normalized, nh = AvenueMesh.V(d.Nh).normalized;
                Vector3 o = AvenueMesh.V(d.O) - g.RootPosition;
                var treads = new SortedSet<int>();
                var fronts = new SortedSet<int>();
                for (int i = 0; i < g.Stone.VertexCount; i++)
                {
                    Vector3 p = g.Stone.Positions[i], n = g.Stone.Normals[i];
                    if (n.y > 0.999f) treads.Add(Mathf.RoundToInt(p.y * 10000f));
                    if (Vector3.Dot(n, nh) > 0.999f) fronts.Add(Mathf.RoundToInt(Vector3.Dot(p - o, nh) * 10000f));
                }
                var ty = new List<double>(); foreach (int v in treads) ty.Add(v / 10000.0);
                var fz = new List<double>(); foreach (int v in fronts) fz.Add(v / 10000.0);
                Assert.AreEqual(3, ty.Count, $"{g.Name}: 3 ledge steps");
                Assert.AreEqual(0.18, ty[0], 0.002, $"{g.Name}: first riser");
                for (int i = 1; i < ty.Count; i++) Assert.AreEqual(0.18, ty[i] - ty[i - 1], 0.002, $"{g.Name}: riser {i + 1}");
                Assert.AreEqual(3, fz.Count, $"{g.Name}: 3 step fronts");
                for (int i = 1; i < fz.Count; i++) Assert.AreEqual(0.4, fz[i] - fz[i - 1], 0.002, $"{g.Name}: tread {i}");
                // the bottom step is 1.6 m wide
                float minH = float.MaxValue, maxH = float.MinValue;
                for (int i = 0; i < g.Stone.VertexCount; i++)
                {
                    Vector3 p = g.Stone.Positions[i];
                    if (p.y > 0.18f - 1e-3f) continue;
                    float a = Vector3.Dot(p - o, h);
                    minH = Mathf.Min(minH, a); maxH = Mathf.Max(maxH, a);
                }
                Assert.AreEqual(1.6, maxH - minH, 0.01, $"{g.Name}: ledge width");
                // bollard pair, 1.0 m, never scaled with the robe height
                float bMax = float.MinValue;
                for (int i = 0; i < g.Bollard.VertexCount; i++) bMax = Mathf.Max(bMax, g.Bollard.Positions[i].y);
                Assert.AreEqual(1.0, bMax, 1e-3, $"{g.Name}: bollard height");
                Assert.AreEqual(2, g.BollardSeams.Count / 7, $"{g.Name}: one bollard pair (7 lathe rings each)");
            }
        }

        // ================================================================== 14.5.2 a, d: profile and asymmetry rules
        [Test]
        public void Colossi_ProfileFlutesAndAsymmetry()
        {
            foreach (var g in All)
            {
                var s = g.Shape;
                // 14-point profile: the knots of 14.5.2 a scaled by k above the 6 m hem band (asymmetry jitter <= 0.5 m above y 120)
                double[] eta = { 0, 6, 40, 80, 120, 160, 190, 215 };
                double[] r = { 22, 21.2, 19.5, 17.2, 15.5, 14, 15.5, 18.5 };
                for (int i = 0; i < eta.Length; i++)
                    Assert.AreEqual(r[i], s.ProfileR(eta[i]), eta[i] < 120 ? 1e-9 : 0.5 + 1e-9, $"{g.Name}: profile r at {eta[i]}");
                Assert.AreEqual(14.0, s.ProfileR(228, 2), 1e-9, $"{g.Name}: shoulder knot");
                Assert.AreEqual(8.0, s.ProfileR(238, 2), 1e-9, $"{g.Name}: neck ring r 8");
                Assert.AreEqual(s.Height / 300.0, s.Kk, 0.002, $"{g.Name}: vertical scale ~ h / 300");
                // flutes: 0.9 m at the hem, gone at y 150 (scaled)
                Assert.AreEqual(0.9, s.FluteAmplitude(3.0), 1e-9, $"{g.Name}: flute amplitude at the hem");
                Assert.AreEqual(0.0, s.FluteAmplitude(s.Y(150)), 1e-9, $"{g.Name}: flutes faded at y 150");
                // footprint wobble within +-1.5 m
                for (int k = 0; k < 360; k++) Assert.LessOrEqual(Math.Abs(s.Wobble(k * Deg, 0.0)), 1.5 + 1e-9, $"{g.Name}: wobble");
                // yaw 4 to 9 deg toward the path: local +Z (the facing) turns toward x = 0
                double yaw = Math.Abs(s.Yaw) / Deg;
                Assert.That(yaw, Is.InRange(4.0, 9.0), $"{g.Name}: yaw");
                Assert.Greater(s.InnerSign * s.LocalToRoot(new D3(0, 0, 1)).x, 0.0, $"{g.Name}: yaw toward the path");
                // head tilt 2 to 5 deg toward the avenue (measured on the bent apex in the local X-Y plane)
                D3 apex = s.Bend(new D3(0, s.Y(300), 0));
                double tilt = Math.Atan2(apex.x, apex.y - s.BendPivotY) / Deg;
                Assert.That(Math.Abs(tilt), Is.InRange(2.0, 5.0), $"{g.Name}: tilt");
                Assert.Greater(s.InnerSign * (s.ApexWorld.x - s.Cx), 0.0, $"{g.Name}: tilt toward the avenue");
                // forward bow (critique run06 E6 / PAC-05, run07 A6 / E6), measured in the local Y-Z plane; the neck weld is unbent
                double nod = Math.Atan2(apex.z, apex.y - s.BendPivotY) / Deg;
                Assert.That(nod, Is.InRange(ColossusSpec.NodMinDeg - 1e-9, ColossusSpec.NodMaxDeg + 1e-9), $"{g.Name}: forward bow");
                // the face turns toward the path (local facing azimuth 28-32 deg, plus the yaw in the world) and looks down
                Assert.That(s.FaceTurn / Deg, Is.InRange(ColossusSpec.FaceTurnMinDeg, ColossusSpec.FaceTurnMaxDeg), $"{g.Name}: face turn");
                D3 face = s.LocalToRoot(s.HeadForward);
                double faceAz = Math.Atan2(s.InnerSign * face.x, face.z) / Deg;
                // (the bow about the lean axis, which points ~10 deg toward the path, turns the facing by a further 1-2 deg)
                Assert.AreEqual(s.FaceTurn / Deg + Math.Abs(s.Yaw) / Deg, faceAz, 2.5, $"{g.Name}: face azimuth toward the path (turn + yaw)");
                Assert.Less(Math.Asin(face.y) / Deg, -15.0, $"{g.Name}: the face plate looks down by more than 15 deg");
                D3 neck = new D3(8.0, s.Y(238), 0.0);
                Assert.AreEqual(0.0, (s.Bend(neck) - neck).Length, 1e-9, $"{g.Name}: neck ring not moved by the bend");
                Assert.AreEqual(72, ColossusSpec.Segments, "72 radial segments");
                Assert.AreEqual(24, ColossusSpec.FluteCount, "24 flutes");
            }
            // rows are generated separately, never mirrored: no left robe is the mirror of a right robe
            for (int pair = 0; pair < 4; pair++)
            {
                var l = All[pair * 2].Shape; var r = All[pair * 2 + 1].Shape;
                Assert.IsFalse(Math.Abs(l.Height - r.Height) < 1e-6 && Math.Abs(l.Yaw + r.Yaw) < 1e-9 && Math.Abs(l.Tilt - r.Tilt) < 1e-9, $"pair {pair + 1} mirrored");
            }
        }

        // ================================================================== 14.5.2 acceptance 7
        [Test]
        public void Sleeves_DoNotIntersectOwnRobe()
        {
            foreach (var g in All)
            {
                var s = g.Shape;
                Assert.AreEqual(2, s.Sleeves.Length, $"{g.Name}: two sleeves");
                Assert.IsFalse(s.Sleeves[0].Bent, $"{g.Name}: outer sleeve straight");
                Assert.IsTrue(s.Sleeves[1].Bent, $"{g.Name}: inner sleeve bent");
                Assert.Greater(s.Sleeves[1].SideX * s.InnerSign, 0.0, $"{g.Name}: the bent sleeve is the inner one (nearer the path)");
                double yBlend = s.Y(ColossusSpec.SleeveBlendEta);
                for (int k = 0; k < s.Sleeves.Length; k++)
                {
                    var sl = s.Sleeves[k];
                    Assert.AreEqual(12, sl.Count, $"{g.Name}: 12 loft sections");
                    // below the attachment blend (y 200) the sleeve keeps the 1.0 m gap everywhere (sampled surface)
                    double min = double.MaxValue;
                    ForSleeveSurface(sl, 4, 24, (c, p) => { if (c.y <= yBlend) min = Math.Min(min, s.RobeClearance(s.LocalToWorld(p))); });
                    Assert.GreaterOrEqual(min, 1.0, $"{g.Name} sleeve {k}: clearance to its own robe");
                    // length 150 m (scaled) from the shoulder, radius 9 -> 6
                    Assert.AreEqual(9.0, sl.R[0], 0.5, $"{g.Name} sleeve {k}: radius at the shoulder");
                    Assert.AreEqual(6.0, sl.R[sl.Count - 1], 1e-6, $"{g.Name} sleeve {k}: radius at the cuff");
                    Assert.AreEqual(s.Y(ColossusSpec.ShoulderEta[0] - ColossusSpec.SleeveCapSink), sl.C[0].y, 1e-6, $"{g.Name} sleeve {k}: top cap sunk 3 m");
                }
                // the straight sleeve hangs 150 m from the shoulder to the end of its closed cuff; the inner one bends
                // 25 deg forward at y 140
                var st = s.Sleeves[0];
                Assert.AreEqual(s.Y(215 - 150), st.CuffC[st.CuffC.Length - 1].y, 0.02, $"{g.Name}: straight sleeve length (cuff end)");
                var bt = s.Sleeves[1];
                Assert.Greater(bt.C[bt.Count - 1].z, 25.0, $"{g.Name}: forearm bent toward +Z");
            }
        }

        // ================================================================== closed cuffs (critique run06 C-10, E7, A6)
        [Test]
        public void Sleeves_EndInClosedClothCuffsWithoutInk()
        {
            foreach (var g in All)
            {
                var s = g.Shape;
                for (int k = 0; k < s.Sleeves.Length; k++)
                {
                    var sl = s.Sleeves[k];
                    int nc = sl.CuffC.Length, last = sl.Count - 1;
                    Assert.AreEqual(ColossusSpec.CuffAxial.Length, nc, $"{g.Name} sleeve {k}: cuff rings");
                    Assert.AreEqual(sl.CuffStart + nc, sl.RingCount, $"{g.Name} sleeve {k}: cuff rings are loft rings (mesh and shadow)");
                    Assert.AreEqual(0.0, sl.CuffR[nc - 1], 1e-12, $"{g.Name} sleeve {k}: the cuff closes in a pole (no hole)");
                    // a slightly flared hem: wider than the last section, by at most a quarter
                    double hem = 0.0;
                    foreach (double r in sl.CuffR) hem = Math.Max(hem, r);
                    Assert.That(hem / sl.R[last], Is.InRange(1.05, 1.25), $"{g.Name} sleeve {k}: flared hem");
                    // the cuff continues the sleeve outward (each ring farther from the shoulder along the last tangent)
                    for (int q = 0; q < nc; q++)
                    {
                        double along = D3.Dot(sl.CuffC[q] - sl.C[last], sl.T[last]);
                        double prev = q == 0 ? 0.0 : D3.Dot(sl.CuffC[q - 1] - sl.C[last], sl.T[last]);
                        Assert.Greater(along, prev, $"{g.Name} sleeve {k}: cuff ring {q} beyond the previous one");
                    }
                    // the flared cuff keeps the 1.0 m gap to its own robe (exact surface samples with the pleats, 4 per column)
                    double min = double.MaxValue;
                    for (int q = 0; q < nc; q++)
                        for (int j = 0; j < 4 * ColossusSpec.SleeveSides; j++)
                        {
                            D3 p = sl.RingPoint(sl.CuffStart + q, 2.0 * Math.PI * j / (4 * ColossusSpec.SleeveSides));
                            min = Math.Min(min, s.RobeClearance(s.LocalToWorld(p)));
                        }
                    Assert.GreaterOrEqual(min, 1.0, $"{g.Name} sleeve {k}: cuff clearance to its own robe");
                    foreach (double gap in sl.CuffGap) Assert.GreaterOrEqual(gap, 1.0, $"{g.Name} sleeve {k}: cuff ring gap");

                    // critique run07 A-r2-3 / B-r2-6: a rounded, gathered end, not a flat cap. The dome from the groove
                    // (the ring before the first pleated one) to the pole is at least 0.35 of its base radius deep.
                    int groove = Array.FindIndex(ColossusSpec.CuffFold, f => f > 0.0) - 1;
                    Assert.Greater(groove, ColossusSpec.CuffHemRing, "the hem rolls under before the pleated dome");
                    double depth = D3.Dot(sl.CuffC[nc - 1] - sl.CuffC[groove], sl.T[last]);
                    Assert.GreaterOrEqual(depth, 0.35 * sl.CuffR[groove], $"{g.Name} sleeve {k}: deep rounded cuff dome");
                    Assert.GreaterOrEqual(ColossusSpec.CuffFolds, 8, "gathered pleats");
                    Assert.AreEqual(0, ColossusSpec.SleeveSides % ColossusSpec.CuffFolds, "pleat crests on loft columns");
                    Assert.AreEqual(0.0, ColossusSpec.CuffFold[0] + ColossusSpec.CuffFold[ColossusSpec.CuffHemRing] + ColossusSpec.CuffFold[nc - 1], 1e-12, "no pleats on the hem outline and at the pole");
                }
                // INK is only in the hood opening: no INK vertex below the neck ring (no sleeve bores)
                float neck = (float)s.Y(ColossusSpec.HoodEta[0]);
                Assert.Greater(g.Ink.VertexCount, 0, $"{g.Name}: hood recess walls");
                for (int i = 0; i < g.Ink.VertexCount; i++)
                    Assert.Greater(g.Ink.Positions[i].y, neck, $"{g.Name}: INK vertex {i} below the neck (a sleeve bore)");
            }
        }

        [Test]
        public void Sleeves_OutlinesAreSmoothAt4K()
        {
            // critique run07 A-r2-3 / B-r2-6 / E-r2-4: from anywhere on the avenue corridor (half-width 14 m, eye 1.7 m,
            // s 90-240) the chord sagitta of the sleeve / cuff polygon stays under 0.5 px at 3840 x 2160, FOV 64
            double f4k = 1080.0 / Math.Tan(32.0 * Deg);
            double hw = ThreadConstants.HalfWidthAvenue, z0 = ThreadConstants.BeatBoundaries[1], z1 = ThreadConstants.BeatBoundaries[2];
            double worst = 0.0;
            foreach (var g in All)
            {
                var s = g.Shape;
                foreach (var sl in s.Sleeves)
                    for (int i = 0; i < sl.RingCount; i++)
                    {
                        sl.Ring(i, out D3 c, out double r, out _, out _);
                        D3 w = s.LocalToWorld(c);
                        double dx = Math.Max(0.0, Math.Abs(w.x) - hw), dy = w.y - ThreadConstants.EyeHeight;
                        double dz = Math.Max(0.0, Math.Max(z0 - w.z, w.z - z1));
                        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz) - r;
                        double radiusPx = f4k * r / dist;
                        worst = Math.Max(worst, radiusPx * (1.0 - Math.Cos(Math.PI / ColossusSpec.SleeveSides)));
                    }
            }
            Assert.Less(worst, 0.5, "sleeve / cuff chord sagitta at 4K (px)");
        }

        // ================================================================== hood opening (critique run06 E6, PAC-05, C-04)
        [Test]
        public void Hood_BrimFramesARecessedBlankFacePlate()
        {
            foreach (var g in All)
            {
                var s = g.Shape;
                Vector3 fwd = AvenueMesh.V(s.LocalToRoot(s.HeadForward)).normalized;
                Assert.Greater(g.Face.TriangleCount, 0, $"{g.Name}: face plate");
                // the plate centre and its forward coordinate
                Vector3 plateC = Vector3.zero;
                float plateFront = float.MinValue;
                for (int i = 0; i < g.Face.VertexCount; i++) { plateC += g.Face.Positions[i]; plateFront = Mathf.Max(plateFront, Vector3.Dot(g.Face.Positions[i], fwd)); }
                plateC /= g.Face.VertexCount;
                // the brim: the robe surface around the opening stands forward of the plate's front by more than the
                // recess plus the brim at the sides (the 4 m brow lip sits on hood surface that curves back ~1.5 m)
                float brimFront = float.MinValue;
                float faceY0 = (float)s.Y(ColossusSpec.FaceEta - ColossusSpec.FaceHalfEta - ColossusSpec.FaceMarginEta - 6.0);
                float faceY1 = (float)s.Y(ColossusSpec.FaceEta + ColossusSpec.FaceHalfEta + ColossusSpec.FaceMarginEta + 6.0);
                for (int i = 0; i < g.Robe.VertexCount; i++)
                {
                    Vector3 p = g.Robe.Positions[i];
                    if (p.y < faceY0 || p.y > faceY1) continue;
                    brimFront = Mathf.Max(brimFront, Vector3.Dot(p, fwd));
                }
                Assert.GreaterOrEqual(brimFront - plateFront, (float)(ColossusSpec.FaceRecess + ColossusSpec.BrimBase),
                                      $"{g.Name}: depth of the hood opening (brim lip to plate)");
                // seen along the face direction, a dark INK ring frames the plate in every direction: per 30-degree
                // sector around the plate centre the INK reaches at least 8 percent farther out than the plate
                Vector3 ax = Vector3.Cross(fwd, Vector3.up).normalized, ay = Vector3.Cross(ax, fwd).normalized;
                var plateR = new float[Sectors]; var inkR = new float[Sectors];
                for (int i = 0; i < g.Face.VertexCount; i++) Sector(g.Face.Positions[i] - plateC, ax, ay, plateR);
                for (int i = 0; i < g.Ink.VertexCount; i++) Sector(g.Ink.Positions[i] - plateC, ax, ay, inkR);
                for (int b = 0; b < Sectors; b++)
                {
                    Assert.Greater(plateR[b], 5.0f, $"{g.Name}: the face plate is a large blank oval (sector {b})");
                    Assert.GreaterOrEqual(inkR[b], 1.08f * plateR[b], $"{g.Name}: INK ring around the plate (sector {b})");
                }
                // the plate faces forward
                for (int i = 0; i < g.Face.VertexCount; i++) Assert.Greater(Vector3.Dot(g.Face.Normals[i], fwd), 0.3f, $"{g.Name}: face plate normal {i} faces forward");
            }
        }

        [Test]
        public void Sleeves_DoNotIntersectNeighbouringColossi()
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i].Shape;
                for (int j = 0; j < all.Count; j++)
                {
                    if (i == j) continue;
                    var b = all[j].Shape;
                    double minRobe = double.MaxValue, minSleeve = double.MaxValue;
                    foreach (var sl in a.Sleeves)
                        ForSleeveSurface(sl, 2, 24, (c, p) =>
                        {
                            D3 w = a.LocalToWorld(p);
                            // robes: exact clearance only when the point is near the neighbour's axis (the robe is < 24 m in radius)
                            double ax = w.x - b.Cx, az = w.z - b.Cz;
                            if (Math.Sqrt(ax * ax + az * az) < 30.0) minRobe = Math.Min(minRobe, b.RobeClearance(w));
                            foreach (var sb in b.Sleeves)
                                for (int q = 0; q + 1 < sb.Count; q++)
                                {
                                    D3 c0 = b.LocalToWorld(sb.C[q]), c1 = b.LocalToWorld(sb.C[q + 1]), ab = c1 - c0;
                                    double t = ColossusShape.Clamp(D3.Dot(w - c0, ab) / Math.Max(1e-9, D3.Dot(ab, ab)), 0.0, 1.0);
                                    minSleeve = Math.Min(minSleeve, (w - (c0 + ab * t)).Length - (sb.R[q] + (sb.R[q + 1] - sb.R[q]) * t));
                                }
                        });
                    Assert.Greater(minRobe, 1.0, $"{all[i].Name} sleeves vs {all[j].Name} robe");
                    Assert.Greater(minSleeve, 1.0, $"{all[i].Name} sleeves vs {all[j].Name} sleeves");
                }
            }
        }

        // ================================================================== T-SHADE (second part), 14.5.2 f
        [Test]
        public void TShade_SunIsTheBeat2Sun()
        {
            D3 sun = ColossusShadowShape.ShadowSun();
            double s = ColossusSpec.ShadowSunS;
            Assert.That(s, Is.InRange(ThreadConstants.BeatBoundaries[1], ThreadConstants.BeatBoundaries[2]), "sampled inside beat 2");
            D3 expect = ThreadMath.SunDirection(s);
            Assert.AreEqual(0.0, (sun - expect).Length, 1e-12, "ThreadMath.SunDirection");
            Assert.AreEqual(24.0, Math.Asin(sun.y) / Deg, 1e-6, "sun elevation");
            Assert.AreEqual(-35.0, Math.Atan2(sun.x, sun.z) / Deg, 1e-6, "sun azimuth");
            foreach (var g in All) Assert.AreEqual(0.0, (g.ShadowShape.Sun - expect).Length, 1e-12, $"{g.Name}: shadow sun");
        }

        [Test]
        public void TShade_LeftRowBandCoversThePathAxis()
        {
            // polygon point-in-polygon tests along x = 0 (14.5.2 acceptance 6): the merged band of the left row covers
            // z = 42 .. 250 and no shadow at all touches the path axis outside z = 38 .. 255
            var all = All;
            double first = double.NaN, last = double.NaN;
            for (double z = -800.0; z <= 800.0; z += 0.05)
            {
                var p = new V2(0.0, z);
                bool left = false, any = false;
                foreach (var g in all)
                {
                    if (!g.ShadowShape.InsideAny(p)) continue;
                    any = true;
                    if (g.Shape.Side == 0) left = true;
                }
                if (z >= 42.0 && z <= 250.0) Assert.IsTrue(left, $"left-row shade band has a gap at z = {z:F2}");
                if (any) { if (double.IsNaN(first)) first = z; last = z; }
            }
            Assert.GreaterOrEqual(first, 38.0, "shade on the path axis before z = 38");
            Assert.LessOrEqual(last, 255.0, "shade on the path axis after z = 255");
            // the right row throws its shadow away from the path
            for (int i = 1; i < all.Count; i += 2)
                foreach (var poly in all[i].ShadowShape.Outlines)
                    foreach (var v in poly) Assert.Greater(v.x, 0.0, $"{all[i].Name}: shadow stays right of the path");
        }

        [Test]
        public void TShade_TipWithinOneMetreOfTheAnalyticProjection()
        {
            D3 sun = ThreadMath.SunDirection(ColossusSpec.ShadowSunS);
            var dir = new V2(-sun.x, -sun.z);
            dir = dir * (1.0 / dir.Length);
            var across = new V2(dir.z, -dir.x);
            double tan24 = Math.Tan(24.0 * Deg);
            foreach (var g in All)
            {
                // analytic: the generated robe / hood vertex whose projection along -S onto y = 0.03 falls farthest
                // along -S (with the forward bow of the head this is a crown vertex next to the apex)
                V2 tip = default; double tipU = double.MinValue;
                for (int i = 0; i < g.Robe.VertexCount; i++)
                {
                    Vector3 top = World(g, g.Robe.Positions[i]);
                    double drop = (top.y - 0.03) / tan24;
                    var pr = new V2(top.x + dir.x * drop, top.z + dir.z * drop);
                    double u = V2.Dot(pr, dir);
                    if (u > tipU) { tipU = u; tip = pr; }
                }
                // generated: the farthest shadow-mesh vertex along -S, and the farthest outline point
                double meshFar = double.MinValue, outFar = double.MinValue;
                V2 outTip = default;
                for (int i = 0; i < g.Shadow.VertexCount; i++)
                {
                    Vector3 p = World(g, g.Shadow.Positions[i]);
                    meshFar = Math.Max(meshFar, V2.Dot(new V2(p.x, p.z), dir));
                }
                foreach (var v in g.ShadowShape.Outlines[0]) { double u = V2.Dot(v, dir); if (u > outFar) { outFar = u; outTip = v; } }
                Assert.AreEqual(tipU, meshFar, 1.0, $"{g.Name}: mesh tip along -S");
                Assert.AreEqual(tipU, outFar, 1.0, $"{g.Name}: outline tip along -S");
                // across -S the farthest point of a nearly flat crown is ill-conditioned: the 72-column mesh may pick a
                // vertex a column away from the true extreme (<= 1.5 m)
                Assert.AreEqual(0.0, V2.Dot(outTip - tip, across), 1.5, $"{g.Name}: tip lateral offset");
                // the tip is the exact projection of the hood point that casts it (independent formula)
                Assert.AreEqual(0.0, (g.ShadowShape.Tip - g.ShadowShape.AnalyticTip).Length, 1e-6, $"{g.Name}: analytic tip");
                // length from the foot: h / tan(24 deg) (674 m for the nominal 300 m figure); the 2-5 deg tilt and the
                // 19-21 deg forward bow (critique run07) lower the crown and move it toward the sun, so each shadow is
                // 1-5 percent shorter
                double len = V2.Dot(outTip - new V2(g.Shape.Cx, g.Shape.Cz), dir);
                double nominal = ThreadConstants.ColossusShadowLength * g.Shape.Height / 300.0;
                Assert.That(len / nominal, Is.InRange(0.95, 0.99), $"{g.Name}: shadow length");
                D3 tp = g.ShadowShape.TipPoint;
                double lenA = (tp.y - 0.03) / tan24 + V2.Dot(new V2(tp.x - g.Shape.Cx, tp.z - g.Shape.Cz), dir);
                Assert.AreEqual(lenA, len, 1e-6, $"{g.Name}: shadow length from the casting point");
                Assert.AreEqual(lenA, g.ShadowShape.Length, 1e-6, $"{g.Name}: recorded shadow length");
            }
        }

        [Test]
        public void TShade_ShadowMeshIsFlatSoftAndFaded()
        {
            foreach (var g in All)
            {
                var sh = g.ShadowShape;
                Assert.AreEqual(0, sh.Folds, $"{g.Name}: shadow strips never fold");
                Assert.AreEqual(3, sh.Outlines.Count, $"{g.Name}: robe + hood strip and two sleeve bars");
                float maxA = 0f;
                for (int i = 0; i < g.Shadow.VertexCount; i++)
                {
                    Vector3 p = World(g, g.Shadow.Positions[i]);
                    Color c = g.Shadow.Colors[i];
                    Assert.AreEqual(0.03, p.y, 1e-5, $"{g.Name}: shadow height 0.03 m");
                    Assert.That(c.a, Is.InRange(0f, 1f), $"{g.Name}: alpha");
                    if (p.z >= 240f) Assert.AreEqual(0f, c.a, 1e-6f, $"{g.Name}: faded out beyond z = 240");
                    if (p.z <= 210f) maxA = Mathf.Max(maxA, c.a);
                    Assert.Greater(g.Shadow.Normals[i].y, 0.999f, $"{g.Name}: up normal");
                }
                Assert.AreEqual(1f, maxA, 1e-6f, $"{g.Name}: full shade before the fade");
                for (int t = 0; t < g.Shadow.Indices.Count; t += 3)
                {
                    Vector3 a = g.Shadow.Positions[g.Shadow.Indices[t]], b = g.Shadow.Positions[g.Shadow.Indices[t + 1]], c = g.Shadow.Positions[g.Shadow.Indices[t + 2]];
                    Assert.Greater(Vector3.Cross(b - a, c - a).y, 0f, $"{g.Name}: shadow triangle faces up");
                }
            }
        }

        [Test]
        public void TShade_ShadowsFadeOutTowardTheirTips()
        {
            // critique run06 C-05 / E8: no shadow may end in a hard line near the horizon. The shade fades to zero over
            // the last 40 percent of each shadow's length, and the soft edge widens with the distance from the foot.
            D3 sun = ThreadMath.SunDirection(ColossusSpec.ShadowSunS);
            var dir = new V2(-sun.x, -sun.z);
            dir = dir * (1.0 / dir.Length);
            Assert.AreEqual(ColossusSpec.ShadowPenumbra0, ColossusShadowShape.PenumbraAt(0.0), 1e-12, "soft edge at the foot");
            Assert.AreEqual(ColossusSpec.ShadowPenumbra1 + ColossusSpec.ShadowPenumbraFar, ColossusShadowShape.PenumbraAt(ThreadConstants.ColossusShadowLength), 1e-9, "soft edge at 674 m");
            for (double u = 0; u < 700; u += 10) Assert.Greater(ColossusShadowShape.PenumbraAt(u + 10), ColossusShadowShape.PenumbraAt(u) - 1e-12, "soft edge widens");
            foreach (var g in All)
            {
                var sh = g.ShadowShape;
                double L = sh.Length;
                Assert.Greater(L, 600.0, $"{g.Name}: shadow length");
                var foot = new V2(g.Shape.Cx, g.Shape.Cz);
                double maxNearTip = 0.0, maxMid = 0.0;
                for (int i = 0; i < sh.Vertices.Count; i++)
                {
                    double u = V2.Dot(sh.Vertices[i] - foot, dir), a = sh.Alpha[i];
                    Assert.LessOrEqual(a, ColossusShadowShape.LengthFade(u, L) + 1e-9, $"{g.Name}: vertex {i} above the length fade");
                    if (u >= 0.95 * L) maxNearTip = Math.Max(maxNearTip, a);
                    if (u <= 0.55 * L && V2.Dot(sh.Vertices[i], new V2(0, 1)) < ThreadConstants.ColossusShadowFadeZ0) maxMid = Math.Max(maxMid, a);
                }
                Assert.Less(maxNearTip, 0.05, $"{g.Name}: shade near the tip");
                Assert.AreEqual(1.0, maxMid, 1e-9, $"{g.Name}: full shade before the length fade");
                Assert.AreEqual(1.0, ColossusShadowShape.LengthFade(0.6 * L, L), 1e-12, $"{g.Name}: fade starts at 60 percent");
                Assert.AreEqual(0.0, ColossusShadowShape.LengthFade(L, L), 1e-12, $"{g.Name}: fade ends at the tip");
            }
        }

        // ================================================================== T-NORMALS
        [Test]
        public void TNormals_ColossusMeshesAreValid()
        {
            foreach (var g in All)
            {
                var meshes = new[] { g.CreateBodyMesh(), g.CreateHemMesh(), g.CreateHemProxyMesh(), g.CreateFootMesh(), g.CreateShadowMesh() };
                foreach (var m in meshes)
                {
                    Temp(m);
                    Assert.IsTrue(MeshKit.Validate(m, out string report), report);
                }
                Assert.AreEqual(3, meshes[0].subMeshCount, $"{g.Name}: body submeshes robe / ink / face plate");
                Assert.AreEqual(4, meshes[3].subMeshCount, $"{g.Name}: foot submeshes stone / bollard / alcove / door frame");
                // bounds padded for the 5 cm hem sway
                Bounds tight = TightBounds(g.Robe, g.Ink, g.Face);
                Assert.GreaterOrEqual(meshes[0].bounds.extents.x - tight.extents.x, 0.05f - 1e-4f, $"{g.Name}: body bounds padding");
            }
        }

        [Test]
        public void TNormals_SeamsDuplicatedWithMatchingNormals()
        {
            foreach (var g in All)
            {
                CheckSeams(g.Robe, g.RobeSeams, g.Name + " robe");
                CheckSeams(g.Hem, g.HemSeams, g.Name + " hem");
                CheckSeams(g.Bollard, g.BollardSeams, g.Name + " bollards");
                // watertight up to the expected open boundaries (hem crease ring of the body; hem top, bottom and doorway)
                Assert.AreEqual(g.BodyOpenEdges, OpenEdges(g.Robe, g.Ink, g.Face), $"{g.Name}: body open edges");
                Assert.AreEqual(g.HemOpenEdges, OpenEdges(g.Hem), $"{g.Name}: hem open edges");
                // the hem lathe faces outward everywhere
                for (int i = 0; i < g.Hem.VertexCount; i++)
                {
                    Vector3 p = g.Hem.Positions[i], n = g.Hem.Normals[i];
                    Assert.Greater(n.x * p.x + n.z * p.z, 0f, $"{g.Name}: hem normal {i} points outward");
                }
            }
        }

        [Test]
        public void TNormals_VertexColoursFollowTheContract()
        {
            foreach (var g in All)
            {
                var s = g.Shape;
                double y150 = s.Y(ColossusSpec.FluteTopEta);
                foreach (var mb in new[] { g.Robe, g.Ink, g.Face, g.Hem, g.Stone, g.Bollard, g.AlcoveInk, g.DoorFrame })
                    for (int i = 0; i < mb.VertexCount; i++)
                    {
                        Color c = mb.Colors[i];
                        Assert.That(c.r, Is.InRange(0.55f, 1f), $"{g.Name}: AO");
                        Assert.That(c.g, Is.InRange(0f, 1f), $"{g.Name}: height shade");
                        Assert.That(c.b, Is.InRange(0f, 1f), $"{g.Name}: sway weight");
                        Assert.AreEqual(1f, c.a, $"{g.Name}: alpha");
                        if (mb.Positions[i].y > y150 + 1e-3) Assert.AreEqual(0f, c.b, 1e-6f, $"{g.Name}: no sway above y 150");
                    }
                // sway weight 1 on the hem flutes, height shade 1 at the base and below 0.1 at the top
                float maxB = 0f;
                for (int i = 0; i < g.Hem.VertexCount; i++) maxB = Mathf.Max(maxB, g.Hem.Colors[i].b);
                Assert.AreEqual(1f, maxB, 1e-4f, $"{g.Name}: hem sway weight");
                float gTop = 1f;
                for (int i = 0; i < g.Robe.VertexCount; i++) if (g.Robe.Positions[i].y > g.TopY - 1f) gTop = Mathf.Min(gTop, g.Robe.Colors[i].g);
                Assert.Less(gTop, 0.1f, $"{g.Name}: height shade at the top");
                for (int i = 0; i < g.HemProxy.VertexCount; i++) Assert.AreEqual(0f, g.HemProxy.Colors[i].b, $"{g.Name}: shadow proxy never sways");
            }
        }

        // ================================================================== 14.7.9 budgets
        [Test]
        public void Budget_TrianglesPerColossus()
        {
            long body = 0, shadows = 0;
            foreach (var g in All)
            {
                // about 11 k (14.5.2 f) plus ~2.9 k for the 48-sided sleeves and the gathered cuffs (critique run07)
                Assert.That(g.ColossusTriangles, Is.InRange(8000, 14500), $"{g.Name}: about 14 k triangles");
                Assert.That(g.Shadow.TriangleCount, Is.InRange(100, 700), $"{g.Name}: ground shadow triangles");
                body += g.ColossusTriangles;
                shadows += g.Shadow.TriangleCount;
            }
            Assert.LessOrEqual(body, 8 * 14500, "colossi budget (88 k + the smooth sleeves)");
            Assert.LessOrEqual(shadows, 4000 * 3 / 2, "ground shadows budget about 4 k");
        }

        // ================================================================== beat 1: salt ground (14.5.1 a)
        [Test]
        public void SaltGround_PolarGridLayout()
        {
            Assert.AreEqual(64, SaltGround.Sectors, "64 sectors");
            CollectionAssert.AreEqual(new[] { 6f, 15f, 40f, 100f, 250f, 600f, 1800f, 5000f, 14000f }, SaltGround.RingRadii, "ring radii");
            CollectionAssert.AreEqual(new[] { 2.4f, 24f, 240f, 2400f }, SaltGround.GroupSnap, "snap cells");
            float[] groupOuter = { 40f, 600f, 5000f, 14000f };
            long tris = 0;
            for (int g = 0; g < SaltGround.GroupCount; g++)
            {
                Assert.AreEqual(groupOuter[g], SaltGround.GroupOuterRadius(g), $"group {g} outer ring");
                if (g == 0) Assert.AreEqual(0f, SaltGround.GroupInnerRadius(0), "group 0 starts at the centre");
                else Assert.AreEqual(groupOuter[g - 1] - SaltGround.GroupSnap[g], SaltGround.GroupInnerRadius(g), 1e-3f, $"group {g} extends inward by one snap cell");
                Assert.AreEqual(-0.001f * g, SaltGround.GroupY(g), 1e-7f, $"group {g}: 1 mm lower per group (no z-fight in the overlap)");
                MeshBuilder mb = SaltGround.BuildGroup(g);
                tris += mb.TriangleCount;
                for (int i = 0; i < mb.VertexCount; i++)
                {
                    Assert.AreEqual(0f, mb.Positions[i].y, $"group {g}: flat");
                    Assert.Greater(mb.Normals[i].y, 0.9999f, $"group {g}: up");
                    Assert.AreEqual(0f, mb.Colors[i].b, $"group {g}: no sway");
                }
                for (int t = 0; t < mb.Indices.Count; t += 3)
                {
                    Vector3 a = mb.Positions[mb.Indices[t]], b = mb.Positions[mb.Indices[t + 1]], c = mb.Positions[mb.Indices[t + 2]];
                    Assert.Greater(Vector3.Cross(b - a, c - a).y, 0f, $"group {g}: triangle faces up");
                }
            }
            Assert.LessOrEqual(tris, 1600, "about 1.2 k triangles (+ one overlap strip per outer group)");
        }

        [Test]
        public void SaltGround_MeshesAreValid()
        {
            for (int g = 0; g < SaltGround.GroupCount; g++)
            {
                Mesh m = Temp(SaltGround.BuildGroup(g).ToMesh("SaltGround_" + g));
                Assert.IsTrue(MeshKit.Validate(m, out string report), report);
            }
        }

        [Test]
        public void SaltGround_SnappedGroupsAlwaysCoverThePlane()
        {
            // whatever the camera position, every point within 12.25 km is covered by at least one snapped group (the
            // outer group's 14 km edge moves by up to 1.7 km with its 2.4 km snap; out there the ground equals the fog)
            var rng = new System.Random(1234);
            for (int it = 0; it < 6000; it++)
            {
                var cam = new Vector2((float)(rng.NextDouble() * 600 - 150), (float)(rng.NextDouble() * 900 - 150));
                double rad = Math.Exp(rng.NextDouble() * Math.Log(12250.0));
                double ang = rng.NextDouble() * 2 * Math.PI;
                var p = cam + new Vector2((float)(Math.Sin(ang) * rad), (float)(Math.Cos(ang) * rad));
                bool covered = false;
                for (int g = 0; g < SaltGround.GroupCount && !covered; g++)
                {
                    float cell = SaltGround.GroupSnap[g];
                    covered = SaltGround.Covers(g, new Vector2(SaltGround.Snap(cam.x, cell), SaltGround.Snap(cam.y, cell)), p);
                }
                Assert.IsTrue(covered, $"camera {cam}, point {p} ({rad:F1} m away) not covered");
            }
        }

        [Test]
        public void SaltGround_FollowerSnapsEachGroupToItsCell()
        {
            var root = Temp(new GameObject("SaltFlatTest")).transform;
            root.position = new Vector3(0f, -123.5f, 0f);   // the ground group has sunk: only XZ may follow the camera
            var groups = new Transform[SaltGround.GroupCount];
            for (int g = 0; g < groups.Length; g++)
            {
                groups[g] = new GameObject("g" + g).transform;
                groups[g].SetParent(root, false);
            }
            var follower = new SaltGroundFollower(root, groups);
            var rng = new System.Random(99);
            for (int it = 0; it < 200; it++)
            {
                var cam = new Vector3((float)(rng.NextDouble() * 400 - 100), (float)(rng.NextDouble() * 200), (float)(rng.NextDouble() * 900 - 100));
                var f = new FrameState { cameraPos = cam, groundActive = true };
                follower.OnFrame(in f);
                for (int g = 0; g < groups.Length; g++)
                {
                    float cell = SaltGround.GroupSnap[g];
                    Vector3 lp = groups[g].localPosition;
                    Assert.AreEqual(0f, Mathf.Abs(lp.x / cell - Mathf.Round(lp.x / cell)), 1e-3f, $"group {g} x on its lattice");
                    Assert.AreEqual(0f, Mathf.Abs(lp.z / cell - Mathf.Round(lp.z / cell)), 1e-3f, $"group {g} z on its lattice");
                    Assert.LessOrEqual(Mathf.Abs(lp.x - cam.x), 0.5f * cell + 1e-3f, $"group {g} follows x");
                    Assert.LessOrEqual(Mathf.Abs(lp.z - cam.z), 0.5f * cell + 1e-3f, $"group {g} follows z");
                    Assert.AreEqual(SaltGround.GroupY(g), lp.y, 1e-6f, $"group {g} height");
                }
            }
        }

        // ================================================================== helpers
        const int Sectors = 12;

        /// <summary>Largest radius (perpendicular to the face direction) of d per 360 / Sectors degree sector around it.</summary>
        static void Sector(Vector3 d, Vector3 ax, Vector3 ay, float[] maxR)
        {
            float x = Vector3.Dot(d, ax), y = Vector3.Dot(d, ay);
            float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            int b = ((int)Mathf.Floor((a + 360f) / (360f / Sectors))) % Sectors;
            maxR[b] = Mathf.Max(maxR[b], Mathf.Sqrt(x * x + y * y));
        }

        /// <summary>Calls visit(centre, point) for surface points of a sleeve: each loft segment split in 'subs', 'around' points per ring.</summary>
        static void ForSleeveSurface(SleeveShape sl, int subs, int around, Action<D3, D3> visit)
        {
            for (int q = 0; q + 1 < sl.Count; q++)
                for (int sub = 0; sub < subs; sub++)
                {
                    double f = sub / (double)subs;
                    D3 c = sl.C[q] * (1 - f) + sl.C[q + 1] * f;
                    double r = sl.R[q] * (1 - f) + sl.R[q + 1] * f;
                    D3 n = (sl.N[q] * (1 - f) + sl.N[q + 1] * f).Normalized, b = (sl.B[q] * (1 - f) + sl.B[q + 1] * f).Normalized;
                    for (int j = 0; j < around; j++)
                    {
                        double ph = 2.0 * Math.PI * j / around;
                        // the mesh is a 24-gon: its flat facets lie inside the circle by cos(pi / 24)
                        visit(c, c + (n * Math.Cos(ph) + b * Math.Sin(ph)) * (r * Math.Cos(Math.PI / ColossusSpec.SleeveSides)));
                    }
                }
            int last = sl.Count - 1;
            for (int j = 0; j < around; j++)
            {
                double ph = 2.0 * Math.PI * j / around;
                visit(sl.C[last], sl.C[last] + (sl.N[last] * Math.Cos(ph) + sl.B[last] * Math.Sin(ph)) * sl.R[last]);
            }
            // the closed cuff rings (exact surface points with the pleats)
            for (int q = 0; q < sl.CuffC.Length; q++)
                for (int j = 0; j < around; j++)
                    visit(sl.CuffC[q], sl.RingPoint(sl.CuffStart + q, 2.0 * Math.PI * j / around));
        }

        /// <summary>True when a triangle of mb (in its plane, normal close to n) contains point p.</summary>
        static bool CoversPoint(MeshBuilder mb, Vector3 p, Vector3 n)
        {
            for (int t = 0; t < mb.Indices.Count; t += 3)
            {
                Vector3 a = mb.Positions[mb.Indices[t]], b = mb.Positions[mb.Indices[t + 1]], c = mb.Positions[mb.Indices[t + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                if (fn.sqrMagnitude < 1e-12f) continue;
                fn.Normalize();
                if (Mathf.Abs(Vector3.Dot(fn, n)) < 0.9f) continue;
                if (Mathf.Abs(Vector3.Dot(p - a, fn)) > 0.05f) continue;
                Vector3 q = p - fn * Vector3.Dot(p - a, fn);
                float d0 = Vector3.Dot(Vector3.Cross(b - a, q - a), fn), d1 = Vector3.Dot(Vector3.Cross(c - b, q - b), fn), d2 = Vector3.Dot(Vector3.Cross(a - c, q - c), fn);
                if ((d0 >= 0 && d1 >= 0 && d2 >= 0) || (d0 <= 0 && d1 <= 0 && d2 <= 0)) return true;
            }
            return false;
        }

        static void CheckSeams(MeshBuilder mb, List<Vector2Int> seams, string name)
        {
            Assert.Greater(seams.Count, 0, name + ": seam pairs recorded");
            foreach (var s in seams)
            {
                Assert.AreNotEqual(s.x, s.y, name + ": seam vertex duplicated");
                Assert.AreEqual(0f, (mb.Positions[s.x] - mb.Positions[s.y]).magnitude, 1e-6f, name + ": seam positions");
                Assert.AreEqual(0f, (mb.Normals[s.x] - mb.Normals[s.y]).magnitude, 1e-6f, name + ": seam normals match");
                Assert.AreEqual(mb.Colors[s.x], mb.Colors[s.y], name + ": seam colours match");
            }
        }

        /// <summary>Edges used by exactly one triangle after welding coincident positions (0.1 mm), over several builders.</summary>
        static int OpenEdges(params MeshBuilder[] parts)
        {
            var weld = new Dictionary<(long, long, long), int>();
            var edges = new Dictionary<long, int>();
            foreach (var mb in parts)
            {
                var canon = new int[mb.VertexCount];
                for (int i = 0; i < mb.VertexCount; i++)
                {
                    Vector3 p = mb.Positions[i];
                    var key = ((long)Math.Round(p.x * 1e4), (long)Math.Round(p.y * 1e4), (long)Math.Round(p.z * 1e4));
                    if (!weld.TryGetValue(key, out int id)) { id = weld.Count; weld.Add(key, id); }
                    canon[i] = id;
                }
                for (int t = 0; t + 2 < mb.Indices.Count; t += 3)
                {
                    int a = canon[mb.Indices[t]], b = canon[mb.Indices[t + 1]], c = canon[mb.Indices[t + 2]];
                    AddEdge(edges, a, b); AddEdge(edges, b, c); AddEdge(edges, c, a);
                }
            }
            int open = 0;
            foreach (var kv in edges) if (kv.Value == 1) open++;
            return open;
        }

        static void AddEdge(Dictionary<long, int> e, int a, int b)
        {
            long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            e.TryGetValue(k, out int c);
            e[k] = c + 1;
        }

        static Bounds TightBounds(params MeshBuilder[] parts)
        {
            var b = new Bounds(parts[0].Positions[0], Vector3.zero);
            foreach (var mb in parts) for (int i = 0; i < mb.VertexCount; i++) b.Encapsulate(mb.Positions[i]);
            return b;
        }
    }
}
