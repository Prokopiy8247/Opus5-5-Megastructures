using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Megastructure.Tests
{
    /// <summary>
    /// Sanity of the runtime QA data (Part 11.2, 14.10): the 14 key shots (unique tags, beats, poses inside the world and
    /// standing at eye height above the walkable path, lift shots on the deck at their ride seconds, clocks), the shot lists
    /// and selectors, the composition-lint projection, the sequence catalogue, and (when Main.unity is in the build settings)
    /// that the QaHarness on the Bootstrap object is inert without QA flags.
    /// </summary>
    public sealed class QaSmokeTests
    {
        [Test]
        public void KeyShots_FourteenUniqueTags()
        {
            Assert.AreEqual(14, KeyShots.All.Length, "14.10 lists 14 key shots");
            var seen = new HashSet<string>();
            for (int i = 0; i < KeyShots.All.Length; i++)
            {
                KeyShot k = KeyShots.All[i];
                Assert.IsTrue(seen.Add(k.Tag), "duplicate tag " + k.Tag);
                Assert.IsTrue(k.Tag.StartsWith("KS" + k.Beat), k.Tag + " does not start with KS" + k.Beat);
                Assert.That(k.Beat, Is.InRange(1, 7));
                Assert.AreEqual(i, KeyShots.IndexOf(k.Tag));
            }
            int fourK = 0;
            foreach (KeyShot k in KeyShots.All) if (k.FourK) fourK++;
            Assert.AreEqual(8, fourK, "4K is marked for KS2a, KS2b, KS3, KS5a, KS5b, KS6, KS7b, KS7c");
        }

        [Test]
        public void KeyShots_PosesInsideWorldAndAboveThePath()
        {
            foreach (KeyShot k in KeyShots.All)
            {
                Assert.IsTrue(KeyShots.WorldBounds.Contains(k.Eye), k.Tag + " eye " + k.Eye + " outside the world bounds");
                Assert.That(k.Fov, Is.InRange(20f, 120f), k.Tag + " FOV");
                Assert.That(k.Pitch, Is.InRange(-90f, 90f), k.Tag + " pitch");
                Assert.That(k.Yaw, Is.InRange(0f, 360f), k.Tag + " yaw");
                Quaternion q = k.Rotation;
                Vector3 right = q * Vector3.right;
                Assert.AreEqual(0f, right.y, 1e-5f, k.Tag + " has roll");
                if (k.OnLift) continue;
                float d = k.PathD;
                bool after = k.AfterRide;
                Vector3 floor = PathGuide.FloorPoint(d, after);
                float lateral = new Vector2(k.Eye.x - floor.x, k.Eye.z - floor.z).magnitude;
                Assert.LessOrEqual(lateral, PathGuide.HalfWidth(d) + 0.01f, k.Tag + " is not above the walkable path (d " + d + ")");
                Assert.AreEqual(AbyssConst.EYE, k.Eye.y - floor.y, 0.05f, k.Tag + " is not at eye height above the floor");
            }
        }

        [Test]
        public void KeyShots_LiftShotsSitOnTheDeckAtTheirRideSeconds()
        {
            string[] tags = { "KS5a_lift_city", "KS5b_lift_fan", "KS5c_lift_down" };
            float[] ride = { 60f, 75f, 100f };
            float[] s = { 240f, 312f, 432f };
            for (int i = 0; i < tags.Length; i++)
            {
                KeyShot k = KeyShots.All[KeyShots.IndexOf(tags[i])];
                Assert.AreEqual(ride[i], k.RideSeconds, 1e-4f, tags[i]);
                Assert.AreEqual(s[i], (float)AbyssConst.LiftS(ride[i]), 0.01f, tags[i] + " s(t)");
                Vector3 deck = LiftController.PositionAtS(AbyssConst.LiftS(ride[i]));
                Assert.AreEqual(deck.x, k.Eye.x, 0.02f, tags[i] + " x");
                Assert.AreEqual(deck.y + AbyssConst.EYE, k.Eye.y, 0.02f, tags[i] + " eye height on the deck");
                Assert.LessOrEqual(Mathf.Abs(k.Eye.z - deck.z), AbyssConst.LIFT_SIZE * 0.5f, tags[i] + " z on the deck");
                Assert.AreEqual(AbyssConst.T_RIDE_START + ride[i], k.Clock, 1e-6, tags[i] + " clock");
            }
        }

        [Test]
        public void KeyShots_ClocksFollowTheNominalTimeline()
        {
            KeyShot ks1 = KeyShots.All[0];
            Assert.AreEqual(8.0 / AbyssConst.V_WALK, ks1.Clock, 0.01, "KS1 at d 8");
            KeyShot ks4 = KeyShots.All[KeyShots.IndexOf("KS4_hall_pistons")];
            Assert.AreEqual(AbyssConst.WD[9] - 2.0, ks4.PathD, 0.05, "KS4 d (arbitrated pose u -2, R1-20)");
            foreach (KeyShot k in KeyShots.All)
            {
                if (k.Beat >= 6) Assert.Greater(k.Clock, AbyssConst.T_GATE_OPEN, k.Tag + " must be after the ride");
                if (k.Beat <= 4) Assert.Less(k.Clock, AbyssConst.T_PLATFORM_CENTRE, k.Tag + " must be before the ride");
            }
            KeyShot ks7b = KeyShots.All[KeyShots.IndexOf("KS7b_honeycomb")];
            Assert.AreEqual(0.34f, KeyShots.HorizonRow(ks7b.Pitch, ks7b.Fov), 0.005f, "KS7b horizon 34 % from the top");
        }

        [Test]
        public void ShotLists_AllTagsAndFilesUnique()
        {
            List<ShotSpec> all = KeyShots.Build("all", false, 0, 0);
            int keys = 0, stills = 0, free = 0, tests = 0, cues = 0;
            var tags = new HashSet<string>();
            var files = new HashSet<string>();
            foreach (ShotSpec s in all)
            {
                Assert.IsTrue(tags.Add(s.Tag), "duplicate tag " + s.Tag);
                Assert.IsTrue(files.Add(s.File), "duplicate file " + s.File);
                Assert.IsTrue(KeyShots.WorldBounds.Contains(s.Eye) || s.Kind == "seam" || s.Kind == "tier", s.Tag + " outside the world");
                Assert.Greater(s.Width, 0);
                Assert.Greater(s.Height, 0);
                if (s.Kind == "keyshot") keys++;
                else if (s.Kind == "still") stills++;
                else if (s.Kind == "freelook") free++;
                else if (s.Kind == "cue") cues++;
                else tests++;
            }
            Assert.AreEqual(14 * 3 + 8, keys, "1920x1080 + 1680x1050 + 2560x1080 for all 14, 3840x2160 for 8");
            Assert.AreEqual(21, stills, "start / middle / end for 7 beats");
            Assert.AreEqual(21, free, "3 free-look stills for 7 beats");
            Assert.AreEqual(6 + 6 + 4, tests, "parallax 6, seams 6, tiers 4");
            Assert.AreEqual(7, cues, "R1-40 cue stills: freight pair, 4 look-back phases, KS7b without post");
        }

        [Test]
        public void ShotLists_Selectors()
        {
            Assert.AreEqual(4, KeyShots.Build("KS2a", false, 0, 0).Count, "KS2a: 1080p, 4K, 16:10, 21:9");
            Assert.AreEqual(3, KeyShots.Build("KS1", false, 0, 0).Count, "KS1 must not match KS1b");
            Assert.AreEqual(6, KeyShots.Build("B3", false, 0, 0).Count, "B3: 3 stills + 3 free-look");
            Assert.AreEqual(1, KeyShots.Build("KS7b_honeycomb_4k", false, 0, 0).Count);
            List<ShotSpec> res = KeyShots.Build("keys", true, 3840, 2160);
            Assert.AreEqual(14, res.Count, "-res collapses the variants");
            foreach (ShotSpec s in res)
            {
                Assert.AreEqual(3840, s.Width);
                Assert.IsTrue(s.Tag.EndsWith("_4k"), s.Tag);
            }
            Assert.AreEqual(0, KeyShots.Build("nothing_like_this", false, 0, 0).Count);
        }

        [Test]
        public void CompositionLint_Projection()
        {
            Vector3 eye = Vector3.zero;
            Quaternion rot = AbyssConst.YawPitch(0f, 0f);
            var ahead = new Bounds(new Vector3(0f, 0f, 10f), new Vector3(1f, 1f, 1f));
            Assert.IsTrue(CompositionLint.ProjectBounds(ahead, eye, rot, 60f, 16f / 9f, 0.1f, out float x0, out float y0, out float x1, out float y1, out bool partly));
            Assert.IsFalse(partly);
            Assert.AreEqual(0.5f, 0.5f * (x0 + x1), 1e-4f);
            Assert.AreEqual(0.5f, 0.5f * (y0 + y1), 1e-4f);
            var behind = new Bounds(new Vector3(0f, 0f, -10f), Vector3.one);
            Assert.IsFalse(CompositionLint.ProjectBounds(behind, eye, rot, 60f, 16f / 9f, 0.1f, out _, out _, out _, out _, out _));

            // a 2 m box at 10 m: tan(half angle) = 0.1, vertical FOV 60 -> half-height 0.1 / tan 30 = 0.1732 of the half frame
            var subj = new KeyShotSubject("box", new Bounds(new Vector3(0f, 0f, 10f), new Vector3(2f, 2f, 0.001f)), 0.15f);
            CompositionLint.SubjectResult r = CompositionLint.Check(in subj, eye, rot, 60f, 16f / 9f, 0.1f);
            Assert.IsTrue(r.Pass && r.CentreOk && r.SizeOk && r.MarginOk);
            Assert.AreEqual(0.1732f, r.Size, 0.002f);

            // up and to the right: the centre moves to the upper right (top-left origin)
            var ur = new KeyShotSubject("ur", new Bounds(new Vector3(3f, 2f, 10f), Vector3.one * 0.2f));
            CompositionLint.SubjectResult r2 = CompositionLint.Check(in ur, eye, rot, 60f, 16f / 9f, 0.1f);
            Assert.Greater(r2.Cx, 0.5f);
            Assert.Less(r2.Cy, 0.5f);

            // a box around the camera is cut by the near plane but still visible
            var around = new Bounds(new Vector3(0f, 0f, 2f), new Vector3(2f, 2f, 6f));
            Assert.IsTrue(CompositionLint.ProjectBounds(around, eye, rot, 60f, 16f / 9f, 0.1f, out _, out _, out _, out _, out bool cut));
            Assert.IsTrue(cut);
        }

        [Test]
        public void Sequences_CatalogueAndDeadCentres()
        {
            List<SeqDef> all = QaSequenceCatalog.All();
            var names = new HashSet<string>();
            foreach (SeqDef d in all)
            {
                Assert.IsTrue(names.Add(d.Name), "duplicate sequence " + d.Name);
                Assert.Greater(d.Seconds * d.Fps, 1f, d.Name + " has fewer than 2 frames");
                if (d.Key >= 0) Assert.Less(d.Key, KeyShots.All.Length, d.Name);
            }
            Assert.AreEqual(7, QaSequenceCatalog.Select("static").Count);
            Assert.AreEqual(7, QaSequenceCatalog.Select("thin").Count);
            Assert.AreEqual(3, QaSequenceCatalog.Select("grad").Count);
            Assert.AreEqual(all.Count, QaSequenceCatalog.Select("all").Count);
            Assert.AreEqual(30, Mathf.RoundToInt(QaSequenceCatalog.Select("thin_b2")[0].Seconds * QaSequenceCatalog.Select("thin_b2")[0].Fps));

            double t = QaSequenceCatalog.DeadCentreAfter(KeyShots.All[5].Clock);
            Assert.GreaterOrEqual(t, KeyShots.All[5].Clock);
            double theta = AbyssConst.CrankThetaIL(t);
            double rem = theta - 45.0 * System.Math.Round(theta / 45.0);
            Assert.AreEqual(0.0, rem, 1e-6, "a dead centre puts the inner-left crank at a multiple of 45 degrees");
        }

        [UnityTest]
        public IEnumerator Harness_InertWithoutQaFlags()
        {
            if (QaHarness.AnyQaFlag())
                Assert.Ignore("The test runner was started with a QA flag; the inert check does not apply.");
            if (!Application.CanStreamedLevelBeLoaded("Main"))
                Assert.Ignore("Main.unity is not in the build settings (run the SceneBuilder first).");
            LogAssert.ignoreFailingMessages = true;   // beat warnings or errors of an unfinished scene are not this test's subject
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
            QaHarness h = Object.FindAnyObjectByType<QaHarness>(FindObjectsInactive.Include);
            bool found = h != null;
            bool enabledState = found && h.enabled;
            QaHarness.Mode mode = found ? h.ActiveMode : QaHarness.Mode.None;
            bool onBootstrap = found && h.GetComponent<GameBootstrap>() != null;

            // leave an empty scene behind so the half-loaded game does not run into later tests
            Scene main = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("QaSmokeEmpty");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(main);
            LogAssert.ignoreFailingMessages = false;

            if (!found) Assert.Ignore("No QaHarness in Main.unity (SceneBuilder not rerun since the QA runtime was added).");
            Assert.IsFalse(enabledState, "QaHarness must disable itself without QA flags (zero cost)");
            Assert.AreEqual(QaHarness.Mode.None, mode);
            Assert.IsTrue(onBootstrap, "QaHarness belongs on the Bootstrap object");
        }
    }
}
