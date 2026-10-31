// SkyMath: double-precision C# mirrors of the analytic sky (Shaders/ThreadSky.shader, Include/ThreadSkyLib.hlsl)
// for EditMode tests and for the framing acceptances of the key shots (MASTER_PROMPT 14.5.1, 14.5.6, 14.5.7,
// 14.7.3, 14.7.5, 14.7.7, 14.10; Part 10 item 35). Pure C#, no UnityEngine dependency, not used per frame.
// Camera convention (as the capture harness): world yaw = azimuth from +Z toward +X, pitch positive up, no roll,
// vertical FOV fixed (Hor+, Part 8.10). Screen coordinates are normalised to the half-extents: -1..1, +x right,
// +y up, (0, 0) the image centre.
// F1 (run06): the sun and the planet are screen-space circular impostors (DiscImpostorScreen: projected centre, radius
// f tan(r)), the dusk gradient of the sky shader is mirrored by SkyGradient, the cable shadow band by ShadowBand*.
using System;

namespace Megastructure.Rendering
{
    /// <summary>
    /// Sky numbers that ThreadConstants does not export yet (mirrored by static consts in ThreadSkyLib.hlsl).
    /// Spec numbers cite their section; B02 design choices are marked [B02]. Candidates for ThreadConstants.
    /// </summary>
    public static class SkyConstants
    {
        // stars (14.7.7, 14.5.7)
        public const int StarCellsPerFace = 82;               // 14.7.7 "about 1.1 deg": 90 / 82 = 1.098 deg (equi-angular cube)
        public const double StarOccupancy = 0.08;             // 14.7.7
        public const double StarTier1Fraction = 0.80, StarTier2Fraction = 0.17, StarTier3Fraction = 0.03; // 14.7.7
        public const double StarPeak1 = 0.9, StarPeak2 = 1.6, StarPeak3 = 2.5;                           // 14.7.7
        public const double GlintPeak = 3.5;                  // 14.5.7 2.5 -> 3.5
        public const double StarSigmaMinPx = 0.6;             // 14.7.7
        public const double StarSigmaRad = 1.4e-4;            // [B02] intrinsic size 0.008 deg
        public const double StarJitterMarginCells = 0.2;      // [B02]
        public const double GlintExclusionDeg = 1.5;          // [B02]

        // planet (14.5.7)
        public const double PlanetLightX = 0.55, PlanetLightY = 0.40, PlanetLightZ = 0.73; // 14.5.7 L_p, disc frame
        public const int PlanetBands = 7;                     // 14.5.7
        public const double PlanetOcclusionA0Km = 90.0;       // A10/E11/PAC-13: the disc hides stars from A 90 km, fully from 150 km

        // sun / planet impostors (A4, B4, C-16): projection guard of the shader (SKY_IMP_MIN_FWD)
        public const double ImpostorMinForward = 0.02;

        // dusk sky (C-15; SKY_HOLD_*, SKY_DUSK_CHROMA*)
        public const double HoldOn = 0.5, HoldD0 = 0.85, HoldD1 = 1.0;
        public const double DuskChroma = 0.8, DuskChromaD0 = 0.5, DuskChromaD1 = 0.6, DuskChromaMaxGain = 4.0;

        // cloud sea / Earth (14.7.7; F1: C-01, B2, C-02, E10)
        public const double CloudCell0 = 6000.0, CloudCell1 = 1500.0, CloudCell2 = 400.0; // 14.7.7
        public const double CloudFadePx0 = 12.0, CloudFadePx1 = 24.0;   // E10 (spec 3 px, D-028 6-16 px)
        public static readonly double[] CloudAltitudeFadeKm0 = { 40.0, 30.0, 10.0 };   // B2/C-02/E10: per octave (6 km, 1.5 km, 400 m) ...
        public static readonly double[] CloudAltitudeFadeKm1 = { 60.0, 50.0, 25.0 };   // ... all gone at 60 km
        public const double CloudWarp = 0.2;                  // C-01 (spec 0.6)
        public const double CloudToneTop = -0.185, CloudToneShade = -0.45, CloudToneEdge = 0.03; // C-01 tone thresholds (v = lit - sin e); edge 0.05 -> 0.03 (wave 3)
        // wave 3 (B-r2-2, C-r2-1, E10): every tone edge is at least CloudTonePx px wide by the analytic screen gradient of v
        // (or by CloudToneFwidthMix x fwidth(v) where that is larger);
        // a dome outline enters over PuffAaPx px, a taller dome takes over across PuffCreasePx px, and the rim floor of the
        // dome slope is raised to sqrt(PuffRimPx fp / r) so the steep rim band stays ~2 px or wider (SKY_TONE_PX, SKY_PUFF_*)
        public const double CloudTonePx = 2.5, CloudToneFwidthMix = 0.7, PuffAaPx = 3.0, PuffCreasePx = 3.0, PuffRimPx = 4.0, PuffRimFloor = 0.2, PuffRimMax = 0.9;
        public const double EarthEdgeAaPx = 0.75;             // A-r2-2/E-r2-2: weather and coast edges 0.75 x fwidth (~1.5 px; SKY_EARTH_AA_PX)
        public const double CloudCoverageMin = 0.45;          // 14.7.7 c(A) = lerp(1.0, 0.45, ...)
        public const double SeaHazeLength = 80000.0;          // 14.7.7 exp(-t / 80 km)
        public const double EarthHazeAtmoGain = 0.6;          // C-02: from orbit the haze grades toward ATMO_RIM x 0.6
        public const double ContinentLandThreshold = 0.58;    // C-02: about 30 percent land
        public const double LimbGlowGain = 1.2;               // 14.7.7
        public const double LimbGlowLayer = 25000.0;          // 14.7.7 eps0 = 25 km / sqrt(A (2R + A))
        public const double ShadowBandNarrowing = 0.3;        // 14.7.7 w(t) = 50 (1 - 0.3 smoothstep(0, 30 km, t)) + 0.0033 t
        public const double ShadowBandNarrowingT = 30000.0;
        public const double ShadowBandEdge = 20.0;            // 14.7.7 (the shader uses 6 m, D-032)
        public const double ShadowBandEdgeShader = 6.0;       // D-032
        public const double ShadowBandStart = 25.0;           // [B02] t > 0 softened over +- 25 m
        public const double ShadowBandAltWiden = 0.012;       // E3: half width + 0.012 h (h above the cloud tops)
        public const double ShadowBandFadeAlt0 = 4.0, ShadowBandFadeAlt1 = 16.0; // E3: fade over [max(12 km, 4 h), max(30 km, 16 h)]
        public const double ShadowBandOpacityF1 = 0.8;        // C-01 / E3 (spec 0.62)
        public const double ShadowBandDuskMix = 0.5;          // A3 / C-01: tone toward SKY_M_DUSK by 0.5 smoothstep(0.5, 1, D)
                                                              // wave 3: applied as a multiplier (tint = tone / CLOUD_TOP), no shade corridor
        public const double GlareGain = 0.6, GlarePow = 6.0;  // PAC-12: sunward sea toward CREAM x TC_SunDiscGain by 0.6 cos^6, x sun alpha

        // stars faded by the luminance of the sky behind them (B-r2-3, E-r2-6; SKY_STAR_SKY_Y0 / Y1)
        public const double StarSkyLuminance0 = 0.25, StarSkyLuminance1 = 0.36;

        // B02 design constants (see the B02 report / DEVELOPMENT.md request)
        public const double LimbHazeFullDeg = 0.2;            // E16/B-r2-7: full haze for 0.2 deg below the R-limb ...
        public const double LimbHazeRampDeg = 1.2;            // ... resolving over 1.2 deg (was a 0.35 deg ramp, [B02] R7 2.3)
        public const double SeaEmergeHeight = 300.0;          // [B02] m above the cloud tops
        public const double GlowGateKm0 = 20.0, GlowGateKm1 = 80.0; // [B02] R7 2.4
        public const double HazeLayerHeight = ThreadConstants.ScaleHeight; // [B02] sea haze path limited to the 8.5 km layer
    }

    /// <summary>Screen-space profile of the far thread on one image row, as the sky shader computes it.</summary>
    public struct ThreadPixelProfile
    {
        public bool found;          // the axis crosses this row inside the search range
        public double centreX;      // normalised screen x of the axis on the row
        public double trueWidthPx;  // 2 halfE / |grad e| (the true analytic width)
        public double shownWidthPx; // max(true, minimum width)
        public double alpha;        // energy rule: 1, or max(true / minimum, 0.35) when clamped
    }

    public static class SkyMath
    {
        const double Deg = ThreadMath.Deg;

        // ------------------------------------------------------------------ directions and projection
        /// <summary>Unit direction for an azimuth (from +Z toward +X) and an elevation, both in degrees.</summary>
        public static D3 DirectionFromAzEl(double azimuthDeg, double elevationDeg)
        {
            double a = azimuthDeg * Deg, e = elevationDeg * Deg;
            return new D3(Math.Cos(e) * Math.Sin(a), Math.Sin(e), Math.Cos(e) * Math.Cos(a));
        }

        public static void AzElFromDirection(D3 d, out double azimuthDeg, out double elevationDeg)
        {
            azimuthDeg = Math.Atan2(d.x, d.z) / Deg;
            elevationDeg = Math.Atan2(d.y, Math.Sqrt(d.x * d.x + d.z * d.z)) / Deg;
        }

        /// <summary>Angle between two directions in degrees (atan2 form, accurate for small and large angles).</summary>
        public static double AngleBetweenDeg(D3 a, D3 b) => Math.Atan2(D3.Cross(a, b).Length, D3.Dot(a, b)) / Deg;

        /// <summary>Orthonormal camera basis for a world yaw and a pitch (no roll).</summary>
        public static void CameraBasis(double yawDeg, double pitchDeg, out D3 right, out D3 up, out D3 forward)
        {
            double y = yawDeg * Deg, p = pitchDeg * Deg;
            forward = new D3(Math.Cos(p) * Math.Sin(y), Math.Sin(p), Math.Cos(p) * Math.Cos(y));
            right = new D3(Math.Cos(y), 0.0, -Math.Sin(y));
            up = new D3(-Math.Sin(p) * Math.Sin(y), Math.Cos(p), -Math.Sin(p) * Math.Cos(y));
        }

        /// <summary>
        /// Rectilinear projection of a world direction (at infinity, so camPos does not enter: backdrop rule 14.7.4)
        /// to normalised screen coordinates. Returns false for directions behind the camera.
        /// </summary>
        public static bool ProjectDirection(D3 camPos, double worldYawDeg, double pitchDeg, double fovVDeg, double aspect, D3 worldDir, out double sx, out double sy)
        {
            CameraBasis(worldYawDeg, pitchDeg, out D3 r, out D3 u, out D3 f);
            double z = D3.Dot(worldDir, f);
            if (z <= 1e-12) { sx = double.NaN; sy = double.NaN; return false; }
            double ty = Math.Tan(0.5 * fovVDeg * Deg);
            sx = D3.Dot(worldDir, r) / z / (ty * aspect);
            sy = D3.Dot(worldDir, u) / z / ty;
            return true;
        }

        /// <summary>Projection of a world point seen from camPos.</summary>
        public static bool ProjectPoint(D3 camPos, double worldYawDeg, double pitchDeg, double fovVDeg, double aspect, D3 worldPoint, out double sx, out double sy)
            => ProjectDirection(camPos, worldYawDeg, pitchDeg, fovVDeg, aspect, worldPoint - camPos, out sx, out sy);

        /// <summary>Unit world ray through a normalised screen position.</summary>
        public static D3 ScreenToDirection(double worldYawDeg, double pitchDeg, double fovVDeg, double aspect, double sx, double sy)
        {
            CameraBasis(worldYawDeg, pitchDeg, out D3 r, out D3 u, out D3 f);
            double ty = Math.Tan(0.5 * fovVDeg * Deg);
            return (r * (sx * ty * aspect) + u * (sy * ty) + f).Normalized;
        }

        /// <summary>Fraction of the extent [min, max] that lies outside the frame [-1, 1] (0 = fully inside).</summary>
        public static double CropFraction(double min, double max)
        {
            double w = max - min;
            if (w <= 0) return 0;
            double outside = Math.Max(0.0, -1.0 - min) + Math.Max(0.0, max - 1.0);
            return Math.Min(1.0, outside / w);
        }

        // ------------------------------------------------------------------ far thread (14.7.3, 14.7.2)
        /// <summary>True half width of the far thread as a sine (halfE = R / r_app), as uploaded in _ThThread.z.</summary>
        public static double ThreadHalfE(double s) => ThreadConstants.CableRadius / ThreadMath.ThreadApparentRange(s);

        /// <summary>Minimum line width in pixels: max(1.5, 1.75 H / 1080) (14.7.2, _ThScreen.y).</summary>
        public static double MinLineWidthPx(double heightPx) => Math.Max(ThreadConstants.ThreadMinWidthFloorPx, ThreadConstants.ThreadMinWidthPx * heightPx / 1080.0);

        /// <summary>Width of the thread on the reference geometry of the 14.7.3 table (nominal path, yaw 0, pitch 0, FOV(s)).</summary>
        public static double ThreadReferenceWidthPx(double s, double heightPx)
            => ThreadMath.PixelWidth(ThreadMath.ThreadThetaRad(s), ThreadMath.ThreadBearingRad(s), ThreadMath.Fov(s), heightPx);

        /// <summary>World XZ of the apparent far-thread axis (x_T, z_T of 14.7.3, from the nominal s; _ThThread.xy).</summary>
        public static void ThreadApparentAxis(double s, out double xT, out double zT) => ThreadMath.ThreadAxis(s, out xT, out zT);

        /// <summary>World azimuth (degrees, from +Z toward +X) of the apparent axis seen from a camera position.</summary>
        public static double ThreadBearingFromCameraDeg(D3 camPos, double s)
        {
            ThreadMath.ThreadAxis(s, out double xT, out double zT);
            return Math.Atan2(xT - camPos.x, zT - camPos.z) / Deg;
        }

        /// <summary>
        /// Rectilinear pixel width (14.7.3 formula) of the far thread for a camera pose at pitch 0: bearing relative to
        /// the view yaw, angular width 2 asin(R / range) of the 50 m cylinder at the apparent axis.
        /// </summary>
        public static double ThreadRectilinearWidthPx(D3 camPos, double worldYawDeg, double fovVDeg, double heightPx, double s)
        {
            ThreadMath.ThreadAxis(s, out double xT, out double zT);
            double dx = xT - camPos.x, dz = zT - camPos.z;
            double range = Math.Sqrt(dx * dx + dz * dz);
            double theta = 2.0 * Math.Asin(Math.Min(1.0, ThreadConstants.CableRadius / Math.Max(range, ThreadConstants.CableRadius)));
            double beta = Math.Atan2(dx, dz) - worldYawDeg * Deg;
            return ThreadMath.PixelWidth(theta, beta, fovVDeg, heightPx);
        }

        /// <summary>
        /// The signed azimuth offset e of the sky shader: the sine of the azimuth difference between the ray and the
        /// direction from the camera to the apparent axis (x_T, z_T); front = the ray looks toward the axis.
        /// </summary>
        public static double ThreadAzimuthOffset(D3 camPos, double xT, double zT, D3 dir, out bool front)
        {
            double l = Math.Max(Math.Sqrt(dir.x * dir.x + dir.z * dir.z), 1e-6);
            double ax = dir.x / l, az = dir.z / l;
            double bx = xT - camPos.x, bz = zT - camPos.z;
            double lb = Math.Max(Math.Sqrt(bx * bx + bz * bz), 1e-3);
            bx /= lb; bz /= lb;
            front = ax * bx + az * bz >= 0.0;
            return ax * bz - az * bx;
        }

        static double ThreadE(D3 camPos, double xT, double zT, double yaw, double pitch, double fov, double aspect, double sx, double sy, out bool front)
            => ThreadAzimuthOffset(camPos, xT, zT, ScreenToDirection(yaw, pitch, fov, aspect, sx, sy), out front);

        /// <summary>
        /// Emulates ThSkyFarThread on one image row (rowNdc): finds the axis, measures |grad e| per pixel by central
        /// differences (the shader's ddx/ddy), and applies the minimum width and the energy rule.
        /// </summary>
        public static ThreadPixelProfile ThreadShaderProfile(D3 camPos, double worldYawDeg, double pitchDeg, double fovVDeg, double aspect, double heightPx, double s, double rowNdc)
        {
            var prof = new ThreadPixelProfile();
            ThreadMath.ThreadAxis(s, out double xT, out double zT);
            double halfE = ThreadHalfE(s);

            const int steps = 1000;
            const double x0 = -3.0, x1 = 3.0;
            double prevX = x0, prevE = ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, x0, rowNdc, out bool prevFront);
            double lo = double.NaN, hi = double.NaN;
            for (int i = 1; i <= steps; i++)
            {
                double x = x0 + (x1 - x0) * i / steps;
                double e = ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, x, rowNdc, out bool front);
                if (front && prevFront && (e == 0.0 || Math.Sign(e) != Math.Sign(prevE))) { lo = prevX; hi = x; break; }
                prevX = x; prevE = e; prevFront = front;
            }
            if (double.IsNaN(lo)) return prof;

            double eLo = ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, lo, rowNdc, out _);
            for (int it = 0; it < 80; it++)
            {
                double mid = 0.5 * (lo + hi);
                double eMid = ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, mid, rowNdc, out _);
                if (Math.Sign(eMid) == Math.Sign(eLo)) { lo = mid; eLo = eMid; } else hi = mid;
            }
            double xc = 0.5 * (lo + hi);

            double pxX = 2.0 / (heightPx * aspect), pxY = 2.0 / heightPx;   // one pixel in normalised units
            double ex = ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, xc + 0.5 * pxX, rowNdc, out _)
                      - ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, xc - 0.5 * pxX, rowNdc, out _);
            double ey = ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, xc, rowNdc + 0.5 * pxY, out _)
                      - ThreadE(camPos, xT, zT, worldYawDeg, pitchDeg, fovVDeg, aspect, xc, rowNdc - 0.5 * pxY, out _);
            double g = Math.Max(Math.Sqrt(ex * ex + ey * ey), 1e-15);
            double trueHalf = halfE / g;
            double minHalf = 0.5 * MinLineWidthPx(heightPx);

            prof.found = true;
            prof.centreX = xc;
            prof.trueWidthPx = 2.0 * trueHalf;
            prof.shownWidthPx = 2.0 * Math.Max(trueHalf, minHalf);
            prof.alpha = trueHalf < minHalf ? Math.Max(trueHalf / minHalf, ThreadConstants.ThreadAlphaFloor) : 1.0;
            return prof;
        }

        // ------------------------------------------------------------------ planet (14.5.7)
        /// <summary>Unit direction to the planet centre (identical to ProgressDirector's _ThPlanetDir).</summary>
        public static D3 PlanetDirection() => DirectionFromAzEl(ThreadConstants.PlanetAzimuthDeg, ThreadConstants.PlanetElevationDeg);

        /// <summary>The planet's disc frame as the shader builds it: x right = normalize(up x c), y up = c x right.</summary>
        public static void PlanetFrame(out D3 centre, out D3 right, out D3 up)
        {
            centre = PlanetDirection();
            right = D3.Cross(new D3(0, 1, 0), centre).Normalized;
            up = D3.Cross(centre, right);
        }

        /// <summary>Direction of the planet rim at the frame angle theta (radians, 0 = right, pi/2 = up).</summary>
        public static D3 PlanetRimDirection(double thetaRad)
        {
            PlanetFrame(out D3 c, out D3 r, out D3 u);
            double a = ThreadConstants.PlanetRadiusDeg * Deg;
            return c * Math.Cos(a) + (r * Math.Cos(thetaRad) + u * Math.Sin(thetaRad)) * Math.Sin(a);
        }

        /// <summary>Key light of the planet in world space (L_p of 14.5.7, disc frame x right, y up, z toward the viewer).</summary>
        public static D3 PlanetLightDirection()
        {
            PlanetFrame(out D3 c, out D3 r, out D3 u);
            return (r * SkyConstants.PlanetLightX + u * SkyConstants.PlanetLightY + c * (-SkyConstants.PlanetLightZ)).Normalized;
        }

        // ------------------------------------------------------------------ sun / planet impostors (A4, B4, C-16)
        /// <summary>
        /// Screen circle of a screen-space circular impostor as ThreadSky draws the sun and the planet: the centre is the
        /// rectilinear projection of the centre direction, the radius is tan(angular radius) on the image plane, i.e.
        /// f tan(r) pixels with f = (H / 2) / tan(FOV_v / 2), identical in x and y pixels (a circle at every aspect ratio).
        /// In normalised half-extents: ry = tan(r) / tan(FOV_v / 2), rx = ry / aspect. False when the centre is not in front
        /// (forward component at or below the shader's guard), where the shader draws nothing.
        /// </summary>
        public static bool DiscImpostorScreen(double worldYawDeg, double pitchDeg, double fovVDeg, double aspect, D3 centreDir, double radiusDeg,
                                              out double cx, out double cy, out double rx, out double ry)
        {
            CameraBasis(worldYawDeg, pitchDeg, out D3 r, out D3 u, out D3 f);
            D3 c = centreDir.Normalized;
            double z = D3.Dot(c, f);
            cx = cy = rx = ry = double.NaN;
            if (z <= SkyConstants.ImpostorMinForward) return false;
            double ty = Math.Tan(0.5 * fovVDeg * Deg);
            cx = D3.Dot(c, r) / z / (ty * aspect);
            cy = D3.Dot(c, u) / z / ty;
            ry = Math.Tan(radiusDeg * Deg) / ty;
            rx = ry / aspect;
            return true;
        }

        /// <summary>Impostor radius in pixels: f tan(r), f = (H / 2) / tan(FOV_v / 2) (the on-axis apparent radius).</summary>
        public static double ImpostorRadiusPx(double radiusDeg, double fovVDeg, double heightPx)
            => 0.5 * heightPx / Math.Tan(0.5 * fovVDeg * Deg) * Math.Tan(radiusDeg * Deg);

        /// <summary>Angular diameter measured from an impostor's pixel diameter with the on-axis rectilinear formula (acceptance 1 of 14.5.7).</summary>
        public static double ImpostorDiameterDeg(double diameterPx, double fovVDeg, double heightPx)
            => 2.0 * Math.Atan(0.5 * diameterPx / (0.5 * heightPx / Math.Tan(0.5 * fovVDeg * Deg))) / Deg;

        /// <summary>Screen bounding box of the planet impostor for a camera pose (the circle of DiscImpostorScreen; false when behind).</summary>
        public static bool PlanetScreenBox(double worldYawDeg, double pitchDeg, double fovVDeg, double aspect,
                                           out double xMin, out double xMax, out double yMin, out double yMax)
        {
            bool ok = DiscImpostorScreen(worldYawDeg, pitchDeg, fovVDeg, aspect, PlanetDirection(), ThreadConstants.PlanetRadiusDeg,
                                         out double cx, out double cy, out double rx, out double ry);
            xMin = cx - rx; xMax = cx + rx; yMin = cy - ry; yMax = cy + ry;
            return ok;
        }

        /// <summary>Screen bounding box of the sun impostor at progress s (0.9 deg disc).</summary>
        public static bool SunScreenBox(double s, double worldYawDeg, double pitchDeg, double fovVDeg, double aspect,
                                        out double xMin, out double xMax, out double yMin, out double yMax)
        {
            bool ok = DiscImpostorScreen(worldYawDeg, pitchDeg, fovVDeg, aspect, ThreadMath.SunDirection(s), ThreadConstants.SunDiscRadiusDeg,
                                         out double cx, out double cy, out double rx, out double ry);
            xMin = cx - rx; xMax = cx + rx; yMin = cy - ry; yMax = cy + ry;
            return ok;
        }

        /// <summary>
        /// Disc-frame coordinates q of a world direction seen inside the planet impostor (C# mirror of the shader): image-plane
        /// offset from the projected centre, in units of the impostor radius, along x = right and y = the projected world
        /// vertical through the centre. |q| = 1 on the rim; the shading normal is (q, sqrt(1 - |q|^2)) (sphere seen head-on).
        /// </summary>
        public static bool PlanetDiscOffset(double worldYawDeg, double pitchDeg, D3 dir, out double qx, out double qy)
        {
            CameraBasis(worldYawDeg, pitchDeg, out D3 r, out D3 u, out D3 f);
            qx = qy = double.NaN;
            D3 c = PlanetDirection();
            double cz = D3.Dot(c, f), dz = D3.Dot(dir, f);
            if (cz <= SkyConstants.ImpostorMinForward || dz <= 1e-9) return false;
            double cxp = D3.Dot(c, r) / cz, cyp = D3.Dot(c, u) / cz;           // image plane, units of the forward distance
            double ox = D3.Dot(dir, r) / dz - cxp, oy = D3.Dot(dir, u) / dz - cyp;
            // image-plane direction of the world vertical through the centre: d/de of (c + e up) projected
            var up = new D3(0, 1, 0);
            double uz = D3.Dot(up, f);
            double eyx = D3.Dot(up, r) * cz - D3.Dot(c, r) * uz, eyy = D3.Dot(up, u) * cz - D3.Dot(c, u) * uz;
            double l = Math.Sqrt(eyx * eyx + eyy * eyy);
            if (l < 1e-12) { eyx = 0; eyy = 1; } else { eyx /= l; eyy /= l; }
            double exx = eyy, exy = -eyx;
            double t = Math.Tan(ThreadConstants.PlanetRadiusDeg * Deg);
            qx = (ox * exx + oy * exy) / t;
            qy = (ox * eyx + oy * eyy) / t;
            return true;
        }

        // ------------------------------------------------------------------ Earth, cloud shell, limb (14.7.5, 14.7.7)
        /// <summary>Horizon dip delta(A) = acos(R / (R + A)) in degrees (atan form, exact for small A).</summary>
        public static double HorizonDipDeg(double altitudeM)
        {
            double R = ThreadConstants.EarthRadius, A = Math.Max(0.0, altitudeM);
            return Math.Atan2(Math.Sqrt(A * (2.0 * R + A)), R) / Deg;
        }

        /// <summary>Elevation of the visible Earth / sea edge (the R-based limb the shader draws): -dip.</summary>
        public static double EarthLimbElevationDeg(double altitudeM) => -HorizonDipDeg(altitudeM);

        /// <summary>Dip of the cloud-shell limb (radius R + 3 km, seen from h = A - 3 km); 0 while h <= 0.</summary>
        public static double ShellLimbDipDeg(double altitudeM)
        {
            double h = altitudeM - ThreadConstants.CloudTop;
            if (h <= 0) return 0.0;
            double Rc = ThreadConstants.EarthRadius + ThreadConstants.CloudTop;
            return Math.Atan2(Math.Sqrt(h * (2.0 * Rc + h)), Rc) / Deg;
        }

        /// <summary>cos(psi_limb) of the cloud shell, the CPU double value uploaded in _ThEarth.x.</summary>
        public static double ShellCosLimb(double altitudeM)
        {
            double h = altitudeM - ThreadConstants.CloudTop;
            if (h <= 0) return 0.0;
            double Rc = ThreadConstants.EarthRadius + ThreadConstants.CloudTop;
            double q = Rc / (Rc + h);
            return Math.Sqrt(Math.Max(0.0, 1.0 - q * q));
        }

        /// <summary>Angular scale of the limb glow eps0 = 25 km / sqrt(A (2R + A)) in radians (_ThEarth.w).</summary>
        public static double LimbGlowEps0Rad(double altitudeM)
        {
            double R = ThreadConstants.EarthRadius, A = Math.Max(1.0, altitudeM);
            return SkyConstants.LimbGlowLayer / Math.Sqrt(A * (2.0 * R + A));
        }

        /// <summary>Screen position of the Earth limb at a world azimuth for a camera pose.</summary>
        public static bool EarthLimbScreen(double worldYawDeg, double pitchDeg, double fovVDeg, double aspect, double altitudeM, double azimuthDeg, out double sx, out double sy)
            => ProjectDirection(new D3(0, 0, 0), worldYawDeg, pitchDeg, fovVDeg, aspect, DirectionFromAzEl(azimuthDeg, EarthLimbElevationDeg(altitudeM)), out sx, out sy);

        /// <summary>
        /// Ray / cloud-shell intersection in the stable angular form of the shader (double): h above the shell,
        /// cosPsi = cos of the angle to the nadir (-d.y), sinPsi = |d.xz|. Returns the slant distance t and the
        /// central angle gamma; hit = false (and the limb values) when the ray misses.
        /// </summary>
        public static bool ShellHit(double h, double cosPsi, double sinPsi, out double t, out double gamma)
        {
            double Rc = ThreadConstants.EarthRadius + ThreadConstants.CloudTop;
            double rho = Rc + h;
            double c = h * (2.0 * Rc + h);
            double muH = Math.Sqrt(c) / rho;
            double mc = Math.Max(cosPsi, muH);
            double root = Math.Sqrt(Math.Max((mc - muH) * (mc + muH), 0.0));
            t = (c / rho) / (mc + root);
            gamma = Math.Atan2(t * sinPsi, rho - t * mc);
            return cosPsi >= muH;
        }

        /// <summary>
        /// Float32 emulation of exactly the shader's expression order (every intermediate rounded to float), with muH
        /// supplied by the CPU in double and cast, as ProgressDirector does. For the precision test (Part 8.6).
        /// </summary>
        public static void ShellHitFloat(float h, float muH, float cosPsi, float sinPsi, out float t, out float gamma)
        {
            float Rc = (float)(ThreadConstants.EarthRadius + ThreadConstants.CloudTop);
            float rho = (float)(Rc + h);
            float mc = Math.Max(cosPsi, muH);
            float a = (float)(mc - muH), b = (float)(mc + muH);
            float root = (float)Math.Sqrt(Math.Max((float)(a * b), 0f));
            float twoRcH = (float)((float)(2f * Rc) + h);
            float cOverRho = (float)((float)(h * twoRcH) / rho);
            t = (float)(cOverRho / (float)(mc + root));
            float baseC = (float)(rho - (float)(t * mc));
            gamma = (float)Math.Atan2((float)(t * sinPsi), baseC);
        }

        // ------------------------------------------------------------------ cable shadow band on the sea (14.7.7; A3, B5, C-01, E3, PAC-10)
        /// <summary>World azimuth (deg) of the band axis: opposite the sun azimuth (145 deg).</summary>
        public static double ShadowBandAzimuthDeg() => ThreadConstants.SunAzimuthDeg + 180.0;

        /// <summary>Height of the camera above the cloud tops at progress s (h = A_virt - 3 km, as _ThEarth.y).</summary>
        public static double HeightAboveCloudTops(double s) => ThreadMath.VirtualAltitude(s) - ThreadConstants.CloudTop;

        /// <summary>Half width (m) of the band at along-distance t for the height h above the tops (shader ThSkyBandGeom).</summary>
        public static double ShadowBandHalfWidth(double t, double h)
        {
            double tp = Math.Max(t, 0.0);
            return ThreadConstants.ShadowBandHalfWidth * (1.0 - SkyConstants.ShadowBandNarrowing * ThreadMath.Smoothstep(0.0, SkyConstants.ShadowBandNarrowingT, tp))
                 + ThreadConstants.ShadowBandPenumbra * tp + SkyConstants.ShadowBandAltWiden * Math.Max(h, 0.0);
        }

        /// <summary>Opacity of the band at along-distance t (no pixel-footprint term), fade over [max(12 km, 4 h), max(30 km, 16 h)].</summary>
        public static double ShadowBandOpacity(double t, double h)
        {
            double f0 = Math.Max(ThreadConstants.ShadowBandFade0, SkyConstants.ShadowBandFadeAlt0 * h);
            double f1 = Math.Max(ThreadConstants.ShadowBandFade1, SkyConstants.ShadowBandFadeAlt1 * h);
            return SkyConstants.ShadowBandOpacityF1 * (1.0 - ThreadMath.Smoothstep(f0, f1, t)) * ThreadMath.Smoothstep(-SkyConstants.ShadowBandStart, SkyConstants.ShadowBandStart, t);
        }

        /// <summary>Band tone (linear): lerp(CLOUD_SHADE x TC_ShadowBandColourGain, SKY_M_DUSK, 0.5 smoothstep(0.5, 1, D)).</summary>
        public static FogMath.Rgb ShadowBandColour(double D)
        {
            var shade = PalRgb("CLOUD_SHADE");
            var dusk = PalRgb("SKY_M_DUSK");
            double g = ThreadConstants.ShadowBandColourGain;
            return FogMath.Rgb.Lerp(new FogMath.Rgb(shade.r * g, shade.g * g, shade.b * g), dusk, SkyConstants.ShadowBandDuskMix * ThreadMath.Smoothstep(0.5, 1.0, D));
        }

        /// <summary>Per-channel multiplier of the band on the sea (wave 3): ShadowBandColour(D) / CLOUD_TOP. The shader multiplies
        /// the sea by lerp(1, tint, coverage x opacity), so over lit cloud the band is exactly ShadowBandColour(D).</summary>
        public static FogMath.Rgb ShadowBandTint(double D)
        {
            var c = ShadowBandColour(D);
            var top = PalRgb("CLOUD_TOP");
            return new FogMath.Rgb(c.r / top.r, c.g / top.g, c.b / top.b);
        }

        /// <summary>Sea haze from the limb band alone (wave 3, E16): 1 down to LimbHazeFullDeg below the R-limb, then a smoothstep
        /// to 0 over LimbHazeRampDeg. The shader combines it with the path haze as 1 - (1 - path) (1 - band).</summary>
        public static double SeaLimbHaze(double degBelowLimb)
            => 1.0 - ThreadMath.Smoothstep(SkyConstants.LimbHazeFullDeg, SkyConstants.LimbHazeFullDeg + SkyConstants.LimbHazeRampDeg, degBelowLimb);

        /// <summary>Star visibility factor from the linear luminance of the sky behind (wave 3, B-r2-3).</summary>
        public static double StarSkyFade(double skyLuminance)
            => 1.0 - ThreadMath.Smoothstep(SkyConstants.StarSkyLuminance0, SkyConstants.StarSkyLuminance1, skyLuminance);

        /// <summary>Linear palette colour as FogMath.Rgb (double, from the hex table; token = the Pal name, e.g. "CLOUD_SHADE").</summary>
        public static FogMath.Rgb PalRgb(string token)
        {
            string hex = ThreadPaletteMath.Hex(token);
            return new FogMath.Rgb(ThreadPaletteMath.SrgbHexChannelToLinear(hex, 0), ThreadPaletteMath.SrgbHexChannelToLinear(hex, 1), ThreadPaletteMath.SrgbHexChannelToLinear(hex, 2));
        }

        /// <summary>Rec. 709 relative luminance of a linear colour.</summary>
        public static double Luminance(FogMath.Rgb c) => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;

        /// <summary>
        /// Direction from the camera to the band axis point at along-distance t (m) from the cable axis on the cloud-top
        /// shell, with the surface mapped as the shader maps it (azimuthal equidistant around the camera: ground arc g at the
        /// point's azimuth, central angle g / Rc). altitudeM = A_virt; slant = distance to the point. False below the tops.
        /// </summary>
        public static bool ShadowBandAxisDirection(D3 camPos, double altitudeM, double t, out D3 dir, out double slant)
        {
            dir = new D3(0, -1, 0); slant = 0;
            double h = altitudeM - ThreadConstants.CloudTop;
            if (h <= 0) return false;
            double az = ShadowBandAzimuthDeg() * Deg;
            double px = ThreadConstants.CableAxisX + Math.Sin(az) * t, pz = ThreadConstants.CableAxisZ + Math.Cos(az) * t;
            double dx = px - camPos.x, dz = pz - camPos.z;
            double g = Math.Sqrt(dx * dx + dz * dz);
            double Rc = ThreadConstants.EarthRadius + ThreadConstants.CloudTop;
            double gam = g / Rc;
            double horiz = Rc * Math.Sin(gam), drop = h + Rc * (1.0 - Math.Cos(gam));
            var v = g > 1e-9 ? new D3(dx / g * horiz, -drop, dz / g * horiz) : new D3(0, -drop, 0);
            slant = v.Length;
            dir = v * (1.0 / slant);
            return true;
        }

        /// <summary>Lateral width (px) of the band at along-distance t for a camera (2 w(t) / slant x f).</summary>
        public static double ShadowBandWidthPx(D3 camPos, double altitudeM, double t, double fovVDeg, double heightPx)
        {
            if (!ShadowBandAxisDirection(camPos, altitudeM, t, out _, out double slant)) return 0;
            double f = 0.5 * heightPx / Math.Tan(0.5 * fovVDeg * Deg);
            return 2.0 * ShadowBandHalfWidth(t, altitudeM - ThreadConstants.CloudTop) / slant * f;
        }

        // ------------------------------------------------------------------ dusk sky (C-15)
        /// <summary>Darkness of the held horizon (ThSkyHoldD): D for D &lt;= 0.5, then 0.5 until D = 0.85, the night stop at D = 1.</summary>
        public static double SkyHoldDarkness(double D)
        {
            double dh = ThreadMath.Lerp(SkyConstants.HoldOn, 1.0, ThreadMath.Smoothstep(SkyConstants.HoldD0, SkyConstants.HoldD1, D));
            return D > SkyConstants.HoldOn ? Math.Min(dh, D) : D;
        }

        static void ToOklab(FogMath.Rgb c, out double L, out double a, out double b)
        {
            double l = Math.Pow(Math.Max(0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b, 0.0), 1.0 / 3.0);
            double m = Math.Pow(Math.Max(0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b, 0.0), 1.0 / 3.0);
            double s = Math.Pow(Math.Max(0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b, 0.0), 1.0 / 3.0);
            L = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
            a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
            b = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        }

        static FogMath.Rgb FromOklab(double L, double a, double b)
        {
            double l = L + 0.3963377774 * a + 0.2158037573 * b, m = L - 0.1055613458 * a - 0.0638541728 * b, s = L - 0.0894841775 * a - 1.2914855480 * b;
            l = l * l * l; m = m * m * m; s = s * s * s;
            return new FogMath.Rgb(Math.Max(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, 0.0),
                                   Math.Max(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, 0.0),
                                   Math.Max(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s, 0.0));
        }

        /// <summary>
        /// Sky colour of the sky shader (ThSkyGradient): FogMath.SkyColor with the darkness blended from the held horizon to D
        /// at the 25 deg stop, and the OKLab chroma of the H -> M segment restored (C-15). Equal to FogMath.SkyColor for D &lt;= 0.5.
        /// </summary>
        public static FogMath.Rgb SkyGradient(double dirY, double D, out FogMath.Rgb horizon)
        {
            double dh = SkyHoldDarkness(D);
            double tM = Math.Sqrt(Math.Sin(ThreadConstants.SkyStopM * Deg));
            double t = Math.Sqrt(ThreadMath.Clamp01(dirY));
            double de = ThreadMath.Lerp(dh, D, ThreadMath.Smoothstep(0.0, tM, t));
            var c = FogMath.SkyColor(dirY, de);
            horizon = FogMath.SkyColor(0.0, dh);
            double k = SkyConstants.DuskChroma * ThreadMath.Smoothstep(SkyConstants.DuskChromaD0, SkyConstants.DuskChromaD1, D);
            if (k > 0.0 && t < tM)
            {
                FogMath.SkyStops(de, out var h, out var m, out _);
                double dr = m.r - h.r, dg = m.g - h.g, db = m.b - h.b;
                double u = ThreadMath.Clamp01(((c.r - h.r) * dr + (c.g - h.g) * dg + (c.b - h.b) * db) / Math.Max(dr * dr + dg * dg + db * db, 1e-8));
                ToOklab(c, out double L, out double a, out double b);
                ToOklab(h, out _, out double ha, out double hb);
                ToOklab(m, out _, out double ma, out double mb);
                double C = Math.Sqrt(a * a + b * b);
                double Ct = ThreadMath.Lerp(Math.Sqrt(ha * ha + hb * hb), Math.Sqrt(ma * ma + mb * mb), u);
                double Cn = Math.Max(C, ThreadMath.Lerp(C, Ct, k));
                double sc = Math.Min(Cn / Math.Max(C, 1e-5), SkyConstants.DuskChromaMaxGain);
                c = FromOklab(L, a * sc, b * sc);
            }
            return c;
        }

        /// <summary>sRGB transfer of a linear channel (clamped to [0, 1]).</summary>
        public static double LinearToSrgb(double c)
        {
            c = ThreadMath.Clamp01(c);
            return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
        }

        /// <summary>HSV saturation of a linear colour after the sRGB transfer (C-15 measure).</summary>
        public static double SrgbSaturation(FogMath.Rgb c)
        {
            double r = LinearToSrgb(c.r), g = LinearToSrgb(c.g), b = LinearToSrgb(c.b);
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            return mx > 1e-9 ? (mx - mn) / mx : 0.0;
        }

        // ------------------------------------------------------------------ stars (14.5.7)
        /// <summary>Direction of the glint star (azimuth 0, elevation 20 degrees).</summary>
        public static D3 GlintStarDirection() => DirectionFromAzEl(ThreadConstants.GlintAzimuthDeg, ThreadConstants.GlintElevationDeg);
    }
}
