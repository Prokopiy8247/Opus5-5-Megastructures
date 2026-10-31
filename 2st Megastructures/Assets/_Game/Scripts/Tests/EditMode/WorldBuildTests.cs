using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Megastructure.Tests
{
    /// <summary>
    /// Builds the whole world synchronously in edit mode (every IBeatBuilder of the runtime assembly, in Order, with a
    /// BuildContext over an instantiated RingAssets), then validates every generated mesh (Part 8.7, Part 10 #29:
    /// no degenerate triangles, valid indices, no NaN vertices, unit normals, winding agreeing with the normals,
    /// bounds containing the vertices) and checks the dimensions of the registered objects against 14.5 / 14.12
    /// (Part 10 #27). A key that is not registered yet is reported with Assert.Ignore, not failed.
    /// Writes QA/mesh-report.json (per-builder triangles, per-mesh results, measured registry dimensions).
    /// </summary>
    [TestFixture]
    [Timeout(1800000)]
    public class WorldBuildTests
    {
        const string AssetsPath = "Assets/_Game/Settings/RingAssets.asset";

        static GameObject _root;
        static BuildContext _ctx;
        static RingAssets _assets;
        static readonly List<string> _buildErrors = new List<string>();
        static readonly List<string> _builders = new List<string>();
        static readonly List<string> _logErrors = new List<string>();
        static readonly Dictionary<string, string> _measured = new Dictionary<string, string>();
        static double _buildSeconds;

        // materials whose meshes are billboards, two-sided cards or additive quads (winding is not meaningful)
        static readonly string[] WindingExempt = { "Mote", "LightPool", "Glow", "Grass", "Flower", "Swarm", "Beacon", "Emissive", "Window" };

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                lock (_logErrors) if (_logErrors.Count < 50) _logErrors.Add(type + ": " + msg);
        }

        [OneTimeSetUp]
        public void BuildWorld()
        {
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            var t0 = DateTime.Now;
            try
            {
                var src = AssetDatabase.LoadAssetAtPath<RingAssets>(AssetsPath);
                _assets = src != null ? Object.Instantiate(src) : ScriptableObject.CreateInstance<RingAssets>();
                _assets.hideFlags = HideFlags.DontSave;
                if (src == null) _buildErrors.Add("RingAssets.asset not found at " + AssetsPath + " (run BatchTools.RebuildAll); materials are null");
                RingAssets.Instance = _assets;
                _root = new GameObject("[WorldBuildTest]") { hideFlags = HideFlags.DontSave };
                _ctx = new BuildContext(_assets, _root.transform);
                RingRig.Context = _ctx;

                var types = new List<Type>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!asm.GetName().Name.StartsWith("Megastructure")) continue;
                    Type[] ts;
                    try { ts = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types; }
                    foreach (var t in ts)
                        if (t != null && typeof(IBeatBuilder).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && t.Name != "WorldBuilder")
                            types.Add(t);
                }
                var builders = new List<IBeatBuilder>();
                foreach (var t in types)
                {
                    try
                    {
                        if (typeof(Component).IsAssignableFrom(t)) builders.Add((IBeatBuilder)_root.AddComponent(t));
                        else if (typeof(ScriptableObject).IsAssignableFrom(t)) builders.Add((IBeatBuilder)ScriptableObject.CreateInstance(t));
                        else builders.Add((IBeatBuilder)Activator.CreateInstance(t));
                    }
                    catch (Exception e) { _buildErrors.Add("cannot create " + t.FullName + ": " + e.Message); }
                }
                builders.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                foreach (var b in builders)
                {
                    _builders.Add(b.Order + " " + b.Name);
                    _ctx.CurrentBuilder = b.Name;
                    try { Run(b.Build(_ctx)); }
                    catch (Exception e) { _buildErrors.Add(b.Name + " threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
                }
            }
            catch (Exception e) { _buildErrors.Add("world build failed: " + e); }
            _buildSeconds = (DateTime.Now - t0).TotalSeconds;
        }

        /// <summary>Runs a builder coroutine to completion, nested enumerators included; other yield values are skipped.</summary>
        static void Run(IEnumerator e)
        {
            if (e == null) return;
            var stack = new Stack<IEnumerator>();
            stack.Push(e);
            long steps = 0;
            while (stack.Count > 0)
            {
                if (++steps > 20000000) throw new Exception("builder did not finish within 2e7 steps");
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            try { WriteReport(); } catch (Exception e) { Debug.LogWarning("[Ring] mesh-report.json: " + e.Message); }
            Application.logMessageReceived -= OnLog;
            // GPU buffers of code-drawn fields (IDisposable or a public Release())
            if (_ctx != null)
            {
                foreach (var d in _ctx.Drawables) ReleaseObject(d);
                foreach (var a in _ctx.Animated) ReleaseObject(a);
            }
            if (_root != null)
            {
                foreach (var mf in _root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh)) Object.DestroyImmediate(mf.sharedMesh);
                foreach (var mc in _root.GetComponentsInChildren<MeshCollider>(true))
                    if (mc.sharedMesh != null && !EditorUtility.IsPersistent(mc.sharedMesh)) Object.DestroyImmediate(mc.sharedMesh);
                Object.DestroyImmediate(_root);
            }
            if (_assets != null && !EditorUtility.IsPersistent(_assets)) Object.DestroyImmediate(_assets);
            RingRig.Context = null;
            RingAssets.Instance = null;
            _root = null; _ctx = null; _assets = null;
            _buildErrors.Clear(); _builders.Clear(); _logErrors.Clear(); _measured.Clear();
        }

        static void ReleaseObject(object o)
        {
            if (o == null) return;
            try
            {
                if (o is IDisposable disp) { disp.Dispose(); return; }
                var m = o.GetType().GetMethod("Release", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m != null) m.Invoke(o, null);
            }
            catch (Exception e) { Debug.LogWarning("[Ring] WorldBuildTests: releasing " + o.GetType().Name + " threw " + e.Message); }
        }

        [SetUp]
        public void PerTest() { LogAssert.ignoreFailingMessages = true; }

        // ---------------- build ----------------
        [Test]
        public void Build_RunsEveryBuilderWithoutErrors()
        {
            if (_builders.Count == 0) Assert.Ignore("no IBeatBuilder implementations exist yet");
            Assert.AreEqual(0, _buildErrors.Count, "builder errors:\n" + string.Join("\n", _buildErrors));
            Assert.AreEqual(0, _logErrors.Count, "errors logged during the build:\n" + string.Join("\n", _logErrors));
            TestContext.WriteLine("builders (" + _builders.Count + ", " + _buildSeconds.ToString("0.0") + " s): " + string.Join(", ", _builders) + "; triangles " + _ctx.Triangles);
        }

        // ---------------- meshes ----------------
        static string MaterialName(MeshFilter mf)
        {
            var r = mf.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return "";
            return r.sharedMaterial.name + "|" + (r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "");
        }

        static bool Exempt(MeshFilter mf)
        {
            string m = MaterialName(mf) + "|" + mf.name + "|" + (mf.sharedMesh != null ? mf.sharedMesh.name : "");
            foreach (var e in WindingExempt) if (m.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        [Test]
        public void Meshes_AreValid()
        {
            if (_root == null) Assert.Ignore("world not built");
            var mfs = _root.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) Assert.Ignore("no meshes generated yet");
            var fails = new StringBuilder();
            int bad = 0;
            foreach (var mf in mfs)
            {
                var m = mf.sharedMesh;
                if (m == null) { bad++; fails.AppendLine(Path(mf.transform) + ": null mesh"); continue; }
                if (!m.isReadable) continue;
                var r = MeshValidation.Validate(m);
                bool tooBig = m.vertexCount > 65535 && m.indexFormat != UnityEngine.Rendering.IndexFormat.UInt32;
                if (!r.ok || tooBig)
                {
                    bad++;
                    if (bad <= 40) fails.AppendLine(Path(mf.transform) + ": degenerate " + r.degenerate + ", bad index " + r.badIndex + ", NaN " + r.nanVerts +
                                                    ", zero normals " + r.zeroNormals + (tooBig ? ", >65535 vertices with 16-bit indices" : ""));
                }
            }
            Assert.AreEqual(0, bad, "invalid meshes (" + bad + " of " + mfs.Length + "):\n" + fails);
        }

        [Test]
        public void Meshes_WindingAgreesWithNormals()
        {
            if (_root == null) Assert.Ignore("world not built");
            var mfs = _root.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) Assert.Ignore("no meshes generated yet");
            var fails = new StringBuilder();
            int bad = 0, checkedN = 0;
            foreach (var mf in mfs)
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable || Exempt(mf) || m.GetTopology(0) != MeshTopology.Triangles) continue;
                checkedN++;
                float w = MeshValidation.WindingAgreement(m);
                if (w < 0.98f)
                {
                    bad++;
                    if (bad <= 40) fails.AppendLine(Path(mf.transform) + " (" + MaterialName(mf) + "): winding agreement " + w.ToString("0.000"));
                }
            }
            Assert.AreEqual(0, bad, "meshes with winding disagreeing with their normals (" + bad + " of " + checkedN + "):\n" + fails);
        }

        [Test]
        public void Meshes_BoundsContainVertices()
        {
            if (_root == null) Assert.Ignore("world not built");
            var mfs = _root.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) Assert.Ignore("no meshes generated yet");
            var v = new List<Vector3>();
            var fails = new StringBuilder();
            int bad = 0;
            foreach (var mf in mfs)
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                m.GetVertices(v);
                var b = m.bounds;
                b.Expand(1e-3f);
                foreach (var p in v) if (!b.Contains(p)) { bad++; if (bad <= 20) fails.AppendLine(Path(mf.transform) + ": vertex " + p + " outside bounds " + b); break; }
            }
            Assert.AreEqual(0, bad, "meshes whose bounds do not contain their vertices:\n" + fails);
        }

        static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null && t.parent != _root.transform) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        // ---------------- registry dimensions ----------------
        public struct Dim
        {
            public string Key;
            public string Check;
            public override string ToString() => Key + " " + Check;
        }

        static Dim D(string key, string check) => new Dim { Key = key, Check = check };

        /// <summary>
        /// Checks per key: "size.x=2.6" (AABB size), "min.y=0.9", "max.z=9.5", "centre.x=114", tolerance after '~'
        /// (default 0.02 m). Values come from 14.5 acceptance lists and 14.12.
        /// </summary>
        public static IEnumerable<Dim> Dims()
        {
            yield return D("Capsule.Interior", "size.x=2.6;size.y=2.5;size.z=11.0;min.y=0.9;min.z=-1.5;max.z=9.5");
            yield return D("Capsule.Porthole", "centre.x=-1.46~0.05;centre.y=2.98~0.05;centre.z=5.65~0.05");
            yield return D("Capsule.Hatch", "size.x=2.0;size.y=1.9;min.y=0.9~0.05");
            yield return D("Bridge.Deck", "max.y=3.0~0.01;size.x=5.0;min.z=120~0.05;max.z=180~0.05");
            yield return D("Bridge.Rail", "max.y=4.0~0.01");
            yield return D("River.Ribbon", "max.y=-2.2~0.05;size.y=0~0.1");
            yield return D("Ark.Hull", "size.x=3000~5;size.y=260~5;size.z=420~5;centre.y=1800~1;centre.z=3180~5");
            yield return D("Footbridge.Opening", "size.x=4.2;size.y=3.2;size.z=8.0;min.z=396~0.05;max.z=404~0.05");
            yield return D("Underpass", "size.y=2.9;min.z=558;max.x=18;min.x=-1.4;max.z=577.4");
            yield return D("Levee", "min.x=-70~0.05;max.x=18~0.05;min.z=558~0.05;max.z=640~0.05;max.y=14~0.15");
            yield return D("Wall", "face.x=114~0.01;max.y=12000~1;min.z=-9000~1;max.z=9000~1");
            yield return D("Door.Opening", "size.z=2.6;size.y=3.0;min.y=0~0.02;centre.z=660~0.02");
            yield return D("Tunnel", "size.z=2.6;size.y=3.0;spanmin.x=114.8;spanmax.x=234");
            yield return D("Tunnel.Rib0", "centre.x=117~0.01;size.x=0.40");
            yield return D("Platform.Deck", "size.x=72~0.05;size.z=12~0.05;min.x=234~0.05;max.x=306~0.05;min.z=654~0.05;max.z=666~0.05");
            yield return D("Beacon", "size.y=1.3;centre.x=301~0.05;centre.z=660~0.05");
            yield return D("Swarm", "count=24000");
        }

        [TestCaseSource(nameof(Dims))]
        public void Registry_Dimensions(Dim dim)
        {
            if (_ctx == null) Assert.Ignore("world not built");
            if (!_ctx.Registry.TryGetValue(dim.Key, out var obj) || obj == null)
            {
                _measured[dim.Key] = "not registered";
                Assert.Ignore("registry key '" + dim.Key + "' is not registered yet");
            }
            if (!RegistryMeasure.TryWorldBounds(obj, out Bounds b, out string how)) Assert.Fail(dim.Key + ": cannot measure");
            var fails = new List<string>();
            var verts = new List<Vector3>();
            foreach (var part in dim.Check.Split(';'))
            {
                string expr = part;
                float tol = 0.02f;
                int ti = expr.IndexOf('~');
                if (ti >= 0) { tol = float.Parse(expr.Substring(ti + 1), System.Globalization.CultureInfo.InvariantCulture); expr = expr.Substring(0, ti); }
                var kv = expr.Split('=');
                string lhs = kv[0];
                float want = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                float got;
                if (lhs == "count")
                {
                    if (!TryCount(obj, out int n)) { _measured[dim.Key] = how + " (no count member)"; Assert.Ignore(dim.Key + ": no count member (PanelCount, TotalPanels, InstanceCount or Count)"); }
                    got = n;
                    tol = 0.5f;
                }
                else if (lhs.StartsWith("face."))
                {
                    // fraction of the vertices lying on the plane (the wall face at x 114, with the plinth proud of it)
                    if (RegistryMeasure.WorldVertices(obj, verts) == 0) { fails.Add(lhs + ": no vertices"); continue; }
                    int on = 0; foreach (var p in verts) if (Mathf.Abs(Axis(p, lhs[5]) - want) <= tol) on++;
                    float frac = on / (float)verts.Count;
                    if (frac < 0.25f) fails.Add(lhs + "=" + want + ": only " + (frac * 100f).ToString("0.0") + " % of the vertices on the plane");
                    continue;
                }
                else
                {
                    var parts = lhs.Split('.');
                    char ax = parts[1][0];
                    switch (parts[0])
                    {
                        case "size": got = Axis(b.size, ax); break;
                        case "min": got = Axis(b.min, ax); break;
                        case "max": got = Axis(b.max, ax); break;
                        case "centre": got = Axis(b.center, ax); break;
                        case "spanmin": got = Axis(b.min, ax); if (got <= want + tol) continue; fails.Add(lhs + " " + got + " > " + want); continue;
                        case "spanmax": got = Axis(b.max, ax); if (got >= want - tol) continue; fails.Add(lhs + " " + got + " < " + want); continue;
                        default: Assert.Fail("bad check " + lhs); return;
                    }
                }
                if (Mathf.Abs(got - want) > tol) fails.Add(lhs + " = " + got.ToString("0.###") + " (expected " + want + " +- " + tol + ")");
            }
            _measured[dim.Key] = how + " min " + b.min.ToString("0.###") + " max " + b.max.ToString("0.###") + (fails.Count > 0 ? " FAIL " + string.Join("; ", fails) : " ok");
            Assert.AreEqual(0, fails.Count, dim.Key + " (" + how + ", bounds " + b.min.ToString("0.###") + " .. " + b.max.ToString("0.###") + "):\n" + string.Join("\n", fails));
        }

        [Test]
        public void Registry_TunnelRibPitch()
        {
            if (_ctx == null) Assert.Ignore("world not built");
            bool has0 = _ctx.Registry.TryGetValue("Tunnel.Rib0", out var r0);
            bool has1 = _ctx.Registry.TryGetValue("Tunnel.Rib1", out var r1);
            if (!has0 || !has1) Assert.Ignore("Tunnel.Rib0 / Tunnel.Rib1 not registered");
            RegistryMeasure.TryWorldBounds(r0, out var b0, out _);
            RegistryMeasure.TryWorldBounds(r1, out var b1, out _);
            Assert.AreEqual(RingConstants.TunnelRibPitch, b1.center.x - b0.center.x, 0.01f, "rib pitch 6.00 +- 0.01 (14.5.6)");
        }

        [Test]
        public void Registry_ExpectedKeysPresent()
        {
            if (_ctx == null || _builders.Count == 0) Assert.Ignore("world not built");
            var missing = new List<string>();
            foreach (var d in Dims()) if (!_ctx.Registry.ContainsKey(d.Key)) missing.Add(d.Key);
            foreach (var k in new[] { "Bridge.Rail", "River.Ribbon", "Tunnel.Rib0" }) if (!_ctx.Registry.ContainsKey(k) && !missing.Contains(k)) missing.Add(k);
            TestContext.WriteLine("registry keys: " + string.Join(", ", new List<string>(_ctx.Registry.Keys)));
            if (missing.Count > 0) Assert.Ignore("not registered yet: " + string.Join(", ", missing));
        }

        static float Axis(Vector3 v, char a) => a == 'x' ? v.x : a == 'y' ? v.y : v.z;

        static bool TryCount(Object obj, out int n)
        {
            n = 0;
            var targets = new List<object> { obj };
            if (obj is GameObject go) targets.AddRange(go.GetComponents<Component>());
            foreach (var t in targets)
            {
                if (t == null) continue;
                var ty = t.GetType();
                foreach (var name in new[] { "PanelCount", "TotalPanels", "InstanceCount", "Count" })
                {
                    var p = ty.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(int)) { n = (int)p.GetValue(t); return true; }
                    var f = ty.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(int)) { n = (int)f.GetValue(t); return true; }
                }
            }
            return false;
        }

        // ---------------- report ----------------
        static void WriteReport()
        {
            if (_ctx == null) return;
            var j = new QaJson();
            j.Obj().KV("generated", QaUtil.Stamp()).KV("build_seconds", _buildSeconds).KV("triangles", _ctx.Triangles).Key("builders").Arr();
            foreach (var b in _builders) j.V(b);
            j.EndArr().Key("triangles_by_builder").Obj();
            foreach (var kv in _ctx.TrianglesByBuilder) j.KV(kv.Key, kv.Value);
            j.EndObj().Key("errors").Arr();
            foreach (var e in _buildErrors) j.V(e);
            foreach (var e in _logErrors) j.V(e);
            j.EndArr().Key("registry").Obj();
            foreach (var kv in _ctx.Registry)
            {
                string m = _measured.TryGetValue(kv.Key, out var x) ? x : (RegistryMeasure.TryWorldBounds(kv.Value, out var bb, out string how) ? how + " min " + bb.min.ToString("0.###") + " max " + bb.max.ToString("0.###") : "unmeasured");
                j.KV(kv.Key, m);
            }
            j.EndObj().Key("meshes").Arr();
            if (_root != null)
                foreach (var mf in _root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (m == null || !m.isReadable) continue;
                    var r = MeshValidation.Validate(m);
                    j.Obj().KV("path", Path(mf.transform)).KV("material", MaterialName(mf)).KV("vertices", m.vertexCount).KV("triangles", r.triangles)
                     .KV("ok", r.ok).KV("degenerate", r.degenerate).KV("winding", Exempt(mf) ? float.NaN : MeshValidation.WindingAgreement(m)).EndObj();
                }
            j.EndArr().EndObj();
            QaUtil.WriteText(System.IO.Path.Combine(QaUtil.ProjectRoot(), "QA", "mesh-report.json"), j.ToString());
        }
    }
}
