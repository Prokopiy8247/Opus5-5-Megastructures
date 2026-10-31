using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Megastructure
{
    /// <summary>
    /// Continuous progress and every eased look parameter (Part 7.3, 14.2, 14.6). Owns GameState.D and GameState.E, the
    /// domain-0 fog/sky globals, the trilight ambient globals, the Key light, its shaft-depth falloff (_AbKeyDepth, and the
    /// ring-chamfer falloff _AbKeyChamfer / _AbRingBand) and haze skylight (_AbHazeSky), the URP shadow distance, the post-processing
    /// Volume overrides, the camera FOV and far plane (through CameraRig), _AbSkyBlend, _AbBeamFade, _AbScatter*, _AbSunDir,
    /// _AbExtSunDir and _AbDitherParams.
    ///
    /// Progress: d = PathGuide.Project(player feet, previous d), frozen at D_LIFT during the dwell and ride, clamped to the
    /// correct side of the lift (before / after the ride) and rate-limited to 6 m/s so a projection jump at a path corner
    /// cannot snap a blend; a teleport (more than 5 m in one frame off the lift) re-snaps everything. E per CONTRACTS section 4.
    /// All values come from <see cref="ProgressTables.Evaluate"/> at E (pure, tested in ProgressTests). Two safety limiters
    /// sit on top of the tables and never engage in normal play: exposure at most 0.35 EV/s, FOV at most 2.5 deg/s.
    ///
    /// Execution order 10 (after BeatDirector 0, which calls <see cref="UpdateProgress"/> first so beats see this frame's d).
    /// Inactive until GameBootstrap calls <see cref="Init"/> and <see cref="SnapAll()"/> at the end of the load. Zero GC.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class ProgressDirector : MonoBehaviour
    {
        public static ProgressDirector Instance;

        public enum DitherMode { Off = 0, Static = 1, Temporal = 2 }

        /// <summary>Maximum believable rate of d (m/s); larger projection steps are smoothed.</summary>
        public const float MAX_D_RATE = 6f;
        /// <summary>Player displacement in one frame (off the lift) treated as a teleport.</summary>
        public const float TELEPORT_DISTANCE = 5f;
        /// <summary>Safety limit on the FOV rate (deg/s); the tables peak at 1.875 deg/s at the brisk speed.</summary>
        public const float MAX_FOV_RATE = 2.5f;

        [Tooltip("The single directional light 'Key' (assigned by GameBootstrap.Init if empty).")]
        public Light keyLight;
        [Tooltip("Global Volume whose instantiated profile receives the per-beat overrides (assigned by GameBootstrap.Init if empty).")]
        public Volume globalVolume;

        /// <summary>Final dither variant (-dither static|temporal|off); default static 2.0 LSB (lead decision after the 11.12 encode test, DEVELOPMENT.md).</summary>
        public DitherMode Dither = DitherMode.Static;   // encode test 11.12 (DEVELOPMENT.md): static survives x264/VP9 better than temporal
        /// <summary>Final dither amplitude in 8-bit LSB (-dither-amp).</summary>
        public float DitherAmplitude = 2.0f;            // 2 LSB TPDF: source banding runs 8 px (1.25 LSB: 14 px > the 12 px limit)

        /// <summary>True after the first SnapAll (the load is complete enough to drive the look).</summary>
        public bool Active { get; private set; }

        /// <summary>The values applied this frame (after the limiters).</summary>
        public ProgressSample Current => _applied;

        /// <summary>The key light state machine (QA reads its level).</summary>
        public KeyLightRig Key => _key;

        /// <summary>-nopost (capture-mode flag): disables post-processing on the main camera for the horizon test.</summary>
        public bool PostProcessingEnabled
        {
            get => _post;
            set { _post = value; ApplyPostFlag(); }
        }

        ProgressSample _target, _applied;
        KeyLightRig _key;
        VolumeProfile _profile;
        ColorAdjustments _colorAdj;
        WhiteBalance _whiteBalance;
        Bloom _bloom;
        Vignette _vignette;
        Tonemapping _tonemapping;
        bool _post = true;

        int _progressFrame = -1;
        float _d;
        Vector3 _lastPlayerPos;
        bool _haveLastPos;
        bool _snapRequested;
        float _lastE;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _key?.Dispose();
        }

        /// <summary>Wire the light and the volume (load time). Safe to call more than once.</summary>
        public void Init(Light key, Volume volume)
        {
            if (key != null) keyLight = key;
            if (volume != null) globalVolume = volume;
            if (_key == null) _key = new KeyLightRig(keyLight);

            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            RenderSettings.reflectionIntensity = 0f;

            if (globalVolume != null && _profile == null)
            {
                _profile = globalVolume.profile;   // instantiated copy: the asset is never modified
                _profile.TryGet(out _tonemapping);
                _profile.TryGet(out _colorAdj);
                _profile.TryGet(out _whiteBalance);
                _profile.TryGet(out _bloom);
                _profile.TryGet(out _vignette);
                if (_tonemapping != null) { _tonemapping.active = true; _tonemapping.mode.Override(TonemappingMode.Neutral); }
                if (_colorAdj == null || _bloom == null || _whiteBalance == null || _vignette == null || _tonemapping == null)
                    Debug.LogError("[ABYSS-ERROR] ProgressDirector: the global Volume profile lacks Tonemapping / ColorAdjustments / WhiteBalance / Bloom / Vignette");
            }
            else if (globalVolume == null)
            {
                Debug.LogError("[ABYSS-ERROR] ProgressDirector: no global Volume assigned");
            }
            if (keyLight == null) Debug.LogError("[ABYSS-ERROR] ProgressDirector: no Key light assigned");
            ApplyPostFlag();
        }

        // ---------------------------------------------------------------- progress (d, E)
        /// <summary>
        /// Compute GameState.D and GameState.E for this frame from the player position and the lift state. Idempotent per
        /// frame; BeatDirector (order 0) calls it before ticking the beats, Update calls it again (no-op).
        /// </summary>
        public void UpdateProgress()
        {
            int frame = Time.frameCount;
            if (frame == _progressFrame || !Active) return;
            _progressFrame = frame;

            Vector3 pos = GameState.PlayerPos;
            LiftPhase lp = GameState.Lift;
            bool onRide = lp == LiftPhase.Dwell || lp == LiftPhase.Riding;
            if (_haveLastPos && !onRide && (pos - _lastPlayerPos).sqrMagnitude > TELEPORT_DISTANCE * TELEPORT_DISTANCE)
            {
                _lastPlayerPos = pos;
                _snapRequested = true;
                _d = Constrain(FindD(pos));
            }
            else
            {
                _lastPlayerPos = pos;
                _haveLastPos = true;
                float target = onRide ? AbyssConst.D_LIFT : Constrain(PathGuide.Project(pos, _d));
                float step = MAX_D_RATE * Mathf.Max(Time.deltaTime, 1f / 240f);
                _d = onRide ? target : Mathf.MoveTowards(_d, target, step);
            }
            GameState.D = _d;
            GameState.E = ComputeE(_d);
        }

        /// <summary>Keep d on the correct side of the lift: before the ride d &lt;= D_LIFT, after it d &gt;= D_LIFT.</summary>
        static float Constrain(float d)
        {
            d = Mathf.Clamp(d, 0f, AbyssConst.POLYLINE_LENGTH);
            if (LiftController.Instance == null) return d;   // no lift in the scene (graybox / tests): free projection
            LiftPhase lp = GameState.Lift;
            if (lp == LiftPhase.Dwell || lp == LiftPhase.Riding) return AbyssConst.D_LIFT;
            bool after = GameState.RideStarted || lp == LiftPhase.Arrived || lp == LiftPhase.Left;
            return after ? Mathf.Max(d, AbyssConst.D_LIFT) : Mathf.Min(d, AbyssConst.D_LIFT);
        }

        /// <summary>Extended progress E (CONTRACTS section 4).</summary>
        static float ComputeE(float d)
        {
            if (LiftController.Instance == null)
                return d > AbyssConst.D_LIFT ? ProgressTables.EPost(d) : d;
            LiftPhase lp = GameState.Lift;
            if (lp == LiftPhase.Left) return ProgressTables.EPost(d);
            bool liftSegment = GameState.RideStarted || lp != LiftPhase.DockedTop;
            if (!liftSegment) return d;
            // Dwell: ride seconds -1 -> 0; ride: D_LIFT + s; arrived: continuous into d + 121 once the gate opened.
            float rs = Mathf.Clamp(GameState.RideSeconds, 0f, ProgressTables.RIDE_E_SPAN);
            return d + rs;
        }

        /// <summary>Global nearest-d search (teleports): PathGuide searches every piece when the hint is negative and penalises
        /// a vertical mismatch, which separates the hall catwalk (y -7) from the gantry (y -487) on the same plan line.</summary>
        static float FindD(Vector3 pos) => PathGuide.Project(pos, -1f);

        // ---------------------------------------------------------------- snapping
        /// <summary>Apply every blended value instantly from the current player position (after teleports / for captures).</summary>
        public void SnapAll() => SnapAll(-1f);

        /// <summary>Apply every blended value instantly for path distance dOverride (negative = search from the player position).</summary>
        public void SnapAll(float dOverride)
        {
            if (_key == null) Init(keyLight, globalVolume);
            Active = true;
            Vector3 pos = GameState.PlayerPos;
            _d = Constrain(dOverride >= 0f ? dOverride : FindD(pos));
            _lastPlayerPos = pos;
            _haveLastPos = true;
            _progressFrame = Time.frameCount;
            GameState.D = _d;
            GameState.E = ComputeE(_d);

            ProgressTables.Evaluate(GameState.E, ref _target);
            _applied = _target;
            _key.Snap(_target.KeyRow);
            Apply();
            _snapRequested = false;
            _lastE = GameState.E;
        }

        // ---------------------------------------------------------------- per frame
        void Update()
        {
            if (!Active) return;
            UpdateProgress();
            // a teleport, or E set directly (capture: ride seconds), lands instantly instead of crawling through the limiters
            if (_snapRequested || Mathf.Abs(GameState.E - _lastE) > TELEPORT_DISTANCE)
            {
                SnapAll(_d);
                return;
            }

            float dt = Time.deltaTime;
            ProgressTables.Evaluate(GameState.E, ref _target);
            float ev = _applied.EV, fov = _applied.Fov;
            _applied = _target;
            _applied.EV = Mathf.MoveTowards(ev, _target.EV, AbyssConst.MAX_EXPOSURE_RATE * dt);
            _applied.Fov = Mathf.MoveTowards(fov, _target.Fov, MAX_FOV_RATE * dt);
            _key.Update(_target.KeyRow, dt);
            Apply();
            _lastE = GameState.E;
        }

        void Apply()
        {
            ref ProgressSample s = ref _applied;

            // ---- domain-0 fog and sky (linear colours, sigma = 0.8326 / D50)
            Shader.SetGlobalFloat(ShaderIds.AbFogSigma, s.FogSigma);
            Shader.SetGlobalFloat(ShaderIds.AbFogDownMul, s.FogDownMul);
            Shader.SetGlobalVector(ShaderIds.AbFogDown, s.FogDown);
            Shader.SetGlobalVector(ShaderIds.AbFogHoriz, s.FogHoriz);
            Shader.SetGlobalVector(ShaderIds.AbFogUp, s.FogUp);
            Shader.SetGlobalFloat(ShaderIds.AbSkyBlend, s.SkyBlend);
            Shader.SetGlobalFloat(ShaderIds.AbBeamFade, s.BeamFade);

            // ---- trilight ambient
            Shader.SetGlobalVector(ShaderIds.AbAmbUp, s.AmbUp);
            Shader.SetGlobalVector(ShaderIds.AbAmbMid, s.AmbMid);
            Shader.SetGlobalVector(ShaderIds.AbAmbDown, s.AmbDown);

            // ---- key light derived globals (the scatter lobe fades with the key's ramps)
            ref readonly KeyRow kr = ref ProgressTables.KeyRows[_key.ActiveRow];
            if (kr.On)
            {
                Vector3 toLight = -AbyssConst.Dir(kr.Yaw, kr.Pitch);
                Shader.SetGlobalVector(ShaderIds.AbSunDir, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
                Color lin = kr.Colour.linear;
                Color sc = lin * ProgressTables.SCATTER_GAIN;
                Shader.SetGlobalVector(ShaderIds.AbScatterColor, new Vector4(sc.r, sc.g, sc.b, 1f));
                Shader.SetGlobalFloat(ShaderIds.AbScatterAmt, _key.ScatterAmount);
                float haze = _key.HazeSky;
                Shader.SetGlobalVector(ShaderIds.AbHazeSky, new Vector4(lin.r * haze, lin.g * haze, lin.b * haze, 0f));
            }
            else
            {
                Shader.SetGlobalFloat(ShaderIds.AbScatterAmt, 0f);
                Shader.SetGlobalVector(ShaderIds.AbHazeSky, Vector4.zero);
            }
            // shaft-depth falloff of the main light (domain 0): carried by the active row, so it switches only while the key is dark
            Shader.SetGlobalVector(ShaderIds.AbKeyDepth, kr.DepthVector);
            // its slow second falloff on the ring top chamfers (R2-06), same switching; the ring band is a constant of the shaft
            Shader.SetGlobalVector(ShaderIds.AbKeyChamfer, kr.ChamferVector);
            Shader.SetGlobalVector(ShaderIds.AbRingBand, ProgressTables.RingBand);
            Vector3 ext = ProgressTables.ExtSunDir;
            Shader.SetGlobalVector(ShaderIds.AbExtSunDir, new Vector4(ext.x, ext.y, ext.z, 0f));

            // ---- final dither
            float temporal = Dither == DitherMode.Temporal ? 1f : 0f;
            float frameIdx = Dither == DitherMode.Temporal ? (float)(ClockDriver.Frame & 63) : 0f;   // walk frame: deterministic in captures
            Shader.SetGlobalVector(ShaderIds.AbDitherParams,
                new Vector4(DitherAmplitude, temporal, frameIdx, Dither == DitherMode.Off ? 0f : 1f));

            // ---- post-processing overrides (manual exposure, no auto exposure)
            if (_colorAdj != null)
            {
                _colorAdj.postExposure.Override(s.EV);
                _colorAdj.contrast.Override(s.Contrast);
                _colorAdj.saturation.Override(s.Saturation);
            }
            if (_whiteBalance != null) _whiteBalance.temperature.Override(s.Temperature);
            if (_bloom != null)
            {
                _bloom.threshold.Override(s.BloomThreshold);
                _bloom.intensity.Override(s.BloomIntensity);
                _bloom.scatter.Override(s.BloomScatter);
            }
            if (_vignette != null) _vignette.intensity.Override(s.Vignette);

            // ---- camera (CameraRig adds its FovOffset, which AutoWalker uses for the H7 push, and applies both in LateUpdate)
            CameraRig rig = CameraRig.Instance;
            if (rig != null)
            {
                rig.BaseFov = s.Fov;
                rig.FarClip = s.FarClip;
            }
        }

        void ApplyPostFlag()
        {
            Camera cam = GameState.MainCamera;
            if (cam == null && GameBootstrap.Instance != null) cam = GameBootstrap.Instance.mainCamera;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = _post;
        }
    }
}
