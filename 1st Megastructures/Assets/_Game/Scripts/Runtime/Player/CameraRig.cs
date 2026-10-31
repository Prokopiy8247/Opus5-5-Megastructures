using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Places the main camera once per frame in LateUpdate (execution order +100, after every Update and before the
    /// instanced draws at +200): position = player eye + <see cref="HeadMotion"/> offsets (tiny bob, stair tread dip, machine
    /// hall vibration), rotation = AbyssConst.YawPitch(PlayerController.Yaw, Pitch), so roll is always exactly zero.
    /// The AutoWalker's gaze arrives through the controller's Yaw / Pitch. No positional smoothing anywhere on the lift.
    ///
    /// Field of view is vertical (Unity's native Hor+): <see cref="BaseFov"/> and <see cref="FarClip"/> are set by
    /// ProgressDirector per beat, <see cref="FovOffset"/> by the AutoWalker (hold H7 push). Near plane 0.1 m.
    /// A capture pose (<see cref="SetCapturePose"/>) overrides everything until cleared.
    /// Publishes GameState.CameraPos / CameraRot / MainCamera and the global _AbPixelAngle = 2 tan(vfov / 2) / pixelHeight.
    /// The clear colour follows the fog horizon colour, never black (R2-16).
    /// QA: records the 14.7.1 camera-to-platform offset error on the lift, and counts frames with the camera inside a
    /// corridor collider (Part 10 item 8).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance;

        /// <summary>The main camera (a child of the rig, found automatically when not assigned).</summary>
        public Camera Camera;

        [Header("Projection (written by ProgressDirector / AutoWalker)")]
        public float BaseFov = AbyssConst.FOV_DEFAULT;
        public float FovOffset;
        public float FarClip = AbyssConst.FAR_PLANE[0];
        public float NearClip = AbyssConst.CAMERA_NEAR;

        [Header("Head motion")]
        public bool HeadBob = true;
        public bool HallVibration = true;
        public bool TreadDip = true;

        [Header("QA")]
        public bool CheckCameraInsideColliders = true;
        /// <summary>Frames in which the camera sphere (0.05 m) overlapped a corridor collider.</summary>
        public int CameraInsideColliderFrames;
        /// <summary>Head-motion offset applied this frame (metres, world).</summary>
        public Vector3 LastHeadOffset { get; private set; }
        /// <summary>Vertical field of view applied this frame.</summary>
        public float CurrentFov { get; private set; }
        public bool HasCapturePose => _capture;

        bool _capture;
        Vector3 _capPos;
        Quaternion _capRot = Quaternion.identity;
        float _capFov = AbyssConst.FOV_DEFAULT;
        float _bobWeight;
        float _appliedFov = -1f, _appliedNear = -1f, _appliedFar = -1f;
        bool _insideLogged;

        const int CorridorMask = 1 << CorridorKit.LayerCorridor;

        void Awake()
        {
            Instance = this;
            if (Camera == null) Camera = GetComponentInChildren<Camera>(true);
            if (Camera == null) Camera = GetComponent<Camera>();
            if (Camera == null)
            {
                Debug.LogError("[ABYSS] CameraRig: no Camera found under " + name);
                return;
            }
            Camera.nearClipPlane = NearClip;
            Camera.farClipPlane = FarClip;
            Camera.fieldOfView = BaseFov;
            GameState.MainCamera = Camera;
            GameState.CameraPos = Camera.transform.position;
            GameState.CameraRot = Camera.transform.rotation;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- capture pose (QA harness)

        /// <summary>Exact eye position, rotation (zero roll expected) and vertical FOV; no bob or shake until cleared. Applied immediately.</summary>
        public void SetCapturePose(Vector3 eyePos, Quaternion rotation, float vFovDeg)
        {
            _capture = true;
            _capPos = eyePos;
            _capRot = rotation;
            _capFov = vFovDeg;
            Apply(eyePos, rotation, vFovDeg);
            LastHeadOffset = Vector3.zero;
        }

        public void ClearCapturePose()
        {
            _capture = false;
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>
        /// R2-16: the camera's clear colour follows the current fog horizon colour (the global _AbFogHoriz, linear, set by
        /// ProgressDirector), never black, so any pixel no surface covers (a sub-pixel crack the skybox misses) shows fog,
        /// not (0, 0, 0). Allocation-free; the camera is only touched when the colour changes.
        /// </summary>
        void UpdateBackground()
        {
            Vector4 v = Shader.GetGlobalVector(ShaderIds.AbFogHoriz);
            if (v.x + v.y + v.z <= 0f) v = new Vector4(MinBackgroundLinear, MinBackgroundLinear, MinBackgroundLinear, 1f);
            if (v == _bgLinear) return;
            _bgLinear = v;
            float r = Mathf.Max(v.x, MinBackgroundLinear), g = Mathf.Max(v.y, MinBackgroundLinear), b = Mathf.Max(v.z, MinBackgroundLinear);
            // Camera.backgroundColor is authored in sRGB and converted to the active (linear) colour space by URP
            Camera.backgroundColor = new Color(r, g, b, 1f).gamma;
        }

        /// <summary>Lowest clear colour (linear), the 14.2 "no pure black" floor.</summary>
        const float MinBackgroundLinear = 0.002f;
        Vector4 _bgLinear = new Vector4(-1f, -1f, -1f, -1f);

        void LateUpdate()
        {
            if (Camera == null) return;
            UpdateBackground();
            if (_capture)
            {
                Apply(_capPos, _capRot, _capFov);
                return;
            }

            var pc = PlayerController.Instance;
            if (pc == null)
            {
                Apply(Camera.transform.position, Camera.transform.rotation, BaseFov + FovOffset);
                return;
            }

            Vector3 eye = pc.ComputeEyeWorld();
            Vector3 off = HeadOffset(pc, eye, Time.deltaTime);
            LastHeadOffset = off;
            Vector3 pos = eye + off;
            Quaternion rot = AbyssConst.YawPitch(pc.Yaw, pc.Pitch);
            Apply(pos, rot, BaseFov + FovOffset);

            if (pc.OnLift)
            {
                var lift = LiftController.Instance;
                if (lift != null)
                {
                    Vector3 expected = lift.transform.TransformPoint(pc.LiftLocalEye + off);
                    lift.RecordCameraOffset((Camera.transform.position - expected).magnitude);
                }
            }

            if (CheckCameraInsideColliders && Physics.CheckSphere(pos, 0.05f, CorridorMask, QueryTriggerInteraction.Ignore))
            {
                CameraInsideColliderFrames++;
                if (!_insideLogged)
                {
                    _insideLogged = true;
                    Debug.LogWarning("[ABYSS] CameraRig: camera inside a corridor collider at " + pos.ToString("F2") + ", d=" +
                                     pc.PathD.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        Vector3 HeadOffset(PlayerController pc, Vector3 eye, float dt)
        {
            Vector3 off = Vector3.zero;

            if (HeadBob)
            {
                float d = pc.PathD;
                bool nearStairs = d > AbyssConst.WD[1] - 1f && d < AbyssConst.WD[2] + 1f;
                float target = (!nearStairs && pc.Grounded && !GameState.CaptureMode)
                    ? Mathf.Clamp01(pc.HorizontalSpeed / AbyssConst.V_WALK) : 0f;
                // ease the weight (0.25 s) so bob never switches on or off abruptly
                _bobWeight += (target - _bobWeight) * (1f - Mathf.Exp(-dt / 0.25f));
                float amp = (AutoWalker.Instance != null && AutoWalker.Instance.Cinematic)
                    ? HeadMotion.BOB_AMPLITUDE_CINEMATIC : HeadMotion.BOB_AMPLITUDE;
                off.y += HeadMotion.Bob(pc.StepPhase01) * amp * _bobWeight;
            }

            if (TreadDip) off.y += HeadMotion.TreadDip(pc.TimeSinceTread);

            if (HallVibration)
            {
                float w = HeadMotion.HallWeight(eye);
                if (w > 0f) off += w * HeadMotion.HallVibration(SceneClock.Time, GameState.Seed);
            }
            return off;
        }

        void Apply(Vector3 pos, Quaternion rot, float fov)
        {
            Camera.transform.SetPositionAndRotation(pos, rot);
            fov = Mathf.Clamp(fov, 20f, 120f);
            if (fov != _appliedFov) { Camera.fieldOfView = fov; _appliedFov = fov; }
            if (NearClip != _appliedNear) { Camera.nearClipPlane = NearClip; _appliedNear = NearClip; }
            if (FarClip != _appliedFar) { Camera.farClipPlane = FarClip; _appliedFar = FarClip; }
            CurrentFov = fov;

            GameState.CameraPos = pos;
            GameState.CameraRot = rot;
            GameState.MainCamera = Camera;

            RenderTexture rt = Camera.targetTexture;
            int h = rt != null ? rt.height : Camera.pixelHeight;
            if (h < 1) h = 1;
            Shader.SetGlobalFloat(ShaderIds.AbPixelAngle, 2f * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) / h);
        }
    }
}
