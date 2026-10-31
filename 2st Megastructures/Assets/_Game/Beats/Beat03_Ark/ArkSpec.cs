using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Material ids owned by builder BI (beats 3 and 4), defined in Scripts/Editor/MaterialDefs/Beat34MaterialDefs.cs
    /// and resolved at load through RingAssets like the RingMat ids (RingMat.ArkHull and RingMat.ArkWindow are defined
    /// there too). Shaders: Megastructure/RingArk (Shaders/RingArk.shader, M07: _Mode 0 hull, 1 window band, 2 engine),
    /// Megastructure/RingCeramic (turf, ashlar) and Megastructure/RingUnderpass (Beats/Beat04_Sunset/RingUnderpass.shader).
    /// </summary>
    public static class Beat34Mat
    {
        public const string ArkEngine = "ArkEngine";             // M07 engine nozzle interiors, EngineBlue HDR 6 (RingArk _Mode 2)
        public const string Turf = "Turf";                       // LeveeGreen turf with mottle (levee berms and tops, footbridge mound)
        public const string Ashlar = "Ashlar34";                 // dressed stone without course lines (voussoirs, jambs, copings)
        public const string LeveeStone = "LeveeStone";           // levee faces: 0.6 x 0.3 m courses, running bond (RingUnderpass mode 1)
        public const string BridgeStone = "BridgeStone";         // footbridge vault and walls: courses as the levee, warmer stone
        public const string LeveeTrim = "LeveeTrim";             // levee copings and stair cheek walls: pale ceramic, sconce-lit
        public const string Lamp = "Lamp34";                     // beat-4 lamp lenses (RingEmissive, lamp level, WarmLampDisplay)
        public const string UnderpassLining = "UnderpassLining"; // underpass lining (RingUnderpass: exposure-weighted sky lights)
        public const string UnderpassRib = "UnderpassRib";       // underpass ribs (RingUnderpass)

        /// <summary>Lamp lens level at lamp level 3 (fully on) for <see cref="Lamp"/>: 0.7 x WarmLampDisplay. With the
        /// beat-4 exposure (+1.6 EV = x 3.03) that is 2.1 linear on screen, bloom carries the glow (critique C11).</summary>
        public const float LampLevel = 0.7f;

        /// <summary>
        /// WarmLamp authored for its displayed colour: each linear channel raised to the power 1.8 (1, 0.52, 0.15). The
        /// spec's WarmLamp x 3.0 under the beat-4 exposure is ~9 linear, which the Neutral tonemapper and the cool night
        /// white balance turn white (critique C11, heads (205, 209, 209)); this tint keeps lamp heads and light pools
        /// near #FFD9A0 on screen. Shared by the lamp material (editor) and the light-pool vertex tints (runtime).
        /// </summary>
        public static Color WarmLampDisplay
        {
            get
            {
                Color w = RingPalette.Lin("WarmLamp");
                return new Color(Mathf.Pow(w.r, 1.8f), Mathf.Pow(w.g, 1.8f), Mathf.Pow(w.b, 1.8f), 1f);
            }
        }
    }

    /// <summary>
    /// Numbers of the ark (MASTER_PROMPT 14.5.3, 14.12 ARK) shared by the runtime builder and the editor material
    /// definitions (window grid, lit fraction). Object space of the ark: origin at the hull centre, +X = bow (heading),
    /// +Y up, +Z = the side away from the player (the player sees the -Z flank and the belly).
    /// </summary>
    public static class ArkSpec
    {
        public const float Length = RingConstants.ArkLength;      // 3 000
        public const float Beam = RingConstants.ArkBeam;          // 420
        public const float Height = RingConstants.ArkHeight;      // 260
        public const float HalfBeam = Beam * 0.5f, HalfHeight = Height * 0.5f;

        public const float SuperN = 2.4f;            // superellipse exponent (14.5.3)
        public const int Radial = 48;                // radial vertices (14.5.3)
        public const int BodyRings = 141;            // rings over t 0..0.88 (body and stern taper)
        public const int NoseRings = 18;             // rings over the bow; with the tip apex: 141 + 18 + 1 = 160 stations
        public const float BowFrac = 0.12f;          // bulbous bow over the front 12 % (14.5.3)
        public const float TaperFrac = 0.18f;        // stern taper over the rear 18 % (14.5.3)
        public const float SternScale = 0.82f;       // section at the stern face (an 18 % taper), carries the 7 engines
        public const float NoseExp = 2.3f;           // blunt (bulbous) nose profile exponent
        public const float BulbAmp = 0.06f;          // slight swelling at the start of the bow

        public const float KeelDepth = 40f;          // 40 m keel (14.5.3)
        public const float KeelTopW = 26f, KeelBottomW = 10f, KeelEmbed = 6f, KeelX0 = -1200f, KeelX1 = 1020f, KeelRamp = 90f;

        public const int SparCount = 12;             // 12 spars 180 x 14 x 14 m, 240 m pitch (14.5.3)
        public const float SparLen = 180f, SparSize = 14f, SparPitch = 240f;

        // Windows: 6 rows x 166 windows 12 x 5 m at 18 m pitch (14.5.3). Each window owns one cell quad (18 m along X
        // by one deck pitch along the flank) of the window band mesh; the 12 x 5 m aperture inside the cell is analytic
        // and footprint-filtered in RingArk _Mode 1 (grid -> energy-preserving band below 8 px, pure band below 4 px).
        public const int WinRows = 6, WinPerRow = 166;
        public const float WinW = 12f, WinH = 5f, WinPitch = 18f;
        public const float RowPitch = 14f;           // deck spacing of the window rows (cell height)
        public const float WinRowY0 = -50f;          // centre height of the lowest row on the -Z flank
        public const float WinStandoff = 0.6f;       // the band floats 0.6 m outside the hull (no co-planar faces)
        public const float WinMinScale = 0.72f;      // no window cell where the bow section is smaller than this
        public const float WinX0 = -0.5f * (WinPerRow - 1) * WinPitch;  // first window centre x (-1 485)
        public const float WinLitFraction = 0.62f;   // share of lit windows (deterministic PCG pattern)
        public const float WinDarkLevel = 0.05f;     // a dark window still glows faintly
        public const uint WinSeed = 0x41524Bu;       // "ARK"

        public const int EngineCount = 7;            // 7 engine cones r 26 m, 90 m (14.5.3)
        public const float EngineR = 26f, EngineLen = 90f, EngineThroatR = 18f, EngineRing = 62f;
        public const float GlowRadius = 34f;         // engine glow billboard core radius (RingMat.Glow)
        public const float GlowIntensity = 1.6f;     // glow peak radiance x EngineBlue (the cones themselves are HDR 6): stays blue after the tonemapper
        public const float MarkerRadius = 3f;        // spar / keel marker lights (WindowWarm, RingMat.Glow)
        public const float MarkerIntensity = 3f;

        public const float RollDeg = 0.4f, RollPeriod = 40f, PitchDeg = 0.2f, PitchPeriod = 55f;  // 14.5.3 motion
        public const float BendMaxDistance = 12000f; // bounds inflation: rise 4 293 m >= the required +2 100 m (14.5.3)

        public static float XToT(float x) => (x + 0.5f * Length) / Length;
        public static float TToX(float t) => t * Length - 0.5f * Length;

        /// <summary>Section scale along the hull, t = 0 stern .. 1 bow tip.</summary>
        public static float SectionScale(float t)
        {
            if (t <= 0f) return SternScale;
            if (t < TaperFrac) return SternScale + (1f - SternScale) * RingMath.Smootherstep01(t / TaperFrac);
            float tb = 1f - BowFrac;
            if (t <= tb) return 1f;
            float q = Mathf.Clamp01((t - tb) / BowFrac);
            float s = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(q, NoseExp)), 1f / NoseExp);
            float b = Mathf.Sin(Mathf.PI * q);
            return s * (1f + BulbAmp * b * b);
        }

        /// <summary>Ring station t values: BodyRings over 0..0.88, then NoseRings clustered toward the tip (the tip
        /// itself, t = 1, is a fan apex and not a ring).</summary>
        public static float[] StationT()
        {
            var t = new float[BodyRings + NoseRings];
            float tb = 1f - BowFrac;
            for (int i = 0; i < BodyRings; i++) t[i] = tb * i / (BodyRings - 1);
            for (int j = 1; j <= NoseRings; j++) t[BodyRings - 1 + j] = tb + BowFrac * Mathf.Sin(0.5f * Mathf.PI * j / (NoseRings + 1));
            return t;
        }

        /// <summary>Superellipse point (z, y) at parameter phi for half extents a (z) and b (y).</summary>
        public static Vector2 SuperPoint(float phi, float a, float b)
        {
            float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
            float e = 2f / SuperN;
            float z = a * (c < 0f ? -1f : 1f) * Mathf.Pow(Mathf.Abs(c), e);
            float y = b * (s < 0f ? -1f : 1f) * Mathf.Pow(Mathf.Abs(s), e);
            return new Vector2(z, y);
        }

        /// <summary>Point and outward normal on the -Z flank at height y for section scale s (analytic surface).</summary>
        public static void FlankPoint(float y, float s, out float z, out Vector2 normalZY)
        {
            float A = HalfBeam * s, B = HalfHeight * s;
            float yy = Mathf.Clamp(y, -0.995f * B, 0.995f * B);
            float r = Mathf.Pow(Mathf.Abs(yy) / B, SuperN);
            z = -A * Mathf.Pow(Mathf.Max(0f, 1f - r), 1f / SuperN);
            float gz = -SuperN * Mathf.Pow(Mathf.Abs(z) / A, SuperN - 1f) / A;
            float gy = SuperN * Mathf.Pow(Mathf.Abs(yy) / B, SuperN - 1f) / B * (yy < 0f ? -1f : 1f);
            normalZY = new Vector2(gz, gy);
            float l = normalZY.magnitude;
            normalZY = l > 1e-9f ? normalZY / l : new Vector2(-1f, 0f);
        }

        /// <summary>Arc length (m) along the analytic -Z flank between heights y0 and y1 (signed: y1 above y0 is
        /// positive) for section scale s.</summary>
        public static float FlankArc(float y0, float y1, float s)
        {
            const int steps = 12;
            float acc = 0f;
            FlankPoint(y0, s, out float zp, out _);
            float yp = y0;
            for (int i = 1; i <= steps; i++)
            {
                float y = Mathf.Lerp(y0, y1, i / (float)steps);
                FlankPoint(y, s, out float z, out _);
                acc += Mathf.Sqrt((y - yp) * (y - yp) + (z - zp) * (z - zp));
                yp = y; zp = z;
            }
            return y1 >= y0 ? acc : -acc;
        }

        /// <summary>Belly height at lateral offset z (negative y) for section scale s.</summary>
        public static float BellyY(float z, float s)
        {
            float A = HalfBeam * s, B = HalfHeight * s;
            float r = Mathf.Pow(Mathf.Min(Mathf.Abs(z) / A, 0.999f), SuperN);
            return -B * Mathf.Pow(Mathf.Max(0f, 1f - r), 1f / SuperN);
        }

        public static float WindowX(int k) => WinX0 + k * WinPitch;
        public static float RowY(int j) => WinRowY0 + j * RowPitch;

        /// <summary>Range of window columns k (0..WinPerRow-1) whose whole cell (18 m) sits on hull with section scale
        /// >= WinMinScale and clear of the stern face. The bulbous bow leaves the last few columns blind.</summary>
        public static void WindowRange(out int kMin, out int kMax)
        {
            kMin = WinPerRow; kMax = -1;
            float stern = -0.5f * Length + 2f;
            for (int k = 0; k < WinPerRow; k++)
            {
                float x = WindowX(k);
                float x0 = x - 0.5f * WinPitch, x1 = x + 0.5f * WinPitch;
                if (x0 < stern) continue;
                if (SectionScale(XToT(x0)) < WinMinScale || SectionScale(XToT(x1)) < WinMinScale) continue;
                if (k < kMin) kMin = k;
                if (k > kMax) kMax = k;
            }
            if (kMax < kMin) { kMin = 0; kMax = 0; }
        }

        /// <summary>Deterministic window level 0..1 (PCG, RingMath): lit windows 0.70..1.0, dark ones WinDarkLevel.</summary>
        public static float WindowLevel(int k, int j)
        {
            uint h = RingMath.Pcg2(k + 7919 * j, (int)WinSeed);
            float lit = RingMath.U01(h);
            float jitter = RingMath.U01(RingMath.Pcg(h));
            return lit < WinLitFraction ? 0.70f + 0.30f * jitter : WinDarkLevel;
        }

        /// <summary>Mean window level over every placed window (the energy of the pure band, RingArk _WinFade.z).</summary>
        public static float WindowMeanLevel()
        {
            WindowRange(out int kMin, out int kMax);
            double acc = 0.0; int n = 0;
            for (int j = 0; j < WinRows; j++)
                for (int k = kMin; k <= kMax; k++) { acc += WindowLevel(k, j); n++; }
            return n > 0 ? (float)(acc / n) : WinLitFraction;
        }

        /// <summary>Engine nozzle centres on the stern face (y, z): one centre engine and six on a ring.</summary>
        public static Vector2 EngineYZ(int i)
        {
            if (i == 0) return Vector2.zero;
            float a = (30f + 60f * (i - 1)) * Mathf.Deg2Rad;
            return new Vector2(EngineRing * Mathf.Sin(a), EngineRing * Mathf.Cos(a));
        }
    }
}
