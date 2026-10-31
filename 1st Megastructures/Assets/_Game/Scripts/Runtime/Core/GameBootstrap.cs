using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Runs the whole load behind a black screen (CONTRACTS section 3, Part 7.4) and starts the walk.
    ///
    /// Sequence (time-sliced: one beat per frame, so the window stays responsive):
    /// parse CliArgs -> display settings (QualityBootstrap) -> MaterialLibrary.MakeCurrent -> procedural textures ->
    /// Build beats 1..7 (ctx.Root = beat GameObject, ctx.Colliders = child "Colliders") -> place the player (start, -beat N,
    /// -progress X or -d D; after the lift the platform is parked at the bottom dock) -> shader warm-up (WarmupLoader) ->
    /// ProgressDirector.SnapAll and BeatDirector.Begin -> settle frames -> clock start -> title timeline (FadeController).
    /// During all of it the camera renders, but the final pass multiplies the picture by _AbPictureFade = 0 and FadeController
    /// covers the screen with a black layer; audio follows GameState.PictureFade and stays silent.
    /// Logs "[ABYSS] load done in X s" (target at most 15 s).
    ///
    /// Failure policy: the load always reaches the title timeline. Every step runs guarded; an exception is logged once as
    /// "[ABYSS-ERROR] ..." (the lead's log scan matches that tag) and the load goes on. A beat whose Build throws is skipped
    /// (not ticked), the warm-up coroutine is stepped by hand inside try/catch, and without a FadeController the picture is
    /// switched on directly.
    ///
    /// Execution order -2000: Awake resets the per-run shared state before every other script's Awake (a scene reload for
    /// Restart keeps statics alive) and holds the scene clock.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        /// <summary>True once the load is complete and the walk (title timeline) has started.</summary>
        public static bool Loaded { get; private set; }

        /// <summary>True once every beat's Build has run.</summary>
        public static bool BeatsBuilt { get; private set; }

        /// <summary>Wall-clock duration of the last load (s).</summary>
        public static float LoadSeconds { get; private set; }

        /// <summary>Start path distance chosen by the CLI (0 = W0).</summary>
        public static float StartD { get; private set; }

        public const float LOAD_TARGET_SECONDS = 15f;

        [Header("Wiring (assigned by the SceneBuilder)")]
        public MaterialLibrary materialLibrary;
        [Tooltip("Beats 1..7 in order.")]
        public BeatBase[] beats = new BeatBase[7];
        public Light keyLight;
        public Volume globalVolume;
        public Camera mainCamera;

        [Header("Load")]
        [Tooltip("Frames rendered (black) after SnapAll before the title timeline starts.")]
        public int settleFrames = 8;

        readonly bool[] _built = new bool[7];
        float _loadStart;

        void Awake()
        {
            Instance = this;
            _loadStart = Time.realtimeSinceStartup;
            Loaded = false;
            BeatsBuilt = false;
            ResetRunState();
            ClockDriver.Hold();
            InstancedRegistry.Clear();
            GameState.PictureFade = 0f;
            Shader.SetGlobalFloat(FadeController.AbPictureFade, 0f);
        }

        void Start()
        {
            StartCoroutine(Load());
        }

        void Update()
        {
            QualityBootstrap.Tick();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Loaded = false;
                BeatsBuilt = false;
            }
        }

        /// <summary>
        /// Reset the shared per-run state to its start values (once, before any other Awake). These fields have other owners
        /// during the run; this is the only write GameBootstrap makes to them, so a Restart begins from a clean state.
        /// </summary>
        static void ResetRunState()
        {
            GameState.D = 0f;
            GameState.E = 0f;
            GameState.Beat = 1;
            GameState.Lift = LiftPhase.DockedTop;
            GameState.RideSeconds = -1f;
            GameState.RideU = 0f;
            GameState.RideStarted = false;
            GameState.Paused = false;
            GameState.Ended = false;
            GameState.GoalReached = false;
            GameState.GoalRealTime = -1;
            GameState.HoldH7 = false;
            GameState.PictureFade = 0f;
            SceneClock.Frozen = false;      // ClockDriver re-applies -freeze-clock in its Awake
            SceneClock.Time = 0;
            SceneClock.RealTime = 0;
            Time.timeScale = 1f;
        }

        /// <summary>Mode flags into GameState (CliArgs applies them only on its first parse; a Restart needs them again).</summary>
        static void ApplyModes()
        {
            CliArgs.Parse();
            GameState.Seed = CliArgs.Seed;
            GameState.AutoTest = CliArgs.AutoTest;
            GameState.Cinematic = CliArgs.Cinematic;
            GameState.CaptureMode = CliArgs.Has(CliArgs.CaptureFlag);
        }

        IEnumerator Load()
        {
            Debug.Log("[ABYSS] load started");
            Step("cli", () =>
            {
                ApplyModes();
                QualityBootstrap.Apply();
            });

            Step("materials", () =>
            {
                if (mainCamera == null) mainCamera = GameState.MainCamera != null ? GameState.MainCamera : Camera.main;
                if (GameState.MainCamera == null) GameState.MainCamera = mainCamera;
                if (materialLibrary == null)
                {
                    Debug.LogError("[ABYSS-ERROR] GameBootstrap: no MaterialLibrary assigned");
                    return;
                }
                materialLibrary.MakeCurrent();
                if (materialLibrary.skybox != null) RenderSettings.skybox = materialLibrary.skybox;
            });
            yield return null;

            Step("textures", () =>
            {
                if (materialLibrary != null) ProceduralTextures.Generate(materialLibrary);
            });
            yield return null;

            // ---------------------------------------------------------------- beats 1..7
            BeatDirector bd = BeatDirector.Instance != null ? BeatDirector.Instance : GetComponent<BeatDirector>();
            if (bd != null && (bd.Beats == null || bd.Beats.Length == 0 || AllNull(bd.Beats))) bd.Beats = beats;
            bool cliTier = false;
            Step("tier flag", () => cliTier = CliArgs.Has(CliArgs.TierFlag));
            for (int i = 0; i < 7; i++)
            {
                _built[i] = false;
                BeatBase b = beats != null && i < beats.Length ? beats[i] : null;
                if (b == null)
                {
                    Debug.LogWarning("[ABYSS] GameBootstrap: beat " + (i + 1) + " is not assigned");
                    continue;
                }
                _built[i] = BuildBeat(i, b, cliTier);
                yield return null;
            }
            BeatsBuilt = true;
            Step("sync transforms", Physics.SyncTransforms);

            // ---------------------------------------------------------------- player placement
            float d = 0f;
            bool after = false;
            Step("placement", () =>
            {
                d = ChooseStartD();
                StartD = d;
                after = d > AbyssConst.D_LIFT + 1e-3f;
                // platform at the top dock, or parked at the bottom with the gate open when the start lies past the lift
                if (LiftController.Instance != null) LiftController.Instance.SnapToProgress(d, after);
                Vector3 feet = PathGuide.WalkPoint(d, after);
                float yaw = PathGuide.HeadingYaw(d, after);
                if (PlayerController.Instance != null) PlayerController.Instance.Teleport(feet, yaw, 0f);   // publishes GameState.PlayerPos
                else Debug.LogError("[ABYSS-ERROR] GameBootstrap: no PlayerController in the scene");
                Physics.SyncTransforms();
                Debug.Log("[ABYSS] start at d = " + F2(d) + (after ? " (after the ride)" : ""));
            });
            yield return null;   // CameraRig places the camera at the new eye in this frame's LateUpdate
            yield return null;

            // ---------------------------------------------------------------- shader warm-up (ProgressDirector still inactive)
            ProgressDirector pd = ProgressDirector.Instance != null ? ProgressDirector.Instance : GetComponent<ProgressDirector>();
            Step("progress init", () =>
            {
                if (pd == null) { Debug.LogError("[ABYSS-ERROR] GameBootstrap: no ProgressDirector"); return; }
                pd.Init(keyLight, globalVolume);
                ConfigureDither(pd);
                pd.PostProcessingEnabled = !CliArgs.NoPost;
            });

            WarmupLoader warm = null;
            Step("warm-up collect", () =>
            {
                warm = new WarmupLoader();
                warm.Collect(materialLibrary);
            });
            Camera cam = GameState.MainCamera != null ? GameState.MainCamera : mainCamera;
            // Stepped by hand so an exception inside the warm-up ends only the warm-up, never this coroutine (a nested
            // "yield return warm.Run()" would propagate it and leave the screen black). C# allows no yield inside a try with
            // catch, so MoveNext is guarded and the yield happens outside. The warm-up restores the Key light in its finally.
            IEnumerator run = warm != null && cam != null ? warm.Run(cam, keyLight) : null;
            while (run != null)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception ex)
                {
                    Debug.LogError("[ABYSS-ERROR] load step 'warm-up' failed, warm-up skipped: " + ex);
                    more = false;
                }
                if (!more) break;
                yield return run.Current;
            }
            if (run is IDisposable disposable) Step("warm-up end", disposable.Dispose);
            if (warm != null) Step("warm-up dispose", warm.Dispose);

            // ---------------------------------------------------------------- look, beats, settle
            double t0 = 0.0;
            Step("clock reset", () =>
            {
                t0 = AbyssConst.NominalTimeAtD(d, after);
                double cliClock = CliArgs.Clock;
                SceneClock.Reset(double.IsNaN(cliClock) ? t0 : cliClock);   // held: machines settle at their start phase
            });
            Step("snap", () =>
            {
                if (pd != null) pd.SnapAll(d);
            });
            // separate step: the beats must tick even if a snap of the blended values failed
            Step("beats begin", () =>
            {
                if (bd != null) bd.Begin(_built, BeatDirector.RawBeat(d));
                else Debug.LogError("[ABYSS-ERROR] GameBootstrap: no BeatDirector");
            });
            for (int i = 0; i < settleFrames; i++) yield return null;

            // ---------------------------------------------------------------- start (every step guarded: the fade-in always starts)
            Step("clock start", () => ClockDriver.Begin(t0));
            LoadSeconds = Time.realtimeSinceStartup - _loadStart;
            Debug.Log("[ABYSS] load done in " + F2(LoadSeconds) + " s");
            if (LoadSeconds > LOAD_TARGET_SECONDS)
                Debug.LogWarning("[ABYSS] load took " + F2(LoadSeconds) + " s, above the " + LOAD_TARGET_SECONDS + " s target");
            Loaded = true;

            Step("cursor", () =>
            {
                if (!GameState.CaptureMode)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            });
            Step("title", () =>
            {
                if (FadeController.Instance != null)
                {
                    FadeController.Instance.BeginTitle(CliArgs.NoFade || GameState.CaptureMode);
                    return;
                }
                // without a FadeController nothing would ever lift _AbPictureFade: show the picture at once
                Debug.LogError("[ABYSS-ERROR] GameBootstrap: no FadeController in the scene; picture switched on without a title");
                GameState.PictureFade = 1f;
                Shader.SetGlobalFloat(FadeController.AbPictureFade, 1f);
            });

            // -cinematic / -autotest: AutoWalker begins by itself once the clock runs (AutoBeginFromFlags); start it here only
            // when that is switched off, with the same mode rule.
            Step("auto walker", () =>
            {
                if ((GameState.Cinematic || GameState.AutoTest) && !GameState.CaptureMode)
                {
                    AutoWalker walker = AutoWalker.Instance;
                    if (walker == null) Debug.LogError("[ABYSS-ERROR] GameBootstrap: no AutoWalker in the scene");
                    else if (!walker.AutoBeginFromFlags) walker.Begin(GameState.Cinematic && !GameState.AutoTest);
                }
            });
        }

        /// <summary>
        /// Build one beat (CONTRACTS section 3). Everything that touches the beat runs inside the try: a beat whose Build
        /// throws is logged once with [ABYSS-ERROR] and skipped (never ticked; what it built before the exception stays in
        /// the scene as it is) and the load goes on with the next beat. Returns true when Build completed.
        /// </summary>
        bool BuildBeat(int i, BeatBase b, bool cliTier)
        {
            float t = Time.realtimeSinceStartup;
            string beatName = AbyssConst.BEAT_NAMES[i];
            try
            {
                if (b.Index != i + 1) Debug.LogError("[ABYSS-ERROR] GameBootstrap: beats[" + i + "] reports Index " + b.Index);
                if (cliTier) b.Tier = CliArgs.Tier;
                var ctx = new BeatContext
                {
                    Root = b.transform,
                    Colliders = EnsureChild(b.transform, "Colliders"),
                    Mats = materialLibrary,
                    Seed = MixSeed(GameState.Seed, (uint)(i + 1)),
                    Tier = b.Tier,
                    EditMode = false,
                };
                b.Build(ctx);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ABYSS-ERROR] beat " + (i + 1) + " (" + beatName + ") Build failed after "
                               + F2(Time.realtimeSinceStartup - t) + " s; beat skipped, load continues: " + ex);
                return false;
            }
            Debug.Log("[ABYSS] beat " + (i + 1) + " (" + beatName + ", " + b.Tier + ") built in "
                      + F2(Time.realtimeSinceStartup - t) + " s");
            return true;
        }

        /// <summary>-d / -progress win over -beat; default 0 (W0).</summary>
        static float ChooseStartD()
        {
            float d = CliArgs.StartD;
            if (d >= 0f) return d;
            int beat = CliArgs.Beat;
            if (beat >= 1 && beat <= 7) return AbyssConst.BEAT_START_D[beat - 1];
            return 0f;
        }

        static void ConfigureDither(ProgressDirector pd)
        {
            string mode = CliArgs.Dither;
            if (mode != null)
            {
                if (mode.Equals("static", StringComparison.OrdinalIgnoreCase)) pd.Dither = ProgressDirector.DitherMode.Static;
                else if (mode.Equals("off", StringComparison.OrdinalIgnoreCase) || mode.Equals("none", StringComparison.OrdinalIgnoreCase))
                    pd.Dither = ProgressDirector.DitherMode.Off;
                else pd.Dither = ProgressDirector.DitherMode.Temporal;
            }
            float amp = CliArgs.DitherAmp;
            if (amp >= 0f) pd.DitherAmplitude = amp;
            Debug.Log("[ABYSS] final dither " + pd.Dither + " " + F2(pd.DitherAmplitude) + " LSB");
        }

        /// <summary>Run one load step; an exception is logged as an error and the load continues (never a black screen forever).</summary>
        static void Step(string name, Action action)
        {
            try { action(); }
            catch (Exception ex) { Debug.LogError("[ABYSS-ERROR] load step '" + name + "' failed: " + ex); }
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static bool AllNull(BeatBase[] a)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != null) return false;
            return true;
        }

        /// <summary>PCG-style integer mix of the global seed and the beat index (never sin-based).</summary>
        public static uint MixSeed(uint seed, uint index)
        {
            uint s = seed * 747796405u + 2891336453u + index * 0x9E3779B9u;
            uint w = ((s >> (int)((s >> 28) + 4u)) ^ s) * 277803737u;
            return (w >> 22) ^ w;
        }

        static string F2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
    }
}
