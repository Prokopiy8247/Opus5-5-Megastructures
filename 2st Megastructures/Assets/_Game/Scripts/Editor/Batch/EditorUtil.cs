using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Batch logging. Every line is prefixed "[BATCH]" so the lead can grep the Unity log. Errors are counted:
    /// a batch entry point exits with a non-zero code when any error was logged.
    /// </summary>
    internal static class BatchLog
    {
        public static int Errors { get; private set; }
        public static int Warnings { get; private set; }
        static readonly List<string> s_Summary = new List<string>();

        public static void Reset() { Errors = 0; Warnings = 0; s_Summary.Clear(); }

        public static void Info(string msg) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", "[BATCH] " + msg);

        public static void Warn(string msg)
        {
            Warnings++;
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", "[BATCH] WARN " + msg);
        }

        public static void Error(string msg)
        {
            Errors++;
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", "[BATCH] ERROR " + msg);
        }

        public static void Exception(string where, Exception e)
        {
            Errors++;
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", "[BATCH] EXCEPTION in " + where + ": " + e);
        }

        /// <summary>Remembers one line for the final summary (created / updated assets and key decisions).</summary>
        public static void Note(string msg) { s_Summary.Add(msg); Info(msg); }

        public static void PrintSummary(string title)
        {
            Info("---- " + title + " summary (" + s_Summary.Count + " items, " + Warnings + " warnings, " + Errors + " errors) ----");
            foreach (var s in s_Summary) Info("  * " + s);
        }

        /// <summary>Ends a batch entry point: logs the exit code and exits the Editor in batch mode only.</summary>
        public static void Finish(string step, int code)
        {
            Info(step + " finished: exit code " + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }

    /// <summary>File and asset helpers for the build scripts (paths are project-relative "Assets/..." paths).</summary>
    internal static class EditorIO
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string Abs(string projectRelative) => Path.GetFullPath(Path.Combine(BatchTools.ProjectRoot, projectRelative));

        /// <summary>Creates an "Assets/a/b/c" folder chain through the AssetDatabase.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            assetFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            int slash = assetFolder.LastIndexOf('/');
            if (slash <= 0) throw new ArgumentException("not an asset folder: " + assetFolder);
            string parent = assetFolder.Substring(0, slash);
            string leaf = assetFolder.Substring(slash + 1);
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, leaf);
            if (string.IsNullOrEmpty(guid)) throw new IOException("could not create folder " + assetFolder);
        }

        /// <summary>Writes a UTF-8 (no BOM, LF) text file only when its content changed. Returns true when written.</summary>
        public static bool WriteTextIfChanged(string projectRelative, string text)
        {
            text = text.Replace("\r\n", "\n");
            string abs = Abs(projectRelative);
            if (File.Exists(abs))
            {
                string old = File.ReadAllText(abs).Replace("\r\n", "\n");
                if (string.Equals(old, text, StringComparison.Ordinal)) return false;
            }
            string dir = Path.GetDirectoryName(abs);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(abs, text, Utf8NoBom);
            return true;
        }

        /// <summary>Loads the asset of type T at path or creates it there (keeps the GUID stable when it exists).</summary>
        public static T LoadOrCreateAsset<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            var other = AssetDatabase.LoadMainAssetAtPath(path);
            if (other != null) BatchLog.Warn("replacing " + other.GetType().Name + " at " + path + " with a new " + typeof(T).Name);
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            BatchLog.Note("created " + path);
            return asset;
        }

        /// <summary>Deletes an asset or folder if it exists. Returns true when something was deleted.</summary>
        public static bool DeleteIfExists(string path)
        {
            bool exists = AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(Abs(path));
            if (!exists) return false;
            if (AssetDatabase.DeleteAsset(path)) { BatchLog.Note("deleted " + path); return true; }
            BatchLog.Warn("could not delete " + path);
            return false;
        }
    }

    /// <summary>SerializedProperty helpers that log (instead of throwing) when a field does not exist.</summary>
    internal static class SerializedUtil
    {
        public static SerializedProperty Find(SerializedObject so, string name, string ctx)
        {
            var p = so.FindProperty(name);
            if (p == null) BatchLog.Warn(ctx + ": serialized field '" + name + "' not found");
            return p;
        }

        public static SerializedProperty FindRel(SerializedProperty parent, string name, string ctx)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) BatchLog.Warn(ctx + ": serialized field '" + name + "' not found");
            return p;
        }

        public static void SetInt(SerializedProperty p, int v)
        {
            if (p == null) return;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: p.boolValue = v != 0; break;
                case SerializedPropertyType.Float: p.floatValue = v; break;
                default: p.intValue = v; break;   // Integer, Enum (raw value), LayerMask, ...
            }
        }

        public static void SetBool(SerializedProperty p, bool v)
        {
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v;
            else p.intValue = v ? 1 : 0;
        }

        public static void SetFloat(SerializedProperty p, float v)
        {
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
            else p.intValue = Mathf.RoundToInt(v);
        }

        public static void SetObject(SerializedProperty p, Object v)
        {
            if (p == null) return;
            p.objectReferenceValue = v;
        }

        /// <summary>Removes element i of an array property (object references are nulled first, which older
        /// Unity versions required before an element is really removed).</summary>
        public static void RemoveAt(SerializedProperty array, int i)
        {
            int before = array.arraySize;
            var el = array.GetArrayElementAtIndex(i);
            if (el.propertyType == SerializedPropertyType.ObjectReference) el.objectReferenceValue = null;
            array.DeleteArrayElementAtIndex(i);
            if (array.arraySize == before) array.DeleteArrayElementAtIndex(i);
        }
    }
}
