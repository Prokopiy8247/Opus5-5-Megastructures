using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Megastructure.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Tests
{
    /// <summary>
    /// EditMode checks of the shader and material layer (Part 8.6, 10.10, 14.2): every Megastructure shader compiles without
    /// errors, the lit shaders carry all four URP passes, the material library is complete after MaterialBuilder.BuildAll,
    /// fog-domain constants in HLSL equal the palette, pattern periods divide the 1332 m wrap, and the procedural textures are
    /// deterministic with the documented statistics.
    /// </summary>
    public sealed class ShaderTests
    {
        static readonly string[] RequiredLightModes = { "UniversalForward", "ShadowCaster", "DepthOnly", "DepthNormals" };
        static readonly string[] FourPassShaders = { MaterialBuilder.SurfaceShader, MaterialBuilder.GratingShader, MaterialBuilder.FacadeShader };

        static IEnumerable<string> AllShaderNames()
        {
            for (int i = 0; i < MaterialBuilder.Shaders.GetLength(0); i++) yield return MaterialBuilder.Shaders[i, 0];
        }

        static string ShaderFilePath(string file) => Path.Combine(MaterialBuilder.ShaderFolder, file);

        [Test]
        public void EveryShaderExistsAndCompiles()
        {
            foreach (string name in AllShaderNames())
            {
                Shader s = MaterialBuilder.FindShader(name);
                Assert.IsNotNull(s, "missing shader " + name);
                Assert.AreEqual(name, s.name, "shader name mismatch");
                Assert.IsFalse(ShaderUtil.ShaderHasError(s), "shader has errors: " + name);
                var messages = ShaderUtil.GetShaderMessages(s);
                foreach (var msg in messages)
                {
                    Assert.AreNotEqual(UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, msg.severity,
                        $"{name}: {msg.message} ({msg.file}:{msg.line})");
                }
                Assert.IsTrue(s.isSupported, "shader not supported: " + name);
            }
        }

        [Test]
        public void LitShadersHaveTheFourUrpPasses()
        {
            var tag = new ShaderTagId("LightMode");
            foreach (string name in FourPassShaders)
            {
                Shader s = MaterialBuilder.FindShader(name);
                // Inspect our own SubShader 0 explicitly: in batchmode the active SubShader of a shader with a FallBack is not
                // resolved until first use, so s.passCount can be 0 (verified with BatchTools.DumpShaders).
                // Unity reports the built-in ShadowCaster LightMode upper-cased ("SHADOWCASTER"): compare case-insensitively.
                var modes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (int p = 0; p < s.GetPassCountInSubshader(0); p++) modes.Add(s.FindPassTagValue(0, p, tag).name);
                foreach (string mode in RequiredLightModes)
                    Assert.IsTrue(modes.Contains(mode), $"{name} lacks a pass with LightMode {mode}");
            }
        }

        [Test]
        public void MaterialLibraryIsCompleteAfterBuild()
        {
            MaterialBuilder.BuildAll();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            Assert.IsNotNull(lib, "MaterialLibrary asset missing");
            Assert.AreEqual((int)MatId.Count, lib.materials.Length, "materials[] length");
            for (int i = 0; i < (int)MatId.Count; i++)
            {
                Material m = lib.materials[i];
                Assert.IsNotNull(m, "missing base material " + (MatId)i);
                Assert.IsTrue(m.shader.name.StartsWith("Megastructure/"), $"{(MatId)i} uses {m.shader.name}");
                Assert.AreEqual(MaterialBuilder.MaterialPath((MatId)i), AssetDatabase.GetAssetPath(m), "asset path of " + (MatId)i);
            }
            Assert.IsNotNull(lib.skybox, "skybox material");
            Assert.AreEqual(MaterialBuilder.SkyShader, lib.skybox.shader.name);
            Assert.IsNotNull(lib.finalDither, "final dither material");
            Assert.AreEqual(MaterialBuilder.FinalDitherShader, lib.finalDither.shader.name);
        }

        [Test]
        public void MaterialBuildIsIdempotentAndKeepsGuids()
        {
            MaterialBuilder.BuildAll();
            var before = new string[(int)MatId.Count];
            for (int i = 0; i < before.Length; i++) before[i] = AssetDatabase.AssetPathToGUID(MaterialBuilder.MaterialPath((MatId)i));
            string libGuid = AssetDatabase.AssetPathToGUID(MaterialBuilder.LibraryPath);
            MaterialBuilder.BuildAll();
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(before[i], AssetDatabase.AssetPathToGUID(MaterialBuilder.MaterialPath((MatId)i)), "GUID changed: " + (MatId)i);
            Assert.AreEqual(libGuid, AssetDatabase.AssetPathToGUID(MaterialBuilder.LibraryPath), "library GUID changed");
        }

        [Test]
        public void FogDomainsAndRecipesOfBaseMaterials()
        {
            MaterialBuilder.BuildAll();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            Assert.AreEqual((float)FogDomain.Exterior, lib[MatId.Lattice].GetFloat(ShaderIds.AbDomain), "Lattice domain");
            Assert.AreEqual((float)FogDomain.Exterior, lib[MatId.HoneyTube].GetFloat(ShaderIds.AbDomain), "HoneyTube domain");
            Assert.AreEqual((float)FogDomain.Shaft, lib[MatId.ConcreteBoard].GetFloat(ShaderIds.AbDomain), "ConcreteBoard domain");
            Assert.AreEqual(0f, lib[MatId.ConcreteBoard].GetFloat(ShaderIds.Recipe));
            Assert.AreEqual(2f, lib[MatId.ConcreteCyl].GetFloat(ShaderIds.Recipe));
            Assert.AreEqual(8f, lib[MatId.HoneyTube].GetFloat(ShaderIds.Recipe));
            Assert.AreEqual(115f, lib[MatId.ConcreteCyl].GetFloat("_PanelCount"), "C1: N = round(2 pi 22 / 1.2) = 115");
            Assert.AreEqual(AbyssConst.MIN_LINE_PX, lib[MatId.Cable].GetFloat(ShaderIds.ThinLine), 1e-6f, "Cable ThinLine");
            Assert.LessOrEqual(lib[MatId.SteelWeathered].GetFloat("_RustAmount"), 0.12f + 1e-6f, "rust at most 12 %");
            Assert.AreEqual(MaterialBuilder.RustSteelPainted, lib[MatId.SteelPainted].GetFloat("_RustAmount"), 1e-6f, "painted steel rust");
            Assert.AreEqual(MaterialBuilder.RustSteelWeathered, lib[MatId.SteelWeathered].GetFloat("_RustAmount"), 1e-6f, "weathered steel rust");
            Assert.AreEqual(MaterialBuilder.RustGrating, lib[MatId.Grating].GetFloat("_RustAmount"), 1e-6f, "grating rust");
            foreach (MatId id in new[] { MatId.ConcreteBoard, MatId.ConcreteFloor, MatId.ConcreteCyl, MatId.SteelPainted, MatId.SteelWeathered, MatId.DoorLeaf })
                Assert.AreEqual(MaterialBuilder.BevelWidthDefault, lib[id].GetFloat("_BevelWidth"), 1e-6f, id + " _BevelWidth");
            foreach (MatId id in new[] { MatId.ConcreteBoard, MatId.SteelPainted, MatId.Grating, MatId.Facade, MatId.FacadeModule, MatId.Lattice, MatId.HoneyTube })
                Assert.AreEqual(MaterialBuilder.KeyLightMaskDefault, lib[id].GetFloat("_KeyLightMask"), 1e-6f, id + " _KeyLightMask");
            Assert.AreEqual(AbyssPalette.AMBER_EMISSIVE, lib[MatId.LampAmber].GetFloat(ShaderIds.EmissionStrength), 1e-6f, "amber 8.0");
            Assert.IsTrue(lib[MatId.Glow].enableInstancing, "Glow instanced");
            Assert.IsTrue(lib[MatId.HoneyTube].enableInstancing, "HoneyTube instanced");
            Assert.IsTrue(lib[MatId.FacadeModule].enableInstancing, "FacadeModule instanced");
            Assert.AreEqual(MaterialBuilder.QueueBeams, lib[MatId.BeamWedge].renderQueue, "explicit beam queue");
        }

        static Vector3 ParseFloat3(string text, string constName)
        {
            var m = Regex.Match(text, constName + @"\s*=\s*float3\(\s*([0-9.eE+-]+)\s*,\s*([0-9.eE+-]+)\s*,\s*([0-9.eE+-]+)\s*\)");
            Assert.IsTrue(m.Success, "constant not found in AbyssFog.hlsl: " + constName);
            return new Vector3(
                float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        static float ParseDefine(string text, string name)
        {
            var m = Regex.Match(text, @"#define\s+" + name + @"\s+([0-9.eE+-]+)");
            Assert.IsTrue(m.Success, "define not found: " + name);
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        static void AssertColor(Vector3 hlsl, Color srgb, string what)
        {
            Color lin = AbyssPalette.Lin(srgb);
            Assert.AreEqual(lin.r, hlsl.x, 2e-5f, what + ".r");
            Assert.AreEqual(lin.g, hlsl.y, 2e-5f, what + ".g");
            Assert.AreEqual(lin.b, hlsl.z, 2e-5f, what + ".b");
        }

        [Test]
        public void FogDomainConstantsMatchThePalette()
        {
            string text = File.ReadAllText(ShaderFilePath("Include/AbyssFog.hlsl"));
            AssertColor(ParseFloat3(text, "AB_TUNNEL_FOG"), AbyssPalette.TUNNEL_FOG, "tunnel fog");
            AssertColor(ParseFloat3(text, "AB_EXT_FOG_DOWN"), AbyssPalette.EXT_FOG_DOWN, "exterior FOG_DOWN");
            AssertColor(ParseFloat3(text, "AB_EXT_FOG_HORIZ"), AbyssPalette.OUT_HORIZ, "exterior FOG_HORIZ");
            AssertColor(ParseFloat3(text, "AB_EXT_FOG_UP"), AbyssPalette.OUT_ZENITH, "exterior FOG_UP");
            Assert.AreEqual(AbyssPalette.TUNNEL_D50, ParseDefine(text, "AB_TUNNEL_D50"), 1e-4f, "tunnel D50");
            Assert.AreEqual(AbyssPalette.EXT_D50, ParseDefine(text, "AB_EXT_D50"), 1e-4f, "exterior D50");
            Assert.AreEqual(AbyssPalette.EXT_M_DOWN, ParseDefine(text, "AB_EXT_M_DOWN"), 1e-6f, "exterior m_down");
            Assert.AreEqual(0.15f, ParseDefine(text, "AB_EXT_LOBE_CAP"), 1e-6f, "exterior lobe cap");
            Assert.AreEqual(0.55f, ParseDefine(text, "AB_HG_G"), 1e-6f, "HG g");
            // no pure black anywhere in the fog sets (14.2: minimum linear 0.002)
            Assert.GreaterOrEqual(ParseFloat3(text, "AB_TUNNEL_FOG").x, AbyssPalette.MIN_LINEAR);
            Assert.GreaterOrEqual(ParseFloat3(text, "AB_EXT_FOG_DOWN").x, AbyssPalette.MIN_LINEAR);
        }

        [Test]
        public void NoUnityFogKeywordsInAnyShader()
        {
            foreach (string file in Directory.GetFiles(MaterialBuilder.ShaderFolder, "*.*", SearchOption.AllDirectories))
            {
                if (!(file.EndsWith(".shader") || file.EndsWith(".hlsl"))) continue;
                string text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains("multi_compile_fog"), file + " uses Unity fog");
                Assert.IsFalse(Regex.IsMatch(text, @"\bFOG_(LINEAR|EXP|EXP2)\b"), file + " uses Unity fog keywords");
                Assert.IsFalse(text.Contains("_FORWARD_PLUS"), file + " uses the deprecated _FORWARD_PLUS keyword");
            }
        }

        [Test]
        public void PatternPeriodsDivideTheNoiseWrap()
        {
            string text = File.ReadAllText(ShaderFilePath("Include/AbyssSurfaceRecipes.hlsl"));
            // (period define, count define) pairs: count * period must equal NOISE_WRAP exactly
            string[,] pairs =
            {
                { "AB_BOARD_H", "AB_BOARD_ROWS" }, { "AB_BOARD_L", "AB_BOARD_COLS" }, { "AB_TIE_PU", "AB_TIE_PU_COUNT" },
                { "AB_LIFT_H", "AB_LIFT_COUNT" }, { "AB_EXP_P", "AB_EXP_COUNT" }, { "AB_MACRO_A", "AB_MACRO_A_N" },
                { "AB_MACRO_B", "AB_MACRO_B_N" }, { "AB_FLOOR_SAW_P", "AB_FLOOR_SLAB_N" },
            };
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                double period = ParseDefine(text, pairs[i, 0]);
                double count = ParseDefine(text, pairs[i, 1]);
                Assert.AreEqual(AbyssConst.NOISE_WRAP, period * count, 1e-3, $"{pairs[i, 0]} x {pairs[i, 1]}");
            }
            // tie-hole vertical pitch, streak repeats and the noise-texture tiles used by the recipes
            // + R1 tiles: patch lookups 14.8 / 9.25 (steel) and 7.4 / 4.5 (grating), mottling 3.7, aggregate 0.9, grain 0.6 / 7.2,
            // slab joints 6 / 12, grime 6, deck scuffs 1.2 / 2.4
            // + R2 tiles: rust runs 44.4 / 27.75 (3 x the patch tiles), stain bombs 7.4 / 4.44 on 6 m cells, plate 1.2 x 2.4
            double[] others = { 1.0, 12.0, 36.0, 0.75, 1.5, 3.0, 7.4, 4.5, 3.7, 0.9, 0.6, 7.2, 6.0, 14.8, 9.25, 1.2, 2.4, 44.4, 27.75, 4.44 };
            foreach (double p in others)
            {
                double n = AbyssConst.NOISE_WRAP / p;
                Assert.AreEqual(System.Math.Round(n), n, 1e-6, "period " + p + " does not divide 1332");
            }
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            Assert.AreEqual(AbyssConst.NOISE_WRAP, ParseDefine(common, "AB_NOISE_WRAP"), 1e-4f, "AB_NOISE_WRAP");
        }

        [Test]
        public void ProceduralTexturesAreDeterministicAndCalibrated()
        {
            Color32[] a = ProceduralTextures.BuildNoisePixels();
            Color32[] b = ProceduralTextures.BuildNoisePixels();
            Assert.AreEqual(ProceduralTextures.NoiseSize * ProceduralTextures.NoiseSize, a.Length);
            double meanR = 0;
            int rustLike = 0;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].r, b[i].r);
                Assert.AreEqual(a[i].g, b[i].g);
                meanR += a[i].r;
                if (a[i].g >= 225) rustLike++;   // threshold 0.88 -> about 12 % of the surface
            }
            meanR /= a.Length;
            Assert.AreEqual(127.5, meanR, 1.0, "equalised noise mean");
            Assert.AreEqual(0.12, rustLike / (double)a.Length, 0.01, "equalised coverage of a 0.88 threshold");

            Color32[] s = ProceduralTextures.BuildStreakPixels();
            Assert.AreEqual(ProceduralTextures.StreakWidth * ProceduralTextures.StreakHeight, s.Length);
            double meanS = 0;
            for (int i = 0; i < s.Length; i++) meanS += s[i].r;
            meanS /= s.Length;
            Assert.AreEqual(127.5, meanS, 2.0, "streak mean 0.5");
        }

        [Test]
        public void ProceduralTexturesBindGlobals()
        {
            ProceduralTextures.Release();
            ProceduralTextures.Generate(null);
            Texture2D noise = ProceduralTextures.NoiseTex;
            Texture2D streak = ProceduralTextures.StreakTex;
            Assert.IsNotNull(noise);
            Assert.IsNotNull(streak);
            Assert.AreEqual(256, noise.width);
            Assert.AreEqual(512, streak.width);
            Assert.AreEqual(128, streak.height);
            Assert.Greater(noise.mipmapCount, 1, "noise mipmaps");
            Assert.Greater(streak.mipmapCount, 1, "streak mipmaps");
            Assert.AreEqual(16, noise.anisoLevel);
            Assert.AreEqual(TextureWrapMode.Repeat, noise.wrapMode);
            Assert.AreSame(noise, Shader.GetGlobalTexture(ProceduralTextures.NoiseTexId));
            Assert.AreSame(streak, Shader.GetGlobalTexture(ProceduralTextures.StreakTexId));
            ProceduralTextures.Release();
        }

        // ------------------------------------------------------------------------------------------ ConcreteCyl u tangent
        // CPU mirror of AbRecipeConcreteCyl's tangent: AbWallTangent(N), then oriented by sum_i dot(dP_i, Tu) du_i. Any two
        // vectors spanning the tangent plane with their UV0.x increments stand in for the screen derivatives (ddx, ddy);
        // the triangle edges are such a pair.
        static Vector3 CylTangentRaw(Vector3 n)
        {
            var t = new Vector2(n.z, -n.x);
            float l = t.magnitude;
            t = l > 1e-4f ? t / l : new Vector2(1f, 0f);
            return new Vector3(t.x, 0f, t.y);
        }

        static void CheckLatheTangents(bool inside, out int checkedTris, out int rawAgrees)
        {
            var profile = new List<Vector2> { new Vector2(22f, 0f), new Vector2(22f, 6f) };
            var mb = new MeshBuilder();
            MeshKit.Lathe(mb, profile, 48, inside: inside);
            checkedTris = 0;
            rawAgrees = 0;
            for (int i = 0; i + 2 < mb.Indices.Count; i += 3)
            {
                int a = mb.Indices[i], b = mb.Indices[i + 1], c = mb.Indices[i + 2];
                Vector3 n = (mb.Normals[a] + mb.Normals[b] + mb.Normals[c]).normalized;
                if (Mathf.Abs(n.y) >= 0.6f) continue;                       // the recipe's wall branch only
                Vector3 e1 = mb.Positions[b] - mb.Positions[a], e2 = mb.Positions[c] - mb.Positions[a];
                float du1 = mb.UV0[b].x - mb.UV0[a].x, du2 = mb.UV0[c].x - mb.UV0[a].x;
                // in-plane gradient of UV0.x: g = p e1 + q e2 with g.e1 = du1, g.e2 = du2
                float g11 = Vector3.Dot(e1, e1), g12 = Vector3.Dot(e1, e2), g22 = Vector3.Dot(e2, e2);
                float det = g11 * g22 - g12 * g12;
                if (det < 1e-10f) continue;
                Vector3 grad = ((du1 * g22 - du2 * g12) / det) * e1 + ((du2 * g11 - du1 * g12) / det) * e2;
                if (grad.sqrMagnitude < 1e-8f) continue;
                Vector3 tu = CylTangentRaw(n);
                if (Vector3.Dot(tu, grad) > 0f) rawAgrees++;
                float orient = Vector3.Dot(e1, tu) * du1 + Vector3.Dot(e2, tu) * du2;
                Vector3 tuOriented = orient >= 0f ? tu : -tu;
                Assert.Greater(Vector3.Dot(tuOriented, grad.normalized), 0.99f,
                    $"oriented Tu must follow +UV0.x (inside {inside}, triangle {i / 3})");
                checkedTris++;
            }
        }

        [Test]
        public void ConcreteCylTangentFollowsUv0OnColumnsAndBores()
        {
            string text = File.ReadAllText(ShaderFilePath("Include/AbyssSurfaceRecipes.hlsl"));
            Assert.IsTrue(Regex.IsMatch(text,
                    @"uOrient\s*=\s*dot\(ri\.dPdx,\s*Tu\)\s*\*\s*ri\.duvdx\.x\s*\+\s*dot\(ri\.dPdy,\s*Tu\)\s*\*\s*ri\.duvdy\.x"),
                "ConcreteCyl orients Tu from the screen derivatives of UV0.x (mirrored here)");
            Assert.IsTrue(Regex.IsMatch(text, @"Tu\s*\*=\s*uOrient\s*>=\s*0\.0\s*\?\s*1\.0\s*:\s*-1\.0"), "ConcreteCyl Tu sign flip");

            CheckLatheTangents(false, out int outTris, out int outRaw);
            CheckLatheTangents(true, out int inTris, out int inRaw);
            Assert.Greater(outTris, 0, "outward column triangles checked");
            Assert.Greater(inTris, 0, "bore triangles checked");
            // the reason for the orientation step: the raw wall tangent is -dP/du on outward columns, +dP/du on bores
            Assert.AreEqual(0, outRaw, "raw AbWallTangent on an outward lathe points toward decreasing UV0.x");
            Assert.AreEqual(inTris, inRaw, "raw AbWallTangent on a bore points toward increasing UV0.x");
        }

        // ------------------------------------------------------------------------------------------ BeamWedge mode 2
        static float Smootherstep01(float x)
        {
            float t = Mathf.Clamp01(x);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        // CPU mirror of BeamFrag's density on the centre ray of a mode-2 prism (x = 0, the ray along the extrusion, so
        // s = t - t0). fixedRule = the feather rule of mode 2; false = the fan feathers (the defect being guarded).
        static float RectCentreDensity(float s, float t, float len, Vector4 feather, Vector4 rect, float pixelAngle, bool fixedRule)
        {
            float fBase = Mathf.Max(feather.x, feather.y * t * pixelAngle);
            float f, fc, axialIn;
            if (fixedRule)
            {
                f = Mathf.Min(fBase, Mathf.Max(0.5f * rect.x, 1e-3f));
                fc = Mathf.Min(Mathf.Max(fBase, 0f), Mathf.Max(0.5f * rect.y, 1e-3f));
                axialIn = Mathf.Max(Mathf.Min(feather.w, 0.25f * len), 1e-3f);
            }
            else
            {
                f = fBase;
                fc = Mathf.Max(f, feather.z);
                axialIn = Mathf.Max(feather.w, 1e-3f);
            }
            float sx = Smootherstep01(rect.x / f), sy = Smootherstep01(rect.y / fc);
            return sx * sx * sy * sy * Smootherstep01(s / axialIn) * (1f - Smootherstep01((s - 0.55f * len) / (0.45f * len)));
        }

        static float RectCentreIntegral(float len, float t0, Vector4 feather, Vector4 rect, float pixelAngle, bool fixedRule)
        {
            const int steps = 4000;
            double sum = 0;
            float ds = len / steps;
            for (int k = 0; k < steps; k++)
            {
                float s = (k + 0.5f) * ds;
                sum += RectCentreDensity(s, t0 + s, len, feather, rect, pixelAngle, fixedRule) * ds;
            }
            return (float)sum;
        }

        [Test]
        public void BeamWedgeRectPrismKeepsItsCentreDensity()
        {
            string shader = File.ReadAllText(ShaderFilePath("BeamWedge.shader"));
            Assert.IsTrue(Regex.IsMatch(shader, @"fMaxX\s*=\s*rect\s*\?\s*max\(0\.5\s*\*\s*_BeamRect\.x"), "mode-2 side feather cap");
            Assert.IsTrue(Regex.IsMatch(shader, @"fMaxY\s*=\s*rect\s*\?\s*max\(0\.5\s*\*\s*_BeamRect\.y"), "mode-2 y feather cap");
            Assert.IsTrue(Regex.IsMatch(shader, @"chordF\s*=\s*rect\s*\?\s*0\.0\s*:\s*_BeamFeather\.z"), "no chord feather in mode 2");
            Assert.IsTrue(Regex.IsMatch(shader, @"axialIn\s*=\s*max\(min\(_BeamFeather\.w,\s*0\.25\s*\*\s*len\)"), "axial fade-in cap");

            MaterialBuilder.BuildAll();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            Material m = lib[MatId.BeamWedge];
            Vector4 feather = m.GetVector("_BeamFeather");
            Vector4 rect = m.GetVector("_BeamRect");
            float pixelAngle = ParseDefine(File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl")), "AB_DEFAULT_PIXEL_ANGLE");
            // the gallery door (1.2 x 2.0 m) is what the base rectangle describes
            Assert.AreEqual(0.5f * AbyssConst.EXIT_DOOR_W, rect.x, 1e-4f, "_BeamRect.x = door half width");
            Assert.AreEqual(0.5f * AbyssConst.EXIT_DOOR_H, rect.y, 1e-4f, "_BeamRect.y = door half height");

            float[] lengths = { 4f, 8f, 16f, 30f, 60f };
            float[] cameraDistances = { 0.5f, 5f, 25f, 80f };
            foreach (float len in lengths)
            foreach (float t0 in cameraDistances)
            {
                float integral = RectCentreIntegral(len, t0, feather, rect, pixelAngle, true);
                Assert.Greater(integral, 0.5f * len, $"mode-2 centre-ray density, length {len} m, camera {t0} m before the prism");
                Assert.LessOrEqual(integral, len + 1e-3f, "density never exceeds 1");
            }
            // the mirror reproduces the defect it guards against (fan feathers on a 0.6 x 1.0 m rectangle)
            Assert.Less(RectCentreIntegral(30f, 5f, feather, rect, pixelAngle, false), 1e-3f * 30f, "fan feathers starve the rectangle");
        }

        // ------------------------------------------------------------------------------------------ constant-domain ambient
        [Test]
        public void ConstantDomainAmbientMatchesTheLookRows()
        {
            string lighting = File.ReadAllText(ShaderFilePath("Include/AbyssLighting.hlsl"));
            float scale = ParseDefine(lighting, "AB_AMB_DOMAIN_SCALE");
            Assert.AreEqual(ProgressTables.Rows[ProgressTables.ROW_TUNNEL].AmbientScale, scale, 1e-6f, "tunnel ambient scale");
            Assert.AreEqual(ProgressTables.Rows[ProgressTables.ROW_OUTSIDE].AmbientScale, scale, 1e-6f, "exterior ambient scale");
            Assert.IsTrue(Regex.IsMatch(lighting, @"float3\s+AbTrilight\(\s*float3\s+n\s*,\s*int\s+domain\s*\)"), "AbTrilight takes the domain");
            Assert.IsTrue(Regex.IsMatch(lighting, @"up\s*=\s*AB_TUNNEL_FOG\s*\*\s*AB_AMB_DOMAIN_SCALE"), "tunnel trilight");
            Assert.IsTrue(Regex.IsMatch(lighting, @"up\s*=\s*AB_EXT_FOG_UP\s*\*\s*AB_AMB_DOMAIN_SCALE"), "exterior sky term = OUT_ZENITH");
            Assert.IsTrue(Regex.IsMatch(lighting, @"mid\s*=\s*AB_EXT_FOG_HORIZ\s*\*\s*AB_AMB_DOMAIN_SCALE"), "exterior equator = OUT_HORIZ");
            Assert.IsTrue(Regex.IsMatch(lighting, @"down\s*=\s*AB_EXT_FOG_DOWN\s*\*\s*AB_AMB_DOMAIN_SCALE"), "exterior ground = FOG_DOWN");
            Assert.IsTrue(Regex.IsMatch(lighting, @"AbTrilight\(N,\s*domain\)"), "AbyssShadeLit passes its domain");
            foreach (string file in Directory.GetFiles(MaterialBuilder.ShaderFolder, "*.*", SearchOption.AllDirectories))
            {
                if (!(file.EndsWith(".shader") || file.EndsWith(".hlsl"))) continue;
                string text = File.ReadAllText(file);
                Assert.IsFalse(Regex.IsMatch(text, @"AbTrilight\(\s*[\w.]+\s*\)"), file + " calls AbTrilight without a domain");
            }
        }

        // ------------------------------------------------------------------------------------------ SRP Batcher layout audit
        static readonly string[] RenderStateOnlyProperties = { "_Cull", "_ZTest", "_AlphaToMask" };

        static string PropertiesBlock(string text)
        {
            int p = text.IndexOf("Properties", System.StringComparison.Ordinal);
            if (p < 0) return string.Empty;
            int open = text.IndexOf('{', p);
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) return text.Substring(open + 1, i - open - 1);
            }
            return string.Empty;
        }

        static HashSet<string> PropertyNames(string text)
        {
            var set = new HashSet<string>();
            foreach (Match m in Regex.Matches(PropertiesBlock(text), "(_[A-Za-z][A-Za-z0-9_]*)\\s*\\(\\s*\""))
                set.Add(m.Groups[1].Value);
            return set;
        }

        static HashSet<string> CBufferMembers(string text, out int count, out int start)
        {
            var set = new HashSet<string>();
            var starts = Regex.Matches(text, @"CBUFFER_START\(\s*UnityPerMaterial\s*\)");
            count = starts.Count;
            start = count > 0 ? starts[0].Index : -1;
            if (count == 0) return set;
            int end = text.IndexOf("CBUFFER_END", start, System.StringComparison.Ordinal);
            string body = text.Substring(start, end - start);
            foreach (Match m in Regex.Matches(body, @"\b(?:float|half|int|uint|bool)(?:[1-4](?:x[1-4])?)?\s+(_[A-Za-z0-9_]+)\s*;"))
                set.Add(m.Groups[1].Value);
            return set;
        }

        /// <summary>
        /// SRP Batcher contract (docs/SHADERS.md 1): every shader declares at most ONE UnityPerMaterial CBUFFER, in the
        /// SubShader-level HLSLINCLUDE (before the first Pass, so every pass compiles the identical layout); every material
        /// property except render-state-only ones is in it and every member is a material property; no include declares a
        /// material CBUFFER and nothing opens a second per-material buffer (UNITY_INSTANCING_BUFFER_START).
        /// </summary>
        [Test]
        public void EveryShaderHasOneMaterialCBufferCoveringItsProperties()
        {
            for (int i = 0; i < MaterialBuilder.Shaders.GetLength(0); i++)
            {
                string file = MaterialBuilder.Shaders[i, 1];
                string text = File.ReadAllText(ShaderFilePath(file));
                HashSet<string> props = PropertyNames(text);
                HashSet<string> members = CBufferMembers(text, out int count, out int start);
                Assert.LessOrEqual(count, 1, file + ": more than one UnityPerMaterial CBUFFER");
                Assert.IsFalse(text.Contains("UNITY_INSTANCING_BUFFER_START"), file + ": per-instance material buffer breaks the SRP Batcher");
                int firstPass = Regex.Match(text, @"\bPass\s*\{").Index;
                if (count == 1) Assert.Less(start, firstPass, file + ": the material CBUFFER must be in the SubShader HLSLINCLUDE");
                foreach (string p in props)
                {
                    if (System.Array.IndexOf(RenderStateOnlyProperties, p) >= 0) continue;
                    Assert.IsTrue(members.Contains(p), $"{file}: property {p} is not in UnityPerMaterial");
                }
                foreach (string m in members) Assert.IsTrue(props.Contains(m), $"{file}: CBUFFER member {m} is not a material property");
                if (text.Contains("#pragma vertex AbVert"))
                {
                    foreach (string req in new[] { "_AbFade", "_ThinLine", "_DistFade" })
                        Assert.IsTrue(members.Contains(req), $"{file}: AbyssPasses.hlsl needs {req} in UnityPerMaterial");
                }
            }
            foreach (string inc in Directory.GetFiles(Path.Combine(MaterialBuilder.ShaderFolder, "Include"), "*.hlsl"))
                Assert.IsFalse(Regex.IsMatch(File.ReadAllText(inc), @"CBUFFER_START\(\s*UnityPerMaterial"), inc + " declares a material CBUFFER");
        }

        /// <summary>
        /// Tier fade, distance fade and fog in every pass that must have them: opaque forward passes clip by _AbFade and
        /// _DistFade and end in AbyssApplyFog; depth-type passes use the shared AbyssPasses fragments (AbDepthClip);
        /// additive shaders scale by |_AbFade| and fog additively.
        /// </summary>
        [Test]
        public void EveryPassAppliesFadeAndFog()
        {
            string[] opaque = { "Surface.shader", "Grating.shader", "Facade.shader", "Emissive.shader", "Lattice.shader" };
            foreach (string file in opaque)
            {
                string text = File.ReadAllText(ShaderFilePath(file));
                Assert.IsTrue(text.Contains("AbFadeClip(_AbFade, input.positionCS.xy)"), file + ": forward pass without _AbFade");
                Assert.IsTrue(text.Contains("AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy)"), file + ": forward pass without _DistFade");
                Assert.IsTrue(Regex.IsMatch(text, @"AbyssApplyFog\([^;]*\(int\)_AbDomain\)|AbyssApplyFog\(color, input\.positionWS, domain\)"), file + ": forward pass without fog");
                // every depth-type pass uses the shared vertex function and the AbDepthClip fragments of AbyssPasses.hlsl
                string[] chunks = Regex.Split(text, @"\bPass\s*\{");
                for (int c = 1; c < chunks.Length; c++)
                {
                    Match mode = Regex.Match(chunks[c], @"""LightMode""\s*=\s*""(\w+)""");
                    Assert.IsTrue(mode.Success, file + ": pass without LightMode");
                    string lm = mode.Groups[1].Value;
                    Assert.IsTrue(chunks[c].Contains("#pragma vertex AbVert"), $"{file} {lm}: not the shared vertex function");
                    if (lm == "ShadowCaster") Assert.IsTrue(chunks[c].Contains("#pragma fragment AbShadowFrag"), file + ": ShadowCaster fragment");
                    if (lm == "DepthOnly") Assert.IsTrue(chunks[c].Contains("#pragma fragment AbDepthOnlyFrag"), file + ": DepthOnly fragment");
                    if (lm == "DepthNormals") Assert.IsTrue(chunks[c].Contains("#pragma fragment AbDepthNormalsFrag"), file + ": DepthNormals fragment");
                }
            }
            string passes = File.ReadAllText(ShaderFilePath("Include/AbyssPasses.hlsl"));
            Assert.IsTrue(Regex.IsMatch(passes, @"void AbDepthClip\(AbVaryings input\)\s*\{\s*AbFadeClip\(_AbFade"), "depth passes clip by _AbFade");
            Assert.AreEqual(3, Regex.Matches(passes, @"AbDepthClip\(input\);").Count, "shadow, depth and depth-normals fragments all call AbDepthClip");
            Assert.IsTrue(passes.Contains("AbThinLineExpand(positionWS, normalWS, input.uv1, input.uv2, _ThinLine, coverage)"), "one vertex function expands ThinLine for every pass");
            foreach (string file in new[] { "Glow.shader", "BeamWedge.shader" })
            {
                string text = File.ReadAllText(ShaderFilePath(file));
                Assert.IsTrue(text.Contains("abs(_AbFade)"), file + ": additive visibility = |_AbFade|");
                Assert.IsTrue(text.Contains("AbyssFogAdditive("), file + ": additive fog");
            }
        }

        // ------------------------------------------------------------------------------------------ Key light mask
        [Test]
        public void LitShadersMaskOnlyTheKeyLight()
        {
            string[] lit = { "Surface.shader", "Grating.shader", "Facade.shader", "Lattice.shader" };
            foreach (string file in lit)
            {
                string text = File.ReadAllText(ShaderFilePath(file));
                Assert.IsTrue(Regex.IsMatch(PropertiesBlock(text), @"_KeyLightMask\s*\(\s*""[^""]*""\s*,\s*Range\(\s*0\s*,\s*1\s*\)\s*\)\s*=\s*1\b"),
                    file + ": _KeyLightMask Range(0, 1) = 1");
                Assert.IsTrue(CBufferMembers(text, out _, out _).Contains("_KeyLightMask"), file + ": _KeyLightMask in UnityPerMaterial");
                bool usesShadeLit = text.Contains("AbyssShadeLit(s, inputData, (int)_AbDomain, _KeyLightMask)");
                bool scalesSun = Regex.IsMatch(text, @"sun\.color\s*\*=\s*saturate\(_KeyLightMask\)");
                Assert.IsTrue(usesShadeLit || scalesSun, file + ": _KeyLightMask is not applied to the main light");
            }
            string lighting = File.ReadAllText(ShaderFilePath("Include/AbyssLighting.hlsl"));
            Assert.IsTrue(Regex.IsMatch(lighting, @"AbDirectLight\(brdf, mainLight, N, V\)\s*\*\s*saturate\(keyLightMask\)"), "main light scaled by the mask");
            Assert.AreEqual(1, Regex.Matches(lighting, @"saturate\(keyLightMask\)").Count, "the mask is applied once (main light only)");
        }

        // ------------------------------------------------------------------------------------------ rust
        static float SmoothstepHlsl(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(b - a, 1e-4f));
            return t * t * (3f - 2f * t);
        }

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// 14.2 RUST, "at most 12 % of a surface set", and the g01 review (saturated orange blotches on the lift deck): CPU
        /// mirror of AbRustMask / AbRustColor on the real NoiseTex channels. The area with mask > 0.5 equals _RustAmount and
        /// the mean weight never exceeds it; the rust albedo keeps about the paint's luma and at most 35 % HSV saturation.
        /// </summary>
        [Test]
        public void RustMaskIsCoverageExactAndSubtle()
        {
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            float rustMax = ParseDefine(common, "AB_RUST_MAX");
            float chroma = ParseDefine(common, "AB_RUST_CHROMA");
            float lumaK = ParseDefine(common, "AB_RUST_LUMA");
            AssertColor(ParseFloat3(common, "AB_RUST_LIN"), AbyssPalette.RUST, "AB_RUST_LIN");
            Assert.AreEqual(0.12f, rustMax, 1e-6f, "14.2 cap");
            Assert.IsTrue(Regex.IsMatch(common, @"AbSmoothstepSafe\(t - a, t \+ a, coarse\)"), "mirrored mask band (widest symmetric band: soft gradients)");
            Assert.IsTrue(common.Contains("float m = max(a, b); return m * m;"), "AbUniformMax2 mirrored below");
            string recipes = File.ReadAllText(ShaderFilePath("Include/AbyssSurfaceRecipes.hlsl"));
            // R2-10: structure patches = two warped, vertically stretched lookups; walking surfaces = stochastic per-cell lookups
            Assert.IsTrue(recipes.Contains("AbUniformMax2(AbRustPatchG(ri, AB_PATCH_TILE_A, float2(0.0, 0.0)), AbRustPatchG(ri, AB_PATCH_TILE_B,"),
                "steel patches combine two incommensurate warped lookups (no round discs, no 3 m repetition)");
            Assert.IsTrue(recipes.Contains("AbBombG(ri.P.xz, ri.dPdx.xz, ri.dPdy.xz, 7.4, 6.0, 222,"), "walking decks use per-cell stochastic stains");
            Assert.IsTrue(recipes.Contains("AbRustMask(_RustAmount, coarse, n1.g, footprint)"), "steel recipes use the exact mask");
            Assert.IsTrue(recipes.Contains("AbRustColor(paint, n1.a)"), "steel recipes use the rust colour");
            Assert.IsTrue(recipes.Contains("AbStainColor(paint, n1.a), walk)"), "walking surfaces get dark-neutral stains, never red");
            string grating = File.ReadAllText(ShaderFilePath("Grating.shader"));
            Assert.IsTrue(grating.Contains("AbRustMask(_RustAmount, stainPatch, nb.a,"), "grating uses the exact mask");
            Assert.IsTrue(grating.Contains("AbUniformMax2(AbBombG(P.xz, dPdx.xz, dPdy.xz, 7.4, 6.0, 222,"), "grating stains are stochastic per 6 m cell");
            float tileA = ParseDefine(recipes, "AB_PATCH_TILE_A"), tileB = ParseDefine(recipes, "AB_PATCH_TILE_B");
            Assert.AreNotEqual(tileA, tileB, "two incommensurate patch tiles");
            foreach (float tile in new[] { tileA, tileB, 7.4f, 4.5f })   // steel tiles and the grating's 7.4 / 4.5 m
            {
                double nt = AbyssConst.NOISE_WRAP / tile;
                Assert.AreEqual(System.Math.Round(nt), nt, 1e-4, "patch tile " + tile + " divides 1332");
            }

            Color32[] px = ProceduralTextures.BuildNoisePixels();
            float[] amounts = { MaterialBuilder.RustSteelPainted, MaterialBuilder.RustSteelWeathered, MaterialBuilder.RustGrating, 0.12f };
            foreach (float amount in amounts)
            {
                Assert.LessOrEqual(amount, MaterialBuilder.RustMax + 1e-6f, "rust amount above the 12 % cap");
                float a = Mathf.Clamp(amount, 0f, rustMax);
                float t = 1f - a;
                int covered = 0;
                double weight = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    float m = SmoothstepHlsl(t - a, t + a, px[i].g / 255f);
                    if (m > 0.5f) covered++;
                    weight += m * (0.55f + 0.45f * (px[i].a / 255f));
                }
                double area = covered / (double)px.Length;
                Assert.AreEqual(a, area, 0.004, $"rust area for _RustAmount {amount}");
                Assert.LessOrEqual(weight / px.Length, a + 0.002, $"mean rust weight for _RustAmount {amount}");

                // the combined patch value max(g(x / 7.4), g(x / 4.5 + 0.31))^2 of two lookups is still uniform: same area
                int coveredMax = 0, samples = 0;
                for (int y = 0; y < 400; y++)
                    for (int x = 0; x < 400; x++)
                    {
                        float wx = x * 0.185f, wy = y * 0.185f;   // a 74 m square in metres (one common period)
                        float ga = NoiseG(px, wx / tileA, wy / tileA), gb = NoiseG(px, wx / tileB + 0.31f, wy / tileB + 0.31f);
                        float mx = Mathf.Max(ga, gb);
                        if (SmoothstepHlsl(t - a, t + a, mx * mx) > 0.5f) coveredMax++;
                        samples++;
                    }
                Assert.AreEqual(a, coveredMax / (double)samples, 0.35 * a + 0.004, $"two-lookup patch area for _RustAmount {amount}");
            }

            // colour: RUST desaturated to AB_RUST_CHROMA at AB_RUST_LUMA x the paint luma (STEEL_DARK, variation 0..1)
            Color paint = AbyssPalette.Lin(AbyssPalette.STEEL_DARK);
            Color rust = AbyssPalette.Lin(AbyssPalette.RUST);
            float rustLuma = Luma(rust);
            foreach (float variation in new[] { 0f, 0.5f, 1f })
            {
                float k = Luma(paint) * lumaK * (0.85f + 0.3f * variation);
                var c = new Color(
                    Mathf.Lerp(1f, rust.r / rustLuma, chroma) * k,
                    Mathf.Lerp(1f, rust.g / rustLuma, chroma) * k,
                    Mathf.Lerp(1f, rust.b / rustLuma, chroma) * k);
                Assert.AreEqual(Luma(paint) * lumaK * (0.85f + 0.3f * variation), Luma(c), 1e-5f, "rust luma follows the paint");
                Color.RGBToHSV(c.gamma, out _, out float sat, out _);
                Color.RGBToHSV(AbyssPalette.RUST, out _, out float satRust, out _);
                // R1-27: a visible RUST hue (the 0.40 chroma of g01 read as hue-less dark blots), still not an orange blotch
                Assert.GreaterOrEqual(sat, 0.30f, "rust shows a RUST hue (sRGB HSV saturation)");
                Assert.LessOrEqual(sat, 0.45f, "rust albedo saturation (sRGB HSV)");
                Assert.Less(sat, 0.8f * satRust, "rust is less saturated than raw RUST");
                Assert.LessOrEqual(Luma(c) / Luma(paint), 1.15f, "rust is not a bright blotch on the paint");
            }
            // dark-neutral stains on walking surfaces: the paint darkened by 8-14 % (R2-10: at most 10-12 % on average), hue and
            // saturation of the paint
            Assert.IsTrue(common.Contains("return paint * (0.86 + 0.06 * saturate(variation));"), "AbStainColor mirrored (matte dark-neutral grime)");
            Assert.LessOrEqual(1f - 0.86f, 0.14f + 1e-6f, "stains at most 14 % darker");
        }

        // ------------------------------------------------------------------------------------------ stochastic stains (R2-10)
        // CPU mirror of AbPcg3d (AbyssCommon.hlsl).
        static void Pcg3d(ref uint x, ref uint y, ref uint z)
        {
            unchecked
            {
                x = x * 1664525u + 1013904223u; y = y * 1664525u + 1013904223u; z = z * 1664525u + 1013904223u;
                x += y * z; y += z * x; z += x * y;
                x ^= x >> 16; y ^= y >> 16; z ^= z >> 16;
                x += y * z; y += z * x; z += x * y;
            }
        }

        static float U01(uint h) => (h >> 8) * (1f / 16777216f);

        // CPU mirrors of AbErf1 / AbPhi / AbPhiInv (AbyssCommon.hlsl).
        static float Erf1(float x)
        {
            float x2 = x * x;
            float k = x2 * (1.2732395f + 0.147f * x2) / (1f + 0.147f * x2);
            return Mathf.Sign(x) * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Exp(-k)));
        }

        static float Phi(float z) => 0.5f + 0.5f * Erf1(z * 0.70710678f);

        static float PhiInv(float p)
        {
            p = Mathf.Clamp(p, 1e-5f, 1f - 1e-5f);
            float q = Mathf.Min(p, 1f - p);
            float t = Mathf.Sqrt(-2f * Mathf.Log(q));
            float z = t - (2.515517f + 0.802853f * t + 0.010328f * t * t) / (1f + 1.432788f * t + 0.189269f * t * t + 0.001308f * t * t * t);
            return p > 0.5f ? z : -z;
        }

        // CPU mirror of AbBombG (nearest-texel lookups, no derivatives).
        static float BombG(Color32[] px, float x, float z, float tile, float cell, int n, uint salt)
        {
            float cx = x / cell - 0.5f, cz = z / cell - 0.5f;
            float ix = Mathf.Floor(cx), iz = Mathf.Floor(cz);
            float fx = cx - ix, fz = cz - iz;
            float wx = fx * fx * (3f - 2f * fx), wz = fz * fz * (3f - 2f * fz);
            float acc = 0f, w2 = 0f;
            for (int j = 0; j < 4; j++)
            {
                int ox = j & 1, oz = j >> 1;
                float wj = (ox == 1 ? wx : 1f - wx) * (oz == 1 ? wz : 1f - wz);
                int ci = (int)ix + ox, cj = (int)iz + oz;
                uint hx = (uint)(((ci % n) + n) % n), hy = (uint)(((cj % n) + n) % n), hz = salt;
                Pcg3d(ref hx, ref hy, ref hz);
                float u = x / tile, v = z / tile;
                if ((hz & 1u) != 0u) { float t = u; u = v; v = t; }
                if ((hz & 2u) != 0u) u = -u;
                if ((hz & 4u) != 0u) v = -v;
                float g = NoiseG(px, u + U01(hx), v + U01(hy));
                acc += wj * PhiInv(g);
                w2 += wj * wj;
            }
            return Phi(acc / Mathf.Sqrt(Mathf.Max(w2, 1e-4f)));
        }

        /// <summary>
        /// R2-10: the gantry deck repeated the same paired stain every 4.5-7.4 m (each patch of a max of two tiled lookups recurs
        /// with that lookup's own period). AbBombG samples the equalised channel with a random offset and rotation per 6 m
        /// cell and blends cells with variance preservation: the stain coverage stays near _RustAmount (threshold of an about
        /// uniform value) while the pattern no longer correlates with itself at a shift of one tile (7.4 m or 4.44 m).
        /// </summary>
        [Test]
        public void StochasticStainsKeepCoverageAndDoNotRepeat()
        {
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            Assert.IsTrue(common.Contains("acc += wj * AbPhiInv(g);") && common.Contains("return AbPhi(acc * rsqrt(max(w2, 1e-4)));"),
                "histogram-preserving (Gaussianised) blend mirrored");
            Assert.IsTrue(common.Contains("uv = uv * s + float2(AbU01(h.x), AbU01(h.y));"), "random offset and mirror per cell mirrored");
            Color32[] px = ProceduralTextures.BuildNoisePixels();
            const int N = 240;
            const float step = 0.25f;                       // a 60 m square
            var v = new float[N, N];
            for (int i = 0; i < N; i++)
                for (int k = 0; k < N; k++)
                {
                    float x = i * step, z = k * step;
                    float a = BombG(px, x, z, 7.4f, 6f, 222, 191u), b = BombG(px, x + 3f, z + 3f, 4.44f, 6f, 222, 193u);
                    float m = Mathf.Max(a, b);
                    v[i, k] = m * m;
                }
            foreach (float amount in new[] { MaterialBuilder.RustGrating, 0.06f })
            {
                float t = 1f - amount;
                int covered = 0;
                for (int i = 0; i < N; i++) for (int k = 0; k < N; k++) if (SmoothstepHlsl(t - amount, t + amount, v[i, k]) > 0.5f) covered++;
                double area = covered / (double)(N * N);
                Assert.AreEqual(amount, area, 0.3 * amount + 0.004, $"stain coverage for {amount} stays at the amount (uniform blend)");
            }
            // correlation of the value with itself shifted by one lookup tile (old: about 1 for the dominant lookup)
            foreach (float shift in new[] { 7.4f, 4.44f })
            {
                int s = Mathf.RoundToInt(shift / step);
                double sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0;
                int cnt = 0;
                for (int i = 0; i + s < N; i++)
                    for (int k = 0; k < N; k++)
                    {
                        double a = v[i, k], b = v[i + s, k];
                        sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; cnt++;
                    }
                double cov = sab / cnt - (sa / cnt) * (sb / cnt);
                double corr = cov / System.Math.Sqrt((saa / cnt - (sa / cnt) * (sa / cnt)) * (sbb / cnt - (sb / cnt) * (sb / cnt)));
                Assert.Less(corr, 0.35, $"stain pattern correlates {corr:F2} with itself at a {shift} m shift");
            }
        }

        /// <summary>Nearest-texel lookup of NoiseTex.g at texture coordinates (u, v) with repeat wrap (CPU mirror).</summary>
        static float NoiseG(Color32[] px, float u, float v)
        {
            int n = ProceduralTextures.NoiseSize;
            int x = ((int)Mathf.Floor(u * n) % n + n) % n;
            int y = ((int)Mathf.Floor(v * n) % n + n) % n;
            return (px[y * n + x].g + 0.5f) / 256f;
        }

        // ------------------------------------------------------------------------------------------ board-formed concrete (R1-08 / R1-09)
        static float ExpStainF(float x, float L) => x > 0f ? L * (1f - Mathf.Exp(-x / L)) : 0f;

        // CPU mirror of AbExpStainBox (AbyssCommon.hlsl).
        static float ExpStainBox(float t, float L, float P, float h)
        {
            h = Mathf.Max(h, 1e-5f);
            float a = t - h, b = t + h;
            float fb = ExpStainF(Mathf.Min(b, P), L) + ExpStainF(b - P, L);
            float fa = ExpStainF(Mathf.Min(a, P), L) + ExpStainF(a - P, L);
            return Mathf.Clamp01((fb - fa) / (2f * h));
        }

        // CPU mirror of AbLineMinWidth: (drawn width, contrast scale).
        static Vector2 LineMinWidth(float p, float w, float fw, float minPx)
        {
            float drawn = Mathf.Max(w, minPx * fw);
            return new Vector2(drawn, Mathf.Sqrt(w / drawn) * SmoothstepHlsl(4f, 8f, p / fw));
        }

        /// <summary>
        /// R1-08 / R1-09: board-formed concrete must read at walking distances and on the 70-500 m Face. Mirrors the
        /// read-at-distance terms of AbConcreteBoard: board tone +/-4-6 % fading only from 2.5 px rows (no hatching), a
        /// per-lift step of +/-6-9 %, a 20-30 mm recess, a weathering band about 20 cm deep under every cold joint whose
        /// exact box filter matches numeric integration (no aliasing), and joint lines held at 1.5 px with a contrast that
        /// stays visible at 300 m (graph-paper cliff) while near joints are drawn at their true width.
        /// </summary>
        [Test]
        public void BoardFormedConcreteReadsAtWalkingAndFaceDistances()
        {
            string recipes = File.ReadAllText(ShaderFilePath("Include/AbyssSurfaceRecipes.hlsl"));
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            float boardTone = ParseDefine(recipes, "AB_BOARD_TONE");
            Assert.That(boardTone, Is.InRange(0.06f, 0.08f), "per-board albedo +/-6-8 % (R2-02)");
            // R2-02: the 3 mm board joints are held at 1.5 px while the rows are over 12 px apart (walking distances) and fade
            // to their true coverage by 6 px rows; the cold joints are a dark recess over a light lip, -25..-30 % stain near
            Assert.IsTrue(recipes.Contains("float held = AbLineBox(x, p, drawn, fw) * sqrt(AB_GROOVE_W / drawn);"), "board joint held at 1.5 px");
            Assert.AreEqual(6f, ParseDefine(recipes, "AB_BOARDLINE_PX0"), 1e-6f, "held board joints fade out from 12 to 6 px rows");
            Assert.AreEqual(12f, ParseDefine(recipes, "AB_BOARDLINE_PX1"), 1e-6f);
            Assert.That(ParseDefine(recipes, "AB_STAIN_DARK_NEAR"), Is.InRange(0.25f, 0.30f), "weathering band -25..-30 % at walking distance");
            Assert.Less(ParseDefine(recipes, "AB_STAIN_DARK_FAR"), ParseDefine(recipes, "AB_STAIN_DARK_NEAR"), "lighter on the far Face (no graph paper)");
            Assert.Greater(ParseDefine(recipes, "AB_LIP_LIGHT"), 0f, "the lip under the recess catches the skylight");
            Assert.That(ParseDefine(recipes, "AB_JOINT_VAR"), Is.InRange(0.2f, 0.5f), "joint darkness varies per (bay, lift) (R2-32)");
            Assert.That(ParseDefine(recipes, "AB_BAY_TONE"), Is.InRange(0.03f, 0.06f), "per pour-campaign tone (R2-32)");
            // R2-02 KS1: a tapered grout-bleed band along every row joint (14 mm, -20..-30 % at its core) held while the rows are
            // resolved: an 8-px block holding it at 1.5 m (about 1.6 mm/px) is about 18 % darker, over 3 levels at 44-79 levels
            float bleedW = ParseDefine(recipes, "AB_BLEED_W"), bleedDark = ParseDefine(recipes, "AB_BLEED_DARK");
            Assert.That(bleedW, Is.InRange(0.01f, 0.02f), "grout bleed 1-2 cm");
            Assert.That(bleedDark, Is.InRange(0.2f, 0.3f), "grout bleed -20..-30 %");
            float blockCover = 0.5f * (Mathf.Min(bleedW / 0.0016f, 8f) + Mathf.Min(0.4f * bleedW / 0.0016f, 8f)) / 8f;
            float lin44 = Mathf.GammaToLinearSpace(44f / 255f);
            Assert.GreaterOrEqual(44f - 255f * Mathf.LinearToGammaSpace(lin44 * (1f - bleedDark * blockCover)), 3f, "KS1 bleed block step >= 3 levels at 44");
            Assert.IsTrue(recipes.Contains("cBleed = lerp(0.7 * AB_BLEED_W / AB_BOARD_H, cBleed, kRows);"), "bleed fades to its mean with the held board line");
            // a recess of 25 mm at -45 % below the KS3 collar (linear radiance about 0.01, level 25) is at least 4 levels
            float recDark = ParseDefine(recipes, "AB_RECESS_DARK");
            float lin = 0.01f;
            float levels = 255f * (Mathf.LinearToGammaSpace(lin) - Mathf.LinearToGammaSpace(lin * (1f - recDark)));
            Assert.GreaterOrEqual(levels, 4f, "a cold-joint recess reads at least 4 levels on the dark lower face");
            Assert.AreEqual(0.05f, ParseDefine(recipes, "AB_BOARD_ROUGH"), 1e-6f, "per-board roughness +/-0.05");
            Assert.GreaterOrEqual(ParseDefine(recipes, "AB_BOARD_FADE0"), 2.5f, "board-scale tone starts fading at 2.5 px rows");
            Assert.IsTrue(recipes.Contains("AbFeatureFadeRange(AB_BOARD_H / max(fwV, 1e-6), AB_BOARD_FADE0, AB_BOARD_FADE1)"), "board fade uses the 2.5-5 px range");
            float liftStep = ParseDefine(recipes, "AB_LIFT_TONE") + ParseDefine(recipes, "AB_POUR_TONE");
            Assert.That(liftStep, Is.InRange(0.06f, 0.095f), "per-lift (pour) tone step +/-6-9 %");
            Assert.That(ParseDefine(recipes, "AB_RECESS_W"), Is.InRange(0.02f, 0.03f), "20-30 mm recess at each cold joint");
            Assert.AreEqual(0.008f, ParseDefine(recipes, "AB_COLD_W"), 1e-6f, "the 8 mm cold-joint band of 14.2 is kept");
            float L = ParseDefine(recipes, "AB_STAIN_L");
            float visibleDepth = L * Mathf.Log(10f);   // where the band falls to 10 %
            Assert.That(visibleDepth, Is.InRange(0.15f, 0.25f), "weathering band 15-25 cm under each joint");

            // exact box filter of the periodic one-sided exponential, against numeric integration (incl. the next period)
            const float P = 3f;
            float[] hs = { 0.002f, 0.03f, 0.2f, 0.9f };
            float[] ts = { 0.001f, 0.05f, 0.4f, 2.95f, 2.999f };
            foreach (float h in hs)
            foreach (float t in ts)
            {
                double sum = 0;
                const int steps = 20000;
                for (int i = 0; i < steps; i++)
                {
                    float x = t - h + (i + 0.5f) * 2f * h / steps;
                    float xm = x - P * Mathf.Floor(x / P);
                    sum += Mathf.Exp(-xm / L);
                }
                float numeric = (float)(sum / steps);
                Assert.AreEqual(numeric, ExpStainBox(t, L, P, h), 0.01f, $"stain box filter t {t} h {h}");
            }
            Assert.IsTrue(common.Contains("float Fb = AbExpStainF(min(b, P), L) + AbExpStainF(b - P, L);"), "AbExpStainBox mirrored");

            // joint lines: true width near, held at >= 1.5 px and still visible on the Face at 300 m (1080p, 66 degrees)
            float pixelAngle = ParseDefine(common, "AB_DEFAULT_PIXEL_ANGLE");
            float w = ParseDefine(recipes, "AB_JSTREAK_W"), dark = ParseDefine(recipes, "AB_JSTREAK_DARK");
            Vector2 near = LineMinWidth(12f, w, 3f * pixelAngle, 1.5f);
            Assert.AreEqual(w, near.x, 1e-6f, "at 3 m the joint streak keeps its true width");
            Assert.AreEqual(1f, near.y, 1e-6f, "and its full contrast");
            Vector2 face = LineMinWidth(12f, w, 300f * pixelAngle, 1.5f);
            Assert.GreaterOrEqual(face.x / (300f * pixelAngle), 1.5f - 1e-4f, "at 300 m the joint line is at least 1.5 px wide");
            Assert.GreaterOrEqual(dark * face.y, 0.06f, "and at least 6 % darker (readable graph paper)");
            Vector2 far = LineMinWidth(12f, w, 2500f * pixelAngle, 1.5f);
            Assert.AreEqual(0f, far.y, 1e-6f, "joints fade out once 12 m is under 4 px (2.5 km)");
            Assert.IsTrue(common.Contains("contrastK = sqrt(w / drawn) * smoothstep(4.0, 8.0, p / fw);"), "AbLineMinWidth mirrored");

            // every new period divides the 1332 m wrap
            foreach (string def in new[] { "AB_GRAIN_PV", "AB_GRAIN_PU", "AB_FLOOR_SAW_P", "AB_FLOOR_EXP_P", "AB_FLOOR_AGG_A", "AB_FLOOR_AGG_B", "AB_DAMP_TILE" })
            {
                double n = AbyssConst.NOISE_WRAP / ParseDefine(recipes, def);
                Assert.AreEqual(System.Math.Round(n), n, 1e-4, def + " divides 1332");
            }
        }

        // ------------------------------------------------------------------------------------------ grating (R1-27)
        // CPU mirror of GratingFamily (Grating.shader): (visible coverage mean or box value, fade weight k).
        static float GratingFamilyK(float pitch, float fw) => SmoothstepHlsl(3f, 6f, pitch / Mathf.Max(fw, 1e-6f));

        static float GratingFamilyMean(float tanA, float k, float farOpacity, float pitch, float bar, float depth)
        {
            float open = pitch - bar;
            float w = Mathf.Min(bar + depth * tanA, pitch);
            float m0 = 1f - Mathf.Sqrt(Mathf.Clamp01(1f - farOpacity));
            float farMean = Mathf.Lerp(m0, 1f, Mathf.Clamp01(depth * tanA / open));
            return Mathf.Lerp(farMean, w / pitch, k);
        }

        /// <summary>
        /// R1-27: the catwalk grating read as a solid slab because both bar families faded together at the LARGER footprint.
        /// Each family now fades with its own footprint: walking along a catwalk (eye 1.7 m), the bars running along the
        /// view stay resolved several metres ahead while the cross bars close up; looking down at the feet the grating is
        /// see-through; far away the mean opacity is _FarOpacity (25 %) at normal incidence.
        /// </summary>
        [Test]
        public void GratingFamiliesFadeWithTheirOwnFootprint()
        {
            string g = File.ReadAllText(ShaderFilePath("Grating.shader"));
            Assert.IsTrue(g.Contains("float2 cU = GratingFamily(uv.x, fwU, tanU, pitch, bar, open);"), "family U uses its own footprint");
            Assert.IsTrue(g.Contains("float2 cV = GratingFamily(uv.y, fwV, tanV, pitch, bar, open);"), "family V uses its own footprint");
            Assert.IsTrue(g.Contains("float k = smoothstep(3.0, 6.0, pitch / max(fw, 1e-6));"), "per-family 6 -> 3 px fade (R2-10)");

            MaterialBuilder.BuildAll();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            Material m = lib[MatId.Grating];
            float pitch = m.GetFloat("_Pitch"), bar = m.GetFloat("_Bar"), depth = m.GetFloat("_BarDepth"), farOp = m.GetFloat("_FarOpacity");
            Assert.AreEqual(0.04f, pitch, 1e-6f);
            Assert.AreEqual(0.008f, bar, 1e-6f);
            Assert.AreEqual(MaterialBuilder.GratingPanelSize, m.GetFloat("_PanelSize"), 1e-6f, "panel size");
            Assert.AreEqual(MaterialBuilder.GratingSideShade, m.GetFloat("_SideShade"), 1e-6f, "side shade");
            float pa = ParseDefine(File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl")), "AB_DEFAULT_PIXEL_ANGLE");

            // walking along the catwalk: a point 5.5 m ahead, eye 1.7 m above the grating
            float r = 5.5f, eye = 1.7f;
            float dist = Mathf.Sqrt(r * r + eye * eye), sinE = eye / dist;
            float fwAcross = dist * pa, fwAlong = dist * pa / sinE;
            float kAlongBars = GratingFamilyK(pitch, fwAcross);              // bars running along the view
            float kCrossBars = GratingFamilyK(pitch, fwAlong);
            float kOldRule = GratingFamilyK(pitch, Mathf.Max(fwAcross, fwAlong));
            Assert.Greater(kAlongBars, 0.3f, "bars along the walk stay resolved 5.5 m ahead");
            Assert.AreEqual(0f, kOldRule, 1e-6f, "the old shared fade had already removed every bar there");
            Assert.Less(kCrossBars, kAlongBars, "cross bars fade first");

            // R2-10 KS4 pose (catwalk eye 1.7 m above the grating, qa pose u -2 / yaw 274 / pitch +8): the bars running with the
            // view are readable within 6 m (k >= 0.75) and still present at 8 m
            foreach (float rr in new[] { 3f, 6f })
            {
                float dd = Mathf.Sqrt(rr * rr + eye * eye);
                Assert.GreaterOrEqual(GratingFamilyK(pitch, dd * pa), 0.75f, $"KS4 catwalk bars readable {rr} m ahead");
            }
            float d8 = Mathf.Sqrt(64f + eye * eye);
            Assert.Greater(GratingFamilyK(pitch, d8 * pa), 0.2f, "KS4 catwalk bars still present 8 m ahead");

            // looking down at the feet (1 m ahead): the grating is see-through
            float r2 = 1f, d2 = Mathf.Sqrt(r2 * r2 + eye * eye);
            float tan2 = r2 / eye;
            float cAlong = GratingFamilyMean(0f, 1f, farOp, pitch, bar, depth);
            float cCross = GratingFamilyMean(tan2, GratingFamilyK(pitch, d2 * pa / (eye / d2)), farOp, pitch, bar, depth);
            float opacity = 1f - (1f - cAlong) * (1f - cCross);
            Assert.Less(opacity, 0.85f, "near the feet the slots are open");

            // far, at normal incidence: _FarOpacity
            float mFar = GratingFamilyMean(0f, 0f, farOp, pitch, bar, depth);
            Assert.AreEqual(farOp, 1f - (1f - mFar) * (1f - mFar), 1e-5f, "far mean opacity at normal incidence");
            double panels = AbyssConst.NOISE_WRAP / MaterialBuilder.GratingPanelSize;
            Assert.AreEqual(System.Math.Round(panels), panels, 1e-6, "grating panels divide 1332");
        }

        // ------------------------------------------------------------------------------------------ downward view (R1-01)
        static float DownViewEmission(float down, float weight, float d0, float d1, float floor)
        {
            float e = Mathf.Lerp(1f, floor, SmoothstepHlsl(d0, d1, down));
            return Mathf.Lerp(1f, e, Mathf.Clamp01(weight));
        }

        /// <summary>
        /// R1-01 shader support: from the bridge, city windows within 50 degrees of the nadir must stay under about 0.10-0.12
        /// luma (linear about 0.012 for the brightest 5.0 window) while the horizon keeps its far lights, and the term is off
        /// unless driven (_AbDownView.x unset = 0), so the lift ride shows the full city. Facade materials opt in.
        /// </summary>
        [Test]
        public void DownViewAttenuationDarkensTheNadirOnlyWhenDriven()
        {
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            float d0 = ParseDefine(common, "AB_DOWNVIEW_DOWN0"), d1 = ParseDefine(common, "AB_DOWNVIEW_DOWN1"), fl = ParseDefine(common, "AB_DOWNVIEW_FLOOR");
            Assert.IsTrue(common.Contains("float w = saturate(_AbDownView.x) * saturate(optIn);"), "weight = global x opt-in");
            Assert.IsTrue(common.Contains("float e = lerp(1.0, AB_DOWNVIEW_FLOOR, smoothstep(AB_DOWNVIEW_DOWN0, AB_DOWNVIEW_DOWN1, down));"), "mirrored");
            float nadir50 = Mathf.Sin(40f * Mathf.Deg2Rad);   // 50 degrees from the nadir = 40 degrees below the horizon
            Assert.LessOrEqual(d1, nadir50 + 1e-4f, "full attenuation from 50 degrees of the nadir");
            float windowMax = AbyssPalette.WINDOW_EMISSIVE_MAX;
            Assert.LessOrEqual(windowMax * DownViewEmission(nadir50, 1f, d0, d1, fl), 0.0126f, "brightest window near the nadir under about 0.12 luma");
            Assert.LessOrEqual(windowMax * DownViewEmission(1f, 1f, d0, d1, fl), 0.0126f, "straight down");
            Assert.AreEqual(1f, DownViewEmission(Mathf.Sin(15f * Mathf.Deg2Rad), 1f, d0, d1, fl), 1e-6f, "the far city toward the horizon keeps its lights");
            Assert.AreEqual(1f, DownViewEmission(1f, 0f, d0, d1, fl), 1e-6f, "off when not driven (ride)");

            foreach (string file in new[] { "Facade.shader", "Surface.shader", "Emissive.shader" })
                Assert.IsTrue(File.ReadAllText(ShaderFilePath(file)).Contains("AbDownViewEmission(input.positionWS, _DownViewAtten,"), file + " applies the term");
            MaterialBuilder.BuildAll();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            Assert.AreEqual(MaterialBuilder.DownViewFacade, lib[MatId.Facade].GetFloat("_DownViewAtten"), 1e-6f, "Facade opts in");
            Assert.AreEqual(MaterialBuilder.DownViewFacade, lib[MatId.FacadeModule].GetFloat("_DownViewAtten"), 1e-6f, "FacadeModule opts in");
            foreach (MatId id in new[] { MatId.LampAmber, MatId.LampCold, MatId.ConcreteBoard, MatId.SteelPainted })
                Assert.AreEqual(0f, lib[id].GetFloat("_DownViewAtten"), 1e-6f, id + " does not opt in");
        }

        // ------------------------------------------------------------------------------------------ sub-pixel bevels (R1-26)
        /// <summary>
        /// R1-26: chamfer highlights under 1.5 px dash and crawl. MeshKit.ChamferBox marks chamfer facets with G = 255 and
        /// gives them 45-degree normals (two components 0.707): exactly what AbBevelResolve detects, while plain faces stay
        /// untouched (G of the builder colour, one axis component). The projected width rule fades a 1 cm chamfer between
        /// about 3 m (resolved) and 10 m (merged) on a 66-degree 1080p frame.
        /// </summary>
        [Test]
        public void SubPixelBevelsMergeIntoTheirFace()
        {
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            Assert.IsTrue(common.Contains("float px = bevelWidth * 1.41421356 * max(abs(dot(N, V)), 0.25) / (dist * AbPixelAngle());"), "projected width mirrored");
            Assert.IsTrue(common.Contains("if (minA < 0.15 && maxA < 0.82 && maxA > 0.6)"), "chamfer test mirrored");
            float pa = ParseDefine(common, "AB_DEFAULT_PIXEL_ANGLE");
            float width = MaterialBuilder.BevelWidthDefault;
            float K(float dist) => SmoothstepHlsl(1f, 2.5f, width * 1.41421356f * 0.78f / (dist * pa));
            Assert.AreEqual(1f, K(2.5f), 1e-6f, "resolved at 2.5 m");
            Assert.AreEqual(0f, K(10f), 1e-6f, "merged at 10 m");

            var mb = new MeshBuilder();
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(2f, 0.03f, 2f), 0.004f);
            int chamfer = 0, faces = 0;
            for (int i = 0; i < mb.Positions.Count; i++)
            {
                Vector3 n = mb.Normals[i];
                Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                float minA = Mathf.Min(a.x, Mathf.Min(a.y, a.z)), maxA = Mathf.Max(a.x, Mathf.Max(a.y, a.z));
                bool detected = minA < 0.15f && maxA < 0.82f && maxA > 0.6f;
                float onBevel = SmoothstepHlsl(0.85f, 0.97f, mb.Colors[i].g / 255f);
                if (maxA > 0.99f)
                {
                    faces++;
                    Assert.AreEqual(0f, onBevel, 1e-6f, "plain faces are not bevels");
                }
                else if (minA < 0.15f)
                {
                    chamfer++;
                    Assert.IsTrue(detected, "edge chamfer detected as 45 degrees");
                    Assert.AreEqual(1f, onBevel, 1e-6f, "chamfer facets carry G = 255");
                }
            }
            Assert.AreEqual(24, faces, "6 faces x 4 vertices");
            Assert.AreEqual(48, chamfer, "12 chamfers x 4 vertices");
        }

        // ------------------------------------------------------------------------------------------ final dither
        static float FracF(float x) => x - Mathf.Floor(x);

        static float IgnF(float x, float y)
        {
            float inner = FracF((float)(x * 0.06711056f) + (float)(y * 0.00583715f));
            return FracF((float)(52.9829189f * inner));
        }

        static float Triangular(float u)
        {
            float n = u * 2f - 1f;
            return Mathf.Sign(n) * (1f - Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Abs(n))));
        }

        // Longest run of identical 8-bit values down 'columns' vertical lines of 'rows' pixels, over slow vertical
        // gradients of -0.25..0.25 levels per pixel (the BandScore of 14.10 on the dither alone).
        static int WorstVerticalRun(float rowShift, int columns, int rows)
        {
            var noise = new float[columns * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    noise[y * columns + x] = Triangular(FracF(IgnF(1000 + x, y) + (float)(rowShift * y)));
            int worst = 0;
            float[] offsets = { 0f, 0.29f };
            for (int si = -25; si <= 25; si++)
            {
                float slope = si * 0.01f;
                foreach (float off in offsets)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        int run = 1;
                        float prev = Mathf.Floor(80f + off + noise[x] + 0.5f);
                        for (int y = 1; y < rows; y++)
                        {
                            float q = Mathf.Floor(80f + off + slope * y + noise[y * columns + x] + 0.5f);
                            run = q == prev ? run + 1 : 1;
                            prev = q;
                            if (run > worst) worst = run;
                        }
                    }
                }
            }
            return worst;
        }

        /// <summary>
        /// g01 KS7d banding (runs of 13-14 px on the fan housing at 16:10 / 21:9): plain IGN nearly repeats every 13 rows
        /// (13 x 0.3092692 = 4.02), which locks onto slow gradients. FinalDither adds a per-row phase (vertical step 0.19);
        /// the CPU mirror shows the worst vertical run on any gradient falls from about 15 to about 6 (limit 12 at 1080p).
        /// </summary>
        [Test]
        public void FinalDitherBreaksVerticalRuns()
        {
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            Assert.IsTrue(common.Contains("frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))))"), "IGN constants mirrored here");
            string dither = File.ReadAllText(ShaderFilePath("FinalDither.shader"));
            float shift = ParseDefine(dither, "AB_DITHER_ROW_SHIFT");
            Assert.IsTrue(dither.Contains("frac(AbIGN(pixel) + AB_DITHER_ROW_SHIFT * pixel.y)"), "row-decorrelated IGN");
            Assert.IsTrue(dither.Contains("AbTriangular(AbDitherUniform(pixel))"), "TPDF of the decorrelated value");
            Assert.AreEqual(0.19, 52.9829189 * 0.00583715 + shift, 1e-5, "vertical step 0.19");

            int fixedRun = WorstVerticalRun(shift, 256, 1080);
            int plainRun = WorstVerticalRun(0f, 256, 1080);
            Assert.LessOrEqual(fixedRun, 8, "worst vertical run of the final dither on a slow gradient");
            Assert.GreaterOrEqual(plainRun, 12, "the mirror reproduces the plain-IGN defect it guards against");
        }

        // ------------------------------------------------------------------------------------------ horizon by construction
        static float Hg(float cosTheta, float g)
        {
            float g2 = g * g;
            float denom = Mathf.Max(1f + g2 - 2f * g * cosTheta, 1e-4f);
            return (1f - g2) / (4f * Mathf.PI * denom * Mathf.Sqrt(denom));
        }

        static int Srgb8(float linear) => Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(linear)) * 255f);

        /// <summary>
        /// 14.7.7 / 14.10 horizon tests, shader side: sky, fog and lattice share AbyssFog.hlsl, so they agree by construction.
        /// CPU mirror of the Exterior FogSky: the horizon row is exactly OUT_HORIZ for every sun direction (the HG lobe is
        /// weighted by smoothstep(0.05, 0.35, v.y)), within 1 level of it over +/-1.7 degrees and within 2 levels over the
        /// +/-2.6 degree sample boxes; the 28 km lattice disc is fully fogged at its rim (no seam against the sky); the
        /// skybox, the lattice and the bore card all route through these functions.
        /// </summary>
        [Test]
        public void ExteriorHorizonAgreesByConstruction()
        {
            string fog = File.ReadAllText(ShaderFilePath("Include/AbyssFog.hlsl"));
            Vector3 down = ParseFloat3(fog, "AB_EXT_FOG_DOWN");
            Vector3 horiz = ParseFloat3(fog, "AB_EXT_FOG_HORIZ");
            Vector3 up = ParseFloat3(fog, "AB_EXT_FOG_UP");
            Vector3 scatter = ParseFloat3(fog, "AB_EXT_SCATTER");
            float g = ParseDefine(fog, "AB_HG_G");
            float cap = ParseDefine(fog, "AB_EXT_LOBE_CAP");
            float amt = ParseDefine(fog, "AB_EXT_SCATTER_AMT");
            float d50 = ParseDefine(fog, "AB_EXT_D50");
            float mDown = ParseDefine(fog, "AB_EXT_M_DOWN");
            Assert.IsTrue(fog.Contains("min(hg * smoothstep(0.05, 0.35, v.y), AB_EXT_LOBE_CAP)"), "mirrored exterior lobe");
            Assert.IsTrue(Regex.IsMatch(fog, @"if \(b >= 1\.0\) return FogSky\(v, AB_DOMAIN_EXTERIOR\);"), "sky = Exterior FogSky after the blend");
            Assert.IsTrue(Regex.IsMatch(fog, @"return lerp\(rgb, AbFogSkySet\(v, s\), F\);"), "opaque fog converges to FogSky");
            Assert.IsTrue(File.ReadAllText(ShaderFilePath("Sky.shader")).Contains("AbyssSkyColor(v)"), "skybox uses AbyssSkyColor");
            // lattice: the fog is the last colour operation, except for the resolved amber dot discs (R2-21), which are drawn
            // over the fogged cell with a weight smoothstep(0.1, 0.4, T) of the fog transmittance: 0 where T < 0.1 (beyond
            // about 13 km; the horizon backdrop rows lie at 20 km and more), so the horizon row is exactly the fogged lattice there
            string latticeSrc = File.ReadAllText(ShaderFilePath("Lattice.shader"));
            Assert.IsTrue(latticeSrc.Contains("float3 cOld = AbyssApplyFog(color * (1.0 - AB_LAMP_MEAN_OCCL * saturate(g)) + AbWarmEmission(E * g), input.positionWS, (int)_AbDomain);"),
                "lattice: the cell and its additive dot are fogged together");
            Assert.IsTrue(latticeSrc.Contains("* smoothstep(0.1, 0.4, T);") && Regex.IsMatch(latticeSrc, @"color = lerp\(cOld, cDisc, discW \* kDots\);\s*return half4\(color, 1\.0\);"),
                "lattice: only the transmittance-weighted disc follows the fog");
            float tHorizon = Mathf.Exp(-Mathf.Pow(0.8326f / d50 * 13000f, 2f));
            Assert.Less(tHorizon, 0.1f + 1e-3f, "discs are off by 13 km (transmittance under 0.1)");
            Assert.IsTrue(File.ReadAllText(ShaderFilePath("Emissive.shader")).Contains("FogSky(v, AB_DOMAIN_EXTERIOR)"), "bore card = Exterior sky");

            Color outHoriz = AbyssPalette.OUT_HORIZ;
            int[] ref8 = { Mathf.RoundToInt(outHoriz.r * 255f), Mathf.RoundToInt(outHoriz.g * 255f), Mathf.RoundToInt(outHoriz.b * 255f) };
            Vector3[] suns =
            {
                new Vector3(0f, 0.02f, -1f), new Vector3(0f, 0.3f, -1f), new Vector3(0.5f, 0.1f, -0.8f),
                new Vector3(0f, 1f, 0f), new Vector3(0f, 0.05f, 1f), new Vector3(-1f, 0.2f, 0f),
            };
            for (int si = 0; si < suns.Length; si++)
            {
                Vector3 sun = suns[si].normalized;
                for (int az = 0; az < 12; az++)
                {
                    float phi = az * Mathf.PI / 6f;
                    for (int k = -9; k <= 9; k++)
                    {
                        float vy = k * 0.005f;
                        float h = Mathf.Sqrt(1f - vy * vy);
                        var v = new Vector3(h * Mathf.Sin(phi), vy, h * Mathf.Cos(phi));
                        Vector3 c = Vector3.Lerp(down, horiz, SmoothstepHlsl(-0.55f, 0f, v.y));
                        c = Vector3.Lerp(c, up, SmoothstepHlsl(0f, 0.8f, v.y));
                        float lobe = Mathf.Min(Hg(Vector3.Dot(v, sun), g) * SmoothstepHlsl(0.05f, 0.35f, v.y), cap);
                        c += scatter * (lobe * amt);
                        if (k == 0) Assert.Less((c - horiz).magnitude, 1e-6f, "horizon row is OUT_HORIZ exactly");
                        int tol = Mathf.Abs(vy) <= 0.03f ? 1 : 2;
                        Assert.LessOrEqual(Mathf.Abs(Srgb8(c.x) - ref8[0]), tol, $"R at v.y {vy}, sun {si}");
                        Assert.LessOrEqual(Mathf.Abs(Srgb8(c.y) - ref8[1]), tol, $"G at v.y {vy}, sun {si}");
                        Assert.LessOrEqual(Mathf.Abs(Srgb8(c.z) - ref8[2]), tol, $"B at v.y {vy}, sun {si}");
                    }
                }
            }
            // the lattice disc rim (28 km, 600 m below the balcony) is fully fogged: F > 0.9999, no seam against the sky
            float rimVy = -602f / 28000f;
            float sigma = 0.8326f / d50;
            float x = sigma * 28000f * (1f + mDown * Mathf.Clamp01(-rimVy));
            Assert.Greater(1f - Mathf.Exp(-x * x), 0.9999f, "lattice rim fog factor");
        }

        [Test]
        public void ShaderMeshesHaveTheContractLayout()
        {
            Mesh quad = ShaderMeshes.GlowQuad();
            Assert.AreEqual(4, quad.vertexCount);
            Mesh beams = ShaderMeshes.BeamWedgeMesh(AbyssConst.BEAM_COUNT, 0f, AbyssConst.BEAM_R1, AbyssConst.BEAM_LENGTH);
            Assert.AreEqual(AbyssConst.BEAM_COUNT * 8, beams.vertexCount);
            Assert.Greater(beams.bounds.extents.x, AbyssConst.BEAM_R1);
            Object.DestroyImmediate(quad);
            Object.DestroyImmediate(beams);
        }

        // ------------------------------------------------------------------------------------------ hue-safe amber (R2-21)
        // CPU mirror of URP's Neutral tonemapper (per channel) and of AbNeutral1 / AbNeutralInverse / AbHueSafe.
        static float NeutralCurve(float x) => ((x * (0.2f * x + 0.0696f) + 0.00544f) / (x * (0.2f * x + 0.29f) + 0.0816f)) - 0.0666667f;
        static float Neutral(float x, float ws) => NeutralCurve(Mathf.Max(x, 0f) * ws) * ws;
        static float NeutralInv(float y, float ws)
        {
            float t = Mathf.Min(Mathf.Max(y, 0f), 1.2f) / ws + 0.0666667f;
            float A = 0.2f * (t - 1f), B = 0.29f * (t - 0.24f), C = 0.272f * (0.3f * t - 0.02f);
            float disc = Mathf.Max(B * B - 4f * A * C, 0f);
            return Mathf.Max((-B - Mathf.Sqrt(disc)) / Mathf.Min(2f * A, -1e-5f), 0f) / ws;
        }

        const float GradeChroma = 1.12f;   // AB_GRADE_CHROMA (checked against the shader in WarmEmittersStayAmberThroughTheTonemapper)

        static Color HueSafe(Color c, float cap, float ws)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float p = Mathf.Min(Neutral(m, ws), cap);
            Color t = c * (p / m);
            float l = 0.2126f * t.r + 0.7152f * t.g + 0.0722f * t.b;
            t = new Color(Mathf.Max(l + (t.r - l) * GradeChroma, 0f), Mathf.Max(l + (t.g - l) * GradeChroma, 0f), Mathf.Max(l + (t.b - l) * GradeChroma, 0f));
            float tm = Mathf.Max(t.r, Mathf.Max(t.g, Mathf.Max(t.b, 1e-5f)));
            t *= p / tm;
            return new Color(NeutralInv(t.r, ws), NeutralInv(t.g, ws), NeutralInv(t.b, ws));
        }

        /// <summary>
        /// R2-21 / lead D4: the honeycomb dots, far lamp sprites and amber windows rendered cream-white (KS7b cores saturation
        /// 0.22) because the Neutral tonemapper compresses each channel toward 1. AbHueSafe pre-compensates the curve exactly:
        /// AMBER at any strength from 1 to 8 displays at hue 28-38 degrees with HSV saturation of at least 0.45, also over a
        /// dim background; cold lamps and neutral colours are untouched (warmth 0). The inverse is exact to 1e-3.
        /// </summary>
        [Test]
        public void WarmEmittersStayAmberThroughTheTonemapper()
        {
            string common = File.ReadAllText(ShaderFilePath("Include/AbyssCommon.hlsl"));
            float ws = ParseDefine(common, "AB_NEUTRAL_WS");
            Assert.AreEqual(1f / NeutralCurve(5.3f), ws, 1e-4f, "white scale of the Neutral curve (white level 5.3)");
            float cap = ParseDefine(common, "AB_AMBER_CAP");
            Assert.AreEqual(GradeChroma, ParseDefine(common, "AB_GRADE_CHROMA"), 1e-6f, "grade chroma pre-compensation mirrored");
            Assert.That(GradeChroma, Is.InRange(1f, 1.2f), "a modest chroma lift (hue stays 28-38 ungraded)");
            Assert.IsTrue(common.Contains("float AbWarmth(float3 c) { return saturate(2.5 * (c.r - c.b) / max(c.r, 1e-5) - 1.0); }"), "warmth mirrored");
            foreach (float x in new[] { 0.001f, 0.05f, 0.18f, 0.5f, 1f, 2.5f, 4f, 8f })
                Assert.AreEqual(x, NeutralInv(Neutral(x, ws), ws), 1e-3f * Mathf.Max(1f, x), "exact inverse at " + x);
            Color amber = AbyssPalette.AMBER.linear;
            foreach (float k in new[] { 1f, 2.5f, 3f, 4.2f, 8f })
                foreach (float bg in new[] { 0f, 0.05f })
                {
                    Color h = HueSafe(amber * k, cap, ws);
                    var disp = new Color(Neutral(h.r + bg, ws), Neutral(h.g + bg, ws), Neutral(h.b + bg, ws)).gamma;
                    Color.RGBToHSV(disp, out float hue, out float sat, out _);
                    Assert.That(hue * 360f, Is.InRange(28f, 38f), $"amber x {k} over {bg}: hue");
                    Assert.GreaterOrEqual(sat, 0.45f, $"amber x {k} over {bg}: saturation");
                    // the uncompensated emitter (f03) at the same strength desaturates
                    if (k >= 4f && bg == 0f)
                    {
                        var raw = new Color(Neutral(amber.r * k, ws), Neutral(amber.g * k, ws), Neutral(amber.b * k, ws)).gamma;
                        Color.RGBToHSV(raw, out _, out float satRaw, out _);
                        Assert.Less(satRaw, sat, "compensation raises the saturation");
                    }
                }
            Color cold = AbyssPalette.COLD_LAMP.linear;
            float warmth = Mathf.Clamp01(2.5f * (cold.r - cold.b) / Mathf.Max(cold.r, 1e-5f) - 1f);
            Assert.AreEqual(0f, warmth, 1e-6f, "cold lamps are not hue-compensated");
            Assert.AreEqual(1f, Mathf.Clamp01(2.5f * (amber.r - amber.b) / amber.r - 1f), 1e-6f, "AMBER is fully compensated");
            foreach (string file in new[] { "Glow.shader", "Lattice.shader", "Emissive.shader", "Facade.shader" })
                Assert.IsTrue(File.ReadAllText(ShaderFilePath(file)).Contains("AbWarmEmission("), file + " keeps warm emitters hue-safe");

            // Enlarged far dots (Glow sprites, Lattice dots) are amber discs over the fogged haze (lead_b01 KS7b cores were
            // (184, 148, 141), hue 10, S 0.24 as additive points): the core pixel (profile >= 0.5 for the sprite, Gaussian
            // >= 0.65 for the lattice) is fully covered, and a disc of AMBER x 2 seen through 60 % fog (half of it applied)
            // over the bluish haze keeps hue 28-38 and S >= 0.45 at coverage >= 0.9
            string glow = File.ReadAllText(ShaderFilePath("Glow.shader"));
            string lattice = File.ReadAllText(ShaderFilePath("Lattice.shader"));
            Assert.GreaterOrEqual(ParseDefine(glow, "AB_GLOW_DISC_GAIN") * 0.5f, 1f, "sprite core pixel fully covered");
            Assert.GreaterOrEqual(ParseDefine(lattice, "AB_LAMP_DISC_GAIN") * 0.65f, 0.95f, "lattice core pixel covered");
            Assert.IsTrue(glow.Contains("return half4(lerp(add, disc, w), a * w);"), "premultiplied amber disc");
            Assert.IsTrue(lattice.Contains("color = lerp(cOld, cDisc, discW * kDots);"), "lattice disc over the fogged cell, the checkpoint dot when unresolved");
            Color haze = new Color(0.17f, 0.19f, 0.215f);
            Color lamp = HueSafe(amber * (2f * Mathf.Lerp(1f, 0.4f, 0.5f)), cap, ws);
            foreach (float cover in new[] { 0.9f, 1f })
            {
                Color pre = haze * (1f - cover) + lamp * cover;
                var disp = new Color(Neutral(pre.r, ws), Neutral(pre.g, ws), Neutral(pre.b, ws)).gamma;
                Color.RGBToHSV(disp, out float hue, out float sat, out _);
                Assert.That(hue * 360f, Is.InRange(28f, 38f), $"far disc hue at coverage {cover}");
                Assert.GreaterOrEqual(sat, 0.45f, $"far disc saturation at coverage {cover}");
            }
        }

        // ------------------------------------------------------------------------------------------ glow points (R2-25)
        /// <summary>
        /// R2-25: 1-2 px light points twinkle under camera motion. The Glow shader integrates its Gaussian over each pixel
        /// (erf) for points drawn under about 6 px: the energy of a 2.5 px point summed over the pixels stays within 3 % of the
        /// R1 profile's energy at any sub-pixel offset, and its brightest pixel varies far less than a point sampled
        /// at pixel centres.
        /// </summary>
        [Test]
        public void GlowPointsKeepTheirEnergyAtSubPixelOffsets()
        {
            string glow = File.ReadAllText(ShaderFilePath("Glow.shader"));
            Assert.IsTrue(glow.Contains("float2 e = AbErf2((dpx + 0.5) / s) - AbErf2((dpx - 0.5) / s);"), "pixel-integrated profile");
            Assert.IsTrue(glow.Contains("profile = 0.25 * e.x * e.y * (AB_GLOW_ENERGY * sigmaPx * sigmaPx) * saturate(4.0 * (1.0 - r2));"), "energy of the R1 profile");
            float glowEnergy = ParseDefine(glow, "AB_GLOW_ENERGY");
            // the R1 profile exp(-5 r^2) (1 - r^2)^2 integrated over the unit disc (quad units) = 0.4269; sigma = 0.316 diam
            double e1 = 0;
            for (int i = 0; i < 4000; i++) { double u = (i + 0.5) / 4000.0; e1 += System.Math.PI * System.Math.Exp(-5 * u) * (1 - u) * (1 - u) / 4000.0; }
            Assert.AreEqual(e1 / 0.1, glowEnergy, 0.01, "AB_GLOW_ENERGY = energy of the R1 profile / sigma^2");
            Assert.IsTrue(glow.Contains("Blend One OneMinusSrcAlpha"), "premultiplied blend (alpha 0 = additive)");
            const float diamPx = 2.5f;
            float sigma = 0.3162278f * diamPx;
            float s = Mathf.Max(sigma, 0.8f) * 1.4142136f;
            float energy = glowEnergy * sigma * sigma;
            float minPeak = 1e9f, maxPeak = 0f, minPeakC = 1e9f, maxPeakC = 0f;
            for (int oy = 0; oy < 5; oy++)
                for (int ox = 0; ox < 5; ox++)
                {
                    float cx = ox / 5f, cy = oy / 5f;                // sub-pixel position of the point
                    float sum = 0f, peak = 0f, peakC = 0f;
                    for (int y = -4; y <= 4; y++)
                        for (int x = -4; x <= 4; x++)
                        {
                            float dx = x - cx, dy = y - cy;
                            float r2 = (dx * dx + dy * dy) / (diamPx * diamPx);
                            float win = Mathf.Clamp01(1f - r2);
                            float ex = Erf1((dx + 0.5f) / s) - Erf1((dx - 0.5f) / s), ey = Erf1((dy + 0.5f) / s) - Erf1((dy - 0.5f) / s);
                            float v = 0.25f * ex * ey * energy * Mathf.Clamp01(4f * (1f - r2));
                            sum += v;
                            peak = Mathf.Max(peak, v);
                            peakC = Mathf.Max(peakC, Mathf.Exp(-5f * r2) * win * win);   // the old centre-sampled profile
                        }
                    Assert.AreEqual(energy, sum, 0.03f * energy, $"energy at offset ({cx}, {cy})");
                    minPeak = Mathf.Min(minPeak, peak); maxPeak = Mathf.Max(maxPeak, peak);
                    minPeakC = Mathf.Min(minPeakC, peakC); maxPeakC = Mathf.Max(maxPeakC, peakC);
                }
            Assert.Less(maxPeak / minPeak, maxPeakC / minPeakC, "the brightest pixel twinkles less than with centre sampling");
        }

        // ------------------------------------------------------------------------------------------ honeycomb air (R2-04 / R2-19)
        /// <summary>
        /// R2-04 / R2-19: every honeycomb tier evaluates the same cell tone and the same air (14.10 c): the shared include's
        /// lattice frame equals AbyssConst, Surface (HoneyTube) and Lattice both call it, and the HoneyTube and Lattice base
        /// materials carry the same _HoneyAir while no other material does. CPU mirror: a deep near pit (1 km, depth 300 m)
        /// becomes a blue-grey of linear luma 0.02-0.05 (display about 0.15-0.21 at the outside exposure) instead of FOG_DOWN,
        /// and the veil stays under 25 % at 10 km so the far cells keep their contrast.
        /// </summary>
        [Test]
        public void HoneycombTiersShareCellToneAndAir()
        {
            string inc = File.ReadAllText(ShaderFilePath("Include/AbyssHoneycomb.hlsl"));
            Assert.AreEqual(AbyssConst.SHAFT_CENTRE.x, ParseDefine(inc, "AB_HONEY_ORIGIN_X"), 1e-4f, "origin x");
            Assert.AreEqual(AbyssConst.SHAFT_CENTRE.y, ParseDefine(inc, "AB_HONEY_ORIGIN_Z"), 1e-4f, "origin z");
            Assert.AreEqual(AbyssConst.LATTICE_PITCH / Mathf.Sqrt(3f), ParseDefine(inc, "AB_HONEY_SIZE"), 1e-3f, "hex size");
            Assert.AreEqual(0.06f, ParseDefine(inc, "AB_HONEY_CELL_TONE"), 1e-6f, "per-cell tone +/-6 %");
            string surface = File.ReadAllText(ShaderFilePath("Surface.shader"));
            string lattice = File.ReadAllText(ShaderFilePath("Lattice.shader"));
            string recipes = File.ReadAllText(ShaderFilePath("Include/AbyssSurfaceRecipes.hlsl"));
            Assert.IsTrue(surface.Contains("color = AbHoneyAir(color, _TopY - input.positionWS.y, d, _HoneyAir);"), "tier 0/1 air");
            Assert.IsTrue(lattice.Contains("AbHoneyAir(c, depth, 0.0, float4(0.0, 1.0, _HoneyAir.zw))"), "tier 2 tube air");
            Assert.IsTrue(lattice.Contains("AbHoneyAir(color, 0.0, dist, float4(_HoneyAir.xy, 1.0, 0.0))"), "tier 2 aerial veil");
            Assert.IsTrue(recipes.Contains("AbHoneyCellTone(ri.positionWS.xz, float2(AB_HONEY_ORIGIN_X, AB_HONEY_ORIGIN_Z), AB_HONEY_SIZE)"), "tier 0/1 cell tone");
            Assert.IsTrue(lattice.Contains("AbHoneyCellTone(p, _LatticeOrigin.xy, size)"), "tier 2 cell tone");

            MaterialBuilder.BuildAll();
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            Vector4 air = lib[MatId.HoneyTube].GetVector("_HoneyAir");
            Assert.AreEqual(MaterialBuilder.HoneyAir, air, "HoneyTube air");
            Assert.AreEqual(air, lib[MatId.Lattice].GetVector("_HoneyAir"), "Lattice air equals HoneyTube air (14.10 c)");
            foreach (MatId id in new[] { MatId.ConcreteBoard, MatId.ConcreteFloor, MatId.SteelPainted })
                Assert.AreEqual(Vector4.zero, lib[id].GetVector("_HoneyAir"), id + " has no honeycomb air (pier, balcony, Face stay unfogged)");

            float tubeK = ParseDefine(inc, "AB_TUBE_AIR_K");
            Color horiz = AbyssPalette.OUT_HORIZ.linear, down = AbyssPalette.EXT_FOG_DOWN.linear;
            float L(Color c) => Luma(c);
            float Air(float lumaIn, float depth, float dist)
            {
                float tube = Mathf.Clamp01(air.w) * (1f - Mathf.Exp(-depth / air.z));
                float c = Mathf.Lerp(lumaIn, L(horiz) * tubeK, tube);
                float aerial = Mathf.Clamp01(air.x) * (1f - Mathf.Exp(-dist / air.y));
                return Mathf.Lerp(c, L(horiz), aerial);
            }
            float pit = Air(L(down), 300f, 1000f);
            Assert.That(pit, Is.InRange(0.02f, 0.05f), "a deep near pit is blue-grey air, not black");
            Assert.LessOrEqual(Mathf.Clamp01(air.x) * (1f - Mathf.Exp(-10000f / air.y)), 0.25f, "veil under 25 % at 10 km");
            Assert.GreaterOrEqual(Mathf.Clamp01(air.x) * (1f - Mathf.Exp(-1000f / air.y)), 0.04f, "a veil of at least 4 % at 1 km");
        }

        // ------------------------------------------------------------------------------------------ facade depth (R2-12)
        /// <summary>
        /// R2-12: windows of both facade tiers fall off with distance (exp(-(d - 170) / 210) beyond 170 m; one shader, so the
        /// 14.10 b crossover holds) and the city gets an aerial blend of 10-15 % by 400 m: a window at 400 m keeps about a
        /// third of its emission, one at 190 m over 90 %.
        /// </summary>
        [Test]
        public void FacadeWindowsFallOffWithDistanceInBothTiers()
        {
            string f = File.ReadAllText(ShaderFilePath("Facade.shader"));
            float d0 = ParseDefine(f, "FACADE_FALL_D0"), l = ParseDefine(f, "FACADE_FALL_L");
            Assert.IsTrue(f.Contains("float fall = exp(-max(camDist - FACADE_FALL_D0, 0.0) / FACADE_FALL_L);"), "falloff mirrored");
            Assert.AreEqual(1f, Mathf.Exp(-Mathf.Max(150f - d0, 0f) / l), 1e-6f, "full emission up to 170 m");
            Assert.Greater(Mathf.Exp(-Mathf.Max(190f - d0, 0f) / l), 0.9f, "near facade (190 m) keeps over 90 %");
            Assert.That(Mathf.Exp(-Mathf.Max(400f - d0, 0f) / l), Is.InRange(0.25f, 0.45f), "a window at 400 m keeps about a third (KS5a face-150)");
            Assert.AreEqual(1f, Mathf.Exp(-Mathf.Max(AbyssConst.NEAR_TIER - d0, 0f) / l), 1e-6f, "no falloff at the 14.10 b tier crossover");
            Assert.That(ParseDefine(f, "FACADE_AERIAL"), Is.InRange(0.10f, 0.15f), "city-only aerial blend 10-15 %");
            Assert.AreEqual(400f, ParseDefine(f, "FACADE_AER_D1"), 1e-4f, "reached by 400 m");
            // the term is outside the tier branch: both tiers (shader quads and module glass) get it
            int branch = f.IndexOf("if (_Recipe > 0.5)");
            Assert.Greater(f.IndexOf("emission *= fall *"), branch, "applied after the tier-specific mask");
        }
    }
}
