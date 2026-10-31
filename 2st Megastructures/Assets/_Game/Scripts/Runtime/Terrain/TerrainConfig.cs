using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Numbers of the terrain systems (clipmap, river ribbon, grass, flowers) that are not in <see cref="RingConstants"/>.
    /// Each cites its source in MASTER_PROMPT Part 14 or DEVELOPMENT.md. The material definitions (editor), the
    /// builders, the shaders (via the materials) and the tests read these, so the C# twins and the GPU agree.
    /// </summary>
    public static class TerrainConfig
    {
        // ---------------- clipmap (14.5.2, ADR-002) ----------------
        /// <summary>Half-extent of a level in quads (half-extent = 64 x spacing, 14.5.2).</summary>
        public const int ClipHalf = 64;
        /// <summary>Skirt depth along every level's outer edge (14.5.2 "2 m skirts").</summary>
        public const float SkirtDepth = 2f;
        /// <summary>Coarse quads [33, 95) of levels 1..8 lie inside the finer level for every camera position (the finer
        /// origin is offset by -1, 0 or +1 coarse quad, ADR-002), so they are left out of the mesh.</summary>
        public const int AlwaysCollapsedLo = 33, AlwaysCollapsedHi = 95;
        /// <summary>Visible triangles of the 9 levels: 8 x 24 576 + 32 768 (14.5.2).</summary>
        public const int ClipVisibleTriangles = 8 * 24576 + 32768;
        /// <summary>Geomorph band: alpha ramps from 54.4 s (85 % of 64 s) and is exactly 1 from 61.4 s on, so the
        /// boundary vertices (at least 63 s from the camera) are always fully morphed (14.5.2 "outer 15 %").</summary>
        public const float MorphStart = 54.4f, MorphWidth = 7.0f;
        /// <summary>Shadow-pass keep radius: terrain quads beyond it are collapsed in the ShadowCaster pass only (the
        /// cascades reach 220 m, 14.2.3; hills of at most 22 m cast at most 45 m of shadow at 26 degrees).</summary>
        public const float ShadowKeep = 420f;
        /// <summary>Path ceiling of the coarse levels (RingTerrainShared.hlsl RING_CEIL_*; keeps coarse triangles out of the
        /// 3.0 m strip at +0.012 m, 14.5.2): every vertex closer than Band0 + BandPerE x e to a strip segment is held at or
        /// below 0, beyond it the ceiling rises Slope : 1. Its weight ramps over the morphed spacing e = E0 .. E1, below which
        /// the reach of a quad (1.64 + 1.414 e) lies inside RingHeight's flat zone (PathDist &lt; 6 m), so it is continuous
        /// in e (no pop) and a trimmed vertex moves by at most Slope x BandPerE / MorphWidth = 0.48 m per metre of camera
        /// travel (4.8 mm per cm).</summary>
        public const float PathCeilSlope = 2.25f, PathCeilE0 = 2f, PathCeilE1 = 3f, PathCeilBand0 = 1.7f, PathCeilBandPerE = 1.5f;
        /// <summary>RingWorld.PathSegments that carry a strip mesh over the terrain: the walk legs 0..8, the cross path 11 and
        /// the two spurs 12 and 13 (9 and 10 run beyond the wall face, where the terrain is clipped whenever a path is there).</summary>
        public static readonly int[] StripSegments = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13 };
        /// <summary>Far-water ramp: the analytic river replaces the mesh carve between these level spacings (m).</summary>
        public const float FarWaterSpacing0 = 4f, FarWaterSpacing1 = 12f;
        /// <summary>Camera region of the walk (flat x, z) used for the pre-bend bounds of camera-following content.</summary>
        public static readonly Vector2 CamMin = new Vector2(-60f, -60f), CamMax = new Vector2(360f, 760f);
        /// <summary>Largest distance of the terrain from the camera: the level-8 edge (8 192 m) plus its snap (128 m).</summary>
        public const float ClipBendDistance = 8400f;

        // ---------------- river ribbon (14.5.2, ADR-006) ----------------
        /// <summary>The ribbon is 40 m wide so its edges are always under the 1:3 bank (waterline at 18 m, ADR-006).</summary>
        public const float RibbonWidth = 40f;
        public const int RibbonStrips = 8;
        /// <summary>Along-river step: 4 m at x = 0 growing by 0.4 % of |x| (10 m at 1.5 km) up to 32 m from 7 km on:
        /// about 1 110 rows x 8 strips = 18 k triangles (14.5.2 "18 k triangles").</summary>
        public const float RibbonStepMin = 4f, RibbonStepGrowth = 0.004f, RibbonStepMax = 32f;
        /// <summary>Largest |z| distance between the ribbon (z 130..900) and the walk (z 0..760): bend inflation.</summary>
        public const float RibbonBendDistance = 1000f;
        /// <summary>Index of the river node (0, 150) in <see cref="RingWorld.River"/>: arc length u = 0 there.</summary>
        public const int RiverOriginNode = 5;

        // ---------------- grass tufts (14.5.2, 14.2.6 M04) ----------------
        public const float GrassCell = 0.35f;
        public const int GrassCells = 160;
        public const int GrassCandidates = 2;
        /// <summary>51 200 = 160 x 160 cells x 2 candidates (14.5.2).</summary>
        public const int GrassInstances = GrassCells * GrassCells * GrassCandidates;
        /// <summary>Fade 22 m -> 27.75 m. The 160 x 0.35 m window has a half-extent of 28 m and the camera can be
        /// 0.175 m off its centre, so the fade must be complete by 27.8 m (the 30 m end of 14.5.2 lies outside the
        /// window; candidates beyond 28 m are collapsed as specified).</summary>
        public const float GrassFadeStart = 22f, GrassFadeEnd = 27.75f, GrassRadius = 28f;
        public const float GrassHeight = 0.26f, GrassHeightVar = 0.10f, GrassBaseWidth = 0.10f, GrassTipWidth = 0.006f;
        /// <summary>Tip sway 0.06 m x (1 + 0.8 _Wind.x) x sin(0.9 t + 0.35 x) (14.2.6 M04).</summary>
        public const float SwayTip = 0.06f, SwayGust = 0.8f, SwayOmega = 0.9f, SwayPhasePerX = 0.35f;
        /// <summary>Tips are 8 % lighter than the terrain near detail (14.5.2).</summary>
        public const float GrassTipLift = 1.08f;

        // ---------------- flowers (14.5.2) ----------------
        public const float FlowerCell = 0.6f;
        /// <summary>136 x 0.6 m = 81.6 m window: covers the 40 m fade radius for any camera offset (0.3 m).</summary>
        public const int FlowerCells = 136;
        public const float FlowerAccept = 0.86f;
        public const int FlowerInstances = FlowerCells * FlowerCells;
        public const float FlowerFadeStart = 14f, FlowerFadeEnd = 40f, FlowerRadius = 40.5f;
        public const float FlowerStem = 0.30f, FlowerHead = 0.04f, FlowerStemWidth = 0.004f;
        public const float FlowerSway = 0.025f;

        // ---------------- exclusions (14.5.2 grass bullet; C# twin of RingFieldExcluded in RingGrass.shader) ----------------
        /// <summary>No grass within 1.7 m of the path (3.0 m strip + 0.14 m kerbs) or 19 m of the river centre line.</summary>
        public const float ExclPathDist = 1.7f, ExclRiverDist = 19f;
        /// <summary>Capsule, gangway and landing pad (x +-3, z 24..30, 14.5.1) with margins.</summary>
        public const float ExclPadHalfX = 3.5f, ExclPadZ0 = -3f, ExclPadZ1 = 31f;
        /// <summary>Under the bridge deck (z 120..180, deck 5.0 m, abutments 6 m wide, 14.5.2) with margins.</summary>
        public const float ExclBridgeHalfX = 3.5f, ExclBridgeZ0 = 116f, ExclBridgeZ1 = 184f;
        /// <summary>Footbridge arch footings around the cross path at z 400 (s 396-404, 14.5.3).</summary>
        public const float ExclFootbridgeHalfX = 12f, ExclFootbridgeZ = 400f, ExclFootbridgeHalfZ = 4.5f;
        /// <summary>Levee footprint (RingConstants.Levee*) grown by this margin.</summary>
        public const float ExclLeveeMargin = 0.5f;
        /// <summary>Plain apron x 98..114 near z 640..680 (14.5.5) with margins.</summary>
        public const float ExclApronX0 = 97.5f, ExclApronX1 = 114.5f, ExclApronZ0 = 639.5f, ExclApronZ1 = 680.5f;
    }
}
