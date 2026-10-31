// PipelineConfigurator: URP asset, renderer, renderer features, default pipeline and the single quality level
// (MASTER_PROMPT 8.1, 8.5, 14.2.4, 14.7.1 items 1-2, 14.7.4; research R1 section 5, R2 1.6-1.8).
//   PC_RPAsset:  HDR on, 64-bit colour buffer (R16G16B16A16), MSAA 4x, render scale 1, depth texture on (halo soft
//                fade), opaque texture off, main-light shadows 4096 / 4 cascades / 160 m / bias 1.0 / normal 1.2,
//                soft shadows high, SRP Batcher on, GPU Resident Drawer off (14.7.9: not needed).
//   PC_Renderer: Forward+, depth priming disabled (ignored under MSAA anyway, R1 pitfall 13), depth copy after
//                opaques (R2 1.6), float depth formats (Default = D32_SFloat_S8_UInt on desktop, R2 1.7),
//                ThreadGradeFeature with material = Grade.mat, SSAO removed (flat look: AO is vertex-baked, 14.2.2).
//   GraphicsSettings.defaultRenderPipeline = PC_RPAsset; QualitySettings: one level "PC" using PC_RPAsset, vSync 1.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    public static class PipelineConfigurator
    {
        public const string GradeFeatureType = "Megastructure.Rendering.ThreadGradeFeature";
        public const string GradeFeatureMaterialField = "material";
        public const string QualityLevelName = "PC";

        // 14.2.4 (line 475): max shadow distance 160 m, depth bias 1.0, normal bias 1.2, resolution 4096, 4 cascades.
        public const float ShadowDistance = 160f;
        public const float ShadowDepthBias = 1.0f;
        public const float ShadowNormalBias = 1.2f;
        public const int ShadowResolution = 4096;
        public const int ShadowCascades = 4;
        // Cascade splits as fractions of the distance: 10.7 / 32 / 74.7 / 160 m (Unity's classic 4-cascade ratios).
        // Cascade 0 (radius ~10.7 m, 2048 texels per tile) gives ~1 cm texels for rails, posts and treads.
        public static readonly Vector3 CascadeSplits = new Vector3(0.0667f, 0.2f, 0.4667f);
        // Fade over the last 20 percent of the last cascade so the shadow never ends on a hard line (8.5).
        public const float CascadeBorder = 0.2f;
        /// <summary>SSAO adds grey contact gradients and halos to the flat style (Part 8.2); AO is baked in vertices.</summary>
        public const bool KeepSsao = false;

        public static void Apply(Material grade, List<string> problems)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ThreadPaths.RpAsset);
            if (asset == null) { ThreadLog.Problem(problems, "Pipeline: " + ThreadPaths.RpAsset + " not found"); return; }
            ConfigureAsset(asset);

            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ThreadPaths.RendererAsset);
            if (rd == null) { ThreadLog.Problem(problems, "Pipeline: " + ThreadPaths.RendererAsset + " not found"); }
            else
            {
                ConfigureRenderer(rd);
                if (!KeepSsao) RemoveFeatures(rd, "ScreenSpaceAmbientOcclusion");
                EnsureGradeFeature(rd, grade, problems);
                EnsureRendererIsUsed(asset, rd, problems);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            ConfigureQuality(asset, problems);
            AssetDatabase.SaveAssets();
            ThreadLog.Info($"Pipeline: HDR={asset.supportsHDR} precision={asset.hdrColorBufferPrecision} MSAA={asset.msaaSampleCount} " +
                           $"depthTex={asset.supportsCameraDepthTexture} opaqueTex={asset.supportsCameraOpaqueTexture} shadows={asset.mainLightShadowmapResolution}/" +
                           $"{asset.shadowCascadeCount} casc/{asset.shadowDistance} m bias {asset.shadowDepthBias}/{asset.shadowNormalBias} " +
                           $"GRD={asset.gpuResidentDrawerMode} SRPBatcher={asset.useSRPBatcher}" +
                           (rd != null ? $" mode={rd.renderingMode} priming={rd.depthPrimingMode} copyDepth={rd.copyDepthMode} features={rd.rendererFeatures.Count}" : ""));
        }

        static void ConfigureAsset(UniversalRenderPipelineAsset asset)
        {
            asset.supportsHDR = true;
            asset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;    // 14.7.1 item 1
            asset.msaaSampleCount = ThreadConstants.Msaa;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = true;                             // halo soft depth fade
            asset.supportsCameraOpaqueTexture = false;
            asset.mainLightShadowmapResolution = ShadowResolution;
            asset.shadowDistance = ShadowDistance;
            asset.shadowCascadeCount = ShadowCascades;
            asset.cascade4Split = CascadeSplits;
            asset.cascadeBorder = CascadeBorder;
            asset.shadowDepthBias = ShadowDepthBias;
            asset.shadowNormalBias = ShadowNormalBias;
            asset.useSRPBatcher = true;
            asset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
            asset.useAdaptivePerformance = false;
            asset.conservativeEnclosingSphere = true;                            // tighter, still stable cascade spheres
            // The template points the asset at SampleSceneProfile (bloom, vignette, tonemapping, motion blur), which
            // TemplateCleanup must remove; URP post is off on our camera, so the kept DefaultVolumeProfile is enough.
            asset.volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ThreadPaths.DefaultVolumeProfile);

            // Internal setters (R1 section 5): through the serialized fields.
            var so = new SerializedObject(asset);
            SetInt(so, "m_MainLightRenderingMode", (int)LightRenderingMode.PerPixel);
            SetBool(so, "m_MainLightShadowsSupported", true);
            // Additional lights stay per-pixel (no local lights exist, 14.2.4; Forward+ sets _CLUSTER_LIGHT_LOOP regardless),
            // but they never cast shadows.
            SetInt(so, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            SetBool(so, "m_AnyShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", (int)SoftShadowQuality.High);
            SetBool(so, "m_RequireOpaqueTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureRenderer(UniversalRendererData rd)
        {
            rd.renderingMode = RenderingMode.ForwardPlus;
            rd.depthPrimingMode = DepthPrimingMode.Disabled;
            rd.copyDepthMode = CopyDepthMode.AfterOpaques;
            rd.depthAttachmentFormat = DepthFormat.Default;   // D32_SFloat_S8_UInt on desktop (reversed-Z float, 14.7.4)
            rd.depthTextureFormat = DepthFormat.Default;
            EditorUtility.SetDirty(rd);
        }

        /// <summary>The URP asset must render with PC_Renderer as its default (index 0) renderer.</summary>
        static void EnsureRendererIsUsed(UniversalRenderPipelineAsset asset, UniversalRendererData rd, List<string> problems)
        {
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            var def = so.FindProperty("m_DefaultRendererIndex");
            if (list == null || def == null) { ThreadLog.Problem(problems, "Pipeline: cannot read m_RendererDataList of the URP asset"); return; }
            int idx = -1;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == rd) idx = i;
            if (idx < 0)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = rd;
                idx = list.arraySize - 1;
            }
            def.intValue = idx;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Removes renderer features by class name (list and local-file-id map kept in sync, R1 section 5).</summary>
        public static void RemoveFeatures(ScriptableRendererData rd, string typeName)
        {
            var so = new SerializedObject(rd);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            var doomed = new List<Object>();
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var obj = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj == null || obj.GetType().Name != typeName) continue;
                doomed.Add(obj);
                int before = list.arraySize;
                list.DeleteArrayElementAtIndex(i);
                if (list.arraySize == before) list.DeleteArrayElementAtIndex(i);   // older behaviour: first call only nulls the slot
                if (map != null && i < map.arraySize) map.DeleteArrayElementAtIndex(i);
            }
            if (doomed.Count == 0) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var o in doomed)
            {
                ThreadLog.Info($"Pipeline: removed renderer feature {o.name} ({typeName})");
                Object.DestroyImmediate(o, true);   // the sub-asset of PC_Renderer.asset
            }
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            AssetDatabase.SaveAssetIfDirty(rd);
        }

        /// <summary>Adds Megastructure.Rendering.ThreadGradeFeature (resolved by name) or keeps the existing one; sets its material.</summary>
        static void EnsureGradeFeature(UniversalRendererData rd, Material grade, List<string> problems)
        {
            var type = EditorUtil.FindType(GradeFeatureType);
            if (type == null || !typeof(ScriptableRendererFeature).IsAssignableFrom(type))
            {
                ThreadLog.Problem(problems, $"Pipeline: renderer feature type {GradeFeatureType} not found (B03 not compiled yet?)");
                return;
            }
            ScriptableRendererFeature feature = null;
            foreach (var f in rd.rendererFeatures) if (f != null && f.GetType() == type) { feature = f; break; }

            bool created = feature == null;
            if (created)
            {
                feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = type.Name;
                AssetDatabase.AddObjectToAsset(feature, rd);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var so = new SerializedObject(rd);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                if (map != null)
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (grade == null) ThreadLog.Problem(problems, "Pipeline: Grade.mat missing, ThreadGradeFeature.material left unset");
            else if (!SetObjectField(feature, GradeFeatureMaterialField, grade))
                ThreadLog.Problem(problems, $"Pipeline: {GradeFeatureType} has no serialized field '{GradeFeatureMaterialField}' of type Material");
            // Optional shader reference (keeps the grade shader in the player even without the material, R1 pitfall 12).
            if (grade != null && grade.shader != null)
            {
                SetObjectField(feature, "m_Shader", grade.shader, quiet: true);
                SetObjectField(feature, "shader", grade.shader, quiet: true);
            }
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            AssetDatabase.SaveAssetIfDirty(rd);
            ThreadLog.Info($"Pipeline: {(created ? "added" : "kept")} {type.Name} on {rd.name} (material {(grade != null ? grade.name : "none")})");
        }

        /// <summary>Sets a serialized object-reference field (SerializedObject first, reflection as fallback).</summary>
        static bool SetObjectField(Object target, string fieldName, Object value, bool quiet = false)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(fieldName);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (p.objectReferenceValue == value) return true;
            }
            var fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && value != null && fi.FieldType.IsAssignableFrom(value.GetType()))
            {
                fi.SetValue(target, value);
                return true;
            }
            if (!quiet) ThreadLog.Warn($"field {fieldName} not found on {target.GetType().Name}");
            return false;
        }

        /// <summary>One quality level "PC" using PC_RPAsset, vSync 1 (Part 8.1); every other level is removed.</summary>
        static void ConfigureQuality(UniversalRenderPipelineAsset asset, List<string> problems)
        {
            var qs = EditorUtil.LoadProjectSettingsObject("ProjectSettings/QualitySettings.asset");
            if (qs == null) { ThreadLog.Problem(problems, "Pipeline: cannot load ProjectSettings/QualitySettings.asset"); return; }
            var so = new SerializedObject(qs);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null) { ThreadLog.Problem(problems, "Pipeline: m_QualitySettings not found"); return; }

            int keep = -1;
            for (int i = 0; i < levels.arraySize; i++)
            {
                var name = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (name != null && name.stringValue == QualityLevelName) { keep = i; break; }
            }
            if (keep < 0) keep = 0;
            for (int i = levels.arraySize - 1; i >= 0; i--)
            {
                if (i == keep) continue;
                var name = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                ThreadLog.Info("Pipeline: removing quality level " + (name != null ? name.stringValue : i.ToString()));
                levels.DeleteArrayElementAtIndex(i);
            }
            var level = levels.GetArrayElementAtIndex(0);
            var lname = level.FindPropertyRelative("name");
            if (lname != null) lname.stringValue = QualityLevelName;
            var rp = level.FindPropertyRelative("customRenderPipeline");
            if (rp != null) rp.objectReferenceValue = asset;
            var vs = level.FindPropertyRelative("vSyncCount");
            if (vs != null) vs.intValue = 1;
            var excl = level.FindPropertyRelative("excludedTargetPlatforms");
            if (excl != null && excl.isArray)
            {
                // The level must be available on Standalone (the Mobile level excluded it, the PC level excluded phones).
                for (int i = excl.arraySize - 1; i >= 0; i--)
                    if (excl.GetArrayElementAtIndex(i).stringValue == "Standalone") excl.DeleteArrayElementAtIndex(i);
            }

            var cur = so.FindProperty("m_CurrentQuality");
            if (cur != null) cur.intValue = 0;
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null) ZeroAllInts(perPlatform);
            so.ApplyModifiedPropertiesWithoutUndo();

            QualitySettings.SetQualityLevel(0, true);
            QualitySettings.vSyncCount = 1;
            if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            ThreadLog.Info($"Pipeline: quality levels = [{string.Join(", ", QualitySettings.names)}], vSync {QualitySettings.vSyncCount}");
        }

        /// <summary>Sets every int child (the "second" of each map entry) of m_PerPlatformDefaultQuality to 0.</summary>
        static void ZeroAllInts(SerializedProperty root)
        {
            var it = root.Copy();
            var end = root.GetEndProperty();
            bool enter = true;
            while (it.Next(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = true;
                if (it.propertyType == SerializedPropertyType.Integer && it.name != "size") it.intValue = 0;
            }
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = v; else ThreadLog.Warn("Pipeline: serialized field " + name + " not found");
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) { ThreadLog.Warn("Pipeline: serialized field " + name + " not found"); return; }
            p.intValue = v;   // for an enum field intValue is the underlying value (enumValueIndex would be the declaration index)
        }
    }
}
