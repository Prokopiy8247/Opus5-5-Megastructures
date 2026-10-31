using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure
{
    /// <summary>
    /// Display and frame pacing (Part 7.3, 8.1, 8.10, 9, 11.7). <see cref="Apply"/> runs once at the start of the load
    /// (GameBootstrap); <see cref="Tick"/> runs every frame for the fullscreen toggles.
    ///
    /// - Resolution: <c>-res WxH</c> opens a window of that size; otherwise the player goes fullscreen (borderless) at the
    ///   display's native resolution, unless Unity's own <c>-screen-width/-screen-height/-screen-fullscreen</c> flags were given.
    ///   Nothing is changed in the Editor (Game view).
    /// - VSync on (vSyncCount 1) by default; <c>-perf-log</c> runs measure uncapped (vSyncCount 0, targetFrameRate -1).
    /// - F11 toggles fullscreen in script. Alt+Enter is Unity's built-in switch when "Allow Fullscreen Switch" is on; if the
    ///   mode has not changed three frames after Alt+Enter (switch disabled), the same toggle runs in script, so it never
    ///   toggles twice. Input System keys only.
    /// </summary>
    public static class QualityBootstrap
    {
        /// <summary>Default window size for F11 / Alt+Enter back to windowed (clamped to the display).</summary>
        public const int DEFAULT_WINDOW_W = 1600, DEFAULT_WINDOW_H = 900;

        /// <summary>True when -perf-log is given (VSync off, uncapped).</summary>
        public static bool PerfMode { get; private set; }

        static int _windowW = DEFAULT_WINDOW_W, _windowH = DEFAULT_WINDOW_H;
        static int _altEnterFrame = -1;
        static FullScreenMode _modeAtAltEnter;

        public static void Apply()
        {
            CliArgs.Parse();
            PerfMode = CliArgs.PerfLog != null;
            QualitySettings.vSyncCount = PerfMode ? 0 : 1;
            Application.targetFrameRate = -1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            bool windowed = CliArgs.Res(out int w, out int h);
            if (windowed) { _windowW = w; _windowH = h; }
#if !UNITY_EDITOR
            bool unityScreenFlags = CliArgs.Has("-screen-width") || CliArgs.Has("-screen-height") || CliArgs.Has("-screen-fullscreen");
            if (windowed)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }
            else if (!unityScreenFlags)
            {
                Screen.SetResolution(NativeWidth, NativeHeight, FullScreenMode.FullScreenWindow);
            }
#endif
            Debug.Log("[ABYSS] quality: vSyncCount " + QualitySettings.vSyncCount + ", targetFrameRate " + Application.targetFrameRate
                      + (windowed ? ", windowed " + w + "x" + h : ", native fullscreen " + NativeWidth + "x" + NativeHeight)
                      + (PerfMode ? " (perf run)" : ""));
        }

        static int NativeWidth => Display.main != null && Display.main.systemWidth > 0 ? Display.main.systemWidth : Screen.currentResolution.width;
        static int NativeHeight => Display.main != null && Display.main.systemHeight > 0 ? Display.main.systemHeight : Screen.currentResolution.height;

        /// <summary>Per-frame fullscreen toggles (allocation-free).</summary>
        public static void Tick()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f11Key.wasPressedThisFrame) ToggleFullscreen();

            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            if (alt && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                _altEnterFrame = Time.frameCount;
                _modeAtAltEnter = Screen.fullScreenMode;
            }
            if (_altEnterFrame >= 0 && Time.frameCount - _altEnterFrame >= 3)
            {
                _altEnterFrame = -1;
                if (Screen.fullScreenMode == _modeAtAltEnter) ToggleFullscreen();   // Unity's built-in switch did not act
            }
        }

        /// <summary>Fullscreen (borderless, native resolution) to windowed and back.</summary>
        public static void ToggleFullscreen()
        {
#if !UNITY_EDITOR
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                Screen.SetResolution(NativeWidth, NativeHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                int w = Mathf.Min(_windowW, Mathf.RoundToInt(NativeWidth * 0.9f));
                int h = Mathf.Min(_windowH, Mathf.RoundToInt(NativeHeight * 0.9f));
                Screen.SetResolution(Mathf.Max(640, w), Mathf.Max(360, h), FullScreenMode.Windowed);
            }
#endif
        }
    }
}
