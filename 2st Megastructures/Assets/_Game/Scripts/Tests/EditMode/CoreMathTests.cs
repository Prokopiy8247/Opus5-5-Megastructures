using System;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Curved-world maths of 14.7 H1 / H10: the float32 bend (the HLSL operation order, C# twin RingBend.Bend) against
    /// the double reference, the rise and ground-elevation numbers, the KS02 terrain-edge elevation, NdotL of flat
    /// terrain with the rotated key, and the bounds inflation that prevents culling holes.
    /// </summary>
    public class CoreMathTests
    {
        static double MaxBendError(float camZ, float dMin, float dMax, float step, float[] heights, out string worst)
        {
            double maxErr = 0; worst = "";
            var cam = new Vector3(0f, 1.7f, camZ);
            foreach (float h in heights)
                for (float d = dMin; d <= dMax + 1e-3f; d += step)
                {
                    var p = new Vector3(3.5f, h, camZ + d);
                    Vector3 b = RingBend.Bend(p, cam);
                    RingBend.BendD(p.x, p.y, p.z, cam.z, out double bx, out double by, out double bz);
                    // error of the displacement B - P: P is the same float position in both, so this is |B32 - B64|
                    double e = Math.Max(Math.Abs(b.x - bx), Math.Max(Math.Abs(b.y - by), Math.Abs(b.z - bz)));
                    if (e > maxErr) { maxErr = e; worst = "h " + h + " d " + d + " camZ " + camZ; }
                }
            return maxErr;
        }

        [Test]
        public void Bend_Float32MatchesDouble_Near_Within0p1mm()
        {
            var hs = new[] { -3.2f, -2.2f, 0f, 0.012f, 0.9f, 1.7f, 3.0f, 14f, 30f };
            foreach (float camZ in new[] { 0f, 150f, 505f, 660f, 960f })
            {
                double e = MaxBendError(camZ, -50f, 50f, 0.25f, hs, out string worst);
                Assert.LessOrEqual(e, 1e-4, "near-field bend error " + (e * 1000).ToString("0.0000") + " mm at " + worst);
            }
        }

        [Test]
        public void Bend_Float32MatchesDouble_Far_Within5mm()
        {
            var hs = new[] { -3.2f, 0f, 1.7f, 22f, 260f, 1800f };
            foreach (float camZ in new[] { 0f, 364f, 960f })
            {
                double e = MaxBendError(camZ, -8192f, 8192f, 8f, hs, out string worst);
                Assert.LessOrEqual(e, 5e-3, "far-field bend error " + (e * 1000).ToString("0.000") + " mm at " + worst);
            }
        }

        [Test]
        public void Bend_StableFormBeatsNaiveForm_NearCamera()
        {
            // the naive R - (R - h) cos t terraces near the camera by about 1 mm in float32 (14.7 H1)
            const float R = RingConstants.RingR;
            double worstNaive = 0, worstStable = 0;
            for (float d = 0.5f; d <= 50f; d += 0.5f)
            {
                float h = 0.012f, t = d / R;
                float naive = R - (R - h) * Mathf.Cos(t);
                float stable = RingBend.Bend(new Vector3(0f, h, d), Vector3.zero).y;
                RingBend.BendD(0, h, d, 0, out _, out double by, out _);
                worstNaive = Math.Max(worstNaive, Math.Abs(naive - by));
                worstStable = Math.Max(worstStable, Math.Abs(stable - by));
            }
            Assert.Less(worstStable, worstNaive, "the stable form must be more accurate than the naive form");
            Assert.LessOrEqual(worstStable, 1e-5, "stable form error near the camera");
        }

        [TestCase(1000f, 31f)]
        [TestCase(2000f, 125f)]
        [TestCase(4000f, 497f)]
        [TestCase(8192f, 2052f)]
        [TestCase(12000f, 4293f)]
        public void Rise_MatchesSpec(float d, float expected)
        {
            Assert.AreEqual(expected, RingBend.Rise(d), 1.0f, "rise at " + d + " m (14.7 H1)");
        }

        [TestCase(1000f, 1.8f)]
        [TestCase(2000f, 3.6f)]
        [TestCase(4000f, 7.2f)]
        [TestCase(8192f, 14.7f)]
        [TestCase(12000f, 21.5f)]
        public void GroundElevation_IsHalfTheta(float d, float expectedDeg)
        {
            // a ground point at axial distance d seen from ground level: inscribed-angle rule, elevation = theta / 2
            Vector3 b = RingBend.Bend(new Vector3(0f, 0f, d), Vector3.zero);
            float el = Mathf.Atan2(b.y, b.z) * Mathf.Rad2Deg;
            float half = 0.5f * d / RingConstants.RingR * Mathf.Rad2Deg;
            Assert.AreEqual(half, el, 0.01f, "elevation vs theta/2");
            Assert.AreEqual(expectedDeg, el, 0.06f, "elevation at " + d + " m");
        }

        [Test]
        public void KS02_TerrainEdgeElevation_14p67()
        {
            var ks = KeyShots.All[2];
            float zEdge = ks.Eye.z + RingConstants.ClipExtent;
            float h = RingWorld.Height(0f, zEdge, 0f);
            Vector3 b = RingBend.Bend(new Vector3(0f, h, zEdge), ks.Eye);
            float el = Mathf.Atan2(b.y - ks.Eye.y, b.z - ks.Eye.z) * Mathf.Rad2Deg;
            Assert.AreEqual(14.67f, el, 0.3f, "KS02 terrain-edge elevation (14.5.2 acceptance 1), ground height " + h);
            Assert.AreEqual(RingConstants.ClipEdgeElevation, 0.5f * RingConstants.ClipExtent / RingConstants.RingR * Mathf.Rad2Deg, 0.01f, "ClipEdgeElevation constant");
        }

        [TestCase(0f)]
        [TestCase(3000f)]
        [TestCase(8000f)]
        public void NdotL_FlatTerrain_0p438_WithRotatedKey(float d)
        {
            float t = RingBend.Theta(d);
            Vector3 n = RingBend.Rotate(Vector3.up, t);
            Vector3 l = RingBend.Rotate(RingConstants.StarL, t);
            Assert.AreEqual(0.438f, Vector3.Dot(n.normalized, l.normalized), 0.01f, "NdotL at " + d + " m (14.7 H10 a)");
            // the bent normal is the true ring normal (toward the axis) at that distance
            Vector3 b0 = RingBend.Bend(new Vector3(0f, 0f, d), Vector3.zero), b1 = RingBend.Bend(new Vector3(0f, 0f, d + 0.5f), Vector3.zero);
            Vector3 tangent = (b1 - b0).normalized;
            Assert.AreEqual(0f, Vector3.Dot(n, tangent), 1e-3f, "rotated normal is perpendicular to the bent surface");
        }

        [Test]
        public void InflateBounds_CoversTheBentGeometry()
        {
            foreach (float maxD in new[] { 40f, 500f, 3000f, 8192f })
            {
                var b = RingBend.InflateBounds(new Bounds(new Vector3(0f, 0f, 0.5f * maxD), new Vector3(10f, 1f, maxD)), maxD);
                // a camera at the near end bends the far end up by Rise(maxD)
                Vector3 far = RingBend.Bend(new Vector3(0f, 0.5f, maxD), Vector3.zero);
                Assert.IsTrue(b.Contains(far), "inflated bounds contain the bent far end at " + maxD + " m (y " + far.y + ", max " + b.max.y + ")");
            }
        }

        [Test]
        public void StarDirections_MatchTheTable()
        {
            var l = RingConstants.StarL;
            Assert.AreEqual(-0.833f, l.x, 0.001f); Assert.AreEqual(0.438f, l.y, 0.001f); Assert.AreEqual(0.337f, l.z, 0.001f);
            var l7 = RingConstants.StarL7;
            Assert.AreEqual(0.987f, l7.x, 0.001f); Assert.AreEqual(0.122f, l7.y, 0.001f); Assert.AreEqual(-0.104f, l7.z, 0.001f);
            Assert.AreEqual(1f, l.magnitude, 1e-4f); Assert.AreEqual(1f, l7.magnitude, 1e-4f);
        }
    }
}
