using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Counts Error / Exception / Assert log messages for the QA reports (Part 11.8, 11.10: zero errors in Player.log).
    /// Installed before the first scene loads, and only when a QA flag is present (<see cref="QaHarness.AnyQaFlag"/>),
    /// so errors raised in any Awake are counted. Thread-safe (logMessageReceivedThreaded). Keeps the first 24 messages.
    /// </summary>
    public static class QaLog
    {
        public const int MaxKept = 24;

        static readonly object s_lock = new object();
        static readonly List<string> s_first = new List<string>(MaxKept);
        static int s_errors, s_exceptions, s_asserts, s_warnings;
        static bool s_installed;

        public static int Errors => Volatile.Read(ref s_errors);
        public static int Exceptions => Volatile.Read(ref s_exceptions);
        public static int Asserts => Volatile.Read(ref s_asserts);
        public static int Warnings => Volatile.Read(ref s_warnings);

        /// <summary>Errors + exceptions + asserts.</summary>
        public static int Total => Errors + Exceptions + Asserts;

        public static bool Installed => s_installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (s_installed) Application.logMessageReceivedThreaded -= OnLog;
            s_installed = false;
            lock (s_lock) s_first.Clear();
            s_errors = s_exceptions = s_asserts = s_warnings = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInstall()
        {
            if (QaHarness.AnyQaFlag()) Install();
        }

        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            switch (type)
            {
                case LogType.Error: Interlocked.Increment(ref s_errors); break;
                case LogType.Exception: Interlocked.Increment(ref s_exceptions); break;
                case LogType.Assert: Interlocked.Increment(ref s_asserts); break;
                case LogType.Warning: Interlocked.Increment(ref s_warnings); return;
                default: return;
            }
            lock (s_lock)
            {
                if (s_first.Count >= MaxKept) return;
                string line = type + ": " + condition;
                if (!string.IsNullOrEmpty(stack))
                {
                    int nl = stack.IndexOf('\n');
                    line += " @ " + (nl > 0 ? stack.Substring(0, nl) : stack);
                }
                if (line.Length > 600) line = line.Substring(0, 600);
                s_first.Add(line);
            }
        }

        /// <summary>Write {"errors", "exceptions", "asserts", "warnings", "total", "first": [...]} under the given key.</summary>
        public static void Write(QaJson j, string key = "log")
        {
            j.Obj(key);
            j.Prop("installed", s_installed);
            j.Prop("errors", Errors);
            j.Prop("exceptions", Exceptions);
            j.Prop("asserts", Asserts);
            j.Prop("warnings", Warnings);
            j.Prop("total", Total);
            j.Arr("first");
            lock (s_lock)
            {
                for (int i = 0; i < s_first.Count; i++) j.Value(s_first[i]);
            }
            j.EndArray();
            j.EndObject();
        }
    }
}
