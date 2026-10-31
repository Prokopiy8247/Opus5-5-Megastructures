using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Megastructure
{
    /// <summary>
    /// The one writer of the shared atmosphere (Part 14.2.2-14.2.5, 14.7 H4, H10): every frame (director chain order
    /// 250, inside LateUpdate) it evaluates RingSchedule at (s, u, set), applies the slew limiters (EV 0.30 EV/s, WB 6/s,
    /// bloom intensity 0.1/s, vFOV 1 degree/s, shadow distance 40 m/s; bypassed for capture stills, kept for frame
    /// sequences), blends the u-driven rows toward beat 4 when the sunset time branch leads below s 423, and writes
    /// every global of the DEVELOPMENT.md uniform table, the key / arch-fill / rim-fill lights, the URP shadow distance
    /// and cascade count (only when changed), the cached volume components and the camera (vFOV, near 0.05, far).
    /// _RingCam is written per camera in RenderPipelineManager.beginCameraRendering (capture cameras too).
    /// Allocation-free per frame (property ids cached, no strings, no LINQ).
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class AtmosphereDriver : MonoBehaviour
    {
        public const int TickOrder = 250;

        [SerializeField] public Light key, archFill, rimFill;
        public Volume volume;
        public Camera cam;

        /// <summary>Optional vFOV override for captures (NaN = schedule).</summary>
        public static float FovOverride = float.NaN;
        /// <summary>The last targets (before slew) and the applied slewed values, for QA.</summary>
        public static RingParams Last;
        public static float AppliedEv, AppliedWb, AppliedBloom, AppliedFov;

        static readonly int IdRingCam = Shader.PropertyToID("_RingCam");
        static readonly int IdRingTime = Shader.PropertyToID("_RingTime");
        static readonly int IdRingSet = Shader.PropertyToID("_RingSet");
        static readonly int IdSkyParams = Shader.PropertyToID("_SkyParams");
        static readonly int IdSunsetParams = Shader.PropertyToID("_SunsetParams");
        static readonly int IdStarDir = Shader.PropertyToID("_StarDir");
        static readonly int IdStarDirOut = Shader.PropertyToID("_StarDirOut");
        static readonly int IdFogParams = Shader.PropertyToID("_FogParams");
        static readonly int IdFogTint = Shader.PropertyToID("_FogTint");
        static readonly int IdFogExtra = Shader.PropertyToID("_FogExtra");
        static readonly int IdFillLight = Shader.PropertyToID("_RingFillLight");
        static readonly int IdAmbSky = Shader.PropertyToID("_AmbSky");
        static readonly int IdAmbEquator = Shader.PropertyToID("_AmbEquator");
        static readonly int IdAmbGround = Shader.PropertyToID("_AmbGround");
        static readonly int IdShipShadow = Shader.PropertyToID("_ShipShadow");
        static readonly int IdCloudA = Shader.PropertyToID("_CloudShadowA");
        static readonly int IdCloudB = Shader.PropertyToID("_CloudShadowB");
        static readonly int IdArkParams = Shader.PropertyToID("_ArkParams");
        static readonly int IdLamps = Shader.PropertyToID("_Lamps");
        static readonly int IdWind = Shader.PropertyToID("_Wind");
        static readonly int IdSwarm = Shader.PropertyToID("_Swarm");
        static readonly int IdKeyDir = Shader.PropertyToID("_KeyLightDir");
        static readonly int IdKeyColor = Shader.PropertyToID("_KeyLightColor");
        static readonly int IdBeaconPulse = Shader.PropertyToID("_BeaconPulse");

        const float FovSlew = 1f;                  // Part 9: FOV changes slower than 1 degree per second
        const float FogDesat = 0.25f;              // 14.2.2: fog colour desaturated by 0.25 f
        const double SwarmWrap = 13200.0;          // LCM of the swarm periods 600 / 1 200 / 2 200 s (no wrap seam)
        const float JumpS = 20f;
        // Part 10 (6) popping / 8.5: the schedule steps the shadow distance (30 m in the capsule -> 220 m at s 12, 90 m
        // at s 834). Live play moves it at 40 m/s (30 -> 220 m in ~4.8 s) so URP's last-cascade border fade brings the
        // valley shadows in gradually instead of all at once; capture stills and teleports take the target directly.
        const float ShadowDistSlew = 40f;
        // Sunset lead (14.5.4, 14.7 H4 one atmosphere): below s 423 (end of the Rule P window round 411) the schedule's
        // light and grade rows are functions of s only; when the sunset time branch leads (lingering or backing up after
        // s 411) they are blended toward the beat-4 rows of u, read at s 430 (inside beat 4, outside every Rule P
        // window 423..582, where those rows are pure functions of u). Local workaround for a RingSchedule contract change.
        const float LeadMaxS = RingConstants.SunsetS0 + 12f;
        const float LeadRefS = 430f;

        Action<float> _tick;
        Bloom _bloom; ColorAdjustments _color; WhiteBalance _wb; Vignette _vig;
        UniversalRenderPipelineAsset _urp;
        float _origShadowDist = -1f; int _origCascades = -1;
        float _ev, _wbT, _bloomI, _fov;
        bool _init, _wasReady;
        float _prevS;
        bool _keyShadows = true; bool _keyShadowInit;
        float _lastShadowDist = -1f; int _lastCascades = -1;
        float _lastFar = -1f, _lastNear = -1f;
        float _lastBiasD = -1f, _lastBiasN = -1f;
        bool _logged, _warnedVolume;
        float _starRadRad;
        bool _hasParams; float _pS, _pU, _pCue, _pLead; int _pSet;

        public static void ResetStatics() { FovOverride = float.NaN; }

        void Awake()
        {
            _tick = Tick;
            RingRig.RegisterDirector(TickOrder, _tick);
            _starRadRad = RingConstants.StarRadiusDeg * Mathf.Deg2Rad;
        }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; }

        void Start()
        {
            ResolveReferences();
            RenderSettings.fog = false;                 // 8.4: one fog model, in our shaders only
            if (key != null) RenderSettings.sun = key;  // the key is always URP's main light
            SetupLights();
            CacheVolume();
            _urp = UniversalRenderPipeline.asset;
            if (_urp != null) { _origShadowDist = _urp.shadowDistance; _origCascades = _urp.shadowCascadeCount; }
            LogGraphics();
        }

        void OnDestroy()
        {
            // the pipeline asset is a project asset: restore what we changed (matters in the Editor)
            if (_urp != null && _origCascades >= 1)
            {
                _urp.shadowDistance = _origShadowDist;
                _urp.shadowCascadeCount = _origCascades;
            }
        }

        void LateUpdate()
        {
            if (!RingRig.Directors.ContainsKey(TickOrder)) RingRig.RegisterDirector(TickOrder, _tick);
        }

        void ResolveReferences()
        {
            if (cam == null) cam = RingRig.Camera != null ? RingRig.Camera : Camera.main;
            if (volume == null) volume = FindAnyObjectByType<Volume>();
            if (key == null || archFill == null || rimFill == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
                for (int i = 0; i < lights.Length; i++)
                {
                    var l = lights[i];
                    if (l.type != LightType.Directional) continue;
                    string n = l.gameObject.name;
                    if (archFill == null && n.IndexOf("Arch", StringComparison.OrdinalIgnoreCase) >= 0) archFill = l;
                    else if (rimFill == null && n.IndexOf("Rim", StringComparison.OrdinalIgnoreCase) >= 0) rimFill = l;
                    else if (key == null && (n.IndexOf("Star", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Sun", StringComparison.OrdinalIgnoreCase) >= 0)) key = l;
                }
                if (key == null && RenderSettings.sun != null) key = RenderSettings.sun;
                if (key == null) Debug.LogWarning("[Ring] AtmosphereDriver: no key light assigned or found");
            }
        }

        void SetupLights()
        {
            if (key != null)
            {
                key.type = LightType.Directional;
                key.shadows = LightShadows.Soft;
                _keyShadows = true; _keyShadowInit = true;
                var data = key.GetUniversalAdditionalLightData();
                if (data != null) data.usePipelineSettings = false;   // per-light biases from the schedule
            }
            if (archFill != null) { archFill.type = LightType.Directional; archFill.shadows = LightShadows.None; }
            if (rimFill != null) { rimFill.type = LightType.Directional; rimFill.shadows = LightShadows.None; }
        }

        void CacheVolume()
        {
            if (volume == null) { Debug.LogWarning("[Ring] AtmosphereDriver: no Volume found; post values are not driven"); return; }
            VolumeProfile prof = volume.sharedProfile != null ? volume.profile : null;   // runtime copy; the asset stays untouched
            if (prof == null) { Debug.LogWarning("[Ring] AtmosphereDriver: the Volume has no profile"); return; }
            prof.TryGet(out _bloom);
            prof.TryGet(out _color);
            prof.TryGet(out _wb);
            prof.TryGet(out _vig);
            if (_bloom != null) { _bloom.threshold.overrideState = true; _bloom.intensity.overrideState = true; _bloom.scatter.overrideState = true; _bloom.clamp.overrideState = true; }
            if (_color != null) { _color.postExposure.overrideState = true; _color.contrast.overrideState = true; _color.saturation.overrideState = true; }
            if (_wb != null) _wb.temperature.overrideState = true;
            if (_vig != null) _vig.intensity.overrideState = true;
            if ((_bloom == null || _color == null || _wb == null || _vig == null) && !_warnedVolume)
            {
                _warnedVolume = true;
                Debug.LogWarning("[Ring] AtmosphereDriver: volume profile lacks " + (_bloom == null ? "Bloom " : "") + (_color == null ? "ColorAdjustments " : "")
                                 + (_wb == null ? "WhiteBalance " : "") + (_vig == null ? "Vignette" : ""));
            }
        }

        void LogGraphics()
        {
            if (_logged) return;
            _logged = true;
            string depth = "unknown";
            if (_urp != null)
            {
                var list = _urp.rendererDataList;
                if (list.Length > 0 && list[0] is UniversalRendererData rd)
                {
                    DepthFormat att = rd.depthAttachmentFormat, tex = rd.depthTextureFormat;
                    var def = CoreUtils.GetDefaultDepthStencilFormat();
                    depth = "attachment " + (att == DepthFormat.Default ? "Default=" + def : att.ToString())
                          + ", texture " + (tex == DepthFormat.Default ? "Default=" + def : tex.ToString());
                }
            }
            Debug.Log("[Ring] Graphics " + SystemInfo.graphicsDeviceType + " (" + SystemInfo.graphicsDeviceName + "), reversed Z " + SystemInfo.usesReversedZBuffer
                      + ", camera depth " + depth + ", pipeline " + (_urp != null ? _urp.name : "none"));
        }

        static Vector4 V(Color c, float w) => new Vector4(c.r, c.g, c.b, w);
        static Vector4 V(Vector3 v, float w) => new Vector4(v.x, v.y, v.z, w);

        /// <summary>Schedule targets for (s, u, set, felt cue, sunset lead); re-evaluated only when an input changed
        /// (RingSchedule is pure, so a standing or paused player costs nothing).</summary>
        RingParams Targets(float s)
        {
            float u = RingState.SunsetU, cue = RingState.ArkCue;
            int set = RingState.Set;
            float lead = s < LeadMaxS ? ProgressDirector.SunsetLead : 0f;
            if (_hasParams && s == _pS && u == _pU && set == _pSet && cue == _pCue && lead == _pLead) return Last;
            _pS = s; _pU = u; _pSet = set; _pCue = cue; _pLead = lead; _hasParams = true;
            Last = RingSchedule.Evaluate(s, u, set);
            if (lead > 0f) BlendTowardSunset(ref Last, RingSchedule.Evaluate(LeadRefS, u, set), lead);
            return Last;
        }

        /// <summary>Moves the u-driven light and grade rows (key, arch fill, ambient trilight, EV, bloom intensity,
        /// saturation, WB, vignette, fog sigma) toward their beat-4 values for the same u, by k (0..1). vFOV, far plane,
        /// shadows, contrast and the set stay on s. k is 0 on a forward walk and in capture mode, so stills and the Rule P
        /// sweep are untouched.</summary>
        static void BlendTowardSunset(ref RingParams p, in RingParams q, float k)
        {
            p.Ev = Mathf.Lerp(p.Ev, q.Ev, k);
            p.BloomIntensity = Mathf.Lerp(p.BloomIntensity, q.BloomIntensity, k);
            p.Saturation = Mathf.Lerp(p.Saturation, q.Saturation, k);
            p.WbTemp = Mathf.Lerp(p.WbTemp, q.WbTemp, k);
            p.Vignette = Mathf.Lerp(p.Vignette, q.Vignette, k);
            p.FogSigma = Mathf.Lerp(p.FogSigma, q.FogSigma, k);
            p.KeyIntensity = Mathf.Lerp(p.KeyIntensity, q.KeyIntensity, k);
            p.KeyColor = Color.Lerp(p.KeyColor, q.KeyColor, k);
            p.ArchFill = Mathf.Lerp(p.ArchFill, q.ArchFill, k);
            p.ArchFillColor = Color.Lerp(p.ArchFillColor, q.ArchFillColor, k);
            p.AmbSky = Color.Lerp(p.AmbSky, q.AmbSky, k);
            p.AmbEquator = Color.Lerp(p.AmbEquator, q.AmbEquator, k);
            p.AmbGround = Color.Lerp(p.AmbGround, q.AmbGround, k);
        }

        void Tick(float dtRaw)
        {
            float s = RingState.S;
            float dt = RingState.Dt;
            bool ready = RingRig.WorldReady;
            var p = Targets(s);

            bool bypass = !_init || !ready || (ready && !_wasReady) || (RingState.Capture && !RingState.SequenceMode) || Mathf.Abs(s - _prevS) > JumpS;
            if (bypass) { _ev = p.Ev; _wbT = p.WbTemp; _bloomI = p.BloomIntensity; _fov = p.Vfov; }
            else
            {
                _ev = RingMath.Slew(_ev, p.Ev, RingConstants.EvSlew, dt);
                _wbT = RingMath.Slew(_wbT, p.WbTemp, RingConstants.WbSlew, dt);
                _bloomI = RingMath.Slew(_bloomI, p.BloomIntensity, RingConstants.BloomSlew, dt);
                _fov = RingMath.Slew(_fov, p.Vfov, FovSlew, dt);
            }
            _init = true; _wasReady = ready; _prevS = s;
            AppliedEv = _ev; AppliedWb = _wbT; AppliedBloom = _bloomI; AppliedFov = _fov;

            WriteGlobals(p, s);
            ApplyLights(p);
            ApplyShadowSettings(p, bypass, dt);
            ApplyVolume(p);
            ApplyCamera(p);
        }

        void WriteGlobals(in RingParams p, float s)
        {
            double T = RingState.T;
            float animT = RingState.AnimT;
            Shader.SetGlobalVector(IdRingTime, new Vector4(animT, (float)(T % 60.0), RingState.Capture ? 1f : 0f, s));
            Shader.SetGlobalVector(IdRingSet, new Vector4(p.Set, p.OutsideSky, p.FarRim, RingConstants.WallX));
            Shader.SetGlobalVector(IdSkyParams, new Vector4(p.SkyState, p.Night, p.StarV, p.ArchNightLift));
            Shader.SetGlobalVector(IdSunsetParams, new Vector4(p.SunsetU, p.SquaresAlpha, p.TerminatorDeg, 1f));
            Shader.SetGlobalVector(IdStarDir, V(RingConstants.StarL, _starRadRad));
            Shader.SetGlobalVector(IdStarDirOut, V(RingConstants.StarL7, _starRadRad));
            Shader.SetGlobalVector(IdFogParams, new Vector4(p.FogSigma, p.FogMistK, FogDesat, p.SpaceDark));
            Shader.SetGlobalVector(IdFogTint, V(p.FogTint, p.FogTintW));
            Shader.SetGlobalVector(IdFogExtra, new Vector4(p.FogAerialInv, p.FogHeightInv, p.AerialDesat, RingConstants.FogSigmaDay));
            const float cal = RingConstants.LightCal;   // 14.2.3 calibration: lights and ambient, never post exposure
            Shader.SetGlobalVector(IdAmbSky, V(p.AmbSky * cal, 1f));
            Shader.SetGlobalVector(IdAmbEquator, V(p.AmbEquator * cal, 1f));
            Shader.SetGlobalVector(IdAmbGround, V(p.AmbGround * cal, 1f));
            // ship shadow: 0 until the ark has spawned (ProgressDirector) and whenever the Ark set is off (BeatDirector, same frame)
            Vector2 sc = RingShadowParams.ShipCentre;
            float ship = BeatDirector.IsOn(RingSet.Ark) ? RingShadowParams.ShipStrength : 0f;
            Shader.SetGlobalVector(IdShipShadow, new Vector4(sc.x, sc.y, ship, RingShadowParams.KeyDip));
            Vector2 ca = RingShadowParams.CloudA, cb = RingShadowParams.CloudB;
            float cs = RingShadowParams.CloudStrength;
            Shader.SetGlobalVector(IdCloudA, new Vector4(ca.x, ca.y, cs, 0f));
            Shader.SetGlobalVector(IdCloudB, new Vector4(cb.x, cb.y, cs, 0f));
            Shader.SetGlobalVector(IdArkParams, new Vector4(ArkPhase.XCentre(RingState.ArkU), p.WindowLevel, 1f,
                ArkPhase.Roll(T, s, RingState.Capture && !RingState.SequenceMode)));
            // _Lamps.z: the beacon builder owns the pulse (its own global _BeaconPulse, written in its IRingAnimated tick);
            // it is mirrored here (previous frame, 0 when no beacon exists) so materials that read _Lamps.z agree with it
            Shader.SetGlobalVector(IdLamps, new Vector4(p.LampLevel, p.CityLevel, Shader.GetGlobalFloat(IdBeaconPulse), p.TunnelSpill));
            Shader.SetGlobalVector(IdWind, new Vector4(RingState.Gust, animT, 0f, 0f));
            Shader.SetGlobalVector(IdSwarm, new Vector4(p.SwarmGlint, (float)(T % SwarmWrap), 0f, 0f));
            Shader.SetGlobalVector(IdKeyDir, V(p.KeyDir, p.KeyIntensity * cal));
            Shader.SetGlobalVector(IdKeyColor, V(p.KeyColor * (p.KeyIntensity * cal), 1f));
            Shader.SetGlobalVector(IdFillLight, V(p.ArchFillColor * (p.ArchFill * cal), 1f));
            if (cam != null) SetRingCam(cam);
        }

        static Quaternion LightRot(Vector3 towardLight)
        {
            Vector3 f = -towardLight;
            if (f.sqrMagnitude < 1e-8f) f = Vector3.down;
            Vector3 up = Mathf.Abs(f.y) > 0.999f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(f, up);
        }

        void ApplyLights(in RingParams p)
        {
            if (key != null)
            {
                key.transform.rotation = LightRot(p.KeyDir);
                key.color = p.KeyColor.gamma;
                key.intensity = p.KeyIntensity * RingConstants.LightCal;
                if (!_keyShadowInit || _keyShadows != p.KeyShadows)
                {
                    _keyShadows = p.KeyShadows; _keyShadowInit = true;
                    key.shadows = p.KeyShadows ? LightShadows.Soft : LightShadows.None;
                }
                if (_lastBiasD != p.ShadowDepthBias || _lastBiasN != p.ShadowNormalBias)
                {
                    _lastBiasD = p.ShadowDepthBias; _lastBiasN = p.ShadowNormalBias;
                    // per-light biases (UniversalAdditionalLightData.usePipelineSettings = false, set in SetupLights);
                    // URP 17.6 reads light.shadowBias / shadowNormalBias then (UniversalRenderPipeline.cs:2146)
                    key.shadowBias = p.ShadowDepthBias;
                    key.shadowNormalBias = p.ShadowNormalBias;
                }
            }
            Fill(archFill, p.ArchFillDir, p.ArchFillColor, p.ArchFill);
            Fill(rimFill, p.RimWashDir, p.RimWashColor, p.RimWash);
        }

        static void Fill(Light l, Vector3 dir, Color linear, float intensity)
        {
            if (l == null) return;
            bool on = intensity > 1e-4f;
            if (l.enabled != on) l.enabled = on;
            if (!on) return;
            l.transform.rotation = LightRot(dir);
            l.color = linear.gamma;
            l.intensity = intensity * RingConstants.LightCal;
        }

        void ApplyShadowSettings(in RingParams p, bool bypass, float dt)
        {
            if (_urp == null) return;
            float target = p.ShadowDistance;
            float d = (bypass || _lastShadowDist < 0f) ? target : RingMath.Slew(_lastShadowDist, target, ShadowDistSlew, dt);
            if (d != _lastShadowDist)
            {
                _lastShadowDist = d;
                _urp.shadowDistance = d;
            }
            int c = Mathf.Clamp(p.Cascades, 1, 4);
            if (_lastCascades != c)
            {
                _lastCascades = c;
                _urp.shadowCascadeCount = c;
            }
        }

        void ApplyVolume(in RingParams p)
        {
            if (_bloom != null)
            {
                _bloom.threshold.value = p.BloomThreshold;
                _bloom.intensity.value = _bloomI;
                _bloom.scatter.value = p.BloomScatter;
                _bloom.clamp.value = p.BloomClamp;
            }
            if (_color != null)
            {
                _color.postExposure.value = _ev;
                _color.contrast.value = p.Contrast;
                _color.saturation.value = p.Saturation;
            }
            if (_wb != null) _wb.temperature.value = _wbT;
            if (_vig != null) _vig.intensity.value = p.Vignette;
        }

        void ApplyCamera(in RingParams p)
        {
            if (cam == null) { cam = RingRig.Camera; if (cam == null) return; }
            float fov = float.IsNaN(FovOverride) ? _fov : FovOverride;
            if (cam.fieldOfView != fov) cam.fieldOfView = fov;
            if (_lastNear != RingConstants.NearPlane) { _lastNear = RingConstants.NearPlane; cam.nearClipPlane = RingConstants.NearPlane; }
            if (_lastFar != p.Far) { _lastFar = p.Far; cam.farClipPlane = p.Far; }
        }

        static void SetRingCam(Camera c)
        {
            Vector3 pos = c.transform.position;
            float h = Mathf.Max(1f, c.pixelHeight);
            float pix = 2f * Mathf.Tan(0.5f * c.fieldOfView * Mathf.Deg2Rad) / h;
            Shader.SetGlobalVector(IdRingCam, new Vector4(pos.x, pos.y, pos.z, pix));
        }

        static void OnBeginCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c != null) SetRingCam(c);
        }
    }
}
