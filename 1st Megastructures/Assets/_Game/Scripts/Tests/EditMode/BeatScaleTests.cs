using System;
using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode;

namespace Megastructure.Tests
{
    /// <summary>
    /// Numeric scale checks of 14.10 for the seven beat generators: each beat is built into a temporary root (EditMode,
    /// Graybox tier, the generated MaterialLibrary), its ReportScale measurements (taken from generated mesh bounds and the
    /// posed moving parts) are compared with AbyssConst (14.12 / 14.4 / 14.5) within +/-0.5 % (angles +/-0.1 degree, counts
    /// exact), every collected mesh passes MeshValidation, every owned key-shot tag yields subjects, and the corridor contract
    /// holds (gantry floor starts at z = 259, nothing solid in the gate or boarding corridors, bridge edge walls flush at
    /// x = +/-0.6, the stair slope never overlaps the flat floors). Beat 1 also checks that its own shadow casters keep the
    /// Key off the gallery interior (14.2) and that the fixture wake ramp survives the RealTime restart at the walk start
    /// (14.5.1). Temporary objects and meshes are destroyed afterwards.
    /// </summary>
    public sealed class BeatScaleTests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";
        const double RelTol = 0.005;

        enum Tol { Relative, AbsDeg, Exact, Abs }

        struct Expect
        {
            public string Key; public double Value; public Tol Kind; public double Abs;
            public Expect(string key, double value, Tol kind = Tol.Relative, double abs = 0.0) { Key = key; Value = value; Kind = kind; Abs = abs; }
        }

        static readonly Type[] BeatTypes =
        {
            typeof(Beat01_Gallery), typeof(Beat02_Bridge), typeof(Beat03_Ledge), typeof(Beat04_Hall),
            typeof(Beat05_Lift), typeof(Beat06_Fan), typeof(Beat07_Reveal),
        };

        static readonly string[][] OwnedTags =
        {
            new[] { "KS1_gallery_start", "KS1b_doorway" },
            new[] { "KS2a_bridge_ahead", "KS2b_bridge_down" },
            new[] { "KS3_column_up" },
            new[] { "KS4_hall_pistons" },
            new[] { "KS5a_lift_city", "KS5b_lift_fan", "KS5c_lift_down" },
            new[] { "KS6_gantry_fan" },
            new[] { "KS7a_tunnel_mouth", "KS7b_honeycomb", "KS7c_goal_door", "KS7d_look_back" },
        };

        static Expect[] Expected(int beat)
        {
            switch (beat)
            {
                case 1:
                    return new[]
                    {
                        new Expect("gallery.width", AbyssConst.GALLERY_W), new Expect("gallery.height", AbyssConst.GALLERY_H),
                        new Expect("gallery.length", AbyssConst.GALLERY_L),
                        new Expect("gallery.door.width", AbyssConst.EXIT_DOOR_W), new Expect("gallery.door.height", AbyssConst.EXIT_DOOR_H),
                        new Expect("pressure.door.width", AbyssConst.PRESSURE_DOOR_W), new Expect("pressure.door.height", AbyssConst.PRESSURE_DOOR_H),
                        new Expect("gallery.stair.count", AbyssConst.STAIR_COUNT, Tol.Exact),
                        new Expect("gallery.stair.rise.min", AbyssConst.STAIR_RISE), new Expect("gallery.stair.rise.max", AbyssConst.STAIR_RISE),
                        new Expect("gallery.stair.run.min", AbyssConst.STAIR_RUN), new Expect("gallery.stair.run.max", AbyssConst.STAIR_RUN),
                        new Expect("gallery.stair.drop", AbyssConst.STAIR_DROP),
                    };
                case 2:
                    {
                        var l = new List<Expect>
                        {
                            new Expect("bridge.width", AbyssConst.BRIDGE_W), new Expect("bridge.length", AbyssConst.BRIDGE_L),
                            new Expect("bridge.lamp.count", AbyssConst.BRIDGE_LAMP_Z.Length, Tol.Exact),
                            new Expect("bridge.lamp.spacing", AbyssConst.BRIDGE_LAMP_SPACING),
                            new Expect("bridge.truss.depth", AbyssConst.BRIDGE_TRUSS_DEPTH),
                            new Expect("landing.width", AbyssConst.LANDING_W), new Expect("landing.length", AbyssConst.LANDING_L),
                            new Expect("shaft.apothem", AbyssConst.SHAFT_APOTHEM),
                            new Expect("shaft.height", AbyssConst.SHAFT_TOP_Y - AbyssConst.SHAFT_BOTTOM_Y),
                        };
                        for (int i = 0; i < AbyssConst.COLUMNS.Length; i++)
                        {
                            l.Add(new Expect("c" + (i + 1) + ".diameter", 2.0 * AbyssConst.COLUMNS[i].z));
                            l.Add(new Expect("c" + (i + 1) + ".height", AbyssConst.COLUMN_TOP_Y - AbyssConst.COLUMN_BOTTOM_Y));
                        }
                        return l.ToArray();
                    }
                case 3:
                    return new[]
                    {
                        new Expect("ledge.width", AbyssConst.LEDGE_W), new Expect("ledge.arc", AbyssConst.ARC_LENGTH),
                        new Expect("ledge.drop", AbyssConst.LEDGE_DROP), new Expect("ledge.thickness", AbyssConst.LEDGE_COLLAR_THICK),
                        new Expect("hatch.height", AbyssConst.HATCH_H),
                        new Expect("apron.length", AbyssConst.APRON_L), new Expect("apron.width", AbyssConst.APRON_W),
                    };
                case 4:
                    return new[]
                    {
                        new Expect("hall.length", AbyssConst.HALL_X0 - AbyssConst.HALL_X1), new Expect("hall.width", AbyssConst.HALL_Z1 - AbyssConst.HALL_Z0),
                        new Expect("hall.catwalk.width", AbyssConst.CATWALK_W),
                        new Expect("hall.opening.width", AbyssConst.HALL_OPENING_W), new Expect("hall.opening.height", AbyssConst.HALL_OPENING_H),
                        new Expect("piston.bore", AbyssConst.PISTON_BORE), new Expect("piston.stroke", AbyssConst.PISTON_STROKE),
                        new Expect("wheel.diameter", AbyssConst.WHEEL_D),
                        new Expect("crank.slider.min", AbyssConst.CRANK_L - AbyssConst.CRANK_R),
                        new Expect("crank.slider.max", AbyssConst.CRANK_L + AbyssConst.CRANK_R),
                        new Expect("crank.rod.error", 0.0, Tol.Abs, 1e-3),
                    };
                case 5:
                    return new[]
                    {
                        new Expect("lift.size", AbyssConst.LIFT_SIZE), new Expect("lift.size.z", AbyssConst.LIFT_SIZE),
                        new Expect("lift.thick", AbyssConst.LIFT_THICK), new Expect("lift.deck.top", 0.0, Tol.Abs, 1e-4),
                        new Expect("lift.rail.height", AbyssConst.RAIL_H),
                        new Expect("dock.width", PathGuide.DOCK_APRON_W), new Expect("dock.length", AbyssConst.W[10].x - AbyssConst.W[11].x),
                        new Expect("guide.rail.segments", AbyssConst.GUIDE_RAIL_SEGMENTS, Tol.Exact),
                        new Expect("lift.drop", AbyssConst.LIFT_DROP), new Expect("lift.travel.x", AbyssConst.LIFT_BOTTOM.x - AbyssConst.LIFT_TOP.x),
                    };
                case 6:
                    return new[]
                    {
                        new Expect("fan.housing.diameter", 2.0 * AbyssConst.FAN_R_OUT), new Expect("fan.hub.radius", AbyssConst.FAN_R_HUB),
                        new Expect("fan.tip.radius", AbyssConst.FAN_R_TIP), new Expect("fan.blades", AbyssConst.FAN_BLADES, Tol.Exact),
                        new Expect("fan.blade.arc.deg", AbyssConst.BEAM_WIDTH_DEG, Tol.AbsDeg),
                        new Expect("fan.gap.deg", 360.0 / AbyssConst.FAN_BLADES - AbyssConst.BEAM_WIDTH_DEG, Tol.AbsDeg),
                        new Expect("gantry.length", AbyssConst.GANTRY_L), new Expect("gantry.width", AbyssConst.GANTRY_W),
                        new Expect("gantry.z0", AbyssConst.GANTRY_Z0, Tol.Abs, 1e-3),
                    };
                case 7:
                    return new[]
                    {
                        new Expect("tunnel.length", AbyssConst.TUNNEL_L), new Expect("tunnel.width", AbyssConst.TUNNEL_W),
                        new Expect("tunnel.height", AbyssConst.TUNNEL_H),
                        new Expect("balcony.width", AbyssConst.BALCONY_W), new Expect("balcony.depth", AbyssConst.BALCONY_D),
                        new Expect("balcony.altitude", AbyssConst.BALCONY_ALTITUDE),
                        new Expect("pier.length", AbyssConst.PIER_L), new Expect("pier.width", AbyssConst.PIER_W),
                        new Expect("door.height", AbyssConst.GOAL_DOOR_H), new Expect("door.width", AbyssConst.GOAL_DOOR_W),
                        new Expect("door.z", AbyssConst.GOAL_DOOR_Z, Tol.Abs, 1e-3),
                        new Expect("face.width", AbyssConst.FACE_WIDTH),
                        new Expect("face.height", AbyssConst.SHAFT_TOP_Y - AbyssConst.TERRACE_Y),
                        new Expect("honeycomb.radius", AbyssConst.HONEY_TIER2),
                    };
            }
            return Array.Empty<Expect>();
        }

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

        BeatBase Build(int beat, bool editMode = true)
        {
            Type t = BeatTypes[beat - 1];
            var root = new GameObject("BeatScaleTest_" + t.Name);
            _roots.Add(root);
            var b = (BeatBase)root.AddComponent(t);
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            var ctx = new BeatContext
            {
                Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = BeatTier.Graybox, EditMode = editMode,
            };
            b.Build(ctx);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        static IEnumerable<int> Beats() { for (int i = 1; i <= 7; i++) yield return i; }

        // ---------------------------------------------------------------- scale

        [Test, TestCaseSource(nameof(Beats))]
        public void ReportedScaleMatchesConstants(int beat)
        {
            BeatBase b = Build(beat);
            Assert.AreEqual(beat, b.Index, "Index");
            var report = new ScaleReport();
            b.ReportScale(report);
            foreach (Expect e in Expected(beat))
            {
                Assert.IsTrue(report.Values.TryGetValue(e.Key, out double v), "beat " + beat + " does not report " + e.Key);
                Assert.IsFalse(double.IsNaN(v) || double.IsInfinity(v), e.Key + " is not finite");
                double tol;
                switch (e.Kind)
                {
                    case Tol.Exact: tol = 1e-9; break;
                    case Tol.AbsDeg: tol = 0.1; break;
                    case Tol.Abs: tol = e.Abs; break;
                    default: tol = Math.Abs(e.Value) * RelTol; break;
                }
                Assert.That(v, Is.EqualTo(e.Value).Within(tol), "beat " + beat + " " + e.Key);
            }
        }

        // ---------------------------------------------------------------- meshes

        [Test, TestCaseSource(nameof(Beats))]
        public void EveryGeneratedMeshIsValid(int beat)
        {
            BeatBase b = Build(beat);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            Assert.Greater(list.Count, 0, "beat " + beat + " collected no meshes");
            foreach (Mesh m in list)
            {
                Assert.IsNotNull(m, "null mesh in beat " + beat);
                MeshReport r = MeshValidation.Validate(m);
                Assert.IsTrue(r.Ok, "beat " + beat + ": " + r.Describe());
            }
        }

        // ---------------------------------------------------------------- QA subjects

        [Test, TestCaseSource(nameof(Beats))]
        public void OwnedKeyShotsHaveSubjects(int beat)
        {
            BeatBase b = Build(beat);
            var subjects = new List<KeyShotSubject>();
            foreach (string tag in OwnedTags[beat - 1])
            {
                subjects.Clear();
                b.CollectSubjects(tag, subjects);
                Assert.Greater(subjects.Count, 0, tag + " has no subjects");
                foreach (KeyShotSubject s in subjects)
                {
                    Vector3 c = s.WorldBounds.center, e = s.WorldBounds.extents;
                    Assert.IsTrue(Finite(c) && Finite(e), tag + "/" + s.Name + " bounds not finite");
                    Assert.Greater(e.magnitude, 0f, tag + "/" + s.Name + " has empty bounds");
                }
            }
        }

        // ---------------------------------------------------------------- corridor contract

        [Test]
        public void GantryFloorStartsAtTheGateAndTheGateCorridorIsClear()
        {
            for (int i = 1; i <= 7; i++) Build(i);
            Bounds floor = default;
            bool found = false;
            foreach (BoxCollider bc in AllBoxes())
            {
                if (bc.name != "GantryFloor") continue;
                floor = WorldAabb(bc);
                found = true;
            }
            Assert.IsTrue(found, "no GantryFloor collider");
            Assert.That(floor.max.z, Is.EqualTo(AbyssConst.GANTRY_Z0).Within(1e-3f), "gantry floor must start exactly at z = 259");
            Assert.That(floor.max.y, Is.EqualTo(AbyssConst.GANTRY_Y).Within(1e-3f), "gantry floor top must be y = -487");
            Assert.That(floor.min.z, Is.EqualTo(AbyssConst.GANTRY_Z1).Within(1e-3f), "gantry floor must end at z = 60");

            // the capsule's volume while it leaves the deck through the 1.15 m gate corridor (local z -10.25 at the exit)
            float gx = AbyssConst.LIFT_BOTTOM.x, gate = AbyssConst.GANTRY_W * 0.5f - AbyssConst.CAPSULE_R;
            var corridor = new Bounds();
            corridor.SetMinMax(new Vector3(gx - gate - AbyssConst.CAPSULE_R + 0.01f, AbyssConst.GANTRY_Y + 0.05f, AbyssConst.LIFT_BOTTOM.z - 10.25f - AbyssConst.CAPSULE_R),
                new Vector3(gx + gate + AbyssConst.CAPSULE_R - 0.01f, AbyssConst.GANTRY_Y + AbyssConst.CAPSULE_H, AbyssConst.LIFT_BOTTOM.z + AbyssConst.LIFT_SIZE * 0.5f));
            AssertNothingSolidIn(corridor, "gate corridor");

            // boarding corridor at the top dock (+X edge of the deck, 4 m opening): only the gap wall (disabled while docked) may sit there
            Vector3 top = AbyssConst.LIFT_TOP;
            float open = LiftController.OPENING_HALF;
            var boarding = new Bounds();
            boarding.SetMinMax(new Vector3(top.x + AbyssConst.LIFT_SIZE * 0.5f - 0.6f, top.y + 0.05f, top.z - open - AbyssConst.CAPSULE_R + 0.01f),
                new Vector3(top.x + AbyssConst.LIFT_SIZE * 0.5f + 0.6f, top.y + AbyssConst.CAPSULE_H, top.z + open + AbyssConst.CAPSULE_R - 0.01f));
            AssertNothingSolidIn(boarding, "boarding corridor");
        }

        [Test]
        public void BridgeEdgeWallsAreFlushWithTheDeck()
        {
            Build(2);
            float left = float.NaN, right = float.NaN;
            foreach (BoxCollider bc in AllBoxes())
            {
                if (bc.name == "BridgeEdgeL") left = WorldAabb(bc).max.x;
                if (bc.name == "BridgeEdgeR") right = WorldAabb(bc).min.x;
            }
            Assert.That(left, Is.EqualTo(-AbyssConst.BRIDGE_W * 0.5f).Within(1e-4f), "left edge wall");
            Assert.That(right, Is.EqualTo(AbyssConst.BRIDGE_W * 0.5f).Within(1e-4f), "right edge wall");
        }

        [Test]
        public void StairSlopeNeitherOverlapsNorStepsAgainstTheFloors()
        {
            Build(1);
            MeshCollider slope = null;
            Bounds upper = default, lower = default;
            foreach (GameObject root in _roots)
            {
                foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
                    if (mc.name == "GalleryStairSlope") slope = mc;
                foreach (BoxCollider bc in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (bc.name == "GalleryFloorUpper") upper = WorldAabb(bc);
                    if (bc.name == "GalleryFloorLower") lower = WorldAabb(bc);
                }
            }
            Assert.IsNotNull(slope, "no stair slope collider");
            Bounds s = slope.sharedMesh.bounds;   // the slope mesh is authored in world coordinates (identity transform)
            // 1e-4 m^3 = float rounding of the z = 44.48 joint over the 2.4 m width (tens of micrometres): no step for the capsule
            Assert.LessOrEqual(OverlapVolume(s, upper), 1e-4, "slope overlaps the upper floor");
            Assert.LessOrEqual(OverlapVolume(s, lower), 1e-4, "slope overlaps the lower floor");
            // the slope top meets each floor top on the same edge (no step): W1 at the top, W2 at the bottom
            Assert.That(s.min.z, Is.EqualTo(upper.max.z).Within(1e-4f));
            Assert.That(s.max.z, Is.EqualTo(lower.min.z).Within(1e-4f));
            Assert.That(s.max.y, Is.EqualTo(upper.max.y).Within(1e-4f));
            Assert.That(upper.max.y - lower.max.y, Is.EqualTo(AbyssConst.STAIR_DROP).Within(1e-4f));
            var v = slope.sharedMesh.vertices;
            foreach (Vector3 p in v)
            {
                if (Mathf.Abs(p.z - AbyssConst.STAIR_Z1) < 1e-3f && p.y > lower.max.y + 1e-4f)
                    Assert.Fail("slope rises above the lower floor at its foot: " + p);
            }
        }

        // ---------------------------------------------------------------- beat 1: Key shadows and fixture wake

        /// <summary>
        /// The beat-2 sun comes on at d 52 while the player is still in the gallery, and beat 1 must stay unlit by the Key
        /// (14.2). From points just above the walking surface and at mid height, across the width and along the whole
        /// gallery (start wall to 1.5 m before the doorway), the ray toward the light must hit a triangle the ShadowCaster
        /// pass keeps: of a renderer that casts, facing the light (Cull Back), or either side for a TwoSided caster.
        /// Only beat 1 is built, so the gallery has to block the sun on its own.
        /// </summary>
        [Test]
        public void GalleryBlocksTheKeyLight()
        {
            BeatBase b = Build(1);
            var corners = new List<Vector3>(8192);
            var bothSides = new List<bool>(4096);
            bool shellFound = false;
            foreach (MeshRenderer mr in b.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name == "GalleryShell")
                {
                    shellFound = true;
                    Assert.AreEqual(ShadowCastingMode.TwoSided, mr.shadowCastingMode, "the open, inward-facing shell must cast TwoSided");
                }
                if (mr.shadowCastingMode == ShadowCastingMode.Off) continue;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Matrix4x4 m = mr.transform.localToWorldMatrix;
                Vector3[] v = mf.sharedMesh.vertices;
                int[] idx = mf.sharedMesh.triangles;
                bool two = mr.shadowCastingMode == ShadowCastingMode.TwoSided;
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    corners.Add(m.MultiplyPoint3x4(v[idx[t]]));
                    corners.Add(m.MultiplyPoint3x4(v[idx[t + 1]]));
                    corners.Add(m.MultiplyPoint3x4(v[idx[t + 2]]));
                    bothSides.Add(two);
                }
            }
            Assert.IsTrue(shellFound, "no GalleryShell renderer");

            float[] xs = { -1.15f, -0.6f, 0f, 0.6f, 1.15f };
            float[] heights = { 0.05f, 1.25f };
            int[] rows = { ProgressTables.KEY_SUN2, ProgressTables.KEY_SUN3 };
            foreach (int row in rows)
            {
                KeyRow key = ProgressTables.KeyRows[row];
                Vector3 toLight = -AbyssConst.Dir(key.Yaw, key.Pitch);
                Assert.Greater(toLight.y, 0f, key.Name + " must shine downward");
                int lit = 0, total = 0;
                string first = null;
                for (int iz = 0; iz <= 236; iz++)
                {
                    float z = -0.4f + 0.25f * iz;   // -0.4 .. 58.6 (the doorway lets the sun in from about 59.3 on)
                    if (z > 58.5f) break;
                    for (int ix = 0; ix < xs.Length; ix++)
                    for (int ih = 0; ih < heights.Length; ih++)
                    {
                        var p = new Vector3(xs[ix], Beat01_Gallery.FloorY(z) + heights[ih], z);
                        total++;
                        if (RayHitsCaster(p, toLight, corners, bothSides)) continue;
                        lit++;
                        if (first == null) first = p.ToString("F2");
                    }
                }
                Assert.AreEqual(0, lit, "key row '" + key.Name + "' reaches " + lit + " of " + total + " gallery points, first at " + first);
            }
        }

        /// <summary>
        /// The fixture wake ramp (14.5.1: within 20 m, ease on over 0.8 s) must survive the load sequence: the beats tick through
        /// the settle frames while the walk clock is held and RealTime counts the load, then ClockDriver.Begin restarts RealTime
        /// at 0. No fixture may wake while the clock is held, the three fixtures within 20 m of the start must reach full
        /// intensity within 0.8 s of the walk start without falling back, and a later backward jump of RealTime must neither
        /// darken nor restart a waking fixture.
        /// </summary>
        [Test]
        public void GalleryFixturesWakeOnTheWalkClock()
        {
            double time0 = SceneClock.Time, real0 = SceneClock.RealTime;
            bool frozen0 = SceneClock.Frozen, running0 = ClockDriver.Running;
            try
            {
                float full = FixtureLight(Build(1), 0).intensity;   // EditMode build: every fixture fully on
                Assert.Greater(full, 0f, "fixture intensity");
                BeatBase b = Build(1, false);
                var fixtures = new Light[10];
                for (int i = 0; i < fixtures.Length; i++)
                {
                    fixtures[i] = FixtureLight(b, i);
                    Assert.AreEqual(0f, fixtures[i].intensity, "fixture " + i + " must start dark in a play-mode build");
                }

                // load: walk clock held, RealTime already at several seconds, the beats tick through 8 settle frames at d = 0
                var f = new BeatFrame { D = 0f };
                ClockDriver.Hold();
                for (int i = 0; i < 8; i++)
                {
                    SceneClock.RealTime = 5.0 + i / 60.0;
                    b.Tick(in f);
                }
                for (int i = 0; i < fixtures.Length; i++)
                    Assert.AreEqual(0f, fixtures[i].intensity, "fixture " + i + " woke while the walk clock was held");

                // walk start: RealTime restarts at 0
                ClockDriver.Begin(0.0);
                float prev = 0f;
                for (int frame = 1; frame <= 60; frame++)
                {
                    SceneClock.RealTime = frame / 60.0;
                    b.Tick(in f);
                    Assert.GreaterOrEqual(fixtures[0].intensity, prev, "the wake ramp fell back at frame " + frame);
                    prev = fixtures[0].intensity;
                    if (frame == 49)   // 48 frames = 0.8 s after the first walk frame
                        for (int i = 0; i < 3; i++)
                            Assert.That(fixtures[i].intensity, Is.EqualTo(full).Within(1e-4f), "fixture " + i + " not fully on 0.8 s into the walk");
                }
                Assert.AreEqual(0f, fixtures[3].intensity, "fixture 3 (z 21) is more than 20 m ahead of d = 0");

                // a backward RealTime jump in the middle of a ramp (fixtures 3 and 4 wake at d = 10)
                f.D = 10f;
                SceneClock.RealTime = 2.0; b.Tick(in f);
                SceneClock.RealTime = 2.4; b.Tick(in f);
                float mid = fixtures[3].intensity;
                Assert.That(mid, Is.GreaterThan(0f).And.LessThan(full), "fixture 3 should be half way after 0.4 s");
                SceneClock.RealTime = 0.0; b.Tick(in f);
                Assert.GreaterOrEqual(fixtures[3].intensity, mid, "a RealTime reset darkened a waking fixture");
                SceneClock.RealTime = 0.5; b.Tick(in f);
                Assert.That(fixtures[3].intensity, Is.EqualTo(full).Within(1e-4f), "fixture 3 should be fully on after 0.9 s of ramp");
            }
            finally
            {
                if (running0) ClockDriver.Begin(time0); else ClockDriver.Hold();
                SceneClock.Time = time0;
                SceneClock.RealTime = real0;
                SceneClock.Frozen = frozen0;
            }
        }

        Light FixtureLight(BeatBase b, int i)
        {
            Transform t = b.transform.Find("Lights/FixtureLight" + i);
            Assert.IsNotNull(t, "no Lights/FixtureLight" + i);
            Light l = t.GetComponent<Light>();
            Assert.IsNotNull(l, "FixtureLight" + i + " has no Light");
            return l;
        }

        /// <summary>
        /// Moller-Trumbore ray test against triangle soup (3 corners per triangle). A one-sided caster triangle counts only when
        /// its front face (MeshBuilder winding: cross(b - a, c - a)) faces the light, as the Cull Back ShadowCaster pass keeps it.
        /// </summary>
        static bool RayHitsCaster(Vector3 origin, Vector3 dir, List<Vector3> corners, List<bool> bothSides)
        {
            for (int t = 0; t < bothSides.Count; t++)
            {
                Vector3 a = corners[3 * t], e1 = corners[3 * t + 1] - a, e2 = corners[3 * t + 2] - a;
                if (!bothSides[t] && Vector3.Dot(Vector3.Cross(e1, e2), dir) <= 0f) continue;
                Vector3 pv = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-12f) continue;
                float inv = 1f / det;
                Vector3 tv = origin - a;
                float u = Vector3.Dot(tv, pv) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 qv = Vector3.Cross(tv, e1);
                float w = Vector3.Dot(dir, qv) * inv;
                if (w < 0f || u + w > 1f) continue;
                if (Vector3.Dot(e2, qv) * inv > 1e-4f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- helpers

        IEnumerable<BoxCollider> AllBoxes()
        {
            foreach (GameObject root in _roots)
                foreach (BoxCollider bc in root.GetComponentsInChildren<BoxCollider>(true))
                    yield return bc;
        }

        void AssertNothingSolidIn(Bounds region, string what)
        {
            foreach (BoxCollider bc in AllBoxes())
            {
                if (bc.isTrigger || !bc.enabled) continue;
                Bounds b = WorldAabb(bc);
                if (OverlapVolume(b, region) > 1e-6)
                    Assert.Fail(what + " intersects collider " + bc.name + " " + b);
            }
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

        static double OverlapVolume(Bounds a, Bounds b)
        {
            double x = Math.Min(a.max.x, b.max.x) - Math.Max(a.min.x, b.min.x);
            double y = Math.Min(a.max.y, b.max.y) - Math.Max(a.min.y, b.min.y);
            double z = Math.Min(a.max.z, b.max.z) - Math.Max(a.min.z, b.min.z);
            if (x <= 0 || y <= 0 || z <= 0) return 0;
            return x * y * z;
        }

        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                                           float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
