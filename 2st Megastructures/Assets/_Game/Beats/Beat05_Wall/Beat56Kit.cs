using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>Material ids owned by beats 5-6 that are not in <see cref="RingMat"/> (defined in Beat56MaterialDefs).</summary>
    public static class Beat56Ids
    {
        public const string WallPlinth = "WallPlinth";     // M06 variant, the plinth along the wall foot
        public const string Apron = "Apron";               // M01 variant, 4 x 4 m slabs, 8 mm joints (14.5.5)
        public const string Climber = "WallClimber";       // the beat-5 climber point (additive, shader-driven)
        public const string TunnelSpill = "TunnelSpill";   // mouth spill quads (additive, _Lamps.w)
        public const string PlainPool = "PlainPool";       // door / plain bollard light pools (additive, _Lamps.x / 3)
        // The door recess (always-on Tunnel set): wall ceramic on M08 RingTunnel, so the opening occlusion blocks every
        // directional light that would have to cross the 120 m tube (the beat-7 key L7 beyond the 90 m shadow distance).
        public const string DoorRecess = "DoorRecess";
        // The tunnel strips and markers: EmissiveAmber's emission with a black lens body, so the beat-7 key L7 (unshadowed
        // beyond 90 m) cannot light them along the tube.
        public const string TunnelAmber = "TunnelAmber";
    }

    /// <summary>Geometry helpers shared by the beat-5 and beat-6 builders (load time only).</summary>
    internal static class Beat56Kit
    {
        /// <summary>Quad a-b-c-d whose winding is chosen so the geometric normal agrees with <paramref name="want"/>.</summary>
        public static void Quad(MeshBuilder mb, int a, int b, int c, int d, Vector3 want)
        {
            Vector3 n = Vector3.Cross(mb.V[b] - mb.V[a], mb.V[c] - mb.V[a]);
            if (Vector3.Dot(n, want) >= 0f) mb.Quad(a, b, c, d);
            else mb.Quad(a, d, c, b);
        }

        public static void Tri(MeshBuilder mb, int a, int b, int c, Vector3 want)
        {
            Vector3 n = Vector3.Cross(mb.V[b] - mb.V[a], mb.V[c] - mb.V[a]);
            if (Vector3.Dot(n, want) >= 0f) mb.Tri(a, b, c);
            else mb.Tri(a, c, b);
        }

        /// <summary>
        /// Band between two chains with the same point count (chain ca at axial xa, chain cb at axial xb), one quad per
        /// chain segment, hard-edged (own vertices). normal(i) gives the face normal at chain point i (also the winding
        /// reference). colour = vertex colour (R AO, G edge wear); ao(p) (optional) overrides R per vertex.
        /// </summary>
        public static void Band(MeshBuilder mb, IList<Vector2> ca, float xa, IList<Vector2> cb, float xb,
            Func<int, Vector3> normal, Color colour, Func<Vector3, float> ao = null)
        {
            int n = Mathf.Min(ca.Count, cb.Count);
            if (n < 2) return;
            int baseA = mb.VertexCount;
            for (int i = 0; i < n; i++)
            {
                Vector3 nr = normal(i);
                Vector3 pa = TunnelProfile.World(ca[i], xa), pb = TunnelProfile.World(cb[i], xb);
                Color c0 = colour, c1 = colour;
                if (ao != null) { c0.r = ao(pa); c1.r = ao(pb); }
                mb.Vtx(pa, nr, new Vector2(i, 0f), c0);
                mb.Vtx(pb, nr, new Vector2(i, 1f), c1);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a0 = baseA + 2 * i, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                Vector3 want = mb.N[a0] + mb.N[a1];
                Quad(mb, a0, a1, b1, b0, want);
            }
        }

        /// <summary>Sets vertex colour R (AO) of vertices [from, to) from their positions.</summary>
        public static void SetAO(MeshBuilder mb, int from, int to, Func<Vector3, float> ao)
        {
            for (int i = from; i < to; i++)
            {
                Color c = mb.C[i];
                c.r = Mathf.Clamp01(ao(mb.V[i]));
                mb.C[i] = c;
            }
        }

        // ------------------------------------------------------------------ polygon triangulation
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriClosed(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            const float e = -1e-6f;
            return Cross(b - a, p - a) >= e && Cross(c - b, p - b) >= e && Cross(a - c, p - c) >= e;
        }

        static float Quality(Vector2 a, Vector2 b, Vector2 c)
        {
            float area = 0.5f * Mathf.Abs(Cross(b - a, c - a));
            float l = (b - a).sqrMagnitude + (c - b).sqrMagnitude + (a - c).sqrMagnitude;
            return l > 1e-12f ? 6.9282032f * area / l : 0f;   // 4 sqrt(3) A / sum(l^2): 1 for equilateral
        }

        /// <summary>
        /// Ear-clipping triangulation of a simple polygon (either orientation), choosing the best-shaped ear each step.
        /// Vertices lying on a candidate edge block that ear, so collinear boundary vertices (shared with neighbouring
        /// mesh cells) always end up as triangle corners: no T-junctions. Returns index triples, counter-clockwise.
        /// </summary>
        public static List<int> EarClip(IList<Vector2> poly, out bool degenerateFallback)
        {
            degenerateFallback = false;
            int n = poly.Count;
            var res = new List<int>(3 * Mathf.Max(n - 2, 0));
            if (n < 3) return res;
            double area2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                area2 += (double)p.x * q.y - (double)q.x * p.y;
            }
            var idx = new List<int>(n);
            if (area2 >= 0.0) for (int i = 0; i < n; i++) idx.Add(i);
            else for (int i = n - 1; i >= 0; i--) idx.Add(i);

            int guard = 0;
            while (idx.Count > 3 && guard++ < 4 * n)
            {
                int cnt = idx.Count, best = -1;
                float bestQ = -1f;
                for (int k = 0; k < cnt; k++)
                {
                    int ia = idx[(k + cnt - 1) % cnt], ib = idx[k], ic = idx[(k + 1) % cnt];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-7f) continue;   // reflex or collinear: not an ear
                    bool blocked = false;
                    for (int m = 0; m < cnt && !blocked; m++)
                    {
                        int im = idx[m];
                        if (im == ia || im == ib || im == ic) continue;
                        if (InTriClosed(poly[im], a, b, c)) blocked = true;
                    }
                    if (blocked) continue;
                    float q = Quality(a, b, c);
                    if (q > bestQ) { bestQ = q; best = k; }
                }
                if (best < 0)
                {
                    // no ear: drop a collinear vertex (never expected for the wall patch; reported by the caller)
                    int drop = -1;
                    for (int k = 0; k < cnt && drop < 0; k++)
                    {
                        Vector2 a = poly[idx[(k + cnt - 1) % cnt]], b = poly[idx[k]], c = poly[idx[(k + 1) % cnt]];
                        if (Mathf.Abs(Cross(b - a, c - b)) <= 1e-7f) drop = k;
                    }
                    if (drop < 0) break;
                    degenerateFallback = true;
                    idx.RemoveAt(drop);
                    continue;
                }
                res.Add(idx[(best + cnt - 1) % cnt]);
                res.Add(idx[best]);
                res.Add(idx[(best + 1) % cnt]);
                idx.RemoveAt(best);
            }
            if (idx.Count == 3)
            {
                Vector2 a = poly[idx[0]], b = poly[idx[1]], c = poly[idx[2]];
                if (Mathf.Abs(Cross(b - a, c - b)) > 1e-7f) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            }
            else if (idx.Count > 3) degenerateFallback = true;
            return res;
        }

        // ------------------------------------------------------------------ registry markers and checks
        /// <summary>
        /// A renderer-less GameObject whose transform carries a measured dimension for tests and composition checks:
        /// position = centre, localScale = size (m). Renderer bounds cannot carry exact sizes because AddMesh inflates
        /// them for the bend.
        /// </summary>
        public static GameObject Marker(BuildContext ctx, RingSet set, string key, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Marker_" + key);
            go.transform.SetParent(ctx.SetRoot(set), false);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(Mathf.Max(size.x, 0.01f), Mathf.Max(size.y, 0.01f), Mathf.Max(size.z, 0.01f));
            ctx.Register(key, go);
            return go;
        }

        /// <summary>Load-time mesh audit (Part 8.7): logs a warning when a generated mesh fails validation.</summary>
        public static void Check(Mesh m)
        {
            var r = MeshValidation.Validate(m);
            if (!r.ok)
                Debug.LogWarning("[Ring] mesh " + m.name + " failed validation: degenerate " + r.degenerate + ", bad index " + r.badIndex
                    + ", NaN " + r.nanVerts + ", zero normals " + r.zeroNormals);
        }

        public static int Triangles(Mesh m)
        {
            int t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (int)m.GetIndexCount(i) / 3;
            return t;
        }
    }
}
