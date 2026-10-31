using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// QA harness changes of fix round R1 (critique R1-29, R1-38, R1-39, R1-40 and the lead's key-shot arbitrations): the
    /// arbitrated key-shot poses (KS4, KS5a, KS5b, KS5c), the composition lint's in-frame rule for oversize subjects and its
    /// depth-buffer occlusion test, the acceptance regions the harness writes for the Python metrics, the cue stills and
    /// sequences, and the per-frame doorway aperture of the t12_ev sequences.
    /// </summary>
    public sealed class QaTests
    {
        static KeyShot Key(string tag) => KeyShots.All[KeyShots.IndexOf(tag)];

        // ---------------------------------------------------------------- arbitrated poses

        [Test]
        public void KeyShots_ArbitratedPoses()
        {
            KeyShot ks4 = Key("KS4_hall_pistons");
            Assert.AreEqual(new Vector3(-30f, -5.3f, 269f), ks4.Eye, "KS4 eye (hall u -2)");
            Assert.AreEqual(274f, ks4.Yaw);
            Assert.AreEqual(8f, ks4.Pitch);
            Assert.AreEqual(62f, ks4.Fov);
            Assert.AreEqual(AbyssConst.WD[9] - 2f, ks4.PathD, 0.05f, "KS4 stands 2 m before the portal plane");

            KeyShot ks5a = Key("KS5a_lift_city");
            Assert.AreEqual(262f, ks5a.Yaw);
            Assert.AreEqual(16f, ks5a.Pitch);
            Assert.AreEqual(GazeCues.KS5A_YAW, ks5a.Yaw, "KS5a matches the ride gaze hold");
            Assert.AreEqual(GazeCues.KS5A_PITCH, ks5a.Pitch, "KS5a matches the ride gaze hold");

            KeyShot ks5b = Key("KS5b_lift_fan");
            Assert.AreEqual(new Vector3(-85.95f, -317.3f, 260.5f), ks5b.Eye);
            Assert.AreEqual(169.8f, ks5b.Yaw);
            KeyShot ks5c = Key("KS5c_lift_down");
            Assert.AreEqual(260.4f, ks5c.Eye.z, 1e-4f);
            // lead decision D1 (R2-03): pitch -38 (spec -55), yaw 176 (spec 180) at the rail stand
            Assert.AreEqual(-38f, ks5c.Pitch);
            Assert.AreEqual(176f, ks5c.Yaw);
            Assert.AreEqual(GazeCues.RIDE_DOWN_PITCH, ks5c.Pitch, "KS5c matches the ride look down");
            // from the rail stand (1.4 m behind the -Z deck edge, eye 1.7 m up) the edge lies 50.5 degrees down: at -38 with
            // the 68 degree frame it falls in the lower third (frame row about 0.6-0.7), so the drop fills the upper two thirds
            float edgeDeg = Mathf.Atan2(AbyssConst.EYE, 1.4f) * Mathf.Rad2Deg;
            float edgeRow = KeyShots.HorizonRow(ks5c.Pitch + edgeDeg, ks5c.Fov);
            Assert.That(edgeRow, Is.InRange(0.6f, 0.75f), "KS5c deck edge row " + edgeRow);
            // both stand where the walker stands at the -Z rail (WalkPlanner.DeckStand), inside the walkable 18 x 18
            float deckZ = AbyssConst.LIFT_TOP.z;
            Assert.AreEqual(deckZ + WalkPlanner.RAIL_STAND_Z, ks5c.Eye.z, 0.15f, "KS5c at the walker's rail stand");
            Assert.AreEqual(deckZ + WalkPlanner.RAIL_STAND_Z, ks5b.Eye.z, 0.15f, "KS5b at the walker's rail stand");
            foreach (KeyShot k in new[] { ks5b, ks5c })
            {
                Vector3 deck = LiftController.PositionAtS(AbyssConst.LiftS(k.RideSeconds));
                Assert.LessOrEqual(Mathf.Abs(k.Eye.z - deck.z), AbyssConst.LIFT_WALKABLE * 0.5f, k.Tag + " inside the walkable deck");
                Assert.AreEqual(deck.x, k.Eye.x, 0.02f, k.Tag + " x on the deck centre line");
            }
        }

        // ---------------------------------------------------------------- composition lint

        [Test]
        public void Lint_InFrameRuleForOversizeSubjects()
        {
            Vector3 eye = Vector3.zero;
            Quaternion rot = AbyssConst.YawPitch(0f, 0f);
            // a 40 m wall 10 m ahead, offset left: its box spans far beyond the frame, the in-frame part is centred inside 5-95 %
            var wall = new KeyShotSubject("wall", new Bounds(new Vector3(-12f, 0f, 10f), new Vector3(40f, 4f, 1f)));
            CompositionLint.SubjectResult r = CompositionLint.Check(in wall, eye, rot, 60f, 16f / 9f, 0.1f);
            Assert.IsFalse(r.Pass, "box centre outside the frame (14.10 rule)");
            Assert.IsTrue(r.Oversize && r.InFramePass && r.FramePass && r.LintPass, "judged by its in-frame part");
            // a small box half out of frame keeps the 14.10 box-centre rule
            var small = new KeyShotSubject("small", new Bounds(new Vector3(9.6f, 0f, 10f), new Vector3(1f, 1f, 1f)));
            r = CompositionLint.Check(in small, eye, rot, 60f, 16f / 9f, 0.1f);
            Assert.IsFalse(r.Oversize, "fits the frame");
            Assert.IsFalse(r.LintPass, "centre at the frame edge fails");
            // a rail box around the viewer (cut by the near plane), below the eye: R1-39
            var rail = new KeyShotSubject("rail", new Bounds(new Vector3(0f, -1.15f, 0f), new Vector3(20f, 1.1f, 20f)));
            r = CompositionLint.Check(in rail, eye, AbyssConst.YawPitch(0f, -10f), 66f, 16f / 9f, 0.1f);
            Assert.IsTrue(r.Oversize && r.LintPass, "subject around the viewer: in-frame part " + r.InCx + ", " + r.InCy);
            Assert.AreEqual(-1f, r.Visible, "no depth buffer: visibility unknown");
        }

        [Test]
        public void Lint_OcclusionFromDepthBuffer()
        {
            const int w = 64, h = 36;
            Vector3 eye = Vector3.zero;
            Quaternion rot = AbyssConst.YawPitch(0f, 0f);
            var box = new KeyShotSubject("box", new Bounds(new Vector3(0f, 0f, 20f), new Vector3(4f, 4f, 2f)));
            var depth = new float[w * h];
            for (int i = 0; i < depth.Length; i++) depth[i] = 30000f;                 // sky
            CompositionLint.SubjectResult r = CompositionLint.Check(in box, eye, rot, 60f, 16f / 9f, 0.1f);
            CompositionLint.Visibility(ref r, box.WorldBounds, eye, rot, 60f, 16f / 9f, 0.1f, depth, w, h);
            Assert.AreEqual(1f, r.Visible, 1e-6f, "nothing in front: fully visible");
            Assert.Greater(r.VisSamples, 32);
            for (int i = 0; i < depth.Length; i++) depth[i] = 19.1f;                  // the box's own front face (at 19 m): visible
            CompositionLint.Visibility(ref r, box.WorldBounds, eye, rot, 60f, 16f / 9f, 0.1f, depth, w, h);
            Assert.AreEqual(1f, r.Visible, 1e-6f, "the subject's own surface is not an occluder");
            for (int i = 0; i < depth.Length; i++) depth[i] = 5f;                     // a wall 5 m ahead
            CompositionLint.Visibility(ref r, box.WorldBounds, eye, rot, 60f, 16f / 9f, 0.1f, depth, w, h);
            Assert.AreEqual(0f, r.Visible, 1e-6f, "hidden behind a wall");
            Assert.IsFalse(r.VisibilityOk);
            Assert.IsFalse(r.LintPass, "an occluded subject fails the lint");
            // lower half of the frame occluded (texture order: row 0 = bottom): about half the rays blocked
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) depth[y * w + x] = y < h / 2 ? 5f : 30000f;
            CompositionLint.Visibility(ref r, box.WorldBounds, eye, rot, 60f, 16f / 9f, 0.1f, depth, w, h);
            Assert.AreEqual(0.5f, r.Visible, 0.15f, "half occluded (a deck across the lower half)");
            Assert.Less(r.VisCy, 0.5f, "the visible centre is in the upper half (top-left origin)");
        }

        [Test]
        public void ProjectPoint_MatchesProjectBounds()
        {
            Vector3 eye = new Vector3(3f, -2f, 5f);
            Quaternion rot = AbyssConst.YawPitch(30f, -12f);
            Vector3 p = eye + rot * new Vector3(1.5f, 0.7f, 12f);
            Assert.IsTrue(CompositionLint.ProjectPoint(p, eye, rot, 66f, 16f / 9f, 0.1f, out float u, out float v));
            Assert.IsTrue(CompositionLint.ProjectBounds(new Bounds(p, Vector3.one * 1e-4f), eye, rot, 66f, 16f / 9f, 0.1f,
                out float x0, out float y0, out float x1, out float y1, out _));
            Assert.AreEqual(0.5f * (x0 + x1), u, 1e-4f);
            Assert.AreEqual(0.5f * (y0 + y1), v, 1e-4f);
            Assert.IsFalse(CompositionLint.ProjectPoint(eye - rot * Vector3.forward, eye, rot, 66f, 16f / 9f, 0.1f, out _, out _));
        }

        [Test]
        public void QaSubjects_Ks1FarGlowIsTheStairHeadCeiling()
        {
            var list = new List<KeyShotSubject>();
            KeyShots.CollectQaSubjects("KS1_gallery_start", list);
            Assert.AreEqual(1, list.Count);
            Bounds b = list[0].WorldBounds;
            Assert.AreEqual(AbyssConst.STAIR_Z0, b.min.z, 1e-3f);
            Assert.AreEqual(AbyssConst.STAIR_Z1, b.max.z, 1e-3f);
            Assert.AreEqual(AbyssConst.GALLERY_H, b.max.y, 1e-3f, "ceiling at the stair top");
            Assert.AreEqual(AbyssConst.GALLERY_H - AbyssConst.STAIR_DROP, b.min.y, 1e-3f, "ceiling at the stair foot");
            KeyShot ks1 = Key("KS1_gallery_start");
            CompositionLint.SubjectResult r = CompositionLint.Check(list[0], ks1.Eye, ks1.Rotation, ks1.Fov, 16f / 9f, AbyssConst.CAMERA_NEAR);
            Assert.IsTrue(r.Pass, "far glow centred in the KS1 frame");
            list.Clear();
            KeyShots.CollectQaSubjects("KS2a_bridge_ahead", list);
            Assert.AreEqual(0, list.Count);
        }

        // ---------------------------------------------------------------- regions for the Python metrics

        static string Regions(ShotSpec s)
        {
            var j = new QaJson();
            j.BeginObject();
            KeyShots.WriteRegions(j, s, AbyssConst.CAMERA_NEAR);
            j.EndObject();
            return j.ToString();
        }

        [Test]
        public void Regions_AcceptanceChecks()
        {
            List<ShotSpec> ks2b = KeyShots.Build("KS2b", false, 0, 0);
            Assert.AreEqual(4, ks2b.Count);
            foreach (ShotSpec s in ks2b) StringAssert.Contains("\"side_strips\"", Regions(s), s.Tag);
            string ks7d = Regions(KeyShots.Build("KS7d_look_back", false, 0, 0)[0]);
            StringAssert.Contains("\"banding\": {}", ks7d, "R1-38: no banding region for KS7d");
            string ks7b = Regions(KeyShots.Build("KS7b_honeycomb", false, 0, 0)[0]);
            StringAssert.Contains("\"amber_dots\"", ks7b);
            StringAssert.Contains("\"cells\"", ks7b);
            Assert.Greater(ks7b.Split('\n').Length, 150, "the KS7b cell list holds the 0.5-12 km cells in the lower 66 %");
            StringAssert.Contains("\"far_glow\"", Regions(KeyShots.Build("KS1_gallery_start", false, 0, 0)[0]));
            StringAssert.Contains("\"window_grid\"", Regions(KeyShots.Build("KS5a_lift_city", false, 0, 0)[0]));
            StringAssert.Contains("\"fan_ring\"", Regions(KeyShots.Build("KS6_gantry_fan", false, 0, 0)[0]));
        }

        [Test]
        public void LatticeCell_MatchesThePitch()
        {
            Vector2 c0 = KeyShots.LatticeCell(0, 0), c1 = KeyShots.LatticeCell(0, 1), c2 = KeyShots.LatticeCell(1, 0);
            Assert.AreEqual(AbyssConst.SHAFT_CENTRE.x, c0.x, 1e-3f);
            Assert.AreEqual(AbyssConst.SHAFT_CENTRE.y, c0.y, 1e-3f);
            Assert.AreEqual(AbyssConst.LATTICE_PITCH, (c1 - c0).magnitude, 0.05f, "neighbour along b1");
            Assert.AreEqual(AbyssConst.LATTICE_PITCH, (c2 - c0).magnitude, 0.05f, "neighbour along b2");
            Assert.AreEqual(AbyssConst.LATTICE_B1.y, (c1 - c0).y, 0.05f);
        }

        // ---------------------------------------------------------------- cues and selectors

        [Test]
        public void Cues_StillsAndSelectors()
        {
            List<ShotSpec> cues = KeyShots.Build("cues", false, 0, 0);
            Assert.AreEqual(7, cues.Count, "freight pair, 4 look-back phases, KS7b without post");
            int pairs = 0;
            foreach (ShotSpec s in cues)
            {
                Assert.AreEqual("cue", s.Kind);
                if (s.PairOf != null) { pairs++; Assert.IsTrue(cues.Exists(c => c.Tag == s.PairOf), s.Tag + " pairs with " + s.PairOf); }
            }
            Assert.AreEqual(4, pairs, "freight t31 and look-back p1..p3");
            ShotSpec np = cues.Find(c => c.Tag.Contains("nopost"));
            Assert.IsNotNull(np);
            Assert.IsTrue(np.NoPost);
            StringAssert.Contains("\"horizon_row\"", Regions(np), "the -nopost cue carries the horizon row");
            ShotSpec t30 = cues.Find(c => c.Tag == "cue_freight_t30"), t31 = cues.Find(c => c.Tag == "cue_freight_t31");
            Assert.AreEqual(KeyShots.FreightRide, t30.RideSeconds);
            Assert.AreEqual(1.0, t31.Clock - t30.Clock, 1e-6, "freight pair 1 s apart, platform frozen");
            Assert.AreEqual(6 + 6 + 4 + 7, KeyShots.Build("tests", false, 0, 0).Count, "parallax, seams, tiers and cues");
        }

        [Test]
        public void Sequences_EvCueAndPistonDefinitions()
        {
            List<SeqDef> all = QaSequenceCatalog.All();
            SeqDef walk = all.Find(d => d.Name == "t12_ev_walk"), brisk = all.Find(d => d.Name == "t12_ev_brisk");
            Assert.IsNotNull(walk);
            Assert.IsNotNull(brisk);
            Assert.IsTrue(walk.ApertureRegion && brisk.ApertureRegion);
            Assert.AreEqual(QaSequenceCatalog.T12_EV_D0, walk.D0);
            Assert.GreaterOrEqual(walk.D0 + walk.Speed * walk.Seconds, QaSequenceCatalog.T12_EV_D1, "covers d 50-75 at 1.5 m/s");
            Assert.GreaterOrEqual(brisk.D0 + brisk.Speed * brisk.Seconds, QaSequenceCatalog.T12_EV_D1, "covers d 50-75 at 2.5 m/s");
            StringAssert.Contains("--luma-region csv", walk.Judge);
            // the falling bolt drops once per run: its cue runs before any sequence that walks through d 72
            Assert.AreEqual("cue_bolt", all[0].Name);
            Assert.AreEqual(3, QaSequenceCatalog.Select("cues").Count);
            SeqDef ending = all.Find(d => d.Name == "cue_ending");
            Assert.IsTrue(ending.Window, "the ending line is overlay UI: window capture");
            // the piston impulse sequence keeps the 14.10 KS4 spot inside the hall (u 28)
            SeqDef pistons = all.Find(d => d.Name == "pistons");
            Assert.AreEqual(AbyssConst.WD[9] + 28f, pistons.StandD, 0.05f);
        }

        // ---------------------------------------------------------------- fix round R2

        [Test]
        public void R2_LintLoggedConflictsAndCueSelector()
        {
            // R2-36 (b): the KS2b truss is a logged spec conflict (DEVELOPMENT ledge row) whatever the beat names it
            Assert.IsTrue(CompositionLint.IsLoggedConflict("KS2b_bridge_down", "truss"));
            Assert.IsFalse(CompositionLint.IsLoggedConflict("KS2a_bridge_ahead", "truss"));
            // R2-36 (e): "cues" (and "all") also records the cue sequences and the title
            Assert.IsTrue(QaCaptureRunner.WantsCueSequences("cues"));
            Assert.IsTrue(QaCaptureRunner.WantsCueSequences("KS7b,cues"));
            Assert.IsTrue(QaCaptureRunner.WantsCueSequences("all"));
            Assert.IsFalse(QaCaptureRunner.WantsCueSequences("look"));
            Assert.IsFalse(QaCaptureRunner.WantsCueSequences("keys"));
        }

        [Test]
        public void R2_Ks3LiftCurvesAndRegions()
        {
            KeyShot ks3 = Key("KS3_column_up");
            var pts = new List<float>();
            foreach (float y in new[] { 3f, 6f })
            {
                Assert.IsTrue(KeyShots.LiftCurve(AbyssConst.COLUMNS[0], y, ks3.Eye, ks3.Rotation, ks3.Fov, 16f / 9f, AbyssConst.CAMERA_NEAR, pts),
                    "the y " + y + " lift joint is in the KS3 frame");
                Assert.AreEqual(y, pts[0]);
                Assert.GreaterOrEqual((pts.Count - 1) / 2, KeyShots.LiftCurveMinPoints);
                for (int i = 1; i < pts.Count; i++) Assert.That(pts[i], Is.InRange(0f, 1f));
            }
            string ks3j = Regions(KeyShots.Build("KS3_column_up", false, 0, 0)[0]);
            StringAssert.Contains("\"lift_curves\"", ks3j);
            StringAssert.Contains("\"wall_board\"", Regions(KeyShots.Build("KS1_gallery_start", false, 0, 0)[0]));
            StringAssert.Contains("\"wall_macro\"", Regions(KeyShots.Build("KS2a_bridge_ahead", false, 0, 0)[0]));
        }

        [Test]
        public void R2_CueSequenceDefinitions()
        {
            List<SeqDef> all = QaSequenceCatalog.All();
            // player note (R2): t56_ride boards while the walker's deck plan still holds the -Z rail stand, and covers the walk
            // back, ride seconds 110-120 and the gate
            SeqDef t56 = all.Find(d => d.Name == "t56_ride");
            Assert.AreEqual(SeqKind.RideFrom, t56.Kind);
            Assert.LessOrEqual(t56.RideT0, WalkPlanner.RAIL_PLAN_LATEST_BEGIN);
            Assert.GreaterOrEqual(t56.RideT0, WalkPlanner.RAIL_OUT_T1);
            Assert.GreaterOrEqual(t56.RideT0 + t56.Seconds, 122f, "through the gate at ride second 121");
            // D3: the bolt is released at beat-2 time 15 s (d 82.5) and recorded from the walker's own pose through fall second 12
            SeqDef bolt = all.Find(d => d.Name == "cue_bolt");
            Assert.AreEqual(SeqKind.WalkAuto, bolt.Kind);
            Assert.AreEqual(CueRef.Bolt, bolt.Cue);
            Assert.AreEqual(AbyssConst.BEAT_START_D[1] + 22.5f, QaSequenceCatalog.BoltReleaseD, 1e-3f);
            Assert.Less(bolt.D0, QaSequenceCatalog.BoltReleaseD);
            Assert.GreaterOrEqual(bolt.D0 + bolt.Speed * bolt.Seconds, QaSequenceCatalog.BoltReleaseD + 12f * AbyssConst.V_WALK, "fall seconds 0-12 recorded");
            // R2-20: the ignition at 10 fps written, marks up to the door lamp at +14.5 s
            SeqDef ign = all.Find(d => d.Name == "cue_ignition");
            Assert.AreEqual(10f, (float)ign.Fps / ign.RecordEvery, 1e-4f);
            Assert.AreEqual(CueRef.Ignition, ign.Cue);
            CollectionAssert.Contains(ign.CueMarks, 14.5);
            // R2-05: the ending judged on warm pixels of the lower half at goal +3 s
            SeqDef end = all.Find(d => d.Name == "cue_ending");
            Assert.AreEqual(CueRef.Goal, end.Cue);
            CollectionAssert.Contains(end.CueMarks, QaSequenceCatalog.EndingWarmMark);
            Assert.AreEqual(0.5f, end.WarmFromRow);
            Assert.IsTrue(QaCaptureRunner.IsWarm(200, 120, 60) && !QaCaptureRunner.IsWarm(120, 125, 130) && !QaCaptureRunner.IsWarm(25, 10, 2));
        }

        [Test]
        public void R2_T12ApertureInsetAndStop()
        {
            KeyShot ks1b = Key("KS1b_doorway");
            Assert.IsTrue(KeyShots.DoorwayAperture(ks1b.Eye, ks1b.Rotation, ks1b.Fov, 16f / 9f, AbyssConst.CAMERA_NEAR, out float a0, out _, out float a1, out _));
            Assert.IsTrue(KeyShots.DoorwayAperture(ks1b.Eye, ks1b.Rotation, ks1b.Fov, 16f / 9f, AbyssConst.CAMERA_NEAR, KeyShots.SequenceApertureInset,
                out float b0, out _, out float b1, out _));
            Assert.Less(b1 - b0, a1 - a0, "the sequence region sits further inside the jambs");
            Assert.AreEqual((a1 - a0) / 0.9f * 0.8f, b1 - b0, 1e-4f, "10 % per side instead of 5 %");
            Assert.That(QaSequenceCatalog.T12_EV_REGION_STOP_D, Is.InRange(QaSequenceCatalog.T12_EV_D0, AbyssConst.EXIT_DOOR_Z));
        }

        [Test]
        public void DoorwayAperture_PerFrameRegion()
        {
            KeyShot ks1b = Key("KS1b_doorway");
            Assert.IsTrue(KeyShots.DoorwayAperture(ks1b.Eye, ks1b.Rotation, ks1b.Fov, 16f / 9f, AbyssConst.CAMERA_NEAR,
                out float x0, out float y0, out float x1, out float y1));
            Assert.AreEqual(0.5f, 0.5f * (x0 + x1), 1e-3f, "centred aperture");
            Assert.Less(x1 - x0, 0.2f, "small at 5 m");
            Quaternion level = AbyssConst.YawPitch(0f, 0f);
            // inside the 0.8 m reveal only the outer plane bounds the view
            Assert.IsTrue(KeyShots.DoorwayAperture(new Vector3(0f, -1.3f, 59.6f), level, 62f, 16f / 9f, AbyssConst.CAMERA_NEAR,
                out float a0, out _, out float a1, out _));
            Assert.Greater(a1 - a0, 0.5f, "wide just behind the outer plane");
            Assert.IsFalse(KeyShots.DoorwayAperture(new Vector3(0f, -1.3f, 60.5f), level, 62f, 16f / 9f, AbyssConst.CAMERA_NEAR,
                out _, out _, out _, out _), "past the doorway: no aperture (the whole frame)");
        }
    }
}
