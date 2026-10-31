using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Project-wide settings written by RebuildAll: generated HLSL (step a), PlayerSettings / input / audio (b),
    /// the single "PC" quality level and the default render pipeline (c), the UI Toolkit theme and PanelSettings and
    /// Always Included Shaders (f), the build scene list (h) and the template cleanup (i).
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>DX12 is the primary (only) Windows graphics API (lead decision, Part 8.1). Set to false to switch
        /// the player, the Editor and the shader gate to Direct3D 11 if DX12 proves unstable.</summary>
        public const bool UseD3D12 = true;

        public const string PaletteHlslPath = BatchTools.ShaderIncludeDir + "/RingPalette.hlsl";
        public const string WorldHlslPath = BatchTools.ShaderIncludeDir + "/RingWorldData.hlsl";
        public const string ThemePath = BatchTools.SettingsDir + "/RingTheme.tss";
        public const string PanelPath = BatchTools.SettingsDir + "/RingPanel.asset";

        // research 06 1.2: the exact content Unity's own PanelSettings creator writes (references the built-in theme)
        const string ThemeText = "@import url(\"unity-theme://default\");\nVisualElement {}\n";

        // Part 14.11: UI Toolkit, Scale With Screen Size at 1920 x 1080, match height
        static readonly Vector2Int UiReferenceResolution = new Vector2Int(1920, 1080);
        const float UiMatchHeight = 1f;
        const float UiSortingOrder = 100f;

        // Part 12 / task BA: default window size of the player (fullscreen window uses the native resolution)
        const int DefaultScreenWidth = 1920, DefaultScreenHeight = 1080;
        // research 05 section 2: request 48 kHz (the runtime always reads AudioSettings.outputSampleRate)
        const int AudioSampleRate = 48000;

        // Part 8.1 / 8.5 / 14.2.3: single PC quality level consistent with the URP asset
        const int QualityVSync = 1;
        const int QualityAnisoForceEnable = 2;        // AnisotropicFiltering.ForceEnable
        const float QualityLodBias = 1f;
        const int QualityShadowsAll = 2;              // ShadowQuality.All
        const int QualityShadowResVeryHigh = 3;       // UnityEngine.ShadowResolution.VeryHigh

        /// <summary>research 06 1.3: UI Toolkit / TextCore runtime shaders that PanelSettings finds with Shader.Find.</summary>
        static readonly string[] UiRuntimeShaders =
        {
            "Hidden/Internal-UIRDefault", "Hidden/Internal-UIRAtlasBlitCopy", "Hidden/UIR/GaussianBlur", "Hidden/UIR/ColorEffect",
            "Hidden/UIR/DropShadowComposite", "Hidden/TextCore/Distance Field SSD", "Hidden/Internal-GUITextureClipText",
            "Hidden/TextCore/Sprite", "Hidden/Internal-UIE-ColorConversionBlit",
        };

        public static GraphicsDeviceType[] WindowsGraphicsApis =>
            UseD3D12 ? new[] { GraphicsDeviceType.Direct3D12 } : new[] { GraphicsDeviceType.Direct3D11 };

        // ---------------------------------------------------------------------------------------------- step a
        public static void WriteGeneratedHlsl()
        {
            bool p = EditorIO.WriteTextIfChanged(PaletteHlslPath, RingPalette.GenerateHlsl());
            bool w = EditorIO.WriteTextIfChanged(WorldHlslPath, RingWorld.GenerateHlsl());
            BatchLog.Note(PaletteHlslPath + (p ? " written" : " unchanged"));
            BatchLog.Note(WorldHlslPath + (w ? " written" : " unchanged"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        // ---------------------------------------------------------------------------------------------- step b
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = BatchTools.CompanyName;
            PlayerSettings.productName = BatchTools.ProductName;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = DefaultScreenWidth;
            PlayerSettings.defaultScreenHeight = DefaultScreenHeight;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.enableFrameTimingStats = true;   // PerfMonitor reads GPU frame times through FrameTimingManager
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.strictShaderVariantMatching = false;   // release build (research 09 3.1: QA builds may turn it on)

            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;
            if (PlayerSettings.SplashScreen.show)
                BatchLog.Note("splash screen: the licence forces the Unity splash (SplashScreen.show stays true); kept (Part 6.6)");
            else
                BatchLog.Note("splash screen: disabled (licence allows SplashScreen.show = false)");

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            // Reflection discovers IBeatBuilder / IRingMaterialDefs implementations: no managed stripping
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);

            var target = BuildTarget.StandaloneWindows64;
            var want = WindowsGraphicsApis;
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(target)) PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            var cur = PlayerSettings.GetGraphicsAPIs(target);
            if (cur == null || !cur.SequenceEqual(want)) PlayerSettings.SetGraphicsAPIs(target, want);
            BatchLog.Note("graphics API (Windows, auto off): " + string.Join(",", want) + (UseD3D12 ? " (DX12 primary; flip ProjectSetup.UseD3D12 for D3D11)" : " (D3D11 fallback)")
                          + "; was " + (cur == null ? "none" : string.Join(",", cur)));

            ApplyActiveInputHandler();
            ApplyAudioSettings();
            BatchLog.Note("PlayerSettings: " + BatchTools.CompanyName + " / " + BatchTools.ProductName + ", FullScreenWindow, resizable, runInBackground, "
                          + DefaultScreenWidth + "x" + DefaultScreenHeight + ", Linear, Mono, stripping Disabled, strict variant matching off");
        }

        static void ApplyActiveInputHandler()
        {
            Object ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault(o => o is PlayerSettings);
            if (ps == null) { BatchLog.Warn("ProjectSettings.asset: PlayerSettings object not found; activeInputHandler not checked"); return; }
            var so = new SerializedObject(ps);
            var p = SerializedUtil.Find(so, "activeInputHandler", "PlayerSettings");
            if (p == null) return;
            if (p.intValue == 1) { BatchLog.Note("activeInputHandler = 1 (Input System only), kept"); return; }
            int old = p.intValue;
            p.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ps);
            BatchLog.Warn("activeInputHandler changed " + old + " -> 1: the Editor must be restarted before the change is active (Part 7.6)");
        }

        static void ApplyAudioSettings()
        {
            Object am = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset").FirstOrDefault(o => o != null);
            if (am == null) { BatchLog.Warn("AudioManager.asset not found"); return; }
            var so = new SerializedObject(am);
            var rate = SerializedUtil.Find(so, "m_SampleRate", "AudioManager");
            if (rate != null) rate.intValue = AudioSampleRate;
            var dsp = so.FindProperty("m_DSPBufferSize");
            var req = so.FindProperty("m_RequestedDSPBufferSize");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(am);
            BatchLog.Note("audio: m_SampleRate = " + AudioSampleRate + ", DSP buffer kept at default (m_DSPBufferSize "
                          + (dsp != null ? dsp.intValue.ToString() : "?") + ", requested " + (req != null ? req.intValue.ToString() : "?") + ")");
        }

        // ---------------------------------------------------------------------------------------------- step c
        /// <summary>Keeps exactly one quality level "PC" that uses the Ring pipeline, and sets the default pipeline.</summary>
        public static void ConfigureQualityAndGraphics(UniversalRenderPipelineAsset rp)
        {
            Object qsObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault(o => o != null);
            if (qsObj == null) throw new InvalidOperationException("ProjectSettings/QualitySettings.asset not found");
            var so = new SerializedObject(qsObj);
            var levels = SerializedUtil.Find(so, "m_QualitySettings", "QualitySettings");
            if (levels == null || levels.arraySize == 0) throw new InvalidOperationException("QualitySettings has no levels");

            int pc = -1;
            for (int i = 0; i < levels.arraySize; i++)
            {
                var n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (n != null && n.stringValue == "PC") { pc = i; break; }
            }
            if (pc < 0) pc = levels.arraySize - 1;   // no "PC" level: keep the last one and rename it
            var removed = new List<string>();
            for (int i = levels.arraySize - 1; i >= 0; i--)
            {
                if (i == pc) continue;
                var n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                removed.Add(n != null ? n.stringValue : ("#" + i));
                levels.DeleteArrayElementAtIndex(i);
            }

            var lvl = levels.GetArrayElementAtIndex(0);
            const string ctx = "QualitySettings[PC]";
            var nameProp = SerializedUtil.FindRel(lvl, "name", ctx);
            if (nameProp != null) nameProp.stringValue = "PC";
            SerializedUtil.SetInt(SerializedUtil.FindRel(lvl, "vSyncCount", ctx), QualityVSync);
            SerializedUtil.SetInt(SerializedUtil.FindRel(lvl, "anisotropicTextures", ctx), QualityAnisoForceEnable);
            SerializedUtil.SetFloat(SerializedUtil.FindRel(lvl, "lodBias", ctx), QualityLodBias);
            SerializedUtil.SetInt(SerializedUtil.FindRel(lvl, "antiAliasing", ctx), UrpSetup.MsaaSamples);
            SerializedUtil.SetInt(SerializedUtil.FindRel(lvl, "shadows", ctx), QualityShadowsAll);
            SerializedUtil.SetInt(SerializedUtil.FindRel(lvl, "shadowResolution", ctx), QualityShadowResVeryHigh);
            SerializedUtil.SetFloat(SerializedUtil.FindRel(lvl, "shadowDistance", ctx), UrpSetup.ShadowDistance);
            SerializedUtil.SetInt(SerializedUtil.FindRel(lvl, "shadowCascades", ctx), UrpSetup.ShadowCascades);
            var split4 = SerializedUtil.FindRel(lvl, "shadowCascade4Split", ctx);
            if (split4 != null && split4.propertyType == SerializedPropertyType.Vector3) split4.vector3Value = UrpSetup.Cascade4Split;
            SerializedUtil.SetBool(SerializedUtil.FindRel(lvl, "enableLODCrossFade", ctx), false);
            SerializedUtil.SetBool(SerializedUtil.FindRel(lvl, "realtimeReflectionProbes", ctx), false);
            SerializedUtil.SetObject(SerializedUtil.FindRel(lvl, "customRenderPipeline", ctx), rp);
            var excluded = lvl.FindPropertyRelative("excludedTargetPlatforms");
            if (excluded != null && excluded.isArray)
                for (int i = excluded.arraySize - 1; i >= 0; i--)
                    if (excluded.GetArrayElementAtIndex(i).stringValue == "Standalone") excluded.DeleteArrayElementAtIndex(i);

            SerializedUtil.SetInt(SerializedUtil.Find(so, "m_CurrentQuality", "QualitySettings"), 0);
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var second = perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                    if (second != null) second.intValue = 0;
                }
            }
            else BatchLog.Warn("QualitySettings: m_PerPlatformDefaultQuality not found");
            so.ApplyModifiedPropertiesWithoutUndo();

            QualitySettings.SetQualityLevel(0, true);
            QualitySettings.renderPipeline = rp;
            GraphicsSettings.defaultRenderPipeline = rp;
            EditorUtility.SetDirty(qsObj);
            BatchLog.Note("quality: single level 'PC' (vSync 1, aniso ForceEnable, lodBias 1, shadows " + UrpSetup.ShadowDistance + " m / "
                          + UrpSetup.ShadowCascades + " cascades) uses " + AssetDatabase.GetAssetPath(rp)
                          + (removed.Count > 0 ? "; removed levels: " + string.Join(", ", removed) : ""));
            BatchLog.Note("GraphicsSettings.defaultRenderPipeline = " + AssetDatabase.GetAssetPath(rp));
        }

        // ---------------------------------------------------------------------------------------------- step f
        public static PanelSettings CreateOrUpdateUi()
        {
            if (EditorIO.WriteTextIfChanged(ThemePath, ThemeText))
            {
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                BatchLog.Note(ThemePath + " written");
            }
            else if (AssetDatabase.LoadMainAssetAtPath(ThemePath) == null)
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) BatchLog.Error("could not load " + ThemePath + " as ThemeStyleSheet (UI text would have no default font)");

            var ps = EditorIO.LoadOrCreateAsset(PanelPath, () => ScriptableObject.CreateInstance<PanelSettings>());
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = UiReferenceResolution;
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = UiMatchHeight;
            ps.sortingOrder = UiSortingOrder;
            ps.clearColor = false;
            EditorUtility.SetDirty(ps);

            // research 06 1.3: the editor-created asset must serialize its UI shaders (player Shader.Find misses them)
            var so = new SerializedObject(ps);
            var sh = so.FindProperty("m_DefaultShader");
            if (sh == null) BatchLog.Warn("PanelSettings: m_DefaultShader field not found (cannot verify shader references)");
            else if (sh.objectReferenceValue == null) BatchLog.Warn("PanelSettings.m_DefaultShader is null; relying on Always Included Shaders");
            BatchLog.Note(PanelPath + ": ScaleWithScreenSize " + UiReferenceResolution.x + "x" + UiReferenceResolution.y + ", match height, sorting " + UiSortingOrder + ", theme " + (theme != null ? "ok" : "MISSING"));
            return ps;
        }

        /// <summary>Adds the UI runtime shaders and every Megastructure/* shader to Always Included Shaders (keeps Unity's defaults).</summary>
        public static void UpdateAlwaysIncludedShaders()
        {
            var wanted = new List<Shader>();
            foreach (var n in UiRuntimeShaders)
            {
                var s = Shader.Find(n);
                if (s == null) BatchLog.Warn("UI shader not found for Always Included: " + n);
                else wanted.Add(s);
            }
            foreach (var s in FindGameShaders())
                if (s.name.StartsWith("Megastructure/", StringComparison.Ordinal)) wanted.Add(s);

            var gs = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(gs);
            var arr = SerializedUtil.Find(so, "m_AlwaysIncludedShaders", "GraphicsSettings");
            if (arr == null) return;
            var have = new HashSet<Object>();
            int nulls = 0;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var o = arr.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o == null) nulls++; else have.Add(o);
            }
            int added = 0;
            foreach (var s in wanted)
            {
                if (have.Contains(s)) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = s;
                have.Add(s);
                added++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gs);
            if (nulls > 0) BatchLog.Warn("Always Included Shaders contains " + nulls + " missing (null) entries; left untouched");
            BatchLog.Note("Always Included Shaders: " + arr.arraySize + " entries (" + added + " added; " + wanted.Count + " UI + Megastructure shaders wanted)");
        }

        /// <summary>Every .shader asset under Assets/_Game (Shaders plus the beat folders' own shaders), sorted by path.</summary>
        public static List<Shader> FindGameShaders()
        {
            var res = new List<Shader>();
            foreach (var path in FindGameShaderPaths())
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s != null) res.Add(s);
            }
            return res;
        }

        public static List<string> FindGameShaderPaths()
        {
            var paths = new List<string>();
            // whole game root: beats keep their own shaders (Beats/Beat05_Wall, Beat06_Tunnel, Beat07_Outside/Shaders)
            if (!AssetDatabase.IsValidFolder(BatchTools.GameRoot)) return paths;
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { BatchTools.GameRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) && !paths.Contains(p)) paths.Add(p);
            }
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        // ---------------------------------------------------------------------------------------------- step h
        public static void SetBuildScenes()
        {
            var scenes = new[] { new EditorBuildSettingsScene(BatchTools.ScenePath, true) };
            EditorBuildSettings.scenes = scenes;
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null)
            {
                if (profile.overrideGlobalScenes)
                {
                    profile.scenes = scenes;
                    EditorUtility.SetDirty(profile);
                    BatchLog.Note("active build profile " + AssetDatabase.GetAssetPath(profile) + " overrides scenes: set to Main.unity");
                }
                else BatchLog.Info("active build profile " + AssetDatabase.GetAssetPath(profile) + " uses the global scene list");
            }
            BatchLog.Note("build scenes: " + BatchTools.ScenePath + " only");
        }

        // ---------------------------------------------------------------------------------------------- step i
        public static void DeleteTemplateLeftovers(UniversalRenderPipelineAsset rp)
        {
            EditorIO.DeleteIfExists("Assets/Scenes/SampleScene.unity");
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Length == 0)
                EditorIO.DeleteIfExists("Assets/Scenes");
            EditorIO.DeleteIfExists("Assets/TutorialInfo");
            EditorIO.DeleteIfExists("Assets/Readme.asset");
            EditorIO.DeleteIfExists("Assets/Settings/Mobile_RPAsset.asset");
            EditorIO.DeleteIfExists("Assets/Settings/Mobile_Renderer.asset");
            EditorIO.DeleteIfExists("Assets/Settings/SampleSceneProfile.asset");

            bool assigned = rp != null && GraphicsSettings.defaultRenderPipeline == rp && QualitySettings.renderPipeline == rp;
            if (assigned)
            {
                EditorIO.DeleteIfExists("Assets/Settings/PC_RPAsset.asset");
                EditorIO.DeleteIfExists("Assets/Settings/PC_Renderer.asset");
            }
            else BatchLog.Error("Ring pipeline is not assigned to GraphicsSettings and the PC quality level; PC_RPAsset/PC_Renderer kept");

            const string actionsPath = "Assets/InputSystem_Actions.inputactions";
            if (AssetDatabase.LoadMainAssetAtPath(actionsPath) != null)
            {
                bool referenced = EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out Object cfg)
                                  && cfg != null && AssetDatabase.GetAssetPath(cfg) == actionsPath;
                if (referenced) BatchLog.Note(actionsPath + " kept (referenced by the Input System project-wide actions setting)");
                else EditorIO.DeleteIfExists(actionsPath);
            }
        }
    }
}
