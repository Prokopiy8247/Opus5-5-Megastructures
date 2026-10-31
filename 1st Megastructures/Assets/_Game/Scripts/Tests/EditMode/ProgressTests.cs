using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Tests
{
    /// <summary>
    /// EditMode checks of the progress tables (14.2, 14.6, 14.10 "every fog-rate row of 14.6 within limits at the brisk speed"),
    /// the beat hysteresis, the title/ending timeline (14.11), the key-light ramps, instanced chunking and detail-tier
    /// hysteresis. Everything here is pure data or plain classes: no play mode, no camera, no assets are modified.
    /// </summary>
    public sealed class ProgressTests
    {
        const float Brisk = AbyssConst.V_BRISK;
        const float Walk = AbyssConst.V_WALK;
        const float SampleStep = 0.01f;   // metres of E per sample

        static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- structure

        [Test]
        public void KnotsAreSortedAndRowsValid()
        {
            AssertSorted("fog", ProgressTables.FogKnots);
            AssertSorted("exposure", ProgressTables.ExposureKnots);
            AssertSorted("fov", ProgressTables.FovKnots);
            AssertSorted("ambient", ProgressTables.AmbientKnots);
            FloatKnot[] b = ProgressTables.BloomIntensityKnots;
            for (int i = 1; i < b.Length; i++) Assert.LessOrEqual(b[i - 1].E, b[i].E, "bloom knots out of order at " + i);
            StepKnot[] k = ProgressTables.KeyKnots;
            for (int i = 1; i < k.Length; i++) Assert.Less(k[i - 1].E, k[i].E, "key knots out of order at " + i);
            StepKnot[] f = ProgressTables.FarKnots;
            for (int i = 1; i < f.Length; i++) Assert.Less(f[i - 1].E, f[i].E, "far knots out of order at " + i);
            Assert.AreEqual(8, ProgressTables.Rows.Length);
        }

        static void AssertSorted(string name, RowKnot[] k)
        {
            for (int i = 0; i < k.Length; i++)
            {
                Assert.That(k[i].Row >= 0 && k[i].Row < ProgressTables.Rows.Length, name + " knot " + i + " row out of range");
                if (i > 0) Assert.LessOrEqual(k[i - 1].E, k[i].E, name + " knots out of order at " + i);
            }
        }

        // ---------------------------------------------------------------- 14.6 rate limits

        [Test]
        public void FogSigmaMeanRateWithinLimitAtBriskSpeed()
        {
            // "fog sigma 15 % of the blend's total change per second (the mean rate at the brisk speed 2.5 m/s)"
            RowKnot[] k = ProgressTables.FogKnots;
            int rows = 0;
            for (int i = 0; i < k.Length - 1; i++)
            {
                float sa = AbyssPalette.D50ToSigma(ProgressTables.Rows[k[i].Row].D50);
                float sb = AbyssPalette.D50ToSigma(ProgressTables.Rows[k[i + 1].Row].D50);
                if (Mathf.Approximately(sa, sb)) continue;
                rows++;
                float mid = 0.5f * (k[i].E + k[i + 1].E);
                float seconds = (k[i + 1].E - k[i].E) / ProgressTables.ERate(mid, Brisk);
                float fractionPerSecond = 1f / seconds;
                Assert.LessOrEqual(fractionPerSecond, AbyssConst.FOG_RATE + 1e-6f,
                    "fog blend " + ProgressTables.Rows[k[i].Row].Name + " -> " + ProgressTables.Rows[k[i + 1].Row].Name
                    + " over E " + F(k[i].E) + "-" + F(k[i + 1].E) + " changes " + F(fractionPerSecond * 100f) + " %/s");

                // the evaluated curve really goes from one sigma to the other inside the window
                Assert.AreEqual(sa, ProgressTables.FogSigmaAt(k[i].E), sa * 1e-4f);
                Assert.AreEqual(sb, ProgressTables.FogSigmaAt(k[i + 1].E), sb * 1e-4f);
            }
            Assert.AreEqual(5, rows, "14.6 has five fog transition rows (1-2, 2-3, 3-4, 4-5, 5-6)");
        }

        [Test]
        public void ExposureRateWithinLimitEverywhere()
        {
            AssertExposureRate(Brisk);
            AssertExposureRate(Walk);
        }

        static void AssertExposureRate(float speed)
        {
            float end = ProgressTables.EEnd;
            float worst = 0f, worstE = 0f;
            float prev = ProgressTables.ExposureAt(0f);
            for (float e = SampleStep; e <= end; e += SampleStep)
            {
                float v = ProgressTables.ExposureAt(e);
                float dt = SampleStep / ProgressTables.ERate(e - 0.5f * SampleStep, speed);
                float rate = Mathf.Abs(v - prev) / dt;
                if (rate > worst) { worst = rate; worstE = e; }
                prev = v;
            }
            Assert.LessOrEqual(worst, AbyssConst.MAX_EXPOSURE_RATE,
                "exposure changes " + F(worst) + " EV/s at E " + F(worstE) + " (speed " + F(speed) + " m/s)");
        }

        [Test]
        public void ExposureRateWithinLimitDuringTheRide()
        {
            float worst = 0f;
            float prev = ProgressTables.ExposureAt(ProgressTables.ERide(0f));
            for (float s = 0.01f; s <= ProgressTables.RIDE_E_SPAN; s += 0.01f)
            {
                float v = ProgressTables.ExposureAt(ProgressTables.ERide(s));
                worst = Mathf.Max(worst, Mathf.Abs(v - prev) / 0.01f);
                prev = v;
            }
            Assert.LessOrEqual(worst, AbyssConst.MAX_EXPOSURE_RATE, "exposure during the ride " + F(worst) + " EV/s");
        }

        [Test]
        public void FovMeanRateAtMostOneDegreePerSecond()
        {
            RowKnot[] k = ProgressTables.FovKnots;
            for (int i = 0; i < k.Length - 1; i++)
            {
                float fa = ProgressTables.Rows[k[i].Row].Fov, fb = ProgressTables.Rows[k[i + 1].Row].Fov;
                if (fa == fb) continue;
                float mid = 0.5f * (k[i].E + k[i + 1].E);
                float seconds = (k[i + 1].E - k[i].E) / ProgressTables.ERate(mid, Brisk);
                float rate = Mathf.Abs(fb - fa) / seconds;
                Assert.LessOrEqual(rate, 1f + 1e-4f, "FOV " + fa + " -> " + fb + " over E " + F(k[i].E) + "-" + F(k[i + 1].E) + ": " + F(rate) + " deg/s");
            }
        }

        [Test]
        public void FovPeakStaysBelowTheSafetyLimiter()
        {
            float worst = 0f;
            float prev = ProgressTables.FovAt(0f);
            for (float e = SampleStep; e <= ProgressTables.EEnd; e += SampleStep)
            {
                float v = ProgressTables.FovAt(e);
                worst = Mathf.Max(worst, Mathf.Abs(v - prev) / (SampleStep / ProgressTables.ERate(e, Brisk)));
                prev = v;
            }
            Assert.LessOrEqual(worst, ProgressDirector.MAX_FOV_RATE, "peak FOV rate " + F(worst) + " deg/s would be clipped by the limiter");
        }

        [Test]
        public void Beat7FovOpensOverTheLastFifteenMetresOfTheTunnel()
        {
            Assert.AreEqual(62f, ProgressTables.FovAt(ProgressTables.EPost(ProgressTables.DAtZ7(15.01f))), 1e-3f);
            Assert.AreEqual(68f, ProgressTables.FovAt(ProgressTables.EPost(ProgressTables.DAtZ7(0f))), 1e-3f);
            Assert.AreEqual(68f, ProgressTables.FovAt(ProgressTables.EPost(ProgressTables.DAtZ7(-40f))), 1e-3f);
        }

        // ---------------------------------------------------------------- 14.2 table values

        [Test]
        public void ExteriorHorizonEqualsOutHoriz()
        {
            LookRow o = ProgressTables.Rows[ProgressTables.ROW_OUTSIDE];
            Assert.AreEqual(AbyssPalette.OUT_HORIZ, o.FogHoriz, "Exterior FOG_HORIZ must be OUT_HORIZ (horizon = sky colour)");
            Assert.AreEqual(AbyssPalette.OUT_ZENITH, o.FogUp);
            Assert.AreEqual(AbyssPalette.EXT_FOG_DOWN, o.FogDown);
            Assert.AreEqual(AbyssPalette.EXT_D50, o.D50);
            Assert.AreEqual(AbyssPalette.EXT_M_DOWN, o.MDown);
            Assert.AreEqual(AbyssPalette.OUT_HORIZ.linear.r, o.FogHorizLin.r, 1e-6f);

            LookRow t = ProgressTables.Rows[ProgressTables.ROW_TUNNEL];
            Assert.AreEqual(AbyssPalette.TUNNEL_FOG, t.FogDown);
            Assert.AreEqual(AbyssPalette.TUNNEL_FOG, t.FogHoriz);
            Assert.AreEqual(AbyssPalette.TUNNEL_FOG, t.FogUp);
            Assert.AreEqual(AbyssPalette.TUNNEL_D50, t.D50);

            Vector3 sun = ProgressTables.ExtSunDir;
            Assert.AreEqual(1f, sun.magnitude, 1e-5f);
            Assert.Greater(sun.y, 0f, "the beat-7 sun stands above the horizon");
            Assert.Less(sun.z, 0f, "travelling +Z, the sun is seen ahead of a player heading -Z");
        }

        [Test]
        public void NoFogColourDarkerThanTheMinimum()
        {
            foreach (LookRow r in ProgressTables.Rows)
            {
                Assert.GreaterOrEqual(r.FogDownLin.maxColorComponent, AbyssPalette.MIN_LINEAR, r.Name + " FOG_DOWN darker than linear 0.002");
                Assert.GreaterOrEqual(r.FogHorizLin.maxColorComponent, AbyssPalette.MIN_LINEAR, r.Name);
                Assert.GreaterOrEqual(r.FogUpLin.maxColorComponent, AbyssPalette.MIN_LINEAR, r.Name);
            }
        }

        [Test]
        public void BeatInteriorsMatchTheTable()
        {
            // E inside each beat, away from every window
            float[] e = { 20f, 150f, 260f, 370f, ProgressTables.ERide(60f), ProgressTables.EPost(600f) };
            int[] row = { 0, 1, 2, 3, 4, 5 };
            var s = new ProgressSample();
            for (int i = 0; i < e.Length; i++)
            {
                ProgressTables.Evaluate(e[i], ref s);
                LookRow r = ProgressTables.Rows[row[i]];
                Assert.AreEqual(0.8326f / r.D50, s.FogSigma, 1e-6f, r.Name + " sigma");
                Assert.AreEqual(r.MDown, s.FogDownMul, 1e-6f, r.Name + " m_down");
                Assert.AreEqual(r.EV, s.EV, 1e-6f, r.Name + " EV");
                Assert.AreEqual(r.Fov, s.Fov, 1e-4f, r.Name + " FOV");
                Assert.AreEqual(r.Contrast, s.Contrast, 1e-4f, r.Name + " contrast");
                Assert.AreEqual(r.BloomIntensity, s.BloomIntensity, 1e-6f, r.Name + " bloom");
                Assert.AreEqual(r.FogHorizLin.g * r.AmbientScale, s.AmbMid.g, 1e-6f, r.Name + " ambient");
            }
            // beat 7: tunnel (z 40) and outside (pier, z -50)
            ProgressTables.Evaluate(ProgressTables.EPost(ProgressTables.DAtZ7(40f)), ref s);
            Assert.AreEqual(0.9f, s.EV, 1e-6f, "tunnel EV");
            Assert.AreEqual(62f, s.Fov, 1e-4f, "tunnel FOV");
            ProgressTables.Evaluate(ProgressTables.EPost(ProgressTables.DAtZ7(-50f)), ref s);
            Assert.AreEqual(-0.5f, s.EV, 1e-6f, "outside EV");
            Assert.AreEqual(68f, s.Fov, 1e-4f, "outside FOV");
            Assert.AreEqual(0.40f, s.BloomIntensity, 1e-6f, "outside bloom");
            Assert.AreEqual(1f, s.SkyBlend, 1e-6f);
            Assert.AreEqual(1f, s.BeamFade, 1e-6f);
            Assert.AreEqual(30000f, s.FarClip);
        }

        [Test]
        public void GalleryToShaftExposureIsNearZeroAtTheDoorway()
        {
            // 14.7.6: exposure over d 52-62, fog over d 52-72 - the first view of the shaft is already near EV 0.0 (the beat-2
            // row, calibrated to -0.25 in look-dev so the KS2b deck meets 14.10 while the nadir keeps FOG_DOWN's luma 0.041)
            float ev2 = ProgressTables.Rows[ProgressTables.ROW_B2].EV;
            Assert.AreEqual(0.0f, ev2, 0.3f, "the beat-2 exposure stays near EV 0.0");
            Assert.AreEqual(ProgressTables.Rows[ProgressTables.ROW_B1].EV, ProgressTables.ExposureAt(50f), 1e-6f);
            Assert.AreEqual(ev2, ProgressTables.ExposureAt(62f), 1e-6f);
            Assert.Less(ProgressTables.ExposureAt(60f), ev2 + 0.05f);
        }

        [Test]
        public void MouthBloomPeaksAndSettles()
        {
            FloatKnot[] k = ProgressTables.BloomIntensityKnots;
            Assert.AreEqual(0.35f, ProgressTables.EvalFloat(k, ProgressTables.EPost(ProgressTables.DAtZ7(30f))), 1e-6f);
            Assert.AreEqual(0.60f, ProgressTables.EvalFloat(k, ProgressTables.EPost(ProgressTables.DAtZ7(3f))), 1e-6f);
            Assert.AreEqual(0.40f, ProgressTables.EvalFloat(k, ProgressTables.EPost(ProgressTables.DAtZ7(-8f))), 1e-6f);
            float max = 0f;
            for (float z = 25f; z > -20f; z -= 0.05f)
                max = Mathf.Max(max, ProgressTables.EvalFloat(k, ProgressTables.EPost(ProgressTables.DAtZ7(z))));
            Assert.AreEqual(0.60f, max, 1e-4f, "the peak is 0.60");
        }

        [Test]
        public void SkyBlendAndBeamFade()
        {
            Assert.AreEqual(0f, ProgressTables.EvalFloat(ProgressTables.SkyBlendKnots, ProgressTables.EPost(639.9f)));
            Assert.AreEqual(1f, ProgressTables.EvalFloat(ProgressTables.SkyBlendKnots, ProgressTables.EPost(668.54f)), 1e-6f);
            float prev = 0f;
            for (float d = 640f; d <= 669f; d += 0.1f)
            {
                float v = ProgressTables.EvalFloat(ProgressTables.SkyBlendKnots, ProgressTables.EPost(d));
                Assert.GreaterOrEqual(v, prev - 1e-6f, "sky blend is monotonic");
                prev = v;
            }
            Assert.AreEqual(0f, ProgressTables.EvalFloat(ProgressTables.BeamFadeKnots, AbyssConst.D_LIFT - 1f));
            Assert.AreEqual(1f, ProgressTables.EvalFloat(ProgressTables.BeamFadeKnots, ProgressTables.ERide(10f)), 1e-6f);
            Assert.AreEqual(1f, ProgressTables.EvalFloat(ProgressTables.BeamFadeKnots, ProgressTables.EPost(700f)), 1e-6f);
        }

        [Test]
        public void KeyLightIsOffInBeatsOneFourAndTheTunnel()
        {
            Assert.AreEqual(ProgressTables.KEY_OFF, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, 20f));
            Assert.AreEqual(ProgressTables.KEY_SUN2, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, 120f));
            Assert.AreEqual(ProgressTables.KEY_SUN3, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, 250f));
            Assert.AreEqual(ProgressTables.KEY_OFF, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, 370f));
            Assert.AreEqual(ProgressTables.KEY_SUN5, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, ProgressTables.ERide(50f)));
            Assert.AreEqual(ProgressTables.KEY_FAN6, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, ProgressTables.EPost(560f)));
            Assert.AreEqual(ProgressTables.KEY_OFF, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, ProgressTables.EPost(ProgressTables.DAtZ7(40f))));
            Assert.AreEqual(ProgressTables.KEY_EXT7, (int)ProgressTables.EvalStep(ProgressTables.KeyKnots, ProgressTables.EPost(ProgressTables.DAtZ7(-30f))));
        }

        [Test]
        public void KeyLightDipsToZeroBeforeChangingDirection()
        {
            var rig = new KeyLightRig(null, false);
            rig.Snap(ProgressTables.KEY_SUN5);
            Assert.AreEqual(ProgressTables.KeyRows[ProgressTables.KEY_SUN5].Intensity, rig.Level, 1e-6f);
            float dt = 1f / 60f;
            bool sawZero = false;
            for (int i = 0; i < 60 * 12; i++)
            {
                rig.Update(ProgressTables.KEY_FAN6, dt);
                if (rig.ActiveRow == ProgressTables.KEY_SUN5) Assert.GreaterOrEqual(rig.Level, 0f);
                if (rig.Level == 0f) sawZero = true;
                if (rig.ActiveRow == ProgressTables.KEY_FAN6) Assert.IsTrue(sawZero, "direction switched while the light was on");
            }
            Assert.AreEqual(ProgressTables.KEY_FAN6, rig.ActiveRow);
            Assert.AreEqual(ProgressTables.KeyRows[ProgressTables.KEY_FAN6].Intensity, rig.Level, 1e-5f);

            // intensity-only change (sun 2 -> sun 3) eases without a dip: the rig adopts the new row at once (same look) and eases
            // the intensity, and the per-row scatter / haze values ride the same ramp, so the scatter lobe never pops
            KeyRow k2 = ProgressTables.KeyRows[ProgressTables.KEY_SUN2], k3 = ProgressTables.KeyRows[ProgressTables.KEY_SUN3];
            float i2 = k2.Intensity, i3 = k3.Intensity;
            rig.Snap(ProgressTables.KEY_SUN2);
            Assert.AreEqual(k2.ScatterAmt, rig.ScatterAmount, 1e-6f);
            Assert.AreEqual(k2.HazeSky, rig.HazeSky, 1e-6f);
            float min = 10f, minFraction = 10f, prevScatter = rig.ScatterAmount, maxScatterStep = 0f;
            for (int i = 0; i < 60 * 4; i++)
            {
                rig.Update(ProgressTables.KEY_SUN3, dt);
                min = Mathf.Min(min, rig.Level);
                minFraction = Mathf.Min(minFraction, rig.Fraction);
                Assert.AreEqual(ProgressTables.KEY_SUN3, rig.ActiveRow, "the same-look row is adopted at once");
                maxScatterStep = Mathf.Max(maxScatterStep, Mathf.Abs(rig.ScatterAmount - prevScatter));
                prevScatter = rig.ScatterAmount;
            }
            Assert.GreaterOrEqual(min, Mathf.Min(i2, i3) - 1e-5f);
            Assert.AreEqual(i3, rig.Level, 1e-5f);
            Assert.AreEqual(ProgressTables.KEY_SUN3, rig.ActiveRow);
            Assert.GreaterOrEqual(minFraction, 1f - 1e-4f, "the key fraction stays 1 through an intensity-only ease");
            Assert.AreEqual(k3.ScatterAmt, rig.ScatterAmount, 1e-6f);
            Assert.AreEqual(k3.HazeSky, rig.HazeSky, 1e-6f);
            Assert.LessOrEqual(maxScatterStep, Mathf.Abs(k3.ScatterAmt - k2.ScatterAmt) * 4f * dt / ProgressTables.KEY_RAMP_SECONDS + 1e-6f,
                "the scatter amount eases with the ramp (no pop)");

            // the scatter lobe and the haze skylight fade with the light through a dip (fan 6 -> sun 5 changes the look)
            rig.Snap(ProgressTables.KEY_SUN5);
            rig.Update(ProgressTables.KEY_FAN6, 1.5f);
            Assert.AreEqual(ProgressTables.KEY_SUN5, rig.ActiveRow);
            Assert.AreEqual(ProgressTables.KeyRows[ProgressTables.KEY_SUN5].ScatterAmt * rig.Fraction, rig.ScatterAmount, 1e-6f);
            Assert.Less(rig.Fraction, 1f);
            rig.Dispose();
        }

        // ---------------------------------------------------------------- round 1: no pure black, shaft rhythm, sky (R1-03/06/14/15)

        static string ShaderInclude(string name) =>
            System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "_Game/Shaders/Include/" + name));

        static float ParseHlslDefine(string text, string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"#define\s+" + name + @"\s+([0-9.eE+-]+)");
            Assert.IsTrue(m.Success, "define not found: " + name);
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>CPU mirror of AbRadianceFloor (AbyssLighting.hlsl): c + f (1 - c / 2f)^2 below 2f, identity above.</summary>
        static float RadianceFloor(float c, float f)
        {
            float t = Mathf.Clamp01(1f - c / (2f * f));
            return c + f * t * t;
        }

        [Test]
        public void RadianceFloorKeepsEveryRowOffPureBlack()
        {
            // R1-14 backstop: every lit pixel ends at or above its domain's FOG_DOWN (14.2 "no pure black"), the curve is
            // monotonic and leaves lit tones alone; through each row's exposure and the Neutral toe (slope 1.06) the brightest
            // channel of an unlit face stays above 6/255 (the critics' near-black test) in every beat.
            string lighting = ShaderInclude("AbyssLighting.hlsl");
            Assert.IsTrue(lighting.Contains("return AbRadianceFloor(color, domain);"), "AbyssShadeLit ends in the radiance floor");
            Assert.IsTrue(lighting.Contains("c + f * (t * t)"), "mirrored floor curve");
            foreach (LookRow r in ProgressTables.Rows)
            {
                for (int ch = 0; ch < 3; ch++)
                {
                    float f = r.FogDownLin[ch];
                    Assert.AreEqual(f, RadianceFloor(0f, f), 1e-7f, r.Name + ": an unlit face settles on FOG_DOWN");
                    float prev = RadianceFloor(0f, f);
                    for (float c = 0.0005f * f; c <= 4f * f; c += 0.01f * f)
                    {
                        float v = RadianceFloor(c, f);
                        Assert.GreaterOrEqual(v, prev - 1e-9f, r.Name + ": monotonic");
                        Assert.GreaterOrEqual(v, c, r.Name + ": never darkens");
                        prev = v;
                    }
                    Assert.AreEqual(2.5f * f, RadianceFloor(2.5f * f, f), 1e-9f, r.Name + ": identity above 2 FOG_DOWN");
                }
                float maxCh = r.FogDownLin.maxColorComponent * Mathf.Pow(2f, Mathf.Min(r.EV, 0f)) * 1.06f;
                Assert.Greater(Mathf.LinearToGammaSpace(maxCh) * 255f, 6f, r.Name + ": the floor displays above 6/255");
            }
        }

        [Test]
        public void ExteriorGroundAndSkyGradient()
        {
            // R1-15: the domain-2 ground term is raised to AB_EXT_GROUND_MIN x the equator (the lit honeycomb below), the same
            // fraction as the "7 outside" row's domain-0 ground floor. R1-03: with the round-1 scatter amount the Exterior sky darkens from the horizon
            // toward the KS7b frame top (+22 degrees, sun azimuth 200 at elevation 40) and is brighter toward the sun side.
            string lighting = ShaderInclude("AbyssLighting.hlsl");
            Assert.AreEqual(ProgressTables.AmbGroundMin[ProgressTables.ROW_OUTSIDE], ParseHlslDefine(lighting, "AB_EXT_GROUND_MIN"), 1e-6f,
                "the Exterior ground bounce equals the outside row's ground floor");
            Assert.IsTrue(lighting.Contains("down = max(down, AB_EXT_GROUND_MIN * mid);"), "exterior ground bounce");

            string fog = ShaderInclude("AbyssFog.hlsl");
            float amt = ParseHlslDefine(fog, "AB_EXT_SCATTER_AMT");
            float cap = ParseHlslDefine(fog, "AB_EXT_LOBE_CAP");
            float g = ParseHlslDefine(fog, "AB_HG_G");
            Vector3 horiz = Lin(AbyssPalette.OUT_HORIZ), up = Lin(AbyssPalette.OUT_ZENITH), sc = Lin(AbyssPalette.Hex(0xD6E2EC));
            Vector3 sun = ProgressTables.ExtSunDir;
            float Luma(Vector3 c) => 0.2126f * c.x + 0.7152f * c.y + 0.0722f * c.z;
            Vector3 Sky(Vector3 v)
            {
                v.Normalize();
                Vector3 c = Vector3.Lerp(horiz, up, Smoothstep(0f, 0.8f, v.y));
                float d = Mathf.Max(1f + g * g - 2f * g * Vector3.Dot(v, sun), 1e-4f);
                float hg = (1f - g * g) / (4f * Mathf.PI * d * Mathf.Sqrt(d));
                return c + sc * (Mathf.Min(hg * Smoothstep(0.05f, 0.35f, v.y), cap) * amt);
            }
            float sinE = Mathf.Sin(22f * Mathf.Deg2Rad), cosE = Mathf.Cos(22f * Mathf.Deg2Rad);
            Vector3 top = new Vector3(0f, sinE, -cosE);                                       // KS7b frame top, yaw 180
            Vector3 topSun = new Vector3(-Mathf.Sin(20f * Mathf.Deg2Rad) * cosE, sinE, -Mathf.Cos(20f * Mathf.Deg2Rad) * cosE);
            Vector3 topAway = new Vector3(Mathf.Sin(40f * Mathf.Deg2Rad) * cosE, sinE, -Mathf.Cos(40f * Mathf.Deg2Rad) * cosE);
            float lh = Luma(horiz), lt = Luma(Sky(top));
            Assert.Less(lt, 0.85f * lh, "the sky at +22 degrees is visibly darker than the horizon (linear luma " + F(lt) + " vs " + F(lh) + ")");
            Assert.Greater(Luma(Sky(topSun)), Luma(Sky(topAway)) + 1e-3f, "brighter toward the sun azimuth");
            Assert.AreEqual(lh, Luma(Sky(new Vector3(0f, 0f, -1f))), 1e-6f, "the horizon row stays OUT_HORIZ");
        }

        static Vector3 Lin(Color c) { Color l = c.linear; return new Vector3(l.r, l.g, l.b); }

        [Test]
        public void AmbientGroundTermIsAtLeastHalfTheEquatorTerm()
        {
            // R1-14: FOG_DOWN x scale as the ground term left down-facing faces at about 1e-4 linear (black rib undersides, trays,
            // beam soffits); every row keeps its ground term at AmbGroundMin x the equator per channel (at least 0.2, and 0.5 in
            // the gallery, bridge and ledge rows where the critics measured the black bands), and so does every evaluated E.
            Assert.AreEqual(ProgressTables.Rows.Length, ProgressTables.AmbGroundMin.Length);
            float minK = 1f;
            for (int r = 0; r < ProgressTables.Rows.Length; r++)
            {
                LookRow row = ProgressTables.Rows[r];
                float k = ProgressTables.AmbGroundMin[r];
                minK = Mathf.Min(minK, k);
                Assert.That(k >= 0.2f && k <= 1f, row.Name + " ground floor fraction " + k);
                Color g = ProgressTables.AmbientGround(r);
                for (int c = 0; c < 3; c++)
                {
                    Assert.GreaterOrEqual(g[c], row.FogHorizLin[c] * row.AmbientScale * k - 1e-7f, row.Name + " ground " + c);
                    Assert.GreaterOrEqual(g[c], row.FogDownLin[c] * row.AmbientScale - 1e-7f, row.Name + " never below FOG_DOWN x scale");
                }
            }
            foreach (int r in new[] { ProgressTables.ROW_B1, ProgressTables.ROW_B2, ProgressTables.ROW_B3 })
                Assert.GreaterOrEqual(ProgressTables.AmbGroundMin[r], 0.5f, ProgressTables.Rows[r].Name);
            var s = new ProgressSample();
            for (float e = 0f; e <= ProgressTables.EEnd; e += 0.5f)
            {
                ProgressTables.Evaluate(e, ref s);
                for (int c = 0; c < 3; c++)
                    Assert.GreaterOrEqual(s.AmbDown[c], s.AmbMid[c] * minK - 1e-6f, "ground term at E " + F(e));
            }
        }

        static float Smoothstep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        [Test]
        public void ShaftSunLightsTheNearRingsButNotTheAbyss()
        {
            // R1-06 (KS2a rhythm) against 14.5.2 KS2b / 14.7.2: the shaft sun reaches the -30 belt top and the -90 rib top
            // (ledges below the deck read as lit lines), but every flat ring top and wall from -210 down (the side walls KS2b
            // sees, from about y -210) gets no direct sun from the main falloff, so the abyss keeps no lit floor. Only the
            // 45-degree top chamfers keep the slow second falloff (R2-06, RingChamfersStepDownTowardTheNadir).
            foreach (int r in new[] { ProgressTables.KEY_SUN2, ProgressTables.KEY_SUN3 })
            {
                KeyRow k = ProgressTables.KeyRows[r];
                float BeltTop(int level) => AbyssConst.RingY(level) + 0.5f * (level % 4 == 0 ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT);
                float Lit(float y) => Smoothstep(k.DepthZero, k.DepthFull, y);
                int belt = 20, rib = 21;   // y -30 (belt) and -90 (rib)
                Assert.AreEqual(-30f, AbyssConst.RingY(belt), 1e-3f);
                Assert.AreEqual(-90f, AbyssConst.RingY(rib), 1e-3f);
                Assert.Greater(Lit(BeltTop(belt)), 0.8f, k.Name + ": the -30 belt top is sunlit");
                Assert.Greater(Lit(BeltTop(rib)), 0.3f, k.Name + ": the -90 rib top is sunlit");
                for (int level = 23; level < AbyssConst.RIB_LEVELS; level++)   // y -210 and below
                    Assert.AreEqual(0f, Lit(BeltTop(level)), 1e-6f, k.Name + ": ring " + level + " (y " + AbyssConst.RingY(level) + ") must stay unlit");
                Assert.LessOrEqual(Lit(BeltTop(22)), 0.05f, k.Name + ": the -150 rib is at most grazed");
                Assert.Greater(k.HazeSky, 0f, k.Name + " has a haze skylight");
                Assert.Greater(k.ScatterAmt, 0.1f, k.Name + ": the haze glows toward the sun");
            }
            foreach (int r in new[] { ProgressTables.KEY_OFF, ProgressTables.KEY_SUN5, ProgressTables.KEY_FAN6, ProgressTables.KEY_EXT7 })
                Assert.AreEqual(0f, ProgressTables.KeyRows[r].HazeSky, ProgressTables.KeyRows[r].Name + " has no haze skylight");
            Assert.AreEqual(0.35f, ProgressTables.KeyRows[ProgressTables.KEY_FAN6].ScatterAmt, 1e-6f, "14.5.6 fan scatter 0.35");
        }

        [Test]
        public void RingChamfersStepDownTowardTheNadir()
        {
            // R2-06 (KS2b strips 'ribs fading to black either side') and R2-07 (2) (KS2a lower right): the ring top chamfers keep a
            // slow second falloff of the shaft sun, so 4 nested ring lines step down toward the nadir (the -150 rib on the near
            // flat, the -210 / -270 / -330 rings on the side flats; the shaft fog dims each deeper line), then the term falls to
            // 0 by -470; nothing else changes (rows without it are off).
            string lighting = ShaderInclude("AbyssLighting.hlsl");
            Assert.IsTrue(lighting.Contains("max(AbKeyDepthFactor(positionWS.y, domain), AbRingChamferFactor(n, positionWS, domain))"),
                "the chamfers take the larger of the two falloffs");
            Assert.IsTrue(lighting.Contains("_AbKeyChamfer.x * saturate((e - e0) / max(1.0 - e0, 1e-4))"), "mirrored chamfer curve");
            Assert.IsTrue(lighting.Contains("AbHazeSkylight(N, keyFactor, domain)"), "the haze skylight follows the same factor");
            foreach (int r in new[] { ProgressTables.KEY_SUN2, ProgressTables.KEY_SUN3 })
            {
                KeyRow k = ProgressTables.KeyRows[r];
                float Main(float y) => Smoothstep(k.DepthZero, k.DepthFull, y);
                float Line(int level)
                {
                    bool belt = AbyssConst.RingIsBelt(level);
                    float top = AbyssConst.RingY(level) + 0.5f * (belt ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT);
                    float y = top - 0.5f * Megastructure.Beats.Beat02Rings.TopChamfer(belt);   // chamfer centre
                    return Mathf.Max(Main(y), ProgressTables.ChamferFactor(k, y));
                }
                Assert.AreEqual(-150f, AbyssConst.RingY(22), 1e-3f);
                // calibrated against the KS2b strip limits (max 0.12, edge 0.03; global_r21 at 0.35 failed them) and the x264 CRF 20
                // encode (global_r23: at 0.06 of the Key the -330 line ended 1 level above the field): the four ring levels in
                // the KS2b strips keep the full amplitude, 0.15-0.25 of the Key, and the fog steps them down
                for (int level = 22; level <= 25; level++)
                {
                    float l = Line(level);
                    Assert.That(l >= 0.15f && l <= 0.25f, k.Name + ": ring " + level + " (y " + AbyssConst.RingY(level) + ") chamfer " + F(l));
                }
                Assert.Less(Line(26), 0.5f * Line(25), k.Name + ": the -390 rib only glimmers");
                Assert.Greater(Line(26), 0f, k.Name + ": the -390 rib is not cut off by a hard edge");
                Assert.Less(Line(27), 0.02f, k.Name + ": nothing left at -450");
                Assert.Less(ProgressTables.ChamferFactor(k, -450f), 0.02f, k.Name + ": nothing left at -450");
                float prev = Line(20);
                for (int level = 21; level < AbyssConst.RIB_LEVELS; level++)
                {
                    float l = Line(level);
                    Assert.LessOrEqual(l, prev + 1e-6f, k.Name + ": ring " + level + " is never brighter than the ring above");
                    prev = l;
                }
                for (float y = -150f; y >= -600f; y -= 5f)   // the curve itself is monotonic and continuous (no step in the wall)
                    Assert.LessOrEqual(Mathf.Abs(ProgressTables.ChamferFactor(k, y) - ProgressTables.ChamferFactor(k, y - 5f)), 0.02f, k.Name + ": smooth at y " + y);
                for (int level = 28; level < AbyssConst.RIB_LEVELS; level++)   // y -510 and below
                    Assert.AreEqual(0f, Line(level), 1e-6f, k.Name + ": ring " + level + " (y " + AbyssConst.RingY(level) + ") stays unlit");
                Assert.LessOrEqual(ProgressTables.ChamferFactor(k, 0f), k.ChamferAmp + 1e-6f, k.Name + ": capped above y0");
            }
            foreach (int r in new[] { ProgressTables.KEY_OFF, ProgressTables.KEY_SUN5, ProgressTables.KEY_FAN6, ProgressTables.KEY_EXT7 })
                Assert.AreEqual(Vector4.zero, ProgressTables.KeyRows[r].ChamferVector, ProgressTables.KeyRows[r].Name + " has no chamfer falloff");

            // the ring band holds the rib (hex radius 344-346) and belt (330-334) chamfers, not the wall (350) or a column (C1: 166)
            Vector4 band = ProgressTables.RingBand;
            Assert.AreEqual(AbyssConst.SHAFT_CENTRE.x, band.x); Assert.AreEqual(AbyssConst.SHAFT_CENTRE.y, band.y);
            foreach (bool belt in new[] { false, true })
            {
                float a = belt ? AbyssConst.BELT_INNER_APOTHEM : AbyssConst.RIB_INNER_APOTHEM;
                Assert.GreaterOrEqual(a, band.z, "chamfer inner edge inside the band");
                Assert.LessOrEqual(a + Megastructure.Beats.Beat02Rings.TopChamfer(belt), band.w, "chamfer outer edge inside the band");
            }
            Assert.Less(band.w + 2f, AbyssConst.SHAFT_APOTHEM + 1e-3f, "the shaft wall lies outside the band");
            foreach (Vector3 col in AbyssConst.COLUMNS)   // (x, z, radius); hex radius is 1-Lipschitz, so centre + radius bounds the column
                Assert.Less(MeshKit.HexApothemOf(AbyssConst.SHAFT_CENTRE, new Vector2(col.x, col.y)) + col.z + AbyssConst.COLUMN_BAND_PROTRUSION,
                    band.z - 2f, "column at (" + col.x + ", " + col.y + ") lies inside the band's inner edge");
        }

        // URP ColorAdjustments contrast (LutBuilderHdr, Neutral tonemapping): LogC (ARRI EI 800) around ACEScc mid grey.
        static float LinearToLogC(float x) => x > 0.011361f ? 0.244161f * Mathf.Log10(5.555556f * x + 0.047996f) + 0.386036f : 5.301883f * x + 0.092819f;
        static float LogCToLinear(float t) => t > 0.1530537f ? (Mathf.Pow(10f, (t - 0.386036f) / 0.244161f) - 0.047996f) / 5.555556f : (t - 0.092819f) / 5.301883f;
        static float UrpContrast(float v, float contrast) => Mathf.Max(0f, LogCToLinear((LinearToLogC(v) - 0.4135884f) * (1f + contrast / 100f) + 0.4135884f));

        [Test]
        public void ShaftRowsDoNotCrushTheAbyssToBlack()
        {
            // 14.7.2: the abyss reads as FOG_DOWN (#070B10, luma 0.041 +/- 0.008). URP's LogC contrast subtracts (c - 1) 0.0605
            // linear in the toe, so the 14.2 "+12" clamps every FOG_DOWN channel (max 0.0052 linear) to 0. The shaft rows keep
            // FOG_DOWN within 5 % through the grading (each channel).
            Assert.AreEqual(0f, UrpContrast(0.0052f, 12f), 1e-6f, "mirror: +12 crushes FOG_DOWN's blue channel");
            foreach (int r in new[] { ProgressTables.ROW_B2, ProgressTables.ROW_B3, ProgressTables.ROW_B5 })
            {
                LookRow row = ProgressTables.Rows[r];
                Color fd = row.FogDownLin;
                for (int c = 0; c < 3; c++)
                {
                    float v = fd[c];
                    Assert.AreEqual(v, UrpContrast(v, row.Contrast), 0.05f * v, row.Name + " FOG_DOWN channel " + c + " survives the contrast");
                }
            }
        }

        [Test]
        public void ShaftSunFadesWithDepthOnlyInTheShaftBeats()
        {
            // 8.5 / 14.7.2: the shaft sun (beats 2, 3, 5) carries a shaft-depth falloff (_AbKeyDepth); the fan light and the
            // beat-7 sun do not. Sun 2 -> sun 3 is an intensity-only change, so both rows share the same falloff.
            KeyRow[] k = ProgressTables.KeyRows;
            foreach (int r in new[] { ProgressTables.KEY_SUN2, ProgressTables.KEY_SUN3, ProgressTables.KEY_SUN5 })
            {
                Assert.IsTrue(k[r].DepthOn, k[r].Name + " has a depth falloff");
                Assert.Greater(k[r].DepthFull, k[r].DepthZero, k[r].Name + ": full light above zero light");
                Assert.LessOrEqual(k[r].DepthFull, AbyssConst.W[3].y, k[r].Name + ": the bridge deck stays fully lit");
                Vector4 v = k[r].DepthVector;
                Assert.AreEqual(1f, v.z, k[r].Name + " enabled");
                Assert.AreEqual(k[r].DepthFull, v.x); Assert.AreEqual(k[r].DepthZero, v.y);
            }
            foreach (int r in new[] { ProgressTables.KEY_OFF, ProgressTables.KEY_FAN6, ProgressTables.KEY_EXT7 })
                Assert.AreEqual(Vector4.zero, k[r].DepthVector, k[r].Name + " has no depth falloff");
            Assert.IsTrue(k[ProgressTables.KEY_SUN2].SameLook(k[ProgressTables.KEY_SUN3]), "sun 2 -> sun 3 changes only the intensity");
            KeyRow other = k[ProgressTables.KEY_SUN3];
            other.DepthZero -= 10f;
            Assert.IsFalse(k[ProgressTables.KEY_SUN3].SameLook(other), "a different falloff is a different look (dip to 0)");
            other = k[ProgressTables.KEY_SUN3];
            other.ChamferZero -= 10f;
            Assert.IsFalse(k[ProgressTables.KEY_SUN3].SameLook(other), "a different chamfer falloff is a different look (dip to 0)");
        }

        [Test]
        public void FarPlaneSchedule()
        {
            Assert.AreEqual(200f, ProgressTables.EvalStep(ProgressTables.FarKnots, 10f));
            Assert.AreEqual(4500f, ProgressTables.EvalStep(ProgressTables.FarKnots, 100f));
            Assert.AreEqual(600f, ProgressTables.EvalStep(ProgressTables.FarKnots, 350f));
            Assert.AreEqual(4500f, ProgressTables.EvalStep(ProgressTables.FarKnots, ProgressTables.ERide(60f)));
            Assert.AreEqual(30000f, ProgressTables.EvalStep(ProgressTables.FarKnots, ProgressTables.EPost(700f)));
            // fog hides the switch: at the 200 m and 600 m planes the domain-0 fog is at least 99.9 %
            Assert.Greater(Fog(ProgressTables.FogSigmaAt(39f), 200f), 0.999f);
            Assert.Greater(Fog(ProgressTables.FogSigmaAt(316f), 600f), 0.999f);
            Assert.Greater(Fog(ProgressTables.FogSigmaAt(419f), 600f), 0.999f);
        }

        static float Fog(float sigma, float dist) { float x = sigma * dist; return 1f - Mathf.Exp(-x * x); }

        // ---------------------------------------------------------------- beat hysteresis

        [Test]
        public void BeatHysteresisIsTwentyMetres()
        {
            Assert.AreEqual(1, BeatDirector.HysteresisBeat(1, 59.9f));
            Assert.AreEqual(2, BeatDirector.HysteresisBeat(1, 60f));
            Assert.AreEqual(2, BeatDirector.HysteresisBeat(2, 45f), "backing out 15 m keeps beat 2");
            Assert.AreEqual(1, BeatDirector.HysteresisBeat(2, 39.9f), "20 m back returns to beat 1");
            Assert.AreEqual(7, BeatDirector.HysteresisBeat(1, 700f), "a jump forward lands on the right beat");
            Assert.AreEqual(5, BeatDirector.HysteresisBeat(5, AbyssConst.D_LIFT), "the ride is beat 5");
            for (int b = 1; b <= 7; b++) Assert.AreEqual(b, BeatDirector.RawBeat(AbyssConst.BEAT_START_D[b - 1] + 0.01f));
        }

        // ---------------------------------------------------------------- title card and ending (14.11)

        [Test]
        public void TitleTimeline()
        {
            Assert.AreEqual(0f, TitleCard.PictureFade(0.0));
            Assert.AreEqual(0f, TitleCard.PictureFade(0.8));
            Assert.AreEqual(1f, TitleCard.PictureFade(3.3), 1e-6f);
            Assert.AreEqual(0f, TitleCard.TitleAlpha(0.99));
            Assert.AreEqual(1f, TitleCard.TitleAlpha(2.0), 1e-6f);
            Assert.AreEqual(1f, TitleCard.TitleAlpha(4.9), 1e-6f);
            Assert.AreEqual(0f, TitleCard.TitleAlpha(6.5), 1e-6f);
            Assert.AreEqual(0f, TitleCard.HintAlpha(6.9));
            Assert.AreEqual(1f, TitleCard.HintAlpha(10.0), 1e-6f);
            Assert.AreEqual(0f, TitleCard.HintAlpha(13.0), 1e-6f);
            Assert.AreEqual(0f, TitleCard.HintAlpha(10.0, 3.0), "moved before 7 s: the hint never shows");
            Assert.AreEqual(0f, TitleCard.HintAlpha(10.5, 10.0), 1e-6f, "the hint is gone 0.4 s after the first movement");
            Assert.AreEqual(1f, TitleCard.EndingPicture(1.0), 1e-6f);
            Assert.AreEqual(0f, TitleCard.EndingPicture(5.0), 1e-6f);
            Assert.AreEqual(0f, TitleCard.EndingLine(6.0), 1e-6f);
            Assert.AreEqual(1f, TitleCard.EndingLine(8.0), 1e-6f);
            Assert.AreEqual(1f, TitleCard.EndingLine(13.9), 1e-6f);
            Assert.AreEqual(0f, TitleCard.EndingLine(TitleCard.EndingDuration), 1e-6f);
            Assert.AreEqual("Shaft 1 of 4,920.", TitleCard.ENDING_LINE);
            // 14.11 "in the same typography" (R2-34): the ending line uses the title's size and tracking, at 70 % opacity
            Assert.AreEqual(TitleCard.TITLE_SIZE, TitleCard.END_SIZE, 1e-6f, "ending line size = title size");
            Assert.GreaterOrEqual(TitleCard.END_SIZE, 0.04f, "ending line at least 4 % of the screen height");
            Assert.AreEqual(0.70f, TitleCard.END_OPACITY, 1e-6f);
        }

        // ---------------------------------------------------------------- instancing and detail tiers

        [Test]
        public void InstancedBatchSplitsChunksAndCalls()
        {
            var mesh = new Mesh { name = "TestCube" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            var mat = new Material(Shader.Find("Hidden/InternalErrorShader")) { enableInstancing = true };
            try
            {
                var b = new InstancedBatch("test", mesh, mat, ShadowCastingMode.On, true, 0, 200f);
                for (int i = 0; i < 1200; i++) b.Add(Matrix4x4.Translate(new Vector3(i * 0.1f, 0f, 10f)));      // one chunk
                for (int i = 0; i < 10; i++) b.Add(Matrix4x4.Translate(new Vector3(5000f + i, 0f, 10f)));     // a far chunk
                b.Seal();
                Assert.AreEqual(1210, b.Count);
                Assert.AreEqual(4, b.DrawCallCount, "1200 -> 3 calls of at most 511, plus one for the far chunk");
                for (int i = 0; i < b.DrawCallCount; i++)
                {
                    Bounds db = b.GetDrawBounds(i);
                    Assert.LessOrEqual(db.size.x, 260f, "a draw never spans more than one chunk (plus 10 %)");
                }
                Assert.IsTrue(b.WorldBounds.Contains(new Vector3(5009f, 0f, 10f)));
                b.Fade = 0.5f;
                b.Fade = 1f;
            }
            finally
            {
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void DetailTierCoverageHasHysteresis()
        {
            var tier = new DetailTier("test", new Bounds(Vector3.zero, Vector3.zero), 150f, 20f, 20f);
            tier.Update(new Vector3(100f, 0f, 0f));
            Assert.AreEqual(1f, tier.Coverage, 1e-6f);
            tier.Update(new Vector3(140f, 0f, 0f));               // receding inside the fade band 130-150
            float c140 = tier.Coverage;
            Assert.That(c140 > 0f && c140 < 1f);
            tier.Update(new Vector3(160f, 0f, 0f));
            Assert.AreEqual(0f, tier.Coverage, 1e-6f);
            tier.Update(new Vector3(140f, 0f, 0f));               // approaching: stays far until the 110-130 band
            Assert.AreEqual(0f, tier.Coverage, 1e-6f);
            tier.Update(new Vector3(120f, 0f, 0f));
            Assert.AreEqual(0.5f, tier.Coverage, 1e-4f);
            tier.Update(new Vector3(125f, 0f, 0f));               // hover backwards a little: no change (inside the band)
            Assert.AreEqual(0.5f, tier.Coverage, 1e-4f);
            tier.Update(new Vector3(100f, 0f, 0f));
            Assert.AreEqual(1f, tier.Coverage, 1e-6f);
            tier.Update(new Vector3(5000f, 0f, 0f));              // teleport far
            Assert.AreEqual(0f, tier.Coverage, 1e-6f);
        }
    }
}
