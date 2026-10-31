// CableBuilder: the space-elevator cable of beat 4 onward (MASTER_PROMPT 14.5.4 (b), 14.7.2, 14.7.4, 14.7.9).
// All meshes are in the cable's local frame: the cable root sits at (CableAxisX, 0, CableAxisZ) under the
// near root (true world coordinates; the cable never sinks), so |x|, |z| <= 52.3 m and float precision is
// ~4e-6 m near the walkway. Bands of FIXED height (always active, no distance switching, nothing pops):
//
//  LOD0  y in [-300, 600]: 36 strand ridges, left-hand lay with pitch 600 m. Ridge n has its crest at
//        theta_n(y) = (n + 0.5) * 2 pi / 36 + 2 pi y / 600 (theta from +Z toward +X), which is exactly where the
//        M05 lay-line term u = 50 theta - 0.5236 y has its half-integer phases (u is constant along a crest:
//        50 * 2 pi / 600 = 0.5236). Each ridge is a circular-arc profile sampled by 12 columns over 8.727 m of
//        circumference with 1.2 m relief (valley r = 50.0, crest r = 51.2); the columns follow the strands, so
//        crests and valleys are mesh edges. The valley column is duplicated with the two one-sided normals
//        (a crisp groove; MSAA resolves the crease because it is a triangle edge). Rings every 4 m and every
//        2 m for y in [28, 180] (the spec's [30, 180] widened to the 4 m grid, so every band divides exactly):
//        264 rings, 263 bands x 864 = 227 232 triangles, 9 chunks of 100 m (16-bit indices).
//        Vertex data: position = ridge surface, normal = analytic ridge normal, tangent.xyz = smooth outward
//        cylinder normal (w = 1), uv1.x = relief r - 50 (m) for the vertex-shader relief fade (140 -> 320 m
//        from the camera; it only moves vertices inward, so the bounds stay conservative), uv0 = (strand
//        fraction, y). Vertex colour (1, 1, 0, 1): groove darkening comes from the filtered lay-line term,
//        not from unfiltered vertex AO (which would alias at km distances).
//        The relief tapers to 0 over the last 40 m at both ends (never visible: those rings are >= 336 m from
//        any camera position, beyond the 320 m fade), so the boundary rings are exact r = 50 polygons with
//        radial normals ("relief zero at the boundary", 14.5.4 pitfalls).
//  LOD1  y in [-3300, -300] and [600, 3500]: plain 108-gon (3 vertices per strand), 20 m rings, columns
//        vertical. Its first ring is a bit-identical COPY of the LOD0 boundary ring (432 vertices) and a
//        4:1 zipper band inside LOD1 steps down to 108 (no T-junction, every boundary edge is shared).
//  LOD2  y in [-20 km, -3.3 km] and [3.5 km, 20 km]: plain 54-gon (54 divides 108; decision logged instead
//        of the spec's 64 sides: identical silhouette at >= 3.3 km, 0.02 px facet), 14 geometric bands per
//        end, first ring a copy of the LOD1 end ring with a 2:1 zipper.
//  Collars: tori every 100 m from y = -300 to 600, tube 1.4, major radius 50.9 so the inner radius 49.5 stays
//        embedded 0.5 m in the smooth r = 50 surface even after the relief has faded (the table's 52.5 would
//        float 1.1 m above the faded cylinder). Segment counts come from each collar's closest approach to
//        the walked path (0.5 px silhouette facet at 1080p); a collar fully enclosed by a bead lens gets the
//        minimum. Index-wrapped (no seam duplicates, so no cos(2 pi) != 1 cracks).
//  Ribbons: two ThreadLine centre-line ribbons on the axis beyond 20 km (to +-200 km), 16 geometric segments,
//        overlapping the LOD2 ends by 400 m (hidden behind the opaque cylinder, so no gap can open at the
//        junction), explicit large bounds.
// The cable, collars and ribbons cast no shadow maps (D-005); the cable and collars receive them.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>One LOD0 ring: 36 strands x 13 vertices (k = 0..12, valley crease duplicated at k = 0 / 12).</summary>
    public sealed class CableRing
    {
        public float Y;
        public readonly Vector3[] Position = new Vector3[CableBuilder.Lod0RingVerts];
        public readonly Vector3[] Normal = new Vector3[CableBuilder.Lod0RingVerts];
        public readonly Vector3[] Smooth = new Vector3[CableBuilder.Lod0RingVerts];
        public readonly float[] Relief = new float[CableBuilder.Lod0RingVerts];
    }

    /// <summary>Every mesh of the cable, by band.</summary>
    public sealed class CableSet
    {
        public readonly List<Mesh> Lod0 = new List<Mesh>();
        public readonly List<Mesh> Lod1 = new List<Mesh>();
        public readonly List<Mesh> Lod2 = new List<Mesh>();
        public readonly List<Mesh> Collars = new List<Mesh>();
        public readonly List<Mesh> Ribbons = new List<Mesh>();
        public readonly List<double> CollarY = new List<double>();
        public readonly List<int> CollarMajor = new List<int>();
        public readonly List<int> CollarMinor = new List<int>();

        public List<Mesh> Lateral()
        {
            var l = new List<Mesh>(Lod0.Count + Lod1.Count + Lod2.Count);
            l.AddRange(Lod0); l.AddRange(Lod1); l.AddRange(Lod2);
            return l;
        }

        public static long Triangles(List<Mesh> meshes)
        {
            long t = 0;
            for (int i = 0; i < meshes.Count; i++) t += MeshKit.TriangleCount(meshes[i]);
            return t;
        }

        public long TotalTriangles => Triangles(Lod0) + Triangles(Lod1) + Triangles(Lod2) + Triangles(Collars) + Triangles(Ribbons);

        public void DestroyAll()
        {
            DestroyList(Lod0); DestroyList(Lod1); DestroyList(Lod2); DestroyList(Collars); DestroyList(Ribbons);
        }

        static void DestroyList(List<Mesh> l)
        {
            for (int i = 0; i < l.Count; i++)
            {
                if (l[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(l[i]); else UnityEngine.Object.DestroyImmediate(l[i]);
            }
            l.Clear();
        }
    }

    /// <summary>One named piece of cable mesh data.</summary>
    public sealed class CablePart
    {
        public readonly string Name;
        public readonly MeshBuilder Data;
        public CablePart(string name, MeshBuilder data) { Name = name; Data = data; }
    }

    /// <summary>The cable as mesh data (CableBuilder.Generate), before any Mesh is created.</summary>
    public sealed class CableParts
    {
        public readonly List<CablePart> Lod0 = new List<CablePart>();
        public readonly List<CablePart> Lod1 = new List<CablePart>();
        public readonly List<CablePart> Lod2 = new List<CablePart>();
        public readonly List<CablePart> Collars = new List<CablePart>();
        public readonly List<CablePart> Ribbons = new List<CablePart>();
        public readonly List<double> CollarY = new List<double>();
        public readonly List<int> CollarMajor = new List<int>();
        public readonly List<int> CollarMinor = new List<int>();
    }

    public static class CableBuilder
    {
        // ---- counts and spacings (14.5.4 line 693 unless noted; listed in the contract requests)
        public const int Strands = ThreadConstants.CableStrands;              // 36
        public const int RidgeVertices = 12;                                  // "a rounded profile of 12 vertices"
        public const int Lod0Cols = Strands * RidgeVertices;                  // 432 angular positions per ring
        public const int Lod0RingVerts = Strands * (RidgeVertices + 1);       // 468 (valley crease duplicated)
        public const int Lod1VertsPerStrand = 3;                              // "3 vertices per strand"
        public const int Lod1Sides = Strands * Lod1VertsPerStrand;            // 108
        public const int Lod2Sides = 54;                                      // spec 64; 54 divides 108 (crack-free)
        public const double Lod0RingStep = 4.0, Lod0FineRingStep = 2.0;       // "4 m ring spacing (2 m for y in [30, 180])"
        public const double Lod0FineY0 = 30.0, Lod0FineY1 = 180.0;
        public const double Lod1RingStep = 20.0;                              // "20 m rings"
        public const int Lod2Bands = 14;                                      // ~3 k triangles for both ends
        public const double Lod0ChunkHeight = 100.0;                          // culling chunks (16-bit each)
        public const int Lod1BandsPerChunk = 25;                              // 500 m chunks
        public const double ReliefTaper = 40.0;                               // relief 0 at the LOD0 band ends
        public const double CollarMajorRadius = ThreadConstants.CableRadius - ThreadConstants.CollarEmbed + ThreadConstants.CollarTube; // 50.9
        public const int CollarMajorMin = 64, CollarMajorMax = 192, CollarMinorMin = 12, CollarMinorMax = 32;
        public const double CollarTolerancePx = 0.5, ReferenceHeightPx = 1080.0;
        public const double CollarAoMin = 0.62, CollarAoB0 = 1.0, CollarAoB1 = 2.2;   // tube angle (rad) from the outer equator
        public const double CollarBeadMargin = 0.2;
        public const int RibbonSegments = 16;                                 // 14.5.1: the Ribbon generator has 16 segments
        public const double RibbonStart = ThreadConstants.CableLod2YMax;      // beyond 20 km (14.5.4)
        public const double RibbonOverlap = 400.0;
        public const double RibbonBoundsHalfWidth = 1000.0;

        static readonly Color VertexColour = new Color(1f, 1f, 0f, 1f);
        const double TwoPi = 2.0 * Math.PI;

        // ------------------------------------------------------------------ public entry
        /// <summary>Generates all cable geometry and uploads it as meshes (load time, behind the black fade).</summary>
        public static CableSet Build()
        {
            CableParts parts = Generate();
            var set = new CableSet();
            ToMeshes(parts.Lod0, set.Lod0);
            ToMeshes(parts.Lod1, set.Lod1);
            ToMeshes(parts.Lod2, set.Lod2);
            ToMeshes(parts.Collars, set.Collars);
            ToMeshes(parts.Ribbons, set.Ribbons);
            for (int i = 0; i < set.Ribbons.Count; i++) set.Ribbons[i].bounds = RibbonBounds(i == 0);
            set.CollarY.AddRange(parts.CollarY);
            set.CollarMajor.AddRange(parts.CollarMajor);
            set.CollarMinor.AddRange(parts.CollarMinor);
            return set;
        }

        /// <summary>Generates all cable geometry as mesh data only (no engine objects; used by Build and by offline checks).</summary>
        public static CableParts Generate()
        {
            var parts = new CableParts();
            var ys0 = Lod0RingYs();
            var rings = new List<CableRing>(ys0.Count);
            for (int i = 0; i < ys0.Count; i++) rings.Add(Lod0Ring(ys0[i]));

            // LOD0 chunks (boundary rings are the same CableRing object in both chunks: bit-identical)
            var breaks = Lod0ChunkRingIndices(ys0);
            for (int c = 0; c + 1 < breaks.Count; c++)
                parts.Lod0.Add(new CablePart($"Cable_LOD0_{c:00}_{ys0[breaks[c]]:0}", Lod0Chunk(rings, breaks[c], breaks[c + 1])));

            CableRing bottom = rings[0], top = rings[rings.Count - 1];
            BuildLod1(parts, top, true);
            BuildLod1(parts, bottom, false);
            parts.Lod2.Add(new CablePart("Cable_LOD2_up", Lod2(true)));
            parts.Lod2.Add(new CablePart("Cable_LOD2_down", Lod2(false)));
            BuildCollars(parts);
            parts.Ribbons.Add(new CablePart("Cable_Ribbon_up", Ribbon(true)));
            parts.Ribbons.Add(new CablePart("Cable_Ribbon_down", Ribbon(false)));
            return parts;
        }

        static void ToMeshes(List<CablePart> src, List<Mesh> dst)
        {
            for (int i = 0; i < src.Count; i++) dst.Add(src[i].Data.ToMesh(src[i].Name));
        }

        // ------------------------------------------------------------------ ring heights
        /// <summary>LOD0 ring heights: 4 m from -300, 2 m from 28 (the 4 m grid point at or below 30) to 180, 4 m to 600.</summary>
        public static List<double> Lod0RingYs()
        {
            double y0 = ThreadConstants.CableLod0YMin, y1 = ThreadConstants.CableLod0YMax;
            double f0 = y0 + Lod0RingStep * Math.Floor((Lod0FineY0 - y0) / Lod0RingStep + 1e-9);
            double f1 = y0 + Lod0RingStep * Math.Ceiling((Lod0FineY1 - y0) / Lod0RingStep - 1e-9);
            var ys = new List<double>(272);
            AppendRange(ys, y0, f0, Lod0RingStep);
            AppendRange(ys, f0, f1, Lod0FineRingStep);
            AppendRange(ys, f1, y1, Lod0RingStep);
            return ys;
        }

        static void AppendRange(List<double> ys, double a, double b, double step)
        {
            int n = Math.Max(1, (int)Math.Round((b - a) / step));
            for (int i = ys.Count == 0 ? 0 : 1; i <= n; i++) ys.Add(i == n ? b : a + (b - a) * i / n);
        }

        /// <summary>Ring indices of the chunk boundaries (every 100 m from the bottom, plus both ends).</summary>
        public static List<int> Lod0ChunkRingIndices(List<double> ys)
        {
            var idx = new List<int> { 0 };
            double next = ys[0] + Lod0ChunkHeight;
            for (int i = 1; i < ys.Count - 1; i++)
                if (ys[i] >= next - 1e-6) { idx.Add(i); next += Lod0ChunkHeight; }
            idx.Add(ys.Count - 1);
            return idx;
        }

        /// <summary>LOD1 ring heights, ordered bottom to top; upper starts at 600, lower ends at -300 (the copied boundary rings).</summary>
        public static List<double> Lod1RingYs(bool upper)
        {
            var ys = new List<double>(160);
            if (upper) AppendRange(ys, ThreadConstants.CableLod0YMax, ThreadConstants.CableLod1YMax, Lod1RingStep);
            else AppendRange(ys, ThreadConstants.CableLod1YMin, ThreadConstants.CableLod0YMin, Lod1RingStep);
            return ys;
        }

        /// <summary>LOD2 ring heights, ordered bottom to top (geometric spacing away from the LOD1 end).</summary>
        public static List<double> Lod2RingYs(bool upper)
        {
            double a = upper ? ThreadConstants.CableLod1YMax : ThreadConstants.CableLod1YMin;
            double b = upper ? ThreadConstants.CableLod2YMax : ThreadConstants.CableLod2YMin;
            var ys = new List<double>(Lod2Bands + 1);
            for (int k = 0; k <= Lod2Bands; k++) ys.Add(k == Lod2Bands ? b : a * Math.Pow(b / a, (double)k / Lod2Bands));
            if (!upper) ys.Reverse();
            return ys;
        }

        // ------------------------------------------------------------------ LOD0 surface
        /// <summary>Relief amplitude a(y) (m) and da/dy: full 1.2 m inside, smoothstep taper to 0 at the band ends.</summary>
        public static void ReliefAmplitude(double y, out double a, out double dady)
        {
            double T = ReliefTaper;
            double u1 = ThreadMath.Clamp01((y - ThreadConstants.CableLod0YMin) / T);
            double u2 = ThreadMath.Clamp01((ThreadConstants.CableLod0YMax - y) / T);
            double s1 = u1 * u1 * (3 - 2 * u1), s2 = u2 * u2 * (3 - 2 * u2);
            double d1 = 6 * u1 * (1 - u1) / T, d2 = -6 * u2 * (1 - u2) / T;
            a = ThreadConstants.CableRidgeRelief * s1 * s2;
            dady = ThreadConstants.CableRidgeRelief * (d1 * s2 + s1 * d2);
        }

        /// <summary>Crest angle of strand n at height y (theta from +Z toward +X, wrapped to [0, 2 pi)).</summary>
        public static double CrestAngle(int n, double y)
        {
            return Wrap2Pi((n + 0.5) * TwoPi / Strands + TwoPi * y / ThreadConstants.CablePitch);
        }

        public static CableRing Lod0Ring(double y)
        {
            var ring = new CableRing { Y = (float)y };
            double R = ThreadConstants.CableRadius, s0 = ThreadConstants.CableRidgeRelief;
            double L = TwoPi * R / Strands;                        // strand width along the circumference (8.727 m)
            double rho = (0.25 * L * L + s0 * s0) / (2.0 * s0);    // radius of the circular-arc ridge profile
            ReliefAmplitude(y, out double a, out double ay);
            double twist = Wrap2Pi(TwoPi * y / ThreadConstants.CablePitch);
            double dTheta = TwoPi / Lod0Cols;
            double crestSlope = TwoPi * R / ThreadConstants.CablePitch;   // arc length per metre of rise (0.5236)
            float yf = (float)y;
            for (int n = 0; n < Strands; n++)
            {
                for (int k = 0; k <= RidgeVertices; k++)
                {
                    int v = n * (RidgeVertices + 1) + k;
                    int col = (n * RidgeVertices + k) % Lod0Cols;
                    double theta = col * dTheta + twist;
                    double x = (k - RidgeVertices * 0.5) * (L / RidgeVertices);   // arc distance from the crest
                    double q = Math.Sqrt(Math.Max(rho * rho - x * x, 1e-12));
                    double G = (k == 0 || k == RidgeVertices) ? 0.0 : Math.Max(0.0, (q - (rho - s0)) / s0);
                    double Gp = -x / (s0 * q);                                     // one-sided at the valleys
                    double h = a * G;
                    double hT = a * Gp * R;                                        // dh / dtheta
                    double hY = ay * G - a * Gp * crestSlope;                      // dh / dy
                    double st = Math.Sin(theta), ct = Math.Cos(theta);
                    double rr = R + h;
                    // N ~ e_r - (h_theta / r) e_theta - h_y e_y with e_r = (sin, 0, cos), e_theta = (cos, 0, -sin)
                    double w = hT / rr;
                    double nx = st - w * ct, ny = -hY, nz = ct + w * st;
                    double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    ring.Position[v] = new Vector3((float)(rr * st), yf, (float)(rr * ct));
                    ring.Normal[v] = new Vector3((float)(nx / nl), (float)(ny / nl), (float)(nz / nl));
                    ring.Smooth[v] = new Vector3((float)st, 0f, (float)ct);
                    ring.Relief[v] = (float)h;
                }
            }
            return ring;
        }

        static void AddLod0Ring(MeshBuilder mb, CableRing ring)
        {
            for (int n = 0; n < Strands; n++)
                for (int k = 0; k <= RidgeVertices; k++)
                {
                    int v = n * (RidgeVertices + 1) + k;
                    Vector3 s = ring.Smooth[v];
                    mb.Add(ring.Position[v], ring.Normal[v], new Vector2((float)k / RidgeVertices, ring.Y), VertexColour,
                           new Vector4(s.x, s.y, s.z, 1f), new Vector4(ring.Relief[v], 0f, 0f, 0f));
                }
        }

        static MeshBuilder Lod0Chunk(List<CableRing> rings, int i0, int i1)
        {
            var mb = new MeshBuilder((i1 - i0 + 1) * Lod0RingVerts);
            for (int i = i0; i <= i1; i++) AddLod0Ring(mb, rings[i]);
            for (int i = 0; i < i1 - i0; i++)
            {
                int bA = i * Lod0RingVerts, bB = (i + 1) * Lod0RingVerts;
                for (int n = 0; n < Strands; n++)
                    for (int k = 0; k < RidgeVertices; k++)
                    {
                        int a0 = bA + n * (RidgeVertices + 1) + k, a1 = a0 + 1;
                        int b0 = bB + n * (RidgeVertices + 1) + k, b1 = b0 + 1;
                        // the upper ring is rotated toward +theta by the lay, so (a1, b0) is the short diagonal
                        TriOut(mb, a0, a1, b0);
                        TriOut(mb, a1, b1, b0);
                    }
            }
            return mb;
        }

        // ------------------------------------------------------------------ LOD1 / LOD2 (plain polygons)
        /// <summary>Adds a plain r = 50 ring of the given side count (angles j * 2 pi / sides, columns vertical).</summary>
        static void AddPlainRing(MeshBuilder mb, double y, int sides)
        {
            double R = ThreadConstants.CableRadius;
            float yf = (float)y;
            for (int j = 0; j < sides; j++)
            {
                double th = j * (TwoPi / sides);
                float st = (float)Math.Sin(th), ct = (float)Math.Cos(th);
                mb.Add(new Vector3((float)(R * Math.Sin(th)), yf, (float)(R * Math.Cos(th))), new Vector3(st, 0f, ct),
                       new Vector2((float)j / sides, yf), VertexColour, new Vector4(st, 0f, ct, 1f), Vector4.zero);
            }
        }

        /// <summary>Copies the 432 distinct positions of a LOD0 boundary ring (relief 0 there) in column order.</summary>
        static void AddBoundaryCopy(MeshBuilder mb, CableRing ring)
        {
            for (int c = 0; c < Lod0Cols; c++)
            {
                int v = (c / RidgeVertices) * (RidgeVertices + 1) + (c % RidgeVertices);
                Vector3 s = ring.Smooth[v];
                mb.Add(ring.Position[v], ring.Normal[v], new Vector2((float)(c % RidgeVertices) / RidgeVertices, ring.Y), VertexColour,
                       new Vector4(s.x, s.y, s.z, 1f), new Vector4(ring.Relief[v], 0f, 0f, 0f));
            }
        }

        /// <summary>Column offset that aligns LOD0 column c of a ring at height y with angle 0 (the lay twist is a whole number of columns at both band ends).</summary>
        public static int Lod0AlignOffset(double y)
        {
            double twist = Wrap2Pi(TwoPi * y / ThreadConstants.CablePitch);
            int o = -(int)Math.Round(twist / (TwoPi / Lod0Cols));
            return ((o % Lod0Cols) + Lod0Cols) % Lod0Cols;
        }

        static void PlainStrip(MeshBuilder mb, int bA, int bB, int sides)
        {
            for (int j = 0; j < sides; j++)
            {
                int jn = (j + 1) % sides;
                TriOut(mb, bA + j, bA + jn, bB + j);
                TriOut(mb, bA + jn, bB + jn, bB + j);
            }
        }

        /// <summary>
        /// Watertight m:1 fan between an aligned fine ring (nf = m * nc vertices; fine vertex m * j + offset at the
        /// angle of coarse vertex j) and a coarse ring: m + 1 triangles per coarse segment, every edge shared.
        /// </summary>
        static void ZipperFan(MeshBuilder mb, int fineBase, int nf, int offset, int coarseBase, int nc)
        {
            int m = nf / nc, h = m / 2;
            for (int j = 0; j < nc; j++)
            {
                int jn = (j + 1) % nc;
                int f0 = m * j + offset;
                for (int q = 0; q < h; q++) TriOut(mb, Wrap(fineBase, f0 + q, nf), Wrap(fineBase, f0 + q + 1, nf), coarseBase + j);
                TriOut(mb, Wrap(fineBase, f0 + h, nf), coarseBase + jn, coarseBase + j);
                for (int q = h; q < m; q++) TriOut(mb, Wrap(fineBase, f0 + q, nf), Wrap(fineBase, f0 + q + 1, nf), coarseBase + jn);
            }
        }

        static int Wrap(int b, int i, int n) => b + ((i % n) + n) % n;

        static void BuildLod1(CableParts parts, CableRing boundary, bool upper)
        {
            var ys = Lod1RingYs(upper);                 // bottom to top
            int bands = ys.Count - 1;
            int chunks = (bands + Lod1BandsPerChunk - 1) / Lod1BandsPerChunk;
            int boundaryIndex = upper ? 0 : ys.Count - 1;
            int offset = Lod0AlignOffset(boundary.Y);
            for (int c = 0; c < chunks; c++)
            {
                int i0 = c * Lod1BandsPerChunk, i1 = Math.Min(bands, i0 + Lod1BandsPerChunk);
                var mb = new MeshBuilder((i1 - i0 + 1) * Lod1Sides + Lod0Cols);
                var ringBase = new int[i1 - i0 + 1];
                var ringSides = new int[i1 - i0 + 1];
                for (int i = i0; i <= i1; i++)
                {
                    ringBase[i - i0] = mb.VertexCount;
                    if (i == boundaryIndex) { AddBoundaryCopy(mb, boundary); ringSides[i - i0] = Lod0Cols; }
                    else { AddPlainRing(mb, ys[i], Lod1Sides); ringSides[i - i0] = Lod1Sides; }
                }
                for (int i = 0; i < i1 - i0; i++)
                {
                    int sa = ringSides[i], sb = ringSides[i + 1];
                    if (sa == sb) PlainStrip(mb, ringBase[i], ringBase[i + 1], sa);
                    else if (sa > sb) ZipperFan(mb, ringBase[i], sa, offset, ringBase[i + 1], sb);
                    else ZipperFan(mb, ringBase[i + 1], sb, offset, ringBase[i], sa);
                }
                parts.Lod1.Add(new CablePart($"Cable_LOD1_{(upper ? "up" : "down")}_{c:00}_{ys[i0]:0}", mb));
            }
        }

        static MeshBuilder Lod2(bool upper)
        {
            var ys = Lod2RingYs(upper);                 // bottom to top
            int joinIndex = upper ? 0 : ys.Count - 1;   // the ring that copies the LOD1 end ring (108 sides)
            var mb = new MeshBuilder(ys.Count * Lod2Sides + Lod1Sides);
            var ringBase = new int[ys.Count];
            var ringSides = new int[ys.Count];
            for (int i = 0; i < ys.Count; i++)
            {
                ringBase[i] = mb.VertexCount;
                ringSides[i] = i == joinIndex ? Lod1Sides : Lod2Sides;
                AddPlainRing(mb, ys[i], ringSides[i]);
            }
            for (int i = 0; i + 1 < ys.Count; i++)
            {
                int sa = ringSides[i], sb = ringSides[i + 1];
                if (sa == sb) PlainStrip(mb, ringBase[i], ringBase[i + 1], sa);
                else if (sa > sb) ZipperFan(mb, ringBase[i], sa, 0, ringBase[i + 1], sb);
                else ZipperFan(mb, ringBase[i + 1], sb, 0, ringBase[i], sa);
            }
            return mb;
        }

        // ------------------------------------------------------------------ collars
        public static List<double> CollarHeights()
        {
            var l = new List<double>(12);
            int n = (int)Math.Round((ThreadConstants.CableLod0YMax - ThreadConstants.CableLod0YMin) / ThreadConstants.CollarSpacing);
            for (int m = 0; m <= n; m++) l.Add(ThreadConstants.CableLod0YMin + ThreadConstants.CollarSpacing * m);
            return l;
        }

        /// <summary>True when a bead lens surface reaches the collar (lens radius above the collar's inner radius anywhere
        /// over the tube height): the collar would pierce the lens or hide inside it (lead decision D-018).</summary>
        public static bool CollarHitsBead(double yc)
        {
            double t = ThreadConstants.CollarTube, inner = CollarMajorRadius - t;
            for (int b = 0; b < ThreadConstants.BeadCount; b++)
            {
                double yb = ThreadConstants.BeadY0 + ThreadConstants.BeadYStep * b;
                for (int k = 0; k <= 16; k++)
                {
                    double u = (yc - t + 2.0 * t * k / 16.0 - yb) / ThreadConstants.BeadHalfHeight;
                    if (Math.Abs(u) >= 1.0) continue;
                    if (ThreadConstants.BeadRadius * Math.Sqrt(1.0 - u * u) > inner - CollarBeadMargin) return true;
                }
            }
            return false;
        }

        /// <summary>True when the whole collar tube lies inside a bead lens (bead rule of 14.5.6: r = 62 sqrt(1 - ((y - y_b) / 12)^2)).</summary>
        public static bool CollarInsideBead(double yc)
        {
            double t = ThreadConstants.CollarTube;
            for (int b = 0; b < ThreadConstants.BeadCount; b++)
            {
                double yb = ThreadConstants.BeadY0 + ThreadConstants.BeadYStep * b;
                bool inside = true;
                for (int k = 0; k <= 8 && inside; k++)
                {
                    double dy = -t + 2.0 * t * k / 8.0;
                    double u = (yc + dy - yb) / ThreadConstants.BeadHalfHeight;
                    if (Math.Abs(u) >= 1.0) { inside = false; break; }
                    double lens = ThreadConstants.BeadRadius * Math.Sqrt(1.0 - u * u);
                    double torus = CollarMajorRadius + Math.Sqrt(Math.Max(0.0, t * t - dy * dy));
                    if (torus > lens - CollarBeadMargin) inside = false;
                }
                if (inside) return true;
            }
            return false;
        }

        /// <summary>
        /// Smallest metres-per-pixel at which the collar at height yc is ever seen from the walked corridor
        /// (eye height, both corridor edges, in-game FOV(s) at the 1080p reference), from the near-cable
        /// activation to the goal.
        /// </summary>
        public static double CollarMinMetresPerPixel(double yc)
        {
            double best = double.MaxValue;
            double Rc = CollarMajorRadius, t = ThreadConstants.CollarTube;
            for (double s = ThreadConstants.NearCableOnS; s <= ThreadConstants.PathLength; s += 1.0)
            {
                double hw = ThreadMath.HalfWidth(s);
                double f = 0.5 * ReferenceHeightPx / Math.Tan(0.5 * ThreadMath.Fov(s) * ThreadMath.Deg);
                for (int e = -1; e <= 1; e++)
                {
                    D3 p = ThreadMath.CorridorToWorld(s, e * hw, ThreadConstants.EyeHeight);
                    double dx = p.x - ThreadConstants.CableAxisX, dz = p.z - ThreadConstants.CableAxisZ;
                    double rxz = Math.Sqrt(dx * dx + dz * dz);
                    double dr = rxz - Rc, dyy = p.y - yc;
                    double d = Math.Max(0.5, Math.Sqrt(dr * dr + dyy * dyy) - t);
                    best = Math.Min(best, d / f);
                }
            }
            return best;
        }

        public static void CollarSegments(double yc, out int major, out int minor)
        {
            if (CollarInsideBead(yc)) { major = CollarMajorMin; minor = CollarMinorMin; return; }
            double tol = CollarTolerancePx * CollarMinMetresPerPixel(yc);     // allowed silhouette facet depth (m)
            double t = ThreadConstants.CollarTube, outer = CollarMajorRadius + t;
            minor = RoundUp(SegmentsFor(tol, t), 4, CollarMinorMin, CollarMinorMax);
            major = RoundUp(SegmentsFor(tol, outer), 8, CollarMajorMin, CollarMajorMax);
        }

        /// <summary>Polygon sides so that the sagitta r (1 - cos(pi / n)) stays below tol.</summary>
        static double SegmentsFor(double tol, double r)
        {
            double c = 1.0 - Math.Min(tol / r, 1.0);
            double half = Math.Acos(Math.Max(-1.0, Math.Min(1.0, c)));
            return half > 1e-9 ? Math.PI / half : 1e9;
        }

        static int RoundUp(double n, int multiple, int min, int max)
        {
            int v = (int)Math.Ceiling(n / multiple - 1e-9) * multiple;
            return Math.Max(min, Math.Min(max, v));
        }

        static void BuildCollars(CableParts parts)
        {
            var hs = CollarHeights();
            for (int i = 0; i < hs.Count; i++)
            {
                if (CollarHitsBead(hs[i])) continue;   // lead decision D-018: collars inside or through a bead lens are omitted
                CollarSegments(hs[i], out int major, out int minor);
                parts.Collars.Add(new CablePart($"Cable_Collar_{hs[i]:0}", Collar(hs[i], major, minor)));
                parts.CollarY.Add(hs[i]);
                parts.CollarMajor.Add(major);
                parts.CollarMinor.Add(minor);
            }
        }

        /// <summary>Index-wrapped torus around the cable axis (local frame), tube angle 0 = outer equator.</summary>
        public static MeshBuilder Collar(double yc, int major, int minor)
        {
            double Rc = CollarMajorRadius, t = ThreadConstants.CollarTube;
            var mb = new MeshBuilder(major * minor);
            for (int i = 0; i < major; i++)
            {
                double a = i * (TwoPi / major);
                double sa = Math.Sin(a), ca = Math.Cos(a);
                for (int j = 0; j < minor; j++)
                {
                    double b = j * (TwoPi / minor);
                    double cb = Math.Cos(b), sb = Math.Sin(b);
                    double bb = Math.Abs(b > Math.PI ? b - TwoPi : b);
                    double ao = ThreadMath.Lerp(1.0, CollarAoMin, ThreadMath.Smoothstep(CollarAoB0, CollarAoB1, bb));
                    var nrm = new Vector3((float)(cb * sa), (float)sb, (float)(cb * ca));
                    double r = Rc + t * cb;
                    var p = new Vector3((float)(r * sa), (float)(yc + t * sb), (float)(r * ca));
                    mb.Add(p, nrm, new Vector2((float)(a * Rc), (float)(b * t)), new Color((float)ao, 1f, 0f, 1f),
                           new Vector4(nrm.x, nrm.y, nrm.z, 1f), Vector4.zero);
                }
            }
            for (int i = 0; i < major; i++)
            {
                int i1 = (i + 1) % major;
                for (int j = 0; j < minor; j++)
                {
                    int j1 = (j + 1) % minor;
                    int v00 = i * minor + j, v10 = i1 * minor + j, v11 = i1 * minor + j1, v01 = i * minor + j1;
                    TriV(mb, v00, v10, v11);
                    TriV(mb, v00, v11, v01);
                }
            }
            return mb;
        }

        // ------------------------------------------------------------------ far ribbons (ThreadLine _LINE_RIBBON)
        /// <summary>
        /// Centre-line ribbon on the axis: per point two vertices at the same position with uv1.y = side (-1 / +1),
        /// normal = line direction (+Y), uv0 = (side, y). 16 geometric segments from 19.6 km to 200 km (up) or the
        /// mirror (down). The triangles are degenerate until the vertex shader expands them (use Cull Off).
        /// </summary>
        public static MeshBuilder Ribbon(bool upper)
        {
            double sign = upper ? 1.0 : -1.0;
            double y0 = RibbonStart - RibbonOverlap, y1 = ThreadConstants.CableRibbonReach;
            var mb = new MeshBuilder(2 * (RibbonSegments + 1));
            Vector3 dir = Vector3.up;
            for (int k = 0; k <= RibbonSegments; k++)
            {
                double y = sign * (k == RibbonSegments ? y1 : y0 * Math.Pow(y1 / y0, (double)k / RibbonSegments));
                for (int side = -1; side <= 1; side += 2)
                    mb.Add(new Vector3(0f, (float)y, 0f), dir, new Vector2(side, (float)y), VertexColour,
                           new Vector4(0f, 1f, 0f, 1f), new Vector4(0f, side, 0f, 0f));
            }
            for (int k = 0; k < RibbonSegments; k++)
            {
                int a = 2 * k, b = 2 * k + 1, c = 2 * k + 3, d = 2 * k + 2;   // a, d: side -1; b, c: side +1
                mb.Tri(a, b, c);
                mb.Tri(a, c, d);
            }
            return mb;
        }

        /// <summary>Explicit local bounds of a ribbon: its centre line plus room for the screen-space expansion.</summary>
        public static Bounds RibbonBounds(bool upper)
        {
            double sign = upper ? 1.0 : -1.0;
            double y0 = RibbonStart - RibbonOverlap, y1 = ThreadConstants.CableRibbonReach, hw = RibbonBoundsHalfWidth;
            return new Bounds(new Vector3(0f, (float)(sign * 0.5 * (y0 + y1)), 0f),
                              new Vector3((float)(2 * hw), (float)(y1 - y0 + 2 * hw), (float)(2 * hw)));
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Triangle oriented to face away from the cable axis (local frame, axis = +Y through the origin).</summary>
        static void TriOut(MeshBuilder mb, int a, int b, int c)
        {
            Vector3 pa = mb.Positions[a], pb = mb.Positions[b], pc = mb.Positions[c];
            Vector3 fn = Vector3.Cross(pb - pa, pc - pa);
            float rx = pa.x + pb.x + pc.x, rz = pa.z + pb.z + pc.z;
            if (fn.x * rx + fn.z * rz >= 0f) mb.Tri(a, b, c); else mb.Tri(a, c, b);
        }

        /// <summary>Triangle oriented along the sum of its vertex normals.</summary>
        static void TriV(MeshBuilder mb, int a, int b, int c)
        {
            Vector3 fn = Vector3.Cross(mb.Positions[b] - mb.Positions[a], mb.Positions[c] - mb.Positions[a]);
            Vector3 n = mb.Normals[a] + mb.Normals[b] + mb.Normals[c];
            if (Vector3.Dot(fn, n) >= 0f) mb.Tri(a, b, c); else mb.Tri(a, c, b);
        }

        public static double Wrap2Pi(double a)
        {
            a -= TwoPi * Math.Floor(a / TwoPi);
            return a >= TwoPi ? a - TwoPi : a;
        }
    }
}
