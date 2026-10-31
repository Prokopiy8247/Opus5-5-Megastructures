// PlayerController: the walker in corridor coordinates (MASTER_PROMPT Part 9, 14.4.2 - 14.4.4, 14.11).
// State is (s, lateral) in double: s = plan distance along the path, lateral = offset to the right of the centre
// line (radial on the helix). The floor is the analytic PathY(s) (no physics stepping, no camera stepping on the
// stairs); the eye follows floor + 1.7 m through a critically damped follower. Primary safety is analytic (s and
// lateral are clamped); redundant physical safety is a capsule check against the invisible corridor colliders of
// PathGuide on layer 30 (a rejected move should never happen; rejections are counted).
// Execution order: CameraRig.Update (-50) -> AutoWalker.Update (-40) -> PlayerController.Update (-30) ->
// CameraRig.LateUpdate -> ProgressDirector.LateUpdate (500). Zero allocations per frame.
using System;
using UnityEngine;

namespace Megastructure.Player
{
    [DefaultExecutionOrder(-30)]
    public class PlayerController : MonoBehaviour, IPlayerState
    {
        public static PlayerController Instance { get; private set; }

        [Tooltip("Optional head bob (Part 9: tiny, never roll).")]
        public bool headBob = true;

        // ---------------------------------------------------------------- state (double)
        double m_s, m_lat;
        double m_velT, m_velR;          // plan velocity in the corridor frame (m/s): along the tangent, to the right
        double m_accT, m_accR;          // critically damped velocity state (acceleration smoothing 0.2 s)
        double m_speed;                 // plan speed magnitude (m/s)
        double m_bobSpeed, m_bobAcc;    // smoothed speed for the head-bob amplitude (no amplitude jump on a start or stop)
        bool m_brisk;
        EyeFollower m_eye;
        FootstepCadence m_steps;
        Vector3 m_eyePos;
        Vector3 m_feetPos;

        // drive (AutoWalker / WallBot)
        bool m_driveActive;
        double m_driveSpeed, m_driveLat;

        // goal / stop ramp (14.11)
        bool m_finished, m_resting;
        StopRamp m_ramp;
        double m_rampT;
        double m_rampLatVel;

        int m_rejections;
        bool m_loggedReject;
        const double BobSpeedSmoothTime = 0.3;

        // ---------------------------------------------------------------- IPlayerState
        public double S => m_s;
        public double Lateral => m_lat;
        public float Speed => (float)m_speed;
        public bool Brisk => m_brisk;
        public bool Finished => m_finished;
        public Vector3 EyePosition => m_eyePos;
        public bool Frozen { get; set; }

        // ---------------------------------------------------------------- extra public state (QA, AutoWalker, UI)
        /// <summary>True once the stop ramp has brought the player to rest at s = 860.</summary>
        public bool Resting => m_resting;
        /// <summary>True once the player (or a drive) has moved at least once (footsteps, hint).</summary>
        public bool HasMoved { get; private set; }
        /// <summary>Moves rejected by the redundant capsule check (must stay 0).</summary>
        public int Rejections => m_rejections;
        /// <summary>Feet position (floor point under the eye), world space.</summary>
        public Vector3 FeetPosition => m_feetPos;
        /// <summary>Test mode (AutoWalker.StartTest): the drive speed is applied exactly, without acceleration smoothing.</summary>
        public bool DriveNoSmoothing { get; set; }
        public bool DriveActive => m_driveActive;
        /// <summary>Plan speed along the path, ds/dt (m/s) this frame.</summary>
        public double PathSpeed { get; private set; }
        public int FootstepCount => m_steps.count;
        public double StepPhase => m_steps.phase;
        public double StopRampDuration => m_finished ? m_ramp.T : 0.0;
        public double StopRampElapsed => m_finished ? m_rampT : 0.0;

        // ---------------------------------------------------------------- lifecycle
        void Awake()
        {
            Instance = this;
            ThreadServices.Player = this;
            m_steps = FootstepCadence.Create();
            SetState(CliArgs.StartS, 0.0);
        }

        void OnDestroy()
        {
            if (ReferenceEquals(ThreadServices.Player, this)) ThreadServices.Player = null;
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- API
        public void Teleport(double s, double lateral)
        {
            SetState(s, lateral);
            m_steps = FootstepCadence.Create();
            if (m_s < ThreadConstants.BeaconTriggerS)
            {
                m_finished = false; m_resting = false; m_rampT = 0;
            }
            else if (m_finished)
            {
                m_resting = true;   // teleported while the ending runs: stay where placed
            }
        }

        public void SetDrive(bool active, float speed, float lateralTarget)
        {
            m_driveActive = active;
            m_driveSpeed = speed;
            m_driveLat = lateralTarget;
            if (!active) DriveNoSmoothing = false;
        }

        void SetState(double s, double lateral)
        {
            m_s = PlayerMath.ClampS(s);
            m_lat = PlayerMath.ClampLateral(m_s, lateral);
            m_velT = m_velR = m_accT = m_accR = 0;
            m_speed = 0; PathSpeed = 0; m_bobSpeed = m_bobAcc = 0;
            m_eye.Reset(ThreadMath.PathY(m_s) + ThreadConstants.EyeHeight);
            UpdatePose(0.0);
        }

        // ---------------------------------------------------------------- frame
        void Update()
        {
            double dt = Math.Min(Time.deltaTime, PlayerTuning.MaxStepDt);
            bool ready = GameBootstrap.Instance == null || GameBootstrap.Ready;
            bool paused = ThreadServices.Fade != null && ThreadServices.Fade.Paused;

            if (Frozen)
            {
                m_velT = m_velR = m_accT = m_accR = 0; m_speed = 0; PathSpeed = 0;
            }
            else if (dt > 0 && ready && !paused)
            {
                if (m_finished) StepStopRamp(dt);
                else StepMove(dt);
            }
            UpdatePose(Frozen || paused ? 0.0 : dt);
        }

        void StepMove(double dt)
        {
            // ---- target plan velocity in the corridor frame
            double tgtT, tgtR;
            if (m_driveActive)
            {
                tgtT = m_driveSpeed;
                double latErr = PlayerMath.ClampLateral(m_s, m_driveLat) - m_lat;
                tgtR = Math.Max(-PlayerTuning.DriveLateralMaxSpeed, Math.Min(PlayerTuning.DriveLateralMaxSpeed, latErr * PlayerTuning.DriveLateralGain));
                m_brisk = m_driveSpeed > 0.5 * (ThreadConstants.WalkSpeed + ThreadConstants.BriskSpeed);
            }
            else
            {
                Vector2 input = ThreadInput.Move();
                m_brisk = ThreadInput.Brisk();
                double speed = m_brisk ? ThreadConstants.BriskSpeed : ThreadConstants.WalkSpeed;
                // camera yaw relative to the path heading: forward = (cos rel, sin rel), right = (-sin rel, cos rel) in (T, R)
                var cam = ThreadServices.CameraControl;
                double heading = ThreadMath.Heading(m_s);
                double yaw = cam != null ? cam.Yaw * ThreadMath.Deg : heading;
                double rel = yaw - heading;
                double c = Math.Cos(rel), sn = Math.Sin(rel);
                tgtT = speed * (input.y * c - input.x * sn);
                tgtR = speed * (input.y * sn + input.x * c);
            }

            // ---- acceleration smoothing (critically damped, ~0.2 s), or exact speed in test mode
            if (m_driveActive && DriveNoSmoothing)
            {
                m_velT = tgtT; m_velR = tgtR; m_accT = m_accR = 0;
            }
            else
            {
                PlayerMath.CritDamp(ref m_velT, ref m_accT, tgtT, ThreadConstants.AccelSmoothTime, dt);
                PlayerMath.CritDamp(ref m_velR, ref m_accR, tgtR, ThreadConstants.AccelSmoothTime, dt);
            }

            // ---- integrate in corridor coordinates
            double toS = ThreadMath.ForwardToS(m_s, m_lat);
            double sNew = PlayerMath.ClampS(m_s + m_velT * toS * dt);
            double latNew = PlayerMath.ClampLateral(sNew, m_lat + m_velR * dt);
            if ((sNew <= 0 && m_velT < 0) || (sNew >= ThreadConstants.PathLength && m_velT > 0)) { m_velT = 0; m_accT = 0; }
            double h = ThreadMath.HalfWidth(sNew);
            if ((latNew >= h && m_velR > 0) || (latNew <= -h && m_velR < 0)) { m_velR = 0; m_accR = 0; }

            // ---- redundant physical safety: reject a move that would intersect the corridor colliders
            if (CapsuleBlocked(sNew, latNew))
            {
                m_rejections++;
                if (!m_loggedReject)
                {
                    m_loggedReject = true;
                    Debug.LogWarning($"[THREAD] corridor capsule rejected a move at s={sNew:F2} lateral={latNew:F3} (analytic clamp should prevent this)");
                }
                m_velT = m_velR = m_accT = m_accR = 0;
                m_speed = 0; PathSpeed = 0;
                return;
            }

            ApplyMove(sNew, latNew, toS, dt);

            // ---- beacon trigger (once): input and drive are replaced by the stop ramp of 14.11
            if (!m_finished && m_s >= ThreadConstants.BeaconTriggerS)
            {
                double v0 = Math.Max(0.0, PathSpeed);
                m_ramp = StopRamp.Begin(m_s, v0);
                m_rampT = 0;
                m_rampLatVel = m_velR;
                m_finished = true;
                Debug.Log($"[THREAD] beacon trigger at s={m_s:F3} v0={v0:F3} m/s, stop ramp T={m_ramp.T:F3} s");
                ThreadServices.Fade?.BeginEnding();
                ThreadServices.Audio?.BeginEnding();
            }
        }

        void StepStopRamp(double dt)
        {
            if (m_resting) { m_velT = m_velR = 0; m_speed = 0; PathSpeed = 0; return; }
            m_rampT += dt;
            double sNew = m_ramp.PositionAt(m_rampT);
            double k = m_ramp.SpeedFactor(m_rampT);
            double latNew = PlayerMath.ClampLateral(sNew, m_lat + m_rampLatVel * k * dt);
            double toS = ThreadMath.ForwardToS(m_s, m_lat);
            m_velT = m_ramp.SpeedAt(m_rampT) / Math.Max(1e-6, toS);
            m_velR = m_rampLatVel * k;
            ApplyMove(sNew, latNew, toS, dt);
            if (m_ramp.Done(m_rampT))
            {
                m_s = ThreadConstants.GoalS;
                m_resting = true;
                m_velT = m_velR = m_accT = m_accR = 0;
                m_speed = 0; PathSpeed = 0;
                Debug.Log($"[THREAD] player at rest at s={m_s:F3} after {m_rampT:F2} s of stop ramp");
            }
        }

        void ApplyMove(double sNew, double latNew, double toS, double dt)
        {
            double fwd = (sNew - m_s) / Math.Max(1e-6, toS);   // forward metres at the walker's own radius
            double side = latNew - m_lat;
            double dist = Math.Sqrt(fwd * fwd + side * side);
            m_s = sNew;
            m_lat = latNew;
            PathSpeed = dt > 0 ? (fwd * toS) / dt : 0;
            m_speed = dt > 0 ? dist / dt : 0;
            if (dist > 1e-6) HasMoved = true;

            // footsteps every 0.78 m of plan distance (0.80 m on the stone stairs), alternating pan
            if (m_steps.Advance(dist, m_s))
            {
                var audio = ThreadServices.Audio;
                if (audio != null) audio.Footstep(PlayerMath.Surface(m_s), m_steps.Pan, PlayerMath.FootstepGain(m_speed));
            }
        }

        void UpdatePose(double dt)
        {
            D3 feet = ThreadMath.CorridorToWorld(m_s, m_lat, 0.0);
            double target = feet.y + ThreadConstants.EyeHeight;
            double eyeY = dt > 0 ? m_eye.Step(target, dt, ThreadConstants.CameraYFollowTime) : (m_eye.Initialised ? m_eye.y : target);
            if (dt > 0) PlayerMath.CritDamp(ref m_bobSpeed, ref m_bobAcc, m_speed, BobSpeedSmoothTime, dt);
            if (headBob) eyeY += PlayerMath.HeadBob(m_s, m_steps.phase, m_bobSpeed);
            m_feetPos = new Vector3((float)feet.x, (float)feet.y, (float)feet.z);
            m_eyePos = new Vector3((float)feet.x, (float)eyeY, (float)feet.z);
            transform.position = m_feetPos;
        }

        // ---------------------------------------------------------------- physical safety
        /// <summary>Capsule of the walker at a corridor coordinate (bottom 0.30 m and top 1.90 m above the floor).</summary>
        public static void Capsule(double s, double lateral, out Vector3 p0, out Vector3 p1, out float radius)
        {
            D3 f = ThreadMath.CorridorToWorld(s, lateral, 0.0);
            radius = ThreadConstants.PlayerRadius - PlayerTuning.CapsuleClearance;
            var b = new Vector3((float)f.x, (float)f.y, (float)f.z);
            p0 = b + Vector3.up * (PlayerTuning.CapsuleBottom + radius);
            p1 = b + Vector3.up * (PlayerTuning.CapsuleTop - radius);
        }

        static bool CapsuleBlocked(double s, double lateral)
        {
            Capsule(s, lateral, out Vector3 p0, out Vector3 p1, out float r);
            return Physics.CheckCapsule(p0, p1, r, PlayerTuning.CorridorMask, QueryTriggerInteraction.Ignore);
        }
    }
}
