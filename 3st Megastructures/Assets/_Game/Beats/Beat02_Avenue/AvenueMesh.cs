// AvenueMesh: small mesh helpers private to the beat 1-2 builders (MASTER_PROMPT 8.7, 10 items 7 and 29).
//  * Grid: a ring x column surface (lathes, lofts) with smooth runs separated by creases, poles closed by
//    triangle fans, cells that can be skipped (holes) and a duplicated seam column that is an exact copy of
//    column 0 (identical position, normal and colour), so lathe seams never show.
//  * Oriented triangles: every triangle is wound so that its face normal agrees with its vertex normals.
//  * ToMesh: several MeshBuilders become the submeshes of one Mesh (one material each).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    public sealed class Grid
    {
        public readonly int Rings, Cols;
        public readonly Vector3[] P;
        public readonly Vector3[] N;
        public readonly Color[] C;
        /// <summary>Smooth-run id per ring; consecutive rings with different ids form a crease (no strip between them).</summary>
        public readonly int[] Run;
        /// <summary>Pole ring (all columns at one point); its normal is PoleNormal.</summary>
        public readonly bool[] Pole;
        public readonly Vector3[] PoleNormal;
        /// <summary>Outward reference point per ring (normals are oriented away from it).</summary>
        public readonly Vector3[] Axis;
        /// <summary>Texture v coordinate per ring (metres along the profile).</summary>
        public readonly float[] V;

        public Grid(int rings, int cols)
        {
            Rings = rings; Cols = cols;
            P = new Vector3[rings * cols];
            N = new Vector3[rings * cols];
            C = new Color[rings * cols];
            Run = new int[rings];
            Pole = new bool[rings];
            PoleNormal = new Vector3[rings];
            Axis = new Vector3[rings];
            V = new float[rings];
        }

        public int Id(int i, int j) => i * Cols + ((j % Cols) + Cols) % Cols;

        /// <summary>Central differences on the final grid, one-sided at run ends, wrapped around the columns.</summary>
        public void ComputeNormals()
        {
            for (int i = 0; i < Rings; i++)
            {
                int ia = (i > 0 && Run[i - 1] == Run[i]) ? i - 1 : i;
                int ib = (i < Rings - 1 && Run[i + 1] == Run[i]) ? i + 1 : i;
                for (int j = 0; j < Cols; j++)
                {
                    int id = Id(i, j);
                    if (Pole[i]) { N[id] = PoleNormal[i].normalized; continue; }
                    Vector3 dTh = P[Id(i, j + 1)] - P[Id(i, j - 1)];
                    Vector3 dT = P[Id(ib, j)] - P[Id(ia, j)];
                    Vector3 n = Vector3.Cross(dTh, dT);
                    Vector3 outward = P[id] - Axis[i];
                    if (n.sqrMagnitude < 1e-14f) n = outward;
                    n.Normalize();
                    if (Vector3.Dot(n, outward) < 0f) n = -n;
                    N[id] = n;
                }
            }
        }

        /// <summary>
        /// Emits the grid. Each ring gets Cols + 1 vertices (the last one is an exact copy of column 0 with u = 1).
        /// skip(i, j) removes the cell between rings i, i + 1 and columns j, j + 1. Returns the vertex index of
        /// (ring, column) in mb; seam pairs (column 0, copy) are appended to seams when given.
        /// </summary>
        public int[] Emit(MeshBuilder mb, Func<int, int, bool> skip = null, List<Vector2Int> seams = null)
        {
            var index = new int[Rings * (Cols + 1)];
            for (int i = 0; i < Rings; i++)
            {
                for (int j = 0; j <= Cols; j++)
                {
                    int id = Id(i, j);
                    index[i * (Cols + 1) + j] = mb.Add(P[id], N[id], new Vector2((float)j / Cols, V[i]), C[id]);
                }
                if (seams != null) seams.Add(new Vector2Int(index[i * (Cols + 1)], index[i * (Cols + 1) + Cols]));
            }
            for (int i = 0; i + 1 < Rings; i++)
            {
                if (Run[i] != Run[i + 1]) continue;
                for (int j = 0; j < Cols; j++)
                {
                    if (skip != null && skip(i, j)) continue;
                    int a = index[i * (Cols + 1) + j], d = index[i * (Cols + 1) + j + 1];
                    int b = index[(i + 1) * (Cols + 1) + j], c = index[(i + 1) * (Cols + 1) + j + 1];
                    if (Pole[i + 1]) AvenueMesh.Tri(mb, a, d, b);
                    else if (Pole[i]) AvenueMesh.Tri(mb, a, b, c);
                    else { AvenueMesh.Tri(mb, a, b, c); AvenueMesh.Tri(mb, a, c, d); }
                }
            }
            return index;
        }

        public int VertexIndex(int[] index, int i, int j) => index[i * (Cols + 1) + j];
    }

    public static class AvenueMesh
    {
        /// <summary>Adds a triangle wound so that cross(b - a, c - a) agrees with the summed vertex normals; skips degenerate ones.</summary>
        public static bool Tri(MeshBuilder mb, int a, int b, int c)
        {
            Vector3 pa = mb.Positions[a], pb = mb.Positions[b], pc = mb.Positions[c];
            Vector3 fn = Vector3.Cross(pb - pa, pc - pa);
            if (fn.sqrMagnitude < 4e-12f) return false;   // area below 1e-6 m2
            Vector3 vn = mb.Normals[a] + mb.Normals[b] + mb.Normals[c];
            if (Vector3.Dot(fn, vn) >= 0f) mb.Tri(a, b, c); else mb.Tri(a, c, b);
            return true;
        }

        /// <summary>Quad a-b-c-d (any consistent order around the boundary) as two oriented triangles.</summary>
        public static void Quad(MeshBuilder mb, int a, int b, int c, int d)
        {
            Tri(mb, a, b, c);
            Tri(mb, a, c, d);
        }

        /// <summary>Flat quad with its own four vertices and one normal (alcove walls, cuff parts).</summary>
        public static void FlatQuad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color col)
        {
            n.Normalize();
            int ia = mb.Add(a, n, new Vector2(0, 0), col), ib = mb.Add(b, n, new Vector2(0, 1), col);
            int ic = mb.Add(c, n, new Vector2(1, 1), col), id = mb.Add(d, n, new Vector2(1, 0), col);
            Quad(mb, ia, ib, ic, id);
        }

        /// <summary>Combines builders into one mesh, one submesh per builder (empty builders keep an empty submesh).</summary>
        public static Mesh ToMesh(string name, float boundsPadding, params MeshBuilder[] parts)
        {
            int vtotal = 0;
            for (int i = 0; i < parts.Length; i++) vtotal += parts[i].VertexCount;
            var pos = new List<Vector3>(vtotal); var nrm = new List<Vector3>(vtotal); var tan = new List<Vector4>(vtotal);
            var col = new List<Color>(vtotal); var uv0 = new List<Vector2>(vtotal); var uv1 = new List<Vector4>(vtotal);
            var subs = new List<int>[parts.Length];
            for (int p = 0; p < parts.Length; p++)
            {
                var mb = parts[p];
                int b = pos.Count;
                pos.AddRange(mb.Positions); nrm.AddRange(mb.Normals); tan.AddRange(mb.Tangents);
                col.AddRange(mb.Colors); uv0.AddRange(mb.UV0); uv1.AddRange(mb.UV1);
                var idx = new List<int>(mb.Indices.Count);
                for (int k = 0; k < mb.Indices.Count; k++) idx.Add(mb.Indices[k] + b);
                subs[p] = idx;
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vtotal > 65534 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetTangents(tan);
            mesh.SetColors(col);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.subMeshCount = parts.Length;
            for (int p = 0; p < parts.Length; p++) mesh.SetIndices(subs[p], MeshTopology.Triangles, p, false);
            mesh.RecalculateBounds();
            if (boundsPadding > 0f)
            {
                var bb = mesh.bounds;
                bb.Expand(boundsPadding * 2f);
                mesh.bounds = bb;
            }
            mesh.UploadMeshData(false);
            return mesh;
        }

        public static Vector3 V(D3 d) => new Vector3((float)d.x, (float)d.y, (float)d.z);

        /// <summary>
        /// Rotation whose local +Z is forward and local +Y is as close to up as possible (Quaternion.LookRotation,
        /// written in managed code so the generators run without the engine, e.g. in offline checks).
        /// </summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 f = forward.normalized;
            Vector3 r = Vector3.Cross(up, f).normalized;
            Vector3 u = Vector3.Cross(f, r);
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float tr = m00 + m11 + m22;
            float x, y, z, w;
            if (tr > 0f)
            {
                float s = Mathf.Sqrt(tr + 1f) * 2f;
                w = 0.25f * s; x = (m21 - m12) / s; y = (m02 - m20) / s; z = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                w = (m21 - m12) / s; x = 0.25f * s; y = (m01 + m10) / s; z = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                w = (m02 - m20) / s; x = (m01 + m10) / s; y = 0.25f * s; z = (m12 + m21) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                w = (m10 - m01) / s; x = (m02 + m20) / s; y = (m12 + m21) / s; z = 0.25f * s;
            }
            return new Quaternion(x, y, z, w);
        }

        /// <summary>Validates a mesh with MeshKit.Validate and logs a [THREAD-FAIL] error when it is invalid.</summary>
        public static bool ValidateOrLog(Mesh mesh, WorldContext ctx)
        {
            bool ok = MeshKit.Validate(mesh, out string report);
            if (!ok) Debug.LogError("[THREAD-FAIL] mesh validation: " + report);
            return ok;
        }
    }
}
