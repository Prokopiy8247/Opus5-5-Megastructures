using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Accumulates vertices and triangles for one procedural mesh. All MeshKit generators write into a MeshBuilder.
    ///
    /// Conventions (Part 8.7, 14.7.5):
    /// - Winding: a triangle (a, b, c) is front-facing on the side where cross(b - a, c - a) points (Unity clockwise rule).
    ///   <see cref="Quad"/>(a, b, c, d) takes the four corners in clockwise order seen from the front.
    /// - Vertex colour: R = baked ambient occlusion (255 = unoccluded), G = edge wear (0 = none), B = free (generator-specific),
    ///   A = free. Set <see cref="Color"/> before adding vertices.
    /// - UV0: surface coordinates in metres where a recipe needs them (M_ConcreteCyl: x = arc length, y = height).
    /// - UV1 (optional): generator-specific data (ThinLine members: xyz = member axis direction, w = member radius).
    /// - <see cref="Matrix"/> transforms positions and normals as they are added (Push/Pop for nested parts).
    /// - Meshes above 65 535 vertices automatically use 32-bit indices. Bounds are computed exactly.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Positions = new List<Vector3>(1024);
        public readonly List<Vector3> Normals = new List<Vector3>(1024);
        public readonly List<Color32> Colors = new List<Color32>(1024);
        public readonly List<Vector2> UV0 = new List<Vector2>(1024);
        public readonly List<Vector4> UV1 = new List<Vector4>();
        public readonly List<int> Indices = new List<int>(4096);

        /// <summary>Colour written with each new vertex (R = AO, G = wear).</summary>
        public Color32 Color = new Color32(255, 0, 0, 255);

        /// <summary>When true each vertex also writes <see cref="CurrentUV1"/> into UV1 (padded with zeros for earlier vertices).</summary>
        public bool WriteUV1;
        public Vector4 CurrentUV1;

        /// <summary>Optional UV2 (TEXCOORD2): ThinLine members write a point on the member axis in object space, w = 1.</summary>
        public readonly List<Vector4> UV2 = new List<Vector4>();
        public bool WriteUV2;
        public Vector4 CurrentUV2;

        /// <summary>Transform applied to positions and normals on add.</summary>
        public Matrix4x4 Matrix = Matrix4x4.identity;
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public int VertexCount => Positions.Count;
        public int TriangleCount => Indices.Count / 3;

        public void Push(Matrix4x4 local) { _stack.Push(Matrix); Matrix = Matrix * local; }
        public void Push(Vector3 pos, Quaternion rot) => Push(Matrix4x4.TRS(pos, rot, Vector3.one));
        public void Pop() { Matrix = _stack.Pop(); }

        /// <summary>Add one vertex (position and normal in the current local frame). Returns its index.</summary>
        public int Vertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            Positions.Add(Matrix.MultiplyPoint3x4(p));
            Vector3 wn = Matrix.MultiplyVector(n);
            float len = wn.magnitude;
            Normals.Add(len > 1e-12f ? wn / len : Vector3.up);
            Colors.Add(Color);
            UV0.Add(uv);
            int index = Positions.Count - 1;
            // UV1/UV2 are stored as given (callers such as MeshKit already express them in mesh space); earlier vertices are padded.
            if (WriteUV1)
            {
                while (UV1.Count < index) UV1.Add(Vector4.zero);
                UV1.Add(CurrentUV1);
            }
            if (WriteUV2)
            {
                while (UV2.Count < index) UV2.Add(Vector4.zero);
                UV2.Add(CurrentUV2);
            }
            return index;
        }

        public int Vertex(Vector3 p, Vector3 n) => Vertex(p, n, Vector2.zero);

        public void Tri(int a, int b, int c) { Indices.Add(a); Indices.Add(b); Indices.Add(c); }

        /// <summary>Two triangles from four corners given clockwise seen from the front.</summary>
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        /// <summary>A flat quad with its own four vertices. Corners clockwise seen from the front; normal from the winding.</summary>
        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
            int i0 = Vertex(a, n, uvA), i1 = Vertex(b, n, uvB), i2 = Vertex(c, n, uvC), i3 = Vertex(d, n, uvD);
            Quad(i0, i1, i2, i3);
        }

        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            => FlatQuad(a, b, c, d, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

        /// <summary>Axis-aligned box (in the current local frame) with hard edges and 24 vertices; UV0 in metres per face.</summary>
        public void Box(Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 c = centre;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            // -Z face (seen from -Z: x right, y up) clockwise: bottom-left, top-left, top-right, bottom-right
            FlatQuad(p000, p010, p110, p100, new Vector2(0, 0), new Vector2(0, size.y), new Vector2(size.x, size.y), new Vector2(size.x, 0));
            // +Z face (seen from +Z: -x is right)
            FlatQuad(p101, p111, p011, p001, new Vector2(0, 0), new Vector2(0, size.y), new Vector2(size.x, size.y), new Vector2(size.x, 0));
            // -X face (seen from -X: +z is left... right is -z)
            FlatQuad(p001, p011, p010, p000, new Vector2(0, 0), new Vector2(0, size.y), new Vector2(size.z, size.y), new Vector2(size.z, 0));
            // +X face
            FlatQuad(p100, p110, p111, p101, new Vector2(0, 0), new Vector2(0, size.y), new Vector2(size.z, size.y), new Vector2(size.z, 0));
            // +Y face (seen from above: x right, z up on screen)
            FlatQuad(p010, p011, p111, p110, new Vector2(0, 0), new Vector2(0, size.z), new Vector2(size.x, size.z), new Vector2(size.x, 0));
            // -Y face (seen from below)
            FlatQuad(p001, p000, p100, p101, new Vector2(0, size.z), new Vector2(0, 0), new Vector2(size.x, 0), new Vector2(size.x, size.z));
        }

        /// <summary>Append another builder's geometry transformed by m (applied on top of the current Matrix).</summary>
        public void Append(MeshBuilder other, Matrix4x4 m)
        {
            Matrix4x4 full = Matrix * m;
            int baseIndex = Positions.Count;
            for (int i = 0; i < other.Positions.Count; i++)
            {
                Positions.Add(full.MultiplyPoint3x4(other.Positions[i]));
                Vector3 n = full.MultiplyVector(other.Normals[i]);
                Normals.Add(n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up);
                Colors.Add(other.Colors[i]);
                UV0.Add(other.UV0[i]);
                if (WriteUV1 || other.UV1.Count > 0)
                {
                    while (UV1.Count < Positions.Count - 1) UV1.Add(Vector4.zero);
                    Vector4 u = other.UV1.Count > i ? other.UV1[i] : Vector4.zero;
                    Vector3 ax = full.MultiplyVector(new Vector3(u.x, u.y, u.z));
                    float al = ax.magnitude;
                    UV1.Add(al > 1e-12f ? new Vector4(ax.x / al, ax.y / al, ax.z / al, u.w * al) : u);
                }
                if (WriteUV2 || other.UV2.Count > 0)
                {
                    while (UV2.Count < Positions.Count - 1) UV2.Add(Vector4.zero);
                    Vector4 u = other.UV2.Count > i ? other.UV2[i] : Vector4.zero;
                    Vector3 ap = full.MultiplyPoint3x4(new Vector3(u.x, u.y, u.z));
                    UV2.Add(u.w != 0f ? new Vector4(ap.x, ap.y, ap.z, u.w) : u);
                }
            }
            for (int i = 0; i < other.Indices.Count; i++) Indices.Add(other.Indices[i] + baseIndex);
        }

        public void Clear()
        {
            Positions.Clear(); Normals.Clear(); Colors.Clear(); UV0.Clear(); UV1.Clear(); UV2.Clear(); Indices.Clear();
            Matrix = Matrix4x4.identity; _stack.Clear();
        }

        /// <summary>Build the Mesh. Uses 32-bit indices above 65 535 vertices, exact bounds, optional tangents.</summary>
        public Mesh ToMesh(string name, bool tangents = false, bool markNoLongerReadable = false)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = Positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Positions);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, UV0);
            if (UV1.Count > 0)
            {
                while (UV1.Count < Positions.Count) UV1.Add(Vector4.zero);
                mesh.SetUVs(1, UV1);
            }
            if (UV2.Count > 0)
            {
                while (UV2.Count < Positions.Count) UV2.Add(Vector4.zero);
                mesh.SetUVs(2, UV2);
            }
            mesh.SetIndices(Indices, MeshTopology.Triangles, 0, false);
            mesh.RecalculateBounds();
            if (tangents) mesh.RecalculateTangents();
            if (markNoLongerReadable) mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
