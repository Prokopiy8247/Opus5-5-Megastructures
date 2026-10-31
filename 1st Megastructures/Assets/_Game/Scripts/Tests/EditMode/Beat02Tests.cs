using System;
using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 2 (14.5.2, 14.7.2, 14.10): ring layout (55 ribs + 19 belts on 74 levels, Simplified every 120 m), no two ring meshes
    /// sharing a coplanar overlapping face (world space, every placed ring piece, both tiers), rings clear of the doorway,
    /// landing, bridge, tunnel mouth, fan bore and hoist lane, ring ends 2 m outside Beat06's fan rim (R 156, 2 m proud); the generated bridge and shaft against the acceptance numbers
    /// (bridge 1.200 +/- 0.005 x 150.0 +/- 0.1, C1 O 44.0 +/- 0.1, 5 lamps at 37.5 +/- 0.1, shaft apothem 350) for every tier;
    /// mesh validity of every tier; the hoist ping-pong (rising phase over the crossing) and the falling bolt (lead decision D3: released
    /// from a belt lamp on the right far wall at beat-2 time 15 s, in open view below the deck); the far-layer washes behind C3,
    /// the outrigger lamp emitters (D4) and the deck's worn walking line.
    /// </summary>
    public sealed class Beat02Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";

        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Mesh> _meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) UnityEngine.Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
        }

        // ================================================================================================ ring layout

        [Test]
        public void RingLevelsAre55RibsAnd19Belts()
        {
            var levels = new List<Beat02Rings.Level>();
            Beat02Rings.Levels(false, true, levels);
            Assert.AreEqual(AbyssConst.RIB_LEVELS, levels.Count, "74 levels");
            int ribs = 0, belts = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                Beat02Rings.Level L = levels[i];
                Assert.AreEqual(i, L.K);
                Assert.That(L.Y, Is.EqualTo(AbyssConst.RingY(L.K)).Within(1e-4f), "level y");
                Assert.AreEqual(AbyssConst.RingIsBelt(L.K), L.Belt, "belt flag");
                if (i > 0) Assert.That(levels[i - 1].Y - L.Y, Is.EqualTo(AbyssConst.RIB_SPACING).Within(1e-3f), "60 m rhythm");
                if (L.Belt) belts++; else ribs++;
            }
            Assert.AreEqual(AbyssConst.RIB_COUNT, ribs, "ribs");
            Assert.AreEqual(AbyssConst.BELT_COUNT, belts, "belts");

            // Simplified (Fallbacks): ribs every 120 m, every belt kept
            Beat02Rings.Levels(true, false, levels);
            int sb = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Belt) sb++;
                if (i > 0) Assert.That(levels[i - 1].Y - levels[i].Y, Is.EqualTo(2f * AbyssConst.RIB_SPACING).Within(1e-3f), "120 m rhythm");
                Assert.IsFalse(levels[i].Lane, "no hoist lane without a hoist");
            }
            Assert.AreEqual(AbyssConst.BELT_COUNT, sb, "simplified keeps all belts");
        }

        [Test]
        public void RingSetPlacesEveryLevelWithItsRibOrBelt([Values(false, true)] bool simplified)
        {
            Beat02Rings.Set set = Beat02Rings.Build(simplified, !simplified);
            _meshes.AddRange(set.Meshes);
            Assert.AreEqual(simplified ? 18 : AbyssConst.RIB_COUNT, set.Ribs, "ribs");
            Assert.AreEqual(AbyssConst.BELT_COUNT, set.Belts, "belts");
            var seen = new HashSet<int>();
            int instanced = 0, separate = 0;
            foreach (Beat02Rings.Draw d in set.Draws)
            {
                seen.Add(d.K);
                if (d.Instanced) instanced++; else separate++;
                Vector3 t = d.Matrix.GetColumn(3);
                Assert.That(t.y, Is.EqualTo(AbyssConst.RingY(d.K)).Within(1e-3f), "draw at its level");
                Assert.That((d.Matrix.lossyScale - Vector3.one).magnitude, Is.LessThan(1e-6f), "rings are never scaled");
            }
            Assert.AreEqual(set.Levels.Count, seen.Count, "every level is drawn");
            Assert.Greater(instanced, set.Levels.Count / 2, "most levels instanced");
            Assert.LessOrEqual(separate, 5, "only the bore levels are separate meshes");
            foreach (Mesh m in set.Meshes)
            {
                MeshReport r = MeshValidation.Validate(m);
                Assert.IsTrue(r.Ok, r.Describe());
            }
        }

        [Test]
        public void NoTwoRingMeshesShareACoplanarOverlappingFace([Values(false, true)] bool simplified)
        {
            Beat02Rings.Set set = Beat02Rings.Build(simplified, !simplified);
            _meshes.AddRange(set.Meshes);
            int overlaps = CountCoplanarOverlaps(set, out string first, out int triangles);
            Assert.Greater(triangles, 1000, "ring triangles collected");
            Assert.AreEqual(0, overlaps, "coplanar overlapping ring faces; first: " + first);
        }

        // Beat06's raised rim around the fan bore: collar R 146 to CITY_FAN_EXCLUSION_R (156), front face 2 m proud of the near
        // flat (Beat06_Fan COLLAR_Z_FRONT 62); ring ends keep 2 m (radially) from it.
        const float RimProud = 2f, RimClearance = 2f;

        [Test]
        public void RingsClearTheWalkwayOpeningsBoreAndHoistLane([Values(false, true)] bool simplified)
        {
            Beat02Rings.Set set = Beat02Rings.Build(simplified, !simplified);
            _meshes.AddRange(set.Meshes);
            Vector3 fan = AbyssConst.FAN_CENTRE, mouth = AbyssConst.W[15];
            var keepOut = new List<(string name, Bounds box)>
            {
                ("doorway", MinMax(new Vector3(-1f, -3.5f, 58f), new Vector3(1f, -0.5f, 62f))),
                ("landing and console", MinMax(new Vector3(-4.5f, -17f, 59f), new Vector3(4.5f, -1.5f, 66.5f))),
                ("bridge and truss", MinMax(new Vector3(-2f, -10f, 66f), new Vector3(2f, -1.5f, 216f))),
                ("tunnel mouth", MinMax(new Vector3(mouth.x - 2.5f, mouth.y - 1f, 58f), new Vector3(mouth.x + 2.5f, mouth.y + AbyssConst.TUNNEL_H + 1f, 62f))),
            };
            if (!simplified)   // Simplified has no hoist, so its far flat is not gapped
                keepOut.Add(("hoist travel", MinMax(new Vector3(Beat02_Bridge.HOIST_X - 5.5f, (float)Beat02_Bridge.HoistYMin - 5.5f, 749f),
                    new Vector3(Beat02_Bridge.HOIST_X + 5.5f, (float)Beat02_Bridge.HoistYTop + 5.5f, 760f))));

            // the fan: ring geometry in front of the near flat (z < 85) stays out of the bore circle R 150 (+1 m), and ring
            // geometry up to 2 m in front of the rim's face stays 2 m outside the rim (R 158), so no ring end runs into or kisses
            // the rim; the corner mitre of the k 24 belt may overhang the rim's footprint, but only 5.8 m or more in front of it
            float boreR = AbyssConst.FAN_R_OUT + 1f, boreZ = AbyssConst.SHAFT_NEAR_Z + 25f;
            float rimR = AbyssConst.CITY_FAN_EXCLUSION_R + RimClearance, rimZ = AbyssConst.SHAFT_NEAR_Z + RimProud + RimClearance;
            double rimMin = double.PositiveInfinity;
            string rimMinAt = "none";
            foreach (Beat02Rings.Draw d in set.Draws)
            {
                Vector3[] v = d.Mesh.vertices;
                int[] idx = d.Mesh.triangles;
                for (int i = 0; i < idx.Length; i += 3)
                {
                    Vector3 a = d.Matrix.MultiplyPoint3x4(v[idx[i]]), b = d.Matrix.MultiplyPoint3x4(v[idx[i + 1]]), c = d.Matrix.MultiplyPoint3x4(v[idx[i + 2]]);
                    var tb = new Bounds(a, Vector3.zero);
                    tb.Encapsulate(b); tb.Encapsulate(c);
                    foreach (var k in keepOut)
                        Assert.IsFalse(k.box.Intersects(tb), d.Mesh.name + " (level " + d.K + ") touches the " + k.name);
                    if (tb.min.z > boreZ) continue;
                    double dBore = PlanDistanceToFanAxis(a, b, c, boreZ, fan);
                    Assert.Greater(dBore, boreR, d.Mesh.name + " (level " + d.K + ") reaches into the fan bore");
                    double dRim = PlanDistanceToFanAxis(a, b, c, rimZ, fan);
                    if (dRim < rimMin) { rimMin = dRim; rimMinAt = d.Mesh.name + " (level " + d.K + ")"; }
                    Assert.GreaterOrEqual(dRim, rimR - 0.02, d.Mesh.name + " (level " + d.K + ") comes within " + RimClearance + " m of the fan rim");
                }
            }
            // the bore levels' ring ends sit on the clear circle itself (cut to it, not clear by accident of the layout)
            Assert.That(rimMin, Is.EqualTo(rimR).Within(0.05), "closest ring end to the rim: " + rimMinAt);
        }

        [Test]
        public void BoreLevelsAreGappedAroundTheRim()
        {
            var levels = new List<Beat02Rings.Level>();
            Beat02Rings.Levels(false, true, levels);
            Vector3 fan = AbyssConst.FAN_CENTRE;
            var bore = new List<int>();
            foreach (Beat02Rings.Level L in levels)
            {
                float hh = 0.5f * Beat02Rings.Height(L.Belt);
                float lo = L.Y - hh, hi = L.Y + hh;
                float dy = fan.y < lo ? lo - fan.y : (fan.y > hi ? fan.y - hi : 0f);
                Assert.AreEqual(dy < Beat02Rings.CLEAR_R, L.Bore, "level " + L.K + ": gapped exactly where the band reaches R 158");
                if (!L.Bore) continue;
                bore.Add(L.K);
                Beat02Rings.BoreGap(L.Y, hh, out float x0, out float x1);
                // the nearest corner of each ring end lies 2 m (radially) outside the rim
                Assert.That(Math.Sqrt((double)(fan.x - x0) * (fan.x - x0) + (double)dy * dy), Is.EqualTo(AbyssConst.CITY_FAN_EXCLUSION_R + RimClearance).Within(1e-3), "x0 end, level " + L.K);
                Assert.That(Math.Sqrt((double)(x1 - fan.x) * (x1 - fan.x) + (double)dy * dy), Is.EqualTo(AbyssConst.CITY_FAN_EXCLUSION_R + RimClearance).Within(1e-3), "x1 end, level " + L.K);
            }
            CollectionAssert.AreEqual(new[] { 23, 24, 25, 26, 27 }, bore, "bore levels");
        }

        // ================================================================================================ the beat

        [Test]
        public void BridgeAndShaftMatchTheAcceptanceNumbers([Values(BeatTier.Full, BeatTier.Simplified, BeatTier.Graybox)] BeatTier tier)
        {
            BeatBase b = Build(tier);
            var report = new ScaleReport();
            b.ReportScale(report);
            Expect(report, "bridge.width", AbyssConst.BRIDGE_W, 0.005);
            Expect(report, "bridge.length", AbyssConst.BRIDGE_L, 0.1);
            Expect(report, "bridge.top.y", AbyssConst.BRIDGE_Y, 0.001);
            Expect(report, "c1.diameter", 2.0 * AbyssConst.COLUMNS[0].z, 0.1);
            Expect(report, "c1.height", AbyssConst.COLUMN_TOP_Y - AbyssConst.COLUMN_BOTTOM_Y, 0.5);
            Expect(report, "c3.diameter", 2.0 * AbyssConst.COLUMNS[2].z, 0.1);
            Expect(report, "column.band.protrusion", AbyssConst.COLUMN_BAND_PROTRUSION, 0.005);
            Expect(report, "bridge.lamp.count", AbyssConst.BRIDGE_LAMP_Z.Length, 0.0);
            Expect(report, "bridge.lamp.spacing", AbyssConst.BRIDGE_LAMP_SPACING, 0.1);
            Expect(report, "bridge.lamp.spacing.inner", AbyssConst.BRIDGE_LAMP_SPACING, 0.001);
            Expect(report, "bridge.lamp.outside", AbyssConst.BRIDGE_LAMP_OUTSIDE, 0.001);
            Expect(report, "bridge.lamp.post.height", AbyssConst.BRIDGE_LAMP_POST_H, 0.001);
            Expect(report, "bridge.lamp.post.diameter", 0.06, 0.001);
            Expect(report, "bridge.truss.depth", AbyssConst.BRIDGE_TRUSS_DEPTH, 0.03);
            Expect(report, "bridge.truss.members", 75, 0.0);
            Expect(report, "bridge.truss.bays", 25, 0.0);
            Expect(report, "landing.width", AbyssConst.LANDING_W, 0.01);
            Expect(report, "landing.length", AbyssConst.LANDING_L, 0.01);
            Expect(report, "shaft.apothem", AbyssConst.SHAFT_APOTHEM, 0.01);
            Expect(report, "shaft.height", AbyssConst.SHAFT_TOP_Y - AbyssConst.SHAFT_BOTTOM_Y, 0.01);
            Expect(report, "shaft.chunks", 6 * 22, 0.0);
            Expect(report, "shaft.ribs", tier == BeatTier.Simplified ? 18 : AbyssConst.RIB_COUNT, 0.0);
            Expect(report, "shaft.belts", AbyssConst.BELT_COUNT, 0.0);
            Expect(report, "rib.height", AbyssConst.RIB_HEIGHT, 0.001);
            Expect(report, "rib.protrusion", AbyssConst.SHAFT_APOTHEM - AbyssConst.RIB_INNER_APOTHEM, 0.001);
            Expect(report, "belt.height", AbyssConst.BELT_HEIGHT, 0.001);
            Expect(report, "belt.protrusion", AbyssConst.SHAFT_APOTHEM - AbyssConst.BELT_INNER_APOTHEM, 0.001);
            if (tier != BeatTier.Simplified)
            {
                Expect(report, "c2.diameter", 2.0 * AbyssConst.COLUMNS[1].z, 0.1);
                Expect(report, "c4.diameter", 2.0 * AbyssConst.COLUMNS[3].z, 0.1);
                Expect(report, "hoist.size", AbyssConst.HOIST_SIZE, 0.01);
                Expect(report, "hoist.y0", Beat02_Bridge.HoistY(0.0), 1e-4);
            }
            else Assert.IsFalse(report.Values.ContainsKey("hoist.size"), "Simplified has no hoist");
            if (tier == BeatTier.Full)
            {
                Expect(report, "bridge.edgeline.width", 0.12, 0.002);
                Expect(report, "bridge.edgeline.offset", 0.005, 0.0005);
            }
        }

        [Test]
        public void EveryGeneratedMeshIsValid([Values(BeatTier.Full, BeatTier.Simplified)] BeatTier tier)
        {
            BeatBase b = Build(tier);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            Assert.Greater(list.Count, 50, "meshes collected");
            foreach (Mesh m in list)
            {
                MeshReport r = MeshValidation.Validate(m);
                Assert.IsTrue(r.Ok, r.Describe());
            }
        }

        [Test]
        public void KeyShotSubjectsExist([Values(BeatTier.Full, BeatTier.Simplified)] BeatTier tier)
        {
            BeatBase b = Build(tier);
            var s = new List<KeyShotSubject>();
            b.CollectSubjects("KS2a_bridge_ahead", s);
            Assert.GreaterOrEqual(s.Count, tier == BeatTier.Full ? 6 : 5, "KS2a subjects");
            int n = s.Count;
            b.CollectSubjects("KS2b_bridge_down", s);
            Assert.AreEqual(n + 2, s.Count, "KS2b subjects");
            Assert.IsTrue(s[n + 1].Name.EndsWith(CompositionLint.KnownConflictSuffix, StringComparison.Ordinal),
                "the KS2b truss is the logged spec conflict (DEVELOPMENT ledge row 'KS2b truss below')");
            foreach (KeyShotSubject k in s) Assert.Greater(k.WorldBounds.extents.magnitude, 0f, k.Name + " has empty bounds");
        }

        // ================================================================================================ motion

        [Test]
        public void HoistFollowsTheSmootherstepPingPong()
        {
            double P = AbyssConst.HOIST_PERIOD;
            double tTop = (2.0 - Beat02_Bridge.HOIST_U0) * P;   // u = 2: top turn
            Assert.That(Beat02_Bridge.HoistY(tTop), Is.EqualTo(Beat02_Bridge.HoistYTop).Within(1e-6), "top at 200");
            Assert.That(Beat02_Bridge.HoistY(tTop + P), Is.EqualTo(Beat02_Bridge.HoistYMin).Within(1e-6), "bottom at -500 one way later");
            for (double t = 0.0; t < 2.0 * P; t += 7.3)
            {
                double y = Beat02_Bridge.HoistY(t);
                Assert.That(y, Is.InRange(Beat02_Bridge.HoistYMin - 1e-6, Beat02_Bridge.HoistYTop + 1e-6));
                Assert.That(Beat02_Bridge.HoistY(t + 2.0 * P), Is.EqualTo(y).Within(1e-6), "period 2 x 583.3 s");
                Assert.That(Beat02_Bridge.HoistY(10.0 * 3600.0 + t), Is.InRange(Beat02_Bridge.HoistYMin - 1e-6, Beat02_Bridge.HoistYTop + 1e-6), "long run");
            }
            // mean speed one way: 700 m / 583.3 s = 1.2 m/s
            Assert.That(Beat02_Bridge.HoistTravel / P, Is.EqualTo(1.2).Within(0.01));
        }

        /// <summary>
        /// R2-07 (3), arbitration of 14.5.2 f's u0: over the bridge crossing (clock 40-144) the hoist rises from y 150 (the spec's
        /// "+12 to +16 degrees at the start": at least 12 degrees from the landing end) and never drops below 150, so its two lamps
        /// stay at least 11 degrees above the ledge lamps and the ledge cold spot (about y 0) seen from the bridge.
        /// </summary>
        [Test]
        public void HoistStaysHighAndRisesOverTheBridgeCrossing()
        {
            Assert.That(Beat02_Bridge.HoistY(40.0), Is.EqualTo(150.0).Within(1.0), "y 150 when the walker steps onto the bridge");
            Assert.Greater(Beat02_Bridge.HoistY(41.0), Beat02_Bridge.HoistY(40.0), "rising at the start of the crossing");
            for (double t = 40.0; t <= 144.0; t += 0.5)
            {
                double y = Beat02_Bridge.HoistY(t);
                Assert.GreaterOrEqual(y, 149.0, "y at clock " + t);
                foreach (float z in new[] { 66f, 141f, 216f })
                {
                    Vector3 p = Beat02_Bridge.HoistPos;
                    float elev = Mathf.Atan2((float)y + 1.3f, p.z - z) * Mathf.Rad2Deg;
                    Assert.GreaterOrEqual(elev, z < 100f ? 12f : 11f, "hoist elevation from z " + z + " at clock " + t);
                }
            }
        }

        [Test]
        public void FallingBoltDissolvesBetweenFallSeconds8And12()
        {
            Assert.AreEqual(0f, Beat02_Bridge.BoltVisibility(-0.1));
            Assert.AreEqual(1f, Beat02_Bridge.BoltVisibility(0.0));
            Assert.AreEqual(1f, Beat02_Bridge.BoltVisibility(7.99));
            float mid = Beat02_Bridge.BoltVisibility(10.0);
            Assert.That(mid, Is.InRange(0.01f, 0.99f));
            Assert.AreEqual(0f, Beat02_Bridge.BoltVisibility(12.0));
            Assert.That(Beat02_Bridge.BoltFallY(1.0), Is.EqualTo(-4.905).Within(1e-9), "g = 9.81");
            Assert.That(Beat02_Bridge.BoltFallY(8.0), Is.EqualTo(-313.92).Within(1e-6), "314 m at the start of the dissolve");
        }

        /// <summary>
        /// Lead decision D3 (R2-09): the bolt is released at beat-2 time 15 s (d 82.5) from a cold lamp bracketed in front of the
        /// -30 belt of the right-hand far wall (face 0, normal 30 degrees), 300 m right of the bridge axis. From the walker's eye
        /// (anywhere across the walkable width, walking on at 1.5 m/s) it lies inside the 14-22 s "pan right to the wall" (relative
        /// yaw 12 to 65) at the release and is never hidden by the 1.2 m deck before fall second 10 (the deck hides a point at
        /// lateral offset X only once it is more than about 1.83 X below the deck). It falls clear of every ring below (at least
        /// 1 m inside the belts' inner faces) through the dissolve, and its Glow sprite is at least 2.5 px.
        /// </summary>
        [Test]
        public void FallingBoltDropsFromTheRightWallBeltInOpenView()
        {
            Assert.That(Beat02_Bridge.BoltReleaseD, Is.EqualTo(82.5f).Within(1e-4f), "release at d 82.5");
            Assert.That(GazeCues.BeatTime(2, Beat02_Bridge.BoltReleaseD), Is.InRange(14f, 22f), "inside the 14-22 s pan right");
            Vector3 s0 = Beat02_Bridge.BoltStart;
            Assert.That(s0.x, Is.EqualTo(Beat02_Bridge.BOLT_LAMP_X).Within(0.5f), "300 m right of the bridge axis");
            Vector2 n = MeshKit.HexFaceNormal(Beat02_Bridge.BELT_LAMP_FACE);
            Assert.That(Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg, Is.EqualTo(30f).Within(1e-3f), "face normal 30 degrees");
            float beltY = AbyssConst.RingY(Beat02_Bridge.BELT_LAMP_K);
            Assert.IsTrue(AbyssConst.RingIsBelt(Beat02_Bridge.BELT_LAMP_K), "a belt");
            Assert.That(beltY, Is.EqualTo(-30f).Within(1e-3f), "the -30 belt");
            Assert.That(s0.y, Is.InRange(beltY - 0.5f * AbyssConst.BELT_HEIGHT - 1f, beltY + 0.5f * AbyssConst.BELT_HEIGHT), "hangs on the belt");

            // falls clear of every ring below: its apothem stays inside the belts' inner faces
            float apothem = Vector2.Dot(new Vector2(s0.x - AbyssConst.SHAFT_CENTRE.x, s0.z - AbyssConst.SHAFT_CENTRE.y), n);
            Assert.LessOrEqual(apothem, AbyssConst.BELT_INNER_APOTHEM - 1f, "the fall line clears the belts' inner faces");
            Assert.Greater(apothem, AbyssConst.BELT_INNER_APOTHEM - 3f, "close in front of the belt");

            // the gaze at the release: bearing from the walker inside the pan right (relative yaw 12-65, heading 0)
            float bearing = Mathf.Atan2(s0.x, s0.z - Beat02_Bridge.BoltReleaseD) * Mathf.Rad2Deg;
            Assert.That(bearing, Is.InRange(12f, 65f), "bearing of the release from the walker");

            // never behind the deck before fall second 10, from the walk limits and the centre line, walking on
            for (double tau = 0.0; tau <= 10.0; tau += 0.25)
            {
                Vector3 p = Beat02_Bridge.BoltPosition(tau);
                foreach (float ex in new[] { -0.35f, 0f, 0.35f })
                {
                    var eye = new Vector3(ex, AbyssConst.BRIDGE_Y + 1.7f, Beat02_Bridge.BoltReleaseD + AbyssConst.V_WALK * (float)tau);
                    float s = (eye.y - AbyssConst.BRIDGE_Y) / (eye.y - p.y);
                    float xc = eye.x + s * (p.x - eye.x), zc = eye.z + s * (p.z - eye.z);
                    bool hidden = Mathf.Abs(xc) < 0.5f * AbyssConst.BRIDGE_W && zc >= AbyssConst.BRIDGE_Z0 && zc <= AbyssConst.BRIDGE_Z1;
                    Assert.IsFalse(hidden, "the deck hides the bolt at fall second " + tau + " from eye x " + ex);
                }
            }

            BeatBase b = Build(BeatTier.Full);
            MeshRenderer bolt = null;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true)) if (r.name == "FallingBolt") bolt = r;
            Assert.IsNotNull(bolt, "falling bolt renderer");
            Assert.GreaterOrEqual(bolt.sharedMaterial.GetFloat(ShaderIds.MinPixels), 2.5f, "Glow sprite of at least 2.5 px");
            Assert.That(bolt.sharedMaterial.GetFloat(ShaderIds.EmissionStrength), Is.EqualTo(3f).Within(1e-4f), "emissive 3.0 (14.5.2)");
            Assert.IsFalse(bolt.enabled, "not drawn before the release");
        }

        /// <summary>
        /// R2-07 (1): the four wash pools behind C3 (an up and a down wash from each twin flood) sit on face 0 within 6 m of the
        /// wall, below and above the -30 belt, and from every point of the bridge each lies within 6 degrees of C3's silhouette
        /// (one pair at its right edge, one behind its left half), so C3 stands against the washed wall; the Glow quad's 100 m
        /// pull toward the camera keeps it behind C3's surface; no real lights are added on the far walls (only the hoist's own wash).
        /// </summary>
        [Test]
        public void FarWashPoolsFrameC3SeenFromTheBridge()
        {
            Vector3 c3 = AbyssConst.COLUMNS[2];
            Vector2 n = MeshKit.HexFaceNormal(Beat02_Bridge.BELT_LAMP_FACE);
            float beltY = AbyssConst.RingY(Beat02_Bridge.BELT_LAMP_K);
            for (int k = 0; k < 2 * (Beat02_Bridge.BeltLampCount - 1); k++)
            {
                int i = 1 + k / 2;
                bool up = (k & 1) == 1;
                Vector3 p = Beat02_Bridge.FarPoolCentre(i, up);
                float ap = Vector2.Dot(new Vector2(p.x - AbyssConst.SHAFT_CENTRE.x, p.z - AbyssConst.SHAFT_CENTRE.y), n);
                Assert.That(ap, Is.InRange(AbyssConst.SHAFT_APOTHEM - 6f, AbyssConst.SHAFT_APOTHEM - 1f), "pool " + i + " on the wall");
                if (up) Assert.Greater(p.y, beltY + 0.5f * AbyssConst.BELT_HEIGHT, "up pool " + i + " above the belt");
                else Assert.Less(p.y, beltY - 0.5f * AbyssConst.BELT_HEIGHT, "pool " + i + " below the belt");
                // the quad (pulled 0.5 x diameter toward the camera) stays behind C3's silhouette edge from every point of the
                // bridge, so the soft-depth fade keeps it off the column
                for (float z = AbyssConst.BRIDGE_Z0; z <= AbyssConst.BRIDGE_Z1; z += 15f)
                {
                    float quad = new Vector2(p.x, p.z - z).magnitude - 0.5f * Beat02_Bridge.FAR_POOL_D;
                    float edge = Mathf.Sqrt(new Vector2(c3.x, c3.y - z).sqrMagnitude - c3.z * c3.z);
                    Assert.Greater(quad, edge, "pool " + i + " quad behind C3's silhouette from z " + z);
                }
                for (float z = AbyssConst.BRIDGE_Z0; z <= AbyssConst.BRIDGE_Z1; z += 15f)
                {
                    float bc = Mathf.Atan2(c3.x, c3.y - z) * Mathf.Rad2Deg;
                    float half = Mathf.Asin(c3.z / new Vector2(c3.x, c3.y - z).magnitude) * Mathf.Rad2Deg;
                    float bp = Mathf.Atan2(p.x, p.z - z) * Mathf.Rad2Deg;
                    float off = Mathf.Max(0f, Mathf.Abs(bp - bc) - half);
                    Assert.LessOrEqual(off, 6f, "pool " + i + " from z " + z + ": " + off + " degrees outside C3");
                }
            }
            BeatBase b = Build(BeatTier.Full);
            int lights = 0;
            foreach (Light l in b.GetComponentsInChildren<Light>(true)) if (l.transform.position.z > 400f && l.name != "HoistWash") lights++;
            Assert.AreEqual(0, lights, "no real lights on the far walls");
        }

        /// <summary>
        /// D4 / R2-21 / R2-23: the outrigger globes and their dot sprites emit at most 2.5 (they display amber, not cream); the lamp
        /// arm is fully metallic black paint (F0 = albedo, no diffuse), so the amber spot above it cannot draw it as a warm bar.
        /// The deck's worn walking line: vertex G peaks on the centre line, falls to 0 at the edge lines and stays far below the
        /// 0.85 bevel threshold (no bent normals).
        /// </summary>
        [Test]
        public void OutriggerEmittersStayAmberArmStaysDarkAndDeckShowsAWornLine()
        {
            BeatBase b = Build(BeatTier.Full);
            MeshRenderer globes = null, arms = null, deck = null;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "LampGlobes") globes = r;
                else if (r.name == "LampArms") arms = r;
                else if (r.name == "Deck") deck = r;
            }
            Assert.IsNotNull(globes);
            Assert.IsNotNull(arms);
            Assert.IsNotNull(deck);
            Assert.LessOrEqual(globes.sharedMaterial.GetFloat(ShaderIds.EmissionStrength), 2.5f, "globe emissive (D4)");
            Assert.LessOrEqual(Beat02_Bridge.GLOBE_DOT_STRENGTH, 2.5f, "dot sprite (D4)");
            Color e = globes.sharedMaterial.GetColor(ShaderIds.EmissionColor);
            Assert.Greater(e.r, 2f * e.g, "deep amber emission");
            Assert.AreEqual(1f, arms.sharedMaterial.GetFloat(ShaderIds.Metallic), 1e-4f, "arm: fully metallic black paint");
            Assert.LessOrEqual(arms.sharedMaterial.GetColor(ShaderIds.BaseColor).linear.maxColorComponent, 0.01f, "arm albedo");

            Mesh m = deck.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = m.vertices;
            Vector3[] nn = m.normals;
            var col = new List<Color32>();
            m.GetColors(col);
            var used = new HashSet<int>(m.triangles);
            int gCentre = 0, gEdge = 0, top = 0;
            foreach (int i in used)
            {
                if (nn[i].y < 0.9999f || v[i].y < AbyssConst.BRIDGE_Y - 1e-4f) continue;
                top++;
                Assert.Less(col[i].g, 200, "top face below the bevel threshold");
                if (Mathf.Abs(v[i].x) < 0.01f) gCentre = Mathf.Max(gCentre, col[i].g);
                if (Mathf.Abs(v[i].x) > 0.5f) gEdge = Mathf.Max(gEdge, col[i].g);
            }
            Assert.GreaterOrEqual(top, 2 * (Beat02_Bridge.DECK_PATH_COLUMNS + 1), "the rebuilt top face");
            Assert.Greater(gCentre, 0, "worn centre line");
            Assert.AreEqual(0, gEdge, "no wear at the edge lines");
        }

        // ================================================================================================ R1-06 / R1-30 / R1-33

        /// <summary>
        /// Every rib and belt carries a 45 degree chamfer along its inner top edge (R1-06): on each of the six sides of the full
        /// rib and belt meshes the faces whose normal is (inward 0.707, up 0.707) cover the whole side (width c sqrt 2 along the
        /// inner edge) between y = h/2 - c and h/2. Such a face is lit by the beat-2 Key (yaw 205, pitch -72: N.L at least 0.4 on
        /// every side) and is seen from below up to 45 degrees of elevation: from the KS2a eye the far-flat chamfers of the
        /// rings above the walkway up to y 450 face the camera, while the inner faces are turned from the Key.
        /// </summary>
        [Test]
        public void RibsAndBeltsCarryAKeyLitTopChamferSeenFromBelow()
        {
            Beat02Rings.Set set = Beat02Rings.Build(false, true);
            _meshes.AddRange(set.Meshes);
            Vector3 toKey = -AbyssConst.Dir(205f, -72f);
            var eye = new Vector3(0f, -1.3f, 120f);
            foreach (bool belt in new[] { false, true })
            {
                Mesh m = belt ? set.BeltFull : set.RibFull;
                float c = Beat02Rings.TopChamfer(belt), hh = 0.5f * Beat02Rings.Height(belt), aIn = Beat02Rings.InnerApothem(belt);
                Vector3[] v = m.vertices;
                int[] idx = m.triangles;
                for (int k = 0; k < 6; k++)
                {
                    Vector2 out2 = MeshKit.HexFaceNormal(k);
                    var expect = new Vector3(-out2.x * 0.70710678f, 0.70710678f, -out2.y * 0.70710678f);
                    double area = 0.0;
                    for (int t = 0; t < idx.Length; t += 3)
                    {
                        Vector3 a = v[idx[t]], b = v[idx[t + 1]], d = v[idx[t + 2]];
                        Vector3 fn = Vector3.Cross(b - a, d - a);
                        if (fn.sqrMagnitude < 1e-12f || Vector3.Dot(fn.normalized, expect) < 0.9999f) continue;
                        area += 0.5 * fn.magnitude;
                        foreach (Vector3 p in new[] { a, b, d })
                        {
                            Assert.That(p.y, Is.InRange(hh - c - 1e-3f, hh + 1e-3f), (belt ? "belt" : "rib") + " chamfer height, side " + k);
                            float ap = Vector2.Dot(new Vector2(p.x, p.z), out2);
                            Assert.That(ap, Is.InRange(aIn - 1e-3f, aIn + c + 1e-3f), (belt ? "belt" : "rib") + " chamfer apothem, side " + k);
                        }
                    }
                    double full = c * 1.41421356 * 2.0 * aIn * Math.Tan(Math.PI / 6.0);
                    Assert.GreaterOrEqual(area, 0.98 * full, (belt ? "belt" : "rib") + ": chamfer covers side " + k);
                    Assert.GreaterOrEqual(Vector3.Dot(expect, toKey), 0.4f, (belt ? "belt" : "rib") + ": Key on the chamfer of side " + k);
                }
            }
            foreach (Beat02Rings.Level L in set.Levels)
            {
                if (L.Y < 0f || L.Y > 450f) continue;
                float aIn = Beat02Rings.InnerApothem(L.Belt), c = Beat02Rings.TopChamfer(L.Belt);
                Vector3 centre = Beat02Rings.Centre(L.Y);
                var p = new Vector3(centre.x, L.Y + 0.5f * Beat02Rings.Height(L.Belt) - 0.5f * c, centre.z + aIn + 0.5f * c);   // far flat (+Z)
                var n = new Vector3(0f, 0.70710678f, -0.70710678f);
                Assert.Greater(Vector3.Dot(n, (eye - p).normalized), 0f, "far-flat chamfer at y " + L.Y + " faces the KS2a eye");
                Assert.Less(Vector3.Dot(Vector3.back, toKey), 0f, "the far-flat inner face is turned from the Key");
            }
        }

        /// <summary>
        /// The C1 floodlight on the bridge (R1-30), all tiers: one cold spot, 14.2 range 90, a wide soft cone (outer 55-70, a
        /// penumbra of at least 40 degrees), evaluated with URP's punctual model on the face line facing the bridge (alpha 0, where
        /// the beam meets the column): the foot stays dark (E below 0.02 up to y 3: collar, y = 0 band, the ledge lamps),
        /// the face brightens monotonically to a highlight (E 1.0-2.5, no clip) at least 20 m up, keeps a quarter of it at y 40
        /// and 5 % at y 57 (60 m above the ledge pad), and the pool is broad: at the highlight height the face 0.45 rad (10 m) to
        /// either side still gets 40 % of the centre (f02's teardrop: 0).
        /// </summary>
        [Test]
        public void FloodlightGradesC1BroadlyFromADarkFoot([Values(BeatTier.Full, BeatTier.Simplified, BeatTier.Graybox)] BeatTier tier)
        {
            BeatBase b = Build(tier);
            Light flood = FindLight(b, "ColumnFlood");
            Assert.IsNotNull(flood, tier + ": no ColumnFlood light");
            Assert.AreEqual(LightType.Spot, flood.type);
            Assert.AreEqual(90f, flood.range, "14.2 range");
            Assert.That(flood.spotAngle, Is.InRange(55f, 70f), "wide cone");
            Assert.GreaterOrEqual(flood.spotAngle - flood.innerSpotAngle, 40f, "wide soft penumbra");
            Assert.AreEqual(LightShadows.None, flood.shadows);

            Vector3 c1 = AbyssConst.COLUMNS[0];
            float R = c1.z;
            const float step = 0.5f;
            float y0 = AbyssConst.BRIDGE_Y;
            var e = new float[161];
            int peak = 0;
            for (int k = 0; k < e.Length; k++)
            {
                e[k] = SpotE(flood, new Vector3(c1.x, y0 + k * step, c1.y - R), Vector3.back);
                if (e[k] > e[peak]) peak = k;
            }
            for (int k = 0; y0 + k * step <= 3f; k++) Assert.Less(e[k], 0.02f, "dark foot at y " + (y0 + k * step));
            for (int k = 1; k <= peak; k++) Assert.GreaterOrEqual(e[k], e[k - 1] - 1e-5f, "brightens upward to the highlight, y " + (y0 + k * step));
            Assert.That(e[peak], Is.InRange(1.0f, 2.5f), "highlight E");
            Assert.GreaterOrEqual(y0 + peak * step, 20f, "highlight height");
            Assert.GreaterOrEqual(e[(int)((40f - y0) / step)], 0.25f * e[peak], "E at y 40");
            Assert.GreaterOrEqual(e[(int)((57f - y0) / step)], 0.05f * e[peak], "E at y 57");
            float yp = y0 + peak * step;
            for (int s = -1; s <= 1; s += 2)
            {
                float a = s * 0.45f;
                var n = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
                float side = SpotE(flood, new Vector3(c1.x + R * n.x, yp, c1.y + R * n.z), n);
                Assert.GreaterOrEqual(side, 0.4f * e[peak], "broad pool, side " + s);
            }
        }

        /// <summary>
        /// The floodlight's cone (outer angle) clears everything near the bridge except C1: the deck edges and underside from
        /// the landing to the ledge, the outrigger lamp globes and posts, the ledge kerb and pad around the landing (alpha
        /// -0.6..0.9) and the ledge lamp heads, so no fixture or deck strip next to it is blown out (local lights cast no
        /// shadows). Its can hangs outboard below the deck: no part of it above the deck top or inboard of the edge.
        /// </summary>
        [Test]
        public void FloodlightConeClearsTheDeckLampsAndLedge()
        {
            BeatBase b = Build(BeatTier.Full);
            Light flood = FindLight(b, "ColumnFlood");
            Assert.IsNotNull(flood);
            Vector3 o = flood.transform.position, ax = flood.transform.forward;
            float cosOuter = Mathf.Cos(0.5f * flood.spotAngle * Mathf.Deg2Rad);
            var pts = new List<(string, Vector3)>();
            float hw = AbyssConst.BRIDGE_W * 0.5f;
            for (float z = AbyssConst.BRIDGE_Z0; z <= AbyssConst.BRIDGE_Z1; z += 0.25f)
                for (int s = -1; s <= 1; s += 2)
                {
                    pts.Add(("deck edge top", new Vector3(s * hw, AbyssConst.BRIDGE_Y, z)));
                    pts.Add(("deck edge bottom", new Vector3(s * hw, AbyssConst.BRIDGE_Y - 0.3f, z)));
                }
            for (int i = 0; i < AbyssConst.BRIDGE_LAMP_Z.Length; i++)
            {
                float px = hw + AbyssConst.BRIDGE_LAMP_OUTSIDE, z = AbyssConst.BRIDGE_LAMP_Z[i];
                for (float y = AbyssConst.BRIDGE_Y - 0.3f; y <= AbyssConst.BRIDGE_Y + AbyssConst.BRIDGE_LAMP_POST_H + 0.15f; y += 0.05f)
                    pts.Add(("outrigger lamp " + i, new Vector3(px, y, z)));
            }
            for (double a = -0.6; a <= 0.9; a += 0.01)
            {
                float fy = Beat03_Ledge.TopY(a);
                pts.Add(("ledge kerb", Beat03_Ledge.Ring(a, AbyssConst.LEDGE_R_OUT, fy + AbyssConst.LEDGE_KERB)));
                pts.Add(("ledge pad", Beat03_Ledge.Ring(a, 25f, fy)));
            }
            for (int i = 0; i < 2; i++)
            {
                double a = AbyssConst.LEDGE_LAMP_ALPHA[i];
                pts.Add(("ledge lamp " + i, Beat03_Ledge.Ring(a, Beat03_Ledge.LAMP_HEAD_R, Beat03_Ledge.TopY(a) + Beat03_Ledge.LAMP_HEAD_ABOVE_FLOOR + Beat03_Ledge.LAMP_GLOBE_R)));
            }
            foreach (var (name, p) in pts)
            {
                float cos = Vector3.Dot((p - o).normalized, ax);
                Assert.Less(cos, cosOuter, "the floodlight cone reaches the " + name + " at " + p.ToString("F2"));
            }
            MeshRenderer can = null;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true)) if (r.name == "Floodlight") can = r;
            Assert.IsNotNull(can, "floodlight can");
            Bounds cb = can.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.LessOrEqual(cb.max.y, AbyssConst.BRIDGE_Y - 0.1f, "the can hangs below the deck top");
            Assert.LessOrEqual(cb.max.x, -hw + 0.001f, "the can and its bracket stay outboard of the left edge");
        }

        /// <summary>
        /// The five outrigger lamps (R1-18) are downward amber spots at their globe centres. R2-23: the deck left of x 0.1
        /// (where the walker's shadow falls at KS2b) lies outside every lamp's cone, so nothing warm lights the inside of the Key
        /// shadow, while the right edge line beside each lamp (x 0.6, within 0.4 m along the deck) stays inside the cone (the
        /// warm strip beside the lamp). C1's face (y -2.5 to 60, alpha -1..1) and the near flat around the doorway (z 60,
        /// |x| 4, y -2.5 to 10), 6.1 m from the end lamps, lie outside the cone of every lamp.
        /// </summary>
        [Test]
        public void OutriggerLampsLightTheDeckNotTheColumnOrWall()
        {
            BeatBase b = Build(BeatTier.Full);
            int lamps = 0;
            Vector3 c1 = AbyssConst.COLUMNS[0];
            foreach (Light l in b.GetComponentsInChildren<Light>(true))
            {
                if (!l.name.StartsWith("OutriggerAmber", StringComparison.Ordinal)) continue;
                lamps++;
                Assert.AreEqual(LightType.Spot, l.type, l.name);
                Vector3 o = l.transform.position, ax = l.transform.forward;
                Assert.Less(ax.y, -0.99f, l.name + " aims down");
                float cosOuter = Mathf.Cos(0.5f * l.spotAngle * Mathf.Deg2Rad);
                Assert.LessOrEqual(l.spotAngle, 100f, l.name + ": narrow cone (R2-23)");
                for (float dz = -3f; dz <= 3f; dz += 0.25f)
                    for (float x = -0.6f; x <= 0.1f; x += 0.1f)
                    {
                        var p = new Vector3(x, AbyssConst.BRIDGE_Y, o.z + dz);
                        Assert.Less(Vector3.Dot((p - o).normalized, ax), cosOuter, l.name + ": deck point in the walker's shadow lit " + p.ToString("F2"));
                    }
                for (float dz = -0.4f; dz <= 0.4f; dz += 0.1f)
                {
                    var p = new Vector3(0.5f * AbyssConst.BRIDGE_W, AbyssConst.BRIDGE_Y, o.z + dz);
                    Assert.Greater(Vector3.Dot((p - o).normalized, ax), cosOuter, l.name + ": right edge beside the lamp in the cone " + p.ToString("F2"));
                }
                for (float a = -1f; a <= 1.001f; a += 0.05f)
                    for (float y = AbyssConst.BRIDGE_Y + 0.5f; y <= 60f; y += 0.5f)
                    {
                        var p = new Vector3(c1.x + c1.z * Mathf.Sin(a), y, c1.y - c1.z * Mathf.Cos(a));
                        Assert.Less(Vector3.Dot((p - o).normalized, ax), cosOuter, l.name + ": C1 face in the cone at " + p.ToString("F2"));
                    }
                for (float x = -4f; x <= 4.001f; x += 0.5f)
                    for (float y = AbyssConst.BRIDGE_Y + 0.5f; y <= 10f; y += 0.5f)
                        Assert.Less(Vector3.Dot((new Vector3(x, y, AbyssConst.SHAFT_NEAR_Z) - o).normalized, ax), cosOuter, l.name + ": near flat in the cone");
            }
            Assert.AreEqual(AbyssConst.BRIDGE_LAMP_Z.Length, lamps, "five outrigger lamps");
        }

        /// <summary>No rust on the walked deck (R1-33: the 3 m rust tile repeated as a stamped half-disc along the left edge line).</summary>
        [Test]
        public void DeckCarriesNoRust()
        {
            BeatBase b = Build(BeatTier.Full);
            MeshRenderer deck = null;
            foreach (MeshRenderer r in b.GetComponentsInChildren<MeshRenderer>(true)) if (r.name == "Deck") deck = r;
            Assert.IsNotNull(deck);
            Assert.AreEqual(0f, deck.sharedMaterial.GetFloat("_RustAmount"), "deck rust");
        }

        static Light FindLight(BeatBase b, string name)
        {
            foreach (Light l in b.GetComponentsInChildren<Light>(true)) if (l.name == name) return l;
            return null;
        }

        /// <summary>URP punctual light (RealtimeLights.hlsl): intensity / d^2, smooth range window, spot angle falloff, Lambert.</summary>
        static float SpotE(Light l, Vector3 x, Vector3 normal)
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

        // ================================================================================================ helpers

        BeatBase Build(BeatTier tier)
        {
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (lib == null || lib.materials == null || lib.materials.Length < (int)MatId.Count) Assert.Inconclusive("MaterialLibrary missing at " + LibraryPath);
            for (int i = 0; i < (int)MatId.Count; i++) if (lib.materials[i] == null) Assert.Inconclusive("MaterialLibrary lacks " + (MatId)i);
            var root = new GameObject("Beat02Test_" + tier);
            _roots.Add(root);
            var b = root.AddComponent<Beat02_Bridge>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            var ctx = new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = lib, Seed = 1u, Tier = tier, EditMode = true };
            b.Build(ctx);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        static void Expect(ScaleReport r, string key, double value, double tol)
        {
            Assert.IsTrue(r.Values.TryGetValue(key, out double v), "missing " + key);
            Assert.That(v, Is.EqualTo(value).Within(tol + 1e-9), key);
        }

        static readonly Vector3d[] s_clip = new Vector3d[4];

        /// <summary>
        /// Smallest distance in the wall plane (x, y) from the fan axis to the part of triangle abc with z &lt;= zMax (the triangle
        /// clipped against that half-space, double precision); 0 when the axis passes through it, +infinity when no part of the
        /// triangle is that close to the near flat.
        /// </summary>
        static double PlanDistanceToFanAxis(Vector3 a, Vector3 b, Vector3 c, float zMax, Vector3 fan)
        {
            Vector3d[] poly = s_clip;
            int n = 0;
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3d(i == 0 ? a : (i == 1 ? b : c));
                var q = new Vector3d(i == 0 ? b : (i == 1 ? c : a));
                bool pin = p.z <= zMax, qin = q.z <= zMax;
                if (pin) poly[n++] = p;
                if (pin != qin)
                {
                    double f = (zMax - p.z) / (q.z - p.z);
                    poly[n++] = new Vector3d(p.x + (q.x - p.x) * f, p.y + (q.y - p.y) * f, zMax);
                }
            }
            if (n == 0) return double.PositiveInfinity;
            double px = fan.x, py = fan.y, best = double.PositiveInfinity;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double x0 = poly[j].x, y0 = poly[j].y, x1 = poly[i].x, y1 = poly[i].y;
                if ((y1 > py) != (y0 > py) && px < x0 + (x1 - x0) * (py - y0) / (y1 - y0)) inside = !inside;
                double ex = x1 - x0, ey = y1 - y0, l2 = ex * ex + ey * ey;
                double t = l2 > 1e-18 ? Math.Max(0.0, Math.Min(1.0, ((px - x0) * ex + (py - y0) * ey) / l2)) : 0.0;
                double dx = x0 + ex * t - px, dy = y0 + ey * t - py;
                best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy));
            }
            return inside ? 0.0 : best;
        }

        static Bounds MinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        struct WTri
        {
            public Vector3d A, B, C, N;
            public double D;
            public int Source;
        }

        struct Vector3d
        {
            public double x, y, z;
            public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
            public Vector3d(Vector3 v) { x = v.x; y = v.y; z = v.z; }
            public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
            public static Vector3d operator *(Vector3d a, double s) => new Vector3d(a.x * s, a.y * s, a.z * s);
            public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;
            public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
            public double Length => Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Coplanar overlap check over every placed ring triangle (world space, double precision): triangles whose planes agree
        /// within 0.06 degrees and 5 mm (either facing) are projected onto the plane and tested with the separating-axis theorem;
        /// an overlap must be deeper than 1 mm on every axis to count (shared edges and corners never do).
        /// </summary>
        static int CountCoplanarOverlaps(Beat02Rings.Set set, out string first, out int triangles)
        {
            var tris = new List<WTri>(32768);
            for (int d = 0; d < set.Draws.Count; d++)
            {
                Beat02Rings.Draw dr = set.Draws[d];
                Vector3[] v = dr.Mesh.vertices;
                int[] idx = dr.Mesh.triangles;
                for (int i = 0; i < idx.Length; i += 3)
                {
                    var a = new Vector3d(dr.Matrix.MultiplyPoint3x4(v[idx[i]]));
                    var b = new Vector3d(dr.Matrix.MultiplyPoint3x4(v[idx[i + 1]]));
                    var c = new Vector3d(dr.Matrix.MultiplyPoint3x4(v[idx[i + 2]]));
                    Vector3d n = Vector3d.Cross(b - a, c - a);
                    double len = n.Length;
                    if (len < 1e-9) continue;
                    n = n * (1.0 / len);
                    if (n.x < -1e-6 || (Math.Abs(n.x) <= 1e-6 && (n.y < -1e-6 || (Math.Abs(n.y) <= 1e-6 && n.z < 0)))) n = n * -1.0;
                    tris.Add(new WTri { A = a, B = b, C = c, N = n, D = Vector3d.Dot(n, a), Source = d });
                }
            }
            triangles = tris.Count;

            var groups = new Dictionary<long, List<int>>();
            for (int i = 0; i < tris.Count; i++)
            {
                Vector3d n = tris[i].N;
                long key = ((long)Math.Round(n.x * 1000.0) + 2048) << 24 | ((long)Math.Round(n.y * 1000.0) + 2048) << 12 | ((long)Math.Round(n.z * 1000.0) + 2048);
                if (!groups.TryGetValue(key, out List<int> g)) { g = new List<int>(); groups[key] = g; }
                g.Add(i);
            }

            int overlaps = 0;
            first = "none";
            foreach (List<int> g in groups.Values)
            {
                g.Sort((p, q) => tris[p].D.CompareTo(tris[q].D));
                for (int i = 0; i < g.Count; i++)
                {
                    WTri ti = tris[g[i]];
                    for (int j = i + 1; j < g.Count; j++)
                    {
                        WTri tj = tris[g[j]];
                        if (tj.D - ti.D > 0.005) break;
                        if (!Overlap2D(ti, tj)) continue;
                        if (overlaps == 0)
                            first = set.Draws[ti.Source].Mesh.name + " (level " + set.Draws[ti.Source].K + ") / " + set.Draws[tj.Source].Mesh.name +
                                " (level " + set.Draws[tj.Source].K + ") near (" + ti.A.x.ToString("F1") + ", " + ti.A.y.ToString("F1") + ", " + ti.A.z.ToString("F1") + ")";
                        overlaps++;
                    }
                }
            }
            return overlaps;
        }

        static bool Overlap2D(in WTri p, in WTri q)
        {
            // plane basis
            Vector3d n = p.N;
            Vector3d refAxis = Math.Abs(n.y) < 0.9 ? new Vector3d(0, 1, 0) : new Vector3d(1, 0, 0);
            Vector3d u = Vector3d.Cross(refAxis, n);
            u = u * (1.0 / u.Length);
            Vector3d w = Vector3d.Cross(n, u);
            Span2 P = new Span2(p, u, w), Q = new Span2(q, u, w);
            // quick reject on the 2D bounds
            if (P.MaxX < Q.MinX + 1e-3 || Q.MaxX < P.MinX + 1e-3 || P.MaxY < Q.MinY + 1e-3 || Q.MaxY < P.MinY + 1e-3) return false;
            for (int e = 0; e < 3; e++)
            {
                if (Separated(P, Q, P.EdgeNormal(e))) return false;
                if (Separated(P, Q, Q.EdgeNormal(e))) return false;
            }
            return true;
        }

        static bool Separated(in Span2 P, in Span2 Q, (double x, double y) axis)
        {
            P.Project(axis, out double p0, out double p1);
            Q.Project(axis, out double q0, out double q1);
            return p1 < q0 + 1e-3 || q1 < p0 + 1e-3;
        }

        readonly struct Span2
        {
            readonly double ax, ay, bx, by, cx, cy;
            public readonly double MinX, MaxX, MinY, MaxY;

            public Span2(in WTri t, Vector3d u, Vector3d w)
            {
                ax = Vector3d.Dot(t.A, u); ay = Vector3d.Dot(t.A, w);
                bx = Vector3d.Dot(t.B, u); by = Vector3d.Dot(t.B, w);
                cx = Vector3d.Dot(t.C, u); cy = Vector3d.Dot(t.C, w);
                MinX = Math.Min(ax, Math.Min(bx, cx)); MaxX = Math.Max(ax, Math.Max(bx, cx));
                MinY = Math.Min(ay, Math.Min(by, cy)); MaxY = Math.Max(ay, Math.Max(by, cy));
            }

            public (double x, double y) EdgeNormal(int e)
            {
                double x0, y0, x1, y1;
                if (e == 0) { x0 = ax; y0 = ay; x1 = bx; y1 = by; }
                else if (e == 1) { x0 = bx; y0 = by; x1 = cx; y1 = cy; }
                else { x0 = cx; y0 = cy; x1 = ax; y1 = ay; }
                double dx = x1 - x0, dy = y1 - y0, l = Math.Sqrt(dx * dx + dy * dy);
                return l > 1e-12 ? (dy / l, -dx / l) : (1.0, 0.0);
            }

            public void Project((double x, double y) a, out double lo, out double hi)
            {
                double p = ax * a.x + ay * a.y, q = bx * a.x + by * a.y, r = cx * a.x + cy * a.y;
                lo = Math.Min(p, Math.Min(q, r));
                hi = Math.Max(p, Math.Max(q, r));
            }
        }
    }
}
