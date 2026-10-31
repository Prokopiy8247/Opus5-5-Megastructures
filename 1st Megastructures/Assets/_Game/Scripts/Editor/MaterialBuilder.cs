using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Generates the base materials (one per <see cref="MatId"/>, Assets/_Game/Materials/M_&lt;MatId&gt;.mat), M_Sky and
    /// M_FinalDither, and the MaterialLibrary asset that references them (Assets/_Game/Settings/MaterialLibrary.asset).
    /// Idempotent: existing assets are updated in place (never deleted or recreated), so GUIDs and scene references stay
    /// stable across RebuildAll runs. Values come from AbyssPalette / AbyssConst and the recipes of 14.2 (docs/SHADERS.md).
    /// Called by BatchTools.RebuildAll; also from the menu.
    /// </summary>
    public static class MaterialBuilder
    {
        public const string ShaderFolder = "Assets/_Game/Shaders";
        public const string MaterialFolder = "Assets/_Game/Materials";
        public const string SettingsFolder = "Assets/_Game/Settings";
        public const string LibraryPath = SettingsFolder + "/MaterialLibrary.asset";
        public const string SkyPath = MaterialFolder + "/M_Sky.mat";
        public const string FinalDitherPath = MaterialFolder + "/M_FinalDither.mat";

        public const string SurfaceShader = "Megastructure/Surface";
        public const string GratingShader = "Megastructure/Grating";
        public const string FacadeShader = "Megastructure/Facade";
        public const string EmissiveShader = "Megastructure/Emissive";
        public const string GlowShader = "Megastructure/Glow";
        public const string BeamWedgeShader = "Megastructure/BeamWedge";
        public const string LatticeShader = "Megastructure/Lattice";
        public const string SkyShader = "Megastructure/Sky";
        public const string FinalDitherShader = "Megastructure/FinalDither";
        public const string ShadowOnlyShader = "Megastructure/ShadowOnly";

        /// <summary>Every shader of the project (shader name, file name under ShaderFolder).</summary>
        public static readonly string[,] Shaders =
        {
            { SurfaceShader, "Surface.shader" },
            { GratingShader, "Grating.shader" },
            { FacadeShader, "Facade.shader" },
            { EmissiveShader, "Emissive.shader" },
            { GlowShader, "Glow.shader" },
            { BeamWedgeShader, "BeamWedge.shader" },
            { LatticeShader, "Lattice.shader" },
            { SkyShader, "Sky.shader" },
            { FinalDitherShader, "FinalDither.shader" },
            { ShadowOnlyShader, "ShadowOnly.shader" },
        };

        /// <summary>Surface recipe ids (_Recipe of Megastructure/Surface).</summary>
        public enum SurfaceRecipe
        {
            ConcreteBoard = 0, ConcreteFloor = 1, ConcreteCyl = 2, SteelPainted = 3, SteelWeathered = 4,
            RodPolished = 5, Cable = 6, DoorLeaf = 7, HoneyTube = 8,
        }

        /// <summary>Emissive modes (_Recipe of Megastructure/Emissive).</summary>
        public enum EmissiveMode { LampAmber = 0, LampCold = 1, ProxyCard = 2, GradientCard = 3 }

        public const int QueueBeams = 2950;   // explicit: additive beams before the glow sprites (order-independent anyway)
        public const int QueueGlow = 3000;

        /// <summary>
        /// RUST coverage per material (_RustAmount). The shaders' AbRustMask makes the coverage exact (area of the surface
        /// where the mask exceeds 0.5), 14.2 caps it at 12 % of a surface set. g01 review: the lift deck (SteelPainted)
        /// showed saturated orange blotches, so the values are low and the rust colour is desaturated in the shader.
        /// Before the exact mask the nominal 0.06 / 0.12 rendered about 1.0 % / 3.8 % (a non-uniform two-octave sum).
        /// </summary>
        public const float RustSteelPainted = 0.01f;
        public const float RustSteelWeathered = 0.04f;
        public const float RustGrating = 0.03f;
        public const float RustMax = 0.12f;

        /// <summary>Default of the per-material main-light (Key) mask of the lit shaders (1 = Key fully applied).</summary>
        public const float KeyLightMaskDefault = 1f;

        /// <summary>
        /// Nominal chamfer width (m) of the sub-pixel bevel fade of Megastructure/Surface (_BevelWidth, R1-26): a ChamferBox
        /// facet (vertex G = 255) narrower than 1-2.5 px merges into its camera-facing neighbour face and loses its
        /// specular / edge-wear highlight, so deck joints and rib bevels no longer dash or crawl. Variants with a known
        /// bevel (e.g. 4 mm tread plates) may set their own value.
        /// </summary>
        public const float BevelWidthDefault = 0.01f;

        /// <summary>
        /// Opt-in of the downward-view emission attenuation (_DownViewAtten, R1-01): 1 on the city facade materials, 0 on
        /// every other material. Inert until the global weight _AbDownView.x is driven (lift / ProgressDirector).
        /// </summary>
        public const float DownViewFacade = 1f;

        /// <summary>Grating panel size (m) and the shade of exposed bar sides (R1-27 grating look).</summary>
        public const float GratingPanelSize = 1f;
        public const float GratingSideShade = 0.42f;

        /// <summary>
        /// Macro variation (_MacroAmp, 1 = the 14.2 nominal +/-12 % of the 9 / 37 m value noise) of the board-formed concrete
        /// base materials (ConcreteBoard, ConcreteCyl, HoneyTube). R2-02: the value noise has a standard deviation of only
        /// about 2.8 % at amplitude 1 (KS2a shaft walls: 0.35-0.52 levels of macro residual against about +/-3.7 expected), so
        /// the base is 2 (sd about 5.5 %). Variants may override it (the beat-7 Face sets 2.2, the honeycomb threads 0).
        /// </summary>
        public const float MacroConcrete = 2f;

        /// <summary>
        /// Honeycomb air (_HoneyAir of Surface / Lattice, R2-04 / R2-19): x = aerial veil amount toward OUT_HORIZ, y = its
        /// length (m): amount x (1 - exp(-d / length)), about 6 % at 1 km, 14 % at 3 km, 21 % at 10 km; z = tube-air depth
        /// length (m), w = tube-air amount: below the web tops the walls go to OUT_HORIZ x 0.085 with 1 - exp(-depth / z).
        /// Set on the HoneyTube and Lattice base materials (every honeycomb tier inherits it: 14.10 c), zero elsewhere.
        /// </summary>
        public static readonly Vector4 HoneyAir = new Vector4(0.22f, 3000f, 120f, 1f);

        [MenuItem("Tools/Megastructure/Build Materials")]
        public static void BuildAllMenu() => BuildAll();

        /// <summary>
        /// Edit-mode default for the global picture fade read by FinalDither (an unset global float is 0 = black):
        /// outside play mode the Scene and Game views show the picture. In play mode and in the player the
        /// bootstrap / FadeController own _AbPictureFade.
        /// </summary>
        [InitializeOnLoadMethod]
        static void EditorGlobalDefaults()
        {
            if (!Application.isPlaying) Shader.SetGlobalFloat("_AbPictureFade", 1f);
        }

        /// <summary>Create or update every material and the MaterialLibrary asset.</summary>
        public static void BuildAll()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(SettingsFolder);

            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<MaterialLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            int count = (int)MatId.Count;
            if (lib.materials == null || lib.materials.Length != count)
            {
                var resized = new Material[count];
                if (lib.materials != null) Array.Copy(lib.materials, resized, Math.Min(count, lib.materials.Length));
                lib.materials = resized;
            }

            for (int i = 0; i < count; i++)
            {
                var id = (MatId)i;
                lib.materials[i] = BuildMaterial(id);
            }
            lib.skybox = BuildSky();
            lib.finalDither = BuildFinalDither();

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ABYSS] MaterialBuilder: {count} base materials + sky + final dither -> {LibraryPath}");
        }

        /// <summary>Asset path of the base material of an id.</summary>
        public static string MaterialPath(MatId id) => $"{MaterialFolder}/M_{id}.mat";

        /// <summary>Shader by name; loads the shader asset by path first (robust right after import in batch mode).</summary>
        public static Shader FindShader(string shaderName)
        {
            for (int i = 0; i < Shaders.GetLength(0); i++)
            {
                if (Shaders[i, 0] != shaderName) continue;
                var s = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/" + Shaders[i, 1]);
                if (s != null) return s;
            }
            var found = Shader.Find(shaderName);
            if (found == null) throw new InvalidOperationException("MaterialBuilder: shader not found: " + shaderName);
            return found;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Load the material at path (keeping its GUID) or create it; assign the shader if it differs.</summary>
        static Material LoadOrCreate(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            m.shaderKeywords = Array.Empty<string>();   // no material keywords anywhere (explicit variants only)
            m.renderQueue = -1;                          // shader default unless set below
            return m;
        }

        // ------------------------------------------------------------------------------------------------ per-id values
        static Material BuildMaterial(MatId id)
        {
            Material m;
            switch (id)
            {
                case MatId.ConcreteBoard:
                    m = Surface(id, SurfaceRecipe.ConcreteBoard, AbyssPalette.CONC_MID, 0.88f, 0f);
                    m.SetFloat("_MacroAmp", MacroConcrete);
                    m.enableInstancing = true;   // ring ribs / belts, honeycomb pieces
                    break;
                case MatId.ConcreteFloor:
                    m = Surface(id, SurfaceRecipe.ConcreteFloor, AbyssPalette.CONC_MID, 0.45f, 0f);
                    m.SetFloat("_WearWidth", 0.5f);
                    break;
                case MatId.ConcreteCyl:
                    m = Surface(id, SurfaceRecipe.ConcreteCyl, AbyssPalette.CONC_MID, 0.88f, 0f);
                    float r = AbyssConst.COLUMNS[0].z;   // C1 default; per-column variants set their own R and N
                    m.SetFloat("_CylRadius", r);
                    m.SetFloat("_PanelCount", Mathf.Round(2f * Mathf.PI * r / 1.2f));
                    m.SetFloat("_CylJoints", 4f);
                    m.SetFloat("_MacroAmp", MacroConcrete);
                    break;
                case MatId.SteelPainted:
                    m = Surface(id, SurfaceRecipe.SteelPainted, AbyssPalette.STEEL_DARK, 0.55f, 0.2f);
                    m.SetFloat("_RustAmount", RustSteelPainted);
                    m.SetFloat("_WearAmount", 1f);
                    m.enableInstancing = true;   // bolts, brackets, rails beyond 25 m
                    break;
                case MatId.SteelWeathered:
                    m = Surface(id, SurfaceRecipe.SteelWeathered, AbyssPalette.STEEL_DARK, 0.62f, 0.2f);
                    m.SetFloat("_RustAmount", RustSteelWeathered);
                    m.SetFloat("_WearAmount", 1.5f);
                    m.enableInstancing = true;
                    break;
                case MatId.RodPolished:
                    m = Surface(id, SurfaceRecipe.RodPolished, AbyssPalette.ROD_POLISHED, 0.30f, 1f);
                    break;
                case MatId.Cable:
                    m = Surface(id, SurfaceRecipe.Cable, AbyssPalette.CABLE_RUBBER, 0.7f, 0f);
                    m.SetFloat("_ThinLine", AbyssConst.MIN_LINE_PX);
                    m.enableInstancing = true;
                    break;
                case MatId.DoorLeaf:
                    m = Surface(id, SurfaceRecipe.DoorLeaf, AbyssPalette.DOOR_LEAF, 0.5f, 0.3f);
                    m.SetFloat("_WearAmount", 0.6f);
                    break;
                case MatId.HoneyTube:
                    m = Surface(id, SurfaceRecipe.HoneyTube, AbyssPalette.CONC_MID, 0.88f, 0f);
                    m.SetFloat("_AbDomain", (float)FogDomain.Exterior);
                    m.SetFloat("_TopY", AbyssConst.TERRACE_Y);
                    m.SetFloat("_DepthFalloff", 140f);
                    m.SetFloat("_MacroAmp", MacroConcrete);
                    m.SetVector("_HoneyAir", HoneyAir);
                    m.enableInstancing = true;   // tier 0 / tier 1 tubes
                    break;
                case MatId.Grating:
                    m = LoadOrCreate(MaterialPath(id), FindShader(GratingShader));
                    m.SetColor("_BaseColor", AbyssPalette.STEEL_DARK);
                    m.SetFloat("_Roughness", 0.6f);
                    m.SetFloat("_Metallic", 0.3f);
                    m.SetFloat("_RustAmount", RustGrating);
                    m.SetFloat("_Pitch", 0.04f);
                    m.SetFloat("_Bar", 0.008f);
                    m.SetFloat("_BarDepth", 0.03f);
                    m.SetFloat("_FarOpacity", 0.25f);
                    m.SetFloat("_SideShade", GratingSideShade);
                    m.SetFloat("_PanelSize", GratingPanelSize);
                    m.SetFloat("_AlphaToMask", 1f);
                    m.SetFloat("_Cull", (float)CullMode.Off);
                    Common(m, FogDomain.Shaft);
                    m.enableInstancing = true;
                    break;
                case MatId.Facade:
                case MatId.FacadeModule:
                    m = LoadOrCreate(MaterialPath(id), FindShader(FacadeShader));
                    m.SetFloat("_Recipe", id == MatId.Facade ? 0f : 1f);
                    m.SetColor("_BaseColor", AbyssPalette.CONC_MID);
                    m.SetColor("_GlassColor", AbyssPalette.GLASS);
                    m.SetColor("_WinCold", AbyssPalette.WIN_COLD);
                    m.SetColor("_WinAmber", AbyssPalette.AMBER);
                    m.SetFloat("_LitFraction", AbyssConst.CITY_LIT);
                    m.SetFloat("_AmberFraction", 0.15f);
                    m.SetVector("_EmissiveRange", new Vector4(2.5f, AbyssPalette.WINDOW_EMISSIVE_MAX, 0f, 0f));
                    m.SetFloat("_EmissionStrength", 1f);
                    m.SetVector("_GridOrigin", new Vector4(AbyssConst.SHAFT_CENTRE.x, AbyssConst.SHAFT_CENTRE.y, AbyssConst.CITY_TOP_Y, 0f));
                    m.SetFloat("_Roughness", 0.9f);
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_DownViewAtten", DownViewFacade);
                    Common(m, FogDomain.Shaft);
                    m.enableInstancing = id == MatId.FacadeModule;   // ~1 071 instanced modules
                    break;
                case MatId.LampAmber:
                    m = Emissive(id, EmissiveMode.LampAmber, AbyssPalette.AMBER, AbyssPalette.AMBER_EMISSIVE);
                    m.enableInstancing = true;
                    break;
                case MatId.LampCold:
                    m = Emissive(id, EmissiveMode.LampCold, AbyssPalette.COLD_LAMP, 4f);
                    m.enableInstancing = true;
                    break;
                case MatId.ProxyCard:
                    m = Emissive(id, EmissiveMode.ProxyCard, AbyssPalette.AMBER, 1f);
                    m.SetColor("_BaseColor", AbyssPalette.BLUE_BLACK);
                    break;
                case MatId.GradientCard:
                    m = Emissive(id, EmissiveMode.GradientCard, AbyssPalette.AMBER, 1f);
                    m.SetColor("_CardTop", AbyssPalette.DARK_CARD_TOP);
                    m.SetColor("_CardBottom", AbyssPalette.DARK_CARD_BOTTOM);
                    m.SetFloat("_CardMode", 0f);
                    m.SetFloat("_CardFog", 1f);
                    break;
                case MatId.Glow:
                    m = LoadOrCreate(MaterialPath(id), FindShader(GlowShader));
                    m.SetColor("_EmissionColor", AbyssPalette.AMBER.linear); // [HDR] colours are not converted
                    m.SetFloat("_EmissionStrength", AbyssPalette.AMBER_EMISSIVE);
                    m.SetFloat("_MinPixels", 2.5f);
                    m.SetFloat("_EnergyConserve", 0f);
                    m.SetFloat("_Ignite", 0f);
                    m.SetFloat("_SoftDepth", 0.05f);
                    m.SetFloat("_AbDomain", (float)FogDomain.Shaft);
                    m.SetFloat("_AbFade", 1f);
                    m.enableInstancing = true;
                    m.renderQueue = QueueGlow;
                    break;
                case MatId.BeamWedge:
                    m = LoadOrCreate(MaterialPath(id), FindShader(BeamWedgeShader));
                    m.SetFloat("_Recipe", 0f);
                    m.SetColor("_EmissionColor", AbyssPalette.Hex(0xC9DDF0).linear); // [HDR] colours are not converted   // beat-6 Key colour (14.2 lighting rig)
                    m.SetFloat("_EmissionStrength", 0.004f);
                    m.SetVector("_BeamDir", AbyssConst.Dir(0f, -AbyssConst.BEAM_PITCH_DEG));
                    m.SetVector("_BeamParams", new Vector4(AbyssConst.BEAM_R0, AbyssConst.BEAM_R1,
                        0.5f * AbyssConst.BEAM_WIDTH_DEG * Mathf.Deg2Rad, AbyssConst.BEAM_LENGTH));
                    m.SetVector("_BeamFeather", new Vector4(2f, 12f, 8f, 4f));
                    m.SetVector("_BeamPatch", new Vector4(6f, 1.05f, 0f, 1f));
                    m.SetVector("_BeamCentre", AbyssConst.FAN_CENTRE);
                    // mode 2 (doorway spill): the gallery exit door, 1.2 x 2.0 m; the shader caps the feathers to it
                    m.SetVector("_BeamRect", new Vector4(0.5f * AbyssConst.EXIT_DOOR_W, 0.5f * AbyssConst.EXIT_DOOR_H, 0f, 0f));
                    m.SetFloat("_PhaseG", 0.3f);
                    m.SetFloat("_UseDepth", 1f);
                    m.SetFloat("_AbDomain", (float)FogDomain.Shaft);
                    m.SetFloat("_AbFade", 1f);
                    m.SetFloat("_Cull", (float)CullMode.Off);
                    m.SetFloat("_ZTest", (float)CompareFunction.Always);
                    m.renderQueue = QueueBeams;
                    break;
                case MatId.Lattice:
                    m = LoadOrCreate(MaterialPath(id), FindShader(LatticeShader));
                    m.SetColor("_BaseColor", AbyssPalette.CONC_MID);
                    m.SetColor("_EmissionColor", AbyssPalette.AMBER.linear); // [HDR] colours are not converted
                    m.SetFloat("_EmissionStrength", AbyssPalette.AMBER_EMISSIVE);
                    m.SetFloat("_MinPixels", 2.5f);
                    m.SetFloat("_LampRadius", 0.3f);
                    m.SetVector("_LatticeOrigin", new Vector4(AbyssConst.SHAFT_CENTRE.x, AbyssConst.SHAFT_CENTRE.y, 0f, 0f));
                    m.SetVector("_LatticeParams", new Vector4(AbyssConst.LATTICE_PITCH / Mathf.Sqrt(3f), AbyssConst.SHAFT_APOTHEM, 6f, 269f));
                    m.SetFloat("_TopY", AbyssConst.TERRACE_Y);
                    m.SetFloat("_DepthFalloff", 140f);
                    m.SetFloat("_TubeDepth", 600f);
                    m.SetFloat("_AbDomain", (float)FogDomain.Exterior);
                    m.SetFloat("_AbFade", 1f);
                    m.SetFloat("_KeyLightMask", KeyLightMaskDefault);
                    m.SetVector("_DistFade", new Vector4(AbyssConst.HONEY_TIER2_START, 6600f, 1f, 0f));
                    m.SetVector("_HoneyAir", HoneyAir);   // the same air as the HoneyTube tiers (14.10 c)
                    break;
                case MatId.ShadowCaster:
                    m = LoadOrCreate(MaterialPath(id), FindShader(ShadowOnlyShader));
                    m.SetFloat("_AbFade", 1f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, "MaterialBuilder: no recipe for this MatId");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Surface(MatId id, SurfaceRecipe recipe, Color baseSrgb, float roughness, float metallic)
        {
            var m = LoadOrCreate(MaterialPath(id), FindShader(SurfaceShader));
            m.SetFloat("_Recipe", (float)recipe);
            m.SetColor("_BaseColor", baseSrgb);
            m.SetFloat("_Roughness", roughness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_ThinLine", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_EmissionStrength", 0f);
            m.SetFloat("_MacroAmp", 1f);
            m.SetFloat("_WearAmount", 1f);
            m.SetFloat("_RustAmount", 0f);
            m.SetFloat("_WearWidth", 0f);
            m.SetFloat("_AlphaToMask", 1f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetColor("_FarTint", baseSrgb);
            m.SetVector("_FarRange", Vector4.zero);
            m.SetFloat("_BevelWidth", BevelWidthDefault);
            m.SetFloat("_DownViewAtten", 0f);
            m.SetVector("_HoneyAir", Vector4.zero);   // honeycomb air off (HoneyTube sets HoneyAir)
            Common(m, FogDomain.Shaft);
            m.enableInstancing = false;
            return m;
        }

        static Material Emissive(MatId id, EmissiveMode mode, Color emissionSrgb, float strength)
        {
            var m = LoadOrCreate(MaterialPath(id), FindShader(EmissiveShader));
            m.SetFloat("_Recipe", (float)mode);
            m.SetColor("_EmissionColor", emissionSrgb.linear); // [HDR] colours are not converted
            m.SetFloat("_EmissionStrength", strength);
            m.SetFloat("_DownViewAtten", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            Common(m, FogDomain.Shaft);
            m.enableInstancing = false;
            return m;
        }

        /// <summary>
        /// Properties every opaque Megastructure material shares: fog domain, tier fade, distance fade, and the Key mask of
        /// the lit shaders (written explicitly so a rebuild resets a hand-edited base material; idempotent).
        /// </summary>
        static void Common(Material m, FogDomain domain)
        {
            m.SetFloat("_AbDomain", (float)domain);
            m.SetFloat("_AbFade", 1f);
            if (m.HasProperty("_DistFade") && m.shader.name != LatticeShader) m.SetVector("_DistFade", Vector4.zero);
            if (m.HasProperty("_KeyLightMask")) m.SetFloat("_KeyLightMask", KeyLightMaskDefault);
        }

        static Material BuildSky()
        {
            var m = LoadOrCreate(SkyPath, FindShader(SkyShader));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material BuildFinalDither()
        {
            var m = LoadOrCreate(FinalDitherPath, FindShader(FinalDitherShader));
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
