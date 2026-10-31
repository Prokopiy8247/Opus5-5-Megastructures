using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Player, quality, tag/layer, audio and graphics-API settings (Part 7.6, 8.1, 12). Idempotent; called by BatchTools.RebuildAll.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ProductName = "Abyss";
        public const string CompanyName = "Megastructures";

        /// <summary>Graphics API for editor and player (Part 8.1: one explicit API, auto off). Changed only by a measured decision in DEVELOPMENT.md.</summary>
        public static readonly GraphicsDeviceType[] WindowsApis = { GraphicsDeviceType.Direct3D11 };

        public static void Apply()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.forceSingleInstance = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, WindowsApis);
            PlayerSettings.gpuSkinning = false;
            PlayerSettings.enableFrameTimingStats = true; // FrameTimingManager GPU times for the Part 11.7 perf log
            PlayerSettings.stripUnusedMeshComponents = false; // procedural meshes need all channels at runtime

            // Splash (Part 6.6): try to disable; the licence may force it back on in the build (recorded in DEVELOPMENT.md).
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            SetLayers();
            SetAudioSampleRate(48000);
            SetQualityLevels();
            SetTimeAndPhysics();
            Debug.Log("[ABYSS] ProjectSetup applied");
        }

        static void SetLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(CorridorKit.LayerCorridor).stringValue = "Corridor";
            layers.GetArrayElementAtIndex(CorridorKit.LayerSafety).stringValue = "Safety";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetAudioSampleRate(int rate)
        {
            var audio = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            var sr = audio.FindProperty("m_SampleRate");
            if (sr != null) sr.intValue = rate;
            var dsp = audio.FindProperty("m_DSPBufferSize");
            if (dsp != null) dsp.intValue = 1024; // "Good latency": stable callback size for OnAudioFilterRead
            var vr = audio.FindProperty("m_DisableAudio");
            if (vr != null) vr.boolValue = false;
            audio.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Keep exactly one quality level ("PC") using the PC URP asset (Part 8.1). VSync on by default.</summary>
        static void SetQualityLevels()
        {
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            for (int i = levels.arraySize - 1; i >= 0; i--)
            {
                var name = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (name != "PC") levels.DeleteArrayElementAtIndex(i);
            }
            qs.FindProperty("m_CurrentQuality").intValue = 0;
            var perPlatform = qs.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
                for (int i = 0; i < perPlatform.arraySize; i++)
                    perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = 0;
            if (levels.arraySize > 0)
            {
                var pc = levels.GetArrayElementAtIndex(0);
                SetRel(pc, "vSyncCount", 1);
                SetRel(pc, "anisotropicTextures", 2);   // ForceEnable
                SetRel(pc, "lodBias", 2f);
                SetRel(pc, "maximumLODLevel", 0);
                SetRel(pc, "realtimeReflectionProbes", 0);
                SetRel(pc, "billboardsFaceCameraPosition", 1);
                SetRel(pc, "globalTextureMipmapLimit", 0);
                SetRel(pc, "streamingMipmapsActive", 0);
                SetRel(pc, "asyncUploadTimeSlice", 8);
                SetRel(pc, "asyncUploadBufferSize", 64);
            }
            qs.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRel(SerializedProperty parent, string name, int v) { var p = parent.FindPropertyRelative(name); if (p != null) p.intValue = v; }
        static void SetRel(SerializedProperty parent, string name, float v) { var p = parent.FindPropertyRelative(name); if (p != null) p.floatValue = v; }

        static void SetTimeAndPhysics()
        {
            Time.fixedDeltaTime = 1f / 60f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Physics.autoSyncTransforms = false;
            // Only Corridor collides with the player; Safety is trigger-only. Disable everything else for the two layers.
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(CorridorKit.LayerCorridor, i, i != 0 && i != CorridorKit.LayerSafety);
            }
            Physics.IgnoreLayerCollision(CorridorKit.LayerCorridor, CorridorKit.LayerCorridor, true);
            Physics.IgnoreLayerCollision(CorridorKit.LayerSafety, CorridorKit.LayerSafety, true);
        }
    }
}
