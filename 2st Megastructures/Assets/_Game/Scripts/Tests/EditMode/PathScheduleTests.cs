using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Path and timeline (14.4), parameter continuity (Rule P, 14.6), key-shot vFOV (14.10), the sunset model (14.5.4)
    /// and the C# twin of ArchHaze (14.7 H2).
    /// </summary>
    public class PathScheduleTests
    {
        [Test]
        public void Path_Length960_AndWaypointProgress()
        {
            var c = RingWorld.Cumulative;
            Assert.AreEqual(960f, c[c.Length - 1], 1e-3f, "path length (14.4)");
            float[] expected = { 0, 24, 276, 411, 558, 576, 594, 676, 760, 774, 894, 960 };
            Assert.AreEqual(expected.Length, c.Length, "waypoint count");
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], c[i], 1e-3f, "s at waypoint P" + i);
            Assert.AreEqual(RingConstants.PathLength, c[c.Length - 1], 1e-3f);
        }

        [Test]
        public void Beats_LengthsAndDurations()
        {
            float[] len = { 24, 252, 135, 183, 180, 120, 66 };
            float total = 0, totalT = 0;
            for (int b = 0; b < 7; b++)
            {
                float l = RingConstants.BeatS[b + 1] - RingConstants.BeatS[b];
                Assert.AreEqual(len[b], l, 1e-3f, "beat " + (b + 1) + " length");
                Assert.AreEqual(l / RingConstants.Walk, RingConstants.BeatT[b], 1e-3f, "beat " + (b + 1) + " duration at 1.5 m/s");
                total += l; totalT += RingConstants.BeatT[b];
            }
            Assert.AreEqual(960f, total, 1e-3f);
            Assert.AreEqual(RingConstants.TotalTime, totalT, 1e-3f);
            Assert.AreEqual(640f, RingConstants.TotalTime, 1e-3f);
            // beat 4 = 147 + 18 + 18 and beat 5 = 82 + 84 + 14 (14.4 legs)
            var c = RingWorld.Cumulative;
            Assert.AreEqual(147f, c[4] - c[3], 1e-3f); Assert.AreEqual(18f, c[5] - c[4], 1e-3f); Assert.AreEqual(18f, c[6] - c[5], 1e-3f);
            Assert.AreEqual(82f, c[7] - c[6], 1e-3f); Assert.AreEqual(84f, c[8] - c[7], 1e-3f); Assert.AreEqual(14f, c[9] - c[8], 1e-3f);
            for (int b = 0; b < 7; b++) Assert.AreEqual(b, RingConstants.BeatAt(RingConstants.BeatS[b] + 0.01f));
        }

        [Test]
        public void Path_ProjectIsConsistentWithPointAt()
        {
            for (float s = 0f; s <= 960f; s += 0.5f)
            {
                var p = RingWorld.PointAt(s);
                float back = RingWorld.Project(p, s, 5f);
                Assert.AreEqual(s, back, 0.01f, "Project(PointAt(s))");
            }
        }

        struct Step { public string Name; public float Limit; public System.Func<RingParams, float> Get; }

        [Test]
        public void Schedule_ContinuitySweep_CaptureMode()
        {
            float oldCue = RingState.ArkCue;
            RingState.ArkCue = 0f;
            try
            {
                var steps = new[]
                {
                    new Step { Name = "EV", Limit = 0.02f, Get = p => p.Ev },
                    new Step { Name = "vFOV", Limit = 0.1f, Get = p => p.Vfov },
                    new Step { Name = "bloom intensity", Limit = 0.002f, Get = p => p.BloomIntensity },
                    new Step { Name = "saturation", Limit = 0.15f, Get = p => p.Saturation },
                    new Step { Name = "contrast", Limit = 0.15f, Get = p => p.Contrast },
                    new Step { Name = "white balance", Limit = 0.15f, Get = p => p.WbTemp },
                    new Step { Name = "vignette", Limit = 0.003f, Get = p => p.Vignette },
                };
                var fails = new StringBuilder();
                int nFail = 0;
                RingParams prev = default;
                for (int i = 0; i <= 9600; i++)
                {
                    float s = i * 0.1f;
                    float u = (s - RingConstants.SunsetS0) / RingConstants.SunsetD;   // capture: time branch = progress branch
                    var p = RingSchedule.Evaluate(s, u, RingSchedule.SetAt(s));
                    if (i > 0)
                        foreach (var st in steps)
                        {
                            float d = Mathf.Abs(st.Get(p) - st.Get(prev));
                            if (float.IsNaN(d) || d > st.Limit)
                            {
                                nFail++;
                                if (nFail <= 40) fails.AppendLine(st.Name + " step " + d.ToString("0.0000") + " > " + st.Limit + " at s " + s.ToString("0.0"));
                            }
                        }
                    prev = p;
                }
                Assert.AreEqual(0, nFail, "Rule P continuity (14.6):\n" + fails);
            }
            finally { RingState.ArkCue = oldCue; }
        }

        [Test]
        public void KeyShots_VfovEqualsSchedule()
        {
            foreach (var k in KeyShots.All)
                Assert.AreEqual(k.Vfov, RingSchedule.Vfov(k.S), 0.05f, k.Id + " vFOV vs the 14.2.5 schedule at s " + k.S);
            Assert.AreEqual(61.3f, 60f + 6f * RingMath.Smootherstep01((900f - 894f) / 18f), 0.05f, "beat 7 vFOV at s 900");
        }

        [Test]
        public void Sunset_StarVisibility()
        {
            Assert.AreEqual(1f, SunsetModel.V(0f), 1e-4f, "V(0) = 1");
            Assert.LessOrEqual(SunsetModel.V(0.55f), 0.01f, "V(0.55) <= 0.01");
            Assert.AreEqual(0.585f, SunsetModel.V(0.295f), 0.1f, "V at KS05 (u 0.295)");
            Assert.AreEqual(0f, SunsetModel.V(1f), 1e-4f, "V(1) = 0");
            float last = 1f;
            for (float u = 0f; u <= 1f; u += 0.002f)
            {
                float v = SunsetModel.V(u);
                Assert.LessOrEqual(v, last + 1e-4f, "V non-increasing at u " + u);
                last = v;
            }
            Assert.AreEqual(0.295f, (442f - RingConstants.SunsetS0) / RingConstants.SunsetD, 1e-3f, "KS05 capture phase u");
        }

        [Test]
        public void Sunset_KeyAndExposure()
        {
            Assert.AreEqual(3.0f, 3.0f * SunsetModel.KeyFactor(0f), 1e-4f, "key at u 0");
            Assert.AreEqual(0.09f, 3.0f * SunsetModel.KeyFactor(1f), 0.005f, "key at u 1 (14.5.4 acceptance 1)");
            float oldCue = RingState.ArkCue;
            RingState.ArkCue = 0f;
            try
            {
                Assert.AreEqual(3.0f, RingSchedule.Evaluate(411f, 0f, 0).KeyIntensity, 0.01f, "schedule key at s 411");
                Assert.AreEqual(0.09f, RingSchedule.Evaluate(516f, 1f, 0).KeyIntensity, 0.005f, "schedule key at s 516");
            }
            finally { RingState.ArkCue = oldCue; }
            float last = float.NegativeInfinity;
            for (float s = 399f; s <= 594f; s += 0.1f)
            {
                float ev = RingSchedule.Ev(s, Mathf.Clamp((s - 411f) / 105f, -2f, 1f));
                Assert.GreaterOrEqual(ev, last - 1e-5f, "EV monotone over the sunset at s " + s);
                last = ev;
            }
            Assert.AreEqual(1.6f, RingSchedule.Ev(516f, 1f), 0.01f, "EV +1.6 at u 1");
        }

        // ---------------- ArchHaze (14.7 H2, haze-layer model of ADR-012) ----------------
        // C# twin of RingArchHaze / RingFogAmountS in RingAtmosphere.hlsl along the heading yaw 0 (the KS02 / KS03 axis):
        // a ray of elevation e hits the arch at t = 2 R sin e; the terrain edge lies at the chord of 8 192 m of arc.
        const float HazeLayer = 2079f, AerialInv = 1f / 120000f;
        static float Fog(float de, float sigma, float mist) { float sd = sigma * de; return 1f - Mathf.Exp(-(sd * sd * (1f + mist) + de * AerialInv)); }
        static float EdgeT => 2f * RingConstants.RingR * Mathf.Sin(0.5f * RingConstants.ClipExtent / RingConstants.RingR);

        static float Hz(float e, float sigma, float mist, float night)
        {
            float t = 2f * RingConstants.RingR * Mathf.Sin(e * Mathf.Deg2Rad);
            float low = Fog(Mathf.Min(t, EdgeT), sigma, mist);
            float high = Fog(Mathf.Min(t, HazeLayer / Mathf.Max(Mathf.Sin(e * Mathf.Deg2Rad), 1e-3f)), RingConstants.FogSigmaDay, 0.8f);
            float hz = Mathf.Lerp(low, high, RingMath.Smoothstep(14.7f, 19.7f, e));
            return hz * (1f - 0.5f * night * RingMath.Smoothstep(16f, 32f, e));
        }

        [Test]
        public void ArchHaze_MonotoneAndAnchored()
        {
            float edgeE = 0.5f * RingConstants.ClipExtent / RingConstants.RingR * Mathf.Rad2Deg;   // 14.67 deg
            // valley (beats 1-3): sigma 1e-4 with the 0.8 mist; beat 4 night (3.6e-4); beat 5 (5e-4); no mist after beat 3
            var cases = new[] { (RingConstants.FogSigma[0], 0.8f), (RingConstants.FogSigmaNight4, 0f), (RingConstants.FogSigma[4], 0f) };
            foreach (var (sigma, mist) in cases)
            {
                // the arch meets the fogged terrain edge (the same fog of the same distance there)
                Assert.AreEqual(Fog(EdgeT, sigma, mist), Hz(edgeE, sigma, mist, 0f), 0.02f, "seam at the terrain edge, sigma " + sigma);
                foreach (float n in new[] { 0f, 0.5f, 1f })
                {
                    float last = 2f;
                    for (float e = edgeE; e <= 90f; e += 0.01f)
                    {
                        float h = Hz(e, sigma, mist, n);
                        Assert.LessOrEqual(h, last + 1e-5f, "ArchHaze non-increasing at e " + e + " (sigma " + sigma + ", N " + n + ")");
                        last = h;
                    }
                }
                Assert.LessOrEqual(Hz(40f, sigma, mist, 0f), 0.2f, "hz(40) <= 0.2 (sigma " + sigma + ")");
            }
            // valley numbers: the land climbs visibly (terrain edge about 0.72), the arch reads through the haze
            Assert.AreEqual(0.72f, Hz(edgeE, RingConstants.FogSigma[0], 0.8f, 0f), 0.03f, "valley terrain edge");
            Assert.AreEqual(0.29f, Hz(30f, RingConstants.FogSigma[0], 0.8f, 0f), 0.04f, "hz(30)");
            // night beats: the seam is still almost fully fogged (the spec's 0.985 regime)
            Assert.GreaterOrEqual(Hz(edgeE, RingConstants.FogSigma[4], 0f, 1f), 0.97f, "beat 5 seam fogged");
        }

        [Test]
        public void ArchHaze_HlslSourceMatchesTwin()
        {
            string path = Path.Combine(Application.dataPath, "_Game/Shaders/Include/RingAtmosphere.hlsl");
            if (!File.Exists(path)) Assert.Ignore("RingAtmosphere.hlsl not found");
            string src = File.ReadAllText(path);
            // the definition, not the first mention (the file header comment names RingArchHaze too)
            int i = src.IndexOf("float RingArchHaze(", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0, "float RingArchHaze(...) defined");
            string body = src.Substring(i, Mathf.Min(700, src.Length - i));
            foreach (var token in new[] { "14.7", "19.7", "0.8", "16.0", "32.0", "tEdge" })
                StringAssert.Contains(token, body, "RingArchHaze constant " + token);
            StringAssert.Contains("#define RING_HAZE_LAYER 2079.0", src, "haze layer thickness");
        }

        // ---------------- QA path helpers ----------------
        [Test]
        public void QaPath_FloorAndEyes()
        {
            Assert.AreEqual(0.9f, QaPath.FloorY(5f), 1e-4f);
            Assert.AreEqual(0.465f, QaPath.FloorY(17f), 1e-3f);
            Assert.AreEqual(0.03f, QaPath.FloorY(27f), 1e-4f);
            Assert.AreEqual(3.0f, QaPath.FloorY(150f), 1e-3f, "bridge crest");
            Assert.AreEqual(0f, QaPath.FloorY(120f), 1e-3f); Assert.AreEqual(0f, QaPath.FloorY(180f), 1e-3f);
            Assert.AreEqual(0.157f, 1.5f * Mathf.PI / 30f, 1e-3f, "bridge max slope");
            Assert.AreEqual(0.062f, (RingConstants.RampY0 - RingConstants.RampY1) / (RingConstants.RampZ1 - RingConstants.RampZ0), 1e-3f, "ramp slope");
            var e = QaPath.EyeAt(150f, out float yaw);
            Assert.AreEqual(KeyShots.All[2].Eye.y, e.y, 1e-3f); Assert.AreEqual(0f, yaw, 1e-3f);
            Vector3 prev = QaPath.EyeAt(0f, out float py);
            for (float s = 0.05f; s <= 960f; s += 0.05f)
            {
                var p = QaPath.EyeAt(s, out float y);
                Vector2 dxz = new Vector2(p.x - prev.x, p.z - prev.z);
                Assert.LessOrEqual(dxz.magnitude, 0.0506f, "eye continuity at s " + s);
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(py, y)), 2.5f, "heading continuity at s " + s);
                prev = p; py = y;
            }
        }

        [Test]
        public void QaPath_OffsetPathsStayInsideTheLane()
        {
            foreach (int side in new[] { -1, 1 })
            {
                var pts = QaPath.OffsetPath(side, 0.40f, 0f, 958.5f, 0.25f);
                float hint = 0f, maxS = 0f, worstDrop = 0f, worstOut = float.NegativeInfinity;
                string at = "";
                foreach (var p in pts)
                {
                    float d = QaPath.DistanceToPath(p, hint, 25f, out float s);
                    hint = s;
                    float hw = Mathf.Max(QaPath.LaneHalfWidth(Mathf.Max(0f, s - 0.5f)), Mathf.Max(QaPath.LaneHalfWidth(s), QaPath.LaneHalfWidth(Mathf.Min(960f, s + 0.5f))));
                    if (d - hw > worstOut) { worstOut = d - hw; at = "s " + s.ToString("0.00"); }
                    maxS = Mathf.Max(maxS, s);
                    worstDrop = Mathf.Max(worstDrop, maxS - s);
                }
                Assert.LessOrEqual(worstOut, 0.01f, "side " + side + ": offset path leaves the lane at " + at);
                Assert.LessOrEqual(worstDrop, 0.3f, "side " + side + ": offset path progress drops");
                Assert.GreaterOrEqual(maxS, 958f, "side " + side + ": offset path reaches the goal");
            }
        }
    }
}
