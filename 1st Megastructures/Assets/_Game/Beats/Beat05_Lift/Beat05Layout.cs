using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Pure layout of beat 5 (14.5.5, 14.7.1, 14.7.3), shared by the generator and the EditMode tests so the counts the tests
    /// compare are the generator's own: the vertical city grid on the shaft faces 210, 150 and 270 (17 x 24 modules of
    /// 24 x 21 m per face, columns from u = -204, so the outer modules overhang 1.93 m past the 202.07 m half face into the
    /// neighbouring wall solid), its exclusions (the fan zone: module rectangle within R 156 of the fan centre, i.e. clear of
    /// beat 6's collar, 180 modules; the spec's centre rule would give 153 but leave modules over the collar and the bore; the
    /// tunnel mouth / gantry root window x -45 +/- 16, y -515..-440 on face 270, 6 more; the bridge void zone: face 270 is bare
    /// concrete from the city top down to the underside of the rib at y -330, rows 0-12 outside the fan zone, R1-01), the
    /// exported CITY_MODULES / CITY_WINDOWS, the per-module tone buckets (R1-31),
    /// the 200 protruding blocks (12 x 18 x 40 m, seeded, between the ring levels, 0-88 m from the wall, four mesh variants and
    /// four tones, none inside the bridge's look straight down), the eight freight
    /// tracks on the left wall (face 210), the inclined guide-rail line and the platform-to-hull clearance maths.
    /// Face frame: n = outward face normal (angle 30 + 60 k from +X toward +Z), t = (-n.z, 0, n.x) (the facade shader's +u),
    /// u = dot(p - shaft centre, t), y = world height. Local module/block frame: x = t, y = up, z = -n (into the shaft).
    /// No Unity objects are created here; nothing runs per frame.
    /// </summary>
    public static class Beat05Layout
    {
        // ------------------------------------------------------------------------------------------ city grid

        /// <summary>Hexagon face indices of the city faces (normals 210, 150 and 270 degrees, AbyssConst.CITY_FACES_DEG).</summary>
        public static readonly int[] CityFaces = { 3, 2, 4 };

        /// <summary>Face index of the near flat (normal 270 degrees, z = 60): the fan wall.</summary>
        public const int FACE_270 = 4;

        /// <summary>Face index of the left wall during the ride (normal 210 degrees).</summary>
        public const int FACE_210 = 3;

        /// <summary>Shader-tier quads sit this far in front of the wall plane (the wall belongs to beat 2).</summary>
        public const float FACADE_INSET = 0.05f;

        /// <summary>u of the first module column: -17 x 24 / 2 = -204.</summary>
        public static readonly float ModuleU0 = -0.5f * AbyssConst.CITY_COLUMNS * AbyssConst.CITY_MODULE_W;

        public static readonly int CellsPerModuleU = Mathf.RoundToInt(AbyssConst.CITY_MODULE_W / AbyssConst.CITY_CELL_W);   // 8
        public static readonly int CellsPerModuleV = Mathf.RoundToInt(AbyssConst.CITY_MODULE_H / AbyssConst.CITY_CELL_H);   // 6

        /// <summary>Windows per module mesh: 8 x 6 = 48.</summary>
        public static readonly int WindowsPerModule = CellsPerModuleU * CellsPerModuleV;

        /// <summary>Half length of a shaft face: apothem x tan 30 = 202.07 m.</summary>
        public static readonly float FaceHalfLength = (float)(AbyssConst.SHAFT_APOTHEM * System.Math.Tan(System.Math.PI / 6.0));

        /// <summary>How far the outer module columns reach past the face corner into the neighbouring wall solid (1.93 m).</summary>
        public static float Overhang => -ModuleU0 - FaceHalfLength;

        /// <summary>Tunnel mouth / gantry root exclusion on face 270 (lead decision): x = -45 +/- 16, y -515..-440.</summary>
        public const float MOUTH_HALF_X = 16f, MOUTH_Y0 = -515f, MOUTH_Y1 = -440f;

        /// <summary>Modules on face 270 whose rectangle reaches within R 156 of the fan centre (180; see <see cref="InFanZone"/>).</summary>
        public static readonly int CITY_FAN_ZONE_MODULES;

        /// <summary>Further modules on face 270 overlapping the tunnel mouth / gantry root window.</summary>
        public static readonly int CITY_MOUTH_MODULES;

        /// <summary>Further modules on face 270 inside the bridge void zone (rows above <see cref="CITY_TOP_270"/>, not fan zone).</summary>
        public static readonly int CITY_BRIDGE_ZONE_MODULES;

        /// <summary>All excluded modules (fan zone + mouth window + bridge void zone).</summary>
        public static readonly int CITY_EXCLUDED;

        /// <summary>Generated modules: 3 x 17 x 24 minus the exclusions.</summary>
        public static readonly int CITY_MODULES;

        /// <summary>Generated windows: 48 x CITY_MODULES.</summary>
        public static readonly int CITY_WINDOWS;

        static Beat05Layout()
        {
            int fan = 0, mouth = 0, bridge = 0;
            for (int col = 0; col < AbyssConst.CITY_COLUMNS; col++)
                for (int row = 0; row < AbyssConst.CITY_ROWS; row++)
                {
                    if (InFanZone(FACE_270, col, row)) fan++;
                    else if (InMouthZone(FACE_270, col, row)) mouth++;
                    else if (InBridgeZone(FACE_270, col, row)) bridge++;
                }
            CITY_FAN_ZONE_MODULES = fan;
            CITY_MOUTH_MODULES = mouth;
            CITY_BRIDGE_ZONE_MODULES = bridge;
            CITY_EXCLUDED = fan + mouth + bridge;
            CITY_MODULES = CityFaces.Length * AbyssConst.CITY_COLUMNS * AbyssConst.CITY_ROWS - CITY_EXCLUDED;
            CITY_WINDOWS = WindowsPerModule * CITY_MODULES;
        }

        public static Vector3 Normal(int face)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            return new Vector3(n.x, 0f, n.y);
        }

        public static Vector3 Tangent(int face)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            return new Vector3(-n.y, 0f, n.x);
        }

        /// <summary>Rotation of the local frame (x = t, y = up, z = -n) of face k.</summary>
        public static Quaternion FaceRotation(int face) => Quaternion.LookRotation(-Normal(face), Vector3.up);

        /// <summary>World point on face k at (u, y), <paramref name="inward"/> metres in front of the wall plane.</summary>
        public static Vector3 WallPoint(int face, float u, float y, float inward)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            float a = AbyssConst.SHAFT_APOTHEM - inward;
            return new Vector3(c.x + n.x * a - n.y * u, y, c.y + n.y * a + n.x * u);
        }

        /// <summary>u of a world point on face k.</summary>
        public static float FaceU(int face, Vector3 p)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            return (p.x - c.x) * -n.y + (p.z - c.y) * n.x;
        }

        /// <summary>Distance of a world point in front of face k's wall plane (positive inside the shaft).</summary>
        public static float FaceDepth(int face, Vector3 p)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            return AbyssConst.SHAFT_APOTHEM - ((p.x - c.x) * n.x + (p.z - c.y) * n.y);
        }

        /// <summary>
        /// Face frame of face k: (u, y, depth in front of the wall plane). The axes (t, up, -n) are orthonormal, so the map is an
        /// isometry of world space (distances are preserved); a module's local frame is this frame shifted to the module centre.
        /// </summary>
        public static Vector3 FaceFrame(int face, Vector3 p) => new Vector3(FaceU(face, p), p.y, FaceDepth(face, p));

        public static float ModuleU(int col) => ModuleU0 + AbyssConst.CITY_MODULE_W * col;
        public static float ModuleCentreU(int col) => ModuleU0 + AbyssConst.CITY_MODULE_W * (col + 0.5f);
        public static float ModuleTopY(int row) => AbyssConst.CITY_TOP_Y - AbyssConst.CITY_MODULE_H * row;
        public static float ModuleCentreY(int row) => AbyssConst.CITY_TOP_Y - AbyssConst.CITY_MODULE_H * (row + 0.5f);

        /// <summary>
        /// Fan zone (face 270 only; on face 270 u = world x): the module RECTANGLE reaches within R 156 of the fan centre. The
        /// spec's centre-only rule (153 modules) leaves 27 modules overlapping beat 6's proud collar (R 146-156) and 13 of them
        /// hanging over the bore opening (R 150); the rectangle rule keeps every module clear of the collar (180 modules).
        /// </summary>
        public static bool InFanZone(int face, int col, int row)
        {
            if (face != FACE_270) return false;
            return FanZoneDistance(col, row) < AbyssConst.CITY_FAN_EXCLUSION_R;
        }

        /// <summary>Distance from the fan centre to the nearest point of a face-270 module rectangle.</summary>
        public static float FanZoneDistance(int col, int row)
        {
            float x0 = ModuleU(col), x1 = x0 + AbyssConst.CITY_MODULE_W;
            float y1 = ModuleTopY(row), y0 = y1 - AbyssConst.CITY_MODULE_H;
            float cx = Mathf.Clamp(AbyssConst.FAN_CENTRE.x, x0, x1), cy = Mathf.Clamp(AbyssConst.FAN_CENTRE.y, y0, y1);
            float dx = cx - AbyssConst.FAN_CENTRE.x, dy = cy - AbyssConst.FAN_CENTRE.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Modules the spec's centre-only fan rule would exclude (153), for the report.</summary>
        public static int SpecCentreRuleFanModules()
        {
            int n = 0;
            float r = AbyssConst.CITY_FAN_EXCLUSION_R;
            for (int col = 0; col < AbyssConst.CITY_COLUMNS; col++)
                for (int row = 0; row < AbyssConst.CITY_ROWS; row++)
                {
                    float dx = ModuleCentreU(col) - AbyssConst.FAN_CENTRE.x, dy = ModuleCentreY(row) - AbyssConst.FAN_CENTRE.y;
                    if (dx * dx + dy * dy < r * r) n++;
                }
            return n;
        }

        /// <summary>Module rectangle overlaps the tunnel mouth / gantry root window (face 270 only).</summary>
        public static bool InMouthZone(int face, int col, int row)
        {
            if (face != FACE_270) return false;
            float x0 = ModuleU(col), x1 = x0 + AbyssConst.CITY_MODULE_W;
            float y1 = ModuleTopY(row), y0 = y1 - AbyssConst.CITY_MODULE_H;
            float mx = AbyssConst.W[15].x;
            return x1 > mx - MOUTH_HALF_X && x0 < mx + MOUTH_HALF_X && y1 > MOUTH_Y0 && y0 < MOUTH_Y1;
        }

        public static bool ModuleIncluded(int face, int col, int row)
            => !InFanZone(face, col, row) && !InMouthZone(face, col, row) && !InBridgeZone(face, col, row);

        // ------------------------------------------------------------------------------------------ bridge void zone (R1-01)

        /// <summary>
        /// Ring level whose underside is the top of the face-270 city: the rib at y -330 (k = 25). The near flat lies right
        /// below the bridge (z 60; the deck runs z 66-216 at x 0), so in the 14.5.2 KS2b look straight down its city rows, 60-330 m
        /// under the deck at 20-75 % fog transmittance, read as a lit floor of rooftops (critique R1-01). The flat therefore stays
        /// bare board-formed concrete from the city top (-60) down to this rib and the city hangs from it: rows 0-12 (273 m) of
        /// face 270 are not generated in either tier (the rib's underside -333 is exactly the top of row 13). Below it the view
        /// straight down passes at most 16 % fog transmittance. Arbitration of 14.5.5 "rows y -60..-564" (logged by the lead).
        /// </summary>
        public const int BRIDGE_ZONE_RING = 25;

        /// <summary>Top of the face-270 city: the underside of ring <see cref="BRIDGE_ZONE_RING"/> (y -333).</summary>
        public static readonly float CITY_TOP_270 = AbyssConst.RingY(BRIDGE_ZONE_RING) - 0.5f * AbyssConst.RIB_HEIGHT;

        /// <summary>Face-270 module above <see cref="CITY_TOP_270"/> (the bare near flat under the bridge).</summary>
        public static bool InBridgeZone(int face, int col, int row) => face == FACE_270 && ModuleTopY(row) > CITY_TOP_270 + 0.01f;

        /// <summary>Nadir view frustum of the bridge: horizontal half-extent tan (21:9 at FOV 66 is 1.539) and along the deck (tan 33 = 0.649), with margin.</summary>
        public const float BRIDGE_VIEW_TAN_X = 1.6f, BRIDGE_VIEW_TAN_Z = 0.7f;

        /// <summary>A block is not placed where any part of it is seen in a nadir view from the bridge through more fog transmittance than this.</summary>
        public const float BRIDGE_VIEW_T_MAX = 0.2f;

        /// <summary>Eye height on the bridge deck (-1.3).</summary>
        public static float BridgeEyeY => AbyssConst.BRIDGE_Y + AbyssConst.EYE;

        /// <summary>
        /// Fog transmittance (beat-2 shaft fog, 14.2: F = 1 - exp(-(sigma d_eff)^2), d_eff = d (1 + m_down saturate(-v.y))) with
        /// which a world point is seen in a look straight down from the bridge (KS2b and the 48-58 s cinematic hold, all four
        /// capture aspects): eye at x 0, deck height + EYE, anywhere along the deck z 66-216, pitch -90 with the deck along the
        /// frame's vertical. 0 when no such view contains the point.
        /// </summary>
        public static float BridgeViewTransmittance(Vector3 p)
        {
            float eyeY = BridgeEyeY, depth = eyeY - p.y;
            if (depth <= 0f || Mathf.Abs(p.x) > BRIDGE_VIEW_TAN_X * depth) return 0f;
            float zc = Mathf.Clamp(p.z, AbyssConst.BRIDGE_Z0, AbyssConst.BRIDGE_Z1);
            if (Mathf.Abs(p.z - zc) > BRIDGE_VIEW_TAN_Z * depth) return 0f;
            return BridgeFogTransmittance(new Vector3(0f, eyeY, zc), p);
        }

        /// <summary>Beat-2 fog transmittance between two points (ProgressTables row "2 bridge": D50 520, m_down 1.5).</summary>
        public static float BridgeFogTransmittance(Vector3 eye, Vector3 p)
        {
            LookRow row = ProgressTables.Rows[ProgressTables.ROW_B2];
            Vector3 v = p - eye;
            float d = v.magnitude;
            if (d < 1e-4f) return 1f;
            float s = 0.83255461f / row.D50 * d * (1f + row.MDown * Mathf.Clamp01(-v.y / d));   // sigma = sqrt(ln 2) / D50
            return Mathf.Exp(-s * s);
        }

        /// <summary>Highest bridge-view transmittance over a block (body, roof plant, mast, stand-off girders; 4 x 4 x 4 samples).</summary>
        public static float BlockBridgeTransmittance(in Block b)
        {
            Matrix4x4 m = BlockFrameMatrix(b);
            float best = 0f;
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                    {
                        var local = new Vector3(Mathf.Lerp(-0.5f * BLOCK_W, 0.5f * BLOCK_W, i / 3f), Mathf.Lerp(0f, BLOCK_H + 6.5f, j / 3f),
                            Mathf.Lerp(-BLOCK_EMBED, b.StandoffM + BLOCK_D, k / 3f));
                        best = Mathf.Max(best, BridgeViewTransmittance(m.MultiplyPoint3x4(local)));
                    }
            return best;
        }

        // ------------------------------------------------------------------------------------------ module looks: districts (R1-31, R2-12)

        /// <summary>
        /// Per-module look buckets, identical in both tiers (the crossover matches): three concrete tones (albedo x 0.92 / 1.0 /
        /// 1.08 linear), DARK (x 0.80, a few lit windows: an emptied district), BUSY (2.6 x the lit fraction) and SEAM (4.5 x: a
        /// vertical lit spine). R2-12: the R1-31 per-module hash was spatially uncorrelated and read as an even 6 % sprinkle
        /// ("pegboard, starfield"); the buckets are now assigned per contiguous DISTRICT of 2-3 module columns x 3-6 rows
        /// (48-72 x 63-126 m, staggered per district column like brick courses), plus one or two vertical seams per face
        /// (a single module column over 8-14 rows). <see cref="FaceLitCorrection"/> rescales every bucket's lit fraction per
        /// face so that the mean over the face's generated modules is exactly the 14.5.5 6 %.
        /// </summary>
        public const int MODULE_BUCKETS = 6, MODULE_DARK = 3, MODULE_BUSY = 4, MODULE_SEAM = 5;
        public static readonly float[] ModuleToneScale = { 0.92f, 1f, 1.08f, 0.80f, 1f, 0.96f };
        public static readonly float[] ModuleLitScale = { 1f, 1f, 1f, 0.1f, 2.6f, 4.5f };
        /// <summary>District probabilities (by district, not by module): dark, busy; the rest split evenly over the three tones.</summary>
        public const float DISTRICT_DARK_P = 0.22f, DISTRICT_BUSY_P = 0.14f;
        /// <summary>District size in modules: 2-3 columns, 3-6 rows.</summary>
        public const int DISTRICT_COLS_MIN = 2, DISTRICT_COLS_MAX = 3, DISTRICT_ROWS_MIN = 3, DISTRICT_ROWS_MAX = 6;
        /// <summary>Seams per face (1-2), each one module column over 8-14 rows, at least 4 columns apart.</summary>
        public const int SEAM_ROWS_MIN = 8, SEAM_ROWS_MAX = 14;

        static uint FaceHash(uint seed, int face, uint salt)
        {
            unchecked { return Pcg(Pcg(seed * 2246822519u + 0x7C1Fu + (uint)face * 977u) ^ salt); }
        }

        static float U01(uint h) => (h >> 8) * (1f / 16777216f);

        /// <summary>District column (0..) of a module column on a face: widths of 2-3 columns by hash, left to right.</summary>
        public static int DistrictColumn(uint seed, int face, int col, out int firstCol)
        {
            unchecked
            {
                int c0 = 0, dc = 0;
                while (true)
                {
                    int w = DISTRICT_COLS_MIN + (int)(U01(FaceHash(seed, face, 0xD15C0000u + (uint)dc * 2654435761u)) * (DISTRICT_COLS_MAX - DISTRICT_COLS_MIN + 1));
                    w = Mathf.Clamp(w, DISTRICT_COLS_MIN, DISTRICT_COLS_MAX);
                    if (col < c0 + w) { firstCol = c0; return dc; }
                    c0 += w;
                    dc++;
                }
            }
        }

        /// <summary>District row (0..) of a module row within district column dc: heights 3-6 by hash, staggered start.</summary>
        public static int DistrictRow(uint seed, int face, int dc, int row)
        {
            unchecked
            {
                // stagger: the first district of each district column is cut short by 0..2 rows, so boundaries never align
                int r0 = -(int)(U01(FaceHash(seed, face, 0x57A6E000u + (uint)dc * 40503u)) * 3f);
                int dr = 0;
                while (true)
                {
                    int h = DISTRICT_ROWS_MIN + (int)(U01(FaceHash(seed, face, 0x0D157000u + (uint)dc * 7919u + (uint)dr * 104729u)) * (DISTRICT_ROWS_MAX - DISTRICT_ROWS_MIN + 1));
                    h = Mathf.Clamp(h, DISTRICT_ROWS_MIN, DISTRICT_ROWS_MAX);
                    if (row < r0 + h) return dr;
                    r0 += h;
                    dr++;
                }
            }
        }

        /// <summary>Seam s (0 or 1) of a face: its module column, first row and row count; false when the face has no seam s.</summary>
        public static bool Seam(uint seed, int face, int s, out int col, out int row0, out int rows)
        {
            unchecked
            {
                uint h0 = FaceHash(seed, face, 0x5EA40000u);
                int count = 1 + (U01(h0) < 0.6f ? 1 : 0);
                col = 1 + (int)(U01(FaceHash(seed, face, 0x5EA40001u)) * (AbyssConst.CITY_COLUMNS - 2));
                col = Mathf.Clamp(col, 1, AbyssConst.CITY_COLUMNS - 2);
                if (s == 1)
                {
                    // the second seam at least 4 columns from the first
                    int off = 4 + Mathf.Min(7, (int)(U01(FaceHash(seed, face, 0x5EA40002u)) * 8f));   // 4..11 of 15: circular gap >= 4
                    col = 1 + ((col - 1 + off) % (AbyssConst.CITY_COLUMNS - 2));
                }
                rows = SEAM_ROWS_MIN + (int)(U01(FaceHash(seed, face, 0x5EA40010u + (uint)s)) * (SEAM_ROWS_MAX - SEAM_ROWS_MIN + 1));
                rows = Mathf.Clamp(rows, SEAM_ROWS_MIN, SEAM_ROWS_MAX);
                // on face 270 the city starts below the bridge void (row 13): keep its seams there
                int rMin = face == FACE_270 ? 13 : 0;
                int span = Mathf.Max(1, AbyssConst.CITY_ROWS - rows - rMin + 1);
                row0 = rMin + (int)(U01(FaceHash(seed, face, 0x5EA40020u + (uint)s)) * span);
                row0 = Mathf.Clamp(row0, 0, Mathf.Max(0, AbyssConst.CITY_ROWS - rows));
                return s < count;
            }
        }

        public static bool InSeam(uint seed, int face, int col, int row)
        {
            for (int s = 0; s < 2; s++)
                if (Seam(seed, face, s, out int c, out int r0, out int n) && col == c && row >= r0 && row < r0 + n) return true;
            return false;
        }

        public static int ModuleBucket(uint seed, int face, int col, int row)
        {
            unchecked
            {
                if (InSeam(seed, face, col, row)) return MODULE_SEAM;
                int dc = DistrictColumn(seed, face, col, out _);
                int dr = DistrictRow(seed, face, dc, row);
                float x = U01(FaceHash(seed, face, 0xB0C40000u + (uint)dc * 2246822519u + (uint)dr * 3266489917u));
                if (x < DISTRICT_DARK_P) return MODULE_DARK;
                if (x < DISTRICT_DARK_P + DISTRICT_BUSY_P) return MODULE_BUSY;
                float t = (x - DISTRICT_DARK_P - DISTRICT_BUSY_P) / (1f - DISTRICT_DARK_P - DISTRICT_BUSY_P);
                return Mathf.Min(2, (int)(t * 3f));
            }
        }

        /// <summary>
        /// Lit-fraction correction of a face: 1 / (mean of <see cref="ModuleLitScale"/> over the face's generated modules), so
        /// the district layout keeps exactly 14.5.5's 6 % lit windows per face (each bucket's lit fraction = 6 % x scale x this).
        /// </summary>
        public static float FaceLitCorrection(uint seed, int face)
        {
            double sum = 0.0;
            int n = 0;
            for (int c = 0; c < AbyssConst.CITY_COLUMNS; c++)
                for (int r = 0; r < AbyssConst.CITY_ROWS; r++)
                {
                    if (!ModuleIncluded(face, c, r)) continue;
                    sum += ModuleLitScale[ModuleBucket(seed, face, c, r)];
                    n++;
                }
            return n == 0 || sum <= 0.0 ? 1f : (float)(n / sum);
        }

        /// <summary>Object-to-world matrix of a module instance (origin = module centre on the wall plane).</summary>
        public static Matrix4x4 ModuleMatrix(int face, int col, int row)
            => Matrix4x4.TRS(WallPoint(face, ModuleCentreU(col), ModuleCentreY(row), 0f), FaceRotation(face), Vector3.one);

        // ------------------------------------------------------------------------------------------ walk path distance (Simplified tier)

        /// <summary>Distance of a world point to the walked line of the beat: the lift line (top to bottom dock) and the gantry.</summary>
        public static float PathDistance(Vector3 p)
        {
            Vector3 g0 = new Vector3(AbyssConst.W[14].x, AbyssConst.GANTRY_Y, AbyssConst.GANTRY_Z0);
            Vector3 g1 = new Vector3(AbyssConst.W[14].x, AbyssConst.GANTRY_Y, AbyssConst.GANTRY_Z1);
            return Mathf.Min(PointSegment3(p, AbyssConst.LIFT_TOP, AbyssConst.LIFT_BOTTOM), PointSegment3(p, g0, g1));
        }

        static float PointSegment3(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
            return (a + ab * t - p).magnitude;
        }

        // ------------------------------------------------------------------------------------------ deterministic random

        /// <summary>PCG hash (Jarzynski and Olano), integer only (CONTRACTS 2: no sin hashes).</summary>
        public static uint Pcg(uint v)
        {
            unchecked
            {
                uint s = v * 747796405u + 2891336453u;
                uint w = ((s >> (int)((s >> 28) + 4u)) ^ s) * 277803737u;
                return (w >> 22) ^ w;
            }
        }

        /// <summary>Tiny PCG stream (build time only).</summary>
        public struct Rng
        {
            uint _s;
            public Rng(uint seed) { _s = Pcg(seed ^ 0x5BD1E995u); }
            public float Next() { _s = Pcg(_s); return (_s >> 8) * (1f / 16777216f); }
            public int Range(int lo, int hiExclusive) => hiExclusive <= lo ? lo : lo + Mathf.Min((int)(Next() * (hiExclusive - lo)), hiExclusive - lo - 1);
        }

        // ------------------------------------------------------------------------------------------ freight lights (left wall)

        public const int FREIGHT_GROUPS = 8, FREIGHT_PER_GROUP = 3;
        public const float FREIGHT_LOOP = 480f, FREIGHT_V_MIN = 3f, FREIGHT_V_MAX = 6f;
        /// <summary>Lights in a group sit one cell apart along the wall.</summary>
        public const float FREIGHT_SPACING = 3f;
        /// <summary>Glow centre in front of the wall plane (clear of the module reveals, behind the ribs' 6 m).</summary>
        public const float FREIGHT_INWARD = 1.6f;
        /// <summary>Fade at both loop ends (m), so a car never pops.</summary>
        public const float FREIGHT_FADE = 14f;

        /// <summary>One freight group: track u on face 210, climb speed (m/s), loop phase (m) and loop length (m, from y -564).</summary>
        public struct Freight { public float U, Speed, Phase, Loop; }

        // R2-11 ("the shaft bursts into view as a mini reveal", 14.6 4 to 5): from the hall exit to the deck centre the walk
        // looks down -X at the blank upper wall of face 210, 178 m away (the city starts 55 m below the eye and the apron and
        // deck mask everything 4-10 degrees below the walk line). The freight group whose track is nearest that sight line
        // runs its loop on up past the city top to y +80 (644 m instead of 480), at a fixed 5 m/s, phased so that on the
        // nominal walk it is already above the deck mask (y -15 to -20 at 178 m) in the hall-exit still and climbs through eye level
        // into its top fade over walk seconds 0-14: three cold lights rising out of the abyss straight ahead, the first lit
        // thing beyond 10 m in the hall-exit and walk-on frames (B4_end, B5_start). On the bridge it is dimmed with the
        // other cars (BRIDGE_GLOW_DIM).
        public const float FREIGHT_DOCK_TOP_Y = 80f, FREIGHT_DOCK_SPEED = 5f;
        /// <summary>Height of the dock group at walk second 0 (the hall exit, d = BEAT_START_D[4], nominal clock).</summary>
        public const float FREIGHT_DOCK_Y_AT_EXIT = -4f;
        public static float FreightDockLoop => FREIGHT_DOCK_TOP_Y - AbyssConst.CITY_BOTTOM_Y;

        /// <summary>Nominal scene clock at the hall exit (walk second 0 of beat 5).</summary>
        public static double HallExitClock => AbyssConst.NominalTimeAtD(AbyssConst.BEAT_START_D[4], false);

        /// <summary>u on face 210 of the walk line's sight line (z = LIFT_TOP.z, looking -X).</summary>
        public static float WalkOnU
        {
            get
            {
                Vector2 n = MeshKit.HexFaceNormal(FACE_210), c = AbyssConst.SHAFT_CENTRE;
                float z = AbyssConst.LIFT_TOP.z;
                float x = c.x + (AbyssConst.SHAFT_APOTHEM - (z - c.y) * n.y) / n.x;
                return FaceU(FACE_210, new Vector3(x, 0f, z));
            }
        }

        /// <summary>Index of the dock group (the track nearest <see cref="WalkOnU"/>) in a placed list, -1 when empty.</summary>
        public static int FreightDockGroup(List<Freight> freight)
        {
            int best = -1;
            float bu = float.MaxValue, u0 = WalkOnU;
            for (int i = 0; i < freight.Count; i++)
            {
                float du = Mathf.Abs(freight[i].U - u0);
                if (du < bu) { bu = du; best = i; }
            }
            return best;
        }

        /// <summary>Top y of a group's loop.</summary>
        public static float FreightTopY(in Freight f) => AbyssConst.CITY_BOTTOM_Y + f.Loop;

        /// <summary>
        /// Eight tracks on face 210 (u snapped to cell centres, at least 30 m apart), seeded speeds 3-6 m/s, 480 m loops; the
        /// dock group (R2-11) loops 644 m at 5 m/s on the walk-on phase.
        /// </summary>
        public static void PlaceFreight(uint seed, List<Freight> result)
        {
            result.Clear();
            var rng = new Rng(seed * 2654435761u + 0x0F5EEu);
            int guard = 0;
            while (result.Count < FREIGHT_GROUPS && guard++ < 4000)
            {
                float u = -170f + 340f * rng.Next();
                u = AbyssConst.CITY_CELL_W * Mathf.Floor(u / AbyssConst.CITY_CELL_W) + 0.5f * AbyssConst.CITY_CELL_W;
                bool ok = true;
                for (int i = 0; i < result.Count; i++) if (Mathf.Abs(result[i].U - u) < 30f) { ok = false; break; }
                if (!ok) continue;
                result.Add(new Freight
                {
                    U = u,
                    Speed = Mathf.Lerp(FREIGHT_V_MIN, FREIGHT_V_MAX, rng.Next()),
                    Phase = FREIGHT_LOOP * rng.Next(),
                    Loop = FREIGHT_LOOP,
                });
            }
            int dock = FreightDockGroup(result);
            if (dock >= 0)
            {
                Freight f = result[dock];
                f.Loop = FreightDockLoop;
                f.Speed = FREIGHT_DOCK_SPEED;
                double x = (FREIGHT_DOCK_Y_AT_EXIT - AbyssConst.CITY_BOTTOM_Y) - FREIGHT_DOCK_SPEED * HallExitClock;
                f.Phase = (float)(x - f.Loop * System.Math.Floor(x / f.Loop));
                result[dock] = f;
            }
        }

        /// <summary>Height above the loop start (0..Loop) of a freight group at scene time t, in double (no drift).</summary>
        public static double FreightOffset(in Freight f, double t)
        {
            double loop = f.Loop > 0f ? f.Loop : FREIGHT_LOOP;
            double x = f.Phase + f.Speed * t;
            return x - loop * System.Math.Floor(x / loop);
        }

        // ------------------------------------------------------------------------------------------ protruding blocks

        /// <summary>Block footprint 12 m along the wall x 18 m deep, 40 m tall (14.5.5 "12 x 18 x 40").</summary>
        public const float BLOCK_W = 12f, BLOCK_D = 18f, BLOCK_H = 40f;
        public const int BLOCK_COUNT = 200;
        /// <summary>Blocks per city face (210 carries the KS5a mid ground).</summary>
        public static readonly int[] BlocksPerFace = { 80, 60, 60 };
        public const float BLOCK_STANDOFF_STEP = 10f;
        public const int BLOCK_STANDOFF_STEPS = 8;     // 0, 10, ..., 70 m: the block front reaches 18-88 m from the wall
        public const float BLOCK_EMBED = 0.3f;          // body runs this far behind its back plane (into the wall for standoff 0)
        /// <summary>Window panel: 3 x 11 cells, bottom 0.75 m above the block base (cell aligned).</summary>
        public const float BLOCK_PANEL_Y0 = 0.75f;
        public const int BLOCK_PANEL_COLS = 3, BLOCK_PANEL_ROWS = 11;
        const float BLOCK_FAN_CLEAR = 175f;
        const float BLOCK_U_MAX = 188f;

        /// <summary>Block mesh variants: 0 banded with roof plant and mast, 1 finned with crown, 2 set back above the 7th panel row, 3 stepped (narrow upper tower).</summary>
        public const int BLOCK_VARIANTS = 4;

        /// <summary>Per-instance concrete tone of the block bodies (albedo x, linear; +/-8 %, R1-01 (3)).</summary>
        public static readonly float[] BlockToneScale = { 0.92f, 0.973f, 1.027f, 1.08f };

        /// <summary>Variant 2: front and sides step back above this many panel rows (at 25.25 m); upper front 6 m behind, sides 1 m in.</summary>
        public const int SETBACK_ROWS = 7;
        public const float SETBACK_FRONT = 6f, SETBACK_SIDE = 1f;

        /// <summary>Variant 3: the full block rises to this many panel rows (21.75 m); a 7.5 m wide tower continues on the -x side, its front 0.4 m in.</summary>
        public const int STEP_ROWS = 6;
        public const float STEP_TOWER_X0 = -5.7f, STEP_TOWER_X1 = 1.8f, STEP_TOWER_RECESS = 0.4f;

        public struct Block
        {
            public int Face;        // hexagon face index
            public float U;         // centre along the face (cell aligned: u - 4.5 on the 3 m grid)
            public float Y0;        // base height
            public int Standoff;    // 0..7 (x 10 m)
            public int Variant;     // 0..BLOCK_VARIANTS-1 (see BLOCK_VARIANTS)
            public int Tone;        // index into BlockToneScale
            public bool Beacon, Amber;
            public float StandoffM => Standoff * BLOCK_STANDOFF_STEP;
        }

        /// <summary>Beacon position in the block body frame (mast top, crown corner, upper roof corners).</summary>
        public static Vector3 BeaconLocal(int variant)
        {
            float h = BLOCK_H, d = BLOCK_D;
            switch (variant)
            {
                case 0: return new Vector3(3f, h + 6.4f, 3f);
                case 1: return new Vector3(5.7f, h + 1.4f, d - 0.3f);
                case 2: return new Vector3(0.5f * BLOCK_W - SETBACK_SIDE - 0.4f, h + 0.4f, d - SETBACK_FRONT - 0.4f);
                default: return new Vector3(STEP_TOWER_X1 - 0.4f, h + 0.4f, d - STEP_TOWER_RECESS - 0.4f);
            }
        }

        /// <summary>Object-to-world matrix of a block body (origin on the wall plane at (u, base y), pushed out by the standoff).</summary>
        public static Matrix4x4 BlockMatrix(in Block b)
            => Matrix4x4.TRS(WallPoint(b.Face, b.U, b.Y0, b.StandoffM), FaceRotation(b.Face), Vector3.one);

        /// <summary>Object-to-world matrix of a block's girder frame (origin on the wall plane at (u, base y)).</summary>
        public static Matrix4x4 BlockFrameMatrix(in Block b)
            => Matrix4x4.TRS(WallPoint(b.Face, b.U, b.Y0, 0f), FaceRotation(b.Face), Vector3.one);

        struct Gap { public float Lo, Hi; }
        static readonly List<Gap> s_gaps = new List<Gap>(16);

        /// <summary>Clear height bands between the ring levels inside the city (rib +/-3, belt +/-6, 1.5 m margin).</summary>
        static void RingGaps(List<Gap> gaps)
        {
            gaps.Clear();
            for (int k = 0; k + 1 < AbyssConst.RIB_LEVELS; k++)
            {
                float yHi = AbyssConst.RingY(k), yLo = AbyssConst.RingY(k + 1);
                float hHi = 0.5f * (AbyssConst.RingIsBelt(k) ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT);
                float hLo = 0.5f * (AbyssConst.RingIsBelt(k + 1) ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT);
                float hi = yHi - hHi - 1.5f, lo = yLo + hLo + 1.5f;
                if (hi > AbyssConst.CITY_TOP_Y - 2f) continue;                  // keep the blocks inside the city rows
                if (lo < AbyssConst.CITY_BOTTOM_Y - 4f) continue;
                if (hi - lo < BLOCK_H + 1f) continue;
                gaps.Add(new Gap { Lo = lo, Hi = hi });
            }
        }

        /// <summary>
        /// Seeded placement of the 200 blocks: per face, u on the 3 m grid (+1.5), base y between two ring levels and snapped so
        /// the window panel lands on the 3.5 m rows, standoff 0-70 m (biased to the wall). Rejected: overlap with another block
        /// on the face (3 m gap), any block of another face (world bounds, 2 m), the fan zone (175 m), the mouth / gantry /
        /// stay corridor on face 270 (|u + 45| &lt; 60 below y -360), the freight tracks on face 210 (12 m), any part above the
        /// face-270 city top (the bare near flat, <see cref="CITY_TOP_270"/>), and the bridge void (R1-01): a block seen in a
        /// look straight down from the bridge through more than <see cref="BRIDGE_VIEW_T_MAX"/> fog transmittance is retried
        /// against the wall (standoff 0) and dropped if still visible. Faces are filled 270, 150, 210; each deficit carries on.
        /// </summary>
        public static void PlaceBlocks(uint seed, List<Block> result, List<Freight> freight)
        {
            result.Clear();
            RingGaps(s_gaps);
            if (s_gaps.Count == 0) return;
            var bounds = new List<Bounds>(BLOCK_COUNT);
            int carry = 0;
            for (int step = 0; step < 2 * s_blockFaceOrder.Length; step++)
            {
                // most constrained face first (270: fan, gantry and bridge zones); a deficit carries over to the next face, and a
                // second round (own stream) takes whatever is still missing, so the count is always BLOCK_COUNT
                int oi = step % s_blockFaceOrder.Length, round = step / s_blockFaceOrder.Length;
                if (round > 0 && carry == 0) break;
                int fi = s_blockFaceOrder[oi];
                int face = CityFaces[fi];
                var rng = new Rng(seed * 747796405u + (uint)(fi + 1 + 3 * round) * 2891336453u);
                int placed = 0, attempts = 0, target = (round == 0 ? BlocksPerFace[fi] : 0) + carry;
                while (placed < target && attempts++ < Mathf.Max(target, 20) * 400)
                {
                    float u = -BLOCK_U_MAX + 2f * BLOCK_U_MAX * rng.Next();
                    u = AbyssConst.CITY_CELL_W * Mathf.Round((u - 1.5f) / AbyssConst.CITY_CELL_W) + 1.5f;
                    if (Mathf.Abs(u) > BLOCK_U_MAX) continue;
                    Gap g = s_gaps[rng.Range(0, s_gaps.Count)];
                    // base y = CITY_TOP_Y - BLOCK_PANEL_Y0 - 3.5 m (panel rows on the city grid)
                    float yTop0 = AbyssConst.CITY_TOP_Y - BLOCK_PANEL_Y0;
                    int mLo = Mathf.CeilToInt((yTop0 - (g.Hi - BLOCK_H)) / AbyssConst.CITY_CELL_H);
                    int mHi = Mathf.FloorToInt((yTop0 - g.Lo) / AbyssConst.CITY_CELL_H);
                    if (mHi < mLo) continue;
                    float y0 = yTop0 - AbyssConst.CITY_CELL_H * rng.Range(mLo, mHi + 1);
                    float r = rng.Next();
                    int standoff = Mathf.Min(BLOCK_STANDOFF_STEPS - 1, (int)(BLOCK_STANDOFF_STEPS * r * r));
                    if (Mathf.Abs(u) > 150f) standoff = Mathf.Min(standoff, 2);   // near the corners keep close to the wall

                    if (face == FACE_270)
                    {
                        if (y0 + BLOCK_H > CITY_TOP_270) continue;   // the bare near flat under the bridge
                        float cx = Mathf.Clamp(AbyssConst.FAN_CENTRE.x, u - 0.5f * BLOCK_W, u + 0.5f * BLOCK_W);
                        float cy = Mathf.Clamp(AbyssConst.FAN_CENTRE.y, y0, y0 + BLOCK_H);
                        float dx = cx - AbyssConst.FAN_CENTRE.x, dy = cy - AbyssConst.FAN_CENTRE.y;
                        if (dx * dx + dy * dy < BLOCK_FAN_CLEAR * BLOCK_FAN_CLEAR) continue;
                        if (Mathf.Abs(u - AbyssConst.W[15].x) < 60f && y0 < -360f) continue;
                    }
                    if (face == FACE_210 && freight != null)
                    {
                        bool hit = false;
                        for (int i = 0; i < freight.Count; i++)
                            if (Mathf.Abs(freight[i].U - u) < 12f + FREIGHT_SPACING) { hit = true; break; }
                        if (hit) continue;
                    }
                    bool overlap = false;
                    for (int i = 0; i < result.Count; i++)
                    {
                        Block o = result[i];
                        if (o.Face != face) continue;
                        if (Mathf.Abs(o.U - u) < BLOCK_W + 3f && y0 < o.Y0 + BLOCK_H + 3f && o.Y0 < y0 + BLOCK_H + 3f) { overlap = true; break; }
                    }
                    if (overlap) continue;
                    var b = new Block
                    {
                        Face = face, U = u, Y0 = y0, Standoff = standoff,
                        Variant = rng.Range(0, BLOCK_VARIANTS),
                        Tone = rng.Range(0, BlockToneScale.Length),
                    };
                    b.Beacon = b.Variant == 0 || rng.Next() < 0.25f;
                    b.Amber = b.Beacon && rng.Next() < 0.12f;
                    // the bridge void: back against the wall, else not here
                    if (BlockBridgeTransmittance(b) > BRIDGE_VIEW_T_MAX)
                    {
                        if (b.Standoff == 0) continue;
                        b.Standoff = 0;
                        if (BlockBridgeTransmittance(b) > BRIDGE_VIEW_T_MAX) continue;
                    }
                    Bounds wb = BlockWorldBounds(b);
                    wb.Expand(4f);
                    for (int i = 0; i < result.Count; i++)
                        if (result[i].Face != face && bounds[i].Intersects(wb)) { overlap = true; break; }
                    if (overlap) continue;
                    result.Add(b);
                    bounds.Add(BlockWorldBounds(b));
                    placed++;
                }
                carry = target - placed;
            }
        }

        /// <summary>
        /// Indices into <see cref="CityFaces"/>: 270 (fan, mouth and bridge zones: most constrained), then 150 (seen least: it takes
        /// the near flat's deficit, so face 210, the KS5a wall, keeps its own density), then 210.
        /// </summary>
        static readonly int[] s_blockFaceOrder = { 2, 1, 0 };

        /// <summary>World AABB of a block body including its standoff girders (back to the wall plane).</summary>
        public static Bounds BlockWorldBounds(in Block b)
        {
            Matrix4x4 m = BlockFrameMatrix(b);
            var local = new Bounds();
            local.SetMinMax(new Vector3(-0.5f * BLOCK_W - 1f, -1f, -BLOCK_EMBED), new Vector3(0.5f * BLOCK_W + 1f, BLOCK_H + 7f, b.StandoffM + BLOCK_D + 1f));
            return InstancedBatch.TransformBounds(m, local);
        }

        // ------------------------------------------------------------------------------------------ guide rails

        public const float GUIDE_SEG = 12f, GUIDE_W = 4f, GUIDE_D = 6f;

        /// <summary>Unit direction of the lift line, top dock to bottom dock (117, -480, 0) / 494.05.</summary>
        public static Vector3 LiftDir => (AbyssConst.LIFT_BOTTOM - AbyssConst.LIFT_TOP).normalized;

        /// <summary>Unit perpendicular to the lift line in the xy plane, toward +x (and slightly up).</summary>
        public static Vector3 LiftPerp { get { Vector3 d = LiftDir; return new Vector3(-d.y, d.x, 0f); } }

        /// <summary>Rail-local frame: z along the line (down), y along <see cref="LiftPerp"/>, x = world -z.</summary>
        public static Quaternion RailRotation => Quaternion.LookRotation(LiftDir, LiftPerp);

        public static float LiftLineLength => (AbyssConst.LIFT_BOTTOM - AbyssConst.LIFT_TOP).magnitude;
        public static float GuideRailLength => AbyssConst.GUIDE_RAIL_SEGMENTS * GUIDE_SEG;

        /// <summary>How far each guide rail reaches past each dock along the line (576 - 494.05) / 2 = 40.97 m.</summary>
        public static float GuideRailExtension => 0.5f * (GuideRailLength - LiftLineLength);

        public static float GuideRailZ(int rail) => rail == 0 ? AbyssConst.GUIDE_RAIL_Z0 : AbyssConst.GUIDE_RAIL_Z1;

        /// <summary>Upper end of the rail centre line.</summary>
        public static Vector3 GuideRailTop(int rail)
        {
            Vector3 p = AbyssConst.LIFT_TOP - LiftDir * GuideRailExtension;
            p.z = GuideRailZ(rail);
            return p;
        }

        /// <summary>Centre of segment i (0 at the top) of a rail.</summary>
        public static Vector3 GuideSegmentCentre(int rail, int i) => GuideRailTop(rail) + LiftDir * (GUIDE_SEG * (i + 0.5f));

        // ------------------------------------------------------------------------------------------ dock apron

        /// <summary>Dock apron (14.5.5): 10 m from the hall exit to the deck edge, 1 m slab, 0.4 m bridging plate, 0.3 m steel nosing.</summary>
        public const float DOCK_LENGTH = 10f, DOCK_SLAB_T = 1f, DOCK_PLATE = 0.4f, DOCK_NOSE = 0.3f, DOCK_RAIL_INSET = 0.1f;

        /// <summary>Edge of the steel nosing (x -151.6): the platform's extended bridging plate covers the last 0.4 m to the deck.</summary>
        public static float DockNoseEdgeX => AbyssConst.HALL_X1 - DOCK_LENGTH + DOCK_PLATE;

        /// <summary>Edge of the concrete slab and end of the dock rails (x -151.3).</summary>
        public static float DockSlabEdgeX => DockNoseEdgeX + DOCK_NOSE;

        /// <summary>An axis-aligned obstacle: xy rectangle and z range (world).</summary>
        public struct Obstacle { public Vector2 Min, Max; public float Z0, Z1; }

        /// <summary>The fixed dock parts the descending platform passes (slab, nosing plate and leg, the two rails), as generated.</summary>
        public static void DockObstacles(List<Obstacle> list)
        {
            list.Clear();
            float y = AbyssConst.LIFT_TOP.y, zc = AbyssConst.LIFT_TOP.z, half = 0.5f * PathGuide.DOCK_APRON_W;
            float xs = DockSlabEdgeX, xn = DockNoseEdgeX, xa = AbyssConst.HALL_X1;
            list.Add(new Obstacle { Min = new Vector2(xs, y - DOCK_SLAB_T), Max = new Vector2(xa, y), Z0 = zc - half, Z1 = zc + half });
            list.Add(new Obstacle { Min = new Vector2(xn, y - 0.05f), Max = new Vector2(xs + 0.02f, y + 0.01f), Z0 = zc - half, Z1 = zc + half });
            list.Add(new Obstacle { Min = new Vector2(xs - 0.02f, y - 0.61f), Max = new Vector2(xs, y - 0.05f), Z0 = zc - half, Z1 = zc + half });
            for (int s = -1; s <= 1; s += 2)
            {
                float z = zc + s * (half - DOCK_RAIL_INSET);
                list.Add(new Obstacle { Min = new Vector2(xs - 0.035f, y - 0.01f), Max = new Vector2(xa - 0.27f, y + AbyssConst.RAIL_H), Z0 = z - 0.035f, Z1 = z + 0.035f });
            }
        }

        // ------------------------------------------------------------------------------------------ deck keep-out (pylons)

        /// <summary>Pylon centres on the deck at (+/-7, +/-7) from the deck centre (14.5.5).</summary>
        public const float PYLON_OFFSET = 7f;

        /// <summary>Half-size of a pylon's footprint at walking height: the 1.3 m base plate (stiffeners reach 0.63, bolts 0.59).</summary>
        public const float PYLON_FOOTPRINT_HALF = 0.65f;

        /// <summary>Half-size of a pylon keep-out square for the capsule centre: footprint + capsule radius (1.0 m).</summary>
        public const float PYLON_KEEP_OUT_HALF = PYLON_FOOTPRINT_HALF + AbyssConst.CAPSULE_R;

        /// <summary>
        /// The four squares (deck-local; Rect x = local x, Rect y = local z) the kinematic player's capsule centre must stay out
        /// of. They span 6..8 m from the centre on both axes: inside the walk rectangle (+/-8.65), clear of the boarding
        /// (+X, |z| &lt;= 1.65) and gate (-Z, |x| &lt;= 1.65) corridors and of each other. 14.5.5 says "free on 18 x 18" but puts
        /// the pylons inside that square; the deck is walkable everywhere else.
        /// </summary>
        public static Rect[] DeckKeepOut()
        {
            var r = new Rect[4];
            float h = PYLON_KEEP_OUT_HALF;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                r[i] = new Rect(sx * PYLON_OFFSET - h, sz * PYLON_OFFSET - h, 2f * h, 2f * h);
            }
            return r;
        }

        /// <summary>
        /// Keep a deck-local capsule-centre move (cx, cz) -> (nx, nz) out of the keep-out rects, in the style of
        /// PlayerController.ClampSoft. A move from outside that ends inside a rect stops on the face it crossed (the axis on
        /// which the previous position lay outside; both, from a corner: the axis of least penetration) and slides along it. A
        /// position already inside (placed there) is never pushed or snapped: it may move anywhere that is not deeper
        /// (penetration = min over the axes of the distance to the nearest edge). Only System.Math and UnityEngine.Rect, so the
        /// lead can call it from (or move it verbatim into) the runtime: MoveOnLift after the rectangle clamp. Allocation-free.
        /// </summary>
        public static void KeepOut(Rect[] rects, double cx, double cz, ref double nx, ref double nz)
        {
            if (rects == null) return;
            for (int i = 0; i < rects.Length; i++)
            {
                Rect r = rects[i];
                double x0 = r.xMin, x1 = r.xMax, z0 = r.yMin, z1 = r.yMax;
                if (nx <= x0 || nx >= x1 || nz <= z0 || nz >= z1) continue;      // the move ends outside (edges count as outside)
                bool outX = cx <= x0 || cx >= x1, outZ = cz <= z0 || cz >= z1;
                if (!outX && !outZ)
                {
                    // already inside: never deeper. Keep the move if it is not deeper, else its non-deepening axis, else stay.
                    double pc = Depth(cx, cz, x0, x1, z0, z1);
                    if (Depth(nx, nz, x0, x1, z0, z1) <= pc) continue;
                    if (Depth(nx, cz, x0, x1, z0, z1) <= pc) { nz = cz; continue; }
                    if (Depth(cx, nz, x0, x1, z0, z1) <= pc) { nx = cx; continue; }
                    nx = cx; nz = cz;
                    continue;
                }
                bool alongX;
                if (outX && outZ)
                {
                    double px = System.Math.Min(nx - x0, x1 - nx), pz = System.Math.Min(nz - z0, z1 - nz);
                    alongX = px <= pz;
                }
                else alongX = outX;
                if (alongX) nx = cx <= x0 ? x0 : x1;
                else nz = cz <= z0 ? z0 : z1;
            }
        }

        /// <summary>Penetration of a point into a rect: the distance to its nearest edge (0 or less = outside).</summary>
        static double Depth(double x, double z, double x0, double x1, double z0, double z1)
            => System.Math.Min(System.Math.Min(x - x0, x1 - x), System.Math.Min(z - z0, z1 - z));

        // ------------------------------------------------------------------------------------------ platform clearance

        /// <summary>The hall hull exterior in the xy plane (z 199..339 contains the whole platform): lower -X edge chamfered 12 x 12.</summary>
        public static Vector2[] HullPolygon()
        {
            float x0 = AbyssConst.HALL_X1, x1 = AbyssConst.HALL_X0, yb = AbyssConst.HALL_HULL_BOTTOM_Y, yt = AbyssConst.HALL_ROOF_Y;
            float c = AbyssConst.HALL_CHAMFER;
            return new[]
            {
                new Vector2(x0, yt), new Vector2(x0, yb + c), new Vector2(x0 + c, yb), new Vector2(x1, yb), new Vector2(x1, yt),
            };
        }

        /// <summary>Platform travel in the xy plane over the whole ride (117, -480).</summary>
        public static Vector2 TravelXY => new Vector2(AbyssConst.LIFT_BOTTOM.x - AbyssConst.LIFT_TOP.x, AbyssConst.LIFT_BOTTOM.y - AbyssConst.LIFT_TOP.y);

        /// <summary>Minimum distance between the hull and the trajectory of a platform-local point over s 0..480 (0 = touches).</summary>
        public static float HullDistance(Vector3 local, Vector2[] hull)
        {
            var a = new Vector2(AbyssConst.LIFT_TOP.x + local.x, AbyssConst.LIFT_TOP.y + local.y);
            return SegmentPolygonDistance(a, a + TravelXY, hull);
        }

        /// <summary>
        /// Minimum distance between the trajectory of a platform-local point and an axis-aligned obstacle box (xy rectangle,
        /// z range). +infinity when the point's z lies outside the box's z range.
        /// </summary>
        public static float BoxDistance(Vector3 local, Vector2 min, Vector2 max, float z0, float z1)
        {
            float z = AbyssConst.LIFT_TOP.z + local.z;
            if (z < z0 || z > z1) return float.PositiveInfinity;
            var a = new Vector2(AbyssConst.LIFT_TOP.x + local.x, AbyssConst.LIFT_TOP.y + local.y);
            s_rect[0] = new Vector2(min.x, max.y); s_rect[1] = new Vector2(min.x, min.y);
            s_rect[2] = new Vector2(max.x, min.y); s_rect[3] = new Vector2(max.x, max.y);
            return SegmentPolygonDistance(a, a + TravelXY, s_rect);
        }

        static readonly Vector2[] s_rect = new Vector2[4];

        /// <summary>Distance between a segment and a convex polygon in 2D (0 when they touch or intersect).</summary>
        public static float SegmentPolygonDistance(Vector2 a, Vector2 b, Vector2[] poly)
        {
            if (InsideConvex(a, poly) || InsideConvex(b, poly)) return 0f;
            float best = float.PositiveInfinity;
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                if (SegmentsIntersect(a, b, p, q)) return 0f;
                best = Mathf.Min(best, PointSegment(a, p, q));
                best = Mathf.Min(best, PointSegment(b, p, q));
                best = Mathf.Min(best, PointSegment(p, a, b));
                best = Mathf.Min(best, PointSegment(q, a, b));
            }
            return best;
        }

        static bool InsideConvex(Vector2 p, Vector2[] poly)
        {
            int n = poly.Length;
            float sign = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                float c = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (Mathf.Abs(c) < 1e-9f) continue;
                if (sign == 0f) sign = Mathf.Sign(c);
                else if (Mathf.Sign(c) != sign) return false;
            }
            return true;
        }

        static float PointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
            return (a + ab * t - p).magnitude;
        }

        static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
