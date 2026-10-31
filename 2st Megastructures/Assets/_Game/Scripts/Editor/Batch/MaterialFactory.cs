using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// RebuildAll step e (materials from every IRingMaterialDefs owner) and step g (RingAssets.asset).
    ///
    /// Colour policy: RingMaterialDef colours are LINEAR values and the shader must receive exactly those values.
    /// In a linear project Unity treats a plain (non-[HDR]) Color property as sRGB and converts it to linear when it
    /// is passed to the shader (Manual "SL-PropertiesInPrograms": "all material color properties are supplied as sRGB
    /// colors, but are converted into linear values"), while [HDR] colours and Vector properties are passed as stored
    /// (ShaderGraph ColorNode: "HDR color picker assumes Linear space, regular color picker assumes SRGB"; URP's
    /// FBXMaterialDescriptionPreprocessor writes `.gamma` into _BaseColor and the raw value into [HDR] _EmissionColor).
    /// So a plain Color property is written as linear.gamma (the GPU then sees the linear value), an [HDR] Color or a
    /// Vector property is written unchanged. Set <see cref="GammaEncodeLdrColorProperties"/> to false to write every
    /// colour unchanged.
    /// </summary>
    public static class MaterialFactory
    {
        public const string RingAssetsPath = BatchTools.SettingsDir + "/RingAssets.asset";
        public const bool GammaEncodeLdrColorProperties = true;
        const string BuiltinFontName = "LegacyRuntime.ttf";   // research 06 1.5

        public sealed class OwnedDef
        {
            public RingMaterialDef Def;
            public string Owner;
        }

        public sealed class Result
        {
            public readonly SortedDictionary<string, Material> Materials = new SortedDictionary<string, Material>(StringComparer.Ordinal);
            public readonly List<string> Skipped = new List<string>();
        }

        public static string MaterialPath(string id) => BatchTools.MaterialsDir + "/M_" + id + ".mat";

        /// <summary>Discovers every IRingMaterialDefs implementation and collects its defs. Throws on duplicate ids.</summary>
        public static List<OwnedDef> CollectDefs()
        {
            // Test assemblies may define fixture owners (e.g. duplicate-id tests): they never produce project materials.
            var types = TypeCache.GetTypesDerivedFrom<IRingMaterialDefs>()
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) != null)
                .Where(t => t.Assembly.GetName().Name.IndexOf("Test", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();
            var all = new List<OwnedDef>();
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            var dups = new List<string>();
            foreach (var t in types)
            {
                List<RingMaterialDef> defs;
                try
                {
                    var inst = (IRingMaterialDefs)Activator.CreateInstance(t);
                    defs = (inst.Defs() ?? Enumerable.Empty<RingMaterialDef>()).ToList();
                }
                catch (Exception e)
                {
                    BatchLog.Exception("material defs owner " + t.FullName, e);
                    continue;
                }
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrEmpty(d.Id) || string.IsNullOrEmpty(d.Shader))
                    {
                        BatchLog.Error(t.FullName + ": material def with an empty id or shader name");
                        continue;
                    }
                    if (d.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        BatchLog.Error(t.FullName + ": material id '" + d.Id + "' is not a valid file name");
                        continue;
                    }
                    if (owners.TryGetValue(d.Id, out var first))
                    {
                        dups.Add("'" + d.Id + "' (" + first + " and " + t.FullName + ")");
                        continue;
                    }
                    owners[d.Id] = t.FullName;
                    all.Add(new OwnedDef { Def = d, Owner = t.FullName });
                }
            }
            BatchLog.Info("material defs: " + all.Count + " from " + types.Count + " owners (" + string.Join(", ", types.Select(t => t.Name)) + ")");
            if (dups.Count > 0) throw new InvalidOperationException("duplicate material ids: " + string.Join("; ", dups));
            return all;
        }

        /// <summary>Creates or updates Assets/_Game/Materials/M_&lt;id&gt;.mat for every def whose shader exists.</summary>
        public static Result BuildAll()
        {
            var result = new Result();
            EditorIO.EnsureFolder(BatchTools.MaterialsDir);
            foreach (var od in CollectDefs())
            {
                var d = od.Def;
                var shader = Shader.Find(d.Shader);
                if (shader == null)
                {
                    BatchLog.Warn("material " + d.Id + " (" + od.Owner + "): shader '" + d.Shader + "' not found, skipped");
                    result.Skipped.Add(d.Id);
                    continue;
                }
                try { result.Materials[d.Id] = CreateOrUpdate(d, shader); }
                catch (Exception e) { BatchLog.Exception("material " + d.Id, e); }
            }

            var known = new HashSet<string>(result.Materials.Keys.Select(MaterialPath).Concat(result.Skipped.Select(MaterialPath)), StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { BatchTools.MaterialsDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!known.Contains(p)) BatchLog.Warn("orphan material (no def): " + p + " (left in place)");
            }
            BatchLog.Note("materials: " + result.Materials.Count + " written to " + BatchTools.MaterialsDir
                          + (result.Skipped.Count > 0 ? ", skipped (shader missing): " + string.Join(", ", result.Skipped) : ""));
            return result;
        }

        static Material CreateOrUpdate(RingMaterialDef d, Shader shader)
        {
            string path = MaterialPath(d.Id);
            var fresh = new Material(shader) { name = "M_" + d.Id };
            ApplyProperties(fresh, d, shader);
            fresh.renderQueue = d.RenderQueue;          // -1 = shader default
            fresh.enableInstancing = d.EnableInstancing;

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) BatchLog.Warn("replacing a non-material asset at " + path);
                AssetDatabase.CreateAsset(fresh, path);
                BatchLog.Info("material created " + path + " (" + shader.name + ")");
                return fresh;
            }
            // Update in place so the GUID (and every reference) stays stable; the copy resets stale values.
            if (existing.shader != shader) existing.shader = shader;
            existing.CopyPropertiesFromMaterial(fresh);
            existing.renderQueue = d.RenderQueue;
            existing.enableInstancing = d.EnableInstancing;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            BatchLog.Info("material updated " + path + " (" + shader.name + ")");
            return existing;
        }

        static void ApplyProperties(Material m, RingMaterialDef d, Shader sh)
        {
            foreach (var kv in d.Floats.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                int i = sh.FindPropertyIndex(kv.Key);
                if (i < 0) { BatchLog.Warn("M_" + d.Id + ": float '" + kv.Key + "' is not a property of " + sh.name + " (set anyway)"); m.SetFloat(kv.Key, kv.Value); continue; }
                var type = sh.GetPropertyType(i);
                bool gamma = (sh.GetPropertyFlags(i) & ShaderPropertyFlags.Gamma) != 0;
                float v = gamma && GammaEncodeLdrColorProperties ? Mathf.LinearToGammaSpace(kv.Value) : kv.Value;
                if (type == ShaderPropertyType.Int) m.SetInteger(kv.Key, Mathf.RoundToInt(v));
                else if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range) m.SetFloat(kv.Key, v);
                else BatchLog.Warn("M_" + d.Id + ": '" + kv.Key + "' is a " + type + " property, float ignored");
            }
            foreach (var kv in d.Colors.OrderBy(k => k.Key, StringComparer.Ordinal))
                SetLinearColor(m, d.Id, sh, kv.Key, kv.Value);
            foreach (var kv in d.Vectors.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                int i = sh.FindPropertyIndex(kv.Key);
                if (i < 0) { BatchLog.Warn("M_" + d.Id + ": vector '" + kv.Key + "' is not a property of " + sh.name + " (set anyway)"); m.SetVector(kv.Key, kv.Value); continue; }
                var type = sh.GetPropertyType(i);
                if (type == ShaderPropertyType.Color) { SetLinearColor(m, d.Id, sh, kv.Key, new Color(kv.Value.x, kv.Value.y, kv.Value.z, kv.Value.w)); continue; }
                if (type != ShaderPropertyType.Vector) { BatchLog.Warn("M_" + d.Id + ": '" + kv.Key + "' is a " + type + " property, vector ignored"); continue; }
                bool gamma = (sh.GetPropertyFlags(i) & ShaderPropertyFlags.Gamma) != 0;
                var v = kv.Value;
                if (gamma && GammaEncodeLdrColorProperties)
                    v = new Vector4(Mathf.LinearToGammaSpace(v.x), Mathf.LinearToGammaSpace(v.y), Mathf.LinearToGammaSpace(v.z), v.w);
                m.SetVector(kv.Key, v);
            }
        }

        /// <summary>Writes a linear colour so that the shader receives the linear value (see the class comment).</summary>
        static void SetLinearColor(Material m, string id, Shader sh, string name, Color linear)
        {
            int i = sh.FindPropertyIndex(name);
            if (i < 0) { BatchLog.Warn("M_" + id + ": colour '" + name + "' is not a property of " + sh.name + " (set unchanged)"); m.SetColor(name, linear); return; }
            var type = sh.GetPropertyType(i);
            var flags = sh.GetPropertyFlags(i);
            if (type == ShaderPropertyType.Vector)
            {
                bool gammaVec = (flags & ShaderPropertyFlags.Gamma) != 0 && GammaEncodeLdrColorProperties;
                var c = gammaVec ? linear.gamma : linear;
                m.SetVector(name, new Vector4(c.r, c.g, c.b, linear.a));
                return;
            }
            if (type != ShaderPropertyType.Color) { BatchLog.Warn("M_" + id + ": '" + name + "' is a " + type + " property, colour ignored"); return; }
            bool hdr = (flags & ShaderPropertyFlags.HDR) != 0;
            if (hdr || !GammaEncodeLdrColorProperties) { m.SetColor(name, linear); return; }
            var g = linear.gamma;   // sRGB-encode; Unity linearises plain Color properties on upload
            m.SetColor(name, new Color(g.r, g.g, g.b, linear.a));
        }

        // ---------------------------------------------------------------------------------------------- step g
        public static RingAssets WriteRingAssets(Result mats, PanelSettings panel)
        {
            var a = EditorIO.LoadOrCreateAsset(RingAssetsPath, () => ScriptableObject.CreateInstance<RingAssets>());
            var list = new List<RingAssets.Entry>();
            if (mats != null)
                foreach (var kv in mats.Materials) list.Add(new RingAssets.Entry { id = kv.Key, material = kv.Value });
            a.materials = list;
            a.panelSettings = panel;
            a.font = FindBuiltinFont();
            EditorUtility.SetDirty(a);
            BatchLog.Note(RingAssetsPath + ": " + list.Count + " materials, panel " + (panel != null ? "set" : "NULL") + ", font "
                          + (a.font != null ? a.font.name + " (" + AssetDatabase.GetAssetPath(a.font) + ")" : "null (runtime falls back to Resources.GetBuiltinResource)"));
            return a;
        }

        static Font FindBuiltinFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>(BuiltinFontName); }
            catch (Exception e) { BatchLog.Info("Resources.GetBuiltinResource<Font>(" + BuiltinFontName + ") failed: " + e.Message); }
            if (f != null && EditorUtility.IsPersistent(f)) return f;
            try { f = AssetDatabase.GetBuiltinExtraResource<Font>(BuiltinFontName); }
            catch (Exception e) { BatchLog.Info("AssetDatabase.GetBuiltinExtraResource<Font>(" + BuiltinFontName + ") failed: " + e.Message); }
            if (f != null && EditorUtility.IsPersistent(f)) return f;
            BatchLog.Warn("built-in font " + BuiltinFontName + " cannot be referenced as an asset; RingAssets.font left null");
            return null;
        }
    }
}
