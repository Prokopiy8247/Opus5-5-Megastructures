using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Batch and menu entry points (Part 7.1, Part 12). Every generated asset, setting and scene is reproduced by RebuildAll;
    /// no manual editor step is ever required.
    ///   Unity.exe -batchmode -projectPath ... -executeMethod Megastructure.EditorTools.BatchTools.RebuildAll -quit
    ///   Unity.exe -batchmode -projectPath ... -executeMethod Megastructure.EditorTools.BatchTools.BuildWindows -quit
    /// </summary>
    public static class BatchTools
    {
        public const string DefaultBuildFolder = "Builds/" + ProjectSetup.ProductName;

        /// <summary>Build output folder: Builds/Abyss, or the folder in the environment variable ABYSS_BUILD_DIR (look-dev agents
        /// build into their own folders so a capture never runs against an exe that another agent is replacing).</summary>
        public static string BuildFolder
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("ABYSS_BUILD_DIR");
                return string.IsNullOrEmpty(env) ? DefaultBuildFolder : env.Replace('\\', '/');
            }
        }

        public static string ExePath => Path.Combine(BuildFolder, ProjectSetup.ProductName + ".exe");

        [MenuItem("Tools/Megastructure/Rebuild All")]
        public static void RebuildAllMenu() => RebuildAllInternal();

        [MenuItem("Tools/Megastructure/Build Windows Player")]
        public static void BuildWindowsMenu() { RebuildAllInternal(); BuildInternal(); }

        /// <summary>Batch entry: regenerate settings, render pipeline, materials, textures and scenes.</summary>
        public static void RebuildAll() => RunBatch(() => RebuildAllInternal());

        /// <summary>Batch entry: RebuildAll, then build the Windows player into Builds/Abyss/Abyss.exe.</summary>
        public static void BuildWindows() => RunBatch(() => { RebuildAllInternal(); BuildInternal(); });

        /// <summary>Batch entry: build the player from the current assets without regenerating them.</summary>
        public static void BuildOnly() => RunBatch(() => BuildInternal());

        public const string MacBuildFolder = "Builds/Mac";

        /// <summary>Batch entry: RebuildAll, then build a universal (Intel + Apple silicon) macOS player into Builds/Mac/Abyss.app.</summary>
        public static void BuildMac() => RunBatch(() => { RebuildAllInternal(); BuildMacInternal(); });

        static void BuildMacInternal()
        {
            Directory.CreateDirectory(MacBuildFolder);
            SetMacUniversal();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new Exception("no scenes in build settings");
            string path = Path.Combine(MacBuildFolder, ProjectSetup.ProductName + ".app");
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[ABYSS] Mac build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalSeconds:F0} s -> {path}");
            if (s.result != BuildResult.Succeeded) throw new Exception("mac player build failed: " + s.result);
        }

        /// <summary>
        /// UserBuildSettings.architecture = x64ARM64 (universal binary), set by reflection because the type lives in the
        /// macOS platform module (UnityEditor.OSXStandalone.Extensions), which an editor assembly cannot reference directly.
        /// </summary>
        static void SetMacUniversal()
        {
            var t = Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
            var p = t?.GetProperty("architecture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (p == null) { Debug.LogWarning("[ABYSS] macOS module not found: building with the default architecture"); return; }
            p.SetValue(null, Enum.Parse(p.PropertyType, "x64ARM64"));
            Debug.Log("[ABYSS] macOS architecture: " + p.GetValue(null));
        }

        /// <summary>Batch entry: log compiler messages, pass list and support state of every Megastructure shader.</summary>
        public static void DumpShaders() => RunBatch(() =>
        {
            foreach (var info in ShaderUtil.GetAllShaderInfo())
            {
                if (!info.name.StartsWith("Megastructure/")) continue;
                var s = Shader.Find(info.name);
                var passes = new System.Text.StringBuilder();
                var tag = new UnityEngine.Rendering.ShaderTagId("LightMode");
                for (int ss = 0; ss < s.subshaderCount; ss++)
                {
                    passes.Append(" [sub").Append(ss).Append(':');
                    for (int p = 0; p < s.GetPassCountInSubshader(ss); p++) passes.Append(s.FindPassTagValue(ss, p, tag).name).Append(',');
                    passes.Append(']');
                }
                Debug.Log($"[ABYSS] shader {info.name}: supported={info.supported} hasErrors={info.hasErrors} subshaders={s.subshaderCount} passes={passes}");
                foreach (var m in ShaderUtil.GetShaderMessages(s))
                    Debug.Log($"[ABYSS] shader {info.name} {m.severity} {m.platform}: {m.message} ({m.file}:{m.line})");
            }
        });

        static void RunBatch(Action a)
        {
            int code = 0;
            try { a(); }
            catch (Exception e)
            {
                Debug.LogError("[ABYSS-ERROR] batch step failed: " + e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void RebuildAllInternal()
        {
            var t0 = DateTime.Now;
            ProjectSetup.Apply();
            MaterialBuilder.BuildAll();
            RenderSetup.Apply();
            SceneBuilder.BuildAll();
            CleanupTemplateLeftovers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ABYSS] RebuildAll OK in {(DateTime.Now - t0).TotalSeconds:F1} s");
        }

        static void BuildInternal()
        {
            Directory.CreateDirectory(BuildFolder);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new Exception("no scenes in build settings");
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[ABYSS] Build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalSeconds:F0} s -> {ExePath}");
            if (s.result != BuildResult.Succeeded) throw new Exception("player build failed: " + s.result);
        }

        /// <summary>Delete the URP template leftovers once our own settings exist (Part 7.2).</summary>
        static void CleanupTemplateLeftovers()
        {
            string[] leftovers =
            {
                "Assets/Scenes/SampleScene.unity", "Assets/Scenes", "Assets/TutorialInfo", "Assets/Readme.asset",
                "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/SampleSceneProfile.asset",
            };
            foreach (var p in leftovers)
            {
                if (AssetDatabase.IsValidFolder(p) || File.Exists(p)) AssetDatabase.DeleteAsset(p);
            }
        }
    }
}
