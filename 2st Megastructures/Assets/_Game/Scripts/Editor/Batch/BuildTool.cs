using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>Player builds (Part 12): Windows Builds/The Ring/The Ring.exe (StandaloneWindows64) and macOS
    /// Builds/Mac/The Ring.app (StandaloneOSX, Intel + Apple silicon, Mono), release (BuildOptions.None).</summary>
    public static class BuildTool
    {
        const int MaxWarningsLogged = 40;

        /// <summary>Builds the Windows player and returns the exit code (0 only when BuildResult.Succeeded).</summary>
        public static int BuildWindowsPlayer() => BuildPlayer(BuildTarget.StandaloneWindows64, BatchTools.BuildPath);

        /// <summary>Builds the macOS player (universal: x64 + ARM64) for the public download; not used by the QA runs.</summary>
        public static int BuildMacPlayer()
        {
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture", "x64ARM64");
            return BuildPlayer(BuildTarget.StandaloneOSX, BatchTools.BuildPathMac);
        }

        static int BuildPlayer(BuildTarget target, string exe)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                BatchLog.Error("BUILD aborted: script compilation failed");
                return 1;
            }
            if (!File.Exists(EditorIO.Abs(BatchTools.ScenePath)))
            {
                BatchLog.Error("BUILD aborted: " + BatchTools.ScenePath + " does not exist (run RebuildAll first)");
                return 1;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(exe));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BatchTools.ScenePath },
                locationPathName = exe,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BatchLog.Info("BUILD start: " + exe + " (" + target + ", graphics " + string.Join(",", PlayerSettings.GetGraphicsAPIs(target))
                          + ", backend " + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone) + ")");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                BatchLog.Error("BUILD failed: BuildPipeline.BuildPlayer returned no report");
                return 1;
            }
            var s = report.summary;
            BatchLog.Info("BUILD result=" + s.result + " totalErrors=" + s.totalErrors + " totalWarnings=" + s.totalWarnings
                          + " size=" + s.totalSize + " bytes (" + (s.totalSize / (1024.0 * 1024.0)).ToString("0.0") + " MB)"
                          + " duration=" + s.totalTime.TotalSeconds.ToString("0.0") + " s output=" + s.outputPath);

            int warningsLogged = 0;
            if (report.steps != null)
            {
                foreach (var step in report.steps)
                {
                    if (step.messages == null) continue;
                    foreach (var m in step.messages)
                    {
                        if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert)
                            BatchLog.Info("BUILD ERROR [" + step.name + "] " + m.content);
                        else if (m.type == LogType.Warning && warningsLogged < MaxWarningsLogged)
                        {
                            warningsLogged++;
                            BatchLog.Info("BUILD WARNING [" + step.name + "] " + m.content);
                        }
                    }
                }
            }
            bool ok = s.result == BuildResult.Succeeded;
            if (ok && !File.Exists(exe) && !Directory.Exists(exe)) { BatchLog.Error("BUILD reported success but " + exe + " is missing"); ok = false; }
            return ok ? 0 : 1;
        }
    }
}
