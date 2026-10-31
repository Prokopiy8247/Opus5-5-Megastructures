// PlayerTests: pure-logic EditMode tests of the walker (MASTER_PROMPT Part 9, 14.4.3, 14.5.3, 14.10.4, 14.11, 14.12).
// Everything here runs the exact shipped functions of Megastructure.Player (PlayerMath, StopRamp, EyeFollower,
// FootstepCadence, CinematicPlan) without a scene, a device or a frame loop.
using System;
using Megastructure.Player;
using NUnit.Framework;

namespace Megastructure.Tests
{
    public class PlayerTests
    {
        const double Dt = 1.0 / 60.0;

        // ------------------------------------------------------------------ stop ramp (14.11)
        [TestCase(1.5)]
        [TestCase(2.5)]
        public void StopRamp_LandsAtGoal(double v0)
        {
            foreach (double dt in new[] { 1.0 / 30.0, 1.0 / 60.0, 1.0 / 144.0 })
            {
                // walk at v0 frame by frame; the trigger fires on the first frame with s >= 858 (overshoot included)
                double s = 850.0;
                while (s < ThreadConstants.BeaconTriggerS) s += v0 * dt;
                var ramp = StopRamp.Begin(s, v0);
                double t = 0, last = s, lastV = v0;
                int frames = 0;
                while (!ramp.Done(t) && frames < 100000)
                {
                    t += dt;
                    double p = ramp.PositionAt(t);
                    double v = ramp.SpeedAt(t);
                    Assert.GreaterOrEqual(p, last - 1e-12, "the ramp never moves backwards");
                    Assert.LessOrEqual(v, lastV + 1e-12, "the speed only decreases");
                    last = p; lastV = v; frames++;
                }
                Assert.AreEqual(ThreadConstants.GoalS, ramp.PositionAt(t), 0.05, $"rest point at v0={v0} dt={dt}");
            }

            // the spec form: trigger exactly at 858 gives T = 4 / v0 and a 2.0 m stop
            var exact = StopRamp.Begin(ThreadConstants.BeaconTriggerS, v0);
            Assert.AreEqual(4.0 / v0, exact.T, 1e-9);
            Assert.AreEqual(ThreadConstants.GoalS, exact.PositionAt(exact.T * (1 - 1e-12)), 1e-6);
            Assert.AreEqual(v0, exact.SpeedAt(0), 1e-9);
            Assert.AreEqual(0.0, exact.SpeedAt(exact.T), 1e-9);

            // the closed form equals the numeric integral of v(t) = v0 (1 - smoothstep(0, T, t))
            double sum = 0, h = exact.T / 20000.0;
            for (int i = 0; i < 20000; i++) sum += exact.SpeedAt((i + 0.5) * h) * h;
            Assert.AreEqual(2.0, sum, 1e-4);
        }

        [Test]
        public void StopRamp_SlowArrivalStaysFinite()
        {
            var r = StopRamp.Begin(ThreadConstants.BeaconTriggerS, 0.0);
            Assert.That(r.T, Is.GreaterThan(0).And.LessThan(20.0));
            Assert.AreEqual(ThreadConstants.GoalS, r.PositionAt(r.T + 1.0), 1e-9);
            var past = StopRamp.Begin(ThreadConstants.GoalS + 0.5, 1.5);
            Assert.AreEqual(ThreadConstants.GoalS + 0.5, past.PositionAt(1.0), 1e-9, "a trigger past the goal does not move back");
        }

        // ------------------------------------------------------------------ corridor (14.4.3)
        [Test]
        public void LateralClamp_MatchesCorridorTable()
        {
            Assert.AreEqual(12.0, PlayerMath.ClampLateral(40, 100), 1e-9);
            Assert.AreEqual(-14.0, PlayerMath.ClampLateral(150, -100), 1e-9);
            Assert.AreEqual(ThreadMath.StairWalkHalfWidth, PlayerMath.ClampLateral(280, 100), 1e-9);   // D-053 stair walk
            Assert.AreEqual(10.0, PlayerMath.ClampLateral(345, 100), 1e-9);
            Assert.AreEqual(ThreadConstants.HalfWidthDeck, PlayerMath.ClampLateral(500, 100), 1e-9);
            Assert.AreEqual(-ThreadConstants.HalfWidthDeck, PlayerMath.ClampLateral(850, -100), 1e-9);
            double prev = ThreadMath.HalfWidth(0);
            const double step = 0.05;
            for (double s = 0; s <= ThreadConstants.PathLength; s += step)
            {
                double h = ThreadMath.HalfWidth(s);
                Assert.That(h, Is.GreaterThanOrEqualTo(ThreadConstants.HalfWidthDeck - 1e-9).And.LessThanOrEqualTo(ThreadConstants.HalfWidthAvenue + 1e-9));
                Assert.AreEqual(h, PlayerMath.ClampLateral(s, 1e6), 1e-12);
                Assert.AreEqual(-h, PlayerMath.ClampLateral(s, -1e6), 1e-12);
                Assert.AreEqual(0.5 * h, PlayerMath.ClampLateral(s, 0.5 * h), 1e-12, "inside the corridor nothing is clamped");
                Assert.Less(Math.Abs(h - prev), 0.7 * step, $"half-width jumps at s={s}");   // smooth funnels (max slope 0.6), no steps
                prev = h;
            }
            Assert.AreEqual(0.0, PlayerMath.ClampS(-5), 1e-12);
            Assert.AreEqual(ThreadConstants.PathLength, PlayerMath.ClampS(900), 1e-12);
        }

        // ------------------------------------------------------------------ footsteps (14.3.5)
        [Test]
        public void Footsteps_SpacingOverWholePath()
        {
            var c = FootstepCadence.Create();
            Assert.IsFalse(c.Advance(0.0, 0.0), "no footstep before the player first moves");
            double s = 0, firstStepS = -1, lastStepS = -1;
            int[] perSurface = new int[5];
            double saltGapSum = 0, metalGapSum = 0, stoneGapSum = 0;
            int saltGaps = 0, metalGaps = 0, stoneGaps = 0;
            bool lastLeft = c.left;
            while (s < ThreadConstants.PathLength)
            {
                double sNew = Math.Min(ThreadConstants.PathLength, s + ThreadConstants.WalkSpeed * Dt);
                if (c.Advance(sNew - s, sNew))
                {
                    Assert.AreNotEqual(lastLeft, c.left, "feet alternate");
                    Assert.AreEqual(c.left ? -PlayerTuning.FootstepPan : PlayerTuning.FootstepPan, c.Pan, 1e-6);
                    lastLeft = c.left;
                    perSurface[(int)PlayerMath.Surface(sNew)]++;
                    if (firstStepS < 0) firstStepS = sNew;
                    else
                    {
                        double gap = sNew - lastStepS;
                        if (lastStepS > 2 && sNew < 238) { saltGapSum += gap; saltGaps++; }
                        else if (lastStepS > 242 && sNew < 318) { stoneGapSum += gap; stoneGaps++; }
                        else if (lastStepS > 402 && sNew < 838) { metalGapSum += gap; metalGaps++; }
                    }
                    lastStepS = sNew;
                }
                s = sNew;
            }
            // analytic count: (1 - FirstStepPhase) of a step to the first footfall, then 0.78 / 0.80 / 0.78 m
            double units = PlayerTuning.FirstStepPhase
                + ThreadConstants.StairStartZ / ThreadConstants.StepLength
                + (ThreadConstants.StairEndZ - ThreadConstants.StairStartZ) / ThreadConstants.StairStepLength
                + (ThreadConstants.PathLength - ThreadConstants.StairEndZ) / ThreadConstants.StepLength;
            Assert.AreEqual(Math.Floor(units), c.count, 1.0, "footstep count over s = 0..860 at 1.5 m/s");
            Assert.AreEqual((1.0 - PlayerTuning.FirstStepPhase) * ThreadConstants.StepLength, firstStepS, 0.03, "first footstep shortly after the first move");
            Assert.AreEqual(ThreadConstants.StepLength, saltGapSum / saltGaps, 0.005);
            Assert.AreEqual(ThreadConstants.StairStepLength, stoneGapSum / stoneGaps, 0.005);
            Assert.AreEqual(ThreadConstants.StepLength, metalGapSum / metalGaps, 0.005);
            Assert.AreEqual(100, perSurface[(int)FootSurface.Stone], 1, "one stone sound per two treads on the 200 treads");
            Assert.Greater(perSurface[(int)FootSurface.Salt], 300);
            Assert.Greater(perSurface[(int)FootSurface.Terrace], 95);
            Assert.Greater(perSurface[(int)FootSurface.Metal], 560);
            Assert.Greater(perSurface[(int)FootSurface.Pier], 20);
            // cadence 1.92 steps/s at 1.5 m/s and 3.2 steps/s at 2.5 m/s (14.3.5)
            Assert.AreEqual(1.92, ThreadConstants.WalkSpeed / ThreadConstants.StepLength, 0.01);
            Assert.AreEqual(3.2, ThreadConstants.BriskSpeed / ThreadConstants.StepLength, 0.01);
        }

        [Test]
        public void Footsteps_SurfacesAndGain()
        {
            Assert.AreEqual(FootSurface.Salt, PlayerMath.Surface(10));
            Assert.AreEqual(FootSurface.Salt, PlayerMath.Surface(239.9));
            Assert.AreEqual(FootSurface.Stone, PlayerMath.Surface(240));
            Assert.AreEqual(FootSurface.Terrace, PlayerMath.Surface(320));
            Assert.AreEqual(FootSurface.Metal, PlayerMath.Surface(400));
            Assert.AreEqual(FootSurface.Metal, PlayerMath.Surface(839.9));
            Assert.AreEqual(FootSurface.Pier, PlayerMath.Surface(840));
            Assert.AreEqual(1.0f, PlayerMath.FootstepGain(ThreadConstants.WalkSpeed), 1e-5f);
            Assert.Greater(PlayerMath.FootstepGain(ThreadConstants.BriskSpeed), PlayerMath.FootstepGain(ThreadConstants.WalkSpeed));
        }

        // ------------------------------------------------------------------ FOV (Part 9, 14.12)
        [Test]
        public void Fov_RateBelowOneDegreePerSecondAndEasedSegmentsLong()
        {
            const double ds = 0.01;
            double maxRate = 0, segStart = -1;
            int segments = 0;
            double minFov = double.MaxValue, maxFov = double.MinValue;
            for (double s = 0; s < ThreadConstants.PathLength; s += ds)
            {
                double f0 = ThreadMath.Fov(s), f1 = ThreadMath.Fov(s + ds);
                minFov = Math.Min(minFov, f0); maxFov = Math.Max(maxFov, f0);
                double rate = Math.Abs(f1 - f0) / ds;              // deg per metre
                maxRate = Math.Max(maxRate, rate);
                bool easing = rate > 1e-7;
                if (easing && segStart < 0) segStart = s;
                if (!easing && segStart >= 0)
                {
                    double seconds = (s - segStart) / ThreadConstants.WalkSpeed;
                    Assert.GreaterOrEqual(seconds, 6.0, $"eased FOV segment from s={segStart:F1} to {s:F1} lasts {seconds:F1} s");
                    segments++;
                    segStart = -1;
                }
            }
            Assert.Less(maxRate * ThreadConstants.WalkSpeed, 1.0, "FOV rate at 1.5 m/s");
            Assert.Less(maxRate * ThreadConstants.BriskSpeed, 1.0, "FOV rate at 2.5 m/s");
            Assert.GreaterOrEqual(segments, 6, "the six eased FOV changes of 14.12");
            Assert.GreaterOrEqual(minFov, 60.0 - 1e-9);
            Assert.LessOrEqual(maxFov, 68.0 + 1e-9);
            Assert.AreEqual(66.0, ThreadMath.Fov(ThreadConstants.GoalS), 1e-6, "the final frame is KF7b (FOV 66)");
        }

        // ------------------------------------------------------------------ eye follower (14.5.3)
        [Test]
        public void EyeFollower_NoSteppingOrLagOnStairsAndHelix()
        {
            var f = new EyeFollower();
            double s = 220;
            f.Reset(ThreadMath.PathY(s) + ThreadConstants.EyeHeight);
            double maxErr = 0, maxErrSlope = 0, maxDv = 0, prevV = 0;
            while (s < 460)
            {
                s += ThreadConstants.WalkSpeed * Dt;
                double target = ThreadMath.PathY(s) + ThreadConstants.EyeHeight;
                double y = f.Step(target, Dt, ThreadConstants.CameraYFollowTime);
                double err = Math.Abs(y - target);
                maxErr = Math.Max(maxErr, err);
                bool midSlope = (s > 250 && s < 310) || (s > 415 && s < 455);
                if (midSlope) maxErrSlope = Math.Max(maxErrSlope, err);
                maxDv = Math.Max(maxDv, Math.Abs(f.v - prevV));
                prevV = f.v;
            }
            Assert.Less(maxErr, 0.02, "eye error at the grade kinks (s = 240, 320, 396-404)");
            Assert.Less(maxErrSlope, 1e-3, "no lag on a constant slope (feed-forward)");
            Assert.Less(maxDv, 0.5, "vertical eye velocity stays continuous (no stepping)");
            // on the stairs the eye climbs at 0.675 m/s at 1.5 m/s
            Assert.AreEqual(ThreadConstants.StairSlope * ThreadConstants.WalkSpeed, 0.675, 1e-9);
        }

        [Test]
        public void EyeFollower_FrameRateIndependent()
        {
            double Run(double dt)
            {
                var f = new EyeFollower();
                double s = 236;
                f.Reset(ThreadMath.PathY(s) + ThreadConstants.EyeHeight);
                double t = 0, y = 0;
                while (t < 4.0) { t += dt; s += ThreadConstants.WalkSpeed * dt; y = f.Step(ThreadMath.PathY(s) + ThreadConstants.EyeHeight, dt, ThreadConstants.CameraYFollowTime); }
                return y - (ThreadMath.PathY(s) + ThreadConstants.EyeHeight);
            }
            Assert.AreEqual(Run(1.0 / 30.0), Run(1.0 / 240.0), 2e-3);
        }

        // ------------------------------------------------------------------ head bob (Part 9)
        [Test]
        public void HeadBob_TinyOffOnStairsNeverAboveLimit()
        {
            for (double s = 0; s <= ThreadConstants.PathLength; s += 0.5)
            {
                double a = PlayerMath.HeadBobAmplitude(s);
                Assert.LessOrEqual(a, ThreadConstants.HeadBobMax + 1e-12);
                if (s >= ThreadConstants.StairStartZ && s <= ThreadConstants.StairEndZ) Assert.AreEqual(0.0, a, 1e-12, $"bob on the stairs at s={s}");
                for (double ph = 0; ph < 1.0; ph += 0.125)
                    Assert.LessOrEqual(Math.Abs(PlayerMath.HeadBob(s, ph, ThreadConstants.BriskSpeed)), 0.5 * ThreadConstants.HeadBobMax + 1e-12);
            }
            Assert.AreEqual(ThreadConstants.HeadBobDeck, PlayerMath.HeadBobAmplitude(500), 1e-12, "metal deck amplitude");
            Assert.AreEqual(0.0, PlayerMath.HeadBob(500, 0.3, 0.0), 1e-12, "no bob at rest");
        }

        // ------------------------------------------------------------------ cinematic plan (Part 9, 14.5, 14.10.4)
        [Test]
        public void Cinematic_DurationWithinBudgetAndAllCuesPlayed()
        {
            var cues = ThreadConstants.GazeCues;
            var plan = new CinematicPlan(cues);
            plan.Begin(0.0);
            // per-cue speed factors (D-054): the planned delay is scaled into the budget, never above it
            Assert.That(plan.BudgetScale, Is.InRange(0.0, 1.0));
            Assert.LessOrEqual(plan.PlannedDelay * plan.BudgetScale, CinematicPlan.DelayBudget + 1e-9);
            foreach (GazeCue c in cues) Assert.That(CinematicPlan.SpeedOf(c), Is.InRange(CinematicPlan.MinSlowFactor, 1.0));
            double s = 0, t = 0;
            int lastActive = -1;
            var fireS = new double[cues.Length];
            for (int i = 0; i < fireS.Length; i++) fireS[i] = double.NaN;
            while (s < ThreadConstants.BeaconTriggerS && t < 3600)
            {
                plan.Step(s, Dt, out double yawRel, out double pitch, out double factor);
                Assert.That(factor, Is.InRange(CinematicPlan.MinSlowFactor - 1e-9, 1.0 + 1e-9));
                Assert.That(Math.Abs(pitch), Is.LessThanOrEqualTo(PlayerTuning.PitchLimit));
                if (plan.ActiveCue != lastActive && plan.ActiveCue >= 0) fireS[plan.ActiveCue] = s;
                lastActive = plan.ActiveCue;
                double ease = ThreadMath.Smoothstep(0, AutoWalker.StartEase, t);
                s += ThreadConstants.WalkSpeed * factor * ease * Dt;
                t += Dt;
            }
            var ramp = StopRamp.Begin(s, ThreadConstants.WalkSpeed);
            double total = t + ramp.T;
            Assert.That(total, Is.InRange(8.0 * 60.0, 10.5 * 60.0), $"cinematic walk lasts {total / 60.0:F2} min");
            Assert.LessOrEqual(plan.ExtraTime, ThreadConstants.CinematicExtraMax + 1e-9);
            Assert.AreEqual(cues.Length, plan.CuesFired, "every gaze cue plays");
            for (int i = 0; i < cues.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(fireS[i]), $"cue {i} at s={cues[i].s} never fired");
                Assert.Less(fireS[i] - cues[i].s, 15.0, $"cue {i} (s={cues[i].s}) fired late at s={fireS[i]:F1}");
                Assert.GreaterOrEqual(fireS[i], cues[i].s - 1e-9);
            }
        }

        [Test]
        public void Cinematic_DefaultGazeBetweenCues()
        {
            var plan = new CinematicPlan(ThreadConstants.GazeCues);
            plan.Begin(31.0);   // after the beat-1 opening cues, before (60, 0, +2)
            plan.Step(40.0, Dt, out double yaw, out double pitch, out double factor);
            Assert.AreEqual(0.0, yaw, 1e-12);
            Assert.AreEqual(ThreadConstants.GazeDefaultPitch, pitch, 1e-12);
            Assert.AreEqual(1.0, factor, 1e-6);
            plan.Step(60.0, Dt, out yaw, out pitch, out factor);
            Assert.AreEqual(2.0, pitch, 1e-9, "cue (60, 0, +2) is active at s = 60");
        }

        [Test]
        public void WrapDeg_StaysInRange()
        {
            for (double a = -1000; a <= 1000; a += 7.3)
            {
                double w = PlayerMath.WrapDeg(a);
                Assert.That(w, Is.GreaterThanOrEqualTo(-180.0).And.LessThan(180.0));
                double d = (a - w) / 360.0;
                Assert.AreEqual(Math.Round(d), d, 1e-9);
            }
        }
    }
}
