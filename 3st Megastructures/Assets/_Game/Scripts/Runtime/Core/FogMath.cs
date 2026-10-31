// FogMath: double-precision C# mirror of the sky gradient and fog functions of ThreadCommon.hlsl
// (ThreadSkyColor, ThreadTauDistance, ThreadTauBank, ThreadTauDeck). EditMode tests compare
// against references (T-BANK 16 vs 256 samples, horizon agreement) and ProgressDirector uses
// SkyColor for the camera clear colour. Pure C#, no UnityEngine dependency.
using System;

namespace Megastructure
{
    public static class FogMath
    {
        public struct Rgb
        {
            public double r, g, b;
            public Rgb(double r, double g, double b) { this.r = r; this.g = g; this.b = b; }
            public static Rgb Lerp(Rgb a, Rgb c, double t) => new Rgb(a.r + (c.r - a.r) * t, a.g + (c.g - a.g) * t, a.b + (c.b - a.b) * t);
        }

        static double Srgb(string hex, int i) => ThreadPaletteMath.SrgbHexChannelToLinear(hex, i);
        static Rgb Tok(string hex) => new Rgb(Srgb(hex, 0), Srgb(hex, 1), Srgb(hex, 2));

        public static void SkyStops(double D, out Rgb h, out Rgb m, out Rgb z)
        {
            double a = ThreadMath.Clamp01(D * 2.0), b = ThreadMath.Clamp01(D * 2.0 - 1.0);
            Func<string, string> P = ThreadPaletteMath.Hex;
            h = Rgb.Lerp(Rgb.Lerp(Tok(P("SKY_H_DAY")), Tok(P("SKY_H_DUSK")), a), Tok(P("SKY_H_NIGHT")), b);
            m = Rgb.Lerp(Rgb.Lerp(Tok(P("SKY_M_DAY")), Tok(P("SKY_M_DUSK")), a), Tok(P("SKY_M_NIGHT")), b);
            z = Rgb.Lerp(Rgb.Lerp(Tok(P("SKY_Z_DAY")), Tok(P("SKY_Z_DUSK")), a), Tok(P("SKY_Z_NIGHT")), b);
        }

        static double Hermite(double p0, double p1, double m0, double m1, double u)
        {
            double u2 = u * u, u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * m1;
        }
        static double Clamp(double v, double a, double b) { double lo = Math.Min(a, b), hi = Math.Max(a, b); return v < lo ? lo : (v > hi ? hi : v); }

        public static Rgb SkyColor(double dirY, double D)
        {
            SkyStops(D, out var h, out var m, out var z);
            double tM = Math.Sqrt(Math.Sin(ThreadConstants.SkyStopM * ThreadMath.Deg));
            double t = Math.Sqrt(ThreadMath.Clamp01(dirY));
            double Ch(double hh, double mm, double zz)
            {
                double slope = zz - hh;
                if (t < tM) return Clamp(Hermite(hh, mm, 0, slope * tM, t / tM), hh, mm);
                return Clamp(Hermite(mm, zz, slope * (1 - tM), 0, (t - tM) / (1 - tM)), mm, zz);
            }
            return new Rgb(Ch(h.r, m.r, z.r), Ch(h.g, m.g, z.g), Ch(h.b, m.b, z.b));
        }

        // ------------------------------------------------------------------ distance fog
        public static double TauDistance(D3 cam, D3 dir, double d, double k, double H, double aVirt)
        {
            if (k <= 0) return 0;
            H = Math.Max(H, 1.0);
            double y0 = cam.y + aVirt;
            double e0 = Math.Exp(-Math.Max(-2000.0, Math.Min(y0, 1e6)) / H);
            double x = dir.y * d / H, f;
            if (Math.Abs(x) < 1e-3) f = d * (1.0 - 0.5 * x + x * x / 6.0);
            else f = H * (1.0 - Math.Exp(-Math.Max(-60, Math.Min(60, x)))) / (Math.Abs(dir.y) > 1e-7 ? dir.y : 1e-7);
            return k * e0 * Math.Max(f, 0.0);
        }

        // ------------------------------------------------------------------ bank
        static double Sm(double e0, double e1, double x) => ThreadMath.Smoothstep(e0, e1, x);
        public static double BankDensityLocal(double x, double y, double z, double xc, double scale = 1.0)
        {
            double g = (x - xc) / ThreadConstants.BankSigmaX; g *= g; g = Math.Exp(-g * g);   // exp(-(x/sigma)^4), D-025
            double pz = Sm(ThreadConstants.BankZ0, ThreadConstants.BankZ1, z) * (1.0 - Sm(ThreadConstants.BankZ2, ThreadConstants.BankZ3, z));
            double yv = 1.0;
            for (int i = 0; i < 4; i++)
            {
                double top = ThreadConstants.BankSlabTop0 + ThreadConstants.BankSlabTopStep * i;
                yv -= 0.25 * Sm(top - ThreadConstants.BankSlabHalfEdge, top + ThreadConstants.BankSlabHalfEdge, y);
            }
            return ThreadConstants.BankDensity * scale * g * pz * yv;
        }

        /// <summary>Bank optical depth with n midpoint samples over the box clip (n = 16 in the shaders); scale = _ThBank.w.</summary>
        public static double TauBank(D3 cam, D3 dir, double d, double xc, double groupOffsetY, int n = 16, double scale = 1.0)
        {
            D3 o = new D3(cam.x, cam.y - groupOffsetY, cam.z);
            double[] bmin = { ThreadConstants.BankBoxXMin, ThreadConstants.BankBoxYMin, ThreadConstants.BankBoxZMin };
            double[] bmax = { ThreadConstants.BankBoxXMax, ThreadConstants.BankBoxYMax, ThreadConstants.BankBoxZMax };
            double[] oo = { o.x, o.y, o.z }, dd = { dir.x, dir.y, dir.z };
            double ta = 0, tb = d;
            for (int a = 0; a < 3; a++)
            {
                double sd = Math.Abs(dd[a]) > 1e-6 ? dd[a] : 1e-6;
                double t0 = (bmin[a] - oo[a]) / sd, t1 = (bmax[a] - oo[a]) / sd;
                ta = Math.Max(ta, Math.Min(t0, t1)); tb = Math.Min(tb, Math.Max(t0, t1));
            }
            if (tb <= ta) return 0;
            double dt = (tb - ta) / n, sum = 0;
            for (int i = 0; i < n; i++)
            {
                double t = ta + (i + 0.5) * dt;
                sum += BankDensityLocal(o.x + dir.x * t, o.y + dir.y * t, o.z + dir.z * t, xc, scale);
            }
            return sum * dt;
        }

        /// <summary>Pixel window (beyond the surface) of the bank footprint filter (TH_FOG_BANK_FOOT_PX).</summary>
        public const double BankFootPx = 12.0;
        /// <summary>The footprint window's medium continues this far under the ground (TH_FOG_BANK_UNDERGROUND).</summary>
        public const double BankUnderground = 1.0e4;

        /// <summary>ThreadTauBankFootprint: TauBank to the surface plus -ln of the mean transmittance over the window of hit
        /// distances d .. 1 / uLo beyond it (uLo = 1/d - BankFootPx fwidth(1/d)); uLo >= 1/d gives TauBank exactly.</summary>
        public static double TauBankFootprint(D3 cam, D3 dir, double d, double uLo, double xc, double groupOffsetY, int n = 16, double scale = 1.0)
        {
            double tau = TauBank(cam, dir, d, xc, groupOffsetY, n, scale);
            double u = 1.0 / Math.Max(d, 1e-4);
            double dFar = uLo > 1e-9 ? 1.0 / uLo : 1.0e9;
            if (!(dFar > d * 1.001)) return tau;
            D3 o = new D3(cam.x, cam.y - groupOffsetY, cam.z);
            double[] bmin = { ThreadConstants.BankBoxXMin, ThreadConstants.BankBoxYMin - BankUnderground, ThreadConstants.BankBoxZMin };
            double[] bmax = { ThreadConstants.BankBoxXMax, ThreadConstants.BankBoxYMax, ThreadConstants.BankBoxZMax };
            double[] oo = { o.x, o.y, o.z }, dd = { dir.x, dir.y, dir.z };
            double ta = d, tb = dFar;
            for (int a = 0; a < 3; a++)
            {
                double sd = Math.Abs(dd[a]) > 1e-6 ? dd[a] : 1e-6;
                double t0 = (bmin[a] - oo[a]) / sd, t1 = (bmax[a] - oo[a]) / sd;
                ta = Math.Max(ta, Math.Min(t0, t1)); tb = Math.Min(tb, Math.Max(t0, t1));
            }
            if (tb <= ta) return tau;
            // march uniformly in 1/t (uniform in screen rows on a plane): every step carries the same window share
            double W = Math.Max(u - uLo, 1e-12), ua = 1.0 / ta, ub = 1.0 / tb, du = (ua - ub) / n;
            double meanT = (u - ua) / W, tx = 0.0, tPrev = ta;
            for (int i = 0; i < n; i++)
            {
                double tN = 1.0 / Math.Max(ua - (i + 1) * du, 1e-12), t = 0.5 * (tPrev + tN);
                double rd = BankDensityLocal(o.x + dir.x * t, o.y + dir.y * t, o.z + dir.z * t, xc, scale) * (tN - tPrev);
                meanT += du / W * Math.Exp(-(tx + 0.5 * rd));
                tx += rd;
                tPrev = tN;
            }
            meanT += (ub - uLo) / W * Math.Exp(-tx);
            return tau - Math.Log(Math.Max(1e-30, Math.Min(meanT, 1.0)));
        }

        // ------------------------------------------------------------------ deck
        public static double SmoothstepIntegral(double a, double w, double y)
        {
            double u = ThreadMath.Clamp01((y - a) / w);
            return w * (u * u * u - 0.5 * u * u * u * u) + Math.Max(y - (a + w), 0.0);
        }
        public static double DeckF(double y, double top, double bot)
        {
            double F = SmoothstepIntegral(bot, ThreadConstants.DeckBottomSkin, y);
            for (int i = 0; i < 3; i++) F -= SmoothstepIntegral(top - ThreadConstants.DeckSkin + ThreadConstants.DeckTopSkinStep * i, ThreadConstants.DeckTopSkinStep, y) / 3.0;
            return F;
        }
        public static double DeckProfile(double y, double top, double bot)
        {
            double f = Sm(bot, bot + ThreadConstants.DeckBottomSkin, y);
            for (int i = 0; i < 3; i++) { double a = top - ThreadConstants.DeckSkin + ThreadConstants.DeckTopSkinStep * i; f -= Sm(a, a + ThreadConstants.DeckTopSkinStep, y) / 3.0; }
            return Math.Max(f, 0);
        }
        public static double TauDeck(D3 cam, D3 dir, double d, double gate, double top, double bot)
        {
            if (gate <= 0) return 0;
            double tau;
            if (Math.Abs(dir.y) < 1e-4) tau = DeckProfile(cam.y, top, bot) * d;
            else
            {
                // clamp both ends to the slab (f = 0 outside it), mirroring the precision-safe HLSL (D-024)
                double y1 = cam.y + dir.y * Math.Min(d, 1e7);
                double a = Math.Max(bot, Math.Min(top, cam.y)), b = Math.Max(bot, Math.Min(top, y1));
                tau = Math.Abs(DeckF(b, top, bot) - DeckF(a, top, bot)) / Math.Abs(dir.y);
            }
            return gate * ThreadConstants.DeckDensity * tau;
        }

        /// <summary>World-space deck slab placement for a camera (14.7.6): top = y_cam - (A - 3000).</summary>
        public static void DeckSlab(double camY, double aVirt, out double top, out double bot)
        {
            top = camY - (aVirt - ThreadConstants.CloudTop);
            bot = top - (ThreadConstants.CloudTop - ThreadConstants.CloudBottom);
        }
        public static double DeckGate(double aVirt) => ThreadMath.Smoothstep(ThreadConstants.DeckGateA0, ThreadConstants.DeckGateA1, aVirt);

        // ------------------------------------------------------------------ hue-preserving haze (ThreadHazeMix)
        /// <summary>Chroma weight exponent of the haze mix (TH_FOG_CHROMA_N): Tc = 1 - (1 - T)^n.</summary>
        public const double HazeChromaN = 1.5;
        public static double Luma(Rgb c) => 0.2126729 * c.r + 0.7151522 * c.g + 0.0721750 * c.b;
        static double Cbrt(double x) => x > 0.0 ? Math.Pow(x, 1.0 / 3.0) : 0.0;   // pow(max(x, 0), 1/3) as in the shader
        public static Rgb Min(Rgb c, double cap) => new Rgb(Math.Min(c.r, cap), Math.Min(c.g, cap), Math.Min(c.b, cap));
        /// <summary>Linear palette colour of a token name (e.g. "CLOUD_TOP").</summary>
        public static Rgb Pal(string token) => Tok(ThreadPaletteMath.Hex(token));

        /// <summary>OKLab (B. Ottosson 2020) of a linear sRGB colour.</summary>
        public static void ToOklab(Rgb c, out double L, out double a, out double b)
        {
            double l = Cbrt(Math.Max(0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b, 0.0));
            double m = Cbrt(Math.Max(0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b, 0.0));
            double s = Cbrt(Math.Max(0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b, 0.0));
            L = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
            a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
            b = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        }
        public static Rgb FromOklab(double L, double a, double b)
        {
            double l = L + 0.3963377774 * a + 0.2158037573 * b, m = L - 0.1055613458 * a - 0.0638541728 * b, s = L - 0.0894841775 * a - 1.2914855480 * b;
            l = l * l * l; m = m * m * m; s = s * s * s;
            return new Rgb(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                           -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                           -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
        }

        /// <summary>ThreadHazeMix: colour c seen through transmittance T toward the fog colour f. Luminance is exactly the
        /// physical lerp(Y_f, Y_c, T); OKLab hue / chroma follow the slower weight 1 - (1 - T)^n. Exact at T = 0 and 1.</summary>
        public static Rgb HazeMix(Rgb c, Rgb f, double T)
        {
            T = ThreadMath.Clamp01(T);
            if (T >= 1.0) return c;
            if (T <= 1e-6) return f;
            double Yc = Luma(c), Yf = Luma(f), Y = Yf + (Yc - Yf) * T;
            double Tc = 1.0 - Math.Pow(1.0 - T, HazeChromaN);
            ToOklab(c, out double Lc, out double ac, out double bc);
            ToOklab(f, out double Lf, out double af, out double bf);
            double offF = Lf - Cbrt(Math.Max(Yf, 0.0)), offC = Lc - Cbrt(Math.Max(Yc, 0.0));
            double L = Cbrt(Math.Max(Y, 0.0)) + offF + (offC - offF) * Tc;
            Rgb rgb = FromOklab(L, af + (ac - af) * Tc, bf + (bc - bf) * Tc);
            rgb = new Rgb(Math.Max(rgb.r, 0.0), Math.Max(rgb.g, 0.0), Math.Max(rgb.b, 0.0));
            double k = Y / Math.Max(Luma(rgb), 1e-6);
            return new Rgb(rgb.r * k, rgb.g * k, rgb.b * k);
        }

        // ------------------------------------------------------------------ the shared fog of ThreadCommon
        /// <summary>The fog uniforms of one frame (_ThSky.x, _ThFog, _ThBank, _ThDeck and the camera), as ProgressDirector sets them.</summary>
        public struct Frame
        {
            public D3 cam;
            public double D, k, H, aVirt, cap, xc, bankScale, groupOffsetY, gate, deckTop, deckBot;
            public bool bankOn;

            /// <summary>Uniforms at progress s for a camera position (ground group active while s &lt; GroundOffS).</summary>
            public static Frame ForProgress(double s, D3 cam, double xc = 0.0)
            {
                var f = new Frame { cam = cam, xc = xc };
                f.D = ThreadMath.Darkness(s);
                f.k = ThreadMath.FogK(s);
                f.H = ThreadMath.FogHeightScale(s);
                f.aVirt = ThreadMath.VirtualAltitude(s);
                f.cap = 0.97 / Math.Pow(2.0, ThreadMath.ExposureEV(s));
                f.bankScale = 1.0 - ThreadMath.Smoothstep(ThreadConstants.BankFadeS0, ThreadConstants.BankFadeS1, s);
                f.bankOn = s < ThreadConstants.GroundOffS && f.bankScale > 0.0;
                f.groupOffsetY = -f.aVirt;
                f.gate = DeckGate(f.aVirt);
                DeckSlab(cam.y, f.aVirt, out f.deckTop, out f.deckBot);
                return f;
            }
        }

        public static Rgb CloudFogColour(Frame f) => Min(Pal("CLOUD_TOP"), f.cap);

        /// <summary>ThreadTauDeckSky: deck depth along an upward sky ray at infinity with the curved-Earth chord limit
        /// (slope at least sqrt(2 dh / R) at the deck bottom dh above the camera).</summary>
        public static double TauDeckSky(Frame f, double dirY)
        {
            if (f.gate <= 0) return 0.0;
            double dh = Math.Max(f.deckBot - f.cam.y, 0.0);
            double dy = Math.Max(Math.Sqrt(dirY * dirY + 2.0 * dh / ThreadConstants.EarthRadius), 1e-4);
            return TauDeck(f.cam, new D3(0, dy, 0), 1e7, f.gate, f.deckTop, f.deckBot);
        }
        public static double DeckSkyTransmittance(Frame f, double dirY) => f.gate <= 0 ? 1.0 : Math.Exp(-Math.Min(TauDeckSky(f, Math.Abs(dirY)), 80.0));
        /// <summary>ThreadFogVeil: colour c veiled toward (capped) CLOUD_TOP by the deck seen along the (mirrored) sky ray.</summary>
        public static Rgb FogVeil(Frame f, Rgb c, double dirY) => HazeMix(c, CloudFogColour(f), DeckSkyTransmittance(f, dirY));
        public static Rgb FogDistanceColour(Frame f, double dirY) => FogVeil(f, SkyColor(dirY, f.D), dirY);

        /// <summary>ThreadFogMix: distance term first (toward the distance colour of the ray), then bank / deck over it.</summary>
        public static Rgb FogMix(Frame f, Rgb color, double tauDist, double tauCloud, double dirY)
        {
            if (tauDist + tauCloud <= 1e-6) return color;
            Rgb c = color;
            if (tauDist > 1e-6) c = HazeMix(c, FogDistanceColour(f, dirY), Math.Exp(-Math.Min(tauDist, 80.0)));
            if (tauCloud > 1e-6) c = HazeMix(c, CloudFogColour(f), Math.Exp(-Math.Min(tauCloud, 80.0)));
            return c;
        }

        /// <summary>ThreadFogTaus for a geometry point at distance d along dir; uLo = 1/d (no footprint) or the
        /// fragment-stage window 1/d - BankFootPx fwidth(1/d).</summary>
        public static void GeometryTaus(Frame f, D3 dir, double d, double uLo, out double tauDist, out double tauCloud)
        {
            tauDist = TauDistance(f.cam, dir, d, f.k, f.H, f.aVirt);
            double bank = f.bankOn ? TauBankFootprint(f.cam, dir, d, uLo, f.xc, f.groupOffsetY, 16, f.bankScale) : 0.0;
            tauCloud = bank + TauDeck(f.cam, dir, d, f.gate, f.deckTop, f.deckBot);
        }

        /// <summary>ThreadFogApplySky: below the horizon the deck veil first, then the bank (to 2 km) and the deck
        /// (curved-deck depth upward, maxD-limited downward).</summary>
        public static Rgb ApplySky(Frame f, Rgb color, D3 dir, double maxD = 1e7)
        {
            if (dir.y < 0.0 && f.gate > 0.0) color = FogVeil(f, color, dir.y);
            double tauDeck = dir.y >= 0.0 ? TauDeckSky(f, dir.y) : TauDeck(f.cam, dir, maxD, f.gate, f.deckTop, f.deckBot);
            double tauCloud = (f.bankOn ? TauBank(f.cam, dir, Math.Min(maxD, 2000.0), f.xc, f.groupOffsetY, 16, f.bankScale) : 0.0) + tauDeck;
            return tauCloud > 1e-6 ? HazeMix(color, CloudFogColour(f), Math.Exp(-Math.Min(tauCloud, 80.0))) : color;
        }
    }

    /// <summary>Palette helpers that do not need UnityEngine (hex table lives in ThreadPalette.Table).</summary>
    public static class ThreadPaletteMath
    {
        static System.Collections.Generic.Dictionary<string, string> s_hex;
        public static string Hex(string token)
        {
            if (s_hex == null)
            {
                s_hex = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var t in ThreadPalette.Table) s_hex[t.id.ToString()] = t.hex;
            }
            return s_hex[token];
        }
        public static double SrgbHexChannelToLinear(string hex, int channel)
        {
            string h = hex.TrimStart('#');
            int v = Convert.ToInt32(h.Substring(channel * 2, 2), 16);
            double c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
