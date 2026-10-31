using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>Result of <see cref="MeshValidation.Validate(Mesh)"/> (Part 8.7, 14.7.5, 14.10 mesh tests).</summary>
    public struct MeshReport
    {
        public string Name;
        public bool Readable;
        public int VertexCount;
        public int TriangleCount;
        public int SubMeshes;

        /// <summary>Triangles with area below <see cref="MeshValidation.DegenerateArea"/> (1e-10 m^2).</summary>
        public int DegenerateTriangles;
        public int NonFinitePositions;
        public int NonFiniteNormals;
        /// <summary>Normals whose length differs from 1 by more than 1 % (includes zero normals). Counted only when the mesh has normals.</summary>
        public int NonUnitNormals;
        public int IndicesOutOfRange;
        /// <summary>Index buffers whose length is not a multiple of 3, or non-triangle topology.</summary>
        public int MalformedSubMeshes;
        /// <summary>Non-degenerate triangles whose winding normal points away from the mean of their vertex normals (dot &lt; 0).</summary>
        public int WindingMismatches;
        public float WindingMismatchRatio;
        public bool HasNormals;
        /// <summary>UInt32 indices exactly when the mesh has more than 65 535 vertices.</summary>
        public bool IndexFormatOk;
        /// <summary>mesh.bounds contains every vertex (tolerance 1e-4 m + 1e-6 of the extent).</summary>
        public bool BoundsOk;

        public bool Ok =>
            Readable && VertexCount > 0 && TriangleCount > 0 && DegenerateTriangles == 0 && NonFinitePositions == 0 &&
            NonFiniteNormals == 0 && NonUnitNormals == 0 && IndicesOutOfRange == 0 && MalformedSubMeshes == 0 &&
            WindingMismatches == 0 && HasNormals && IndexFormatOk && BoundsOk;

        public string Describe()
        {
            if (!Readable) return (Name ?? "mesh") + ": FAIL (not readable on the CPU)";
            return string.Format(
                "{0}: {1} ({2} verts, {3} tris, {4} submeshes; degenerate {5}, non-finite pos {6} / nrm {7}, non-unit normals {8}, " +
                "indices out of range {9}, malformed submeshes {10}, winding mismatches {11} ({12:P2}), normals {13}, index format {14}, bounds {15})",
                Name ?? "mesh", Ok ? "OK" : "FAIL", VertexCount, TriangleCount, SubMeshes, DegenerateTriangles, NonFinitePositions,
                NonFiniteNormals, NonUnitNormals, IndicesOutOfRange, MalformedSubMeshes, WindingMismatches, WindingMismatchRatio,
                HasNormals ? "yes" : "MISSING", IndexFormatOk ? "ok" : "WRONG", BoundsOk ? "ok" : "WRONG");
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Automated mesh checks used by EditMode tests and the QA harness (BeatBase.CollectMeshes): degenerate triangles, NaN/Inf
    /// positions and normals, out-of-range indices, normals inconsistent with the winding (MeshBuilder convention: front face where
    /// cross(b - a, c - a) points), 32-bit index format above 65 535 vertices, bounds containing all vertices.
    /// Load/QA-time only (reuses static lists; main thread).
    /// </summary>
    public static class MeshValidation
    {
        public const float DegenerateArea = 1e-10f;

        static readonly List<Vector3> s_pos = new List<Vector3>(4096);
        static readonly List<Vector3> s_nrm = new List<Vector3>(4096);
        static readonly List<int> s_idx = new List<int>(8192);

        /// <summary>Validate a Mesh (must be CPU-readable: validate before UploadMeshData(true)).</summary>
        public static MeshReport Validate(Mesh mesh)
        {
            var r = new MeshReport { Name = mesh != null ? mesh.name : "null" };
            if (mesh == null) return r;
            r.Readable = mesh.isReadable;
            if (!r.Readable) return r;

            mesh.GetVertices(s_pos);
            s_nrm.Clear();
            r.HasNormals = mesh.HasVertexAttribute(VertexAttribute.Normal);
            if (r.HasNormals) mesh.GetNormals(s_nrm);
            r.VertexCount = s_pos.Count;
            r.SubMeshes = mesh.subMeshCount;
            r.IndexFormatOk = (r.VertexCount > 65535) == (mesh.indexFormat == IndexFormat.UInt32);

            AnalyseVertices(ref r);
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                if (mesh.GetTopology(sm) != MeshTopology.Triangles) { r.MalformedSubMeshes++; continue; }
                mesh.GetIndices(s_idx, sm, true);
                AnalyseTriangles(s_idx, ref r);
            }
            r.WindingMismatchRatio = r.TriangleCount > 0 ? (float)r.WindingMismatches / r.TriangleCount : 0f;

            Bounds b = mesh.bounds;
            float tol = 1e-4f + 1e-6f * b.size.magnitude;
            b.Expand(2f * tol);
            r.BoundsOk = true;
            for (int i = 0; i < s_pos.Count; i++)
            {
                if (!b.Contains(s_pos[i])) { r.BoundsOk = false; break; }
            }
            return r;
        }

        /// <summary>Validate builder data directly (index format and bounds are produced correctly by ToMesh and reported ok).</summary>
        public static MeshReport Validate(MeshBuilder mb, string name = "MeshBuilder")
        {
            var r = new MeshReport { Name = name, Readable = true, SubMeshes = 1, IndexFormatOk = true, BoundsOk = true };
            if (mb == null) { r.Readable = false; return r; }
            s_pos.Clear(); s_pos.AddRange(mb.Positions);
            s_nrm.Clear(); s_nrm.AddRange(mb.Normals);
            r.HasNormals = mb.Normals.Count == mb.Positions.Count;
            r.VertexCount = s_pos.Count;
            AnalyseVertices(ref r);
            AnalyseTriangles(mb.Indices, ref r);
            r.WindingMismatchRatio = r.TriangleCount > 0 ? (float)r.WindingMismatches / r.TriangleCount : 0f;
            return r;
        }

        static void AnalyseVertices(ref MeshReport r)
        {
            for (int i = 0; i < s_pos.Count; i++)
            {
                Vector3 p = s_pos[i];
                if (!Finite(p)) r.NonFinitePositions++;
            }
            if (!r.HasNormals) return;
            for (int i = 0; i < s_nrm.Count; i++)
            {
                Vector3 n = s_nrm[i];
                if (!Finite(n)) { r.NonFiniteNormals++; continue; }
                if (Mathf.Abs(n.magnitude - 1f) > 0.01f) r.NonUnitNormals++;
            }
        }

        static void AnalyseTriangles(List<int> idx, ref MeshReport r)
        {
            if (idx.Count % 3 != 0) r.MalformedSubMeshes++;
            int vc = s_pos.Count;
            bool normals = r.HasNormals && s_nrm.Count == vc;
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                r.TriangleCount++;
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vc || b >= vc || c >= vc) { r.IndicesOutOfRange++; continue; }
                Vector3 pa = s_pos[a];
                Vector3 cr = Vector3.Cross(s_pos[b] - pa, s_pos[c] - pa);
                float area = 0.5f * cr.magnitude;
                if (!(area >= DegenerateArea)) { r.DegenerateTriangles++; continue; } // also catches NaN
                if (!normals) continue;
                Vector3 mean = s_nrm[a] + s_nrm[b] + s_nrm[c];
                if (Vector3.Dot(cr, mean) < 0f) r.WindingMismatches++;
            }
        }

        static bool Finite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        // ------------------------------------------------------------------------------------------ orientation helpers (tests)

        /// <summary>
        /// Signed volume by the divergence theorem, sum of dot(a, cross(b, c)) / 6. Positive for a closed mesh whose triangles wind
        /// outward (front faces outside); negative when inside-out; meaningless for open meshes.
        /// </summary>
        public static double SignedVolume(Mesh mesh)
        {
            mesh.GetVertices(s_pos);
            double v = 0;
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                mesh.GetIndices(s_idx, sm, true);
                v += SignedVolume(s_pos, s_idx);
            }
            return v;
        }

        public static double SignedVolume(List<Vector3> p, List<int> idx)
        {
            double v = 0;
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                Vector3 a = p[idx[t]], b = p[idx[t + 1]], c = p[idx[t + 2]];
                v += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return v / 6.0;
        }

        /// <summary>
        /// Mean over vertices of dot(normal, normalize(vertex - centroid)), centroid = mean vertex position. Positive when normals
        /// point outward on average (closed, roughly star-shaped meshes); negative for inward-facing meshes (tunnels, holes).
        /// </summary>
        public static float OutwardScore(Mesh mesh)
        {
            mesh.GetVertices(s_pos);
            mesh.GetNormals(s_nrm);
            return OutwardScore(s_pos, s_nrm);
        }

        public static float OutwardScore(List<Vector3> p, List<Vector3> n)
        {
            if (p.Count == 0 || n.Count != p.Count) return 0f;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < p.Count; i++) c += p[i];
            c /= p.Count;
            double s = 0;
            int used = 0;
            for (int i = 0; i < p.Count; i++)
            {
                Vector3 d = p[i] - c;
                float m = d.magnitude;
                if (m < 1e-9f) continue;
                s += Vector3.Dot(n[i], d / m);
                used++;
            }
            return used > 0 ? (float)(s / used) : 0f;
        }
    }
}
