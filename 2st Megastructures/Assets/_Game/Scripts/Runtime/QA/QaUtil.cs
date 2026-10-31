using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// QA helpers shared by the capture harness, the autotest, the wall test and the perf monitor:
    /// project-root path resolution, quitting with an exit code (player and editor), and reflection into
    /// modules owned by other builders (so the QA folder has no compile dependency on them).
    /// </summary>
    public static class QaUtil
    {
        static string _root;

        /// <summary>The project root: the current directory when it is the project (contains MASTER_PROMPT.md or
        /// Assets + ProjectSettings), else the first such parent of Application.dataPath (the player lives in
        /// Builds/&lt;Product&gt;/&lt;Product&gt;_Data), else the current directory.</summary>
        public static string ProjectRoot()
        {
            if (_root != null) return _root;
            var cands = new List<string>();
            try { cands.Add(Environment.CurrentDirectory); } catch (Exception) { }
            try
            {
                var d = new DirectoryInfo(Application.dataPath);
                for (int i = 0; i < 6 && d != null; i++) { cands.Add(d.FullName); d = d.Parent; }
            }
            catch (Exception) { }
            foreach (var c in cands)
            {
                try
                {
                    if (File.Exists(Path.Combine(c, "MASTER_PROMPT.md")) ||
                        (Directory.Exists(Path.Combine(c, "Assets")) && Directory.Exists(Path.Combine(c, "ProjectSettings"))))
                        return _root = c;
                }
                catch (Exception) { }
            }
            return _root = cands.Count > 0 ? cands[0] : Application.persistentDataPath;
        }

        /// <summary>Absolute path: rooted paths are kept, relative paths are taken relative to the project root.</summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return ProjectRoot();
            return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(ProjectRoot(), path));
        }

        public static void EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        /// <summary>Writes a text file (UTF-8, no BOM), creating its folder.</summary>
        public static void WriteText(string path, string text)
        {
            EnsureDir(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        // quit holds: a QA behaviour that must keep recording after another one finished (the boot capture records the
        // last frames after the autotest's ending) holds the quit; a Quit meanwhile is deferred, and the process exits
        // with the largest code reported by anyone once the last hold is released
        static int _holds, _deferred = -1, _floor = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetQuitHolds() { _holds = 0; _deferred = -1; _floor = -1; }

        /// <summary>Defers every <see cref="Quit"/> until the matching <see cref="ReleaseQuit"/>.</summary>
        public static void HoldQuit() { _holds++; }

        /// <summary>True when a Quit arrived while a hold was active (it runs at the last release).</summary>
        public static bool QuitDeferred => _deferred >= 0;

        /// <summary>Releases a hold and contributes <paramref name="code"/> to the final exit code. Quits at once when a
        /// Quit was deferred meanwhile, or when <paramref name="quitNow"/> is set and nothing else holds; otherwise the
        /// next Quit (by another QA behaviour) exits with the larger code.</summary>
        public static void ReleaseQuit(int code, bool quitNow)
        {
            if (_holds > 0) _holds--;
            _floor = Math.Max(_floor, code);
            if (_holds > 0) return;
            if (_deferred >= 0) Quit(_deferred);
            else if (quitNow) Quit(code);
        }

        /// <summary>Quits with an exit code: Application.Quit(code) in the player; EditorApplication.Exit in an
        /// Editor batchmode run (Application.Quit is ignored in the Editor); stops play mode in an interactive Editor.
        /// The code is the largest of this one and those contributed through <see cref="ReleaseQuit"/>; while a hold is
        /// active the quit is deferred.</summary>
        public static void Quit(int code)
        {
            code = Math.Max(code, _floor);
            if (_holds > 0)
            {
                _deferred = Math.Max(_deferred, code);
                Debug.Log("[Ring] QA exit code " + code + " deferred (another QA behaviour is still recording)");
                return;
            }
            Debug.Log("[Ring] QA exit code " + code);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        public static string Stamp() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        public static string F(float v, string fmt = "0.###") => v.ToString(fmt, CultureInfo.InvariantCulture);

        // ---------------- reflection into other modules ----------------
        static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

        /// <summary>Finds a type by simple name in the loaded assemblies, preferring the Megastructure namespaces.
        /// Returns null when absent (the module does not exist yet).</summary>
        public static Type FindType(string simpleName)
        {
            if (_types.TryGetValue(simpleName, out var cached)) return cached;
            Type best = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name;
                if (!an.StartsWith("Megastructure") && !an.StartsWith("Assembly-CSharp")) continue;
                Type[] ts;
                try { ts = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { ts = e.Types; }
                if (ts == null) continue;
                foreach (var t in ts)
                {
                    if (t == null || t.Name != simpleName) continue;
                    if (best == null || (t.Namespace != null && t.Namespace.StartsWith("Megastructure"))) best = t;
                }
            }
            _types[simpleName] = best;
            return best;
        }

        /// <summary>The first loaded component of the named type (inactive included), or null.</summary>
        public static Component FindComponent(string typeName)
        {
            var t = FindType(typeName);
            if (t == null || !typeof(Component).IsAssignableFrom(t)) return null;
            return UnityEngine.Object.FindAnyObjectByType(t, FindObjectsInactive.Include) as Component;
        }

        public const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        public const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static MethodInfo FindMethod(Type t, string name, BindingFlags flags, params Type[] args)
        {
            if (t == null) return null;
            try { return t.GetMethod(name, flags, null, args, null); }
            catch (AmbiguousMatchException) { return null; }
        }

        /// <summary>Reads a static int/float member whose name contains one of the fragments (case-insensitive).</summary>
        public static bool TryReadStaticNumber(Type t, string[] fragments, out double value, out string member)
        {
            value = 0; member = null;
            if (t == null) return false;
            foreach (var f in t.GetFields(AnyStatic))
                if (IsNumber(f.FieldType) && Matches(f.Name, fragments)) { value = Convert.ToDouble(f.GetValue(null), CultureInfo.InvariantCulture); member = t.Name + "." + f.Name; return true; }
            foreach (var p in t.GetProperties(AnyStatic))
                if (p.CanRead && p.GetIndexParameters().Length == 0 && IsNumber(p.PropertyType) && Matches(p.Name, fragments))
                { value = Convert.ToDouble(p.GetValue(null), CultureInfo.InvariantCulture); member = t.Name + "." + p.Name; return true; }
            return false;
        }

        static bool IsNumber(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double);

        static bool Matches(string name, string[] fragments)
        {
            foreach (var f in fragments) if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Sets a bool/float/int field or property by name on an object if it exists. Returns true if set.</summary>
        public static bool TrySet(object target, string name, object value)
        {
            if (target == null) return false;
            var t = target.GetType();
            var f = t.GetField(name, AnyInstance);
            try
            {
                if (f != null && f.FieldType.IsInstanceOfType(value)) { f.SetValue(target, value); return true; }
                var p = t.GetProperty(name, AnyInstance);
                if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(value)) { p.SetValue(target, value); return true; }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Disables every loaded Behaviour of the named type (used by the wall test to take over the
        /// CharacterController from the PlayerController and the AutoWalker). Returns the number disabled.</summary>
        public static int DisableBehaviours(string typeName)
        {
            var t = FindType(typeName);
            if (t == null || !typeof(Behaviour).IsAssignableFrom(t)) return 0;
            int n = 0;
            foreach (var o in UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include))
                if (o is Behaviour b && b.enabled) { b.enabled = false; n++; }
            return n;
        }

        // ---------------- small maths ----------------
        /// <summary>Signed roll of a transform in degrees (euler z mapped to -180..180).</summary>
        public static float Roll(Transform t)
        {
            float z = t.eulerAngles.z;
            return z > 180f ? z - 360f : z;
        }

        /// <summary>Rec.709 luma of 8-bit sRGB values (the "luma" of Part 14.1 conventions).</summary>
        public static float Luma(byte r, byte g, byte b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    /// <summary>Minimal JSON writer for QA reports (no external dependencies). NaN and infinity are written as null.</summary>
    public sealed class QaJson
    {
        readonly StringBuilder _sb = new StringBuilder(1 << 14);
        readonly Stack<bool> _first = new Stack<bool>();
        bool _afterKey;

        void Sep()
        {
            if (_afterKey) { _afterKey = false; return; }
            if (_first.Count == 0) return;
            if (_first.Peek()) { _first.Pop(); _first.Push(false); }
            else _sb.Append(',');
        }

        public QaJson Obj() { Sep(); _sb.Append('{'); _first.Push(true); return this; }
        public QaJson EndObj() { _first.Pop(); _sb.Append('}'); return this; }
        public QaJson Arr() { Sep(); _sb.Append('['); _first.Push(true); return this; }
        public QaJson EndArr() { _first.Pop(); _sb.Append(']'); return this; }

        public QaJson Key(string k) { Sep(); Esc(k); _sb.Append(':'); _afterKey = true; return this; }

        public QaJson V(string s) { Sep(); if (s == null) _sb.Append("null"); else Esc(s); return this; }
        public QaJson V(bool b) { Sep(); _sb.Append(b ? "true" : "false"); return this; }
        public QaJson V(int i) { Sep(); _sb.Append(i.ToString(CultureInfo.InvariantCulture)); return this; }
        public QaJson V(long i) { Sep(); _sb.Append(i.ToString(CultureInfo.InvariantCulture)); return this; }
        public QaJson V(float f)
        {
            Sep();
            if (float.IsNaN(f) || float.IsInfinity(f)) _sb.Append("null");
            else _sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
            return this;
        }
        public QaJson V(double d)
        {
            Sep();
            if (double.IsNaN(d) || double.IsInfinity(d)) _sb.Append("null");
            else _sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
            return this;
        }
        public QaJson V(Vector3 v) { Arr(); V(v.x); V(v.y); V(v.z); return EndArr(); }
        public QaJson V(Vector2 v) { Arr(); V(v.x); V(v.y); return EndArr(); }
        public QaJson Raw(string json) { Sep(); _sb.Append(json); return this; }

        public QaJson KV(string k, string v) => Key(k).V(v);
        public QaJson KV(string k, bool v) => Key(k).V(v);
        public QaJson KV(string k, int v) => Key(k).V(v);
        public QaJson KV(string k, long v) => Key(k).V(v);
        public QaJson KV(string k, float v) => Key(k).V(v);
        public QaJson KV(string k, double v) => Key(k).V(v);
        public QaJson KV(string k, Vector3 v) => Key(k).V(v);

        void Esc(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        public override string ToString() => _sb.ToString();
    }
}
