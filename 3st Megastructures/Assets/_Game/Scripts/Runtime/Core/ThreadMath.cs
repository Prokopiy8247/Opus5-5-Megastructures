// ThreadMath: pure, double-precision functions of the plan distance s (MASTER_PROMPT 14.4 - 14.7).
// The single source of truth for the path, the corridor, the FOV curve, the thread law, the ascent
// curves and the per-beat parameter blends. No UnityEngine dependency (EditMode tests and the
// offline audio renderer call it directly).
using System;

namespace Megastructure
{
    public struct D3
    {
        public double x, y, z;
        public D3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public static D3 operator +(D3 a, D3 b) => new D3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static D3 operator *(D3 a, double k) => new D3(a.x * k, a.y * k, a.z * k);
        public double Length => Math.Sqrt(x * x + y * y + z * z);
        public D3 Normalized { get { double l = Length; return l > 1e-12 ? this * (1.0 / l) : new D3(0, 0, 0); } }
        public static double Dot(D3 a, D3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static D3 Cross(D3 a, D3 b) => new D3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public static class ThreadMath
    {
        public const double Deg = Math.PI / 180.0;

        // ------------------------------------------------------------------ easing
        public static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double Smoothstep(double e0, double e1, double x)
        {
            double t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }
        public static double Smootherstep01(double x)
        {
            double t = Clamp01(x);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
        public static double Smootherstep(double e0, double e1, double x) => Smootherstep01((x - e0) / (e1 - e0));

        // ------------------------------------------------------------------ beats
        public static int BeatIndex(double s)
        {
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 1; i < b.Length - 1; i++) if (s < b[i]) return i - 1;
            return ThreadConstants.BeatCount - 1;
        }

        /// <summary>Per-beat table value with smootherstep blends of 24 m centred on each boundary (14.6).</summary>
        public static double BeatBlend(double[] perBeat, double s, double window = ThreadConstants.BlendWindow)
        {
            var b = ThreadConstants.BeatBoundaries;
            double v = perBeat[0];
            for (int i = 1; i < ThreadConstants.BeatCount; i++)
                v += (perBeat[i] - perBeat[i - 1]) * Smootherstep(b[i] - window * 0.5, b[i] + window * 0.5, s);
            return v;
        }

        public static D3 BeatBlend3(double[,] perBeat, double s, double window = ThreadConstants.BlendWindow)
        {
            var b = ThreadConstants.BeatBoundaries;
            var v = new D3(perBeat[0, 0], perBeat[0, 1], perBeat[0, 2]);
            for (int i = 1; i < ThreadConstants.BeatCount; i++)
            {
                double t = Smootherstep(b[i] - window * 0.5, b[i] + window * 0.5, s);
                v += new D3(perBeat[i, 0] - perBeat[i - 1, 0], perBeat[i, 1] - perBeat[i - 1, 1], perBeat[i, 2] - perBeat[i - 1, 2]) * t;
            }
            return v;
        }

        // ------------------------------------------------------------------ path (14.4.2)
        /// <summary>Path point (deck top / floor surface) at plan distance s.</summary>
        public static D3 PathPoint(double s)
        {
            s = Math.Max(0, Math.Min(ThreadConstants.PathLength, s));
            if (s <= ThreadConstants.HelixStartS)
                return new D3(0, PathY(s), s);
            if (s <= ThreadConstants.HelixEndS)
            {
                double psi = (s - ThreadConstants.HelixStartS) / ThreadConstants.HelixRadius;
                double r = ThreadConstants.HelixRadius;
                return new D3(ThreadConstants.HelixCentreX - r * Math.Cos(psi), PathY(s), ThreadConstants.HelixCentreZ + r * Math.Sin(psi));
            }
            return new D3(0, ThreadConstants.TopY, ThreadConstants.PierStartZ + (s - ThreadConstants.HelixEndS));
        }

        /// <summary>Floor height of the path (analytic; stairs are a smooth ramp, 14.4.3).</summary>
        public static double PathY(double s)
        {
            if (s <= ThreadConstants.StairStartZ) return 0.0;
            if (s <= ThreadConstants.StairEndZ) return ThreadConstants.StairSlope * (s - ThreadConstants.StairStartZ);
            if (s <= ThreadConstants.GradeBlendStart) return ThreadConstants.StairRise;
            if (s <= ThreadConstants.GradeBlendEnd)
            {
                // integral of 0.25 * smoothstep(0, 8, s - 396): 2 (u^3 - u^4 / 2), u = (s - 396) / 8
                double u = (s - ThreadConstants.GradeBlendStart) / (ThreadConstants.GradeBlendEnd - ThreadConstants.GradeBlendStart);
                return ThreadConstants.HelixBaseY + 2.0 * (u * u * u - 0.5 * u * u * u * u);
            }
            if (s <= ThreadConstants.HelixEndS) return ThreadConstants.HelixBaseY + ThreadConstants.HelixGrade * (s - ThreadConstants.HelixStartS);
            return ThreadConstants.TopY;
        }

        /// <summary>Analytic slope dy/ds of the floor.</summary>
        public static double PathGrade(double s)
        {
            if (s < ThreadConstants.StairStartZ) return 0;
            if (s < ThreadConstants.StairEndZ) return ThreadConstants.StairSlope;
            if (s < ThreadConstants.GradeBlendStart) return 0;
            if (s < ThreadConstants.GradeBlendEnd) return ThreadConstants.HelixGrade * Smoothstep(ThreadConstants.GradeBlendStart, ThreadConstants.GradeBlendEnd, s);
            if (s < ThreadConstants.HelixEndS) return ThreadConstants.HelixGrade;
            return 0;
        }

        /// <summary>Heading (yaw from +Z toward +X, radians) of the path tangent.</summary>
        public static double Heading(double s)
        {
            if (s <= ThreadConstants.HelixStartS) return 0;
            if (s <= ThreadConstants.HelixEndS) return (s - ThreadConstants.HelixStartS) / ThreadConstants.HelixRadius;
            return 2 * Math.PI;
        }

        /// <summary>Unit plan tangent (x, z) of the path at s.</summary>
        public static void Tangent(double s, out double tx, out double tz)
        {
            double h = Heading(s);
            tx = Math.Sin(h); tz = Math.Cos(h);
        }

        /// <summary>Unit plan right vector (x, z): tangent rotated +90 degrees (toward +X at s = 0, toward the cable on the helix).</summary>
        public static void Right(double s, out double rx, out double rz)
        {
            double h = Heading(s);
            rx = Math.Cos(h); rz = -Math.Sin(h);
        }

        /// <summary>World position of a corridor coordinate (s, lateral offset to the right, height above floor).</summary>
        public static D3 CorridorToWorld(double s, double lateral, double height)
        {
            var p = PathPoint(s);
            Right(s, out double rx, out double rz);
            return new D3(p.x + rx * lateral, p.y + height, p.z + rz * lateral);
        }

        /// <summary>Plan-distance factor ds / (forward metres) at a lateral offset (helix radius changes with offset).</summary>
        public static double ForwardToS(double s, double lateral)
        {
            if (s > ThreadConstants.HelixStartS && s < ThreadConstants.HelixEndS)
                return ThreadConstants.HelixRadius / Math.Max(1.0, ThreadConstants.HelixRadius - lateral);
            return 1.0;
        }

        /// <summary>Half-width of the stair walk between the walk-line bollards (D-053).</summary>
        public const double StairWalkHalfWidth = 4.0;

        /// <summary>Walkable half-width (already minus the player radius, 14.4.3). D-053: the stair corridor narrows to
        /// 4 m between the walk-line bollards (14.5.3 says 10 m) over s 230-255 and widens back to 10 m on the terrace
        /// over s 322-342; every transition keeps the 0.6 m/m slope limit.</summary>
        public static double HalfWidth(double s)
        {
            if (s < 80) return 12.0;
            if (s < 100) return Lerp(12.0, 14.0, Smoothstep(80, 100, s));
            if (s < 230) return 14.0;
            if (s < 255) return Lerp(14.0, StairWalkHalfWidth, Smoothstep(230, 255, s));
            if (s < 322) return StairWalkHalfWidth;
            if (s < 342) return Lerp(StairWalkHalfWidth, 10.0, Smoothstep(322, 342, s));
            if (s < 350) return 10.0;
            if (s < 400) return Lerp(10.0, ThreadConstants.HalfWidthDeck, Smoothstep(350, 400, s));
            return ThreadConstants.HalfWidthDeck;
        }

        /// <summary>Nearest plan distance on the path for a world position, searching near a hint (continuity).</summary>
        public static double ProjectToPath(D3 pos, double hint)
        {
            double best = hint, bestD = double.MaxValue;
            double lo = Math.Max(0, hint - 30), hi = Math.Min(ThreadConstants.PathLength, hint + 30);
            for (double s = lo; s <= hi; s += 0.5)
            {
                var p = PathPoint(s);
                double dx = p.x - pos.x, dz = p.z - pos.z, dy = (p.y - pos.y) * 0.25;
                double d = dx * dx + dz * dz + dy * dy;
                if (d < bestD) { bestD = d; best = s; }
            }
            double step = 0.25;
            for (int it = 0; it < 24; it++)
            {
                double a = Math.Max(0, best - step), b = Math.Min(ThreadConstants.PathLength, best + step);
                double da = Dist2(pos, a), db = Dist2(pos, b), dm = Dist2(pos, best);
                if (da < dm) best = a; else if (db < dm) best = b; else step *= 0.5;
            }
            return best;
        }

        static double Dist2(D3 pos, double s)
        {
            var p = PathPoint(s);
            double dx = p.x - pos.x, dz = p.z - pos.z, dy = (p.y - pos.y) * 0.25;
            return dx * dx + dz * dz + dy * dy;
        }

        // ------------------------------------------------------------------ FOV(s) (14.12)
        public static double Fov(double s)
        {
            double f = 62.0;
            f += 2.0 * Smoothstep(78, 102, s);
            f += 4.0 * Smoothstep(300, 332, s);
            f -= 4.0 * Smoothstep(340, 400, s);
            f += 2.0 * Smoothstep(560, 600, s);
            f -= 2.0 * Smoothstep(740, 768, s);
            f += 2.0 * Smoothstep(820, 858, s);
            return f;
        }

        // ------------------------------------------------------------------ thread law (14.7.3)
        public static double ThreadThetaRad(double s)
        {
            double sc = Math.Min(s, ThreadConstants.SwapS);
            return ThreadConstants.ThreadTheta0Deg * Deg * Math.Pow(1.0 - sc / ThreadConstants.ThreadS, -ThreadConstants.ThreadQ);
        }
        public static double ThreadApparentRange(double s) => 2.0 * ThreadConstants.CableRadius / ThreadThetaRad(s);
        /// <summary>Apparent bearing of the far thread: the true bearing of the cable axis minus an offset that keeps the line
        /// inside the avenue gap in beat 1 (the right-hand robes and sleeves hide bearing 9.9 deg from s = 0) and eases to 0
        /// inside the bank veil, so the swap at s = 285 is exact (decision D-026).</summary>
        public static double ThreadBearingRad(double s)
        {
            double sc = Math.Min(s, ThreadConstants.SwapS);
            double off = ThreadConstants.ThreadBearingOffsetDeg * Deg * (1.0 - Smoothstep(ThreadConstants.ThreadBearingOffsetS0, ThreadConstants.ThreadBearingOffsetS1, sc));
            return Math.Atan2(ThreadConstants.CableAxisX, ThreadConstants.CableAxisZ - sc) - off;
        }
        /// <summary>World XZ of the apparent far-thread axis, computed from the nominal s.</summary>
        public static void ThreadAxis(double s, out double x, out double z)
        {
            double r = ThreadApparentRange(s), b = ThreadBearingRad(s);
            x = r * Math.Sin(b);
            z = Math.Min(s, ThreadConstants.SwapS) + r * Math.Cos(b);
        }

        /// <summary>Rectilinear pixel width of a vertical line of angular width theta at bearing beta (14.7.3).</summary>
        public static double PixelWidth(double thetaRad, double betaRad, double fovVDeg, double heightPx)
        {
            double f = heightPx * 0.5 / Math.Tan(fovVDeg * 0.5 * Deg);
            return f * (Math.Tan(betaRad + thetaRad * 0.5) - Math.Tan(betaRad - thetaRad * 0.5));
        }

        // ------------------------------------------------------------------ ascent (14.7.5)
        public static double AscentW(double s) => Clamp01((s - ThreadConstants.HelixStartS) / (ThreadConstants.HelixEndS - ThreadConstants.HelixStartS));
        public static double AscentE(double w) => 3 * w * w - 2 * w * w * w;
        /// <summary>Virtual altitude in metres (double).</summary>
        public static double VirtualAltitude(double s)
        {
            double e = AscentE(AscentW(s));
            return ThreadConstants.AscentMaxAltitude * (Math.Exp(ThreadConstants.AscentExp * e) - 1.0) / (Math.Exp(ThreadConstants.AscentExp) - 1.0);
        }
        public static double Darkness(double s) => Smootherstep01((AscentW(s) - 0.03) / 0.75);
        public static double StarAlpha(double s) => Smoothstep(0.35, 0.85, Darkness(s));
        /// <summary>Far-thread opacity: 1 until the bank core, 0 at the swap (D-049).</summary>
        public static double FarThreadAlpha(double s) => s >= ThreadConstants.SwapS ? 0.0 : 1.0 - Smoothstep(ThreadConstants.ThreadSwapFadeS0, ThreadConstants.SwapS, s);
        public static double PlanetAlpha(double s) => Smoothstep(ThreadConstants.PlanetAlphaA0, ThreadConstants.PlanetAlphaA1, VirtualAltitude(s) / 1000.0);
        public static double AirDensity(double altitudeM) => Math.Exp(-altitudeM / ThreadConstants.ScaleHeight);
        public static double HorizonDipRad(double altitudeM)
        {
            double R = ThreadConstants.EarthRadius;
            return Math.Acos(R / (R + Math.Max(0, altitudeM)));
        }
        /// <summary>cos of the angle between the nadir and the limb direction (CPU double, 14.7.5).</summary>
        public static double CosPsiLimb(double altitudeM)
        {
            double R = ThreadConstants.EarthRadius, k = R / (R + Math.Max(0, altitudeM));
            return Math.Sqrt(Math.Max(0, 1 - k * k));
        }
        public static double SunAlpha(double s) => 1.0 - Smoothstep(0.55, 0.85, Darkness(s));

        // ------------------------------------------------------------------ sun and light (14.2.4)
        public static double SunElevationDeg(double s)
        {
            if (s < ThreadConstants.HelixStartS)
                return BeatBlend(ThreadConstants.SunElevationDeg, s);
            double D = Darkness(s);
            double e = D <= 0.51 ? Lerp(22, 16, D / 0.51) : Lerp(16, 12, (D - 0.51) / 0.49);
            return e;
        }

        /// <summary>Unit vector toward the sun: (cos e sin a, sin e, cos e cos a).</summary>
        public static D3 SunDirection(double s)
        {
            double e = SunElevationDeg(s) * Deg, a = ThreadConstants.SunAzimuthDeg * Deg;
            return new D3(Math.Cos(e) * Math.Sin(a), Math.Sin(e), Math.Cos(e) * Math.Cos(a));
        }

        /// <summary>Sun colour blend weights: 0 = 5600 K, 1 = 6200 K, 2 = 6500 K (returns a position 0..2).</summary>
        public static double SunColourPosition(double s)
        {
            if (s < ThreadConstants.HelixStartS) return 0;
            double D = Darkness(s);
            return D <= 0.51 ? D / 0.51 : 1.0 + (D - 0.51) / 0.49;
        }

        public static double SunIntensity(double s)
        {
            // beats 1-4 table; beat 5 ramps 1.00 -> 1.05 with D; beat 6 1.05; beat 7 1.10 (blended at 750)
            if (s < ThreadConstants.HelixStartS) return BeatBlend(ThreadConstants.SunIntensity, s);
            double D = Darkness(s);
            double v = Lerp(1.00, 1.05, Clamp01(D / 0.51));
            v += (1.10 - 1.05) * Smootherstep(750 - 12, 750 + 12, s);
            return v;
        }

        public static void Ambient(double s, out D3 up, out D3 side, out D3 down)
        {
            up = BeatBlend3(ThreadConstants.AmbientUp, s);
            side = BeatBlend3(ThreadConstants.AmbientSide, s);
            down = BeatBlend3(ThreadConstants.AmbientDown, s);
            if (s > ThreadConstants.HelixStartS && s < 580 + 12)
            {
                // beat 5 blends toward the beat 6 values with D (instead of the boundary window)
                double t = Clamp01(Darkness(s) / 0.51);
                D3 u1 = Row(ThreadConstants.AmbientUp, 4), u6 = Row(ThreadConstants.AmbientUp, 5);
                D3 s1 = Row(ThreadConstants.AmbientSide, 4), s6 = Row(ThreadConstants.AmbientSide, 5);
                D3 d1 = Row(ThreadConstants.AmbientDown, 4), d6 = Row(ThreadConstants.AmbientDown, 5);
                up = u1 + (u6 - u1) * t; side = s1 + (s6 - s1) * t; down = d1 + (d6 - d1) * t;
            }
        }
        static D3 Row(double[,] a, int i) => new D3(a[i, 0], a[i, 1], a[i, 2]);

        // ------------------------------------------------------------------ fog and post (14.2.5, 14.6)
        /// <summary>Distance-fog coefficient k = 3.912 / V, blended per beat in k-space; 0 = no fog.</summary>
        public static double FogK(double s)
        {
            var V = ThreadConstants.FogVisibility;
            double[] k = new double[V.Length];
            for (int i = 0; i < V.Length; i++) k[i] = V[i] > 0 ? 3.912 / V[i] : 0.0;
            // 4 -> 5 uses the explicit window s = 400 to 460 (14.6)
            double v = k[0];
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 1; i < k.Length; i++)
            {
                double t = i == 4 ? Smootherstep(400, 460, s) : Smootherstep(b[i] - 12, b[i] + 12, s);
                v += (k[i] - k[i - 1]) * t;
            }
            return v;
        }

        public static double FogHeightScale(double s) => Lerp(900.0, 8500.0, Smootherstep(400, 460, s));

        /// <summary>Manual exposure EV schedule (14.6 transition table, exposure windows of 9 m).</summary>
        public static double ExposureEV(double s)
        {
            double ev = 0;
            ev += -0.2 * Smootherstep(250, 259, s);
            ev += 0.2 * Smootherstep(330, 339, s);
            ev += 0.1 * Smootherstep(590, 599, s);
            ev += -0.2 * Smootherstep(750 - 4.5, 750 + 4.5, s);
            return ev;
        }

        public static double BloomThreshold(double s) => BeatBlend(ThreadConstants.BloomThreshold, s);
        public static double BloomIntensity(double s) => BeatBlend(ThreadConstants.BloomIntensity, s);
        public static double BloomScatter(double s) => BeatBlend(ThreadConstants.BloomScatter, s);
        public static double Vignette(double s) => BeatBlend(ThreadConstants.Vignette, s);
        public static double Saturation(double s) => BeatBlend(ThreadConstants.Saturation, s);
        public static double Contrast(double s) => BeatBlend(ThreadConstants.Contrast, s);

        /// <summary>Bank z-profile P(z): 0 at 225, 1 at 265..305, 0 at 345 (smooth).</summary>
        public static double BankP(double z)
        {
            return Smoothstep(ThreadConstants.BankZ0, ThreadConstants.BankZ1, z) * (1.0 - Smoothstep(ThreadConstants.BankZ2, ThreadConstants.BankZ3, z));
        }
        public static double BankY(double y)
        {
            var t = ThreadConstants.BankSlabTops;
            double v = 1.0;
            for (int i = 0; i < t.Length; i++) v -= 0.25 * Smoothstep(t[i] - ThreadConstants.BankSlabHalfEdge, t[i] + ThreadConstants.BankSlabHalfEdge, y);
            return v;
        }
        public static double BankG(double x, double xc)
        {
            double u = (x - xc) / ThreadConstants.BankSigmaX;
            u *= u;
            return Math.Exp(-u * u);   // flat-topped exp(-(x/sigma)^4), D-025
        }
        /// <summary>Bank density at a ground-group-local point (before the -A_virt offset).</summary>
        public static double BankDensity(double x, double y, double z, double xc)
        {
            return ThreadConstants.BankDensity * BankG(x, xc) * BankP(z) * BankY(y);
        }

        /// <summary>Lateral drift of the bank (ambient clock): +-6 m over 40 s with 15 percent noise at 30 s.</summary>
        public static double BankDrift(double ambientTime, double amplitudeScale = 1.0)
        {
            double a = ThreadConstants.BankDriftAmp * amplitudeScale;
            double main = Math.Sin(2 * Math.PI * ambientTime / ThreadConstants.BankDriftPeriod);
            double noise = Math.Sin(2 * Math.PI * ambientTime / ThreadConstants.BankNoisePeriod + 1.3) * 0.6 + Math.Sin(2 * Math.PI * ambientTime / (ThreadConstants.BankNoisePeriod * 0.61) + 0.4) * 0.4;
            return a * (main + ThreadConstants.BankNoiseAmp * noise);
        }

        /// <summary>Analytic cable shadow on the deck / arms / beads for a fragment at world XZ (14.7.7).</summary>
        public static double CableShadow(double px, double pz, D3 sunDir)
        {
            double sx = sunDir.x, sz = sunDir.z, l = Math.Sqrt(sx * sx + sz * sz);
            if (l < 1e-9) return 1;
            sx /= l; sz /= l;
            double qx = px - ThreadConstants.CableAxisX, qz = pz - ThreadConstants.CableAxisZ;
            double t = -(qx * sx + qz * sz);
            double n = Math.Abs(qx * sz - qz * sx);
            if (t <= 0) return 1.0;
            return Smoothstep(ThreadConstants.DeckShadowN0, ThreadConstants.DeckShadowN1, n);
        }
    }
}
