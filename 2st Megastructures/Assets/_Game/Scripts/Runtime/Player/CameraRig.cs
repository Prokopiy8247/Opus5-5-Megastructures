using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure
{
    /// <summary>
    /// First-person camera on the Camera child of the player (Part 9, 14.4 Colliders (4)).
    /// - Look: raw mouse delta (0.08 degrees per pixel, no smoothing, no acceleration, Y not inverted) and the right stick
    ///   (120 degrees/s); pitch clamped to +-85; NO roll ever (rotation = Euler(-pitch, yaw, 0)).
    /// - Eye 1.7 m above the feet with critically damped vertical smoothing (~0.06 s) of step offsets; the analytic
    ///   ramp / bridge floor is fed forward so slopes never lag.
    /// - Head bob 0.008 m vertical at most, manual walking only (none in cinematic, autotest or capture).
    /// - Camera safety: each LateUpdate a 0.2 m sphere at the camera must overlap no collider (the player's own
    ///   CharacterController ignored), else it is pushed out (Physics.ComputePenetration); violations are counted for QA.
    /// - External drive: SetLook + ExternalLook (AutoWalker), SetPose + ExternalPose (pre-warm, captures).
    /// Writes RingRig.Camera, RingRig.Yaw, RingRig.Pitch. Allocation-free per frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        /// <summary>Frames in which the 0.2 m camera sphere overlapped a collider by more than 1 mm (QA expects 0).</summary>
        public static int Violations;
        /// <summary>Largest penetration depth seen (m).</summary>
        public static float MaxPenetration;
        /// <summary>Largest |roll| of the camera transform seen (degrees; must stay 0).</summary>
        public static float MaxRollDeg;

        public const float MouseSensitivity = 0.08f;   // degrees per pixel
        public const float StickRate = 120f;           // degrees per second
        public const float PitchLimit = 85f;
        public const float ProbeRadius = 0.2f;         // 14.4 Colliders (4)
        const float EyeOmega = 1f / 0.06f;             // vertical eye smoothing time constant ~0.06 s
        const float PenetrationTolerance = 0.001f;

        /// <summary>When true the AutoWalker owns yaw / pitch (user look is ignored).</summary>
        [System.NonSerialized] public bool ExternalLook;
        /// <summary>When true the camera pose is set from outside (pre-warm, captures): no smoothing, bob or push-out.</summary>
        [System.NonSerialized] public bool ExternalPose;

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public Camera Cam => _cam;

        Camera _cam;
        float _yaw, _pitch;
        float _eyeY, _eyeV, _prevFloor; bool _eyeInit;
        Vector3 _poseEye; float _poseYaw, _posePitch;
        InputAction _lookStick;
        SphereCollider _probe;
        readonly Collider[] _buf = new Collider[32];
        bool _inputBlocked;

        public static void ResetStatics() { Violations = 0; MaxPenetration = 0f; MaxRollDeg = 0f; }

        void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            RingRig.Camera = _cam;
            _lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", processors: "stickDeadzone");
            var go = new GameObject("CameraProbe");
            go.hideFlags = HideFlags.DontSave;
            go.layer = 2;   // Ignore Raycast
            go.transform.position = new Vector3(0f, -1000f, 0f);
            _probe = go.AddComponent<SphereCollider>();
            _probe.isTrigger = true;
            _probe.radius = ProbeRadius;
        }

        void OnEnable() { _lookStick.Enable(); }
        void OnDisable() { _lookStick.Disable(); }

        void Start()
        {
            _inputBlocked = CliArgs.AutoTest || CliArgs.Capturing || CliArgs.WallTest;
            Vector3 e = transform.eulerAngles;
            if (!ExternalLook) { _yaw = Wrap(e.y); _pitch = Mathf.Clamp(-Wrap(e.x), -PitchLimit, PitchLimit); }
        }

        void OnDestroy()
        {
            _lookStick?.Dispose();
            if (_probe != null) Destroy(_probe.gameObject);
            if (Instance == this) Instance = null;
        }

        static float Wrap(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;

        /// <summary>FOV policy of Part 8.10 / 14.1 (Hor+): the schedule's vFOV is the vertical FOV at every aspect
        /// (16:9, 16:10, 21:9), so the horizontal FOV follows the aspect. Unity's Camera.fieldOfView is vertical, so the
        /// value is used unchanged (QA: RingQaHooks.VfovForAspect by reflection).</summary>
        public static float VfovForAspect(float vfov169, float aspect) => vfov169;

        /// <summary>Horizontal FOV (degrees) for a vertical FOV and an aspect ratio.</summary>
        public static float HorizontalFov(float vfov, float aspect)
            => 2f * Mathf.Atan(Mathf.Tan(0.5f * vfov * Mathf.Deg2Rad) * Mathf.Max(0.01f, aspect)) * Mathf.Rad2Deg;

        /// <summary>Sets yaw / pitch in degrees (0 = +Z, 90 = +X, pitch up positive).</summary>
        public void SetLook(float yaw, float pitch)
        {
            _yaw = Wrap(yaw);
            _pitch = Mathf.Clamp(pitch, -PitchLimit, PitchLimit);
        }

        /// <summary>Puts the camera at a fixed pose and keeps it there (ExternalPose) until <see cref="ReleasePose"/>.</summary>
        public void SetPose(Vector3 eye, float yaw, float pitch)
        {
            ExternalPose = true;
            _poseEye = eye; _poseYaw = yaw; _posePitch = Mathf.Clamp(pitch, -89.9f, 89.9f);
            transform.SetPositionAndRotation(eye, Quaternion.Euler(-_posePitch, yaw, 0f));
            RingRig.Yaw = Wrap(yaw); RingRig.Pitch = _posePitch;
        }

        public void ReleasePose()
        {
            ExternalPose = false;
            _eyeInit = false;
        }

        /// <summary>Restarts the eye smoothing at the current feet height (after a teleport).</summary>
        public void SnapEye() { _eyeInit = false; }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (ExternalPose)
            {
                transform.SetPositionAndRotation(_poseEye, Quaternion.Euler(-_posePitch, _poseYaw, 0f));
                RingRig.Yaw = Wrap(_poseYaw); RingRig.Pitch = _posePitch; RingRig.Camera = _cam;
                return;
            }

            // look
            if (!ExternalLook && !_inputBlocked && !RingState.Paused && RingRig.WorldReady)
            {
                var m = Mouse.current;
                if (m != null)
                {
                    Vector2 md = m.delta.ReadValue();
                    _yaw += md.x * MouseSensitivity;
                    _pitch += md.y * MouseSensitivity;
                }
                Vector2 st = _lookStick.ReadValue<Vector2>();
                _yaw += st.x * StickRate * dt;
                _pitch += st.y * StickRate * dt;
                _yaw = Wrap(_yaw);
                _pitch = Mathf.Clamp(_pitch, -PitchLimit, PitchLimit);
            }

            // eye height with step smoothing and slope feed-forward
            Transform feetT = RingRig.Player != null ? RingRig.Player : transform.parent;
            Vector3 feet = feetT != null ? feetT.position : transform.position - Vector3.up * RingConstants.Eye;
            float target = feet.y + RingConstants.Eye;
            float floor = PathGuide.SmoothFloor(feet.x, feet.z);
            if (!_eyeInit || Mathf.Abs(target - _eyeY) > 0.6f || dt <= 0f)
            {
                _eyeY = target; _eyeV = 0f; _eyeInit = true;
            }
            else
            {
                _eyeY += floor - _prevFloor;
                float e = RingMath.CriticalDamp(_eyeY - target, ref _eyeV, 0f, EyeOmega, dt);
                _eyeY = target + e;
            }
            _prevFloor = floor;

            float bob = 0f;
            var pc = PlayerController.Instance;
            if (pc != null && !AutoWalker.Active && !RingState.Capture && !_inputBlocked)
                bob = -RingConstants.HeadBob * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * pc.StepPhase)) * pc.BobWeight;

            Vector3 pos = new Vector3(feet.x, _eyeY + bob, feet.z);
            pos = PushOut(pos);
            transform.SetPositionAndRotation(pos, Quaternion.Euler(-_pitch, _yaw, 0f));

            RingRig.Camera = _cam;
            RingRig.Yaw = _yaw;
            RingRig.Pitch = _pitch;
            float roll = Mathf.Abs(Wrap(transform.eulerAngles.z));
            if (roll > MaxRollDeg) MaxRollDeg = roll;
        }

        Vector3 PushOut(Vector3 pos)
        {
            bool violated = false;
            for (int iter = 0; iter < 3; iter++)
            {
                int n = Physics.OverlapSphereNonAlloc(pos, ProbeRadius, _buf, ~0, QueryTriggerInteraction.Ignore);
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    var c = _buf[i];
                    if (c == null || c == RingRig.Controller || c == _probe) continue;
                    Transform ct = c.transform;
                    if (Physics.ComputePenetration(_probe, pos, Quaternion.identity, c, ct.position, ct.rotation, out Vector3 dir, out float dist)
                        && dist > PenetrationTolerance)
                    {
                        pos += dir * (dist + 0.0005f);
                        if (dist > MaxPenetration) MaxPenetration = dist;
                        any = true;
                    }
                }
                if (!any) break;
                violated = true;
            }
            if (violated) Violations++;
            return pos;
        }
    }
}
