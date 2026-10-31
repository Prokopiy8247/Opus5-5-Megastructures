// QaCommon: shared plumbing of the runtime QA harness (MASTER_PROMPT 11.1, 11.8, 11.10, 14.10.4).
//   QaMode         which QA run the command line selects (capture > soak > wallbot > autotest)
//   QaLog          counts every Error / Exception / Assert log message from the first frame on
//   QaExit         the one exit path (flushes PerfMonitor, Application.Quit(code) / EditorApplication.Exit)
//   PathGuideProbe reads PathGuide.KillCount and runs PathGuide.CoverageCheck by reflection, so this
//                  folder compiles without the player builder's code
//   QaDrive        helpers to drive the player through IPlayerState / ICameraControl
//   QaChecks       named pass/fail assertions collected into a report
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure.QA
{
    public enum QaRunMode { None = 0, Capture = 1, Soak = 2, WallBot = 3, AutoTest = 4 }

    public static class QaMode
    {
        static bool s_resolved;
        static QaRunMode s_mode;

        /// <summary>The QA run selected by the command line (cached; one runner drives the player).</summary>
        public static QaRunMode Current
        {
            get
            {
                if (!s_resolved) { s_mode = Resolve(); s_resolved = true; }
                return s_mode;
            }
        }

        /// <summary>Precedence: -capture, then -soak S, then -wallbot, then -autotest.</summary>
        public static QaRunMode Resolve()
        {
            if (!string.IsNullOrEmpty(CliArgs.CaptureDir)) return QaRunMode.Capture;
            if (CliArgs.SoakSeconds > 0) return QaRunMode.Soak;
            if (CliArgs.WallBot) return QaRunMode.WallBot;
            if (CliArgs.AutoTest) return QaRunMode.AutoTest;
            return QaRunMode.None;
        }

        public static bool PerfRequested => !string.IsNullOrEmpty(CliArgs.PerfLog);
        public static bool AnyQa => Current != QaRunMode.None || PerfRequested || CliArgs.QuitAfter > 0;

        /// <summary>Drive speed of the bots: -speed X, else 2.5 with -brisk, else 1.5 m/s (14.12).</summary>
        public static float DriveSpeed
        {
            get
            {
                double d = CliArgs.Brisk ? ThreadConstants.BriskSpeed : ThreadConstants.WalkSpeed;
                double v = CliArgs.GetDouble("-speed", d);
                return (float)(v > 0.05 ? v : d);
            }
        }

        /// <summary>"walk", "brisk" or "speed2.00" (used in report file names).</summary>
        public static string SpeedName(float speed)
        {
            if (Math.Abs(speed - ThreadConstants.WalkSpeed) < 1e-4) return "walk";
            if (Math.Abs(speed - ThreadConstants.BriskSpeed) < 1e-4) return "brisk";
            return "speed" + speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_resolved = false; }
    }

    /// <summary>Counts error-level log messages (11.10: zero errors). Installed before the scene loads when any QA flag is present.</summary>
    public static class QaLog
    {
        const int MaxKept = 40;
        static int s_errors, s_warnings;
        static bool s_installed;
        static readonly object s_lock = new object();
        static readonly List<string> s_messages = new List<string>(MaxKept);

        public static int ErrorCount => Volatile.Read(ref s_errors);
        public static int WarningCount => Volatile.Read(ref s_warnings);

        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        public static void Uninstall()
        {
            if (!s_installed) return;
            s_installed = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }

        /// <summary>Copy of the first error messages (condition + first stack line).</summary>
        public static List<string> Messages()
        {
            lock (s_lock) return new List<string>(s_messages);
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) { Interlocked.Increment(ref s_warnings); return; }
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Interlocked.Increment(ref s_errors);
            lock (s_lock)
            {
                if (s_messages.Count >= MaxKept) return;
                string first = null;
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    int nl = stackTrace.IndexOf('\n');
                    first = nl > 0 ? stackTrace.Substring(0, nl) : stackTrace;
                }
                s_messages.Add(type + ": " + condition + (first != null ? " @ " + first.Trim() : ""));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Uninstall();
            s_errors = 0;
            s_warnings = 0;
            lock (s_lock) s_messages.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInstall()
        {
            if (QaMode.AnyQa) Install();
        }
    }

    /// <summary>The single exit path of every QA run.</summary>
    public static class QaExit
    {
        static bool s_quitting;
        public static bool Quitting => s_quitting;

        public static void Quit(int code, string reason)
        {
            if (s_quitting) return;
            s_quitting = true;
            try { PerfMonitor.FlushActive(); }
            catch (Exception e) { Debug.LogWarning("[THREAD] perf flush failed: " + e.Message); }
            Debug.Log("[THREAD] QA exit code=" + code + " (" + reason + ")");
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;   // never close an interactive Editor
#else
            Application.Quit(code);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_quitting = false; }
    }

    /// <summary>Reflective access to Megastructure.Player.PathGuide (owned by the player builder).</summary>
    public static class PathGuideProbe
    {
        public const string TypeName = "Megastructure.Player.PathGuide";
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static Type FindType()
        {
            Type t = typeof(GameBootstrap).Assembly.GetType(TypeName, false);
            if (t != null) return t;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = a.GetType(TypeName, false); } catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        public static object FindInstance(Type t)
        {
            if (t == null) return null;
            PropertyInfo ip = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (ip != null)
            {
                object o = ip.GetValue(null);
                if (o is UnityEngine.Object uo) { if (uo != null) return o; }
                else if (o != null) return o;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(t);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>PathGuide.KillCount (property or field, static or instance).</summary>
        public static bool TryGetKillCount(out int count, out string note)
        {
            count = -1;
            Type t = FindType();
            if (t == null) { note = TypeName + " not found"; return false; }
            object inst = FindInstance(t);
            if (TryReadNumber(t, inst, "KillCount", out long v))
            {
                count = (int)v;
                note = "ok";
                return true;
            }
            note = "PathGuide.KillCount not readable (instance " + (inst != null ? "found" : "missing") + ")";
            return false;
        }

        /// <summary>Runs PathGuide.CoverageCheck (ray-cast grid, 14.4.3) and extracts the miss count.</summary>
        public static bool TryCoverage(out int misses, out string note)
        {
            misses = -1;
            Type t = FindType();
            if (t == null) { note = TypeName + " not found"; return false; }
            object inst = FindInstance(t);
            MethodInfo m = null;
            foreach (MethodInfo mi in t.GetMethods(All))
            {
                if (mi.Name != "CoverageCheck" || mi.ContainsGenericParameters) continue;
                if (m == null || mi.GetParameters().Length < m.GetParameters().Length) m = mi;
            }
            if (m == null) { note = "PathGuide.CoverageCheck not found"; return false; }
            if (!m.IsStatic && inst == null) { note = "PathGuide instance not found"; return false; }

            ParameterInfo[] ps = m.GetParameters();
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                if (pt.IsByRef) pt = pt.GetElementType();
                if (!ps[i].IsOut && ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else args[i] = pt != null && pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            object ret;
            try { ret = m.Invoke(m.IsStatic ? null : inst, args); }
            catch (TargetInvocationException e) { note = "CoverageCheck threw: " + (e.InnerException != null ? e.InnerException.Message : e.Message); return false; }

            string sig = m.Name + "(" + ps.Length + " params) -> " + m.ReturnType.Name;
            // 1. an int-like return value is the miss count
            if (m.ReturnType != typeof(bool) && TryAsLong(ret, out long r)) { misses = (int)r; note = sig + " = " + r; return true; }
            // 2. an out/ref parameter whose name contains "miss"
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].ParameterType.IsByRef && ps[i].Name != null && ps[i].Name.IndexOf("miss", StringComparison.OrdinalIgnoreCase) >= 0 && TryAsLong(args[i], out long om))
                { misses = (int)om; note = sig + ", " + ps[i].Name + " = " + om; return true; }
            // 3. a result object with a member named like "misses"
            if (ret != null && !(ret is bool))
            {
                Type rt = ret.GetType();
                foreach (MemberInfo mem in rt.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (mem.Name.IndexOf("miss", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (TryReadNumber(rt, ret, mem.Name, out long mv)) { misses = (int)mv; note = sig + ", " + mem.Name + " = " + mv + " (" + ret + ")"; return true; }
                }
            }
            // 4. a bool result: true = full coverage
            if (ret is bool ok)
            {
                for (int i = 0; i < ps.Length; i++)
                    if (ps[i].ParameterType.IsByRef && TryAsLong(args[i], out long anyOut)) { misses = (int)anyOut; note = sig + " = " + ok + ", out " + ps[i].Name + " = " + anyOut; return true; }
                misses = ok ? 0 : 1;
                note = sig + " = " + ok + " (no miss count exposed)";
                return true;
            }
            note = sig + " returned an unrecognised value: " + (ret != null ? ret.ToString() : "null");
            return false;
        }

        static bool TryReadNumber(Type t, object inst, string name, out long v)
        {
            v = 0;
            PropertyInfo p = t.GetProperty(name, All);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                MethodInfo g = p.GetGetMethod(true);
                if (g != null && (g.IsStatic || inst != null)) return TryAsLong(p.GetValue(g.IsStatic ? null : inst), out v);
            }
            FieldInfo f = t.GetField(name, All);
            if (f != null && (f.IsStatic || inst != null)) return TryAsLong(f.GetValue(f.IsStatic ? null : inst), out v);
            return false;
        }

        static bool TryAsLong(object o, out long v)
        {
            v = 0;
            if (o == null) return false;
            switch (o)
            {
                case int i: v = i; return true;
                case long l: v = l; return true;
                case short sh: v = sh; return true;
                case uint ui: v = ui; return true;
                case ulong ul: v = (long)ul; return true;
                case byte b: v = b; return true;
                default: return false;
            }
        }
    }

    /// <summary>Driving the player for the bots (zero allocation per call).</summary>
    public static class QaDrive
    {
        /// <summary>Path heading at s in degrees (world yaw from +Z toward +X).</summary>
        public static float HeadingDeg(double s) => (float)(ThreadMath.Heading(s) / ThreadMath.Deg);

        public static void LookForward(double s, float pitch)
        {
            ICameraControl c = ThreadServices.CameraControl;
            if (c != null) c.SetLook(HeadingDeg(s), pitch);
        }

        /// <summary>Common run setup: background running, frame pacing, virtual time.</summary>
        public static void Prepare(bool virtualTime, bool vSync)
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = virtualTime ? 1f / VirtualFps : 0f;
        }

        /// <summary>Virtual frame rate of the deterministic autotest (Time.captureDeltaTime = 1/60).</summary>
        public const int VirtualFps = 60;

        /// <summary>Max and min |grade| of the floor over [s - back, s] (camera-smoothing allowance).</summary>
        public static void GradeRangeBehind(double s, double back, out double gMin, out double gMax)
        {
            gMin = double.MaxValue; gMax = 0;
            double lo = Math.Max(0, s - back);
            for (double x = lo; x <= s + 1e-9; x += 0.25)
            {
                double g = Math.Abs(ThreadMath.PathGrade(x));
                if (g > gMax) gMax = g;
                if (g < gMin) gMin = g;
            }
            if (gMin == double.MaxValue) gMin = 0;
        }

        public static string Devices()
        {
            return "keyboard=" + (Keyboard.current != null ? "yes" : "no")
                 + " mouse=" + (Mouse.current != null ? "yes" : "no")
                 + " gamepad=" + (Gamepad.current != null ? "yes" : "no");
        }
    }

    /// <summary>Named assertions of one run.</summary>
    public sealed class QaChecks
    {
        struct Check { public string name; public bool ok; public bool required; public string detail; }
        readonly List<Check> m_checks = new List<Check>(32);

        public bool AllRequiredOk
        {
            get
            {
                for (int i = 0; i < m_checks.Count; i++) if (m_checks[i].required && !m_checks[i].ok) return false;
                return true;
            }
        }

        public void Add(string name, bool ok, string detail, bool required = true)
        {
            m_checks.Add(new Check { name = name, ok = ok, required = required, detail = detail });
        }

        public string FailedNames()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m_checks.Count; i++)
            {
                if (m_checks[i].ok || !m_checks[i].required) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(m_checks[i].name).Append(" (").Append(m_checks[i].detail).Append(')');
            }
            return sb.ToString();
        }

        public void Write(QaJson j)
        {
            j.Key("checks").BeginArray();
            for (int i = 0; i < m_checks.Count; i++)
            {
                j.BeginObject(true);
                j.Prop("name", m_checks[i].name).Prop("ok", m_checks[i].ok).Prop("required", m_checks[i].required).Prop("detail", m_checks[i].detail);
                j.EndObject();
            }
            j.EndArray();
        }
    }
}
