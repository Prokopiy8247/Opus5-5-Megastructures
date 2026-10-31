// CameraRig: first-person look and the camera pose (MASTER_PROMPT Part 9, 8.9, 14.12 FOV(s)).
// Mouse look from raw Mouse.current.delta counts (no smoothing, no acceleration, Y not inverted), gamepad right
// stick as a rate; world yaw (from +Z toward +X) and pitch (up positive), pitch clamped to +-88, never roll. The
// camera sits at the player's eye; the vertical FOV is ThreadMath.Fov(s) every frame (Hor+ is Unity's default for a
// vertical FOV). The AutoWalker drives the look through SetLook; the capture harness overrides the whole pose.
// Update (-50) integrates the look before AutoWalker (-40) and PlayerController (-30) read it; LateUpdate places the
// camera after the player moved and before ProgressDirector (500) computes the uniforms. Zero allocations per frame.
using UnityEngine;

namespace Megastructure.Player
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour, ICameraControl
    {
        public static CameraRig Instance { get; private set; }

        [Tooltip("Degrees per raw mouse count (Part 9: raw input, no acceleration).")]
        public float mouseDegPerCount = PlayerTuning.MouseDegPerCount;
        [Tooltip("Gamepad right-stick look rate at full deflection (deg/s, quadratic response).")]
        public float stickDegPerSec = PlayerTuning.StickDegPerSec;

        Camera m_cam;
        float m_yaw, m_pitch;
        bool m_lookInput;
        int m_lastDriveFrame = -10;

        bool m_override;
        Vector3 m_ovPos;
        float m_ovYaw, m_ovPitch, m_ovFov;

        bool m_captureMode;

        // ---------------------------------------------------------------- ICameraControl
        public Camera Camera => m_cam;
        public float Yaw => m_yaw;
        public float Pitch => m_pitch;
        public bool PoseOverridden => m_override;
        public bool LookInputThisFrame => m_lookInput;

        void Awake()
        {
            Instance = this;
            m_cam = GetComponent<Camera>();
            ThreadServices.CameraControl = this;
            m_captureMode = CliArgs.CaptureDir != null;
            // 14.12: near 0.1 m, far 250 000 m (the scene builder sets the same; enforced here so nothing drifts)
            m_cam.nearClipPlane = ThreadConstants.CameraNear;
            m_cam.farClipPlane = ThreadConstants.CameraFar;

            double s = CliArgs.StartS;
            m_yaw = (float)PlayerMath.WrapDeg(ThreadMath.Heading(s) / ThreadMath.Deg);
            // the opening frame equals the first cinematic gaze cue (beat 1 looks up at the thread); elsewhere the default gaze
            var cues = ThreadConstants.GazeCues;
            m_pitch = (float)(s < 1.0 && cues.Length > 0 ? cues[0].pitch : ThreadConstants.GazeDefaultPitch);
            ApplyPose();
        }

        void Start()
        {
            if (!m_captureMode) ThreadInput.SetCursorLocked(true);
        }

        void OnDestroy()
        {
            if (ReferenceEquals(ThreadServices.CameraControl, this)) ThreadServices.CameraControl = null;
            if (Instance == this) Instance = null;
        }

        void OnApplicationFocus(bool focus)
        {
            bool paused = ThreadServices.Fade != null && ThreadServices.Fade.Paused;
            if (focus && !paused && !m_captureMode) ThreadInput.SetCursorLocked(true);
        }

        // ---------------------------------------------------------------- API
        public void SetLook(float worldYawDeg, float pitchDeg)
        {
            m_yaw = (float)PlayerMath.WrapDeg(worldYawDeg);
            m_pitch = Mathf.Clamp(pitchDeg, -PlayerTuning.PitchLimit, PlayerTuning.PitchLimit);
            m_lastDriveFrame = Time.frameCount;
        }

        public void SetPoseOverride(Vector3 position, float worldYawDeg, float pitchDeg, float fovDeg)
        {
            m_override = true;
            m_ovPos = position;
            m_ovYaw = (float)PlayerMath.WrapDeg(worldYawDeg);
            m_ovPitch = Mathf.Clamp(pitchDeg, -PlayerTuning.PitchLimit, PlayerTuning.PitchLimit);
            m_ovFov = fovDeg;
            ApplyPose();   // immediately, so a capture can render right after this call
        }

        public void ClearPoseOverride()
        {
            m_override = false;
            ApplyPose();
        }

        // ---------------------------------------------------------------- frame
        void Update()
        {
            m_lookInput = false;
            bool paused = ThreadServices.Fade != null && ThreadServices.Fade.Paused;
            if (paused) return;
            m_lookInput = ThreadInput.LookActive(PlayerTuning.LookThresholdCounts, PlayerTuning.StickThreshold);
            Vector2 d = ThreadInput.LookDegrees(mouseDegPerCount, stickDegPerSec);
            // While the AutoWalker drives the look it sets yaw/pitch absolutely each frame; sub-threshold mouse noise
            // is not integrated on top of it (above-threshold input cancels the autowalk in its own Update).
            if (Time.frameCount - m_lastDriveFrame <= 1) return;
            m_yaw = (float)PlayerMath.WrapDeg(m_yaw + d.x);
            m_pitch = Mathf.Clamp(m_pitch + d.y, -PlayerTuning.PitchLimit, PlayerTuning.PitchLimit);
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        void ApplyPose()
        {
            if (m_cam == null) return;
            if (m_override)
            {
                transform.SetPositionAndRotation(m_ovPos, Quaternion.Euler(-m_ovPitch, m_ovYaw, 0f));
                var p = ThreadServices.Player;
                double s = p != null ? p.S : ProgressDirector.S;
                m_cam.fieldOfView = m_ovFov > 0f ? m_ovFov : (float)ThreadMath.Fov(s);
                return;
            }
            var player = ThreadServices.Player;
            Vector3 pos = player != null ? player.EyePosition : transform.position;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(-m_pitch, m_yaw, 0f));   // never roll
            m_cam.fieldOfView = (float)ThreadMath.Fov(player != null ? player.S : ProgressDirector.S);
        }
    }
}
