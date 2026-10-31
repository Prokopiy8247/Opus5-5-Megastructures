using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure
{
    /// <summary>
    /// First-person movement on the flat world (Part 9, 14.4 Colliders (3), (5)). New Input System actions are created
    /// in code: WASD + arrows / left stick move, Shift or right shoulder brisk, C cinematic toggle, Esc or Start pause,
    /// F11 and Alt+Enter fullscreen toggle. Look is read by the CameraRig. Speeds 1.5 / 2.5 m/s with critically damped
    /// acceleration smoothing (0.2 s), gravity with ground snapping (ramp and bridge stay glued), no jump or crouch.
    /// Distance-based footsteps (stride 0.75 m walk, 0.85 m brisk, alternating feet) raise RingEvents.Footstep.
    /// Safety volume: y &lt; -3 or more than 1 m outside the lane union snaps to the last safe point.
    /// In -autotest, -capture and -walltest the user input is ignored entirely. Allocation-free per frame.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        /// <summary>Number of safety-volume snaps (QA expects 0).</summary>
        public static int SafetySnaps { get; private set; }

        // ---------------- numbers ----------------
        const float AccelOmega = 4.74f / RingConstants.Accel;   // critically damped: 95 % of the target speed after 0.2 s
        const float Gravity = 9.81f;
        const float StickVel = 2f;          // downward velocity while grounded (keeps contact on the 9 degree bridge slope)
        const float MaxFall = 25f;
        const float SnapDist = 0.35f;       // ground snap reach after leaving the ground without falling
        const float Skin = 0.03f;           // CharacterController skin width (feet transform = floor level)
        const float GrassLateral = 1.5f;    // 14.4: off the 3 m path strip = grass
        const float CancelMouse = 2f;       // px per frame of mouse motion that cancels the cinematic walk
        const float SafetyDrop = -3f;       // 14.4 Colliders (5)
        const float SafetyOutside = 1f;

        /// <summary>0..1 position inside the current stride (head bob phase).</summary>
        public float StepPhase { get; private set; }
        /// <summary>0..1 head-bob weight (manual walking only).</summary>
        public float BobWeight { get; private set; }
        public bool Grounded { get; private set; }
        /// <summary>True when the last movement came from the user (not the AutoWalker).</summary>
        public bool ManualMotion { get; private set; }

        CharacterController _cc;
        InputAction _move, _brisk, _cinematic, _pause, _fullscreen, _lookStick;
        bool _inputBlocked;
        Vector2 _vel, _velV;
        float _vy;
        float _strideAcc, _stillT;
        int _foot;
        bool _movedRaised;
        Vector3 _extDelta; bool _extPending;
        Vector3 _lastSafe; bool _hasSafe;
        bool _altEnterPending; float _altEnterT; FullScreenMode _altEnterMode;
        bool _subscribed;

        public static void ResetStatics() { SafetySnaps = 0; }

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _cc.radius = RingConstants.PlayerRadius;
            _cc.height = RingConstants.PlayerHeight;
            _cc.skinWidth = Skin;
            _cc.center = new Vector3(0f, 0.5f * RingConstants.PlayerHeight + Skin, 0f);
            _cc.stepOffset = RingConstants.StepOffset;
            _cc.slopeLimit = RingConstants.SlopeLimit;
            _cc.minMoveDistance = 0f;
            RingRig.Player = transform;
            RingRig.Controller = _cc;

            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone");
            _brisk = new InputAction("Brisk", InputActionType.Button);
            _brisk.AddBinding("<Keyboard>/leftShift");
            _brisk.AddBinding("<Keyboard>/rightShift");
            _brisk.AddBinding("<Gamepad>/rightShoulder");
            _cinematic = new InputAction("Cinematic", InputActionType.Button, "<Keyboard>/c");
            _pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");
            _fullscreen = new InputAction("Fullscreen", InputActionType.Button, "<Keyboard>/f11");
            _lookStick = new InputAction("LookStickProbe", InputActionType.Value, "<Gamepad>/rightStick", processors: "stickDeadzone");
        }

        void OnEnable()
        {
            _move.Enable(); _brisk.Enable(); _cinematic.Enable(); _pause.Enable(); _fullscreen.Enable(); _lookStick.Enable();
        }

        void OnDisable()
        {
            _move.Disable(); _brisk.Disable(); _cinematic.Disable(); _pause.Disable(); _fullscreen.Disable(); _lookStick.Disable();
        }

        void Start()
        {
            _inputBlocked = CliArgs.AutoTest || CliArgs.Capturing || CliArgs.WallTest;
            RingRig.Player = transform;
            RingRig.Controller = _cc;
            RingEvents.PauseChanged += OnPauseChanged;
            RingEvents.LoadComplete += OnLoadComplete;
            _subscribed = true;
            ApplyCursor();
        }

        void OnDestroy()
        {
            if (_subscribed)
            {
                RingEvents.PauseChanged -= OnPauseChanged;
                RingEvents.LoadComplete -= OnLoadComplete;
            }
            _move?.Dispose(); _brisk?.Dispose(); _cinematic?.Dispose(); _pause?.Dispose(); _fullscreen?.Dispose(); _lookStick?.Dispose();
            if (Instance == this) Instance = null;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) ApplyCursor();
        }

        void OnLoadComplete()
        {
            bool want = CliArgs.Cinematic || GameBootstrap.CinematicOnLoad;
            GameBootstrap.CinematicOnLoad = false;
            if (want && !_inputBlocked && !RingState.Cinematic) SetCinematic(true);
        }

        void OnPauseChanged(bool paused)
        {
            RingState.Paused = paused;
            ApplyCursor();
        }

        void ApplyCursor()
        {
            bool free = RingState.Paused && !_inputBlocked;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        /// <summary>Cinematic autowalk on / off (RingState.Cinematic + RingEvents.CinematicChanged, only on change).</summary>
        public static void SetCinematic(bool on)
        {
            if (RingState.Cinematic == on) return;
            RingState.Cinematic = on;
            RingEvents.RaiseCinematic(on);
        }

        /// <summary>Movement for this frame from the AutoWalker or a QA bot (world delta in metres, y ignored).</summary>
        public void DriveExternal(Vector3 horizontalDelta)
        {
            _extDelta = horizontalDelta;
            _extPending = true;
        }

        /// <summary>Places the feet at p (y = feet) facing yaw; resets velocities and the camera eye smoothing.</summary>
        public void Teleport(Vector3 feet, float yaw)
        {
            bool en = _cc.enabled;
            _cc.enabled = false;
            transform.position = feet;
            transform.rotation = Quaternion.identity;
            _cc.enabled = en;
            Physics.SyncTransforms();
            _vel = Vector2.zero; _velV = Vector2.zero; _vy = 0f;
            RingRig.PlayerVelocity = Vector3.zero;
            var rig = CameraRig.Instance;
            if (rig != null) { rig.SetLook(yaw, rig.Pitch); rig.SnapEye(); }
            _lastSafe = feet; _hasSafe = true;
        }

        void Update()
        {
            if (RingRig.Player == null) { RingRig.Player = transform; RingRig.Controller = _cc; }
            if (!RingRig.WorldReady) { _extPending = false; return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            SystemInput();
            if (RingState.Paused)
            {
                _vel = Vector2.zero; _velV = Vector2.zero; _extPending = false;
                RingRig.PlayerVelocity = Vector3.zero;
                return;
            }

            Vector3 delta;
            bool manual = false;
            if (_extPending)
            {
                delta = _extDelta; delta.y = 0f;
                _extPending = false;
                _vel = new Vector2(delta.x, delta.z) / dt;   // continuity when the user takes over
                _velV = Vector2.zero;
            }
            else
            {
                Vector2 target = Vector2.zero;
                if (!_inputBlocked && !AutoWalker.Active)
                {
                    Vector2 mv = _move.ReadValue<Vector2>();
                    if (mv.sqrMagnitude > 1f) mv.Normalize();
                    if (mv.sqrMagnitude > 0.01f)
                    {
                        manual = true;
                        if (!_movedRaised) { _movedRaised = true; RingEvents.RaisePlayerMoved(); }
                    }
                    float speed = _brisk.IsPressed() ? RingConstants.Brisk : RingConstants.Walk;
                    float yaw = RingRig.Yaw * Mathf.Deg2Rad;
                    Vector2 fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                    Vector2 right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
                    target = (fwd * mv.y + right * mv.x) * speed;
                }
                _vel.x = RingMath.CriticalDamp(_vel.x, ref _velV.x, target.x, AccelOmega, dt);
                _vel.y = RingMath.CriticalDamp(_vel.y, ref _velV.y, target.y, AccelOmega, dt);
                if (target.sqrMagnitude < 1e-8f && _vel.sqrMagnitude < 1e-6f) { _vel = Vector2.zero; _velV = Vector2.zero; }
                delta = new Vector3(_vel.x * dt, 0f, _vel.y * dt);
                manual |= _vel.sqrMagnitude > 0.0025f && !_inputBlocked && !AutoWalker.Active;
            }
            ManualMotion = manual;

            // gravity + ground snapping
            bool wasGrounded = _cc.isGrounded;
            if (wasGrounded && _vy <= 0f) _vy = -StickVel;
            else _vy = Mathf.Max(_vy - Gravity * dt, -MaxFall);
            delta.y = _vy * dt;
            Vector3 before = transform.position;
            _cc.Move(delta);
            if (wasGrounded && !_cc.isGrounded && _vy <= 0f) SnapDown();
            Grounded = _cc.isGrounded;
            Vector3 after = transform.position;
            Vector3 hv = (after - before) / dt; hv.y = 0f;
            RingRig.PlayerVelocity = hv;

            Footsteps(hv, after, dt);
            Safety(after);
        }

        void SnapDown()
        {
            Vector3 o = transform.position + Vector3.up * 0.3f;
            if (Physics.Raycast(o, Vector3.down, out RaycastHit hit, 0.3f + SnapDist, ~0, QueryTriggerInteraction.Ignore))
            {
                float drop = hit.distance - 0.3f;
                if (drop > 0f) _cc.Move(Vector3.down * (drop + 0.01f));
            }
        }

        void Footsteps(Vector3 hv, Vector3 pos, float dt)
        {
            float speed = hv.magnitude;
            float stride = Mathf.Lerp(RingConstants.StrideWalk, RingConstants.StrideBrisk, Mathf.InverseLerp(RingConstants.Walk, RingConstants.Brisk, speed));
            if (Grounded && speed > 0.05f)
            {
                _stillT = 0f;
                _strideAcc += speed * dt;
                if (_strideAcc >= stride)
                {
                    _strideAcc -= stride;
                    if (_strideAcc > stride) _strideAcc = 0f;
                    float s = RingState.S;
                    bool grass = s >= RingConstants.PadZ1 && s < 558f
                                 && !(s >= RingConstants.FootbridgeS0 && s < RingConstants.FootbridgeS1)
                                 && Mathf.Abs(pos.x) > GrassLateral;
                    RingEvents.RaiseFootstep(RingEvents.SurfaceAt(s, grass), _foot, speed);
                    _foot ^= 1;
                }
            }
            else
            {
                _stillT += dt;
                if (_stillT > 0.4f) _strideAcc = Mathf.Min(_strideAcc, 0.5f * stride);   // first step comes soon after a stop
            }
            StepPhase = Mathf.Clamp01(_strideAcc / stride);
            float bobTarget = (ManualMotion && Grounded && !AutoWalker.Active && !_inputBlocked && !RingState.Capture) ? Mathf.Clamp01(speed / RingConstants.Walk) : 0f;
            BobWeight += (bobTarget - BobWeight) * (1f - Mathf.Exp(-dt / 0.15f));
        }

        void Safety(Vector3 pos)
        {
            float outside = PathGuide.DistanceOutsideLanes(pos);
            if (pos.y < SafetyDrop || outside > SafetyOutside)
            {
                SafetySnaps++;
                float s = RingState.S;
                Vector3 target;
                if (_hasSafe && Mathf.Abs(ProgressDirector.ProjectS(_lastSafe, s) - s) < 6f) target = _lastSafe;
                else target = PathGuide.CentreAt(s);
                if (SafetySnaps <= 5) Debug.LogWarning("[Ring] safety snap " + SafetySnaps + " at s " + s.ToString("0.0") + " (y " + pos.y.ToString("0.00") + ", outside " + outside.ToString("0.00") + " m)");
                Teleport(target + Vector3.up * 0.02f, RingRig.Yaw);
                return;
            }
            if (Grounded && outside <= 0f && pos.y > -0.5f) { _lastSafe = pos; _hasSafe = true; }
        }

        // ---------------- system keys ----------------
        void SystemInput()
        {
            if (_inputBlocked) return;
            if (_pause.WasPressedThisFrame()) RingEvents.RaisePause(!RingState.Paused);
            if (_fullscreen.WasPressedThisFrame()) QualityBootstrap.ToggleFullscreen();
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed))
            {
                // the Windows player may toggle natively (PlayerSettings.allowFullscreenSwitch); toggle ourselves only
                // if the mode has not changed shortly after, so it never double-toggles
                _altEnterPending = true; _altEnterT = Time.unscaledTime; _altEnterMode = Screen.fullScreenMode;
            }
            if (_altEnterPending && Time.unscaledTime - _altEnterT > 0.4f)
            {
                _altEnterPending = false;
                if (Screen.fullScreenMode == _altEnterMode) QualityBootstrap.ToggleFullscreen();
            }
            if (RingState.Paused) return;
            if (_cinematic.WasPressedThisFrame()) { SetCinematic(!RingState.Cinematic); return; }
            if (RingState.Cinematic && ManualInputThisFrame()) SetCinematic(false);
        }

        bool ManualInputThisFrame()
        {
            if (_move.ReadValue<Vector2>().sqrMagnitude > 0.04f) return true;
            if (_brisk.WasPressedThisFrame()) return true;
            var m = Mouse.current;
            if (m != null && m.delta.ReadValue().sqrMagnitude > CancelMouse * CancelMouse) return true;
            if (_lookStick.ReadValue<Vector2>().sqrMagnitude > 0.09f) return true;
            return false;
        }
    }
}
