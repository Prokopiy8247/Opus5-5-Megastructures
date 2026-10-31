using System;
using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 4 acceptance (14.5.4, 14.10): bore 12.0 +/-0.05, stroke 24.0 +/-0.1 sampled on the posed crossheads, cycle
    /// 120.0 +/-0.05 s, one dead centre every 15.00 +/-0.05 s (eight per cycle, never two at once), catwalk 1.50 +/-0.01,
    /// rod 32 m and pin radius 12 m on the posed parts, rotation sense per type, and the bounds test: no wheel, rod, crosshead,
    /// cylinder, post or rail AABB intersects another over a full revolution (mechanical joints of one bank excluded), the
    /// swinging rods clear the guide-frame legs, every full-tier mesh is valid, the visible triangle budget (1.2 M) holds,
    /// every beat-4 row of 14.12 is measured within +/-0.5 % (angles 0.1 degree, times 0.05 s) in all three tiers, the KS4
    /// subjects name every described object and pass the composition lint at the arbitrated capture pose (the tagged outer-right bank is hidden behind cylinder B),
    /// and the 14.6 activation (interior from d 250, portal card gone at 285, exit proxy gone at 420, interior swapped to the
    /// dark cards at ride second 40). The full tier is built once (EditMode, generated MaterialLibrary); objects and meshes
    /// are destroyed afterwards.
    /// </summary>
    public sealed class Beat04Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";

        MaterialLibrary _lib;
        Beat04_Hall _full;
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        bool _registryTouched;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (_lib == null) Assert.Inconclusive("MaterialLibrary missing at " + LibraryPath + " (run MaterialBuilder.BuildAll)");
            if (_lib.materials == null || _lib.materials.Length < (int)MatId.Count) Assert.Inconclusive("MaterialLibrary incomplete");
            for (int i = 0; i < (int)MatId.Count; i++)
                if (_lib.materials[i] == null) Assert.Inconclusive("MaterialLibrary lacks base material " + (MatId)i);
            _full = Build(BeatTier.Full, true);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) UnityEngine.Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
            if (_registryTouched) InstancedRegistry.Clear();
        }

        Beat04_Hall Build(BeatTier tier, bool editMode)
        {
            var root = new GameObject("Beat04Test_" + tier);
            _roots.Add(root);
            var b = root.AddComponent<Beat04_Hall>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            var ctx = new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = editMode };
            if (!editMode) _registryTouched = true;
            b.Build(ctx);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        ScaleReport _fullReport;

        /// <summary>One ReportScale per beat object (it samples a full revolution); the full tier's report is cached.</summary>
        double Report(BeatBase b, string key)
        {
            ScaleReport r;
            if (b == _full && _fullReport != null) r = _fullReport;
            else
            {
                r = new ScaleReport();
                b.ReportScale(r);
                if (b == _full) _fullReport = r;
            }
            Assert.IsTrue(r.Values.TryGetValue(key, out double v), "beat 4 does not report " + key);
            return v;
        }

        static IEnumerable<BeatTier> BuiltTiers() { yield return BeatTier.Full; yield return BeatTier.Simplified; }
        static IEnumerable<BeatTier> AllTiers() { yield return BeatTier.Full; yield return BeatTier.Simplified; yield return BeatTier.Graybox; }

        // ---------------------------------------------------------------- 14.12 rows of beat 4 (14.10: dimensions +/-0.5 %, angles +/-0.1 degree, times +/-0.05 s)

        struct Row
        {
            public string Key; public double Value, Tol;
            public Row(string key, double value, double tol) { Key = key; Value = value; Tol = tol; }
        }

        static Row Rel(string key, double value) => new Row(key, value, Math.Abs(value) * 0.005);

        static List<Row> Rows1412(Beat04_Hall b)
        {
            var rows = new List<Row>
            {
                Rel("hall.x0", AbyssConst.HALL_X0), Rel("hall.x1", AbyssConst.HALL_X1),
                Rel("hall.z0", AbyssConst.HALL_Z0), Rel("hall.z1", AbyssConst.HALL_Z1),
                Rel("hall.length", AbyssConst.HALL_X0 - AbyssConst.HALL_X1), Rel("hall.width", AbyssConst.HALL_Z1 - AbyssConst.HALL_Z0),
                Rel("hall.floor.y", AbyssConst.HALL_FLOOR_Y), Rel("hall.roof.y", AbyssConst.HALL_ROOF_Y),
                Rel("hall.hull.bottom", AbyssConst.HALL_HULL_BOTTOM_Y), Rel("hall.hull.height", AbyssConst.HALL_ROOF_Y - AbyssConst.HALL_HULL_BOTTOM_Y),
                Rel("hall.chamfer.x", AbyssConst.HALL_CHAMFER), Rel("hall.chamfer.y", AbyssConst.HALL_CHAMFER),
                Rel("hall.opening.width", AbyssConst.HALL_OPENING_W), Rel("hall.opening.height", AbyssConst.HALL_OPENING_H),
                Rel("hall.catwalk.width", AbyssConst.CATWALK_W), Rel("hall.catwalk.y", AbyssConst.CATWALK_Y), Rel("hall.rail.height", AbyssConst.RAIL_H),
                Rel("piston.bore", AbyssConst.PISTON_BORE), Rel("piston.rod.diameter", AbyssConst.PISTON_ROD),
                Rel("piston.rod.length", 27.0),   // 14.5.4: 27 m from the crosshead front to the piston
                Rel("piston.stroke", AbyssConst.PISTON_STROKE),
                Rel("crank.radius", AbyssConst.CRANK_R), Rel("crank.rod.length", AbyssConst.CRANK_L), Rel("crank.omega", AbyssConst.CRANK_OMEGA),
                Rel("wheel.diameter", AbyssConst.WHEEL_D),
                new Row("crank.cycle", AbyssConst.CRANK_PERIOD, 0.05),
                new Row("crank.stroke.time.min", AbyssConst.STROKE_TIME, 0.05), new Row("crank.stroke.time.max", AbyssConst.STROKE_TIME, 0.05),
                new Row("crank.deadcentre.interval.min", AbyssConst.DEAD_CENTRE_INTERVAL, 0.05),
                new Row("crank.deadcentre.interval.max", AbyssConst.DEAD_CENTRE_INTERVAL, 0.05),
            };
            for (int k = 0; k < 4; k++)
            {
                if (!b.IsBankBuilt(k)) continue;
                rows.Add(Rel("crank.u." + k, AbyssConst.BANK_TYPE_A[k] ? AbyssConst.CRANK_U_A : AbyssConst.CRANK_U_B));
                rows.Add(Rel("bank.v." + k, AbyssConst.BANK_V[k]));
                rows.Add(new Row("crank.phase." + k, AbyssConst.BANK_PHASE_DEG[k], 0.1));
            }
            return rows;
        }

        [Test, TestCaseSource(nameof(AllTiers))]
        public void Rows1412_AreMeasuredInEveryTier(BeatTier tier)
        {
            Beat04_Hall b = tier == BeatTier.Full ? _full : Build(tier, true);
            ScaleReport r;
            if (b == _full) { Report(_full, "piston.bore"); r = _fullReport; }
            else { r = new ScaleReport(); b.ReportScale(r); }
            foreach (Row row in Rows1412(b))
            {
                Assert.IsTrue(r.Values.TryGetValue(row.Key, out double v), tier + " does not report " + row.Key);
                Assert.IsFalse(double.IsNaN(v) || double.IsInfinity(v), tier + " " + row.Key + " is not finite");
                Assert.That(v, Is.EqualTo(row.Value).Within(row.Tol), tier + " " + row.Key);
            }
            for (int k = 0; k < 4; k++)
                if (!b.IsBankBuilt(k)) Assert.IsFalse(r.Values.ContainsKey("crank.u." + k), tier + " reports bank " + k + ", which it does not build");
        }

        // ---------------------------------------------------------------- dimensions

        [Test]
        public void Bore_Is12()
        {
            Assert.That(Report(_full, "piston.bore"), Is.EqualTo(AbyssConst.PISTON_BORE).Within(0.05), "bore");
            Assert.That(Report(_full, "piston.cylinder.length"), Is.EqualTo(28.0).Within(0.05), "cylinder length (O12 x 28)");
            Assert.That(Report(_full, "wheel.diameter"), Is.EqualTo(AbyssConst.WHEEL_D).Within(0.05), "wheel O30");
        }

        [Test]
        public void Catwalk_Is150()
        {
            Assert.That(Report(_full, "hall.catwalk.width"), Is.EqualTo(AbyssConst.CATWALK_W).Within(0.01), "catwalk width");
            Assert.That(Report(_full, "hall.catwalk.length"), Is.EqualTo(AbyssConst.HALL_X0 - AbyssConst.HALL_X1).Within(0.05), "catwalk length");
            Assert.That(Report(_full, "hall.rail.height"), Is.EqualTo(AbyssConst.RAIL_H).Within(0.01), "rail height");
            Assert.That(Report(_full, "hall.support.bottom"), Is.EqualTo(AbyssConst.HALL_FLOOR_Y).Within(0.06), "posts stand on the floor");
            Assert.That(Report(_full, "hall.support.top"), Is.EqualTo(AbyssConst.CATWALK_Y).Within(0.35), "posts carry the deck at y -7");
        }

        [Test]
        public void Hull_MatchesTheSpec()
        {
            Assert.That(Report(_full, "hall.length"), Is.EqualTo(110.0).Within(0.01));
            Assert.That(Report(_full, "hall.width"), Is.EqualTo(140.0).Within(0.01));
            Assert.That(Report(_full, "hall.hull.height"), Is.EqualTo(AbyssConst.HALL_ROOF_Y - AbyssConst.HALL_HULL_BOTTOM_Y).Within(0.01));
            Assert.That(Report(_full, "hall.opening.width"), Is.EqualTo(AbyssConst.HALL_OPENING_W).Within(0.01));
            Assert.That(Report(_full, "hall.opening.height"), Is.EqualTo(AbyssConst.HALL_OPENING_H).Within(0.01));
            Assert.That(Report(_full, "hall.exit.width"), Is.EqualTo(AbyssConst.HALL_OPENING_W).Within(0.01));
            Assert.That(Report(_full, "hall.exit.height"), Is.EqualTo(AbyssConst.HALL_OPENING_H).Within(0.01));
            Assert.AreEqual(12.0, Report(_full, "hall.trusses"), "12 roof trusses");
            Assert.AreEqual(8.0, Report(_full, "hall.spot.count"), "8 spot cans");
            Assert.AreEqual(6.0, Report(_full, "hall.hazard.count"), "6 amber hazard points");
            Assert.AreEqual(24.0, Report(_full, "cylinder.flange.bolts"), "24 bolts per flange");
        }

        [Test, TestCaseSource(nameof(BuiltTiers))]
        public void SimplifiedAndFullBuildTheirAssemblies(BeatTier tier)
        {
            Beat04_Hall b = tier == BeatTier.Full ? _full : Build(tier, true);
            int expected = tier == BeatTier.Full ? 4 : 2;
            int built = 0;
            for (int k = 0; k < 4; k++) if (b.IsBankBuilt(k)) built++;
            Assert.AreEqual(expected, built, tier + " assemblies");
            Assert.AreEqual(tier == BeatTier.Full ? 12.0 : 6.0, Report(b, "hall.trusses"), tier + " trusses (every 10 m / every 20 m)");
        }

        // ---------------------------------------------------------------- kinematics

        [Test]
        public void Stroke_SampledOnThePosedCrossheads_Is24()
        {
            for (int k = 0; k < 4; k++)
            {
                Vector3 c = Beat04_Hall.CrankCentre(k);
                float ax = Beat04_Hall.SliderAxisX(k);
                double dMin = double.MaxValue, dMax = double.MinValue, rodErr = 0, pinErr = 0, lateral = 0;
                for (int i = 0; i < 2400; i++)
                {
                    double t = 1000.0 + 0.05 * i;
                    _full.PoseAt(t);
                    Vector3 ch = _full.CrossheadPosition(k), pin = _full.PinPosition(k);
                    double d = (ch.x - c.x) * ax;
                    dMin = Math.Min(dMin, d); dMax = Math.Max(dMax, d);
                    rodErr = Math.Max(rodErr, Math.Abs((ch - pin).magnitude - AbyssConst.CRANK_L));
                    pinErr = Math.Max(pinErr, Math.Abs(new Vector2(pin.x - c.x, pin.z - c.z).magnitude - AbyssConst.CRANK_R));
                    lateral = Math.Max(lateral, Math.Abs(ch.z - c.z));
                }
                Assert.That(dMax - dMin, Is.EqualTo(AbyssConst.PISTON_STROKE).Within(0.1), "bank " + k + " stroke");
                Assert.That(dMin, Is.EqualTo(AbyssConst.CRANK_L - AbyssConst.CRANK_R).Within(0.05), "bank " + k + " inner dead centre 20 m");
                Assert.That(dMax, Is.EqualTo(AbyssConst.CRANK_L + AbyssConst.CRANK_R).Within(0.05), "bank " + k + " outer dead centre 44 m");
                Assert.Less(rodErr, 1e-3, "bank " + k + " rod length 32 m");
                Assert.Less(pinErr, 1e-3, "bank " + k + " pin radius 12 m");
                Assert.Less(lateral, 1e-4, "bank " + k + " crosshead stays on the slider axis");
            }
            Assert.That(Report(_full, "piston.stroke"), Is.EqualTo(AbyssConst.PISTON_STROKE).Within(0.1), "reported stroke");
        }

        [Test]
        public void Cycle_Is120Seconds()
        {
            Beat04_Hall.MeasureClock(out double cycle, out _, out _, out _);
            Assert.That(cycle, Is.EqualTo(AbyssConst.CRANK_PERIOD).Within(0.05), "measured cycle");
            // the posed machine repeats after 120 s (including after an hour of play) and not after 60 s
            for (int k = 0; k < 4; k++)
            {
                double worst = 0, half = 0;
                for (int i = 0; i < 50; i++)
                {
                    double t = 300.0 + 2.37 * i;
                    _full.PoseAt(t);
                    Vector3 a = _full.CrossheadPosition(k);
                    _full.PoseAt(t + AbyssConst.CRANK_PERIOD + 3600.0);
                    worst = Math.Max(worst, (_full.CrossheadPosition(k) - a).magnitude);
                    _full.PoseAt(t + 0.5 * AbyssConst.CRANK_PERIOD);
                    half = Math.Max(half, (_full.CrossheadPosition(k) - a).magnitude);
                }
                Assert.Less(worst, 1e-3, "bank " + k + " repeats after 120 s");
                Assert.Greater(half, 1.0, "bank " + k + " must not repeat after 60 s");
            }
            // theta_IL(t) = 60 + 3 (t - 219.69) degrees: IL at 60 degrees at the portal crossing
            Assert.That(Beat04_Hall.BankThetaDeg(0, AbyssConst.PORTAL_T), Is.EqualTo(60.0).Within(1e-6));
            Assert.That(Beat04_Hall.BankThetaDeg(1, AbyssConst.PORTAL_T), Is.EqualTo(105.0).Within(1e-6));
            Assert.That(Beat04_Hall.BankThetaDeg(0, AbyssConst.PORTAL_T + 10.0), Is.EqualTo(90.0).Within(1e-6));
        }

        [Test]
        public void DeadCentres_OneEvery15Seconds()
        {
            Beat04_Hall.MeasureClock(out _, out double dcMin, out double dcMax, out int perCycle);
            Assert.That(dcMin, Is.EqualTo(AbyssConst.DEAD_CENTRE_INTERVAL).Within(0.05), "shortest gap between dead centres");
            Assert.That(dcMax, Is.EqualTo(AbyssConst.DEAD_CENTRE_INTERVAL).Within(0.05), "longest gap between dead centres");
            Assert.AreEqual(8, perCycle, "two dead centres per piston per 120 s cycle");
            // bank k reaches its dead centres phi_k / 3 s before bank 0
            for (int k = 1; k < 4; k++)
            {
                double dt = AbyssConst.BANK_PHASE_DEG[k] / 3.0;
                for (int i = 0; i < 20; i++)
                {
                    double t = 500.0 + 3.1 * i;
                    Assert.That(Beat04_Hall.SlideDistance(k, t - dt), Is.EqualTo(Beat04_Hall.SlideDistance(0, t)).Within(1e-9), "phase of bank " + k);
                }
            }
        }

        [Test]
        public void Wheels_TurnTheRightWay()
        {
            for (int k = 0; k < 4; k++)
            {
                Vector3 c = Beat04_Hall.CrankCentre(k);
                _full.PoseAt(1000.0);
                Vector3 p0 = _full.PinPosition(k) - c;
                _full.PoseAt(1001.0);
                Vector3 p1 = _full.PinPosition(k) - c;
                float cross = p0.x * p1.z - p0.z * p1.x;   // > 0: counter-clockwise seen from above (+X right, +Z up)
                if (AbyssConst.BANK_TYPE_A[k]) Assert.Less(cross, 0f, "type A turns clockwise seen from above (bank " + k + ")");
                else Assert.Greater(cross, 0f, "type B turns counter-clockwise seen from above (bank " + k + ")");
            }
        }

        // ---------------------------------------------------------------- bounds test (14.10)

        [Test]
        public void NoWheelRodCrossheadCylinderPostOrRailIntersects_OverAFullRevolution()
        {
            var parts = new List<Beat04_Hall.HallPart>(32);
            _full.PoseAt(1000.0);
            _full.CollectParts(parts);
            foreach (string kind in new[] { "wheel", "rod", "crosshead", "cylinder", "pistonrod", "shaft" })
                for (int k = 0; k < 4; k++)
                    Assert.IsTrue(parts.Exists(p => p.Kind == kind && p.Bank == k), "missing part " + kind + " of bank " + k);
            Assert.IsTrue(parts.Exists(p => p.Kind == "post"), "missing catwalk posts");
            Assert.IsTrue(parts.Exists(p => p.Kind == "rail"), "missing catwalk rails");

            int steps = 240;
            for (int s = 0; s < steps; s++)
            {
                double t = AbyssConst.PORTAL_T + AbyssConst.CRANK_PERIOD * s / steps;
                _full.PoseAt(t);
                _full.CollectParts(parts);
                for (int i = 0; i < parts.Count; i++)
                    for (int j = i + 1; j < parts.Count; j++)
                    {
                        if (Beat04_Hall.IsJoinedPair(parts[i], parts[j])) continue;
                        if (parts[i].World.Intersects(parts[j].World))
                            Assert.Fail(string.Format("t = {0:F2}: {1}[{2}] {3} intersects {4}[{5}] {6}", t, parts[i].Kind, parts[i].Bank, parts[i].World,
                                parts[j].Kind, parts[j].Bank, parts[j].World));
                    }
            }
            Assert.AreEqual(0.0, Report(_full, "hall.parts.overlaps"), "reported overlaps");
        }

        [Test]
        public void Rods_ClearTheGuideFrameLegs()
        {
            float need = Beat04_Hall.GuideFrameLegHalf * Mathf.Sqrt(2f) + Beat04_Hall.RodHalfWidthMax + 0.2f;
            for (int k = 0; k < 4; k++)
            {
                Vector3 c = Beat04_Hall.CrankCentre(k);
                float ax = Beat04_Hall.SliderAxisX(k);
                float worst = float.MaxValue;
                for (int i = 0; i < 1440; i++)
                {
                    _full.PoseAt(1000.0 + 120.0 * i / 1440.0);
                    Vector3 pin = _full.PinPosition(k), ch = _full.CrossheadPosition(k);
                    for (int f = 0; f < Beat04_Hall.GuideFrameCount; f++)
                        for (int l = -1; l <= 1; l += 2)
                        {
                            var leg = new Vector2(c.x + ax * Beat04_Hall.GuideFrameStation(f), c.z + l * Beat04_Hall.GuideFrameLegOffset);
                            worst = Mathf.Min(worst, SegmentDistance(leg, new Vector2(pin.x, pin.z), new Vector2(ch.x, ch.z)));
                        }
                }
                Assert.Greater(worst, need, "bank " + k + ": the rod passes too close to a guide-frame leg");
            }
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
            return (a + ab * t - p).magnitude;
        }

        // ---------------------------------------------------------------- meshes and budget

        [Test]
        public void EveryFullTierMeshIsValid_AndTheTriangleBudgetHolds()
        {
            var list = new List<Mesh>();
            _full.CollectMeshes(list);
            Assert.Greater(list.Count, 20, "full tier meshes");
            long tris = 0;
            foreach (Mesh m in list)
            {
                Assert.IsNotNull(m);
                MeshReport r = MeshValidation.Validate(m);
                Assert.IsTrue(r.Ok, r.Describe());
                tris += r.TriangleCount;
            }
            Assert.LessOrEqual(tris, 1200000, "visible triangle maximum of beat 4 (14.7.8)");
        }

        // ---------------------------------------------------------------- KS4 composition lint (14.10)

        static readonly float[] Aspects = { 16f / 9f, 16f / 10f, 64f / 27f };

        /// <summary>
        /// Every object the KS4 description names is a subject (the outer banks only where they are built). The KS4 pose of
        /// record is the capture pose in KeyShots (arbitrated by the qa owner away from the 14.10 pose, critique R1-20: u -2,
        /// yaw 274, pitch +8). There every untagged subject passes the harness's frame rule (box centre in 5-95 %, or for a
        /// subject larger than the frame the centre of its in-frame part) at the three capture aspects over a whole revolution.
        /// The tagged known conflict (the outer-right bank) must really be unshowable, so a stale tag shows: either it fails
        /// the frame rule, or at least 60 % of the sight lines from the eye to its box (full tier >= 0.7, graybox boxes 0.67) pass through the inner-right cylinder's
        /// box first (the depth-buffer occlusion test of the player capture cannot run in EditMode).
        /// </summary>
        [Test, TestCaseSource(nameof(AllTiers))]
        public void Ks4Subjects_NameEveryObject_AndPassTheLint(BeatTier tier)
        {
            Beat04_Hall b = tier == BeatTier.Full ? _full : Build(tier, true);
            KeyShot ks = KeyShots.All[KeyShots.IndexOf("KS4_hall_pistons")];
            Quaternion rot = ks.Rotation;
            string tag = Beat04_Hall.SpecConflictTag;
            var subjects = new List<KeyShotSubject>();
            for (int i = 0; i < 8; i++)
            {
                b.PoseAt(AbyssConst.PORTAL_T + 15.0 * i);
                subjects.Clear();
                b.CollectSubjects("KS4_hall_pistons", subjects);
                var names = new List<string>();
                Bounds cylB = default;
                foreach (KeyShotSubject s in subjects) if (s.Name.StartsWith("inner-right cylinder", StringComparison.Ordinal)) cylB = s.WorldBounds;
                foreach (KeyShotSubject s in subjects)
                {
                    names.Add(s.Name);
                    Vector3 c = s.WorldBounds.center;
                    Assert.IsFalse(float.IsNaN(c.x + c.y + c.z) || float.IsInfinity(c.x + c.y + c.z), s.Name + " not finite");
                    Assert.Greater(s.WorldBounds.extents.magnitude, 0f, s.Name + " has empty bounds");
                    if (s.Name.EndsWith(tag, StringComparison.Ordinal))
                    {
                        CompositionLint.SubjectResult r = CompositionLint.Check(in s, ks.Eye, rot, ks.Fov, 16f / 9f, AbyssConst.CAMERA_NEAR);
                        float hidden = OccludedFraction(s.WorldBounds, cylB, ks.Eye, 6);
                        Assert.IsTrue(!r.FramePass || hidden >= 0.6f, s.Name + " is showable now (frame rule passes, " + hidden + " behind cylinder B): remove the spec-conflict tag");
                        continue;
                    }
                    foreach (float aspect in Aspects)
                    {
                        CompositionLint.SubjectResult r = CompositionLint.Check(in s, ks.Eye, rot, ks.Fov, aspect, AbyssConst.CAMERA_NEAR);
                        Assert.IsFalse(r.Behind, tier + " KS4/" + s.Name + " behind the camera");
                        Assert.IsTrue(r.FramePass, tier + " KS4/" + s.Name + " fails the lint at aspect " + aspect + ": centre (" + r.Cx + ", " + r.Cy
                                                   + "), in-frame centre (" + r.InCx + ", " + r.InCy + ")");
                    }
                }
                bool outer = b.IsBankBuilt(2) && b.IsBankBuilt(3);
                string[] expected =
                {
                    "catwalk rails and grating", "inner-right cylinder (B)", "inner-left cylinder (A)", "crosshead rods",
                    "left crank wheel", "trusses", "exit opening",
                };
                foreach (string n in expected) Assert.Contains(n, names, tier + " KS4 lacks the subject " + n);
                Assert.AreEqual(outer, names.Contains("outer-right bank" + tag), tier + " outer-right bank subject (known conflict)");
                Assert.AreEqual(outer, names.Contains("outer-left bank"), tier + " outer-left bank subject");
                Assert.AreEqual(expected.Length + (outer ? 2 : 0), subjects.Count, tier + " KS4 subject count");
            }
        }

        /// <summary>Share of sight lines from the eye to an n^3 grid of points in 'target' that cross 'occluder' before reaching the point.</summary>
        static float OccludedFraction(Bounds target, Bounds occluder, Vector3 eye, int n)
        {
            int all = 0, hidden = 0;
            Vector3 mn = target.min, mx = target.max;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                    {
                        var p = new Vector3(Mathf.Lerp(mn.x, mx.x, (i + 0.5f) / n), Mathf.Lerp(mn.y, mx.y, (j + 0.5f) / n), Mathf.Lerp(mn.z, mx.z, (k + 0.5f) / n));
                        all++;
                        Vector3 d = p - eye;
                        if (occluder.IntersectRay(new Ray(eye, d), out float t) && t < d.magnitude) hidden++;
                    }
            return all > 0 ? (float)hidden / all : 0f;
        }

        // ---------------------------------------------------------------- activation (14.6)

        [Test]
        public void Activation_FollowsTheTransitionTable()
        {
            Beat04_Hall b = Build(BeatTier.Full, false);
            Transform root = b.transform;
            GameObject interior = root.Find("Geometry/Interior").gameObject;
            MeshRenderer portal = root.Find("Geometry/Skeleton/PortalDarkCard").GetComponent<MeshRenderer>();
            MeshRenderer exitCard = root.Find("Geometry/Skeleton/ExitDarkCard").GetComponent<MeshRenderer>();
            MeshRenderer proxy = root.Find("Geometry/Interior/ExitProxyCard").GetComponent<MeshRenderer>();
            var f = new BeatFrame { Time = 1000.0, CameraPos = new Vector3(-60f, -5.3f, 269f) };

            f.E = 100f; b.Tick(in f);
            Assert.IsFalse(interior.activeSelf, "interior off on the bridge");
            Assert.IsTrue(portal.enabled && exitCard.enabled, "both openings closed by dark cards");

            f.E = 260f; b.Tick(in f);
            Assert.IsTrue(interior.activeSelf, "interior on from d 250");
            Assert.IsTrue(portal.enabled && !portal.HasPropertyBlock(), "portal card still closed at d 260");
            Assert.IsFalse(exitCard.enabled, "exit card off while the interior is on");
            Assert.IsTrue(proxy.enabled, "exit proxy closes the shaft view");

            f.E = 280f; b.Tick(in f);
            Assert.IsTrue(portal.enabled && portal.HasPropertyBlock(), "portal card dissolving over d 275-285");

            f.E = 300f; b.Tick(in f);
            Assert.IsFalse(portal.enabled, "portal open from d 285");
            Assert.IsTrue(proxy.enabled && !proxy.HasPropertyBlock(), "exit proxy fully shown before d 405");

            f.E = 412f; b.Tick(in f);
            Assert.IsTrue(proxy.enabled && proxy.HasPropertyBlock(), "exit proxy dissolving over d 405-420");

            f.E = 430f; b.Tick(in f);
            Assert.IsFalse(proxy.enabled, "exit proxy off from d 420");
            Assert.IsTrue(interior.activeSelf, "interior still on at the dock");

            f.E = AbyssConst.D_LIFT + 39f; b.Tick(in f);
            Assert.IsTrue(interior.activeSelf, "interior on until ride second 40");

            f.E = AbyssConst.D_LIFT + 41f; b.Tick(in f);
            Assert.IsFalse(interior.activeSelf, "interior swapped to the dark cards after ride second 40");
            Assert.IsTrue(portal.enabled && exitCard.enabled, "both openings closed again");

            // walking back into the ledge: hysteresis keeps the interior on until d 245
            f.E = 300f; b.Tick(in f);
            f.E = 247f; b.Tick(in f);
            Assert.IsTrue(interior.activeSelf, "hysteresis");
            f.E = 244f; b.Tick(in f);
            Assert.IsFalse(interior.activeSelf, "off below d 245");
        }

        // ---------------------------------------------------------------- lighting rig (14.2 row 4 + look-dev)

        /// <summary>
        /// Every light of the full tier is a local light (CONTRACTS 5: explicit range, no shadows, never directional) whose
        /// source lies inside the hull box (the opening reveals included) and is part of the interior, so it switches with it;
        /// the 8 cans keep the 14.2 range 60 and cone 70; the two daylight spills sit in their reveal and travel into the hall
        /// (portal toward -X, exit toward +X), so the inner face of their own wall and everything outside lie behind them; the
        /// exit haze shaft is a mode-2 BeamWedge with _AbBeamFade pinned to 1 (the global stays 0 until the ride).
        /// </summary>
        [Test]
        public void Lights_AreLocalInsideTheHull_AndTheSpillsPointIntoTheHall()
        {
            Transform interior = _full.transform.Find("Geometry/Interior");
            Assert.IsNotNull(interior, "interior group");
            Light[] lights = _full.GetComponentsInChildren<Light>(true);
            Assert.Greater(lights.Length, 0, "lights built");
            int cans = 0, spills = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                Assert.AreNotEqual(LightType.Directional, l.type, l.name + ": the Key is the only directional light");
                Assert.AreEqual(LightShadows.None, l.shadows, l.name + ": local lights cast no shadows");
                Assert.Greater(l.range, 0f, l.name + ": explicit range");
                Assert.IsTrue(l.transform.IsChildOf(interior), l.name + ": switches with the interior");
                Vector3 p = l.transform.position;
                Assert.That(p.x, Is.InRange(AbyssConst.HALL_X1, AbyssConst.HALL_X0), l.name + " x inside the hull");
                Assert.That(p.z, Is.InRange(AbyssConst.HALL_Z0, AbyssConst.HALL_Z1), l.name + " z inside the hull");
                Assert.That(p.y, Is.InRange(AbyssConst.HALL_FLOOR_Y, AbyssConst.HALL_ROOF_Y), l.name + " y inside the hull");
                if (l.name.StartsWith("SpotCan", StringComparison.Ordinal) && !l.name.StartsWith("SpotCanUp", StringComparison.Ordinal))
                {
                    cans++;
                    Assert.AreEqual(60f, l.range, 1e-4f, l.name + " range (14.2)");
                    Assert.AreEqual(70f, l.spotAngle, 1e-4f, l.name + " cone (14.2)");
                }
                if (l.name.StartsWith("Spill", StringComparison.Ordinal))
                {
                    spills++;
                    bool portal = l.name == "SpillPortal";
                    float wallMid = portal ? AbyssConst.HALL_X0 - 0.5f : AbyssConst.HALL_X1 + 0.5f;
                    Assert.AreEqual(wallMid, p.x, 1e-3f, l.name + " in the mid plane of its reveal");
                    Assert.Greater((portal ? -1f : 1f) * l.transform.forward.x, 0.95f, l.name + " travels into the hall");
                    Assert.Less(l.range, AbyssConst.HALL_X0 - AbyssConst.HALL_X1, l.name + " ends before the opposite wall");
                }
            }
            Assert.AreEqual(8, cans, "8 spot cans");
            Assert.AreEqual(2, spills, "portal and exit spill");

            Transform shaft = interior.Find("ExitSpillShaft");
            Assert.IsNotNull(shaft, "exit haze shaft");
            var r = shaft.GetComponent<MeshRenderer>();
            Assert.AreEqual(2f, r.sharedMaterial.GetFloat("_Recipe"), "BeamWedge mode 2 (rectangular prism)");
            Assert.IsTrue(r.HasPropertyBlock(), "per-renderer block");
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            Assert.AreEqual(1f, mpb.GetFloat("_AbBeamFade"), "shaft visible before the ride");
            Assert.Greater(shaft.forward.x, 0.99f, "prism extruded into the hall");
        }

        // ---------------------------------------------------------------- critique R1-20: frames do not wall off the machines

        /// <summary>
        /// Two portal frames per bank at the guide-bar ends. Between them, below the top tie, the catwalk-facing leg plane holds
        /// no geometry at all (no diagonals hiding the cylinders and wheels); the one X-brace lies in the outboard plane, which
        /// really faces away from the catwalk. The frames carry a darker paint than the machines (dark silhouettes, the lit
        /// cylinders and wheels read in front of them).
        /// </summary>
        [Test]
        public void GuideFrames_TwoPerBank_BracedOnlyAwayFromTheCatwalk()
        {
            Assert.AreEqual(2, Beat04_Hall.GuideFrameCount, "two portal frames per bank");
            float s0 = Beat04_Hall.GuideFrameStation(0), s1 = Beat04_Hall.GuideFrameStation(1), leg = Beat04_Hall.GuideFrameLegOffset;
            Transform machines = _full.transform.Find("Geometry/Interior/Machines");
            Assert.IsNotNull(machines, "machines group");
            for (int k = 0; k < 4; k++)
            {
                Transform bank = machines.Find("Bank" + k + (AbyssConst.BANK_TYPE_A[k] ? "A" : "B"));
                Assert.IsNotNull(bank, "bank " + k);
                int outSide = Beat04_Hall.GuideFrameOutboardSide(k);
                Vector3 legOut = bank.TransformPoint(new Vector3(s0, 0f, outSide * leg));
                Assert.Greater(Mathf.Abs(legOut.z - AbyssConst.W[8].z), Mathf.Abs(AbyssConst.BANK_V[k]), "bank " + k + ": the braced side faces away from the catwalk");
                Transform frames = bank.Find("GuideFrames");
                Mesh mesh = frames.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] v = mesh.vertices;   // bank frame: x along the slider, z lateral
                int[] tri = mesh.triangles;
                // members spanning between the frames (triangles longer than half the frame spacing along the slider axis)
                // below the top tie, in either leg plane
                int inner = 0, outer = 0;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = v[tri[i]], bb = v[tri[i + 1]], cc = v[tri[i + 2]];
                    float sMin = Mathf.Min(a.x, Mathf.Min(bb.x, cc.x)), sMax = Mathf.Max(a.x, Mathf.Max(bb.x, cc.x));
                    float yMin = Mathf.Min(a.y, Mathf.Min(bb.y, cc.y));
                    float zMid = (a.z + bb.z + cc.z) / 3f;
                    if (sMax - sMin < 0.5f * (s1 - s0) || yMin > 7f) continue;
                    if (Mathf.Abs(zMid + outSide * leg) < 0.8f) inner++;
                    if (Mathf.Abs(zMid - outSide * leg) < 0.8f) outer++;
                }
                Assert.AreEqual(0, inner, "bank " + k + ": geometry in the catwalk-facing leg plane between the frames");
                Assert.Greater(outer, 0, "bank " + k + ": the outboard X-brace");
                Color frame = frames.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
                Color machine = bank.Find("Cylinder").GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
                Assert.Less(frame.linear.grayscale, 0.8f * machine.linear.grayscale, "bank " + k + ": frames darker than the machines");
            }
        }

        /// <summary>
        /// One cold uplight per bank on the pit plate, inboard of the crankshaft and aimed up: the underside of the wheel (seen
        /// from the catwalk 4-8 m below its plane) lies inside its cone, the base of the polished crankshaft outside it (no
        /// specular hot spot), and it is a local light inside the hull like the rest of the rig.
        /// </summary>
        [Test]
        public void WheelUplights_LightTheWheelUndersides_NotThePolishedShaft()
        {
            Light[] lights = _full.GetComponentsInChildren<Light>(true);
            int n = 0;
            foreach (Light l in lights)
            {
                if (!l.name.StartsWith("WheelUplight", StringComparison.Ordinal)) continue;
                n++;
                int k = l.name[l.name.Length - 1] - '0';
                Vector3 c = Beat04_Hall.CrankCentre(k), p = l.transform.position, f = l.transform.forward;
                Assert.AreEqual(LightType.Spot, l.type, l.name);
                Assert.Greater(f.y, 0.95f, l.name + " aimed up");
                Assert.Less(Mathf.Abs(p.z - AbyssConst.W[8].z), Mathf.Abs(AbyssConst.BANK_V[k]), l.name + " inboard of the crankshaft");
                var wheel = new Vector3(c.x, -1f, c.z);
                Assert.Less(Vector3.Angle(f, wheel - p), 0.5f * l.spotAngle, l.name + ": wheel centre inside the cone");
                var shaftBase = new Vector3(c.x, -17.5f, c.z + Mathf.Sign(p.z - c.z) * 1.5f);
                Assert.Greater(Vector3.Angle(f, shaftBase - p), 0.5f * l.spotAngle, l.name + ": crankshaft base outside the cone");
                Assert.Greater(l.range, (wheel - p).magnitude + 5f, l.name + " reaches the wheel");
            }
            Assert.AreEqual(4, n, "one wheel uplight per bank");

            // cylinder flank uplights of the inner banks: the cylinder axis inside the cone, the catwalk deck outside it
            int m = 0;
            foreach (Light l in lights)
            {
                if (!l.name.StartsWith("CylinderUplight", StringComparison.Ordinal)) continue;
                m++;
                int k = l.name[l.name.Length - 1] - '0';
                Assert.Less(Mathf.Abs(AbyssConst.BANK_V[k]), 25f, l.name + " belongs to an inner bank");
                Vector3 c = Beat04_Hall.CrankCentre(k), p = l.transform.position, f = l.transform.forward;
                Assert.AreEqual(LightType.Spot, l.type, l.name);
                var axis = new Vector3(p.x, 4.5f, c.z);
                Assert.Less(Vector3.Angle(f, axis - p), 0.5f * l.innerSpotAngle, l.name + ": cylinder axis inside the inner cone");
                for (float dz = -0.75f; dz <= 0.75f; dz += 0.25f)
                    Assert.Greater(Vector3.Angle(f, new Vector3(p.x, AbyssConst.CATWALK_Y, AbyssConst.W[8].z + dz) - p), 0.5f * l.spotAngle,
                        l.name + ": catwalk deck outside the cone");
            }
            Assert.AreEqual(2, m, "a flank uplight under each inner cylinder");
        }

        // ---------------------------------------------------------------- critique R1-32: catwalk lamps do not burn their own post

        /// <summary>
        /// Every catwalk lamp is a spot aimed down across the walkway; its own post (grating to top rail) lies at least at the
        /// outer cone edge minus 1.5 degrees (the spot's falloff is zero there) and the top and mid rail beside it (3 m either way)
        /// outside the cone, so no non-emissive surface sits a few centimetres from an unshielded source.
        /// </summary>
        [Test]
        public void CatwalkLamps_KeepTheirOwnPostAndRailsOutOfTheCone()
        {
            Light[] lights = _full.GetComponentsInChildren<Light>(true);
            float zc = AbyssConst.W[8].z, yTop = AbyssConst.CATWALK_Y + AbyssConst.RAIL_H - 0.025f, yMid = AbyssConst.CATWALK_Y + 0.55f;
            int n = 0;
            foreach (Light l in lights)
            {
                if (!l.name.StartsWith("CatwalkLamp", StringComparison.Ordinal)) continue;
                n++;
                Assert.AreEqual(LightType.Spot, l.type, l.name);
                Vector3 p = l.transform.position, f = l.transform.forward;
                Assert.Less(f.y, -0.75f, l.name + " aimed down onto the grating");
                float zr = zc + Mathf.Sign(p.z - zc) * 0.725f, half = 0.5f * l.spotAngle;
                for (float y = AbyssConst.CATWALK_Y + 0.05f; y <= yTop; y += 0.05f)
                    Assert.Greater(Vector3.Angle(f, new Vector3(p.x, y, zr) - p), half - 1.5f, l.name + ": own post at y " + y);
                for (float dx = -3f; dx <= 3f; dx += 0.25f)
                {
                    Assert.Greater(Vector3.Angle(f, new Vector3(p.x + dx, yTop, zr) - p), half, l.name + ": top rail at dx " + dx);
                    Assert.Greater(Vector3.Angle(f, new Vector3(p.x + dx, yMid, zr) - p), half, l.name + ": mid rail at dx " + dx);
                }
            }
            Assert.AreEqual(11, n, "a catwalk lamp every 10 m plus the portal-end lamp at u 3 (R2-10)");
        }

        // ---------------------------------------------------------------- critique R2-10 / R2-26: KS4 catwalk grating, lens caps

        /// <summary>
        /// KS4 catwalk (critique R2-10): the walkway in front of the arbitrated KS4 eye is M_Grating; on its centreline from the
        /// frame's lower edge to 6 m from the eye, the bar family running with the view keeps its own footprint above the shader's
        /// 4 px fade end (per-family fade k = smoothstep(4, 8, pitch / footprint) at the KS4 FOV at 1080 rows, k >= 0.45: the
        /// bars read), and that stretch (u 2-3.8) lies inside a catwalk lamp's cone and range, so the bars are lit against the
        /// shaded slots instead of merging into a dark band.
        /// </summary>
        [Test]
        public void Ks4Catwalk_IsGrating_ResolvedAndLitWithinSixMetres()
        {
            Transform cw = _full.transform.Find("Geometry/Interior/Catwalk/CatwalkGrating");
            Assert.IsNotNull(cw, "catwalk grating renderer");
            Assert.AreEqual(_lib[MatId.Grating].shader, cw.GetComponent<MeshRenderer>().sharedMaterial.shader, "the catwalk deck uses the M_Grating shader");
            float pitch = _lib[MatId.Grating].GetFloat("_Pitch");

            KeyShot ks = KeyShots.All[KeyShots.IndexOf("KS4_hall_pistons")];
            float pa = 2f * Mathf.Tan(0.5f * ks.Fov * Mathf.Deg2Rad) / KeyShots.H1080;   // radians per pixel row at 1080p
            float eyeH = ks.Eye.y - AbyssConst.CATWALK_Y;
            float bottom = (ks.Pitch - 0.5f * ks.Fov) * Mathf.Deg2Rad;                    // frame's lower edge (negative)
            float rBottom = eyeH / Mathf.Tan(-bottom);                                      // horizontal distance where it meets the grating
            Light[] lights = _full.GetComponentsInChildren<Light>(true);
            int samples = 0;
            for (float r = rBottom; ; r += 0.25f)
            {
                float slant = Mathf.Sqrt(r * r + eyeH * eyeH);
                if (slant > 6f) break;
                samples++;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 8f, pitch / (slant * pa)));
                Assert.GreaterOrEqual(k, 0.45f, "bars along the walk resolved at " + slant + " m from the KS4 eye");
                var p = new Vector3(ks.Eye.x - r, AbyssConst.CATWALK_Y, AbyssConst.W[8].z);
                bool lit = false;
                foreach (Light l in lights)
                {
                    if (!l.name.StartsWith("CatwalkLamp", StringComparison.Ordinal)) continue;
                    Vector3 v = p - l.transform.position;
                    if (v.magnitude < 0.9f * l.range && Vector3.Angle(l.transform.forward, v) < 0.5f * l.spotAngle) { lit = true; break; }
                }
                Assert.IsTrue(lit, "catwalk at u " + (AbyssConst.HALL_X0 - p.x) + " (" + slant + " m from the KS4 eye) inside a catwalk lamp cone");
            }
            Assert.Greater(samples, 4, "the grating is in the KS4 frame from " + rBottom + " m");
        }

        /// <summary>
        /// Lens caps (critique R2-26, lead decision D4): the hall's cold lenses (catwalk lamps, uplights, washers, cans) are
        /// emissive at most 0.9 x COLD_LAMP (about 0.79 display after the beat-4 EV and the Neutral tonemapper, not a pure-white
        /// panel), the amber lenses (jamb hazards, crank pits) at most 2.5 x AMBER; the catwalk lamps' end lenses sit in a hood.
        /// </summary>
        [Test]
        public void HallLenses_AreCappedBelowWhite()
        {
            Transform interior = _full.transform.Find("Geometry/Interior");
            Material cold = interior.Find("LampLensesCold").GetComponent<MeshRenderer>().sharedMaterial;
            Material amber = interior.Find("LampLensesAmber").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.LessOrEqual(cold.GetFloat("_EmissionStrength"), 0.9f, "cold lens emission");
            Assert.LessOrEqual(amber.GetFloat("_EmissionStrength"), 2.5f, "amber lens emission (D4)");
            foreach (MeshRenderer r in interior.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "PitLampLens") Assert.LessOrEqual(r.sharedMaterial.GetFloat("_EmissionStrength"), 2.5f, "pit lamp lens (D4)");
            // the hood: housing steel reaches 25 mm beyond each end lens of every catwalk lamp
            Bounds housings = VertexBounds(interior.Find("LampHousings"), out Vector3[] hv);
            Assert.Greater(housings.size.x, 1f);
            Light[] lights = _full.GetComponentsInChildren<Light>(true);
            foreach (Light l in lights)
            {
                if (!l.name.StartsWith("CatwalkLamp", StringComparison.Ordinal)) continue;
                float x = l.transform.position.x;
                float reach = 0f;
                for (int i = 0; i < hv.Length; i++)
                    if (Mathf.Abs(hv[i].x - x) < 0.2f && Mathf.Abs(hv[i].y - (AbyssConst.CATWALK_Y + AbyssConst.RAIL_H + 0.05f)) < 0.06f)
                        reach = Mathf.Max(reach, Mathf.Abs(hv[i].x - x));
                Assert.GreaterOrEqual(reach, 0.14f, l.name + ": hood 2 cm beyond the end lenses (0.12)");
            }
        }

        // ---------------------------------------------------------------- critique R1-19: hull exterior

        /// <summary>World-space bounds of a child renderer's mesh vertices.</summary>
        static Bounds VertexBounds(Transform t, out Vector3[] world)
        {
            Vector3[] v = t.GetComponent<MeshFilter>().sharedMesh.vertices;
            world = new Vector3[v.Length];
            Matrix4x4 m = t.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(v[0]), Vector3.zero);
            for (int i = 0; i < v.Length; i++) { world[i] = m.MultiplyPoint3x4(v[i]); b.Encapsulate(world[i]); }
            return b;
        }

        /// <summary>
        /// The hull exterior reads as a carried mass with human scale (critiques R1-19, R2-08): two girders run from the
        /// near-flat belt (inner face z 80) into the hull's -Z face inside the belt band, a 1.1 m walkway rail on the nearer one
        /// ends at a service door; no free-standing roof stacks; two hangers rise from roof plinths (standing on the roof slab,
        /// never reaching into the hall below the ceiling) to the y 210 belt and enter it through its inner face inside the
        /// belt band and under its 4 m top chamfer; none of it can enter the KS2b nadir view at any capture aspect. The lamp
        /// rows stay 20 m apart (14.5.4) with 38-52 % of the fixtures dark on the bridge-facing faces and 7-25 % on the others,
        /// the panel joints form a 24 / 12 / 6 m hierarchy, and nothing on the -X face protrudes more than 0.45 m (lift
        /// clearance, 14.5.5).
        /// </summary>
        [Test]
        public void HullExterior_CarriesLoadPaths_ScaleCues_AndVariedLamps()
        {
            Transform sk = _full.transform.Find("Geometry/Skeleton");
            Assert.IsNotNull(sk, "skeleton");
            float beltY = AbyssConst.RingY(20), beltHalf = 0.5f * AbyssConst.BELT_HEIGHT;
            Assert.IsTrue(AbyssConst.RingIsBelt(20), "level 20 is a belt");
            Bounds g = VertexBounds(sk.Find("HullGirders"), out Vector3[] gv);
            Assert.That(g.min.z, Is.InRange(AbyssConst.SHAFT_NEAR_Z + 19f, AbyssConst.SHAFT_NEAR_Z + 20f), "girders start inside the belt's inner face (z 80)");
            Assert.That(g.max.z, Is.InRange(AbyssConst.HALL_Z0, AbyssConst.HALL_Z0 + 4f), "girders end inside the hull");
            Assert.GreaterOrEqual(g.min.y, AbyssConst.HALL_HULL_BOTTOM_Y, "nothing below the hull bottom");
            Assert.That(g.min.x, Is.GreaterThan(AbyssConst.HALL_X1));
            Assert.That(g.max.x, Is.LessThan(AbyssConst.HALL_X0));
            for (int i = 0; i < gv.Length; i++)
                if (gv[i].z < AbyssConst.HALL_Z0 - 15f)
                    Assert.That(gv[i].y, Is.InRange(beltY - beltHalf, beltY + beltHalf), "girder span inside the belt band at z " + gv[i].z);
            Assert.AreEqual(2.0, Report(_full, "hall.hull.girders"), "two girders");

            Bounds rails = VertexBounds(sk.Find("HullWalkwayRails"), out _);
            Assert.AreEqual(g.max.y + AbyssConst.RAIL_H, rails.max.y, 0.02f, "1.1 m rail on the girder top");
            Bounds door = VertexBounds(sk.Find("HullServiceDoorLeaf"), out _);
            Assert.That(door.size.y, Is.InRange(2.0f, 2.6f), "door height (human scale)");
            Assert.That(door.max.z, Is.InRange(AbyssConst.HALL_Z0, AbyssConst.HALL_Z0 + 0.05f), "door leaf set into the -Z face");
            Assert.IsNull(sk.Find("HullRoofStacks"), "no free-standing roof stacks (R2-08)");

            // hangers: from the roof slab to the belt of their level, entering through its inner face
            Assert.IsTrue(AbyssConst.RingIsBelt(Beat04_Hall.HangerBeltLevel), "the hangers end on a belt");
            float hy = Beat04_Hall.HangerBeltY, hz = Beat04_Hall.HangerBeltInnerZ;
            Assert.Greater(hy, AbyssConst.HALL_ROOF_Y + 100f, "the hanger belt lies far above the roof (no readable top)");
            Bounds hb = VertexBounds(sk.Find("HullHangers"), out Vector3[] hv);
            Assert.AreEqual(2.0, Report(_full, "hall.hull.hangers"), "two hangers");
            Assert.GreaterOrEqual(hb.min.y, AbyssConst.HALL_ROOF_Y - 1.0f, "hanger plinths stay in the roof slab (ceiling at 37.5)");
            Assert.Greater(hb.max.y, hy, "hangers reach the belt");
            for (int i = 0; i < hv.Length; i++)
            {
                Vector3 p = hv[i];
                Assert.That(p.x, Is.InRange(AbyssConst.HALL_X1, AbyssConst.HALL_X0), "hanger inside the hull's x range");
                if (p.z < AbyssConst.HALL_Z0 && p.y < AbyssConst.HALL_ROOF_Y + 1f) Assert.Fail("hanger below the roof outside the hull at " + p);
                if (p.z <= hz + 0.01f)
                {
                    // inside the belt: between its bottom and its top, under the 45 degree top chamfer (4 m on belts)
                    Assert.Greater(p.y, hy - 0.5f * AbyssConst.BELT_HEIGHT + 0.3f, "hanger enters the belt above its bottom at " + p);
                    float chamferTop = hy + 0.5f * AbyssConst.BELT_HEIGHT - 4f + (hz - p.z);
                    Assert.Less(p.y, Mathf.Min(chamferTop, hy + 0.5f * AbyssConst.BELT_HEIGHT) - 0.1f, "hanger stays under the belt's top chamfer at " + p);
                    Assert.Greater(p.z, AbyssConst.SHAFT_NEAR_Z, "hanger end inside the belt, in front of the wall");
                }
            }
            // where the hangers cross the belt's inner face (z 80) their section lies inside the belt band
            for (int gi = 0; gi < Beat04_Hall.HullGirderCount; gi++)
            {
                Beat04_Hall.HangerAxis(Beat04_Hall.HullGirderX(gi), out Vector3 foot, out Vector3 end);
                Assert.Less(end.z, hz, "hanger axis ends inside the belt");
                Assert.AreEqual(Beat04_Hall.HullGirderX(gi), foot.x, 1e-4f, "hanger over its girder");
            }

            // joint hierarchy: 24 m joints at full contrast, 12 m and 6 m joints fade with distance (6 m to at most 30 %)
            Material j24 = sk.Find("HullPanelGrid").GetComponent<MeshRenderer>().sharedMaterial;
            Material j12 = sk.Find("HullPanelGrid12").GetComponent<MeshRenderer>().sharedMaterial;
            Material j6 = sk.Find("HullPanelGrid6").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual(0f, j24.GetVector("_FarRange").y, 1e-6f, "24 m joints keep their contrast");
            Vector4 r6 = j6.GetVector("_FarRange"), r12 = j12.GetVector("_FarRange");
            Assert.That(r6.x, Is.InRange(20f, 100f), "6 m joints full near the hull (apron, portal, bridge end)");
            Assert.That(r6.y, Is.InRange(100f, 200f), "6 m joints faded by 200 m at the latest");
            Assert.Greater(r12.x, r6.x, "12 m joints fade later than the 6 m joints");
            Color hull = sk.Find("HullShell").GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor").linear;
            Color dark = j24.GetColor("_BaseColor").linear;
            float share6 = (hull.grayscale - j6.GetColor("_FarTint").linear.grayscale) / (hull.grayscale - dark.grayscale);
            float share12 = (hull.grayscale - j12.GetColor("_FarTint").linear.grayscale) / (hull.grayscale - dark.grayscale);
            Assert.That(share6, Is.InRange(0.15f, 0.35f), "6 m joint contrast at range (R2-08: at most 30 % by 200 m)");
            Assert.That(share12, Is.InRange(share6 + 0.1f, 0.9f), "12 m joints stay stronger than the 6 m joints");
            Assert.AreEqual(0, Beat04_Hall.JointLevel(4)); Assert.AreEqual(1, Beat04_Hall.JointLevel(2)); Assert.AreEqual(2, Beat04_Hall.JointLevel(3));
            Assert.Greater(sk.Find("HullShell").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_MacroAmp"), 1.2f, "hull macro term raised");

            // KS2b (bridge, nadir) never sees the structure, at every capture aspect
            KeyShot ks2b = KeyShots.All[KeyShots.IndexOf("KS2b_bridge_down")];
            // the girder span (prisms along z: their vertices at the belt end and on the stiffeners give the exact x-y extent;
            // the corbels at the hull end, z > 184, lie beyond the nadir frame's z range and would only inflate the box)
            var span = new Bounds();
            bool first = true;
            for (int i = 0; i < gv.Length; i++)
            {
                if (gv[i].z > AbyssConst.HALL_Z0 - 15f) continue;
                if (first) { span = new Bounds(gv[i], Vector3.zero); first = false; }
                else span.Encapsulate(gv[i]);
            }
            span.Encapsulate(new Vector3(span.center.x, span.center.y, AbyssConst.HALL_Z0 - 15f));
            foreach (float aspect in Aspects)
            {
                bool any = CompositionLint.ProjectBounds(span, ks2b.Eye, ks2b.Rotation, ks2b.Fov, aspect, AbyssConst.CAMERA_NEAR,
                    out float x0, out float y0, out float x1, out float y1, out _);
                Assert.IsTrue(!any || x1 < 0f || x0 > 1f || y1 < 0f || y0 > 1f, "girder span enters KS2b at aspect " + aspect + " (x " + x0 + ".." + x1 + ")");
            }
            foreach (string name in new[] { "HullWalkwayRails", "HullServiceDoorFrame", "HullHangers" })
            {
                Bounds b = VertexBounds(sk.Find(name), out _);
                foreach (float aspect in Aspects)
                {
                    bool any = CompositionLint.ProjectBounds(b, ks2b.Eye, ks2b.Rotation, ks2b.Fov, aspect, AbyssConst.CAMERA_NEAR,
                        out float x0, out float y0, out float x1, out float y1, out _);
                    Assert.IsTrue(!any || x1 < 0f || x0 > 1f || y1 < 0f || y0 > 1f, name + " enters KS2b at aspect " + aspect);
                }
            }

            // lamp rows every 20 m, 7-25 % dark
            for (int i = 1; i < Beat04_Hall.HullLampRowCount; i++)
                Assert.AreEqual(20f, Beat04_Hall.HullLampRowY(i) - Beat04_Hall.HullLampRowY(i - 1), 1e-4f, "lamp row spacing (14.5.4)");
            double lit = Report(_full, "hall.hull.lamps.lit"), darkLamps = Report(_full, "hall.hull.lamps.dark");
            double litB = Report(_full, "hall.hull.lamps.lit.bridgefaces"), darkB = Report(_full, "hall.hull.lamps.dark.bridgefaces");
            Assert.Greater(lit, 25.0, "lit hull lamps");
            Assert.Greater(litB + darkB, 20.0, "fixtures on the bridge-facing faces");
            Assert.That(darkB / (litB + darkB), Is.InRange(0.38, 0.52), "share of bridge-facing hull lamps left dark (R2-08: 40-50 %)");
            double litO = lit - litB, darkO = darkLamps - darkB;
            Assert.That(darkO / (litO + darkO), Is.InRange(0.07, 0.25), "share of the other hull lamps left dark");

            // -X face (lift side): nothing more than 0.45 m proud
            foreach (string name in new[] { "HullLampHousings", "HullLampLenses", "HullLampLensesDark", "HullPanelGrid", "HullPanelGrid12", "HullPanelGrid6" })
            {
                Transform t = sk.Find(name);
                if (t == null) continue;
                VertexBounds(t, out Vector3[] w);
                for (int i = 0; i < w.Length; i++)
                    if (w[i].x < AbyssConst.HALL_X1)
                        Assert.GreaterOrEqual(w[i].x, AbyssConst.HALL_X1 - 0.45f, name + " protrudes from the -X face at " + w[i]);
            }
        }

        /// <summary>
        /// Hull exterior read (critique R2-08, lead_b01 KS2a): the shell and its joint strips carry a height falloff of their
        /// ambient (vertex R: 0.6 at the bottom, 1 at the roof) so the skylit face sinks into the lower shaft instead of reading
        /// as one flat dark panel; C1's shadow on the lit +X face is completed above C1's last casting chunk (y 200) by an
        /// invisible ShadowsOnly cylinder inside C1 (the truncated shadow drew a crossed line pair on the face), which casts
        /// only, stays inside C1 and never reaches the hull or the walk; the service door under its lamp is a lit gradient
        /// card inside its jambs and in front of its leaf, lighter at the top, far below lamp-lens values.
        /// </summary>
        [Test]
        public void HullExterior_FadesDownward_C1ShadowWhole_AndShowsALitDoor()
        {
            Transform sk = _full.transform.Find("Geometry/Skeleton");
            MeshRenderer shell = sk.Find("HullShell").GetComponent<MeshRenderer>();
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, shell.shadowCastingMode, "the hull casts (beat-5 platform, apron)");

            // C1 shadow continuation: shadow-only, inside C1, overlapping C1's last casting chunk (top y 200)
            Transform cont = sk.Find("HullC1ShadowContinuation");
            Assert.IsNotNull(cont, "C1 shadow continuation");
            MeshRenderer cr = cont.GetComponent<MeshRenderer>();
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, cr.shadowCastingMode, "shadow only");
            Assert.AreEqual(_lib[MatId.ShadowCaster].shader, cr.sharedMaterial.shader, "invisible ShadowOnly material");
            Vector3 c1 = AbyssConst.COLUMNS[0];
            VertexBounds(cont, out Vector3[] cv);
            float maxR = 0f, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 p in cv)
            {
                maxR = Mathf.Max(maxR, new Vector2(p.x - c1.x, p.z - c1.y).magnitude);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            Assert.Less(maxR, c1.z, "inside C1's radius");
            Assert.Greater(maxR, c1.z - 0.5f, "C1's silhouette");
            Assert.That(minY, Is.InRange(AbyssConst.HALL_ROOF_Y + 100f, 200f), "overlaps C1's last casting chunk (y <= 200), far above the hull");
            Assert.Greater(maxY, 400f, "covers every shadow ray from the hull's +X face (they leave C1 below y 260)");

            // height falloff on the outer -Z face (the face the bridge sees)
            Mesh m = shell.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = m.vertices, nrm = m.normals;
            Color32[] col = m.colors32;
            Matrix4x4 w = shell.transform.localToWorldMatrix;
            int low = 0, high = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (nrm[i].z > -0.99f) continue;
                Vector3 p = w.MultiplyPoint3x4(v[i]);
                if (Mathf.Abs(p.z - AbyssConst.HALL_Z0) > 0.01f) continue;
                Assert.AreEqual(255f * Beat04_Hall.HullHeightAO(p.y), col[i].r, 1.01f, "-Z face AO at y " + p.y);
                if (p.y < AbyssConst.HALL_HULL_BOTTOM_Y + 0.01f) { low++; Assert.That((int)col[i].r, Is.InRange(140, 165), "AO at the hull bottom"); }
                if (p.y > AbyssConst.HALL_ROOF_Y - 0.01f) { high++; Assert.AreEqual(255, (int)col[i].r, "AO at the roof"); }
            }
            Assert.Greater(low, 0, "-Z face vertices on the hull bottom");
            Assert.Greater(high, 0, "-Z face vertices on the roof edge");
            Mesh joints = sk.Find("HullPanelGrid").GetComponent<MeshFilter>().sharedMesh;
            Color32[] jc = joints.colors32;
            Vector3[] jv = joints.vertices;
            Matrix4x4 jw = sk.Find("HullPanelGrid").localToWorldMatrix;
            for (int i = 0; i < jv.Length; i += 7)
                Assert.LessOrEqual((int)jc[i].r, Mathf.CeilToInt(255f * Beat04_Hall.HullHeightAO(jw.MultiplyPoint3x4(jv[i]).y)) + 1, "joint strips share the falloff");

            // the lit service door
            Transform doorLight = sk.Find("HullServiceDoorLight");
            Assert.IsNotNull(doorLight, "lit service door card");
            Bounds dl = VertexBounds(doorLight, out _);
            Bounds leaf = VertexBounds(sk.Find("HullServiceDoorLeaf"), out _);
            Bounds frame = VertexBounds(sk.Find("HullServiceDoorFrame"), out _);
            Assert.Less(dl.max.z, leaf.min.z, "card in front of the leaf");
            Assert.Greater(dl.min.z, frame.min.z, "card behind the jamb fronts");
            Assert.That(dl.size.y, Is.InRange(2.0f, 2.6f), "card covers the door height");
            Assert.Less(dl.size.x, leaf.size.x, "card inside the leaf outline");
            Assert.AreEqual(Beat04_Hall.HullGirderX(0), dl.center.x, 0.01f, "door on the walkway girder, under its lamp");
            Material dm = doorLight.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual(_lib[MatId.GradientCard].shader, dm.shader, "GradientCard");
            Color top = dm.GetColor("_CardTop"), bottom = dm.GetColor("_CardBottom");
            Assert.Greater(top.grayscale, bottom.grayscale, "lighter under the lamp than at the sill");
            Assert.Less(top.grayscale, 0.35f, "a lit door, not an emitter");
            Assert.AreEqual(1f, dm.GetFloat("_CardFog"), "fogged like the hull");
            Assert.LessOrEqual(dm.GetFloat("_EmissionStrength"), 1f);
        }
    }
}
