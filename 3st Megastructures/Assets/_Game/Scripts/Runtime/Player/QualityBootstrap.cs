// QualityBootstrap: frame pacing, window mode and the fullscreen toggle (MASTER_PROMPT 8.1, 8.10, Part 9, 11.7, R8 4).
// VSync on by default (the player runs at the monitor refresh); performance runs (-perf-log) use vSyncCount = 0 and
// targetFrameRate = -1. -res WxH opens a window of that size. F11 toggles FullScreenWindow (at the display's native
// size, so there are never black bars) and Windowed. Alt+Enter is the native Windows switch (PlayerSettings
// allowFullscreenSwitch = true, R8 4); handling it in code as well would toggle twice, so code only acts on Alt+Enter
// when the native switch did not change the mode within 0.5 s (a fallback if the player setting is ever off).
using System;
using UnityEngine;

namespace Megastructure.Player
{
    [DefaultExecutionOrder(-900)]
    public class QualityBootstrap : MonoBehaviour
    {
        public const double AltEnterFallbackDelay = 0.5;

        int m_windowW, m_windowH;
        bool m_altEnterPending;
        double m_altEnterTime;
        FullScreenMode m_modeAtAltEnter;

        void Awake()
        {
            bool perf = CliArgs.PerfLog != null;
            QualitySettings.vSyncCount = perf ? 0 : 1;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;   // autoplay, captures and the audio dump must not stall when unfocused

            if (CliArgs.TryGetRes(out int w, out int h) && !Application.isEditor)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                m_windowW = w; m_windowH = h;
            }
            Debug.Log($"[THREAD] quality: vSync={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate} perf={perf} " +
                      $"screen={Screen.width}x{Screen.height} mode={Screen.fullScreenMode} gfx={SystemInfo.graphicsDeviceType}");
        }

        void Start()
        {
            ThreadInput.DeviceSummary(out bool kb, out bool mouse, out bool gp);
            Debug.Log($"[THREAD] input devices: keyboard={kb} mouse={mouse} gamepad={gp}");
        }

        void Update()
        {
            if (ThreadInput.F11Pressed()) { m_altEnterPending = false; Toggle(); return; }

            double now = Time.unscaledTimeAsDouble;
            if (ThreadInput.AltEnterPressed())
            {
                m_altEnterPending = true;
                m_altEnterTime = now;
                m_modeAtAltEnter = Screen.fullScreenMode;
            }
            else if (m_altEnterPending && now - m_altEnterTime > AltEnterFallbackDelay)
            {
                m_altEnterPending = false;
                if (Screen.fullScreenMode == m_modeAtAltEnter) Toggle();   // the native switch did not act
            }
        }

        /// <summary>FullScreenWindow at the native display size, or back to the last windowed size.</summary>
        public void Toggle()
        {
            if (Application.isEditor) return;   // SetResolution only affects the Game view in the Editor
            var di = Screen.mainWindowDisplayInfo;
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                m_windowW = Screen.width; m_windowH = Screen.height;
                int fw = di.width > 0 ? di.width : Screen.currentResolution.width;
                int fh = di.height > 0 ? di.height : Screen.currentResolution.height;
                Screen.SetResolution(fw, fh, FullScreenMode.FullScreenWindow);
                Debug.Log($"[THREAD] fullscreen on: {fw}x{fh}");
            }
            else
            {
                int w = m_windowW > 0 ? m_windowW : Mathf.RoundToInt(Math.Max(1, di.width) * 0.75f);
                int h = m_windowH > 0 ? m_windowH : Mathf.RoundToInt(Math.Max(1, di.height) * 0.75f);
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                Debug.Log($"[THREAD] fullscreen off: {w}x{h}");
            }
        }
    }
}
