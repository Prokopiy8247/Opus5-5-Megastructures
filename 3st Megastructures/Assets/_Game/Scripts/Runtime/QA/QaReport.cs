// QaReport: report plumbing of the runtime QA harness (MASTER_PROMPT 11.2, 11.8, 14.10).
// QaPaths resolves where reports and captures go (project root in the Editor, -report-dir or
// Application.persistentDataPath in the player); QaFiles writes UTF-8 without BOM; QaEnvironment writes
// the environment block every report carries. The JSON builder itself is QaJson (QaJson.cs).
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Megastructure.QA
{
    /// <summary>Where QA output goes.</summary>
    public static class QaPaths
    {
        /// <summary>Project root in the Editor (parent of Assets), null in the player.</summary>
        public static string ProjectRoot
        {
            get
            {
                if (!Application.isEditor) return null;
                try { return Directory.GetParent(Application.dataPath).FullName; } catch { return null; }
            }
        }

        /// <summary>Absolute path: rooted paths stay; relative ones resolve against the project root (Editor) or the working directory (player).</summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            string root = ProjectRoot;
            return Path.GetFullPath(root != null ? Path.Combine(root, path) : path);
        }

        /// <summary>Report folder: -report-dir if given, else QA/reports in the Editor, else persistentDataPath.</summary>
        public static string ReportDir
        {
            get
            {
                string d = CliArgs.Get("-report-dir");
                if (!string.IsNullOrEmpty(d)) return Resolve(d);
                string root = ProjectRoot;
                if (root != null) return Path.Combine(root, "QA", "reports");
                return Application.persistentDataPath;
            }
        }

        public static string ReportPath(string fileName) => Path.Combine(ReportDir, fileName);
    }

    public static class QaFiles
    {
        static readonly UTF8Encoding s_utf8 = new UTF8Encoding(false);

        public static bool WriteText(string path, string text)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, text, s_utf8);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[THREAD] QA could not write " + path + ": " + e.Message);
                return false;
            }
        }

        public static string IsoNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    /// <summary>Environment block shared by every report (proves the run happened in the player, which API, which settings).</summary>
    public static class QaEnvironment
    {
        public static void Write(QaJson j)
        {
            j.Key("env").BeginObject();
            j.Prop("unity", Application.unityVersion);
            j.Prop("isEditor", Application.isEditor);
            j.Prop("batchMode", Application.isBatchMode);
            j.Prop("platform", Application.platform.ToString());
            j.Prop("graphicsApi", SystemInfo.graphicsDeviceType.ToString());
            j.Prop("gpu", SystemInfo.graphicsDeviceName);
            j.Prop("gpuMemoryMB", SystemInfo.graphicsMemorySize);
            j.Prop("cpu", SystemInfo.processorType);
            j.Prop("reversedZ", SystemInfo.usesReversedZBuffer);
            j.Prop("screen", Screen.width + "x" + Screen.height);
            j.Prop("vSyncCount", QualitySettings.vSyncCount);
            j.Prop("seed", CliArgs.Seed);
            j.Prop("freezeDither", CliArgs.FreezeDither);
            j.Prop("freezeAmbient", CliArgs.FreezeAmbient);
            j.Prop("args", string.Join(" ", CliArgs.Args));
            j.EndObject();
        }
    }
}
