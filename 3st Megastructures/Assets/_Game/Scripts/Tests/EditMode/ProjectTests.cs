// ProjectTests: EditMode checks of the generated project state (MASTER_PROMPT 11.8 EditMode tests, 14.10.1
// T-PAL and the static part of T-DEPTH, Part 12 project settings). They read what RebuildAll produced; run
// Megastructure.EditorTools.BatchTools.RebuildAll first. Editor-assembly types (ShaderGen, MaterialFactory)
// are reached by reflection so this file compiles without a direct reference to Megastructure.Editor.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Megastructure.Tests
{
    public class ProjectTests
    {
        const string MaterialsDir = "Assets/_Game/Materials";
        const string LibraryAsset = "Assets/_Game/Materials/MaterialLibrary.asset";
        const string PaletteAsset = "Assets/_Game/Settings/ThreadPalette.asset";
        const string MainScene = "Assets/_Game/Scenes/Main.unity";
        const string PaletteHlsl = "Assets/_Game/Shaders/Generated/ThreadPalette.hlsl";
        const string ConstantsHlsl = "Assets/_Game/Shaders/Generated/ThreadConstantsGen.hlsl";
        const string RpAsset = "Assets/Settings/PC_RPAsset.asset";
        const string RendererAsset = "Assets/Settings/PC_Renderer.asset";
        const string GradeFeatureType = "Megastructure.Rendering.ThreadGradeFeature";
        static readonly string[] ShaderNames =
            { "Megastructure/ThreadFlat", "Megastructure/ThreadLine", "Megastructure/ThreadEmissive", "Megastructure/ThreadSky", "Megastructure/ThreadGrade" };

        /// <summary>14.10.1 T-PAL: material colours within 0.004 linear of a token or of a lerp of two tokens.</summary>
        const float PalTolerance = 0.004f;

        static string Root => Path.GetDirectoryName(Application.dataPath);
        static string Abs(string assetPath) => Path.Combine(Root, assetPath.Replace('/', Path.DirectorySeparatorChar));
        static readonly UTF8Encoding s_Utf8NoBom = new UTF8Encoding(false);

        // ======================================================================================== T-PAL
        static Vector3[] TokenColours()
        {
            var t = ThreadPalette.Table;
            var v = new Vector3[t.Length];
            for (int i = 0; i < t.Length; i++) v[i] = ThreadPalette.LinearV(t[i].id);
            return v;
        }

        static float MaxAbs(Vector3 d) => Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z)));

        /// <summary>Smallest per-channel distance of p to any token or any segment between two tokens.</summary>
        static float PaletteDistance(Vector3 p, Vector3[] tokens, out string nearest)
        {
            float best = float.MaxValue;
            nearest = "";
            for (int i = 0; i < tokens.Length; i++)
            {
                float d = MaxAbs(p - tokens[i]);
                if (d < best) { best = d; nearest = ThreadPalette.Table[i].id.ToString(); }
            }
            for (int i = 0; i < tokens.Length; i++)
                for (int j = i + 1; j < tokens.Length; j++)
                {
                    Vector3 ab = tokens[j] - tokens[i];
                    float len2 = Vector3.Dot(ab, ab);
                    if (len2 < 1e-12f) continue;
                    float t = Mathf.Clamp01(Vector3.Dot(p - tokens[i], ab) / len2);
                    float d = MaxAbs(p - (tokens[i] + ab * t));
                    if (d < best) { best = d; nearest = $"lerp({ThreadPalette.Table[i].id}, {ThreadPalette.Table[j].id}, {t:F3})"; }
                }
            return best;
        }

        [Test]
        public void TPal_MaterialColoursArePaletteTokensOrLerps()
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsDir });
            Assert.Greater(guids.Length, 0, "no materials under " + MaterialsDir + ": run BatchTools.RebuildAll");
            var tokens = TokenColours();
            var failures = new List<string>();
            int checkedProps = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) { failures.Add(path + ": no shader"); continue; }
                var sh = mat.shader;
                int n = sh.GetPropertyCount();
                for (int i = 0; i < n; i++)
                {
                    if (sh.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                    string name = sh.GetPropertyName(i);
                    Color c = mat.GetColor(name);
                    // A plain Color property is stored as sRGB and linearised for the shader; [HDR] colours are stored
                    // linear by SetColor, and GetColor's convention for them is not documented: accept either form.
                    bool hdr = (sh.GetPropertyFlags(i) & ShaderPropertyFlags.HDR) != 0;
                    Color lin = c.linear;
                    float d = PaletteDistance(new Vector3(lin.r, lin.g, lin.b), tokens, out string nearest);
                    if (hdr)
                    {
                        float dRaw = PaletteDistance(new Vector3(c.r, c.g, c.b), tokens, out string nearestRaw);
                        if (dRaw < d) { d = dRaw; nearest = nearestRaw; }
                    }
                    checkedProps++;
                    if (d > PalTolerance)
                        failures.Add($"{path} {name} linear=({lin.r:F4}, {lin.g:F4}, {lin.b:F4}) is {d:F4} from the nearest {nearest}");
                }
            }
            Debug.Log($"[THREAD] T-PAL materials: {guids.Length} materials, {checkedProps} colour properties checked");
            Assert.IsEmpty(failures, "T-PAL material colours:\n" + string.Join("\n", failures));
        }

        static string InvokeEditorStatic(string typeName, string method)
        {
            var t = Type.GetType(typeName + ", Megastructure.Editor");
            Assert.IsNotNull(t, typeName + " not found in Megastructure.Editor");
            var mi = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.IsNotNull(mi, typeName + "." + method + "() not found");
            return (string)mi.Invoke(null, null);
        }

        static string FirstDifference(string expected, string actual)
        {
            var e = expected.Split('\n');
            var a = actual.Split('\n');
            for (int i = 0; i < Math.Max(e.Length, a.Length); i++)
            {
                string el = i < e.Length ? e[i] : "<eof>", al = i < a.Length ? a[i] : "<eof>";
                if (el != al) return $"line {i + 1}: expected '{el}' got '{al}'";
            }
            return "same text, different bytes (BOM or line endings)";
        }

        static void AssertFileBytes(string assetPath, string expected)
        {
            string abs = Abs(assetPath);
            Assert.IsTrue(File.Exists(abs), assetPath + " missing: run RebuildAll or Tools/gen_hlsl.py");
            byte[] want = s_Utf8NoBom.GetBytes(expected);
            byte[] have = File.ReadAllBytes(abs);
            bool same = want.Length == have.Length;
            for (int i = 0; same && i < want.Length; i++) same = want[i] == have[i];
            if (!same) Assert.Fail(assetPath + " differs from ShaderGen: " + FirstDifference(expected, Encoding.UTF8.GetString(have)));
        }

        [Test]
        public void TPal_GeneratedHlslEqualsShaderGen()
        {
            AssertFileBytes(PaletteHlsl, InvokeEditorStatic("Megastructure.EditorTools.ShaderGen", "PaletteHlsl"));
            AssertFileBytes(ConstantsHlsl, InvokeEditorStatic("Megastructure.EditorTools.ShaderGen", "ConstantsHlsl"));
        }

        [Test]
        public void TPal_PaletteAssetEqualsTable()
        {
            var pal = AssetDatabase.LoadAssetAtPath<ThreadPalette>(PaletteAsset);
            Assert.IsNotNull(pal, PaletteAsset + " missing: run RebuildAll");
            var table = ThreadPalette.Table;
            Assert.AreEqual(table.Length, pal.tokens.Count, "token count");
            for (int i = 0; i < table.Length; i++)
            {
                var tok = pal.tokens[i];
                Assert.AreEqual(table[i].id.ToString(), tok.name, "token name at " + i);
                Assert.AreEqual(table[i].hex, tok.hex, "hex of " + tok.name);
                Color want = ThreadPalette.HexToLinear(table[i].hex);
                Assert.Less(MaxAbs(new Vector3(want.r - tok.linear.r, want.g - tok.linear.g, want.b - tok.linear.b)), 1e-6f, "linear value of " + tok.name);
            }
        }

        static string FindPython()
        {
            foreach (var exe in new[] { "python", "py" })
            {
                try
                {
                    var psi = new ProcessStartInfo(exe, "--version") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                    using (var p = Process.Start(psi))
                    {
                        if (p == null) continue;
                        if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } continue; }
                        if (p.ExitCode == 0) return exe;
                    }
                }
                catch (Exception) { /* not on PATH */ }
            }
            return null;
        }

        /// <summary>Runs Tools/gen_hlsl.py on a temporary mirror of its inputs and compares with ShaderGen (byte for byte).</summary>
        [Test]
        public void TPal_ShaderGenEqualsPythonGenerator()
        {
            string py = FindPython();
            if (py == null) Assert.Ignore("python not found on PATH: the Python cross-check is skipped (ShaderGen still checked against the files)");
            string tmp = Path.Combine(Path.GetTempPath(), "thread_gen_hlsl_" + Guid.NewGuid().ToString("N"));
            try
            {
                string core = Path.Combine(tmp, "Assets", "_Game", "Scripts", "Runtime", "Core");
                Directory.CreateDirectory(Path.Combine(tmp, "Tools"));
                Directory.CreateDirectory(core);
                File.Copy(Path.Combine(Root, "Tools", "gen_hlsl.py"), Path.Combine(tmp, "Tools", "gen_hlsl.py"));
                foreach (var f in new[] { "ThreadPalette.cs", "ThreadConstants.cs" })
                    File.Copy(Abs("Assets/_Game/Scripts/Runtime/Core/" + f), Path.Combine(core, f));
                var psi = new ProcessStartInfo(py, "-I \"" + Path.Combine(tmp, "Tools", "gen_hlsl.py") + "\"")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = tmp };
                using (var p = Process.Start(psi))
                {
                    Assert.IsNotNull(p, "could not start " + py);
                    string err = p.StandardError.ReadToEnd();
                    Assert.IsTrue(p.WaitForExit(60000), "gen_hlsl.py timed out");
                    Assert.AreEqual(0, p.ExitCode, "gen_hlsl.py failed: " + err);
                }
                string outDir = Path.Combine(tmp, "Assets", "_Game", "Shaders", "Generated");
                string pyPal = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(outDir, "ThreadPalette.hlsl")));
                string pyTc = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(outDir, "ThreadConstantsGen.hlsl")));
                string csPal = InvokeEditorStatic("Megastructure.EditorTools.ShaderGen", "PaletteHlsl");
                string csTc = InvokeEditorStatic("Megastructure.EditorTools.ShaderGen", "ConstantsHlsl");
                Assert.AreEqual(pyPal.Length, csPal.Length, "palette length differs: " + FirstDifference(pyPal, csPal));
                Assert.AreEqual(pyPal, csPal, "palette: " + FirstDifference(pyPal, csPal));
                Assert.AreEqual(pyTc, csTc, "constants: " + FirstDifference(pyTc, csTc));
            }
            finally
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception) { /* temp folder, best effort */ }
            }
        }

        static readonly Regex s_FloatLiteral = new Regex(@"\bfloat[34]\s*\(", RegexOptions.Compiled);
        static readonly Regex s_HalfNumeric = new Regex(@"\bhalf[34]\s*\(\s*[-+]?[0-9.]", RegexOptions.Compiled);
        static readonly Regex s_PalDefine = new Regex(@"^\s*#define\s+PAL_\w+\s+float3\(", RegexOptions.Compiled);

        /// <summary>Removes // and /* */ comments from one line (block state carried across lines).</summary>
        static string StripComments(string line, ref bool inBlock)
        {
            var sb = new StringBuilder(line.Length);
            for (int i = 0; i < line.Length; i++)
            {
                if (inBlock)
                {
                    if (line[i] == '*' && i + 1 < line.Length && line[i + 1] == '/') { inBlock = false; i++; }
                    continue;
                }
                if (line[i] == '/' && i + 1 < line.Length)
                {
                    if (line[i + 1] == '/') break;
                    if (line[i + 1] == '*') { inBlock = true; i++; continue; }
                }
                sb.Append(line[i]);
            }
            return sb.ToString();
        }

        /// <summary>14.7.8: every float3/float4 constructor in a shader file is a PAL_ macro or on a "pal:ok" line.</summary>
        [Test]
        public void TPal_ShaderLiteralsArePaletteMacrosOrMarked()
        {
            string gameRoot = Abs("Assets/_Game");
            var files = new List<string>();
            foreach (var f in Directory.GetFiles(gameRoot, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".shader" || ext == ".hlsl" || ext == ".cginc") files.Add(f);
            }
            Assert.Greater(files.Count, 0, "no shader files under Assets/_Game");
            var failures = new List<string>();
            int notes = 0;
            string generatedPalette = Path.GetFullPath(Abs(PaletteHlsl));
            foreach (var f in files)
            {
                bool isPalette = string.Equals(Path.GetFullPath(f), generatedPalette, StringComparison.OrdinalIgnoreCase);
                string rel = f.Substring(Root.Length + 1).Replace('\\', '/');
                var lines = File.ReadAllLines(f);
                bool inBlock = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string code = StripComments(line, ref inBlock);
                    if (line.IndexOf("pal:ok", StringComparison.Ordinal) >= 0) continue;
                    if (isPalette && s_PalDefine.IsMatch(line)) continue;
                    if (s_FloatLiteral.IsMatch(code)) failures.Add($"{rel}:{i + 1}: {line.Trim()}");
                    else if (s_HalfNumeric.IsMatch(code))
                    {
                        notes++;
                        Debug.Log($"[THREAD] T-PAL note (half literal, not a failure): {rel}:{i + 1}: {line.Trim()}");
                    }
                }
            }
            Debug.Log($"[THREAD] T-PAL shaders: {files.Count} files scanned, {failures.Count} unmarked float3/float4 literals, {notes} half literals noted");
            Assert.IsEmpty(failures, "unmarked float3/float4 literals (use a PAL_ macro or add // pal:ok):\n" + string.Join("\n", failures));
        }

        // ======================================================================================== build settings / scene
        [Test]
        public void BuildSettings_MainIsTheOnlyEnabledScene()
        {
            var enabled = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s != null && s.enabled) enabled.Add(s.path);
            Assert.AreEqual(1, enabled.Count, "enabled scenes: " + string.Join(", ", enabled));
            Assert.AreEqual(MainScene, enabled[0]);
            Assert.IsTrue(File.Exists(Abs(MainScene)), MainScene + " missing");
        }

        static GameObject FindPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name != parts[0]) continue;
                Transform t = r.transform;
                for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                return t != null ? t.gameObject : null;
            }
            return null;
        }

        [Test]
        public void MainScene_HasNoMissingScriptsAndFollowsTheContract()
        {
            Assert.IsTrue(File.Exists(Abs(MainScene)), MainScene + " missing: run RebuildAll");
            Scene scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                int missing = 0;
                var where = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (n > 0) { missing += n; where.Add(t.name); }
                    }
                Assert.AreEqual(0, missing, "missing scripts on: " + string.Join(", ", where));

                foreach (var p in new[] { "Game", "Player", "Player/CameraRig", "Sun", "World/NearRoot", "World/GroundGroup", "PathGuide" })
                    Assert.IsNotNull(FindPath(scene, p), "scene object " + p + " missing");

                var rig = FindPath(scene, "Player/CameraRig");
                var cam = rig.GetComponent<Camera>();
                Assert.IsNotNull(cam, "CameraRig has no Camera");
                Assert.IsTrue(rig.CompareTag("MainCamera"), "CameraRig is not tagged MainCamera");
                Assert.AreEqual(ThreadConstants.CameraNear, cam.nearClipPlane, 1e-6f, "near plane (14.7.4)");
                Assert.AreEqual(ThreadConstants.CameraFar, cam.farClipPlane, 1e-2f, "far plane (14.7.4)");
                Assert.IsTrue(cam.allowHDR, "camera HDR");
                Assert.AreEqual(CameraClearFlags.Skybox, cam.clearFlags, "camera clear flags");
                var acd = rig.GetComponent<UniversalAdditionalCameraData>();
                Assert.IsNotNull(acd, "UniversalAdditionalCameraData missing");
                Assert.IsFalse(acd.renderPostProcessing, "URP post must be off (ThreadGrade does bloom + grade, R1 option B)");
                Assert.IsFalse(acd.dithering, "URP camera dithering must be off (one dither only, 8.2)");
                Assert.AreEqual(AntialiasingMode.None, acd.antialiasing, "camera AA (MSAA comes from the asset)");
                Assert.IsNotNull(rig.GetComponent<AudioListener>(), "AudioListener missing");

                var sun = FindPath(scene, "Sun").GetComponent<Light>();
                Assert.IsNotNull(sun, "Sun has no Light");
                Assert.AreEqual(LightType.Directional, sun.type);
                Assert.AreEqual(LightShadows.Soft, sun.shadows);

                var gb = FindPath(scene, "Game").GetComponent<GameBootstrap>();
                Assert.IsNotNull(gb, "GameBootstrap missing on Game");
                Assert.IsNotNull(gb.materials, "GameBootstrap.materials");
                Assert.IsNotNull(gb.palette, "GameBootstrap.palette");
                Assert.IsNotNull(gb.nearRoot, "GameBootstrap.nearRoot");
                Assert.IsNotNull(gb.groundGroup, "GameBootstrap.groundGroup");
                Assert.IsNotNull(gb.sun, "GameBootstrap.sun");
                Assert.IsNotNull(gb.mainCamera, "GameBootstrap.mainCamera");
                Assert.IsNotNull(FindPath(scene, "Game").GetComponent<ProgressDirector>(), "ProgressDirector missing");
                Assert.IsNotNull(FindPath(scene, "Game").GetComponent<BeatDirector>(), "BeatDirector missing");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MaterialLibrary_EveryFieldAssignedAndContractHolds()
        {
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryAsset);
            Assert.IsNotNull(lib, LibraryAsset + " missing: run RebuildAll");
            var problems = new List<string>();
            foreach (var f in typeof(MaterialLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(Material)) continue;
                var m = f.GetValue(lib) as Material;
                if (m == null) { problems.Add(f.Name + " is null"); continue; }
                if (m.name != f.Name) problems.Add($"{f.Name}: material asset is named {m.name}");
                if (m.shader == null || !m.shader.name.StartsWith("Megastructure/", StringComparison.Ordinal))
                    problems.Add($"{f.Name}: shader {(m.shader != null ? m.shader.name : "null")}");
            }
            // The full contract (properties, values, keyword, queue, instancing) is MaterialFactory.Check.
            var t = Type.GetType("Megastructure.EditorTools.MaterialFactory, Megastructure.Editor");
            Assert.IsNotNull(t, "MaterialFactory not found");
            var check = t.GetMethod("Check", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(check, "MaterialFactory.Check not found");
            check.Invoke(null, new object[] { lib, problems });
            Assert.IsEmpty(problems, "material contract:\n" + string.Join("\n", problems));
        }

        // ======================================================================================== shaders
        [Test]
        public void Shaders_ExistAndCompileWithoutErrors()
        {
            var failures = new List<string>();
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                // Compile every pass of every library material (its own keyword set) so HLSL errors surface here.
                var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryAsset);
                if (lib != null)
                    foreach (var f in typeof(MaterialLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var m = f.GetValue(lib) as Material;
                        if (m == null || m.shader == null) continue;
                        for (int p = 0; p < m.passCount; p++) ShaderUtil.CompilePass(m, p, true);
                    }
            }
            finally { ShaderUtil.allowAsyncCompilation = async; }

            foreach (var name in ShaderNames)
            {
                var sh = Shader.Find(name);
                if (sh == null) { failures.Add(name + ": not found"); continue; }
                if (ShaderUtil.ShaderHasError(sh)) failures.Add(name + ": ShaderHasError");
                foreach (var msg in ShaderUtil.GetShaderMessages(sh))
                    if (msg.severity == ShaderCompilerMessageSeverity.Error)
                        failures.Add($"{name}: {msg.file}:{msg.line} {msg.message}");
                if (!sh.isSupported) failures.Add(name + ": not supported on " + SystemInfo.graphicsDeviceType);
            }
            Assert.IsEmpty(failures, "shader problems:\n" + string.Join("\n", failures));
        }

        // ======================================================================================== T-DEPTH (static part)
        static bool IsFloatDepth(DepthFormat f, out string desc)
        {
            GraphicsFormat g = f == DepthFormat.Default ? CoreUtils.GetDefaultDepthStencilFormat() : (GraphicsFormat)f;
            desc = f + " -> " + g;
            return g.ToString().IndexOf("SFloat", StringComparison.Ordinal) >= 0;
        }

        [Test]
        public void TDepth_ReversedZAndFloatDepthFormats()
        {
            var dev = SystemInfo.graphicsDeviceType;
            Debug.Log($"[THREAD] T-DEPTH device {dev}, SystemInfo.usesReversedZBuffer = {SystemInfo.usesReversedZBuffer}");
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAsset);
            Assert.IsNotNull(rd, RendererAsset + " missing");
            Assert.IsTrue(IsFloatDepth(rd.depthAttachmentFormat, out string a), "depth attachment must be float (14.7.4): " + a);
            Assert.IsTrue(IsFloatDepth(rd.depthTextureFormat, out string b), "depth texture must be float (14.7.4): " + b);
            Debug.Log($"[THREAD] T-DEPTH attachment {a}, texture {b}");
            bool realDevice = dev == GraphicsDeviceType.Direct3D11 || dev == GraphicsDeviceType.Direct3D12 || dev == GraphicsDeviceType.Vulkan || dev == GraphicsDeviceType.Metal;
            if (realDevice) Assert.IsTrue(SystemInfo.usesReversedZBuffer, "reversed-Z expected on " + dev + " (else enable the two-camera fallback of 14.7.4)");
        }

        // ======================================================================================== settings
        static int ActiveInputHandler()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"))
            {
                if (o == null || o.GetType().Name != "PlayerSettings") continue;
                var p = new SerializedObject(o).FindProperty("activeInputHandler");
                return p != null ? p.intValue : -1;
            }
            return -1;
        }

        [Test]
        public void PlayerSettings_AreConfigured()
        {
            Assert.AreEqual("Thread", PlayerSettings.productName, "productName");
            Assert.AreEqual("Megastructures", PlayerSettings.companyName, "companyName");
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace, "colour space");
            Assert.AreEqual(1, ActiveInputHandler(), "Active Input Handling must be 'Input System Package (New)' only (7.6)");
#if ENABLE_LEGACY_INPUT_MANAGER
            Assert.Fail("ENABLE_LEGACY_INPUT_MANAGER is defined: the legacy input manager is still active (7.6)");
#endif
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("ENABLE_INPUT_SYSTEM is not defined (7.6)");
#endif
            var target = BuildTarget.StandaloneWindows64;
            Assert.IsFalse(PlayerSettings.GetUseDefaultGraphicsAPIs(target), "graphics API must be explicit (8.1)");
            var apis = PlayerSettings.GetGraphicsAPIs(target);
            Assert.GreaterOrEqual(apis.Length, 1, "no graphics API");
            Assert.IsTrue(apis[0] == GraphicsDeviceType.Direct3D11 || apis[0] == GraphicsDeviceType.Direct3D12, "first API is " + apis[0]);
            Assert.AreEqual(ScriptingImplementation.Mono2x, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), "scripting backend (Part 12)");
            Assert.AreEqual(FullScreenMode.FullScreenWindow, PlayerSettings.fullScreenMode, "fullscreen mode");
            Assert.IsTrue(PlayerSettings.resizableWindow, "resizable window");
            Assert.IsTrue(PlayerSettings.runInBackground, "run in background");
            Assert.IsTrue(PlayerSettings.enableFrameTimingStats, "frame timing stats (11.7)");
            Debug.Log($"[THREAD] PlayerSettings: APIs [{string.Join(",", apis)}], splash {PlayerSettings.SplashScreen.show}");
        }

        [Test]
        public void Pipeline_IsConfigured()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAsset);
            Assert.IsNotNull(asset, RpAsset + " missing");
            Assert.AreEqual(asset, GraphicsSettings.defaultRenderPipeline, "default render pipeline");
            Assert.AreEqual(1, QualitySettings.count, "quality levels: " + string.Join(", ", QualitySettings.names));
            Assert.AreEqual(asset, QualitySettings.GetRenderPipelineAssetAt(0), "quality level pipeline");
            Assert.IsTrue(asset.supportsHDR, "HDR");
            Assert.AreEqual(HDRColorBufferPrecision._64Bits, asset.hdrColorBufferPrecision, "HDR precision (14.7.1 item 1)");
            Assert.AreEqual(ThreadConstants.Msaa, asset.msaaSampleCount, "MSAA");
            Assert.AreEqual(1f, asset.renderScale, 1e-6f, "render scale");
            Assert.IsTrue(asset.supportsCameraDepthTexture, "depth texture (halo soft fade)");
            Assert.AreEqual(GPUResidentDrawerMode.Disabled, asset.gpuResidentDrawerMode, "GPU Resident Drawer");
            Assert.IsTrue(asset.supportsMainLightShadows, "main light shadows");
            Assert.AreEqual(4096, asset.mainLightShadowmapResolution, "shadow resolution (14.2.4)");
            Assert.AreEqual(4, asset.shadowCascadeCount, "cascades (8.5)");
            Assert.AreEqual(160f, asset.shadowDistance, 1e-3f, "shadow distance (14.2.4)");
            Assert.IsTrue(asset.useSRPBatcher, "SRP Batcher");

            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAsset);
            Assert.IsNotNull(rd, RendererAsset + " missing");
            Assert.AreEqual(RenderingMode.ForwardPlus, rd.renderingMode, "Forward+ (8.1)");
            ScriptableRendererFeature grade = null;
            foreach (var f in rd.rendererFeatures)
            {
                Assert.IsNotNull(f, "null renderer feature in " + RendererAsset);
                Assert.AreNotEqual("ScreenSpaceAmbientOcclusion", f.GetType().Name, "SSAO is removed on purpose (flat style, vertex AO; PipelineConfigurator.KeepSsao)");
                if (f.GetType().FullName == GradeFeatureType) grade = f;
            }
            Assert.IsNotNull(grade, GradeFeatureType + " missing on " + RendererAsset);
            Assert.IsTrue(grade.isActive, "grade feature inactive");
            var mp = new SerializedObject(grade).FindProperty("material");
            Assert.IsNotNull(mp, "ThreadGradeFeature has no serialized 'material' field");
            var gm = mp.objectReferenceValue as Material;
            Assert.IsNotNull(gm, "ThreadGradeFeature.material is not set");
            Assert.AreEqual("Megastructure/ThreadGrade", gm.shader != null ? gm.shader.name : "", "grade material shader");
        }
    }
}
