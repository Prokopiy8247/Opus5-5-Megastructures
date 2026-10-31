using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Generates Assets/_Game/Scenes/Main.unity and the build settings (Part 7.7, CONTRACTS section 3). The scene holds only
    /// the systems; all geometry is generated at load by the beats. Beat components are resolved by type name so this script
    /// does not depend on the beat assembly being complete.
    /// </summary>
    public static class SceneBuilder
    {
        public const string MainScenePath = "Assets/_Game/Scenes/Main.unity";
        public const string LightingSettingsPath = "Assets/_Game/Settings/AbyssLighting.lighting";

        static readonly string[] BeatTypes =
        {
            "Megastructure.Beats.Beat01_Gallery", "Megastructure.Beats.Beat02_Bridge", "Megastructure.Beats.Beat03_Ledge",
            "Megastructure.Beats.Beat04_Hall", "Megastructure.Beats.Beat05_Lift", "Megastructure.Beats.Beat06_Fan",
            "Megastructure.Beats.Beat07_Reveal",
        };

        public static void BuildAll()
        {
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(RenderSetup.MaterialLibraryPath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderSetup.VolumeProfilePath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(RenderSetup.PanelSettingsPath);
            if (lib == null || profile == null) throw new Exception("MaterialLibrary or volume profile missing");

            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------------- lighting environment (no baking, no probes, no Unity fog)
            RenderSettings.skybox = lib.skybox;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "AbyssLighting" };
                AssetDatabase.CreateAsset(ls, LightingSettingsPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            ls.autoGenerate = false;
            Lightmapping.lightingSettings = ls;

            // ---------------------------------------------------------------- key light (the only directional light, 14.2)
            var keyGo = new GameObject("Key");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = AbyssPalette.Hex(0xC4D6E8);
            key.intensity = 0f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 1f;
            key.lightmapBakeType = LightmapBakeType.Realtime;
            keyGo.transform.rotation = AbyssConst.YawPitch(205f, -72f);
            keyGo.AddComponent<UniversalAdditionalLightData>();

            // ---------------------------------------------------------------- global volume
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = profile;

            // ---------------------------------------------------------------- camera rig
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.position = new Vector3(0f, AbyssConst.EYE, 0f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = AbyssConst.CAMERA_NEAR;
            cam.farClipPlane = AbyssConst.FAR_PLANE[0];
            cam.fieldOfView = AbyssConst.FOV_PER_BEAT[0];
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.useOcclusionCulling = false;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;  // MSAA 4x from the pipeline asset (Part 8.1); no FXAA (it would dither after our pass)
            camData.dithering = false;                    // exactly one dither: our final pass (Part 8.2)
            camData.stopNaN = true;
            camData.renderShadows = true;
            camData.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
            camGo.AddComponent<AudioListener>();
            rigGo.AddComponent<CameraRig>().Camera = cam;

            // ---------------------------------------------------------------- player
            var playerGo = new GameObject("Player");
            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = AbyssConst.CAPSULE_R;
            cc.height = AbyssConst.CAPSULE_H;
            cc.center = new Vector3(0f, AbyssConst.CAPSULE_H * 0.5f, 0f);
            cc.slopeLimit = 48f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.035f;
            cc.minMoveDistance = 0f;
            playerGo.AddComponent<PlayerController>();
            playerGo.AddComponent<AutoWalker>();

            // ---------------------------------------------------------------- audio
            var audioGo = new GameObject("Audio");
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            audioGo.AddComponent<AudioDirector>();

            // ---------------------------------------------------------------- UI
            var uiGo = new GameObject("UI");
            uiGo.AddComponent<FadeController>().panelSettings = panel;

            // ---------------------------------------------------------------- beats
            var beats = new BeatBase[7];
            for (int i = 0; i < BeatTypes.Length; i++)
            {
                var go = new GameObject(BeatTypes[i].Substring(BeatTypes[i].LastIndexOf('.') + 1));
                var t = FindType(BeatTypes[i]);
                if (t == null) { Debug.LogWarning("[ABYSS] SceneBuilder: beat type not found: " + BeatTypes[i]); continue; }
                beats[i] = (BeatBase)go.AddComponent(t);
            }

            // ---------------------------------------------------------------- bootstrap and directors
            var bootGo = new GameObject("Bootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            boot.materialLibrary = lib;
            boot.beats = beats;
            boot.keyLight = key;
            boot.globalVolume = vol;
            boot.mainCamera = cam;
            bootGo.AddComponent<ClockDriver>();
            bootGo.AddComponent<BeatDirector>().Beats = beats;
            var pd = bootGo.AddComponent<ProgressDirector>();
            pd.keyLight = key;
            pd.globalVolume = vol;
            bootGo.AddComponent<InstancedRegistryDriver>();
            var qa = FindType("Megastructure.QaHarness");
            if (qa != null) bootGo.AddComponent(qa);

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            Debug.Log("[ABYSS] SceneBuilder: Main.unity written (" + CountBeats(beats) + "/7 beats)");
        }

        static int CountBeats(BeatBase[] b) { int n = 0; foreach (var x in b) if (x != null) n++; return n; }

        static Type FindType(string fullName)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                if (t.FullName == fullName) return t;
            return null;
        }
    }
}
