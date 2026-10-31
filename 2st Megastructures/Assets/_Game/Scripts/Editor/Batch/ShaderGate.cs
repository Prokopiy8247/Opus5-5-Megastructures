using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Compile + shader gate (research 09 section 4): fails when scripts do not compile, when any shader under
    /// Assets/_Game (Shaders and the beat folders) reports an error (import messages, ShaderHasError, unsupported), when a representative
    /// variant of any pass fails to compile for the player's graphics API, or when a material def's shader is missing
    /// or fails CompilePass. Every message is logged as "[BATCH] SHADER &lt;name&gt;: &lt;file&gt;:&lt;line&gt; ...".
    /// </summary>
    public static class ShaderGate
    {
        /// <summary>The variant URP keeps for Forward+ with cascaded soft shadows (research 09 2.3), filtered per pass.</summary>
        static readonly string[] ForwardPlusKeywords =
            { "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_CLUSTER_LIGHT_LOOP", "INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" };
        const string GameKeywordPrefix = "RING_";
        static readonly ShaderType[] Stages = { ShaderType.Vertex, ShaderType.Fragment };

        static int s_Errors, s_Warnings, s_Variants;

        /// <summary>False under -nographics: device-dependent checks (isSupported, CompilePass) are skipped, the
        /// offline per-variant compile still runs.</summary>
        static bool HasDevice => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        public static int Run()
        {
            s_Errors = 0; s_Warnings = 0; s_Variants = 0;
            if (EditorUtility.scriptCompilationFailed)
            {
                BatchLog.Error("GATE: script compilation failed (see the compiler errors above)");
                return 1;
            }
            BatchLog.Info("GATE: scripts compiled");

            var platform = ProjectSetup.UseD3D12 ? ShaderCompilerPlatform.D3D12 : ShaderCompilerPlatform.D3D11;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                foreach (var hlsl in new[] { ProjectSetup.PaletteHlslPath, ProjectSetup.WorldHlslPath })
                    if (!System.IO.File.Exists(EditorIO.Abs(hlsl))) Fail("GATE: generated include " + hlsl + " is missing (run RebuildAll)");

                var paths = ProjectSetup.FindGameShaderPaths();
                BatchLog.Info("GATE: " + paths.Count + " shaders under " + BatchTools.GameRoot + ", variant platform " + platform
                              + ", editor device " + SystemInfo.graphicsDeviceType + (HasDevice ? "" : " (no device: isSupported / CompilePass skipped)"));
                foreach (var path in paths) CheckShaderFile(path, platform);
                CheckComputeShaders();
                CheckMaterials();
            }
            finally { ShaderUtil.allowAsyncCompilation = prevAsync; }

            BatchLog.Info("GATE RESULT errors=" + s_Errors + " warnings=" + s_Warnings + " variants=" + s_Variants);
            return s_Errors > 0 ? 1 : 0;
        }

        static void Fail(string msg) { s_Errors++; BatchLog.Error(msg); }

        static void CheckShaderFile(string path, ShaderCompilerPlatform platform)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) { Fail("SHADER " + path + ": could not be loaded"); return; }

            int before = s_Errors;
            LogMessages(shader, ShaderUtil.GetShaderMessages(shader), path);
            if (ShaderUtil.ShaderHasError(shader) && s_Errors == before) Fail("SHADER " + shader.name + ": ShaderHasError (no message reported)");
            if (HasDevice && !shader.isSupported) Fail("SHADER " + shader.name + ": not supported on the current graphics device (" + SystemInfo.graphicsDeviceType + ")");
            CompileVariants(shader, path, platform);
            BatchLog.Info("GATE " + shader.name + " (" + path + "): " + (s_Errors == before ? "OK" : (s_Errors - before) + " error(s)"));
        }

        /// <summary>Compute shaders (GPU placement for the indirect draws) anywhere under Assets/_Game.</summary>
        static void CheckComputeShaders()
        {
            var paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(BatchTools.GameRoot)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:ComputeShader", new[] { BatchTools.GameRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".compute", StringComparison.OrdinalIgnoreCase) && !paths.Contains(p)) paths.Add(p);
            }
            paths.Sort(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (cs == null) { Fail("SHADER " + path + ": compute shader could not be loaded"); continue; }
                int before = s_Errors;
                var msgs = ShaderUtil.GetComputeShaderMessages(cs);
                if (msgs != null)
                {
                    foreach (var m in msgs)
                    {
                        string file = string.IsNullOrEmpty(m.file) ? path : m.file;
                        string line = "SHADER " + cs.name + " (compute): " + file + ":" + m.line + " [" + m.platform + "] " + m.severity + ": " + m.message;
                        if (m.severity == ShaderCompilerMessageSeverity.Error) Fail(line);
                        else { s_Warnings++; BatchLog.Info(line); }
                    }
                }
                BatchLog.Info("GATE " + cs.name + " (" + path + "): " + (s_Errors == before ? "OK" : (s_Errors - before) + " error(s)"));
            }
        }

        static void LogMessages(Shader shader, ShaderMessage[] msgs, string fallbackFile)
        {
            if (msgs == null) return;
            foreach (var m in msgs)
            {
                string file = string.IsNullOrEmpty(m.file) ? fallbackFile : m.file;
                string line = "SHADER " + shader.name + ": " + file + ":" + m.line + " [" + m.platform + "] " + m.severity + ": " + m.message
                              + (string.IsNullOrEmpty(m.messageDetails) ? "" : " | " + m.messageDetails.Replace('\n', ' '));
                if (m.severity == ShaderCompilerMessageSeverity.Error) Fail(line);
                else { s_Warnings++; BatchLog.Info(line); }
            }
        }

        static void CompileVariants(Shader shader, string path, ShaderCompilerPlatform platform)
        {
            var data = ShaderUtil.GetShaderData(shader);
            if (data == null) { Fail("SHADER " + shader.name + ": no ShaderData"); return; }
            for (int s = 0; s < data.SubshaderCount; s++)
            {
                var sub = data.GetSubshader(s);
                for (int p = 0; p < sub.PassCount; p++)
                {
                    var pass = sub.GetPass(p);
                    if (pass.IsGrabPass) continue;
                    var pid = new PassIdentifier((uint)s, (uint)p);
                    foreach (var stage in Stages)
                    {
                        if (!pass.HasShaderStage(stage)) continue;
                        foreach (var set in KeywordSets(shader, pid, stage))
                        {
                            s_Variants++;
                            var r = pass.CompileVariant(stage, set, platform, BuildTarget.StandaloneWindows64);
                            string where = shader.name + " pass '" + pass.Name + "' " + stage + " [" + string.Join(" ", set) + "]";
                            if (r.Messages != null)
                            {
                                foreach (var m in r.Messages)
                                {
                                    string file = string.IsNullOrEmpty(m.file) ? path : m.file;
                                    string line = "SHADER " + where + ": " + file + ":" + m.line + " " + m.severity + ": " + m.message;
                                    if (m.severity == ShaderCompilerMessageSeverity.Error) Fail(line);
                                    else { s_Warnings++; BatchLog.Info(line); }
                                }
                            }
                            if (!r.Success && (r.Messages == null || !r.Messages.Any(m => m.severity == ShaderCompilerMessageSeverity.Error)))
                                Fail("SHADER " + where + ": variant compile failed without a message");
                        }
                    }
                }
            }
        }

        /// <summary>No keywords; the Forward+ shadow set; and that set plus each game keyword (RING_*), all filtered to the
        /// keywords the pass declares, so no pass ever receives an undeclared keyword.</summary>
        static List<string[]> KeywordSets(Shader shader, PassIdentifier pid, ShaderType stage)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var k in ShaderUtil.GetPassKeywords(shader, in pid, stage)) declared.Add(k.name);
            }
            catch (Exception e) { BatchLog.Info("GetPassKeywords failed for " + shader.name + ": " + e.Message); }

            var sets = new List<string[]> { Array.Empty<string>() };
            var lit = ForwardPlusKeywords.Where(declared.Contains).ToArray();
            if (lit.Length > 0) sets.Add(lit);
            foreach (var k in declared.Where(k => k.StartsWith(GameKeywordPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal))
                sets.Add(lit.Concat(new[] { k }).ToArray());
            return sets;
        }

        static void CheckMaterials()
        {
            List<MaterialFactory.OwnedDef> defs;
            try { defs = MaterialFactory.CollectDefs(); }
            catch (Exception e) { Fail("GATE: material defs invalid: " + e.Message); return; }

            foreach (var od in defs)
            {
                var d = od.Def;
                var shader = Shader.Find(d.Shader);
                if (shader == null) { Fail("SHADER " + d.Shader + ": not found for material " + d.Id + " (" + od.Owner + ")"); continue; }
                var mat = new Material(shader) { name = "Gate_" + d.Id, hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    int before = s_Errors;
                    if (HasDevice)
                        for (int p = 0; p < mat.passCount; p++) ShaderUtil.CompilePass(mat, p, true);
                    if (ShaderUtil.ShaderHasError(shader)) LogMessages(shader, ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == ShaderCompilerMessageSeverity.Error).ToArray(), d.Shader);
                    if (ShaderUtil.ShaderHasError(shader) && s_Errors == before) Fail("SHADER " + shader.name + ": ShaderHasError after CompilePass (material " + d.Id + ")");

                    var asset = AssetDatabase.LoadAssetAtPath<Material>(MaterialFactory.MaterialPath(d.Id));
                    if (asset == null) BatchLog.Warn("GATE: " + MaterialFactory.MaterialPath(d.Id) + " does not exist yet (run RebuildAll)");
                    else if (asset.shader != shader) Fail("GATE: " + MaterialFactory.MaterialPath(d.Id) + " uses " + (asset.shader != null ? asset.shader.name : "null") + " instead of " + d.Shader);
                }
                finally { Object.DestroyImmediate(mat); }
            }
            BatchLog.Info("GATE: " + defs.Count + " material defs checked");
        }
    }
}
