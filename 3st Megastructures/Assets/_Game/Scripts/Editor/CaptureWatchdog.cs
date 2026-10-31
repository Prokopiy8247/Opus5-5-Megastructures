// CaptureWatchdog: guards BatchTools.CaptureInEditor (Editor batchmode play-mode captures, research R4 3.5).
// [InitializeOnLoad] re-installs the hooks after every domain reload; the state lives in SessionState, which
// survives reloads (Enter Play Mode Options may or may not reload the domain).
// Exit codes: 3 = timeout (-editor-timeout seconds, default 1800) or play mode never started / ended without a
// result; when play mode ends normally the code is taken from the process environment variable THREAD_EXIT_CODE
// (set by the runtime harness before it stops play mode), so a runtime that cannot reference UnityEditor can
// still report its result. A runtime may also call UnityEditor.EditorApplication.Exit(code) directly.
using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Megastructure.EditorTools
{
    [InitializeOnLoad]
    public static class CaptureWatchdog
    {
        public const string ExitCodeEnv = "THREAD_EXIT_CODE";
        public const string TimeoutArg = "-editor-timeout";
        public const double DefaultTimeoutSeconds = 1800.0;
        /// <summary>Play mode must have started this many seconds after arming (compile errors block it silently).</summary>
        public const double StartGraceSeconds = 120.0;

        const string kActive = "Megastructure.Capture.Active";
        const string kDeadline = "Megastructure.Capture.Deadline";
        const string kArmedAt = "Megastructure.Capture.ArmedAt";
        const string kEnteredPlay = "Megastructure.Capture.EnteredPlay";

        static bool s_Active;
        static double s_Deadline, s_ArmedAt;
        static bool s_Exiting;

        static CaptureWatchdog()
        {
            s_Active = SessionState.GetBool(kActive, false);
            s_Deadline = ReadDouble(kDeadline, double.MaxValue);
            s_ArmedAt = ReadDouble(kArmedAt, 0);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static bool Active => s_Active;

        /// <summary>Starts the watchdog (call right before EditorApplication.isPlaying = true).</summary>
        public static void Arm(double timeoutSeconds)
        {
            double now = EditorApplication.timeSinceStartup;
            s_Active = true;
            s_ArmedAt = now;
            s_Deadline = now + Math.Max(10.0, timeoutSeconds);
            SessionState.SetBool(kActive, true);
            SessionState.SetBool(kEnteredPlay, false);
            SessionState.SetString(kDeadline, s_Deadline.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetString(kArmedAt, s_ArmedAt.ToString("R", CultureInfo.InvariantCulture));
            ThreadLog.Info($"CaptureWatchdog: armed, timeout {timeoutSeconds:F0} s");
        }

        public static void Disarm()
        {
            s_Active = false;
            SessionState.EraseBool(kActive);
            SessionState.EraseBool(kEnteredPlay);
            SessionState.EraseString(kDeadline);
            SessionState.EraseString(kArmedAt);
        }

        static double ReadDouble(string key, double fallback)
        {
            string s = SessionState.GetString(key, "");
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        static void Tick()
        {
            if (!s_Active || s_Exiting) return;
            double now = EditorApplication.timeSinceStartup;
            if (now > s_Deadline)
            {
                Finish(3, "timeout: the capture did not finish within " + TimeoutArg);
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(kEnteredPlay, false) && now > s_ArmedAt + StartGraceSeconds)
                Finish(3, "play mode did not start (compile errors?)");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!s_Active || s_Exiting) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(kEnteredPlay, true);
                ShaderUtil.allowAsyncCompilation = false;   // no cyan placeholder shaders in captures (R4 pitfall 7)
                ThreadLog.Info("CaptureWatchdog: entered play mode");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                string env = Environment.GetEnvironmentVariable(ExitCodeEnv);
                if (int.TryParse(env, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                    Finish(code, "play mode ended, runtime reported exit code " + code);
                else
                    Finish(3, "play mode ended without a result (" + ExitCodeEnv + " not set)");
            }
        }

        static void Finish(int code, string reason)
        {
            s_Exiting = true;
            if (code == 0) ThreadLog.Info("CaptureWatchdog: " + reason + " -> exit 0");
            else Debug.LogError(ThreadLog.Prefix + "CaptureWatchdog: " + reason + " -> exit " + code);
            Disarm();
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else s_Exiting = false;
        }
    }
}
