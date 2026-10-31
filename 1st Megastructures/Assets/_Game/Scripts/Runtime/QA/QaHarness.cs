using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Runtime QA harness (Part 11, 14.10; docs/QA_RUNTIME.md). Lives on the Bootstrap GameObject (added by the SceneBuilder),
    /// execution order +300: its Update runs after every game Update and its LateUpdate after CameraRig (+100) and the
    /// instanced draws (+200), which is where captures are rendered.
    ///
    /// Inert unless a QA flag is present: Awake disables the component (no Update, no LateUpdate, no allocation, no log hook).
    /// QA flags: -capture DIR, -capture-parity, -autotest, -bot NAME, -perf-log FILE, -soak S, -quit-after S, -firstlast.
    ///
    /// Modes (one per run, first match wins): Capture (stills, key-shot variants, composition lint, frame sequences, parity)
    /// > Bot (wallhug, raygrid) > Soak > AutoTest > Perf (autowalk with the perf log) > Idle (only -quit-after / -firstlast).
    /// -perf-log also records alongside AutoTest and Soak; -firstlast adds window captures of the first / fade-in / last frames.
    /// Every mode ends through <see cref="RequestQuit"/>: the registered finalizers write their reports and return an exit
    /// code (0 pass, 1 fail); the process exits with the largest. -quit-after S (wall clock since start-up) aborts any mode:
    /// finalizers then write partial reports and an unfinished mode returns 1.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class QaHarness : MonoBehaviour
    {
        public static QaHarness Instance { get; private set; }

        // ---------------------------------------------------------------- extra QA flags (CliArgs keeps unknown flags with one
        // following non-flag token as the value, so zero-arity flags must be followed by another flag or end the line)
        public const string QaOutFlag = "-qa-out";               // DIR: JSON outputs of autotest / bots / soak (default QA/)
        public const string ParityFlag = "-capture-parity";      // [TAG]: window vs RenderTexture parity at the window size
        public const string SeqSecondsFlag = "-seq-seconds";     // S: override the length of every selected sequence
        public const string SeqFpsFlag = "-seq-fps";             // F: override the virtual frame rate of every sequence
        public const string SeqStaticFlag = "-seq-static";       // gradient sequences hold the key pose instead of moving
        public const string RealtimeFlag = "-realtime";          // autotest / bots / perf / soak run in real time (no captureDeltaTime)
        public const string LintOnlyFlag = "-lint-only";         // capture: composition lint and report only, no PNGs
        public const string FlipFlag = "-capture-flip";          // on|off: reverse readback rows (platform orientation override)
        public const string FirstLastFlag = "-firstlast";        // [DIR]: window captures of the first 60 / fade-in 60 / last 60 frames
        public const string SoakVirtualFlag = "-soak-virtual";   // soak in virtual time (captureDeltaTime 1/60, VSync off)
        public const string SoakMemTolFlag = "-soak-mem-tol-mb"; // MB: soak memory growth tolerance (default 32 MB or 5 %)
        public const string BotFilterFlag = "-bot-filter";       // TEXT: run only wall-hug runs whose name contains TEXT
        public const string BotSpeedsFlag = "-bot-speeds";       // walk|brisk|both (default both)
        public const string SettleFlag = "-capture-settle";      // N: frames between pose and render (default 3)

        public enum Mode { None = 0, Capture = 1, Bot = 2, Soak = 3, AutoTest = 4, Perf = 5, Idle = 6 }

        /// <summary>Contributes an exit code (0 pass, 1 fail) and writes its report; aborted = -quit-after fired first.</summary>
        public delegate int Finalizer(bool aborted);

        public Mode ActiveMode { get; private set; }
        public QaHoldDriver Hold { get; } = new QaHoldDriver();
        public QaMonitor Monitor { get; private set; }
        public QaPerf Perf { get; private set; }

        /// <summary>Seconds until the scheduled quit (virtual while captureDeltaTime is set), +inf when none is scheduled.</summary>
        public float QuitRemaining => _quitRequested ? _quitDelay : float.PositiveInfinity;
        public bool QuitRequested => _quitRequested;
        public string RunId { get; private set; }

        readonly List<Finalizer> _finalizers = new List<Finalizer>(4);
        QaCaptureRunner _capture;
        bool _quitRequested, _quitting, _aborted, _fastVirtual, _vsyncApplied;
        float _quitDelay;
        float _quitAfter = -1f;

        // ---------------------------------------------------------------- flag checks

        /// <summary>True when any flag that activates the harness is present.</summary>
        public static bool AnyQaFlag()
        {
            CliArgs.Parse();
            return CliArgs.Has(CliArgs.CaptureFlag) || CliArgs.Has(ParityFlag) || CliArgs.AutoTest || CliArgs.Has(CliArgs.BotFlag)
                   || CliArgs.Has(CliArgs.PerfLogFlag) || CliArgs.Has(CliArgs.SoakFlag) || CliArgs.Has(CliArgs.QuitAfterFlag)
                   || CliArgs.Has(FirstLastFlag);
        }

        public static bool Realtime => CliArgs.Has(RealtimeFlag);

        static Mode ChooseMode()
        {
            if (CliArgs.Has(CliArgs.CaptureFlag) || CliArgs.Has(ParityFlag)) return Mode.Capture;
            if (CliArgs.Has(CliArgs.BotFlag)) return Mode.Bot;
            if (CliArgs.Has(CliArgs.SoakFlag)) return Mode.Soak;
            if (CliArgs.AutoTest) return Mode.AutoTest;
            if (CliArgs.Has(CliArgs.PerfLogFlag)) return Mode.Perf;
            return Mode.Idle;
        }

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            if (!AnyQaFlag())
            {
                enabled = false;   // zero cost: no Update / LateUpdate, nothing subscribed
                return;
            }
            if (Instance != null && Instance != this) Debug.LogWarning("[ABYSS] QA harness: replacing a previous instance");
            Instance = this;
            QaLog.Install();
            ActiveMode = ChooseMode();
            RunId = QaPaths.Stamp();
            _quitAfter = CliArgs.QuitAfter;
            Monitor = new QaMonitor();

            // virtual time for speed and determinism (set before the walk clock starts)
            switch (ActiveMode)
            {
                case Mode.Capture:
                    _fastVirtual = true;
                    Time.captureDeltaTime = 1f / 60f;
                    break;
                case Mode.AutoTest:
                case Mode.Bot:
                case Mode.Perf:
                    if (!Realtime) { Time.captureDeltaTime = 1f / 60f; _fastVirtual = true; }
                    break;
                case Mode.Soak:
                    if (CliArgs.Has(SoakVirtualFlag) && !Realtime) { Time.captureDeltaTime = 1f / 60f; _fastVirtual = true; }
                    break;
            }
            Debug.Log("[ABYSS] QA harness: mode " + ActiveMode + (Time.captureDeltaTime > 0f ? ", virtual dt " + QaJson.F(Time.captureDeltaTime, 5) : ", real time")
                      + ", outputs under " + QaPaths.OutDir);
        }

        void Start()
        {
            switch (ActiveMode)
            {
                case Mode.Capture:
                    _capture = new QaCaptureRunner(this);
                    StartCoroutine(_capture.Run());
                    break;
                case Mode.Bot:
                    StartCoroutine(new QaBots(this).Run());
                    break;
                case Mode.Soak:
                    StartCoroutine(new QaSoak(this).Run());
                    break;
                case Mode.AutoTest:
                    StartCoroutine(new QaAutoTest(this).Run());
                    break;
                case Mode.Perf:
                    StartCoroutine(PerfOnlyRun());
                    break;
            }
            bool walking = ActiveMode == Mode.AutoTest || ActiveMode == Mode.Perf || ActiveMode == Mode.Soak;
            if (walking && CliArgs.PerfLog != null)
            {
                Perf = new QaPerf(this);
                AddFinalizer(Perf.Finish);
            }
            if (ActiveMode != Mode.Capture && ActiveMode != Mode.Bot && CliArgs.Has(FirstLastFlag))
            {
                var fl = new QaFirstLast(this);
                AddFinalizer(fl.Finish);
                StartCoroutine(fl.Run());
            }
        }

        void Update()
        {
            if (!_vsyncApplied)
            {
                // after GameBootstrap.Start applied the display settings (QualityBootstrap): uncapped for virtual-time runs
                _vsyncApplied = true;
                if (_fastVirtual)
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                }
            }

            if (!_quitRequested && _quitAfter > 0f && Time.realtimeSinceStartup >= _quitAfter)
            {
                Debug.Log("[ABYSS] QA: -quit-after " + QaJson.F(_quitAfter, 1) + " s reached (mode " + ActiveMode + ")");
                RequestQuit(0f, true);
            }

            if (_quitRequested && !_quitting)
            {
                _quitDelay -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
                if (_quitDelay <= 0f) DoQuit();
            }
        }

        void LateUpdate()
        {
            Monitor?.LateTick();
            Perf?.LateTick();
            _capture?.LateTick();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _capture?.Dispose();
            Perf?.Dispose();
        }

        // ---------------------------------------------------------------- shared helpers for the runners

        /// <summary>Register a report writer that runs right before the process exits.</summary>
        public void AddFinalizer(Finalizer f) { if (f != null) _finalizers.Add(f); }

        /// <summary>Schedule the quit after delay seconds (virtual seconds while captureDeltaTime is set).</summary>
        public void RequestQuit(float delaySeconds, bool aborted = false)
        {
            if (_quitRequested)
            {
                if (delaySeconds < _quitDelay) _quitDelay = delaySeconds;
                _aborted |= aborted;
                return;
            }
            _quitRequested = true;
            _aborted = aborted;
            _quitDelay = Mathf.Max(0f, delaySeconds);
        }

        void DoQuit()
        {
            _quitting = true;
            int code = 0;
            for (int i = 0; i < _finalizers.Count; i++)
            {
                try { code = Math.Max(code, _finalizers[i](_aborted)); }
                catch (Exception ex)
                {
                    code = Math.Max(code, 1);
                    Debug.LogWarning("[ABYSS] QA finalizer failed: " + ex);
                }
            }
            if (_aborted && ActiveMode != Mode.Idle && _finalizers.Count == 0) code = Math.Max(code, 1);
            try { AsyncGPUReadback.WaitAllRequests(); } catch (Exception) { }
            Debug.Log("[ABYSS] QA: quitting with exit code " + code + " (mode " + ActiveMode + (_aborted ? ", aborted by -quit-after" : "") + ")");
            Time.captureDeltaTime = 0f;
            QuitProcess(code);
        }

        /// <summary>Exit the player with a code (Editor: stop play mode, or exit the batchmode Editor with the code).</summary>
        public static void QuitProcess(int code)
        {
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        /// <summary>Wait until GameBootstrap finished the load (and, when asked, until the picture is fully faded in).</summary>
        public IEnumerator WaitForLoad(bool needPicture)
        {
            while (!GameBootstrap.Loaded) yield return null;
            if (!needPicture) yield break;
            float t0 = Time.realtimeSinceStartup;
            while (GameState.PictureFade < 0.999f && Time.realtimeSinceStartup - t0 < 15f) yield return null;
            if (GameState.PictureFade < 0.999f)
                Debug.LogWarning("[ABYSS] QA: picture fade still " + QaJson.F(GameState.PictureFade, 3) + " 15 s after the load");
        }

        /// <summary>Wait the given virtual seconds (frames x deltaTime).</summary>
        public static IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            }
        }

        /// <summary>Install the hold driver on the player (captures: nothing human moves the player or the view).</summary>
        public void InstallHold(float yaw, float pitch)
        {
            var pc = PlayerController.Instance;
            Hold.Yaw = yaw;
            Hold.Pitch = pitch;
            Hold.Active = true;
            if (pc == null) return;
            var walker = AutoWalker.Instance;
            if (walker != null && walker.IsRunning) walker.Stop();
            pc.Driver = Hold;
        }

        /// <summary>Common header of every QA JSON report.</summary>
        public void WriteHeader(QaJson j, string mode)
        {
            j.Prop("mode", mode);
            j.Prop("run", RunId);
            j.Prop("date", QaPaths.IsoNow());
            j.Prop("build", QaPaths.BuildKind);
            j.Prop("development", Debug.isDebugBuild);
            j.Prop("unity", Application.unityVersion);
            j.Prop("product", Application.productName);
            j.Prop("seed", (long)GameState.Seed);
            j.Prop("virtual_dt", Time.captureDeltaTime);
            j.Prop("realtime", Time.captureDeltaTime <= 0f);
            j.Prop("screen", Screen.width + "x" + Screen.height);
            j.Prop("graphics", SystemInfo.graphicsDeviceType.ToString());
            j.Prop("gpu", SystemInfo.graphicsDeviceName);
            j.Prop("load_seconds", GameBootstrap.LoadSeconds);
            j.Prop("args", string.Join(" ", CliArgs.Raw));
        }

        /// <summary>Default autowalk for -perf-log alone: autowalk (or cinematic with -cinematic), finish, write, quit.</summary>
        IEnumerator PerfOnlyRun()
        {
            yield return WaitForLoad(false);
            var walker = AutoWalker.Instance;
            yield return WaitSeconds(0.5f);
            if (walker != null && !walker.IsRunning) walker.Begin(GameState.Cinematic);
            double t0 = SceneClock.Time;
            while (walker != null && !walker.Finished && SceneClock.Time - t0 < AbyssConst.TOTAL_DURATION + 120.0) yield return null;
            RequestQuit(2f);
        }
    }
}
