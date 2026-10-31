// SkyTests: EditMode tests of the double mirrors of the analytic sky (SkyMath) against MASTER_PROMPT 14.5.6,
// 14.5.7, 14.7.3, 14.7.5, 14.7.7 and the framing acceptances of 14.10.2 (T-THREAD, T-CURV, KF1, KF6a, KF7a, KF7b).
using System;
using Megastructure.Rendering;
using NUnit.Framework;

namespace Megastructure.Tests
{
    public class SkyTests
    {
        // 14.7.3 table (printed values are for orientation; the formula is the reference of T-THREAD)
        static readonly double[] TableS = { 0, 45, 90, 150, 200, 240, 265, 285 };
        static readonly double[] TableThetaDeg = { 0.124, 0.155, 0.201, 0.314, 0.524, 0.952, 1.651, 3.208 };
        static readonly double[] TablePx1080 = { 2.0, 2.5, 3.3, 5.1, 8.9, 17.1, 31.6, 66.4 };
        static readonly double[] TablePx2160 = { 4.0, 5.0, 6.5, 10.2, 17.8, 34.2, 63.2, 132.7 };

        const double Aspect169 = 16.0 / 9.0, Aspect1610 = 16.0 / 10.0, Aspect219 = 2560.0 / 1080.0;

        /// <summary>T-THREAD tolerance: 0.3 px below 10 px, 3 percent above.</summary>
        static double WidthTol(double px) => px < 10.0 ? 0.3 : 0.03 * px;

        static KeyShot Shot(string tag)
        {
            foreach (var k in ThreadConstants.KeyShots) if (k.tag == tag) return k;
            Assert.Fail("key shot " + tag + " missing from ThreadConstants.KeyShots");
            return default;
        }

        // ------------------------------------------------------------------ independent reference projection
        // Closed-form spherical trigonometry, deliberately not SkyMath.CameraBasis / QaProjection.Basis, so the expectations
        // for the decided key-shot poses (ThreadConstants.KeyShots) are derived from the pose and not copied from a run.

        /// <summary>Normalised screen position (half-extents, y up) of the direction (azimuth, elevation) for a camera
        /// world yaw / pitch (no roll), vertical FOV and aspect (Hor+). False when the direction is behind the camera.</summary>
        internal static bool PoseNdc(double yawDeg, double pitchDeg, double fovDeg, double aspect, double azDeg, double elDeg, out double x, out double y)
        {
            double a = (azDeg - yawDeg) * ThreadMath.Deg, e = elDeg * ThreadMath.Deg, p = pitchDeg * ThreadMath.Deg;
            double cx = Math.Cos(e) * Math.Sin(a);
            double cy = Math.Cos(p) * Math.Sin(e) - Math.Sin(p) * Math.Cos(e) * Math.Cos(a);
            double cz = Math.Cos(p) * Math.Cos(e) * Math.Cos(a) + Math.Sin(p) * Math.Sin(e);
            double t = Math.Tan(0.5 * fovDeg * ThreadMath.Deg);
            x = y = double.NaN;
            if (cz <= 1e-12) return false;
            x = cx / cz / (t * aspect);
            y = cy / cz / t;
            return true;
        }

        /// <summary>Screen box (half-extents) of a disc at infinity of angular radius rDeg around (azimuth, elevation), from
        /// n rim points placed with the great-circle destination formula (independent of the disc frames of SkyMath / QaProjection).</summary>
        internal static bool DiscBoxNdc(double yawDeg, double pitchDeg, double fovDeg, double aspect, double azDeg, double elDeg, double rDeg, int n,
                                        out double x0, out double x1, out double y0, out double y1)
        {
            x0 = y0 = double.MaxValue; x1 = y1 = double.MinValue;
            double e0 = elDeg * ThreadMath.Deg, rho = rDeg * ThreadMath.Deg;
            for (int i = 0; i < n; i++)
            {
                double phi = 2.0 * Math.PI * i / n;   // bearing on the sphere, 0 = toward the zenith
                double e = Math.Asin(Math.Sin(e0) * Math.Cos(rho) + Math.Cos(e0) * Math.Sin(rho) * Math.Cos(phi));
                double da = Math.Atan2(Math.Sin(phi) * Math.Sin(rho) * Math.Cos(e0), Math.Cos(rho) - Math.Sin(e0) * Math.Sin(e));
                if (!PoseNdc(yawDeg, pitchDeg, fovDeg, aspect, azDeg + da / ThreadMath.Deg, e / ThreadMath.Deg, out double x, out double y)) return false;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            return true;
        }

        // ------------------------------------------------------------------ far thread (14.7.3)
        [Test]
        public void ThreadTheta_MatchesTable()
        {
            for (int i = 0; i < TableS.Length; i++)
            {
                double th = ThreadMath.ThreadThetaRad(TableS[i]) / ThreadMath.Deg;
                Assert.That(th, Is.EqualTo(TableThetaDeg[i]).Within(0.03 * TableThetaDeg[i]), "theta at s = " + TableS[i]);
            }
        }

        [Test]
        public void ThreadReferenceWidth_MatchesTable_1080_and_4K()
        {
            // 14.7.3 (lines 814-823) prints the widths for the TRUE cable bearing; D-026 offsets the apparent bearing by
            // 8.6 deg x (1 - smoothstep(90, 240, s)) with theta(s) unchanged, so the shipped reference width is checked against
            // the formula re-evaluated at the decided bearing (e.g. 1.95 px instead of 2.0 at s = 0, 9.78 instead of 10.2 at 4K, s = 150)
            for (int i = 0; i < TableS.Length; i++)
            {
                double s = TableS[i], theta = ThreadMath.ThreadThetaRad(s), fov = ThreadMath.Fov(s);
                // the printed table is the formula at the true bearing (spec reference geometry)
                double t1 = ThreadMath.PixelWidth(theta, CoreTests.TrueThreadBearingRad(s), fov, 1080);
                double t4 = ThreadMath.PixelWidth(theta, CoreTests.TrueThreadBearingRad(s), fov, 2160);
                Assert.That(t1, Is.EqualTo(TablePx1080[i]).Within(WidthTol(TablePx1080[i])), "printed 1080p width (true bearing) at s = " + s);
                Assert.That(t4, Is.EqualTo(TablePx2160[i]).Within(WidthTol(TablePx2160[i])), "printed 4K width (true bearing) at s = " + s);
                // the shipped width at the D-026 bearing, same T-THREAD rule
                double e1 = ThreadMath.PixelWidth(theta, CoreTests.DecidedThreadBearingRad(s), fov, 1080);
                double e4 = ThreadMath.PixelWidth(theta, CoreTests.DecidedThreadBearingRad(s), fov, 2160);
                double w1 = SkyMath.ThreadReferenceWidthPx(s, 1080);
                double w4 = SkyMath.ThreadReferenceWidthPx(s, 2160);
                Assert.That(w1, Is.EqualTo(e1).Within(WidthTol(e1)), "1080p width at s = " + s + " (D-026 bearing)");
                Assert.That(w4, Is.EqualTo(e4).Within(WidthTol(e4)), "4K width at s = " + s + " (D-026 bearing)");
                // never below the minimum line width (14.7.2: 1.75 px at 1080p, scaled by H / 1080)
                Assert.GreaterOrEqual(w1, SkyMath.MinLineWidthPx(1080), "1080p width at s = " + s + " vs the minimum width");
                Assert.GreaterOrEqual(w4, SkyMath.MinLineWidthPx(2160), "4K width at s = " + s + " vs the minimum width");
            }
            Assert.GreaterOrEqual(SkyMath.ThreadReferenceWidthPx(0, 1080), ThreadConstants.ThreadMinWidthPx, "first-frame width at 1080p >= 1.75 px");
        }

        [Test]
        public void ThreadShaderWidth_MatchesRectilinearFormula()
        {
            // the sky shader's |grad e| width on the reference geometry (nominal path, yaw 0, pitch 0, FOV(s), horizon row)
            foreach (double H in new[] { 1080.0, 2160.0 })
            {
                for (int i = 0; i < TableS.Length; i++)
                {
                    double s = TableS[i];
                    var cam = new D3(0, ThreadConstants.EyeHeight, s);
                    var p = SkyMath.ThreadShaderProfile(cam, 0, 0, ThreadMath.Fov(s), Aspect169, H, s, 0.0);
                    Assert.IsTrue(p.found, "axis not found at s = " + s);
                    double reference = SkyMath.ThreadReferenceWidthPx(s, H);
                    Assert.That(p.trueWidthPx, Is.EqualTo(reference).Within(WidthTol(reference)), "shader width at s = " + s + ", H = " + H);
                    // the minimum-width clamp is inactive in beats 1-3 at the reference pose (14.7.2)
                    Assert.That(p.alpha, Is.EqualTo(1.0), "energy rule must be inactive at s = " + s);
                    Assert.That(p.shownWidthPx, Is.EqualTo(p.trueWidthPx).Within(1e-9));
                    // the axis sits at the apparent bearing beta(s)
                    double ty = Math.Tan(0.5 * ThreadMath.Fov(s) * ThreadMath.Deg);
                    double xExpected = Math.Tan(ThreadMath.ThreadBearingRad(s)) / (ty * Aspect169);
                    Assert.That(p.centreX, Is.EqualTo(xExpected).Within(1e-4), "axis position at s = " + s);
                }
            }
        }

        [Test]
        public void ThreadPoseWidth_And_LateralParallax()
        {
            for (int i = 0; i < TableS.Length; i++)
            {
                double s = TableS[i];
                var cam = new D3(0, ThreadConstants.EyeHeight, s);
                // the pose-based rectilinear width equals the table formula on the reference geometry
                double w = SkyMath.ThreadRectilinearWidthPx(cam, 0, ThreadMath.Fov(s), 1080, s);
                double reference = SkyMath.ThreadReferenceWidthPx(s, 1080);
                Assert.That(w, Is.EqualTo(reference).Within(WidthTol(reference)), "pose width at s = " + s);
                // the apparent bearing from the nominal path is beta(s)
                Assert.That(SkyMath.ThreadBearingFromCameraDeg(cam, s), Is.EqualTo(ThreadMath.ThreadBearingRad(s) / ThreadMath.Deg).Within(1e-9));
                // T-BACKDROP: a 10 m move across the line of sight shifts the bearing by 10 / r_app (+- 10 percent)
                double r = ThreadMath.ThreadApparentRange(s);
                double b = ThreadMath.ThreadBearingRad(s);
                var moved = new D3(10.0 * Math.Cos(b), cam.y, s - 10.0 * Math.Sin(b));
                double shift = (SkyMath.ThreadBearingFromCameraDeg(cam, s) - SkyMath.ThreadBearingFromCameraDeg(moved, s)) * ThreadMath.Deg;
                Assert.That(shift, Is.EqualTo(10.0 / r).Within(0.1 * 10.0 / r), "parallax at s = " + s);
            }
            SkyMath.ThreadApparentAxis(0, out double x0, out double z0);
            Assert.That(Math.Sqrt(x0 * x0 + z0 * z0) / 1000.0, Is.EqualTo(46.2).Within(0.1), "r_app(0)");
        }

        [Test]
        public void ThreadMinimumWidth_EnergyRule()
        {
            // at 720p the true 2.0 px x 720 / 1080 = 1.34 px is below the 1.5 px floor: shown 1.5 px, alpha = true / min
            var cam = new D3(0, ThreadConstants.EyeHeight, 0);
            var p = SkyMath.ThreadShaderProfile(cam, 0, 0, ThreadMath.Fov(0), Aspect169, 720, 0, 0.0);
            Assert.IsTrue(p.found);
            double minW = SkyMath.MinLineWidthPx(720);
            Assert.That(minW, Is.EqualTo(ThreadConstants.ThreadMinWidthFloorPx).Within(1e-12));
            Assert.Less(p.trueWidthPx, minW);
            Assert.That(p.shownWidthPx, Is.EqualTo(minW).Within(1e-9));
            Assert.That(p.alpha, Is.EqualTo(Math.Max(p.trueWidthPx / minW, ThreadConstants.ThreadAlphaFloor)).Within(1e-9));
            Assert.GreaterOrEqual(p.alpha, ThreadConstants.ThreadAlphaFloor);
        }

        [Test]
        public void KF1_ThreadAndSunFraming()
        {
            var k = Shot("beat01_salt_KF1");
            var cam = new D3(k.x, k.y, k.z);
            // 14.5.1 KF1 (line 649) puts the thread at bearing 9.9 deg, x about 1 115 px of 1920; D-026 offsets the apparent
            // bearing by 8.6 deg in beat 1 (the robes of pairs 3-4 hide 9.9 deg), so the column is derived from the decided
            // bearing: on the centre row (sy = 0) the ray of world azimuth a has tan(a - yaw) = sx tan(FOV/2) aspect / cos(pitch),
            // about 980 px. Width 1.75 to 2.6 px over the visible column (acceptance 1)
            var c = SkyMath.ThreadShaderProfile(cam, k.yaw, k.pitch, k.fov, Aspect169, 1080, k.s, 0.0);
            Assert.IsTrue(c.found);
            ThreadMath.ThreadAxis(k.s, out double xT, out double zT);
            double rel = Math.Atan2(xT - cam.x, zT - cam.z) - k.yaw * ThreadMath.Deg;
            double sxThread = Math.Tan(rel) * Math.Cos(k.pitch * ThreadMath.Deg) / (Math.Tan(0.5 * k.fov * ThreadMath.Deg) * Aspect169);
            Assert.That((c.centreX + 1.0) * 0.5 * 1920.0, Is.EqualTo((sxThread + 1.0) * 0.5 * 1920.0).Within(6.0), "thread column x (D-026 bearing)");
            foreach (double row in new[] { 0.1, 0.4, 0.7, 0.95 })
            {
                var p = SkyMath.ThreadShaderProfile(cam, k.yaw, k.pitch, k.fov, Aspect169, 1080, k.s, row);
                Assert.IsTrue(p.found, "row " + row);
                Assert.That(p.shownWidthPx, Is.InRange(1.75, 2.6), "thread width at row " + row);
            }
            // sun disc at about (-0.60, +0.49) of the half-extents and inside the frame at 16:9, 16:10 and 21:9
            D3 sun = ThreadMath.SunDirection(k.s);
            Assert.IsTrue(SkyMath.ProjectDirection(cam, k.yaw, k.pitch, k.fov, Aspect169, sun, out double sx, out double sy));
            Assert.That(sx, Is.EqualTo(-0.60).Within(0.03));
            Assert.That(sy, Is.EqualTo(0.49).Within(0.03));
            foreach (double asp in new[] { Aspect169, Aspect1610, Aspect219 })
            {
                Assert.IsTrue(SkyMath.ProjectDirection(cam, k.yaw, k.pitch, k.fov, asp, sun, out double ax, out double ay));
                Assert.That(Math.Abs(ax), Is.LessThan(0.97), "sun x at aspect " + asp);
                Assert.That(Math.Abs(ay), Is.LessThan(0.97), "sun y at aspect " + asp);
            }
        }

        // ------------------------------------------------------------------ planet (14.5.7)
        [Test]
        public void Planet_CentreAndAngularSize()
        {
            SkyMath.AzElFromDirection(SkyMath.PlanetDirection(), out double az, out double el);
            Assert.That(az, Is.EqualTo(-32.0).Within(0.5));
            Assert.That(el, Is.EqualTo(12.0).Within(0.5));
            // diameter 28 +- 0.5 deg between opposite rim points in several orientations
            for (int i = 0; i < 8; i++)
            {
                double th = Math.PI * i / 8.0;
                double dia = SkyMath.AngleBetweenDeg(SkyMath.PlanetRimDirection(th), SkyMath.PlanetRimDirection(th + Math.PI));
                Assert.That(dia, Is.EqualTo(28.0).Within(0.5), "diameter at frame angle " + th);
            }
            // lower rim 2 degrees below the mathematical horizon
            SkyMath.AzElFromDirection(SkyMath.PlanetRimDirection(-0.5 * Math.PI), out _, out double lowEl);
            Assert.That(lowEl, Is.EqualTo(-2.0).Within(0.1));
            // the key light comes from the upper right front of the disc (about 75 percent phase), never the sun
            D3 lp = SkyMath.PlanetLightDirection();
            SkyMath.PlanetFrame(out D3 c, out D3 r, out D3 u);
            Assert.Greater(D3.Dot(lp, r), 0.0);
            Assert.Greater(D3.Dot(lp, u), 0.0);
            Assert.Less(D3.Dot(lp, c), 0.0);   // toward the viewer
            Assert.That(D3.Dot(lp, lp), Is.EqualTo(1.0).Within(1e-12));
        }

        /// <summary>Screen box (half-extents) of a screen-space circular impostor (A4, B4, C-16), computed independently of SkyMath:
        /// centre = PoseNdc of the centre direction, radius tan(r) / tan(FOV / 2) of the half-height (a circle in pixels).</summary>
        internal static bool ImpostorBoxNdc(double yawDeg, double pitchDeg, double fovDeg, double aspect, double azDeg, double elDeg, double rDeg,
                                            out double x0, out double x1, out double y0, out double y1)
        {
            x0 = x1 = y0 = y1 = double.NaN;
            if (!PoseNdc(yawDeg, pitchDeg, fovDeg, aspect, azDeg, elDeg, out double cx, out double cy)) return false;
            double ry = Math.Tan(rDeg * ThreadMath.Deg) / Math.Tan(0.5 * fovDeg * ThreadMath.Deg), rx = ry / aspect;
            x0 = cx - rx; x1 = cx + rx; y0 = cy - ry; y1 = cy + ry;
            return true;
        }

        [Test]
        public void KF7b_PlanetBoxInsideFrame()
        {
            var k = Shot("beat07_top_KF7b");
            Assert.That(ThreadMath.Fov(k.s), Is.EqualTo(k.fov).Within(0.5), "KF7b is an in-game FOV shot");
            var origin = new D3(0, 0, 0);
            // The planet is a screen-space circular impostor (A4, B4, C-16): centre = rectilinear projection of the centre
            // direction, radius f tan(14 deg), a circle in pixels. 14.5.7's box (x -0.91..-0.28, y -0.04..+0.94 for yaw 0,
            // pitch -1) was the stretched true projection; every expectation here is derived from the actual key-shot pose.
            double pa = ThreadConstants.PlanetAzimuthDeg, pe = ThreadConstants.PlanetElevationDeg, pr = ThreadConstants.PlanetRadiusDeg;
            Assert.IsTrue(PoseNdc(k.yaw, k.pitch, k.fov, Aspect169, pa, pe, out double ecx, out double ecy));
            Assert.IsTrue(SkyMath.ProjectDirection(origin, k.yaw, k.pitch, k.fov, Aspect169, SkyMath.PlanetDirection(), out double cx, out double cy));
            Assert.That(cx, Is.EqualTo(ecx).Within(0.02), "planet centre x");
            Assert.That(cy, Is.EqualTo(ecy).Within(0.02), "planet centre y");

            // 16:9: impostor box against the independent circle; inside with a 3 percent margin
            Assert.IsTrue(SkyMath.PlanetScreenBox(k.yaw, k.pitch, k.fov, Aspect169, out double x0, out double x1, out double y0, out double y1));
            Assert.IsTrue(ImpostorBoxNdc(k.yaw, k.pitch, k.fov, Aspect169, pa, pe, pr, out double ex0, out double ex1, out double ey0, out double ey1));
            Assert.That(x0, Is.EqualTo(ex0).Within(1e-9), "16:9 box left");
            Assert.That(x1, Is.EqualTo(ex1).Within(1e-9), "16:9 box right");
            Assert.That(y0, Is.EqualTo(ey0).Within(1e-9), "16:9 box bottom");
            Assert.That(y1, Is.EqualTo(ey1).Within(1e-9), "16:9 box top");
            // a circle in pixels: width = height = 2 f tan(14 deg) (about 415 px at 1080p, FOV 66)
            Assert.That((x1 - x0) * 0.5 * 1920.0, Is.EqualTo((y1 - y0) * 0.5 * 1080.0).Within(1e-6), "impostor is round in pixels");
            Assert.That((y1 - y0) * 0.5 * 1080.0, Is.EqualTo(2.0 * SkyMath.ImpostorRadiusPx(pr, k.fov, 1080)).Within(1e-6));
            CoreTests.Log(FormattableString.Invariant($"KF7b planet impostor 16:9: centre ({cx:F3}, {cy:F3}), box x {x0:F3}..{x1:F3}, y {y0:F3}..{y1:F3}"));
            AssertInsideWithMargin(x0, x1, y0, y1, FrameMargin, "16:9");

            // 21:9 (2560 x 1080, Hor+): inside with the same margin
            Assert.IsTrue(SkyMath.PlanetScreenBox(k.yaw, k.pitch, k.fov, Aspect219, out x0, out x1, out y0, out y1));
            AssertInsideWithMargin(x0, x1, y0, y1, FrameMargin, "21:9");

            // 16:10 (1680 x 1050): at most 2 percent of its width cropped, and fully inside vertically
            Assert.IsTrue(SkyMath.PlanetScreenBox(k.yaw, k.pitch, k.fov, Aspect1610, out x0, out x1, out y0, out y1));
            Assert.That(SkyMath.CropFraction(x0, x1), Is.LessThanOrEqualTo(0.02), "16:10 horizontal crop");
            Assert.That(SkyMath.CropFraction(y0, y1), Is.EqualTo(0.0), "16:10 vertical crop");
        }

        /// <summary>Framing margin of 11.2: 3 percent of the frame on every side (QaProjection.FrameMargin).</summary>
        const double FrameMargin = 0.03;

        /// <summary>Box (half-extents) inside the frame with a margin given as a fraction of the frame: 3 percent of the frame
        /// is 0.06 of the half-extent (32.4 px of 1080, as QaSubject.BBoxInsideMargin; 14.5.7 prints the top edge at +0.94).</summary>
        static void AssertInsideWithMargin(double x0, double x1, double y0, double y1, double margin, string label)
        {
            double lim = 1.0 - 2.0 * margin;
            Assert.GreaterOrEqual(x0, -lim, label + " left");
            Assert.LessOrEqual(x1, lim, label + " right");
            Assert.GreaterOrEqual(y0, -lim, label + " bottom");
            Assert.LessOrEqual(y1, lim, label + " top");
        }

        [Test]
        public void KF7a_PlanetAboveAxis_LimbBelowAxis()
        {
            var k = Shot("beat07_top_KF7a");
            double A = ThreadMath.VirtualAltitude(k.s);
            Assert.That(A / 1000.0, Is.EqualTo(487.0).Within(1.0), "A at s = 800");
            var origin = new D3(0, 0, 0);
            Assert.IsTrue(SkyMath.ProjectDirection(origin, k.yaw, k.pitch, k.fov, Aspect169, SkyMath.PlanetDirection(), out _, out double py));
            Assert.That(py, Is.EqualTo(0.46).Within(0.02), "planet centre y");
            Assert.IsTrue(SkyMath.EarthLimbScreen(k.yaw, k.pitch, k.fov, Aspect169, A, k.yaw, out _, out double ly));
            Assert.That(ly, Is.EqualTo(-0.51).Within(0.02), "Earth limb y on the view azimuth");
            Assert.That(SkyMath.HorizonDipDeg(A) + k.pitch, Is.EqualTo(17.7).Within(0.1), "limb 17.7 deg below the axis");
        }

        // ------------------------------------------------------------------ Earth curvature (14.7.5, T-CURV)
        [Test]
        public void HorizonDip_MatchesAscentTable()
        {
            double[] s = { 500, 540, 580, 597, 620, 664, 700, 750, 800, 840 };
            double[] dip = { 0.42, 0.76, 1.40, 1.82, 2.61, 5.09, 8.43, 15.04, 21.71, 23.95 };
            for (int i = 0; i < s.Length; i++)
                Assert.That(SkyMath.HorizonDipDeg(ThreadMath.VirtualAltitude(s[i])), Is.EqualTo(dip[i]).Within(0.05), "dip at s = " + s[i]);
            // key-frame values (14.5.6 acceptance 3, 14.5.7 acceptance 2) with the T-CURV tolerance
            Assert.That(SkyMath.HorizonDipDeg(ThreadMath.VirtualAltitude(624)), Is.EqualTo(2.77).Within(0.05), "KF6a");
            Assert.That(SkyMath.HorizonDipDeg(ThreadMath.VirtualAltitude(664)), Is.EqualTo(5.09).Within(0.05), "KF6a2");
            Assert.That(SkyMath.HorizonDipDeg(600000.0), Is.EqualTo(23.95).Within(0.05), "s = 840");
            // the atan form equals acos(R / (R + A))
            double A = 7462.5, R = ThreadConstants.EarthRadius;
            Assert.That(SkyMath.HorizonDipDeg(A), Is.EqualTo(Math.Acos(R / (R + A)) / ThreadMath.Deg).Within(1e-6));
        }

        [Test]
        public void KF6a_HorizonAboveImageCentre()
        {
            // 14.5.6: dip 2.77 deg at s = 624; at the spec pose (pitch -8) the sea edge lies 5.2 deg above the image centre
            // (y = +0.14). The expectation is derived from the decided pose (the run06 re-frame may change pitch / s, C-01 / A3).
            var k = Shot("beat06_clouds_KF6a");
            double A = ThreadMath.VirtualAltitude(k.s);
            Assert.IsTrue(SkyMath.EarthLimbScreen(k.yaw, k.pitch, k.fov, Aspect169, A, k.yaw, out _, out double y));
            Assert.IsTrue(PoseNdc(k.yaw, k.pitch, k.fov, Aspect169, k.yaw, -SkyMath.HorizonDipDeg(A), out _, out double yExp));
            Assert.That(y, Is.EqualTo(yExp).Within(0.005), "sea edge on the view azimuth");
            Assert.Greater(y, 0.0, "the sea edge lies above the image centre (sky with stars above it)");
            // the shell limb (R + 3 km) is higher than the R-based limb; the shader hazes the band between them fully,
            // so the visible sea edge is the R-based limb of the acceptance
            Assert.Less(SkyMath.ShellLimbDipDeg(A), SkyMath.HorizonDipDeg(A));
        }

        [Test]
        public void SeaLimb_MeetsTheSkyWithoutALine()
        {
            // E16 / B-r2-7 (wave 3): the seam metric of 14.10.3 compares the rows 3-7 px above and below the R-limb (1080p, FOV 66:
            // 0.18-0.43 deg). Above the limb the sky is exactly the held horizon colour, the haze colour of the sea. The limb band
            // alone hazes the sea there by >= 90 percent; the path haze is >= 0.7 at the R-limb in beat 6 (tHit >= 113 km at
            // 80 km), so the sea differs from the sky by <= 3 percent of CLOUD_TOP - horizon (~70 codes): about 2 codes, < 2 percent
            double pxDeg = 66.0 / 1080.0;
            for (double px = 0.0; px <= 7.0; px += 0.25)
                Assert.GreaterOrEqual(SkyMath.SeaLimbHaze(px * pxDeg), 0.9, "limb band at " + px + " px below the limb");
            Assert.That(SkyMath.SeaLimbHaze(-1.0), Is.EqualTo(1.0), "above the limb: the sky colour");
            // the visible sea edge stays at the D-017 dips: fully hazed only within the 0.3 deg tolerance of 14.5.6 (3) / 14.5.7 (2),
            // and the surface is fully resolved within 1.5 deg below the limb
            Assert.LessOrEqual(SkyConstants.LimbHazeFullDeg, 0.25);
            Assert.That(SkyMath.SeaLimbHaze(1.5), Is.EqualTo(0.0).Within(1e-12));
            // monotonic (no bright or dark line inside the band)
            for (double a = 0.0; a < 1.5; a += 0.01)
                Assert.GreaterOrEqual(SkyMath.SeaLimbHaze(a), SkyMath.SeaLimbHaze(a + 0.01));
        }

        [Test]
        public void KF7b_LimbSag()
        {
            // 14.5.7 (line 750) prints the limb at y = -0.76 / -0.86 / -0.98 for azimuth 30 / 40 / 48 (centre line -0.65, sag
            // 11.4 percent) for yaw 0, pitch -1. The decided KF7b pose (ThreadConstants.KeyShots: yaw -1, pitch -0.5, chosen so
            // the planet keeps its 3 percent margin) lowers the arc (y = -0.78 at azimuth 30) and tilts it by the yaw, so the
            // positions are derived from the actual pose and the assertion is the acceptance of 14.5.7 (2) / 14.10.2: the sag
            // between azimuth 30 and 48 is 10 to 13 percent of the image height, on either side of the pier.
            var k = Shot("beat07_top_KF7b");
            double A = ThreadMath.VirtualAltitude(k.s);
            double limbEl = -ThreadMath.HorizonDipRad(A) / ThreadMath.Deg;
            double[] az = { 30, 40, 48 };
            Assert.IsTrue(SkyMath.EarthLimbScreen(k.yaw, k.pitch, k.fov, Aspect169, A, 0.0, out _, out double y0));
            Assert.IsTrue(PoseNdc(k.yaw, k.pitch, k.fov, Aspect169, 0.0, limbEl, out _, out double y0Exp));
            Assert.That(y0, Is.EqualTo(y0Exp).Within(0.02), "extrapolated centre line");
            bool anySideInFrame = false;
            var log = new System.Text.StringBuilder();
            foreach (int side in new[] { 1, -1 })
            {
                double[] y = new double[3];
                bool inFrame = true;
                for (int i = 0; i < 3; i++)
                {
                    double a = side * az[i];
                    Assert.IsTrue(SkyMath.EarthLimbScreen(k.yaw, k.pitch, k.fov, Aspect169, A, a, out double x, out y[i]));
                    Assert.IsTrue(PoseNdc(k.yaw, k.pitch, k.fov, Aspect169, a, limbEl, out double xExp, out double yExp));
                    Assert.That(y[i], Is.EqualTo(yExp).Within(0.02), "limb y at azimuth " + a);
                    Assert.That(x, Is.EqualTo(xExp).Within(0.02), "limb x at azimuth " + a);
                    Assert.That(Math.Abs(x), Is.LessThan(1.0), "limb point inside the frame at azimuth " + a);
                    inFrame &= Math.Abs(y[i]) < 1.0;
                    log.Append(FormattableString.Invariant($"az {a:F0}: ({x:F3}, {y[i]:F3})  "));
                }
                // a convex arc: lower toward the frame side, above it toward the (deck-hidden) centre
                Assert.Greater(y0, y[0], "centre line above the limb at azimuth " + side * az[0]);
                Assert.Greater(y[0], y[1], "limb falls from azimuth " + side * az[0] + " to " + side * az[1]);
                Assert.Greater(y[1], y[2], "limb falls from azimuth " + side * az[1] + " to " + side * az[2]);
                double sag = (y[0] - y[2]) * 0.5;   // fraction of the image height (half-extent units / 2)
                Assert.That(sag, Is.InRange(0.10, 0.13), "T-CURV limb sag between azimuth " + side * az[0] + " and " + side * az[2]);
                log.Append(FormattableString.Invariant($"sag {sag * 100:F1} %{(inFrame ? "" : " (azimuth-48 point outside the frame)")}\n"));
                anySideInFrame |= inFrame;
            }
            CoreTests.Log("KF7b limb: " + log);
            // the arc between 30 and 48 must be measurable on the capture on at least one side (the spec's -0.98 at 48 was
            // inside the frame; at the decided pose the right-hand point at azimuth +48 drops to y = -1.02)
            Assert.IsTrue(anySideInFrame, "limb points at azimuth 30 / 40 / 48 inside the frame on at least one side: " + log);
        }

        [Test]
        public void LimbGlowScale_At600km()
        {
            Assert.That(SkyMath.LimbGlowEps0Rad(600000.0) / ThreadMath.Deg, Is.EqualTo(0.506).Within(0.01));
        }

        [Test]
        public void ShellIntersection_FloatMatchesDouble()
        {
            // Part 8.6: the float32 form of the shader against double, from the nadir to 0.05 deg inside the limb
            double[] altitudes = { 7462.5, 25181.7, 226132.2, 600000.0 };
            foreach (double A in altitudes)
            {
                double h = A - ThreadConstants.CloudTop;
                double muH = SkyMath.ShellCosLimb(A);
                double psiLimb = Math.Acos(muH);
                double worstT = 0, worstG = 0;
                for (int i = 0; i <= 2000; i++)
                {
                    double psi = (psiLimb - 0.05 * ThreadMath.Deg) * i / 2000.0;
                    double m = Math.Cos(psi), sp = Math.Sin(psi);
                    Assert.IsTrue(SkyMath.ShellHit(h, m, sp, out double t, out double g));
                    SkyMath.ShellHitFloat((float)h, (float)muH, (float)m, (float)sp, out float tf, out float gf);
                    Assert.IsFalse(float.IsNaN(tf) || float.IsNaN(gf) || float.IsInfinity(tf));
                    worstT = Math.Max(worstT, Math.Abs(tf - t) / t);
                    if (g > 1e-6) worstG = Math.Max(worstG, Math.Abs(gf - g) / g);
                    // the stable form equals the textbook root in double
                    double rho = ThreadConstants.EarthRadius + ThreadConstants.CloudTop + h;
                    double c = h * (2.0 * (ThreadConstants.EarthRadius + ThreadConstants.CloudTop) + h);
                    double tRef = rho * m - Math.Sqrt(rho * rho * m * m - c);
                    Assert.That(t, Is.EqualTo(tRef).Within(1e-7 * tRef + 1e-6));
                }
                Assert.Less(worstT, 2e-5, "float t error at A = " + A);
                Assert.Less(worstG, 2e-5, "float gamma error at A = " + A);
                // nadir: t = h exactly and gamma = 0
                SkyMath.ShellHit(h, 1.0, 0.0, out double tn, out double gn);
                Assert.That(tn, Is.EqualTo(h).Within(1e-6 * h));
                Assert.That(gn, Is.EqualTo(0.0).Within(1e-15));
                // a ray above the shell limb misses
                Assert.IsFalse(SkyMath.ShellHit(h, muH - 1e-4, Math.Sqrt(1 - (muH - 1e-4) * (muH - 1e-4)), out _, out _));
            }
        }

        [Test]
        public void ShellCosLimb_MatchesProgressDirectorFormula()
        {
            // ProgressDirector uploads _ThEarth.x = sqrt(1 - (Rc / (Rc + h))^2); SkyMath must agree (one source)
            foreach (double A in new[] { 3500.0, 7462.5, 25181.7, 226132.2, 600000.0 })
            {
                double h = A - ThreadConstants.CloudTop, Rc = ThreadConstants.EarthRadius + ThreadConstants.CloudTop;
                double q = Rc / (Rc + h);
                Assert.That(SkyMath.ShellCosLimb(A), Is.EqualTo(Math.Sqrt(1 - q * q)).Within(1e-12));
                Assert.That(Math.Asin(SkyMath.ShellCosLimb(A)) / ThreadMath.Deg, Is.EqualTo(SkyMath.ShellLimbDipDeg(A)).Within(1e-6));
            }
            Assert.That(SkyMath.ShellCosLimb(2000.0), Is.EqualTo(0.0));
        }

        // ------------------------------------------------------------------ sun / planet impostors (A4, B4, C-16)
        [Test]
        public void Impostors_AreRoundWithTheOnAxisSize_AtEveryAspect()
        {
            double pa = ThreadConstants.PlanetAzimuthDeg, pe = ThreadConstants.PlanetElevationDeg, pr = ThreadConstants.PlanetRadiusDeg;
            foreach (double asp in new[] { Aspect169, Aspect1610, Aspect219 })
            {
                double W = 1080.0 * asp;
                foreach (string tag in new[] { "beat07_top_KF7a", "beat07_top_KF7b", "beat01_salt_KF1" })
                {
                    var k = Shot(tag);
                    // planet: a circle in pixels, radius f tan(14 deg), at the projected centre
                    if (SkyMath.DiscImpostorScreen(k.yaw, k.pitch, k.fov, asp, SkyMath.PlanetDirection(), pr, out double cx, out double cy, out double rx, out double ry))
                    {
                        Assert.That(rx * 0.5 * W, Is.EqualTo(ry * 0.5 * 1080.0).Within(1e-9), tag + " planet round at aspect " + asp);
                        Assert.That(ry * 0.5 * 1080.0, Is.EqualTo(SkyMath.ImpostorRadiusPx(pr, k.fov, 1080)).Within(1e-9));
                        Assert.IsTrue(PoseNdc(k.yaw, k.pitch, k.fov, asp, pa, pe, out double ex, out double ey));
                        Assert.That(cx, Is.EqualTo(ex).Within(1e-9)); Assert.That(cy, Is.EqualTo(ey).Within(1e-9));
                        // acceptance 1 of 14.5.7 measured on the impostor: 28 deg with the on-axis formula, at any position
                        Assert.That(SkyMath.ImpostorDiameterDeg(2.0 * ry * 0.5 * 1080.0, k.fov, 1080), Is.EqualTo(2.0 * pr).Within(1e-9));
                    }
                    // sun: 0.9 deg disc, same construction
                    if (SkyMath.SunScreenBox(k.s, k.yaw, k.pitch, k.fov, asp, out double sx0, out double sx1, out double sy0, out double sy1))
                        Assert.That((sx1 - sx0) * 0.5 * W, Is.EqualTo((sy1 - sy0) * 0.5 * 1080.0).Within(1e-9), tag + " sun round at aspect " + asp);
                }
            }
            // on axis (camera looking at the centre) the impostor equals the true angular disc: same radius, centred
            var c = SkyMath.PlanetDirection();
            SkyMath.AzElFromDirection(c, out double az, out double el);
            Assert.IsTrue(SkyMath.DiscImpostorScreen(az, el, 66.0, Aspect169, c, pr, out double ox, out double oy, out _, out double ory));
            Assert.That(ox, Is.EqualTo(0.0).Within(1e-12)); Assert.That(oy, Is.EqualTo(0.0).Within(1e-12));
            Assert.IsTrue(DiscBoxNdc(az, el, 66.0, Aspect169, pa, pe, pr, 3600, out _, out _, out double dy0, out double dy1));
            Assert.That(ory, Is.EqualTo(0.5 * (dy1 - dy0)).Within(2e-3), "on-axis impostor radius = projected angular radius");
            // 14.5.7: 431 px diameter at the centre of a FOV 64 view at 1080p
            Assert.That(2.0 * SkyMath.ImpostorRadiusPx(pr, 64.0, 1080), Is.EqualTo(431.0).Within(1.0));
            // the KF1 sun: the same 0.9 deg disc is about 14 px at 1080p (14.7.7) wherever it sits
            Assert.That(2.0 * SkyMath.ImpostorRadiusPx(ThreadConstants.SunDiscRadiusDeg, 62.0, 1080), Is.EqualTo(28.0).Within(1.0), "sun diameter px");
        }

        [Test]
        public void PlanetImpostor_DiscFrameIsTheHeadOnSphere()
        {
            // the shader's normalised screen offset: 0 at the centre, |q| = 1 on the drawn circle, y along the projected vertical
            foreach (string tag in new[] { "beat07_top_KF7a", "beat07_top_KF7b" })
            {
                var k = Shot(tag);
                Assert.IsTrue(SkyMath.DiscImpostorScreen(k.yaw, k.pitch, k.fov, Aspect169, SkyMath.PlanetDirection(), ThreadConstants.PlanetRadiusDeg,
                                                         out double cx, out double cy, out double rx, out double ry));
                Assert.IsTrue(SkyMath.PlanetDiscOffset(k.yaw, k.pitch, SkyMath.PlanetDirection(), out double q0x, out double q0y));
                Assert.That(Math.Abs(q0x) + Math.Abs(q0y), Is.LessThan(1e-9), tag + " centre");
                for (int i = 0; i < 16; i++)
                {
                    double th = 2.0 * Math.PI * i / 16.0;
                    D3 d = SkyMath.ScreenToDirection(k.yaw, k.pitch, k.fov, Aspect169, cx + rx * Math.Cos(th), cy + ry * Math.Sin(th));
                    Assert.IsTrue(SkyMath.PlanetDiscOffset(k.yaw, k.pitch, d, out double qx, out double qy));
                    Assert.That(Math.Sqrt(qx * qx + qy * qy), Is.EqualTo(1.0).Within(1e-9), tag + " rim point " + i);
                }
                // the disc frame's up is the projected world vertical: a point straight above the centre (world) has qx ~ 0
                D3 above = (SkyMath.PlanetDirection() + new D3(0, 0.05, 0)).Normalized;
                Assert.IsTrue(SkyMath.PlanetDiscOffset(k.yaw, k.pitch, above, out double ux, out double uy));
                Assert.That(Math.Abs(ux), Is.LessThan(0.01 * Math.Abs(uy)), tag + " vertical stays vertical");
                Assert.Greater(uy, 0.0);
            }
        }

        [Test]
        public void PlanetReveal_StarsHiddenBeforeTheColourRises()
        {
            // A10 / E11 / PAC-13: the disc hides the stars completely before its colour starts to rise from the sky colour, and
            // it never hides a visible sun (the occlusion ramp starts after the sun disc is gone)
            Assert.Less(SkyConstants.PlanetOcclusionA0Km, ThreadConstants.PlanetAlphaA0);
            for (double s = 600.0; s <= 860.0; s += 0.5)
            {
                double aKm = ThreadMath.VirtualAltitude(s) / 1000.0;
                double occl = ThreadMath.Smoothstep(SkyConstants.PlanetOcclusionA0Km, ThreadConstants.PlanetAlphaA0, aKm);
                if (ThreadMath.PlanetAlpha(s) > 0.0) Assert.That(occl, Is.EqualTo(1.0).Within(1e-12), "stars fully hidden at s = " + s);
                if (occl > 0.0) Assert.That(ThreadMath.SunAlpha(s), Is.EqualTo(0.0).Within(1e-12), "no visible sun under the disc at s = " + s);
            }
        }

        // ------------------------------------------------------------------ dusk sky (C-15)
        [Test]
        public void DuskSky_HoldsThePeachHorizon_AndNeverGoesGrey()
        {
            // beats 1-5 (D <= 0.5): exactly ThreadSkyColor, so the fogged geometry still meets the sky at one colour
            for (double D = 0.0; D <= 0.5; D += 0.05)
            {
                Assert.That(SkyMath.SkyHoldDarkness(D), Is.EqualTo(D).Within(1e-15));
                for (double e = 0.0; e <= 90.0; e += 5.0)
                {
                    double y = Math.Sin(e * ThreadMath.Deg);
                    var a = SkyMath.SkyGradient(y, D, out _);
                    var b = FogMath.SkyColor(y, D);
                    Assert.That(Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b), Is.LessThan(1e-12), "D " + D + " elevation " + e);
                }
            }
            // the held horizon darkness is continuous, monotone, 0.5 through D = 0.85 and the night stop at D = 1
            double prev = SkyMath.SkyHoldDarkness(0.5);
            for (double D = 0.5; D <= 1.0 + 1e-9; D += 0.001)
            {
                double h = SkyMath.SkyHoldDarkness(D);
                Assert.GreaterOrEqual(h, prev - 1e-12);
                Assert.Less(h - prev, 0.02, "continuous at D = " + D);
                prev = h;
            }
            Assert.That(SkyMath.SkyHoldDarkness(0.8), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(SkyMath.SkyHoldDarkness(1.0), Is.EqualTo(1.0).Within(1e-12));
            // KF6a / KF6a2: the horizon glows SKY_H_DUSK (peach) and no sky row is grey (HSV saturation >= 0.15 before the grade)
            var peach = SkyMath.PalRgb("SKY_H_DUSK");
            foreach (double s in new[] { 624.0, 636.0, 664.0 })
            {
                double D = ThreadMath.Darkness(s);
                SkyMath.SkyGradient(0.0, D, out var hor);
                if (s < 650.0) Assert.That(Math.Abs(hor.r - peach.r) + Math.Abs(hor.g - peach.g) + Math.Abs(hor.b - peach.b), Is.LessThan(1e-9), "peach horizon at s = " + s);
                double worst = 1.0, worstE = 0.0;
                for (double e = 0.0; e <= 89.0; e += 0.5)
                {
                    double sat = SkyMath.SrgbSaturation(SkyMath.SkyGradient(Math.Sin(e * ThreadMath.Deg), D, out _));
                    if (sat < worst) { worst = sat; worstE = e; }
                }
                CoreTests.Log(FormattableString.Invariant($"C-15 sky at s = {s}: D = {D:F3}, minimum saturation {worst:F3} at {worstE:F1} deg"));
                Assert.GreaterOrEqual(worst, 0.15, "grey sky row at s = " + s + ", elevation " + worstE);
            }
            // continuous across the 25 deg stop (the chroma restoration is the identity at both stops)
            double tM = Math.Sin(ThreadConstants.SkyStopM * ThreadMath.Deg);
            var lo = SkyMath.SkyGradient(tM - 1e-7, 0.75, out _);
            var hi = SkyMath.SkyGradient(tM + 1e-7, 0.75, out _);
            Assert.That(Math.Abs(lo.r - hi.r) + Math.Abs(lo.g - hi.g) + Math.Abs(lo.b - hi.b), Is.LessThan(1e-4));
        }

        // ------------------------------------------------------------------ cloud sea (C-01, B2, C-02, E10)
        [Test]
        public void CloudSea_OctavesGoneFromOrbit_AndFadeOverWideFootprints()
        {
            // B2 / C-02 / E10: by 60 km every cloud octave is gone (only the weather systems remain), and each octave fades
            // over at least 12 -> 24 px of footprint so thresholded detail never narrows below ~1.5 px
            for (int o = 0; o < 3; o++)
            {
                Assert.Less(SkyConstants.CloudAltitudeFadeKm0[o], SkyConstants.CloudAltitudeFadeKm1[o]);
                Assert.LessOrEqual(SkyConstants.CloudAltitudeFadeKm1[o], 60.0, "octave " + o);
            }
            Assert.GreaterOrEqual(SkyConstants.CloudFadePx0, 12.0);
            Assert.GreaterOrEqual(SkyConstants.CloudFadePx1, 2.0 * SkyConstants.CloudFadePx0);
            // finer octaves leave first, so the cluster mask is never needed where the cluster octave has faded
            Assert.LessOrEqual(SkyConstants.CloudAltitudeFadeKm1[2], SkyConstants.CloudAltitudeFadeKm1[1]);
            Assert.LessOrEqual(SkyConstants.CloudAltitudeFadeKm1[1], SkyConstants.CloudAltitudeFadeKm1[0]);
            Assert.LessOrEqual(SkyConstants.CloudWarp, 0.25, "C-01: warp about 0.2 cell");
            Assert.Less(SkyConstants.CloudToneShade, SkyConstants.CloudToneTop);
        }

        [Test]
        public void CloudSea_EveryToneEdgeIsFilteredOverPixels()
        {
            // B-r2-2 / C-r2-1 / E10 (wave 3): no tone edge sharper than ~1.5 px, and every pixel-scale blend of the puff union
            // (disc coverage, crease) spans at least 2 px, so the analytic tone gradient sees it from every pixel (fwidth
            // only sees jumps inside a 2 x 2 quad)
            Assert.GreaterOrEqual(SkyConstants.CloudTonePx, 1.5, "tone edges at least 1.5 px wide");
            Assert.LessOrEqual(SkyConstants.CloudTonePx, 3.0, "and still crisp flat tones (no airbrush)");
            Assert.GreaterOrEqual(SkyConstants.PuffAaPx, 2.0, "dome outline ramp");
            Assert.GreaterOrEqual(SkyConstants.PuffCreasePx, 2.0, "crease ramp between overlapping domes");
            // the steep rim band of a dome (width ~ rho^2 r / 2 cells) with rho = sqrt(PuffRimPx fp / r) is PuffRimPx / 2 px wide
            Assert.GreaterOrEqual(0.5 * SkyConstants.PuffRimPx, 2.0, "rim crescents at least 2 px (else they fade into the flank)");
            Assert.Less(SkyConstants.PuffRimFloor, SkyConstants.PuffRimMax);
            Assert.Less(SkyConstants.CloudToneEdge, 0.5 * (SkyConstants.CloudToneTop - SkyConstants.CloudToneShade), "the mid tone stays a flat band");
            // Earth from orbit (A-r2-2 / E-r2-2): crisp weather and coast edges of about 1-2 px, never a fixed world softness
            Assert.That(SkyConstants.EarthEdgeAaPx, Is.InRange(0.5, 1.0));
        }

        // ------------------------------------------------------------------ cable shadow band (A3, B5, C-01, E3, PAC-10)
        [Test]
        public void ShadowBand_OwnDeeperTone_AtLeast25PercentAgainstTheLocalSea()
        {
            var top = SkyMath.PalRgb("CLOUD_TOP");
            var shade = SkyMath.PalRgb("CLOUD_SHADE");
            var mix = FogMath.Rgb.Lerp(top, shade, 0.5);
            double op = SkyConstants.ShadowBandOpacityF1;
            for (double s = 590.0; s <= 760.0; s += 2.0)
            {
                double D = ThreadMath.Darkness(s);
                var band = SkyMath.ShadowBandColour(D);
                var tint = SkyMath.ShadowBandTint(D);
                // wave 3 (B-r2-1, C-r2-2, E-r2-1, PAC-r2-4): the band multiplies whatever tone lies under it (no shade corridor,
                // no light verges), so it is at least 25 percent darker over every tone, including the shaded flanks
                foreach (var under in new[] { shade, mix, top })
                {
                    var m = new FogMath.Rgb(1.0 - op * (1.0 - tint.r), 1.0 - op * (1.0 - tint.g), 1.0 - op * (1.0 - tint.b));
                    double yUnder = SkyMath.Luminance(under);
                    double yBand = SkyMath.Luminance(new FogMath.Rgb(under.r * m.r, under.g * m.g, under.b * m.b));
                    Assert.GreaterOrEqual(1.0 - yBand / yUnder, 0.25, "band contrast at s = " + s);
                }
                // a pure shadow: it never lifts a channel of the sea
                Assert.LessOrEqual(Math.Max(tint.r, Math.Max(tint.g, tint.b)), 1.0, "band tint at s = " + s);
                // over lit cloud the band is exactly its own tone, darker than the sea's own shade tone
                Assert.That(top.r * tint.r, Is.EqualTo(band.r).Within(1e-12));
                Assert.Less(SkyMath.Luminance(band), SkyMath.Luminance(shade));
            }
            // E3: the band reaches toward the horizon at every altitude (the fade end grows with the height above the tops)
            foreach (double s in new[] { 624.0, 664.0, 700.0 })
            {
                double h = SkyMath.HeightAboveCloudTops(s);
                Assert.Greater(SkyMath.ShadowBandOpacity(Math.Max(ThreadConstants.ShadowBandFade0, SkyConstants.ShadowBandFadeAlt0 * h) * 0.99, h), 0.5 * op, "s = " + s);
                Assert.That(SkyMath.ShadowBandOpacity(Math.Max(ThreadConstants.ShadowBandFade1, SkyConstants.ShadowBandFadeAlt1 * h), h), Is.EqualTo(0.0).Within(1e-12));
            }
        }

        /// <summary>KF6a re-frame for C-01 / A3 / B5 / E3 / PAC-10 (handed to the lead for ThreadConstants.KeyShots): s = 624, at the
        /// outer corridor edge (lateral -1.15), world yaw 175 (heading 183.3 - 8.3), pitch -16, FOV 66.</summary>
        internal const double Kf6aS = 624.0, Kf6aLateral = -1.15, Kf6aYaw = 157.0, Kf6aPitch = -12.0, Kf6aFov = 66.0;

        /// <summary>True when a ray from cam hits the walkway deck slab (top = PathY, 0.4 m thick, |lateral| &lt;= 1.5) or a handrail tube
        /// within 40 m, or the cable (radius 51.2) at any distance. The deck is the helix of 14.4.2 around the cable axis.</summary>
        static bool WalkwayOrCableOccludes(D3 cam, D3 dir, double sRef)
        {
            // cable cylinder
            double ox = cam.x - ThreadConstants.CableAxisX, oz = cam.z - ThreadConstants.CableAxisZ;
            double a = dir.x * dir.x + dir.z * dir.z, b = 2.0 * (dir.x * ox + dir.z * oz), c = ox * ox + oz * oz - ThreadConstants.CableRidgeCrest * ThreadConstants.CableRidgeCrest;
            double disc = b * b - 4.0 * a * c;
            if (a > 1e-12 && disc > 0.0 && (-b - Math.Sqrt(disc)) / (2.0 * a) > 0.0) return true;
            double R = ThreadConstants.HelixRadius, turn = ThreadConstants.HelixEndS - ThreadConstants.HelixStartS;
            double tube = 0.5 * ThreadConstants.HandrailDiameter;
            for (double tau = 0.01; tau < 40.0; tau += 0.01)
            {
                D3 p = cam + dir * tau;
                double qx = p.x - ThreadConstants.HelixCentreX, qz = p.z - ThreadConstants.HelixCentreZ;
                double r = Math.Sqrt(qx * qx + qz * qz);
                double sp = ThreadConstants.HelixStartS + R * Math.Atan2(qz, -qx);
                sp += turn * Math.Round((sRef - sp) / turn);
                if (sp < ThreadConstants.HelixStartS || sp > ThreadConstants.HelixEndS) continue;
                double lat = R - r, yTop = ThreadMath.PathY(sp);
                if (Math.Abs(lat) <= Megastructure.Beats.WalkwayLayout.HalfWidth && p.y <= yTop && p.y >= yTop - ThreadConstants.DeckThickness) return true;
                for (int side = -1; side <= 1; side += 2)
                {
                    double dl = lat - side * Megastructure.Beats.WalkwayLayout.RailLateral, dyy = p.y - (yTop + Megastructure.Beats.WalkwayLayout.RailCentreHeight);
                    if (dl * dl + dyy * dyy < tube * tube) return true;
                }
            }
            return false;
        }

        static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
        {
            double vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            double t = l2 > 1e-12 ? ThreadMath.Clamp01(((px - ax) * vx + (py - ay) * vy) / l2) : 0.0;
            double dx = px - (ax + t * vx), dy = py - (ay + t * vy);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void KF6a_Reframe_ShadowBandIsALeadingLineOverOpenSea()
        {
            const double W = 1920.0, H = 1080.0;
            double s = Kf6aS, yaw = Kf6aYaw, pitch = Kf6aPitch, fov = Kf6aFov;
            D3 cam = ThreadMath.CorridorToWorld(s, Kf6aLateral, ThreadConstants.EyeHeight);
            Assert.LessOrEqual(Math.Abs(Kf6aLateral), ThreadConstants.HalfWidthDeck + 1e-12, "inside the walkable corridor");
            double A = ThreadMath.VirtualAltitude(s), h = A - ThreadConstants.CloudTop;
            double f1 = Math.Max(ThreadConstants.ShadowBandFade1, SkyConstants.ShadowBandFadeAlt1 * h);
            // the band axis on the sea, sampled from 60 m to the end of its fade, projected and tested against deck, rails and cable
            var pts = new System.Collections.Generic.List<(double x, double y, double t)>();
            int n = 160;
            for (int i = 0; i <= n; i++)
            {
                double t = 60.0 * Math.Pow(f1 / 60.0, i / (double)n);
                if (SkyMath.ShadowBandOpacity(t, h) < 0.05 * SkyConstants.ShadowBandOpacityF1) continue;
                Assert.IsTrue(SkyMath.ShadowBandAxisDirection(cam, A, t, out D3 dir, out _));
                if (!SkyMath.ProjectDirection(new D3(0, 0, 0), yaw, pitch, fov, W / H, dir, out double x, out double y)) continue;
                if (Math.Abs(x) > 1.0 || Math.Abs(y) > 1.0) continue;
                if (WalkwayOrCableOccludes(cam, dir, s)) continue;
                pts.Add(((x + 1.0) * 0.5 * W, (1.0 - y) * 0.5 * H, t));
            }
            Assert.Greater(pts.Count, 20, "band visible over open sea");
            double len = 0.0, lowest = double.MinValue, highest = double.MaxValue, tLow = 0.0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) len += Math.Sqrt((pts[i].x - pts[i - 1].x) * (pts[i].x - pts[i - 1].x) + (pts[i].y - pts[i - 1].y) * (pts[i].y - pts[i - 1].y));
                if (pts[i].y > lowest) { lowest = pts[i].y; tLow = pts[i].t; }
                highest = Math.Min(highest, pts[i].y);
            }
            // horizon (R-based limb) on the view azimuth
            Assert.IsTrue(SkyMath.EarthLimbScreen(yaw, pitch, fov, W / H, A, yaw, out _, out double hy));
            double horizonPx = (1.0 - hy) * 0.5 * H;
            double widthLow = SkyMath.ShadowBandWidthPx(cam, A, tLow, fov, H);
            CoreTests.Log(FormattableString.Invariant($"KF6a re-frame: cam ({cam.x:F2}, {cam.y:F2}, {cam.z:F2}) yaw {yaw} pitch {pitch}; band visible {len:F0} px from y {lowest:F0} px (width {widthLow:F1} px) to y {highest:F0} px, horizon at {horizonPx:F0} px"));
            // acceptance 2 of 14.5.6: at least 12 percent of the image width; it starts in the lower fifth and reaches near the horizon
            Assert.GreaterOrEqual(len, 0.12 * W, "band length");
            Assert.GreaterOrEqual(lowest, 0.8 * H, "the band starts at the bottom of the frame (not hidden by the deck)");
            Assert.LessOrEqual(highest - horizonPx, 0.06 * H, "the band reaches toward the horizon");
            Assert.Greater(horizonPx, 0.2 * H, "sky (stars) above the horizon");
            Assert.GreaterOrEqual(widthLow, 15.0, "14.5.6: at least 15 px wide at the bottom of the frame");
            // the axis runs exactly opposite the sun
            Assert.That(SkyMath.ShadowBandAzimuthDeg(), Is.EqualTo(ThreadConstants.SunAzimuthDeg + 180.0).Within(1e-12));
            // B5: no rail post within 25 px of the visible band axis (a post beside the band reads as its reflection)
            double worst = double.MaxValue;
            foreach (var post in Megastructure.Beats.WalkwayLayout.Posts())
            {
                D3 b = post.basePos, top = post.basePos + new D3(0, Megastructure.Beats.WalkwayLayout.RailCentreHeight, 0);
                if (!SkyMath.ProjectPoint(cam, yaw, pitch, fov, W / H, b, out double bx, out double by)) continue;
                if (!SkyMath.ProjectPoint(cam, yaw, pitch, fov, W / H, top, out double tx, out double ty)) continue;
                double ax0 = (bx + 1.0) * 0.5 * W, ay0 = (1.0 - by) * 0.5 * H, ax1 = (tx + 1.0) * 0.5 * W, ay1 = (1.0 - ty) * 0.5 * H;
                foreach (var p in pts) worst = Math.Min(worst, SegmentDistance(p.x, p.y, ax0, ay0, ax1, ay1));
            }
            CoreTests.Log(FormattableString.Invariant($"KF6a re-frame: nearest post {worst:F0} px from the band axis"));
            Assert.GreaterOrEqual(worst, 25.0, "post beside the band");
        }

        // ------------------------------------------------------------------ stars (14.5.7)
        [Test]
        public void GlintStar_DirectionAndClearOfPlanet()
        {
            SkyMath.AzElFromDirection(SkyMath.GlintStarDirection(), out double az, out double el);
            Assert.That(az, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(el, Is.EqualTo(20.0).Within(1e-9));
            double sep = SkyMath.AngleBetweenDeg(SkyMath.GlintStarDirection(), SkyMath.PlanetDirection());
            Assert.Greater(sep, ThreadConstants.PlanetRadiusDeg + 5.0, "the glint star must not sit on the planet");
            // and it is in the final frame (KF7b) above the beacon
            var k = Shot("beat07_top_KF7b");
            Assert.IsTrue(SkyMath.ProjectDirection(new D3(0, 0, 0), k.yaw, k.pitch, k.fov, Aspect169, SkyMath.GlintStarDirection(), out double gx, out double gy));
            Assert.That(Math.Abs(gx), Is.LessThan(0.05));
            Assert.That(gy, Is.InRange(0.3, 0.9));
        }

        [Test]
        public void StarTiers_SumToOne_AndCellsAboutOnePointOneDegrees()
        {
            Assert.That(SkyConstants.StarTier1Fraction + SkyConstants.StarTier2Fraction + SkyConstants.StarTier3Fraction, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(90.0 / SkyConstants.StarCellsPerFace, Is.EqualTo(1.1).Within(0.05));
        }

        [Test]
        public void Stars_FadeInTheBrightTwilightBand_KeepTheDarkSky()
        {
            // B-r2-3 / E-r2-6 (wave 3): stars fade with the luminance of the sky behind them. KF6a2 (s 664): none in the bright
            // horizon band, full strength at the zenith; KF6a (s 624): the top edge of the frame (+17 deg) keeps its stars
            // (acceptance 14.5.6 (4) counts them); beat 7's night sky is untouched.
            double D2 = ThreadMath.Darkness(664.0);
            Assert.That(SkyMath.StarSkyFade(SkyMath.Luminance(SkyMath.SkyGradient(0.0, D2, out _))), Is.EqualTo(0.0).Within(1e-9), "KF6a2 horizon band");
            Assert.That(SkyMath.StarSkyFade(SkyMath.Luminance(SkyMath.SkyGradient(1.0, D2, out _))), Is.EqualTo(1.0).Within(1e-9), "KF6a2 zenith");
            double D1 = ThreadMath.Darkness(624.0);
            Assert.GreaterOrEqual(SkyMath.StarSkyFade(SkyMath.Luminance(SkyMath.SkyGradient(Math.Sin(17.0 * ThreadMath.Deg), D1, out _))), 0.9, "KF6a top edge");
            Assert.That(SkyMath.StarSkyFade(SkyMath.Luminance(SkyMath.SkyGradient(0.0, 1.0, out _))), Is.EqualTo(1.0).Within(1e-9), "night horizon");
        }
    }
}
