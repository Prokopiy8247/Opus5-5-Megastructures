using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Player quality settings at start (Part 7.3, 8.1, 8.10, 11.7): -res WxH (Screen.SetResolution keeping the mode,
    /// default FullScreenWindow), VSync on (vSyncCount 1, targetFrameRate -1) except -perf / -perf-log
    /// (vSyncCount 0, targetFrameRate -1), Application.runInBackground = true, and a one-time log of the graphics API and
    /// reversed Z. Also owns the fullscreen toggle (F11 / Alt+Enter: FullScreenWindow &lt;-&gt; Windowed).
    /// </summary>
    [DefaultExecutionOrder(-990)]
    public sealed class QualityBootstrap : MonoBehaviour
    {
        static bool _applied;
        static int _winW = 1600, _winH = 900;

        public static void ResetStatics() { _applied = false; }

        void Awake() { Apply(); }

        /// <summary>Idempotent: applies the settings once per run.</summary>
        public static void Apply()
        {
            if (_applied) return;
            _applied = true;
            Application.runInBackground = true;
            // Part 8.1 / 11.7: VSync on by default; -perf / -perf-log measure with vSyncCount 0 and no frame cap
            // (the QA harnesses set their own timing for captures)
            bool noVsync = CliArgs.PerfMode;
            QualitySettings.vSyncCount = noVsync ? 0 : 1;
            Application.targetFrameRate = -1;

            var cur = Screen.currentResolution;
            if (CliArgs.Res(out int w, out int h))
            {
                var mode = Screen.fullScreenMode;
                Screen.SetResolution(w, h, mode);
                if (mode == FullScreenMode.Windowed) { _winW = w; _winH = h; }
                Debug.Log("[Ring] resolution " + w + "x" + h + " (" + mode + ")");
            }
            // a windowed size that fits the desktop (16:9, at most 80 % of it)
            if (cur.width > 0 && cur.height > 0 && (_winW > cur.width * 0.9f || _winH > cur.height * 0.9f))
            {
                _winH = Mathf.RoundToInt(cur.height * 0.8f);
                _winW = Mathf.RoundToInt(_winH * 16f / 9f);
            }
            Debug.Log("[Ring] QualityBootstrap: " + SystemInfo.graphicsDeviceType + ", reversed Z " + SystemInfo.usesReversedZBuffer
                      + ", vSync " + QualitySettings.vSyncCount + ", screen " + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode
                      + ", runInBackground " + Application.runInBackground);
        }

        /// <summary>FullScreenWindow &lt;-&gt; Windowed (Part 9: Alt+Enter or F11).</summary>
        public static void ToggleFullscreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(_winW, _winH, FullScreenMode.Windowed);
            }
        }
    }
}
