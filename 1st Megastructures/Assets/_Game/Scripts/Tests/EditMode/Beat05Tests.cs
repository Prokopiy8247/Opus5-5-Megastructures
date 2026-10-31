using System.Collections.Generic;
using Megastructure.Beats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// Beat 5 acceptance checks (14.5.5, 14.7.1, 14.7.3, 14.10): the city counts are the generator's own (CITY_MODULES =
    /// 3 x 17 x 24 minus the generator's fan-zone and mouth exclusions, CITY_WINDOWS = 48 x modules; the fan zone contains the
    /// spec's 153 centre-rule modules and keeps every module clear of beat 6's collar), the outer overhang is at most 2 m,
    /// the ride takes 120 s for 480 m, the platform keeps at least 3.0 m from the hall hull for every s (sampled) and clears the
    /// dock apron, the full tier reports the 14.12 dimensions, its meshes validate, the module glass lands exactly on the
    /// shader-tier window rectangles, the blocks and freight cars obey their layout rules, and the layout is deterministic.
    /// Round 1 (R1-01, R1-31, R1-39): the bridge's look straight down sees no block and no face-270 module above the y -330
    /// rib, the bridge-view fog model matches 14.5.2's nadir fog, the module buckets keep the 6 % lit mean in both tiers, the
    /// bridge-void weight dims the city until the hall, and the KS5a subjects pass the lint in every aspect.
    /// Round 2 (R2-11, R2-12, R2-14, R2-26, R2-27, lead decision D4): the module looks form contiguous districts and seams with
    /// exactly 6 % lit per face, face 210's facades drop the Key to 0.35, amber windows and beacons peak at 2.5 with the AMBER
    /// hue, the guide-rail marker lamps light both rails and clear the riding platform, the tread plates are butted and
    /// tone-varied under a subtle keep-clear line, and the block cantilevers stay inside the bridge-view sample envelope.
    /// </summary>
    public sealed class Beat05Tests
    {
        const string LibraryPath = "Assets/_Game/Settings/MaterialLibrary.asset";

        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Mesh> _meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _roots.Count; i++) if (_roots[i] != null) Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) Object.DestroyImmediate(_meshes[i]);
            _meshes.Clear();
        }

        Beat05_Lift Build(BeatTier tier)
        {
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (lib == null || lib.materials == null || lib.materials.Length < (int)MatId.Count) Assert.Inconclusive("MaterialLibrary missing or incomplete at " + LibraryPath);
            for (int i = 0; i < (int)MatId.Count; i++) if (lib.materials[i] == null) Assert.Inconclusive("MaterialLibrary lacks " + (MatId)i);
            var root = new GameObject("Beat05Test");
            _roots.Add(root);
            var b = root.AddComponent<Beat05_Lift>();
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);
            b.Build(new BeatContext { Root = root.transform, Colliders = colliders.transform, Mats = lib, Seed = 1u, Tier = tier, EditMode = true });
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            _meshes.AddRange(list);
            return b;
        }

        static double Report(BeatBase b, string key)
        {
            var r = new ScaleReport();
            b.ReportScale(r);
            Assert.IsTrue(r.Values.TryGetValue(key, out double v), "beat 5 does not report " + key);
            return v;
        }

        // ---------------------------------------------------------------- city counts (14.10 numeric checks)

        [Test]
        public void CityCountsAreTheGeneratorOutput()
        {
            int all = Beat05Layout.CityFaces.Length * AbyssConst.CITY_COLUMNS * AbyssConst.CITY_ROWS;
            Assert.AreEqual(3 * 17 * 24, all);
            Assert.AreEqual(153, Beat05Layout.SpecCentreRuleFanModules(), "the spec's centre rule (14.5.5: 153 modules)");
            Assert.GreaterOrEqual(Beat05Layout.CITY_FAN_ZONE_MODULES, 153, "the generator's fan zone contains the spec's");
            Assert.Greater(Beat05Layout.CITY_BRIDGE_ZONE_MODULES, 0, "bridge void zone (R1-01)");
            Assert.AreEqual(all - Beat05Layout.CITY_FAN_ZONE_MODULES - Beat05Layout.CITY_MOUTH_MODULES - Beat05Layout.CITY_BRIDGE_ZONE_MODULES,
                Beat05_Lift.CITY_MODULES);

            // the face-270 city hangs from the underside of the y -330 rib: an exact module row boundary, rows 0-12 bare
            Assert.AreEqual(-333f, Beat05Layout.CITY_TOP_270, 1e-4f);
            Assert.IsFalse(AbyssConst.RingIsBelt(Beat05Layout.BRIDGE_ZONE_RING), "a rib, 6 m");
            Assert.AreEqual(Beat05Layout.CITY_TOP_270, Beat05Layout.ModuleTopY(13), 1e-4f, "on a module row boundary");
            for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                {
                    bool above = Beat05Layout.ModuleTopY(r) > Beat05Layout.CITY_TOP_270 + 0.01f;
                    if (above) Assert.IsFalse(Beat05Layout.ModuleIncluded(Beat05Layout.FACE_270, c, r), "face-270 module " + c + "/" + r + " under the bridge");
                    Assert.IsFalse(Beat05Layout.InBridgeZone(Beat05Layout.FACE_210, c, r) || Beat05Layout.InBridgeZone(Beat05Layout.CityFaces[1], c, r));
                }

            // no generated module reaches beat 6's collar (R 146-156) or the bore, nor the mouth / gantry window
            for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                {
                    if (!Beat05Layout.ModuleIncluded(Beat05Layout.FACE_270, c, r)) continue;
                    Assert.GreaterOrEqual(Beat05Layout.FanZoneDistance(c, r), AbyssConst.CITY_FAN_EXCLUSION_R, "module " + c + "/" + r + " over the fan collar");
                    float x0 = Beat05Layout.ModuleU(c), y1 = Beat05Layout.ModuleTopY(r);
                    bool mouth = x0 + AbyssConst.CITY_MODULE_W > AbyssConst.W[15].x - Beat05Layout.MOUTH_HALF_X && x0 < AbyssConst.W[15].x + Beat05Layout.MOUTH_HALF_X
                        && y1 > Beat05Layout.MOUTH_Y0 && y1 - AbyssConst.CITY_MODULE_H < Beat05Layout.MOUTH_Y1;
                    Assert.IsFalse(mouth, "module " + c + "/" + r + " over the tunnel mouth / gantry root");
                }
            Assert.AreEqual(48, Beat05Layout.WindowsPerModule);
            Assert.AreEqual(48 * Beat05_Lift.CITY_MODULES, Beat05_Lift.CITY_WINDOWS);

            // recount independently from the module predicate
            int n = 0;
            foreach (int face in Beat05Layout.CityFaces)
                for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                    for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                        if (Beat05Layout.ModuleIncluded(face, c, r)) n++;
            Assert.AreEqual(Beat05_Lift.CITY_MODULES, n);
        }

        [Test]
        public void FullTierBuildsExactlyTheExportedModules()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            Assert.AreEqual(Beat05_Lift.CITY_MODULES, Report(b, "city.modules"), 1e-9);
            Assert.AreEqual(Beat05_Lift.CITY_WINDOWS, Report(b, "city.windows"), 1e-9);
            Assert.AreEqual(48.0, Report(b, "city.module.windows"), 1e-9, "glass quads (vertex B = 255) in the module mesh");
            Assert.AreEqual(AbyssConst.CITY_MODULE_W, Report(b, "city.module.width"), 1e-3);
            Assert.AreEqual(AbyssConst.CITY_MODULE_H, Report(b, "city.module.height"), 1e-3);
        }

        [Test]
        public void OuterModulesOverhangAtMostTwoMetres()
        {
            float o = Beat05Layout.Overhang;
            Assert.LessOrEqual(o, 2f, "outer overhang");
            Assert.That(o, Is.EqualTo(1.93f).Within(0.01f));
            Assert.AreEqual(-204f, Beat05Layout.ModuleU0, 1e-4f);
        }

        // ---------------------------------------------------------------- ride

        [Test]
        public void RideIs120SecondsFor480Metres()
        {
            Assert.AreEqual(480.0, AbyssConst.LiftS(120.0) - AbyssConst.LiftS(0.0), 0.01, "travel");
            double t = 0.0;
            while (AbyssConst.LiftS(t) < 480.0 - 1e-9 && t < 200.0) t += 0.001;
            Assert.AreEqual(120.0, t, 0.05, "ride duration");
            double prev = AbyssConst.LiftS(0.0);
            for (double x = 0.05; x <= 120.0; x += 0.05)
            {
                double s = AbyssConst.LiftS(x);
                Assert.GreaterOrEqual(s, prev - 1e-9, "s(t) is monotonic");
                Assert.LessOrEqual(s - prev, AbyssConst.LIFT_CRUISE * 0.05 + 1e-6, "speed never exceeds the 4.8 m/s cruise");
                prev = s;
            }
            Assert.AreEqual(240.0, AbyssConst.LiftS(60.0), 1e-6, "KS5a at s = 240");
            Assert.AreEqual(432.0, AbyssConst.LiftS(100.0), 1e-6, "KS5c at s = 432");
        }

        // ---------------------------------------------------------------- clearance

        [Test]
        public void PlatformKeepsThreeMetresFromTheHullForEveryS()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            var pts = new List<Vector3>();
            b.CollectRidingPlatformPoints(pts);
            Assert.Greater(pts.Count, 1000, "platform vertices");

            // the hull spans z 199..339; the whole platform (deck, shoes) lies inside that z range, so the check is 2D (xy)
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (Vector3 p in pts) { zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z); }
            Assert.Greater(AbyssConst.LIFT_TOP.z + zMin, AbyssConst.HALL_Z0);
            Assert.Less(AbyssConst.LIFT_TOP.z + zMax, AbyssConst.HALL_Z1);

            List<Vector2> hullA = ConvexHull(pts);
            Vector2[] hull = Beat05Layout.HullPolygon();
            float best = float.MaxValue, bestS = 0f;
            Vector2 travel = Beat05Layout.TravelXY / AbyssConst.LIFT_DROP;
            var moved = new List<Vector2>(hullA.Count);
            for (float s = 0f; s <= AbyssConst.LIFT_DROP + 1e-3f; s += 0.25f)
            {
                moved.Clear();
                Vector2 o = new Vector2(AbyssConst.LIFT_TOP.x, AbyssConst.LIFT_TOP.y) + travel * s;
                for (int i = 0; i < hullA.Count; i++) moved.Add(hullA[i] + o);
                float d = PolygonDistance(moved, hull);
                if (d < best) { best = d; bestS = s; }
            }
            Assert.GreaterOrEqual(best, 3.0f, "platform-hull clearance (minimum at s = " + bestS + ")");

            // the per-vertex trajectory measure the generator reports agrees
            Assert.GreaterOrEqual(Report(b, "lift.hull.clearance"), 3.0);
        }

        [Test]
        public void DescendingPlatformClearsTheDockApron()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            var pts = new List<Vector3>();
            b.CollectRidingPlatformPoints(pts);
            var obstacles = new List<Beat05Layout.Obstacle>();
            Beat05Layout.DockObstacles(obstacles);
            float best = float.PositiveInfinity;
            foreach (Vector3 p in pts)
                foreach (Beat05Layout.Obstacle o in obstacles)
                    best = Mathf.Min(best, Beat05Layout.BoxDistance(p, o.Min, o.Max, o.Z0, o.Z1));
            Assert.Greater(best, 0.02f, "every platform part passes the dock nosing, slab and rails");
        }

        // ---------------------------------------------------------------- dimensions and meshes

        [Test]
        public void FullTierReportsTheSpecifiedDimensions()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            Assert.AreEqual(AbyssConst.LIFT_SIZE, Report(b, "lift.size"), 0.005 * AbyssConst.LIFT_SIZE);
            Assert.AreEqual(AbyssConst.LIFT_SIZE, Report(b, "lift.size.z"), 0.005 * AbyssConst.LIFT_SIZE);
            Assert.AreEqual(AbyssConst.LIFT_THICK, Report(b, "lift.thick"), 0.005 * AbyssConst.LIFT_THICK);
            Assert.AreEqual(0.0, Report(b, "lift.deck.top"), 1e-4);
            Assert.AreEqual(AbyssConst.RAIL_H, Report(b, "lift.rail.height"), 0.005 * AbyssConst.RAIL_H);
            Assert.AreEqual(12.0, Report(b, "lift.pylon.height"), 0.06);
            Assert.AreEqual(0.12, Report(b, "lift.cable.diameter"), 1e-4);
            Assert.AreEqual(0.4, Report(b, "lift.plate.depth"), 0.002);
            Assert.AreEqual(PathGuide.DOCK_APRON_W, Report(b, "dock.width"), 0.005 * PathGuide.DOCK_APRON_W);
            Assert.AreEqual(AbyssConst.W[10].x - AbyssConst.W[11].x, Report(b, "dock.length"), 0.05);
            Assert.AreEqual(AbyssConst.GUIDE_RAIL_SEGMENTS, Report(b, "guide.rail.segments"), 1e-9);
            Assert.GreaterOrEqual(Report(b, "guide.rail.extension"), 40.0, "guide rails reach 40 m past each dock");
            Assert.AreEqual(AbyssConst.LIFT_DROP, Report(b, "lift.drop"), 0.01);
            Assert.AreEqual(AbyssConst.LIFT_DURATION, Report(b, "lift.ride.seconds"), 0.05);
            Assert.AreEqual(Beat05Layout.BLOCK_COUNT, Report(b, "city.blocks"), 1e-9);
            Assert.AreEqual(Beat05Layout.FREIGHT_GROUPS, Report(b, "freight.groups"), 1e-9);
            Assert.AreEqual(Beat05Layout.FREIGHT_GROUPS * Beat05Layout.FREIGHT_PER_GROUP, Report(b, "freight.lights"), 1e-9);
            Assert.LessOrEqual(Report(b, "city.module.triangles"), 2400.0, "module mesh about 1.9-2.2 k triangles");
        }

        [Test]
        public void FullAndSimplifiedMeshesValidate()
        {
            foreach (BeatTier tier in new[] { BeatTier.Full, BeatTier.Simplified })
            {
                Beat05_Lift b = Build(tier);
                var list = new List<Mesh>();
                b.CollectMeshes(list);
                Assert.Greater(list.Count, 20, tier + ": meshes");
                foreach (Mesh m in list)
                {
                    MeshReport r = MeshValidation.Validate(m);
                    Assert.IsTrue(r.Ok, tier + ": " + r.Describe());
                }
            }
            Beat05_Lift s = Build(BeatTier.Simplified);
            Assert.AreEqual(400.0, Report(s, "city.modules"), 1e-9, "Simplified: 400 modules");
            Assert.AreEqual(0.0, Report(s, "city.blocks"), 1e-9, "Simplified: no blocks");
        }

        [Test]
        public void ModuleGlassLandsOnTheShaderWindowRectangles()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            Mesh module = null;
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            foreach (Mesh m in list) if (m.name == "CityModule") module = m;
            Assert.IsNotNull(module, "CityModule mesh");
            Vector3[] v = module.vertices;
            Color32[] c = module.colors32;
            int checkedVerts = 0;
            foreach (int face in Beat05Layout.CityFaces)
            {
                int col = face == Beat05Layout.FACE_270 ? 0 : 7, row = face == Beat05Layout.FACE_270 ? 1 : 11;
                Matrix4x4 mtx = Beat05Layout.ModuleMatrix(face, col, row);
                for (int i = 0; i < v.Length; i++)
                {
                    if (c[i].b != 255) continue;
                    Vector3 w = mtx.MultiplyPoint3x4(v[i]);
                    float u = Beat05Layout.FaceU(face, w), vv = w.y - AbyssConst.CITY_TOP_Y;
                    float fu = u - AbyssConst.CITY_CELL_W * Mathf.Floor(u / AbyssConst.CITY_CELL_W + 1e-4f);
                    float fv = vv - AbyssConst.CITY_CELL_H * Mathf.Floor(vv / AbyssConst.CITY_CELL_H + 1e-4f);
                    Assert.IsTrue(Near(fu, 0.9f) || Near(fu, 2.1f), "glass u " + fu + " on face " + face);
                    Assert.IsTrue(Near(fv, 0.95f) || Near(fv, 2.55f), "glass v " + fv + " on face " + face);
                    float depth = Beat05Layout.FaceDepth(face, w);
                    Assert.Greater(depth, Beat05Layout.FACADE_INSET + 0.05f, "glass in front of the shader-tier quad");
                    checkedVerts++;
                }
            }
            Assert.AreEqual(3 * 48 * 4, checkedVerts);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 2e-3f;

        // ---------------------------------------------------------------- blocks and freight

        [Test]
        public void BlocksObeyTheLayoutRules()
        {
            var freight = new List<Beat05Layout.Freight>();
            Beat05Layout.PlaceFreight(1u, freight);
            var blocks = new List<Beat05Layout.Block>();
            Beat05Layout.PlaceBlocks(1u, blocks, freight);
            Assert.AreEqual(Beat05Layout.BLOCK_COUNT, blocks.Count, "200 blocks");
            for (int i = 0; i < blocks.Count; i++)
            {
                Beat05Layout.Block a = blocks[i];
                float far = a.StandoffM + Beat05Layout.BLOCK_D;
                Assert.LessOrEqual(far, 90f, "block within 90 m of the wall");
                Assert.LessOrEqual(Mathf.Abs(a.U) + 0.5f * Beat05Layout.BLOCK_W, Beat05Layout.FaceHalfLength, "block on its face");
                Assert.GreaterOrEqual(a.Y0, AbyssConst.CITY_BOTTOM_Y - 4f);
                Assert.LessOrEqual(a.Y0 + Beat05Layout.BLOCK_H, AbyssConst.CITY_TOP_Y);
                if (a.Face == Beat05Layout.FACE_270)
                {
                    float cx = Mathf.Clamp(AbyssConst.FAN_CENTRE.x, a.U - 6f, a.U + 6f), cy = Mathf.Clamp(AbyssConst.FAN_CENTRE.y, a.Y0, a.Y0 + 40f);
                    Assert.GreaterOrEqual(new Vector2(cx - AbyssConst.FAN_CENTRE.x, cy - AbyssConst.FAN_CENTRE.y).magnitude, 170f, "fan zone clear");
                }
                // ring levels: a block never overlaps a rib or belt band
                for (int k = 0; k < AbyssConst.RIB_LEVELS; k++)
                {
                    float y = AbyssConst.RingY(k), h = 0.5f * (AbyssConst.RingIsBelt(k) ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT);
                    Assert.IsFalse(a.Y0 < y + h && a.Y0 + Beat05Layout.BLOCK_H > y - h, "block crosses ring level " + k);
                }
                Bounds wa = Beat05Layout.BlockWorldBounds(a);
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    Beat05Layout.Block o = blocks[j];
                    if (o.Face == a.Face)
                        Assert.IsFalse(Mathf.Abs(o.U - a.U) < Beat05Layout.BLOCK_W && a.Y0 < o.Y0 + Beat05Layout.BLOCK_H && o.Y0 < a.Y0 + Beat05Layout.BLOCK_H, "blocks overlap");
                    else
                        Assert.IsFalse(wa.Intersects(Beat05Layout.BlockWorldBounds(o)), "blocks of two faces collide");
                }
            }
            // deterministic
            var again = new List<Beat05Layout.Block>();
            Beat05Layout.PlaceBlocks(1u, again, freight);
            Assert.AreEqual(blocks.Count, again.Count);
            for (int i = 0; i < blocks.Count; i++)
            {
                Assert.AreEqual(blocks[i].U, again[i].U);
                Assert.AreEqual(blocks[i].Y0, again[i].Y0);
                Assert.AreEqual(blocks[i].Standoff, again[i].Standoff);
                Assert.AreEqual(blocks[i].Variant, again[i].Variant);
                Assert.AreEqual(blocks[i].Tone, again[i].Tone);
            }
        }

        // ---------------------------------------------------------------- bridge void (R1-01) and city variety (R1-31)

        [Test]
        public void BridgeViewFogMatchesTheSpecifiedNadirFog()
        {
            // 14.5.2: looking straight down the fog is 21 % at 120 m, 76 % at 300 m, 99.7 % at 600 m
            float y = Beat05Layout.BridgeEyeY;
            Assert.AreEqual(-1.3f, y, 1e-4f);
            Assert.AreEqual(1f - 0.21f, Beat05Layout.BridgeViewTransmittance(new Vector3(0f, y - 120f, 141f)), 0.01f);
            Assert.AreEqual(1f - 0.76f, Beat05Layout.BridgeViewTransmittance(new Vector3(0f, y - 300f, 141f)), 0.01f);
            Assert.AreEqual(1f - 0.997f, Beat05Layout.BridgeViewTransmittance(new Vector3(0f, y - 600f, 141f)), 0.002f);
            // outside every nadir frame (beyond the 21:9 edge, above the eye) nothing is seen
            Assert.AreEqual(0f, Beat05Layout.BridgeViewTransmittance(new Vector3(200f, y - 100f, 141f)));
            Assert.AreEqual(0f, Beat05Layout.BridgeViewTransmittance(new Vector3(0f, y + 5f, 141f)));
            // the frustum is at least as wide as the widest capture (21:9 at FOV 66) and as long as the vertical FOV
            float tanV = Mathf.Tan(33f * Mathf.Deg2Rad);
            Assert.GreaterOrEqual(Beat05Layout.BRIDGE_VIEW_TAN_X, tanV * KeyShots.W219 / KeyShots.H219);
            Assert.GreaterOrEqual(Beat05Layout.BRIDGE_VIEW_TAN_Z, tanV);
        }

        [Test]
        public void TheLookStraightDownFromTheBridgeSeesNoBlock()
        {
            var freight = new List<Beat05Layout.Freight>();
            var blocks = new List<Beat05Layout.Block>();
            foreach (uint seed in new[] { 1u, 2u, 77u })
            {
                Beat05Layout.PlaceFreight(seed, freight);
                Beat05Layout.PlaceBlocks(seed, blocks, freight);
                Assert.AreEqual(Beat05Layout.BLOCK_COUNT, blocks.Count, "seed " + seed + ": still 200 blocks");
                var variants = new HashSet<int>();
                var tones = new HashSet<int>();
                int[] perFace = new int[3];
                foreach (Beat05Layout.Block b in blocks)
                {
                    Assert.LessOrEqual(Beat05Layout.BlockBridgeTransmittance(b), Beat05Layout.BRIDGE_VIEW_T_MAX, "seed " + seed + ": block in the bridge void");
                    if (b.Face == Beat05Layout.FACE_270)
                        Assert.LessOrEqual(b.Y0 + Beat05Layout.BLOCK_H, Beat05Layout.CITY_TOP_270, "face-270 block on the bare near flat");
                    variants.Add(b.Variant);
                    tones.Add(b.Tone);
                    perFace[System.Array.IndexOf(Beat05Layout.CityFaces, b.Face)]++;
                }
                Assert.AreEqual(Beat05Layout.BLOCK_VARIANTS, variants.Count, "every block mesh variant is used");
                Assert.AreEqual(Beat05Layout.BlockToneScale.Length, tones.Count, "every tone is used");
                Assert.GreaterOrEqual(perFace[0], 70, "face 210 (KS5a) keeps nearly its 80 blocks (74-76 for seeds 1, 2, 77)");
            }
            // the sampling is not blind: a block right under the bridge would be rejected
            var under = new Beat05Layout.Block { Face = Beat05Layout.FACE_270, U = 1.5f, Y0 = -140f, Standoff = 5 };
            Assert.Greater(Beat05Layout.BlockBridgeTransmittance(under), 0.5f);
        }

        [Test]
        public void ModuleBucketsFormDistrictsAndSeamsAndKeepTheLitMean()
        {
            // R2-12: buckets come in contiguous districts (2-3 columns x 3-6 rows) plus 1-2 vertical lit seams per face, and the
            // per-face lit correction keeps exactly 14.5.5's 6 % over the generated modules
            foreach (uint seed in new[] { 1u, 2u, 77u })
            {
                int n = 0, dark = 0, busy = 0, seam = 0, sameH = 0, pairsH = 0, sameV = 0, pairsV = 0;
                var tones = new int[3];
                foreach (int face in Beat05Layout.CityFaces)
                {
                    double lit = 0.0;
                    int nf = 0;
                    for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                        for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                        {
                            int b = Beat05Layout.ModuleBucket(seed, face, c, r);
                            Assert.AreEqual(b, Beat05Layout.ModuleBucket(seed, face, c, r), "deterministic");
                            Assert.That(b, Is.InRange(0, Beat05Layout.MODULE_BUCKETS - 1));
                            n++;
                            if (b == Beat05Layout.MODULE_DARK) dark++;
                            else if (b == Beat05Layout.MODULE_BUSY) busy++;
                            else if (b == Beat05Layout.MODULE_SEAM) seam++;
                            else tones[b]++;
                            if (c + 1 < AbyssConst.CITY_COLUMNS) { pairsH++; if (Beat05Layout.ModuleBucket(seed, face, c + 1, r) == b) sameH++; }
                            if (r + 1 < AbyssConst.CITY_ROWS) { pairsV++; if (Beat05Layout.ModuleBucket(seed, face, c, r + 1) == b) sameV++; }
                            if (!Beat05Layout.ModuleIncluded(face, c, r)) continue;
                            lit += Beat05Layout.ModuleLitScale[b];
                            nf++;
                        }
                    float corr = Beat05Layout.FaceLitCorrection(seed, face);
                    Assert.That(corr, Is.InRange(0.6f, 1.3f), "seed " + seed + " face " + face + ": lit correction");
                    Assert.AreEqual(1.0, lit * corr / nf, 1e-4, "seed " + seed + " face " + face + ": the mean lit fraction is exactly 6 %");

                    // seams: one or two vertical runs of a single module column, 8-14 rows, at least 4 columns apart
                    int seams = 0, firstCol = -100;
                    for (int s = 0; s < 2; s++)
                    {
                        if (!Beat05Layout.Seam(seed, face, s, out int sc, out int r0, out int rows)) continue;
                        seams++;
                        Assert.That(rows, Is.InRange(Beat05Layout.SEAM_ROWS_MIN, Beat05Layout.SEAM_ROWS_MAX));
                        Assert.That(r0 + rows, Is.LessThanOrEqualTo(AbyssConst.CITY_ROWS));
                        for (int r = r0; r < r0 + rows; r++) Assert.AreEqual(Beat05Layout.MODULE_SEAM, Beat05Layout.ModuleBucket(seed, face, sc, r));
                        if (s == 0) firstCol = sc;
                        else Assert.GreaterOrEqual(Mathf.Abs(sc - firstCol), 4, "seams apart");
                    }
                    Assert.That(seams, Is.InRange(1, 2), "seed " + seed + " face " + face + ": seams");
                }
                Assert.That(dark / (double)n, Is.InRange(0.10, 0.36), "seed " + seed + ": dark districts");
                Assert.That(busy / (double)n, Is.InRange(0.04, 0.26), "seed " + seed + ": busy districts");
                Assert.That(seam / (double)n, Is.InRange(0.015, 0.09), "seed " + seed + ": seam modules");
                foreach (int t in tones) Assert.Greater(t, n / 10, "seed " + seed + ": each concrete tone");
                // contiguous districts: a random per-module hash gives about 0.25 equal neighbours
                Assert.Greater(sameH / (double)pairsH, 0.5, "seed " + seed + ": horizontal neighbours share a district look");
                Assert.Greater(sameV / (double)pairsV, 0.6, "seed " + seed + ": vertical neighbours share a district look");
            }
            Assert.AreEqual(0.92f, Beat05Layout.ModuleToneScale[0], 1e-6f);
            Assert.AreEqual(1.08f, Beat05Layout.ModuleToneScale[2], 1e-6f);
            Assert.AreEqual(0f, Beat05Layout.ModuleLitScale[0] - 1f, 1e-6f);
        }

        [Test]
        public void FacadeMaterialsDarkenTheNearFaceAndKeepAmberWindowsAmber()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            var f = new BeatFrame { Time = 0.0, D = AbyssConst.D_LIFT, CameraPos = RideEye(60.0) };
            b.Tick(in f);
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            float baseKey = lib[MatId.Facade].GetFloat("_KeyLightMask");
            float baseLit = lib[MatId.Facade].GetFloat("_LitFraction");
            Color amber = lib[MatId.Facade].GetColor("_WinAmber").linear;   // the base hue (14.2 AMBER)
            for (int fi = 0; fi < Beat05Layout.CityFaces.Length; fi++)
            {
                float corr = Beat05Layout.FaceLitCorrection(1u, Beat05Layout.CityFaces[fi]);
                for (int bucket = 0; bucket < Beat05Layout.MODULE_BUCKETS; bucket++)
                    foreach (bool module in new[] { false, true })
                    {
                        Material m = b.FacadeMaterial(fi, bucket, module);
                        string what = "face " + Beat05Layout.CityFaces[fi] + " bucket " + bucket + (module ? " module" : " quad");
                        // R2-12 (1): the near face 210 drops the Key and its haze skylight to 0.35, the others keep them
                        Assert.AreEqual(baseKey * (fi == 0 ? 0.35f : 1f), m.GetFloat("_KeyLightMask"), 1e-5f, what + ": key mask");
                        Assert.AreEqual(baseLit * Beat05Layout.ModuleLitScale[bucket] * corr, m.GetFloat("_LitFraction"), 1e-5f, what + ": lit fraction");
                        // R2-12 (2): on the ride the far face 150 dims its windows (distance staging), 210 and 270 keep them
                        float baseEmit = lib[module ? MatId.FacadeModule : MatId.Facade].GetFloat("_EmissionStrength");
                        Assert.AreEqual(baseEmit * Beat05_Lift.RIDE_WINDOW_SCALE[fi], m.GetFloat("_EmissionStrength"), 1e-5f, what + ": ride window scale");
                        // D4: the amber window peak is at most 2.5 on the ride, hue unchanged (linear G/R and B/R ratios of AMBER)
                        Assert.LessOrEqual(Beat05_Lift.WindowAmberPeak(m), Beat05_Lift.AMBER_WINDOW_PEAK + 1e-3f, what + ": amber peak");
                        Assert.LessOrEqual(Beat05_Lift.AMBER_WINDOW_PEAK, Beat05_Lift.AMBER_SMALL_EMISSIVE_MAX, "D4 cap");
                        Color w = m.GetColor("_WinAmber").linear;
                        Assert.AreEqual(amber.g / amber.r, w.g / w.r, 0.01f, what + ": amber hue (G/R)");
                        Assert.AreEqual(amber.b / amber.r, w.b / w.r, 0.01f, what + ": amber hue (B/R)");
                    }
            }
            Assert.AreEqual(1f, Beat05_Lift.RIDE_WINDOW_SCALE[0], 1e-6f, "face 210 (near) keeps full window emission");
            Assert.AreEqual(1f, Beat05_Lift.RIDE_WINDOW_SCALE[2], 1e-6f, "face 270 keeps full window emission");
            Assert.That(Beat05_Lift.RIDE_WINDOW_SCALE[1], Is.InRange(0.15f, 0.6f), "face 150 (far) dims");
            // both tiers of a face and bucket carry identical window and concrete parameters (14.10 b crossover)
            for (int fi = 0; fi < Beat05Layout.CityFaces.Length; fi++)
                for (int bucket = 0; bucket < Beat05Layout.MODULE_BUCKETS; bucket++)
                {
                    Material q = b.FacadeMaterial(fi, bucket, false), m = b.FacadeMaterial(fi, bucket, true);
                    foreach (string p in new[] { "_LitFraction", "_KeyLightMask", "_EmissionStrength" }) Assert.AreEqual(q.GetFloat(p), m.GetFloat(p), 1e-6f, p);
                    Assert.AreEqual(q.GetColor("_WinAmber"), m.GetColor("_WinAmber"), "_WinAmber");
                    Assert.AreEqual(q.GetColor("_BaseColor"), m.GetColor("_BaseColor"), "_BaseColor");
                }
            // the block amber beacons: Glow sprites at most 2.5 (D4)
            Material beacon = lib.Variant(MatId.Glow, "b05amber", null);
            Assert.LessOrEqual(beacon.GetFloat("_EmissionStrength"), Beat05_Lift.AMBER_SMALL_EMISSIVE_MAX + 1e-6f, "amber beacon strength");
        }

        [Test]
        public void BothCityTiersCarryTheSameModulesPerBucket()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            var expected = new int[Beat05Layout.MODULE_BUCKETS];
            foreach (int face in Beat05Layout.CityFaces)
                for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                    for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                        if (Beat05Layout.ModuleIncluded(face, c, r)) expected[Beat05Layout.ModuleBucket(1u, face, c, r)]++;
            var far = new int[Beat05Layout.MODULE_BUCKETS];
            foreach (MeshFilter mf in b.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("Facade", System.StringComparison.Ordinal)) continue;
                string[] parts = mf.name.Split('_');
                int bucket = int.Parse(parts[parts.Length - 1]);
                far[bucket] += mf.sharedMesh.triangles.Length / 6;   // two triangles per module quad
            }
            for (int k = 0; k < far.Length; k++) Assert.AreEqual(expected[k], far[k], "shader-tier quads of bucket " + k);
            Assert.AreEqual(Beat05_Lift.CITY_MODULES, Report(b, "city.modules"), 1e-9, "near-tier modules");
            Assert.AreEqual(Beat05Layout.CITY_BRIDGE_ZONE_MODULES, Report(b, "city.bridge.zone.modules"), 1e-9);
            Assert.LessOrEqual(Report(b, "city.blocks.bridge.view.max"), Beat05Layout.BRIDGE_VIEW_T_MAX);
        }

        [Test]
        public void BridgeVoidWeightDimsTheCityUntilTheHall()
        {
            Assert.AreEqual(1f, Beat05_Lift.BridgeVoidWeight(0f), 1e-6f, "gallery");
            Assert.AreEqual(1f, Beat05_Lift.BridgeVoidWeight(141f), 1e-6f, "KS2b");
            Assert.AreEqual(1f, Beat05_Lift.BridgeVoidWeight(AbyssConst.BEAT_START_D[3]), 1e-6f, "the ledge, up to the hall portal");
            Assert.AreEqual(0f, Beat05_Lift.BridgeVoidWeight(AbyssConst.BEAT_START_D[3] + 22f), 1e-6f, "22 m into the hall (hull hides the shaft)");
            Assert.AreEqual(0f, Beat05_Lift.BridgeVoidWeight(AbyssConst.D_LIFT), 1e-6f, "the ride");
            Assert.AreEqual(0f, Beat05_Lift.BridgeVoidWeight(700f), 1e-6f, "after the ride");
            float prev = 1f;
            for (float d = 290f; d < 330f; d += 0.5f)
            {
                float w = Beat05_Lift.BridgeVoidWeight(d);
                Assert.LessOrEqual(w, prev + 1e-6f, "monotonic");
                Assert.LessOrEqual(prev - w, 0.04f, "smooth (0.5 m steps)");
                prev = w;
            }

            // the built beat drives the shader global and its facade emission from d
            Beat05_Lift b = Build(BeatTier.Full);
            var f = new BeatFrame { Time = 0.0, D = 141f, CameraPos = new Vector3(0f, -1.3f, 141f) };
            b.Tick(in f);
            Assert.AreEqual(1f, Shader.GetGlobalVector("_AbDownView").x, 1e-6f);
            Assert.AreEqual(1f, b.BridgeWeight, 1e-6f);
            f.D = AbyssConst.D_LIFT;
            f.CameraPos = RideEye(60.0);
            b.Tick(in f);
            Assert.AreEqual(0f, Shader.GetGlobalVector("_AbDownView").x, 1e-6f);
            Assert.AreEqual(0f, b.BridgeWeight, 1e-6f);
        }

        [Test]
        public void KS5aSubjectsAreInFrameInEveryVariant()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            var subjects = new List<KeyShotSubject>();
            b.CollectSubjects("KS5a_lift_city", subjects);
            KeyShot ks = default;
            foreach (KeyShot k in KeyShots.All) if (k.Tag == "KS5a_lift_city") ks = k;
            int rails = 0;
            foreach (KeyShotSubject s in subjects)
            {
                if (s.Name.StartsWith("deck rail", System.StringComparison.Ordinal)) rails++;
                if (s.Name == "facade 190 m" || s.Name == "protruding blocks") continue;   // far subjects: size / extent checks
                foreach (float aspect in new[] { 16f / 9f, (float)KeyShots.W1610 / KeyShots.H1610, (float)KeyShots.W219 / KeyShots.H219 })
                {
                    CompositionLint.SubjectResult r = CompositionLint.Check(in s, ks.Eye, ks.Rotation, ks.Fov, aspect, 0.05f);
                    Assert.IsTrue(r.Pass, s.Name + " at aspect " + aspect + ": centre (" + r.Cx + ", " + r.Cy + ")");
                }
            }
            Assert.GreaterOrEqual(rails, 1, "the deck rail in front of the eye (14.10 KS5a foreground)");
        }

        [Test]
        public void FreightCarsClimbAtThreeToSixMetresPerSecondLoopingEvery480()
        {
            var freight = new List<Beat05Layout.Freight>();
            Beat05Layout.PlaceFreight(1u, freight);
            Assert.AreEqual(8, freight.Count);
            int dock = Beat05Layout.FreightDockGroup(freight);
            for (int i = 0; i < freight.Count; i++)
            {
                Beat05Layout.Freight f = freight[i];
                Assert.That(f.Speed, Is.InRange(3f, 6f));
                for (int j = i + 1; j < freight.Count; j++) Assert.GreaterOrEqual(Mathf.Abs(freight[j].U - f.U), 30f, "tracks apart");
                // every group loops 480 m, the dock group (R2-11) 644 m
                double loop = i == dock ? Beat05Layout.FreightDockLoop : Beat05Layout.FREIGHT_LOOP;
                Assert.AreEqual(loop, f.Loop, 1e-4, "group " + i + " loop");
                double period = loop / f.Speed;
                foreach (double t in new[] { 0.0, 13.7, 4000.25, 1e6 })
                {
                    double a = Beat05Layout.FreightOffset(f, t), b = Beat05Layout.FreightOffset(f, t + period);
                    Assert.That(a, Is.InRange(0.0, loop));
                    double diff = System.Math.Abs(a - b);
                    Assert.IsTrue(diff < 1e-4 || System.Math.Abs(diff - loop) < 1e-4, "loops every " + loop + " m");
                    double v = (Beat05Layout.FreightOffset(f, t + 0.01) - a) / 0.01;
                    if (v > 0) Assert.AreEqual(f.Speed, v, 1e-3, "climbs at its speed");
                }
            }
        }

        [Test]
        public void DockFreightGroupRisesPastTheWalkOnSightLine()
        {
            // R2-11: the track nearest the walk line's sight line on face 210 (u -39.3, 178 m beyond the hall exit) runs its
            // loop on to y +80 and is phased on the nominal walk: above the deck mask (y -15 at 180 m) from 2 s before the hall
            // exit, through eye level, into its top fade only after the walker reaches the deck centre (walk second 13.33)
            Assert.AreEqual(-39.3f, Beat05Layout.WalkOnU, 0.3f, "walk-on sight line on face 210");
            foreach (uint seed in new[] { 1u, 2u, 77u })
            {
                var freight = new List<Beat05Layout.Freight>();
                Beat05Layout.PlaceFreight(seed, freight);
                int dock = Beat05Layout.FreightDockGroup(freight);
                Assert.GreaterOrEqual(dock, 0);
                Beat05Layout.Freight f = freight[dock];
                Assert.Less(Mathf.Abs(f.U - Beat05Layout.WalkOnU), 45f, "seed " + seed + ": within 15 degrees of the sight line");
                for (int i = 0; i < freight.Count; i++) if (i != dock) Assert.AreEqual(Beat05Layout.FREIGHT_LOOP, freight[i].Loop, 1e-4);
                Assert.AreEqual(Beat05Layout.FREIGHT_DOCK_TOP_Y, Beat05Layout.FreightTopY(f), 1e-3f);
                double t0 = Beat05Layout.HallExitClock;
                Assert.AreEqual(AbyssConst.BEAT_START_D[4] / AbyssConst.V_WALK, t0, 1e-3, "nominal hall-exit clock");
                System.Func<double, double> y = s => AbyssConst.CITY_BOTTOM_Y + Beat05Layout.FreightOffset(f, t0 + s);
                Assert.AreEqual(Beat05Layout.FREIGHT_DOCK_Y_AT_EXIT, y(0.0), 1e-2, "seed " + seed + ": height at the hall exit");
                Assert.Greater(y(-2.0), -15.0, "above the deck mask from walk second -2 (B4_end at -1.33)");
                Assert.Less(y(13.33), Beat05Layout.FREIGHT_DOCK_TOP_Y - Beat05Layout.FREIGHT_FADE, "not yet fading at the deck centre");
                for (double s = -2.0; s <= 13.33; s += 0.5)
                {
                    double ys = y(s);
                    Assert.That(ys, Is.InRange(-15.0, Beat05Layout.FREIGHT_DOCK_TOP_Y), "climbing in the walk-on window");
                    Assert.IsFalse(ys > -36.0 && ys < -24.0, "never hidden behind the y -30 belt in the window");
                }
            }
        }

        // ---------------------------------------------------------------- city near tier on the ride (14.5.5, 14.6)

        static Vector3 RideEye(double rideSeconds)
            => LiftController.PositionAtS(AbyssConst.LiftS(rideSeconds)) + new Vector3(0f, AbyssConst.EYE, 0f);

        static void TickAt(Beat05_Lift b, Vector3 eye)
        {
            var f = new BeatFrame { Time = 0.0, CameraPos = eye };
            b.Tick(in f);
        }

        /// <summary>Exact distance from a point to the nearest generated module of the full city (module OBBs, brute force).</summary>
        static float NearestModuleDistance(Vector3 eye, Bounds local)
        {
            float best = float.MaxValue;
            foreach (int face in Beat05Layout.CityFaces)
                for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                    for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                    {
                        if (!Beat05Layout.ModuleIncluded(face, c, r)) continue;
                        Vector3 p = Beat05Layout.ModuleMatrix(face, c, r).inverse.MultiplyPoint3x4(eye);
                        best = Mathf.Min(best, Mathf.Sqrt(local.SqrDistance(p)));
                    }
            return best;
        }

        static float Smooth01(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        [Test]
        public void CityNearTierFollowsTheTrueModuleDistanceOnTheRide()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            Mesh module = null;
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            foreach (Mesh m in list) if (m.name == "CityModule") module = m;
            Assert.IsNotNull(module, "CityModule mesh");
            Assert.Greater(b.CityTileCount, 100, "city tiles");

            // start far away (as in the game: every beat ticks from the gallery on), then ride
            TickAt(b, new Vector3(0f, 900f, -400f));
            float maxEarly = 0f;
            for (double t = 0.0; t <= 120.0 + 1e-9; t += 0.5)
            {
                Vector3 eye = RideEye(t);
                TickAt(b, eye);
                float maxC = 0f, minD = float.MaxValue;
                for (int i = 0; i < b.CityTileCount; i++)
                {
                    float d = b.CityTileDistance(i), c = b.CityTileCoverage(i);
                    // no hysteresis: coverage is a pure function of the camera distance, 1 - smoothstep(130, 170, d)
                    Assert.AreEqual(1f - Smooth01(130f, 170f, d), c, 1e-4f, "tile " + i + " at ride second " + t);
                    maxC = Mathf.Max(maxC, c);
                    minD = Mathf.Min(minD, d);
                }
                if (t <= 60.0 && System.Math.Abs(t - System.Math.Round(t / 5.0) * 5.0) < 1e-9)
                {
                    // the tier boxes measure the true distance to the modules (face frame), not a world AABB of a slanted tile
                    Assert.AreEqual(NearestModuleDistance(eye, module.bounds), minD, 0.05f, "nearest module at ride second " + t);
                }
                if (t <= 1e-9) Assert.That(maxC, Is.InRange(0.45f, 0.65f), "near-tier coverage at the top dock (nearest module about 149 m)");
                if (t <= 30.0) maxEarly = Mathf.Max(maxEarly, maxC);
                if (t >= 42.0) Assert.AreEqual(0f, maxC, 1e-6f, "shader tier only from s 150 on (ride second " + t + ")");
            }
            Assert.Greater(maxEarly, 0.58f, "near tier peaks near s 25 (nearest module about 147 m)");

            // a teleport to the dock gives the same coverage as riding there (no history in the tiers)
            var walked = new float[b.CityTileCount];
            TickAt(b, RideEye(0.0));
            for (int i = 0; i < walked.Length; i++) walked[i] = b.CityTileCoverage(i);
            TickAt(b, new Vector3(0f, 900f, -400f));
            TickAt(b, RideEye(0.0));
            for (int i = 0; i < walked.Length; i++) Assert.AreEqual(walked[i], b.CityTileCoverage(i), 1e-6f, "tile " + i + " after a teleport");
        }

        // ---------------------------------------------------------------- deck keep-out (pylons inside the 18 x 18 walk square)

        static bool InsideAny(Rect[] rects, double x, double z)
        {
            foreach (Rect r in rects)
                if (x > r.xMin && x < r.xMax && z > r.yMin && z < r.yMax) return true;
            return false;
        }

        static double DepthIn(Rect r, double x, double z)
            => System.Math.Min(System.Math.Min(x - r.xMin, r.xMax - x), System.Math.Min(z - r.yMin, r.yMax - z));

        [Test]
        public void SlidingSectionsAreOpenRailsAndTheDeckLineIsProud()
        {
            // KS5c (2 m behind the gate, 55 degrees down) looks through the gate into the shaft: no infill panel; the painted
            // keep-clear line stands 5 mm proud of the treads (never co-planar) and inside the kerb
            Beat05_Lift b = Build(BeatTier.Full);
            foreach (Transform t in b.GetComponentsInChildren<Transform>(true))
                Assert.IsFalse(t.name == "GateGrating" || t.name == "BoardingGrating", "infill panel " + t.name);
            Mesh line = null, treads = null;
            foreach (MeshFilter mf in b.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name == "DeckLine") line = mf.sharedMesh;
                if (mf.name == "DeckTreads") treads = mf.sharedMesh;
            }
            Assert.IsNotNull(line, "painted keep-clear line");
            Assert.IsNotNull(treads, "tread plates");
            Assert.That(line.bounds.max.y - treads.bounds.max.y, Is.GreaterThanOrEqualTo(0.005f - 1e-4f), "line top 5 mm above the treads");
            float inner = 0.5f * AbyssConst.LIFT_SIZE - 0.35f;   // kerb inner face
            Assert.That(Mathf.Max(line.bounds.max.x, -line.bounds.min.x), Is.LessThan(inner - 0.3f));
            Assert.That(Mathf.Max(line.bounds.max.z, -line.bounds.min.z), Is.LessThan(inner - 0.3f));
        }

        [Test]
        public void PylonKeepOutCoversThePylonsAndKeepsTheCapsuleOut()
        {
            Rect[] rects = Beat05Layout.DeckKeepOut();
            Assert.AreEqual(4, rects.Length);
            double lim = LiftController.WalkHalf, open = LiftController.OPENING_HALF;
            foreach (Rect r in rects)
            {
                Assert.AreEqual(2f * (Beat05Layout.PYLON_FOOTPRINT_HALF + AbyssConst.CAPSULE_R), r.width, 1e-5f);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(r.xMin), Mathf.Abs(r.xMax)), lim, "inside the walk rectangle (x)");
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(r.yMin), Mathf.Abs(r.yMax)), lim, "inside the walk rectangle (z)");
                Assert.Greater(Mathf.Min(Mathf.Abs(r.xMin), Mathf.Abs(r.xMax)), open, "clear of the gate corridor");
                Assert.Greater(Mathf.Min(Mathf.Abs(r.yMin), Mathf.Abs(r.yMax)), open, "clear of the boarding corridor");
            }

            // the generated pylons (columns, base plates, stiffeners, bolts, collars, lugs) lie inside the footprint squares
            foreach (BeatTier tier in new[] { BeatTier.Full, BeatTier.Graybox })
            {
                Beat05_Lift b = Build(tier);
                var list = new List<Mesh>();
                b.CollectMeshes(list);
                Mesh pylons = null;
                foreach (Mesh m in list) if (m.name == "Pylons") pylons = m;
                Assert.IsNotNull(pylons, tier + ": Pylons mesh");
                foreach (Vector3 v in pylons.vertices)
                {
                    float cx = Mathf.Sign(v.x) * Beat05Layout.PYLON_OFFSET, cz = Mathf.Sign(v.z) * Beat05Layout.PYLON_OFFSET;
                    float cheb = Mathf.Max(Mathf.Abs(v.x - cx), Mathf.Abs(v.z - cz));
                    Assert.LessOrEqual(cheb, Beat05Layout.PYLON_FOOTPRINT_HALF + 1e-3f, tier + ": pylon vertex " + v + " outside the footprint");
                }
            }

            // walk the capsule centre from three starts in 36 headings, 2 cm steps, with the MoveOnLift rectangle clamp first
            var starts = new[] { new Vector2(0f, 0f), new Vector2(4f, 5f), new Vector2(-8f, -3f) };
            foreach (Vector2 s0 in starts)
                for (int k = 0; k < 36; k++)
                {
                    double a = k * 10.0 * System.Math.PI / 180.0 + 0.013, dx = 0.02 * System.Math.Cos(a), dz = 0.02 * System.Math.Sin(a);
                    double x = s0.x, z = s0.y;
                    for (int step = 0; step < 900; step++)
                    {
                        // steer toward the nearest pylon after a while, so the walker also grinds along faces and corners
                        if (step == 300) { double tx = System.Math.Sign(x) * 7.0 - x, tz = System.Math.Sign(z) * 7.0 - z, l = System.Math.Sqrt(tx * tx + tz * tz) + 1e-9; dx = 0.02 * tx / l; dz = 0.02 * tz / l; }
                        double nx = System.Math.Max(-lim, System.Math.Min(lim, x + dx)), nz = System.Math.Max(-lim, System.Math.Min(lim, z + dz));
                        double bx = nx, bz = nz;
                        Beat05Layout.KeepOut(rects, x, z, ref nx, ref nz);
                        Assert.IsFalse(InsideAny(rects, nx, nz), "capsule centre inside a pylon keep-out at (" + nx + ", " + nz + ")");
                        if (!InsideAny(rects, bx, bz)) Assert.IsTrue(nx == bx && nz == bz, "a move that ends outside is unchanged");
                        Assert.LessOrEqual(System.Math.Abs(nx - bx) + System.Math.Abs(nz - bz), 0.0401, "the push-out never exceeds the step");
                        x = nx; z = nz;
                    }
                }

            // sliding: an oblique approach to a face keeps its tangential motion
            {
                Rect r = rects[3];   // (+7, +7)
                double x = r.xMin - 0.005, z = 7.0, nx = x + 0.02, nz = z + 0.01;
                Beat05Layout.KeepOut(rects, x, z, ref nx, ref nz);
                Assert.AreEqual(r.xMin, nx, 1e-6, "stopped on the face it crossed");
                Assert.AreEqual(7.01, nz, 1e-9, "slides along the face");
            }

            // a position already inside (placed there) is never snapped out or pushed deeper, and can always walk out
            {
                Rect r = rects[0];
                var rng = new Beat05Layout.Rng(7u);
                double x = r.center.x + 0.3, z = r.center.y - 0.2;
                for (int step = 0; step < 2000; step++)
                {
                    double ang = rng.Next() * 2.0 * System.Math.PI;
                    double nx = x + 0.03 * System.Math.Cos(ang), nz = z + 0.03 * System.Math.Sin(ang);
                    double before = DepthIn(r, x, z);
                    Beat05Layout.KeepOut(rects, x, z, ref nx, ref nz);
                    if (before > 0.0) Assert.LessOrEqual(DepthIn(r, nx, nz), before + 1e-12, "never deeper");
                    x = nx; z = nz;
                }
                double ox = r.center.x + 0.3, oz = r.center.y - 0.2;
                for (int step = 0; step < 100; step++)
                {
                    double nx = ox + 0.02, nz = oz;   // walking out along +x
                    Beat05Layout.KeepOut(rects, ox, oz, ref nx, ref nz);
                    Assert.AreEqual(ox + 0.02, nx, 1e-12, "walking out is free");
                    ox = nx;
                }
                Assert.IsFalse(InsideAny(rects, ox, oz), "walked out of the keep-out");
            }
        }

        // ---------------------------------------------------------------- R2 round: markers, deck, block cantilevers

        [Test]
        public void GuideRailMarkersLightTheRailsAndClearTheRidingPlatform()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            int n = AbyssConst.GUIDE_RAIL_SEGMENTS;
            Assert.AreEqual(2 * (n - 1), b.GuideMarkerCount, "one lamp per segment of both rails (segment 0 inside the head frame)");
            Assert.AreEqual(2 * (n - 1), Report(b, "guide.rail.markers"), 1e-9);

            // a lamp near eye height on the walk-on: rail 0's nearest lamp above the dock eye stands 1-4 m above it, 12-20 m away
            Vector3 dockEye = new Vector3(-150f, AbyssConst.LIFT_TOP.y + AbyssConst.EYE, AbyssConst.LIFT_TOP.z);
            float bestUp = float.MaxValue, bestDist = 0f;
            for (int i = 1; i < n; i++)
            {
                Vector3 p = Beat05_Lift.MarkerWorld(0, i);
                float up = p.y - dockEye.y;
                if (up > 0f && up < bestUp) { bestUp = up; bestDist = Vector3.Distance(p, dockEye); }
            }
            Assert.That(bestUp, Is.InRange(1f, 4f), "nearest lamp above the dock eye");
            Assert.That(bestDist, Is.InRange(12f, 20f), "its distance");

            // the lamps sit on the platform / shaft side of their rail, 0.3-1.0 m outside the chord corner, and never inside
            // the head frame or another lamp's housing; consecutive lamps follow the lift line 12 m apart
            Vector3 d = Beat05Layout.LiftDir;
            for (int r = 0; r < 2; r++)
                for (int i = 1; i < n; i++)
                {
                    Vector3 p = Beat05_Lift.MarkerWorld(r, i);
                    Assert.Less(p.z, Beat05Layout.GuideRailZ(r) - 1.9f, "rail " + r + " lamp " + i + ": platform side (-z) of the rail");
                    Assert.Greater(p.y, Beat05Layout.GuideRailTop(r).y - Beat05Layout.GuideRailLength * -d.y - 1f, "inside the rail's length");
                    Assert.Less(p.y, Beat05Layout.GuideRailTop(r).y - 3f, "below the head frame");
                    if (i > 1) Assert.AreEqual(12f, Vector3.Distance(p, Beat05_Lift.MarkerWorld(r, i - 1)), 1e-3f, "12 m apart along the line");
                }

            // the riding platform (guide shoes, deck, rails, pylons) never passes within 0.25 m of a lamp housing centre
            var pts = new List<Vector3>(40000);
            b.CollectRidingPlatformPoints(pts);
            Assert.Greater(pts.Count, 1000);
            Vector3 top = AbyssConst.LIFT_TOP, travel = AbyssConst.LIFT_BOTTOM - AbyssConst.LIFT_TOP;
            float worst = float.MaxValue;
            for (int r = 0; r < 2; r++)
                for (int i = 1; i < n; i++)
                {
                    Vector3 m = Beat05_Lift.MarkerWorld(r, i);
                    for (int k = 0; k < pts.Count; k++)
                    {
                        Vector3 a = top + pts[k];
                        float t = Mathf.Clamp01(Vector3.Dot(m - a, travel) / travel.sqrMagnitude);
                        worst = Mathf.Min(worst, (a + travel * t - m).magnitude);
                    }
                }
            Assert.Greater(worst, 0.25f, "closest approach of the riding platform to a marker lamp");

            // dimmed with the city glows while the walk is on the bridge, full from the hall on
            var f = new BeatFrame { Time = 0.0, D = 141f, CameraPos = new Vector3(0f, -1.3f, 141f) };
            b.Tick(in f);
            Assert.LessOrEqual(b.GlowDim, 0.011f, "bridge");
            f.D = AbyssConst.D_LIFT;
            f.CameraPos = RideEye(5.0);
            b.Tick(in f);
            Assert.AreEqual(1f, b.GlowDim, 1e-6f, "ride");
        }

        [Test]
        public void DeckTreadsAreButtedToneVariedAndTheLineIsSubtle()
        {
            Beat05_Lift b = Build(BeatTier.Full);
            var treadMeshes = new List<MeshRenderer>();
            MeshRenderer line = null;
            foreach (MeshRenderer mr in b.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name == "DeckTreads" || mr.name.StartsWith("DeckTreads_", System.StringComparison.Ordinal)) treadMeshes.Add(mr);
                if (mr.name == "DeckLine") line = mr;
            }
            Assert.GreaterOrEqual(treadMeshes.Count, 3, "plates in several paint tones");
            int verts = 0;
            float lumaMin = float.MaxValue, lumaMax = 0f;
            foreach (MeshRenderer mr in treadMeshes)
            {
                Mesh m = mr.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(0f, m.bounds.max.y, 1e-4f, mr.name + ": tread tops at the deck top");
                Color c = mr.sharedMaterial.GetColor("_BaseColor").linear;
                float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                lumaMin = Mathf.Min(lumaMin, l); lumaMax = Mathf.Max(lumaMax, l);
                Assert.AreEqual(0.004f, mr.sharedMaterial.GetFloat("_BevelWidth"), 1e-6f, mr.name + ": the 4 mm bevels resolve by their own width");
                verts += m.vertexCount;
            }
            // 9 x 9 identical plates split by TreadTone
            Assert.AreEqual(0, verts % 81, "9 x 9 plates");
            int perPlate = verts / 81;
            foreach (MeshRenderer mr in treadMeshes)
            {
                int t = mr.name == "DeckTreads" ? 0 : int.Parse(mr.name.Substring("DeckTreads_".Length));
                int expected = 0;
                for (int i = 0; i < 9; i++) for (int j = 0; j < 9; j++) if (Beat05_Lift.TreadTone(i, j) == t) expected++;
                Assert.AreEqual(expected * perPlate, mr.GetComponent<MeshFilter>().sharedMesh.vertexCount, mr.name + ": its plates");
            }
            Assert.That(lumaMax / lumaMin, Is.InRange(1.15f, 1.4f), "per-plate tone spread (+/-12 %)");

            // butted: the plates tile the span between the kerbs with no gap (the joint is the bevel V)
            float span = 0f;
            foreach (MeshRenderer mr in treadMeshes)
            {
                Bounds bb = mr.GetComponent<MeshFilter>().sharedMesh.bounds;
                span = Mathf.Max(span, bb.max.x);
            }
            float kerbInner = 0.5f * AbyssConst.LIFT_SIZE - 0.35f;
            Assert.That(span, Is.InRange(kerbInner - 0.01f, kerbInner - 0.001f), "outer plates 1-10 mm from the kerb, never co-planar");

            // R2-26: the keep-clear line is grey paint at most twice the plate albedo (r1: CONC_LIGHT, 11x), matte
            Assert.IsNotNull(line, "DeckLine");
            Color lc = line.sharedMaterial.GetColor("_BaseColor").linear;
            float ll = 0.2126f * lc.r + 0.7152f * lc.g + 0.0722f * lc.b;
            Assert.LessOrEqual(ll, 2f * lumaMax, "line albedo against the brightest plate");
            Assert.GreaterOrEqual(ll, 1.2f * lumaMin, "still a line");
            Assert.GreaterOrEqual(line.sharedMaterial.GetFloat("_Roughness"), 0.8f, "matte paint");
        }

        [Test]
        public void BlockCantileversStayInsideTheBridgeSampleEnvelope()
        {
            // R2-27: the stand-off structure is one pair of deep concrete cantilevers in the block's tone; every vertex lies inside the
            // envelope that BlockBridgeTransmittance samples (+/- 6 m across, 0-46.5 m up with 0.3 m below, wall to block front)
            Beat05_Lift b = Build(BeatTier.Full);
            var list = new List<Mesh>();
            b.CollectMeshes(list);
            int frames = 0;
            foreach (Mesh m in list)
            {
                if (!m.name.StartsWith("BlockFrame", System.StringComparison.Ordinal)) continue;
                frames++;
                int g = int.Parse(m.name.Substring("BlockFrame".Length));
                float standoff = g * Beat05Layout.BLOCK_STANDOFF_STEP;
                Bounds bb = m.bounds;
                Assert.LessOrEqual(Mathf.Max(-bb.min.x, bb.max.x), 0.5f * Beat05Layout.BLOCK_W, m.name + ": across");
                Assert.GreaterOrEqual(bb.min.y, -0.3f, m.name + ": soffit");
                Assert.LessOrEqual(bb.max.y, Beat05Layout.BLOCK_H, m.name + ": top");
                Assert.GreaterOrEqual(bb.min.z, -Beat05Layout.BLOCK_EMBED - 0.31f, m.name + ": into the wall");
                Assert.LessOrEqual(bb.max.z, standoff + 1f, m.name + ": into the block back");
                Assert.Greater(bb.max.z, standoff + 0.3f, m.name + ": reaches into the body (back plane at standoff - 0.3)");
                Assert.LessOrEqual(m.triangles.Length / 3, 200, m.name + ": two chamfered beams");
            }
            Assert.AreEqual(Beat05Layout.BLOCK_STANDOFF_STEPS - 1, frames, "one mesh per stand-off step");
        }

        // ---------------------------------------------------------------- helpers

        static List<Vector2> ConvexHull(List<Vector3> pts)
        {
            var p = new List<Vector2>(pts.Count);
            foreach (Vector3 v in pts) p.Add(new Vector2(v.x, v.y));
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                for (int k = 0; k < p.Count; k++)
                {
                    Vector2 q = pass == 0 ? p[k] : p[p.Count - 1 - k];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0f) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>Distance between two convex polygons (0 when they intersect).</summary>
        static float PolygonDistance(List<Vector2> a, Vector2[] b)
        {
            var arr = a.ToArray();
            float best = float.MaxValue;
            for (int i = 0; i < arr.Length; i++)
                best = Mathf.Min(best, Beat05Layout.SegmentPolygonDistance(arr[i], arr[(i + 1) % arr.Length], b));
            // a polygon B entirely inside A would give 0 nowhere: guard with a containment test of B's first vertex
            if (Inside(arr, b[0])) return 0f;
            return best;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
