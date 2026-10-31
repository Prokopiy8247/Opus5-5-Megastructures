using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Output locations of the runtime QA (docs/QA_RUNTIME.md). Every QA output goes under the directory given on the command
    /// line or under the project's <c>QA/</c> folder. The project root is found from <see cref="Application.dataPath"/>:
    /// in the Editor it is the parent of <c>Assets</c>; in the player (<c>Builds/Abyss/Abyss_Data</c>) the first ancestor that
    /// contains both <c>Assets</c> and <c>ProjectSettings</c>, else the folder of the executable. Relative paths given on the
    /// command line resolve against that root (not the working directory, which differs between launchers).
    /// QA-only code: allocates freely, never runs without a QA flag.
    /// </summary>
    public static class QaPaths
    {
        static string s_root;

        /// <summary>Project root (or the executable folder when the player runs outside the project).</summary>
        public static string ProjectRoot
        {
            get
            {
                if (s_root != null) return s_root;
                string data = Application.dataPath;
                string dir = Path.GetDirectoryName(data) ?? ".";
#if UNITY_EDITOR
                s_root = dir;
#else
                string probe = dir;
                s_root = dir;
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(probe); i++)
                {
                    if (Directory.Exists(Path.Combine(probe, "ProjectSettings")) && Directory.Exists(Path.Combine(probe, "Assets")))
                    {
                        s_root = probe;
                        break;
                    }
                    probe = Path.GetDirectoryName(probe);
                }
#endif
                return s_root;
            }
        }

        /// <summary>The project's QA folder.</summary>
        public static string QaDir => Path.Combine(ProjectRoot, "QA");

        /// <summary>Output folder for JSON reports of autotest, bots, soak and perf (-qa-out DIR, default QA/).</summary>
        public static string OutDir
        {
            get
            {
                string o = CliArgs.String(QaHarness.QaOutFlag, null);
                return string.IsNullOrEmpty(o) ? QaDir : Resolve(o);
            }
        }

        /// <summary>Absolute path: rooted paths stay, relative paths resolve against the project root.</summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return ProjectRoot;
            path = path.Trim().Trim('"');
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        /// <summary>Create the directory (and parents) and return it.</summary>
        public static string EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Create the parent directory of a file path and return the path.</summary>
        public static string EnsureParent(string file)
        {
            string d = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
            return file;
        }

        /// <summary>Local timestamp for run ids (yyyyMMdd-HHmmss).</summary>
        public static string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        /// <summary>UTC ISO-8601 time for reports.</summary>
        public static string IsoNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        /// <summary>"player" or "editor".</summary>
        public static string BuildKind => Application.isEditor ? "editor" : "player";
    }
}
