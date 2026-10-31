// PlayerMath: pure, frame-rate independent player logic in double (MASTER_PROMPT Part 9, 14.4.3, 14.5.3, 14.11).
// No MonoBehaviour state: PlayerController, AutoWalker and the EditMode tests (PlayerTests) share these exact
// functions, so the stop ramp, the eye follower, the footstep cadence and the head bob are tested as shipped.
using System;

namespace Megastructure.Player
{
    /// <summary>
    /// Player tuning that is not a Part 14 number (those live in ThreadConstants). Each value cites its spec line.
    /// Listed in the B07 report under contract_requests in case the lead wants them in ThreadConstants.
    /// </summary>
    public static class PlayerTuning
    {
        // Part 9 line 243 / R8 6: raw mouse counts to degrees ("about 0.06 to 0.10 deg/count").
        public const float MouseDegPerCount = 0.08f;
        // R8 6: gamepad look is rate based with a quadratic response.
        public const float StickDegPerSec = 120f;
        // Task B07: pitch clamp +-88 degrees (never roll).
        public const float PitchLimit = 88f;
        // Look input above these thresholds counts as "player input" (cancels the autowalk, hides the hint).
        public const float LookThresholdCounts = 3f;
        public const float StickThreshold = 0.3f;

        // Part 9 line 244 ("at most 1.5 cm"): salt / terrace bob, well below ThreadConstants.HeadBobMax.
        public const double HeadBobSalt = 0.006;
        // Drive (AutoWalker / WallBot): lateral steering gain (1/s) and cap (m/s) toward the lateral target.
        public const double DriveLateralGain = 1.5;
        public const double DriveLateralMaxSpeed = 1.2;
        // Movement integration step cap (s): a load hitch never teleports the player.
        public const double MaxStepDt = 0.1;
        // Stop ramp (14.11): v0 floor so T = 2 d / v0 stays finite when the trigger is reached very slowly.
        public const double StopRampMinSpeed = 0.3;
        // First footstep after 1 - FirstStepPhase of a step length (no footstep before the player first moves, 14.3.6 item 1).
        public const double FirstStepPhase = 0.55;
        // Footstep stereo pan (14.3.5, MASTER_PROMPT line 559: alternate pan +-0.15).
        public const float FootstepPan = 0.15f;

        // Physical safety (14.4.3): corridor colliders live on layer 30. Walls sit at h + PlayerRadius + WallGap;
        // the redundant capsule check uses PlayerRadius - CapsuleClearance, so a player clamped exactly to the
        // half-width keeps a 7 cm margin to every wall and never registers a false rejection.
        public const int CorridorLayer = 30;
        public const int CorridorMask = 1 << CorridorLayer;
        public const float WallGap = 0.03f;
        public const float CapsuleClearance = 0.04f;
        public const float CapsuleBottom = 0.30f;   // above the floor: clears the 0.175 m kink overlap of the slabs
        public const float CapsuleTop = 1.90f;
        public const float SlabTopBelowFloor = 0.05f;
    }

    public static class PlayerMath
    {
        // ------------------------------------------------------------------ corridor
        /// <summary>Lateral offset clamped to the walkable half-width of 14.4.3 (radial on the helix).</summary>
        public static double ClampLateral(double s, double lateral)
        {
            double h = ThreadMath.HalfWidth(s);
            return lateral < -h ? -h : (lateral > h ? h : lateral);
        }

        public static double ClampS(double s) => s < 0 ? 0 : (s > ThreadConstants.PathLength ? ThreadConstants.PathLength : s);

        // ------------------------------------------------------------------ surfaces and steps (14.3.5)
        public static FootSurface Surface(double s)
        {
            if (s < ThreadConstants.StairStartZ) return FootSurface.Salt;
            if (s < ThreadConstants.StairEndZ) return FootSurface.Stone;
            if (s < ThreadConstants.HelixStartS) return FootSurface.Terrace;
            if (s < ThreadConstants.HelixEndS) return FootSurface.Metal;
            return FootSurface.Pier;
        }

        /// <summary>Plan distance per footstep: 0.78 m, one stone sound per two treads (0.80 m) on the stairs.</summary>
        public static double StepLength(double s)
        {
            return s >= ThreadConstants.StairStartZ && s < ThreadConstants.StairEndZ ? ThreadConstants.StairStepLength : ThreadConstants.StepLength;
        }

        /// <summary>Footstep gain from the plan speed: 1.0 at 1.5 m/s, 1.2 at 2.5 m/s, softer when creeping.</summary>
        public static float FootstepGain(double speed)
        {
            double g = 0.7 + 0.2 * speed;
            return (float)(g < 0.75 ? 0.75 : (g > 1.2 ? 1.2 : g));
        }

        // ------------------------------------------------------------------ head bob (Part 9, 14.5.3, 14.5.5)
        /// <summary>
        /// Peak-to-peak bob amplitude (m) at s: salt and terrace HeadBobSalt, off on the stairs (s 240-320), the metal
        /// deck ThreadConstants.HeadBobDeck; every change is a 4 m smoothstep, so the amplitude never jumps.
        /// </summary>
        public static double HeadBobAmplitude(double s)
        {
            double salt = PlayerTuning.HeadBobSalt;
            double a = salt * (1.0 - ThreadMath.Smoothstep(ThreadConstants.StairStartZ - 4.0, ThreadConstants.StairStartZ, s));
            a += salt * ThreadMath.Smoothstep(ThreadConstants.StairEndZ, ThreadConstants.StairEndZ + 4.0, s) * (1.0 - ThreadMath.Smoothstep(ThreadConstants.HelixStartS, ThreadConstants.HelixStartS + 4.0, s));
            a += ThreadConstants.HeadBobDeck * ThreadMath.Smoothstep(ThreadConstants.HelixStartS, ThreadConstants.HelixStartS + 4.0, s);
            return Math.Min(a, ThreadConstants.HeadBobMax);
        }

        /// <summary>Vertical bob offset (zero mean, lowest at the footfall), scaled by speed / walk speed (0 at rest).</summary>
        public static double HeadBob(double s, double stepPhase, double speed)
        {
            double k = speed / ThreadConstants.WalkSpeed;
            k = k < 0 ? 0 : (k > 1 ? 1 : k);
            return -0.5 * HeadBobAmplitude(s) * k * Math.Cos(2.0 * Math.PI * stepPhase);
        }

        // ------------------------------------------------------------------ critically damped helpers
        /// <summary>
        /// Exact critically damped step toward a constant target (no overshoot from rest). omega = 2 / smoothTime, the
        /// same convention as Mathf.SmoothDamp. Frame-rate independent.
        /// </summary>
        public static void CritDamp(ref double x, ref double v, double target, double smoothTime, double dt)
        {
            if (dt <= 0) return;
            double w = 2.0 / Math.Max(1e-4, smoothTime);
            double e = x - target;
            double b = v + w * e;
            double ex = Math.Exp(-w * dt);
            x = target + (e + b * dt) * ex;
            v = (v - w * b * dt) * ex;
        }

        public static double WrapDeg(double a)
        {
            a %= 360.0;
            if (a >= 180.0) a -= 360.0;
            else if (a < -180.0) a += 360.0;
            return a;
        }
    }

    /// <summary>
    /// The end-of-path velocity ramp of 14.11: v(t) = v0 (1 - smoothstep(0, T, t)). The distance is v0 T / 2; with
    /// T = 2 (860 - s0) / v0 the player rests exactly at s = 860 (this is T = 4 / v0 when the trigger fires at 858).
    /// The position is the closed-form integral, so the landing point does not depend on the frame rate.
    /// </summary>
    public struct StopRamp
    {
        public double s0, v0, T;

        public static StopRamp Begin(double s0, double v0)
        {
            var r = new StopRamp();
            double d = Math.Max(0.0, ThreadConstants.GoalS - s0);
            r.s0 = s0;
            r.v0 = Math.Max(v0, PlayerTuning.StopRampMinSpeed);
            r.T = d > 1e-9 ? 2.0 * d / r.v0 : 0.0;
            return r;
        }

        /// <summary>Plan distance at time t after the trigger: s0 + v0 T (u - u^3 + u^4 / 2), u = t / T.</summary>
        public double PositionAt(double t)
        {
            if (T <= 0 || t >= T) return Math.Max(s0, ThreadConstants.GoalS);
            double u = t <= 0 ? 0 : t / T;
            return s0 + v0 * T * (u - u * u * u + 0.5 * u * u * u * u);
        }

        public double SpeedAt(double t) => T <= 0 ? 0 : v0 * (1.0 - ThreadMath.Smoothstep(0, T, t));
        /// <summary>Remaining fraction of the initial speed (lateral motion decays with the same ramp).</summary>
        public double SpeedFactor(double t) => T <= 0 ? 0 : 1.0 - ThreadMath.Smoothstep(0, T, t);
        public bool Done(double t) => t >= T;
    }

    /// <summary>
    /// Eye-height follower (14.5.3: "a critically damped follower of 0.12 s that only follows the analytic height").
    /// The error to the analytic target is integrated exactly with the target's own velocity as feed-forward, so on a
    /// constant slope (stairs 0.45, helix 0.25) the eye sits exactly at floor + 1.7 m with no lag, and at a grade kink
    /// (s = 240, 320, 840) the eye velocity stays continuous and the height error peaks below 1.5 cm.
    /// </summary>
    public struct EyeFollower
    {
        public double y, v;
        double m_lastTarget;
        bool m_init;

        public bool Initialised => m_init;

        public void Reset(double target)
        {
            y = target; v = 0; m_lastTarget = target; m_init = true;
        }

        public double Step(double target, double dt, double smoothTime)
        {
            if (!m_init || double.IsNaN(y) || Math.Abs(target - m_lastTarget) > 2.0) { Reset(target); return y; }
            if (dt <= 0) return y;
            double u = (target - m_lastTarget) / dt;           // analytic floor velocity this frame
            double w = 2.0 / Math.Max(1e-4, smoothTime);
            double e = y - m_lastTarget;                        // error relative to the target trajectory
            double ev = v - u;
            double b = ev + w * e;
            double ex = Math.Exp(-w * dt);
            double e1 = (e + b * dt) * ex;
            double ev1 = (ev - w * b * dt) * ex;
            y = target + e1;
            v = u + ev1;
            m_lastTarget = target;
            return y;
        }
    }

    /// <summary>Footstep cadence: one footfall per StepLength(s) of plan distance travelled, alternating feet.</summary>
    public struct FootstepCadence
    {
        public double phase;     // 0..1 within the current step (0 = footfall)
        public bool left;        // foot of the last footfall
        public int count;

        public static FootstepCadence Create()
        {
            var c = new FootstepCadence();
            c.phase = PlayerTuning.FirstStepPhase;
            return c;
        }

        /// <summary>Advance by a travelled plan distance (m) at progress s; true when a footfall happens.</summary>
        public bool Advance(double distance, double s)
        {
            if (distance <= 0) return false;
            phase += distance / StepLength(s);
            if (phase < 1.0) return false;
            phase -= Math.Floor(phase);   // a huge step (hitch) emits one footfall, never a burst
            left = !left;
            count++;
            return true;
        }

        static double StepLength(double s) => PlayerMath.StepLength(s);

        public float Pan => left ? -PlayerTuning.FootstepPan : PlayerTuning.FootstepPan;
    }
}
