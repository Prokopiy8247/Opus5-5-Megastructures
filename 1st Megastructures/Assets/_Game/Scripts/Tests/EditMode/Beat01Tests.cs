using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 1 acceptance (14.5.1, 14.10 numeric checks) for every tier: gallery width 2.40 +/-0.01, door height 2.00 +/-0.01
    /// (exit doorway and pressure door), all 16 steps 0.1875 x 0.28; plus the Full / Simplified dressing (counts and sizes of
    /// 14.5.1 measured from the generated vertices), mesh validity, the Key mask on the interior materials, the interior detail
    /// tier handing over to the proxy card by d 90 (14.6), the wall fan turning at 0.5 rev/s from the scene clock, and the extra
    /// colliders (open leaf, fan) staying clear of the walking lane and of the QA ray grid's 0.85 m capsule-centre lanes. Also:
    /// the KS1 / KS1b subjects pass the composition lint and KS1 names nothing hidden behind the stair head (its far glow lies
    /// on the visible sloped ceiling), the doorway spill shaft is lit during beat 1 (its own _AbBeamFade) and hands over with
    /// the interior, no Corridor collider of beats 1 or 2 stands in the gallery's exit section, and the round-1 lighting
    /// (R1-07 doorway haze within the exposure rate, R1-37 spill cone, R1-18 local exit amber, R1-16 stair-head glow).
    /// Each test builds into a temporary root (EditMode, the generated MaterialLibrary) and destroys it afterwards.
    /// </summary>
    public sealed class Beat01Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";
        const double LenTol = 0.01, StairTol = 1e-3;

        MaterialLibrary _lib;
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<InstancedBatch> _batchesBefore = new List<InstancedBatch>();

        [SetUp]
        public void SetUp()
        {
            _lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (_lib == null) Assert.Inconclusive("MaterialLibrary missing at " + LibraryPath + " (run MaterialBuilder.BuildAll)");
            if (_lib.materials == null || _lib.materials.Length < (int)MatId.Count) Assert.Inconclusive("MaterialLibrary incomplete");
            for (int i = 0; i < (int)MatId.Count; i++)
                if (_lib.materials[i] == null) Assert.Inconclusive("MaterialLibrary lacks base material " + (MatId)i);
            _batchesBefore.Clear();
            for (int i = 0; i < InstancedRegistry.Batches.Count; i++) _batchesBefore.Add(InstancedRegistry.Batches[i]);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
            // the beat registers its bolt batch at build; OnDestroy does not run in edit mode, so drop what this test added
            var added = new List<InstancedBatch>();
            for (int i = 0; i < InstancedRegistry.Batches.Count; i++)
                if (!_batchesBefore.Contains(InstancedRegistry.Batches[i])) added.Add(InstancedRegistry.Batches[i]);
            for (int i = 0; i < added.Count; i++) InstancedRegistry.Unregister(added[i]);
        }

        Beat01_Gallery Build(BeatTier tier)
        {
            var root = new GameObject("Beat01Test_" + tier);
            _roots.Add(root);
            var b = root.AddComponent<Beat01_Gallery>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            var ctx = new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = true };
            b.Build(ctx);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        static ScaleReport Report(BeatBase b)
        {
            var r = new ScaleReport();
            b.ReportScale(r);
            return r;
        }

        static double Get(ScaleReport r, string key)
        {
            Assert.IsTrue(r.Values.TryGetValue(key, out double v), "missing scale key " + key);
            Assert.IsFalse(double.IsNaN(v) || double.IsInfinity(v), key + " is not finite");
            return v;
        }

        static IEnumerable<BeatTier> Tiers()
        {
            yield return BeatTier.Full;
            yield return BeatTier.Simplified;
            yield return BeatTier.Graybox;
        }

        // ---------------------------------------------------------------- 14.5.1 acceptance, every tier

        [Test, TestCaseSource(nameof(Tiers))]
        public void WidthDoorAndStairsMeetTheAcceptance(BeatTier tier)
        {
            ScaleReport r = Report(Build(tier));
            Assert.That(Get(r, "gallery.width"), Is.EqualTo(2.40).Within(LenTol), "gallery width");
            Assert.That(Get(r, "gallery.height"), Is.EqualTo(AbyssConst.GALLERY_H).Within(LenTol), "gallery height");
            Assert.That(Get(r, "gallery.door.height"), Is.EqualTo(2.00).Within(LenTol), "exit door height");
            Assert.That(Get(r, "gallery.door.width"), Is.EqualTo(AbyssConst.EXIT_DOOR_W).Within(LenTol), "exit door width");
            Assert.That(Get(r, "pressure.door.height"), Is.EqualTo(2.00).Within(LenTol), "pressure door height");
            Assert.That(Get(r, "pressure.door.width"), Is.EqualTo(AbyssConst.PRESSURE_DOOR_W).Within(LenTol), "pressure door width");
            Assert.That(Get(r, "gallery.length"), Is.EqualTo(AbyssConst.GALLERY_L).Within(LenTol), "gallery length");
            Assert.AreEqual(16.0, Get(r, "gallery.stair.count"), "16 steps");
            Assert.That(Get(r, "gallery.stair.rise.min"), Is.EqualTo(0.1875).Within(StairTol), "rise min");
            Assert.That(Get(r, "gallery.stair.rise.max"), Is.EqualTo(0.1875).Within(StairTol), "rise max");
            Assert.That(Get(r, "gallery.stair.run.min"), Is.EqualTo(0.28).Within(StairTol), "run min");
            Assert.That(Get(r, "gallery.stair.run.max"), Is.EqualTo(0.28).Within(StairTol), "run max");
            Assert.That(Get(r, "gallery.stair.drop"), Is.EqualTo(AbyssConst.STAIR_DROP).Within(LenTol), "stair drop");
        }

        [Test, TestCaseSource(nameof(Tiers))]
        public void EveryGeneratedMeshIsValid(BeatTier tier)
        {
            Beat01_Gallery b = Build(tier);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            Assert.Greater(list.Count, 0, "no meshes collected");
            foreach (Mesh m in list)
            {
                Assert.IsNotNull(m, "null mesh");
                MeshReport rep = MeshValidation.Validate(m);
                Assert.IsTrue(rep.Ok, tier + ": " + rep.Describe());
            }
        }

        // ---------------------------------------------------------------- key-shot subjects: in frame and actually visible

        static readonly Vector3 KS1_EYE = new Vector3(0f, 1.7f, 8f), KS1B_EYE = new Vector3(0f, -1.3f, 55f);
        static readonly float[] Aspects = { 16f / 9f, 16f / 10f, 64f / 27f };

        static void AssertLintPasses(string tag, List<KeyShotSubject> subjects, Vector3 eye)
        {
            foreach (KeyShotSubject s in subjects)
            foreach (float aspect in Aspects)
            {
                CompositionLint.SubjectResult r = CompositionLint.Check(in s, eye, Quaternion.identity, 62f, aspect, AbyssConst.CAMERA_NEAR);
                Assert.IsFalse(r.Behind, tag + "/" + s.Name + " behind the camera");
                Assert.IsTrue(r.Pass, tag + "/" + s.Name + " fails the lint at aspect " + aspect + ": centre (" + r.Cx + ", " + r.Cy + "), size " + r.Size);
            }
        }

        [Test, TestCaseSource(nameof(Tiers))]
        public void Ks1SubjectsAreInFrameAndOnTheVisibleSideOfTheStairHead(BeatTier tier)
        {
            Beat01_Gallery b = Build(tier);
            var subjects = new List<KeyShotSubject>();
            b.CollectSubjects("KS1_gallery_start", subjects);
            AssertLintPasses("KS1", subjects, KS1_EYE);
            // From (0, 1.7, 8) every ray that clears the nosing (z 40, y 0) meets the sloped stair ceiling by z 44.1: nothing
            // past the stair head (exit section, amber lamp, doorway) is visible, so no KS1 subject may lie there.
            float rayDrop = (KS1_EYE.y - AbyssConst.W[1].y) / (AbyssConst.STAIR_Z0 - KS1_EYE.z);          // per metre, steepest ray
            float ceilDrop = AbyssConst.STAIR_DROP / (AbyssConst.STAIR_Z1 - AbyssConst.STAIR_Z0);          // per metre
            float zHit = AbyssConst.STAIR_Z0 + AbyssConst.GALLERY_H / (ceilDrop - rayDrop);
            Assert.Less(zHit, AbyssConst.STAIR_Z1, "the sightline over the nosing reaches the exit section (geometry changed?)");
            bool farGlow = false;
            foreach (KeyShotSubject s in subjects)
            {
                Assert.AreNotEqual("doorway", s.Name, "the doorway is hidden at KS1");
                if (s.Name == "far glow")
                {
                    // R1-16: the extreme-far layer is the lit underside of the sloped stair ceiling, which lies past the stair
                    // head but above the sight line over the nosing and in front of the point where that line meets it
                    farGlow = true;
                    Bounds g = s.WorldBounds;
                    Assert.GreaterOrEqual(g.min.z, AbyssConst.STAIR_Z0, "far glow starts on the stair slope");
                    Assert.LessOrEqual(g.max.z, zHit, "far glow ends before the nosing sight line meets the slope");
                    float lineY = KS1_EYE.y - rayDrop * (g.max.z - KS1_EYE.z);
                    Assert.Greater(Beat01_Gallery.CeilY(g.max.z), lineY, "the far glow's lowest slope point is above the nosing sight line");
                    continue;
                }
                Assert.LessOrEqual(s.WorldBounds.max.z, AbyssConst.STAIR_Z0 + 0.01f, "KS1/" + s.Name + " lies behind the stair head (hidden)");
            }
            Assert.IsTrue(farGlow, "KS1 names the far glow (R1-16)");
            subjects.Clear();
            b.CollectSubjects("KS1b_doorway", subjects);
            AssertLintPasses("KS1b", subjects, KS1B_EYE);
            bool doorway = false;
            foreach (KeyShotSubject s in subjects) doorway |= s.Name == "doorway";
            Assert.IsTrue(doorway, "KS1b frames the doorway");
        }

        [Test, TestCaseSource(nameof(Tiers))]
        public void KeyShotsHaveSubjects(BeatTier tier)
        {
            Beat01_Gallery b = Build(tier);
            var subjects = new List<KeyShotSubject>();
            foreach (string tag in new[] { "KS1_gallery_start", "KS1b_doorway" })
            {
                subjects.Clear();
                b.CollectSubjects(tag, subjects);
                Assert.GreaterOrEqual(subjects.Count, 2, tag + " subjects");
                foreach (KeyShotSubject s in subjects) Assert.Greater(s.WorldBounds.extents.magnitude, 0f, tag + "/" + s.Name + " empty");
            }
            subjects.Clear();
            b.CollectSubjects("KS1_gallery_start", subjects);
            bool fan = false;
            foreach (KeyShotSubject s in subjects) if (s.Name == "wall fan") fan = true;
            Assert.AreEqual(tier == BeatTier.Full, fan, "the wall fan is a KS1 subject exactly in the Full tier");
        }

        // ---------------------------------------------------------------- Full and Simplified dressing

        [Test]
        public void FullTierBuildsTheSpecifiedDressing()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            ScaleReport r = Report(b);
            Assert.AreEqual(20.0, Get(r, "gallery.beams.count"), "20 ceiling beams");
            Assert.That(Get(r, "gallery.beam.depth"), Is.EqualTo(0.25).Within(0.005), "beam depth under the ceiling");
            Assert.AreEqual(20.0, Get(r, "gallery.pilasters.count"), "20 pilasters");
            Assert.AreEqual(20.0, Get(r, "gallery.flanges.count"), "a flange every 3 m");
            Assert.AreEqual(160.0, Get(r, "gallery.bolts.count"), "160 instanced hex bolts");
            Assert.AreEqual(10.0, Get(r, "gallery.fixtures.count"), "10 fixtures");
            Assert.AreEqual(3.0, Get(r, "gallery.cables.count"), "3 cables");
            Assert.That(Get(r, "gallery.cable.diameter"), Is.EqualTo(0.04).Within(0.002), "cable O 0.04");
            Assert.That(Get(r, "gallery.pipe.diameter"), Is.EqualTo(0.18).Within(0.002), "pipe O 0.18");
            Assert.That(Get(r, "gallery.tray.y"), Is.EqualTo(2.1).Within(LenTol), "tray at y 2.1");
            Assert.That(Get(r, "gallery.handwheel.diameter"), Is.EqualTo(0.4).Within(LenTol), "torus handwheel O 0.4");
            Assert.That(Get(r, "gallery.fan.diameter"), Is.EqualTo(0.5).Within(LenTol), "wall fan O 0.5");
            Assert.AreEqual(6.0, Get(r, "gallery.fan.blades"), "6 blades");
            Assert.That(Get(r, "gallery.drain.size"), Is.EqualTo(AbyssConst.DRAIN).Within(LenTol), "drain 0.3");
            Assert.That(Get(r, "gallery.floorline.width"), Is.EqualTo(0.10).Within(0.002), "floor line 0.10");
            Assert.That(Get(r, "gallery.floorline.offset"), Is.EqualTo(0.005).Within(0.001), "floor line 5 mm offset");
            Assert.AreEqual(16.0, Get(r, "gallery.nosings.count"), "a painted nosing on every step");
            Assert.IsNotNull(b.transform.Find("Geometry/WallFanRotor"), "fan rotor");
            Assert.IsNotNull(b.transform.Find("Geometry/GalleryProxyCard"), "proxy card");
            for (int i = 0; i < 10; i++) Assert.IsNotNull(b.transform.Find("Lights/FixtureLight" + i), "FixtureLight" + i);
        }

        [Test]
        public void SimplifiedTierDropsPilastersBoltsFanAndHalfTheFixtures()
        {
            Beat01_Gallery b = Build(BeatTier.Simplified);
            ScaleReport r = Report(b);
            Assert.AreEqual(5.0, Get(r, "gallery.fixtures.count"), "5 fixtures");
            Assert.AreEqual(0.0, Get(r, "gallery.pilasters.count"), "no pilasters");
            Assert.AreEqual(0.0, Get(r, "gallery.bolts.count"), "no bolts");
            Assert.IsFalse(r.Values.ContainsKey("gallery.fan.diameter"), "no wall fan");
            Assert.IsNull(b.transform.Find("Geometry/WallFanRotor"), "no fan rotor");
            for (int i = 0; i < 10; i++)
            {
                bool expected = i % 2 == 0;
                Assert.AreEqual(expected, b.transform.Find("Lights/FixtureLight" + i) != null, "FixtureLight" + i);
            }
        }

        // ---------------------------------------------------------------- Key mask (14.2: the Key never lights the gallery)

        [Test]
        public void InteriorSurfacesUseTheKeyMaskedVariant()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            int checkedCount = 0;
            foreach (MeshRenderer mr in b.transform.Find("Geometry").GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                if (n == "DoorwayThreshold" || n == "GalleryFaceBoss" || n == "GalleryRoofCaster") continue;   // outside / shadow only
                Material m = mr.sharedMaterial;
                Assert.IsNotNull(m, n + " has no material");
                if (!m.HasProperty("_KeyLightMask")) continue;   // unlit (emissive, glow, cards, beam) or shader not updated yet
                Assert.AreEqual(0f, m.GetFloat("_KeyLightMask"), n + " (" + m.name + ") receives the Key");
                checkedCount++;
            }
            if (checkedCount == 0) Assert.Inconclusive("no lit shader declares _KeyLightMask yet");
        }

        // ---------------------------------------------------------------- 14.6: interior off by d 90 behind the proxy card

        [Test]
        public void InteriorHandsOverToTheProxyCardByD90()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            MeshRenderer shell = b.transform.Find("Geometry/GalleryShell").GetComponent<MeshRenderer>();
            MeshRenderer card = b.transform.Find("Geometry/GalleryProxyCard").GetComponent<MeshRenderer>();
            MeshRenderer jamb = b.transform.Find("Geometry/DoorJambL").GetComponent<MeshRenderer>();
            double t0 = SceneClock.Time;
            try
            {
                var f = new BeatFrame { D = 8f, CameraPos = new Vector3(0f, AbyssConst.EYE, 8f) };
                b.Tick(in f);
                Assert.IsTrue(shell.enabled, "interior shown inside the gallery");
                Assert.IsFalse(card.enabled, "proxy hidden inside the gallery");

                f = new BeatFrame { D = 91f, CameraPos = new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EYE, 91f) };
                b.Tick(in f);
                Assert.IsFalse(shell.enabled, "interior still on at d 91");
                Assert.IsTrue(card.enabled, "proxy card off at d 91");
                Assert.IsTrue(jamb.enabled, "the doorway frame is persistent");

                // inside the hysteresis band (d 75) the proxy stays; back in the doorway the interior returns
                f = new BeatFrame { D = 75f, CameraPos = new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EYE, 75f) };
                b.Tick(in f);
                Assert.IsFalse(shell.enabled, "no flicker back inside the hysteresis band");
                f = new BeatFrame { D = 59.5f, CameraPos = new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EYE, 59.5f) };
                b.Tick(in f);
                Assert.IsTrue(shell.enabled, "interior back in the doorway");
                Assert.IsFalse(card.enabled, "proxy hidden again");
            }
            finally { SceneClock.Time = t0; }
        }

        // ---------------------------------------------------------------- 14.5.1 / 14.7.4: the doorway spill shows during beat 1

        [Test]
        public void DoorwaySpillIgnoresTheFanBeamFadeAndHandsOverWithTheInterior()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            Transform t = b.transform.Find("Geometry/DoorwaySpillShaft");
            Assert.IsNotNull(t, "doorway spill shaft");
            MeshRenderer shaft = t.GetComponent<MeshRenderer>();
            Assert.AreEqual(2f, shaft.sharedMaterial.GetFloat("_Recipe"), "BeamWedge rectangular prism (mode 2)");
            var block = new MaterialPropertyBlock();
            double t0 = SceneClock.Time;
            try
            {
                // inside the gallery the fan beams' global fade is still 0 (ride second < 0); the shaft pins its own to 1
                var f = new BeatFrame { D = 50f, CameraPos = new Vector3(0f, AbyssConst.W[2].y + AbyssConst.EYE, 50f) };
                b.Tick(in f);
                Assert.IsTrue(shaft.enabled, "shaft shown inside the gallery");
                Assert.IsTrue(shaft.HasPropertyBlock(), "shaft carries its own block");
                shaft.GetPropertyBlock(block);
                Assert.AreEqual(1f, block.GetFloat(ShaderIds.AbBeamFade), "per-renderer _AbBeamFade overrides the fan-beam global");
                Assert.AreEqual(1f, block.GetFloat(ShaderIds.AbFade), "full interior coverage");

                f = new BeatFrame { D = 91f, CameraPos = new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EYE, 91f) };
                b.Tick(in f);
                Assert.IsFalse(shaft.enabled, "shaft off with the interior at d 91");

                f = new BeatFrame { D = 59.5f, CameraPos = new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EYE, 59.5f) };
                b.Tick(in f);
                Assert.IsTrue(shaft.enabled, "shaft back with the interior");
                shaft.GetPropertyBlock(block);
                Assert.AreEqual(1f, block.GetFloat(ShaderIds.AbBeamFade), "the block survives the hand-over");
            }
            finally { SceneClock.Time = t0; }
        }

        // ---------------------------------------------------------------- 14.7.6: the doorway is the bright feature of the exit section

        /// <summary>
        /// R1-07 / R2-01: the doorway reveal puts the light behind the subject. Two world-fixed, unfogged (domain 2), pale cold
        /// sprites (bluer than COLD_LAMP, HSV saturation at most 25 %), each strength exposure-compensated:
        /// the near veil (DoorwayHaze) wraps C1 and fills the doorway on the approach (C1 fills the opening's width until
        /// d 55, see C1FillsTheDoorwayOnTheApproach) and thins out over the threshold: at KS1b (d 55) it still lights the
        /// opening (displayed at C1's face centre at least 0.13 linear, sRGB about 0.4), on the landing (d 62) at most 0.0485
        /// linear (sRGB 0.25, the R2-01 cap), never faster than 0.35 EV/s at the cinematic 1.5 m/s (it is local to C1's area
        /// while the opening widens onto the backlight), off by HAZE_D1;
        /// the backlight (DoorwayBacklight) lies wholly behind C1 (its camera-facing plane, pulled half a diameter toward the
        /// viewer, is still beyond C1's far limb), so the column, the deck and the ledge occlude it per MSAA sample; it holds
        /// through the threshold and dissolves on the bridge within 0.35 EV/s at the brisk 2.5 m/s (14.6), off by FAR_D1.
        /// Neither ever brightens on the way out.
        /// </summary>
        [Test]
        public void DoorwayRevealPutsTheLightBehindC1WithinTheExposureRate()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            Assert.IsNull(b.transform.Find("Geometry/DoorwayGlow"), "the gallery-only doorway veil is gone (R1-07)");
            MeshRenderer haze = b.transform.Find("Geometry/DoorwayHaze").GetComponent<MeshRenderer>();
            MeshRenderer back = b.transform.Find("Geometry/DoorwayBacklight").GetComponent<MeshRenderer>();
            Color cold = AbyssPalette.COLD_LAMP.linear;
            Color c = haze.sharedMaterial.GetColor(ShaderIds.EmissionColor);   // stored linear ([HDR], not converted)
            foreach (MeshRenderer r in new[] { haze, back })
            {
                Assert.AreEqual(2f, r.sharedMaterial.GetFloat(ShaderIds.AbDomain), r.name + " unfogged (Exterior constants)");
                Color rc = r.sharedMaterial.GetColor(ShaderIds.EmissionColor);
                Assert.Greater(rc.b / Mathf.Max(rc.r, 1e-4f), cold.b / cold.r, r.name + " bluer than COLD_LAMP");
                Color.RGBToHSV(rc.gamma, out _, out float sat, out _);
                Assert.LessOrEqual(sat, 0.25f, r.name + " saturation at most 25 % (R1-07)");
            }

            // placement: the near veil sits on C1 and wraps it (R2-01, gallery r31): its soft-depth fade (half a diameter
            // behind the pulled plane) is partial at the face centre and complete at the limbs, so C1 reads as a column
            // (darker middle, lit flanks); it is centred above the KS1b eye line, so the abyss below the horizon is dimmer;
            // the backlight's pulled plane lies beyond C1's far limb
            Vector3 c1 = AbyssConst.COLUMNS[0];
            float c1Face = c1.y - c1.z;
            float nearD = haze.transform.lossyScale.x, farD = back.transform.lossyScale.x;
            float nearPlane = haze.transform.position.z - 0.5f * nearD;
            Assert.That(haze.transform.position.z, Is.InRange(c1Face - 5f, c1.y), "near veil centred on C1");
            float faceFade = (c1Face - nearPlane) / (0.5f * nearD), limbFade = (c1.y - 6f - nearPlane) / (0.5f * nearD);
            Assert.That(faceFade, Is.InRange(0.5f, 0.8f), "C1's face centre partly veiled (the column shows through)");
            Assert.GreaterOrEqual(limbFade, 0.95f, "C1's limbs fully veiled (haze-lit flanks)");
            Assert.Greater(nearPlane, AbyssConst.BRIDGE_Z0 + 60f, "the near deck stands in front of the veil");
            Assert.Greater(haze.transform.position.y, AbyssConst.BRIDGE_Y + AbyssConst.EYE + 5f, "near veil above the eye line (abyss dimmer)");
            float gain = Beat01_Gallery.HazeRefGain();
            Assert.That(gain, Is.InRange(0.5f, 0.7f), "reference weight: partial fade at the face centre, near the Gaussian's peak");
            Assert.Greater(back.transform.position.z - 0.5f * farD, c1.y + c1.z + 50f, "backlight plane behind C1's far limb");
            Assert.Less(back.transform.position.z + 0.5f * farD, AbyssConst.SHAFT_FAR_Z + 100f, "backlight inside the shaft's depth");
            Assert.Greater(back.transform.position.y, AbyssConst.BRIDGE_Y + 20f, "backlight above the eye line (the abyss below stays darker)");

            // the schedules: monotone, rate-limited, gone at the end
            const float walkEvPerMetre = 0.35f / AbyssConst.V_WALK, briskEvPerMetre = 0.35f / AbyssConst.V_BRISK;
            float prevN = Mathf.Log(Beat01_Gallery.HazeDisplayed(30f), 2f), prevF = Mathf.Log(Beat01_Gallery.FarDisplayed(30f), 2f);
            for (float d = 30.05f; d <= 120f; d += 0.05f)
            {
                float evN = Mathf.Log(Beat01_Gallery.HazeDisplayed(d), 2f), evF = Mathf.Log(Beat01_Gallery.FarDisplayed(d), 2f);
                Assert.LessOrEqual(Mathf.Abs(evN - prevN) / 0.05f, walkEvPerMetre + 1e-3f, "near-veil rate at d " + d);
                Assert.LessOrEqual(Mathf.Abs(evF - prevF) / 0.05f, briskEvPerMetre + 1e-3f, "backlight rate at d " + d);
                Assert.LessOrEqual(evN, prevN + 1e-6f, "the near veil never brightens on the way out (d " + d + ")");
                Assert.LessOrEqual(evF, prevF + 1e-6f, "the backlight never brightens on the way out (d " + d + ")");
                prevN = evN;
                prevF = evF;
            }
            Assert.GreaterOrEqual(Beat01_Gallery.HazeDisplayed(55f), 0.13f, "the doorway still bright at KS1b (D6 band 0.40-0.60)");
            Assert.LessOrEqual(Beat01_Gallery.HazeDisplayed(62f), 0.0485f, "C1 at most sRGB 0.25 under the veil on the landing (R2-01)");
            Assert.AreEqual(Beat01_Gallery.FAR_PEAK, Beat01_Gallery.FarDisplayed(55f), 1e-6f, "the backlight full at KS1b");
            Assert.GreaterOrEqual(Beat01_Gallery.FarDisplayed(62f), 0.8f * Beat01_Gallery.FAR_PEAK, "the backlight still bright on the landing");
            Assert.AreEqual(0f, Beat01_Gallery.HazeStrength(Beat01_Gallery.HAZE_D1), "near veil off at the end of its ease");
            Assert.AreEqual(0f, Beat01_Gallery.FarStrength(Beat01_Gallery.FAR_D1), "backlight off at the end of its ease");
            Assert.AreEqual(0.5f, Beat01_Gallery.EasedRamp(0.5f, Beat01_Gallery.HAZE_EASE), 1e-6f, "eased ramp symmetric");
            // the exposure is divided out: strength x 2^EV is each sprite's own share of its displayed value at every d
            foreach (float d in new[] { 50f, 55f, 57f, 60f, 62f, 70f, 90f })
            {
                float ev = ProgressTables.ExposureAt(ProgressTables.EPre(d));
                float share = Beat01_Gallery.HazeStrength(d) * Mathf.Pow(2f, ev) * Luma(c) * gain;
                Assert.That(share, Is.EqualTo(Beat01_Gallery.HazeDisplayed(d) - Beat01_Gallery.HAZE_BG).Within(1e-4f), "near veil exposure-compensated at d " + d);
                float shareF = Beat01_Gallery.FarStrength(d) * Mathf.Pow(2f, ev) * Luma(c);
                Assert.That(shareF, Is.EqualTo(Beat01_Gallery.FarDisplayed(d) - Beat01_Gallery.HAZE_BG).Within(1e-4f), "backlight exposure-compensated at d " + d);
            }

            var block = new MaterialPropertyBlock();
            double t0 = SceneClock.Time;
            try
            {
                foreach (float d in new[] { 40f, 55f, 62f, 68f, 80f })
                {
                    float eye = Beat01_Gallery.FloorY(Mathf.Min(d, AbyssConst.EXIT_DOOR_Z)) + AbyssConst.EYE;
                    var f = new BeatFrame { D = d, CameraPos = new Vector3(0f, eye, d) };
                    b.Tick(in f);
                    Assert.AreEqual(Beat01_Gallery.HazeStrength(d) > 1e-4f, haze.enabled, "near veil state at d " + d);
                    if (haze.enabled)
                    {
                        haze.GetPropertyBlock(block);
                        Assert.That(block.GetFloat(ShaderIds.EmissionStrength), Is.EqualTo(Beat01_Gallery.HazeStrength(d)).Within(2e-3f * Beat01_Gallery.HazeStrength(d)),
                            "near veil strength at d " + d);
                    }
                    Assert.IsTrue(back.enabled, "backlight lit at d " + d);
                    back.GetPropertyBlock(block);
                    Assert.That(block.GetFloat(ShaderIds.EmissionStrength), Is.EqualTo(Beat01_Gallery.FarStrength(d)).Within(2e-3f * Beat01_Gallery.FarStrength(d)),
                        "backlight strength at d " + d);
                }
                Assert.IsFalse(haze.enabled, "near veil off on the bridge (d 80)");
                var off = new BeatFrame { D = 106f, CameraPos = new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EYE, 106f) };
                b.Tick(in off);
                Assert.IsFalse(haze.enabled, "near veil off at d 106");
                Assert.IsFalse(back.enabled, "backlight off at d 106");
            }
            finally { SceneClock.Time = t0; }
        }

        /// <summary>
        /// The geometric fact behind the two-layer reveal (R2-01): from every pose of the approach up to KS1b (d 44.5-55, eye
        /// on the axis) C1's silhouette is at least as wide as the clear opening (outer plane z 60), so the doorway can only
        /// show C1's face; from the threshold (d 58) the opening is wider than C1 and the shaft beside it comes into view.
        /// </summary>
        [Test]
        public void C1FillsTheDoorwayOnTheApproach()
        {
            Vector3 c1 = AbyssConst.COLUMNS[0];
            float half = AbyssConst.EXIT_DOOR_W * 0.5f;
            foreach (float d in new[] { 44.5f, 50f, 54f })
            {
                float c1Half = Mathf.Asin(c1.z / (c1.y - d));
                float openHalf = Mathf.Atan(half / (AbyssConst.EXIT_DOOR_Z - d));
                Assert.GreaterOrEqual(c1Half, openHalf, "C1 fills the opening at d " + d);
            }
            float c1At58 = Mathf.Asin(c1.z / (c1.y - 58f)), openAt58 = Mathf.Atan(half / (AbyssConst.EXIT_DOOR_Z - 58f));
            Assert.Greater(openAt58, 2f * c1At58, "from the threshold the shaft shows beside C1");
        }

        /// <summary>
        /// R2-22: the fixture lens is a diffuser, not a flat card: on its ridge the gradient value peaks in the middle of the
        /// lens and falls to at most 0.2 at its ends (about 0.6 of the centre colour), its edges are 0, and it carries no step
        /// (every ridge vertex value follows the smooth profile).
        /// </summary>
        [Test]
        public void FixtureLensFallsOffTowardItsEdgesAndEnds()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            Transform t = b.transform.Find("Geometry/FixtureLens1");
            Assert.IsNotNull(t, "fixture lens 1");
            Mesh m = t.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = m.vertices;
            var uv = new List<Vector2>();
            m.GetUVs(0, uv);
            Bounds bb = m.bounds;
            float maxRidge = 0f, endRidge = 1f;
            int ridge = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (Mathf.Abs(v[i].x - bb.center.x) > 1e-3f) { Assert.AreEqual(0f, uv[i].y, 1e-5f, "lens edge value"); continue; }
                ridge++;
                maxRidge = Mathf.Max(maxRidge, uv[i].y);
                if (Mathf.Abs(v[i].z - bb.center.z) > 0.49f * bb.size.z) endRidge = Mathf.Min(endRidge, uv[i].y);
            }
            Assert.Greater(ridge, 10, "ridge subdivided along the lens");
            Assert.AreEqual(1f, maxRidge, 1e-5f, "full colour in the middle");
            Assert.LessOrEqual(endRidge, 0.2f, "ends fall off");
        }

        static float Luma(Color linear) => 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;

        /// <summary>
        /// R1-37 / R1-07: the cold spill is the bulkhead lamp over the doorway (a spot just under and in front of Beat02's
        /// housing): its cone holds the threshold, the near landing and the exit-section floor and leaves the far landing
        /// (the B2_start foreground, z 64.6-66) outside, and it is no longer the 14.2 point at z 66 that lit the whole landing.
        /// </summary>
        [Test, TestCaseSource(nameof(Tiers))]
        public void DoorSpillLightsTheThresholdAndTheNearLandingOnly(BeatTier tier)
        {
            Beat01_Gallery b = Build(tier);
            Light spill = b.transform.Find("Lights/DoorSpill").GetComponent<Light>();
            Assert.AreEqual(LightType.Spot, spill.type, "spot");
            Assert.AreEqual(LightShadows.None, spill.shadows, "no shadows");
            Assert.AreEqual(25f, spill.range, 1e-4f, "range as specified (14.2)");
            Vector3 p = spill.transform.position, axis = spill.transform.forward;
            Assert.Greater(p.z, AbyssConst.EXIT_DOOR_Z, "outside the doorway");
            Assert.Greater(p.y, AbyssConst.W[3].y + AbyssConst.EXIT_DOOR_H, "above the opening");
            float half = 0.5f * spill.spotAngle;
            float yFloor = AbyssConst.W[3].y;
            foreach (float z in new[] { 64.6f, 65.3f, 66f })
                Assert.Greater(Vector3.Angle(axis, new Vector3(0f, yFloor, z) - p), half, "far landing z " + z + " outside the cone");
            foreach (float z in new[] { 61f, 59.6f, 56f })
                Assert.Less(Vector3.Angle(axis, new Vector3(0f, yFloor, z) - p), half, "z " + z + " inside the cone");
        }

        /// <summary>
        /// R1-18: the exit lamp's amber point keeps its warmth local (range at most 2 m, at most 0.12 irradiance on the end
        /// wall 0.4 m beside the lamp, where the cold fill keeps the mix neutral), and it stands at least 0.2 m in front of
        /// the lamp head (R1-32); the Full tier's glow sprite is no wider than the dome.
        /// </summary>
        [Test, TestCaseSource(nameof(Tiers))]
        public void ExitLampKeepsItsAmberLocal(BeatTier tier)
        {
            Beat01_Gallery b = Build(tier);
            Light amber = b.transform.Find("Lights/ExitAmber").GetComponent<Light>();
            Assert.AreEqual(LightType.Point, amber.type, "M_Lamp keeps its amber point");
            Assert.LessOrEqual(amber.range, 2f, "amber range");
            Vector3 p = amber.transform.position;
            Renderer lamp = b.transform.Find("Geometry/ExitLamp").GetComponent<Renderer>();
            Bounds lb = lamp.bounds;
            Assert.GreaterOrEqual(Mathf.Sqrt(lb.SqrDistance(p)), 0.2f, "amber point clear of the lamp head");
            float wallZ = AbyssConst.EXIT_DOOR_Z - AbyssConst.EXIT_DEPTH;
            var wall = new Vector3(0.4f, lb.center.y, wallZ);
            float dist = Vector3.Distance(p, wall);
            float e = amber.intensity * Mathf.Abs(wallZ - p.z) / (dist * dist * dist);
            Assert.LessOrEqual(e, 0.12f, "amber irradiance on the end wall 0.4 m beside the lamp");
            Transform glow = b.transform.Find("Geometry/ExitLampGlow");
            if (glow != null) Assert.LessOrEqual(glow.lossyScale.x, 0.1f, "glow sprite no wider than the dome");
        }

        /// <summary>
        /// Lead decision D4 (R2-21): the exit lamp's dome keeps M_Lamp's AMBER x 8.0 only in a small white-hot core (weight 1
        /// within 12 % of its projected radius, at most 0.07 from 30 %) and falls to a saturated deep-amber limb (weight 0 at
        /// the rim, limb colour R > G > 2 B with HSV saturation at least 0.6), so it is not a cream-white disc.
        /// </summary>
        [Test]
        public void ExitDomeKeepsTheWhiteHotCoreSmall()
        {
            Assert.AreEqual(1f, Beat01_Gallery.DomeCoreWeight(0f), 1e-6f, "core");
            Assert.AreEqual(1f, Beat01_Gallery.DomeCoreWeight(0.1f), 1e-6f, "core to 10 %");
            Assert.LessOrEqual(Beat01_Gallery.DomeCoreWeight(0.3f), 0.07f + 1e-6f, "amber band from 30 %");
            Assert.AreEqual(0f, Beat01_Gallery.DomeCoreWeight(1f), 1e-6f, "deep amber at the rim");
            float prev = 1f;
            for (float rho = 0f; rho <= 1f; rho += 0.01f)
            {
                float w = Beat01_Gallery.DomeCoreWeight(rho);
                Assert.LessOrEqual(w, prev + 1e-6f, "monotonic at rho " + rho);
                prev = w;
            }
            Beat01_Gallery b = Build(BeatTier.Full);
            Material m = b.transform.Find("Geometry/ExitLamp").GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual(AbyssPalette.AMBER_EMISSIVE, m.GetFloat(ShaderIds.EmissionStrength), 1e-6f, "core at M_Lamp's 8.0");
            Color limb = m.GetColor(ShaderIds.CardBottom);
            Assert.IsTrue(limb.r > limb.g && limb.g > 2f * limb.b, "limb a deep amber (R > G > 2 B)");
            Color.RGBToHSV(limb, out _, out float sat, out _);
            Assert.GreaterOrEqual(sat, 0.6f, "limb saturation");
        }

        /// <summary>
        /// R1-16: the stair-head glow is a cold spot in the lower stairwell (above the treads, in front of the sloped ceiling's
        /// plane) whose cone holds the slope the KS1 eye sees, and not the treads below it or the flat exit ceiling. It is lit
        /// from the first frame (KS1, no fade-in) and hands over to the woken stair fixtures with a slow ease that is over
        /// before the camera reaches the stair head (no pulse when descending under the lit slope).
        /// </summary>
        [Test, TestCaseSource(nameof(Tiers))]
        public void StairGlowLightsTheSlopeKs1SeesAndHandsOverBeforeTheStairs(BeatTier tier)
        {
            Beat01_Gallery b = Build(tier);
            Light glow = b.transform.Find("Lights/StairGlow").GetComponent<Light>();
            Assert.AreEqual(LightType.Spot, glow.type, "spot");
            Assert.Greater(glow.intensity, 0f, "lit");
            Vector3 p = glow.transform.position, axis = glow.transform.forward;
            float z0 = AbyssConst.STAIR_Z0, z1 = AbyssConst.STAIR_Z1, h = AbyssConst.GALLERY_H;
            Assert.That(p.z, Is.InRange(z0, z1), "in the stairwell");
            Assert.Greater(p.y, Beat01_Gallery.FloorY(p.z) + 0.2f, "above the treads");
            // the slope plane: (z0, yTop + h) -> (z1, yLow + h); its underside normal points down and toward -Z
            var a = new Vector3(0f, AbyssConst.W[1].y + h, z0);
            Vector3 along = new Vector3(0f, AbyssConst.W[2].y + h, z1) - a;
            Vector3 n = Vector3.Cross(along, Vector3.right).normalized;
            if (n.y > 0f) n = -n;
            Assert.Greater(Vector3.Dot(p - a, n), 1f, "in front of the sloped ceiling by more than 1 m");
            float half = 0.5f * glow.spotAngle;
            foreach (float z in new[] { 41.5f, 42.5f, 43.5f })
                Assert.Less(Vector3.Angle(axis, new Vector3(0f, Beat01_Gallery.CeilY(z), z) - p), half, "slope at z " + z + " lit");
            Assert.Greater(Vector3.Angle(axis, new Vector3(0f, Beat01_Gallery.FloorY(p.z), p.z) - p), half, "tread under the light dark");
            Assert.Greater(Vector3.Angle(axis, new Vector3(0f, AbyssConst.W[2].y + h, z1 + 1f) - p), half, "flat exit ceiling dark");
            float i0 = glow.intensity;
            // the hand-over: full at KS1 (d 8), monotone, at most a smoothstep's 1.5 x mean slope, off at the stair head
            Assert.AreEqual(1f, Beat01_Gallery.StairGlowLevel(0f), 1e-6f, "full at the start");
            Assert.AreEqual(1f, Beat01_Gallery.StairGlowLevel(8f), 1e-6f, "full at KS1");
            Assert.LessOrEqual(Beat01_Gallery.STAIR_GLOW_D1, AbyssConst.STAIR_Z0, "off before the camera reaches the stair head");
            Assert.GreaterOrEqual(Beat01_Gallery.STAIR_GLOW_D1 - Beat01_Gallery.STAIR_GLOW_D0, 20f, "a slow ease (at least 20 m)");
            float maxSlope = 1.5f / (Beat01_Gallery.STAIR_GLOW_D1 - Beat01_Gallery.STAIR_GLOW_D0) + 1e-3f;
            float prev = Beat01_Gallery.StairGlowLevel(0f);
            for (float d = 0.1f; d <= 60f; d += 0.1f)
            {
                float l = Beat01_Gallery.StairGlowLevel(d);
                Assert.LessOrEqual(l, prev + 1e-6f, "monotone at d " + d);
                Assert.LessOrEqual((prev - l) / 0.1f, maxSlope, "smooth at d " + d);
                prev = l;
            }
            double t0 = SceneClock.Time;
            try
            {
                var f = new BeatFrame { D = 8f, CameraPos = new Vector3(0f, AbyssConst.EYE, 8f) };
                b.Tick(in f);
                Assert.IsTrue(glow.enabled, "lit at KS1");
                Assert.AreEqual(i0, glow.intensity, 1e-6f, "full at KS1");
                f = new BeatFrame { D = 27f, CameraPos = new Vector3(0f, AbyssConst.EYE, 27f) };
                b.Tick(in f);
                Assert.AreEqual(i0 * Beat01_Gallery.StairGlowLevel(27f), glow.intensity, 1e-4f, "easing at d 27");
                f = new BeatFrame { D = 44f, CameraPos = new Vector3(0f, Beat01_Gallery.FloorY(44f) + AbyssConst.EYE, 44f) };
                b.Tick(in f);
                Assert.IsFalse(glow.enabled, "off on the stairs");
                f = new BeatFrame { D = 8f, CameraPos = new Vector3(0f, AbyssConst.EYE, 8f) };
                b.Tick(in f);
                Assert.IsTrue(glow.enabled, "back on when the camera is placed at KS1 again");
            }
            finally { SceneClock.Time = t0; }
        }

        /// <summary>The two fixtures of the exit section (z 51, 57) give less light than the corridor's (14.7.6), none is dark.</summary>
        [Test]
        public void ExitSectionFixturesGiveWayToTheDoorway()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            float corridor = b.transform.Find("Lights/FixtureLight0").GetComponent<Light>().intensity;
            Assert.Greater(corridor, 0f, "corridor fixture lit in an edit-mode build");
            for (int i = 1; i < 10; i++)
            {
                float v = b.transform.Find("Lights/FixtureLight" + i).GetComponent<Light>().intensity;
                if (3f + 6f * i > AbyssConst.EXIT_DOOR_Z - 12f)
                {
                    Assert.Greater(v, 0f, "exit fixture " + i + " lit");
                    Assert.Less(v, corridor, "exit fixture " + i + " dimmer than the corridor's");
                }
                else Assert.AreEqual(corridor, v, 1e-5f, "corridor fixture " + i);
            }
        }

        // ---------------------------------------------------------------- seam 1 -> 2: no invisible wall in the exit section

        /// <summary>
        /// The gallery's exit section (|x| &lt; 1.2, z 44.48-59.2, floor -3 up to the capsule top) must be free of every Corridor
        /// collider of beats 1 and 2: the beat-2 landing back walls have to stay inside the 0.8 m web (z 59.2-60), or the walker
        /// and the wall-hug bot meet an invisible wall before the visible end wall.
        /// </summary>
        [Test]
        public void NoCorridorColliderIntrudesIntoTheGalleryExitSection()
        {
            Beat01_Gallery b1 = Build(BeatTier.Full);
            BeatBase b2 = BuildBeat02(BeatTier.Graybox);
            float hw = AbyssConst.GALLERY_W * 0.5f - 0.01f, y0 = AbyssConst.W[2].y;
            var walk = new Bounds();
            walk.SetMinMax(new Vector3(-hw, y0 + 0.01f, AbyssConst.STAIR_Z1 + 0.01f),
                new Vector3(hw, y0 + AbyssConst.CAPSULE_H, AbyssConst.EXIT_DOOR_Z - AbyssConst.EXIT_DEPTH - 0.01f));
            int checkedCount = 0;
            foreach (BeatBase beat in new BeatBase[] { b1, b2 })
            foreach (Collider c in beat.GetComponentsInChildren<Collider>(true))
            {
                if (c.gameObject.layer != CorridorKit.LayerCorridor || c.isTrigger) continue;
                checkedCount++;
                Bounds w = WorldAabb(c);
                Assert.IsFalse(w.Intersects(walk), "beat " + beat.Index + " collider " + c.name + " (world box " + w.min + " .. " + w.max +
                    ") intrudes into the gallery exit section " + walk.min + " .. " + walk.max + ": an invisible wall");
            }
            Assert.Greater(checkedCount, 10, "corridor colliders of beats 1 and 2");
        }

        BeatBase BuildBeat02(BeatTier tier)
        {
            var root = new GameObject("Beat02ForBeat01Test_" + tier);
            _roots.Add(root);
            var b = root.AddComponent<Beat02_Bridge>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            var ctx = new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = _lib, Seed = 1u, Tier = tier, EditMode = true };
            b.Build(ctx);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        /// <summary>World AABB of a box or mesh collider from its transform (edit-mode physics bounds may lag behind).</summary>
        static Bounds WorldAabb(Collider c)
        {
            Vector3 centre, size;
            if (c is BoxCollider bc) { centre = bc.center; size = bc.size; }
            else if (c is MeshCollider mc && mc.sharedMesh != null) { centre = mc.sharedMesh.bounds.center; size = mc.sharedMesh.bounds.size; }
            else return c.bounds;
            Transform t = c.transform;
            Vector3 h = size * 0.5f;
            var w = new Bounds(t.TransformPoint(centre - h), Vector3.zero);
            for (int i = 1; i < 8; i++)
                w.Encapsulate(t.TransformPoint(centre + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z)));
            return w;
        }

        // ---------------------------------------------------------------- wall fan 0.5 rev/s from the scene clock

        [Test]
        public void WallFanTurnsHalfARevolutionPerSecond()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            Transform rotor = b.transform.Find("Geometry/WallFanRotor");
            Assert.IsNotNull(rotor);
            double t0 = SceneClock.Time;
            try
            {
                var f = new BeatFrame { D = 8f, CameraPos = new Vector3(0f, AbyssConst.EYE, 8f) };
                SceneClock.Time = 0.0;
                b.Tick(in f);
                Quaternion q0 = rotor.localRotation;
                SceneClock.Time = 0.25;   // a quarter second = 45 degrees
                b.Tick(in f);
                Assert.That(Quaternion.Angle(q0, rotor.localRotation), Is.EqualTo(45f).Within(0.05f), "0.5 rev/s");
                SceneClock.Time = 2.0 + 1e6;   // whole turns later (wrapped in double): the same pose as t = 0
                b.Tick(in f);
                Assert.That(Quaternion.Angle(q0, rotor.localRotation), Is.LessThan(0.05f), "wrapped clock");
            }
            finally { SceneClock.Time = t0; }
        }

        // ---------------------------------------------------------------- extra colliders stay out of the lanes

        [Test]
        public void LeafAndFanCollidersStayClearOfTheLanes()
        {
            Beat01_Gallery b = Build(BeatTier.Full);
            int found = 0;
            foreach (BoxCollider bc in b.GetComponentsInChildren<BoxCollider>(true))
            {
                if (bc.name != "PressureDoorLeaf" && bc.name != "WallFan") continue;
                found++;
                // world AABB from the transform (axis-aligned boxes; edit-mode physics bounds may lag behind)
                var w = new Bounds(bc.transform.TransformPoint(bc.center), Vector3.Scale(bc.size, bc.transform.lossyScale));
                float lane = AbyssConst.CAPSULE_R;   // the auto-walk capsule at x = 0
                Assert.Less(w.max.x, -lane, bc.name + " blocks the walking lane");
                float probe = -(AbyssConst.GALLERY_W * 0.5f - AbyssConst.CAPSULE_R);   // QA ray grid probe lane x = -0.85
                Assert.Greater(w.min.x, probe, bc.name + " reaches the ray-grid probe lane");
            }
            Assert.AreEqual(2, found, "leaf and fan colliders");
        }
    }
}
