using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>The key-shot table (14.10), the capture plans (Part 11.2) and the analytic composition check (Part 10 #35).</summary>
    public class KeyShotTests
    {
        [Test]
        public void Table_HasSixteenUniqueShots()
        {
            Assert.AreEqual(16, KeyShots.All.Length);
            var ids = new HashSet<string>(); var tags = new HashSet<string>();
            for (int i = 0; i < KeyShots.All.Length; i++)
            {
                var k = KeyShots.All[i];
                Assert.AreEqual(i, k.Index);
                Assert.AreEqual("KS" + i.ToString("00"), k.Id);
                Assert.IsTrue(ids.Add(k.Id)); Assert.IsTrue(tags.Add(k.Tag), "unique tag " + k.Tag);
                Assert.IsTrue(Regex.IsMatch(k.Tag, "^[a-z0-9_]+$"), "tag is a file-name suffix");
                Assert.GreaterOrEqual(k.Vfov, 60f); Assert.LessOrEqual(k.Vfov, 68f);
            }
            Assert.AreEqual(3.0, KeyShots.All[0].T, 1e-9, "KS00 at t 3.0 s");
            Assert.IsTrue(KeyShots.All[2].ExtraAspects && KeyShots.All[6].ExtraAspects && KeyShots.All[12].ExtraAspects);
        }

        [Test]
        public void Table_EyesLieOnThePathAtEyeHeight()
        {
            foreach (var k in KeyShots.All)
            {
                float s = RingWorld.Project(new Vector2(k.Eye.x, k.Eye.z));
                Assert.AreEqual(k.S, s, 0.05f, k.Id + " s equals the projection of its eye");
                if (k.Index == 0) { Assert.AreEqual(RingConstants.CapsuleFloorY + RingConstants.Eye, k.Eye.y, 1e-3f); continue; }
                Assert.AreEqual(QaPath.FloorY(k.S) + RingConstants.Eye, k.Eye.y, 0.01f, k.Id + " eye height");
            }
            // KS01 lateral offset is inside the 0.95 m capsule half-lane (14.5.1)
            Assert.LessOrEqual(Mathf.Abs(KeyShots.All[1].Eye.x), 0.95f);
        }

        [Test]
        public void FileNames_FollowThePattern()
        {
            var k = KeyShots.All[2];
            Assert.AreEqual("ks02_valley_crest_1920x1080.png", KeyShots.FileName(k, 1920, 1080));
            Assert.AreEqual(4, KeyShots.ResolutionsFor(k).Length);
            Assert.AreEqual(2, KeyShots.ResolutionsFor(KeyShots.All[3]).Length);
            Assert.IsTrue(KeyShots.TryFind("KS12", out var a) && a.Index == 12);
            Assert.IsTrue(KeyShots.TryFind("swarm", out var b) && b.Index == 12);
            Assert.IsTrue(KeyShots.TryFind("ks5", out var c) && c.Index == 5);
            Assert.IsFalse(KeyShots.TryFind("ks99", out _));
        }

        [Test]
        public void Plans_AllContainsEveryRequiredImage()
        {
            var unknown = new List<string>();
            var plan = CapturePlans.Parse("all", null, unknown);
            Assert.AreEqual(0, unknown.Count);
            var files = new HashSet<string>();
            foreach (var s in plan) Assert.IsTrue(files.Add(s.File), "unique file " + s.File);
            foreach (var k in KeyShots.All)
                foreach (var r in KeyShots.ResolutionsFor(k))
                    Assert.IsTrue(files.Contains(KeyShots.FileName(k, r.x, r.y)), "plan contains " + KeyShots.FileName(k, r.x, r.y));
            var still = new Regex(@"^beat0[1-7]_[a-z]+_(start|mid|end|free_yaw(\+90|-90|180)_p(0|60))\.png$");
            for (int b = 1; b <= 7; b++)
            {
                int n = 0;
                foreach (var f in files) if (f.StartsWith("beat0" + b + "_") && still.IsMatch(f)) n++;
                Assert.AreEqual(9, n, "beat " + b + ": start, mid, end and six free-looks");
            }
            foreach (var t in new[] { "t2_bridge_down_1920x1080.png", "t5_wall_foot_1920x1080.png", "t7_platform_back_1920x1080.png",
                                      "beat05_rimwall_x8_back_set0_1920x1080.png", "beat05_rimwall_x8_back_set1_1920x1080.png",
                                      "beat05_rimwall_x6_s700_yaw90_p0_3840x2160.png", "beat05_rimwall_x7_s700_yaw0_p80_3840x2160.png" })
                Assert.IsTrue(files.Contains(t), "plan contains " + t);
            // capture poses of key shots come only from the table
            foreach (var s in plan)
                if (s.IsKeyShot)
                {
                    Assert.AreEqual(s.Ks.Eye, s.Eye); Assert.AreEqual(s.Ks.Yaw, s.Yaw); Assert.AreEqual(s.Ks.Pitch, s.Pitch); Assert.AreEqual(s.Ks.Vfov, s.Vfov169);
                }
            // -res override
            var one = CapturePlans.Parse("KS02,beat3", new Vector2Int(1280, 720), unknown);
            foreach (var s in one) { Assert.AreEqual(1280, s.W); Assert.AreEqual(720, s.H); }
        }

        [Test]
        public void Sequences_AllPlansAreWellFormed()
        {
            var all = CapturePlans.AllSequences();
            var names = new HashSet<string>();
            foreach (var p in all)
            {
                Assert.IsTrue(names.Add(p.Name), "unique sequence " + p.Name);
                Assert.Greater(p.Frames, 1, p.Name);
                Assert.IsTrue(p.Fps == 30 || p.Fps == 60, p.Name + " fps");
                Assert.LessOrEqual(p.S1, RingConstants.PathLength); Assert.GreaterOrEqual(p.S0, 0f);
            }
            foreach (var n in new[] { "b12", "b23", "b34", "b45", "b56", "b67", "ark", "sunset", "exit", "static1", "static7", "enc_dusk", "enc_night", "enc_exit", "enc_swarm" })
                Assert.IsTrue(names.Contains(n), "sequence " + n);
            var sunset = all.Find(p => p.Name == "sunset");
            Assert.AreEqual(2101, sunset.Frames, "105 m at 1.5 m/s and 30 fps");
            var enc = all.Find(p => p.Name == "enc_dusk");
            Assert.AreEqual(601, enc.Frames, "10 s at 60 fps");
            Assert.AreEqual(6 + 3, CapturePlans.ParseSequences("boundaries", null).Count);
            // shimmer pans (14.10): every 1080p pan has a 2 x 2 supersampled twin with the same pose, pan and frames
            var pans = CapturePlans.ParseSequences("shimmer", null);
            Assert.GreaterOrEqual(pans.Count, 2);
            foreach (var p in pans)
            {
                if (p.Scale != 1) continue;
                var ss = all.Find(x => x.Name == p.Pair);
                Assert.IsNotNull(ss, p.Name + " has its supersampled pair");
                Assert.AreEqual(2, ss.Scale); Assert.AreEqual(p.Frames, ss.Frames); Assert.AreEqual(p.PanDegPerFrame, ss.PanDegPerFrame);
                Assert.AreEqual(p.Eye, ss.Eye); Assert.IsTrue(p.FreezeClock && ss.FreezeClock);
                Assert.Greater(p.PanDegPerFrame, 0f);
            }
            var k2 = KeyShots.All[2];
            Assert.AreEqual(0.5f * 2f * Mathf.Tan(0.5f * k2.Vfov * Mathf.Deg2Rad) / 1080f * Mathf.Rad2Deg, SequencePlan.DegPerPx1080(k2.Vfov, 0.5f), 1e-6f);
        }

        [Test]
        public void Composition_KeyShotsAnalytic()
        {
            foreach (var k in KeyShots.All)
            {
                var names = KeyShots.SubjectsFor(k);
                for (int a = 0; a < CompositionCheck.Aspects.Length; a++)
                {
                    var r = CompositionCheck.Evaluate(k.Eye, k.Yaw, k.Pitch, k.Vfov, CompositionCheck.Aspects[a], names, null);
                    foreach (var x in r)
                    {
                        if (x.Name == "Ark.Hull") continue;   // depends on the ark phase (RingState.ArkU), checked in the player
                        Assert.IsTrue(x.Resolved, k.Id + " " + x.Name + " resolves analytically");
                        if (x.MustBeInside) Assert.IsTrue(x.Inside, k.Id + " " + x.Name + " inside with 3 % margin at " + CompositionCheck.AspectNames[a] + " (viewport " + x.Min + " .. " + x.Max + ")");
                        else Assert.IsTrue(x.Visible, k.Id + " " + x.Name + " visible at " + CompositionCheck.AspectNames[a]);
                    }
                }
            }
        }

        [Test]
        public void Composition_KS05StarAndKS02Edge()
        {
            var k5 = KeyShots.All[5];
            // star 10 degrees left of and 6 degrees above the centre (14.5.4)
            var inv = Quaternion.Inverse(Quaternion.Euler(-k5.Pitch, k5.Yaw, 0f));
            Vector3 c = inv * RingConstants.StarL;
            Assert.AreEqual(-10f, Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg, 1.5f, "KS05 star horizontal offset");
            Assert.AreEqual(6f, Mathf.Atan2(c.y, c.z) * Mathf.Rad2Deg, 1.0f, "KS05 star vertical offset");
            // KS02: the terrain edge (14.67 deg) sits at the frame centre
            var k2 = KeyShots.All[2];
            var v = CompositionCheck.Project(k2.Eye, k2.Yaw, k2.Pitch, k2.Vfov, 16f / 9f, k2.Eye + RingConstants.DirFromAzEl(0f, RingConstants.ClipEdgeElevation) * 1000f);
            Assert.AreEqual(0.5f, v.x, 1e-3f); Assert.AreEqual(0.5f, v.y, 0.02f, "KS02 edge at the centre");
            // KS12: the star is 4 degrees right and 2 degrees below the centre; every band lies inside (largest 16.7 < 33)
            var k12 = KeyShots.All[12];
            var s12 = CompositionCheck.Project(k12.Eye, k12.Yaw, k12.Pitch, k12.Vfov, 16f / 9f, k12.Eye + RingConstants.StarL7 * 1000f);
            Assert.Greater(s12.x, 0.5f); Assert.Less(s12.y, 0.5f);
        }
    }
}
