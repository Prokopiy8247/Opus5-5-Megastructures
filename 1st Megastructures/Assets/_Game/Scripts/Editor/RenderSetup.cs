using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// URP asset, renderer, final dither feature, post-processing profile and UI Toolkit panel settings (Part 8.1, 8.2, 8.5, 6.5).
    /// Field names verified against the URP 17.6 package source (docs/research/urp-csharp.md). Idempotent.
    /// </summary>
    public static class RenderSetup
    {
        public const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        public const string VolumeProfilePath = "Assets/_Game/Settings/AbyssVolumeProfile.asset";
        public const string PanelSettingsPath = "Assets/_Game/Settings/AbyssPanelSettings.asset";
        public const string ThemePath = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
        public const string MaterialLibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";
        public const string DitherFeatureName = "AbyssFinalDither";

        public static void Apply()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rp == null || renderer == null) throw new System.Exception("URP PC asset or renderer missing");

            ConfigurePipelineAsset(rp);
            ConfigureRenderer(renderer);
            GraphicsSettings.defaultRenderPipeline = rp;
            QualitySettings.renderPipeline = rp;
            RenderSettings.fog = false;
            CreateVolumeProfile();
            CreatePanelSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[ABYSS] RenderSetup applied");
        }

        static void ConfigurePipelineAsset(UniversalRenderPipelineAsset rp)
        {
            var so = new SerializedObject(rp);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_HDRColorBufferPrecision", 1);          // 64-bit (R16G16B16A16): 32-bit B10G11R11 bands and eats the dither
            Set(so, "m_MSAA", 4);                             // MSAA 4x base (Part 8.1)
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_RequireDepthTexture", true);           // beams soft-depth (14.7.4)
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_MainLightRenderingMode", 1);           // per pixel
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_AdditionalLightsRenderingMode", 1);    // per pixel
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_AdditionalLightsPerObjectLimit", 8);   // ignored in Forward+, kept for the Forward fallback
            Set(so, "m_AnyShadowsSupported", true);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 3);                // High
            Set(so, "m_ShadowDistance", 120f);                // per beat at runtime (ProgressDirector)
            Set(so, "m_ShadowCascadeCount", 4);
            SetV3(so, "m_Cascade4Split", new Vector3(0.06f, 0.18f, 0.42f));
            Set(so, "m_CascadeBorder", 0.15f);
            Set(so, "m_ShadowDepthBias", 1.0f);
            Set(so, "m_ShadowNormalBias", 1.0f);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_GPUResidentDrawerMode", 0);            // Disabled (docs/research/mesh-instancing.md)
            Set(so, "m_ColorGradingMode", 1);                 // HDR grading with Neutral tonemapping
            Set(so, "m_ColorGradingLutSize", 32);
            Set(so, "m_EnableLODCrossFade", false);           // we do our own dithered tier fades (_AbFade)
            Set(so, "m_SupportsLightCookies", false);
            Set(so, "m_SupportsLightLayers", false);
            Set(so, "m_MixedLightingSupported", false);
            Set(so, "m_VolumeFrameworkUpdateMode", 0);        // every frame (ProgressDirector edits the profile)
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
        }

        static void ConfigureRenderer(UniversalRendererData data)
        {
            var so = new SerializedObject(data);
            Set(so, "m_RenderingMode", 2);                    // Forward+ (required by Part 14)
            Set(so, "m_DepthPrimingMode", 0);
            Set(so, "m_CopyDepthMode", 0);                    // after opaques: depth texture available to the beams
            so.ApplyModifiedPropertiesWithoutUndo();

            // Remove every feature except our dither (the template SSAO is off by Part 14.2).
            so.Update();
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            var keepObj = new System.Collections.Generic.List<Object>();
            var keepId = new System.Collections.Generic.List<long>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var f = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                long id = i < map.arraySize ? map.GetArrayElementAtIndex(i).longValue : 0;
                if (f != null && f.name == DitherFeatureName && f is FullScreenPassRendererFeature) { keepObj.Add(f); keepId.Add(id); }
                else if (f != null) Object.DestroyImmediate(f, true);
            }
            list.arraySize = keepObj.Count;
            map.arraySize = keepObj.Count;
            for (int i = 0; i < keepObj.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = keepObj[i];
                map.GetArrayElementAtIndex(i).longValue = keepId[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialLibraryPath);
            Material dither = lib != null ? lib.finalDither : null;
            if (dither == null) throw new System.Exception("MaterialLibrary.finalDither missing (run MaterialBuilder.BuildAll first)");

            FullScreenPassRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && f.name == DitherFeatureName) feature = fs;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = DitherFeatureName;
                feature.hideFlags |= HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                so.Update();
                list = so.FindProperty("m_RendererFeatures");
                map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            feature.passMaterial = dither;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }

        static void CreateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VolumeProfilePath));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            var tm = GetOrAdd<Tonemapping>(profile);
            tm.mode.Override(TonemappingMode.Neutral);
            var ca = GetOrAdd<ColorAdjustments>(profile);
            ca.postExposure.Override(0.4f); ca.contrast.Override(10f); ca.saturation.Override(-22f);
            ca.colorFilter.overrideState = false; ca.hueShift.overrideState = false;
            var wb = GetOrAdd<WhiteBalance>(profile);
            wb.temperature.Override(-6f); wb.tint.Override(0f);
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.0f); bloom.intensity.Override(0.2f); bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(true); bloom.clamp.Override(65000f);
            var vig = GetOrAdd<Vignette>(profile);
            vig.intensity.Override(0.18f); vig.smoothness.Override(0.6f); vig.rounded.Override(false);
            // Explicitly neutral: no grain, motion blur, DoF, chromatic aberration, lens distortion (Part 8.2)
            GetOrAdd<FilmGrain>(profile).intensity.Override(0f);
            GetOrAdd<MotionBlur>(profile).intensity.Override(0f);
            GetOrAdd<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);
            GetOrAdd<ChromaticAberration>(profile).intensity.Override(0f);
            GetOrAdd<LensDistortion>(profile).intensity.Override(0f);
            GetOrAdd<PaniniProjection>(profile).distance.Override(0f);
            foreach (var c in profile.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(profile);
        }

        static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet<T>(out var c)) return c;
            c = p.Add<T>(false);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void CreatePanelSettings()
        {
            if (!File.Exists(ThemePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            }
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f; // scale with height: text sizes are fractions of the screen height (14.11)
            ps.sortingOrder = 100;
            ps.clearColor = false;
            EditorUtility.SetDirty(ps);
        }

        static void Set(SerializedObject so, string name, bool v) { var p = Req(so, name); if (p != null) p.boolValue = v; }
        static void Set(SerializedObject so, string name, int v) { var p = Req(so, name); if (p != null) p.intValue = v; }
        static void Set(SerializedObject so, string name, float v) { var p = Req(so, name); if (p != null) p.floatValue = v; }
        static void SetV3(SerializedObject so, string name, Vector3 v) { var p = Req(so, name); if (p != null) p.vector3Value = v; }

        static SerializedProperty Req(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogWarning("[ABYSS] RenderSetup: serialized field not found: " + name);
            return p;
        }
    }
}
