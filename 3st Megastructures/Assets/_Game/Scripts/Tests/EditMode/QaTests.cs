// QaTests: EditMode tests of the pure helpers of the runtime QA harness (MASTER_PROMPT 11.2, 11.8,
// 14.10.2): the rectilinear projection used for the framing acceptances, the key-shot subjects against
// the positions the spec states, the capture plan (every key shot at both resolutions, wide shots with
// their in-game FOV version, framing variants, tag format beatNN_<name>_<tag>, sequence windows),
// the image metrics, the perf summary, the soak growth rule and the JSON writer.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Megastructure.QA;
using Megastructure.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    public class QaTests
    {
        static QaView View169(double yaw, double pitch, double fov) => new QaView(0, 1.7, 0, yaw, pitch, fov, 1920, 1080);

        static KeyShot Key(string tag)
        {
            int i = ShotPlan.FindKeyShot(tag);
            Assert.GreaterOrEqual(i, 0, "key shot " + tag + " missing from ThreadConstants.KeyShots");
            return ThreadConstants.KeyShots[i];
        }

        static QaView KeyView(KeyShot k, int w, int h) => new QaView(k.x, k.y, k.z, k.yaw, k.pitch, k.fov, w, h);

        static QaSubject Find(List<QaSubject> list, string name)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].name == name) return list[i];
            Assert.Fail("subject " + name + " missing");
            return null;
        }

        // ------------------------------------------------------------------ projection

        [Test]
        public void Projection_ForwardMapsToCentre()
        {
            var v = View169(37.0, -12.0, 64.0);
            Assert.IsTrue(QaProjection.ProjectDir(v, QaProjection.AzElDir(37.0, -12.0), out double x, out double y));
            Assert.AreEqual(0.0, x, 1e-9);
            Assert.AreEqual(0.0, y, 1e-9);
        }

        [Test]
        public void Projection_VerticalFovEdgeIsNdcOne_HorizontalFollowsAspect()
        {
            var v = View169(0, 0, 62.0);
            Assert.IsTrue(QaProjection.ProjectDir(v, QaProjection.AzElDir(0, 31.0), out _, out double y));
            Assert.AreEqual(1.0, y, 1e-9);
            double tanH = Math.Tan(31.0 * ThreadMath.Deg) * 1920.0 / 1080.0;   // Hor+: vertical FOV fixed
            double az = Math.Atan(tanH) / ThreadMath.Deg;
            Assert.IsTrue(QaProjection.ProjectDir(v, QaProjection.AzElDir(az, 0), out double x, out _));
            Assert.AreEqual(1.0, x, 1e-9);
        }

        [Test]
        public void Projection_BehindCameraIsRejected()
        {
            var v = View169(0, 0, 64.0);
            Assert.IsFalse(QaProjection.ProjectDir(v, new D3(0, 0, -1), out _, out _));
        }

        [Test]
        public void Projection_BasisMatchesUnityEulerConvention()
        {
            double[] yaws = { 0, 32, -32.7, 145, 327.3 };
            double[] pitches = { 0, 12, -55, 65, 89 };
            for (int i = 0; i < yaws.Length; i++)
            {
                QaProjection.Basis(yaws[i], pitches[i], out D3 f, out D3 r, out D3 u);
                Quaternion q = Quaternion.Euler(-(float)pitches[i], (float)yaws[i], 0f);
                Vector3 uf = q * Vector3.forward, ur = q * Vector3.right, uu = q * Vector3.up;
                Assert.AreEqual(uf.x, f.x, 1e-5); Assert.AreEqual(uf.y, f.y, 1e-5); Assert.AreEqual(uf.z, f.z, 1e-5);
                Assert.AreEqual(ur.x, r.x, 1e-5); Assert.AreEqual(ur.y, r.y, 1e-5); Assert.AreEqual(ur.z, r.z, 1e-5);
                Assert.AreEqual(uu.x, u.x, 1e-5); Assert.AreEqual(uu.y, u.y, 1e-5); Assert.AreEqual(uu.z, u.z, 1e-5);
            }
        }

        [Test]
        public void Projection_PanStepMovesTheCentreByThePixelsAsked()
        {
            double step = QaProjection.PanStepDeg(0.1, 64.0, 1080);
            var v = View169(0, 0, 64.0);
            Assert.IsTrue(QaProjection.ProjectDir(v, QaProjection.AzElDir(0, step), out _, out double y));
            Assert.AreEqual(0.1, y * 540.0, 1e-6);
        }

        [Test]
        public void KeyFrameId_StripsVariantSuffixes()
        {
            Assert.AreEqual("KF2b", QaFraming.KeyFrameId("beat02_avenue_KF2b_1610"));
            Assert.AreEqual("KF4b", QaFraming.KeyFrameId("beat04_tower_KF4b_wide_4k"));
            Assert.AreEqual("KF7a", QaFraming.KeyFrameId("beat07_top_KF7a_s750"));
            Assert.AreEqual("KF6a2", QaFraming.KeyFrameId("beat06_clouds_KF6a2"));
            Assert.AreEqual("", QaFraming.KeyFrameId("beat01_salt_mid"));
        }

        // ------------------------------------------------------------------ key-shot subjects vs the spec (14.10.2 notes)

        [Test]
        public void Kf1_SunIsWhereTheSpecSays()
        {
            KeyShot k = Key("beat01_salt_KF1");
            var subs = QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080));
            QaSubject sun = Find(subs, "sun");
            Assert.IsTrue(sun.required);
            Assert.AreEqual(QaFraming.Kf1SunNdcX, sun.anchorNdcX, 0.01);
            Assert.AreEqual(QaFraming.Kf1SunNdcY, sun.anchorNdcY, 0.01);
            Assert.IsTrue(sun.ok, sun.verdict);
        }

        [Test]
        public void Kf1_ThreadBaseIsInTheAliasingCropAndAboutTwoPixelsWide()
        {
            KeyShot k = Key("beat01_salt_KF1");
            var subs = QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080));
            QaSubject th = Find(subs, "thread");
            Assert.IsTrue(th.hasAnchor);
            // 14.10.2 KF1 (line 947) centres the 64 x 256 aliasing crop at x = 1115 +- 32, the column of the true bearing 9.9 deg;
            // D-026 moves the apparent bearing to 1.33 deg in beat 1, so the crop is centred on the projected thread base:
            // the apparent axis (ThreadMath.ThreadAxis) where it meets the ground, projected with SkyMath (not QaProjection), about 981 px
            ThreadMath.ThreadAxis(k.s, out double xT, out double zT);
            var baseDir = new D3(xT - k.x, -k.y, zT - k.z);
            Assert.IsTrue(SkyMath.ProjectDirection(new D3(0, 0, 0), k.yaw, k.pitch, k.fov, 1920.0 / 1080.0, baseDir, out double bx, out _));
            double cropX = (bx + 1.0) * 0.5 * 1920.0;
            Assert.That(cropX - 32.0 >= 0.0 && cropX + 32.0 <= 1920.0, Is.True, "the 64 px aliasing crop at x = " + cropX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " lies inside the frame");
            Assert.AreEqual(cropX, th.anchorX, 32.0, "14.10.2 KF1: aliasing crop at the projected thread column +- 32 (spec 1115 at bearing 9.9; D-026)");
            Assert.That(th.widthPx, Is.InRange(1.8, 2.4), "thread law: 2.0 px at 1080p (14.7.3)");
            Assert.IsTrue(th.ok, th.verdict);
        }

        [Test]
        public void Kf7b_PlanetIsWhereTheSpecSaysAndInsideTheFrameAt169()
        {
            KeyShot k = Key("beat07_top_KF7b");
            var subs = QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080));
            QaSubject planet = Find(subs, "planet");
            // 14.10.2 (line 962) / 14.5.7 (line 750): centre (-0.54, +0.41) (QaFraming.Kf7bPlanetNdcX/Y) for yaw 0, pitch -1.
            // ThreadConstants.KeyShots moves KF7b to yaw -1, pitch -0.5 so the disc keeps the 3 % margin (at pitch -1 its top
            // reached y = 30.4 px of 1080, 2 px into the 32.4 px margin); the expected centre is the projection of the planet (azimuth -32,
            // elevation 12) from the actual key-shot pose, about (-0.52, +0.40), computed independently of QaProjection
            Assert.IsTrue(SkyTests.PoseNdc(k.yaw, k.pitch, k.fov, 1920.0 / 1080.0, ThreadConstants.PlanetAzimuthDeg, ThreadConstants.PlanetElevationDeg, out double ex, out double ey));
            Assert.AreEqual(ex, planet.anchorNdcX, 0.01);
            Assert.AreEqual(ey, planet.anchorNdcY, 0.01);
            Assert.IsTrue(planet.BBoxInsideFrame, "planet disc inside the 16:9 frame");
            // the decided pose exists to keep the 3 % margin, so it is now required (it was only reported at pitch -1)
            Assert.IsTrue(planet.BBoxInsideMargin, "planet disc inside the 3 % margin at 16:9 (14.5.7 acceptance 2): bbox px " +
                FormattableString.Invariant($"{planet.x0:F1}, {planet.y0:F1}, {planet.x1:F1}, {planet.y1:F1}"));
            Assert.AreEqual(planet.BBoxInsideMargin, planet.ok, "the BBoxInside rule is the 3 % margin rule");
            Assert.IsTrue(planet.ok, planet.verdict);
            Assert.AreEqual(1.0, planet.alpha, 1e-6, "planet fully faded in at s = 858");
            Assert.IsTrue(Find(subs, "beacon").ok);
        }

        [Test]
        public void Projection_DiscBoxMatchesAnIndependentDenseSampling()
        {
            // KF7b planet: the sky draws a screen-space circular impostor (D-046), so the reference is SkyTests.ImpostorBoxNdc,
            // computed independently from the actual key-shot pose (centre = PoseNdc, radius f tan(r)); the 180-sample box must
            // agree within 0.5 px. (The former reference, SkyTests.DiscBoxNdc, was the stretched true projection of the disc.)
            KeyShot k = Key("beat07_top_KF7b");
            QaSubject planet = Find(QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080)), "planet");
            Assert.IsTrue(SkyTests.ImpostorBoxNdc(k.yaw, k.pitch, k.fov, 1920.0 / 1080.0, ThreadConstants.PlanetAzimuthDeg, ThreadConstants.PlanetElevationDeg,
                                                  ThreadConstants.PlanetRadiusDeg, out double nx0, out double nx1, out double ny0, out double ny1));
            // half-extents (y up) to pixels with a top-left origin
            double px0 = (nx0 + 1.0) * 0.5 * 1920.0, px1 = (nx1 + 1.0) * 0.5 * 1920.0;
            double py0 = (1.0 - ny1) * 0.5 * 1080.0, py1 = (1.0 - ny0) * 0.5 * 1080.0;
            Assert.AreEqual(px0, planet.x0, 0.5);
            Assert.AreEqual(py0, planet.y0, 0.5);
            Assert.AreEqual(px1, planet.x1, 0.5);
            Assert.AreEqual(py1, planet.y1, 0.5);
        }

        [Test]
        public void Kf2b_HoodsOfPairs2To4AreInTheSpecRange()
        {
            KeyShot k = Key("beat02_avenue_KF2b");
            var subs = QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080));
            int hoods = 0;
            for (int i = 0; i < subs.Count; i++)
            {
                if (!subs[i].name.StartsWith("hood_", StringComparison.Ordinal)) continue;
                hoods++;
                Assert.IsTrue(subs[i].ok, subs[i].name + ": " + subs[i].verdict + " ndcY=" + subs[i].anchorNdcY);
            }
            Assert.AreEqual(6, hoods, "pairs 2-4, left and right");
        }

        [Test]
        public void Kf6b_PlanetNotYetVisible()
        {
            KeyShot k = Key("beat06_clouds_KF6b");
            QaSubject planet = Find(QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080)), "planet");
            Assert.AreEqual(FramingRule.Absent, planet.rule);
            Assert.IsTrue(planet.ok, planet.verdict);
        }

        [Test]
        public void Kf4a_CableCoversAQuarterOfTheWidth()
        {
            KeyShot k = Key("beat04_tower_KF4a");
            QaSubject cable = Find(QaFraming.Evaluate(k.tag, k.s, KeyView(k, 1920, 1080)), "cable");
            Assert.GreaterOrEqual(cable.VisibleWidthFraction, QaFraming.Kf4aMinCableWidth);
        }

        // ------------------------------------------------------------------ capture plan: key shots

        static List<StillShot> KeyPlan() => ShotPlan.Build("keyshots").stills;

        static StillShot FindStill(List<StillShot> list, string tag)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].tag == tag) return list[i];
            return null;
        }

        [Test]
        public void KeyShots_EveryShotAtBothResolutions()
        {
            var plan = KeyPlan();
            foreach (KeyShot k in ThreadConstants.KeyShots)
            {
                StillShot a = FindStill(plan, k.tag), b = FindStill(plan, k.tag + "_4k");
                Assert.IsNotNull(a, k.tag + " at 1920x1080");
                Assert.IsNotNull(b, k.tag + " at 3840x2160");
                Assert.AreEqual(1920, a.width); Assert.AreEqual(1080, a.height);
                Assert.AreEqual(3840, b.width); Assert.AreEqual(2160, b.height);
                Assert.AreEqual(k.x, a.x, 1e-9); Assert.AreEqual(k.y, a.y, 1e-9); Assert.AreEqual(k.z, a.z, 1e-9);
                Assert.AreEqual(k.yaw, a.yaw, 1e-9); Assert.AreEqual(k.pitch, a.pitch, 1e-9); Assert.AreEqual(k.s, a.s, 1e-9);
                Assert.IsTrue(a.framing);
            }
        }

        [Test]
        public void KeyShots_WideShotsAlsoGetTheInGameFovVersion()
        {
            var plan = KeyPlan();
            int wide = 0;
            foreach (KeyShot k in ThreadConstants.KeyShots)
            {
                StillShot game = FindStill(plan, k.tag);
                if (k.wide)
                {
                    wide++;
                    foreach (string suffix in new[] { "", "_4k" })
                    {
                        StillShot w = FindStill(plan, k.tag + "_wide" + suffix);
                        StillShot g = FindStill(plan, k.tag + suffix);
                        Assert.IsNotNull(w, k.tag + "_wide" + suffix);
                        Assert.AreEqual(k.fov, w.fov, 1e-9, "the _wide shot uses the table FOV");
                        Assert.IsFalse(w.gameFov);
                        Assert.AreEqual(ThreadMath.Fov(k.s), g.fov, 1e-9, "the plain tag uses FOV(s)");
                        Assert.IsTrue(g.gameFov);
                    }
                }
                else
                {
                    Assert.AreEqual(k.fov, game.fov, 1e-9);
                    Assert.IsNull(FindStill(plan, k.tag + "_wide"), "no _wide variant for " + k.tag);
                }
            }
            Assert.AreEqual(2, wide, "14.10.2: KF4b and KF7c are the _wide shots");
        }

        [Test]
        public void KeyShots_TableFovMatchesGameFovUnlessWide()
        {
            foreach (KeyShot k in ThreadConstants.KeyShots)
            {
                double d = Math.Abs(k.fov - ThreadMath.Fov(k.s));
                if (k.wide) Assert.Greater(d, 0.5, k.tag);
                else Assert.LessOrEqual(d, 0.5, k.tag + " (FOV rule of 14.10.2)");
            }
        }

        [Test]
        public void KeyShots_FramingVariantsAt1610And219()
        {
            var plan = KeyPlan();
            foreach (KeyShot k in ThreadConstants.KeyShots)
            {
                StillShot a = FindStill(plan, k.tag + "_1610"), b = FindStill(plan, k.tag + "_219");
                Assert.IsNotNull(a, k.tag + "_1610");
                Assert.IsNotNull(b, k.tag + "_219");
                Assert.AreEqual(1680, a.width); Assert.AreEqual(1050, a.height);
                Assert.AreEqual(2560, b.width); Assert.AreEqual(1080, b.height);
                Assert.IsTrue(a.framing && b.framing);
                Assert.AreEqual(ThreadMath.Fov(k.s), a.fov, 0.5);
            }
        }

        [Test]
        public void KeyShots_ExtraStillsOfTheTableNotes()
        {
            var plan = KeyPlan();
            StillShot s305 = FindStill(plan, "beat03_stairs_KF3_s305");
            Assert.IsNotNull(s305);
            Assert.AreEqual(305.0, s305.s, 1e-9);
            Assert.AreEqual(ThreadMath.PathY(305) + ThreadConstants.EyeHeight, s305.y, 1e-9);
            StillShot s750 = FindStill(plan, "beat07_top_KF7a_s750");
            Assert.IsNotNull(s750);
            Assert.AreEqual(750.0, s750.s, 1e-9);
            Assert.IsNotNull(FindStill(plan, "beat07_top_KF7a_s750_4k"));
        }

        [Test]
        public void KeyShots_TagBeatMatchesTheBeatOfS()
        {
            var rx = new Regex("^beat(0[1-7])_([a-z]+)_KF");
            foreach (KeyShot k in ThreadConstants.KeyShots)
            {
                Match m = rx.Match(k.tag);
                Assert.IsTrue(m.Success, k.tag);
                int nn = int.Parse(m.Groups[1].Value);
                Assert.AreEqual(ThreadMath.BeatIndex(k.s) + 1, nn, k.tag + " s=" + k.s);
                Assert.AreEqual(ThreadConstants.BeatNames[nn - 1], m.Groups[2].Value, k.tag);
            }
        }

        // ------------------------------------------------------------------ capture plan: beats and tags

        [Test]
        public void Tags_FollowBeatNameTagPattern()
        {
            var rx = new Regex("^beat(0[1-7])_([a-z]+)_([A-Za-z0-9_]+)$");
            var plan = ShotPlan.Build("keyshots,beats");
            Assert.Greater(plan.stills.Count, 100);
            foreach (StillShot st in plan.stills)
            {
                Match m = rx.Match(st.tag);
                Assert.IsTrue(m.Success, "tag " + st.tag + " is not beatNN_<name>_<tag>");
                int nn = int.Parse(m.Groups[1].Value);
                Assert.AreEqual(ThreadConstants.BeatNames[nn - 1], m.Groups[2].Value, st.tag);
                Assert.AreEqual(st.tag + ".png", st.File);
            }
        }

        [Test]
        public void BeatStills_StartMidEndAndFreeLook()
        {
            var plan = ShotPlan.Build("beats").stills;
            Assert.AreEqual(7 * 6 + 3 * 2, plan.Count);
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 0; i < ThreadConstants.BeatCount; i++)
            {
                string pre = ShotPlan.BeatPrefix(i);
                StillShot start = FindStill(plan, pre + "_start"), mid = FindStill(plan, pre + "_mid"), end = FindStill(plan, pre + "_end");
                Assert.IsNotNull(start); Assert.IsNotNull(mid); Assert.IsNotNull(end);
                Assert.AreEqual(b[i] + 2, start.s, 1e-9);
                Assert.AreEqual(0.5 * (b[i] + b[i + 1]), mid.s, 1e-9);
                Assert.AreEqual(b[i + 1] - 2, end.s, 1e-9);
                double heading = ThreadMath.Heading(mid.s) / ThreadMath.Deg;
                Assert.AreEqual(heading + 90, FindStill(plan, pre + "_look_r").yaw, 1e-9);
                Assert.AreEqual(heading - 90, FindStill(plan, pre + "_look_l").yaw, 1e-9);
                Assert.AreEqual(heading + 180, FindStill(plan, pre + "_look_back").yaw, 1e-9);
                Assert.AreEqual(0.0, FindStill(plan, pre + "_look_back").pitch, 1e-9);
                bool upDown = i >= 4;
                Assert.AreEqual(upDown, FindStill(plan, pre + "_look_up") != null, pre + " look_up");
                Assert.AreEqual(upDown, FindStill(plan, pre + "_look_down") != null, pre + " look_down");
                foreach (StillShot st in new[] { start, mid, end })
                {
                    D3 p = ThreadMath.PathPoint(st.s);
                    Assert.AreEqual(p.x, st.x, 1e-9); Assert.AreEqual(p.z, st.z, 1e-9);
                    Assert.AreEqual(p.y + ThreadConstants.EyeHeight, st.y, 1e-9);
                    Assert.AreEqual(ThreadMath.Heading(st.s) / ThreadMath.Deg, st.yaw, 1e-9);
                    Assert.AreEqual(ThreadConstants.GazeDefaultPitch, st.pitch, 1e-9);
                    Assert.AreEqual(ThreadMath.Fov(st.s), st.fov, 1e-9);
                    Assert.AreEqual(1920, st.width); Assert.AreEqual(1080, st.height);
                }
            }
        }

        [Test]
        public void Plan_TagsFilesAndSequenceNamesAreUnique()
        {
            var plan = ShotPlan.Build("everything", new CapturePlanOptions { encode4k = true });
            var seen = new HashSet<string>();
            foreach (StillShot st in plan.stills) Assert.IsTrue(seen.Add(st.File), "duplicate " + st.File);
            var seq = new HashSet<string>();
            foreach (SequenceSpec sq in plan.sequences) Assert.IsTrue(seq.Add(sq.name), "duplicate " + sq.name);
            Assert.IsNotNull(plan.parity);
        }

        [Test]
        public void Plan_SetNamesAndFilters()
        {
            var p = ShotPlan.Build("stills,bogus");
            Assert.IsNotNull(p.parity);
            Assert.AreEqual(0, p.sequences.Count);
            CollectionAssert.Contains(p.unknownSets, "bogus");
            var all = ShotPlan.Build("all");
            Assert.IsFalse(all.sequences.Exists(s => s.set == "encode"), "encode clips only with 'encode' or 'everything'");
            var only = ShotPlan.Build("keyshots,sequences", new CapturePlanOptions { only = new[] { "KF7b", "seq_swap" } });
            Assert.IsTrue(only.stills.TrueForAll(s => s.tag.Contains("KF7b")));
            Assert.AreEqual(1, only.sequences.Count);
            Assert.AreEqual("seq_swap", only.sequences[0].name);
        }

        // ------------------------------------------------------------------ capture plan: sequences

        static SequenceSpec FindSeq(List<SequenceSpec> list, string name)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].name == name) return list[i];
            return null;
        }

        [Test]
        public void Sequences_EveryTransitionWindowCoveredForAtLeast20s()
        {
            var seqs = ShotPlan.Build("transitions").sequences;
            int n = ShotPlan.TransitionWindows.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                SequenceSpec sq = FindSeq(seqs, "seq_b" + (i + 1) + (i + 2));
                Assert.IsNotNull(sq);
                Assert.AreEqual(30, sq.fps);
                Assert.GreaterOrEqual(sq.Seconds, 20.0 - 1e-9, sq.name);
                Assert.LessOrEqual(sq.s0, ShotPlan.TransitionWindows[i, 0] + 1e-9, sq.name);
                Assert.GreaterOrEqual(sq.s1, ShotPlan.TransitionWindows[i, 1] - 1e-9, sq.name);
                // the last frame lands on s1
                sq.FramePose(sq.frames - 1, out double sLast, out _, out _, out _, out _, out _, out _);
                Assert.AreEqual(sq.s1, sLast, 1e-6, sq.name);
            }
            SequenceSpec swap = FindSeq(seqs, "seq_swap");
            Assert.AreEqual(280.0, swap.s0, 1e-9);
            Assert.AreEqual(290.0, swap.s1, 1e-9);
            Assert.AreEqual(201, swap.frames, "10 m at 1.5 m/s and 30 fps, both ends included");
            Assert.IsNotNull(FindSeq(seqs, "seq_bankexit"));
            Assert.IsNotNull(FindSeq(seqs, "seq_deck"));
        }

        [Test]
        public void Sequences_WalkPoseFollowsThePathCentreLookingForward()
        {
            SequenceSpec sq = FindSeq(ShotPlan.Build("transitions").sequences, "seq_b56");
            for (int n = 0; n < sq.frames; n += 97)
            {
                sq.FramePose(n, out double s, out double x, out double y, out double z, out double yaw, out double pitch, out double fov);
                Assert.AreEqual(sq.s0 + ThreadConstants.WalkSpeed * n / 30.0, s, 1e-9);
                D3 p = ThreadMath.PathPoint(s);
                Assert.AreEqual(p.x, x, 1e-9); Assert.AreEqual(p.y + ThreadConstants.EyeHeight, y, 1e-9); Assert.AreEqual(p.z, z, 1e-9);
                Assert.AreEqual(ThreadMath.Heading(s) / ThreadMath.Deg, yaw, 1e-9);
                Assert.AreEqual(ThreadConstants.GazeDefaultPitch, pitch, 1e-9);
                Assert.AreEqual(ThreadMath.Fov(s), fov, 1e-9);
            }
        }

        [Test]
        public void Sequences_StaticClipsPerBeatAndBankPositions()
        {
            var seqs = ShotPlan.Build("statics").sequences;
            for (int i = 0; i < ThreadConstants.BeatCount; i++)
            {
                SequenceSpec sq = FindSeq(seqs, "seq_static_" + ShotPlan.BeatPrefix(i));
                Assert.IsNotNull(sq);
                Assert.AreEqual(90, sq.frames, "3 s at 30 fps");
                sq.FramePose(0, out _, out double x0, out double y0, out double z0, out double yaw0, out _, out _);
                sq.FramePose(89, out _, out double x1, out double y1, out double z1, out double yaw1, out _, out _);
                Assert.AreEqual(x0, x1); Assert.AreEqual(y0, y1); Assert.AreEqual(z0, z1); Assert.AreEqual(yaw0, yaw1);
            }
            foreach (double s in ShotPlan.BankStaticS)
                Assert.IsNotNull(FindSeq(seqs, "seq_bank_s" + ((int)s).ToString("D3")), "T-BANK s=" + s);
        }

        [Test]
        public void Sequences_SlowPansAndMoirePans()
        {
            var seqs = ShotPlan.Build("pans").sequences;
            for (int i = 4; i < ThreadConstants.BeatCount; i++)
            {
                SequenceSpec up = FindSeq(seqs, "seq_pan_" + ShotPlan.BeatPrefix(i) + "_up");
                SequenceSpec down = FindSeq(seqs, "seq_pan_" + ShotPlan.BeatPrefix(i) + "_down");
                Assert.IsNotNull(up); Assert.IsNotNull(down);
                Assert.AreEqual(5, up.frames);
                double px = Math.Tan(Math.Abs(up.panStepDeg) * ThreadMath.Deg) * QaProjection.FocalPx(up.fov, up.height);
                Assert.AreEqual(0.1, px, 1e-9, "0.1 px per frame");
                up.FramePose(4, out _, out _, out _, out _, out _, out double pUp, out _);
                Assert.Less(pUp, 89.0); Assert.Greater(pUp, 88.0);
            }
            foreach (string m in ShotPlan.MoireNames)
            {
                SequenceSpec sq = FindSeq(seqs, "seq_moire_" + m);
                Assert.IsNotNull(sq);
                double px = Math.Tan(sq.panStepDeg * ThreadMath.Deg) * QaProjection.FocalPx(sq.fov, sq.height);
                Assert.AreEqual(1.0, px, 1e-9, "1 px per frame");
                Assert.Greater(sq.pitch, 0.0, "aims up the stairs");
            }
        }

        [Test]
        public void Sequences_StartFirstAndEndingLast()
        {
            var seqs = ShotPlan.Build("everything").sequences;
            Assert.AreEqual(SeqMode.RealStart, seqs[0].mode);
            Assert.AreEqual(60, seqs[0].frames);
            SequenceSpec end = seqs[seqs.Count - 1];
            Assert.AreEqual(SeqMode.RealEnd, end.mode);
            Assert.Less(end.s0, ThreadConstants.BeaconTriggerS);
            Assert.GreaterOrEqual(end.Seconds, ThreadConstants.EndLineOut1 + 2.0, "covers the 14 s ending");
        }

        [Test]
        public void Encode_TenSecondsAt60FpsAnd4kOnRequest()
        {
            var a = ShotPlan.Build("encode").sequences;
            Assert.Greater(a.Count, 8);
            foreach (SequenceSpec sq in a)
            {
                Assert.AreEqual(60, sq.fps);
                Assert.AreEqual(1920, sq.width);
                Assert.IsTrue(Math.Abs(sq.Seconds - ShotPlan.EncodeSeconds) < 1e-9 || Math.Abs(sq.Seconds - ShotPlan.EncodeTransitionSeconds) < 1e-9, sq.name + " " + sq.Seconds + " s");
                Assert.LessOrEqual(sq.s0 + sq.Seconds * ThreadConstants.WalkSpeed, ThreadConstants.BeaconTriggerS, sq.name + " stays before the trigger");
            }
            var b = ShotPlan.Build("encode", new CapturePlanOptions { encode4k = true }).sequences;
            Assert.AreEqual(2 * a.Count, b.Count);
            Assert.IsTrue(b.Exists(s => s.width == 3840 && s.height == 2160));
        }

        // ------------------------------------------------------------------ metrics, perf, soak, json

        [Test]
        public void CaptureMetrics_LumaClipMagentaDiffAndPacking()
        {
            // 2 x 2 RGBA: magenta, white / black, grey (row 0 first)
            byte[] rgba = { 255, 0, 255, 255, 255, 255, 255, 255, 0, 0, 0, 255, 128, 128, 128, 255 };
            byte[] prev = new byte[16];
            byte[] rgb = new byte[12];
            var r = new CaptureResult();
            CaptureMetrics.Compute(rgba, prev, 2, 2, rgb, null, true, new int[256], r);
            double expectLuma = (CaptureMetrics.WR * (255 + 255 + 0 + 128) + CaptureMetrics.WG * (0 + 255 + 0 + 128) + CaptureMetrics.WB * (255 + 255 + 0 + 128)) / (255.0 * 4);
            Assert.AreEqual(expectLuma, r.meanLuma, 1e-12);
            Assert.AreEqual(1, r.magentaPixels);
            Assert.AreEqual(0.5, r.clippedFraction, 1e-12);
            Assert.AreEqual((510 + 765 + 384) / 12.0, r.meanAbsDiff, 1e-12);
            // flipped: source row 0 lands in destination row 1
            Assert.AreEqual(new byte[] { 0, 0, 0, 128, 128, 128, 255, 0, 255, 255, 255, 255 }, rgb);
            Assert.IsFalse(r.Black);
        }

        [Test]
        public void PerfSummary_AveragesLowsAndBudget()
        {
            var ms = new float[1000];
            for (int i = 0; i < ms.Length; i++) ms[i] = 10f;
            for (int i = 0; i < 10; i++) ms[i * 100] = 40f;   // ten slow frames = the slowest 1 %
            PerfSummary s = PerfSummary.From(ms, ms.Length, 33.0);
            Assert.AreEqual(1000, s.frames);
            Assert.AreEqual(10, s.framesOverBudget);
            Assert.AreEqual(40.0, s.maxMs, 1e-6);
            Assert.AreEqual(1000.0 * 1000 / (990 * 10 + 10 * 40), s.avgFps, 1e-6);
            Assert.AreEqual(25.0, s.low1Fps, 1e-6, "mean of the slowest 1 % = 40 ms");
            Assert.AreEqual(25.0, s.low01Fps, 1e-6);
        }

        [Test]
        public void Soak_GrowthUsesWarmupMinimumAndTailMinimum()
        {
            double[] t = { 0, 10, 20, 30, 40, 50, 60, 70 };
            long[] v = { 900, 100, 120, 110, 300, 250, 260, 255 };
            // warm-up 10 s: baseline = min over t in [10, 40] = 100; tail = min of the last three = 250
            Assert.AreEqual(150.0, SoakRunner.Growth(v, t, v.Length, 10.0), 1e-9);
        }

        [Test]
        public void AutoTest_ExpectedTimesOf14_10_4()
        {
            Assert.AreEqual(573.33, AutoTestRunner.ExpectedSeconds(ThreadConstants.WalkSpeed), 0.01);
            Assert.AreEqual(8.0, AutoTestRunner.ToleranceSeconds(ThreadConstants.WalkSpeed));
            Assert.AreEqual(344.0, AutoTestRunner.ExpectedSeconds(ThreadConstants.BriskSpeed), 0.01);
            Assert.AreEqual(5.0, AutoTestRunner.ToleranceSeconds(ThreadConstants.BriskSpeed));
        }

        [Test]
        public void QaMode_PrecedenceAndSpeed()
        {
            try
            {
                CliArgs.Override(new[] { "Thread.exe", "-autotest", "-wallbot" });
                Assert.AreEqual(QaRunMode.WallBot, QaMode.Resolve());
                CliArgs.Override(new[] { "Thread.exe", "-autotest", "-capture", "x" });
                Assert.AreEqual(QaRunMode.Capture, QaMode.Resolve());
                CliArgs.Override(new[] { "Thread.exe", "-soak", "120", "-autotest" });
                Assert.AreEqual(QaRunMode.Soak, QaMode.Resolve());
                CliArgs.Override(new[] { "Thread.exe", "-autotest", "-brisk" });
                Assert.AreEqual(QaRunMode.AutoTest, QaMode.Resolve());
                Assert.AreEqual(ThreadConstants.BriskSpeed, QaMode.DriveSpeed, 1e-6);
                Assert.AreEqual("brisk", QaMode.SpeedName(QaMode.DriveSpeed));
                CliArgs.Override(new[] { "Thread.exe" });
                Assert.AreEqual(QaRunMode.None, QaMode.Resolve());
                Assert.AreEqual("walk", QaMode.SpeedName(QaMode.DriveSpeed));
            }
            finally { CliArgs.Override(null); }
        }

        [Test]
        public void Json_EscapesStringsAndWritesInvariantNumbers()
        {
            var j = new QaJson();
            j.BeginObject().Prop("a", 1.5).Prop("s", "q\"\\\n").Prop("nan", double.NaN).Prop("b", true);
            j.Key("arr").BeginArray(true).Value(1).Value(2.25).EndArray();
            j.EndObject();
            string s = j.ToString();
            StringAssert.Contains("\"a\": 1.5", s);
            StringAssert.Contains("\"s\": \"q\\\"\\\\\\n\"", s);
            StringAssert.Contains("\"nan\": null", s);
            StringAssert.Contains("\"arr\": [1, 2.25]", s);
            Assert.AreEqual('}', s[s.Length - 1]);
        }
    }
}
