using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 7 (14.5.7 Acceptance, 14.7.7, 14.11): the reported dimensions of every tier (tunnel 60, balcony 20 x 14, pier 60,
    /// door 2.00, altitude 600 +/- 1, Face 473.4 x 2287), valid meshes, the honeycomb cell basis (flat-top axial as the
    /// Lattice shader, b1 = (0, 760), b2 = (658.18, 380)), lamps at the cell centre + (0, -269) (= the S0 middle bridge lamp),
    /// about 4 920 cells inside 28 km, tier cell sets that cover every terrace pixel at every camera position of the walk,
    /// the honeycomb parts that are never faded lying under the lattice plane, the QA tier override, the goal / leaf, the 14.10
    /// composition lint of every KS7 subject at every capture size, and no beat-7 face on Beat02's near flat (z = 60) with its
    /// facing (cross-beat z-fight at the shaft-side tunnel mouth).
    /// </summary>
    public sealed class Beat07Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";
        static readonly int DistFadeId = Shader.PropertyToID("_DistFade");

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
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
        }

        Beat07_Reveal Build(BeatTier tier)
        {
            var root = new GameObject("Beat07Test_" + tier);
            _roots.Add(root);
            var b = root.AddComponent<Beat07_Reveal>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            b.Build(new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = true });
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        static double Get(ScaleReport r, string key)
        {
            Assert.IsTrue(r.Values.TryGetValue(key, out double v), "missing measurement " + key);
            Assert.IsFalse(double.IsNaN(v) || double.IsInfinity(v), key + " not finite");
            return v;
        }

        // ---------------------------------------------------------------- 14.5.7 Acceptance dimensions

        [TestCase(BeatTier.Full)]
        [TestCase(BeatTier.Simplified)]
        [TestCase(BeatTier.Graybox)]
        public void AcceptanceDimensions(BeatTier tier)
        {
            var r = new ScaleReport();
            Build(tier).ReportScale(r);
            Assert.That(Get(r, "tunnel.length"), Is.EqualTo(AbyssConst.TUNNEL_L).Within(0.1), "tunnel 60.0 +/- 0.1");
            Assert.That(Get(r, "tunnel.width"), Is.EqualTo(AbyssConst.TUNNEL_W).Within(0.02), "tunnel width 3.6");
            Assert.That(Get(r, "tunnel.height"), Is.EqualTo(AbyssConst.TUNNEL_H).Within(0.02), "tunnel height 3.4");
            Assert.That(Get(r, "balcony.width"), Is.EqualTo(AbyssConst.BALCONY_W).Within(0.05), "balcony 20.0 +/- 0.05");
            Assert.That(Get(r, "balcony.depth"), Is.EqualTo(AbyssConst.BALCONY_D).Within(0.05), "balcony 14.0 +/- 0.05");
            Assert.That(Get(r, "balcony.thickness"), Is.EqualTo(AbyssConst.BALCONY_THICK).Within(0.02), "balcony slab 2.5");
            Assert.That(Get(r, "balcony.altitude"), Is.EqualTo(AbyssConst.BALCONY_ALTITUDE).Within(1.0), "altitude above terrace 600 +/- 1");
            Assert.That(Get(r, "pier.length"), Is.EqualTo(AbyssConst.PIER_L).Within(0.1), "pier 60.0 +/- 0.1");
            Assert.That(Get(r, "pier.width"), Is.EqualTo(AbyssConst.PIER_W).Within(0.02), "pier 3.0");
            Assert.That(Get(r, "door.height"), Is.EqualTo(AbyssConst.GOAL_DOOR_H).Within(0.01), "door 2.00 +/- 0.01");
            Assert.That(Get(r, "door.width"), Is.EqualTo(AbyssConst.GOAL_DOOR_W).Within(0.01), "door 1.00");
            Assert.That(Get(r, "door.frame.width"), Is.EqualTo(3.0).Within(0.01), "frame 3.0 wide");
            Assert.That(Get(r, "door.frame.height"), Is.EqualTo(2.6).Within(0.01), "frame 2.6 tall");
            Assert.That(Get(r, "door.z"), Is.EqualTo(AbyssConst.GOAL_DOOR_Z).Within(1e-3), "door at z -74");
            Assert.That(Get(r, "face.width"), Is.EqualTo(AbyssConst.FACE_WIDTH).Within(0.05), "Face 473.4 wide");
            Assert.That(Get(r, "face.height"), Is.EqualTo(AbyssConst.SHAFT_TOP_Y - AbyssConst.TERRACE_Y).Within(0.5), "Face -1087..+1200");
            Assert.That(Get(r, "honeycomb.radius"), Is.EqualTo(AbyssConst.HONEY_TIER2).Within(1.0), "lattice radius 28 km");
        }

        [TestCase(BeatTier.Full)]
        [TestCase(BeatTier.Simplified)]
        public void EveryMeshIsValid(BeatTier tier)
        {
            Beat07_Reveal b = Build(tier);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            Assert.Greater(list.Count, 20, "too few meshes for the full tier");
            foreach (Mesh m in list)
            {
                Assert.IsNotNull(m);
                MeshReport rep = MeshValidation.Validate(m);
                Assert.IsTrue(rep.Ok, rep.Describe());
            }
        }

        // ---------------------------------------------------------------- honeycomb geometry

        [Test]
        public void CellBasisMatchesTheLattice()
        {
            Vector2 c0 = Beat07_Reveal.CellCentre(0, 0);
            Assert.That((c0 - AbyssConst.SHAFT_CENTRE).magnitude, Is.LessThan(1e-4f), "cell (0, 0) is S0");
            Assert.That((Beat07_Reveal.CellCentre(0, 1) - c0 - AbyssConst.LATTICE_B1).magnitude, Is.LessThan(0.01f), "b1 = (0, 760)");
            Assert.That((Beat07_Reveal.CellCentre(1, 0) - c0 - AbyssConst.LATTICE_B2).magnitude, Is.LessThan(0.01f), "b2 = (658.18, 380)");

            // flat-top axial with the Lattice material's size (Red Blob Games: x = 3/2 s q, z = s (sqrt3/2 q + sqrt3 r))
            Material lattice = _lib[MatId.Lattice];
            double size = lattice.HasProperty("_LatticeParams") ? lattice.GetVector("_LatticeParams").x : Beat07_Reveal.HexSize;
            Assert.That(size, Is.EqualTo(AbyssConst.LATTICE_PITCH / System.Math.Sqrt(3.0)).Within(1e-3), "lattice size 760 / sqrt 3");
            Vector4 origin = lattice.HasProperty("_LatticeOrigin") ? lattice.GetVector("_LatticeOrigin") : new Vector4(0f, 410f, 0f, 0f);
            for (int q = -6; q <= 6; q++)
            for (int r = -6; r <= 6; r++)
            {
                Vector2 c = Beat07_Reveal.CellCentre(q, r);
                double x = origin.x + 1.5 * size * q, z = origin.y + size * (0.5 * System.Math.Sqrt(3.0) * q + System.Math.Sqrt(3.0) * r);
                Assert.That(c.x, Is.EqualTo(x).Within(0.01), "cell x " + q + "," + r);
                Assert.That(c.y, Is.EqualTo(z).Within(0.01), "cell z " + q + "," + r);
                Vector2 exp = AbyssConst.SHAFT_CENTRE + q * AbyssConst.LATTICE_B2 + r * AbyssConst.LATTICE_B1;
                Assert.That((c - exp).magnitude, Is.LessThan(0.01f * (1 + Mathf.Abs(q))), "q b2 + r b1 at " + q + "," + r);
                Assert.AreEqual(new Vector2Int(q, r), Beat07_Reveal.CellOf(c), "CellOf(centre) round trip");
            }
        }

        [Test]
        public void CellOfIsTheNearestCentre()
        {
            // the hexagonal Voronoi cell of the lattice: every point belongs to the nearest centre (flats perpendicular to Z)
            var rng = new System.Random(7);
            for (int i = 0; i < 4000; i++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 16000.0 - 8000.0), (float)(rng.NextDouble() * 16000.0 - 8000.0));
                Vector2Int c = Beat07_Reveal.CellOf(p);
                float d = (p - Beat07_Reveal.CellCentre(c.x, c.y)).magnitude;
                for (int dq = -1; dq <= 1; dq++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    float dn = (p - Beat07_Reveal.CellCentre(c.x + dq, c.y + dr)).magnitude;
                    Assert.That(d, Is.LessThanOrEqualTo(dn + 1e-2f), "point " + p + " is nearer to cell " + (c.x + dq) + "," + (c.y + dr));
                }
                Assert.That(MeshKit.HexApothemOf(Beat07_Reveal.CellCentre(c.x, c.y), p), Is.LessThanOrEqualTo(0.5f * AbyssConst.LATTICE_PITCH + 0.01f),
                    "a point lies within the half-pitch hexagon of its cell");
            }
        }

        [Test]
        public void LampsSitAtTheCellCentreMinus269()
        {
            for (int q = -4; q <= 4; q++)
            for (int r = -4; r <= 4; r++)
            {
                Vector2 l = Beat07_Reveal.LampXZ(q, r), c = Beat07_Reveal.CellCentre(q, r);
                Assert.That(l.x, Is.EqualTo(c.x).Within(1e-4f));
                Assert.That(l.y - c.y, Is.EqualTo(-269f).Within(1e-3f));
            }
            // the lamp is the bridge midpoint (near flat + 6 .. + 156), and in S0 it is the middle lamp of the bridge (z = 141)
            float mid = 0.5f * (Beat07_Reveal.HONEY_BRIDGE_NEAR + Beat07_Reveal.HONEY_BRIDGE_FAR) - AbyssConst.SHAFT_APOTHEM;
            Assert.That(mid, Is.EqualTo(-Beat07_Reveal.HONEY_LAMP_OFFSET).Within(1e-4f), "bridge midpoint");
            Assert.That(Beat07_Reveal.LampXZ(0, 0).y, Is.EqualTo(AbyssConst.BRIDGE_LAMP_Z[2]).Within(1e-3f), "S0's middle bridge lamp");
            Assert.That(Beat07_Reveal.HONEY_BRIDGE_FAR - Beat07_Reveal.HONEY_BRIDGE_NEAR, Is.EqualTo(AbyssConst.BRIDGE_L).Within(1e-4f), "bridge 150 m");
            Assert.That(Beat07_Reveal.HONEY_DECK_TOP_Y, Is.EqualTo(-1089f).Within(1e-4f), "deck y -1089 (14.5.7)");

            // the lamp (Glow sprite) stands 1.1 m above the deck at the bridge midpoint; the built deck spans the lamp's z
            for (int q = -3; q <= 3; q++)
            {
                Vector3 lw = Beat07_Reveal.LampWorld(q, 1 - q);
                Vector2 c = Beat07_Reveal.CellCentre(q, 1 - q);
                Assert.That(lw.y, Is.EqualTo(Beat07_Reveal.HONEY_DECK_TOP_Y + Beat07_Reveal.HONEY_LAMP_HEIGHT).Within(1e-4f), "lamp 1.1 m above the deck");
                Assert.That(lw.z - c.y, Is.EqualTo(-Beat07_Reveal.HONEY_LAMP_OFFSET).Within(1e-3f));
            }
            Beat07_Reveal b = Build(BeatTier.Full);
            Bounds deck = FindMesh(b, "HoneyT0Steel").bounds;
            Assert.That(deck.min.z, Is.LessThan(-Beat07_Reveal.HONEY_LAMP_OFFSET));
            Assert.That(deck.max.z, Is.GreaterThan(-Beat07_Reveal.HONEY_LAMP_OFFSET));
            Assert.That(Mathf.Abs(deck.center.x), Is.LessThan(1e-3f), "deck on the cell's z axis under the lamp");
        }

        [Test]
        public void CellCountInside28KmIsAbout4920()
        {
            int n = Beat07_Reveal.CountCellsWithin(AbyssConst.SHAFT_CENTRE, AbyssConst.HONEY_TIER2);
            Assert.That(n, Is.EqualTo(AbyssConst.WORLD_CELLS_28KM).Within(25), "cells inside 28 km (4 923.6 by area)");
            double byArea = System.Math.PI * AbyssConst.HONEY_TIER2 * AbyssConst.HONEY_TIER2 / (0.5 * System.Math.Sqrt(3.0) * AbyssConst.LATTICE_PITCH * AbyssConst.LATTICE_PITCH);
            Assert.That(byArea, Is.EqualTo(4923.6).Within(0.5), "14.7.7 area estimate");
            Assert.That(n, Is.EqualTo(byArea).Within(0.01 * byArea));
            // tier 0 holds the 'about 16' cells within 1.6 km of the balcony (S0 excluded)
            var near = new List<Vector2Int>();
            Beat07_Reveal.CellsInRange(new Vector2(AbyssConst.W[17].x, AbyssConst.W[17].z), 0f, AbyssConst.HONEY_TIER0, near);
            Assert.That(near.Count, Is.InRange(12, 20), "cells within 1.6 km");
        }

        [Test]
        public void TierSetsCoverEveryTerracePixelOfTheWalk()
        {
            float r0 = Beat07_Reveal.CELL_CIRCUMRADIUS, m = Beat07_Reveal.HONEY_MARGIN;
            var t0 = new HashSet<Vector2Int>();
            var t1 = new HashSet<Vector2Int>();
            var tmp = new List<Vector2Int>();
            Beat07_Reveal.CellsInRange(Beat07_Reveal.HONEY_REF, 0f, Beat07_Reveal.TIER0_FADE_END + r0 + m, tmp);
            foreach (Vector2Int c in tmp) t0.Add(c);
            tmp.Clear();
            Beat07_Reveal.CellsInRange(Beat07_Reveal.HONEY_REF, Beat07_Reveal.TIER0_FADE_START - r0 - m, Beat07_Reveal.TIER1_SPLIT, tmp);
            Beat07_Reveal.CellsInRange(Beat07_Reveal.HONEY_REF, Beat07_Reveal.TIER1_SPLIT, Beat07_Reveal.TIER2_FADE_END + r0 + m, tmp);
            foreach (Vector2Int c in tmp) t1.Add(c);
            Assert.AreEqual(tmp.Count, t1.Count, "the two tier-1 sets overlap");
            // the tier-1 rib cell set (no fade) lies beyond every cell that takes part in the 1.6 km hand-over (those draw the
            // tier-0 tube with its own ribs) and ends before the lattice band
            Assert.That(Beat07_Reveal.TIER1_RIBS_START, Is.GreaterThanOrEqualTo(Beat07_Reveal.TIER0_FADE_END + 2f * (r0 + m)));
            Assert.That(Beat07_Reveal.TIER1_RIBS_END, Is.LessThan(AbyssConst.HONEY_TIER2_START), "ribs end before the lattice band");

            float[] camZ = { PathGuide.Beat7Z(AbyssConst.D_EXTERIOR_ON), AbyssConst.W[16].z, AbyssConst.W[17].z, AbyssConst.GOAL_DOOR_Z };
            foreach (float cz in camZ)
            {
                var cam = new Vector2(AbyssConst.W[15].x, cz);
                for (int a = 0; a < 360; a += 3)
                {
                    var dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad));
                    for (float r = 25f; r <= Beat07_Reveal.TIER2_FADE_END; r += 25f)
                    {
                        Vector2Int c = Beat07_Reveal.CellOf(cam + dir * r);
                        if (c == Vector2Int.zero) continue;   // S0: the tower
                        if (r <= Beat07_Reveal.TIER0_FADE_END)
                            Assert.IsTrue(t0.Contains(c), "tier 0 misses cell " + c + " at " + r + " m from z " + cz);
                        if (r >= Beat07_Reveal.TIER0_FADE_START)
                            Assert.IsTrue(t1.Contains(c), "tier 1 misses cell " + c + " at " + r + " m from z " + cz);
                    }
                }
            }
        }

        [Test]
        public void UnfadedHoneycombPartsStayUnderTheLatticePlane()
        {
            // the parts drawn without a distance fade must lie below the lattice plane (0.6 m under the web tops), so that the
            // lattice covers them wherever it is kept; the faded tops reach the web tops
            Beat07_Reveal b = Build(BeatTier.Full);
            float plane = -Beat07_Reveal.LATTICE_DROP;
            foreach (string name in new[] { "HoneyT0Deep", "HoneyWalls", "HoneyT1Extras", "HoneyT1Ribs", "HoneyT0Steel", "HoneyCap" })
                Assert.That(FindMesh(b, name).bounds.max.y, Is.LessThan(plane - 0.01f), name + " reaches above the lattice plane");
            Bounds top = FindMesh(b, "HoneyCellTop").bounds;
            Assert.That(top.max.y, Is.EqualTo(0f).Within(1e-4f), "web tops on the terrace plane");
            Assert.That(top.min.y, Is.EqualTo(-Beat07_Reveal.HONEY_TOP_BAND).Within(1e-4f));
            Assert.That(FindMesh(b, "HoneyWalls").bounds.min.y, Is.EqualTo(-Beat07_Reveal.HONEY_TUBE_DEPTH).Within(0.01f), "tubes 600 m deep");

            var r = new ScaleReport();
            b.ReportScale(r);
            Assert.That(Get(r, "honeycomb.rings"), Is.EqualTo(10), "ribs every 60 m (10 rings)");
            Assert.That(Get(r, "honeycomb.tube.depth"), Is.EqualTo(Beat07_Reveal.HONEY_TUBE_DEPTH).Within(0.01));
            Assert.That(Get(r, "honeycomb.bridge.length"), Is.EqualTo(AbyssConst.BRIDGE_L).Within(0.01));
            Assert.That(Get(r, "honeycomb.deck.y"), Is.EqualTo(Beat07_Reveal.HONEY_DECK_TOP_Y).Within(0.05));
            Assert.That(Get(r, "honeycomb.tier0.cells"), Is.InRange(14, 40));
            Assert.That(Get(r, "honeycomb.tier1.cells"), Is.InRange(200, 400), "about 210 cells in 1.6-6 km");
            Assert.That(Get(r, "honeycomb.lamps"), Is.GreaterThan(150), "a lamp in every shaft out to the lattice band");
        }

        // ---------------------------------------------------------------- QA hook and ending

        [Test]
        public void TierOverrideSwitchesBetweenTier1AndTheLattice()
        {
            float saved = QaHooks.TierOverride;
            try
            {
                Beat07_Reveal b = Build(BeatTier.Full);
                Transform lattice = b.transform.Find("Geometry/Honeycomb/HoneycombLattice");
                Assert.IsNotNull(lattice, "no lattice renderer");
                Material mat = lattice.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.IsNotNull(lattice.GetComponent<BackdropRig>(), "the lattice follows the camera with locked rotation");
                var f = new BeatFrame { D = 760f, E = 881f, CameraPos = new Vector3(-45f, -485.3f, -12f) };

                QaHooks.TierOverride = -1f; b.Tick(in f);
                Assert.IsTrue(lattice.gameObject.activeSelf);
                Assert.That(mat.GetVector(DistFadeId).z, Is.EqualTo(1f), "lattice is the far tier");
                QaHooks.TierOverride = 1f; b.Tick(in f);
                Assert.IsTrue(lattice.gameObject.activeSelf, "tier 1 only: the lattice stays beyond tier 1's reach");
                Assert.That(mat.GetVector(DistFadeId).x, Is.GreaterThan(Beat07_Reveal.TIER2_FADE_END), "tier 1 only: no lattice inside 6.6 km");
                QaHooks.TierOverride = 0f; b.Tick(in f);
                Assert.IsTrue(lattice.gameObject.activeSelf, "tier 2 only: lattice on");
                Assert.That(mat.GetVector(DistFadeId).z, Is.EqualTo(0f), "tier 2 only: lattice unfaded");
                QaHooks.TierOverride = -1f; b.Tick(in f);
                Assert.That(mat.GetVector(DistFadeId).x, Is.EqualTo(AbyssConst.HONEY_TIER2_START).Within(1e-3f), "fade restored");
            }
            finally { QaHooks.TierOverride = saved; }
        }

        [Test]
        public void GoalWithin3MetresOpensTheLeafAndTheSliverSlidesOntoThePier()
        {
            // lead decision D2 (R2-05): the trigger is 3.0 m from the door plane (z <= -71.0); the leaf opens 12 degrees over
            // 3 s, the door spot (I 14) follows the opening and its aim slides from the sill out onto the pier, and the spill
            // light behind the latch-side gap follows the opening too
            Vector3 pos0 = GameState.PlayerPos;
            bool goal0 = GameState.GoalReached;
            double goalT0 = GameState.GoalRealTime, real0 = SceneClock.RealTime;
            try
            {
                Assert.That(AbyssConst.GOAL_DOOR_Z - Beat07_Reveal.GOAL_TRIGGER_Z, Is.EqualTo(-3.0f).Within(1e-4f), "D2: 3.0 m from the door plane");
                Beat07_Reveal b = Build(BeatTier.Full);
                Transform leaf = b.transform.Find("Geometry/Door/LeafHinge");
                Light spot = b.transform.Find("Lights/DoorSpot").GetComponent<Light>();
                Light spill = b.transform.Find("Lights/DoorSpill").GetComponent<Light>();
                Assert.That(spot.spotAngle, Is.EqualTo(60f).Within(1e-3f), "door spot angle 60 (14.5.7)");
                Assert.That(spot.range, Is.EqualTo(14f).Within(1e-3f), "door spot range 14 (14.5.7)");
                GameState.GoalReached = false;
                GameState.GoalRealTime = -1;
                var f = new BeatFrame { D = 800f, E = 921f, CameraPos = new Vector3(-45f, -485.3f, -71f) };

                SceneClock.RealTime = 100.0;
                GameState.PlayerPos = new Vector3(-45f, AbyssConst.W[18].y, Beat07_Reveal.GOAL_TRIGGER_Z + 0.1f);
                b.Tick(in f);
                Assert.IsFalse(GameState.GoalReached, "more than 3.0 m from the door (no walker)");
                Assert.That(spot.intensity, Is.EqualTo(0f), "door spot dark while closed");
                Assert.That(spill.intensity, Is.EqualTo(0f), "spill dark while closed");
                float zShut = AimOnFloor(spot).z;

                GameState.PlayerPos = new Vector3(-45f, AbyssConst.W[18].y, Beat07_Reveal.GOAL_TRIGGER_Z - 0.05f);
                b.Tick(in f);
                Assert.IsTrue(GameState.GoalReached, "goal at z <= -71.0");
                Assert.That(GameState.GoalRealTime, Is.EqualTo(100.0).Within(1e-9));

                SceneClock.RealTime = 103.0;
                b.Tick(in f);
                Assert.That(Mathf.DeltaAngle(0f, leaf.localEulerAngles.y), Is.EqualTo(-12f).Within(0.01f), "leaf open 12 degrees after 3 s");
                Assert.That(spot.intensity, Is.EqualTo(14f).Within(1e-3f), "door spot I 14 when open");
                Assert.That(spill.intensity, Is.GreaterThan(0f), "light spills through the gap when open");
                float zOpen = AimOnFloor(spot).z;
                float zFront = AbyssConst.W[18].z + 0.6f;
                Assert.That(zShut, Is.LessThan(zFront), "shut: the spot aims at the sill inside the recess");
                Assert.That(zOpen, Is.GreaterThan(zFront + 2f), "open: the pool has slid out onto the pier toward the camera");
            }
            finally
            {
                GameState.PlayerPos = pos0;
                GameState.GoalReached = goal0;
                GameState.GoalRealTime = goalT0;
                SceneClock.RealTime = real0;
            }
        }

        // ---------------------------------------------------------------- 14.10 composition lint

        [TestCase(BeatTier.Full)]
        [TestCase(BeatTier.Graybox)]
        public void KeyShotSubjectsPassTheCompositionLint(BeatTier tier)
        {
            // the lint as the capture harness runs it (QaCaptureRunner -> CompositionLint): every subject of every beat-7 key
            // shot through the 14.10 pose at every capture size (1080p, 4K where marked, 16:10, 21:9), centre inside 5-95 % and
            // at least the subject's minimum size
            Beat07_Reveal b = Build(tier);
            var specs = new List<ShotSpec>();
            var subjects = new List<KeyShotSubject>();
            int shots = 0;
            for (int k = 0; k < KeyShots.All.Length; k++)
            {
                if (KeyShots.All[k].Beat != 7) continue;
                specs.Clear();
                KeyShots.AddKeyVariants(specs, k, false, 0, 0);
                for (int i = 0; i < specs.Count; i++)
                {
                    ShotSpec s = specs[i];
                    subjects.Clear();
                    b.CollectSubjects(s.BaseTag, subjects);
                    Assert.Greater(subjects.Count, 0, s.Tag + " has no subjects");
                    for (int j = 0; j < subjects.Count; j++)
                    {
                        KeyShotSubject subj = subjects[j];
                        CompositionLint.SubjectResult r = CompositionLint.Check(in subj, s.Eye, s.Rot, s.Fov, s.AspectRatio, AbyssConst.CAMERA_NEAR);
                        string what = tier + " " + s.Tag + " / " + subj.Name + ": rect (" + r.X0.ToString("F3") + ", " + r.Y0.ToString("F3") + ", "
                                      + r.X1.ToString("F3") + ", " + r.Y1.ToString("F3") + ")";
                        Assert.IsFalse(r.Behind, what + " behind the camera");
                        Assert.IsTrue(r.CentreOk, what + " centre (" + r.Cx.ToString("F3") + ", " + r.Cy.ToString("F3") + ") outside 5-95 %");
                        Assert.IsTrue(r.SizeOk, what + " size " + r.Size.ToString("F3") + " under " + r.MinSize.ToString("F3"));
                        Assert.IsTrue(r.Pass, what + " fails the lint");
                        if (s.BaseTag == "KS7b_honeycomb" && subj.Name == "balcony front edge and kerb")
                        {
                            // 14.5.7: the edge sits at 90 % of the frame height (foreground in the bottom 10 %), inside the 3 % margin
                            Assert.That(r.Cy, Is.InRange(0.85f, 0.95f), what + ": the kerb is not in the bottom 10 %");
                            Assert.IsTrue(r.MarginOk, what + " clipped by the frame");
                        }
                    }
                    shots++;
                }
            }
            Assert.AreEqual(14, shots, "KS7a (3 sizes), KS7b (4), KS7c (4), KS7d (3)");
        }

        // ---------------------------------------------------------------- cross-beat seams

        [Test]
        public void NoFaceLiesOnBeat02sNearFlatWithItsFacing()
        {
            // Beat02's near flat (z = SHAFT_NEAR_Z) is solid under the tunnel mouth (hole y >= -487). A beat-7 face on that plane
            // with the same facing would z-fight with it (the tunnel slab's shaft end did). Faces within 5 mm count as on it.
            const float tol = 0.005f;
            float zN = AbyssConst.SHAFT_NEAR_Z;
            Vector3 mouth = AbyssConst.W[15];

            BuildBeat02(BeatTier.Graybox, out Transform beat02);
            var wall = new List<PlaneTri>();
            CollectPlaneTris(beat02, zN, 1e-3f, wall);
            // beside the gantry (|x - X| > 1.5) and under the mouth: the strip the old slab end fought with
            var probe = new Vector2(mouth.x + 0.5f * AbyssConst.TUNNEL_W - 0.1f, mouth.y - 0.15f);
            int facing = 0;
            for (int i = 0; i < wall.Count && facing == 0; i++) if (wall[i].Contains(probe)) facing = wall[i].Facing;
            Assert.AreNotEqual(0, facing, "Beat02 has no near-flat face under the tunnel mouth (test premise)");

            foreach (BeatTier tier in new[] { BeatTier.Full, BeatTier.Graybox })
            {
                Beat07_Reveal b = Build(tier);
                var mine = new List<PlaneTri>();
                CollectPlaneTris(b.transform, zN, tol, mine);
                for (int i = 0; i < mine.Count; i++)
                {
                    PlaneTri t = mine[i];
                    if (t.Facing != facing) continue;
                    for (int j = 0; j < wall.Count; j++)
                        if (wall[j].Facing == facing && PlaneTri.Overlap(t, wall[j]))
                            Assert.Fail(tier + ": " + t.Name + " has a face on z = " + zN + " facing " + (facing > 0 ? "+Z" : "-Z") + " over Beat02's "
                                        + wall[j].Name + " " + t);
                }
                Bounds slab = FindMesh(b, "TunnelFloor").bounds;
                Assert.That(slab.max.z, Is.LessThanOrEqualTo(zN - tol), tier + ": the tunnel slab reaches the near-flat plane");
                Assert.That(slab.size.z, Is.EqualTo(AbyssConst.TUNNEL_L).Within(0.1f), tier + ": tunnel 60.0 +/- 0.1");
            }
        }

        // ---------------------------------------------------------------- helpers

        void BuildBeat02(BeatTier tier, out Transform root)
        {
            var go = new GameObject("Beat07Test_Beat02_" + tier);
            _roots.Add(go);
            var b = go.AddComponent<Beat02_Bridge>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(go.transform, false);
            b.Build(new BeatContext { Root = go.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = true });
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            root = go.transform;
        }

        /// <summary>A world triangle lying on a z-plane: its (x, y) corners and its facing (+1 front toward +Z, -1 toward -Z).</summary>
        struct PlaneTri
        {
            public Vector2 A, B, C;
            public int Facing;
            public string Name;

            static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

            public bool Contains(Vector2 p)
            {
                float d0 = Side(A, B, p), d1 = Side(B, C, p), d2 = Side(C, A, p);
                return (d0 >= 0f && d1 >= 0f && d2 >= 0f) || (d0 <= 0f && d1 <= 0f && d2 <= 0f);
            }

            /// <summary>Separating-axis test: true when the two triangles share an area (more than touching along an edge).</summary>
            public static bool Overlap(in PlaneTri p, in PlaneTri q)
            {
                const float eps = 1e-4f;
                for (int t = 0; t < 2; t++)
                {
                    PlaneTri s = t == 0 ? p : q;
                    for (int e = 0; e < 3; e++)
                    {
                        Vector2 a = e == 0 ? s.A : (e == 1 ? s.B : s.C), b = e == 0 ? s.B : (e == 1 ? s.C : s.A);
                        var axis = new Vector2(a.y - b.y, b.x - a.x);
                        float len = axis.magnitude;
                        if (len < 1e-9f) continue;
                        axis /= len;
                        Project(p, axis, out float p0, out float p1);
                        Project(q, axis, out float q0, out float q1);
                        if (p1 <= q0 + eps || q1 <= p0 + eps) return false;
                    }
                }
                return true;
            }

            static void Project(in PlaneTri t, Vector2 axis, out float lo, out float hi)
            {
                float a = Vector2.Dot(t.A, axis), b = Vector2.Dot(t.B, axis), c = Vector2.Dot(t.C, axis);
                lo = Mathf.Min(a, Mathf.Min(b, c));
                hi = Mathf.Max(a, Mathf.Max(b, c));
            }

            public override string ToString() => "(" + A + ", " + B + ", " + C + ")";
        }

        /// <summary>
        /// Every triangle of the MeshFilters under root whose three world vertices lie within tol of z = planeZ and whose plane is
        /// parallel to it, with its facing from the winding (Unity: front face clockwise, normal = (b - a) x (c - a)).
        /// </summary>
        static void CollectPlaneTris(Transform root, float planeZ, float tol, List<PlaneTri> outList)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = mf.sharedMesh;
                if (m == null) continue;
                Matrix4x4 w = mf.transform.localToWorldMatrix;
                Bounds lb = m.bounds;
                float zMin = float.MaxValue, zMax = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f));
                    float z = w.MultiplyPoint3x4(c).z;
                    zMin = Mathf.Min(zMin, z);
                    zMax = Mathf.Max(zMax, z);
                }
                if (zMax < planeZ - tol || zMin > planeZ + tol) continue;

                Vector3[] v = m.vertices;
                int[] tris = m.triangles;
                var world = new Vector3[v.Length];
                var on = new bool[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    world[i] = w.MultiplyPoint3x4(v[i]);
                    on[i] = Mathf.Abs(world[i].z - planeZ) < tol;
                }
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                    if (!on[i0] || !on[i1] || !on[i2]) continue;
                    Vector3 a = world[i0], b = world[i1], c = world[i2];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    float nl = n.magnitude;
                    if (nl < 1e-10f || Mathf.Abs(n.z) < 0.9f * nl) continue;
                    outList.Add(new PlaneTri
                    {
                        A = new Vector2(a.x, a.y), B = new Vector2(b.x, b.y), C = new Vector2(c.x, c.y),
                        Facing = n.z > 0f ? 1 : -1, Name = mf.name,
                    });
                }
            }
        }

        // ---------------------------------------------------------------- look-dev guards (reveal r01-r02)

        [Test]
        public void HandOverCellsDrawTheTier0MeshesInBothTiers()
        {
            // the cells that meet the 1.6 km dithered hand-over swap identical geometry, so the per-pixel fade leaves no stipple
            Beat07_Reveal b = Build(BeatTier.Full);
            List<InstancedBatch> deep = Batches(b, "Beat07.HoneyT1HandDeep");
            InstancedBatch steel = b.FindHoneyBatch("Beat07.HoneyT1HandSteel");
            Assert.IsNotNull(steel, "hand-over steel batch");
            Assert.That(CountOf(deep), Is.GreaterThan(0));
            Assert.That(steel.Count, Is.EqualTo(CountOf(deep)));
            Assert.AreSame(FindMesh(b, "HoneyT0Steel"), steel.Mesh, "far tier of the hand-over cells = the tier-0 bridge");
            Vector4 fade = deep[0].Material.GetVector("_DistFade");
            foreach (InstancedBatch d in deep)
            {
                Assert.AreSame(FindMesh(b, "HoneyT0Deep"), d.Mesh, "far tier of the hand-over cells = the tier-0 tube");
                Vector4 f = d.Material.GetVector("_DistFade");
                Assert.That(f.z, Is.EqualTo(1f), "far-tier fade");
                Assert.That(f.x, Is.EqualTo(Beat07_Reveal.TIER0_FADE_START).Within(0.5f));
                Assert.That(f.y, Is.EqualTo(Beat07_Reveal.TIER0_FADE_END).Within(0.5f));
            }
            foreach (InstancedBatch t0 in Batches(b, "Beat07.HoneyT0Deep"))
            {
                Vector4 near = t0.Material.GetVector("_DistFade");
                Assert.That(near.z, Is.EqualTo(-1f), "near-tier fade");
                Assert.That(near.x, Is.EqualTo(fade.x)); Assert.That(near.y, Is.EqualTo(fade.y));
            }
            Assert.IsNull(b.FindHoneyBatch("Beat07.HoneyT1ExtrasNear"), "extras near are replaced by the tier-0 meshes");
            // nothing else of tier 1 dissolves on its own: every tier-1 part below the lattice plane is unfaded (the plane and
            // the 5.4-6.6 km top fade hand it to the lattice), only the tops and the hand-over cells carry a distance fade
            foreach (string n in new[] { "Beat07.HoneyT1InExtras", "Beat07.HoneyT1OutExtras", "Beat07.HoneyT1Ribs", "Beat07.HoneyT1OutWalls" })
                foreach (InstancedBatch x in Batches(b, n))
                    Assert.That(x.Material.GetVector("_DistFade").z, Is.EqualTo(0f), x.Name + " is not distance-faded");
        }

        [Test]
        public void HoneycombAirIsOneVectorAndThePitsFallIntoDarkness()
        {
            // R2-04 (1) / (3): one _HoneyAir on every honeycomb variant and the Lattice (a continuous function of camera distance
            // and depth: no per-cell steps, 14.10 c by construction); no additive lift on any surface
            Beat07_Reveal b = Build(BeatTier.Full);
            Vector4 air = Beat07_Reveal.HONEY_AIR;
            foreach (string n in new[] { "Beat07.HoneyT0Top", "Beat07.HoneyT0Walls", "Beat07.HoneyT0Deep", "Beat07.HoneyT0Steel", "Beat07.HoneyT1InTop",
                                         "Beat07.HoneyT1InWalls", "Beat07.HoneyT1HandDeep", "Beat07.HoneyT1InExtras", "Beat07.HoneyT1Ribs",
                                         "Beat07.HoneyT1OutTop", "Beat07.HoneyT1OutWalls", "Beat07.HoneyT1OutExtras", "Beat07.HoneyT1OutSteel" })
            {
                List<InstancedBatch> l = Batches(b, n);
                Assert.That(l.Count, Is.EqualTo(1), n + ": one batch (no distance bands)");
                Assert.AreEqual(air, l[0].Material.GetVector("_HoneyAir"), n + " air");
                Assert.That(l[0].Material.GetFloat("_EmissionStrength"), Is.EqualTo(0f), n + ": no additive lift");
            }
            Assert.AreEqual(air, b.LatticeMaterial.GetVector("_HoneyAir"), "the Lattice breathes the same air (14.10 c)");
            // the veil: 8-16 % at the 0.8 km near cells, saturating at most 25 % (the 5-9 km cells keep their contrast)
            float Veil(float d) => air.x * (1f - Mathf.Exp(-d / air.y));
            Assert.That(Veil(800f), Is.InRange(0.08f, 0.16f), "near cells veiled toward the haze");
            Assert.That(Veil(3000f), Is.GreaterThan(Veil(800f) + 0.04f), "the field recedes with distance");
            Assert.That(air.x, Is.LessThanOrEqualTo(0.25f), "far veil capped");
            // CPU model of a near pit wall (shadowed, lit tone 1): sky-light AO then the tube air toward the dim haze; the deep
            // floor stays darker than the wall under the lip and than every ring of the top 200 m (no lit "tray floor")
            float haze = Beat07_Reveal.TUBE_AIR_K;   // relative to OUT_HORIZ; a shadowed wall top is about 0.35 x OUT_HORIZ
            float Pit(float depth, float ao) => Mathf.Lerp(0.35f * ao, haze, air.w * (1f - Mathf.Exp(-depth / air.z)));
            float floor = Pit(500f, Beat07_Reveal.DepthAO(500f));
            Assert.That(floor, Is.LessThan(0.5f * haze), "the deep pit falls well under the tube-air haze tone");
            for (float d = 30f; d <= 200f; d += 60f)
                Assert.That(Pit(d, Beat07_Reveal.RingAO(d)), Is.GreaterThan(floor * 1.5f), "ring at " + d + " m stands above the deep floor");
            Assert.That(Pit(10f, Beat07_Reveal.DepthAO(10f)), Is.GreaterThan(3f * floor), "lit lip over a dark well");
        }

        [Test]
        public void WalkToppingIsTheBrightestNearLayer()
        {
            // R2-04 (2): the sunlit balcony and pier toppings sit above the web tops (same sun, unfogged), the kerbs stay darker
            Beat07_Reveal b = Build(BeatTier.Full);
            Material walk = null, kerb = null;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "WalkTopping") walk = r.sharedMaterial;
                if (r.name == "Kerbs") kerb = r.sharedMaterial;
            }
            Assert.IsNotNull(walk); Assert.IsNotNull(kerb);
            float web = AbyssPalette.CONC_LIGHT.linear.g * Beat07_Reveal.WEB_TOP_GAIN;
            Assert.That(walk.GetColor("_BaseColor").linear.g, Is.GreaterThan(1.1f * web), "topping albedo above the web tops");
            Assert.That(kerb.GetColor("_BaseColor").linear.g, Is.LessThan(0.6f * walk.GetColor("_BaseColor").linear.g), "dark kerb lines frame the walk");
        }

        [Test]
        public void HoneycombTiersShareTheConcreteAndTheLampsStayAmber()
        {
            Beat07_Reveal b = Build(BeatTier.Full);
            Material lattice = b.LatticeMaterial;
            Assert.IsNotNull(lattice);
            // 14.10 (c): tube walls = the lattice's wall albedo; web tops = the lattice's webs, whose lit rim (x 1.35) covers the
            // fraction of the 30 m half web that gives the same mean gain as the tier-0 / tier-1 top meshes (WEB_TOP_GAIN)
            Color wall = lattice.GetColor("_BaseColor").linear;
            Vector4 lp = lattice.GetVector("_LatticeParams");
            float halfWeb = 0.5f * (AbyssConst.LATTICE_PITCH - AbyssConst.SHAFT_FLAT_TO_FLAT);
            Assert.That(1f + 0.35f * lp.z / halfWeb, Is.EqualTo(Beat07_Reveal.WEB_FAR_GAIN).Within(1e-3f), "lattice mean web gain = the tops' far gain");
            Assert.That(Beat07_Reveal.WEB_TOP_GAIN, Is.LessThan(Beat07_Reveal.WEB_FAR_GAIN), "near webs subdued under the pier (R2-04)");
            Assert.That(lp.y, Is.EqualTo(AbyssConst.SHAFT_APOTHEM).Within(1e-3f));
            Assert.That(lp.w, Is.EqualTo(Beat07_Reveal.HONEY_LAMP_OFFSET).Within(1e-3f));
            Assert.That(lp.x, Is.EqualTo((float)Beat07_Reveal.HexSize).Within(1e-3f));
            foreach (string n in new[] { "Beat07.HoneyT0Top", "Beat07.HoneyT1InTop", "Beat07.HoneyT1OutTop", "Beat07.HoneyT0Walls", "Beat07.HoneyT1InWalls", "Beat07.HoneyT1OutWalls" })
            {
                foreach (InstancedBatch bt in Batches(b, n))
                {
                    // near albedo CONC_LIGHT (x the web gain on the tops); beyond 3.2 km every surface blends to the far albedo
                    // (FAR_ALBEDO_GAIN, R2-04 (4)), which is the Lattice's (14.10 c)
                    Color c = bt.Material.GetColor("_BaseColor").linear;
                    bool top = n.EndsWith("Top");
                    float g = top ? Beat07_Reveal.WEB_TOP_GAIN : 1f, k = Beat07_Reveal.FAR_ALBEDO_GAIN;
                    Assert.That(Mathf.Abs(c.r - g * wall.r / k) + Mathf.Abs(c.g - g * wall.g / k) + Mathf.Abs(c.b - g * wall.b / k), Is.LessThan(2e-3f), bt.Name + " near albedo");
                    Color far = bt.Material.GetColor("_FarTint").linear;
                    float gf = top ? Beat07_Reveal.WEB_FAR_GAIN : Beat07_Reveal.WALL_FAR_TINT;
                    Assert.That(Mathf.Abs(far.r - gf * wall.r) + Mathf.Abs(far.g - gf * wall.g) + Mathf.Abs(far.b - gf * wall.b), Is.LessThan(2e-3f), bt.Name + " far albedo matches the lattice (14.10 c)");
                    Assert.That(bt.Material.GetVector("_FarRange").y, Is.LessThanOrEqualTo(AbyssConst.HONEY_TIER2_START), bt.Name + " far albedo before 5.4 km");
                    if (!top) continue;
                    // R2-19 (2): weathered macro tone on the webs, blended to the plain web tone before the lattice hand-over
                    Assert.That(bt.Material.GetFloat("_MacroAmp"), Is.GreaterThan(1.5f), bt.Name + " macro tone");
                    Vector4 fr = bt.Material.GetVector("_FarRange");
                    Assert.That(fr.y, Is.GreaterThan(fr.x)); Assert.That(fr.y, Is.LessThanOrEqualTo(AbyssConst.HONEY_TIER2_START), bt.Name + " plain before 5.4 km");
                    Color ft = bt.Material.GetColor("_FarTint").linear;
                    Assert.That(Mathf.Abs(ft.g - Beat07_Reveal.WEB_FAR_GAIN * wall.g), Is.LessThan(2e-3f), bt.Name + " far tint = the lattice web");
                }
            }
            List<InstancedBatch> rings = Batches(b, "Beat07.HoneyLamps");
            InstancedBatch lamps = rings[0];
            foreach (Material m in new[] { lamps.Material, lattice })
            {
                Color e = m.GetColor("_EmissionColor");
                Assert.That(e.r, Is.GreaterThan(e.g)); Assert.That(e.g, Is.GreaterThan(e.b));
                Assert.That(e.b / e.r, Is.LessThan(0.2f), m.name + ": amber hue (linear AMBER), not its sRGB triple");
                Assert.That(m.GetFloat("_EmissionStrength"), Is.LessThanOrEqualTo(Beat07_Reveal.AMBER_DOT_CAP), m.name + ": D4 cap of small / distant amber emitters");
                Assert.That(m.GetFloat("_MinPixels"), Is.GreaterThanOrEqualTo(2.5f), m.name + ": sprites at least 2.5 px");
            }
            Color le = lattice.GetColor("_EmissionColor"), ge = lamps.Material.GetColor("_EmissionColor");
            Assert.That(Mathf.Abs(le.r - ge.r) + Mathf.Abs(le.g - ge.g) + Mathf.Abs(le.b - ge.b), Is.LessThan(1e-4f), "one lamp colour on both tiers");
            // R2-20: the halos are faint (at most 0.15) and 3-4 x the dot
            List<InstancedBatch> halos = Batches(b, "Beat07.HoneyHalos");
            Material hm = halos[0].Material;
            Assert.That(hm.GetFloat("_EmissionStrength"), Is.LessThanOrEqualTo(0.15f), "faint halo");
            Assert.That(hm.GetFloat("_MinPixels") / lamps.Material.GetFloat("_MinPixels"), Is.InRange(3f, 4f), "halo 3-4 x the dot");
            Assert.That(CountOf(halos), Is.GreaterThan(5), "halos on the near lamps");
        }

        [Test]
        public void WalkingConcreteStaysMatte()
        {
            // 14.2: concrete is matte; the floor recipe takes up to 0.12 + 0.04 off _Roughness along the worn path
            Beat07_Reveal b = Build(BeatTier.Full);
            int n = 0;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name != "WalkTopping") continue;
                n++;
                Assert.That(r.sharedMaterial.GetFloat("_Roughness") - 0.12f - 0.04f, Is.GreaterThanOrEqualTo(0.6f), "pier / balcony topping roughness");
            }
            Assert.That(n, Is.EqualTo(1), "one walking topping");
        }

        // ---------------------------------------------------------------- critique r1 guards (R1-03, 12, 13, 15, 21-24, 32, 34)

        [Test]
        public void NeighbourCellsHaveNoColumnStubs()
        {
            // R1-03: the 14.5.7 neighbour cell has no columns; inside the hole only the rings (from the wall), the landing at
            // the near flat and the bridge deck may stand
            // R2-30: plus the flat C1 landing pad at the deck's far end (no higher than the deck top, 1 m thick: not a peg)
            Beat07_Reveal b = Build(BeatTier.Full);
            float ringIn = Mathf.Min(AbyssConst.RIB_INNER_APOTHEM, AbyssConst.BELT_INNER_APOTHEM) - 0.01f;
            float nearFlat = -AbyssConst.SHAFT_APOTHEM;
            float deckTop = Beat07_Reveal.HONEY_DECK_TOP_Y - AbyssConst.TERRACE_Y;
            Vector2 pc = Beat07_Reveal.PadCentre;
            int padVerts = 0;
            foreach (string name in new[] { "HoneyT0Deep", "HoneyWalls", "HoneyT1Extras", "HoneyT1Ribs" })
            {
                foreach (Vector3 v in FindMesh(b, name).vertices)
                {
                    float apo = MeshKit.HexApothemOf(Vector2.zero, new Vector2(v.x, v.z));
                    bool landing = v.z <= nearFlat + AbyssConst.LANDING_L + 0.05f && Mathf.Abs(v.x) <= 0.5f * AbyssConst.LANDING_W + 0.05f;
                    bool pad = (new Vector2(v.x, v.z) - pc).magnitude <= AbyssConst.LEDGE_R_OUT + 0.01f;
                    if (pad && name != "HoneyWalls" && name != "HoneyT1Ribs")
                    {
                        padVerts++;
                        Assert.That(v.y, Is.LessThanOrEqualTo(deckTop - 0.04f), name + ": the pad stays under the deck top (no peg)");
                        Assert.That(v.y, Is.GreaterThanOrEqualTo(deckTop - Beat07_Reveal.PAD_DROP - Beat07_Reveal.PAD_THICK - 0.01f), name + ": a 1 m slab");
                        continue;
                    }
                    Assert.IsTrue(apo >= ringIn || (name == "HoneyT0Deep" && landing), name + ": vertex " + v + " stands inside the hole (column stub?)");
                }
            }
            Assert.That(padVerts, Is.GreaterThan(0), "every neighbour bridge ends on the C1 landing pad (R2-30)");
            // the deck reaches the pad (its far end lies inside the pad's radius)
            Bounds deck = FindMesh(b, "HoneyT0Steel").bounds;
            Assert.That(pc.y - deck.max.z, Is.LessThan(AbyssConst.LEDGE_R_OUT), "the thread ends on the pad");
            foreach (Vector3 v in FindMesh(b, "HoneyT0Steel").vertices)
                Assert.That(Mathf.Abs(v.x), Is.LessThan(0.61f), "the bridge is the 1.2 m deck alone (no posts, chord or lamp head)");
            Assert.That(FindMesh(b, "HoneyT0Steel").triangles.Length / 3, Is.EqualTo(24), "deck = two boxes either side of the lamp gap (R2-29)");
            foreach (Vector3 v in FindMesh(b, "HoneyT0Steel").vertices)
                Assert.That(Mathf.Abs(v.z + Beat07_Reveal.HONEY_LAMP_OFFSET), Is.GreaterThanOrEqualTo(Beat07_Reveal.DECK_LAMP_GAP - 1e-3f), "no deck under the lamp sprite");
        }

        [Test]
        public void HolesDarkenWithDepthAndCarryNoKeyLight()
        {
            // R1-03: below the web band every tube surface has sky-light AO exp(-depth / L) in vertex colour R and no Key; the
            // Lattice's analytic holes use the same L (14.10 c)
            // R2-04 (3): the rings (and the C1 pads) keep a longer sky-light falloff than the walls, so nested ring outlines
            // step down into the pits; the walls and the landing keep L
            Beat07_Reveal b = Build(BeatTier.Full);
            float L = Beat07_Reveal.HONEY_AO_FALLOFF, LR = Beat07_Reveal.HONEY_RING_AO_FALLOFF;
            Assert.That(L, Is.InRange(25f, 80f));
            Assert.That(LR, Is.InRange(100f, 120f), "ring top falloff 100-120 m (R2-04)");
            float nearFlat = -AbyssConst.SHAFT_APOTHEM;
            foreach (string name in new[] { "HoneyT0Deep", "HoneyWalls", "HoneyT1Extras", "HoneyT1Ribs" })
            {
                Mesh m = FindMesh(b, name);
                Vector3[] v = m.vertices;
                Color32[] c = m.colors32;
                Assert.AreEqual(v.Length, c.Length, name + " has vertex colours");
                for (int i = 0; i < v.Length; i++)
                {
                    bool landing = name == "HoneyT0Deep" && v[i].y > -6f && v[i].z <= nearFlat + AbyssConst.LANDING_L + 0.05f
                                   && Mathf.Abs(v[i].x) <= 0.5f * AbyssConst.LANDING_W + 0.05f;
                    float len = name == "HoneyWalls" || landing ? L : LR;
                    float expect = 255f * Mathf.Exp(-Mathf.Max(0f, -v[i].y) / len);
                    Assert.That(c[i].r, Is.EqualTo(expect).Within(1.01f), name + " AO at depth " + (-v[i].y));
                }
            }
            Assert.That(Beat07_Reveal.RingAO(143f) / Beat07_Reveal.DepthAO(143f), Is.GreaterThan(1.5f), "the first belt stands out of its wall");
            // the walls are subdivided so the AO gradient is smooth: no wall row taller than 0.75 L above 4 L depth
            var ys = new SortedSet<float>();
            foreach (Vector3 p in FindMesh(b, "HoneyWalls").vertices) ys.Add(Mathf.Round(p.y * 100f) / 100f);
            float prev = float.NaN;
            foreach (float y in ys)   // ascending: prev is the lower edge of the row
            {
                if (!float.IsNaN(prev) && -prev < 4f * L) Assert.That(y - prev, Is.LessThanOrEqualTo(0.75f * L), "wall row " + prev + " .. " + y);
                prev = y;
            }
            Assert.That(Beat07_Reveal.DepthAO(2f * L), Is.LessThan(0.14f), "two falloff lengths down the walls are under 14 % sky light");

            // rings and landing: no Key (the rib tops the walk sees lie in the shadow of the wall above)
            foreach (string n in new[] { "Beat07.HoneyT0Deep", "Beat07.HoneyT1Ribs", "Beat07.HoneyT1InExtras", "Beat07.HoneyT1OutExtras", "Beat07.HoneyT1HandDeep" })
            {
                foreach (InstancedBatch x in Batches(b, n))
                {
                    Assert.That(x.Material.GetFloat("_KeyLightMask"), Is.EqualTo(0f), x.Name + ": rings in the sun's shadow");
                    Assert.That(x.Material.GetFloat("_DepthFalloff"), Is.GreaterThan(1e6f), x.Name + ": no second (albedo) falloff");
                }
            }
            // walls: Key on, one mesh on every tier, the sun faded by the bent normals
            Mesh walls = FindMesh(b, "HoneyWalls");
            foreach (string n in new[] { "Beat07.HoneyT0Walls", "Beat07.HoneyT1InWalls", "Beat07.HoneyT1OutWalls" })
            {
                foreach (InstancedBatch x in Batches(b, n))
                {
                    Assert.AreSame(walls, x.Mesh, x.Name);
                    Assert.That(x.Material.GetFloat("_KeyLightMask"), Is.EqualTo(1f), x.Name);
                    Assert.That(x.Material.GetFloat("_DepthFalloff"), Is.GreaterThan(1e6f), x.Name);
                }
            }
            Vector3 sun = ProgressTables.ExtSunDir;
            Vector3[] wv = walls.vertices, wn = walls.normals;
            int bent = 0;
            for (int i = 0; i < wv.Length; i++)
            {
                Vector3 nh = new Vector3(wn[i].x, 0f, wn[i].z).normalized;
                float a = Vector3.Dot(nh, sun);
                if (a > 1e-3f)
                {
                    Assert.That(Vector3.Dot(wn[i], sun), Is.EqualTo(a * Beat07_Reveal.DepthAO(-wv[i].y)).Within(2e-3f), "sun on a sun-facing wall at depth " + (-wv[i].y));
                    if (wn[i].y < -1e-3f) bent++;
                }
                else Assert.That(Mathf.Abs(wn[i].y), Is.LessThan(1e-4f), "walls facing away from the sun keep their normal");
            }
            Assert.That(bent, Is.GreaterThan(0), "the sun-facing walls are bent");
            foreach (string n in new[] { "Beat07.HoneyT0Top", "Beat07.HoneyT1InTop", "Beat07.HoneyT1OutTop" })
                Assert.That(b.FindHoneyBatch(n).Material.GetFloat("_KeyLightMask"), Is.EqualTo(1f), n + ": web tops are sunlit");
            // R2-04: the near pits grade over L, the Lattice's far holes keep the short falloff; the tier-1 walls and rings
            // beyond 3.2 km are darkened to the Lattice's mean hole tone over the top 60 m (14.10 c)
            float LF = Beat07_Reveal.HONEY_FAR_FALLOFF;
            Assert.That(b.LatticeMaterial.GetFloat("_DepthFalloff"), Is.EqualTo(LF).Within(1e-3f), "lattice holes: the far falloff");
            Assert.That(LF, Is.LessThan(L));
            float meanFar = 0f, meanNear = 0f;
            for (int i = 0; i < 600; i++) { float d = (i + 0.5f) * 0.1f; meanFar += Mathf.Exp(-d / LF); meanNear += Mathf.Exp(-d / L); }
            Assert.That(Beat07_Reveal.WALL_FAR_TINT, Is.EqualTo(meanFar / meanNear).Within(2e-3f), "far tint = mean-tone match over 60 m");
        }

        [Test]
        public void NeighbourBridgesAreMatchedThinLineThreads()
        {
            // R1-12 / R1-13: one deck mesh on every tier, a ThinLine of at least 1.5 px with alpha-to-coverage, no specular
            // steel, the same material values in both tiers of the 1.6 km hand-over, gone before the 5.4 km lattice hand-over
            Beat07_Reveal b = Build(BeatTier.Full);
            Mesh deck = FindMesh(b, "HoneyT0Steel");
            var uv1 = new List<Vector4>();
            deck.GetUVs(1, uv1);
            Assert.AreEqual(deck.vertexCount, uv1.Count, "ThinLine UV1 on every deck vertex");
            foreach (Vector4 u in uv1) Assert.That(u.w, Is.GreaterThan(0.1f), "member radius in UV1.w");
            Material near = null;
            foreach (string n in new[] { "Beat07.HoneyT0Steel", "Beat07.HoneyT1HandSteel", "Beat07.HoneyT1InSteel", "Beat07.HoneyT1OutSteel" })
            {
                InstancedBatch x = b.FindHoneyBatch(n);
                Assert.IsNotNull(x, n);
                Assert.AreSame(deck, x.Mesh, n + " draws the one deck mesh");
                Material m = x.Material;
                Assert.That(m.GetFloat("_ThinLine"), Is.GreaterThanOrEqualTo(AbyssConst.MIN_LINE_PX), n);
                Assert.That(m.GetFloat("_AlphaToMask"), Is.EqualTo(1f), n + " alpha-to-coverage");
                Assert.That(m.GetFloat("_Metallic"), Is.LessThan(0.05f), n + ": no metallic sparkle");
                Vector4 fr = m.GetVector("_FarRange");
                Assert.That(fr.y, Is.GreaterThan(fr.x));
                Assert.That(fr.y, Is.LessThanOrEqualTo(AbyssConst.HONEY_TIER2_START), n + ": the thread has dimmed before the lattice hand-over");
                if (near == null) { near = m; continue; }
                foreach (string p in new[] { "_ThinLine", "_Roughness", "_MacroAmp", "_KeyLightMask", "_DepthFalloff" })
                    Assert.That(m.GetFloat(p), Is.EqualTo(near.GetFloat(p)), n + " " + p + " matches tier 0");
                Assert.That(m.GetColor("_BaseColor"), Is.EqualTo(near.GetColor("_BaseColor")), n + " colour matches tier 0");
                Assert.That(m.GetVector("_FarRange"), Is.EqualTo(near.GetVector("_FarRange")), n);
            }
        }

        [Test]
        public void LampsIgniteAtTheBalconyFrontAndTheDoorLightsLast()
        {
            // R1-24: ignition at the balcony front (start of H7), 12 s sweep; R1-22: door lamp, lens and backing dark until the
            // sweep has run, then eased on over 2.5 s; lit at the goal in any case
            Vector3 pos0 = GameState.PlayerPos, cam0 = GameState.CameraPos;
            bool goal0 = GameState.GoalReached;
            double goalT0 = GameState.GoalRealTime;
            try
            {
                GameState.GoalReached = false;
                GameState.GoalRealTime = -1;
                GameState.CameraPos = new Vector3(-45f, -485.3f, 20f);   // in the tunnel: never past the trigger itself
                Beat07_Reveal b = Build(BeatTier.Full);
                Light lamp = b.transform.Find("Lights/DoorLamp").GetComponent<Light>();
                Assert.That(Beat07_Reveal.HONEY_IGNITE_Z, Is.InRange(AbyssConst.W[17].z, AbyssConst.W[17].z + 3f), "at the balcony front");
                Assert.That(Beat07_Reveal.IGNITE_SECONDS, Is.InRange(10f, 12f));
                float y = AbyssConst.W[17].y;
                var f = new BeatFrame { D = 760f, E = 881f, CameraPos = new Vector3(-45f, -485.3f, -6f), Time = 100.0 };
                GameState.PlayerPos = new Vector3(-45f, y, -6f);
                b.Tick(in f);
                Assert.That(b.DoorLightLevel, Is.EqualTo(0f), "door dark before the ignition");
                Assert.That(lamp.intensity, Is.EqualTo(0f));

                GameState.PlayerPos = new Vector3(-45f, y, Beat07_Reveal.HONEY_IGNITE_Z - 0.05f);
                b.Tick(in f);                                         // ignites at t = 100
                Assert.That(b.DoorLightLevel, Is.EqualTo(0f), "the door is the last lamp: dark while the sweep runs");
                f.Time = 100.0 + Beat07_Reveal.IGNITE_SECONDS - 0.5;
                b.Tick(in f);
                Assert.That(b.DoorLightLevel, Is.EqualTo(0f));
                f.Time = 100.0 + Beat07_Reveal.DOOR_LIGHT_DELAY + 0.5 * Beat07_Reveal.DOOR_LIGHT_RAMP;
                b.Tick(in f);
                Assert.That(b.DoorLightLevel, Is.EqualTo(0.5f).Within(1e-3f), "half way through the ramp");
                f.Time = 100.0 + Beat07_Reveal.DOOR_LIGHT_DELAY + Beat07_Reveal.DOOR_LIGHT_RAMP + 0.1;
                b.Tick(in f);
                Assert.That(b.DoorLightLevel, Is.EqualTo(1f));
                Assert.That(lamp.intensity, Is.EqualTo(10f).Within(1e-3f), "door lamp I 10 (14.5.7)");
                Assert.That(Beat07_Reveal.DOOR_LIGHT_RAMP, Is.LessThanOrEqualTo(3f), "eased on over 2-3 s");
                Assert.That(Beat07_Reveal.DOOR_LIGHT_DELAY, Is.GreaterThanOrEqualTo(Beat07_Reveal.IgniteDelay(Beat07_Reveal.IGNITE_D1) + Beat07_Reveal.LAMP_RAMP - 1e-3f),
                    "the door lights after the last readable lamp has finished its ramp");

                // a capture past the trigger shows lamps and door lit; one before it shows the door dark
                GameState.PlayerPos = new Vector3(-45f, y, -6f);
                b.PrepareCapture(500.0);
                Assert.That(b.DoorLightLevel, Is.EqualTo(0f));
                GameState.PlayerPos = new Vector3(-45f, y, -12f);
                b.PrepareCapture(500.0);
                Assert.That(b.DoorLightLevel, Is.EqualTo(1f));
            }
            finally
            {
                GameState.PlayerPos = pos0;
                GameState.CameraPos = cam0;
                GameState.GoalReached = goal0;
                GameState.GoalRealTime = goalT0;
            }
        }

        [Test]
        public void IgnitionWaveSpendsItsTimeOnTheVisibleField()
        {
            // R2-20: a distance-warped delay (square root): the visible 0.7-10 km band lights over at least 6 s of the 14 s hold,
            // each ring ramps over 1 s (no flicker), and the Lattice's linear shader ignite follows the curve over 5.4-12 km
            Assert.That(Beat07_Reveal.IgniteDelay(Beat07_Reveal.IGNITE_D0), Is.EqualTo(0f).Within(1e-4f));
            float prev = -1f;
            for (float d = 700f; d <= 28000f; d += 100f)
            {
                float t = Beat07_Reveal.IgniteDelay(d);
                Assert.That(t, Is.GreaterThan(prev), "monotonic at " + d);
                prev = t;
            }
            float visible = Beat07_Reveal.IgniteDelay(10000f) - Beat07_Reveal.IgniteDelay(700f);
            Assert.That(visible, Is.InRange(6f, 14f), "0.7-10 km on screen for 6-14 s");
            Assert.That(Beat07_Reveal.IgniteDelay(2000f), Is.GreaterThan(2f), "the near field does not flash on within 2 s");
            Assert.That(Beat07_Reveal.LAMP_RAMP, Is.GreaterThanOrEqualTo(0.5f), "per-lamp ramp at least 0.5 s");
            for (float d = AbyssConst.HONEY_TIER2_START; d <= 12000f; d += 200f)
            {
                float lin = Beat07_Reveal.LatticeIgniteOffset + d / Beat07_Reveal.LatticeIgniteSpeed;
                Assert.That(lin, Is.EqualTo(Beat07_Reveal.IgniteDelay(d)).Within(0.5f), "lattice dots follow the curve at " + d);
            }

            Vector3 pos0 = GameState.PlayerPos, cam0 = GameState.CameraPos;
            try
            {
                GameState.CameraPos = new Vector3(-45f, -485.3f, 20f);
                Beat07_Reveal b = Build(BeatTier.Full);
                List<InstancedBatch> rings = Batches(b, "Beat07.HoneyLamps");
                Assert.That(rings.Count, Is.GreaterThanOrEqualTo(8), "the near lamps light in many rings");
                foreach (InstancedBatch r in rings) Assert.IsFalse(r.Visible, r.Name + " dark before the ignition");
                float[] delays = b.IgnitionRingDelays;
                for (int k = 1; k < delays.Length; k++) Assert.That(delays[k], Is.GreaterThanOrEqualTo(delays[k - 1]), "rings in order");
                float y = AbyssConst.W[17].y;
                GameState.PlayerPos = new Vector3(-45f, y, Beat07_Reveal.HONEY_IGNITE_Z - 0.05f);
                var f = new BeatFrame { D = 760f, E = 881f, CameraPos = new Vector3(-45f, -485.3f, -12f), Time = 100.0 };
                b.Tick(in f);                                   // ignites at t = 100
                f.Time = 100.5;
                b.Tick(in f);
                Assert.IsTrue(rings[0].Visible, "the nearest ring is kindling at +0.5 s");
                Assert.That(rings[0].Fade, Is.InRange(0.05f, 0.95f), "still ramping (1 s smoothstep)");
                Assert.IsFalse(rings[rings.Count - 1].Visible, "the farthest sprite ring is still dark");
                f.Time = 160.0;
                b.Tick(in f);
                foreach (InstancedBatch r in rings) Assert.That(r.Fade, Is.EqualTo(1f).Within(1e-4f), r.Name + " lit after the sweep");
            }
            finally
            {
                GameState.PlayerPos = pos0;
                GameState.CameraPos = cam0;
            }
        }

        [Test]
        public void DoorLampLooksIntoTheRecessAndLeavesThePierCold()
        {
            // R1-21 / R1-32: a spot under the housing; the lintel face (behind and below the lamp) and everything above it
            // (frame top, rods) lie outside its cone. R2-05: it looks down into the recess (sill and lower leaf face lit) and
            // the pier more than 0.3 m in front of the frame stays outside the cone until the leaf opens
            Beat07_Reveal b = Build(BeatTier.Full);
            Light lamp = b.transform.Find("Lights/DoorLamp").GetComponent<Light>();
            Assert.AreEqual(LightType.Spot, lamp.type);
            Assert.That(lamp.range, Is.EqualTo(12f).Within(1e-3f));
            Vector3 axis = lamp.transform.forward, pos = lamp.transform.position;
            Assert.That(axis.y, Is.LessThan(-0.95f), "aimed down");
            Assert.That(axis.z, Is.LessThan(0f), "back into the recess");
            float half = 0.5f * lamp.spotAngle;
            float y = AbyssConst.W[18].y, zFront = AbyssConst.W[18].z + 0.6f;
            Assert.That(pos.z, Is.GreaterThan(zFront + 0.05f), "in front of the frame face");
            for (float ly = AbyssConst.GOAL_DOOR_H + 0.01f; ly < 2.6f; ly += 0.02f)
            {
                var lintel = new Vector3(-45f, y + ly, zFront);
                Assert.That(Vector3.Angle(axis, lintel - pos), Is.GreaterThan(half + 4f), "lintel face at " + ly + " m outside the cone");
            }
            var lug = new Vector3(-45f + 1.3f, y + 2.6f, zFront - 0.3f);
            Assert.That(Vector3.Angle(axis, lug - pos), Is.GreaterThan(90f), "rods and lugs above the lamp");
            for (float dz = 0.3f; dz < 12f; dz += 0.1f)
            {
                var pier = new Vector3(-45f, y, zFront + dz);
                Assert.That(Vector3.Angle(axis, pier - pos), Is.GreaterThan(half), "the pier " + dz + " m out stays cold");
            }
            var sill = new Vector3(-45f, y, zFront - 0.2f);
            Assert.That(Vector3.Angle(axis, sill - pos), Is.LessThan(0.6f * half), "the sill in the recess is lit");
            var leafLow = new Vector3(-45f, y + 0.4f, AbyssConst.W[18].z + 0.2f);
            Assert.That(Vector3.Angle(axis, leafLow - pos), Is.LessThan(half), "the lower leaf face is lit");
            // the leaf sits back in a reveal of at least 0.35 m, still 0.15 m in front of the backing, which clears the back panel
            Transform leaf = b.transform.Find("Geometry/Door/LeafHinge");
            Assert.That(zFront - (leaf.position.z + 0.025f), Is.GreaterThan(0.35f), "deep reveal");
            Bounds backing = FindMesh(b, "AmberBacking").bounds;
            Assert.That(leaf.position.z - 0.025f - backing.center.z, Is.EqualTo(0.15f).Within(1e-3f), "backing 0.15 m behind the leaf");
            Assert.That(backing.center.z - AbyssConst.W[18].z, Is.GreaterThanOrEqualTo(0.005f - 1e-4f), "backing clear of the back panel");
        }

        [Test]
        public void TunnelStripsFadeTowardTheMouthWithoutAnEntrancePulse()
        {
            // R1-23: still fading toward the mouth (14.5.7), the entrance strip no longer 1.7 EV brighter than the rest
            Beat07_Reveal b = Build(BeatTier.Full);
            float prev = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                Light l = b.transform.Find("Lights/TunnelStrip" + i).GetComponent<Light>();
                Assert.That(l.intensity, Is.LessThanOrEqualTo(prev), "strip " + i + " fades toward the mouth");
                Assert.That(l.intensity, Is.LessThanOrEqualTo(0.35f * 7f), "strip " + i + " dimmed (R1-23)");
                prev = l.intensity;
            }
        }

        [Test]
        public void FaceBandsEndInsideTheBoreCollar()
        {
            // R1-34: every band that crosses the collar ends with its end face inside the collar ring (no free stub)
            Vector3 fan = AbyssConst.FAN_CENTRE;
            float rIn = AbyssConst.FAN_R_OUT - 0.1f, rOut = AbyssConst.FAN_R_OUT + 3f;
            int crossing = 0;
            for (int k = 0; k < AbyssConst.RIB_LEVELS; k++)
            {
                float yk = AbyssConst.RingY(k);
                float bh = AbyssConst.RingIsBelt(k) ? 4f : 1.5f;
                float dc = Mathf.Abs(yk - fan.y);
                if (dc - 0.5f * bh >= rOut) continue;
                float xe = Beat07_Reveal.BandGapHalf(yk, bh, fan, rIn, rOut);
                Assert.That(xe, Is.GreaterThan(0f), "level " + yk + " crosses the collar");
                float rFar = Mathf.Sqrt(xe * xe + (dc + 0.5f * bh) * (dc + 0.5f * bh));
                float rNear = Mathf.Sqrt(xe * xe + Mathf.Max(0f, dc - 0.5f * bh) * Mathf.Max(0f, dc - 0.5f * bh));
                Assert.That(rFar, Is.LessThan(rOut - 0.01f), "level " + yk + ": outer corner buried in the collar");
                Assert.That(rNear, Is.GreaterThan(rIn), "level " + yk + ": inner corner outside the bore");
                crossing++;
            }
            Assert.That(crossing, Is.GreaterThanOrEqualTo(4), "the bore spans several ring levels");
        }

        [Test]
        public void CorbelUndersideFacesDownAndAway()
        {
            // R1-15 (reveal part): the balcony corbel's sloped underside is wound and normalled outward (down and toward -Z),
            // so its dark tone in KS7d is the ambient ground term, not inverted faces
            Beat07_Reveal b = Build(BeatTier.Full);
            Transform t = b.transform.Find("Geometry/Balcony/BalconyAndPier");
            Assert.IsNotNull(t);
            Mesh m = t.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = m.vertices, n = m.normals;
            int[] tri = m.triangles;
            float bottom = AbyssConst.W[17].y - AbyssConst.BALCONY_THICK;
            int sloped = 0;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = v[tri[i]], c1 = v[tri[i + 1]], c2 = v[tri[i + 2]];
                Vector3 cen = (a + c1 + c2) / 3f;
                Vector3 g = Vector3.Cross(c1 - a, c2 - a);
                if (g.sqrMagnitude < 1e-10f) continue;
                g.Normalize();
                Vector3 vn = (n[tri[i]] + n[tri[i + 1]] + n[tri[i + 2]]).normalized;
                Assert.That(Vector3.Dot(g, vn), Is.GreaterThan(0f), "winding agrees with the normals at " + cen);
                bool corbel = cen.y < bottom - 0.2f && cen.z > AbyssConst.W[17].z + 0.7f && Mathf.Abs(cen.x + 45f) < 9f;
                // the sloped underside only (not the end caps, the pier's sides / bottom or chamfers)
                if (!corbel || Mathf.Abs(g.x) > 0.5f || Mathf.Abs(g.y) > 0.95f || Mathf.Abs(g.z) > 0.95f) continue;
                Assert.That(g.y, Is.LessThan(-0.4f), "corbel underside faces down at " + cen);
                Assert.That(g.z, Is.LessThan(-0.3f), "corbel underside faces away from the Face at " + cen);
                sloped++;
            }
            Assert.That(sloped, Is.GreaterThan(0), "found the corbel underside");
        }

        /// <summary>Where a spot's axis meets the pier floor plane (y of W18).</summary>
        static Vector3 AimOnFloor(Light spot)
        {
            Vector3 p = spot.transform.position, d = spot.transform.forward;
            float t = (AbyssConst.W[18].y - p.y) / Mathf.Min(d.y, -1e-4f);
            return p + d * t;
        }

        static List<InstancedBatch> Batches(Beat07_Reveal b, string prefix)
        {
            var l = new List<InstancedBatch>();
            b.FindHoneyBatches(prefix, l);
            Assert.That(l.Count, Is.GreaterThan(0), "no batch " + prefix);
            return l;
        }

        static int CountOf(List<InstancedBatch> l)
        {
            int n = 0;
            foreach (InstancedBatch x in l) n += x.Count;
            return n;
        }

        static Mesh FindMesh(Beat07_Reveal b, string name)
        {
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            foreach (Mesh m in list) if (m != null && m.name == name) return m;
            Assert.Fail("no mesh named " + name);
            return null;
        }
    }
}
