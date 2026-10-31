using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Path, timeline and lift-curve checks of 14.4 / 14.5.5 / 14.10 (numeric scale checks), the AutoWalker plan
    /// (autotest duration 658 +/- 8 s, ride start 308.4 +/- 1 s, cinematic slowdowns 12 s), the ride's deck choreography and
    /// gaze (R1-04 / R1-05 / R1-25 / R1-35 / R1-36), and the humanoid player-shadow proxy (R1-02).
    /// </summary>
    public sealed class PathTests
    {
        const float Eps = 1e-3f;

        // ---------------------------------------------------------------- polyline

        [Test]
        public void PolylineLength_Is80254()
        {
            Assert.That(PathGuide.PolylineLength, Is.EqualTo(AbyssConst.POLYLINE_LENGTH).Within(0.05f));
            Assert.That(PathGuide.EndD, Is.EqualTo(AbyssConst.POLYLINE_LENGTH).Within(0.05f));
        }

        [Test]
        public void ArcLength_Is7854()
        {
            Assert.That(PathGuide.ArcLength, Is.EqualTo(AbyssConst.ARC_LENGTH).Within(0.05f));
            Assert.That(AbyssConst.WD[8] - AbyssConst.WD[6], Is.EqualTo(AbyssConst.ARC_LENGTH).Within(0.05f));
        }

        [Test]
        public void WaypointDistances_MatchGeometry()
        {
            var W = AbyssConst.W;
            var WD = AbyssConst.WD;
            for (int i = 0; i + 1 < W.Length; i++)
            {
                if (i == 6 || i == 7 || i == 12) continue;   // arc halves and the lift (zero length in d)
                float h = new Vector2(W[i + 1].x - W[i].x, W[i + 1].z - W[i].z).magnitude;
                Assert.That(WD[i + 1] - WD[i], Is.EqualTo(h).Within(0.01f), "segment W" + i + "-W" + (i + 1));
            }
            Assert.That(WD[13], Is.EqualTo(WD[12]), "the lift has zero length in d");
        }

        [Test]
        public void FloorPoint_HitsEveryWaypoint()
        {
            var W = AbyssConst.W;
            var WD = AbyssConst.WD;
            for (int i = 0; i < W.Length; i++)
            {
                bool after = i >= 13;
                Vector3 p = PathGuide.FloorPoint(WD[i], after);
                Assert.That((p - W[i]).magnitude, Is.LessThan(0.01f), "W" + i + " at d=" + WD[i] + " gave " + p);
            }
        }

        [Test]
        public void Arc_FollowsC1WithLinearDrop()
        {
            var c = AbyssConst.ARC_CENTRE;
            for (int k = 0; k <= 100; k++)
            {
                float d = Mathf.Lerp(AbyssConst.WD[6], AbyssConst.WD[8], k / 100f);
                Vector3 p = PathGuide.FloorPoint(d);
                float r = new Vector2(p.x - c.x, p.z - c.y).magnitude;
                Assert.That(r, Is.EqualTo(AbyssConst.ARC_RADIUS).Within(1e-3f));
                Assert.That(p.y, Is.EqualTo(-3f - AbyssConst.LEDGE_DROP * k / 100f).Within(1e-3f));
            }
            Vector3 w7 = PathGuide.FloorPoint(AbyssConst.WD[7]);
            Assert.That((w7 - AbyssConst.W[7]).magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void Tangent_IsHorizontalUnitAndHeadingsAreRight()
        {
            for (float d = 0f; d <= PathGuide.EndD; d += 0.5f)
            {
                Vector3 t = PathGuide.Tangent(d, d > AbyssConst.D_LIFT);
                Assert.That(t.y, Is.EqualTo(0f));
                Assert.That(t.magnitude, Is.EqualTo(1f).Within(1e-4f));
            }
            Assert.That(PathGuide.HeadingYaw(10f), Is.EqualTo(0f).Within(0.01f));              // gallery +Z
            float dArc = PathGuide.FilletD1 + 0.01f;                                            // on the arc: heading 90 - alpha
            float alphaDeg = (dArc - AbyssConst.WD[6]) / (AbyssConst.WD[8] - AbyssConst.WD[6]) * 180f;
            Assert.That(PathGuide.HeadingYaw(dArc), Is.EqualTo(90f - alphaDeg).Within(0.05f));
            Assert.That(PathGuide.HeadingYaw(AbyssConst.WD[7]), Is.EqualTo(0f).Within(0.05f).Or.EqualTo(360f).Within(0.05f)); // W7 +Z
            Assert.That(PathGuide.HeadingYaw(380f), Is.EqualTo(270f).Within(0.01f));            // hall -X
            Assert.That(PathGuide.HeadingYaw(600f, true), Is.EqualTo(180f).Within(0.01f));      // gantry -Z
        }

        // ---------------------------------------------------------------- walked curve and fillet

        /// <summary>Closed form of the 3 m fillet tangent to the straight W5-W6 and to the r = 25 arc (S-bend).</summary>
        static double ExactWalkLength()
        {
            double r = AbyssConst.FILLET_R, R = AbyssConst.ARC_RADIUS;
            double dz = System.Math.Sqrt((R + r) * (R + r) - r * r);
            double alphaT = System.Math.Asin(r / (R + r));               // tangent point angle on the arc
            double replacedLine = AbyssConst.W[6].z - (AbyssConst.ARC_CENTRE.y - dz); // fillet start to W6 on the straight
            double replaced = replacedLine + R * alphaT;
            double fillet = r * (System.Math.PI * 0.5 - alphaT);
            return PathGuide.PolylineLength - replaced + fillet;
        }

        [Test]
        public void WalkLength_MatchesExactFillet()
        {
            Assert.That(PathGuide.WalkLength, Is.EqualTo((float)ExactWalkLength()).Within(0.01f));
            Assert.That(PathGuide.WalkS(PathGuide.EndD), Is.EqualTo(PathGuide.WalkLength).Within(0.01f));
        }

        /// <summary>
        /// WALK_LENGTH is the lead's calibrated 801.41 m (DEVELOPMENT.md, Calibrations): the spec's 801.25 used the
        /// straight-corner formula 2r - pi r / 2 = 1.29 m, but the 3 m fillet at W6 is tangent to the r = 25 ledge arc (which
        /// curves back toward C1) and shortens the path by 1.132 m, so the walked curve is 801.408 m long.
        /// </summary>
        [Test]
        public void WalkLength_MatchesCalibratedConstant()
        {
            Assert.That(PathGuide.WalkLength, Is.EqualTo(AbyssConst.WALK_LENGTH).Within(0.05f));
            Assert.That(PathGuide.PolylineLength - PathGuide.WalkLength, Is.EqualTo(1.132f).Within(0.01f));
        }

        [Test]
        public void WalkCurve_IsContinuousAndSmooth()
        {
            // dense sampling through the fillet region and its neighbourhood: positions continuous, heading continuous
            float d0 = PathGuide.FilletD0 - 2f, d1 = PathGuide.FilletD1 + 2f;
            Vector3 prev = PathGuide.WalkPoint(d0);
            Vector3 prevT = PathGuide.WalkTangent(d0);
            double sampled = 0.0;
            const float step = 0.005f;
            for (float d = d0 + step; d <= d1; d += step)
            {
                Vector3 p = PathGuide.WalkPoint(d);
                Vector3 t = PathGuide.WalkTangent(d);
                float seg = new Vector2(p.x - prev.x, p.z - prev.z).magnitude;
                Assert.That(seg, Is.LessThan(0.02f), "position jump at d=" + d);
                Assert.That(Vector3.Angle(prevT, t), Is.LessThan(0.5f), "heading jump at d=" + d);
                sampled += seg;
                prev = p; prevT = t;
            }
            float expected = PathGuide.WalkS(d1) - PathGuide.WalkS(d0);
            Assert.That((float)sampled, Is.EqualTo(expected).Within(0.01f));
            // the fillet ends on the arc and starts on the straight
            Assert.That((PathGuide.WalkPoint(PathGuide.FilletD0) - PathGuide.FloorPoint(PathGuide.FilletD0)).magnitude, Is.LessThan(Eps));
            Assert.That((PathGuide.WalkPoint(PathGuide.FilletD1) - PathGuide.FloorPoint(PathGuide.FilletD1)).magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void WalkS_RoundTrips()
        {
            for (float d = 0f; d <= PathGuide.EndD; d += 0.37f)
                Assert.That(PathGuide.WalkDAtS(PathGuide.WalkS(d)), Is.EqualTo(d).Within(1e-3f));
        }

        // ---------------------------------------------------------------- projection

        [Test]
        public void Project_RoundTripsAlongTheWholePath()
        {
            for (float d = 0f; d <= PathGuide.EndD; d += 0.25f)
            {
                bool after = d > AbyssConst.D_LIFT;
                Vector3 p = PathGuide.FloorPoint(d, after);
                float local = PathGuide.Project(p, Mathf.Max(0f, d - 2f));
                Assert.That(local, Is.EqualTo(d).Within(0.01f), "hinted projection at d=" + d);
                float global = PathGuide.Project(p, -1f);
                Assert.That(global, Is.EqualTo(d).Within(0.01f), "global projection at d=" + d);
            }
        }

        [Test]
        public void Project_LateralOffsetsStayOnTheirDistance()
        {
            for (float d = 1f; d < PathGuide.EndD - 1f; d += 0.5f)
            {
                if (Mathf.Abs(d - AbyssConst.WD[6]) < 4f) continue;          // the only plan corner
                if (Mathf.Abs(d - AbyssConst.WD[8]) < 1f) continue;          // arc-to-straight joint (tangent, near-tie)
                if (d > AbyssConst.WD[11] - 1f && d < AbyssConst.WD[14] + 1f) continue;   // lift deck (handled by the controller)
                bool after = d > AbyssConst.D_LIFT;
                Vector3 p = PathGuide.FloorPoint(d, after);
                Vector3 t = PathGuide.Tangent(d, after);
                Vector3 right = Vector3.Cross(Vector3.up, t);
                float off = Mathf.Min(PathGuide.HalfWidth(d) * 0.8f, 2.5f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 q = p + right * (s * off);
                    float pd = PathGuide.Project(q, d);
                    Assert.That(pd, Is.EqualTo(d).Within(0.02f), "offset " + (s * off) + " at d=" + d);
                }
            }
        }

        [Test]
        public void Project_SeparatesHallFromBottomDock()
        {
            // (-45, y, 269) lies on the hall catwalk at y -7 (d 342.54) and on the bottom dock at y -487 (d 459.54)
            Assert.That(PathGuide.Project(new Vector3(-45f, -7f, 269f), -1f), Is.EqualTo(342.54f).Within(0.01f));
            Assert.That(PathGuide.Project(new Vector3(-45f, -487f, 269f), -1f), Is.EqualTo(AbyssConst.D_LIFT).Within(0.01f));
            Assert.That(PathGuide.Project(new Vector3(-45f, -487f, 264f), AbyssConst.D_LIFT), Is.EqualTo(AbyssConst.D_LIFT + 5f).Within(0.01f));
        }

        // ---------------------------------------------------------------- widths, surfaces, beats

        [Test]
        public void HalfWidths_FollowTheWalkableWidths()
        {
            Assert.That(PathGuide.HalfWidth(20f), Is.EqualTo(1.2f).Within(Eps));     // gallery 2.4
            Assert.That(PathGuide.HalfWidth(63f), Is.EqualTo(4.0f).Within(Eps));     // landing 8
            Assert.That(PathGuide.HalfWidth(140f), Is.EqualTo(0.6f).Within(Eps));    // bridge 1.2, walls flush
            Assert.That(PathGuide.HalfWidth(260f), Is.EqualTo(3.0f).Within(Eps));    // ledge 6
            Assert.That(PathGuide.HalfWidth(310f), Is.EqualTo(7.0f).Within(Eps));    // apron 14
            Assert.That(PathGuide.HalfWidth(380f), Is.EqualTo(0.75f).Within(Eps));   // catwalk 1.5
            Assert.That(PathGuide.HalfWidth(455f), Is.EqualTo(9.0f).Within(Eps));    // lift deck walkable 18
            Assert.That(PathGuide.HalfWidth(600f), Is.EqualTo(1.5f).Within(Eps));    // gantry 3.0
            Assert.That(PathGuide.HalfWidth(700f), Is.EqualTo(1.8f).Within(Eps));    // tunnel 3.6
            Assert.That(PathGuide.HalfWidth(735f), Is.EqualTo(9.4f).Within(Eps));    // balcony 20 with walls 0.6 inside
            Assert.That(PathGuide.HalfWidth(780f), Is.EqualTo(1.0f).Within(Eps));    // pier 3.0, walls 0.5 inside: corridor 2.0
            Assert.That(PathGuide.VisibleHalfWidth(780f), Is.EqualTo(1.5f).Within(Eps));
            Assert.That(PathGuide.HalfWidth(140f) - AbyssConst.CAPSULE_R, Is.EqualTo(0.25f).Within(Eps)); // 0.35 m from the edge
        }

        [Test]
        public void Surfaces_PerFootstepTable()
        {
            Assert.That(PathGuide.Surface(20f), Is.EqualTo(SurfaceKind.Concrete));
            Assert.That(PathGuide.Surface(42f), Is.EqualTo(SurfaceKind.Stair));
            Assert.That(PathGuide.Surface(100f), Is.EqualTo(SurfaceKind.SteelPlate));
            Assert.That(PathGuide.Surface(250f), Is.EqualTo(SurfaceKind.Concrete));
            Assert.That(PathGuide.Surface(380f), Is.EqualTo(SurfaceKind.Grating));
            Assert.That(PathGuide.Surface(455f), Is.EqualTo(SurfaceKind.LiftDeck));
            Assert.That(PathGuide.Surface(600f), Is.EqualTo(SurfaceKind.Grating));
            Assert.That(PathGuide.Surface(780f), Is.EqualTo(SurfaceKind.Concrete));
        }

        [Test]
        public void Stairs_HaveSixteenTreads()
        {
            int last = -1, count = 0;
            for (float d = AbyssConst.WD[1]; d < AbyssConst.WD[2]; d += 0.01f)
            {
                int t = PathGuide.StairTread(d);
                if (t != last) { count++; last = t; }
            }
            Assert.That(count, Is.EqualTo(AbyssConst.STAIR_COUNT));
            Assert.That(AbyssConst.STAIR_COUNT * AbyssConst.STAIR_RUN, Is.EqualTo(AbyssConst.WD[2] - AbyssConst.WD[1]).Within(Eps));
            Assert.That(AbyssConst.STAIR_COUNT * AbyssConst.STAIR_RISE, Is.EqualTo(AbyssConst.STAIR_DROP).Within(Eps));
        }

        [Test]
        public void BeatAt_UsesBeatStarts()
        {
            Assert.That(PathGuide.BeatAt(0f), Is.EqualTo(1));
            Assert.That(PathGuide.BeatAt(59.9f), Is.EqualTo(1));
            Assert.That(PathGuide.BeatAt(60f), Is.EqualTo(2));
            Assert.That(PathGuide.BeatAt(300f), Is.EqualTo(4));
            Assert.That(PathGuide.BeatAt(AbyssConst.D_LIFT), Is.EqualTo(5));
            Assert.That(PathGuide.BeatAt(700f), Is.EqualTo(7));
            Assert.That(PathGuide.BeatAt(PathGuide.EndD), Is.EqualTo(7));
        }

        // ---------------------------------------------------------------- timeline

        [Test]
        public void Timeline_SumsTo65803()
        {
            var sd = AbyssConst.BEAT_START_D;
            var st = AbyssConst.BEAT_START_T;
            double t = 0.0;
            for (int b = 0; b < sd.Length; b++)
            {
                Assert.That((float)t, Is.EqualTo(st[b]).Within(0.02f), "start time of beat " + (b + 1));
                double len = (b + 1 < sd.Length ? sd[b + 1] : AbyssConst.POLYLINE_LENGTH) - sd[b];
                double dur = len / AbyssConst.V_WALK;
                if (b == 4) dur += AbyssConst.LIFT_DWELL + AbyssConst.LIFT_DURATION + AbyssConst.LIFT_GATE_DELAY;
                t += dur;
            }
            Assert.That((float)t, Is.EqualTo(AbyssConst.TOTAL_DURATION).Within(0.05f));
            Assert.That(AbyssConst.T_PLATFORM_CENTRE, Is.EqualTo(st[4] + 20f / AbyssConst.V_WALK).Within(0.02f));
            Assert.That(AbyssConst.T_RIDE_START, Is.EqualTo(AbyssConst.T_PLATFORM_CENTRE + AbyssConst.LIFT_DWELL).Within(0.02f));
            Assert.That(AbyssConst.T_RIDE_END, Is.EqualTo(AbyssConst.T_RIDE_START + AbyssConst.LIFT_DURATION).Within(0.02f));
            Assert.That(AbyssConst.T_GATE_OPEN, Is.EqualTo(AbyssConst.T_RIDE_END + AbyssConst.LIFT_GATE_DELAY).Within(0.02f));
        }

        // ---------------------------------------------------------------- lift curve (14.5.5)

        [Test]
        public void LiftS_KeyValues()
        {
            Assert.That(AbyssConst.LiftS(0.0), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(AbyssConst.LiftS(20.0), Is.EqualTo(48.0).Within(1e-6));
            Assert.That(AbyssConst.LiftS(60.0), Is.EqualTo(240.0).Within(1e-6));
            Assert.That(AbyssConst.LiftS(75.0), Is.EqualTo(312.0).Within(1e-6));
            Assert.That(AbyssConst.LiftS(100.0), Is.EqualTo(432.0).Within(1e-6));
            Assert.That(AbyssConst.LiftS(120.0), Is.EqualTo(480.0).Within(0.01));
            Assert.That(AbyssConst.LiftS(500.0), Is.EqualTo(480.0).Within(1e-9));
        }

        [Test]
        public void LiftS_ContinuousWithPeakAcceleration036()
        {
            const double h = 1e-3;
            double peak = 0.0, prevV = 0.0, prevS = 0.0;
            for (double t = 0.0; t <= 120.0 + 1e-9; t += h)
            {
                double s = AbyssConst.LiftS(t);
                double v = LiftController.LiftSpeed(t);
                Assert.That(System.Math.Abs(s - prevS), Is.LessThan(4.8 * h + 1e-6), "s jump at t=" + t);
                Assert.That(System.Math.Abs(v - prevV), Is.LessThan(0.36 * h + 1e-6), "speed jump at t=" + t);
                if (t > 0.0) peak = System.Math.Max(peak, System.Math.Abs(v - prevV) / h);
                prevS = s; prevV = v;
            }
            Assert.That(peak, Is.EqualTo(0.36).Within(0.005));
            // derivative of s matches the analytic speed (left and right of the joints)
            foreach (double t in new[] { 5.0, 19.999, 20.001, 60.0, 99.999, 100.001, 115.0 })
            {
                double num = (AbyssConst.LiftS(t + 1e-4) - AbyssConst.LiftS(t - 1e-4)) / 2e-4;
                Assert.That(num, Is.EqualTo(LiftController.LiftSpeed(t)).Within(1e-3), "ds/dt at " + t);
            }
            // exact 4.8 (LiftS and LiftSpeed share exact double constants; the float LIFT_CRUISE is 4.80000019)
            Assert.That(LiftController.LiftSpeed(60.0), Is.EqualTo(AbyssConst.LIFT_CRUISE).Within(1e-6));
            Assert.That(LiftController.LiftSpeed(AbyssConst.LIFT_RAMP - 1e-9), Is.EqualTo(LiftController.LiftSpeed(AbyssConst.LIFT_RAMP)).Within(1e-6));
            Assert.That(LiftController.LiftSpeed(0.0), Is.EqualTo(0.0));
            Assert.That(LiftController.LiftSpeed(120.0), Is.EqualTo(0.0));
        }

        [Test]
        public void LiftPosition_IsLinearAlongTheLiftLine()
        {
            Vector3 top = LiftController.PositionAtS(0.0), bottom = LiftController.PositionAtS(AbyssConst.LIFT_DROP);
            Assert.That((top - AbyssConst.LIFT_TOP).magnitude, Is.LessThan(Eps));
            Assert.That((bottom - AbyssConst.LIFT_BOTTOM).magnitude, Is.LessThan(Eps));
            Vector3 mid = LiftController.PositionAtS(240.0);
            Assert.That((mid - (AbyssConst.LIFT_TOP + AbyssConst.LIFT_BOTTOM) * 0.5f).magnitude, Is.LessThan(Eps));
            // KS5a pose height check: ride 60 s, s = 240 -> deck at y -247, eye -245.3
            Assert.That(mid.y + AbyssConst.EYE, Is.EqualTo(-245.3f).Within(0.01f));
        }

        // ---------------------------------------------------------------- AutoWalker plan

        [Test]
        public void Autotest_NominalDuration()
        {
            double total = WalkPlanner.SimulateDuration(false, 1f / 60f, out double rideStart, out double rideEnd);
            Assert.That(total, Is.EqualTo(658.0).Within(8.0), "autotest duration");
            Assert.That(rideStart, Is.EqualTo(AbyssConst.T_RIDE_START).Within(1.0), "lift start");
            Assert.That(rideEnd, Is.EqualTo(AbyssConst.T_RIDE_END).Within(1.0), "lift end");
            // the same plan at 30 fps lands within a few frames of the 60 fps plan (analytic stops; the lift waits quantise to frames)
            double total30 = WalkPlanner.SimulateDuration(false, 1f / 30f, out _, out _);
            Assert.That(total30, Is.EqualTo(total).Within(0.5));
        }

        [Test]
        public void Cinematic_SlowdownsAndHold()
        {
            Assert.That(WalkPlanner.CinematicExtraSeconds, Is.EqualTo(WalkPlanner.CINEMATIC_SLOWDOWN_TOTAL).Within(0.1f));
            Assert.That(WalkPlanner.SlowK, Is.InRange(0.01f, 0.5f));
            Assert.That(WalkPlanner.ProfileSpeed(PathGuide.Beat7D(-4f), true), Is.EqualTo(WalkPlanner.BEAT7_SLOW_SPEED).Within(1e-3f));
            Assert.That(WalkPlanner.ProfileSpeed(20f, true), Is.EqualTo(AbyssConst.V_WALK).Within(1e-4f));
            double total = WalkPlanner.SimulateDuration(true, 1f / 60f, out _, out _);
            // 658 + 12 s slowdowns + 14 s hold ("about 684") + 4-5 s of gentler eased starts and stops around the holds:
            // 688-689 s measured (DEVELOPMENT.md, Calibrations), inside the spec's tolerance
            Assert.That(total, Is.EqualTo(684.0).Within(8.0));
        }

        [Test]
        public void GazeTables_AreOrderedAndNeverExceedPitchLimitsMuch()
        {
            var tables = new[] { GazeCues.Beat1, GazeCues.Beat2, GazeCues.Beat3, GazeCues.Beat4, GazeCues.Beat5Walk, GazeCues.Beat5Ride, GazeCues.Beat6, GazeCues.Beat7 };
            foreach (var tab in tables)
            {
                for (int i = 1; i < tab.Length; i++)
                    Assert.That(tab[i].T, Is.GreaterThan(tab[i - 1].T), "keys must be strictly increasing");
                foreach (var k in tab)
                    Assert.That(Mathf.Abs(k.Pitch), Is.LessThanOrEqualTo(90f));
            }
            // H7 hold: identical keys at both ends so a walk without the hold stays continuous
            var ctx = new GazeCues.Context { Eye = new Vector3(-45f, -485.3f, -12f), HeadingYaw = 180f, SceneTime = 0.0 };
            GazeCues.Evaluate(GazeCues.Beat7, GazeCues.H7_T0, in ctx, out float y0, out float p0);
            GazeCues.Evaluate(GazeCues.Beat7, GazeCues.H7_T1, in ctx, out float y1, out float p1);
            Assert.That(Mathf.DeltaAngle(y0, y1), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(p1, Is.EqualTo(p0).Within(1e-3f));
            Assert.That(GazeCues.Beat7FovOffset(GazeCues.H7_T1), Is.EqualTo(GazeCues.H7_FOV_PUSH).Within(1e-4f));
            Assert.That(GazeCues.Beat7FovOffset(GazeCues.H7_T1 + GazeCues.H7_FOV_RETURN), Is.EqualTo(0f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- ride choreography (R1-04 / R1-05 / R1-25)

        [Test]
        public void DeckStand_WalksToTheRailAndBackInsideTheRide()
        {
            Rect[] keepOut = Megastructure.Beats.Beat05Layout.DeckKeepOut();
            float walkHalf = LiftController.WalkHalf;
            const float h = 0.01f;
            WalkPlanner.DeckStand(-2f, out Vector2 prev, out _);
            Assert.That(prev.magnitude, Is.LessThan(1e-6f), "boards and starts at the centre (dwell)");
            for (int i = 1; i <= 13500; i++)
            {
                float t = -2f + i * h;
                WalkPlanner.DeckStand(t, out Vector2 p, out Vector2 v);
                Assert.That(Mathf.Abs(p.x) <= walkHalf && Mathf.Abs(p.y) <= walkHalf, Is.True, "inside the 18 x 18 walk clamp at " + t);
                foreach (Rect r in keepOut) Assert.That(r.Contains(p), Is.False, "clear of the pylon keep-outs at " + t);
                Assert.That(v.magnitude, Is.LessThanOrEqualTo(WalkPlanner.RAIL_MAX_SPEED), "planned speed at " + t);
                Assert.That((p - prev).magnitude, Is.LessThanOrEqualTo(WalkPlanner.RAIL_MAX_SPEED * h + 1e-4f), "continuous at " + t);
                prev = p;
            }
            // the feed-forward velocity is the derivative of the stand
            foreach (float t in new[] { 64.5f, 65.9f, 70f, 74.2f, 110f, 111.7f, 115f, 120f })
            {
                WalkPlanner.DeckStand(t - 1e-3f, out Vector2 a, out _);
                WalkPlanner.DeckStand(t + 1e-3f, out Vector2 b, out _);
                WalkPlanner.DeckStand(t, out _, out Vector2 v);
                Assert.That(((b - a) / 2e-3f - v).magnitude, Is.LessThan(5e-3f), "d/dt at " + t);
            }
            // the stand at the key moments: centre for the city, at the -Z rail for KS5b / the fan hold and KS5c / the look
            // down, back at the centre before the gate opens
            WalkPlanner.DeckStand(60f, out Vector2 s60, out _);
            WalkPlanner.DeckStand(75f, out Vector2 s75, out _);
            WalkPlanner.DeckStand(100f, out Vector2 s100, out _);
            WalkPlanner.DeckStand(WalkPlanner.RAIL_BACK_T0, out Vector2 sBack, out _);
            float gate = AbyssConst.LIFT_DURATION + AbyssConst.LIFT_GATE_DELAY;
            WalkPlanner.DeckStand(gate, out Vector2 sGate, out Vector2 vGate);
            Assert.That(s60.magnitude, Is.LessThan(1e-6f));
            Assert.That(s75.y, Is.EqualTo(WalkPlanner.RAIL_STAND_Z).Within(1e-5f));
            Assert.That(s100.y, Is.EqualTo(WalkPlanner.RAIL_STAND_Z).Within(1e-5f));
            Assert.That(sBack.y, Is.EqualTo(WalkPlanner.RAIL_STAND_Z).Within(1e-5f));
            Assert.That(sGate.magnitude, Is.LessThan(1e-6f), "back at the centre when the gate opens");
            Assert.That(vGate.magnitude, Is.EqualTo(0f));
            Assert.That(WalkPlanner.RAIL_BACK_T1, Is.LessThan(gate));
            Assert.That(WalkPlanner.RAIL_STAND_Z, Is.GreaterThanOrEqualTo(-walkHalf));
            // world z of the stand at the bottom dock: 260.4, the KS5b / KS5c eye line (1.4 m from the -Z deck edge)
            Assert.That(AbyssConst.LIFT_BOTTOM.z + WalkPlanner.RAIL_STAND_Z, Is.EqualTo(260.4f).Within(1e-3f));
        }

        static KeyShot Shot(string tag)
        {
            foreach (KeyShot k in KeyShots.All) if (k.Tag == tag) return k;
            Assert.Fail("no key shot " + tag);
            return default;
        }

        /// <summary>Eye on the platform at ride second t at the planned deck stand.</summary>
        static Vector3 RideEye(float t)
        {
            Vector3 deck = LiftController.PositionAtS(AbyssConst.LiftS(Mathf.Max(0f, t)));
            WalkPlanner.DeckStand(t, out Vector2 s, out _);
            return deck + new Vector3(s.x, AbyssConst.EYE, s.y);
        }

        [Test]
        public void RideGaze_IsConsistentWithTheKeyShotPoses()
        {
            // KS5a (R1-25): the 10-25 facade hold looks along the facade at the key shot's yaw (pitch lower: the eye is level
            // with the city's top row then), and the 25-45 pan rises to the key shot's pitch
            var ctx = new GazeCues.Context { Eye = RideEye(21.5f), HeadingYaw = 270f, SceneTime = 0.0 };
            GazeCues.Evaluate(GazeCues.Beat5Ride, 21.5f, in ctx, out float yA, out float pA);
            Assert.That(yA, Is.EqualTo(GazeCues.KS5A_YAW).Within(1e-3f));
            Assert.That(pA, Is.EqualTo(GazeCues.RIDE_FACADE_PITCH).Within(1e-3f));
            KeyShot a = Shot("KS5a_lift_city");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yA, a.Yaw)), Is.LessThanOrEqualTo(6f), "KS5a yaw (qa pose) vs the facade hold");
            GazeCues.Evaluate(GazeCues.Beat5Ride, 45f, in ctx, out _, out float pPan);
            Assert.That(Mathf.Abs(pPan - a.Pitch), Is.LessThanOrEqualTo(4f), "KS5a pitch (qa pose) vs the end of the facade pan");
            // the eye during the hold is within 20 m of the city's top row (y -60), so the hold must not look far up
            Assert.That(Mathf.Abs(RideEye(21.5f).y + 60f), Is.LessThan(20f));

            // KS5b (R1-04): the walker stands where the key shot is captured and holds the fan
            KeyShot b = Shot("KS5b_lift_fan");
            Vector3 eyeB = RideEye(b.RideSeconds);
            Assert.That(Mathf.Abs(eyeB.z - b.Eye.z), Is.LessThanOrEqualTo(0.3f), "KS5b eye z vs the deck stand");
            ctx.Eye = b.Eye;
            GazeCues.Evaluate(GazeCues.Beat5Ride, 80f, in ctx, out float yB, out _);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yB, b.Yaw)), Is.LessThanOrEqualTo(3f), "fan hold yaw vs KS5b");

            // KS5c (R1-05 / R2-03, lead decision D1): over the rail at yaw 176, 38 degrees down
            KeyShot c = Shot("KS5c_lift_down");
            Vector3 eyeC = RideEye(c.RideSeconds);
            Assert.That(Mathf.Abs(eyeC.z - c.Eye.z), Is.LessThanOrEqualTo(0.3f), "KS5c eye z vs the deck stand");
            ctx.Eye = eyeC;
            GazeCues.Evaluate(GazeCues.Beat5Ride, c.RideSeconds, in ctx, out float yC, out float pC);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yC, c.Yaw)), Is.LessThanOrEqualTo(1f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yC, GazeCues.RIDE_DOWN_YAW)), Is.LessThanOrEqualTo(1e-3f));
            Assert.That(pC, Is.EqualTo(GazeCues.RIDE_DOWN_PITCH).Within(1e-3f));
            Assert.That(Mathf.Abs(pC - c.Pitch), Is.LessThanOrEqualTo(3f), "KS5c pitch (qa pose) vs the look down");
            // the -Z deck edge seen from the rail stand (1.4 m behind it at yaw 180): the deck may take at most 35 % of the
            // frame height (R2-03 target "deck plus rail at most 35 %")
            float edgeBack = (AbyssConst.LIFT_BOTTOM.z + WalkPlanner.RAIL_STAND_Z) - (AbyssConst.LIFT_BOTTOM.z - AbyssConst.LIFT_SIZE * 0.5f);
            float edgeDeg = Mathf.Atan2(AbyssConst.EYE, edgeBack) * Mathf.Rad2Deg;
            float deckFrac = Mathf.Clamp01(((-pC + 0.5f * c.Fov) - edgeDeg) / c.Fov);
            Assert.That(deckFrac, Is.LessThanOrEqualTo(0.35f), "deck share of the KS5c / ride look-down frame (edge at " + edgeDeg + " deg)");

            // walk-on (R1-35): the look reaches the +Z guide rails plunging past the dock edge, and the walk table hands over to
            // the ride table
            var w = new GazeCues.Context { Eye = new Vector3(-150f, -5.3f, 269f), HeadingYaw = 270f, SceneTime = 0.0 };
            GazeCues.Evaluate(GazeCues.Beat5Walk, 8f, in w, out float yW, out float pW);
            Vector3 toRail = new Vector3(-162f, -7f, AbyssConst.GUIDE_RAIL_Z0) - w.Eye;
            float railYaw = Mathf.Atan2(toRail.x, toRail.z) * Mathf.Rad2Deg;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yW, railYaw)), Is.LessThanOrEqualTo(20f), "toward the guide rails at the dock");
            Assert.That(pW, Is.EqualTo(GazeCues.WALKON_PITCH).Within(1e-3f));
            GazeCues.Evaluate(GazeCues.Beat5Walk, 20f, in w, out float yEnd, out float pEnd);
            GazeCues.Evaluate(GazeCues.Beat5Ride, -2f, in ctx, out float yDwell, out float pDwell);
            Assert.That(Mathf.DeltaAngle(yEnd, yDwell), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(pEnd, Is.EqualTo(pDwell).Within(1e-3f));
        }

        /// <summary>Largest yaw / pitch rate (deg/s) of a table sampled at 50 Hz with a moving eye.</summary>
        static float MaxGazeRate(GazeCues.Key[] tab, float t0, float t1, float heading, System.Func<float, Vector3> eye)
        {
            const float h = 0.02f;
            float max = 0f;
            var ctx = new GazeCues.Context { Eye = eye(t0), HeadingYaw = heading, SceneTime = 0.0 };
            GazeCues.Evaluate(tab, t0, in ctx, out float y0, out float p0);
            for (float t = t0 + h; t <= t1; t += h)
            {
                ctx.Eye = eye(t);
                GazeCues.Evaluate(tab, t, in ctx, out float y, out float p);
                max = Mathf.Max(max, Mathf.Abs(Mathf.DeltaAngle(y0, y)) / h, Mathf.Abs(p - p0) / h);
                y0 = y; p0 = p;
            }
            return max;
        }

        [Test]
        public void RideWalkAndGantryGaze_TurnGently()
        {
            // no target swing faster than 25 deg/s (the follower adds omega 2.5 damping on top)
            Assert.That(MaxGazeRate(GazeCues.Beat5Walk, 0f, 14f, 270f, t => new Vector3(-142f - 1.5f * t, -5.3f, 269f)), Is.LessThanOrEqualTo(25f));
            Assert.That(MaxGazeRate(GazeCues.Beat5Ride, -2f, 125f, 270f, RideEye), Is.LessThanOrEqualTo(25f));
            Assert.That(MaxGazeRate(GazeCues.Beat6, 0f, 133f, 180f, t => new Vector3(-45f, -485.3f, 259f - 1.5f * t)), Is.LessThanOrEqualTo(25f));
            // beat 2 up to the vertigo hold (the bolt follow, R2-09) and beat 7 (H7 pans, pier vertigo look, door; R2-18)
            Assert.That(MaxGazeRate(GazeCues.Beat2, 0f, 47.9f, 0f, Beat2Eye), Is.LessThanOrEqualTo(25f));
            Assert.That(MaxGazeRate(GazeCues.Beat7, 0f, 102f, 180f, Beat7Eye), Is.LessThanOrEqualTo(25f));
        }

        /// <summary>Eye on the bridge at beat-2 time t (1.5 m/s from W3).</summary>
        static Vector3 Beat2Eye(float t) => new Vector3(0f, AbyssConst.BRIDGE_Y + AbyssConst.EYE, AbyssConst.W[3].z + AbyssConst.V_WALK * t);

        /// <summary>Eye in beat 7 at cinematic clock tau (walk to the hold at z -12, the 14 s hold, then 1.5 m/s to the stop).</summary>
        static Vector3 Beat7Eye(float tau)
        {
            float z = tau <= GazeCues.H7_T0 ? AbyssConst.W[15].z - AbyssConst.V_WALK * tau
                    : tau <= GazeCues.H7_T1 ? GazeCues.H7_Z
                    : Mathf.Max(WalkPlanner.END_STOP_Z, GazeCues.H7_Z - AbyssConst.V_WALK * (tau - GazeCues.H7_T1));
            return new Vector3(AbyssConst.W[15].x, AbyssConst.W[15].y + AbyssConst.EYE, z);
        }

        /// <summary>Angle (degrees) between the gaze (yaw, pitch) and the direction from the eye to p.</summary>
        static float AngleTo(float yaw, float pitch, Vector3 eye, Vector3 p)
        {
            Vector3 look = AbyssConst.YawPitch(yaw, pitch) * Vector3.forward;
            return Vector3.Angle(look, p - eye);
        }

        [Test]
        public void Beat2Gaze_FollowsTheFallingBoltIntoTheFog()
        {
            // D3 (R2-09): released from the right-wall belt lamp at beat time 15 (d 82.5), about 300 m off the bridge axis
            Vector3 rel = GazeCues.BoltRelease;
            Assert.That(rel.x, Is.EqualTo(300f).Within(1f), "300 m from the bridge axis");
            Assert.That(rel.y, Is.InRange(AbyssConst.RingY(GazeCues.BOLT_BELT_K) - 6f, AbyssConst.RingY(GazeCues.BOLT_BELT_K) + 6f), "at the y -30 belt");
            // in front of the belt of the 30-degree flat: dot((x, z - 410), (cos 30, sin 30)) a little under 330
            float apo = rel.x * 0.8660254f + (rel.z - AbyssConst.SHAFT_CENTRE.y) * 0.5f;
            Assert.That(apo, Is.InRange(AbyssConst.BELT_INNER_APOTHEM - 3f, AbyssConst.BELT_INNER_APOTHEM));
            // the gaze looks where Beat02 drops the bolt, at the same beat time
            Assert.That((rel - Megastructure.Beats.Beat02_Bridge.BoltStart).magnitude, Is.LessThan(0.5f), "gaze target = Beat02 bolt start");
            Assert.That(GazeCues.BOLT_RELEASE_T, Is.EqualTo(Megastructure.Beats.Beat02_Bridge.BOLT_BEAT_TIME).Within(1e-4f));
            // the deck never hides it: X / (h + 1.7) stays far above 0.6 / 1.7 for the whole visible fall
            Assert.That(rel.x / (GazeCues.BoltPosition(GazeCues.BOLT_FADE1).y * -1f + 1.7f), Is.GreaterThan(0.6f / 1.7f));

            // from just after the release to its dissolve the bolt stays well inside the frame (vertical FOV 66, 16:9:
            // half-height 33, half-width 49 degrees): within 22 degrees of the look direction
            float t0 = GazeCues.BOLT_RELEASE_T + 1f, t1 = GazeCues.BOLT_RELEASE_T + GazeCues.BOLT_FADE0 + 2f;
            for (float t = t0; t <= t1; t += 0.1f)
            {
                var ctx = new GazeCues.Context { Eye = Beat2Eye(t), HeadingYaw = 0f, SceneTime = 0.0 };
                GazeCues.Evaluate(GazeCues.Beat2, t, in ctx, out float y, out float p);
                Vector3 bolt = GazeCues.BoltPosition(t - GazeCues.BOLT_RELEASE_T);
                Assert.That(AngleTo(y, p, ctx.Eye, bolt), Is.LessThanOrEqualTo(22f), "bolt in frame at beat time " + t);
            }
            // level again before the 22-48 drift and the vertigo hold
            var c30 = new GazeCues.Context { Eye = Beat2Eye(30f), HeadingYaw = 0f, SceneTime = 0.0 };
            GazeCues.Evaluate(GazeCues.Beat2, 30f, in c30, out float y30, out float p30);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(y30, 0f)), Is.LessThan(1e-3f));
            Assert.That(p30, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Beat7Gaze_HoldPansBothWaysAndThePierLookRhymesTheVertigoHold()
        {
            // H7 (R2-18 (1)): left 25 to yaw 155, then right 25 past the axis to yaw 205 (toward the sun azimuth)
            float minYaw = 360f, maxYaw = 0f;
            for (float tau = GazeCues.H7_T0; tau <= GazeCues.H7_T1; tau += 0.05f)
            {
                var ctx = new GazeCues.Context { Eye = Beat7Eye(tau), HeadingYaw = 180f, SceneTime = 0.0 };
                GazeCues.Evaluate(GazeCues.Beat7, tau, in ctx, out float y, out _);
                minYaw = Mathf.Min(minYaw, y);
                maxYaw = Mathf.Max(maxYaw, y);
            }
            Assert.That(minYaw, Is.EqualTo(180f - GazeCues.H7_PAN).Within(0.01f));
            Assert.That(maxYaw, Is.EqualTo(180f + GazeCues.H7_PAN).Within(0.01f));

            // pier vertigo look (R2-18 (2)) around z -45: the pier's slab edge in the lower 25-50 % of the frame (FOV 68)
            float tauMid = 0.5f * (GazeCues.PIER_LOOK_T1 + GazeCues.PIER_LOOK_T2);
            Vector3 eye = Beat7Eye(tauMid);
            Assert.That(eye.z, Is.InRange(-50f, -40f), "the look happens around z -45");
            var cp = new GazeCues.Context { Eye = eye, HeadingYaw = 180f, SceneTime = 0.0 };
            for (float tau = GazeCues.PIER_LOOK_T1; tau <= GazeCues.PIER_LOOK_T2; tau += 0.25f)
            {
                cp.Eye = Beat7Eye(tau);
                GazeCues.Evaluate(GazeCues.Beat7, tau, in cp, out float y, out float p);
                float off = Mathf.Abs(Mathf.DeltaAngle(180f, y)) * Mathf.Deg2Rad;
                float edgeDist = (AbyssConst.PIER_W * 0.5f) / Mathf.Sin(off);   // along the view direction, from the centreline
                float edgeDeg = Mathf.Atan2(AbyssConst.EYE, edgeDist) * Mathf.Rad2Deg;
                float slabFrac = ((-p + 34f) - edgeDeg) / 68f;
                Assert.That(slabFrac, Is.InRange(0.25f, 0.5f), "pier slab share of the frame at tau " + tau);
                Assert.That(Mathf.DeltaAngle(180f, y), Is.LessThan(-60f), "looking off the left (+X) edge");
            }

            // the stop (D2): facing the door at END_PITCH from z END_STOP_Z
            float tauEnd = GazeCues.Beat7Clock(WalkPlanner.EndD, false, 0f);
            var ce = new GazeCues.Context { Eye = Beat7Eye(tauEnd), HeadingYaw = 180f, SceneTime = 0.0 };
            GazeCues.Evaluate(GazeCues.Beat7, tauEnd, in ce, out float ye, out float pe);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(ye, 180f)), Is.LessThan(0.5f));
            Assert.That(pe, Is.EqualTo(GazeCues.END_PITCH).Within(1e-3f));
            // the frame head (2.6 m) stays under the top edge, and the lower edge meets the pier in front of the door frame
            float frameFront = AbyssConst.GOAL_DOOR_Z + 0.6f;
            float toFrame = WalkPlanner.END_STOP_Z - frameFront;
            Assert.That(Mathf.Atan2(2.6f - AbyssConst.EYE, toFrame) * Mathf.Rad2Deg, Is.LessThan(pe + 34f - 1f), "frame head in frame");
            float floorFrom = AbyssConst.EYE / Mathf.Tan((34f - pe) * Mathf.Deg2Rad);
            Assert.That(toFrame - floorFrom, Is.InRange(0.2f, 1.0f), "metres of pier visible in front of the door frame");
        }

        [Test]
        public void Ending_StopsAtTheDoorTriggerAndFallsBackPastIt()
        {
            // D2 (R2-05): trigger 3.0 m from the door plane (feet z <= -71.0), the walk stops at z -70.8 facing the door;
            // Beat07 also opens the door for a walker that has finished within its slack of the trigger line
            float stopZ = PathGuide.Beat7Z(WalkPlanner.EndD);
            Assert.That(stopZ, Is.EqualTo(WalkPlanner.END_STOP_Z).Within(1e-3f));
            Assert.That(stopZ - AbyssConst.GOAL_DOOR_Z, Is.EqualTo(3.2f).Within(1e-3f), "3.2 m from the door plane");
            Assert.That(WalkPlanner.D2_TRIGGER_Z, Is.EqualTo(Megastructure.Beats.Beat07_Reveal.GOAL_TRIGGER_Z).Within(1e-4f));
            Assert.That(stopZ, Is.LessThanOrEqualTo(Megastructure.Beats.Beat07_Reveal.GOAL_TRIGGER_Z + Megastructure.Beats.Beat07_Reveal.WALKER_STOP_SLACK - 0.1f),
                        "the finished walker is inside Beat07's slack of the trigger line");
            // fallbacks: first just past D2's line, then just past whatever AbyssConst holds, never nearer than END_MARGIN
            float nearZ = PathGuide.Beat7Z(WalkPlanner.GoalFallbackNearD), fbZ = PathGuide.Beat7Z(WalkPlanner.GoalFallbackD);
            Assert.That(nearZ, Is.LessThan(WalkPlanner.D2_TRIGGER_Z));
            Assert.That(fbZ, Is.LessThan(AbyssConst.DOOR_TRIGGER_Z));
            Assert.That(fbZ, Is.GreaterThanOrEqualTo(AbyssConst.GOAL_DOOR_Z + WalkPlanner.END_MARGIN - 1e-3f));
            Assert.That(WalkPlanner.GoalFallbackNearD, Is.GreaterThan(WalkPlanner.EndD));
            Assert.That(WalkPlanner.GoalFallbackD, Is.GreaterThanOrEqualTo(WalkPlanner.GoalFallbackNearD - 1e-3f));

            // planner: the walk stops on EndD, ExtendEnd walks on (only when beyond) and stops exactly on the new end
            var p = new WalkPlanner();
            p.Begin(PathGuide.Beat7D(-60f), false, true, false, AbyssConst.V_WALK);
            for (int i = 0; i < 6000 && p.Mode != WalkPlanner.Stage.Done; i++) p.Step(1f / 60f, false);
            Assert.That(p.Mode, Is.EqualTo(WalkPlanner.Stage.Done));
            Assert.That(p.D, Is.EqualTo(WalkPlanner.EndD).Within(1e-3f));
            Assert.That(p.ExtendEnd(WalkPlanner.EndD), Is.False, "no extension to the same stop");
            float further = PathGuide.Beat7D(-72.9f);
            Assert.That(p.ExtendEnd(further), Is.True);
            float prevS = p.S, maxV = 0f, maxSpeed = 0f;
            for (int i = 0; i < 6000 && p.Mode != WalkPlanner.Stage.Done; i++)
            {
                p.Step(1f / 60f, false);
                Assert.That(p.S, Is.GreaterThanOrEqualTo(prevS - 1e-5f), "never walks back");
                maxV = Mathf.Max(maxV, Mathf.Abs(p.S - prevS) * 60f);
                maxSpeed = Mathf.Max(maxSpeed, p.Speed);
                prevS = p.S;
            }
            Assert.That(p.D, Is.EqualTo(further).Within(1e-3f));
            // the planner's own speed never exceeds the walk speed; the speed measured from S differences carries the float
            // quantisation of S (about 800 m here: one ulp is 6.1e-5 m, i.e. 0.0037 m/s at 60 steps per second)
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(AbyssConst.V_WALK + 1e-4f));
            float ulpSpeed = 2f * 60f * (p.S * 1.1920929e-7f);
            Assert.That(maxV, Is.LessThanOrEqualTo(AbyssConst.V_WALK + ulpSpeed + 1e-4f));
        }

        [Test]
        public void Beat6Gaze_GlancesAtABladeShadowAndDriftsAsymmetrically()
        {
            var ctx = new GazeCues.Context { HeadingYaw = 180f, SceneTime = 0.0 };
            float minPitch = 90f, maxRel = 0f;
            for (float t = 60f; t <= 95f; t += 0.1f)
            {
                float z = 259f - 1.5f * t;
                ctx.Eye = new Vector3(-45f, -485.3f, z);
                GazeCues.Evaluate(GazeCues.Beat6, t, in ctx, out float y, out float p);
                if (p < minPitch && p < -10f)
                {
                    // the glance lands on the gantry deck inside the blade-shadow band z 148-259 (14.5.6)
                    float hit = z - AbyssConst.EYE / Mathf.Tan(-p * Mathf.Deg2Rad) * Mathf.Cos(Mathf.DeltaAngle(180f, y) * Mathf.Deg2Rad);
                    Assert.That(hit, Is.InRange(148f, 259f), "glance target z at t " + t);
                }
                minPitch = Mathf.Min(minPitch, p);
                maxRel = Mathf.Max(maxRel, Mathf.DeltaAngle(180f, y));
            }
            Assert.That(minPitch, Is.LessThanOrEqualTo(-20f), "one pitch-down glance");
            Assert.That(maxRel, Is.InRange(12f, 15f), "asymmetric yaw drift of 12-15 degrees");
        }

        // ---------------------------------------------------------------- player shadow proxy (R1-02)

        static void AddEdge(Dictionary<long, int> edges, int a, int b)
        {
            long k = ((long)a << 32) | (uint)b;
            edges.TryGetValue(k, out int n);
            edges[k] = n + 1;
        }

        [Test]
        public void ShadowProxy_PartsAreClosedOutwardShells()
        {
            for (int i = 0; i < PlayerShadow.PART_COUNT; i++)
            {
                var mb = new MeshBuilder();
                PlayerShadow.BuildPart(mb, (PlayerShadow.Part)i);
                List<int> idx = mb.Indices;
                List<Vector3> pos = mb.Positions;
                var edges = new Dictionary<long, int>();
                double vol = 0.0;
                for (int t = 0; t < idx.Count; t += 3)
                {
                    int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                    Assert.That(Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]).magnitude, Is.GreaterThan(1e-8f), "degenerate triangle in " + (PlayerShadow.Part)i);
                    AddEdge(edges, a, b); AddEdge(edges, b, c); AddEdge(edges, c, a);
                    vol += Vector3.Dot(pos[a], Vector3.Cross(pos[b], pos[c])) / 6.0;
                }
                foreach (KeyValuePair<long, int> kv in edges)
                {
                    int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffffL);
                    Assert.That(kv.Value, Is.EqualTo(1), "directed edge used twice in " + (PlayerShadow.Part)i);
                    Assert.That(edges.ContainsKey(((long)b << 32) | (uint)a), Is.True, "open edge in " + (PlayerShadow.Part)i);
                }
                Assert.That(vol, Is.GreaterThan(0.0), "outward winding of " + (PlayerShadow.Part)i);
            }
        }

        [Test]
        public void ShadowProxy_IsHumanSized()
        {
            var mb = new MeshBuilder();
            PlayerShadow.BuildPose(mb, 0f);
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            foreach (Vector3 p in mb.Positions) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            Assert.That(hi.y, Is.EqualTo(PlayerShadow.HEIGHT).Within(0.01f), "1.8 m like CAPSULE_H");
            Assert.That(lo.y, Is.EqualTo(0f).Within(0.006f), "feet on the floor");
            Assert.That(hi.x - lo.x, Is.InRange(0.45f, PlayerShadow.MAX_WIDTH), "overall width");
            Assert.That(hi.z - lo.z, Is.LessThan(0.42f), "toe to back of the head");
            // the eye (0, 1.7, 0) is inside the head's front half, so the camera never sits outside the proxy's head
            float ex = 0f, ey = (AbyssConst.EYE - PlayerShadow.HEAD_Y) / PlayerShadow.HEAD_RY, ez = -PlayerShadow.HEAD_Z / PlayerShadow.HEAD_RZ;
            Assert.That(ex * ex + ey * ey + ez * ez, Is.LessThan(1f));
            Assert.That(-PlayerShadow.HEAD_Z, Is.GreaterThan(0f));
        }

        /// <summary>
        /// Shadow of the proxy (body yaw 0) on y = 0 for a light travelling toward (yaw, pitch): a 1 cm grid in u (along the
        /// light's horizontal direction, from -0.5 m) by w (across it, from -0.5 m).
        /// </summary>
        static bool[,] ProxyShadow(float lightYaw, float lightPitch, float swing)
        {
            const float cell = 0.01f;
            const int nu = 300, nw = 100;
            var mb = new MeshBuilder();
            PlayerShadow.BuildPose(mb, swing);
            Vector3 L = AbyssConst.YawPitch(lightYaw, lightPitch) * Vector3.forward;
            Assert.That(L.y, Is.LessThan(-0.1f));
            var u = new Vector2(L.x, L.z).normalized;
            var w = new Vector2(u.y, -u.x);
            var q = new Vector2[mb.Positions.Count];
            for (int i = 0; i < q.Length; i++)
            {
                Vector3 p = mb.Positions[i];
                Vector3 g = p - L * (p.y / L.y);
                var g2 = new Vector2(g.x, g.z);
                q[i] = new Vector2((Vector2.Dot(g2, u) + 0.5f) / cell, (Vector2.Dot(g2, w) + 0.5f) / cell);
            }
            var grid = new bool[nu, nw];
            List<int> idx = mb.Indices;
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector2 a = q[idx[t]], b = q[idx[t + 1]], c = q[idx[t + 2]];
                int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), i1 = Mathf.Min(nu - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), j1 = Mathf.Min(nw - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    var s = new Vector2(i + 0.5f, j + 0.5f);
                    float d1 = Cross2(b - a, s - a), d2 = Cross2(c - b, s - b), d3 = Cross2(a - c, s - c);
                    bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
                    if (!(neg && pos)) grid[i, j] = true;
                }
            }
            return grid;
        }

        static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Filled runs and total width (cells) of grid row i.</summary>
        static int Runs(bool[,] g, int i, out int width)
        {
            int runs = 0, first = -1, last = -1;
            for (int j = 0; j < g.GetLength(1); j++)
            {
                if (!g[i, j]) continue;
                if (j == 0 || !g[i, j - 1]) runs++;
                if (first < 0) first = j;
                last = j;
            }
            width = first < 0 ? 0 : last - first + 1;
            return runs;
        }

        [Test]
        public void ShadowProxy_ReadsAsAPersonUnderTheKeys()
        {
            // KS2b: body yaw 0, the beat-2 Key travelling toward yaw 205 at pitch -72 (14.2)
            bool[,] g = ProxyShadow(205f, -72f, 0f);
            int filled = 0, maxW = 0, lastRow = -1;
            for (int i = 0; i < g.GetLength(0); i++)
            {
                Runs(g, i, out int wdt);
                for (int j = 0; j < g.GetLength(1); j++) if (g[i, j]) filled++;
                if (wdt > 0) lastRow = i;
                maxW = Mathf.Max(maxW, wdt);
            }
            float area = filled * 1e-4f;
            // the spec capsule (r 0.25, 1.8 m) at elevation 72: sphere ellipse pi r^2 / sin + swept 2r (1.8 - 2r) cot
            float r = 0.25f, el = 72f * Mathf.Deg2Rad;
            float capsule = Mathf.PI * r * r / Mathf.Sin(el) + 2f * r * (1.8f - 2f * r) / Mathf.Tan(el);
            Assert.That(area, Is.InRange(0.12f, 0.75f * capsule), "shadow area vs the capsule's " + capsule);
            Assert.That(maxW * 0.01f, Is.InRange(0.36f, 0.56f), "shoulder-wide at its widest");
            // the head protrudes beyond the shoulders: 4 cm from the far end the shadow is under half its widest
            Runs(g, lastRow - 4, out int headW);
            Assert.That(headW, Is.LessThan(0.45f * maxW), "head narrower than the shoulders");

            // two separate legs: light from straight behind (beat-2 elevation), rows 10-15 cm from the feet hold only the legs
            bool[,] back = ProxyShadow(180f, -72f, 0f);
            for (int i = 60; i <= 65; i++)
                Assert.That(Runs(back, i, out _), Is.EqualTo(2), "two legs at u " + (i - 50) + " cm");
            // beat 7 (sun travelling toward yaw 20 while the walker faces 180, i.e. 200 in the body frame, elevation 40): the
            // long shadow behind the standing walker shows the legs apart; mid-stride the leg shadows move (they may cross,
            // as a real shadow's do with the sun 20 degrees off the walk axis)
            bool[,] b7 = ProxyShadow(200f, -40f, 0f);
            int twoLegRows = 0;
            for (int i = 80; i <= 110; i++) if (Runs(b7, i, out _) >= 2) twoLegRows++;
            Assert.That(twoLegRows, Is.GreaterThanOrEqualTo(20), "legs apart in the beat-7 shadow");
            bool[,] b7Stride = ProxyShadow(200f, -40f, PlayerShadow.LEG_SWING);
            int changed = 0;
            for (int i = 0; i < b7.GetLength(0); i++)
                for (int j = 0; j < b7.GetLength(1); j++)
                    if (b7[i, j] != b7Stride[i, j]) changed++;
            Assert.That(changed * 1e-4f, Is.GreaterThan(0.02f), "the stride moves the leg shadows (m^2 changed)");
        }

        [Test]
        public void ShadowProxy_GaitAndBodyYaw()
        {
            Assert.That(PlayerShadow.GaitSwing(0f, 0.3f, 1f), Is.EqualTo(0f), "standing: neutral stance");
            Assert.That(PlayerShadow.GaitSwing(AbyssConst.V_WALK, 0f, 1f), Is.EqualTo(PlayerShadow.LEG_SWING).Within(1e-4f), "extreme at foot contact");
            // continuous across a footstep (the phase wraps to 0 and the parity flips)
            Assert.That(PlayerShadow.GaitSwing(AbyssConst.V_WALK, 1f, 1f), Is.EqualTo(PlayerShadow.GaitSwing(AbyssConst.V_WALK, 0f, -1f)).Within(1e-4f));
            Assert.That(Mathf.Abs(PlayerShadow.GaitSwing(AbyssConst.V_BRISK, 0f, 1f)), Is.LessThanOrEqualTo(1.35f * PlayerShadow.LEG_SWING + 1e-4f));
            // standing: the torso faces the look; walking +Z: the head turns up to 50 degrees before the torso follows;
            // walking backwards: the torso faces the look
            Assert.That(PlayerShadow.BodyYawTarget(30f, 0f, 0f, 0f), Is.EqualTo(30f).Within(1e-3f));
            Assert.That(PlayerShadow.BodyYawTarget(20f, 0f, 1.5f, 1.5f), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(PlayerShadow.BodyYawTarget(80f, 0f, 1.5f, 1.5f), Is.EqualTo(80f - PlayerShadow.HEAD_TURN_MAX).Within(1e-3f));
            Assert.That(Mathf.DeltaAngle(PlayerShadow.BodyYawTarget(0f, 0f, -1.5f, 1.5f), 0f), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void HallVibration_RmsAndImpulse()
        {
            // 9 Hz band RMS 3.0 +/- 0.5 mm, sway peak 12 +/- 1 mm, combined RMS <= 10 mm, impulse peak 6 +/- 1 mm, every 15 s
            const double fs = 600.0;
            double sumY = 0.0, sumZ = 0.0, peakZ = 0.0;
            int n = 0;
            for (double t = 1000.0; t < 1060.0; t += 1.0 / fs)
            {
                Vector3 v = HeadMotion.HallVibration(t, 1u);
                double band = v.y + HeadMotion.HALL_IMPULSE * HeadMotion.ImpulseEnvelope(HeadMotion.DeadCentreAge(t));
                sumY += band * band;
                sumZ += v.z * (double)v.z;
                peakZ = System.Math.Max(peakZ, System.Math.Abs(v.z));
                n++;
            }
            double rmsBand = System.Math.Sqrt(sumY / n), rmsSway = System.Math.Sqrt(sumZ / n);
            Assert.That(rmsBand * 1000.0, Is.EqualTo(3.0).Within(0.5));
            Assert.That(peakZ * 1000.0, Is.EqualTo(12.0).Within(1.0));
            Assert.That(System.Math.Sqrt(rmsBand * rmsBand + rmsSway * rmsSway) * 1000.0, Is.LessThanOrEqualTo(10.0));
            double peakImp = 0.0;
            for (double a = 0.0; a < 1.0; a += 0.001) peakImp = System.Math.Max(peakImp, HeadMotion.ImpulseEnvelope(a));
            Assert.That(peakImp * HeadMotion.HALL_IMPULSE * 1000.0, Is.EqualTo(6.0).Within(1.0));
            Assert.That(HeadMotion.ImpulseEnvelope(HeadMotion.HALL_IMPULSE_DECAY), Is.EqualTo(0f));   // exactly zero at the end of the decay
            Assert.That(HeadMotion.ImpulseEnvelope(HeadMotion.HALL_IMPULSE_DECAY - 1e-3), Is.LessThan(1e-5f));
            // dead centres every 15 s: the age wraps at t = 199.69 + 15 n
            Assert.That(HeadMotion.DeadCentreAge(199.69 + 15.0 * 7 + 0.5), Is.EqualTo(0.5).Within(1e-6));
        }

        [Test]
        public void TreadDip_SecondDifferenceBudget()
        {
            // 1.0 mm raised cosine over 100 ms: the 60 Hz second difference stays under 0.6 mm
            const float dt = 1f / 60f;
            float maxD2 = 0f, peak = 0f;
            for (float t = -0.05f; t < 0.2f; t += 0.001f)
            {
                float d2 = HeadMotion.TreadDip(t + dt) - 2f * HeadMotion.TreadDip(t) + HeadMotion.TreadDip(t - dt);
                maxD2 = Mathf.Max(maxD2, Mathf.Abs(d2));
                peak = Mathf.Min(peak, HeadMotion.TreadDip(t));
            }
            Assert.That(-peak, Is.EqualTo(HeadMotion.TREAD_DIP).Within(1e-5f));
            Assert.That(maxD2, Is.LessThanOrEqualTo(0.0006f));
        }
    }
}
