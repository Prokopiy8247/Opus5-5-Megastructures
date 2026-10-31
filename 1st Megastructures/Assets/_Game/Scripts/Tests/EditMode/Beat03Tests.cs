using System;
using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 3 (14.5.3) checks in all three tiers: ledge 6.0 +/- 0.02, arc 78.54 +/- 0.05, drop 4.0, collar 2.5 thick, hatch
    /// 2.0 +/- 0.01 (0.9 wide), apron 32 x 14, kerb 0.15 x 0.15, corbel 8 m, 5 cm column overlap, pad end on x = -3; every mesh
    /// valid; the walking-surface collider is the visible helicoid (y linear in alpha from -3 to -7, shared vertices) and a
    /// QA-style 0.25 m ray grid over the ledge and the apron hits it everywhere within 3 mm (no gaps, no steps); flush
    /// transitions to the bridge end and the apron; lamps, hatch and lights per tier; KS3 subjects; the detail tier swap.
    /// </summary>
    public sealed class Beat03Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";

        MaterialLibrary _lib;
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Mesh> _meshes = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            _lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (_lib == null) Assert.Inconclusive("MaterialLibrary missing at " + LibraryPath + " (run MaterialBuilder.BuildAll)");
            if (_lib.materials == null || _lib.materials.Length < (int)MatId.Count) Assert.Inconclusive("MaterialLibrary incomplete");
            for (int i = 0; i < (int)MatId.Count; i++)
                if (_lib.materials[i] == null) Assert.Inconclusive("MaterialLibrary lacks base material " + (MatId)i);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) UnityEngine.Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
        }

        Beat03_Ledge Build(BeatTier tier)
        {
            var root = new GameObject("Beat03Test_" + tier);
            _roots.Add(root);
            var b = root.AddComponent<Beat03_Ledge>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            b.Build(new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = true });
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            Physics.SyncTransforms();
            return b;
        }

        static IEnumerable<BeatTier> Tiers()
        {
            yield return BeatTier.Full;
            yield return BeatTier.Simplified;
            yield return BeatTier.Graybox;
        }

        static Dictionary<string, double> Report(BeatBase b)
        {
            var r = new ScaleReport();
            b.ReportScale(r);
            return r.Values;
        }

        static void Near(Dictionary<string, double> r, string key, double expected, double tol, BeatTier tier)
        {
            Assert.IsTrue(r.TryGetValue(key, out double v), tier + ": ReportScale lacks " + key);
            Assert.That(v, Is.EqualTo(expected).Within(tol), tier + " " + key);
        }

        // ---------------------------------------------------------------- dimensions

        [Test, TestCaseSource(nameof(Tiers))]
        public void LedgeArcHatchAndApronMatchTheSpec(BeatTier tier)
        {
            Dictionary<string, double> r = Report(Build(tier));
            Near(r, "ledge.width", AbyssConst.LEDGE_W, 0.02, tier);
            Near(r, "ledge.arc", AbyssConst.ARC_LENGTH, 0.05, tier);
            Near(r, "ledge.drop", AbyssConst.LEDGE_DROP, 0.005, tier);
            Near(r, "ledge.thickness", AbyssConst.LEDGE_COLLAR_THICK, 0.005, tier);
            Near(r, "ledge.column.overlap", Beat03_Ledge.COLUMN_OVERLAP, 0.002, tier);
            Near(r, "ledge.pad.y", AbyssConst.BRIDGE_Y, 1e-4, tier);
            Near(r, "ledge.pad.end.x", Beat03_Ledge.PAD_END_X, 0.003, tier);
            Near(r, "apron.length", AbyssConst.APRON_L, 0.01, tier);
            Near(r, "apron.width", AbyssConst.APRON_W, 0.01, tier);
            Near(r, "lamp.head.above.floor", Beat03_Ledge.LAMP_HEAD_ABOVE_FLOOR, 0.005, tier);
            Near(r, "lamp.count", tier == BeatTier.Simplified ? 2 : AbyssConst.LEDGE_LAMP_ALPHA.Length, 1e-9, tier);
            if (tier == BeatTier.Simplified)
            {
                Assert.IsFalse(r.ContainsKey("hatch.height"), "Simplified has no hatch (14.5.3 Fallbacks)");
            }
            else
            {
                Near(r, "hatch.height", AbyssConst.HATCH_H, 0.01, tier);
                Near(r, "hatch.width", AbyssConst.HATCH_W, 0.01, tier);
            }
            if (tier != BeatTier.Graybox)
            {
                Near(r, "ledge.kerb.height", AbyssConst.LEDGE_KERB, 0.003, tier);
                Near(r, "ledge.kerb.width", AbyssConst.LEDGE_KERB, 0.003, tier);
                Near(r, "ledge.corbel.drop", AbyssConst.LEDGE_CORBEL, 0.02, tier);
            }
        }

        [Test, TestCaseSource(nameof(Tiers))]
        public void EveryMeshIsValid(BeatTier tier)
        {
            Beat03_Ledge b = Build(tier);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            Assert.Greater(list.Count, 3, tier + ": too few meshes");
            foreach (Mesh m in list)
            {
                MeshReport rep = MeshValidation.Validate(m);
                Assert.IsTrue(rep.Ok, tier + ": " + rep.Describe());
            }
        }

        // ---------------------------------------------------------------- walking surface == collider

        [Test, TestCaseSource(nameof(Tiers))]
        public void WalkColliderIsTheVisibleHelicoid(BeatTier tier)
        {
            Beat03_Ledge b = Build(tier);
            Mesh col = b.WalkColliderMesh;
            Assert.IsNotNull(col, "no walking-surface collider mesh");
            Vector3[] cv = col.vertices;
            Vector3[] cn = col.normals;
            float rMin = float.MaxValue, rMax = 0f;
            double aMin = double.MaxValue, aMax = double.MinValue;
            int top = 0;
            for (int i = 0; i < cv.Length; i++)
            {
                if (cn[i].y < 0.5f) continue;
                top++;
                double a = Beat03_Ledge.AlphaOf(cv[i].x, cv[i].z);
                Assert.That(cv[i].y, Is.EqualTo(Beat03_Ledge.TopY(a)).Within(2e-4f), "collider top off the helicoid at " + cv[i].ToString("F3"));
                float r = Radius(cv[i]);
                rMin = Mathf.Min(rMin, r);
                rMax = Mathf.Max(rMax, r);
                aMin = Math.Min(aMin, a);
                aMax = Math.Max(aMax, a);
            }
            Assert.Greater(top, 1000, "collider top surface too coarse");
            Assert.LessOrEqual(rMin, AbyssConst.COLLIDER_INNER_R - 0.5f, "collider must reach under the column wall");
            Assert.GreaterOrEqual(rMax, AbyssConst.LEDGE_R_OUT + 0.5f, "collider must reach under the outer wall");
            Assert.LessOrEqual(aMin, Beat03_Ledge.AlphaOf(Beat03_Ledge.PAD_END_X, AbyssConst.ARC_CENTRE.y - AbyssConst.LEDGE_R_OUT) - 0.05, "collider must reach past the pad end");
            Assert.GreaterOrEqual(aMax, Math.PI + 0.05, "collider must run onto the apron");

            // the visible walking surface lies on the same function, and every one of its vertices is a collider vertex
            var set = new HashSet<Vector3>(cv);
            int shared = 0;
            foreach (Mesh m in new[] { b.PadTopMesh, b.HelixTopMesh })
            {
                Assert.IsNotNull(m, tier + ": walking surface mesh missing");
                foreach (Vector3 p in m.vertices)
                {
                    Assert.That(p.y, Is.EqualTo(Beat03_Ledge.SurfaceY(p.x, p.z)).Within(2e-4f), "visible floor off the helicoid at " + p.ToString("F3"));
                    float r = Radius(p);
                    Assert.That(r, Is.InRange(Beat03_Ledge.COLLIDER_R_IN, Beat03_Ledge.COLLIDER_R_OUT), "visible floor outside the collider at " + p.ToString("F3"));
                    if (m == b.HelixTopMesh)
                    {
                        Assert.IsTrue(set.Contains(p), "visible helix vertex " + p.ToString("F4") + " is not a collider vertex");
                        shared++;
                    }
                }
            }
            Assert.Greater(shared, 0);
        }

        /// <summary>
        /// The QA ray grid of the built player (QaBots.RayGrid: 0.25 m along d and across, out to HalfWidth - 0.02, rays from
        /// 1 m above the floor) over the ledge (from the bridge end W5) and the apron: every ray hits a corridor collider, and
        /// within 3 mm of the true walking surface (the helicoid on the ledge, y -7 on the apron).
        /// </summary>
        [Test]
        public void RayGridHitsTheWalkingSurfaceEverywhere()
        {
            Build(BeatTier.Full);
            int mask = 1 << CorridorKit.LayerCorridor;
            int points = 0, misses = 0;
            float worst = 0f;
            string firstMiss = null, worstAt = null;
            for (float d = AbyssConst.WD[5] + 0.05f; d < AbyssConst.WD[9] - 0.05f; d += 0.25f)
            {
                Vector3 c = PathGuide.FloorPoint(d);
                Vector3 t = PathGuide.Tangent(d);
                var right = new Vector3(t.z, 0f, -t.x);
                float half = PathGuide.HalfWidth(d);
                int n = Mathf.FloorToInt((half - 0.02f) / 0.25f);
                for (int k = -n - 1; k <= n + 1; k++)
                {
                    float o = k * 0.25f;
                    if (k == -n - 1) o = -(half - 0.02f);
                    if (k == n + 1) o = half - 0.02f;
                    Vector3 p = c + right * o;
                    points++;
                    float expect = d < AbyssConst.WD[8] ? Beat03_Ledge.SurfaceY(p.x, p.z) : AbyssConst.W[8].y;
                    if (!Physics.Raycast(p + Vector3.up, Vector3.down, out RaycastHit h, 3f, mask, QueryTriggerInteraction.Ignore))
                    {
                        misses++;
                        if (firstMiss == null) firstMiss = "d " + d.ToString("F2") + " offset " + o.ToString("F2") + " " + p.ToString("F2");
                        continue;
                    }
                    float dy = Mathf.Abs(h.point.y - expect);
                    if (dy > worst) { worst = dy; worstAt = "d " + d.ToString("F2") + " offset " + o.ToString("F2") + " (" + h.collider.name + ")"; }
                }
            }
            Assert.Greater(points, 10000, "grid too small");
            Assert.AreEqual(0, misses, misses + " of " + points + " rays found no collider, first at " + firstMiss);
            Assert.LessOrEqual(worst, 0.003f, "collider off the walking surface by " + worst + " m at " + worstAt);
        }

        [Test]
        public void TransitionsToBridgeAndApronAreFlush()
        {
            Beat03_Ledge b = Build(BeatTier.Full);
            Assert.That(Beat03_Ledge.TopY(0.0), Is.EqualTo(AbyssConst.BRIDGE_Y).Within(1e-6f), "helix start = bridge deck height");
            Assert.That(Beat03_Ledge.TopY(Math.PI), Is.EqualTo(AbyssConst.W[8].y).Within(1e-6f), "helix end = apron height");
            Assert.That(Beat03_Ledge.TopY(-0.1), Is.EqualTo(AbyssConst.BRIDGE_Y).Within(1e-6f), "pad is flat");
            Assert.That(Beat03_Ledge.TopY(0.5 * Math.PI), Is.EqualTo(AbyssConst.W[7].y).Within(1e-5f), "W7 height");

            BoxCollider apron = null, padEnd = null;
            foreach (BoxCollider bc in b.GetComponentsInChildren<BoxCollider>(true))
            {
                if (bc.name == "ApronFloor") apron = bc;
                if (bc.name == "PadEndWall") padEnd = bc;
            }
            Assert.IsNotNull(apron, "no ApronFloor");
            Assert.IsNotNull(padEnd, "no PadEndWall");
            Assert.That(WorldAabb(apron).max.y, Is.EqualTo(AbyssConst.W[8].y).Within(1e-4f), "apron floor top");
            Assert.That(WorldAabb(padEnd).max.x, Is.EqualTo(Beat03_Ledge.PAD_END_X).Within(1e-3f), "pad end wall flush with the cut");

            int mask = 1 << CorridorKit.LayerCorridor;
            // the bridge end: the deck (Beat02) ends at z = 216, the walk collider is under it at the deck height
            for (float x = -0.55f; x <= 0.551f; x += 0.11f)
            {
                Assert.IsTrue(Physics.Raycast(new Vector3(x, -2f, AbyssConst.BRIDGE_Z1 + 0.01f), Vector3.down, out RaycastHit h, 3f, mask, QueryTriggerInteraction.Ignore), "bridge end x " + x);
                Assert.That(h.point.y, Is.EqualTo(Beat03_Ledge.SurfaceY(x, AbyssConst.BRIDGE_Z1 + 0.01f)).Within(0.002f), "bridge end x " + x);
                Assert.That(h.point.y, Is.EqualTo(AbyssConst.BRIDGE_Y).Within(0.03f), "step at the bridge end x " + x);
            }
            // the apron joint: both sides of x = 0 at the same height
            for (float z = 266.5f; z <= 271.5f; z += 1f)
            {
                Assert.IsTrue(Physics.Raycast(new Vector3(0.05f, -6f, z), Vector3.down, out RaycastHit e, 3f, mask, QueryTriggerInteraction.Ignore));
                Assert.IsTrue(Physics.Raycast(new Vector3(-0.05f, -6f, z), Vector3.down, out RaycastHit w, 3f, mask, QueryTriggerInteraction.Ignore));
                Assert.That(e.point.y - w.point.y, Is.EqualTo(0f).Within(0.01f), "step at the apron joint z " + z);
            }
        }

        // ---------------------------------------------------------------- content per tier

        [Test]
        public void TiersCarryTheirLampsHatchAndLights()
        {
            // the second cold spot of the 14.2 beat-3 rig (the face seen from the bridge) is Beat02's floodlight (R1-30)
            Beat03_Ledge full = Build(BeatTier.Full);
            Assert.AreEqual(4, full.LampCount);
            Assert.IsTrue(full.HasHatch);
            Assert.AreEqual(5, full.GetComponentsInChildren<Light>(true).Length, "full: 4 amber + 1 cold spot");
            Assert.IsNotNull(full.Detail, "full tier switches its near detail");

            Beat03_Ledge simple = Build(BeatTier.Simplified);
            Assert.AreEqual(2, simple.LampCount);
            Assert.IsFalse(simple.HasHatch);
            Assert.AreEqual(3, simple.GetComponentsInChildren<Light>(true).Length, "simplified: 2 amber + 1 cold spot");

            Beat03_Ledge gray = Build(BeatTier.Graybox);
            Assert.AreEqual(4, gray.LampCount);
            Assert.AreEqual(5, gray.GetComponentsInChildren<Light>(true).Length);

            foreach (Light l in full.GetComponentsInChildren<Light>(true))
            {
                Assert.AreEqual(LightShadows.None, l.shadows, l.name);
                Assert.Greater(l.range, 0f, l.name);
            }
        }

        // ---------------------------------------------------------------- lights against the column

        /// <summary>
        /// Every lamp globe clears C1 and its banded collars (AbyssConst.COLUMN_BAND_*: 2 m bands every 120 m from the column
        /// top, protruding 0.4 m; the y = 0 band sits just above lamp alpha 0.6) by at least 0.3 m. Its amber light (R1-18) is
        /// a spot at the globe centre, aimed down and tilted outward away from the column: with URP's spot model the floor
        /// directly below gets a warm pool (E 0.5 or more), while no point of the C1 face or its bands within +/- 0.3 rad and
        /// 8 m above the floor gets more than E 0.01 (f02's points reached 1.7-2.8 there and washed the face orange-brown).
        /// </summary>
        [Test, TestCaseSource(nameof(Tiers))]
        public void LampGlobesClearTheColumnAndTheirSpotsLightTheFloorNotTheFace(BeatTier tier)
        {
            Beat03_Ledge b = Build(tier);
            var amber = new List<Light>();
            foreach (Light l in b.GetComponentsInChildren<Light>(true)) if (l.name.StartsWith("LedgeAmber", StringComparison.Ordinal)) amber.Add(l);
            float R = AbyssConst.COLUMNS[0].z;
            int lamps = 0;
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                if (!b.TryGetLampHead(i, out Vector3 head)) continue;
                lamps++;
                float d = ColumnDistance(head);
                float clear = d - Beat03_Ledge.LAMP_GLOBE_R;
                Assert.GreaterOrEqual(clear, 0.3f, tier + ": lamp " + i + " globe clears C1 and its bands by only " + clear + " m");
                Light at = null;
                foreach (Light l in amber) if ((l.transform.position - head).magnitude < 0.005f) at = l;
                Assert.IsNotNull(at, tier + ": no amber light at the centre of globe " + i + " " + head.ToString("F3"));
                Assert.AreEqual(LightType.Spot, at.type, tier + ": lamp " + i);
                Assert.LessOrEqual(at.transform.forward.y, -0.95f, tier + ": lamp " + i + " spot aims down");
                double alpha = AbyssConst.LEDGE_LAMP_ALPHA[i];
                Vector3 outward = Beat03_Ledge.Ring(alpha, 1f, 0f) - Beat03_Ledge.Ring(alpha, 0f, 0f);
                Assert.Greater(Vector3.Dot(at.transform.forward, outward), 0.1f, tier + ": lamp " + i + " spot tilts away from the column");

                float floorE = SpotIlluminance(at, Beat03_Ledge.Ring(alpha, Beat03_Ledge.LAMP_HEAD_R, Beat03_Ledge.TopY(alpha)), Vector3.up);
                Assert.GreaterOrEqual(floorE, 0.5f, tier + ": lamp " + i + " warm pool on the floor below");
                float worst = 0f;
                string worstAt = "";
                for (double a = alpha - 0.3; a <= alpha + 0.3001; a += 0.02)
                {
                    Vector3 n = Beat03_Ledge.Ring(a, 1f, 0f) - Beat03_Ledge.Ring(a, 0f, 0f);
                    float fy = Beat03_Ledge.TopY(a);
                    for (float h = 0f; h <= 8f; h += 0.25f)
                    {
                        float y = fy + h;
                        // the face, and the band's outer face where a band is (2 m bands every 120 m from the column top)
                        float bandC = AbyssConst.COLUMN_TOP_Y - AbyssConst.COLUMN_BAND_SPACING * Mathf.Round((AbyssConst.COLUMN_TOP_Y - y) / AbyssConst.COLUMN_BAND_SPACING);
                        bool onBand = Mathf.Abs(y - bandC) <= 0.5f * AbyssConst.COLUMN_BAND_H;
                        float e = SpotIlluminance(at, Beat03_Ledge.Ring(a, R, y), n);
                        if (onBand) e = Mathf.Max(e, SpotIlluminance(at, Beat03_Ledge.Ring(a, R + AbyssConst.COLUMN_BAND_PROTRUSION, y), n));
                        if (e > worst) { worst = e; worstAt = "alpha " + a.ToString("F2") + " +" + h + " m"; }
                    }
                }
                Assert.LessOrEqual(worst, 0.01f, tier + ": lamp " + i + " lights the column face at " + worstAt);
            }
            Assert.AreEqual(b.LampCount, lamps, tier + ": lamp heads");
            Assert.AreEqual(lamps, amber.Count, tier + ": one amber light per globe");
        }

        /// <summary>
        /// The lamp globes (R1-17), Full and Simplified: one camera-facing renderer per lamp at the globe centre, a sphere of
        /// LAMP_GLOBE_R with at least 48 segments around its pole (a smooth silhouette at 2-3 m), turned by FaceGlobes so the
        /// pole points at the camera; UV0.y (the GradientCard core weight, Beat03_Ledge.GlobeCoreWeight) is 1 at the pole, falls
        /// monotonically to 0 at the limb and stays 0 behind it; the core emits M_Lamp's AMBER x 8.0 and the limb at least 4x less,
        /// so the globe reads as a lit diffuser that blooms; each globe has an amber halo sprite. Lead decision D4 (R2-21): the
        /// white-hot peak stays in a small core: from 30 % of the projected radius out the weight is at most GLOBE_MID_WEIGHT
        /// (0.14), where the emission displays as saturated amber instead of tonemapper cream.
        /// </summary>
        [Test]
        public void GlobesFaceTheCameraWithAHotCoreAndAnAmberLimb([Values(BeatTier.Full, BeatTier.Simplified)] BeatTier tier)
        {
            Beat03_Ledge b = Build(tier);
            Assert.GreaterOrEqual(Beat03_Ledge.GLOBE_LON, 48, "longitude segments");
            var cam = new Vector3(25f, -3.3f, 244f);   // KS3 eye
            b.FaceGlobes(cam);
            int globes = 0;
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                if (!b.TryGetLampHead(i, out Vector3 head)) { Assert.IsNull(b.GlobeTransform(i)); continue; }
                Transform g = b.GlobeTransform(i);
                Assert.IsNotNull(g, tier + ": globe " + i);
                globes++;
                Assert.That((g.position - head).magnitude, Is.LessThan(1e-4f), "globe " + i + " centred on its lamp head");
                Assert.Greater(Vector3.Dot(g.forward, (cam - head).normalized), 0.9999f, "globe " + i + " pole faces the camera");

                var mr = g.GetComponent<MeshRenderer>();
                Material m = mr.sharedMaterial;
                Assert.AreEqual(0f, m.GetFloat("_CardMode"), "gradient card mode");
                Assert.That(m.GetFloat(ShaderIds.EmissionStrength), Is.EqualTo(AbyssPalette.AMBER_EMISSIVE).Within(1e-4f), "core = M_Lamp peak");
                Color top = m.GetColor(ShaderIds.CardTop).linear, bottom = m.GetColor(ShaderIds.CardBottom).linear;
                Assert.Greater(Luma(top), 4f * Luma(bottom), "core much brighter than the limb");
                Assert.Greater(bottom.r, 2f * bottom.g, "amber limb");

                Mesh mesh = g.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.bounds.extents.x, Is.EqualTo(Beat03_Ledge.LAMP_GLOBE_R).Within(1e-3f), "globe radius");
                Vector3[] v = mesh.vertices;
                var uv = new List<Vector2>();
                mesh.GetUVs(0, uv);
                int equator = 0;
                for (int k = 0; k < v.Length; k++)
                {
                    float c = v[k].z / Beat03_Ledge.LAMP_GLOBE_R;
                    float w = uv[k].y;
                    float expect = Beat03_Ledge.GlobeCoreWeight(c);
                    Assert.That(w, Is.EqualTo(expect).Within(1e-4f), "core weight at cos " + c);
                    float rho = Mathf.Sqrt(Mathf.Max(0f, 1f - c * c));
                    if (c > 0f && rho >= 0.3f) Assert.LessOrEqual(w, Beat03_Ledge.GLOBE_MID_WEIGHT + 1e-4f, "small white-hot core (D4), rho " + rho);
                    if (Mathf.Abs(c) < 1e-4f) equator++;
                }
                Assert.GreaterOrEqual(equator, Beat03_Ledge.GLOBE_LON, "silhouette ring vertices");
                // monotonic from the pole to the limb, 1 at the pole
                Assert.That(Beat03_Ledge.GlobeCoreWeight(1f), Is.EqualTo(1f).Within(1e-5f), "core weight 1 at the pole");
                float prev = 1f;
                for (int a = 1; a <= 90; a++)
                {
                    float wa = Beat03_Ledge.GlobeCoreWeight(Mathf.Cos(a * Mathf.Deg2Rad));
                    Assert.LessOrEqual(wa, prev + 1e-6f, "monotonic at " + a + " degrees");
                    prev = wa;
                }
                Assert.That(prev, Is.EqualTo(0f).Within(1e-5f), "0 at the limb");
            }
            Assert.AreEqual(b.LampCount, globes);
            int halos = 0;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("LampHalo", StringComparison.Ordinal)) halos++;
            Assert.AreEqual(globes, halos, "one halo per globe");
        }

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// KS3 (14.5.3): the amber lamp at alpha 2.2 is in frame, right of centre, about 15 m away. It stands at least 4 degrees
        /// clear of the C1 silhouette, so it reads against the void instead of hard against the column edge.
        /// </summary>
        [Test]
        public void Ks3LampStandsClearOfTheColumnSilhouette()
        {
            Beat03_Ledge b = Build(BeatTier.Full);
            Assert.IsTrue(b.TryGetLampHead(2, out Vector3 head), "lamp alpha 2.2");
            KeyShot ks = Array.Find(KeyShots.All, k => k.Tag == "KS3_column_up");
            Assert.AreEqual("KS3_column_up", ks.Tag);
            Vector3 v = head - ks.Eye;
            Assert.That(v.magnitude, Is.InRange(14f, 16.5f), "lamp distance from the KS3 eye");

            var toLamp = new Vector2(v.x, v.z);
            var toAxis = new Vector2(AbyssConst.ARC_CENTRE.x - ks.Eye.x, AbyssConst.ARC_CENTRE.y - ks.Eye.z);
            float silhouette = Mathf.Asin(AbyssConst.COLUMNS[0].z / toAxis.magnitude) * Mathf.Rad2Deg;
            float clear = Vector2.Angle(toAxis, toLamp) - silhouette;
            Assert.GreaterOrEqual(clear, 4f, "lamp 2.2 only " + clear + " degrees clear of the C1 silhouette in KS3");

            Vector3 local = Quaternion.Inverse(ks.Rotation) * v;
            Assert.Greater(local.z, 0f, "lamp in front of the KS3 camera");
            float tanV = Mathf.Tan(0.5f * ks.Fov * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
            float right = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            Assert.That(right, Is.InRange(6f, 20f), "lamp 2.2 right of the KS3 centre");
            Assert.LessOrEqual(Mathf.Abs(local.x / local.z), tanH, "lamp inside the frame horizontally");
            Assert.LessOrEqual(Mathf.Abs(local.y / local.z), tanV, "lamp inside the frame vertically");
        }

        /// <summary>
        /// The KS3 cold spot grades the C1 face upward (14.5.3), evaluated with URP's punctual-light model on the face line where
        /// its beam axis meets the column: 1 / d^2 with the smooth range window, the squared linear falloff between the inner
        /// and outer cone, and Lambert. It keeps the foot dark (E below 0.02 up to 3 m above the floor: the 2 m hatch is
        /// unlit), rises monotonically to a highlight that does not clip (E 1.2-3) 5-10 m above the floor, i.e. just above the
        /// y = 0 band where KS3 sees the pour lifts, and falls off above the KS3 frame (20 m). R1-30: the penumbra is wide
        /// (inner cone at least 20 degrees, outer 55-70) so the face grades continuously instead of showing a hard pool edge:
        /// half the peak is still reached 3 m above the peak, and the face 0.12 rad (2.6 m) to either side of the beam line
        /// still gets a quarter of the peak at the peak height. (The face seen from the bridge: Beat02Tests.)
        /// </summary>
        [Test]
        public void ColdSpotGradesTheKs3FaceUpward()
        {
            Beat03_Ledge b = Build(BeatTier.Full);
            float R = AbyssConst.COLUMNS[0].z;
            int spots = 0;
            foreach (Light l in b.GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Spot || l.name.StartsWith("LedgeAmber", StringComparison.Ordinal)) continue;
                spots++;
                Assert.GreaterOrEqual(l.innerSpotAngle, 20f, l.name + ": soft core");
                Assert.That(l.spotAngle, Is.InRange(55f, 70f), l.name + ": wide penumbra");
                Assert.AreEqual(90f, l.range, l.name + ": 14.2 range");
                double alpha = AimAlpha(l, R);
                float floor = Beat03_Ledge.TopY(alpha);
                Vector3 n = Beat03_Ledge.Ring(alpha, 1f, 0f) - Beat03_Ledge.Ring(alpha, 0f, 0f);
                const float step = 0.5f;
                var e = new float[141];
                int peak = 0;
                for (int k = 0; k < e.Length; k++)
                {
                    e[k] = SpotIlluminance(l, Beat03_Ledge.Ring(alpha, R, floor + k * step), n);
                    if (e[k] > e[peak]) peak = k;
                }
                for (int k = 0; k * step <= 3f; k++) Assert.Less(e[k], 0.02f, l.name + ": the foot must stay dark, E at " + k * step + " m");
                for (int k = 1; k <= peak; k++)
                    Assert.GreaterOrEqual(e[k], e[k - 1] - 1e-5f, l.name + ": the face must brighten upward to the peak (" + k * step + " m)");
                Assert.That(e[peak], Is.InRange(1.2f, 3f), l.name + ": peak face illuminance");
                Assert.That(peak * step, Is.InRange(5f, 10f), l.name + ": KS3 highlight above the band");
                Assert.Less(e[40], 0.5f * e[peak], l.name + ": falls off above the KS3 frame (20 m)");
                Assert.GreaterOrEqual(e[peak + 6], 0.5f * e[peak], l.name + ": soft fall above the highlight");
                float hp = floor + peak * step;
                for (int s = -1; s <= 1; s += 2)
                {
                    double a2 = alpha + s * 0.12;
                    Vector3 n2 = Beat03_Ledge.Ring(a2, 1f, 0f) - Beat03_Ledge.Ring(a2, 0f, 0f);
                    float side = SpotIlluminance(l, Beat03_Ledge.Ring(a2, R, hp), n2);
                    Assert.GreaterOrEqual(side, 0.25f * e[peak], l.name + ": broad pool, side " + s);
                }
            }
            Assert.AreEqual(1, spots, "one cold spot on the ledge");
        }

        /// <summary>Alpha where the horizontal projection of the light's beam axis meets the column circle of radius r.</summary>
        static double AimAlpha(Light l, float r)
        {
            Vector3 p = l.transform.position, f = l.transform.forward;
            var o = new Vector2(p.x - AbyssConst.ARC_CENTRE.x, p.z - AbyssConst.ARC_CENTRE.y);
            var d = new Vector2(f.x, f.z);
            if (d.sqrMagnitude < 1e-8f) return Beat03_Ledge.AlphaOf(p.x, p.z);
            d.Normalize();
            float bq = Vector2.Dot(o, d), c = o.sqrMagnitude - r * r, disc = bq * bq - c;
            float t = disc >= 0f ? -bq - Mathf.Sqrt(disc) : -bq;
            Vector2 h = o + d * Mathf.Max(0f, t);
            return Beat03_Ledge.AlphaOf(h.x + AbyssConst.ARC_CENTRE.x, h.y + AbyssConst.ARC_CENTRE.y);
        }

        /// <summary>URP punctual light (RealtimeLights.hlsl): intensity / d^2, smooth range window, spot angle falloff, Lambert.</summary>
        static float SpotIlluminance(Light l, Vector3 x, Vector3 normal)
        {
            Vector3 toX = x - l.transform.position;
            float d2 = toX.sqrMagnitude;
            Vector3 dir = toX / Mathf.Sqrt(d2);
            float cosOuter = Mathf.Cos(0.5f * l.spotAngle * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(0.5f * l.innerSpotAngle * Mathf.Deg2Rad);
            float t = Mathf.Clamp01((Vector3.Dot(l.transform.forward, dir) - cosOuter) / Mathf.Max(cosInner - cosOuter, 1e-4f));
            float f = d2 / (l.range * l.range);
            float window = Mathf.Clamp01(1f - f * f);
            return l.intensity * t * t * window * window * Mathf.Max(0f, -Vector3.Dot(dir, normal)) / d2;
        }

        /// <summary>
        /// Distance from p to C1 with its bands, in the (radius, y) half plane. The bands are taken as un-chamfered rectangles,
        /// which slightly under-estimates the distance (conservative).
        /// </summary>
        static float ColumnDistance(Vector3 p)
        {
            float R = AbyssConst.COLUMNS[0].z, r = Radius(p);
            float hb = AbyssConst.COLUMN_BAND_H * 0.5f, sp = AbyssConst.COLUMN_BAND_SPACING;
            float best = r - R;
            float k0 = Mathf.Round((AbyssConst.COLUMN_TOP_Y - p.y) / sp);
            for (float k = k0 - 1f; k <= k0 + 1f; k += 1f)
            {
                float yc = AbyssConst.COLUMN_TOP_Y - sp * k;
                float dr = Mathf.Max(0f, r - (R + AbyssConst.COLUMN_BAND_PROTRUSION));
                float dy = Mathf.Max(0f, Mathf.Abs(p.y - yc) - hb);
                best = Mathf.Min(best, Mathf.Sqrt(dr * dr + dy * dy));
            }
            return best;
        }

        [Test]
        public void HatchSitsAtTheSpecifiedPosition()
        {
            Beat03_Ledge b = Build(BeatTier.Full);
            MeshRenderer leaf = null;
            foreach (MeshRenderer mr in b.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name == "HatchLeaf") leaf = mr;
            Assert.IsNotNull(leaf, "no HatchLeaf");
            Bounds lb = leaf.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 p = AbyssConst.HATCH_POS;
            Assert.LessOrEqual(new Vector2(lb.center.x - p.x, lb.center.z - p.z).magnitude, 0.1f, "hatch centre (x, z)");
            Assert.That(lb.min.y, Is.EqualTo(p.y).Within(0.05f), "hatch bottom at the ledge floor");
            Assert.That(Beat03_Ledge.AlphaOf(lb.center.x, lb.center.z), Is.EqualTo(AbyssConst.HATCH_ALPHA).Within(0.002), "hatch alpha");
            float r = Radius(lb.center);
            Assert.That(r, Is.InRange(AbyssConst.LEDGE_R_IN, AbyssConst.LEDGE_R_IN + 0.04f), "the leaf sits flush on the column face");
        }

        [Test, TestCaseSource(nameof(Tiers))]
        public void KeyShotSubjectsArePresent(BeatTier tier)
        {
            Beat03_Ledge b = Build(tier);
            var subjects = new List<KeyShotSubject>();
            b.CollectSubjects("KS3_column_up", subjects);
            bool hatch = false, lamp = false;
            foreach (KeyShotSubject s in subjects)
            {
                Assert.Greater(s.WorldBounds.extents.magnitude, 0f, s.Name + " has empty bounds");
                if (s.Name == "hatch") hatch = true;
                if (s.Name == "amber lamp alpha 2.2") lamp = true;
            }
            Assert.AreEqual(tier != BeatTier.Simplified, hatch, tier + ": hatch subject");
            Assert.IsTrue(lamp, tier + ": lamp alpha 2.2 subject");
            Assert.GreaterOrEqual(subjects.Count, 3);
        }

        [Test]
        public void NearDetailSwapsByCameraDistance()
        {
            if (QaHooks.TierOverride >= 0f) Assert.Inconclusive("QA tier override active");
            Beat03_Ledge b = Build(BeatTier.Full);
            DetailTier t = b.Detail;
            Assert.IsNotNull(t);
            t.Update(new Vector3(25f, -3.3f, 244f));                     // KS3 pose
            Assert.AreEqual(1f, t.Coverage, "near detail at KS3");
            t.Update(new Vector3(0f, -1.3f, AbyssConst.BRIDGE_Z0 - 60f));   // far beyond the near tier
            Assert.AreEqual(0f, t.Coverage, "far from the ledge the near detail is hidden");
        }

        // ---------------------------------------------------------------- helpers

        static float Radius(Vector3 p)
        {
            Vector2 c = AbyssConst.ARC_CENTRE;
            return new Vector2(p.x - c.x, p.z - c.y).magnitude;
        }

        static Bounds WorldAabb(BoxCollider bc)
        {
            Transform t = bc.transform;
            Vector3 h = bc.size * 0.5f;
            var b = new Bounds(t.TransformPoint(bc.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z);
                b.Encapsulate(t.TransformPoint(bc.center + corner));
            }
            return b;
        }
    }
}
