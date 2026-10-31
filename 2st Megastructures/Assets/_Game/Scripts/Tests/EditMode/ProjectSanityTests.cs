using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Megastructure.Tests
{
    /// <summary>
    /// Project sanity of Part 11.8: build settings contain only Main.unity, the scene and the prefabs have no missing
    /// scripts, every Megastructure shader compiles without errors (ShaderUtil messages, and per-pass D3D11 variant
    /// compiles of the keyword sets the game uses), and the materials of RingAssets reference supported shaders.
    /// </summary>
    public class ProjectSanityTests
    {
        const string MainScene = "Assets/_Game/Scenes/Main.unity";

        [SetUp]
        public void PerTest() { LogAssert.ignoreFailingMessages = true; }

        [Test]
        public void BuildSettings_OnlyMainScene()
        {
            var scenes = EditorBuildSettings.scenes;
            var list = new List<string>();
            foreach (var s in scenes) list.Add(s.path + (s.enabled ? "" : " (disabled)"));
            Assert.AreEqual(1, scenes.Length, "build settings scenes: " + string.Join(", ", list));
            Assert.AreEqual(MainScene, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled, "Main.unity enabled");
        }

        static int MissingScripts(GameObject go, List<string> where, string prefix)
        {
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (n > 0) where.Add(prefix + go.name + " (" + n + ")");
            foreach (Transform c in go.transform) n += MissingScripts(c.gameObject, where, prefix + go.name + "/");
            return n;
        }

        [Test]
        public void MainScene_HasNoMissingScripts()
        {
            if (!System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), MainScene)))
                Assert.Fail(MainScene + " does not exist (BatchTools.RebuildAll writes it)");
            var scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive); opened = true; }
            try
            {
                var where = new List<string>();
                int n = 0;
                foreach (var root in scene.GetRootGameObjects()) n += MissingScripts(root, where, "");
                Assert.AreEqual(0, n, "missing scripts in Main.unity: " + string.Join(", ", where));
                Assert.Greater(scene.rootCount, 0, "Main.unity has root objects");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Prefabs_HaveNoMissingScripts()
        {
            var where = new List<string>();
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) n += MissingScripts(go, where, path + ":");
            }
            Assert.AreEqual(0, n, "missing scripts in prefabs: " + string.Join(", ", where));
        }

        static List<Shader> GameShaders()
        {
            var list = new List<Shader>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/_Game" }))
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) list.Add(s);
            }
            return list;
        }

        [Test]
        public void Shaders_CompileWithoutErrors()
        {
            var shaders = GameShaders();
            Assert.Greater(shaders.Count, 0, "no shaders under Assets/_Game");
            var fails = new StringBuilder();
            int bad = 0;
            foreach (var s in shaders)
            {
                string path = AssetDatabase.GetAssetPath(s);
                if (!s.name.StartsWith("Megastructure/") && !s.name.StartsWith("Hidden/Megastructure"))
                { bad++; fails.AppendLine(path + ": shader name '" + s.name + "' is not Megastructure/..."); }
                if (ShaderUtil.ShaderHasError(s)) { bad++; fails.AppendLine(path + ": ShaderHasError"); }
                foreach (var m in ShaderUtil.GetShaderMessages(s))
                    if (m.severity == ShaderCompilerMessageSeverity.Error) { bad++; fails.AppendLine(path + ":" + m.line + " [" + m.platform + "] " + m.message); }
                if (!s.isSupported) { bad++; fails.AppendLine(path + ": not supported on this GPU/API"); }
            }
            Assert.AreEqual(0, bad, "shader errors:\n" + fails);
        }

        static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_CLUSTER_LIGHT_LOOP" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHT_SHADOWS", "_CLUSTER_LIGHT_LOOP" },
            new[] { "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
        };

        [Test]
        [Category("ShaderVariants")]
        [Timeout(1800000)]
        public void Shaders_VariantsCompileOnD3D11()
        {
            var fails = new StringBuilder();
            int bad = 0, compiled = 0;
            foreach (var s in GameShaders())
            {
                var data = ShaderUtil.GetShaderData(s);
                if (data == null) continue;
                for (int si = 0; si < data.SubshaderCount; si++)
                {
                    var sub = data.GetSubshader(si);
                    for (int pi = 0; pi < sub.PassCount; pi++)
                    {
                        var pass = sub.GetPass(pi);
                        if (pass.IsGrabPass) continue;
                        foreach (var kws in KeywordSets)
                            foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                            {
                                if (!pass.HasShaderStage(stage)) continue;
                                var r = pass.CompileVariant(stage, kws, ShaderCompilerPlatform.D3D11, BuildTarget.StandaloneWindows64);
                                compiled++;
                                if (r.Success) continue;
                                bad++;
                                if (bad <= 30)
                                    foreach (var m in r.Messages)
                                        if (m.severity == ShaderCompilerMessageSeverity.Error)
                                            fails.AppendLine(s.name + " pass '" + pass.Name + "' " + stage + " [" + string.Join(" ", kws) + "] line " + m.line + ": " + m.message);
                            }
                    }
                }
            }
            TestContext.WriteLine("variant compiles: " + compiled);
            Assert.AreEqual(0, bad, "variant compile failures (" + bad + " of " + compiled + "):\n" + fails);
        }

        [Test]
        public void RingAssets_MaterialsHaveSupportedShaders()
        {
            var assets = AssetDatabase.LoadAssetAtPath<RingAssets>("Assets/_Game/Settings/RingAssets.asset");
            if (assets == null) Assert.Ignore("RingAssets.asset not generated yet (BatchTools.RebuildAll)");
            var fails = new List<string>();
            var ids = new HashSet<string>();
            foreach (var e in assets.materials)
            {
                if (!ids.Add(e.id)) fails.Add("duplicate id " + e.id);
                if (e.material == null) { fails.Add(e.id + ": null material"); continue; }
                var sh = e.material.shader;
                if (sh == null || !sh.isSupported || sh.name == "Hidden/InternalErrorShader") fails.Add(e.id + ": shader " + (sh != null ? sh.name : "null") + " unsupported");
            }
            foreach (var f in typeof(RingMat).GetFields())
                if (f.IsLiteral && f.FieldType == typeof(string) && !ids.Contains((string)f.GetRawConstantValue()))
                    TestContext.WriteLine("RingMat id without a material yet: " + f.GetRawConstantValue());
            Assert.AreEqual(0, fails.Count, string.Join("\n", fails));
        }
    }
}
