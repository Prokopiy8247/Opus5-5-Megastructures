using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// RebuildAll step h: writes Assets/_Game/Scenes/Main.unity from scratch. The scene only holds the bootstrap objects
    /// (ADR-001); runtime components are added BY TYPE NAME from the Megastructure.Runtime assembly so the scene can be
    /// rebuilt while builders are still writing them (a missing type is a warning), and serialized fields are wired
    /// BY NAME when they exist.
    /// </summary>
    public static class SceneFactory
    {
        public const string RuntimeAssembly = "Megastructure.Runtime";
        public const string LightingSettingsPath = BatchTools.SettingsDir + "/RingLighting.lighting";

        const float SkinWidth = 0.03f;                    // task BA (about 10 % of the 0.35 m radius, Unity guidance)
        const float MinMoveDistance = 0f;                 // task BA
        const float StarIntensity = 3.0f;                 // 14.2.3 beats 1-2: key 3.0
        const float ArchFillIntensity = 0.55f;            // 14.2.3 beat 2 arch fill
        const float ArchFillAz = 112f, ArchFillEl = 74f;  // 14.2.3 arch fill az / el
        const float RimFillIntensity = 0f;                // 14.2.3: RimFill wash only in beat 5 (driven at runtime)
        const float RimFillAz = -90f, RimFillEl = 30f;    // 14.2.3 beat 5 RimFill wash az / el
        const float LightShadowBias = 0.04f;              // 14.2.3 depth bias
        const float LightShadowNormalBias = 0.8f;         // 14.2.3 normal bias

        static readonly Dictionary<string, Type> s_TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        public static void BuildMainScene(RingAssets assets, PanelSettings panel, VolumeProfile profile)
        {
            s_TypeCache.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gameGo = new GameObject("[Game]");
            var playerGo = new GameObject("[Player]");
            var lightsGo = new GameObject("[Lights]");
            var volumeGo = new GameObject("[Volume]");

            // ---- [Lights]
            var star = MakeDirectional(lightsGo.transform, "Star", StarIntensity, Srgb("KeyLight"), true, RingConstants.StarL);
            var archFill = MakeDirectional(lightsGo.transform, "ArchFill", ArchFillIntensity, Srgb("ArchFill"), false, RingConstants.DirFromAzEl(ArchFillAz, ArchFillEl));
            var rimFill = MakeDirectional(lightsGo.transform, "RimFill", RimFillIntensity, Srgb("RimFill"), false, RingConstants.DirFromAzEl(RimFillAz, RimFillEl));

            // ---- [Volume]
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            if (profile == null) BatchLog.Error("[Volume]: RingVolume profile is null");

            // ---- [Player]
            playerGo.transform.position = new Vector3(0f, RingConstants.CapsuleFloorY, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = RingConstants.PlayerRadius;
            cc.height = RingConstants.PlayerHeight;
            cc.center = new Vector3(0f, RingConstants.PlayerHeight * 0.5f, 0f);
            cc.stepOffset = RingConstants.StepOffset;
            cc.slopeLimit = RingConstants.SlopeLimit;
            cc.skinWidth = SkinWidth;
            cc.minMoveDistance = MinMoveDistance;
            AddByName(playerGo, "PlayerController");
            AddByName(playerGo, "AutoWalker");

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, RingConstants.Eye, 0f);
            camGo.transform.localRotation = Quaternion.identity;
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = RingConstants.Vfov[0];
            cam.nearClipPlane = RingConstants.NearPlane;
            cam.farClipPlane = RingConstants.FarDefault;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 1f);
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.allowDynamicResolution = false;
            cam.depth = 0f;
            var acd = cam.GetUniversalAdditionalCameraData();
            acd.renderType = CameraRenderType.Base;
            acd.renderPostProcessing = true;
            acd.dithering = false;                                   // H5: the FinalDither pass is the only dither
            acd.antialiasing = AntialiasingMode.None;                // MSAA 4x comes from the pipeline asset
            acd.stopNaN = true;
            acd.renderShadows = true;
            acd.requiresDepthTexture = false;
            acd.requiresColorTexture = false;
            acd.allowHDROutput = false;
            AddByName(camGo, "CameraRig");
            camGo.AddComponent<AudioListener>();

            // research 05 F11: the OnAudioFilterRead generator must not share a GameObject with the AudioListener
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(playerGo.transform, false);
            audioGo.transform.localPosition = new Vector3(0f, RingConstants.Eye, 0f);
            var src = audioGo.AddComponent<AudioSource>();
            src.clip = null;
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            var audioDirector = AddByName(audioGo, "AudioDirector");
            var reverb = audioGo.GetComponent<AudioReverbFilter>();
            if (reverb == null) reverb = audioGo.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.User;            // research 05: User preset, fields driven by AudioDirector
            EnsureOrder(audioGo, src, audioDirector, reverb);

            // ---- [Game]
            var boot = AddByName(gameGo, "GameBootstrap");
            Wire(boot, "assets", assets);
            AddByName(gameGo, "QualityBootstrap");
            AddByName(gameGo, "WorldBuilder");
            AddByName(gameGo, "ProgressDirector");
            AddByName(gameGo, "BeatDirector");
            var atmo = AddByName(gameGo, "AtmosphereDriver");
            Wire(atmo, "key", star);
            Wire(atmo, "archFill", archFill);
            Wire(atmo, "rimFill", rimFill);
            Wire(atmo, "volume", volume);
            Wire(atmo, "cam", cam);
            AddByName(gameGo, "PerfMonitor");
            AddByName(gameGo, "CaptureHarness");
            var doc = gameGo.GetComponent<UIDocument>();
            if (doc == null) doc = gameGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            if (panel == null) BatchLog.Error("[Game] UIDocument: RingPanel PanelSettings is null");
            var ui = AddByName(gameGo, "UiDirector");
            Wire(ui, "document", doc);

            // ---- environment (Part 8.3, 8.4, 14.7 H10): no Unity fog, no skybox, no probe reflections, nothing baked
            RenderSettings.sun = star;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.fog = false;
            var lighting = EditorIO.LoadOrCreateAsset(LightingSettingsPath, () => new LightingSettings { name = "RingLighting" });
            lighting.bakedGI = false;      // nothing is baked (Part 8.3); LightingSettings.autoGenerate is obsolete in 6000.6
            lighting.realtimeGI = false;
            EditorUtility.SetDirty(lighting);
            Lightmapping.lightingSettings = lighting;

            ReportUnwired(gameGo, playerGo, camGo, audioGo);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, BatchTools.ScenePath))
                throw new InvalidOperationException("could not save " + BatchTools.ScenePath);
            BatchLog.Note(BatchTools.ScenePath + " saved: [Game] " + Describe(gameGo) + "; [Player] " + Describe(playerGo) + " / Camera " + Describe(camGo)
                          + " / Audio " + Describe(audioGo) + "; [Lights] Star, ArchFill, RimFill; [Volume] global RingVolume");
        }

        // ---------------------------------------------------------------------------------------------- lights
        static Light MakeDirectional(Transform parent, string name, float intensity, Color srgb, bool shadows, Vector3 towardLight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(-towardLight.normalized, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.useColorTemperature = false;
            l.color = srgb;   // Light colours are gamma-space; URP linearises them (lightsUseLinearIntensity in a linear project)
            l.intensity = intensity;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 1f;
            l.shadowBias = LightShadowBias;
            l.shadowNormalBias = LightShadowNormalBias;
            l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.FromQualitySettings;
            var data = l.GetUniversalAdditionalLightData();
            data.usePipelineSettings = true;   // depth / normal bias from the pipeline asset (same values)
            return l;
        }

        static Color Srgb(string token)
        {
            foreach (var e in RingPalette.Entries)
                if (e.name == token) return RingPalette.HexToSrgb(e.hex);
            BatchLog.Error("palette token '" + token + "' not found");
            return Color.white;
        }

        // ---------------------------------------------------------------------------------------------- components
        /// <summary>Megastructure.Runtime component type by simple name (namespace Megastructure preferred), or null.</summary>
        public static Type FindRuntimeType(string name)
        {
            if (s_TypeCache.TryGetValue(name, out var cached)) return cached;
            Type best = null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (t.Name != name || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                if (t.Assembly.GetName().Name != RuntimeAssembly) continue;
                if (best == null || t.Namespace == "Megastructure") best = t;
            }
            s_TypeCache[name] = best;
            return best;
        }

        static Component AddByName(GameObject go, string typeName)
        {
            var t = FindRuntimeType(typeName);
            if (t == null)
            {
                BatchLog.Warn(go.name + ": component type '" + typeName + "' does not exist in " + RuntimeAssembly + " yet (skipped)");
                return null;
            }
            var existing = go.GetComponent(t);
            if (existing != null) return existing;
            var c = go.AddComponent(t);
            if (c == null) BatchLog.Error(go.name + ": AddComponent(" + t.FullName + ") failed");
            return c;
        }

        /// <summary>Assigns an object reference to a serialized field by name, if the component has that field.</summary>
        static void Wire(Component c, string field, Object value)
        {
            if (c == null) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            string ctx = c.GetType().Name + "." + field;
            if (p == null) { BatchLog.Info(ctx + ": no such serialized field (not wired)"); return; }
            if (p.propertyType != SerializedPropertyType.ObjectReference) { BatchLog.Warn(ctx + ": is a " + p.propertyType + ", not an object reference (not wired)"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();
            var check = so.FindProperty(field).objectReferenceValue;
            if (value != null && check != value) BatchLog.Warn(ctx + ": type does not accept " + value.GetType().Name + " (left " + (check != null ? check.name : "null") + ")");
            else BatchLog.Info(ctx + " = " + (value != null ? value.GetType().Name + " '" + value.name + "'" : "null"));
        }

        /// <summary>Lists top-level object-reference fields that are still null, for the lead to review.</summary>
        static void ReportUnwired(params GameObject[] gos)
        {
            foreach (var go in gos)
            {
                foreach (var c in go.GetComponents<MonoBehaviour>())
                {
                    if (c == null) continue;
                    var so = new SerializedObject(c);
                    var it = so.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                        if (it.objectReferenceValue == null) BatchLog.Info("unwired (null) " + go.name + "/" + c.GetType().Name + "." + it.name);
                    }
                }
            }
        }

        /// <summary>Moves components so they appear in the given order (component order = audio processing order).</summary>
        static void EnsureOrder(GameObject go, params Component[] order)
        {
            var seq = order.Where(c => c != null).ToArray();
            for (int pass = 0; pass < 16; pass++)
            {
                bool moved = false;
                for (int k = 1; k < seq.Length; k++)
                {
                    if (IndexOf(go, seq[k]) < IndexOf(go, seq[k - 1]))
                    {
                        UnityEditorInternal.ComponentUtility.MoveComponentUp(seq[k - 1]);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            for (int k = 1; k < seq.Length; k++)
                if (IndexOf(go, seq[k]) < IndexOf(go, seq[k - 1]))
                    BatchLog.Error(go.name + ": component order " + seq[k - 1].GetType().Name + " before " + seq[k].GetType().Name + " could not be enforced");
        }

        static int IndexOf(GameObject go, Component c) => Array.IndexOf(go.GetComponents<Component>(), c);

        static string Describe(GameObject go) =>
            string.Join(" + ", go.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
    }
}
