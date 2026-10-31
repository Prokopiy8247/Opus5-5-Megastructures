using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Batch entry points of the reproducible project setup (Part 7.1, Part 12). Every method logs lines prefixed
    /// "[BATCH]" and, in batch mode, ends with EditorApplication.Exit(code) (0 = success). Run them through
    /// Tools/unity-queue.ps1, e.g. -executeMethod Megastructure.EditorTools.BatchTools.RebuildAll.
    /// </summary>
    public static class BatchTools
    {
        public const string GameRoot = "Assets/_Game";
        public const string SettingsDir = GameRoot + "/Settings";
        public const string MaterialsDir = GameRoot + "/Materials";
        public const string ScenesDir = GameRoot + "/Scenes";
        public const string ShadersDir = GameRoot + "/Shaders";
        public const string ShaderIncludeDir = ShadersDir + "/Include";

        /// <summary>The single gameplay scene (Part 7.7).</summary>
        public const string ScenePath = ScenesDir + "/Main.unity";

        /// <summary>Product name; the player is Builds/&lt;ProductName&gt;/&lt;ProductName&gt;.exe (Part 12).</summary>
        public const string ProductName = "The Ring";
        public const string CompanyName = "Megastructures";

        /// <summary>Absolute project root (the folder that contains Assets/).</summary>
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>Absolute path of the Windows player executable.</summary>
        public static string BuildPath => Path.GetFullPath(Path.Combine(ProjectRoot, "Builds", ProductName, ProductName + ".exe"));
        public static string BuildPathMac => Path.GetFullPath(Path.Combine(ProjectRoot, "Builds", "Mac", ProductName + ".app"));

        // ------------------------------------------------------------------------------------------------------
        // 1. RebuildAll: settings, URP assets, volume, materials, UI, RingAssets, scene, cleanup (idempotent)
        // ------------------------------------------------------------------------------------------------------

        [MenuItem("Tools/Megastructure/Rebuild All")]
        public static void RebuildAll()
        {
            BatchLog.Reset();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int code = 0;
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    BatchLog.Info("RebuildAll cancelled by the user (unsaved scene changes kept)");
                    return;
                }
                BatchLog.Info("RebuildAll start (Unity " + Application.unityVersion + ", project " + ProjectRoot + ")");
                EditorIO.EnsureFolder(SettingsDir);
                EditorIO.EnsureFolder(MaterialsDir);
                EditorIO.EnsureFolder(ScenesDir);
                EditorIO.EnsureFolder(ShaderIncludeDir);

                Step("a generated HLSL", ProjectSetup.WriteGeneratedHlsl);
                Step("b player settings", ProjectSetup.ApplyPlayerSettings);

                UrpSetup.PipelineAssets urp = default;
                Step("c URP pipeline", () => { urp = UrpSetup.CreateOrUpdatePipeline(); });
                if (urp.Asset == null) throw new InvalidOperationException("URP pipeline asset could not be created");
                Step("c quality + graphics", () => ProjectSetup.ConfigureQualityAndGraphics(urp.Asset));

                UnityEngine.Rendering.VolumeProfile profile = null;
                Step("d volume profile", () => { profile = UrpSetup.CreateOrUpdateVolumeProfile(); });

                MaterialFactory.Result mats = null;
                Step("e materials", () => { mats = MaterialFactory.BuildAll(); });
                Step("c final dither feature", () => UrpSetup.ConfigureFinalDither(urp.Renderer));

                UnityEngine.UIElements.PanelSettings panel = null;
                Step("f UI panel + theme", () => { panel = ProjectSetup.CreateOrUpdateUi(); });
                Step("f always included shaders", ProjectSetup.UpdateAlwaysIncludedShaders);

                RingAssets ringAssets = null;
                Step("g RingAssets", () => { ringAssets = MaterialFactory.WriteRingAssets(mats, panel); });

                Step("h main scene", () => SceneFactory.BuildMainScene(ringAssets, panel, profile));
                Step("h build scene list", ProjectSetup.SetBuildScenes);

                Step("i template leftovers", () => ProjectSetup.DeleteTemplateLeftovers(urp.Asset));
                Step("i default volume profile", UrpSetup.NeutralizeDefaultVolumeProfile);

                AssetDatabase.SaveAssets();
                BatchLog.PrintSummary("RebuildAll");
            }
            catch (Exception e)
            {
                BatchLog.Exception("RebuildAll", e);
                try { AssetDatabase.SaveAssets(); } catch (Exception e2) { BatchLog.Exception("RebuildAll SaveAssets", e2); }
            }
            if (BatchLog.Errors > 0) code = 1;
            BatchLog.Info("RebuildAll took " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s, " + BatchLog.Warnings + " warnings, " + BatchLog.Errors + " errors");
            BatchLog.Finish("RebuildAll", code);
        }

        static void Step(string name, Action action)
        {
            BatchLog.Info("step " + name);
            action();
        }

        // ------------------------------------------------------------------------------------------------------
        // 2. Compile + shader gate
        // ------------------------------------------------------------------------------------------------------

        public static void CompileAndShaderGate()
        {
            BatchLog.Reset();
            int code;
            try { code = ShaderGate.Run(); }
            catch (Exception e) { BatchLog.Exception("CompileAndShaderGate", e); code = 1; }
            if (BatchLog.Errors > 0) code = 1;
            BatchLog.Finish("CompileAndShaderGate", code);
        }

        // ------------------------------------------------------------------------------------------------------
        // 3. Windows player build
        // ------------------------------------------------------------------------------------------------------

        [MenuItem("Tools/Megastructure/Build Windows")]
        public static void BuildWindows()
        {
            BatchLog.Reset();
            int code;
            try { code = BuildTool.BuildWindowsPlayer(); }
            catch (Exception e) { BatchLog.Exception("BuildWindows", e); code = 1; }
            BatchLog.Finish("BuildWindows", code);
        }

        [MenuItem("Tools/Megastructure/Build macOS")]
        public static void BuildMac()
        {
            BatchLog.Reset();
            int code;
            try { code = BuildTool.BuildMacPlayer(); }
            catch (Exception e) { BatchLog.Exception("BuildMac", e); code = 1; }
            BatchLog.Finish("BuildMac", code);
        }

        // ------------------------------------------------------------------------------------------------------
        // 4. EditMode tests (asynchronous: launch WITHOUT -quit; the Editor exits from the RunFinished callback)
        // ------------------------------------------------------------------------------------------------------

        public static void RunEditModeTests()
        {
            BatchLog.Reset();
            try { TestRunnerBatch.Start(); }
            catch (Exception e)
            {
                BatchLog.Exception("RunEditModeTests", e);
                TestRunnerBatch.WriteFailureSummary("exception while starting the test run: " + e.Message);
                BatchLog.Finish("RunEditModeTests", TestRunnerBatch.ExitRunError);
            }
        }
    }
}
