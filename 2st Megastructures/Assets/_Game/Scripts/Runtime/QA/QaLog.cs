using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Process-wide log listener for QA (Part 11.10): counts errors, exceptions and asserts from the very first frame
    /// (installed at SubsystemRegistration through Application.logMessageReceivedThreaded, so worker-thread logs count
    /// too), keeps the first messages for the reports, and records "[Ring]" lines that mention an activation, a set
    /// swap or a beat so frame sequences can be aligned with the BeatDirector activation log (14.6, 14.10).
    /// Thread-safe; the frame index comes from <see cref="MainFrame"/>, written on the main thread by the QA behaviours.
    /// </summary>
    public static class QaLog
    {
        public struct Entry { public int Frame; public LogType Type; public string Message; }

        static readonly object _lock = new object();
        static readonly List<Entry> _errors = new List<Entry>();
        static readonly List<Entry> _ring = new List<Entry>();
        static int _errorCount, _warningCount;
        static bool _installed;
        const int MaxKept = 64;
        const int MaxRing = 4096;

        /// <summary>Main-thread frame counter mirrored for worker threads (Time.frameCount is main-thread only).</summary>
        public static volatile int MainFrame;

        public static int ErrorCount { get { lock (_lock) return _errorCount; } }
        public static int WarningCount { get { lock (_lock) return _warningCount; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            lock (_lock) { _errors.Clear(); _ring.Clear(); _errorCount = 0; _warningCount = 0; }
            if (_installed) Application.logMessageReceivedThreaded -= OnLog;
            _installed = false;
            Install();
        }

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            int frame = MainFrame;
            lock (_lock)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    _errorCount++;
                    if (_errors.Count < MaxKept)
                    {
                        string m = message ?? "";
                        if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
                            m += " | " + (stackTrace.Length > 600 ? stackTrace.Substring(0, 600) : stackTrace);
                        _errors.Add(new Entry { Frame = frame, Type = type, Message = m });
                    }
                }
                else if (type == LogType.Warning) _warningCount++;

                if (message != null && message.StartsWith("[Ring]") && _ring.Count < MaxRing && IsActivation(message))
                    _ring.Add(new Entry { Frame = frame, Type = type, Message = message });
            }
        }

        static bool IsActivation(string m)
        {
            return m.IndexOf("activat", System.StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("swap", System.StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf(" set ", System.StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("beat", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static List<Entry> FirstErrors()
        {
            lock (_lock) return new List<Entry>(_errors);
        }

        /// <summary>Copies the "[Ring]" activation lines recorded since index <paramref name="from"/>; returns the new count.</summary>
        public static int RingEventsSince(int from, List<Entry> into)
        {
            lock (_lock)
            {
                for (int i = from; i < _ring.Count; i++) into.Add(_ring[i]);
                return _ring.Count;
            }
        }

        public static int RingEventCount { get { lock (_lock) return _ring.Count; } }

        /// <summary>Writes {"count", "warnings", "first": [...]} into a JSON writer.</summary>
        public static void WriteJson(QaJson j)
        {
            var errs = FirstErrors();
            j.Obj().KV("count", ErrorCount).KV("warnings", WarningCount).Key("first").Arr();
            foreach (var e in errs) j.Obj().KV("frame", e.Frame).KV("type", e.Type.ToString()).KV("message", e.Message).EndObj();
            j.EndArr().EndObj();
        }
    }
}
