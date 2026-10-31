// SceneBuilder: builds Assets/_Game/Scenes/Main.unity from scratch on every RebuildAll (MASTER_PROMPT 7.1, 7.7,
// 14.7.4). Every component of the scene contract is added BY FULL TYPE NAME through reflection, so the editor
// assembly compiles while the runtime systems are still being written; a missing type is logged as a problem.
//   Game        GameBootstrap (materials, palette, nearRoot, groundGroup, sun, mainCamera), ProgressDirector,
//               BeatDirector, Player.QualityBootstrap, UI.FadeController, QA.CaptureHarness, QA.AutoTestRunner,
//               QA.PerfMonitor, QA.WallBot
//   Player      Player.PlayerController, Player.AutoWalker
//     CameraRig Player.CameraRig + Camera (MainCamera, near 0.1, far 250 000, HDR, skybox) + UniversalAdditionalCameraData
//               (URP post OFF: ThreadGrade does bloom + grade + dither, R1 option B) + AudioListener + Audio.AudioDirector
//   Sun         directional light, realtime, soft shadows, strength 1, biases from the pipeline asset
//   World       NearRoot, GroundGroup (identity)
//   PathGuide   Player.PathGuide (corridor colliders on layer 30 at runtime)
// RenderSettings: skybox = Sky.mat, flat black ambient (our shaders ignore Unity ambient), fog off, no reflections,
// no baked or realtime GI.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Megastructure.EditorTools
{
    public static class SceneBuilder
    {
        public static readonly string[] GameComponents =
        {
            typeof(GameBootstrap).FullName, typeof(ProgressDirector).FullName, typeof(BeatDirector).FullName,
            "Megastructure.Player.QualityBootstrap", "Megastructure.UI.FadeController", "Megastructure.QA.CaptureHarness",
            "Megastructure.QA.AutoTestRunner", "Megastructure.QA.PerfMonitor", "Megastructure.QA.WallBot",
        };
        public static readonly string[] PlayerComponents = { "Megastructure.Player.PlayerController", "Megastructure.Player.AutoWalker" };
        public static readonly string[] CameraRigComponents = { "Megastructure.Player.CameraRig", "Megastructure.Audio.AudioDirector" };
        public static readonly string[] PathGuideComponents = { "Megastructure.Player.PathGuide" };
        public static readonly string[] RootNames = { "Game", "Player", "Sun", "World", "PathGuide" };

        public static void Build(MaterialLibrary lib, ThreadPalette palette, List<string> problems)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                ThreadLog.Problem(problems, "SceneBuilder: cancelled by the user (unsaved scene)");
                return;
            }
            EditorUtil.EnsureFolder(ThreadPaths.ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene(Single) unloads unused assets: re-acquire the library and palette from disk.
            if (lib == null) lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ThreadPaths.LibraryAsset);
            if (palette == null) palette = AssetDatabase.LoadAssetAtPath<ThreadPalette>(ThreadPaths.PaletteAsset);

            // ---- Sun (directional light; ProgressDirector rotates it every frame from ThreadMath.SunDirection)
            var sunGo = new GameObject("Sun");
            D3 s0 = ThreadMath.SunDirection(0);
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(-(float)s0.x, -(float)s0.y, -(float)s0.z), Vector3.up);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.intensity = 1f;
            sun.color = Color.white;          // shaders read the sun colour from _Th globals (ProgressDirector)
            sun.useColorTemperature = false;
            sun.cullingMask = ~0;
            var sunData = sun.GetUniversalAdditionalLightData();
            sunData.usePipelineSettings = true;   // depth / normal bias from PC_RPAsset (14.2.4)
            sunData.softShadowQuality = SoftShadowQuality.UsePipelineSettings;

            // ---- World roots
            var world = new GameObject("World");
            var nearRoot = new GameObject("NearRoot");
            nearRoot.transform.SetParent(world.transform, false);
            var groundGroup = new GameObject("GroundGroup");
            groundGroup.transform.SetParent(world.transform, false);

            // ---- Player and camera rig
            var player = new GameObject("Player");
            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(player.transform, false);
            rig.transform.localPosition = new Vector3(0f, ThreadConstants.EyeHeight, 0f);
            rig.tag = "MainCamera";
            var cam = rig.AddComponent<Camera>();
            cam.nearClipPlane = ThreadConstants.CameraNear;
            cam.farClipPlane = ThreadConstants.CameraFar;
            cam.fieldOfView = (float)ThreadMath.Fov(0);
            cam.allowHDR = true;
            cam.allowMSAA = true;                  // sample count comes from the URP asset (4x)
            cam.allowDynamicResolution = false;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.useOcclusionCulling = false;       // no baked occlusion data in a procedural world
            cam.depth = 0f;
            var acd = cam.GetUniversalAdditionalCameraData();
            acd.renderType = CameraRenderType.Base;
            acd.renderPostProcessing = false;      // R1 option B
            acd.antialiasing = AntialiasingMode.None;
            acd.dithering = false;                 // exactly one dither: ThreadGrade's (8.2, 14.7.1)
            acd.renderShadows = true;
            acd.stopNaN = false;
            acd.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
            acd.requiresColorOption = CameraOverrideOption.UsePipelineSettings;
            acd.allowXRRendering = false;
            acd.allowHDROutput = false;
            rig.AddComponent<AudioListener>();
            foreach (var n in CameraRigComponents) EditorUtil.AddComponentByName(rig, n, problems);
            foreach (var n in PlayerComponents) EditorUtil.AddComponentByName(player, n, problems);

            // ---- Path guide
            var pathGuide = new GameObject("PathGuide");
            foreach (var n in PathGuideComponents) EditorUtil.AddComponentByName(pathGuide, n, problems);

            // ---- Game systems (last, so serialized references can point at everything above)
            var game = new GameObject("Game");
            Component bootstrap = null;
            foreach (var n in GameComponents)
            {
                var c = EditorUtil.AddComponentByName(game, n, problems);
                if (n == typeof(GameBootstrap).FullName) bootstrap = c;
            }
            if (bootstrap != null)
            {
                var so = new SerializedObject(bootstrap);
                SetRef(so, "materials", lib, problems);
                SetRef(so, "palette", palette, problems);
                SetRef(so, "nearRoot", nearRoot.transform, problems);
                SetRef(so, "groundGroup", groundGroup.transform, problems);
                SetRef(so, "sun", sun, problems);
                SetRef(so, "mainCamera", cam, problems);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Root order in the hierarchy: Game, Player, Sun, World, PathGuide.
            game.transform.SetSiblingIndex(0);
            player.transform.SetSiblingIndex(1);
            sunGo.transform.SetSiblingIndex(2);
            world.transform.SetSiblingIndex(3);
            pathGuide.transform.SetSiblingIndex(4);

            ConfigureRenderSettings(lib, sun, problems);
            RemoveUnexpectedRoots(scene);

            if (!EditorSceneManager.SaveScene(scene, ThreadPaths.MainScene, false))
                ThreadLog.Problem(problems, "SceneBuilder: SaveScene failed for " + ThreadPaths.MainScene);
            else
                ThreadLog.Info($"SceneBuilder: wrote {ThreadPaths.MainScene} ({scene.rootCount} roots)");
        }

        static void SetRef(SerializedObject so, string field, Object value, List<string> problems)
        {
            var p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference)
            {
                ThreadLog.Problem(problems, $"SceneBuilder: {so.targetObject.GetType().Name}.{field} is not a serialized object field");
                return;
            }
            if (value == null) ThreadLog.Problem(problems, $"SceneBuilder: {so.targetObject.GetType().Name}.{field} has no value to assign");
            p.objectReferenceValue = value;
        }

        static void ConfigureRenderSettings(MaterialLibrary lib, Light sun, List<string> problems)
        {
            var sky = lib != null ? lib.Sky : null;
            if (sky == null) ThreadLog.Problem(problems, "SceneBuilder: Sky material missing, RenderSettings.skybox left empty");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.fog = false;                                   // 8.4: all fog comes from ThreadFogApply
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflection = null;                       // no sky cubemap capture (shaders ignore it)
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.reflectionBounces = 1;

            // No lightmaps, no realtime GI (8.3): a LightingSettings asset shared by the scene.
            EditorUtil.EnsureFolder(ThreadPaths.SettingsDir);
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(ThreadPaths.LightingAsset);
            if (ls == null)
            {
                ls = new LightingSettings { name = "ThreadLighting" };
                AssetDatabase.CreateAsset(ls, ThreadPaths.LightingAsset);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            ls.autoGenerate = false;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;
        }

        /// <summary>The scene contract is exact: objects created by edit-mode callbacks of components are removed.</summary>
        static void RemoveUnexpectedRoots(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                if (System.Array.IndexOf(RootNames, r.name) >= 0) continue;
                if ((r.hideFlags & HideFlags.DontSave) != 0) continue;
                ThreadLog.Warn($"SceneBuilder: removing unexpected root object '{r.name}' (created in edit mode by a component?)");
                Object.DestroyImmediate(r);
            }
        }
    }
}
