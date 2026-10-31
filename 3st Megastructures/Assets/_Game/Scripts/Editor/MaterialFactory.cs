// MaterialFactory: creates every MaterialLibrary field as Assets/_Game/Materials/<FieldName>.mat with the exact
// shader, keyword, palette colours, floats, render state, render queue and instancing flag of the material
// contract (MASTER_PROMPT 14.2.3, 14.7.8 c), then Assets/_Game/Materials/MaterialLibrary.asset referencing them.
//
// Colour space: the project is Linear. Unity stores a material Color property as an sRGB value and converts it
// to linear when the shader reads it (a property flagged [HDR] is converted at SetColor time instead), so the
// factory passes ThreadPalette.Linear(token).gamma and every shader receives exactly the linear palette value.
// Existing .mat files are rewritten in place (EditorUtility.CopySerialized from a fresh material), so GUIDs and
// scene references survive every rebuild and no stale property or keyword is carried over.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>Numbers of the material contract that have no ThreadConstants field yet (see contract_requests).</summary>
    public static class MaterialConstants
    {
        // D-038 per-material shade tints (critic C-07/C-08; F3 model): shade reads as cool lilac on salt and as a
        // hue-preserving warm shade on sand instead of concrete grey / mud brown; stations keep their hue.
        public static readonly Vector3 TintLilac = new Vector3(0.64f, 0.62f, 1.00f);
        public static readonly Vector3 TintSand = new Vector3(0.74f, 0.70f, 0.80f);
        public static readonly Vector3 TintMint = new Vector3(0.66f, 0.72f, 0.74f);
        public static readonly Vector3 TintPeach = new Vector3(0.72f, 0.66f, 0.76f);
        public static readonly Vector3 TintLilacBead = new Vector3(0.68f, 0.70f, 0.80f);
        public static readonly Vector3 TintStairs = new Vector3(0.86f, 0.84f, 0.96f);   // D-050: risers face away from the sun; a lighter shade keeps the white-out luminous

        /// <summary>14.5.2 "Motion" (line 664): robe hem flutes sway 0.05 m with period 11 s.</summary>
        public const float RobeSway = 0.05f;
        /// <summary>14.7.2 (line 797): posts are drawn up to 60 m and cross-faded (A2C, D-042) over 15 m; the handrails fade with them (B3).</summary>
        public const float PostFadeCentre = 60f, PostFadeWidth = 15f;
        public const float PostFadeNear = PostFadeCentre - 0.5f * PostFadeWidth;   // 52.5 m
        public const float PostFadeFar = PostFadeCentre + 0.5f * PostFadeWidth;    // 67.5 m
        /// <summary>14.2.3 M08 (line 467): CREAM window dots, emissive 1.5.</summary>
        public const float WindowEmission = 1.5f;
        /// <summary>Material contract (B11): window dots fade to the bead colour between 150 and 200 m.</summary>
        public const float WindowFadeNear = 150f, WindowFadeFar = 200f;
        /// <summary>Material contract (B11): additive beacon halo emission 0.5.</summary>
        public const float HaloEmission = 0.5f;
        /// <summary>Rim strength multiplier default (14.2.2 item 5 uses the shader's own 0.16).</summary>
        public const float RimScaleDefault = 1f;
    }

    public enum ShaderFamily { Flat, Line, Emissive, Sky, Grade }

    /// <summary>One row of the material contract.</summary>
    public sealed class MatSpec
    {
        public string Field;
        public ShaderFamily Family;
        public string Keyword;                       // at most one shader_feature_local keyword
        public Pal Base = Pal.SALT_WHITE;
        public bool HasShade;
        public Pal Shade = Pal.SALT_WHITE;
        public Pal Second = Pal.SALT_WHITE;
        public Pal Third = Pal.SALT_WHITE;
        public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
        public BlendMode Src = BlendMode.One, Dst = BlendMode.Zero;
        public bool ZWrite = true;
        public CullMode Cull = CullMode.Back;
        public float OffsetFactor, OffsetUnits;
        public int Queue = -1;                       // -1 = the shader's queue
        public bool Instancing;
        public bool CastShadows = true;              // false: ShadowCaster pass disabled on the material
        public bool DepthPasses = true;              // false: DepthOnly / DepthNormals passes disabled
        public Vector4 ShadeTint;                    // w = 1: per-material shade tint (D-038), else the shader's TH_SHADE_TINT

        public MatSpec Kw(string k) { Keyword = k; return this; }
        public MatSpec ShadeC(Pal p) { HasShade = true; Shade = p; return this; }
        public MatSpec Sec(Pal p) { Second = p; return this; }
        public MatSpec Thi(Pal p) { Third = p; return this; }
        public MatSpec F(string name, double v) { Floats[name] = (float)v; return this; }
        public MatSpec Blend(BlendMode s, BlendMode d) { Src = s; Dst = d; return this; }
        public MatSpec NoZWrite() { ZWrite = false; return this; }
        public MatSpec CullOff() { Cull = CullMode.Off; return this; }
        public MatSpec Offset(float factor, float units) { OffsetFactor = factor; OffsetUnits = units; return this; }
        public MatSpec Q(int q) { Queue = q; return this; }
        public MatSpec Inst() { Instancing = true; return this; }
        public MatSpec NoShadows() { CastShadows = false; return this; }
        public MatSpec NoDepth() { DepthPasses = false; return this; }
        public MatSpec Tint(Vector3 t) { ShadeTint = new Vector4(t.x, t.y, t.z, 1f); return this; }
    }

    public static class MaterialFactory
    {
        public const string ErrorShaderName = "Hidden/InternalErrorShader";

        static readonly string[] s_FlatKeywords = { "_PATTERN_SALT", "_PATTERN_STEPS", "_PATTERN_MANDALA", "_PATTERN_CABLE", "_PATTERN_DECK" };
        static readonly string[] s_LineKeywords = { "_LINE_TUBE", "_LINE_RIBBON" };
        static readonly string[] s_EmissiveKeywords = { "_EMISSIVE_BEACON", "_EMISSIVE_HALO", "_EMISSIVE_WINDOW" };
        static readonly string[] s_None = new string[0];

        public static string ShaderName(ShaderFamily f)
        {
            switch (f)
            {
                case ShaderFamily.Flat: return ThreadPaths.ShaderFlat;
                case ShaderFamily.Line: return ThreadPaths.ShaderLine;
                case ShaderFamily.Emissive: return ThreadPaths.ShaderEmissive;
                case ShaderFamily.Sky: return ThreadPaths.ShaderSky;
                default: return ThreadPaths.ShaderGrade;
            }
        }

        public static string[] FamilyKeywords(ShaderFamily f)
        {
            switch (f)
            {
                case ShaderFamily.Flat: return s_FlatKeywords;
                case ShaderFamily.Line: return s_LineKeywords;
                case ShaderFamily.Emissive: return s_EmissiveKeywords;
                default: return s_None;
            }
        }

        static MatSpec Make(string field, ShaderFamily fam, Pal baseColour) => new MatSpec { Field = field, Family = fam, Base = baseColour };
        static MatSpec Flat(string field, Pal baseColour) => Make(field, ShaderFamily.Flat, baseColour);
        static MatSpec Line(string field, Pal baseColour) => Make(field, ShaderFamily.Line, baseColour);
        static MatSpec Emissive(string field, Pal baseColour) => Make(field, ShaderFamily.Emissive, baseColour);

        /// <summary>The material contract, one row per MaterialLibrary field.</summary>
        public static List<MatSpec> Specs()
        {
            int geometry = (int)RenderQueue.Geometry, transparent = (int)RenderQueue.Transparent;
            float sway = MaterialConstants.RobeSway;
            return new List<MatSpec>
            {
                // ---- beats 1-2
                Flat("Salt", Pal.SALT_WHITE).Kw("_PATTERN_SALT").F("_CableShadowOn", 1).Tint(MaterialConstants.TintLilac),
                Flat("SaltShadow", Pal.SALT_WHITE).Kw("_PATTERN_SALT").F("_ShadowMode", 1).Tint(MaterialConstants.TintLilac)
                    .Blend(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha).NoZWrite().Offset(-2f, -2f)
                    .Q(geometry + 2).NoShadows().NoDepth(),
                Flat("RobeTeal", Pal.TEAL).F("_Sway", sway),
                Flat("RobeCoral", Pal.CORAL).ShadeC(Pal.CORAL_DEEP).F("_RimTint", 1).F("_Sway", sway),
                Flat("RobeHem", Pal.SAND).F("_Sway", sway).Tint(MaterialConstants.TintSand),
                Flat("HoodInk", Pal.INK),
                Flat("FacePlate", Pal.SAND_SHADE).Tint(MaterialConstants.TintSand),
                Flat("Stone", Pal.SAND).Tint(MaterialConstants.TintSand),
                Flat("Bollard", Pal.INK).Inst(),   // 14.7.9: the 28 bollards are instanced (RenderMeshInstanced needs the flag)
                Flat("DoorFrame", Pal.CREAM).Tint(MaterialConstants.TintSand),   // A15: CREAM 0.1 m colossus door frame
                // ---- beats 3-4
                Flat("Steps", Pal.SAND).Kw("_PATTERN_STEPS").Sec(Pal.SAND_SHADE).Thi(Pal.SALT_WHITE).Tint(MaterialConstants.TintStairs),
                Flat("Stringer", Pal.SAND).Tint(MaterialConstants.TintSand),
                Flat("Terrace", Pal.SAND).Kw("_PATTERN_MANDALA").Sec(Pal.SALT_WHITE).F("_CableShadowOn", 1).Tint(MaterialConstants.TintSand),
                // Cable: _ThirdColor = CREAM marker plates (A11); _BelowHaze on the cable, its collars and its ribbons (A5)
                Flat("Cable", Pal.TEAL).Kw("_PATTERN_CABLE").ShadeC(Pal.TEAL_DEEP).Sec(Pal.CORAL).Thi(Pal.CREAM).F("_ReliefFade", 1)
                    .F("_CableShadowOn", 0).F("_BelowHaze", 1),
                Flat("Collar", Pal.SAND_SHADE).F("_BelowHaze", 1).Tint(MaterialConstants.TintSand),
                // ---- beats 5-7 (deck, arms, posts and handrails dissolve far below the walker at altitude, A-r2-1;
                // the thin deck and arm casters render both faces into the shadow map, B7)
                Flat("Deck", Pal.SAND_SHADE).Kw("_PATTERN_DECK").Sec(Pal.CREAM).F("_CableShadowOn", 1).F("_BelowFade", 1)
                    .F("_ShadowCull", (double)CullMode.Off).Tint(MaterialConstants.TintSand),
                Flat("Arm", Pal.SAND_SHADE).F("_CableShadowOn", 1).F("_BelowFade", 1).F("_ShadowCull", (double)CullMode.Off)
                    .Tint(MaterialConstants.TintSand),
                Flat("RailPost", Pal.INK).F("_FadeNear", MaterialConstants.PostFadeNear).F("_FadeFar", MaterialConstants.PostFadeFar)
                    .F("_CableShadowOn", 1).F("_BelowFade", 1).Inst(),
                // the handrails fade with the posts (B3: beyond the post fade they read as ghost double lines)
                Line("Handrail", Pal.CORAL).Kw("_LINE_TUBE").F("_LineWidth", ThreadConstants.HandrailDiameter).F("_CableShadowOn", 1)
                    .F("_FadeNear", MaterialConstants.PostFadeNear).F("_FadeFar", MaterialConstants.PostFadeFar).F("_BelowFade", 1),
                Flat("BeadMint", Pal.STATION_MINT).F("_CableShadowOn", 1).Tint(MaterialConstants.TintMint),
                Flat("BeadPeach", Pal.STATION_PEACH).F("_CableShadowOn", 1).Tint(MaterialConstants.TintPeach),
                Flat("BeadLilac", Pal.STATION_LILAC).F("_CableShadowOn", 1).Tint(MaterialConstants.TintLilacBead),
                Flat("BeadDoor", Pal.INK).F("_CableShadowOn", 1),
                Emissive("Window", Pal.CREAM).Kw("_EMISSIVE_WINDOW").F("_Emission", MaterialConstants.WindowEmission)
                    .F("_FadeNear", MaterialConstants.WindowFadeNear).F("_FadeFar", MaterialConstants.WindowFadeFar)
                    .Sec(Pal.STATION_MINT).Inst().NoShadows(),
                Flat("BeaconPillar", Pal.SAND).Tint(MaterialConstants.TintSand),
                Emissive("BeaconCrystal", Pal.BEACON).Kw("_EMISSIVE_BEACON").F("_Emission", ThreadConstants.BeaconEmissive),
                Emissive("BeaconHalo", Pal.BEACON).Kw("_EMISSIVE_HALO").F("_Emission", MaterialConstants.HaloEmission)
                    .F("_HaloRadius", ThreadConstants.BeaconHaloR0)
                    .Blend(BlendMode.One, BlendMode.One).NoZWrite().CullOff().Q(transparent + 10).NoShadows().NoDepth(),
                Line("ThreadRibbon", Pal.TEAL).Kw("_LINE_RIBBON").ShadeC(Pal.TEAL_DEEP).F("_LineWidth", 2.0 * ThreadConstants.CableRadius)
                    .F("_BelowHaze", 1)
                    .Blend(BlendMode.One, BlendMode.OneMinusSrcAlpha).NoZWrite().CullOff().Q(transparent).NoShadows().NoDepth(),
                // ---- sky / post
                Make("Sky", ShaderFamily.Sky, Pal.SALT_WHITE),       // skybox: no material properties
                Make("Grade", ShaderFamily.Grade, Pal.SALT_WHITE),   // ThreadGradeFeature: passes drawn by index
            };
        }

        /// <summary>Colour properties of the contract per family, with the token each one receives.</summary>
        public static List<KeyValuePair<string, Pal>> ContractColours(MatSpec s)
        {
            var l = new List<KeyValuePair<string, Pal>>();
            switch (s.Family)
            {
                case ShaderFamily.Flat:
                    l.Add(new KeyValuePair<string, Pal>("_BaseColor", s.Base));
                    l.Add(new KeyValuePair<string, Pal>("_ShadeColor", s.Shade));
                    l.Add(new KeyValuePair<string, Pal>("_SecondColor", s.Second));
                    l.Add(new KeyValuePair<string, Pal>("_ThirdColor", s.Third));
                    break;
                case ShaderFamily.Line:
                    l.Add(new KeyValuePair<string, Pal>("_BaseColor", s.Base));
                    l.Add(new KeyValuePair<string, Pal>("_ShadeColor", s.Shade));
                    break;
                case ShaderFamily.Emissive:
                    l.Add(new KeyValuePair<string, Pal>("_BaseColor", s.Base));
                    l.Add(new KeyValuePair<string, Pal>("_SecondColor", s.Second));
                    break;
            }
            return l;
        }

        /// <summary>Every float property of the contract (family defaults overridden by the row), incl. render state.</summary>
        public static Dictionary<string, float> ContractFloats(MatSpec s)
        {
            var d = new Dictionary<string, float>();
            switch (s.Family)
            {
                case ShaderFamily.Flat:
                    d["_UseShadeColor"] = s.HasShade ? 1f : 0f;
                    d["_RimTint"] = 0f;
                    d["_RimScale"] = MaterialConstants.RimScaleDefault;
                    d["_CableShadowOn"] = 0f;
                    d["_ShadowMode"] = 0f;
                    d["_Sway"] = 0f;
                    d["_ReliefFade"] = 0f;
                    d["_Emission"] = 0f;
                    d["_FadeNear"] = 0f;
                    d["_FadeFar"] = 0f;
                    d["_BelowFade"] = 0f;
                    d["_BelowHaze"] = 0f;
                    d["_ShadowCull"] = (float)s.Cull;                          // the caster culls like the surface unless a row says otherwise
                    d["_OffsetFactor"] = s.OffsetFactor;
                    d["_OffsetUnits"] = s.OffsetUnits;
                    break;
                case ShaderFamily.Line:
                    d["_UseShadeColor"] = s.HasShade ? 1f : 0f;
                    d["_LineWidth"] = 0f;
                    d["_MinWidthPx"] = 0f;                                   // 0 = use _ThScreen.y
                    d["_AlphaFloor"] = (float)ThreadConstants.ThreadAlphaFloor;
                    d["_CableShadowOn"] = 0f;
                    d["_FadeNear"] = 0f;
                    d["_FadeFar"] = 0f;
                    d["_BelowFade"] = 0f;
                    d["_BelowHaze"] = 0f;
                    break;
                case ShaderFamily.Emissive:
                    d["_Emission"] = 0f;
                    d["_FadeNear"] = 0f;
                    d["_FadeFar"] = 0f;
                    d["_HaloRadius"] = 0f;
                    break;
                default:
                    return d;   // sky / grade: no material properties
            }
            d["_SrcBlend"] = (float)s.Src;
            d["_DstBlend"] = (float)s.Dst;
            d["_ZWrite"] = s.ZWrite ? 1f : 0f;
            d["_Cull"] = (float)s.Cull;
            foreach (var kv in s.Floats) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>
        /// Alpha-to-coverage (with MSAA) for the opaque handrail tube and for opaque flat materials with a coverage fade
        /// (distance fade D-042, or the far-below fade of the walkway at altitude); blended / additive materials never.
        /// </summary>
        public static bool UsesAlphaToMask(MatSpec s)
        {
            if (s.Keyword == "_LINE_TUBE") return true;
            if (s.Family != ShaderFamily.Flat) return false;
            return s.Floats.ContainsKey("_FadeFar") || (s.Floats.TryGetValue("_BelowFade", out float below) && below > 0.5f);
        }

        public static Shader FindShader(string shaderName)
        {
            var sh = Shader.Find(shaderName);
            if (sh != null) return sh;
            int slash = shaderName.LastIndexOf('/');
            string leaf = slash >= 0 ? shaderName.Substring(slash + 1) : shaderName;
            return AssetDatabase.LoadAssetAtPath<Shader>(ThreadPaths.ShadersDir + "/" + leaf + ".shader");
        }

        /// <summary>The value to pass to Material.SetColor so the shader receives the linear token (see header).</summary>
        public static Color StoredColour(Pal p) => ThreadPalette.Linear(p).gamma;

        public static string MaterialPath(string field) => ThreadPaths.MaterialsDir + "/" + field + ".mat";

        public static List<FieldInfo> LibraryFields()
        {
            var l = new List<FieldInfo>();
            foreach (var f in typeof(MaterialLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(Material)) l.Add(f);
            return l;
        }

        /// <summary>Creates / rewrites every material and the library asset.</summary>
        public static MaterialLibrary Build(List<string> problems)
        {
            EditorUtil.EnsureFolder(ThreadPaths.MaterialsDir);
            var specs = Specs();
            var fields = LibraryFields();

            var byName = new Dictionary<string, MatSpec>();
            foreach (var s in specs) byName[s.Field] = s;
            foreach (var f in fields)
                if (!byName.ContainsKey(f.Name)) ThreadLog.Problem(problems, $"MaterialFactory: MaterialLibrary.{f.Name} has no row in the material contract");

            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ThreadPaths.LibraryAsset);
            bool createdLib = lib == null;
            if (createdLib)
            {
                lib = ScriptableObject.CreateInstance<MaterialLibrary>();
                lib.name = "MaterialLibrary";
                AssetDatabase.CreateAsset(lib, ThreadPaths.LibraryAsset);
            }

            int made = 0;
            foreach (var s in specs)
            {
                FieldInfo field = null;
                foreach (var f in fields) if (f.Name == s.Field) { field = f; break; }
                if (field == null) { ThreadLog.Problem(problems, $"MaterialFactory: contract row '{s.Field}' has no MaterialLibrary field"); continue; }
                var mat = CreateOrUpdate(s, problems);
                if (mat == null) continue;
                field.SetValue(lib, mat);
                made++;
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            ThreadLog.Info($"MaterialFactory: {made}/{specs.Count} materials written, library {(createdLib ? "created" : "updated")} at {ThreadPaths.LibraryAsset}");
            return lib;
        }

        static Material CreateOrUpdate(MatSpec s, List<string> problems)
        {
            string shaderName = ShaderName(s.Family);
            var sh = FindShader(shaderName);
            if (sh == null)
            {
                ThreadLog.Problem(problems, $"MaterialFactory: shader '{shaderName}' not found for material {s.Field} (using {ErrorShaderName} until it exists)");
                sh = Shader.Find(ErrorShaderName);
                if (sh == null) return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(s.Field));
            }

            var fresh = new Material(sh) { name = s.Field };
            Configure(fresh, s, sh.name == shaderName);

            string path = MaterialPath(s.Field);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = s.Field;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Applies one contract row to a fresh material (no previous keywords or properties).</summary>
        static void Configure(Material m, MatSpec s, bool realShader)
        {
            Shader sh = m.shader;

            // Colours: contract tokens first, every other colour property of the shader = SALT_WHITE.
            var colours = ContractColours(s);
            var assigned = new HashSet<string>();
            foreach (var kv in colours)
            {
                if (realShader && !m.HasProperty(kv.Key)) ThreadLog.Warn($"material {s.Field}: shader {sh.name} has no colour property {kv.Key}");
                m.SetColor(kv.Key, StoredColour(kv.Value));
                assigned.Add(kv.Key);
            }
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                if (sh.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                string pn = sh.GetPropertyName(i);
                if (assigned.Contains(pn)) continue;
                m.SetColor(pn, StoredColour(Pal.SALT_WHITE));
                if (realShader) ThreadLog.Info($"material {s.Field}: extra colour property {pn} set to SALT_WHITE");
            }

            // Floats (incl. render state).
            foreach (var kv in ContractFloats(s))
            {
                if (realShader && !m.HasProperty(kv.Key)) ThreadLog.Warn($"material {s.Field}: shader {sh.name} has no float property {kv.Key}");
                m.SetFloat(kv.Key, kv.Value);
            }
            // Optional alpha-to-coverage toggle (R8 1.5: the handrail tube uses A2C with MSAA; additive/blended never).
            if (m.HasProperty("_AlphaToMask")) m.SetFloat("_AlphaToMask", UsesAlphaToMask(s) ? 1f : 0f);
            // Per-material shade tint (D-038; a Vector, not a Color, so T-PAL colour checks do not apply to it).
            if (m.HasProperty("_ShadeTint")) m.SetVector("_ShadeTint", s.ShadeTint);

            // Keyword: exactly the contract one (fresh material has none enabled).
            if (!string.IsNullOrEmpty(s.Keyword))
            {
                var lk = sh.keywordSpace.FindKeyword(s.Keyword);
                if (lk.isValid) m.EnableKeyword(lk);
                else
                {
                    m.EnableKeyword(s.Keyword);   // kept as an invalid keyword until the shader declares it
                    if (realShader) ThreadLog.Warn($"material {s.Field}: shader {sh.name} does not declare keyword {s.Keyword}");
                }
            }

            m.renderQueue = s.Queue;
            m.enableInstancing = s.Instancing;
            m.SetShaderPassEnabled("ShadowCaster", s.CastShadows);
            m.SetShaderPassEnabled("DepthOnly", s.DepthPasses);
            m.SetShaderPassEnabled("DepthNormals", s.DepthPasses);
            if (s.Queue > (int)RenderQueue.GeometryLast) m.SetOverrideTag("RenderType", "Transparent");
        }

        /// <summary>Contract check used by Validate and the tests: returns one line per problem.</summary>
        public static void Check(MaterialLibrary lib, List<string> problems)
        {
            if (lib == null) { problems.Add("MaterialLibrary asset missing: run RebuildAll"); return; }
            foreach (var s in Specs())
            {
                FieldInfo field = typeof(MaterialLibrary).GetField(s.Field, BindingFlags.Public | BindingFlags.Instance);
                var m = field != null ? field.GetValue(lib) as Material : null;
                if (m == null) { problems.Add($"material {s.Field}: MaterialLibrary field is null"); continue; }
                string want = ShaderName(s.Family);
                if (m.shader == null || m.shader.name != want) { problems.Add($"material {s.Field}: shader is '{(m.shader != null ? m.shader.name : "null")}', expected '{want}'"); continue; }
                foreach (var kv in ContractColours(s))
                    if (!m.HasProperty(kv.Key)) problems.Add($"material {s.Field}: shader {want} lacks colour property {kv.Key}");
                foreach (var kv in ContractFloats(s))
                {
                    if (!m.HasProperty(kv.Key)) { problems.Add($"material {s.Field}: shader {want} lacks float property {kv.Key}"); continue; }
                    float v = m.GetFloat(kv.Key);
                    if (Mathf.Abs(v - kv.Value) > 1e-5f) problems.Add($"material {s.Field}: {kv.Key} = {v}, contract {kv.Value}");
                }
                int enabled = 0;
                foreach (var k in FamilyKeywords(s.Family)) if (m.IsKeywordEnabled(k)) enabled++;
                if (enabled > 1) problems.Add($"material {s.Field}: {enabled} pattern/mode keywords enabled (max 1)");
                if (!string.IsNullOrEmpty(s.Keyword))
                {
                    if (!m.shader.keywordSpace.FindKeyword(s.Keyword).isValid) problems.Add($"material {s.Field}: shader {want} does not declare keyword {s.Keyword}");
                    else if (!m.IsKeywordEnabled(s.Keyword)) problems.Add($"material {s.Field}: keyword {s.Keyword} not enabled");
                }
                if (m.enableInstancing != s.Instancing) problems.Add($"material {s.Field}: enableInstancing = {m.enableInstancing}, contract {s.Instancing}");
                int wantQueue = s.Queue >= 0 ? s.Queue : m.shader.renderQueue;
                if (m.renderQueue != wantQueue) problems.Add($"material {s.Field}: renderQueue = {m.renderQueue}, contract {wantQueue}");
            }
        }
    }
}
