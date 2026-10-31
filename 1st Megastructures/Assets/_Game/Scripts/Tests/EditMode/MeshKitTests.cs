using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Megastructure.Tests
{
    /// <summary>
    /// EditMode tests of MeshKit, MeshValidation and VertexBake (14.7.5, 14.10 mesh tests): triangle counts, validity of every
    /// generator (no degenerate triangles, finite data, winding consistent with normals, index format, bounds), outward normals
    /// on closed shapes (positive signed volume and normals pointing away from the centroid), inward-facing shells, ring
    /// dimensions of the shaft rib and belt, Lathe arc-length UVs, seamless 200 m column chunks with bands on the chunk
    /// boundaries, no coplanar overlapping faces in the Warren truss, ThinLine UV1 / UV2 in mesh space, AO at floor/wall
    /// junctions, 32-bit indices and load-time generation speed.
    /// </summary>
    public sealed class MeshKitTests
    {
        readonly List<Mesh> _meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) UnityEngine.Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
        }

        Mesh Make(MeshBuilder mb, string name)
        {
            Mesh m = mb.ToMesh(name);
            _meshes.Add(m);
            return m;
        }

        static MeshReport AssertValid(Mesh m)
        {
            MeshReport r = MeshValidation.Validate(m);
            Assert.IsTrue(r.Ok, r.Describe());
            return r;
        }

        /// <summary>Closed mesh: valid, positive signed volume and normals pointing away from the vertex centroid on average.</summary>
        static void AssertClosedOutward(Mesh m)
        {
            AssertValid(m);
            Assert.Greater(MeshValidation.SignedVolume(m), 0.0, m.name + ": signed volume must be positive (outward winding)");
            Assert.Greater(MeshValidation.OutwardScore(m), 0f, m.name + ": normals must point away from the centroid on average");
        }

        /// <summary>Closed but not star-shaped (rings, arcs, helices): valid and positive signed volume.</summary>
        static void AssertClosedSolid(Mesh m)
        {
            AssertValid(m);
            Assert.Greater(MeshValidation.SignedVolume(m), 0.0, m.name + ": signed volume must be positive (outward winding)");
        }

        // ------------------------------------------------------------------------------------------ ChamferBox / HexBolt

        [Test]
        public void ChamferBox_Has44Triangles_AndIsClosedOutward()
        {
            Vector3[] sizes = { new Vector3(1f, 1f, 1f), new Vector3(2.4f, 0.25f, 0.3f), new Vector3(0.4f, 0.4f, 27f), new Vector3(0.05f, 0.05f, 0.05f) };
            float[] bevels = { 0.02f, 0.01f, 0.03f, 0.5f };
            for (int i = 0; i < sizes.Length; i++)
            {
                var mb = new MeshBuilder();
                MeshKit.ChamferBox(mb, new Vector3(3f, -2f, 7f), sizes[i], bevels[i], Quaternion.Euler(10f, 33f, -5f));
                Assert.AreEqual(44, mb.TriangleCount, "triangles of box " + i);
                Assert.AreEqual(96, mb.VertexCount, "vertices of box " + i);
                AssertClosedOutward(Make(mb, "ChamferBox" + i));
            }
        }

        [Test]
        public void ChamferBox_BoundsMatchSize_AndChamfersCarryEdgeWear()
        {
            var mb = new MeshBuilder();
            Vector3 size = new Vector3(2.4f, 0.25f, 0.3f);
            MeshKit.ChamferBox(mb, new Vector3(0f, 2.375f, 9f), size, 0.02f);
            Mesh m = Make(mb, "ChamferBoxBounds");
            AssertValid(m);
            Assert.AreEqual(size.x, m.bounds.size.x, 1e-5f);
            Assert.AreEqual(size.y, m.bounds.size.y, 1e-5f);
            Assert.AreEqual(size.z, m.bounds.size.z, 1e-5f);
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 n = mb.Normals[i];
                bool face = Mathf.Abs(n.x) > 0.999f || Mathf.Abs(n.y) > 0.999f || Mathf.Abs(n.z) > 0.999f;
                Assert.AreEqual(face ? 0 : 255, mb.Colors[i].g, "edge wear of vertex " + i);
                Assert.AreEqual(255, mb.Colors[i].r, "AO default of vertex " + i);
            }
        }

        [Test]
        public void ChamferBox_RespectsBuilderMatrix_EvenWhenMirrored()
        {
            var mb = new MeshBuilder();
            mb.Push(Matrix4x4.TRS(new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(-1f, 1f, 1f)));
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(1f, 2f, 3f), 0.03f);
            mb.Pop();
            Assert.AreEqual(44, mb.TriangleCount);
            AssertClosedOutward(Make(mb, "ChamferBoxMirrored"));
        }

        [Test]
        public void HexBolt_Has12Triangles_AndIsClosedOutward()
        {
            var mb = new MeshBuilder();
            Vector3 axis = new Vector3(0.3f, 1f, -0.2f).normalized;
            MeshKit.HexBolt(mb, new Vector3(1f, 2f, 3f), axis, 0.08f, 15f);
            Assert.AreEqual(12, mb.TriangleCount);
            Mesh m = Make(mb, "HexBolt");
            AssertClosedOutward(m);
            Assert.AreEqual(1f, Vector3.Dot(mb.Normals[0], axis), 1e-4f, "top face normal is the bolt axis");
        }

        // ------------------------------------------------------------------------------------------ Lathe family

        [Test]
        public void Lathe_UV0ArcLengthEquals2PiRAtTheSeam()
        {
            const float r = 3f;
            var profile = new List<Vector2> { new Vector2(r, 0f), new Vector2(r, 5f) };
            int[] segmentCounts = { 24, 64, 128 };
            foreach (int seg in segmentCounts)
            {
                var mb = new MeshBuilder();
                MeshKit.Lathe(mb, profile, seg);
                AssertValid(Make(mb, "LatheSeam" + seg));
                float maxU = float.NegativeInfinity, minU = float.PositiveInfinity;
                for (int i = 0; i < mb.VertexCount; i++) { maxU = Mathf.Max(maxU, mb.UV0[i].x); minU = Mathf.Min(minU, mb.UV0[i].x); }
                Assert.AreEqual(2f * Mathf.PI * r, maxU, 1e-3f, "u at the seam");
                Assert.AreEqual(0f, minU, 1e-5f);
                bool seamShared = false;
                for (int i = 0; i < mb.VertexCount && !seamShared; i++)
                {
                    if (Mathf.Abs(mb.UV0[i].x - 2f * Mathf.PI * r) > 1e-3f) continue;
                    for (int j = 0; j < mb.VertexCount; j++)
                        if (Mathf.Abs(mb.UV0[j].x) < 1e-5f && (mb.Positions[i] - mb.Positions[j]).sqrMagnitude < 1e-10f
                            && Mathf.Abs(mb.UV0[i].y - mb.UV0[j].y) < 1e-5f) { seamShared = true; break; }
                }
                Assert.IsTrue(seamShared, "seam vertices u = 0 and u = 2 pi r share a position");
                for (int i = 0; i < mb.VertexCount; i++) Assert.AreEqual(mb.Positions[i].y, mb.UV0[i].y, 1e-5f, "v = height");
            }
        }

        [Test]
        public void Lathe_BandedColumnWithCaps_IsClosedOutward()
        {
            var profile = new List<Vector2>();
            MeshKit.BandedColumnProfile(profile, 22f, -100f, 100f, 120f, 2f, 0.4f, 0.03f, 0f);
            foreach (int seg in new[] { 24, 96, 128 })
            {
                var mb = new MeshBuilder();
                MeshKit.Lathe(mb, profile, seg, 40f, 0f, 360f, true);
                Mesh m = Make(mb, "Column" + seg);
                AssertClosedOutward(m);
                Assert.AreEqual(22.4f, m.bounds.max.x, 1e-3f, "band protrusion");
                Assert.AreEqual(-100f, m.bounds.min.y, 1e-3f);
                Assert.AreEqual(100f, m.bounds.max.y, 1e-3f);
            }
        }

        [Test]
        public void BandedColumnProfile_ChunksKeepStraddlingBands_AndStitchSeamlessly()
        {
            // 14.5.2 b: 2 m bands every 120 m at y = 1200 - 120 k, 0.4 m proud; columns +1200 to -3000 in 200 m chunks, so the
            // bands at 600, 0, -600, ... are centred exactly on chunk boundaries
            const float r = 22f, ro = 22.4f, bev = 0.05f;
            var chunks = new List<List<Vector2>>();
            for (int c = 0; c < 21; c++)
            {
                float y0 = -3000f + 200f * c, y1 = y0 + 200f;
                var prof = new List<Vector2>();
                MeshKit.BandedColumnProfile(prof, r, y0, y1, 120f, 2f, 0.4f, bev, 1200f);
                Assert.AreEqual(y0, prof[0].y, 0f, "chunk " + c + " starts at y0");
                Assert.AreEqual(y1, prof[prof.Count - 1].y, 0f, "chunk " + c + " ends at y1");
                for (int i = 1; i < prof.Count; i++) Assert.GreaterOrEqual(prof[i].y, prof[i - 1].y, "profile runs bottom to top");
                if (c > 0)
                {
                    Vector2 below = chunks[c - 1][chunks[c - 1].Count - 1];
                    Assert.AreEqual(below.x, prof[0].x, 0f, "chunks " + (c - 1) + "/" + c + " meet on the same ring (radius)");
                    Assert.AreEqual(below.y, prof[0].y, 0f, "chunks " + (c - 1) + "/" + c + " meet on the same ring (height)");
                }
                chunks.Add(prof);
                var mb = new MeshBuilder();
                MeshKit.Lathe(mb, prof, 96);
                AssertValid(Make(mb, "ColumnChunk" + c));
            }
            for (int k = 0; k <= 35; k++)
            {
                float yc = 1200f - 120f * k;
                int c = Mathf.Clamp(Mathf.FloorToInt((yc + 3000f) / 200f), 0, 20);
                Assert.AreEqual(ro, RadiusAt(chunks[c], yc), 1e-4f, "band at y = " + yc + " protrudes");
                if (c > 0 && yc == -3000f + 200f * c)
                    Assert.AreEqual(ro, chunks[c - 1][chunks[c - 1].Count - 1].x, 1e-4f, "band at y = " + yc + " (boundary) below");
                float between = yc - 60f;
                if (between > -3000f)
                    Assert.AreEqual(r, RadiusAt(chunks[Mathf.FloorToInt((between + 3000f) / 200f)], between), 1e-4f, "column between bands");
            }

            // a horizontal band face exactly on a boundary belongs to the chunk above; both chunks still meet at one point
            foreach (float phase in new[] { 1f, -1f }) // band [0, 2]: bottom face at 0; band [-2, 0]: top face at 0
            {
                var lower = new List<Vector2>();
                var upper = new List<Vector2>();
                MeshKit.BandedColumnProfile(lower, r, -200f, 0f, 120f, 2f, 0.4f, bev, phase);
                MeshKit.BandedColumnProfile(upper, r, 0f, 200f, 120f, 2f, 0.4f, bev, phase);
                Vector2 last = lower[lower.Count - 1];
                Assert.AreEqual(last.x, upper[0].x, 0f);
                Assert.AreEqual(0f, last.y, 0f);
                Assert.AreEqual(0f, upper[0].y, 0f);
                Assert.Less(lower[lower.Count - 2].y, 0f, "no horizontal face at the top of the lower chunk");
                Assert.AreEqual(0f, upper[1].y, 0f, "the band face on the boundary is the upper chunk's first segment");
                Assert.AreEqual(phase > 0f ? r : ro - bev, upper[0].x, 1e-5f);
                Assert.AreEqual(phase > 0f ? ro - bev : r, upper[1].x, 1e-5f);
            }

            // a chunk shorter than a band is clipped at both ends
            var thin = new List<Vector2>();
            MeshKit.BandedColumnProfile(thin, r, 0.5f, 1.5f, 120f, 2f, 0.4f, bev, 1f);
            Assert.AreEqual(2, thin.Count);
            Assert.AreEqual(ro, thin[0].x, 1e-5f);
            Assert.AreEqual(ro, thin[1].x, 1e-5f);
        }

        static float RadiusAt(List<Vector2> prof, float y)
        {
            for (int i = 0; i + 1 < prof.Count; i++)
            {
                Vector2 p = prof[i], q = prof[i + 1];
                if (q.y > p.y && y >= p.y && y <= q.y) return p.x + (q.x - p.x) * (y - p.y) / (q.y - p.y);
            }
            return float.NaN;
        }

        [Test]
        public void Lathe_ReversedProfile_StillOutward_AndInsideFlips()
        {
            var down = new List<Vector2> { new Vector2(2f, 4f), new Vector2(2f, 0f) };
            var mb = new MeshBuilder();
            MeshKit.Lathe(mb, down, 32, 40f, 0f, 360f, true);
            AssertClosedOutward(Make(mb, "LatheReversed"));

            var mbIn = new MeshBuilder();
            MeshKit.Lathe(mbIn, down, 32, 40f, 0f, 360f, true, true);
            Mesh inside = Make(mbIn, "LatheInside");
            AssertValid(inside);
            Assert.Less(MeshValidation.SignedVolume(inside), 0.0, "inside lathe faces the axis");
        }

        [Test]
        public void Lathe_PartialArcWithEndCaps_IsClosed()
        {
            var ring = new List<Vector2>
            {
                new Vector2(22f, -2.5f), new Vector2(28f, -2.5f), new Vector2(28f, 0f), new Vector2(22f, 0f), new Vector2(22f, -2.5f)
            };
            var mb = new MeshBuilder();
            MeshKit.Lathe(mb, ring, 64, 40f, 30f, 200f, false, false, false, true);
            AssertClosedSolid(Make(mb, "LatheArc"));
        }

        [Test]
        public void Cylinders_AreClosedOutward_AtFinalLength()
        {
            Vector3 a = new Vector3(1f, 2f, 3f), b = new Vector3(4f, 6f, 3f);
            var mb = new MeshBuilder();
            MeshKit.Cylinder(mb, a, b, 0.3f, 24);
            AssertClosedOutward(Make(mb, "Cylinder"));
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i];
                float t = Vector3.Dot(p - a, (b - a).normalized);
                Assert.GreaterOrEqual(t, -1e-4f); Assert.LessOrEqual(t, 5f + 1e-4f);
            }

            var mbT = new MeshBuilder();
            MeshKit.TaperedCylinder(mbT, a, b, 0.5f, 0.2f, 32);
            AssertClosedOutward(Make(mbT, "TaperedCylinder"));

            var mbC = new MeshBuilder();
            MeshKit.TaperedCylinder(mbC, a, b, 0.5f, 0f, 32);
            AssertClosedOutward(Make(mbC, "Cone"));

            var mbL = new MeshBuilder();
            MeshKit.Cylinder(mbL, a, b, 0.05f, 8, true, true);
            Mesh line = Make(mbL, "ThinLineCylinder");
            AssertClosedOutward(line);
            Assert.AreEqual(mbL.VertexCount, mbL.UV1.Count, "ThinLine UV1 on every vertex");
            Vector4 uv1 = mbL.UV1[0];
            Assert.AreEqual(1f, Vector3.Dot(new Vector3(uv1.x, uv1.y, uv1.z), (b - a).normalized), 1e-4f);
            Assert.AreEqual(0.05f, uv1.w, 1e-6f);
        }

        // ------------------------------------------------------------------------------------------ Loft family

        [Test]
        public void IBeam_IsClosedOutward_AtFinalLength()
        {
            var mb = new MeshBuilder();
            MeshKit.IBeam(mb, new Vector3(0f, -3f, 66f), new Vector3(0f, -3f, 72f), 0.4f, 0.4f, 0.03f, 0.04f, Vector3.up);
            Assert.AreEqual(44, mb.TriangleCount);
            Mesh m = Make(mb, "IBeam");
            AssertClosedOutward(m);
            Assert.AreEqual(6f, m.bounds.size.z, 1e-4f);
            Assert.AreEqual(0.4f, m.bounds.size.y, 1e-5f);
        }

        [Test]
        public void Loft_RoundedRectangleAlongBentPath_IsClosedOutward()
        {
            var path = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 5f), new Vector3(5f, 1f, 10f), new Vector3(5f, 1f, 16f) };
            var frames = new List<LoftFrame>();
            MeshKit.PathFrames(path, Vector3.up, frames);
            foreach (bool hard in new[] { false, true })
            {
                var section = MeshKit.RoundedRectangle(2f, 1f, 0.2f, hard ? 1 : 4, null, default, hard);
                var mb = new MeshBuilder();
                MeshKit.Loft(mb, section, frames, true);
                AssertClosedOutward(Make(mb, "LoftRect" + hard));
            }
        }

        [Test]
        public void Loft_GalleryShellInside_FacesInward_AndKeepsVerticalHeightOnStairs()
        {
            var path = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 40f), new Vector3(0f, -3f, 44.48f), new Vector3(0f, -3f, 60f) };
            var frames = new List<LoftFrame>();
            MeshKit.PathFrames(path, Vector3.up, frames, true);
            Assert.IsTrue(frames[1].Hard && frames[2].Hard, "slope changes are hard frames");
            var section = MeshKit.RoundedRectangle(AbyssConst.GALLERY_W, AbyssConst.GALLERY_H, 0.08f, 1, null,
                new Vector2(0f, AbyssConst.GALLERY_H * 0.5f), true);
            var mb = new MeshBuilder();
            MeshKit.Loft(mb, section, frames, false, true);
            Mesh m = Make(mb, "GalleryShell");
            AssertValid(m);
            Assert.AreEqual(AbyssConst.GALLERY_W, m.bounds.size.x, 1e-4f, "gallery width");

            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i], n = mb.Normals[i];
                if (Mathf.Abs(n.x) > 0.9f) Assert.Less(n.x * p.x, 0f, "side walls face the corridor axis");
                // flat ceiling rings: z <= 40 has floor 0, z >= 44.48 floor -3 (the sloped rings between have n.y = -0.83)
                if (n.y < -0.9f) Assert.Greater(p.y - (p.z > 42f ? -3f : 0f), 1f, "ceiling faces down");
                if (Mathf.Abs(p.z - 60f) < 1e-3f) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            }
            Assert.AreEqual(-3f, minY, 1e-4f);
            Assert.AreEqual(-0.5f, maxY, 1e-4f, "ceiling = floor + 2.5 after the stairs");
        }

        [Test]
        public void LoftVarying_FanBlade_IsClosedOutward()
        {
            const int frameCount = 12;
            var frames = new List<LoftFrame>();
            for (int k = 0; k < frameCount; k++)
            {
                float r = Mathf.Lerp(AbyssConst.FAN_R_HUB, AbyssConst.FAN_R_TIP, (float)k / (frameCount - 1));
                float chord = 14f + 16f * (r - 40f) / 98f;
                float pitch = Mathf.Acos(Mathf.Clamp01(0.2094f * r / chord)) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.LookRotation(Vector3.right, Vector3.up);
                frames.Add(new LoftFrame(new Vector3(r, 0f, 0f), rot));
            }
            var shared = new CrossSection();
            Func<int, CrossSection> at = k =>
            {
                float r = frames[k].Position.x;
                return MeshKit.Airfoil(14f + 16f * (r - 40f) / 98f, 3f, 12, shared);
            };
            var mb = new MeshBuilder();
            MeshKit.LoftVarying(mb, at, frames, true);
            Mesh m = Make(mb, "Blade");
            AssertClosedOutward(m);
            Assert.AreEqual(AbyssConst.FAN_R_TIP, m.bounds.max.x, 0.2f, "tip radius");
        }

        [Test]
        public void HelixTube_IsClosed()
        {
            var mb = new MeshBuilder();
            MeshKit.HelixTube(mb, new Vector3(0f, 0f, 0f), 2f, 1.5f, 0f, 2.25f, 0.1f, 48, 8);
            AssertClosedSolid(Make(mb, "HelixTube"));

            var mbR = new MeshBuilder();
            MeshKit.HelixRibbon(mbR, new Vector3(3f, 1f, -2f), 4f, 3f, 45f, 1.5f, 0.8f, 64);
            AssertValid(Make(mbR, "HelixRibbon"));
            for (int i = 0; i < mbR.VertexCount; i++) Assert.Greater(mbR.Normals[i].y, 0.9f, "ribbon faces up");
        }

        // ------------------------------------------------------------------------------------------ simple shapes

        [Test]
        public void TorusAndSphere_AreClosedOutward()
        {
            var mb = new MeshBuilder();
            MeshKit.Torus(mb, new Vector3(0f, 1.2f, 22f), Vector3.forward, 0.2f, 0.02f, 32, 10);
            AssertClosedOutward(Make(mb, "Torus"));
            var mbS = new MeshBuilder();
            MeshKit.Sphere(mbS, new Vector3(1f, 2f, 3f), 0.25f, 24, 12);
            AssertClosedOutward(Make(mbS, "Sphere"));
        }

        [Test]
        public void FlatShapes_FaceTheRequestedDirection()
        {
            var mb = new MeshBuilder();
            MeshKit.GridQuad(mb, Vector3.zero, Vector3.right, Vector3.up, 4f, 3f, 4, 3);
            AssertValid(Make(mb, "GridQuad"));
            for (int i = 0; i < mb.VertexCount; i++) Assert.AreEqual(-1f, mb.Normals[i].z, 1e-5f, "normal = cross(V, U)");

            var mbA = new MeshBuilder();
            MeshKit.Annulus(mbA, new Vector3(0f, -3f, 244f), 22f, 28f, 64);
            AssertValid(Make(mbA, "Annulus"));
            for (int i = 0; i < mbA.VertexCount; i++) Assert.AreEqual(1f, mbA.Normals[i].y, 1e-5f);

            var mbD = new MeshBuilder();
            Vector3 dn = new Vector3(1f, 1f, 0f).normalized;
            MeshKit.Disc(mbD, new Vector3(2f, 3f, 4f), dn, 0.5f, 24);
            AssertValid(Make(mbD, "Disc"));
            for (int i = 0; i < mbD.VertexCount; i++) Assert.AreEqual(1f, Vector3.Dot(mbD.Normals[i], dn), 1e-5f);

            var mbH = new MeshBuilder();
            MeshKit.HexAnnulus(mbH, Vector2.zero, -1087f, 350f, 380f);
            AssertValid(Make(mbH, "HexAnnulus"));
            for (int i = 0; i < mbH.VertexCount; i++) Assert.AreEqual(1f, mbH.Normals[i].y, 1e-5f);
        }

        [Test]
        public void HelicalAnnulus_LedgeIsClosed_WithSpecHeights()
        {
            var mb = new MeshBuilder();
            // ledge: alpha 0..pi maps to phi = alpha - pi/2; floor -3 at alpha 0 to -7 at alpha pi
            MeshKit.HelicalAnnulus(mb, AbyssConst.ARC_CENTRE, AbyssConst.LEDGE_R_IN, AbyssConst.LEDGE_R_OUT, -0.5f * Mathf.PI,
                0.5f * Mathf.PI, -3f, -7f, 96, AbyssConst.LEDGE_COLLAR_THICK, true, true, true, 2);
            Mesh m = Make(mb, "Ledge");
            AssertClosedSolid(m);
            Assert.AreEqual(-3f, m.bounds.max.y, 1e-4f);
            Assert.AreEqual(-7f - AbyssConst.LEDGE_COLLAR_THICK, m.bounds.min.y, 1e-4f);
            Assert.AreEqual(AbyssConst.LEDGE_R_OUT, m.bounds.max.x, 1e-3f);
            // W7 (alpha pi/2, phi 0): top surface at y -5 at radius 25
            bool found = false;
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i];
                if (mb.Normals[i].y > 0.9f && Mathf.Abs(p.z - 244f) < 1e-3f && Mathf.Abs(p.x - 25f) < 1e-3f)
                {
                    Assert.AreEqual(-5f, p.y, 1e-3f);
                    found = true;
                }
            }
            Assert.IsTrue(found, "a top vertex at W7");
        }

        // ------------------------------------------------------------------------------------------ hexagonal structures

        static void MeasureRing(MeshBuilder mb, Vector2 c, out float aMin, out float aMax)
        {
            aMin = float.PositiveInfinity; aMax = float.NegativeInfinity;
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i];
                float a = MeshKit.HexApothemOf(c, new Vector2(p.x, p.z));
                aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
            }
        }

        static void AssertRingFacesInward(MeshBuilder mb, Vector2 c)
        {
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 n = mb.Normals[i], p = mb.Positions[i];
                if (Mathf.Abs(n.y) > 0.1f) continue;
                Assert.Less(n.x * (p.x - c.x) + n.z * (p.z - c.y), 0f, "vertical faces look toward the axis");
            }
        }

        [Test]
        public void MitredRing_RibDimensions()
        {
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            float protrusion = AbyssConst.SHAFT_APOTHEM - AbyssConst.RIB_INNER_APOTHEM;
            var mb = new MeshBuilder();
            MeshKit.MitredRing(mb, c, 0f, AbyssConst.RIB_INNER_APOTHEM, protrusion, AbyssConst.RIB_HEIGHT, AbyssConst.RIB_BEVEL);
            Assert.AreEqual(60, mb.TriangleCount);
            Mesh m = Make(mb, "Rib");
            AssertValid(m);
            MeasureRing(mb, c, out float aMin, out float aMax);
            Assert.AreEqual(344f, aMin, 1e-3f, "inner apothem");
            Assert.AreEqual(350f, aMax, 1e-3f, "outer apothem");
            Assert.AreEqual(6f, m.bounds.size.y, 1e-4f, "height");
            Assert.AreEqual(350f, m.bounds.max.z - c.y, 1e-3f, "flats perpendicular to Z at the outer apothem");
            Assert.AreEqual(350f / Mathf.Cos(30f * Mathf.Deg2Rad), m.bounds.max.x - c.x, 1e-2f, "corners on +X");
            AssertRingFacesInward(mb, c);
        }

        [Test]
        public void MitredRing_BeltDimensions()
        {
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            var mb = new MeshBuilder();
            MeshKit.MitredRing(mb, c, 120f, AbyssConst.BELT_INNER_APOTHEM, 20f, AbyssConst.BELT_HEIGHT, AbyssConst.BELT_BEVEL);
            Mesh m = Make(mb, "Belt");
            AssertValid(m);
            MeasureRing(mb, c, out float aMin, out float aMax);
            Assert.AreEqual(330f, aMin, 1e-3f, "inner apothem");
            Assert.AreEqual(350f, aMax, 1e-3f, "outer apothem");
            Assert.AreEqual(12f, m.bounds.size.y, 1e-4f, "height");
            Assert.AreEqual(120f, m.bounds.center.y, 1e-4f, "centred on y");
            AssertRingFacesInward(mb, c);
            int worn = 0;
            for (int i = 0; i < mb.VertexCount; i++) if (mb.Colors[i].g == 255) worn++;
            Assert.AreEqual(6 * 2 * 4, worn, "bevel vertices carry edge wear");
        }

        [Test]
        public void MitredRing_WithOuterFace_IsClosed()
        {
            var mb = new MeshBuilder();
            MeshKit.MitredRing(mb, new Vector2(5f, -3f), 2f, 10f, 1f, 0.5f, 0.05f, true, 0f, 3);
            AssertClosedSolid(Make(mb, "RingClosed"));
        }

        [Test]
        public void HexTube_WallsFaceTheHole_TenRibs()
        {
            Vector2 c = new Vector2(658.18f, 380f);
            var mb = new MeshBuilder();
            int ribs = MeshKit.HexTube(mb, c, AbyssConst.SHAFT_APOTHEM, AbyssConst.TERRACE_Y, 600f);
            Assert.AreEqual(10, ribs);
            Mesh m = Make(mb, "HexTube");
            AssertValid(m);
            AssertRingFacesInward(mb, c);
            Assert.AreEqual(AbyssConst.TERRACE_Y, m.bounds.max.y, 1e-3f);
            Assert.AreEqual(AbyssConst.TERRACE_Y - 600f, m.bounds.min.y, 1e-3f);
        }

        [Test]
        public void HexWallPanel_MatchesShaftFlats_AndSharesCorners()
        {
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            Assert.AreEqual(MeshKit.HexCorner(c, 350f, 0), MeshKit.HexCorner(c, 350f, 6));
            var mb = new MeshBuilder();
            MeshKit.HexWallPanel(mb, c, AbyssConst.SHAFT_APOTHEM, 1, -200f, 0f, 4, 2, true); // face normal +Z: the far flat
            AssertValid(Make(mb, "FarFlat"));
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Assert.AreEqual(AbyssConst.SHAFT_FAR_Z, mb.Positions[i].z, 1e-3f);
                Assert.AreEqual(-1f, mb.Normals[i].z, 1e-5f, "inward panel faces the shaft centre");
            }
            var mbN = new MeshBuilder();
            MeshKit.HexWallPanel(mbN, c, AbyssConst.SHAFT_APOTHEM, 4, -200f, 0f, 1, 1, true); // the near flat
            for (int i = 0; i < mbN.VertexCount; i++) Assert.AreEqual(AbyssConst.SHAFT_NEAR_Z, mbN.Positions[i].z, 1e-3f);
            float faceWidth = (MeshKit.HexCorner(c, 350f, 1) - MeshKit.HexCorner(c, 350f, 2)).magnitude;
            Assert.AreEqual(AbyssConst.SHAFT_R_CIRC, faceWidth, 0.01f);
        }

        [Test]
        public void WarrenTruss_25Bays_Has75Members_AndIsValid()
        {
            foreach (TrussMemberShape shape in new[] { TrussMemberShape.IBeam, TrussMemberShape.ChamferBox })
            {
                var mb = new MeshBuilder();
                int members = MeshKit.WarrenTruss(mb, new Vector3(0f, -3.2f, AbyssConst.BRIDGE_Z0), new Vector3(0f, -3.2f, AbyssConst.BRIDGE_Z1),
                    AbyssConst.BRIDGE_TRUSS_DEPTH, 25, 0.4f, Vector3.up, shape);
                Assert.AreEqual(75, members);
                Assert.AreEqual((75 + 26) * 44, mb.TriangleCount, "44 triangles per member and per gusset plate (26 diagonal nodes)");
                Mesh m = Make(mb, "Truss" + shape);
                AssertClosedSolid(m);
                Assert.AreEqual(-3.2f - 6f - 0.2f, m.bounds.min.y, 1e-3f);
                Assert.AreEqual(-3.2f + 0.2f, m.bounds.max.y, 1e-3f);

                var bare = new MeshBuilder();
                Assert.AreEqual(75, MeshKit.WarrenTruss(bare, new Vector3(0f, -3.2f, AbyssConst.BRIDGE_Z0),
                    new Vector3(0f, -3.2f, AbyssConst.BRIDGE_Z1), AbyssConst.BRIDGE_TRUSS_DEPTH, 25, 0.4f, Vector3.up, shape, false, false));
                Assert.AreEqual(75 * 44, bare.TriangleCount, "no gussets");
            }
        }

        [Test]
        public void WarrenTruss_HasNoCoplanarOverlappingFaces()
        {
            // bridge (14.5.2: 6 m deep, 25 bays of 6 m, 0.4 m members, 45 degree diagonals) and gantry (14.5.6: 4 m, 33 bays, 0.3 m)
            float[] depth = { 6f, 4f }, length = { 150f, 198f }, size = { 0.4f, 0.3f };
            int[] bays = { 25, 33 };
            for (int c = 0; c < 2; c++)
            foreach (TrussMemberShape shape in new[] { TrussMemberShape.IBeam, TrussMemberShape.ChamferBox })
            {
                var mb = new MeshBuilder();
                Vector3 a = new Vector3(1f, -3.2f, 66f);
                MeshKit.WarrenTruss(mb, a, a + new Vector3(0f, 0f, length[c]), depth[c], bays[c], size[c], Vector3.up, shape);
                Assert.AreEqual((3 * bays[c] + bays[c] + 1) * 44, mb.TriangleCount);
                Assert.AreEqual(0, CoplanarOverlaps(mb, 44, 1e-3f), "z-fighting faces in truss " + c + " " + shape);
            }
        }

        [Test]
        public void CoplanarOverlaps_DetectsCoincidentMembers()
        {
            var mb = new MeshBuilder();
            MeshKit.IBeam(mb, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f), 0.4f, 0.4f, 0.03f, 0.04f, Vector3.up);
            MeshKit.IBeam(mb, new Vector3(0f, 0f, 2f), new Vector3(3f, 0f, 5f), 0.4f, 0.4f, 0.03f, 0.04f, Vector3.up);
            Assert.Greater(CoplanarOverlaps(mb, 44, 1e-3f), 0, "two identical crossing I-beams share their flange planes");
            var abut = new MeshBuilder();
            MeshKit.IBeam(abut, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f), 0.4f, 0.4f, 0.03f, 0.04f, Vector3.up);
            MeshKit.IBeam(abut, new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, 8f), 0.4f, 0.4f, 0.03f, 0.04f, Vector3.up);
            Assert.AreEqual(0, CoplanarOverlaps(abut, 44, 1e-3f), "end-to-end members only touch");
        }

        /// <summary>
        /// Same-facing triangle pairs from different parts (consecutive runs of trisPerPart triangles) whose planes are closer
        /// than planeTol and whose interiors overlap by more than 0.1 mm in the plane: the faces that z-fight.
        /// </summary>
        static int CoplanarOverlaps(MeshBuilder mb, int trisPerPart, float planeTol)
        {
            List<Vector3> P = mb.Positions;
            List<int> I = mb.Indices;
            int tc = mb.TriangleCount;
            var nrm = new Vector3[tc];
            var lo = new Vector3[tc];
            var hi = new Vector3[tc];
            for (int t = 0; t < tc; t++)
            {
                Vector3 a = P[I[3 * t]], b = P[I[3 * t + 1]], c = P[I[3 * t + 2]];
                nrm[t] = Vector3.Cross(b - a, c - a).normalized;
                lo[t] = Vector3.Min(a, Vector3.Min(b, c));
                hi[t] = Vector3.Max(a, Vector3.Max(b, c));
            }
            var A = new Vector2[3];
            var B = new Vector2[3];
            int count = 0;
            for (int i = 0; i < tc; i++)
            for (int j = (i / trisPerPart + 1) * trisPerPart; j < tc; j++) // pairs from different parts only
            {
                if (Vector3.Dot(nrm[i], nrm[j]) < 0.99999f) continue;
                if (lo[i].x > hi[j].x + planeTol || lo[j].x > hi[i].x + planeTol || lo[i].y > hi[j].y + planeTol ||
                    lo[j].y > hi[i].y + planeTol || lo[i].z > hi[j].z + planeTol || lo[j].z > hi[i].z + planeTol) continue;
                Vector3 n = nrm[i];
                if (Mathf.Abs(Vector3.Dot(n, P[I[3 * j]] - P[I[3 * i]])) > planeTol) continue;
                Vector3 u = Vector3.Cross(Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right, n).normalized;
                Vector3 v = Vector3.Cross(n, u);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 pa = P[I[3 * i + k]], pb = P[I[3 * j + k]];
                    A[k] = new Vector2(Vector3.Dot(pa, u), Vector3.Dot(pa, v));
                    B[k] = new Vector2(Vector3.Dot(pb, u), Vector3.Dot(pb, v));
                }
                if (TrianglesOverlap2D(A, B, 1e-4f)) count++;
            }
            return count;
        }

        /// <summary>Separating-axis test of two 2D triangles: true when they overlap by more than margin on every edge axis.</summary>
        static bool TrianglesOverlap2D(Vector2[] A, Vector2[] B, float margin)
        {
            for (int tri = 0; tri < 2; tri++)
            {
                Vector2[] T = tri == 0 ? A : B;
                for (int e = 0; e < 3; e++)
                {
                    Vector2 ed = T[(e + 1) % 3] - T[e];
                    Vector2 ax = new Vector2(-ed.y, ed.x);
                    float m = ax.magnitude;
                    if (m < 1e-9f) return false;
                    ax /= m;
                    float minA = float.PositiveInfinity, maxA = float.NegativeInfinity;
                    float minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
                    for (int k = 0; k < 3; k++)
                    {
                        float pa = Vector2.Dot(A[k], ax), pb = Vector2.Dot(B[k], ax);
                        minA = Mathf.Min(minA, pa); maxA = Mathf.Max(maxA, pa);
                        minB = Mathf.Min(minB, pb); maxB = Mathf.Max(maxB, pb);
                    }
                    if (Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB) < margin) return false;
                }
            }
            return true;
        }

        [Test]
        public void ThinLineMembers_WriteAxisPointUV2_InMeshSpace()
        {
            var mb = new MeshBuilder();
            mb.Color = new Color32(255, 0, 0, 255);
            MeshKit.ChamferBox(mb, new Vector3(0f, -1f, 0f), new Vector3(1f, 0.2f, 1f), 0.02f); // plain part before the members
            int plainBefore = mb.VertexCount;
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(10f, -4f, 2f), Quaternion.Euler(0f, 30f, 10f), Vector3.one);
            mb.Push(m);
            Vector3 a = new Vector3(1f, 2f, 3f), b = new Vector3(4f, 6f, 3f);
            var starts = new List<int>();
            starts.Add(mb.VertexCount); MeshKit.ChamferMember(mb, a, b, 0.1f, 0.06f, 0.01f, Vector3.up, true);
            starts.Add(mb.VertexCount); MeshKit.IBeam(mb, a, b, 0.3f, 0.2f, 0.02f, 0.03f, Vector3.up, true);
            starts.Add(mb.VertexCount); MeshKit.Cylinder(mb, a, b, 0.05f, 8, true, true);
            starts.Add(mb.VertexCount);
            mb.Pop();
            int members = mb.VertexCount;
            MeshKit.ChamferBox(mb, new Vector3(0f, 3f, 0f), Vector3.one, 0.02f); // plain part after the members
            Assert.IsFalse(mb.WriteUV1 || mb.WriteUV2, "ThinLine state restored after each member");

            Mesh mesh = Make(mb, "ThinLineUV2");
            AssertValid(mesh);
            var uv1 = new List<Vector4>();
            var uv2 = new List<Vector4>();
            mesh.GetUVs(1, uv1);
            mesh.GetUVs(2, uv2);
            Assert.AreEqual(mb.VertexCount, uv1.Count);
            Assert.AreEqual(mb.VertexCount, uv2.Count);
            Vector3 wa = m.MultiplyPoint3x4(a), wb = m.MultiplyPoint3x4(b);
            Vector3 axis = (wb - wa).normalized;
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector4 u = uv2[i];
                if (i < plainBefore || i >= members)
                {
                    Assert.AreEqual(0f, u.w, "plain vertices keep UV2.w = 0 (normal-based ThinLine path)");
                    continue;
                }
                Assert.AreEqual(1f, u.w, "ThinLine vertex " + i + " flags its axis point");
                Vector3 q = new Vector3(u.x, u.y, u.z) - wa;
                Assert.Less((q - axis * Vector3.Dot(q, axis)).magnitude, 1e-4f, "UV2 point lies on the member axis in mesh space");
                Assert.AreEqual(1f, Vector3.Dot(new Vector3(uv1[i].x, uv1[i].y, uv1[i].z), axis), 1e-4f, "UV1 axis in mesh space");
            }
            // the push direction (radial from the axis point, as in AbThinLineExpand) is shared by split-normal vertices
            for (int part = 0; part + 1 < starts.Count; part++)
            for (int i = starts[part]; i < starts[part + 1]; i++)
            for (int j = i + 1; j < starts[part + 1]; j++)
            {
                if ((mb.Positions[i] - mb.Positions[j]).sqrMagnitude > 1e-10f) continue;
                Assert.Less((Push(mb.Positions[i], uv1[i], uv2[i]) - Push(mb.Positions[j], uv1[j], uv2[j])).magnitude, 1e-5f,
                    "coincident vertices of part " + part + " move together");
            }

            var truss = new MeshBuilder();
            MeshKit.WarrenTruss(truss, new Vector3(0f, -3.2f, 66f), new Vector3(0f, -3.2f, 216f), 6f, 25, 0.4f, Vector3.up,
                TrussMemberShape.IBeam, true);
            Assert.AreEqual(truss.VertexCount, truss.UV2.Count);
            for (int i = 0; i < truss.VertexCount; i++)
            {
                Vector4 u1 = truss.UV1[i], u2 = truss.UV2[i];
                Assert.AreEqual(1f, u2.w);
                Assert.Greater(u1.w, 0f);
                Vector3 ax = new Vector3(u1.x, u1.y, u1.z);
                Vector3 d = truss.Positions[i] - new Vector3(u2.x, u2.y, u2.z);
                Assert.Less((d - ax * Vector3.Dot(d, ax)).magnitude, 0.8f, "every truss vertex is pushed from a nearby axis");
            }
        }

        static Vector3 Push(Vector3 p, Vector4 uv1, Vector4 uv2)
        {
            Vector3 axis = new Vector3(uv1.x, uv1.y, uv1.z).normalized;
            Vector3 d = p - new Vector3(uv2.x, uv2.y, uv2.z);
            d -= axis * Vector3.Dot(d, axis);
            return d.normalized;
        }

        // ------------------------------------------------------------------------------------------ index format, validator

        [Test]
        public void MoreThan65535Vertices_UsesUInt32Indices()
        {
            var mb = new MeshBuilder();
            MeshKit.GridQuad(mb, Vector3.zero, Vector3.right, Vector3.forward, 300f, 300f, 300, 300);
            Assert.Greater(mb.VertexCount, 65535);
            Mesh m = Make(mb, "BigGrid");
            Assert.AreEqual(IndexFormat.UInt32, m.indexFormat);
            AssertValid(m);

            var small = new MeshBuilder();
            MeshKit.GridQuad(small, Vector3.zero, Vector3.right, Vector3.forward, 10f, 10f, 10, 10);
            Mesh ms = Make(small, "SmallGrid");
            Assert.AreEqual(IndexFormat.UInt16, ms.indexFormat);
            AssertValid(ms);
        }

        [Test]
        public void MeshValidation_DetectsDefects()
        {
            var mb = new MeshBuilder();
            int a = mb.Vertex(new Vector3(0f, 0f, 0f), Vector3.up);
            int b = mb.Vertex(new Vector3(0f, 0f, 1f), Vector3.up);
            int c = mb.Vertex(new Vector3(1f, 0f, 0f), Vector3.up);
            mb.Tri(a, b, c);
            Assert.IsTrue(MeshValidation.Validate(mb).Ok, MeshValidation.Validate(mb).Describe());

            mb.Tri(a, c, b);                      // winding against the normals
            mb.Tri(a, a, b);                      // degenerate
            mb.Indices.Add(0); mb.Indices.Add(1); mb.Indices.Add(99); // out of range
            mb.Vertex(new Vector3(float.NaN, 0f, 0f), Vector3.up);
            MeshReport r = MeshValidation.Validate(mb);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(1, r.WindingMismatches);
            Assert.AreEqual(1, r.DegenerateTriangles);
            Assert.AreEqual(1, r.IndicesOutOfRange);
            Assert.AreEqual(1, r.NonFinitePositions);
            Assert.AreEqual(4, r.TriangleCount);
            StringAssert.Contains("FAIL", r.Describe());
        }

        // ------------------------------------------------------------------------------------------ vertex bakes

        [Test]
        public void BakeAO_DarkensOccludedVertices_Deterministically()
        {
            var bounds = new List<Bounds> { new Bounds(new Vector3(5f, 1f, 5f), new Vector3(2f, 2f, 2f)) };
            Color32[] first = null;
            for (int pass = 0; pass < 2; pass++)
            {
                var mb = new MeshBuilder();
                MeshKit.GridQuad(mb, Vector3.zero, Vector3.right, Vector3.forward, 10f, 10f, 20, 20); // floor facing up
                Assert.AreEqual(1f, mb.Normals[0].y, 1e-5f);
                MeshKit.BakeAO(mb, bounds, 2f, 32, 7u);
                int near = Nearest(mb, new Vector3(5f, 0f, 3.5f)), far = Nearest(mb, Vector3.zero);
                Assert.Less(mb.Colors[near].r, 235, "vertex 0.5 m from the box is occluded");
                Assert.AreEqual(255, mb.Colors[far].r, "vertex beyond maxDistance is untouched");
                if (first == null) first = mb.Colors.ToArray();
                else for (int i = 0; i < first.Length; i++) Assert.AreEqual(first[i].r, mb.Colors[i].r, "deterministic AO");
            }
        }

        [Test]
        public void BakeAO_FloorWallJunction_IsHalfOccluded_NotBlack()
        {
            var mb = new MeshBuilder();
            MeshKit.GridQuad(mb, Vector3.zero, Vector3.right, Vector3.forward, 10f, 10f, 10, 10); // floor y = 0, vertices every 1 m
            // wall x 4.5..6 from below the floor to 3 m: floor vertices at x = 6 lie on its +X face, those at x = 5 inside it
            var wall = new List<Bounds> { new Bounds(new Vector3(5.25f, 1f, 5f), new Vector3(1.5f, 4f, 12f)) };
            MeshKit.BakeAO(mb, wall, 2f, 64, 11u);
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i];
                int r = mb.Colors[i].r;
                if (Mathf.Abs(p.x - 6f) < 1e-4f)
                {
                    // only the rays entering the wall (about half the hemisphere) are blocked
                    Assert.Greater(r, 90, "junction vertex " + p + " is not black");
                    Assert.Less(r, 170, "junction vertex " + p + " is occluded by the wall");
                    int next = Nearest(mb, p + new Vector3(1f, 0f, 0f));
                    Assert.Greater(mb.Colors[next].r, r, "occlusion fades away from the wall");
                }
                else if (Mathf.Abs(p.x - 5f) < 1e-4f) Assert.AreEqual(0, r, "vertex inside the wall is fully occluded");
                else if (p.x > 8.5f) Assert.AreEqual(255, r, "vertex beyond maxDistance is untouched");
            }

            // the solid's own bounds: rays leave its faces outward, its surface stays unoccluded
            var box = new MeshBuilder();
            box.Box(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 2f));
            MeshKit.BakeAO(box, new List<Bounds> { new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 2f)) }, 2f, 32, 3u);
            for (int i = 0; i < box.VertexCount; i++) Assert.AreEqual(255, box.Colors[i].r, "own bounds never occlude");
        }

        static int Nearest(MeshBuilder mb, Vector3 p)
        {
            int best = 0;
            float bd = float.PositiveInfinity;
            for (int i = 0; i < mb.VertexCount; i++)
            {
                float d = (mb.Positions[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        [Test]
        public void EdgeWearFromNormals_MarksCreasesOnly()
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(1f, 2f, 3f), new Vector3(1f, 1f, 1f));
            MeshKit.EdgeWearFromNormals(mb);
            for (int i = 0; i < mb.VertexCount; i++) Assert.AreEqual(255, mb.Colors[i].g, "box corners are 90 degree creases");

            var mbS = new MeshBuilder();
            MeshKit.Sphere(mbS, Vector3.zero, 1f, 24, 12);
            MeshKit.EdgeWearFromNormals(mbS);
            for (int i = 0; i < mbS.VertexCount; i++) Assert.AreEqual(0, mbS.Colors[i].g, "smooth sphere has no creases");
        }

        // ------------------------------------------------------------------------------------------ performance

        [Test]
        public void Generation_IsFastEnoughFor1Point5MillionTriangles()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long tris = 0;
            var mb = new MeshBuilder();

            for (int i = 0; i < 4000; i++) MeshKit.ChamferBox(mb, new Vector3(i * 0.5f, 0f, 0f), new Vector3(0.4f, 0.3f, 2.4f), 0.02f);
            tris += mb.TriangleCount; mb.Clear();

            for (int i = 0; i < 4000; i++) MeshKit.HexBolt(mb, new Vector3(i * 0.1f, 0f, 0f), Vector3.up, 0.08f);
            tris += mb.TriangleCount; mb.Clear();

            var prof = new List<Vector2>();
            MeshKit.BandedColumnProfile(prof, 44f, -600f, 600f, 120f, 2f, 0.4f, 0.05f, 0f);
            for (int i = 0; i < 4; i++) { MeshKit.Lathe(mb, prof, 128, 40f, 0f, 360f, true); tris += mb.TriangleCount; mb.Clear(); }

            MeshKit.GridQuad(mb, Vector3.zero, Vector3.right, Vector3.up, 400f, 200f, 300, 300);
            tris += mb.TriangleCount; mb.Clear();

            for (int i = 0; i < 20; i++)
                MeshKit.WarrenTruss(mb, new Vector3(i * 3f, 0f, 0f), new Vector3(i * 3f, 0f, 150f), 6f, 25, 0.4f, Vector3.up);
            tris += mb.TriangleCount; mb.Clear();

            for (int i = 0; i < 16; i++) MeshKit.HexTube(mb, new Vector2(i * 760f, 0f), 350f, -1087f, 600f, 0f, 60f, 1.5f, 2f, 0.05f, 8);
            tris += mb.TriangleCount; mb.Clear();

            var path = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 40f), new Vector3(0f, -3f, 44.48f), new Vector3(0f, -3f, 60f) };
            var frames = new List<LoftFrame>();
            MeshKit.PathFrames(path, Vector3.up, frames, true);
            var sec = MeshKit.RoundedRectangle(2.4f, 2.5f, 0.3f, 16);
            for (int i = 0; i < 50; i++) MeshKit.Loft(mb, sec, frames, false, true);
            tris += mb.TriangleCount; mb.Clear();

            sw.Stop();
            double projected = sw.Elapsed.TotalSeconds * 1.5e6 / Math.Max(1, tris);
            Debug.Log($"[ABYSS] MeshKit perf: {tris} triangles in {sw.Elapsed.TotalMilliseconds:F0} ms, projected {projected:F2} s per 1.5 M triangles");
            Assert.Greater(tris, 500000);
            Assert.Less(projected, 10.0, "1.5 M triangles must generate well within the 15 s load budget");
        }
    }
}
