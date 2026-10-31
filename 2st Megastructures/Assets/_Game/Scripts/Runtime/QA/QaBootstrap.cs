using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megastructure
{
    /// <summary>
    /// Makes the QA entry points independent of scene wiring: before the first scene loads it sets the player options
    /// the QA modes need (run in background; vsync off and no frame cap for captures and perf runs), and after the
    /// scene loads it adds CaptureHarness / AutoTest / WallTest / PerfMonitor / BootCapture to [Game] when their flag is
    /// present and the scene does not already contain them (AfterSceneLoad runs before the first rendered frame, so the
    /// boot capture sees frame 0). A scene reload (the soak restart, the pause-menu restart) re-adds the PerfMonitor
    /// only (its samples live in static storage). Nothing is added in normal play (no debug leftovers, Part 10 #26).
    /// </summary>
    public static class QaBootstrap
    {
        static bool AnyQa => CliArgs.Capturing || CliArgs.AutoTest || CliArgs.WallTest || CliArgs.PerfMode || CliArgs.Soak > 0f || BootCapture.Requested;
        static int _loads;
        static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loads = 0;
            if (_subscribed) SceneManager.sceneLoaded -= OnSceneLoaded;
            _subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeScene()
        {
            QaLog.Install();
            if (!AnyQa) return;
            Application.runInBackground = true;
            if (CliArgs.Capturing || CliArgs.PerfMode)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
            if (!_subscribed) { SceneManager.sceneLoaded += OnSceneLoaded; _subscribed = true; }
            Debug.Log("[Ring] QA mode:" + (CliArgs.Capturing ? " capture" : "") + (CliArgs.AutoTest ? " autotest" : "") +
                      (CliArgs.WallTest ? " walltest" : "") + (CliArgs.PerfMode ? " perf" : "") + (CliArgs.Soak > 0f ? " soak" : "") +
                      (BootCapture.Requested ? " boot-capture" : "") +
                      " (project root " + QaUtil.ProjectRoot() + ")");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterScene()
        {
            if (!AnyQa) return;
            if (_loads == 0) _loads = 1;   // the first scene (sceneLoaded may or may not have fired before this)
            EnsureAll(true);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            _loads++;
            if (_loads > 1) EnsureAll(false);   // a reload: only the perf monitor carries over
        }

        static void EnsureAll(bool first)
        {
            GameObject host = GameObject.Find("[Game]");
            if (host == null) host = new GameObject("[QA]");
            if (first)
            {
                if (BootCapture.Requested) Ensure<BootCapture>(host);
                if (CliArgs.Capturing) Ensure<CaptureHarness>(host);
                if (CliArgs.AutoTest) Ensure<AutoTest>(host);
                if (CliArgs.WallTest) Ensure<WallTest>(host);
            }
            if (CliArgs.PerfMode || CliArgs.Soak > 0f) Ensure<PerfMonitor>(host);
        }

        static void Ensure<T>(GameObject host) where T : Behaviour
        {
            if (Object.FindAnyObjectByType<T>(FindObjectsInactive.Include) != null) return;
            host.AddComponent<T>();
            Debug.Log("[Ring] QA: added " + typeof(T).Name + " to " + host.name);
        }
    }
}
