// ProgressDirector: continuous progress s and every eased parameter derived from it (MASTER_PROMPT 14.6,
// 14.7.5). Computes all global shader uniforms in double each frame (after the camera moved), drives the
// sun light, the sinking ground group, GradeState, and calls every registered IWorldUpdater.
// Zero allocations per frame.
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    [DefaultExecutionOrder(500)]
    public class ProgressDirector : MonoBehaviour
    {
        public static ProgressDirector Instance { get; private set; }
        /// <summary>Progress used for all parameters this frame.</summary>
        public static double S { get; private set; }
        public static FrameState Frame;

        /// <summary>Capture/test override of s (parameters only; the camera pose is set separately).</summary>
        public bool OverrideActive;
        public double OverrideS;

        WorldContext m_ctx;
        Light m_sun;
        Transform m_ground;
        bool m_groundActive = true;
        bool m_nearActive = true;
        Transform m_near;
        Color m_sun5600, m_sun6200, m_sun6500;

        static float s_ditherLsb = (float)ThreadConstants.DitherLsb;

        void Awake()
        {
            Instance = this;
            s_ditherLsb = (float)CliArgs.GetDouble("-dither-lsb", ThreadConstants.DitherLsb);   // encode-test trials (11.12)
            m_sun5600 = ThreadPalette.Linear(Pal.SUN_5600);
            m_sun6200 = ThreadPalette.Linear(Pal.SUN_6200);
            m_sun6500 = ThreadPalette.Linear(Pal.SUN_6500);
            S = CliArgs.StartS;
            ApplyGlobals(S, Camera.main);   // valid uniforms before the first frame
        }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Init(WorldContext ctx)
        {
            m_ctx = ctx;
            m_ground = ctx.GroundGroup;
            m_near = ctx.NearRoot;
            var gb = GameBootstrap.Instance;
            m_sun = gb != null ? gb.sun : null;
        }

        void LateUpdate()
        {
            ThreadClock.Tick(Time.timeAsDouble);
            Apply();
        }

        /// <summary>Recompute everything for the current camera pose (the capture harness calls this before rendering).</summary>
        public void Apply()
        {
            double s = OverrideActive ? OverrideS : (ThreadServices.Player != null ? ThreadServices.Player.S : S);
            S = s;
            Camera cam = m_ctx != null && m_ctx.Camera != null ? m_ctx.Camera : Camera.main;
            ApplyGlobals(s, cam);
            if (m_ctx != null)
            {
                var ups = m_ctx.Updaters;
                for (int i = 0; i < ups.Count; i++)
                {
                    try { ups[i].OnFrame(in Frame); }
                    catch (Exception e) { Debug.LogError($"[THREAD-FAIL] updater {ups[i].GetType().Name}: {e.Message}"); }
                }
            }
        }

        void ApplyGlobals(double s, Camera cam)
        {
            double t = ThreadClock.Ambient;
            double A = ThreadMath.VirtualAltitude(s);
            double D = ThreadMath.Darkness(s);

            // ground group (sinks by -A_virt, deactivated inside the deck white-out with hysteresis)
            if (m_groundActive && s >= ThreadConstants.GroundOffS) m_groundActive = false;
            else if (!m_groundActive && s < ThreadConstants.GroundOffS - 4.0) m_groundActive = true;
            if (m_ground != null)
            {
                m_ground.localPosition = new Vector3(0f, (float)(-A), 0f);
                if (m_ground.gameObject.activeSelf != m_groundActive) m_ground.gameObject.SetActive(m_groundActive);
            }

            // near frame (cable, walkway, beads, pier): exists only from s = 262, switched inside the bank core where the
            // optical depth to it exceeds 5 (14.6 "no beat ever activates inside the visible frame"; decision D-027)
            if (m_nearActive && s < ThreadConstants.NearCableOnS - 4.0) m_nearActive = false;
            else if (!m_nearActive && s >= ThreadConstants.NearCableOnS) m_nearActive = true;
            if (m_near != null && m_near.gameObject.activeSelf != m_nearActive) m_near.gameObject.SetActive(m_nearActive);

            // sun
            D3 sun = ThreadMath.SunDirection(s);
            double cpos = ThreadMath.SunColourPosition(s);
            Color sc = cpos <= 1.0 ? Color.Lerp(m_sun5600, m_sun6200, (float)cpos) : Color.Lerp(m_sun6200, m_sun6500, (float)(cpos - 1.0));
            double I = ThreadMath.SunIntensity(s);
            Shader.SetGlobalVector(ShaderIds.SunDir, new Vector4((float)sun.x, (float)sun.y, (float)sun.z, (float)ThreadMath.SunAlpha(s)));
            Shader.SetGlobalVector(ShaderIds.SunColor, new Vector4(sc.r * (float)I, sc.g * (float)I, sc.b * (float)I, (float)I));
            if (m_sun != null) m_sun.transform.rotation = Quaternion.LookRotation(new Vector3(-(float)sun.x, -(float)sun.y, -(float)sun.z), Vector3.up);

            // ambient
            ThreadMath.Ambient(s, out D3 up, out D3 side, out D3 down);
            Shader.SetGlobalVector(ShaderIds.AmbUp, V4(up));
            Shader.SetGlobalVector(ShaderIds.AmbSide, V4(side));
            Shader.SetGlobalVector(ShaderIds.AmbDown, V4(down));

            // sky / ascent
            double starAlpha = ThreadMath.StarAlpha(s);
            Shader.SetGlobalVector(ShaderIds.Sky, new Vector4((float)D, (float)(A / 1000.0), (float)ThreadMath.AirDensity(A), (float)starAlpha));

            // fog, exposure cap
            double k = ThreadMath.FogK(s), H = ThreadMath.FogHeightScale(s);
            double ev = ThreadMath.ExposureEV(s), gain = Math.Pow(2.0, ev);
            Shader.SetGlobalVector(ShaderIds.Fog, new Vector4((float)k, (float)H, (float)A, (float)(0.97 / gain)));

            // bank (moves with the ground group; amplitude of the breathing doubled in beat 3)
            double ampScale = 1.0 + ThreadMath.Smootherstep(228, 252, s) - ThreadMath.Smootherstep(308, 332, s);
            double xc = ThreadMath.BankDrift(t, ampScale);
            double bankScale = 1.0 - ThreadMath.Smoothstep(ThreadConstants.BankFadeS0, ThreadConstants.BankFadeS1, s);   // D-036
            Shader.SetGlobalVector(ShaderIds.Bank, new Vector4(m_groundActive && bankScale > 0.0 ? 1f : 0f, (float)xc, (float)(-A), (float)bankScale));

            // deck slab (placed relative to the camera)
            double camY = cam != null ? cam.transform.position.y : ThreadMath.PathPoint(s).y + ThreadConstants.EyeHeight;
            FogMath.DeckSlab(camY, A, out double top, out double bot);
            Shader.SetGlobalVector(ShaderIds.Deck, new Vector4((float)FogMath.DeckGate(A), (float)top, (float)bot, (float)ThreadConstants.DeckDensity));

            // analytic cable shadow (on once the near cable exists)
            double shl = Math.Sqrt(sun.x * sun.x + sun.z * sun.z);
            float shx = shl > 1e-9 ? (float)(sun.x / shl) : 0f, shz = shl > 1e-9 ? (float)(sun.z / shl) : 1f;
            Shader.SetGlobalVector(ShaderIds.Cable, new Vector4(s >= ThreadConstants.NearCableOnS ? 1f : 0f, shx, shz, 0f));

            // far thread (beats 1-3), faded out over s 268-285 inside the bank core and swapped off at s = 285 (D-049)
            ThreadMath.ThreadAxis(s, out double xT, out double zT);
            double rApp = ThreadMath.ThreadApparentRange(s);
            double halfE = ThreadConstants.CableRadius / rApp;
            Shader.SetGlobalVector(ShaderIds.Thread, new Vector4((float)xT, (float)zT, (float)halfE, (float)ThreadMath.FarThreadAlpha(s)));
            double haze = Math.Min(1.0 - Math.Exp(-k * rApp), ThreadConstants.ThreadHazeCap);
            Shader.SetGlobalVector(ShaderIds.Thread2, new Vector4((float)haze, 0f, 0f, 0f));

            // planet (own key light; alpha by A_km), glint star
            double tilt = (ThreadConstants.PlanetTiltDeg + ThreadConstants.PlanetTiltDriftDegPerS * t) * ThreadMath.Deg;
            double bandPhase = 0.001 * t;   // 1 percent of a cycle per 10 s
            double gph = t % ThreadConstants.GlintPeriod;
            double glint = gph < 0.6 ? ThreadMath.Smoothstep(0, 0.6, gph) : Math.Exp(-(gph - 0.6) / 0.8) * (1.0 - ThreadMath.Smoothstep(2.4, 3.0, gph));
            Shader.SetGlobalVector(ShaderIds.Planet, new Vector4((float)ThreadMath.PlanetAlpha(s), (float)tilt, (float)(bandPhase - Math.Floor(bandPhase)), (float)glint));
            double pa = ThreadConstants.PlanetAzimuthDeg * ThreadMath.Deg, pe = ThreadConstants.PlanetElevationDeg * ThreadMath.Deg;
            Shader.SetGlobalVector(ShaderIds.PlanetDir, new Vector4((float)(Math.Cos(pe) * Math.Sin(pa)), (float)Math.Sin(pe), (float)(Math.Cos(pe) * Math.Cos(pa)), 0f));

            // Earth / cloud shell (radius R + 3 km, seen from h = A - 3 km)
            double h = A - ThreadConstants.CloudTop;
            double Rc = ThreadConstants.EarthRadius + ThreadConstants.CloudTop;
            double cosLimb = 0.0;
            if (h > 0) { double q = Rc / (Rc + h); cosLimb = Math.Sqrt(Math.Max(0.0, 1.0 - q * q)); }
            double coverage = ThreadMath.Lerp(1.0, 0.45, ThreadMath.Smoothstep(ThreadConstants.CloudCoverageA0, ThreadConstants.CloudCoverageA1, A / 1000.0));
            double eps0 = A > 1.0 ? 25000.0 / Math.Sqrt(A * (2.0 * ThreadConstants.EarthRadius + A)) : 0.01;
            Shader.SetGlobalVector(ShaderIds.Earth, new Vector4((float)cosLimb, (float)h, (float)coverage, (float)eps0));
            double drift = ThreadConstants.CloudDriftSpeed * t;
            double daz = ThreadConstants.CloudDriftAzimuthDeg * ThreadMath.Deg;
            double earthDrift = ThreadConstants.EarthCloudDriftDegPerS * t * ThreadMath.Deg;
            bool seaOn = s >= ThreadConstants.SeaOnS && h > 0;
            Shader.SetGlobalVector(ShaderIds.Sea, new Vector4(seaOn ? 1f : 0f, (float)(drift * Math.Sin(daz)), (float)(drift * Math.Cos(daz)), (float)earthDrift));

            // time, beacon
            double tw = t % 3600.0;
            double sway = 2.0 * Math.PI * ((t / 11.0) - Math.Floor(t / 11.0));
            Shader.SetGlobalVector(ShaderIds.Time, new Vector4((float)tw, (float)sway, 0f, 0f));
            double pulse = 1.0 + ThreadConstants.BeaconPulseDepth * Math.Sin(2.0 * Math.PI * ThreadConstants.BeaconPulseHz * t);
            Shader.SetGlobalVector(ShaderIds.Beacon, new Vector4((float)ThreadMath.Smoothstep(700, 780, s), (float)ThreadMath.Smoothstep(730, 790, s), (float)pulse, 0f));

            // grade
            GradeState.ExposureEV = (float)ev;
            GradeState.BloomThreshold = (float)ThreadMath.BloomThreshold(s);
            GradeState.BloomIntensity = (float)ThreadMath.BloomIntensity(s);
            GradeState.BloomScatter = (float)ThreadMath.BloomScatter(s);
            GradeState.Vignette = (float)ThreadMath.Vignette(s);
            GradeState.Saturation = (float)ThreadMath.Saturation(s);
            GradeState.Contrast = (float)ThreadMath.Contrast(s);
            GradeState.Darkness = (float)D;
            GradeState.DitherLsb = s_ditherLsb;

            // frame state for updaters
            Frame.s = s;
            Frame.ambientTime = t;
            Frame.aVirt = A;
            Frame.darkness = D;
            Frame.beat = ThreadMath.BeatIndex(s);
            Frame.groundActive = m_groundActive;
            Frame.nearActive = m_nearActive;
            if (cam != null)
            {
                Frame.cameraPos = cam.transform.position;
                Frame.cameraForward = cam.transform.forward;
                Frame.renderHeight = cam.pixelHeight;
                Frame.fovV = cam.fieldOfView;
            }
        }

        static void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            float H = Mathf.Max(1, cam.pixelHeight);
            float minW = Mathf.Max((float)ThreadConstants.ThreadMinWidthFloorPx, (float)ThreadConstants.ThreadMinWidthPx * H / 1080f);
            Shader.SetGlobalVector(ShaderIds.Screen, new Vector4(H, minW, 1f / H, 0f));
        }

        static Vector4 V4(D3 v) => new Vector4((float)v.x, (float)v.y, (float)v.z, 0f);
    }
}
