// BatchTools: the -executeMethod entry points and the Tools/Megastructure menu (MASTER_PROMPT 7.1, Part 12).
//   RebuildAll       palette asset -> generated HLSL -> refresh -> materials + library -> URP pipeline -> project
//                    settings -> Main.unity -> build settings -> template cleanup -> save
//   BuildWindows     Builds/Thread/Thread.exe (StandaloneWindows64, release; -dev-build for a development build)
//   BuildMac         Builds/Mac/Thread.app (StandaloneOSX, Intel + Apple silicon, release; unsigned when built on Windows)
//   CompileCheck     a successful batch compile (exit 0) or 1 when scripts failed to compile
//   Validate         Main.unity contract, missing scripts / materials, shader errors (compiled per material) -> table
//   CaptureInEditor  opens Main.unity and enters play mode for runtime captures (no -quit; CaptureWatchdog exits)
// Every batch method logs "[THREAD] ..." lines and ends with EditorApplication.Exit(0/1) in batch mode.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    public static class BatchTools
    {
        public const string Menu = "Tools/Megastructure/";

        // ------------------------------------------------------------------------------------------------ RebuildAll
        [MenuItem(Menu + "Rebuild All", priority = 0)]
        public static void RebuildAll()
        {
            Run("RebuildAll", RebuildAllInternal);
        }

        /// <summary>The full regeneration; returns the problem list (empty = success).</summary>
        public static List<string> RebuildAllInternal()
        {
            var problems = new List<string>();
            ThreadPalette palette = null;
            MaterialLibrary lib = null;
            Step("PaletteWriter", problems, () => { palette = PaletteWriter.Write(); });
            Step("ShaderGen", problems, () => { ShaderGen.WriteAll(); });
            Step("AssetDatabase.Refresh", problems, () => { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); });
            Step("MaterialFactory", problems, () => { lib = MaterialFactory.Build(problems); });
            Step("PipelineConfigurator", problems, () => { PipelineConfigurator.Apply(lib != null ? lib.Grade : null, problems); });
            Step("ProjectConfigurator", problems, () => { ProjectConfigurator.Apply(problems); });
            Step("SceneBuilder", problems, () => { SceneBuilder.Build(lib, palette, problems); });
            Step("BuildSettings", problems, ApplyBuildSettings);
            Step("TemplateCleanup", problems, () => { TemplateCleanup.Run(problems); });
            Step("SaveAssets", problems, () => { AssetDatabase.SaveAssets(); });
            return problems;
        }

        public static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ThreadPaths.MainScene, true) };
            ThreadLog.Info("BuildSettings: scenes = [" + ThreadPaths.MainScene + "]");
        }

        [MenuItem(Menu + "Generate HLSL (palette + constants)", priority = 20)]
        public static void GenerateHlsl() => Run("GenerateHlsl", () => { var p = new List<string>(); ShaderGen.WriteAll(); return p; });

        [MenuItem(Menu + "Rebuild Materials", priority = 21)]
        public static void RebuildMaterials() => Run("RebuildMaterials", () => { var p = new List<string>(); PaletteWriter.Write(); MaterialFactory.Build(p); return p; });

        [MenuItem(Menu + "Rebuild Scene", priority = 22)]
        public static void RebuildScene() => Run("RebuildScene", () =>
        {
            var p = new List<string>();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ThreadPaths.LibraryAsset);
            var pal = AssetDatabase.LoadAssetAtPath<ThreadPalette>(ThreadPaths.PaletteAsset);
            SceneBuilder.Build(lib, pal, p);
            ApplyBuildSettings();
            return p;
        });

        // ------------------------------------------------------------------------------------------------ BuildWindows
        [MenuItem(Menu + "Build Windows Player", priority = 40)]
        public static void BuildWindows()
        {
            Run("BuildWindows", BuildWindowsInternal);
        }

        public static string PlayerExePath() =>
            Path.Combine(ThreadPaths.ProjectRoot, "Builds", ThreadPaths.ProductName, ThreadPaths.ProductName + ".exe");

        static List<string> BuildWindowsInternal()
        {
            var problems = new List<string>();
            if (!File.Exists(ThreadPaths.Abs(ThreadPaths.MainScene)))
            {
                ThreadLog.Problem(problems, "BuildWindows: " + ThreadPaths.MainScene + " missing (run RebuildAll first)");
                return problems;
            }
            if (CliArgs.Get(ProjectConfigurator.GraphicsApiArg) != null) ProjectConfigurator.ApplyGraphicsApi(problems);
            ApplyBuildSettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                ThreadLog.Info("BuildWindows: switching the active build target to StandaloneWindows64 (prefer -buildTarget win64)");
                EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, BuildTarget.StandaloneWindows64);
            }

            // Shader errors are logged, not fatal, in a player build (R8 pitfall 3): check the five shaders first.
            foreach (var name in ThreadPaths.ShaderNames)
            {
                var sh = MaterialFactory.FindShader(name);
                if (sh == null) ThreadLog.Problem(problems, "BuildWindows: shader " + name + " missing");
                else if (ShaderUtil.ShaderHasError(sh)) ThreadLog.Problem(problems, "BuildWindows: shader " + name + " has compile errors");
            }

            string exe = PlayerExePath();
            Directory.CreateDirectory(Path.GetDirectoryName(exe));
            bool dev = CliArgs.Has("-dev-build");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ThreadPaths.MainScene },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            ThreadLog.Info($"BuildWindows: building {(dev ? "DEVELOPMENT" : "release")} player -> {exe} (APIs {ProjectConfigurator.ApiList()})");
            var sw = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        ThreadLog.Info($"BuildWindows: [{step.name}] {m.type}: {m.content}");
            ThreadLog.Info($"BuildWindows: result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                           $"size={s.totalSize / (1024.0 * 1024.0):F1} MB time={s.totalTime.TotalSeconds:F1} s (wall {sw.Elapsed.TotalSeconds:F1} s) out={s.outputPath}");
            if (s.result != BuildResult.Succeeded) ThreadLog.Problem(problems, "BuildWindows: build result " + s.result);
            else if (!File.Exists(exe)) ThreadLog.Problem(problems, "BuildWindows: build reported success but " + exe + " does not exist");
            return problems;
        }

        // ------------------------------------------------------------------------------------------------ BuildMac
        [MenuItem(Menu + "Build macOS Player", priority = 41)]
        public static void BuildMac()
        {
            Run("BuildMac", BuildMacInternal);
        }

        public static string PlayerAppPath() =>
            Path.Combine(ThreadPaths.ProjectRoot, "Builds", "Mac", ThreadPaths.ProductName + ".app");

        static List<string> BuildMacInternal()
        {
            var problems = new List<string>();
            if (!File.Exists(ThreadPaths.Abs(ThreadPaths.MainScene)))
            {
                ThreadLog.Problem(problems, "BuildMac: " + ThreadPaths.MainScene + " missing (run RebuildAll first)");
                return problems;
            }
            ApplyBuildSettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, BuildTarget.StandaloneOSX);
            // one universal binary: Intel and Apple silicon Macs
            EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64ARM64");

            string app = PlayerAppPath();
            Directory.CreateDirectory(Path.GetDirectoryName(app));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ThreadPaths.MainScene },
                locationPathName = app,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            ThreadLog.Info("BuildMac: building release player -> " + app);
            var sw = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        ThreadLog.Info($"BuildMac: [{step.name}] {m.type}: {m.content}");
            ThreadLog.Info($"BuildMac: result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                           $"size={s.totalSize / (1024.0 * 1024.0):F1} MB time={s.totalTime.TotalSeconds:F1} s (wall {sw.Elapsed.TotalSeconds:F1} s) out={s.outputPath}");
            if (s.result != BuildResult.Succeeded) ThreadLog.Problem(problems, "BuildMac: build result " + s.result);

            // leave the project on the Windows target the QA tools expect
            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, BuildTarget.StandaloneWindows64);
            return problems;
        }

        // ------------------------------------------------------------------------------------------------ CompileCheck
        public static void CompileCheck()
        {
            Run("CompileCheck", () =>
            {
                var p = new List<string>();
                if (EditorUtility.scriptCompilationFailed) ThreadLog.Problem(p, "CompileCheck: script compilation failed");
                ThreadLog.Info($"CompileCheck: Unity {Application.unityVersion}, device {SystemInfo.graphicsDeviceType}, " +
                               $"reversedZ {SystemInfo.usesReversedZBuffer}, APIs {ProjectConfigurator.ApiList()}");
                return p;
            });
        }

        // ------------------------------------------------------------------------------------------------ Validate
        [MenuItem(Menu + "Validate", priority = 41)]
        public static void Validate()
        {
            Run("Validate", ValidateInternal);
        }

        sealed class Row { public string item, status, detail; }

        public static List<string> ValidateInternal()
        {
            var problems = new List<string>();
            var rows = new List<Row>();
            void Add(string item, bool ok, string detail)
            {
                rows.Add(new Row { item = item, status = ok ? "OK" : "FAIL", detail = detail });
                if (!ok) problems.Add(item + ": " + detail);
            }

            // ---- scene
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                problems.Add("Validate cancelled (unsaved scene)");
                return problems;
            }
            bool sceneExists = File.Exists(ThreadPaths.Abs(ThreadPaths.MainScene));
            Add("scene file", sceneExists, ThreadPaths.MainScene);
            if (sceneExists)
            {
                var scene = EditorSceneManager.OpenScene(ThreadPaths.MainScene, OpenSceneMode.Single);
                int missing = 0, gos = 0;
                var missingOn = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        gos++;
                        int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (n > 0) { missing += n; missingOn.Add(t.name); }
                        foreach (var r in t.GetComponents<Renderer>())
                            foreach (var m in r.sharedMaterials)
                                if (m == null || m.shader == null || m.shader.name == MaterialFactory.ErrorShaderName)
                                    Add("material on " + t.name, false, "missing or error-shader material on renderer");
                    }
                Add("missing scripts", missing == 0, missing == 0 ? $"{gos} objects scanned" : $"{missing} on {string.Join(", ", missingOn)}");
                CheckSceneContract(scene, Add);
                Add("skybox", RenderSettings.skybox != null && RenderSettings.skybox.shader != null && RenderSettings.skybox.shader.name == ThreadPaths.ShaderSky,
                    RenderSettings.skybox != null ? RenderSettings.skybox.name : "none");
                Add("unity fog off", !RenderSettings.fog, "RenderSettings.fog = " + RenderSettings.fog);
            }

            // ---- build settings
            var enabled = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) enabled.Add(s.path);
            Add("build scenes", enabled.Count == 1 && enabled[0] == ThreadPaths.MainScene, string.Join(", ", enabled));

            // ---- material contract
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ThreadPaths.LibraryAsset);
            var matProblems = new List<string>();
            MaterialFactory.Check(lib, matProblems);
            Add("material contract", matProblems.Count == 0, matProblems.Count == 0 ? $"{MaterialFactory.Specs().Count} materials" : matProblems.Count + " issue(s)");
            foreach (var mp in matProblems) Add("  material", false, mp);

            // ---- shaders: compile every pass of every library material (its keyword set), then read the messages
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                if (lib != null)
                    foreach (var f in MaterialFactory.LibraryFields())
                    {
                        var m = f.GetValue(lib) as Material;
                        if (m == null || m.shader == null) continue;
                        for (int p = 0; p < m.passCount; p++) ShaderUtil.CompilePass(m, p, true);
                    }
            }
            catch (Exception e) { ThreadLog.Warn("Validate: CompilePass threw " + e.Message); }
            finally { ShaderUtil.allowAsyncCompilation = async; }

            foreach (var name in ThreadPaths.ShaderNames)
            {
                var sh = MaterialFactory.FindShader(name);
                if (sh == null) { Add("shader " + name, false, "not found"); continue; }
                int errors = 0, warnings = 0;
                foreach (var msg in ShaderUtil.GetShaderMessages(sh))
                {
                    bool err = msg.severity == ShaderCompilerMessageSeverity.Error;
                    if (err) errors++; else warnings++;
                    ThreadLog.Info($"Validate: {name} {(err ? "ERROR" : "warning")} {msg.platform} {msg.file}:{msg.line} {msg.message} {msg.messageDetails}");
                }
                bool has = ShaderUtil.ShaderHasError(sh);
                Add("shader " + name, !has && errors == 0, $"{sh.passCount} passes, {errors} errors, {warnings} warnings");
            }

            // ---- pipeline
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ThreadPaths.RpAsset);
            Add("default pipeline", GraphicsSettings.defaultRenderPipeline == asset && asset != null, EditorUtil.AssetPathOf(GraphicsSettings.defaultRenderPipeline));
            Add("quality levels", QualitySettings.count == 1 && QualitySettings.GetRenderPipelineAssetAt(0) == asset, string.Join(", ", QualitySettings.names));
            if (asset != null)
                Add("hdr 64-bit / msaa", asset.supportsHDR && asset.hdrColorBufferPrecision == HDRColorBufferPrecision._64Bits && asset.msaaSampleCount == ThreadConstants.Msaa,
                    $"HDR {asset.supportsHDR}, {asset.hdrColorBufferPrecision}, MSAA {asset.msaaSampleCount}");
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ThreadPaths.RendererAsset);
            if (rd != null)
            {
                Add("forward+", rd.renderingMode == RenderingMode.ForwardPlus, rd.renderingMode.ToString());
                ScriptableRendererFeature grade = null;
                foreach (var f in rd.rendererFeatures) if (f != null && f.GetType().FullName == PipelineConfigurator.GradeFeatureType) grade = f;
                Material gm = null;
                if (grade != null)
                {
                    var so = new SerializedObject(grade);
                    var p = so.FindProperty(PipelineConfigurator.GradeFeatureMaterialField);
                    gm = p != null ? p.objectReferenceValue as Material : null;
                }
                Add("grade feature", grade != null && grade.isActive && gm != null, grade == null ? "missing" : "material " + (gm != null ? gm.name : "null"));
            }
            Add("input handler", ProjectConfigurator.ActiveInputHandler() == ProjectConfigurator.InputSystemOnly, "activeInputHandler = " + ProjectConfigurator.ActiveInputHandler());
            Add("graphics API", !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64), ProjectConfigurator.ApiList());

            // ---- table
            ThreadLog.Info("VALIDATE | status | item | detail");
            foreach (var r in rows) ThreadLog.Info($"VALIDATE | {r.status,-4} | {r.item} | {r.detail}");
            ThreadLog.Info($"VALIDATE | {(problems.Count == 0 ? "PASS" : "FAIL")} | {rows.Count} checks, {problems.Count} problem(s)");
            return problems;
        }

        /// <summary>Every object and component of the scene contract exists; GameBootstrap references are set.</summary>
        static void CheckSceneContract(Scene scene, Action<string, bool, string> add)
        {
            GameObject Find(string path)
            {
                string[] parts = path.Split('/');
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (r.name != parts[0]) continue;
                    Transform t = r.transform;
                    for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                    return t != null ? t.gameObject : null;
                }
                return null;
            }
            void Need(string path, string[] types)
            {
                var go = Find(path);
                if (go == null) { add("object " + path, false, "missing"); return; }
                foreach (var tn in types)
                {
                    var t = EditorUtil.FindType(tn);
                    bool ok = t != null && go.GetComponent(t) != null;
                    add(path + " : " + tn, ok, t == null ? "type not found" : (ok ? "present" : "component missing"));
                }
            }
            Need("Game", SceneBuilder.GameComponents);
            Need("Player", SceneBuilder.PlayerComponents);
            Need("Player/CameraRig", SceneBuilder.CameraRigComponents);
            Need("Sun", new string[0]);
            Need("World/NearRoot", new string[0]);
            Need("World/GroundGroup", new string[0]);
            Need("PathGuide", SceneBuilder.PathGuideComponents);

            var rig = Find("Player/CameraRig");
            var cam = rig != null ? rig.GetComponent<Camera>() : null;
            var acd = cam != null ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
            add("camera", cam != null && rig.CompareTag("MainCamera") && cam.allowHDR && cam.clearFlags == CameraClearFlags.Skybox &&
                          Mathf.Approximately(cam.nearClipPlane, ThreadConstants.CameraNear) && Mathf.Approximately(cam.farClipPlane, ThreadConstants.CameraFar),
                cam != null ? $"near {cam.nearClipPlane} far {cam.farClipPlane} HDR {cam.allowHDR} clear {cam.clearFlags}" : "missing");
            add("camera URP data", acd != null && !acd.renderPostProcessing && acd.antialiasing == AntialiasingMode.None && !acd.dithering && acd.renderShadows,
                acd != null ? $"post {acd.renderPostProcessing} aa {acd.antialiasing} dither {acd.dithering} shadows {acd.renderShadows}" : "missing");
            add("audio listener", rig != null && rig.GetComponent<AudioListener>() != null, "on CameraRig");
            var sun = Find("Sun");
            var light = sun != null ? sun.GetComponent<Light>() : null;
            add("sun light", light != null && light.type == LightType.Directional && light.shadows == LightShadows.Soft, light != null ? $"{light.type} {light.shadows}" : "missing");

            var game = Find("Game");
            var gb = game != null ? game.GetComponent<GameBootstrap>() : null;
            if (gb != null)
                add("GameBootstrap refs", gb.materials != null && gb.palette != null && gb.nearRoot != null && gb.groundGroup != null && gb.sun != null && gb.mainCamera != null,
                    $"materials {gb.materials != null} palette {gb.palette != null} nearRoot {gb.nearRoot != null} ground {gb.groundGroup != null} sun {gb.sun != null} camera {gb.mainCamera != null}");
        }

        // ------------------------------------------------------------------------------------------------ CaptureInEditor
        [MenuItem(Menu + "Capture In Editor (play mode)", priority = 60)]
        public static void CaptureInEditor()
        {
            ThreadLog.QuietStackTraces();
            try
            {
                if (Application.isBatchMode && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                {
                    Debug.LogError(ThreadLog.Prefix + "CaptureInEditor: null graphics device (do not pass -nographics)");
                    EditorApplication.Exit(4);
                    return;
                }
                if (!File.Exists(ThreadPaths.Abs(ThreadPaths.MainScene)))
                {
                    Debug.LogError(ThreadLog.Prefix + "CaptureInEditor: " + ThreadPaths.MainScene + " missing (run RebuildAll)");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ThreadPaths.MainScene, OpenSceneMode.Single);
                ShaderUtil.allowAsyncCompilation = false;
                if (Application.isBatchMode) CaptureWatchdog.Arm(CliArgs.GetDouble(CaptureWatchdog.TimeoutArg, CaptureWatchdog.DefaultTimeoutSeconds));
                ThreadLog.Info($"CaptureInEditor: entering play mode (device {SystemInfo.graphicsDeviceType}, args: {string.Join(" ", CliArgs.Args)})");
                EditorApplication.isPlaying = true;   // same as UTF EnterPlayModeTask; the runtime CaptureHarness takes over
            }
            catch (Exception e)
            {
                Debug.LogError(ThreadLog.Prefix + "CaptureInEditor threw: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------------------------------------ plumbing
        static void Step(string name, List<string> problems, Action a)
        {
            var sw = Stopwatch.StartNew();
            int before = problems.Count;
            try { a(); }
            catch (Exception e) { ThreadLog.Problem(problems, $"{name} threw: {e}"); }
            ThreadLog.Info($"step {name}: {(problems.Count == before ? "ok" : (problems.Count - before) + " problem(s)")} ({sw.Elapsed.TotalMilliseconds:F0} ms)");
        }

        /// <summary>Runs a batch method body; in batch mode exits 0 (no problems) or 1.</summary>
        static void Run(string name, Func<List<string>> body)
        {
            ThreadLog.QuietStackTraces();
            ThreadLog.Info($"{name}: start (Unity {Application.unityVersion}, batch {Application.isBatchMode})");
            var sw = Stopwatch.StartNew();
            List<string> problems;
            try { problems = body() ?? new List<string>(); }
            catch (Exception e)
            {
                problems = new List<string> { name + " threw: " + e };
                Debug.LogError(ThreadLog.Prefix + "ERROR " + name + " threw: " + e);
            }
            foreach (var p in problems) ThreadLog.Info("problem: " + p);
            int code = problems.Count == 0 ? 0 : 1;
            ThreadLog.Info($"{name}: {(code == 0 ? "SUCCESS" : "FAILED")} with {problems.Count} problem(s) in {sw.Elapsed.TotalSeconds:F1} s -> exit {code}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
