// CoreTests: EditMode NUnit checks of the core maths (ThreadMath, ThreadConstants, ThreadPalette and the
// sky gradient of FogMath) against MASTER_PROMPT Part 14:
//   T-PATH (14.4.2 - 14.4.4, 14.10.1), corridor half-widths (14.4.3), T-THREAD and the looming rate (14.7.3),
//   T-CURV and the ascent table (14.7.5), the FOV curve (14.12) and the exposure schedule (14.6),
//   the palette tokens (14.2.1) and the sky gradient (14.2.1, D-008), the first part of T-SHADE (14.7.7,
//   D-004) and the constants of table 14.12.
// Values in the tables marked "oracle" are transcribed from the specification (line numbers cited); they
// are the reference of the test and deliberately not read from ThreadConstants. Everything else is derived
// from the contract. Where the lead deliberately changed a Part-14 number (DEVELOPMENT.md, D-010 to D-031) the test
// asserts the decided value and cites both the spec line and the decision id.
// Every test collects all failing checks and reports them with numbers in one message.
// Pure maths: no scene, no GameObject, no rendering.
using System;
using System.Text;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    public class CoreTests
    {
        // ================================================================================================
        // Shared kit (FogTests uses it too)
        // ================================================================================================

        /// <summary>Culture-invariant formatting (the test machine may use a decimal comma).</summary>
        public static string Inv(FormattableString f) => FormattableString.Invariant(f);

        public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>Culture-invariant vector text (D3.ToString uses the current culture).</summary>
        public static string V(D3 p) => Inv($"({p.x:F3}, {p.y:F3}, {p.z:F3})");

        /// <summary>Writes a diagnostic line into the NUnit result (falls back to the console outside a runner).</summary>
        public static void Log(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (Exception) { Console.WriteLine(line); }
        }

        /// <summary>Collects every failing check of one test so a single run reports all numbers at once.</summary>
        public sealed class Checks
        {
            const int MaxLines = 40;
            readonly string m_title;
            readonly StringBuilder m_lines = new StringBuilder();
            int m_failed, m_total;

            public Checks(string title) { m_title = title; }
            public int Failed => m_failed;
            public int Total => m_total;

            public bool That(bool ok, string message)
            {
                m_total++;
                if (ok) return true;
                m_failed++;
                if (m_failed <= MaxLines) m_lines.Append("  - ").Append(message).Append('\n');
                return false;
            }

            /// <summary>|actual - expected| within tol (NaN and infinities always fail).</summary>
            public bool Near(double actual, double expected, double tol, string what)
            {
                bool ok = IsFinite(actual) && Math.Abs(actual - expected) <= tol;
                return That(ok, Inv($"{what}: expected {expected:G7} +-{tol:G3}, got {actual:G9} (diff {actual - expected:G3})"));
            }

            /// <summary>|actual - expected| within rel x |expected|.</summary>
            public bool NearRel(double actual, double expected, double rel, string what)
            {
                bool ok = IsFinite(actual) && Math.Abs(actual - expected) <= rel * Math.Abs(expected);
                double pct = expected != 0 ? (actual / expected - 1.0) * 100.0 : double.NaN;
                return That(ok, Inv($"{what}: expected {expected:G7} +-{rel * 100:G3} %, got {actual:G9} ({pct:F2} %)"));
            }

            public void AssertAll()
            {
                if (m_failed == 0) return;
                string more = m_failed > MaxLines ? Inv($"  ... and {m_failed - MaxLines} more\n") : "";
                Assert.Fail(Inv($"{m_title}: {m_failed} of {m_total} checks failed\n") + m_lines + more);
            }
        }

        /// <summary>SplitMix64: deterministic on every runtime (System.Random's sequence is an implementation detail).</summary>
        public sealed class Rng
        {
            ulong m_state;
            public Rng(ulong seed) { m_state = seed; }

            public ulong NextU64()
            {
                m_state += 0x9E3779B97F4A7C15UL;
                ulong z = m_state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            /// <summary>Uniform in [0, 1) with 53 random bits.</summary>
            public double Next01() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
            public double Range(double a, double b) => a + (b - a) * Next01();
            public double LogRange(double a, double b) => Math.Exp(Range(Math.Log(a), Math.Log(b)));

            /// <summary>Uniform direction on the unit sphere.</summary>
            public D3 UnitVector()
            {
                double y = Range(-1.0, 1.0), phi = Range(0.0, 2.0 * Math.PI), r = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                return new D3(r * Math.Sin(phi), y, r * Math.Cos(phi));
            }

            /// <summary>Direction with the given elevation (radians) and a uniform azimuth.</summary>
            public D3 AtElevation(double elevation)
            {
                double phi = Range(0.0, 2.0 * Math.PI), ce = Math.Cos(elevation);
                return new D3(ce * Math.Sin(phi), Math.Sin(elevation), ce * Math.Cos(phi));
            }
        }

        static double Deg => ThreadMath.Deg;

        /// <summary>True bearing of the cable axis from the nominal path, frozen at the swap (14.7.3 line 812: atan2(70.028, 400 - s)).</summary>
        public static double TrueThreadBearingRad(double s)
            => Math.Atan2(ThreadConstants.CableAxisX, ThreadConstants.CableAxisZ - Math.Min(s, ThreadConstants.SwapS));

        /// <summary>Apparent far-thread bearing as decided in D-026, written out from the decision text (not from ThreadMath):
        /// atan2(70.028, 400 - s) - 8.6 deg x (1 - smoothstep(90, 240, s)); equal to the true bearing from s = 240.</summary>
        public static double DecidedThreadBearingRad(double s)
        {
            double sc = Math.Min(s, ThreadConstants.SwapS);
            double t = (sc - ThreadConstants.ThreadBearingOffsetS0) / (ThreadConstants.ThreadBearingOffsetS1 - ThreadConstants.ThreadBearingOffsetS0);
            t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
            return TrueThreadBearingRad(s) - ThreadConstants.ThreadBearingOffsetDeg * Deg * (1.0 - t * t * (3.0 - 2.0 * t));
        }

        // ================================================================================================
        // T-PATH (14.4.2, 14.4.4, 14.10.1)
        // ================================================================================================

        const double SamplesPerMetre = 20.0;   // sampled every 0.05 m (task B12 / T-PATH)
        const double GapTol = 0.001;           // "no gap above 1 mm" (14.10.1 T-PATH)
        static int PathSamples => (int)Math.Round(ThreadConstants.PathLength * SamplesPerMetre);
        static double SampleS(int i) => i / SamplesPerMetre;   // exact at every boundary, no accumulation

        /// <summary>Explains a gap at the helix -> pier joint (one turn of radius 70.028 is 1.1 mm short of 440 m).</summary>
        static string HelixClosureHint()
        {
            double turn = 2.0 * Math.PI * ThreadConstants.HelixRadius;
            double span = ThreadConstants.HelixEndS - ThreadConstants.HelixStartS;
            return Inv($" [one helix turn 2 pi R_h = {turn:F5} m but the plan span is {span:F1} m: the helix end overshoots the pier start by {(span - turn) * 1000.0:F2} mm of arc; R_h = 440 / (2 pi) = {span / (2.0 * Math.PI):F6} m (14.4.2 '70.028 (= 440 / 2 pi)') closes the turn exactly]");
        }

        [Test]
        public void TPath_ContinuousAtFiveCentimetreSampling()
        {
            var c = new Checks("T-PATH continuity (14.4.2: PathPoint sampled every 0.05 m over 0..860, no gap above 1 mm)");
            int n = PathSamples;
            D3 prev = ThreadMath.PathPoint(0.0);
            double worstPlan = 0, worstPlanS = 0, worstRise = 0, worstRiseS = 0;
            for (int i = 1; i <= n; i++)
            {
                double s0 = SampleS(i - 1), s1 = SampleS(i), ds = s1 - s0;
                D3 p = ThreadMath.PathPoint(s1);
                double dx = p.x - prev.x, dz = p.z - prev.z;
                double chord = Math.Sqrt(dx * dx + dz * dz);
                // plan chord equals the plan step (the helix chord deficit at 0.05 m is 1e-8 m)
                double planErr = Math.Abs(chord - ds);
                // rise equals the analytic grade at the midpoint times the step (exact on ramps, 1e-9 m in the blend)
                double riseErr = Math.Abs((p.y - prev.y) - ThreadMath.PathGrade(0.5 * (s0 + s1)) * ds);
                if (planErr > worstPlan) { worstPlan = planErr; worstPlanS = s1; }
                if (riseErr > worstRise) { worstRise = riseErr; worstRiseS = s1; }
                c.That(IsFinite(p.x) && IsFinite(p.y) && IsFinite(p.z), Inv($"PathPoint({s1:F2}) is not finite: {V(p)}"));
                c.That(planErr <= GapTol, Inv($"plan step s {s0:F2} -> {s1:F2}: chord {chord:F6} m for a step of {ds:F2} m (gap {planErr * 1000.0:F3} mm){(s1 > ThreadConstants.HelixEndS - 0.1 && s1 < ThreadConstants.HelixEndS + 0.1 ? HelixClosureHint() : "")}"));
                c.That(riseErr <= GapTol, Inv($"rise step s {s0:F2} -> {s1:F2}: dy {p.y - prev.y:F6} m vs grade {ThreadMath.PathGrade(0.5 * (s0 + s1)):F4} x {ds:F2} m (gap {riseErr * 1000.0:F3} mm)"));
                prev = p;
            }

            // left and right limits at every branch point of PathPoint / PathY
            double[] joints =
            {
                ThreadConstants.StairStartZ, ThreadConstants.StairEndZ, ThreadConstants.GradeBlendStart,
                ThreadConstants.HelixStartS, ThreadConstants.GradeBlendEnd, ThreadConstants.HelixEndS,
            };
            const double eps = 1e-7;
            for (int j = 0; j < joints.Length; j++)
            {
                double sj = joints[j];
                double gap = (ThreadMath.PathPoint(sj + eps) - ThreadMath.PathPoint(sj - eps)).Length;
                c.That(gap <= GapTol, Inv($"joint at s = {sj:F1}: |P(s+) - P(s-)| = {gap * 1000.0:F3} mm{(sj == ThreadConstants.HelixEndS ? HelixClosureHint() : "")}"));
            }

            // outside 0..860 the path clamps to its ends
            c.That((ThreadMath.PathPoint(-5.0) - ThreadMath.PathPoint(0.0)).Length < 1e-12, "PathPoint(-5) must clamp to P0");
            c.That((ThreadMath.PathPoint(ThreadConstants.PathLength + 5.0) - ThreadMath.PathPoint(ThreadConstants.PathLength)).Length < 1e-12, "PathPoint(865) must clamp to P6");

            Log(Inv($"T-PATH: worst plan gap {worstPlan * 1000.0:F4} mm at s = {worstPlanS:F2}, worst rise gap {worstRise * 1000.0:F4} mm at s = {worstRiseS:F2}"));
            c.AssertAll();
        }

        [Test]
        public void TPath_PlanLength()
        {
            var c = new Checks("T-PATH plan length (14.4.2: 860.0 +-0.1 m in total, helix 440.0 +-0.1 m)");
            int n = PathSamples;
            double total = 0, helix = 0;
            D3 prev = ThreadMath.PathPoint(0.0);
            for (int i = 1; i <= n; i++)
            {
                double s0 = SampleS(i - 1), s1 = SampleS(i);
                D3 p = ThreadMath.PathPoint(s1);
                double dx = p.x - prev.x, dz = p.z - prev.z, chord = Math.Sqrt(dx * dx + dz * dz);
                total += chord;
                if (s0 >= ThreadConstants.HelixStartS && s1 <= ThreadConstants.HelixEndS) helix += chord;
                prev = p;
            }
            c.Near(total, 860.0, 0.1, "plan length of the polyline sampled every 0.05 m (m)");
            c.Near(helix, 440.0, 0.1, "plan length of the helix s 400..840 (m)");
            c.Near(ThreadConstants.PathLength, 860.0, 1e-9, "ThreadConstants.PathLength");
            c.Near(ThreadConstants.HelixEndS - ThreadConstants.HelixStartS, 440.0, 1e-9, "helix plan span HelixEndS - HelixStartS");
            c.Near(2.0 * Math.PI * ThreadConstants.HelixRadius, 440.0, 0.1, "one helix turn 2 pi R_h (m)");
            c.Near(ThreadConstants.HelixRise, 110.0, 1e-9, "helix rise");
            c.Near(ThreadConstants.HelixGrade * (ThreadConstants.HelixEndS - ThreadConstants.HelixStartS), ThreadConstants.HelixRise, 1e-9, "grade x plan span = rise");
            c.AssertAll();
        }

        // oracle: 14.4.2 waypoint table (MASTER_PROMPT lines 588-597): s, x, y, z
        static readonly double[,] s_waypoints =
        {
            { 0,   0, 0,     0 },     // P0 spawn, heading +Z
            { 90,  0, 0,     90 },    // P1 end of beat 1
            { 240, 0, 0,     240 },   // P2 foot of the stairs
            { 320, 0, 36.0,  320 },   // P3 top of the stairs
            { 400, 0, 36.19, 400 },   // P4 start of the helix (y includes the grade blend)
            { 840, 0, 146.0, 400 },   // P5 end of the helix
            { 860, 0, 146.0, 420 },   // P6 goal, 4 m short of the pad
        };

        [Test]
        public void TPath_Waypoints()
        {
            var c = new Checks("T-PATH waypoints P0..P6 (14.4.2)");
            const double tol = 0.005;   // y(400) = 36.19 +-0.005 (task B12); the other coordinates are exact
            for (int i = 0; i < s_waypoints.GetLength(0); i++)
            {
                double s = s_waypoints[i, 0];
                D3 p = ThreadMath.PathPoint(s);
                c.Near(p.x, s_waypoints[i, 1], tol, Inv($"P{i} (s = {s:F0}) x"));
                c.Near(p.y, s_waypoints[i, 2], tol, Inv($"P{i} (s = {s:F0}) y"));
                c.Near(p.z, s_waypoints[i, 3], tol, Inv($"P{i} (s = {s:F0}) z"));
            }
            c.Near(ThreadMath.PathY(400.0), 36.19, 0.005, "y(400) (grade blend, 14.4.2)");
            c.Near(ThreadMath.PathY(404.0), 37.0, 1e-9, "y(404) (blend integral equals the linear ramp from 400)");
            c.Near(ThreadMath.PathY(840.0), 146.0, 1e-9, "y(840) (end of the helix)");
            c.Near(ThreadMath.PathY(860.0), 146.0, 1e-9, "y(860) (pier)");
            c.Near(ThreadConstants.PadCentreZ - ThreadMath.PathPoint(ThreadConstants.GoalS).z, 4.0, 1e-9, "goal P6 is 4 m short of the pad / beacon at z = 424");
            c.Near(ThreadConstants.TopY, 146.0, 1e-9, "ThreadConstants.TopY");
            c.AssertAll();
        }

        // oracle: 14.4.2 helix check points (line 599), printed to 0.1 m: s, x, y, z
        static readonly double[,] s_helixCheckPoints =
        {
            { 490, 50.3,  58.5,  467.2 },
            { 520, 80.0,  66.0,  469.3 },
            { 580, 128.9, 81.0,  437.9 },
            { 648, 134.5, 98.0,  372.8 },
            { 750, 50.3,  123.5, 332.8 },
            { 776, 27.3,  130.0, 344.5 },
        };

        /// <summary>The helix formula exactly as printed in 14.4.2 (line 599), independent of ThreadConstants.</summary>
        static D3 SpecHelix(double s)
        {
            double psi = (s - 400.0) / 70.028;
            return new D3(70.028 - 70.028 * Math.Cos(psi), 36.0 + 17.507 * psi, 400.0 + 70.028 * Math.Sin(psi));
        }

        [Test]
        public void TPath_HelixCheckPoints()
        {
            var c = new Checks("T-PATH helix (14.4.2: generator within 0.02 m of the formula; check points printed to 0.1 m)");
            const double generatorTol = 0.02;          // 14.4.2 "Tolerance of the generator: 0.02 m"
            const double printTol = generatorTol + 0.05; // + half a unit of the 0.1 m print precision
            for (int i = 0; i < s_helixCheckPoints.GetLength(0); i++)
            {
                double s = s_helixCheckPoints[i, 0];
                D3 p = ThreadMath.PathPoint(s), f = SpecHelix(s);
                c.That((p - f).Length <= generatorTol, Inv($"s = {s:F0}: PathPoint {V(p)} is {(p - f).Length * 1000.0:F2} mm from the 14.4.2 formula {V(f)}"));
                c.Near(p.x, s_helixCheckPoints[i, 1], printTol, Inv($"check point s = {s:F0} x"));
                c.Near(p.y, s_helixCheckPoints[i, 2], printTol, Inv($"check point s = {s:F0} y"));
                c.Near(p.z, s_helixCheckPoints[i, 3], printTol, Inv($"check point s = {s:F0} z"));
            }
            // the whole helix after the grade blend follows the formula
            double worst = 0, worstS = 0;
            for (double s = ThreadConstants.GradeBlendEnd; s <= ThreadConstants.HelixEndS + 1e-9; s += 0.5)
            {
                double e = (ThreadMath.PathPoint(s) - SpecHelix(s)).Length;
                if (e > worst) { worst = e; worstS = s; }
            }
            c.That(worst <= generatorTol, Inv($"helix s 404..840 deviates from the 14.4.2 formula by up to {worst * 1000.0:F2} mm (at s = {worstS:F1})"));
            // psi printed for two check points (lines 599, 632)
            c.Near(ThreadMath.Heading(580.0) / Deg, 147.3, 0.05, "psi(580) (deg)");
            c.Near(ThreadMath.Heading(750.0) / Deg, 286.4, 0.05, "psi(750) (deg)");
            Log(Inv($"T-PATH helix: worst deviation from the formula {worst * 1000.0:F3} mm at s = {worstS:F1}"));
            c.AssertAll();
        }

        [Test]
        public void TPath_GradeBlend()
        {
            var c = new Checks("T-PATH grade blend s 396..404 (14.4.2: grade = 0.25 smoothstep(0, 8, s - 396))");
            c.Near(ThreadMath.PathY(396.0), 36.0, 1e-9, "y(396) (flat terrace)");
            c.Near(ThreadMath.PathY(400.0), 36.19, 0.005, "y(400)");
            c.Near(ThreadMath.PathY(404.0), 37.0, 1e-9, "y(404)");
            c.Near(ThreadMath.PathY(404.0) - ThreadMath.PathY(396.0), 1.0, 1e-9, "rise of the blend (= linear ramp from 400, 0.25 x 4)");
            c.Near(ThreadMath.PathGrade(396.0), 0.0, 1e-12, "grade(396)");
            c.Near(ThreadMath.PathGrade(400.0), 0.125, 1e-12, "grade(400)");
            c.Near(ThreadMath.PathGrade(404.0), 0.25, 1e-12, "grade(404)");
            c.Near(ThreadMath.PathGrade(600.0), 0.25, 1e-12, "grade on the helix");
            // the analytic grade is the derivative of the floor height
            const double h = 1e-4;
            for (double s = 390.0; s <= 410.0 + 1e-9; s += 0.25)
            {
                double num = (ThreadMath.PathY(s + h) - ThreadMath.PathY(s - h)) / (2.0 * h);
                c.Near(num, ThreadMath.PathGrade(s), 1e-4, Inv($"dy/ds at s = {s:F2} (numeric vs PathGrade)"));
            }
            // no kink: the grade is continuous through the blend and the helix start
            double maxStep = 0, maxStepS = 0;
            for (double s = 380.0; s < 420.0; s += 0.01)
            {
                double step = Math.Abs(ThreadMath.PathGrade(s + 0.01) - ThreadMath.PathGrade(s));
                if (step > maxStep) { maxStep = step; maxStepS = s; }
            }
            c.That(maxStep <= 1e-3, Inv($"grade jumps by {maxStep:F5} within 0.01 m at s = {maxStepS:F2} (kink between the terrace and the 0.25 grade)"));
            c.AssertAll();
        }

        [Test]
        public void TPath_HeadingContinuity()
        {
            var c = new Checks("T-PATH heading (14.4.2: yaw from +Z toward +X, = psi on the helix, continuous)");
            int n = PathSamples;
            double maxStep = 0, maxStepS = 0;
            for (int i = 1; i <= n; i++)
            {
                double s0 = SampleS(i - 1), s1 = SampleS(i);
                double step = Math.Abs(ThreadMath.Heading(s1) - ThreadMath.Heading(s0));
                double allowed = (s1 - s0) / ThreadConstants.HelixRadius + 1e-4;
                if (step > maxStep) { maxStep = step; maxStepS = s1; }
                c.That(step <= allowed, Inv($"heading jumps by {step / Deg:F4} deg between s = {s0:F2} and {s1:F2}"));
            }
            // the heading is the direction of motion of PathPoint
            double worstAngle = 0, worstS = 0;
            for (double s = 0.01; s < ThreadConstants.PathLength - 0.01; s += 0.25)
            {
                D3 a = ThreadMath.PathPoint(s - 0.005), b = ThreadMath.PathPoint(s + 0.005);
                double dx = b.x - a.x, dz = b.z - a.z;
                ThreadMath.Tangent(s, out double tx, out double tz);
                double ang = Math.Abs(Math.Atan2(dx * tz - dz * tx, dx * tx + dz * tz));
                if (ang > worstAngle) { worstAngle = ang; worstS = s; }
            }
            c.That(worstAngle <= 0.05 * Deg, Inv($"tangent differs from the direction of motion by {worstAngle / Deg:F4} deg at s = {worstS:F2}"));
            // the right vector is perpendicular and points to the cable axis on the helix
            for (double s = 0.0; s <= ThreadConstants.PathLength; s += 10.0)
            {
                ThreadMath.Tangent(s, out double tx, out double tz);
                ThreadMath.Right(s, out double rx, out double rz);
                c.Near(tx * rx + tz * rz, 0.0, 1e-12, Inv($"tangent . right at s = {s:F0}"));
                c.Near(tx * rz - tz * rx, -1.0, 1e-12, Inv($"right is the tangent turned +90 deg (toward +X at heading 0) at s = {s:F0}"));
                if (s > ThreadConstants.HelixStartS && s < ThreadConstants.HelixEndS)
                {
                    D3 q = ThreadMath.CorridorToWorld(s, ThreadConstants.HelixRadius, 0.0);
                    double off = Math.Sqrt((q.x - ThreadConstants.CableAxisX) * (q.x - ThreadConstants.CableAxisX) + (q.z - ThreadConstants.CableAxisZ) * (q.z - ThreadConstants.CableAxisZ));
                    c.That(off < 1e-6, Inv($"s = {s:F0}: right x R_h from the path lands {off:F6} m from the cable axis"));
                }
            }
            ThreadMath.Right(0.0, out double r0x, out double r0z);
            c.Near(r0x, 1.0, 1e-12, "right at s = 0 is +X (x)");
            c.Near(r0z, 0.0, 1e-12, "right at s = 0 is +X (z)");
            c.Near(ThreadMath.Heading(0.0), 0.0, 1e-12, "heading at spawn (+Z)");
            c.Near(Math.Sin(ThreadMath.Heading(850.0)), 0.0, 1e-9, "pier heading is +Z (sin)");
            c.Near(Math.Cos(ThreadMath.Heading(850.0)), 1.0, 1e-9, "pier heading is +Z (cos)");
            Log(Inv($"T-PATH heading: max step {maxStep / Deg:F5} deg at s = {maxStepS:F2}, worst tangent error {worstAngle / Deg:F5} deg at s = {worstS:F2}"));
            c.AssertAll();
        }

        // oracle: 14.4.4 beat table (lines 621-630) and 14.12 beat boundaries (line 1006)
        static readonly double[] s_beatBoundaries = { 0, 90, 240, 320, 400, 580, 750, 860 };
        static readonly double[] s_beatDuration15 = { 60.00, 100.00, 53.33, 53.33, 120.00, 113.33, 73.33 };
        static readonly double[] s_beatCumulative15 = { 60.00, 160.00, 213.33, 266.67, 386.67, 500.00, 573.33 };
        static readonly double[] s_beatDuration25 = { 36.00, 60.00, 32.00, 32.00, 72.00, 68.00, 44.00 };

        [Test]
        public void TPath_BeatBoundariesAndIndex()
        {
            var c = new Checks("T-PATH beats (14.4.4: boundaries 0/90/240/320/400/580/750/860, BeatIndex, timing)");
            var b = ThreadConstants.BeatBoundaries;
            c.That(b.Length == s_beatBoundaries.Length, Inv($"BeatBoundaries has {b.Length} entries, expected {s_beatBoundaries.Length}"));
            c.That(ThreadConstants.BeatCount == 7, Inv($"BeatCount = {ThreadConstants.BeatCount}, expected 7"));
            c.That(ThreadConstants.BeatNames.Length == ThreadConstants.BeatCount, Inv($"BeatNames has {ThreadConstants.BeatNames.Length} entries"));
            int nb = Math.Min(b.Length, s_beatBoundaries.Length);
            for (int i = 0; i < nb; i++) c.Near(b[i], s_beatBoundaries[i], 1e-12, Inv($"beat boundary {i}"));
            c.Near(b[b.Length - 1], ThreadConstants.PathLength, 1e-12, "last boundary = path length");
            c.Near(ThreadConstants.HelixStartS, s_beatBoundaries[4], 1e-12, "helix starts at the beat 5 boundary");

            for (int i = 0; i < ThreadConstants.BeatCount && i + 1 < nb; i++)
            {
                double s0 = s_beatBoundaries[i], s1 = s_beatBoundaries[i + 1];
                c.That(ThreadMath.BeatIndex(s0) == i, Inv($"BeatIndex({s0:F0}) = {ThreadMath.BeatIndex(s0)}, expected {i}"));
                c.That(ThreadMath.BeatIndex(0.5 * (s0 + s1)) == i, Inv($"BeatIndex({0.5 * (s0 + s1):F0}) = {ThreadMath.BeatIndex(0.5 * (s0 + s1))}, expected {i}"));
                c.That(ThreadMath.BeatIndex(s1 - 1e-6) == i, Inv($"BeatIndex({s1:F0} - 1e-6) = {ThreadMath.BeatIndex(s1 - 1e-6)}, expected {i}"));

                double len = s1 - s0;
                c.Near(len / ThreadConstants.WalkSpeed, s_beatDuration15[i], 0.005, Inv($"beat {i + 1} duration at 1.5 m/s (s)"));
                c.Near(s1 / ThreadConstants.WalkSpeed, s_beatCumulative15[i], 0.005, Inv($"beat {i + 1} cumulative end at 1.5 m/s (s)"));
                c.Near(len / ThreadConstants.BriskSpeed, s_beatDuration25[i], 0.005, Inv($"beat {i + 1} duration at 2.5 m/s (s)"));
            }
            c.That(ThreadMath.BeatIndex(ThreadConstants.PathLength) == ThreadConstants.BeatCount - 1, Inv($"BeatIndex(860) = {ThreadMath.BeatIndex(ThreadConstants.PathLength)}, expected 6"));
            c.That(ThreadMath.BeatIndex(-1.0) == 0, "BeatIndex(-1) must clamp to beat 0");
            c.That(ThreadMath.BeatIndex(900.0) == ThreadConstants.BeatCount - 1, "BeatIndex(900) must clamp to the last beat");

            c.Near(ThreadConstants.PathLength / ThreadConstants.WalkSpeed, 573.33, 0.005, "total time at 1.5 m/s (s)");
            c.Near(ThreadConstants.TotalTimeWalk, ThreadConstants.PathLength / ThreadConstants.WalkSpeed, 0.001, "ThreadConstants.TotalTimeWalk");
            c.Near(ThreadConstants.PathLength / ThreadConstants.BriskSpeed, 344.0, 0.005, "total time at 2.5 m/s (s)");

            // key trigger positions (14.4.4 line 632)
            c.Near(ThreadConstants.BankCoreS0, 268.0, 1e-12, "bank core start");
            c.Near(ThreadConstants.BankCoreS1, 302.0, 1e-12, "bank core end");
            c.Near(ThreadConstants.SwapS, 285.0, 1e-12, "swap s");
            c.Near(ThreadConstants.DeckPassS0, 578.0, 1e-12, "cloud deck start s");
            c.Near(ThreadConstants.DeckPassS1, 596.0, 1e-12, "cloud deck end s");
            c.Near(ThreadConstants.BeaconTriggerS, 858.0, 1e-12, "beacon trigger s");
            c.AssertAll();
        }

        // ================================================================================================
        // Corridor half-widths (14.4.3)
        // ================================================================================================

        // oracle: 14.4.3 table (lines 605-615) evaluated at representative s: s, half-width h (m)
        static readonly double[,] s_halfWidths =
        {
            // D-053: the stair walk narrows to 4 m between the walk-line bollards (s 230-255) and widens back on the terrace (322-342)
            { 0, 12.0 }, { 40, 12.0 }, { 79.9, 12.0 }, { 90, 13.0 }, { 100, 14.0 }, { 150, 14.0 }, { 229.9, 14.0 },
            { 242.5, 9.0 }, { 255, 4.0 }, { 280, 4.0 }, { 320, 4.0 }, { 332, 7.0 }, { 342, 10.0 }, { 349.9, 10.0 }, { 375, 5.575 },
            { 400, 1.15 }, { 500, 1.15 }, { 700, 1.15 }, { 840, 1.15 }, { 850, 1.15 }, { 860, 1.15 },
        };

        [Test]
        public void Corridor_HalfWidths()
        {
            var c = new Checks("corridor half-widths (14.4.3, smoothstep transitions)");
            for (int i = 0; i < s_halfWidths.GetLength(0); i++)
                c.Near(ThreadMath.HalfWidth(s_halfWidths[i, 0]), s_halfWidths[i, 1], 1e-9, Inv($"half-width at s = {s_halfWidths[i, 0]:F1}"));
            // continuous: the steepest transition is 14 -> 4 over 25 m (smoothstep peak slope 0.6 m/m)
            double maxStep = 0, maxStepS = 0;
            int n = PathSamples;
            for (int i = 1; i <= n; i++)
            {
                double step = Math.Abs(ThreadMath.HalfWidth(SampleS(i)) - ThreadMath.HalfWidth(SampleS(i - 1)));
                if (step > maxStep) { maxStep = step; maxStepS = SampleS(i); }
            }
            c.That(maxStep <= 0.6 * 0.05 + 1e-6, Inv($"half-width jumps by {maxStep:F4} m within 0.05 m at s = {maxStepS:F2}"));
            c.Near(ThreadConstants.HalfWidthDeck, ThreadConstants.DeckWidth * 0.5 - ThreadConstants.PlayerRadius, 1e-6, "deck half-width = 3.0 / 2 - player radius 0.35");
            c.That(ThreadConstants.HalfWidthAvenue + ThreadConstants.PlayerRadius < ThreadConstants.ColossusInnerEdgeX,
                Inv($"avenue corridor edge {ThreadConstants.HalfWidthAvenue + ThreadConstants.PlayerRadius:F2} m must stay inside the colossi inner edges x = +-{ThreadConstants.ColossusInnerEdgeX:F1}"));
            c.AssertAll();
        }

        // ================================================================================================
        // T-THREAD (14.7.3)
        // ================================================================================================

        // oracle: 14.7.3 table (lines 814-823): s, theta (deg), px at 1080p, px at 4K, r_app (km), bearing (deg), FOV(s).
        // The bearing column and the pixel widths are printed for the TRUE cable bearing atan2(70.028, 400 - s) (line 812).
        // D-026 offsets the APPARENT bearing by 8.6 deg x (1 - smoothstep(90, 240, s)) (the robes of pairs 3-4 hide 9.9 deg
        // in KF1); the width law theta(s) is unchanged, so the shipped widths are checked against the formula re-evaluated at
        // the decided bearing (1.95 px instead of 2.0 at s = 0; 9.78 px instead of 10.2 at 4K, s = 150).
        static readonly double[,] s_threadTable =
        {
            { 0,   0.124, 2.0,  4.0,   46.2, 9.9,  62 },
            { 45,  0.155, 2.5,  5.0,   37.0, 11.2, 62 },
            { 90,  0.201, 3.3,  6.5,   28.4, 12.7, 63 },
            { 150, 0.314, 5.1,  10.2,  18.2, 15.6, 64 },
            { 200, 0.524, 8.9,  17.8,  10.9, 19.3, 64 },
            { 240, 0.952, 17.1, 34.2,  6.0,  23.6, 64 },
            { 265, 1.651, 31.6, 63.2,  3.5,  27.4, 64 },
            { 285, 3.208, 66.4, 132.7, 1.79, 31.3, 64 },
        };

        /// <summary>T-THREAD width acceptance: 0.3 px below 10 px, 3 percent above (14.10.1).</summary>
        static void CheckPixelWidth(Checks c, double actual, double expected, string what)
        {
            if (expected < 10.0) c.Near(actual, expected, 0.3, what);
            else c.NearRel(actual, expected, 0.03, what);
        }

        [Test]
        public void TThread_LawMatchesTable()
        {
            var c = new Checks("T-THREAD (14.7.3: theta within 3 %, width within 0.3 px below 10 px / 3 % above; reference camera yaw 0, pitch 0 on the nominal path; apparent bearing of D-026)");
            for (int i = 0; i < s_threadTable.GetLength(0); i++)
            {
                double s = s_threadTable[i, 0];
                double theta = ThreadMath.ThreadThetaRad(s), beta = ThreadMath.ThreadBearingRad(s), fov = ThreadMath.Fov(s);
                c.NearRel(theta / Deg, s_threadTable[i, 1], 0.03, Inv($"theta({s:F0}) (deg)"));
                // the law as printed: 0.124 deg x (1 - s/320)^-1.47 (unchanged by D-026)
                double thetaLaw = 0.124 * Math.Pow(1.0 - s / 320.0, -1.47);
                c.NearRel(theta / Deg, thetaLaw, 1e-9, Inv($"theta({s:F0}) vs the printed law (deg)"));
                c.NearRel(ThreadMath.ThreadApparentRange(s) / 1000.0, s_threadTable[i, 4], 0.03, Inv($"r_app({s:F0}) (km)"));
                c.NearRel(ThreadMath.ThreadApparentRange(s), 100.0 / theta, 1e-12, Inv($"r_app({s:F0}) = 100 m / theta"));
                // 14.7.3 prints the true cable bearing; D-026 subtracts 8.6 deg x (1 - smoothstep(90, 240, s)), so the apparent
                // bearing equals the printed value only from s = 240 (23.6 / 27.4 / 31.3 deg)
                double trueBeta = TrueThreadBearingRad(s);
                c.Near(trueBeta / Deg, s_threadTable[i, 5], 0.06, Inv($"true cable bearing({s:F0}) (deg, printed to 0.1)"));
                c.Near(beta, DecidedThreadBearingRad(s), 1e-12, Inv($"apparent bearing({s:F0}) vs the D-026 law (rad)"));
                if (s >= ThreadConstants.ThreadBearingOffsetS1)
                    c.Near(beta / Deg, s_threadTable[i, 5], 0.06, Inv($"apparent bearing({s:F0}) equals the printed (true) bearing from s = 240 (deg)"));
                c.Near(fov, s_threadTable[i, 6], 1e-9, Inv($"FOV({s:F0}) (deg)"));
                // the printed widths are the rectilinear formula at the true bearing (the spec's reference geometry)
                CheckPixelWidth(c, ThreadMath.PixelWidth(theta, trueBeta, fov, 1080.0), s_threadTable[i, 2], Inv($"pixel width at 1080p at the true bearing, s = {s:F0} (printed table)"));
                CheckPixelWidth(c, ThreadMath.PixelWidth(theta, trueBeta, fov, 2160.0), s_threadTable[i, 3], Inv($"pixel width at 4K at the true bearing, s = {s:F0} (printed table)"));
                // the shipped width (apparent bearing, measured from the view axis of the reference camera, yaw 0) against the
                // formula re-evaluated with the printed law, the table FOV and the D-026 bearing, with the same T-THREAD rule
                double w1080 = ThreadMath.PixelWidth(theta, beta, fov, 1080.0), w2160 = ThreadMath.PixelWidth(theta, beta, fov, 2160.0);
                double e1080 = ThreadMath.PixelWidth(thetaLaw * Deg, DecidedThreadBearingRad(s), s_threadTable[i, 6], 1080.0);
                double e2160 = ThreadMath.PixelWidth(thetaLaw * Deg, DecidedThreadBearingRad(s), s_threadTable[i, 6], 2160.0);
                CheckPixelWidth(c, w1080, e1080, Inv($"pixel width at 1080p, s = {s:F0} (D-026 bearing)"));
                CheckPixelWidth(c, w2160, e2160, Inv($"pixel width at 4K, s = {s:F0} (D-026 bearing)"));
                // never below the minimum line width of 14.7.2 (1.75 H / 1080 px), so the clamp stays inactive on the reference geometry
                c.That(w1080 >= ThreadConstants.ThreadMinWidthPx, Inv($"width at 1080p, s = {s:F0}: {w1080:F3} px below the {ThreadConstants.ThreadMinWidthPx:F2} px minimum"));
                c.That(w2160 >= ThreadConstants.ThreadMinWidthPx * 2.0, Inv($"width at 4K, s = {s:F0}: {w2160:F3} px below the {ThreadConstants.ThreadMinWidthPx * 2.0:F2} px minimum"));
            }
            // focal lengths quoted in 14.7.3 (899 px at FOV 62, 864 px at FOV 64)
            c.Near(540.0 / Math.Tan(31.0 * Deg), 899.0, 0.5, "focal length at FOV 62 (px)");
            c.Near(540.0 / Math.Tan(32.0 * Deg), 864.0, 0.5, "focal length at FOV 64 (px)");
            // first frame (14.7.3 line 804: "about 2 px", 2.0 at the true bearing 9.9 deg). D-026 puts the line at 1.33 deg, where the
            // same theta0 is 1.95 px: within 0.05 px of the formula at the decided bearing, about 2 px by the T-THREAD rule, and
            // at or above the 1.75 px minimum width at 1080p
            double w0 = ThreadMath.PixelWidth(ThreadMath.ThreadThetaRad(0.0), ThreadMath.ThreadBearingRad(0.0), ThreadMath.Fov(0.0), 1080.0);
            c.Near(w0, ThreadMath.PixelWidth(0.124 * Deg, DecidedThreadBearingRad(0.0), 62.0, 1080.0), 0.05, "first-frame width at 1080p vs the formula at the D-026 bearing (px)");
            c.Near(w0, 2.0, 0.3, "first-frame width at 1080p: about 2 px (14.7.3, T-THREAD 0.3 px)");
            c.That(w0 >= ThreadConstants.ThreadMinWidthPx, Inv($"first-frame width at 1080p {w0:F3} px is below the {ThreadConstants.ThreadMinWidthPx:F2} px minimum (14.7.2)"));
            c.AssertAll();
        }

        [Test]
        public void TThread_AxisGeometryAndSwap()
        {
            var c = new Checks("T-THREAD apparent axis (14.7.3: x_T = r sin beta, z_T = s + r cos beta; D-026 bearing, equal to the true cable bearing from s = 240 and at the swap)");
            for (double s = 0.0; s <= ThreadConstants.SwapS + 1e-9; s += 5.0)
            {
                ThreadMath.ThreadAxis(s, out double xT, out double zT);
                double r = ThreadMath.ThreadApparentRange(s), beta = ThreadMath.ThreadBearingRad(s);
                double dist = Math.Sqrt(xT * xT + (zT - s) * (zT - s));
                c.NearRel(dist, r, 1e-9, Inv($"axis distance from the nominal camera at s = {s:F0}"));
                c.Near(Math.Atan2(xT, zT - s), beta, 1e-9, Inv($"axis bearing seen from the nominal camera at s = {s:F0}"));
                // 14.7.3 (line 812) sets beta = atan2(70.028, 400 - s), the true cable bearing; D-026 subtracts 8.6 deg x
                // (1 - smoothstep(90, 240, s)), so the line stands in the avenue gap in beat 1 and is the true bearing from s = 240
                c.Near(beta, DecidedThreadBearingRad(s), 1e-12, Inv($"bearing follows the D-026 law at s = {s:F0}"));
                double trueBeta = Math.Atan2(ThreadConstants.CableAxisX, ThreadConstants.CableAxisZ - s);
                if (s >= ThreadConstants.ThreadBearingOffsetS1)
                    c.Near(beta, trueBeta, 1e-12, Inv($"bearing equals the true cable bearing at s = {s:F0} (D-026: from s = 240)"));
                else
                    c.That(beta < trueBeta, Inv($"bearing at s = {s:F0} must lie left of the true cable bearing before s = 240 (D-026 offset): {beta / Deg:F4} vs {trueBeta / Deg:F4} deg"));
            }
            // D-026: in beat 1 the line stands 1.3 deg right of centre (the printed 9.9 deg minus the 8.6 deg offset)
            c.Near(ThreadMath.ThreadBearingRad(0.0) / Deg, s_threadTable[0, 5] - ThreadConstants.ThreadBearingOffsetDeg, 0.06, "far-thread bearing at s = 0 (D-026, deg)");
            // swap at s = 285 (14.7.3 item 5): true cable at 134.7 m, bearing 31.3 deg, angular width 43.6 deg
            double sw = ThreadConstants.SwapS;
            double range = Math.Sqrt(ThreadConstants.CableAxisX * ThreadConstants.CableAxisX + (ThreadConstants.CableAxisZ - sw) * (ThreadConstants.CableAxisZ - sw));
            c.Near(range, 134.7, 0.1, "range of the true cable axis at the swap (m)");
            c.Near(2.0 * Math.Asin(ThreadConstants.CableRadius / range) / Deg, 43.6, 0.1, "angular width of the true cable at the swap (deg)");
            c.Near(ThreadMath.ThreadBearingRad(sw) / Deg, 31.3, 0.06, "far-thread bearing at the swap (deg)");
            // D-026: the offset is gone before the swap, so the far thread and the true cable share the bearing exactly
            c.Near(ThreadMath.ThreadBearingRad(sw), Math.Atan2(ThreadConstants.CableAxisX, ThreadConstants.CableAxisZ - sw), 1e-12, "far-thread bearing at the swap equals the true cable bearing (exact swap, rad)");
            // the far thread stays far: about 1.8 km at the swap (table 1.79 km), monotonic approach
            c.That(ThreadMath.ThreadApparentRange(sw) >= 1780.0, Inv($"r_app at the swap = {ThreadMath.ThreadApparentRange(sw):F1} m (14.7.3: kept above about 1.8 km, table 1.79 km)"));
            double prev = double.MaxValue;
            for (double s = 0.0; s <= sw + 1e-9; s += 0.5)
            {
                double r = ThreadMath.ThreadApparentRange(s);
                c.That(r < prev, Inv($"r_app is not strictly decreasing at s = {s:F1}"));
                prev = r;
            }
            c.AssertAll();
        }

        [Test]
        public void TThread_LoomingRateMonotonic()
        {
            var c = new Checks("T-THREAD looming rate (14.7.3: d ln theta / ds = 1.47 / (320 - s), smooth and monotonic)");
            const double h = 0.01;
            double prev = 0;
            for (double s = 0.0; s <= ThreadConstants.SwapS + 1e-9; s += 0.5)
            {
                // one-sided at the swap (the law is frozen after s = 285)
                double hi = Math.Min(s + h, ThreadConstants.SwapS), lo = hi - 2.0 * h;
                double rate = (Math.Log(ThreadMath.ThreadThetaRad(hi)) - Math.Log(ThreadMath.ThreadThetaRad(lo))) / (hi - lo);
                double mid = 0.5 * (hi + lo);
                c.NearRel(rate, 1.47 / (320.0 - mid), 1e-3, Inv($"looming rate at s = {mid:F2} (1/m)"));
                c.That(rate > prev, Inv($"looming rate not increasing at s = {s:F1}: {rate:G6} after {prev:G6}"));
                prev = rate;
            }
            double r0 = 1.47 / 320.0, r285 = 1.47 / (320.0 - 285.0);
            c.Near(r0 * 100.0, 0.46, 0.005, "looming rate at the start (percent per metre)");
            c.Near(r0 * ThreadConstants.WalkSpeed * 100.0, 0.7, 0.05, "looming rate at the start at 1.5 m/s (percent per second)");
            c.Near(r285 * 100.0, 4.2, 0.05, "looming rate at s = 285 (percent per metre)");
            c.AssertAll();
        }

        // ================================================================================================
        // T-CURV and the ascent (14.7.5)
        // ================================================================================================

        // oracle: 14.7.5 table (lines 842-854): s, w, A_virt (m), rho, D, star alpha, planet alpha, dip (deg)
        static readonly double[,] s_ascentTable =
        {
            { 400, 0.000, 0,      1.000, 0.000, 0.00, 0.00, 0.00 },
            { 500, 0.227, 168,    0.98,  0.118, 0.00, 0.00, 0.42 },
            { 540, 0.318, 564,    0.94,  0.291, 0.00, 0.00, 0.76 },
            { 580, 0.409, 1910,   0.80,  0.51,  0.24, 0.00, 1.40 },
            { 597, 0.448, 3230,   0.68,  0.606, 0.52, 0.00, 1.82 },
            { 620, 0.500, 6590,   0.46,  0.728, 0.85, 0.00, 2.61 },
            { 664, 0.600, 25200,  0.05,  0.907, 1.00, 0.00, 5.09 },
            { 700, 0.682, 69600,  0.00,  0.982, 1.00, 0.00, 8.43 },
            { 750, 0.795, 226000, 0.00,  1.0,   1.00, 0.51, 15.04 },
            { 800, 0.909, 487000, 0.00,  1.0,   1.00, 1.00, 21.71 },
            { 840, 1.000, 600000, 0.00,  1.0,   1.00, 1.00, 23.95 },
        };

        [Test]
        public void TCurv_HorizonDip()
        {
            var c = new Checks("T-CURV horizon dip (14.7.5: delta = acos(R / (R + A)) within 0.05 deg of the table)");
            for (int i = 0; i < s_ascentTable.GetLength(0); i++)
            {
                double s = s_ascentTable[i, 0];
                c.Near(ThreadMath.HorizonDipRad(ThreadMath.VirtualAltitude(s)) / Deg, s_ascentTable[i, 7], 0.05, Inv($"dip at s = {s:F0} (deg)"));
            }
            // key shots quote the dip (14.10.2): KF6a at s = 624 (2.77 deg), KF6a2 at s = 664 (5.09 deg)
            c.Near(ThreadMath.HorizonDipRad(ThreadMath.VirtualAltitude(624.0)) / Deg, 2.77, 0.05, "dip at KF6a (s = 624, deg)");
            c.Near(ThreadMath.HorizonDipRad(ThreadMath.VirtualAltitude(664.0)) / Deg, 5.09, 0.05, "dip at KF6a2 (s = 664, deg)");
            c.Near(ThreadMath.HorizonDipRad(0.0), 0.0, 1e-12, "dip at A = 0");
            c.Near(ThreadMath.HorizonDipRad(-50.0), 0.0, 1e-12, "dip below sea level clamps to 0");
            c.AssertAll();
        }

        [Test]
        public void TCurv_LimbCosine()
        {
            var c = new Checks("T-CURV Earth limb (14.7.5: cos psi_limb = sqrt(1 - (R / (R + A))^2))");
            double R = ThreadConstants.EarthRadius;
            c.Near(R, 6371000.0, 1e-6, "Earth radius (m)");
            double[] altitudes = { 0, 1, 168, 1910, 3000, 25200, 69600, 226000, 487000, 600000 };
            double prev = -1;
            for (int i = 0; i < altitudes.Length; i++)
            {
                double A = altitudes[i], k = R / (R + A);
                double cl = ThreadMath.CosPsiLimb(A);
                c.Near(cl, Math.Sqrt(1.0 - k * k), 1e-12, Inv($"cos psi_limb at A = {A:F0} m"));
                c.Near(cl, Math.Sin(ThreadMath.HorizonDipRad(A)), 1e-12, Inv($"cos psi_limb = sin(dip) at A = {A:F0} m"));
                c.Near(Math.Acos(cl) / Deg, 90.0 - ThreadMath.HorizonDipRad(A) / Deg, 1e-7, Inv($"psi_limb = 90 deg - dip at A = {A:F0} m"));
                c.That(cl > prev, Inv($"cos psi_limb not increasing at A = {A:F0} m"));
                prev = cl;
            }
            c.Near(ThreadMath.CosPsiLimb(0.0), 0.0, 1e-12, "cos psi_limb at A = 0 (the limb is the horizontal)");
            c.AssertAll();
        }

        [Test]
        public void Ascent_TableValues()
        {
            var c = new Checks("ascent table (14.7.5: w, A_virt within 2 % or 1 m, rho 0.01, D 0.005, star and planet alpha 0.02)");
            for (int i = 0; i < s_ascentTable.GetLength(0); i++)
            {
                double s = s_ascentTable[i, 0];
                double A = ThreadMath.VirtualAltitude(s);
                c.Near(ThreadMath.AscentW(s), s_ascentTable[i, 1], 0.0006, Inv($"w at s = {s:F0}"));
                double expA = s_ascentTable[i, 2];
                c.Near(A, expA, Math.Max(0.02 * expA, 1.0), Inv($"A_virt at s = {s:F0} (m)"));
                c.Near(ThreadMath.AirDensity(A), s_ascentTable[i, 3], 0.01, Inv($"rho at s = {s:F0}"));
                c.Near(ThreadMath.Darkness(s), s_ascentTable[i, 4], 0.005, Inv($"D at s = {s:F0}"));
                c.Near(ThreadMath.StarAlpha(s), s_ascentTable[i, 5], 0.02, Inv($"star alpha at s = {s:F0}"));
                c.Near(ThreadMath.PlanetAlpha(s), s_ascentTable[i, 6], 0.02, Inv($"planet alpha at s = {s:F0}"));
            }
            // the formulas as printed (line 840), independent of the contract
            for (double s = 400.0; s <= 840.0 + 1e-9; s += 2.0)
            {
                double w = (s - 400.0) / 440.0, e = 3 * w * w - 2 * w * w * w;
                double A = 600000.0 * (Math.Exp(9.0 * e) - 1.0) / (Math.Exp(9.0) - 1.0);
                c.Near(ThreadMath.VirtualAltitude(s), A, 1e-6 * Math.Max(1.0, A), Inv($"A_virt({s:F0}) vs the printed law"));
                double x = ThreadMath.Clamp01((w - 0.03) / 0.75), D = x * x * x * (x * (x * 6 - 15) + 10);
                c.Near(ThreadMath.Darkness(s), D, 1e-12, Inv($"D({s:F0}) vs the printed law"));
            }
            // 14.6 / 14.7.5 statements derived from the same curves
            c.Near(ThreadMath.VirtualAltitude(420.0), 4.0, 0.5, "the ground group sinks from about 4 m at s = 420 (m)");
            c.Near(ThreadMath.Darkness(413.0), 0.0, 1e-12, "the first sky change begins at w = 0.03 (s = 413.2): D(413)");
            c.That(ThreadMath.Darkness(414.0) > 0.0, "D(414) must be above 0 (sky change has begun)");
            c.That(ThreadMath.PlanetAlpha(765.0) > 0.999, Inv($"planet alpha at s = 765 = {ThreadMath.PlanetAlpha(765.0):F5} (14.6: 100 percent at s = 765)"));
            c.Near(ThreadMath.SunAlpha(648.0), 0.0, 1e-9, "the sun is gone from the sky at s = 648 (alpha)");
            c.Near(ThreadMath.VirtualAltitude(860.0), 600000.0, 1e-6, "A_virt on the pier (m)");
            c.Near(ThreadMath.VirtualAltitude(300.0), 0.0, 1e-12, "A_virt before the helix (m)");
            c.AssertAll();
        }

        [Test]
        public void Ascent_MonotoneAndDeckPass()
        {
            var c = new Checks("ascent curves monotone over 0..860, deck pass A = 1.8..3.0 km at s = 578..595 (14.7.5)");
            double pA = -1, pD = -1, pStar = -1, pPlanet = -1, pRho = 2;
            double s18 = double.NaN, s30 = double.NaN;
            int n = PathSamples;
            for (int i = 0; i <= n; i++)
            {
                double s = SampleS(i);
                double A = ThreadMath.VirtualAltitude(s), D = ThreadMath.Darkness(s), st = ThreadMath.StarAlpha(s), pl = ThreadMath.PlanetAlpha(s), rho = ThreadMath.AirDensity(A);
                c.That(A >= pA && D >= pD && st >= pStar && pl >= pPlanet && rho <= pRho,
                    Inv($"ascent curve reverses at s = {s:F2}: A {A:G6}, D {D:G6}, star {st:G6}, planet {pl:G6}, rho {rho:G6}"));
                if (double.IsNaN(s18) && A >= 1800.0) s18 = s;
                if (double.IsNaN(s30) && A >= 3000.0) s30 = s;
                pA = A; pD = D; pStar = st; pPlanet = pl; pRho = rho;
            }
            c.Near(s18, 578.0, 0.5, "s where A_virt reaches 1.8 km (deck bottom)");
            c.Near(s30, 595.0, 0.5, "s where A_virt reaches 3.0 km (deck top)");
            c.AssertAll();
        }

        // ================================================================================================
        // FOV curve (14.12) and exposure schedule (14.6)
        // ================================================================================================

        // oracle: FOV(s) of 14.12 (line 1009) evaluated at the knots and window midpoints: s, FOV (deg)
        static readonly double[,] s_fovKnots =
        {
            { 0, 62 }, { 78, 62 }, { 90, 63 }, { 102, 64 }, { 200, 64 }, { 300, 64 }, { 316, 66 }, { 332, 68 },
            { 336, 68 }, { 340, 68 }, { 370, 66 }, { 400, 64 }, { 480, 64 }, { 560, 64 }, { 580, 65 }, { 600, 66 },
            { 700, 66 }, { 740, 66 }, { 754, 65 }, { 768, 64 }, { 800, 64 }, { 820, 64 }, { 839, 65 }, { 858, 66 }, { 860, 66 },
        };
        // oracle: the six change windows of 14.12 (line 1009)
        static readonly double[,] s_fovWindows = { { 78, 102 }, { 300, 332 }, { 340, 400 }, { 560, 600 }, { 740, 768 }, { 820, 858 } };

        // oracle: exposure plateaus (14.6 lines 766-771, 14.12 line 1028): s, EV
        static readonly double[,] s_exposurePlateaus =
        {
            { 0, 0 }, { 100, 0 }, { 249, 0 }, { 260, -0.2 }, { 300, -0.2 }, { 329, -0.2 }, { 340, 0 }, { 400, 0 }, { 500, 0 },
            { 589, 0 }, { 600, 0.1 }, { 700, 0.1 }, { 745, 0.1 }, { 755, -0.1 }, { 800, -0.1 }, { 860, -0.1 },
        };
        // oracle: exposure windows of 9 m (14.6: 250-259, 330-339, 590-599; beat 7 centred on its boundary 750)
        static readonly double[,] s_exposureWindows = { { 250, 259 }, { 330, 339 }, { 590, 599 }, { 745.5, 754.5 } };

        /// <summary>Maximal intervals of s (scanned in 0.01 m steps) over which f changes.</summary>
        static double[,] ChangeWindows(Func<double, double> f, out int count)
        {
            var w = new double[32, 2];
            count = 0;
            const double h = 0.01;
            int n = (int)Math.Round(ThreadConstants.PathLength / h);
            bool inside = false;
            for (int i = 0; i < n; i++)
            {
                double s = i * h, s1 = (i + 1) * h;
                bool changing = Math.Abs(f(s1) - f(s)) > 1e-12;
                if (changing && !inside) { inside = true; if (count < 32) w[count, 0] = s; }
                if (!changing && inside) { inside = false; if (count < 32) w[count, 1] = s; count++; }
            }
            if (inside) { if (count < 32) w[count, 1] = ThreadConstants.PathLength; count++; }
            return w;
        }

        [Test]
        public void Fov_KnotsAndEasing()
        {
            var c = new Checks("FOV(s) (14.12: one eased curve 62/64/68/64/66/64/66)");
            for (int i = 0; i < s_fovKnots.GetLength(0); i++)
                c.Near(ThreadMath.Fov(s_fovKnots[i, 0]), s_fovKnots[i, 1], 1e-9, Inv($"FOV({s_fovKnots[i, 0]:F0}) (deg)"));
            // eased: zero slope at every window edge
            for (int i = 0; i < s_fovWindows.GetLength(0); i++)
            {
                for (int e = 0; e < 2; e++)
                {
                    double k = s_fovWindows[i, e];
                    double slope = Math.Abs(ThreadMath.Fov(k + 0.01) - ThreadMath.Fov(k - 0.01)) / 0.02;
                    c.That(slope < 1e-3, Inv($"FOV slope at the knot s = {k:F0} is {slope:G4} deg/m (must be eased to 0)"));
                }
            }
            c.AssertAll();
        }

        [Test]
        public void Fov_ChangeWindowsRates()
        {
            var c = new Checks("FOV change windows (14.12: average rate below 1 deg/s at 1.5 and 2.5 m/s, no window shorter than 6 s at 1.5 m/s)");
            Func<double, double> fov = ThreadMath.Fov;
            double[,] w = ChangeWindows(fov, out int count);
            int expected = s_fovWindows.GetLength(0);
            c.That(count == expected, Inv($"found {count} FOV change windows, expected {expected}"));
            double[] speeds = { ThreadConstants.WalkSpeed, ThreadConstants.BriskSpeed };
            for (int i = 0; i < Math.Min(count, 32); i++)
            {
                double a = w[i, 0], b = w[i, 1], len = b - a, dF = Math.Abs(fov(b) - fov(a));
                if (i < expected)
                {
                    c.Near(a, s_fovWindows[i, 0], 0.02, Inv($"FOV window {i} start"));
                    c.Near(b, s_fovWindows[i, 1], 0.02, Inv($"FOV window {i} end"));
                }
                c.That(len / ThreadConstants.WalkSpeed >= 6.0, Inv($"FOV window s {a:F2}..{b:F2} lasts {len / ThreadConstants.WalkSpeed:F2} s at 1.5 m/s (minimum 6 s)"));
                double peak = 0;
                for (double s = a; s < b; s += 0.01) peak = Math.Max(peak, Math.Abs(fov(s + 0.01) - fov(s)) / 0.01);
                for (int k = 0; k < speeds.Length; k++)
                {
                    double v = speeds[k], avg = dF / (len / v);
                    c.That(avg < 1.0, Inv($"FOV window s {a:F2}..{b:F2}: average rate {avg:F3} deg/s at {v:F1} m/s (limit 1 deg/s)"));
                    c.That(peak * v < 1.0, Inv($"FOV window s {a:F2}..{b:F2}: peak rate {peak * v:F3} deg/s at {v:F1} m/s"));
                }
            }
            c.AssertAll();
        }

        [Test]
        public void Exposure_ScheduleContinuity()
        {
            var c = new Checks("exposure schedule (14.6: EV 0 / -0.2 / 0 / +0.1 / -0.1, windows of 9 m = 6 s, continuous)");
            for (int i = 0; i < s_exposurePlateaus.GetLength(0); i++)
                c.Near(ThreadMath.ExposureEV(s_exposurePlateaus[i, 0]), s_exposurePlateaus[i, 1], 1e-9, Inv($"EV at s = {s_exposurePlateaus[i, 0]:F0}"));
            // per-beat values of 14.12 at the beat midpoints of beats 1, 2, 3, 5, 6, 7 (beat 4 ramps -0.2 -> 0 at 330..339)
            var ev = ThreadConstants.ExposureEV;
            int[] beats = { 0, 1, 2, 4, 5, 6 };
            for (int k = 0; k < beats.Length; k++)
            {
                int bi = beats[k];
                double mid = 0.5 * (ThreadConstants.BeatBoundaries[bi] + ThreadConstants.BeatBoundaries[bi + 1]);
                if (bi == 2) mid = 300.0;   // after the 250..259 window
                if (bi == 5) mid = 700.0;   // after the 590..599 window
                c.Near(ThreadMath.ExposureEV(mid), ev[bi], 1e-9, Inv($"EV in beat {bi + 1} (s = {mid:F0}) vs ThreadConstants.ExposureEV"));
            }
            // continuity: steepest window is 0.2 EV over 9 m with smootherstep (peak 0.042 EV/m)
            double maxStep = 0, maxStepS = 0;
            for (double s = 0.0; s < ThreadConstants.PathLength; s += 0.01)
            {
                double step = Math.Abs(ThreadMath.ExposureEV(s + 0.01) - ThreadMath.ExposureEV(s));
                if (step > maxStep) { maxStep = step; maxStepS = s; }
            }
            c.That(maxStep < 1e-3, Inv($"EV jumps by {maxStep:G4} within 0.01 m at s = {maxStepS:F2}"));
            Func<double, double> f = ThreadMath.ExposureEV;
            double[,] w = ChangeWindows(f, out int count);
            int expected = s_exposureWindows.GetLength(0);
            c.That(count == expected, Inv($"found {count} exposure change windows, expected {expected}"));
            for (int i = 0; i < Math.Min(count, expected); i++)
            {
                c.Near(w[i, 0], s_exposureWindows[i, 0], 0.02, Inv($"exposure window {i} start"));
                c.Near(w[i, 1], s_exposureWindows[i, 1], 0.02, Inv($"exposure window {i} end"));
                c.Near(w[i, 1] - w[i, 0], ThreadConstants.ExposureWindow, 0.03, Inv($"exposure window {i} length (m)"));
                c.Near((w[i, 1] - w[i, 0]) / ThreadConstants.WalkSpeed, 6.0, 0.03, Inv($"exposure window {i} duration at 1.5 m/s (s)"));
            }
            c.AssertAll();
        }

        // ================================================================================================
        // Palette (14.2.1) and sky gradient (14.2.1, D-008)
        // ================================================================================================

        // oracle: 14.2.1 palette table (lines 422-439): token, sRGB hex, linear RGB as printed
        struct PalRow
        {
            public Pal id; public string hex; public double r, g, b;
            public PalRow(Pal id, string hex, double r, double g, double b) { this.id = id; this.hex = hex; this.r = r; this.g = g; this.b = b; }
        }

        static readonly PalRow[] s_palette =
        {
            new PalRow(Pal.SALT_WHITE,    "#F2EDE2", 0.8879, 0.8469, 0.7605),
            new PalRow(Pal.SAND,          "#E3C79A", 0.7682, 0.5711, 0.3231),
            new PalRow(Pal.SAND_SHADE,    "#C9A777", 0.5841, 0.3864, 0.1845),
            new PalRow(Pal.CORAL,         "#E8745E", 0.8070, 0.1746, 0.1119),
            new PalRow(Pal.CORAL_DEEP,    "#B9483F", 0.4851, 0.0648, 0.0497),
            new PalRow(Pal.TEAL,          "#2E8C8A", 0.0273, 0.2623, 0.2542),
            new PalRow(Pal.TEAL_DEEP,     "#1B5560", 0.0110, 0.0908, 0.1170),
            new PalRow(Pal.INK,           "#1D2233", 0.0123, 0.0160, 0.0331),
            new PalRow(Pal.CREAM,         "#FBF4E4", 0.9647, 0.9047, 0.7758),
            new PalRow(Pal.SKY_H_DAY,     "#F3E4D0", 0.8963, 0.7758, 0.6308),
            new PalRow(Pal.SKY_M_DAY,     "#BFD9E3", 0.5210, 0.6939, 0.7682),
            new PalRow(Pal.SKY_Z_DAY,     "#6FAFD3", 0.1590, 0.4287, 0.6514),
            new PalRow(Pal.SKY_H_DUSK,    "#F2C9A6", 0.8879, 0.5841, 0.3813),
            new PalRow(Pal.SKY_M_DUSK,    "#7F86B8", 0.2122, 0.2384, 0.4793),
            new PalRow(Pal.SKY_Z_DUSK,    "#2F3A7A", 0.0284, 0.0423, 0.1946),
            new PalRow(Pal.SKY_H_NIGHT,   "#1E2A4F", 0.0130, 0.0232, 0.0782),
            new PalRow(Pal.SKY_M_NIGHT,   "#0A0F26", 0.0030, 0.0048, 0.0194),
            new PalRow(Pal.SKY_Z_NIGHT,   "#04050D", 0.0012, 0.0015, 0.0040),
            new PalRow(Pal.CLOUD_TOP,     "#FFF6EA", 1.0,    0.9216, 0.8228),
            new PalRow(Pal.CLOUD_SHADE,   "#C9B8D6", 0.5841, 0.4793, 0.6724),
            new PalRow(Pal.STATION_MINT,  "#A8DCCB", 0.3916, 0.7157, 0.5972),
            new PalRow(Pal.STATION_PEACH, "#F7C1A4", 0.9301, 0.5333, 0.3712),
            new PalRow(Pal.STATION_LILAC, "#C7B6E2", 0.5711, 0.4678, 0.7605),
            new PalRow(Pal.EARTH_OCEAN,   "#2C6E9E", 0.0252, 0.1559, 0.3419),
            new PalRow(Pal.EARTH_LAND,    "#D9B982", 0.6939, 0.4851, 0.2232),
            new PalRow(Pal.ATMO_RIM,      "#7FC4F0", 0.2122, 0.5520, 0.8714),
            new PalRow(Pal.PLANET_A,      "#E8745E", 0.8070, 0.1746, 0.1119),   // = CORAL
            new PalRow(Pal.PLANET_B,      "#F0D9B0", 0.8714, 0.6939, 0.4342),
            new PalRow(Pal.PLANET_C,      "#2E8C8A", 0.0273, 0.2623, 0.2542),   // = TEAL
            new PalRow(Pal.BEACON,        "#FFD9A0", 1.0,    0.6939, 0.3515),
        };

        static string TableHex(Pal id)
        {
            var t = ThreadPalette.Table;
            for (int i = 0; i < t.Length; i++) if (t[i].id == id) return t[i].hex;
            return null;
        }

        [Test]
        public void Palette_HexToLinearMatchesTable()
        {
            var c = new Checks("palette (14.2.1: HexToLinear of every token within 0.0006 of the printed linear values)");
            for (int i = 0; i < s_palette.Length; i++)
            {
                PalRow row = s_palette[i];
                string hex = TableHex(row.id);
                c.That(hex != null && string.Equals(hex, row.hex, StringComparison.OrdinalIgnoreCase), Inv($"{row.id}: ThreadPalette.Table hex {hex ?? "<missing>"}, spec {row.hex}"));
                Color lin = ThreadPalette.HexToLinear(row.hex);
                c.Near(lin.r, row.r, 0.0006, Inv($"{row.id} {row.hex} linear r"));
                c.Near(lin.g, row.g, 0.0006, Inv($"{row.id} {row.hex} linear g"));
                c.Near(lin.b, row.b, 0.0006, Inv($"{row.id} {row.hex} linear b"));
                c.Near(lin.a, 1.0, 0.0, Inv($"{row.id} alpha"));
                if (hex == null) continue;
                Color tok = ThreadPalette.Linear(row.id);
                Color fromTable = ThreadPalette.HexToLinear(hex);
                c.Near(tok.r, fromTable.r, 1e-7, Inv($"{row.id}: Linear() r vs HexToLinear(Table)"));
                c.Near(tok.g, fromTable.g, 1e-7, Inv($"{row.id}: Linear() g vs HexToLinear(Table)"));
                c.Near(tok.b, fromTable.b, 1e-7, Inv($"{row.id}: Linear() b vs HexToLinear(Table)"));
                // the UnityEngine-free mirror used by FogMath agrees
                c.Near(ThreadPaletteMath.SrgbHexChannelToLinear(hex, 0), fromTable.r, 1e-6, Inv($"{row.id}: ThreadPaletteMath r"));
                c.Near(ThreadPaletteMath.SrgbHexChannelToLinear(hex, 1), fromTable.g, 1e-6, Inv($"{row.id}: ThreadPaletteMath g"));
                c.Near(ThreadPaletteMath.SrgbHexChannelToLinear(hex, 2), fromTable.b, 1e-6, Inv($"{row.id}: ThreadPaletteMath b"));
            }
            c.AssertAll();
        }

        [Test]
        public void Palette_TableCompleteAndAliases()
        {
            var c = new Checks("palette table (every Pal token once, well-formed hex; PLANET_A == CORAL, PLANET_C == TEAL)");
            var t = ThreadPalette.Table;
            var values = (Pal[])Enum.GetValues(typeof(Pal));
            for (int v = 0; v < values.Length; v++)
            {
                int n = 0;
                for (int i = 0; i < t.Length; i++) if (t[i].id == values[v]) n++;
                c.That(n == 1, Inv($"{values[v]} appears {n} times in ThreadPalette.Table"));
            }
            for (int i = 0; i < t.Length; i++)
            {
                string h = t[i].hex;
                bool ok = h != null && h.Length == 7 && h[0] == '#';
                for (int k = 1; ok && k < 7; k++) ok = Uri.IsHexDigit(h[k]);
                c.That(ok, Inv($"{t[i].id}: malformed hex '{h}'"));
            }
            c.That(string.Equals(TableHex(Pal.PLANET_A), TableHex(Pal.CORAL), StringComparison.OrdinalIgnoreCase), "PLANET_A must equal CORAL (14.2.1)");
            c.That(string.Equals(TableHex(Pal.PLANET_C), TableHex(Pal.TEAL), StringComparison.OrdinalIgnoreCase), "PLANET_C must equal TEAL (14.2.1)");
            Color pa = ThreadPalette.Linear(Pal.PLANET_A), co = ThreadPalette.Linear(Pal.CORAL);
            Color pc = ThreadPalette.Linear(Pal.PLANET_C), te = ThreadPalette.Linear(Pal.TEAL);
            c.That(pa == co, "Linear(PLANET_A) == Linear(CORAL)");
            c.That(pc == te, "Linear(PLANET_C) == Linear(TEAL)");
            // the sRGB transfer functions are inverse to each other
            double worst = 0;
            for (int i = 0; i <= 1000; i++)
            {
                double x = i / 1000.0;
                worst = Math.Max(worst, Math.Abs(ThreadPalette.LinearToSrgb(ThreadPalette.SrgbToLinear(x)) - x));
            }
            c.That(worst < 1e-9, Inv($"sRGB round trip error {worst:G3}"));
            c.AssertAll();
        }

        static void Stops(double D, out Pal h, out Pal m, out Pal z)
        {
            if (D < 0.25) { h = Pal.SKY_H_DAY; m = Pal.SKY_M_DAY; z = Pal.SKY_Z_DAY; }
            else if (D < 0.75) { h = Pal.SKY_H_DUSK; m = Pal.SKY_M_DUSK; z = Pal.SKY_Z_DUSK; }
            else { h = Pal.SKY_H_NIGHT; m = Pal.SKY_M_NIGHT; z = Pal.SKY_Z_NIGHT; }
        }

        static double Ch(FogMath.Rgb c, int i) => i == 0 ? c.r : (i == 1 ? c.g : c.b);
        static double Ch(Color c, int i) => i == 0 ? c.r : (i == 1 ? c.g : c.b);

        [Test]
        public void Sky_StopsAtHorizonMiddleZenith()
        {
            var c = new Checks("sky gradient stops (14.2.1: elevation 0 / 25 / 90 deg = H / M / Z; D 0 / 0.5 / 1 = DAY / DUSK / NIGHT)");
            double[] Ds = { 0.0, 0.5, 1.0 };
            double[] dirY = { 0.0, Math.Sin(25.0 * Deg), 1.0 };
            string[] names = { "H", "M", "Z" };
            for (int k = 0; k < Ds.Length; k++)
            {
                Stops(Ds[k], out Pal h, out Pal m, out Pal z);
                Pal[] tok = { h, m, z };
                for (int j = 0; j < 3; j++)
                {
                    FogMath.Rgb sky = FogMath.SkyColor(dirY[j], Ds[k]);
                    Color want = ThreadPalette.Linear(tok[j]);
                    for (int ch = 0; ch < 3; ch++)
                        c.Near(Ch(sky, ch), Ch(want, ch), 2e-6, Inv($"SkyColor(dirY {dirY[j]:F4}, D {Ds[k]:F1}) channel {ch} vs {tok[j]} ({names[j]})"));
                }
            }
            c.Near(ThreadConstants.SkyStopM, 25.0, 1e-12, "middle stop elevation (deg)");
            c.AssertAll();
        }

        [Test]
        public void Sky_MonotoneSegmentsAndBelowHorizon()
        {
            var c = new Checks("sky gradient (14.2.1 / D-008: monotone between the stops, C1 at M where the stops are monotone, horizon colour below the horizon)");
            double yM = Math.Sin(ThreadConstants.SkyStopM * Deg);
            double[] Ds = { 0.0, 0.25, 0.5, 0.75, 1.0 };
            const int steps = 2000;
            for (int k = 0; k < Ds.Length; k++)
            {
                double D = Ds[k];
                FogMath.SkyStops(D, out FogMath.Rgb H, out FogMath.Rgb M, out FogMath.Rgb Z);
                for (int ch = 0; ch < 3; ch++)
                {
                    double h = Ch(H, ch), m = Ch(M, ch), z = Ch(Z, ch);
                    // segment H -> M over dirY 0..sin 25, then M -> Z over sin 25..1
                    for (int seg = 0; seg < 2; seg++)
                    {
                        double y0 = seg == 0 ? 0.0 : yM, y1 = seg == 0 ? yM : 1.0;
                        double a = seg == 0 ? h : m, b = seg == 0 ? m : z, sign = Math.Sign(b - a);
                        double prev = Ch(FogMath.SkyColor(y0, D), ch), lo = Math.Min(a, b) - 1e-12, hi = Math.Max(a, b) + 1e-12;
                        bool mono = true, inRange = true; double where = 0;
                        for (int i = 1; i <= steps; i++)
                        {
                            double y = y0 + (y1 - y0) * i / steps, v = Ch(FogMath.SkyColor(y, D), ch);
                            if ((v - prev) * sign < -1e-12 || (sign == 0 && Math.Abs(v - prev) > 1e-12)) { if (mono) where = y; mono = false; }
                            if (v < lo || v > hi) { if (inRange) where = y; inRange = false; }
                            prev = v;
                        }
                        c.That(mono, Inv($"D {D:F2} channel {ch} segment {(seg == 0 ? "H->M" : "M->Z")}: not monotone (first at dirY {where:F4})"));
                        c.That(inRange, Inv($"D {D:F2} channel {ch} segment {(seg == 0 ? "H->M" : "M->Z")}: leaves the stop range (first at dirY {where:F4})"));
                    }
                    // D-008: C1 in t = sqrt(dirY) at the middle stop when the three stops are monotone in this channel
                    if ((m - h) * (z - m) > 0)
                    {
                        double tM = Math.Sqrt(yM), dt = 1e-5;
                        double left = (m - Ch(FogMath.SkyColor((tM - dt) * (tM - dt), D), ch)) / dt;
                        double right = (Ch(FogMath.SkyColor((tM + dt) * (tM + dt), D), ch) - m) / dt;
                        c.Near(right, left, 1e-3 * Math.Abs(z - h) + 1e-6, Inv($"D {D:F2} channel {ch}: slope in t left/right of the 25 deg stop"));
                    }
                }
                // below the horizon: the horizon colour
                FogMath.Rgb h0 = FogMath.SkyColor(0.0, D);
                double[] below = { -1e-6, -0.1, -0.5, -1.0 };
                for (int i = 0; i < below.Length; i++)
                {
                    FogMath.Rgb v = FogMath.SkyColor(below[i], D);
                    for (int ch = 0; ch < 3; ch++)
                    {
                        c.Near(Ch(v, ch), Ch(h0, ch), 0.0, Inv($"SkyColor(dirY {below[i]:G3}, D {D:F2}) channel {ch} must equal the horizon colour"));
                        c.Near(Ch(v, ch), Ch(H, ch), 1e-12, Inv($"SkyColor(dirY {below[i]:G3}, D {D:F2}) channel {ch} vs the H stop"));
                    }
                }
            }
            c.AssertAll();
        }

        // ================================================================================================
        // T-SHADE first part: analytic cable shadow on the deck (14.7.7, D-004)
        // ================================================================================================

        static double DeckLit(double s)
        {
            D3 p = ThreadMath.PathPoint(s);
            return ThreadMath.CableShadow(p.x, p.z, ThreadMath.SunDirection(s));
        }

        [Test]
        public void TShade_DeckCableShadow()
        {
            var c = new Checks("T-SHADE cable shadow on the deck centre (14.7.7: lit factor 0 at s = 664, 1 at s = 600, lit -> shade at 631 +-3, shade -> lit at 743 +-3)");
            c.That(DeckLit(664.0) < 0.01, Inv($"lit factor at s = 664 is {DeckLit(664.0):F4} (deck in the cable shadow, expected 0)"));
            c.That(DeckLit(600.0) > 0.99, Inv($"lit factor at s = 600 is {DeckLit(600.0):F4} (deck in the sun, expected 1)"));

            // the sun's azimuth is constant, so the shadow direction is azimuth 145 deg throughout
            for (double s = 0.0; s <= ThreadConstants.PathLength; s += 10.0)
            {
                D3 sun = ThreadMath.SunDirection(s);
                c.Near(Math.Atan2(sun.x, sun.z) / Deg, ThreadConstants.SunAzimuthDeg, 1e-9, Inv($"sun azimuth at s = {s:F0} (deg)"));
                c.Near(sun.Length, 1.0, 1e-12, Inv($"|sun| at s = {s:F0}"));
            }
            c.Near(ThreadConstants.SunAzimuthDeg + 180.0, 145.0, 1e-12, "shadow azimuth (deg)");

            // crossings of the 0.5 level along the deck centre over the helix
            int down = 0, up = 0;
            double sDown = double.NaN, sUp = double.NaN;
            const double h = 0.01;
            double prev = DeckLit(ThreadConstants.HelixStartS);
            for (double s = ThreadConstants.HelixStartS + h; s <= ThreadConstants.HelixEndS + 1e-9; s += h)
            {
                double v = DeckLit(s);
                c.That(IsFinite(v) && v >= 0.0 && v <= 1.0, Inv($"lit factor at s = {s:F2} is {v:G6}"));
                if (prev >= 0.5 && v < 0.5) { down++; sDown = s - h + h * (prev - 0.5) / (prev - v); }
                if (prev < 0.5 && v >= 0.5) { up++; sUp = s - h + h * (0.5 - prev) / (v - prev); }
                prev = v;
            }
            c.That(down == 1 && up == 1, Inv($"expected exactly one lit -> shade and one shade -> lit transition on the helix, found {down} and {up}"));
            c.Near(sDown, 631.0, 3.0, "lit -> shade transition (s where the lit factor crosses 0.5)");
            c.Near(sUp, 743.0, 3.0, "shade -> lit transition (s where the lit factor crosses 0.5)");
            // psi of the shade interval printed in 14.7.7: 189.4 .. 280.6 deg
            c.Near((sDown - ThreadConstants.HelixStartS) / ThreadConstants.HelixRadius / Deg, 189.4, 0.5, "psi at the lit -> shade transition (deg)");
            c.Near((sUp - ThreadConstants.HelixStartS) / ThreadConstants.HelixRadius / Deg, 280.6, 0.5, "psi at the shade -> lit transition (deg)");
            // on the sun side of the cable (t < 0) the deck is always lit
            c.Near(DeckLit(500.0), 1.0, 1e-12, "lit factor at s = 500 (sun side)");
            Log(Inv($"T-SHADE: lit -> shade at s = {sDown:F2}, shade -> lit at s = {sUp:F2}"));
            c.AssertAll();
        }

        // ================================================================================================
        // Constants of table 14.12 (and the scalar facts of 14.4 / 14.7) mirrored in ThreadConstants
        // ================================================================================================

        [Test]
        public void Constants_Table1412()
        {
            var c = new Checks("ThreadConstants vs table 14.12");
            // player / timing
            c.Near(ThreadConstants.EyeHeight, 1.7, 1e-6, "eye height");
            c.Near(ThreadConstants.WalkSpeed, 1.5, 1e-9, "walk speed");
            c.Near(ThreadConstants.BriskSpeed, 2.5, 1e-9, "brisk speed");
            c.Near(ThreadConstants.StepLength, 0.78, 1e-6, "step length");
            c.Near(ThreadConstants.Hysteresis, 12.0, 1e-12, "hysteresis");
            c.Near(ThreadConstants.BlendWindow, 24.0, 1e-12, "blend window");
            c.Near(ThreadConstants.ExposureWindow, 9.0, 1e-12, "exposure window");
            c.Near(ThreadConstants.CameraNear, 0.1, 1e-6, "camera near");
            c.Near(ThreadConstants.CameraFar, 250000.0, 1e-3, "camera far");
            c.That(ThreadConstants.Msaa == 4, "MSAA 4x");
            c.Near(ThreadConstants.DitherLsb, 1.0, 1e-12, "dither LSB");
            c.Near(ThreadConstants.DitherDarkGain, 1.5, 1e-12, "dither gain for D > 0.5");
            c.That(ThreadConstants.DitherTile == 64, "dither tile 64");
            c.Near(ThreadConstants.HalfWidthSalt, 12.0, 1e-12, "half-width salt");
            c.Near(ThreadConstants.HalfWidthAvenue, 14.0, 1e-12, "half-width avenue");
            c.Near(ThreadConstants.HalfWidthStairs, 10.0, 1e-12, "half-width stairs");
            c.Near(ThreadConstants.HalfWidthDeck, 1.15, 1e-12, "half-width deck");
            // stairs and terrace
            c.Near(ThreadConstants.StairStartZ, 240.0, 1e-12, "stairs start z");
            c.Near(ThreadConstants.StairEndZ, 320.0, 1e-12, "stairs end z");
            c.That(ThreadConstants.StairTreads == 200, "200 treads");
            c.Near(ThreadConstants.TreadDepth, 0.40, 1e-12, "tread");
            c.Near(ThreadConstants.RiserHeight, 0.18, 1e-12, "riser");
            c.Near(ThreadConstants.StairWidth, 40.0, 1e-12, "stair width");
            c.Near(ThreadConstants.StairRise, 36.0, 1e-12, "stair rise");
            c.Near(ThreadConstants.StairTreads * ThreadConstants.RiserHeight, ThreadConstants.StairRise, 1e-9, "treads x riser = rise");
            c.Near(ThreadConstants.StairTreads * ThreadConstants.TreadDepth, ThreadConstants.StairEndZ - ThreadConstants.StairStartZ, 1e-9, "treads x tread = run");
            c.Near(ThreadConstants.StairSlope, ThreadConstants.RiserHeight / ThreadConstants.TreadDepth, 1e-12, "slope = riser / tread = 0.45");
            c.Near(ThreadConstants.TerraceXMin, -60.0, 1e-12, "terrace x min");
            c.Near(ThreadConstants.TerraceXMax, 190.0, 1e-12, "terrace x max");
            c.Near(ThreadConstants.TerraceZMin, 320.0, 1e-12, "terrace z min");
            c.Near(ThreadConstants.TerraceZMax, 520.0, 1e-12, "terrace z max");
            c.Near(ThreadConstants.TerraceY, 36.0, 1e-12, "terrace top y");
            // colossi
            double[] pairs = { 144, 192, 240, 288 };
            c.That(ThreadConstants.ColossusPairZ.Length == pairs.Length, "4 colossus pairs");
            for (int i = 0; i < Math.Min(pairs.Length, ThreadConstants.ColossusPairZ.Length); i++) c.Near(ThreadConstants.ColossusPairZ[i], pairs[i], 1e-12, Inv($"colossus pair {i + 1} z"));
            c.Near(ThreadConstants.ColossusCentreX, 50.0, 1e-12, "colossus centre x");
            c.Near(ThreadConstants.ColossusInnerEdgeX, 28.0, 1e-12, "colossus inner edge x");
            c.Near(ThreadConstants.ColossusFootprintX, 22.0, 1e-12, "colossus footprint x");
            c.Near(ThreadConstants.ColossusFootprintZ, 18.0, 1e-12, "colossus footprint z");
            // the footprint scales the lathe radius (14.5.2 "footprint scaled 22 x 18 (x:z)"), so it is the half-extent
            c.Near(ThreadConstants.ColossusCentreX - ThreadConstants.ColossusFootprintX, ThreadConstants.ColossusInnerEdgeX, 1e-9, "centre - footprint radius = inner edge");
            c.Near(ThreadConstants.ColossusHeightMin, 282.0, 1e-12, "colossus height min");
            c.Near(ThreadConstants.ColossusHeightMax, 318.0, 1e-12, "colossus height max");
            c.Near(ThreadConstants.DoorHeight, 2.0, 1e-12, "door height");
            c.Near(ThreadConstants.DoorWidth, 1.1, 1e-12, "door width");
            c.Near(ThreadConstants.ColossusShadowLength, 300.0 / Math.Tan(24.0 * Deg), 0.5, "ground shadow length 300 / tan 24 deg");
            c.Near(ThreadConstants.ShadeBandZ0, 42.0, 1e-12, "shade band z0");
            c.Near(ThreadConstants.ShadeBandZ1, 250.0, 1e-12, "shade band z1");
            // bank
            // 14.12 (line 1016): peak density 0.11 /m (V 35 m); D-030 (0.16) was reverted after critic round 1
            c.Near(ThreadConstants.BankDensity, 0.11, 1e-12, "bank peak density (14.12: 0.11)");
            c.Near(3.912 / ThreadConstants.BankDensity, 35.0, 0.6, "bank visibility V = 3.912 / density (m) (14.12: 35)");
            c.Near(ThreadConstants.BankZ0, 225.0, 1e-12, "bank z0");
            c.Near(ThreadConstants.BankZ1, 265.0, 1e-12, "bank z1");
            c.Near(ThreadConstants.BankZ2, 305.0, 1e-12, "bank z2");
            c.Near(ThreadConstants.BankZ3, 345.0, 1e-12, "bank z3");
            // 14.12 (line 1016) says x profile exp(-((x - x_c)/90)^2); D-025 uses the flat-topped exp(-((x - x_c)/220)^4)
            c.Near(ThreadConstants.BankSigmaX, 220.0, 1e-12, "bank x scale (14.12: 90; D-025: 220)");
            c.Near(ThreadMath.BankG(3.5 + 220.0, 3.5), Math.Exp(-1.0), 1e-12, "bank x profile at x_c + 220 (D-025: exp(-1))");
            c.Near(ThreadMath.BankG(3.5 - 110.0, 3.5), Math.Exp(-1.0 / 16.0), 1e-12, "bank x profile at x_c - 110 (D-025 exponent 4: exp(-1/16), flat top)");
            c.Near(ThreadMath.BankG(ThreadConstants.BankBoxXMax, 0.0), 0.03, 0.005, "bank x profile at the box edge x = 300 (D-025: still falls to 0.03)");
            // 14.7.6 (line 860) writes smoothstep(58, 62, y) (half-edge 2 m) and names 8 m transitions as the T-BANK remedy; D-011 takes it
            c.Near(ThreadConstants.BankSlabHalfEdge, 4.0, 1e-12, "bank slab half-edge (14.7.6: 2 m; D-011: 4 m, 8 m transitions)");
            double[] tops = { 60, 70, 80, 90 };
            for (int i = 0; i < tops.Length; i++)
            {
                c.Near(ThreadConstants.BankSlabTop0 + ThreadConstants.BankSlabTopStep * i, tops[i], 1e-12, Inv($"bank slab top {i}"));
                if (i < ThreadConstants.BankSlabTops.Length) c.Near(ThreadConstants.BankSlabTops[i], tops[i], 1e-12, Inv($"BankSlabTops[{i}]"));
            }
            c.Near(ThreadConstants.BankBoxXMin, -300.0, 1e-12, "bank box x min");
            c.Near(ThreadConstants.BankBoxXMax, 300.0, 1e-12, "bank box x max");
            c.Near(ThreadConstants.BankBoxZMin, 200.0, 1e-12, "bank box z min");
            c.Near(ThreadConstants.BankBoxZMax, 380.0, 1e-12, "bank box z max");
            c.Near(ThreadConstants.BankBoxYMin, 0.0, 1e-12, "bank box y min");
            c.Near(ThreadConstants.BankBoxYMax, 100.0, 1e-12, "bank box y max");
            c.That(ThreadConstants.BankSamples == 16, "16 fixed bank samples");
            // thread law
            c.Near(ThreadConstants.ThreadTheta0Deg, 0.124, 1e-12, "theta0");
            c.Near(ThreadConstants.ThreadS, 320.0, 1e-12, "S");
            c.Near(ThreadConstants.ThreadQ, 1.47, 1e-12, "q");
            c.Near(ThreadConstants.ThreadHazeCap, 0.74, 1e-12, "haze cap");
            c.Near(ThreadConstants.ThreadMinWidthPx, 1.75, 1e-12, "min width 1.75 H / 1080 px");
            c.Near(ThreadConstants.ThreadMinWidthFloorPx, 1.5, 1e-12, "min width floor 1.5 px");
            // 14.7.3 (line 812) has no bearing offset; D-026 subtracts 8.6 deg x (1 - smoothstep(90, 240, s)), gone before the swap
            c.Near(ThreadConstants.ThreadBearingOffsetDeg, 8.6, 1e-12, "far-thread bearing offset (D-026, deg)");
            c.Near(ThreadConstants.ThreadBearingOffsetS0, 90.0, 1e-12, "bearing offset fade start s (D-026)");
            c.Near(ThreadConstants.ThreadBearingOffsetS1, 240.0, 1e-12, "bearing offset fade end s (D-026)");
            c.That(ThreadConstants.ThreadBearingOffsetS1 <= ThreadConstants.SwapS, Inv($"bearing offset must be gone by the swap (D-026): ends at s = {ThreadConstants.ThreadBearingOffsetS1:F0}, swap at {ThreadConstants.SwapS:F0}"));
            // cable and walkway
            c.Near(ThreadConstants.CableAxisX, 70.028, 0.02, "cable axis x");
            // 14.4.2 (line 595) prints 70.028 "(= 440 / 2 pi)"; D-010 uses the exact value so one turn closes (T-PATH < 1 mm)
            c.Near(ThreadConstants.CableAxisX, 440.0 / (2.0 * Math.PI), 1e-12, "cable axis x = 440 / (2 pi) exactly (D-010)");
            c.Near(ThreadConstants.CableAxisZ, 400.0, 1e-12, "cable axis z");
            c.Near(ThreadConstants.CableAxisX, ThreadConstants.HelixCentreX, 1e-12, "cable axis = helix centre (x)");
            c.Near(ThreadConstants.CableAxisZ, ThreadConstants.HelixCentreZ, 1e-12, "cable axis = helix centre (z)");
            c.Near(ThreadConstants.HelixCentreX, ThreadConstants.HelixRadius, 1e-12, "helix starts at x = 0 (centre x = R_h)");
            c.Near(ThreadConstants.CableRadius, 50.0, 1e-12, "cable radius");
            c.Near(ThreadConstants.CableRidgeCrest, 51.2, 1e-12, "ridge crest radius");
            c.That(ThreadConstants.CableStrands == 36, "36 strands");
            c.Near(ThreadConstants.CablePitch, 600.0, 1e-12, "strand pitch");
            c.Near(ThreadConstants.CableBandSpacing, 50.0, 1e-12, "band spacing");
            c.Near(ThreadConstants.CableBandHeight, 10.0, 1e-12, "band height");
            c.Near(ThreadConstants.CollarRadius, 52.5, 1e-12, "collar radius (14.12 constant, kept)");
            c.Near(ThreadConstants.CollarTube, 1.4, 1e-12, "collar tube");
            c.Near(ThreadConstants.CollarSpacing, 100.0, 1e-12, "collar spacing");
            // 14.5.4 (line 694) builds the collar torus at R 52.5; D-018 seats it on the smooth r = 50 cylinder instead:
            // major radius 50.9 = 50 - 0.5 embed + 1.4 tube (inner 49.5); the printed 52.5 would float 1.1 m off the cable
            c.Near(CableBuilder.CollarMajorRadius, 50.9, 1e-9, "built collar major radius (14.5.4: 52.5; D-018: 50.9)");
            c.Near(CableBuilder.CollarMajorRadius - ThreadConstants.CollarTube, ThreadConstants.CableRadius - ThreadConstants.CollarEmbed, 1e-9, "built collar inner radius = cable radius - embed 0.5 (D-018: 49.5)");
            c.Near(ThreadConstants.HelixRadius, 70.028, 0.02, "walkway centre-line radius R_h (T-SCALE-4 +-0.02)");
            // 14.4.2 (line 595) prints R_h = 70.028 "(= 440 / 2 pi)"; D-010 uses the exact value (70.028175: with 70.028 PathPoint jumps 1.1 mm at s = 840)
            c.Near(ThreadConstants.HelixRadius, 440.0 / (2.0 * Math.PI), 1e-12, "R_h = 440 / (2 pi) exactly (D-010)");
            c.Near(ThreadConstants.HelixCentreX, 440.0 / (2.0 * Math.PI), 1e-12, "helix centre x = 440 / (2 pi) exactly (D-010)");
            c.Near(ThreadConstants.DeckWidth, 3.0, 1e-12, "walkway width");
            c.Near(ThreadConstants.HelixRadius - ThreadConstants.DeckWidth * 0.5, 68.528, 0.02, "deck inner edge radius");
            c.Near(ThreadConstants.HelixRadius - ThreadConstants.DeckWidth * 0.5 - ThreadConstants.CableRidgeCrest, 17.3, 0.05, "gap between ridge crest and deck inner edge");
            c.Near(ThreadConstants.RailHeight, 1.0, 1e-12, "rail height");
            c.Near(ThreadConstants.PostSpacing, 2.0, 1e-12, "post spacing");
            c.Near(ThreadConstants.ArmSpacing, 11.0, 1e-12, "arm spacing");
            c.That(ThreadConstants.ArmCountHelix == 40 && ThreadConstants.ArmCountPier == 3, "43 arms (40 helix + 3 pier)");
            c.Near(ThreadConstants.GradeBlendStart, 396.0, 1e-12, "grade blend start");
            c.Near(ThreadConstants.GradeBlendEnd, 404.0, 1e-12, "grade blend end");
            c.Near(ThreadConstants.HelixRisePerRad, ThreadConstants.HelixRise / (2.0 * Math.PI), 1e-3, "rise per radian 110 / (2 pi)");
            // beads, pier, pad, beacon
            c.That(ThreadConstants.BeadCount == 9, "9 beads");
            // 14.12 (line 1020) / 14.5.6 / T-SCALE-5: y_k = 66 + 32 (k - 1); D-023 shifts the column up one slot: y_k = 98 + 32 (k - 1)
            c.Near(ThreadConstants.BeadY0, 230.0, 1e-12, "bead 1 y (14.12: 66; D-035: 230)");
            c.Near(ThreadConstants.BeadYStep, 40.0, 1e-12, "bead spacing (14.12: 32; D-035: 40)");
            c.Near(ThreadConstants.BeadY0 + ThreadConstants.BeadYStep * (ThreadConstants.BeadCount - 1), 550.0, 1e-12, "bead 9 y (D-035: 230 + 40 x 8)");
            // D-035: the whole bead column lies above the pier (y = 146): no bead equator is passed by the deck
            c.That(ThreadConstants.BeadY0 - ThreadConstants.BeadHalfHeight > ThreadConstants.TopY, "lowest bead (S1) bottom above the pier (D-035)");
            c.Near(ThreadConstants.BeadRadius, 62.0, 1e-12, "bead radius");
            c.Near(ThreadConstants.BeadHalfHeight, 12.0, 1e-12, "bead half-height");
            c.Near(ThreadConstants.WindowW, 0.9, 1e-12, "window width");
            c.Near(ThreadConstants.WindowH, 1.2, 1e-12, "window height");
            c.Near(ThreadConstants.PadCentreZ, 424.0, 1e-12, "pad centre z");
            c.Near(ThreadConstants.PadRadius, 3.0, 1e-12, "pad radius");
            c.Near(ThreadConstants.PadThickness, 0.6, 1e-12, "pad thickness");
            c.Near(ThreadConstants.BeaconPillarHeight, 1.6, 1e-12, "beacon pillar (D-051; spec 1.2)");
            c.Near(ThreadConstants.BeaconCrystalHeight, 0.5, 1e-12, "beacon crystal");
            c.Near(ThreadConstants.BeaconEmissive, 6.0, 1e-12, "beacon emissive");
            // ascent, atmosphere, Earth
            c.Near(ThreadConstants.AscentMaxAltitude, 600000.0, 1e-9, "A_max (m)");
            c.Near(ThreadConstants.AscentExp, 9.0, 1e-12, "ascent exponent");
            c.Near(ThreadConstants.ScaleHeight, 8500.0, 1e-9, "scale height (m)");
            c.Near(ThreadConstants.CloudTop, 3000.0, 1e-9, "cloud top / deck top (m)");
            c.Near(ThreadConstants.CloudBottom, 1800.0, 1e-9, "deck bottom (m)");
            c.Near(ThreadConstants.DeckDensity, 0.09, 1e-12, "deck density");
            // 14.5.6 (line 726) / 14.7.6 item 3 (line 860): the top 150 m tapered over three stacked slabs; D-031 widens the
            // top skin to 3 x 200 m = 600 m (ThreadConstants: the emergence took about 1 s); the 100 m bottom skin is D-006's
            c.Near(ThreadConstants.DeckSkin, 600.0, 1e-12, "deck top skin (14.7.6: 150 m; D-031: 600 m)");
            c.Near(ThreadConstants.DeckTopSkinStep, 200.0, 1e-12, "deck top sub-slab width (D-031: 200 m)");
            c.Near(3.0 * ThreadConstants.DeckTopSkinStep, ThreadConstants.DeckSkin, 1e-12, "three stacked sub-slabs tile the top skin (14.7.6 item 3)");
            c.Near(ThreadConstants.DeckBottomSkin, 100.0, 1e-12, "deck bottom skin (D-006: 100 m)");
            c.That(ThreadConstants.DeckSkin + ThreadConstants.DeckBottomSkin <= ThreadConstants.CloudTop - ThreadConstants.CloudBottom,
                Inv($"deck skins {ThreadConstants.DeckBottomSkin:F0} + {ThreadConstants.DeckSkin:F0} m must fit inside the {ThreadConstants.CloudTop - ThreadConstants.CloudBottom:F0} m slab"));
            c.Near(ThreadConstants.CloudCoverageA0, 40.0, 1e-12, "cloud coverage A0 (km)");
            c.Near(ThreadConstants.CloudCoverageA1, 200.0, 1e-12, "cloud coverage A1 (km)");
            // sun, planet, shadow band
            c.Near(ThreadConstants.SunAzimuthDeg, -35.0, 1e-12, "sun azimuth");
            c.Near(ThreadConstants.SunDiscRadiusDeg, 0.9, 1e-12, "sun disc radius");
            c.Near(ThreadConstants.SunDiscGain, 1.3, 1e-12, "sun disc gain");
            c.Near(ThreadConstants.PlanetAzimuthDeg, -32.0, 1e-12, "planet azimuth");
            c.Near(ThreadConstants.PlanetElevationDeg, 12.0, 1e-12, "planet elevation");
            c.Near(ThreadConstants.PlanetRadiusDeg, 14.0, 1e-12, "planet angular radius");
            c.Near(ThreadConstants.PlanetTiltDeg, 18.0, 1e-12, "planet tilt");
            c.Near(ThreadConstants.PlanetAlphaA0, 150.0, 1e-12, "planet alpha A0 (km)");
            c.Near(ThreadConstants.PlanetAlphaA1, 300.0, 1e-12, "planet alpha A1 (km)");
            c.Near(ThreadConstants.ShadowBandHalfWidth, 50.0, 1e-12, "shadow band half-width");
            c.Near(ThreadConstants.ShadowBandOpacity, 0.8, 1e-12, "shadow band opacity (D-045; spec 0.62)");
            c.Near(ThreadConstants.ShadowBandFade0, 12000.0, 1e-9, "shadow band fade start (m)");
            c.Near(ThreadConstants.ShadowBandFade1, 30000.0, 1e-9, "shadow band fade end (m)");
            c.Near(ThreadConstants.ShadowBandPenumbra, 0.0033, 1e-12, "shadow band penumbra");
            c.Near(ThreadConstants.ShadowBandColourGain, 0.78, 1e-12, "shadow band colour gain");
            c.Near(ThreadConstants.DeckShadowN0, 42.0, 1e-12, "deck shadow penumbra n0");
            c.Near(ThreadConstants.DeckShadowN1, 58.0, 1e-12, "deck shadow penumbra n1");
            // fog and exposure per beat
            double[] V = { 1600, 2400, 2400, 3000, 6000, 40000, 0 };
            double[] Hf = { 900, 900, 900, 900, 8500, 8500 };
            double[] EV = { 0, 0, -0.2, -0.1, 0, 0.1, -0.1 };
            for (int i = 0; i < 7; i++)
            {
                c.Near(ThreadConstants.FogVisibility[i], V[i], 1e-9, Inv($"fog V beat {i + 1} (0 = none)"));
                if (i < Hf.Length) c.Near(ThreadConstants.FogHeight[i], Hf[i], 1e-9, Inv($"fog H_f beat {i + 1}"));
                c.Near(ThreadConstants.ExposureEV[i], EV[i], 1e-12, Inv($"exposure EV beat {i + 1}"));
            }
            // audio
            c.Near(ThreadConstants.DroneF1, 36.71, 1e-12, "drone f1");
            c.Near(ThreadConstants.DroneF2, 55.00, 1e-12, "drone f2");
            c.Near(ThreadConstants.HumBase, 27.5, 1e-12, "hum base");
            // 14.3.1 (line 513) writes 0.89 tanh(x / 0.89) "(ceiling about -1.5 dBFS)", but 0.89 is -1.01 dBFS; D-012 follows the
            // -1.5 dBFS of 14.12 and T-AUDIO with 0.84 (-1.51 dBFS)
            c.Near(ThreadConstants.LimiterCeiling, 0.84, 1e-12, "limiter ceiling (14.3.1: 0.89; D-012: 0.84)");
            double ceilingDb = 20.0 * Math.Log10(ThreadConstants.LimiterCeiling);
            c.Near(ceilingDb, -1.51, 0.005, "limiter ceiling (D-012: -1.51 dBFS)");
            c.That(ceilingDb <= -1.5 + 1e-6, Inv($"limiter ceiling {ThreadConstants.LimiterCeiling:G4} linear = {ceilingDb:F2} dBFS, table 14.12 asks for -1.5 dBFS (= {Math.Pow(10.0, -1.5 / 20.0):F4} linear) and T-AUDIO for sample peaks below -1.5 dBFS"));
            double[] rms = { -38, -32, -28, -26, -29, -30, -36 };
            for (int i = 0; i < rms.Length; i++) c.Near(ThreadConstants.TargetRmsDb[i], rms[i], 1e-12, Inv($"RMS target beat {i + 1} (dBFS)"));
            // title / ending (14.11)
            c.Near(ThreadConstants.TitleIn0, 1.0, 1e-12, "title fade-in start");
            c.Near(ThreadConstants.TitleOut1, 5.0, 1e-12, "title fade-out end");
            c.Near(ThreadConstants.HintStart, 6.0, 1e-12, "hint start");
            c.Near(ThreadConstants.HintDuration, 6.0, 1e-12, "hint duration");
            c.Near(ThreadConstants.EndFade, 4.0, 1e-12, "ending fade");
            c.Near(ThreadConstants.EndLineOut1, 14.0, 1e-12, "ending length");
            c.That(ThreadConstants.EndText == "the thread goes on", "ending text");
            c.That(ThreadConstants.TitleText == "THREAD", "title text");
            c.Near(4.0 / ThreadConstants.WalkSpeed, 2.67, 0.005, "stop ramp T = 4 / v0 at 1.5 m/s (s)");
            c.AssertAll();
        }
    }
}
