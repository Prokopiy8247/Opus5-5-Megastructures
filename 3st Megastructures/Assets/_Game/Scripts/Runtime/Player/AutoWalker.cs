// AutoWalker: the cinematic autowalk and the deterministic test walk (MASTER_PROMPT Part 9, 14.5 "Gaze", 14.10.4).
// Cinematic (C key or -cinematic): walks the centre line from the current s at 1.5 m/s (2.5 with -brisk, -speed X
// overrides), slows to each held gaze cue's own speed factor (0.5x at the peak reveals, 1x in the calm beats; total
// extra time budgeted and capped), looks along the gaze cues of ThreadConstants.GazeCues (yaw relative to the path
// heading, pitch, hold, yaw and pitch at the end of the hold for slow pans) with a critically damped look (smooth
// time 1.2 s, no overshoot from rest) and a seeded handheld sway of at most 0.3 deg.
// Any player input cancels it. Test mode (StartTest, used by the QA harness): exactly 1.5 or 2.5 m/s from s = 0,
// gaze locked forward, no cue holds, no sway, not cancellable, ElapsedVirtual = virtual seconds to the rest at 860.
// Update (-40) runs after CameraRig.Update (-50) and before PlayerController.Update (-30). Zero allocations per frame.
using System;
using UnityEngine;

namespace Megastructure.Player
{
    /// <summary>
    /// Pure gaze-cue schedule (no UnityEngine state, EditMode-testable). A cue becomes active when s reaches its plan
    /// position and the previous hold has finished (holds queue, so the two s = 0 cues of beat 1 play in order, and a
    /// cue placed at or before the previous hold's end follows it directly: related looks are chained, PAC-r2-2);
    /// between holds the gaze returns to the default (yaw 0, pitch GazeDefaultPitch). While a cue is held the walk
    /// slows to the cue's own speed factor (slow-downs at the reveals, 14.1; PAC-r2-6); if the planned delay of the
    /// remaining cues exceeds the budget, every slow-down is scaled back by the same ratio (BudgetScale).
    /// </summary>
    public sealed class CinematicPlan
    {
        /// <summary>Cue holds longer than this mean "hold until the end" (the final cue of beat 7): no slow-down.</summary>
        public const double HoldForever = 1e6;
        /// <summary>
        /// Target total delay (s). 14.10.4: the cinematic walk must stay within 8 to 10.5 minutes; 573.3 s at 1.5 m/s
        /// plus this budget plus the start ease and the stop ramp gives about 10.35 min. ThreadConstants.CinematicExtraMax
        /// stays the hard cap.
        /// </summary>
        public const double DelayBudget = 45.0;
        /// <summary>Lowest per-cue speed factor. Part 9 / task B07 asks "eased slow-downs (about 0.7x)"; the reveal cues of
        /// beats 4, 6 and 7 use 0.5x so the budget is spent at the peaks instead of evenly (PAC-r2-6, DEVELOPMENT.md D-054).</summary>
        public const double MinSlowFactor = 0.5;
        /// <summary>Smooth time (s) of the speed factor: slow-downs and recoveries are eased, never abrupt.</summary>
        public const double FactorSmoothTime = 1.0;

        readonly GazeCue[] m_cues;
        int m_next;
        int m_active = -1;
        double m_holdT;
        double m_factor = 1.0, m_factorV;

        /// <summary>Scale (0..1] applied to every cue's slow-down (1 - speed) so the planned delay fits DelayBudget.</summary>
        public double BudgetScale { get; private set; } = 1.0;
        /// <summary>Planned delay (s) of the remaining cues before the budget scale: sum of hold x (1 - speed).</summary>
        public double PlannedDelay { get; private set; }
        public double ExtraTime { get; private set; }
        public int ActiveCue => m_active;
        public int CuesFired { get; private set; }

        public CinematicPlan(GazeCue[] cues) { m_cues = cues; }

        public void Begin(double s)
        {
            m_next = 0;
            while (m_next < m_cues.Length && m_cues[m_next].s < s - 0.01) m_next++;
            m_active = -1; m_holdT = 0;
            m_factor = 1.0; m_factorV = 0;
            ExtraTime = 0; CuesFired = 0;
            double delay = 0;
            for (int i = m_next; i < m_cues.Length; i++)
                if (m_cues[i].hold < HoldForever) delay += m_cues[i].hold * (1.0 - SpeedOf(m_cues[i]));
            PlannedDelay = delay;
            BudgetScale = delay > DelayBudget ? DelayBudget / delay : 1.0;
        }

        /// <summary>The cue's speed factor clamped to [MinSlowFactor, 1].</summary>
        public static double SpeedOf(in GazeCue c) => Math.Max(MinSlowFactor, Math.Min(1.0, c.speed));

        /// <summary>Advance by dt at progress s; returns the gaze target (yaw relative to the heading, pitch) and the speed factor.</summary>
        public void Step(double s, double dt, out double yawRel, out double pitch, out double speedFactor)
        {
            if (m_active >= 0)
            {
                m_holdT += dt;
                if (m_cues[m_active].hold < HoldForever && m_holdT >= m_cues[m_active].hold) m_active = -1;
            }
            if (m_active < 0 && m_next < m_cues.Length && s >= m_cues[m_next].s)
            {
                m_active = m_next++;
                m_holdT = 0;
                CuesFired++;
            }

            double target = 1.0;
            if (m_active >= 0)
            {
                GazeCue c = m_cues[m_active];
                double u = c.hold < HoldForever ? ThreadMath.Smoothstep(0, c.hold, m_holdT) : 0.0;
                yawRel = ThreadMath.Lerp(c.yaw, c.yawEnd, u);
                pitch = ThreadMath.Lerp(c.pitch, c.pitchEnd, u);
                if (c.hold < HoldForever && ExtraTime < ThreadConstants.CinematicExtraMax) target = 1.0 - (1.0 - SpeedOf(c)) * BudgetScale;
            }
            else
            {
                yawRel = 0;
                pitch = ThreadConstants.GazeDefaultPitch;
            }
            PlayerMath.CritDamp(ref m_factor, ref m_factorV, target, FactorSmoothTime, dt);
            if (m_factor > 1.0) { m_factor = 1.0; m_factorV = 0; }
            ExtraTime += (1.0 - m_factor) * dt;
            speedFactor = m_factor;
        }
    }

    [DefaultExecutionOrder(-40)]
    public class AutoWalker : MonoBehaviour
    {
        public static AutoWalker Instance { get; private set; }

        // Look rate caps (deg/s): a 160 deg cue swing stays a graceful pan instead of a whip (the damping itself is
        // the critically damped 1.2 s of 14.5; the cap only bounds the peak rate of the largest swings).
        public const double MaxYawRate = 60.0;
        public const double MaxPitchRate = 40.0;
        // Ease-in of the walk speed when the cinematic starts (s).
        public const double StartEase = 2.5;
        // Input ignored for this long after a start or an unpause (the key or click that caused it).
        public const double CancelGrace = 0.3;

        PlayerController m_player;
        CinematicPlan m_plan;

        bool m_active, m_test, m_completed;
        double m_elapsed;           // virtual seconds since start (the player's integration step)
        double m_sinceStart;
        double m_baseSpeed;
        double m_yaw, m_pitch, m_yawV, m_pitchV;   // damped gaze (world yaw, pitch; deg)
        double m_graceUntil;
        bool m_wasPaused;
        bool m_cinematicPending;

        bool m_captureMode;

        // seeded handheld sway: 3 incommensurate sines per axis; per-axis amplitudes sum to GazeSwayMaxDeg / sqrt 2,
        // so the combined yaw + pitch deviation never exceeds GazeSwayMaxDeg (0.3 deg, Part 9)
        readonly double[] m_swayFreq = new double[6];
        readonly double[] m_swayPhase = new double[6];
        readonly double[] m_swayAmp = new double[6];

        // ---------------------------------------------------------------- public state
        /// <summary>The walker is running (cinematic or test). Test mode turns it off once the player rests at s = 860.</summary>
        public bool Active => m_active;
        public bool TestMode => m_test;
        public bool Cinematic => m_active && !m_test;
        /// <summary>Test mode reached the goal and came to rest.</summary>
        public bool Completed => m_completed;
        /// <summary>Virtual seconds walked since StartTest / StartCinematic (frozen at completion in test mode).</summary>
        public double ElapsedVirtual => m_elapsed;
        /// <summary>Cinematic delay accumulated by the gaze slow-downs (s), at most ThreadConstants.CinematicExtraMax.</summary>
        public double ExtraTime => m_plan != null ? m_plan.ExtraTime : 0.0;
        public int CuesFired => m_plan != null ? m_plan.CuesFired : 0;
        public double BaseSpeed => m_baseSpeed;

        void Awake()
        {
            Instance = this;
            m_player = GetComponent<PlayerController>();
            m_plan = new CinematicPlan(ThreadConstants.GazeCues);
            var rng = new System.Random(CliArgs.Seed);
            double[] baseF = { 0.071, 0.113, 0.187 };   // Hz: slow, incommensurate (a breathing hand, never a buzz)
            double[] weight = { 0.5, 0.3, 0.2 };
            for (int i = 0; i < 6; i++)
            {
                m_swayFreq[i] = baseF[i % 3] * (0.85 + 0.3 * rng.NextDouble());
                m_swayPhase[i] = 2.0 * Math.PI * rng.NextDouble();
                m_swayAmp[i] = ThreadConstants.GazeSwayMaxDeg * weight[i % 3] / Math.Sqrt(2.0);
            }
            m_captureMode = CliArgs.CaptureDir != null;
            m_cinematicPending = CliArgs.Cinematic && !CliArgs.AutoTest && !m_captureMode;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        PlayerController Player
        {
            get
            {
                if (m_player == null) m_player = PlayerController.Instance;
                return m_player;
            }
        }

        // ---------------------------------------------------------------- API
        /// <summary>Starts the cinematic autowalk from the current progress.</summary>
        public void StartCinematic()
        {
            var p = Player;
            if (p == null) return;
            m_active = true; m_test = false; m_completed = false;
            m_elapsed = 0; m_sinceStart = 0;
            double sp = CliArgs.GetDouble("-speed", -1);
            m_baseSpeed = sp > 0 ? sp : (CliArgs.Brisk ? ThreadConstants.BriskSpeed : ThreadConstants.WalkSpeed);
            m_plan.Begin(p.S);
            var cam = ThreadServices.CameraControl;
            m_yaw = cam != null ? cam.Yaw : ThreadMath.Heading(p.S) / ThreadMath.Deg;
            m_pitch = cam != null ? cam.Pitch : ThreadConstants.GazeDefaultPitch;
            m_yawV = m_pitchV = 0;
            m_graceUntil = Time.unscaledTimeAsDouble + CancelGrace;
            p.DriveNoSmoothing = false;
            p.SetDrive(true, 0f, 0f);
            ThreadServices.Fade?.HideHint();
            Debug.Log($"[THREAD] cinematic autowalk on at s={p.S:F2} speed={m_baseSpeed:F2} budget scale={m_plan.BudgetScale:F3} (planned delay {m_plan.PlannedDelay:F1} s)");
        }

        /// <summary>QA test walk: from s = 0 at exactly 1.5 m/s (2.5 when brisk), gaze locked forward, deterministic.</summary>
        public void StartTest(bool brisk)
        {
            var p = Player;
            if (p == null) { Debug.LogError("[THREAD-FAIL] AutoWalker.StartTest: no PlayerController"); return; }
            m_active = true; m_test = true; m_completed = false;
            m_elapsed = 0; m_sinceStart = 0;
            m_baseSpeed = brisk ? ThreadConstants.BriskSpeed : ThreadConstants.WalkSpeed;
            p.Teleport(0.0, 0.0);
            p.DriveNoSmoothing = true;
            p.SetDrive(true, (float)m_baseSpeed, 0f);
            m_yaw = ThreadMath.Heading(0.0) / ThreadMath.Deg;
            m_pitch = ThreadConstants.GazeDefaultPitch;
            m_yawV = m_pitchV = 0;
            ThreadServices.CameraControl?.SetLook((float)m_yaw, (float)m_pitch);
            ThreadServices.Fade?.HideHint();
            Debug.Log($"[THREAD] autowalk test start speed={m_baseSpeed:F2} m/s");
        }

        /// <summary>Stops the walker and hands control back to the player.</summary>
        public void Stop()
        {
            if (!m_active) return;
            m_active = false;
            var p = Player;
            if (p != null) p.SetDrive(false, 0f, 0f);
            Debug.Log($"[THREAD] autowalk off at s={(p != null ? p.S : 0):F2} after {m_elapsed:F2} s (extra {ExtraTime:F2} s)");
            m_test = false;
        }

        public void ToggleCinematic()
        {
            if (m_active) Stop(); else StartCinematic();
        }

        // ---------------------------------------------------------------- frame
        void Update()
        {
            var p = Player;
            if (p == null) return;
            bool paused = ThreadServices.Fade != null && ThreadServices.Fade.Paused;
            double now = Time.unscaledTimeAsDouble;
            if (paused) { m_wasPaused = true; return; }
            if (m_wasPaused) { m_wasPaused = false; m_graceUntil = now + CancelGrace; }

            bool ready = GameBootstrap.Instance == null || GameBootstrap.Ready;
            if (m_cinematicPending && ready) { m_cinematicPending = false; StartCinematic(); }

            if (!m_test && !m_captureMode && ThreadInput.CinematicToggle()) { ToggleCinematic(); return; }
            if (!m_active) return;

            // any player input cancels the cinematic walk (never the test walk)
            if (!m_test && now >= m_graceUntil && ThreadInput.AnyCancelInput(PlayerTuning.LookThresholdCounts, PlayerTuning.StickThreshold))
            {
                Stop();
                return;
            }

            double dt = Math.Min(Time.deltaTime, PlayerTuning.MaxStepDt);   // the player integrates the same step
            if (dt <= 0 || !ready) return;

            // the player came to rest during its previous Update: the walk took exactly the steps counted so far
            if (m_test && p.Resting && !m_completed)
            {
                m_completed = true;
                Debug.Log($"[THREAD] autowalk test complete: s={p.S:F3} in {m_elapsed:F2} s virtual time");
                m_active = false;
                p.SetDrive(false, 0f, 0f);
                return;
            }
            m_elapsed += dt;   // this frame's step, integrated by PlayerController.Update right after
            m_sinceStart += dt;

            if (m_test) StepTest(p);
            else StepCinematic(p, dt);
        }

        void StepTest(PlayerController p)
        {
            if (!p.Finished) p.SetDrive(true, (float)m_baseSpeed, 0f);
            m_yaw = ThreadMath.Heading(p.S) / ThreadMath.Deg;
            m_pitch = ThreadConstants.GazeDefaultPitch;
            ThreadServices.CameraControl?.SetLook((float)m_yaw, (float)m_pitch);
        }

        void StepCinematic(PlayerController p, double dt)
        {
            double s = p.S;
            m_plan.Step(s, dt, out double yawRel, out double pitchT, out double factor);
            double ease = ThreadMath.Smoothstep(0, StartEase, m_sinceStart);
            if (!p.Finished) p.SetDrive(true, (float)(m_baseSpeed * factor * ease), 0f);

            // critically damped look toward the cue (shortest angular path), rate capped for the largest swings
            double yawT = ThreadMath.Heading(s) / ThreadMath.Deg + yawRel;
            double dy = PlayerMath.WrapDeg(yawT - m_yaw);
            double yawTarget = m_yaw + dy;
            double y0 = m_yaw, p0 = m_pitch;
            PlayerMath.CritDamp(ref m_yaw, ref m_yawV, yawTarget, ThreadConstants.GazeSmoothTime, dt);
            PlayerMath.CritDamp(ref m_pitch, ref m_pitchV, pitchT, ThreadConstants.GazeSmoothTime, dt);
            LimitRate(ref m_yaw, ref m_yawV, y0, MaxYawRate, dt);
            LimitRate(ref m_pitch, ref m_pitchV, p0, MaxPitchRate, dt);
            m_yaw = PlayerMath.WrapDeg(m_yaw);

            // handheld sway (seeded, smooth, <= 0.3 deg per axis), on top of the damped gaze
            double t = m_elapsed;
            double swayYaw = 0, swayPitch = 0;
            for (int i = 0; i < 3; i++) swayYaw += m_swayAmp[i] * Math.Sin(2.0 * Math.PI * m_swayFreq[i] * t + m_swayPhase[i]);
            for (int i = 3; i < 6; i++) swayPitch += m_swayAmp[i] * Math.Sin(2.0 * Math.PI * m_swayFreq[i] * t + m_swayPhase[i]);
            double swayIn = ThreadMath.Smoothstep(0, StartEase, m_sinceStart);   // no sway pop at the start
            ThreadServices.CameraControl?.SetLook((float)(m_yaw + swayYaw * swayIn), (float)(m_pitch + swayPitch * swayIn));
        }

        static void LimitRate(ref double x, ref double v, double x0, double maxRate, double dt)
        {
            double step = x - x0, lim = maxRate * dt;
            if (step > lim) { x = x0 + lim; v = maxRate; }
            else if (step < -lim) { x = x0 - lim; v = -maxRate; }
        }
    }
}
