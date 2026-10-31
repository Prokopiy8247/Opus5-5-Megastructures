// ProjectConfigurator: PlayerSettings, graphics API, Always Included Shaders and the Corridor layer
// (MASTER_PROMPT Part 6 item 6, 7.6, 8.1, 8.6, Part 12; research R8 section 5, R2 1.8, R4 1.9).
// The graphics API is a parameter: -graphics-api d3d11|d3d12 (default d3d11, R8 5.1 recommendation); it is applied
// to StandaloneWindows64, which the Windows Editor also uses (first API of the list, from its next start).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.EditorTools
{
    public static class ProjectConfigurator
    {
        public const string GraphicsApiArg = "-graphics-api";
        public const string DefaultGraphicsApi = "d3d11";
        /// <summary>ProjectSettings.asset activeInputHandler: 0 = legacy, 1 = Input System (new) only, 2 = both.</summary>
        public const int InputSystemOnly = 1;
        public const int DefaultWindowWidth = 1920, DefaultWindowHeight = 1080;

        public static void Apply(List<string> problems)
        {
            PlayerSettings.companyName = ThreadPaths.CompanyName;
            PlayerSettings.productName = ThreadPaths.ProductName;

            // Splash (Part 6.6): off if the licence allows it; otherwise black and static.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            if (PlayerSettings.SplashScreen.show) ThreadLog.Warn("ProjectConfigurator: the licence keeps the Unity splash screen on (black static fallback set)");

            // Window (Part 12, Part 8.10).
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = DefaultWindowWidth;
            PlayerSettings.defaultScreenHeight = DefaultWindowHeight;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;      // native Alt+Enter; F11 is handled in code
            PlayerSettings.forceSingleInstance = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.useFlipModelSwapchain = true;
            PlayerSettings.allowHDRDisplaySupport = false;    // SDR grade only (R1 pitfall 9, R4 pitfall 22)

            // Rendering / scripting.
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.enableFrameTimingStats = true;     // FrameTimingManager GPU times in the release player (11.7)
            ApplyGraphicsApi(problems);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);

            CheckInputHandler(problems);
            EnsureAlwaysIncludedShaders(problems);
            EnsureCorridorLayer(problems);
            AssetDatabase.SaveAssets();

            ThreadLog.Info($"ProjectConfigurator: {PlayerSettings.companyName}/{PlayerSettings.productName} splash={PlayerSettings.SplashScreen.show} " +
                           $"pro={PlayerSettings.advancedLicense} colorSpace={PlayerSettings.colorSpace} apis={ApiList()} " +
                           $"backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)} " +
                           $"api-compat={PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone)} frameTiming={PlayerSettings.enableFrameTimingStats}");
        }

        /// <summary>Requested API from the command line (d3d11 | d3d12), default d3d11.</summary>
        public static GraphicsDeviceType RequestedApi(List<string> problems)
        {
            string a = CliArgs.Get(GraphicsApiArg, DefaultGraphicsApi).Trim().ToLowerInvariant();
            if (a == "d3d12" || a == "dx12" || a == "direct3d12") return GraphicsDeviceType.Direct3D12;
            if (a != "d3d11" && a != "dx11" && a != "direct3d11")
                ThreadLog.Problem(problems, $"ProjectConfigurator: unknown {GraphicsApiArg} '{a}' (use d3d11 or d3d12); using Direct3D11");
            return GraphicsDeviceType.Direct3D11;
        }

        /// <summary>Explicit graphics API (auto off) for StandaloneWindows64; only touches the setting when it differs.</summary>
        public static void ApplyGraphicsApi(List<string> problems)
        {
            var want = RequestedApi(problems);
            var target = BuildTarget.StandaloneWindows64;
            var cur = PlayerSettings.GetGraphicsAPIs(target);
            bool same = !PlayerSettings.GetUseDefaultGraphicsAPIs(target) && cur != null && cur.Length == 1 && cur[0] == want;
            if (same) return;
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { want });
            ThreadLog.Info($"ProjectConfigurator: graphics API for {target} set to [{want}] (the Editor uses it from its next start; " +
                           $"running device: {SystemInfo.graphicsDeviceType})");
        }

        public static string ApiList()
        {
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var parts = new string[apis.Length];
            for (int i = 0; i < apis.Length; i++) parts[i] = apis[i].ToString();
            return (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64) ? "auto:" : "") + "[" + string.Join(",", parts) + "]";
        }

        /// <summary>Serialized PlayerSettings object (ProjectSettings/ProjectSettings.asset).</summary>
        public static SerializedObject PlayerSettingsObject()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"))
                if (o != null && o.GetType().Name == "PlayerSettings") return new SerializedObject(o);
            return null;
        }

        public static int ActiveInputHandler()
        {
            var so = PlayerSettingsObject();
            var p = so != null ? so.FindProperty("activeInputHandler") : null;
            return p != null ? p.intValue : -1;
        }

        /// <summary>Part 7.6: Input System only. Changing it needs an Editor restart, so it is only repaired, loudly.</summary>
        static void CheckInputHandler(List<string> problems)
        {
            var so = PlayerSettingsObject();
            var p = so != null ? so.FindProperty("activeInputHandler") : null;
            if (p == null) { ThreadLog.Warn("ProjectConfigurator: activeInputHandler not found in PlayerSettings"); return; }
            if (p.intValue == InputSystemOnly) return;
            p.intValue = InputSystemOnly;
            so.ApplyModifiedPropertiesWithoutUndo();
            ThreadLog.Problem(problems, "ProjectConfigurator: activeInputHandler was not 'Input System only'; set to 1, RESTART THE EDITOR before building");
        }

        /// <summary>Adds the five Megastructure shaders to GraphicsSettings.m_AlwaysIncludedShaders (R2 1.8).</summary>
        static void EnsureAlwaysIncludedShaders(List<string> problems)
        {
            var gs = EditorUtil.LoadProjectSettingsObject("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) { ThreadLog.Problem(problems, "ProjectConfigurator: cannot load GraphicsSettings"); return; }
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) { ThreadLog.Problem(problems, "ProjectConfigurator: m_AlwaysIncludedShaders not found"); return; }
            int added = 0;
            foreach (var name in ThreadPaths.ShaderNames)
            {
                var sh = MaterialFactory.FindShader(name);
                if (sh == null) { ThreadLog.Problem(problems, $"ProjectConfigurator: shader {name} not found, not added to Always Included Shaders"); continue; }
                bool present = false;
                for (int i = 0; i < arr.arraySize && !present; i++) present = arr.GetArrayElementAtIndex(i).objectReferenceValue == sh;
                if (present) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                added++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            ThreadLog.Info($"ProjectConfigurator: Always Included Shaders: {added} added, {arr.arraySize} total");
        }

        /// <summary>TagManager layer 30 = "Corridor" (PathGuide colliders, scene contract).</summary>
        static void EnsureCorridorLayer(List<string> problems)
        {
            var tm = EditorUtil.LoadProjectSettingsObject("ProjectSettings/TagManager.asset");
            if (tm == null) { ThreadLog.Problem(problems, "ProjectConfigurator: cannot load TagManager"); return; }
            var so = new SerializedObject(tm);
            var layers = so.FindProperty("layers");
            if (layers == null || layers.arraySize <= ThreadPaths.CorridorLayer) { ThreadLog.Problem(problems, "ProjectConfigurator: TagManager layers array not found"); return; }
            var l = layers.GetArrayElementAtIndex(ThreadPaths.CorridorLayer);
            if (l.stringValue == ThreadPaths.CorridorLayerName) return;
            if (!string.IsNullOrEmpty(l.stringValue)) ThreadLog.Warn($"ProjectConfigurator: layer {ThreadPaths.CorridorLayer} was '{l.stringValue}', renamed to {ThreadPaths.CorridorLayerName}");
            l.stringValue = ThreadPaths.CorridorLayerName;
            so.ApplyModifiedPropertiesWithoutUndo();
            ThreadLog.Info($"ProjectConfigurator: layer {ThreadPaths.CorridorLayer} = {ThreadPaths.CorridorLayerName}");
        }
    }
}
