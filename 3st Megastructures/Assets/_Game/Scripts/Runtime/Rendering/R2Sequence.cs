// R2Sequence: Martin Roberts' R2 low-discrepancy sequence ("The unreasonable effectiveness of
// quasirandom sequences", 2018), used to offset the 64 x 64 blue-noise dither tile each frame
// (MASTER_PROMPT 14.7.1 #4, research R6 3.4). A spatial toroidal shift keeps every frame's noise
// spectrum exactly blue; consecutive offsets jump by about (16, 28) texels on the torus, so the tile
// never appears to crawl, and over time every offset is used evenly.
// Evaluated in double on the CPU (n * a1 loses its fraction in float after a few thousand frames).
using System;

namespace Megastructure.Rendering
{
    public static class R2Sequence
    {
        /// <summary>Plastic number g (g^3 = g + 1).</summary>
        public const double G = 1.32471795724474602596;
        public const double A1 = 1.0 / G;          // 0.7548776662466927
        public const double A2 = 1.0 / (G * G);    // 0.5698402909980532
        /// <summary>Frame indices are wrapped to keep n * a fully precise (2^31 frames = 1.1 years at 60 fps).</summary>
        const long Wrap = 1L << 31;

        static double Frac(double v) => v - Math.Floor(v);

        /// <summary>The n-th R2 point in [0,1)^2 (seed 0.5).</summary>
        public static void Point(long n, out double x, out double y)
        {
            long m = n % Wrap;
            if (m < 0) m += Wrap;
            x = Frac(0.5 + A1 * m);
            y = Frac(0.5 + A2 * m);
        }

        /// <summary>Integer toroidal texel offset of frame n for a tile of size tile (a power of two).</summary>
        public static void TileOffset(long n, int tile, out int ox, out int oy)
        {
            Point(n, out double x, out double y);
            int mask = tile - 1;
            ox = (int)(x * tile) & mask;
            oy = (int)(y * tile) & mask;
        }

        /// <summary>The frame index that drives the dither: the grade frame counter, or 0 when the dither is frozen.</summary>
        public static long DitherFrame(long frame, bool freeze) => freeze ? 0L : frame;
    }
}
