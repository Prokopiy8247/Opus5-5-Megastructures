using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Procedural ark (MASTER_PROMPT 14.5.3) in ark object space (origin = hull centre, +X bow, +Y up, the player
    /// sees the -Z flank and the belly). Four meshes: hull (hull loft, stern cap, keel, spars, nozzle outer bells,
    /// material RingMat.ArkHull; keel and spars carry vertex G = 1, the structure tone), engine interiors
    /// (Beat34Mat.ArkEngine), the window band (RingMat.ArkWindow: one cell quad per window with its id in uv1, apertures
    /// analytic in the shader) and the glow billboards (RingMat.Glow: 7 engine glows, the spar and keel marker lights).
    /// About 23 k triangles in total (budget 40 k).
    /// </summary>
    public static class ArkMeshes
    {
        static readonly Color Plain = new Color(1f, 0f, 0f, 1f);
        static readonly Color Struct = new Color(1f, 1f, 0f, 1f);   // vertex G = 1: structure tone (keel, spars) in RingArk

        // ------------------------------------------------------------------ hull
        /// <summary>Hull loft (160 stations: 159 rings and the bow apex), superellipse n 2.4, 48 radial vertices,
        /// stern cap, keel, 12 spars and the outer bells of the 7 nozzles.</summary>
        public static MeshBuilder Hull(MeshBuilder b)
        {
            float[] ts = ArkSpec.StationT();
            int nr = ts.Length, ns = ArkSpec.Radial;
            var P = new Vector3[nr, ns];
            for (int i = 0; i < nr; i++)
            {
                float s = ArkSpec.SectionScale(ts[i]);
                float x = ArkSpec.TToX(ts[i]);
                for (int k = 0; k < ns; k++)
                {
                    float phi = 0.5f * Mathf.PI + 2f * Mathf.PI * k / ns;     // k = 0 is the top centre line (uv seam)
                    Vector2 zy = ArkSpec.SuperPoint(phi, ArkSpec.HalfBeam * s, ArkSpec.HalfHeight * s);
                    P[i, k] = new Vector3(x, zy.y, zy.x);
                }
            }
            // normals from finite differences, oriented away from the hull axis
            var N = new Vector3[nr, ns];
            for (int i = 0; i < nr; i++)
                for (int k = 0; k < ns; k++)
                {
                    Vector3 dRing = P[i, (k + 1) % ns] - P[i, (k - 1 + ns) % ns];
                    Vector3 dLong = P[Mathf.Min(i + 1, nr - 1), k] - P[Mathf.Max(i - 1, 0), k];
                    Vector3 n = Vector3.Cross(dLong, dRing);
                    Vector3 radial = new Vector3(0f, P[i, k].y, P[i, k].z);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    N[i, k] = n.sqrMagnitude > 1e-12f ? n.normalized : radial.normalized;
                }
            // vertices (seam duplicated at k = ns for continuous perimeter uvs)
            var idx = new int[nr, ns + 1];
            var arcTot = new float[nr];
            for (int i = 0; i < nr; i++)
            {
                float arc = 0f;
                for (int k = 0; k <= ns; k++)
                {
                    int kk = k % ns;
                    if (k > 0) arc += (P[i, kk] - P[i, k - 1]).magnitude;
                    idx[i, k] = b.Vtx(P[i, kk], N[i, kk], new Vector2(P[i, kk].x, arc), Plain);
                }
                arcTot[i] = arc;
            }
            for (int i = 0; i < nr - 1; i++)
                for (int k = 0; k < ns; k++)
                {
                    int kk = k % ns, k1 = (k + 1) % ns;
                    Vector3 d = N[i, kk] + N[i, k1] + N[i + 1, kk] + N[i + 1, k1];
                    Beat34Geo.QuadO(b, idx[i, k], idx[i, k + 1], idx[i + 1, k + 1], idx[i + 1, k], d);
                }
            // bow apex: one apex vertex per triangle so the perimeter uv does not smear
            {
                int last = nr - 1;
                Vector3 tip = new Vector3(0.5f * ArkSpec.Length, 0f, 0f);
                for (int k = 0; k < ns; k++)
                {
                    int k1 = k + 1;
                    float arcMid = 0.5f * (b.UV[idx[last, k]].y + b.UV[idx[last, k1]].y);
                    int a = b.Vtx(tip, Vector3.right, new Vector2(tip.x, arcMid), Plain);
                    Vector3 d = N[last, k % ns] + N[last, k1 % ns] + Vector3.right;
                    Beat34Geo.TriO(b, a, idx[last, k], idx[last, k1], d);
                }
            }
            // stern cap (flat, facing -X)
            {
                Vector3 c = new Vector3(-0.5f * ArkSpec.Length, 0f, 0f);
                int ci = b.Vtx(c, Vector3.left, new Vector2(0f, 0f), Plain);
                var ring = new int[ns];
                for (int k = 0; k < ns; k++) ring[k] = b.Vtx(P[0, k], Vector3.left, new Vector2(P[0, k].z, P[0, k].y), Plain);
                for (int k = 0; k < ns; k++) Beat34Geo.TriO(b, ci, ring[k], ring[(k + 1) % ns], Vector3.left);
            }
            Keel(b, ts);
            Spars(b);
            EngineBells(b);
            return b;
        }

        static void Keel(MeshBuilder b, float[] ts)
        {
            // stations: hull station x positions inside the keel span, plus its ends
            var xs = new List<float> { ArkSpec.KeelX0 };
            for (int i = 0; i < ts.Length; i++)
            {
                float x = ArkSpec.TToX(ts[i]);
                if (x > ArkSpec.KeelX0 + 1f && x < ArkSpec.KeelX1 - 1f) xs.Add(x);
            }
            xs.Add(ArkSpec.KeelX1);
            int n = xs.Count;
            var tl = new Vector3[n]; var tr = new Vector3[n]; var br = new Vector3[n]; var bl = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float x = xs[i];
                float s = ArkSpec.SectionScale(ArkSpec.XToT(x));
                float yb = ArkSpec.BellyY(0f, s);
                float ramp = Mathf.Min(x - ArkSpec.KeelX0, ArkSpec.KeelX1 - x) / ArkSpec.KeelRamp;
                float depth = 2f + (ArkSpec.KeelDepth - 2f) * RingMath.Smootherstep01(ramp);
                float ht = 0.5f * ArkSpec.KeelTopW, hb = 0.5f * ArkSpec.KeelBottomW * Mathf.Lerp(1.6f, 1f, RingMath.Smootherstep01(ramp));
                float ytop = yb + ArkSpec.KeelEmbed;
                tl[i] = new Vector3(x, ytop, -ht); tr[i] = new Vector3(x, ytop, ht);
                bl[i] = new Vector3(x, yb - depth, -hb); br[i] = new Vector3(x, yb - depth, hb);
            }
            for (int i = 0; i < n - 1; i++)
            {
                Beat34Geo.FlatQuadP(b, tl[i], tl[i + 1], bl[i + 1], bl[i], new Vector3(0f, -0.3f, -1f), Struct, 0, 1);   // -Z side
                Beat34Geo.FlatQuadP(b, tr[i], tr[i + 1], br[i + 1], br[i], new Vector3(0f, -0.3f, 1f), Struct, 0, 1);    // +Z side
                Beat34Geo.FlatQuadP(b, bl[i], bl[i + 1], br[i + 1], br[i], Vector3.down, Struct, 0, 2);                    // bottom
            }
            Beat34Geo.FlatQuadP(b, tl[0], tr[0], br[0], bl[0], Vector3.left, Struct, 2, 1);
            Beat34Geo.FlatQuadP(b, tl[n - 1], tr[n - 1], br[n - 1], bl[n - 1], Vector3.right, Struct, 2, 1);
        }

        static void Spars(MeshBuilder b)
        {
            const int seg = 8;
            float h = 0.5f * ArkSpec.SparSize;
            for (int si = 0; si < ArkSpec.SparCount; si++)
            {
                float x = (si - 0.5f * (ArkSpec.SparCount - 1)) * ArkSpec.SparPitch;
                float s = ArkSpec.SectionScale(ArkSpec.XToT(x));
                var c = new Vector3[seg + 1]; var nn = new Vector3[seg + 1];
                for (int j = 0; j <= seg; j++)
                {
                    float z = -0.5f * ArkSpec.SparLen + ArkSpec.SparLen * j / seg;
                    float y = ArkSpec.BellyY(z, s);
                    c[j] = new Vector3(x, y, z);
                }
                for (int j = 0; j <= seg; j++)
                {
                    Vector3 t = c[Mathf.Min(j + 1, seg)] - c[Mathf.Max(j - 1, 0)];
                    Vector3 n = Vector3.Cross(t, Vector3.right).normalized;   // in the zy plane, perpendicular to the belly
                    if (n.y > 0f) n = -n;                                     // outward = downward
                    nn[j] = n;
                }
                Vector3 X = Vector3.right * h;
                for (int j = 0; j < seg; j++)
                {
                    Vector3 o0 = c[j] + nn[j] * h, o1 = c[j + 1] + nn[j + 1] * h;
                    Vector3 i0 = c[j] - nn[j] * h, i1 = c[j + 1] - nn[j + 1] * h;
                    Vector3 nOut = (nn[j] + nn[j + 1]).normalized;
                    Beat34Geo.FlatQuadP(b, o0 - X, o1 - X, o1 + X, o0 + X, nOut, Struct, 2, 0);              // outer face
                    Beat34Geo.FlatQuadP(b, i0 - X, i1 - X, o1 - X, o0 - X, Vector3.left, Struct, 2, 1);      // stern-side face
                    Beat34Geo.FlatQuadP(b, i0 + X, i1 + X, o1 + X, o0 + X, Vector3.right, Struct, 2, 1);     // bow-side face
                }
                for (int e = 0; e < 2; e++)
                {
                    int j = e == 0 ? 0 : seg;
                    Vector3 side = e == 0 ? Vector3.back : Vector3.forward;
                    Vector3 o = c[j] + nn[j] * h, ii = c[j] - nn[j] * h;
                    Beat34Geo.FlatQuadP(b, ii - X, ii + X, o + X, o - X, side, Struct, 0, 1);
                }
            }
        }

        static Matrix4x4 EngineFrame(int i)
        {
            Vector2 yz = ArkSpec.EngineYZ(i);
            // local +Y points backward (-X); local y = 0 is the stern face plane
            return Matrix4x4.TRS(new Vector3(-0.5f * ArkSpec.Length, yz.x, yz.y), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
        }

        static void EngineBells(MeshBuilder b)
        {
            var old = b.M; var oldC = b.Col;
            b.Col = Plain;
            var outer = new[] { new Vector2(ArkSpec.EngineThroatR, -1f), new Vector2(0.5f * (ArkSpec.EngineThroatR + ArkSpec.EngineR) - 0.5f, 0.5f * ArkSpec.EngineLen), new Vector2(ArkSpec.EngineR, ArkSpec.EngineLen) };
            var rim = new[] { new Vector2(ArkSpec.EngineR, ArkSpec.EngineLen), new Vector2(ArkSpec.EngineR - 1.5f, ArkSpec.EngineLen) };
            for (int i = 0; i < ArkSpec.EngineCount; i++)
            {
                b.M = EngineFrame(i);
                b.Lathe(Vector3.zero, outer, 32);
                b.Lathe(Vector3.zero, rim, 32);
            }
            b.M = old; b.Col = oldC;
        }

        // ------------------------------------------------------------------ engine interiors
        /// <summary>Inner bell surfaces and throat discs of the 7 nozzles (RingArk _Mode 2, EngineBlue HDR 6). Vertex
        /// colour B carries the depth profile: 1 at the throat, 0.35 at the exit (the shader brightens the throat).</summary>
        public static MeshBuilder EngineInner(MeshBuilder b)
        {
            var old = b.M; var oldC = b.Col;
            float ri = ArkSpec.EngineR - 1.5f;
            var inner = new[] { new Vector2(ri, ArkSpec.EngineLen), new Vector2(0.5f * (ri + ArkSpec.EngineThroatR - 2.5f), 0.5f * ArkSpec.EngineLen), new Vector2(ArkSpec.EngineThroatR - 2.5f, 1f) };
            const int seg = 32;
            for (int i = 0; i < ArkSpec.EngineCount; i++)
            {
                b.M = EngineFrame(i);
                int first = b.V.Count;
                b.Col = Plain;
                b.Lathe(Vector3.zero, inner, seg);
                // Lathe order: segment-major, profile-minor (closed sweep: seg columns of inner.Length vertices)
                for (int v = first; v < b.V.Count; v++)
                {
                    int k = (v - first) % inner.Length;
                    float depth = Mathf.Clamp01(inner[k].y / ArkSpec.EngineLen);
                    b.C[v] = new Color(1f, 0f, Mathf.Lerp(1f, 0.35f, depth), 1f);
                }
                b.Col = new Color(1f, 0f, 1f, 1f);
                b.Disc(new Vector3(0f, 0.6f, 0f), ArkSpec.EngineThroatR - 2.2f, seg, true);   // faces local +Y = backward
            }
            b.M = old; b.Col = oldC;
            return b;
        }

        // ------------------------------------------------------------------ window band
        /// <summary>
        /// The 6 window rows as one mesh of per-window cells on the -Z flank: window (k, j) owns the cell (two quads,
        /// lower and upper half) 18 m along X (centred on its window) by one deck pitch (14 m) along the flank, so the cells tile the band
        /// without gaps (neighbours share corner positions computed identically) and no sub-pixel geometry exists.
        /// uv0 = cell-local metres (x along the hull, flank arc from the row centre): the 12 x 5 m aperture is analytic
        /// and footprint-filtered in RingArk _Mode 1. uv1 = (window id j * 166 + k, deterministic level 0..1).
        /// The band floats WinStandoff outside the analytic hull surface (the hull facets lie inside it).
        /// Returns the number of window cells.
        /// </summary>
        public static int WindowCells(MeshBuilder b, int kMin, int kMax)
        {
            float hp = 0.5f * ArkSpec.WinPitch, hr = 0.5f * ArkSpec.RowPitch;
            int cells = 0;
            for (int j = 0; j < ArkSpec.WinRows; j++)
            {
                float yc = ArkSpec.RowY(j);
                float y0 = yc - hr, y1 = yc + hr;
                for (int k = kMin; k <= kMax; k++)
                {
                    float xc = ArkSpec.WindowX(k);
                    float x0 = xc - hp, x1 = xc + hp;
                    var uv1 = new Vector2(j * ArkSpec.WinPerRow + k, ArkSpec.WindowLevel(k, j));
                    // two quads along the flank (lower / upper half): the chords stay >= 0.5 m outside the hull facets
                    int i0 = CellVertex(b, x0, y0, xc, yc, uv1);
                    int i1 = CellVertex(b, x1, y0, xc, yc, uv1);
                    int i2 = CellVertex(b, x1, yc, xc, yc, uv1);
                    int i3 = CellVertex(b, x0, yc, xc, yc, uv1);
                    int i4 = CellVertex(b, x1, y1, xc, yc, uv1);
                    int i5 = CellVertex(b, x0, y1, xc, yc, uv1);
                    Beat34Geo.QuadO(b, i0, i1, i2, i3, b.N[i0] + b.N[i1] + b.N[i2] + b.N[i3]);
                    Beat34Geo.QuadO(b, i3, i2, i4, i5, b.N[i3] + b.N[i2] + b.N[i4] + b.N[i5]);
                    cells++;
                }
            }
            return cells;
        }

        static float FlankZ(float x, float y)
        {
            ArkSpec.FlankPoint(y, ArkSpec.SectionScale(ArkSpec.XToT(x)), out float z, out _);
            return z;
        }

        static int CellVertex(MeshBuilder b, float x, float y, float xc, float yc, Vector2 uv1)
        {
            float s = ArkSpec.SectionScale(ArkSpec.XToT(x));
            float z = FlankZ(x, y);
            // outward normal of z(x, y) on the -Z flank, including the taper slope along X
            const float d = 0.5f;
            float dzdx = (FlankZ(x + d, y) - FlankZ(x - d, y)) / (2f * d);
            float dzdy = (FlankZ(x, y + d) - FlankZ(x, y - d)) / (2f * d);
            Vector3 n = new Vector3(dzdx, dzdy, -1f).normalized;
            Vector3 p = new Vector3(x, y, z) + n * ArkSpec.WinStandoff;
            var uv = new Vector2(x - xc, ArkSpec.FlankArc(yc, y, s));
            int i = b.Vtx(p, n, uv, Plain);
            b.UV1[i] = uv1;
            return i;
        }

        // ------------------------------------------------------------------ glow
        /// <summary>Engine glows: one camera-facing Gaussian billboard per nozzle just behind its exit (RingMat.Glow,
        /// RingAdditive _Mode 1: minimum size with energy preservation, fog attenuation), EngineBlue, constant level.</summary>
        public static MeshBuilder Glow(MeshBuilder b)
        {
            Color tint = RingPalette.Lin("EngineBlue");
            float xe = -0.5f * ArkSpec.Length - ArkSpec.EngineLen - 0.5f * ArkSpec.GlowRadius;
            for (int i = 0; i < ArkSpec.EngineCount; i++)
            {
                Vector2 yz = ArkSpec.EngineYZ(i);
                Beat34Additive.Glow(b, new Vector3(xe, yz.x, yz.y), ArkSpec.GlowRadius, tint, ArkSpec.GlowIntensity, Beat34Additive.ChannelConstant);
            }
            MarkerLights(b);
            return b;
        }

        /// <summary>Marker lights (scale cue, 14.5.3 "spars"): one WindowWarm point under both ends of every spar and one
        /// under the keel at every spar inside the keel span, 2 m below the structure. Sub-pixel at KS04, so the Glow
        /// material draws them at its 2 px minimum with energy preservation (no sparkle); they mark the 240 m spar rhythm
        /// along the dark belly.</summary>
        static void MarkerLights(MeshBuilder b)
        {
            Color warm = RingPalette.Lin("WindowWarm");
            float zEnd = 0.5f * ArkSpec.SparLen - 3f;
            for (int si = 0; si < ArkSpec.SparCount; si++)
            {
                float x = (si - 0.5f * (ArkSpec.SparCount - 1)) * ArkSpec.SparPitch;
                float s = ArkSpec.SectionScale(ArkSpec.XToT(x));
                for (int e = -1; e <= 1; e += 2)
                {
                    float z = e * zEnd;
                    float y = ArkSpec.BellyY(z, s) - 0.5f * ArkSpec.SparSize - 2f - ArkSpec.MarkerRadius;
                    Beat34Additive.Glow(b, new Vector3(x, y, z), ArkSpec.MarkerRadius, warm, ArkSpec.MarkerIntensity, Beat34Additive.ChannelConstant);
                }
                if (x > ArkSpec.KeelX0 + ArkSpec.KeelRamp && x < ArkSpec.KeelX1 - ArkSpec.KeelRamp)
                {
                    float y = ArkSpec.BellyY(0f, s) - ArkSpec.KeelDepth - 2f - ArkSpec.MarkerRadius;
                    Beat34Additive.Glow(b, new Vector3(x, y, 0f), ArkSpec.MarkerRadius, warm, ArkSpec.MarkerIntensity, Beat34Additive.ChannelConstant);
                }
            }
        }
    }
}
