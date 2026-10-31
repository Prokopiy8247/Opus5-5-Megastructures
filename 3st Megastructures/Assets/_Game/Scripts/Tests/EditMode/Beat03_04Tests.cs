// EditMode tests of the beat 3 and beat 4 geometry (MASTER_PROMPT 14.10.1 T-SCALE-1, T-SCALE-3, T-NORMALS,
// 14.5.4 acceptance 1 and 5, 14.7.9 budgets, 8.7 co-planarity / cracks; critique run06 A2: the terrace's flat
// ground shadow).
// The expected values below are the specification's numbers on purpose: the tests check that the
// constants and generators reproduce them.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Megastructure.Beats;

namespace Megastructure.Tests
{
    public class Beat03_04Tests
    {
        static CableSet s_cable;
        static CableSet Cable { get { if (s_cable == null) s_cable = CableBuilder.Build(); return s_cable; } }

        readonly List<Mesh> m_temp = new List<Mesh>();

        Mesh Temp(MeshBuilder mb, string name) { var m = mb.ToMesh(name); m_temp.Add(m); return m; }

        [TearDown]
        public void DestroyTemp()
        {
            for (int i = 0; i < m_temp.Count; i++) if (m_temp[i] != null) UnityEngine.Object.DestroyImmediate(m_temp[i]);
            m_temp.Clear();
        }

        [OneTimeTearDown]
        public void DestroyCable()
        {
            if (s_cable != null) s_cable.DestroyAll();
            s_cable = null;
        }

        // ================================================================== beat 3: stairs
        [Test]
        public void TScale1_Stairs()
        {
            MeshBuilder mb = StairBuilder.BuildSteps();
            var treadY = new SortedSet<int>();
            var riserZ = new SortedSet<int>();
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i], n = mb.Normals[i];
                if (n.y > 0.999f) treadY.Add(Mathf.RoundToInt(p.y * 10000f));
                if (n.z < -0.999f) riserZ.Add(Mathf.RoundToInt(p.z * 10000f));
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            }
            Assert.AreEqual(200, treadY.Count, "number of tread levels");
            Assert.AreEqual(200, riserZ.Count, "number of riser planes");

            var ys = new List<double>(); foreach (int v in treadY) ys.Add(v / 10000.0);
            var zs = new List<double>(); foreach (int v in riserZ) zs.Add(v / 10000.0);
            Assert.AreEqual(0.18, ys[0] - 0.0, 0.002, "first riser above the salt");
            for (int i = 1; i < ys.Count; i++) Assert.AreEqual(0.18, ys[i] - ys[i - 1], 0.002, $"riser {i + 1}");
            Assert.AreEqual(36.0, ys[ys.Count - 1], 0.01, "total rise");
            Assert.AreEqual(240.0, zs[0], 0.002, "foot of the stairs");
            for (int i = 1; i < zs.Count; i++) Assert.AreEqual(0.40, zs[i] - zs[i - 1], 0.002, $"tread {i}");
            Assert.AreEqual(40.0, maxX - minX, 0.05, "width");

            // 200 tread faces (one quad each)
            int upTris = 0;
            for (int t = 0; t < mb.Indices.Count; t += 3)
                if (mb.Normals[mb.Indices[t]].y > 0.999f && mb.Normals[mb.Indices[t + 1]].y > 0.999f && mb.Normals[mb.Indices[t + 2]].y > 0.999f) upTris++;
            Assert.AreEqual(400, upTris, "tread triangles");

            // the stair body enters the salt (skirt) and no step face lies in the salt plane y = 0
            float minY = float.MaxValue;
            for (int i = 0; i < mb.VertexCount; i++) minY = Mathf.Min(minY, mb.Positions[i].y);
            Assert.AreEqual(-4.0, minY, 1e-4, "skirt depth");
            for (int i = 0; i < mb.VertexCount; i++)
                if (mb.Normals[i].y > 0.999f) Assert.Greater(mb.Positions[i].y, 0.1f, "no tread co-planar with the salt");
        }

        [Test]
        public void Stairs_BollardsAndNosings()
        {
            var bases = new List<Vector3>();
            StairBuilder.BollardBases(bases);
            Assert.AreEqual(12, bases.Count, "bollard instances");
            for (int i = 0; i < bases.Count; i++)
            {
                Vector3 b = bases[i];
                Assert.Less(Mathf.Abs(b.x), 20f - 0.175f, "bollard inside the stair width");
                Assert.Greater(Mathf.Abs(b.x), 10f + 0.35f, "bollard outside the 10 m corridor");
                int k = i % StairBuilder.BollardsPerEdge;
                Assert.AreEqual(240.0 + 16.0 * k + 0.2, b.z, 1e-3, "bollard every 16 m from z = 240");
                // stands on its tread (or the terrace) and clears the next riser and the nosing chamfer
                double zLocal = (b.z - 240.0) % 0.4;
                Assert.Greater(zLocal - 0.175, 0.02, "clear of the nosing");
                Assert.Less(zLocal + 0.175, 0.4, "clear of the next riser");
                double expectY = Math.Min(36.0, 0.18 * (Math.Floor((b.z - 240.0) / 0.4) + 1));
                Assert.AreEqual(expectY, b.y, 1e-3, "bollard base on the walking surface");
            }
            // walk-line bollards (D-050 / D-053): 2 x 17 at x = +-4.75 (outside the 4 m stair walk plus the player radius),
            // every 4 m from z = 256, each on its walking surface and clear of the nosing and the next riser
            var walk = new List<Vector3>();
            StairBuilder.WalkLineBollardBases(walk);
            Assert.AreEqual(34, walk.Count, "walk-line bollards");
            for (int i = 0; i < walk.Count; i++)
            {
                Vector3 b = walk[i];
                Assert.AreEqual(4.75, Mathf.Abs(b.x), 1e-4, "walk line at x = +-4.75");
                Assert.Greater(Mathf.Abs(b.x) - 0.175f, (float)ThreadMath.HalfWidth(b.z) + 0.35f + 0.05f, "clear of the corridor and the player capsule");
                int k = i % StairBuilder.WalkLinePerSide;
                Assert.AreEqual(256.0 + 4.0 * k + 0.2, b.z, 1e-3, "one pair every 4 m from z = 256");
                double zLocal = (b.z - 240.0) % 0.4;
                Assert.Greater(zLocal - 0.175, 0.02, "clear of the nosing");
                Assert.Less(zLocal + 0.175, 0.4, "clear of the next riser");
                double expectY = Math.Min(36.0, 0.18 * (Math.Floor((b.z - 240.0) / 0.4) + 1));
                Assert.AreEqual(expectY, b.y, 1e-3, "walk-line bollard base on the walking surface");
            }
            Mesh m = Temp(StairBuilder.BuildBollards(), "bollards");
            Bounds bb = m.bounds;
            Assert.AreEqual(36.0 + 1.0, bb.max.y, 1e-3, "top bollard is 1.0 m tall on the terrace");
            // bollard profile: 1.0 m tall above the surface, 0.35 m diameter
            var prof = StairBuilder.BollardProfile();
            Assert.AreEqual(1.0, prof[prof.Count - 1].x, 1e-5);
            Assert.AreEqual(0.175, prof[0].y, 1e-5);

            // nosing chamfer 0.02 m at 45 degrees
            MeshBuilder steps = StairBuilder.BuildSteps();
            int noses = 0;
            for (int i = 0; i < steps.VertexCount; i++)
            {
                Vector3 n = steps.Normals[i];
                if (n.y > 0.5f && n.y < 0.9f) { noses++; Assert.AreEqual(-n.y, n.z, 1e-4, "45 degree nosing"); }
            }
            Assert.AreEqual(200 * 4, noses, "one chamfer quad per nosing");
        }

        [Test]
        public void StairTerraceJoint_SharedEdgeWithoutOverlap()
        {
            MeshBuilder steps = StairBuilder.BuildSteps();
            MeshBuilder stringers = StairBuilder.BuildStringers();
            MeshBuilder top = TerraceBuilder.BuildTop();
            MeshBuilder sides = TerraceBuilder.BuildSides();
            float jz = TerraceBuilder.JointZ;
            Assert.AreEqual(320.05, jz, 1e-4, "joint = terrace front edge after its 0.05 m chamfer");

            float stepMaxZ = float.MinValue;
            for (int i = 0; i < steps.VertexCount; i++) stepMaxZ = Mathf.Max(stepMaxZ, steps.Positions[i].z);
            for (int i = 0; i < stringers.VertexCount; i++) stepMaxZ = Mathf.Max(stepMaxZ, stringers.Positions[i].z);
            Assert.AreEqual(jz, stepMaxZ, "stair ends exactly on the joint (bitwise)");

            float topMinZ = float.MaxValue;
            for (int i = 0; i < top.VertexCount; i++) topMinZ = Mathf.Min(topMinZ, top.Positions[i].z);
            Assert.AreEqual(jz, topMinZ, "terrace top starts exactly on the joint (bitwise)");

            // the shared edge: (+-20, 36, jz) exist bitwise in both meshes; no terrace vertex strictly between them
            var a = new Vector3(-20f, 36f, jz); var b = new Vector3(20f, 36f, jz);
            Assert.IsTrue(HasExact(steps, a) && HasExact(steps, b), "stair top tread has the joint corners");
            Assert.IsTrue(HasExact(top, a) && HasExact(top, b), "terrace top has the joint corners");
            for (int i = 0; i < top.VertexCount; i++)
            {
                Vector3 p = top.Positions[i];
                if (p.z == jz) Assert.IsFalse(p.x > -20f && p.x < 20f, "no T-junction on the shared edge");
            }
            for (int i = 0; i < steps.VertexCount; i++)
            {
                Vector3 p = steps.Positions[i];
                if (p.z == jz) Assert.IsTrue(p.x == -20f || p.x == 20f, "stair back edge is one edge");
            }
            // both tops are at y = 36 and only abut (steps z <= jz, terrace z >= jz) -> no co-planar overlap
            for (int i = 0; i < steps.VertexCount; i++) Assert.LessOrEqual(steps.Positions[i].z, jz);
            for (int i = 0; i < top.VertexCount; i++)
            {
                Assert.GreaterOrEqual(top.Positions[i].z, jz);
                Assert.AreEqual(36f, top.Positions[i].y);
            }
            // side walls are never co-planar: stair stringers at x = +-20, terrace walls at x = -60 / 190
            for (int i = 0; i < stringers.VertexCount; i++) Assert.AreEqual(20f, Mathf.Abs(stringers.Positions[i].x));
            for (int i = 0; i < sides.VertexCount; i++)
            {
                Vector3 n = sides.Normals[i], p = sides.Positions[i];
                if (Mathf.Abs(n.x) > 0.999f) Assert.IsTrue(p.x == -60f || p.x == 190f, "terrace side walls");
            }
            // terrace extents
            Bounds tb = Temp(sides, "sides").bounds;
            Assert.AreEqual(-60f, tb.min.x, 1e-3); Assert.AreEqual(190f, tb.max.x, 1e-3);
            Assert.AreEqual(320f, tb.min.z, 1e-3); Assert.AreEqual(520f, tb.max.z, 1e-3);
            Assert.AreEqual(-4f, tb.min.y, 1e-3); Assert.AreEqual(36f, tb.max.y, 1e-3);
        }

        [Test]
        public void Terrace_WatertightAcrossMeshes()
        {
            // top + sides welded: the only open edges are the wall bottoms (y = -4, inside the salt) and the
            // inner hole at r = 49 m around the cable axis (inside the cable); everything else is shared
            var meshes = new List<Mesh> { Temp(TerraceBuilder.BuildTop(), "top"), Temp(TerraceBuilder.BuildSides(), "sides") };
            var open = OpenEdges(meshes, out int nonManifold, out var openList);
            Assert.AreEqual(0, nonManifold, "non-manifold edges");
            var axis = new Vector3((float)ThreadConstants.CableAxisX, 0f, (float)ThreadConstants.CableAxisZ);
            int bottom = 0, hole = 0, other = 0;
            for (int i = 0; i < openList.Count; i++)
            {
                Vector3 p = openList[i].Item1, q = openList[i].Item2;
                if (p.y < -3.99f && q.y < -3.99f) bottom++;
                else if (Radius(p - axis) < 49.01 && Radius(q - axis) < 49.01) hole++;
                else other++;
            }
            Assert.AreEqual(TerraceBuilder.FootSegments, hole, "hole inside the cable");
            Assert.AreEqual(0, other, "unexpected open edges (cracks)");
            Assert.Greater(bottom, 0, "wall bottoms");
            Assert.AreEqual(open, bottom + hole + other);
            // the polar foot region never reaches the terrace edges, and the AO ring stays inside it
            Assert.Less(TerraceBuilder.FootRings[TerraceBuilder.FootRings.Length - 1], ThreadConstants.CableAxisZ - TerraceBuilder.JointZ);
            Assert.LessOrEqual(TerraceBuilder.FootAoR1, TerraceBuilder.FootRings[TerraceBuilder.FootRings.Length - 1]);
            Assert.Less(TerraceBuilder.FootRings[0], ThreadConstants.CableRadius, "open hole is inside the cable");
        }

        // ================================================================== beat 4: terrace ground shadow (critique run06 A2)
        [Test]
        public void TerraceGroundShadow_ProjectsTheSlabAlongTheBeat5Sun()
        {
            Mesh m = Temp(TerraceBuilder.BuildGroundShadow(), "terraceShadow");
            Assert.IsTrue(MeshKit.Validate(m, out string report), report);
            Assert.Less(MeshKit.TriangleCount(m), 1000L, "a few hundred triangles");
            var v = m.vertices;
            var nrm = m.normals;
            var col = m.colors;
            int[] idx = m.GetIndices(0);
            float yPlane = (float)ThreadConstants.ColossusShadowY;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.AreEqual(yPlane, v[i].y, "flat on the salt-shadow plane (0.03 m above the salt, 14.5.2 f)");
                Assert.AreEqual(1f, nrm[i].y, 1e-6f, "normal up");
                Assert.That(col[i].a, Is.InRange(0f, 1f), "alpha = coverage");
            }
            for (int t = 0; t + 2 < idx.Length; t += 3)
                Assert.Greater(Vector3.Cross(v[idx[t + 1]] - v[idx[t]], v[idx[t + 2]] - v[idx[t]]).y, 0f, "every triangle faces up");

            // projection of the top outline (y = 36) along -S of the beat-5 sun onto y = 0.03: (36 - 0.03) / tan e
            D3 sun = TerraceBuilder.ShadowSun();
            D3 beat5 = ThreadMath.SunDirection(TerraceBuilder.ShadowSunS);
            Assert.AreEqual(beat5.x, sun.x, 1e-12); Assert.AreEqual(beat5.y, sun.y, 1e-12); Assert.AreEqual(beat5.z, sun.z, 1e-12);
            Assert.That(TerraceBuilder.ShadowSunS, Is.InRange(ThreadConstants.BeatBoundaries[4], ThreadConstants.BeatBoundaries[5]), "a beat-5 sun");
            double e = ThreadMath.SunElevationDeg(TerraceBuilder.ShadowSunS) * ThreadMath.Deg;
            TerraceBuilder.ShadowOffset(out double ox, out double oz);
            double L = Math.Sqrt(ox * ox + oz * oz);
            Assert.AreEqual((ThreadConstants.TerraceY - ThreadConstants.ColossusShadowY) / Math.Tan(e), L, 1e-6, "shadow length");
            double ux = ox / L, uz = oz / L;
            Assert.Less(ux * sun.x + uz * sun.z, 0.0, "the shadow falls away from the sun");
            double pFar = TerraceBuilder.ShadowPenumbra0 + TerraceBuilder.ShadowPenumbraGrowth * L;

            double x0 = ThreadConstants.TerraceXMin, x1 = ThreadConstants.TerraceXMax, z0 = ThreadConstants.TerraceZMin, z1 = ThreadConstants.TerraceZMax;
            // walls: midpoint (x, z) and outward normal (x, z); front, right, back, left
            double[,] walls = { { 0.5 * (x0 + x1), z0, 0, -1 }, { x1, 0.5 * (z0 + z1), 1, 0 }, { 0.5 * (x0 + x1), z1, 0, 1 }, { x0, 0.5 * (z0 + z1), -1, 0 } };
            var lit = new bool[4];
            int shaded = 0;
            for (int w = 0; w < 4; w++)
            {
                double mx = walls[w, 0], mz = walls[w, 1], nx = walls[w, 2], nz = walls[w, 3];
                lit[w] = nx * sun.x + nz * sun.z > 0.0;
                if (lit[w])
                {
                    // the sunlit salt is never darkened: the shadow ramp ends exactly at the wall foot
                    Assert.AreEqual(0.0, AlphaAt(m, mx + nx * 0.01, mz + nz * 0.01), 1e-6, $"wall {w}: lit salt untouched");
                    for (int i = 0; i < v.Length; i++)
                        Assert.LessOrEqual((v[i].x - mx) * nx + (v[i].z - mz) * nz, 1e-3, $"wall {w}: no shadow vertex beyond the sunlit wall");
                    continue;
                }
                shaded++;
                // a shaded wall: full shadow from its foot to the projected top edge, the 0.5 contour on the projection
                Assert.AreEqual(1.0, AlphaAt(m, mx + nx * 0.5, mz + nz * 0.5), 1e-4, $"wall {w}: shadow reaches the wall foot");
                Assert.AreEqual(1.0, AlphaAt(m, mx + 0.5 * ox, mz + 0.5 * oz), 1e-4, $"wall {w}: full shadow half-way");
                Assert.AreEqual(0.5, AlphaAt(m, mx + ox, mz + oz), 0.02, $"wall {w}: 0.5 contour on the projected top edge");
                double h = 0.5 * pFar + 0.05;
                Assert.AreEqual(1.0, AlphaAt(m, mx + ox - nx * h, mz + oz - nz * h), 1e-4, $"wall {w}: inside the soft edge");
                Assert.AreEqual(0.0, AlphaAt(m, mx + ox + nx * h, mz + oz + nz * h), 1e-4, $"wall {w}: beyond the soft edge");
            }
            Assert.AreEqual(2, shaded, "two walls face away from the sun (azimuth -35 deg)");
            // corners: a sunlit and a shaded wall meet -> a slanted side along the shadow; two shaded walls -> a far corner
            int[,] corners = { { 0, 3 }, { 0, 1 }, { 1, 2 }, { 2, 3 } };   // front-left, front-right, back-right, back-left
            double[,] cxz = { { x0, z0 }, { x1, z0 }, { x1, z1 }, { x0, z1 } };
            for (int c = 0; c < 4; c++)
            {
                int wa = corners[c, 0], wb = corners[c, 1];
                double px = cxz[c, 0], pz = cxz[c, 1];
                if (!lit[wa] && !lit[wb])
                {
                    Assert.AreEqual(0.5, AlphaAt(m, px + ox, pz + oz), 0.02, $"corner {c}: projected far corner on the 0.5 contour");
                    continue;
                }
                if (lit[wa] && lit[wb]) continue;
                int wl = lit[wa] ? wa : wb;
                double qx = walls[wl, 2], qz = walls[wl, 3];
                double dq = qx * ux + qz * uz;
                qx -= dq * ux; qz -= dq * uz;
                double ql = Math.Sqrt(qx * qx + qz * qz);
                qx /= ql; qz /= ql;   // across the slanted side, toward the sunlit salt
                for (int k = 1; k <= 3; k++)
                {
                    double t = 0.25 * k * L, sx = px + ux * t, sz = pz + uz * t;
                    double hp = 0.5 * (TerraceBuilder.ShadowPenumbra0 + TerraceBuilder.ShadowPenumbraGrowth * t) + 0.05;
                    Assert.AreEqual(0.5, AlphaAt(m, sx, sz), 0.02, $"corner {c}: 0.5 contour on the slanted side at {t:F1} m");
                    Assert.AreEqual(0.0, AlphaAt(m, sx + qx * hp, sz + qz * hp), 1e-4, $"corner {c}: lit beside the slanted side");
                    Assert.AreEqual(1.0, AlphaAt(m, sx - qx * hp, sz - qz * hp), 1e-4, $"corner {c}: shade inside the slanted side");
                }
            }
        }

        /// <summary>Interpolated vertex alpha of a flat (y = const) mesh at (x, z); 0 where no triangle covers the point.</summary>
        static double AlphaAt(Mesh m, double x, double z)
        {
            var v = m.vertices;
            var c = m.colors;
            int[] idx = m.GetIndices(0);
            double best = 0.0;
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                Vector3 a = v[idx[t]], b = v[idx[t + 1]], d = v[idx[t + 2]];
                double det = ((double)b.x - a.x) * ((double)d.z - a.z) - ((double)d.x - a.x) * ((double)b.z - a.z);
                if (Math.Abs(det) < 1e-12) continue;
                double l1 = ((x - a.x) * ((double)d.z - a.z) - ((double)d.x - a.x) * (z - a.z)) / det;
                double l2 = (((double)b.x - a.x) * (z - a.z) - (x - a.x) * ((double)b.z - a.z)) / det;
                double l0 = 1.0 - l1 - l2;
                if (l0 < -1e-7 || l1 < -1e-7 || l2 < -1e-7) continue;
                best = Math.Max(best, l0 * c[idx[t]].a + l1 * c[idx[t + 1]].a + l2 * c[idx[t + 2]].a);
            }
            return best;
        }

        // ================================================================== beat 4: cable
        [Test]
        public void TScale3_Cable()
        {
            double rMin = double.MaxValue, rMax = double.MinValue;
            for (int m = 0; m < Cable.Lod0.Count; m++)
            {
                var v = Cable.Lod0[m].vertices;
                for (int i = 0; i < v.Length; i++) { double r = Radius(v[i]); rMin = Math.Min(rMin, r); rMax = Math.Max(rMax, r); }
            }
            Assert.AreEqual(50.0, rMin, 0.05, "cable radius (valleys)");
            Assert.AreEqual(51.2, rMax, 0.05, "ridge crest radius");

            // 36 ridges counted from a full-relief ring of the mesh, crests at theta_n(y)
            foreach (float y in new[] { 100f, 332f, -152f })   // full-relief ring heights (2 m and 4 m bands)
            {
                var ring = RingFromMeshes(Cable.Lod0, y);
                var angR = new List<KeyValuePair<double, double>>();
                for (int i = 0; i < ring.Count; i++) angR.Add(new KeyValuePair<double, double>(CableBuilder.Wrap2Pi(Math.Atan2(ring[i].x, ring[i].z)), Radius(ring[i])));
                angR.Sort((p, q) => p.Key.CompareTo(q.Key));
                var dedup = new List<KeyValuePair<double, double>>();
                for (int i = 0; i < angR.Count; i++) if (dedup.Count == 0 || angR[i].Key - dedup[dedup.Count - 1].Key > 1e-5) dedup.Add(angR[i]);
                Assert.AreEqual(432, dedup.Count, $"distinct ring positions at y = {y}");
                int ridges = 0;
                for (int i = 0; i < dedup.Count; i++)
                {
                    double r = dedup[i].Value, rp = dedup[(i - 1 + dedup.Count) % dedup.Count].Value, rn = dedup[(i + 1) % dedup.Count].Value;
                    if (r > rp && r >= rn)
                    {
                        ridges++;
                        Assert.AreEqual(51.2, r, 0.05, "crest radius");
                        // crest lies at (n + 0.5) * 2 pi / 36 + 2 pi y / 600 for some n
                        double tw = 2.0 * Math.PI * y / 600.0;
                        double n = (CableBuilder.Wrap2Pi(dedup[i].Key - tw) / (2.0 * Math.PI / 36.0)) - 0.5;
                        Assert.AreEqual(Math.Round(n), n, 1e-3, "crest angle theta_n(y)");
                        // the M05 lay-line term has its crests (half-integer phase) on the geometric crests
                        double th = dedup[i].Key > Math.PI ? dedup[i].Key - 2.0 * Math.PI : dedup[i].Key;   // shader atan2 range
                        double u = ThreadConstants.CableLayLineScale * th - ThreadConstants.CableLayLineRise * y;
                        double phase = u / ThreadConstants.CableLayLinePeriod;
                        double fr = phase - Math.Floor(phase);
                        Assert.AreEqual(0.5, fr, 0.02, "lay-line crest phase");
                    }
                }
                Assert.AreEqual(36, ridges, $"ridges at y = {y}");
            }

            // diameter 100.0 from the relief-free LOD1 bounds; band spacing and height (shader term constants)
            Bounds b1 = Cable.Lod1[0].bounds;
            Assert.AreEqual(100.0, b1.size.x, 0.05, "cable diameter (x)");
            Assert.AreEqual(100.0, b1.size.z, 0.05, "cable diameter (z)");
            Assert.AreEqual(50.0, ThreadConstants.CableBandSpacing, 1e-9, "band spacing");
            Assert.AreEqual(10.0, ThreadConstants.CableBandHeight, 1e-9, "band height");
            Assert.AreEqual(36, ThreadConstants.CableStrands);
            // the lean of the lay: atan(2 pi R / pitch) = 27.6 degrees
            double lean = Math.Atan(2.0 * Math.PI * ThreadConstants.CableRadius / ThreadConstants.CablePitch) / ThreadMath.Deg;
            Assert.AreEqual(27.6, lean, 0.05, "strand lean");
        }

        [Test]
        public void Cable_RingLayoutAndReliefData()
        {
            var ys = CableBuilder.Lod0RingYs();
            Assert.AreEqual(264, ys.Count, "LOD0 rings");
            Assert.AreEqual(-300.0, ys[0], 1e-9); Assert.AreEqual(600.0, ys[ys.Count - 1], 1e-9);
            for (int i = 1; i < ys.Count; i++)
            {
                double d = ys[i] - ys[i - 1];
                bool fine = ys[i - 1] >= 30.0 - 1e-9 && ys[i] <= 180.0 + 1e-9;
                if (fine) Assert.AreEqual(2.0, d, 1e-9, $"2 m rings in [30, 180] at {ys[i]}");
                else Assert.LessOrEqual(d, 4.0 + 1e-9, $"ring spacing at {ys[i]}");
            }
            // vertex contract: tangent.xyz = smooth outward normal, uv1.x = relief r - 50 (>= 0, <= 1.2)
            var tangents = new List<Vector4>(); var uv1 = new List<Vector4>();
            Mesh m = Cable.Lod0[4];
            m.GetTangents(tangents); m.GetUVs(1, uv1);
            var v = m.vertices; var n = m.normals;
            for (int i = 0; i < v.Length; i++)
            {
                var sm = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                var radial = new Vector3(v[i].x, 0f, v[i].z).normalized;
                Assert.Greater(Vector3.Dot(sm, radial), 0.99999f, "smooth normal is radial");
                Assert.AreEqual(Radius(v[i]) - 50.0, uv1[i].x, 1e-3, "relief in uv1.x");
                Assert.GreaterOrEqual(uv1[i].x, -1e-4f); Assert.LessOrEqual(uv1[i].x, 1.2f + 1e-4f);
                // smooth surface after the shader's relief fade: position - smooth * relief is on r = 50
                Vector3 faded = v[i] - sm * uv1[i].x;
                Assert.AreEqual(50.0, Radius(faded), 1e-3, "faded position on the r = 50 cylinder");
                Assert.Greater(Vector3.Dot(n[i], radial), 0f, "ridge normal points outward");
            }
            // relief is zero on both LOD0 end rings
            CableRing r0 = CableBuilder.Lod0Ring(-300.0), r1 = CableBuilder.Lod0Ring(600.0);
            for (int i = 0; i < CableBuilder.Lod0RingVerts; i++)
            {
                Assert.AreEqual(0f, r0.Relief[i]); Assert.AreEqual(0f, r1.Relief[i]);
            }
            // the walked corridor never comes within the relief-fade end (320 m) of an LOD0 end ring, so the
            // relief is fully faded at every LOD boundary even without the taper
            double dMin = double.MaxValue;
            for (double s = ThreadConstants.NearCableOnS; s <= ThreadConstants.PathLength; s += 1.0)
            {
                double hw = ThreadMath.HalfWidth(s);
                for (int e = -1; e <= 1; e++)
                {
                    D3 p = ThreadMath.CorridorToWorld(s, e * hw, ThreadConstants.EyeHeight);
                    double dx = p.x - ThreadConstants.CableAxisX, dz = p.z - ThreadConstants.CableAxisZ;
                    double dr = Math.Max(0.0, Math.Sqrt(dx * dx + dz * dz) - ThreadConstants.CableRadius);
                    foreach (double yb in new[] { ThreadConstants.CableLod0YMin, ThreadConstants.CableLod0YMax })
                        dMin = Math.Min(dMin, Math.Sqrt(dr * dr + (p.y - yb) * (p.y - yb)));
                }
            }
            Assert.Greater(dMin, ThreadConstants.CableReliefFade1, "LOD boundary inside the relief-fade range");
        }

        [Test]
        public void Cable_LodBoundaryRingsIdentical()
        {
            var lod0 = Cable.Lod0;
            var breaks = CableBuilder.Lod0ChunkRingIndices(CableBuilder.Lod0RingYs());
            var ys = CableBuilder.Lod0RingYs();
            for (int c = 1; c < lod0.Count; c++)
                AssertSameRing(RingFrom(lod0[c - 1], (float)ys[breaks[c]]), RingFrom(lod0[c], (float)ys[breaks[c]]), $"LOD0 chunk {c - 1}/{c}");

            var up1 = new List<Mesh>(); var dn1 = new List<Mesh>();
            for (int i = 0; i < Cable.Lod1.Count; i++) (Cable.Lod1[i].name.Contains("_up_") ? up1 : dn1).Add(Cable.Lod1[i]);
            Assert.AreEqual(6, up1.Count); Assert.AreEqual(6, dn1.Count);
            AssertSameRing(RingFrom(lod0[lod0.Count - 1], 600f), RingFrom(up1[0], 600f), "LOD0 / LOD1 at y = 600", 432);
            AssertSameRing(RingFrom(lod0[0], -300f), RingFrom(dn1[dn1.Count - 1], -300f), "LOD0 / LOD1 at y = -300", 432);
            for (int i = 1; i < up1.Count; i++)
            {
                float y = up1[i].bounds.min.y;
                AssertSameRing(RingFrom(up1[i - 1], y), RingFrom(up1[i], y), $"LOD1 up {i - 1}/{i}", 108);
            }
            for (int i = 1; i < dn1.Count; i++)
            {
                float y = dn1[i].bounds.min.y;
                AssertSameRing(RingFrom(dn1[i - 1], y), RingFrom(dn1[i], y), $"LOD1 down {i - 1}/{i}", 108);
            }
            AssertSameRing(RingFrom(up1[up1.Count - 1], 3500f), RingFrom(Cable.Lod2[0], 3500f), "LOD1 / LOD2 at y = 3500", 108);
            AssertSameRing(RingFrom(dn1[0], -3300f), RingFrom(Cable.Lod2[1], -3300f), "LOD1 / LOD2 at y = -3300", 108);

            // relief data is zero on the LOD0 boundary rings
            var uv1 = new List<Vector4>();
            foreach (Mesh m in new[] { lod0[0], lod0[lod0.Count - 1] })
            {
                m.GetUVs(1, uv1);
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++)
                    if (Mathf.Abs(v[i].y - 600f) < 1e-3f || Mathf.Abs(v[i].y + 300f) < 1e-3f) Assert.AreEqual(0f, uv1[i].x, 1e-6f, "relief at the boundary");
            }
        }

        [Test]
        public void Cable_LateralSurfaceIsWatertight()
        {
            // all LOD bands welded: one tube, open only at y = +-20 km (no T-junction, no crack)
            int open = OpenEdges(Cable.Lateral(), out int nonManifold, out var openList);
            Assert.AreEqual(0, nonManifold, "non-manifold edges");
            Assert.AreEqual(2 * CableBuilder.Lod2Sides, open, "open edges (only the two far ends)");
            for (int i = 0; i < openList.Count; i++)
                Assert.Greater(Mathf.Abs(openList[i].Item1.y), 19999f, "open edge away from the ends");
        }

        [Test]
        public void Cable_Collars()
        {
            var cs = Cable.Collars;
            var expected = new System.Collections.Generic.List<double>();
            foreach (var h in CableBuilder.CollarHeights()) if (!CableBuilder.CollarHitsBead(h)) expected.Add(h);
            // D-018 omits collars that meet a bead lens; since D-035 moved the column to y = 230 + 40 k none does, so all ten
            // collars of 14.5.4 (every 100 m from -300 to 600) are back, including the y = 100 / 200 scale cues of beat 4 (E21)
            Assert.AreEqual(10, expected.Count, "collars every 100 m from -300 to 600 (none meets a bead after D-035)");
            Assert.AreEqual(expected.Count, cs.Count);
            Assert.IsFalse(CableBuilder.CollarHitsBead(100) || CableBuilder.CollarHitsBead(200) || CableBuilder.CollarHitsBead(300));
            Assert.AreEqual(ThreadConstants.CableRadius - ThreadConstants.CollarEmbed, CableBuilder.CollarMajorRadius - ThreadConstants.CollarTube, 1e-9);
            for (int c = 0; c < cs.Count; c++)
            {
                double yc = expected[c];
                Assert.AreEqual(yc, Cable.CollarY[c], 1e-9);
                var v = cs[c].vertices;
                double rMin = double.MaxValue, rMax = double.MinValue;
                for (int i = 0; i < v.Length; i++)
                {
                    double r = Radius(v[i]);
                    rMin = Math.Min(rMin, r); rMax = Math.Max(rMax, r);
                    Assert.LessOrEqual(Math.Abs(v[i].y - yc), 1.4 + 1e-3, "tube height");
                }
                Assert.AreEqual(49.5, rMin, 0.01, "collar embedded 0.5 m in the r = 50 surface");
                Assert.AreEqual(52.3, rMax, 0.01, "collar outer radius");
                Assert.AreEqual(0, Cable.CollarMinor[c] % 2, "even minor count (inner equator vertex exists)");
            }
        }

        [Test]
        public void Cable_Ribbons()
        {
            Assert.AreEqual(2, Cable.Ribbons.Count);
            var uv1 = new List<Vector4>();
            for (int r = 0; r < 2; r++)
            {
                Mesh m = Cable.Ribbons[r];
                var v = m.vertices; var n = m.normals; m.GetUVs(1, uv1);
                Assert.AreEqual(2 * (CableBuilder.RibbonSegments + 1), v.Length, "centre-line vertices");
                Assert.AreEqual(2 * CableBuilder.RibbonSegments, MeshKit.TriangleCount(m), "ribbon triangles");
                double sign = r == 0 ? 1.0 : -1.0, yMin = double.MaxValue, yMax = double.MinValue;
                for (int i = 0; i < v.Length; i++)
                {
                    Assert.AreEqual(0f, v[i].x); Assert.AreEqual(0f, v[i].z);
                    Assert.AreEqual(1f, n[i].magnitude, 1e-5f, "line direction is a unit vector");
                    Assert.AreEqual(1f, Mathf.Abs(n[i].y), 1e-5f, "line direction along the axis");
                    Assert.AreEqual(1f, Mathf.Abs(uv1[i].y), 1e-6f, "side = -1 / +1");
                    double y = sign * v[i].y; yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
                }
                Assert.LessOrEqual(yMin, 20000.0, "ribbon starts at (or before) 20 km");
                Assert.AreEqual(200000.0, yMax, 1.0, "ribbon reaches 200 km");
                int[] idx = m.GetIndices(0);
                for (int i = 0; i < idx.Length; i++) Assert.IsTrue(idx[i] >= 0 && idx[i] < v.Length);
                Bounds b = m.bounds;
                Assert.IsTrue(b.Contains(new Vector3(0f, (float)(sign * yMin), 0f)) && b.Contains(new Vector3(0f, (float)(sign * yMax), 0f)), "bounds cover the ribbon");
                Assert.GreaterOrEqual(b.extents.x, 500f, "bounds allow the screen-space expansion");
            }
        }

        [Test]
        public void NearCable_ActivationWithHysteresis()
        {
            var go = new GameObject("near-cable-test");
            try
            {
                var sw = new NearCableSwitch(go, 0.0);
                Assert.IsFalse(go.activeSelf, "off in beats 1 and 2");
                var f = new FrameState { s = 261.9 }; sw.OnFrame(in f); Assert.IsFalse(go.activeSelf);
                f.s = 262.0; sw.OnFrame(in f); Assert.IsTrue(go.activeSelf, "on at s = 262 (inside the bank)");
                f.s = 259.0; sw.OnFrame(in f); Assert.IsTrue(go.activeSelf, "hysteresis keeps it on");
                f.s = 257.9; sw.OnFrame(in f); Assert.IsFalse(go.activeSelf, "off again below 258");
                f.s = 700.0; sw.OnFrame(in f); Assert.IsTrue(go.activeSelf);
                var sw2 = new NearCableSwitch(go, 500.0);
                Assert.IsTrue(sw2.On && go.activeSelf, "initial state from the start progress");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ================================================================== validity and budgets
        [Test]
        public void TNormals_AllMeshesValid()
        {
            var meshes = new List<Mesh>
            {
                Temp(StairBuilder.BuildSteps(), "steps"), Temp(StairBuilder.BuildStringers(), "stringers"), Temp(StairBuilder.BuildBollards(), "bollards"),
                Temp(TerraceBuilder.BuildTop(), "terraceTop"), Temp(TerraceBuilder.BuildSides(), "terraceSides"),
                Temp(TerraceBuilder.BuildGroundShadow(), "terraceShadow"),
            };
            meshes.AddRange(Cable.Lateral());
            meshes.AddRange(Cable.Collars);
            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh m = meshes[i];
                Assert.IsTrue(MeshKit.Validate(m, out string report), report);
                var v = m.vertices;
                Bounds b = m.bounds; b.Expand(2e-3f);
                for (int k = 0; k < v.Length; k++)
                {
                    Assert.IsTrue(Finite(v[k]), $"{m.name}: non-finite vertex");
                    Assert.IsTrue(b.Contains(v[k]), $"{m.name}: vertex outside bounds");
                }
                if (v.Length > 65534) Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, m.indexFormat, $"{m.name}: 32-bit indices");
            }
            // cable lateral normals point away from the axis
            var lat = Cable.Lateral();
            for (int i = 0; i < lat.Count; i++)
            {
                var v = lat[i].vertices; var n = lat[i].normals;
                for (int k = 0; k < v.Length; k++)
                    Assert.Greater(n[k].x * v[k].x + n[k].z * v[k].z, 0f, $"{lat[i].name}: inward normal");
            }
            // ribbons: indices in range and finite data (their triangles are degenerate by design until expanded)
            for (int i = 0; i < Cable.Ribbons.Count; i++)
            {
                var v = Cable.Ribbons[i].vertices;
                for (int k = 0; k < v.Length; k++) Assert.IsTrue(Finite(v[k]));
            }
        }

        [Test]
        public void TriangleBudget()
        {
            long lod0 = CableSet.Triangles(Cable.Lod0), lod1 = CableSet.Triangles(Cable.Lod1), lod2 = CableSet.Triangles(Cable.Lod2);
            long collars = CableSet.Triangles(Cable.Collars), ribbons = CableSet.Triangles(Cable.Ribbons);
            Assert.AreEqual(263L * 864L, lod0, "LOD0 = 263 bands x 432 quads (about 227 k)");
            Assert.That(lod1, Is.InRange(55000L, 75000L), "LOD1 about 70 k");
            Assert.That(lod2, Is.InRange(1000L, 4000L), "LOD2 about 3 k");
            Assert.Less(collars, 30000L, "collars");
            Assert.AreEqual(64L, ribbons, "two ribbons");
            Assert.Less(Cable.TotalTriangles, 330000L, "cable about 300 k");
            long stairs = MeshKit.TriangleCount(Temp(StairBuilder.BuildSteps(), "s")) + MeshKit.TriangleCount(Temp(StairBuilder.BuildStringers(), "t"));
            long terrace = MeshKit.TriangleCount(Temp(TerraceBuilder.BuildTop(), "u")) + MeshKit.TriangleCount(Temp(TerraceBuilder.BuildSides(), "w"));
            long bollards = MeshKit.TriangleCount(Temp(StairBuilder.BuildBollards(), "x"));
            Assert.Less(stairs, 4000L, "stairs");
            Assert.Less(terrace, 4000L, "terrace");
            Assert.Less(bollards, 18000L, "bollards (12 edge + 34 walk-line, D-050 / D-053)");
            for (int i = 0; i < Cable.Lod0.Count; i++) Assert.LessOrEqual(Cable.Lod0[i].vertexCount, 65534, "LOD0 chunks stay 16-bit");
        }

        // ================================================================== helpers
        static double Radius(Vector3 p) => Math.Sqrt((double)p.x * p.x + (double)p.z * p.z);

        static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

        static bool HasExact(MeshBuilder mb, Vector3 p)
        {
            for (int i = 0; i < mb.VertexCount; i++) if (mb.Positions[i] == p && mb.Positions[i].x == p.x && mb.Positions[i].z == p.z && mb.Positions[i].y == p.y) return true;
            return false;
        }

        static List<Vector3> RingFrom(Mesh m, float y)
        {
            var l = new List<Vector3>();
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) if (Mathf.Abs(v[i].y - y) < 1e-3f) l.Add(v[i]);
            return l;
        }

        static List<Vector3> RingFromMeshes(List<Mesh> meshes, float y)
        {
            for (int i = 0; i < meshes.Count; i++)
            {
                var r = RingFrom(meshes[i], y);
                if (r.Count > 0) return r;
            }
            return new List<Vector3>();
        }

        static void AssertSameRing(List<Vector3> a, List<Vector3> b, string what, int expectDistinct = 0)
        {
            Assert.Greater(a.Count, 0, what + ": ring missing in the first mesh");
            Assert.Greater(b.Count, 0, what + ": ring missing in the second mesh");
            double worst = Math.Max(MaxNearest(a, b), MaxNearest(b, a));
            Assert.Less(worst, 1e-3, what + ": max vertex distance");
            if (expectDistinct > 0)
            {
                Assert.AreEqual(expectDistinct, Distinct(a), what + ": distinct positions (first)");
                Assert.AreEqual(expectDistinct, Distinct(b), what + ": distinct positions (second)");
            }
        }

        static double MaxNearest(List<Vector3> a, List<Vector3> b)
        {
            double worst = 0;
            for (int i = 0; i < a.Count; i++)
            {
                double best = double.MaxValue;
                for (int j = 0; j < b.Count; j++) best = Math.Min(best, (a[i] - b[j]).sqrMagnitude);
                worst = Math.Max(worst, Math.Sqrt(best));
            }
            return worst;
        }

        static int Distinct(List<Vector3> a)
        {
            var set = new HashSet<Vector3Int>();
            for (int i = 0; i < a.Count; i++) set.Add(Key(a[i]));
            return set.Count;
        }

        static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));

        /// <summary>Counts open (used once) and non-manifold (used > 2 times) edges of the position-welded union.</summary>
        static int OpenEdges(List<Mesh> meshes, out int nonManifold, out List<Tuple<Vector3, Vector3>> openList)
        {
            var ids = new Dictionary<Vector3Int, int>();
            var pos = new List<Vector3>();
            var edges = new Dictionary<long, int>();
            for (int m = 0; m < meshes.Count; m++)
            {
                var v = meshes[m].vertices;
                var canon = new int[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    var k = Key(v[i]);
                    if (!ids.TryGetValue(k, out int id)) { id = pos.Count; ids.Add(k, id); pos.Add(v[i]); }
                    canon[i] = id;
                }
                int[] idx = meshes[m].GetIndices(0);
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    int a = canon[idx[t]], b = canon[idx[t + 1]], c = canon[idx[t + 2]];
                    Assert.IsTrue(a != b && b != c && a != c, $"{meshes[m].name}: collapsed triangle");
                    AddEdge(edges, a, b); AddEdge(edges, b, c); AddEdge(edges, c, a);
                }
            }
            int open = 0; nonManifold = 0;
            openList = new List<Tuple<Vector3, Vector3>>();
            foreach (var kv in edges)
            {
                if (kv.Value == 1) { open++; openList.Add(Tuple.Create(pos[(int)(kv.Key >> 32)], pos[(int)(kv.Key & 0xffffffffL)])); }
                else if (kv.Value > 2) nonManifold++;
            }
            return open;
        }

        static void AddEdge(Dictionary<long, int> e, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            e.TryGetValue(key, out int c);
            e[key] = c + 1;
        }
    }
}
