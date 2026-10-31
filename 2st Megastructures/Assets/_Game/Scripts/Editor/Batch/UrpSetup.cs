using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// URP 17.6 assets (Part 8.1, 8.2, 8.5, 14.2.3, 14.2.5): the pipeline asset, the Forward+ renderer with the
    /// FinalDither full-screen feature, the saved global volume profile, and the neutralised default profile.
    /// Internal URP setters are written through SerializedObject with the field names of
    /// Runtime/Data/UniversalRenderPipelineAsset.cs (research 02 section 1a).
    /// </summary>
    public static class UrpSetup
    {
        public const string AssetPath = BatchTools.SettingsDir + "/RingURP.asset";
        public const string RendererPath = BatchTools.SettingsDir + "/RingURP_Renderer.asset";
        public const string VolumePath = BatchTools.SettingsDir + "/RingVolume.asset";
        public const string FinalDitherFeatureName = "FinalDither";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        const string TemplateDefaultVolumeProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        public const int MsaaSamples = 4;                 // 14.2.5 MSAA 4x
        public const float ShadowDistance = 220f;         // 14.2.3: only objects within 220 m cast shadows
        public const int ShadowCascades = 4;              // Part 8.5, 14.2.3
        public static readonly Vector3 Cascade4Split = new Vector3(0.06f, 0.18f, 0.45f);   // 13 / 40 / 99 / 220 m
        const int MainLightShadowmapResolution = 4096;    // 14.2.3
        const float ShadowDepthBias = 0.04f;              // 14.2.3 beat 2
        const float ShadowNormalBias = 0.8f;              // 14.2.3 beat 2
        const int LightRenderingPerPixel = 1;             // URP LightRenderingMode.PerPixel (values are not sequential)
        const int SoftShadowQualityHigh = 3;              // URP SoftShadowQuality.High
        // Grading mode: LDR keeps tonemapping analytic in UberPost (_TONEMAP_NEUTRAL, research 02 section 4). Not in the
        // spec; switch to HighDynamicRange here if captures prefer HDR grading (research 08 5.3 item 6: LUT clip ~58.9).
        const ColorGradingMode GradingMode = ColorGradingMode.LowDynamicRange;

        // 14.2.5 beat-2 values (the AtmosphereDriver animates them at runtime on its instanced copy)
        const float BloomThreshold = 1.0f, BloomIntensity = 0.18f, BloomScatter = 0.7f, BloomClamp = 40f;
        const float PostExposure = 0f, Contrast = 8f, Saturation = 10f, WbTemperature = 10f;
        const float VignetteIntensity = 0.12f, VignetteSmoothness = 0.4f;

        public struct PipelineAssets
        {
            public UniversalRenderPipelineAsset Asset;
            public UniversalRendererData Renderer;
        }

        // ---------------------------------------------------------------------------------------------- pipeline
        public static PipelineAssets CreateOrUpdatePipeline()
        {
            var rd = EditorIO.LoadOrCreateAsset(RendererPath, () => ScriptableObject.CreateInstance<UniversalRendererData>());
            var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (ppd == null) BatchLog.Error("PostProcessData not found at " + PostProcessDataPath + ": post-processing would be disabled");
            rd.postProcessData = ppd;
            rd.renderingMode = RenderingMode.ForwardPlus;
            rd.depthPrimingMode = DepthPrimingMode.Disabled;
            rd.depthAttachmentFormat = DepthFormat.Depth_32_Stencil_8;   // D32_SFloat_S8_UInt (14.12 camera row, H3)
            rd.depthTextureFormat = DepthFormat.Depth_32_Stencil_8;
            rd.intermediateTextureMode = IntermediateTextureMode.Auto;
            RemoveUnwantedFeatures(rd);
            rd.SetDirty();
            EditorUtility.SetDirty(rd);

            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (rp == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null) BatchLog.Warn("replacing a non-URP asset at " + AssetPath);
                rp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(rp, AssetPath);
                BatchLog.Note("created " + AssetPath);
            }

            rp.supportsHDR = true;
            rp.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;   // R16G16B16A16_SFloat (H5)
            rp.msaaSampleCount = MsaaSamples;
            rp.renderScale = 1f;
            rp.upscalingFilter = UpscalingFilterSelection.Auto;
            rp.useSRPBatcher = true;
            rp.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
            rp.mainLightShadowmapResolution = MainLightShadowmapResolution;
            rp.shadowCascadeCount = ShadowCascades;
            rp.cascade4Split = Cascade4Split;
            rp.shadowDistance = ShadowDistance;
            rp.shadowDepthBias = ShadowDepthBias;
            rp.shadowNormalBias = ShadowNormalBias;
            rp.supportsCameraDepthTexture = false;
            rp.supportsCameraOpaqueTexture = false;
            rp.volumeProfile = null;
            rp.colorGradingMode = GradingMode;

            var so = new SerializedObject(rp);
            const string ctx = "RingURP";
            var list = SerializedUtil.Find(so, "m_RendererDataList", ctx);
            if (list != null)
            {
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = rd;
            }
            SerializedUtil.SetInt(SerializedUtil.Find(so, "m_DefaultRendererIndex", ctx), 0);
            SerializedUtil.SetInt(SerializedUtil.Find(so, "m_MainLightRenderingMode", ctx), LightRenderingPerPixel);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_MainLightShadowsSupported", ctx), true);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_AnyShadowsSupported", ctx), true);   // editor-only, drives _SHADOWS_SOFT stripping
            SerializedUtil.SetInt(SerializedUtil.Find(so, "m_AdditionalLightsRenderingMode", ctx), LightRenderingPerPixel);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_AdditionalLightShadowsSupported", ctx), false);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_SoftShadowsSupported", ctx), true);
            SerializedUtil.SetInt(SerializedUtil.Find(so, "m_SoftShadowQuality", ctx), SoftShadowQualityHigh);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_EnableLODCrossFade", ctx), false);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_SupportsDynamicBatching", ctx), false);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_MixedLightingSupported", ctx), false);   // nothing is baked (Part 8.3)
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_ReflectionProbeBlending", ctx), false);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_ReflectionProbeBoxProjection", ctx), false);
            SerializedUtil.SetBool(SerializedUtil.Find(so, "m_ConservativeEnclosingSphere", ctx), true);
            SerializedUtil.SetObject(SerializedUtil.Find(so, "m_VolumeProfile", ctx), null);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);

            BatchLog.Note(AssetPath + ": HDR 64-bit, MSAA " + MsaaSamples + "x, scale 1, SRP batcher, GRD off, main light per-pixel + shadows "
                          + MainLightShadowmapResolution + ", " + ShadowCascades + " cascades " + Cascade4Split + " of " + ShadowDistance
                          + " m, soft High, bias " + ShadowDepthBias + "/" + ShadowNormalBias + ", additional per-pixel no shadows, depth/opaque textures off, LOD cross-fade off, grading " + GradingMode);
            BatchLog.Note(RendererPath + ": Forward+, depth priming off, depth D32_S8, PostProcessData " + (ppd != null ? "set" : "MISSING") + ", features " + rd.rendererFeatures.Count);
            return new PipelineAssets { Asset = rp, Renderer = rd };
        }

        /// <summary>Removes every renderer feature except one FullScreenPassRendererFeature named FinalDither (no SSAO).</summary>
        static void RemoveUnwantedFeatures(UniversalRendererData rd)
        {
            var so = new SerializedObject(rd);
            var list = SerializedUtil.Find(so, "m_RendererFeatures", "RingURP_Renderer");
            var map = SerializedUtil.Find(so, "m_RendererFeatureMap", "RingURP_Renderer");
            if (list == null || map == null) return;
            bool keptDither = false;   // iterating from the end keeps the last FinalDither and drops duplicates
            var toDestroy = new List<Object>();
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var f = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                bool keep = f is FullScreenPassRendererFeature && f.name == FinalDitherFeatureName && !keptDither;
                if (keep) { keptDither = true; continue; }
                BatchLog.Note("renderer feature removed: " + (f != null ? f.GetType().Name + " '" + f.name + "'" : "(missing)"));
                SerializedUtil.RemoveAt(list, i);
                if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                if (f != null) toDestroy.Add(f);
            }
            while (map.arraySize > list.arraySize) map.DeleteArrayElementAtIndex(map.arraySize - 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var f in toDestroy)
            {
                if (AssetDatabase.IsSubAsset(f)) AssetDatabase.RemoveObjectFromAsset(f);
                Object.DestroyImmediate(f, true);
            }
        }

        /// <summary>Creates or updates the FinalDither feature (after the materials exist, RebuildAll step e).</summary>
        public static void ConfigureFinalDither(UniversalRendererData rd)
        {
            if (rd == null) { BatchLog.Error("ConfigureFinalDither: renderer data is null"); return; }
            string matPath = BatchTools.MaterialsDir + "/M_" + RingMat.FinalDither + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            FullScreenPassRendererFeature feature = null;
            foreach (var f in rd.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && f.name == FinalDitherFeatureName) { feature = fs; break; }
            if (feature == null) feature = AddFeature<FullScreenPassRendererFeature>(rd, FinalDitherFeatureName);

            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.passMaterial = mat;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            if (mat == null) BatchLog.Warn("FinalDither feature: " + matPath + " does not exist yet; passMaterial left null (the pass is skipped until the material exists)");
            else BatchLog.Note("FinalDither feature: AfterRenderingPostProcessing, fetchColorBuffer, material " + matPath + " (shader " + (mat.shader != null ? mat.shader.name : "null") + ")");
        }

        static T AddFeature<T>(UniversalRendererData rd, string name) where T : ScriptableRendererFeature
        {
            // Same recipe as URP/Editor/ScriptableRendererDataEditor.cs AddComponent (research 02 section 2)
            var f = ScriptableObject.CreateInstance<T>();
            f.name = name;
            f.hideFlags |= HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(f, rd);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId);
            var so = new SerializedObject(rd);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = f;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            BatchLog.Note("renderer feature added: " + typeof(T).Name + " '" + name + "' (localId " + localId + ")");
            return f;
        }

        // ---------------------------------------------------------------------------------------------- volume
        /// <summary>Saved global profile (research 09 2.4: profiles that only exist at runtime lose Bloom/Tonemapping in the player).</summary>
        public static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            var p = EditorIO.LoadOrCreateAsset(VolumePath, () => ScriptableObject.CreateInstance<VolumeProfile>());

            var tm = Get<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.Neutral);

            var bloom = Get<Bloom>(p);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.clamp.Override(BloomClamp);
            bloom.highQualityFiltering.Override(true);

            var ca = Get<ColorAdjustments>(p);
            ca.postExposure.Override(PostExposure);
            ca.contrast.Override(Contrast);
            ca.saturation.Override(Saturation);

            var wb = Get<WhiteBalance>(p);
            wb.temperature.Override(WbTemperature);

            var vg = Get<Vignette>(p);
            vg.intensity.Override(VignetteIntensity);
            vg.smoothness.Override(VignetteSmoothness);

            var allowed = new HashSet<Type> { typeof(Tonemapping), typeof(Bloom), typeof(ColorAdjustments), typeof(WhiteBalance), typeof(Vignette) };
            for (int i = p.components.Count - 1; i >= 0; i--)
            {
                var c = p.components[i];
                if (c == null) { p.components.RemoveAt(i); continue; }
                if (allowed.Contains(c.GetType())) continue;
                BatchLog.Note("volume override removed from RingVolume: " + c.GetType().Name);
                p.components.RemoveAt(i);
                if (AssetDatabase.IsSubAsset(c)) AssetDatabase.RemoveObjectFromAsset(c);
                Object.DestroyImmediate(c, true);
            }

            foreach (var c in p.components)
            {
                c.active = true;
                c.SetAllOverridesTo(true);   // every parameter overrideState true (values above are kept)
                EditorUtility.SetDirty(c);
            }
            EditorUtility.SetDirty(p);
            BatchLog.Note(VolumePath + ": Tonemapping Neutral, Bloom " + BloomThreshold + "/" + BloomIntensity + "/" + BloomScatter + "/" + BloomClamp + " HQ, ColorAdjustments EV "
                          + PostExposure + " contrast " + Contrast + " sat " + Saturation + ", WhiteBalance " + WbTemperature + ", Vignette " + VignetteIntensity + "/" + VignetteSmoothness);
            return p;
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet(out T c) && c != null)
            {
                // Re-runs: the component is normally already a sub-asset of the profile file; only attach it when it is not
                // stored anywhere (AddObjectToAsset throws for an object that already belongs to an asset).
                string pp = AssetDatabase.GetAssetPath(p);
                if (!string.IsNullOrEmpty(pp) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(c)))
                {
                    try { AssetDatabase.AddObjectToAsset(c, p); }
                    catch (UnityException e) { BatchLog.Note("volume component " + typeof(T).Name + " already stored: " + e.Message); }
                }
                return c;
            }
            return VolumeProfileFactory.CreateVolumeComponent<T>(p, true, false);
        }

        /// <summary>The URP default volume profile is the base of every volume blend: make sure none of its
        /// post-processing overrides is active (they would stack on RingVolume). ColorCurves is always "active"
        /// (identity curves) and is left alone.</summary>
        public static void NeutralizeDefaultVolumeProfile()
        {
            VolumeProfile def = null;
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPDefaultVolumeProfileSettings>(out var s) && s != null) def = s.volumeProfile;
            if (def == null) def = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TemplateDefaultVolumeProfilePath);
            if (def == null) { BatchLog.Warn("URP default volume profile not found; nothing to neutralise"); return; }

            int reset = 0, stillActive = 0;
            foreach (var c in def.components)
            {
                if (c == null || c is ColorCurves) continue;
                if (!(c is IPostProcessComponent pp) || !pp.IsActive()) continue;
                var fresh = (VolumeComponent)ScriptableObject.CreateInstance(c.GetType());
                try
                {
                    int n = Math.Min(c.parameters.Count, fresh.parameters.Count);
                    for (int i = 0; i < n; i++) c.parameters[i].SetValue(fresh.parameters[i]);
                }
                finally { Object.DestroyImmediate(fresh); }
                EditorUtility.SetDirty(c);
                reset++;
                if (pp.IsActive()) { stillActive++; BatchLog.Warn("default volume profile: " + c.GetType().Name + " is still active after a reset to defaults"); }
                else BatchLog.Note("default volume profile: " + c.GetType().Name + " reset to its inactive defaults");
            }
            EditorUtility.SetDirty(def);
            BatchLog.Note("default volume profile " + AssetDatabase.GetAssetPath(def) + ": " + reset + " active overrides reset, " + stillActive + " still active");
        }
    }
}
